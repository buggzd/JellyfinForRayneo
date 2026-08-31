using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JellyfinForRayNeo
{
    public sealed class JellyfinHomeSection
    {
        public string Key;
        public string Title;
        public List<JellyfinItem> Items;
    }

    public sealed class HomeCatalogService
    {
        private const int ShelfLimit = 10;
        private const int MaxLibraryShelves = 6;
        private const int MaxGenreShelves = 3;
        private readonly JellyfinApiClient _api;

        public HomeCatalogService(JellyfinApiClient api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<List<JellyfinHomeSection>> LoadHomeAsync(CancellationToken cancellationToken)
        {
            Task<JellyfinQueryResult> viewsTask = _api.GetUserViewsAsync(cancellationToken);
            Task<JellyfinQueryResult> resumeTask = _api.GetResumeItemsAsync(12, cancellationToken);
            Task<JellyfinQueryResult> nextUpTask = _api.GetNextUpAsync(12, cancellationToken);
            Task<List<JellyfinItem>> latestMoviesTask = _api.GetLatestItemsAsync("Movie", ShelfLimit, cancellationToken);
            Task<List<JellyfinItem>> latestSeriesTask = _api.GetLatestItemsAsync("Series", ShelfLimit, cancellationToken);
            Task<JellyfinQueryResult> genresTask = _api.GetGenresAsync(MaxGenreShelves, cancellationToken);

            await Task.WhenAll(viewsTask, resumeTask, nextUpTask, latestMoviesTask, latestSeriesTask, genresTask);

            List<JellyfinHomeSection> sections = new List<JellyfinHomeSection>();
            AddIfPopulated(sections, "resume", "继续观看", resumeTask.Result != null ? resumeTask.Result.Items : null);
            AddIfPopulated(sections, "next-up", "下一集", nextUpTask.Result != null ? nextUpTask.Result.Items : null);
            AddIfPopulated(sections, "latest-movies", "最新电影", latestMoviesTask.Result);
            AddIfPopulated(sections, "latest-series", "最新剧集", latestSeriesTask.Result);

            List<JellyfinItem> views = viewsTask.Result != null && viewsTask.Result.Items != null
                ? viewsTask.Result.Items
                    .Where(view => view != null && !string.IsNullOrWhiteSpace(view.Id))
                    .Take(MaxLibraryShelves)
                    .ToList()
                : new List<JellyfinItem>();

            List<Task<JellyfinHomeSection>> libraryTasks = views
                .Select(view => LoadLibrarySectionAsync(view, cancellationToken))
                .ToList();
            JellyfinHomeSection[] librarySections = await Task.WhenAll(libraryTasks);
            foreach (JellyfinHomeSection section in librarySections)
            {
                if (section != null && section.Items != null && section.Items.Count > 0)
                {
                    sections.Add(section);
                }
            }

            List<JellyfinItem> genres = genresTask.Result != null && genresTask.Result.Items != null
                ? genresTask.Result.Items
                    .Where(genre => genre != null && !string.IsNullOrWhiteSpace(genre.Id))
                    .Take(MaxGenreShelves)
                    .ToList()
                : new List<JellyfinItem>();
            List<Task<JellyfinHomeSection>> genreTasks = genres
                .Select(genre => LoadGenreSectionAsync(genre, cancellationToken))
                .ToList();
            JellyfinHomeSection[] genreSections = await Task.WhenAll(genreTasks);
            foreach (JellyfinHomeSection section in genreSections)
            {
                if (section != null && section.Items != null && section.Items.Count > 0)
                {
                    sections.Add(section);
                }
            }

            return sections;
        }

        private async Task<JellyfinHomeSection> LoadLibrarySectionAsync(JellyfinItem view, CancellationToken cancellationToken)
        {
            JellyfinQueryResult result = await _api.GetLibraryItemsAsync(view.Id, ShelfLimit, cancellationToken);
            return new JellyfinHomeSection
            {
                Key = "library-" + view.Id,
                Title = string.IsNullOrWhiteSpace(view.Name) ? "媒体库" : view.Name,
                Items = result != null && result.Items != null ? result.Items : new List<JellyfinItem>()
            };
        }

        private async Task<JellyfinHomeSection> LoadGenreSectionAsync(JellyfinItem genre, CancellationToken cancellationToken)
        {
            JellyfinQueryResult result = await _api.GetItemsByGenreAsync(genre.Id, ShelfLimit, cancellationToken);
            return new JellyfinHomeSection
            {
                Key = "genre-" + genre.Id,
                Title = string.IsNullOrWhiteSpace(genre.Name) ? "分类" : genre.Name,
                Items = result != null && result.Items != null ? result.Items : new List<JellyfinItem>()
            };
        }

        private static void AddIfPopulated(
            ICollection<JellyfinHomeSection> sections,
            string key,
            string title,
            IEnumerable<JellyfinItem> items)
        {
            if (items == null)
            {
                return;
            }

            List<JellyfinItem> materialized = items.Where(item => item != null).ToList();
            if (materialized.Count == 0)
            {
                return;
            }

            sections.Add(new JellyfinHomeSection
            {
                Key = key,
                Title = title,
                Items = materialized
            });
        }
    }
}
