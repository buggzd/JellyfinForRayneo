using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JellyfinForRayNeo
{
    public enum JellyfinBrowseFilter
    {
        All,
        Unplayed,
        Resumable,
        Favorite
    }

    public enum JellyfinBrowseSort
    {
        Name,
        DateCreated,
        CommunityRating
    }

    public sealed class JellyfinBrowseState
    {
        public const int DefaultPageSize = 30;

        public string Title;
        public string ParentId;
        public string CollectionType;
        public string SearchTerm;
        public string SearchInitial;
        public int StartIndex;
        public int PageSize = DefaultPageSize;
        public JellyfinBrowseFilter Filter;
        public JellyfinBrowseSort Sort;
        public bool Recursive;
        public bool IsSearch;
        public bool PreferLandscape;

        public JellyfinBrowseState Clone()
        {
            return (JellyfinBrowseState)MemberwiseClone();
        }

        public static JellyfinBrowseState ForLibrary(JellyfinItem library)
        {
            if (library == null || string.IsNullOrWhiteSpace(library.Id))
            {
                throw new ArgumentException("A Jellyfin library is required.", nameof(library));
            }

            string collectionType = (library.CollectionType ?? string.Empty).Trim();
            bool flatLibrary = IsFlatCollection(collectionType);
            return new JellyfinBrowseState
            {
                Title = string.IsNullOrWhiteSpace(library.Name) ? "媒体库" : library.Name,
                ParentId = library.Id,
                CollectionType = collectionType,
                Recursive = flatLibrary,
                PreferLandscape = !flatLibrary,
                Sort = JellyfinBrowseSort.Name,
                Filter = JellyfinBrowseFilter.All
            };
        }

        public static JellyfinBrowseState ForFolder(JellyfinItem folder)
        {
            if (folder == null || string.IsNullOrWhiteSpace(folder.Id))
            {
                throw new ArgumentException("A Jellyfin folder is required.", nameof(folder));
            }

            return new JellyfinBrowseState
            {
                Title = string.IsNullOrWhiteSpace(folder.Name) ? "文件夹" : folder.Name,
                ParentId = folder.Id,
                CollectionType = folder.CollectionType,
                Recursive = false,
                PreferLandscape = true,
                Sort = JellyfinBrowseSort.Name,
                Filter = JellyfinBrowseFilter.All
            };
        }

        public static JellyfinBrowseState ForSearch(string searchInitial = null)
        {
            return new JellyfinBrowseState
            {
                Title = "搜索",
                SearchInitial = JellyfinTitleInitials.NormalizeSelection(searchInitial),
                Recursive = true,
                IsSearch = true,
                PreferLandscape = false,
                Sort = JellyfinBrowseSort.Name,
                Filter = JellyfinBrowseFilter.All
            };
        }

        public static JellyfinBrowseState ForAllMedia()
        {
            return new JellyfinBrowseState
            {
                Title = "媒体库",
                Recursive = true,
                PreferLandscape = false,
                Sort = JellyfinBrowseSort.Name,
                Filter = JellyfinBrowseFilter.All
            };
        }

        public static JellyfinBrowseState ForFavorites()
        {
            return new JellyfinBrowseState
            {
                Title = "我的收藏",
                Recursive = true,
                PreferLandscape = false,
                Sort = JellyfinBrowseSort.Name,
                Filter = JellyfinBrowseFilter.Favorite
            };
        }

        private static bool IsFlatCollection(string collectionType)
        {
            return string.Equals(collectionType, "movies", StringComparison.OrdinalIgnoreCase)
                || string.Equals(collectionType, "tvshows", StringComparison.OrdinalIgnoreCase)
                || string.Equals(collectionType, "boxsets", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class BrowseCatalogService
    {
        private const string SearchItemTypes = "Movie,Series,Episode,Season,Video,BoxSet";
        private const int InitialIndexBatchSize = 500;
        private static readonly TimeSpan InitialIndexLifetime = TimeSpan.FromMinutes(5d);
        private readonly JellyfinApiClient _api;
        private string _initialIndexKey;
        private DateTime _initialIndexBuiltAtUtc;
        private List<JellyfinItem> _initialIndex;

        public BrowseCatalogService(JellyfinApiClient api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public Task<JellyfinQueryResult> LoadPageAsync(
            JellyfinBrowseState state,
            CancellationToken cancellationToken)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.IsSearch
                && JellyfinTitleInitials.NormalizeSelection(state.SearchInitial) != null)
            {
                return LoadInitialPageAsync(state, cancellationToken);
            }

            if (state.IsSearch && string.IsNullOrWhiteSpace(state.SearchTerm))
            {
                return Task.FromResult(new JellyfinQueryResult());
            }

            return _api.GetItemsAsync(BuildQuery(state), cancellationToken);
        }

        private async Task<JellyfinQueryResult> LoadInitialPageAsync(
            JellyfinBrowseState state,
            CancellationToken cancellationToken)
        {
            string selection = JellyfinTitleInitials.NormalizeSelection(state.SearchInitial);
            List<JellyfinItem> catalog = await LoadInitialIndexAsync(state, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return BuildInitialPage(catalog, state, selection);
        }

        public static JellyfinQueryResult BuildInitialPage(
            IEnumerable<JellyfinItem> catalog,
            JellyfinBrowseState state,
            string selection = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            string normalized = JellyfinTitleInitials.NormalizeSelection(
                selection ?? state.SearchInitial);
            int requestedStart = Math.Max(0, state.StartIndex);
            int pageSize = Math.Max(1, state.PageSize);
            int matchCount = 0;
            List<JellyfinItem> pageItems = new List<JellyfinItem>(pageSize);
            foreach (JellyfinItem item in catalog ?? new JellyfinItem[0])
            {
                if (!JellyfinTitleInitials.Matches(item, normalized))
                {
                    continue;
                }

                if (matchCount >= requestedStart && pageItems.Count < pageSize)
                {
                    pageItems.Add(item);
                }
                matchCount++;
            }

            return new JellyfinQueryResult
            {
                Items = pageItems,
                StartIndex = Math.Min(requestedStart, matchCount),
                TotalRecordCount = matchCount
            };
        }

        private async Task<List<JellyfinItem>> LoadInitialIndexAsync(
            JellyfinBrowseState state,
            CancellationToken cancellationToken)
        {
            string cacheKey = InitialIndexKey(state);
            if (_initialIndex != null
                && string.Equals(_initialIndexKey, cacheKey, StringComparison.Ordinal)
                && DateTime.UtcNow - _initialIndexBuiltAtUtc < InitialIndexLifetime)
            {
                return _initialIndex;
            }

            JellyfinBrowseState indexState = state.Clone();
            indexState.SearchInitial = null;
            indexState.SearchTerm = null;
            indexState.StartIndex = 0;
            indexState.PageSize = InitialIndexBatchSize;
            JellyfinItemsQuery query = BuildQuery(indexState);
            List<JellyfinItem> items = new List<JellyfinItem>();
            HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
            int nextIndex = 0;
            int expectedTotal;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                query.StartIndex = nextIndex;
                JellyfinQueryResult page = await _api.GetItemsAsync(query, cancellationToken)
                    ?? new JellyfinQueryResult();
                List<JellyfinItem> pageItems = page.Items ?? new List<JellyfinItem>();
                foreach (JellyfinItem item in pageItems)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.Id) || seenIds.Add(item.Id))
                    {
                        items.Add(item);
                    }
                }

                nextIndex += pageItems.Count;
                expectedTotal = Math.Max(nextIndex, page.TotalRecordCount);
                if (pageItems.Count == 0)
                {
                    break;
                }
            }
            while (nextIndex < expectedTotal);

            cancellationToken.ThrowIfCancellationRequested();
            _initialIndex = items;
            _initialIndexKey = cacheKey;
            _initialIndexBuiltAtUtc = DateTime.UtcNow;
            return _initialIndex;
        }

        private string InitialIndexKey(JellyfinBrowseState state)
        {
            JellyfinSession session = _api.Session;
            return string.Join(
                "|",
                session != null ? session.ServerUrl ?? string.Empty : string.Empty,
                session != null ? session.UserId ?? string.Empty : string.Empty,
                state.Filter.ToString(),
                state.Sort.ToString());
        }

        public static JellyfinItemsQuery BuildQuery(JellyfinBrowseState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            JellyfinItemsQuery query = new JellyfinItemsQuery
            {
                ParentId = state.ParentId,
                SearchTerm = string.IsNullOrWhiteSpace(state.SearchTerm)
                    ? null
                    : state.SearchTerm.Trim(),
                StartIndex = Math.Max(0, state.StartIndex),
                Limit = Math.Max(1, state.PageSize),
                Recursive = state.Recursive || state.IsSearch,
                IncludeItemTypes = IncludedTypes(state),
                Filters = FilterValue(state.Filter)
            };

            ApplySort(state, query);
            return query;
        }

        public static string SortLabel(JellyfinBrowseSort sort)
        {
            switch (sort)
            {
                case JellyfinBrowseSort.DateCreated:
                    return "最近加入";
                case JellyfinBrowseSort.CommunityRating:
                    return "评分最高";
                default:
                    return "名称 A–Z";
            }
        }

        public static string FilterLabel(JellyfinBrowseFilter filter)
        {
            switch (filter)
            {
                case JellyfinBrowseFilter.Unplayed:
                    return "未观看";
                case JellyfinBrowseFilter.Resumable:
                    return "可继续";
                case JellyfinBrowseFilter.Favorite:
                    return "已收藏";
                default:
                    return "全部";
            }
        }

        private static string IncludedTypes(JellyfinBrowseState state)
        {
            if (state.IsSearch || string.IsNullOrWhiteSpace(state.ParentId))
            {
                return SearchItemTypes;
            }

            switch ((state.CollectionType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "movies":
                    return "Movie";
                case "tvshows":
                    return "Series";
                case "boxsets":
                    return "BoxSet";
                default:
                    return null;
            }
        }

        private static string FilterValue(JellyfinBrowseFilter filter)
        {
            switch (filter)
            {
                case JellyfinBrowseFilter.Unplayed:
                    return "IsUnplayed";
                case JellyfinBrowseFilter.Resumable:
                    return "IsResumable";
                case JellyfinBrowseFilter.Favorite:
                    return "IsFavorite";
                default:
                    return null;
            }
        }

        private static void ApplySort(
            JellyfinBrowseState state,
            JellyfinItemsQuery query)
        {
            switch (state.Sort)
            {
                case JellyfinBrowseSort.DateCreated:
                    query.SortBy = "DateCreated,SortName";
                    query.SortOrder = "Descending,Ascending";
                    break;
                case JellyfinBrowseSort.CommunityRating:
                    query.SortBy = "CommunityRating,SortName";
                    query.SortOrder = "Descending,Ascending";
                    break;
                default:
                    if (state.PreferLandscape && !state.Recursive)
                    {
                        query.SortBy = "IsFolder,SortName";
                        query.SortOrder = "Ascending";
                    }
                    else
                    {
                        query.SortBy = "SortName";
                        query.SortOrder = "Ascending";
                    }
                    break;
            }
        }
    }
}
