using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace JellyfinForRayNeo
{
    public sealed class JellyfinApiClient
    {
        private const int DefaultItemLimit = 18;
        private const int DirectPlayMaxBitrate = 120_000_000;
        private const int TranscodeMaxBitrate = 20_000_000;
        private const int SubtitleRequestTimeoutSeconds = 300;
        private const string ListItemFields =
            "PrimaryImageAspectRatio,Overview,OriginalTitle,Genres,DateCreated," +
            "ChildCount,RecursiveItemCount,ParentId";
        private const string DetailItemFields =
            ListItemFields + ",Studios,People,ProviderIds,ExternalUrls,Tags,Taglines," +
            "ProductionLocations,MediaSources,MediaStreams,Chapters";

        private readonly string _deviceId;
        private JellyfinSession _session;

        public JellyfinApiClient(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("A stable device id is required.", nameof(deviceId));
            }
            _deviceId = deviceId;
        }

        public JellyfinSession Session
        {
            get { return _session; }
        }

        public void SetSession(JellyfinSession session)
        {
            if (session == null || !session.IsValid)
            {
                throw new ArgumentException("A valid Jellyfin session is required.", nameof(session));
            }
            _session = session;
        }

        public void ClearSession()
        {
            _session = null;
        }

        public Task<JellyfinPublicSystemInfo> GetPublicSystemInfoAsync(string serverUrl, CancellationToken cancellationToken)
        {
            string normalized = JellyfinUrl.NormalizeServerUrl(serverUrl);
            return SendJsonAsync<JellyfinPublicSystemInfo>(
                UnityWebRequest.kHttpVerbGET,
                JellyfinUrl.Combine(normalized, "/System/Info/Public"),
                null,
                false,
                cancellationToken);
        }

        public Task<JellyfinAuthenticationResult> AuthenticateAsync(
            string serverUrl,
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("请输入用户名。", nameof(username));
            }

            JellyfinAuthenticationRequest body = new JellyfinAuthenticationRequest
            {
                Username = username.Trim(),
                Pw = password ?? string.Empty
            };

            return SendJsonAsync<JellyfinAuthenticationResult>(
                UnityWebRequest.kHttpVerbPOST,
                JellyfinUrl.Combine(serverUrl, "/Users/AuthenticateByName"),
                body,
                false,
                cancellationToken);
        }

        public Task<bool> GetQuickConnectEnabledAsync(
            string serverUrl,
            CancellationToken cancellationToken)
        {
            string normalized = JellyfinUrl.NormalizeServerUrl(serverUrl);
            return SendJsonAsync<bool>(
                UnityWebRequest.kHttpVerbGET,
                JellyfinUrl.Combine(normalized, "/QuickConnect/Enabled"),
                null,
                false,
                cancellationToken);
        }

        public Task<JellyfinQuickConnectResult> InitiateQuickConnectAsync(
            string serverUrl,
            CancellationToken cancellationToken)
        {
            string normalized = JellyfinUrl.NormalizeServerUrl(serverUrl);
            return SendJsonAsync<JellyfinQuickConnectResult>(
                UnityWebRequest.kHttpVerbPOST,
                JellyfinUrl.Combine(normalized, "/QuickConnect/Initiate"),
                null,
                false,
                cancellationToken);
        }

        public Task<JellyfinQuickConnectResult> GetQuickConnectStateAsync(
            string serverUrl,
            string secret,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new ArgumentException("A quick connect secret is required.", nameof(secret));
            }

            string endpoint = JellyfinUrl.WithQuery(
                JellyfinUrl.Combine(JellyfinUrl.NormalizeServerUrl(serverUrl), "/QuickConnect/Connect"),
                new Dictionary<string, string>
                {
                    { "secret", secret }
                });
            return SendJsonAsync<JellyfinQuickConnectResult>(
                UnityWebRequest.kHttpVerbGET,
                endpoint,
                null,
                false,
                cancellationToken);
        }

        public Task<JellyfinAuthenticationResult> AuthenticateWithQuickConnectAsync(
            string serverUrl,
            string secret,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new ArgumentException("A quick connect secret is required.", nameof(secret));
            }

            return SendJsonAsync<JellyfinAuthenticationResult>(
                UnityWebRequest.kHttpVerbPOST,
                JellyfinUrl.Combine(
                    JellyfinUrl.NormalizeServerUrl(serverUrl),
                    "/Users/AuthenticateWithQuickConnect"),
                new JellyfinQuickConnectAuthenticationRequest
                {
                    Secret = secret
                },
                false,
                cancellationToken);
        }

        public Task<JellyfinQueryResult> GetUserViewsAsync(CancellationToken cancellationToken)
        {
            string url = BuildSessionUrl("/UserViews", new Dictionary<string, string>
            {
                { "userId", RequireSession().UserId },
                { "includeExternalContent", "false" },
                { "includeHidden", "false" }
            });
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetResumeItemsAsync(int limit, CancellationToken cancellationToken)
        {
            string url = BuildSessionUrl("/UserItems/Resume", CommonItemQuery(limit));
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<List<JellyfinItem>> GetLatestItemsAsync(string includeItemTypes, int limit, CancellationToken cancellationToken)
        {
            Dictionary<string, string> query = CommonItemQuery(limit);
            query["includeItemTypes"] = includeItemTypes;
            string url = BuildSessionUrl("/Items/Latest", query);
            return SendJsonAsync<List<JellyfinItem>>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<List<JellyfinItem>> GetLatestItemsForLibraryAsync(
            string parentId,
            int limit,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(parentId))
            {
                throw new ArgumentException("A Jellyfin library id is required.", nameof(parentId));
            }

            Dictionary<string, string> query = CommonItemQuery(limit);
            query["parentId"] = parentId;
            query["includeItemTypes"] = "Movie,Series,Episode,Video";
            query["groupItems"] = "false";
            string url = BuildSessionUrl("/Items/Latest", query);
            return SendJsonAsync<List<JellyfinItem>>(
                UnityWebRequest.kHttpVerbGET,
                url,
                null,
                true,
                cancellationToken);
        }

        public Task<JellyfinQueryResult> GetNextUpAsync(int limit, CancellationToken cancellationToken)
        {
            Dictionary<string, string> query = CommonItemQuery(limit);
            query["disableFirstEpisode"] = "false";
            string url = BuildSessionUrl("/Shows/NextUp", query);
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetLibraryItemsAsync(string parentId, int limit, CancellationToken cancellationToken)
        {
            Dictionary<string, string> query = CommonItemQuery(limit);
            query["parentId"] = parentId;
            query["recursive"] = "true";
            query["includeItemTypes"] = "Movie,Series";
            query["sortBy"] = "DateCreated,SortName";
            query["sortOrder"] = "Descending";
            string url = BuildSessionUrl("/Items", query);
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetItemsAsync(
            JellyfinItemsQuery itemQuery,
            CancellationToken cancellationToken)
        {
            if (itemQuery == null)
            {
                throw new ArgumentNullException(nameof(itemQuery));
            }

            Dictionary<string, string> query = CommonItemQuery(itemQuery.Limit);
            query["startIndex"] = Math.Max(0, itemQuery.StartIndex).ToString();
            query["recursive"] = itemQuery.Recursive ? "true" : "false";
            query["enableTotalRecordCount"] = itemQuery.EnableTotalRecordCount ? "true" : "false";
            SetQueryValue(query, "parentId", itemQuery.ParentId);
            SetQueryValue(query, "searchTerm", itemQuery.SearchTerm);
            SetQueryValue(query, "includeItemTypes", itemQuery.IncludeItemTypes);
            SetQueryValue(query, "excludeItemTypes", itemQuery.ExcludeItemTypes);
            SetQueryValue(query, "sortBy", itemQuery.SortBy);
            SetQueryValue(query, "sortOrder", itemQuery.SortOrder);
            SetQueryValue(query, "filters", itemQuery.Filters);
            SetQueryValue(query, "genreIds", itemQuery.GenreIds);
            string url = BuildSessionUrl("/Items", query);
            return SendJsonAsync<JellyfinQueryResult>(
                UnityWebRequest.kHttpVerbGET,
                url,
                null,
                true,
                cancellationToken);
        }

        public Task<JellyfinQueryResult> GetGenresAsync(int limit, CancellationToken cancellationToken)
        {
            Dictionary<string, string> query = CommonItemQuery(limit);
            query["recursive"] = "true";
            query["sortBy"] = "SortName";
            query["sortOrder"] = "Ascending";
            string url = BuildSessionUrl("/Genres", query);
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetItemsByGenreAsync(string genreId, int limit, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(genreId))
            {
                throw new ArgumentException("A Jellyfin genre id is required.", nameof(genreId));
            }

            Dictionary<string, string> query = CommonItemQuery(limit);
            query["genreIds"] = genreId;
            query["recursive"] = "true";
            query["includeItemTypes"] = "Movie,Series";
            query["sortBy"] = "CommunityRating,SortName";
            query["sortOrder"] = "Descending";
            string url = BuildSessionUrl("/Items", query);
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetEpisodesAsync(string seriesId, int limit, CancellationToken cancellationToken)
        {
            return GetEpisodesAsync(seriesId, null, limit, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetEpisodesAsync(
            string seriesId,
            string seasonId,
            int limit,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                throw new ArgumentException("A Jellyfin series id is required.", nameof(seriesId));
            }

            Dictionary<string, string> query = CommonItemQuery(limit);
            query["isMissing"] = "false";
            query["sortBy"] = "ParentIndexNumber,IndexNumber,SortName";
            query["sortOrder"] = "Ascending";
            SetQueryValue(query, "seasonId", seasonId);
            string url = BuildSessionUrl(
                "/Shows/" + Uri.EscapeDataString(seriesId) + "/Episodes",
                query);
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinQueryResult> GetSeasonsAsync(
            string seriesId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                throw new ArgumentException("A Jellyfin series id is required.", nameof(seriesId));
            }

            Dictionary<string, string> query = CommonItemQuery(100);
            query["sortBy"] = "SortName";
            query["sortOrder"] = "Ascending";
            string url = BuildSessionUrl(
                "/Shows/" + Uri.EscapeDataString(seriesId) + "/Seasons",
                query);
            return SendJsonAsync<JellyfinQueryResult>(
                UnityWebRequest.kHttpVerbGET,
                url,
                null,
                true,
                cancellationToken);
        }

        public Task<JellyfinQueryResult> GetSimilarItemsAsync(
            string itemId,
            int limit,
            CancellationToken cancellationToken)
        {
            RequireItemId(itemId);
            Dictionary<string, string> query = CommonItemQuery(limit);
            string url = BuildSessionUrl(
                "/Items/" + Uri.EscapeDataString(itemId) + "/Similar",
                query);
            return SendJsonAsync<JellyfinQueryResult>(
                UnityWebRequest.kHttpVerbGET,
                url,
                null,
                true,
                cancellationToken);
        }

        public Task<JellyfinItem> GetItemAsync(string itemId, CancellationToken cancellationToken)
        {
            RequireItemId(itemId);
            string url = BuildSessionUrl("/Items/" + Uri.EscapeDataString(itemId), new Dictionary<string, string>
            {
                { "userId", RequireSession().UserId },
                { "fields", DetailItemFields },
                { "enableImages", "true" },
                { "enableUserData", "true" }
            });
            return SendJsonAsync<JellyfinItem>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinUserData> SetFavoriteAsync(
            string itemId,
            bool isFavorite,
            CancellationToken cancellationToken)
        {
            RequireItemId(itemId);
            JellyfinSession session = RequireSession();
            string path = "/Users/" + Uri.EscapeDataString(session.UserId)
                + "/FavoriteItems/" + Uri.EscapeDataString(itemId);
            string method = isFavorite ? UnityWebRequest.kHttpVerbPOST : UnityWebRequest.kHttpVerbDELETE;
            return SendJsonAsync<JellyfinUserData>(
                method,
                BuildSessionUrl(path, null),
                null,
                true,
                cancellationToken);
        }

        public Task<JellyfinUserData> SetPlayedAsync(
            string itemId,
            bool isPlayed,
            CancellationToken cancellationToken)
        {
            RequireItemId(itemId);
            JellyfinSession session = RequireSession();
            string path = "/Users/" + Uri.EscapeDataString(session.UserId)
                + "/PlayedItems/" + Uri.EscapeDataString(itemId);
            string method = isPlayed ? UnityWebRequest.kHttpVerbPOST : UnityWebRequest.kHttpVerbDELETE;
            return SendJsonAsync<JellyfinUserData>(
                method,
                BuildSessionUrl(path, null),
                null,
                true,
                cancellationToken);
        }

        public async Task<JellyfinPlaybackPlan> GetPlaybackPlanAsync(
            JellyfinItem item,
            long startPositionTicks,
            CancellationToken cancellationToken)
        {
            List<JellyfinPlaybackPlan> plans = await GetPlaybackPlansAsync(
                item,
                startPositionTicks,
                null,
                PlaybackCapabilities.Detect(),
                cancellationToken);
            return plans[0];
        }

        public async Task<List<JellyfinPlaybackPlan>> GetPlaybackPlansAsync(
            JellyfinItem item,
            long startPositionTicks,
            JellyfinPlaybackSelection selection,
            PlaybackCapabilities capabilities,
            CancellationToken cancellationToken)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                throw new ArgumentException("A playable Jellyfin item is required.", nameof(item));
            }

            JellyfinSession session = RequireSession();
            capabilities = capabilities ?? PlaybackCapabilities.Detect();
            long startTicks = Math.Max(0L, startPositionTicks);
            JellyfinPlaybackInfoRequest directRequest = new JellyfinPlaybackInfoRequest
            {
                UserId = session.UserId,
                StartTimeTicks = startTicks,
                MediaSourceId = selection != null ? selection.MediaSourceId : null,
                AudioStreamIndex = selection != null ? selection.AudioStreamIndex : null,
                SubtitleStreamIndex = selection != null ? selection.SubtitleStreamIndex : null,
                MaxStreamingBitrate = DirectPlayMaxBitrate,
                MaxAudioChannels = 8,
                DeviceProfile = JellyfinDeviceProfile.CreateRayNeoAirProfile(
                    DirectPlayMaxBitrate,
                    capabilities)
            };

            string endpoint = BuildSessionUrl("/Items/" + Uri.EscapeDataString(item.Id) + "/PlaybackInfo", null);
            JellyfinPlaybackInfoResponse directResponse = await SendJsonAsync<JellyfinPlaybackInfoResponse>(
                UnityWebRequest.kHttpVerbPOST,
                endpoint,
                directRequest,
                true,
                cancellationToken);

            if (directResponse == null
                || directResponse.MediaSources == null
                || directResponse.MediaSources.Count == 0)
            {
                string reason = directResponse != null ? directResponse.ErrorCode : null;
                throw new JellyfinApiException(
                    "服务器没有返回可播放的媒体源" + (string.IsNullOrEmpty(reason) ? "。" : "：" + reason),
                    0,
                    endpoint);
            }

            JellyfinMediaSource directSource = PlaybackLadder.SelectMediaSource(
                directResponse.MediaSources,
                selection != null ? selection.MediaSourceId : null);
            int? audioStreamIndex = PlaybackLadder.ResolveAudioIndex(
                directSource,
                selection != null ? selection.AudioStreamIndex : null);
            int? subtitleStreamIndex = PlaybackLadder.ResolveSubtitleIndex(
                directSource,
                selection != null ? selection.SubtitleStreamIndex : null);
            bool burnSubtitle = PlaybackLadder.RequiresSubtitleBurnIn(
                directSource,
                subtitleStreamIndex);

            JellyfinPlaybackInfoResponse transcodeResponse = null;
            Exception transcodeError = null;
            JellyfinPlaybackInfoRequest transcodeRequest = new JellyfinPlaybackInfoRequest
            {
                UserId = session.UserId,
                StartTimeTicks = startTicks,
                MediaSourceId = directSource.Id,
                AudioStreamIndex = audioStreamIndex,
                SubtitleStreamIndex = subtitleStreamIndex,
                MaxStreamingBitrate = TranscodeMaxBitrate,
                MaxAudioChannels = 2,
                EnableDirectPlay = false,
                EnableDirectStream = false,
                EnableTranscoding = true,
                AllowVideoStreamCopy = false,
                AllowAudioStreamCopy = false,
                AlwaysBurnInSubtitleWhenTranscoding = burnSubtitle,
                DeviceProfile = JellyfinDeviceProfile.CreateRayNeoAirProfile(
                    TranscodeMaxBitrate,
                    capabilities)
            };
            try
            {
                transcodeResponse = await SendJsonAsync<JellyfinPlaybackInfoResponse>(
                    UnityWebRequest.kHttpVerbPOST,
                    endpoint,
                    transcodeRequest,
                    true,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                transcodeError = exception;
            }

            JellyfinMediaSource transcodeSource = PlaybackLadder.SelectMediaSource(
                transcodeResponse != null ? transcodeResponse.MediaSources : null,
                directSource.Id);
            if (transcodeSource == null || string.IsNullOrWhiteSpace(transcodeSource.TranscodingUrl))
            {
                transcodeSource = !string.IsNullOrWhiteSpace(directSource.TranscodingUrl)
                    ? directSource
                    : transcodeSource;
            }

            List<PlaybackTier> tiers = PlaybackLadder.SelectTiers(
                directSource,
                transcodeSource,
                capabilities,
                audioStreamIndex,
                subtitleStreamIndex);
            if (tiers.Count == 0)
            {
                if (transcodeError != null)
                {
                    throw transcodeError;
                }
                string reason = transcodeResponse != null
                    ? transcodeResponse.ErrorCode
                    : directResponse.ErrorCode;
                throw new JellyfinApiException(
                    "当前设备与服务器没有可用的播放路径"
                    + (string.IsNullOrWhiteSpace(reason) ? "。" : "：" + reason),
                    0,
                    endpoint);
            }

            int audioTrackCount = PlaybackCapabilities.StreamsOfType(directSource, "Audio").Count;
            int audioTrackOrdinal = PlaybackLadder.AudioTrackOrdinal(
                directSource,
                audioStreamIndex);
            string directUrl = BuildDirectStreamUrl(
                item.Id,
                directSource,
                startTicks,
                audioStreamIndex,
                subtitleStreamIndex,
                directResponse.PlaySessionId);
            JellyfinMediaStream selectedSubtitle =
                PlaybackCapabilities.ResolveSubtitleStream(
                    directSource,
                    subtitleStreamIndex);
            string subtitleUrl = burnSubtitle || !subtitleStreamIndex.HasValue || subtitleStreamIndex.Value < 0
                ? null
                : BuildSubtitleUrl(item.Id, directSource, selectedSubtitle);
            List<JellyfinPlaybackPlan> plans = new List<JellyfinPlaybackPlan>();
            foreach (PlaybackTier tier in tiers)
            {
                bool transcoding = tier == PlaybackTier.ServerTranscode;
                plans.Add(new JellyfinPlaybackPlan
                {
                    Item = item,
                    MediaSource = directSource,
                    Url = transcoding
                        ? JellyfinUrl.AppendApiKey(
                            JellyfinUrl.Combine(session.ServerUrl, transcodeSource.TranscodingUrl),
                            session.AccessToken)
                        : directUrl,
                    PlaySessionId = transcoding && transcodeResponse != null
                        ? transcodeResponse.PlaySessionId
                        : directResponse.PlaySessionId,
                    PlayMethod = transcoding ? "Transcode" : "DirectPlay",
                    StartPositionTicks = startTicks,
                    Tier = tier,
                    AudioStreamIndex = audioStreamIndex,
                    SubtitleStreamIndex = subtitleStreamIndex,
                    LocalAudioTrackIndex = transcoding ? 0 : audioTrackOrdinal,
                    LocalAudioTrackCount = transcoding ? 1 : Math.Max(1, audioTrackCount),
                    SubtitleUrl = transcoding && burnSubtitle ? null : subtitleUrl,
                    SubtitleCodec = "vtt",
                    SubtitleBurnedIn = transcoding && burnSubtitle
                });
            }

            return plans;
        }

        public Task<string> GetSubtitleTextAsync(
            JellyfinPlaybackPlan plan,
            CancellationToken cancellationToken)
        {
            if (plan == null || string.IsNullOrWhiteSpace(plan.SubtitleUrl))
            {
                return Task.FromResult<string>(null);
            }
            return SendStringAsync(
                UnityWebRequest.kHttpVerbGET,
                plan.SubtitleUrl,
                null,
                true,
                cancellationToken,
                SubtitleRequestTimeoutSeconds);
        }

        public Task ReportPlaybackStartAsync(JellyfinPlaybackPlan plan, bool paused, long positionTicks, CancellationToken cancellationToken)
        {
            JellyfinPlaybackStartInfo body = new JellyfinPlaybackStartInfo
            {
                CanSeek = true,
                ItemId = plan.Item.Id,
                MediaSourceId = plan.MediaSource != null ? plan.MediaSource.Id : null,
                IsPaused = paused,
                IsMuted = false,
                PositionTicks = Math.Max(0L, positionTicks),
                PlayMethod = plan.PlayMethod,
                PlaySessionId = plan.PlaySessionId,
                AudioStreamIndex = plan.AudioStreamIndex,
                SubtitleStreamIndex = plan.SubtitleStreamIndex
            };
            return SendEmptyAsync(UnityWebRequest.kHttpVerbPOST, BuildSessionUrl("/Sessions/Playing", null), body, cancellationToken);
        }

        public Task ReportPlaybackProgressAsync(JellyfinPlaybackPlan plan, bool paused, long positionTicks, CancellationToken cancellationToken)
        {
            JellyfinPlaybackProgressInfo body = new JellyfinPlaybackProgressInfo
            {
                CanSeek = true,
                ItemId = plan.Item.Id,
                MediaSourceId = plan.MediaSource != null ? plan.MediaSource.Id : null,
                IsPaused = paused,
                IsMuted = false,
                PositionTicks = Math.Max(0L, positionTicks),
                PlayMethod = plan.PlayMethod,
                PlaySessionId = plan.PlaySessionId,
                AudioStreamIndex = plan.AudioStreamIndex,
                SubtitleStreamIndex = plan.SubtitleStreamIndex
            };
            return SendEmptyAsync(UnityWebRequest.kHttpVerbPOST, BuildSessionUrl("/Sessions/Playing/Progress", null), body, cancellationToken);
        }

        public Task ReportPlaybackStoppedAsync(JellyfinPlaybackPlan plan, long positionTicks, bool failed, CancellationToken cancellationToken)
        {
            JellyfinPlaybackStopInfo body = new JellyfinPlaybackStopInfo
            {
                ItemId = plan.Item.Id,
                MediaSourceId = plan.MediaSource != null ? plan.MediaSource.Id : null,
                PositionTicks = Math.Max(0L, positionTicks),
                PlaySessionId = plan.PlaySessionId,
                Failed = failed
            };
            return SendEmptyAsync(UnityWebRequest.kHttpVerbPOST, BuildSessionUrl("/Sessions/Playing/Stopped", null), body, cancellationToken);
        }

        public string BuildPrimaryImageUrl(JellyfinItem item, int maxWidth = 360)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return null;
            }

            string tag = null;
            if (item.ImageTags != null)
            {
                item.ImageTags.TryGetValue("Primary", out tag);
            }

            return BuildImageUrl(item.Id, "Primary", tag, maxWidth);
        }

        public string BuildBackdropImageUrl(JellyfinItem item, int maxWidth = 1280)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return null;
            }

            string imageItemId = string.IsNullOrWhiteSpace(item.ParentBackdropItemId)
                ? item.Id
                : item.ParentBackdropItemId;
            string tag = item.BackdropImageTags != null && item.BackdropImageTags.Count > 0
                ? item.BackdropImageTags[0]
                : null;
            return BuildImageUrl(imageItemId, "Backdrop", tag, maxWidth);
        }

        private string BuildImageUrl(string itemId, string imageType, string tag, int maxWidth)
        {
            JellyfinSession session = RequireSession();
            Dictionary<string, string> query = new Dictionary<string, string>
            {
                { "maxWidth", Math.Max(64, maxWidth).ToString() },
                { "quality", "90" },
                { "api_key", session.AccessToken }
            };
            if (!string.IsNullOrWhiteSpace(tag))
            {
                query["tag"] = tag;
            }

            return JellyfinUrl.WithQuery(
                JellyfinUrl.Combine(session.ServerUrl, "/Items/" + Uri.EscapeDataString(itemId) + "/Images/" + imageType),
                query);
        }

        private string BuildDirectStreamUrl(
            string itemId,
            JellyfinMediaSource mediaSource,
            long startPositionTicks,
            int? audioStreamIndex,
            int? subtitleStreamIndex,
            string playSessionId)
        {
            JellyfinSession session = RequireSession();
            Dictionary<string, string> query = new Dictionary<string, string>
            {
                { "static", "true" },
                { "deviceId", session.DeviceId },
                { "api_key", session.AccessToken }
            };
            if (mediaSource != null && !string.IsNullOrWhiteSpace(mediaSource.Id))
            {
                query["mediaSourceId"] = mediaSource.Id;
            }
            if (startPositionTicks > 0)
            {
                query["startTimeTicks"] = startPositionTicks.ToString();
            }
            if (audioStreamIndex.HasValue)
            {
                query["audioStreamIndex"] = audioStreamIndex.Value.ToString();
            }
            if (subtitleStreamIndex.HasValue)
            {
                query["subtitleStreamIndex"] = subtitleStreamIndex.Value.ToString();
            }
            if (!string.IsNullOrWhiteSpace(playSessionId))
            {
                query["playSessionId"] = playSessionId;
            }

            string container = mediaSource != null && !string.IsNullOrWhiteSpace(mediaSource.Container)
                ? "." + Uri.EscapeDataString(mediaSource.Container.Trim().ToLowerInvariant())
                : string.Empty;
            return JellyfinUrl.WithQuery(
                JellyfinUrl.Combine(
                    session.ServerUrl,
                    "/Videos/" + Uri.EscapeDataString(itemId) + "/stream" + container),
                query);
        }

        private string BuildSubtitleUrl(
            string itemId,
            JellyfinMediaSource mediaSource,
            JellyfinMediaStream stream)
        {
            JellyfinSession session = RequireSession();
            if (stream == null)
            {
                return null;
            }
            if (!string.IsNullOrWhiteSpace(stream.DeliveryUrl))
            {
                return JellyfinUrl.AppendApiKey(
                    JellyfinUrl.Combine(session.ServerUrl, stream.DeliveryUrl),
                    session.AccessToken);
            }
            if (mediaSource == null || string.IsNullOrWhiteSpace(mediaSource.Id))
            {
                return null;
            }
            string path = "/Videos/" + Uri.EscapeDataString(itemId)
                + "/" + Uri.EscapeDataString(mediaSource.Id)
                + "/Subtitles/" + stream.Index
                + "/Stream.vtt";
            return JellyfinUrl.WithQuery(
                JellyfinUrl.Combine(session.ServerUrl, path),
                new Dictionary<string, string>
                {
                    { "api_key", session.AccessToken },
                    { "copyTimestamps", "false" },
                    { "addVttTimeMap", "false" },
                    { "startPositionTicks", "0" }
                });
        }

        private Dictionary<string, string> CommonItemQuery(int limit)
        {
            JellyfinSession session = RequireSession();
            return new Dictionary<string, string>
            {
                { "userId", session.UserId },
                { "limit", Math.Max(1, limit <= 0 ? DefaultItemLimit : limit).ToString() },
                { "fields", ListItemFields },
                { "enableImages", "true" },
                { "enableUserData", "true" },
                { "imageTypeLimit", "1" },
                { "enableImageTypes", "Primary,Backdrop,Thumb" }
            };
        }

        private static void SetQueryValue(
            IDictionary<string, string> query,
            string key,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query[key] = value.Trim();
            }
        }

        private string BuildSessionUrl(string path, IDictionary<string, string> query)
        {
            JellyfinSession session = RequireSession();
            string url = JellyfinUrl.Combine(session.ServerUrl, path);
            return query == null ? url : JellyfinUrl.WithQuery(url, query);
        }

        private JellyfinSession RequireSession()
        {
            if (_session == null || !_session.IsValid)
            {
                throw new InvalidOperationException("Jellyfin user session is not available.");
            }
            return _session;
        }

        private static void RequireItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("A Jellyfin item id is required.", nameof(itemId));
            }
        }

        private Task SendEmptyAsync(string method, string url, object body, CancellationToken cancellationToken)
        {
            return SendStringAsync(method, url, body, true, cancellationToken);
        }

        private async Task<T> SendJsonAsync<T>(
            string method,
            string url,
            object body,
            bool requiresSession,
            CancellationToken cancellationToken)
        {
            string response = await SendStringAsync(method, url, body, requiresSession, cancellationToken);
            if (string.IsNullOrWhiteSpace(response))
            {
                return default(T);
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(response);
            }
            catch (JsonException exception)
            {
                throw new JellyfinApiException("无法解析 Jellyfin 服务器响应。", 0, url, Truncate(response), exception);
            }
        }

        private async Task<string> SendStringAsync(
            string method,
            string url,
            object body,
            bool requiresSession,
            CancellationToken cancellationToken,
            int timeoutSeconds = 30)
        {
            JellyfinSession session = requiresSession ? RequireSession() : null;
            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.timeout = Math.Max(1, timeoutSeconds);
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    string json = JsonConvert.SerializeObject(body, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                string token = session != null ? session.AccessToken : null;
                string authorization = JellyfinUrl.BuildAuthorizationHeader(_deviceId, token);
                request.SetRequestHeader("Authorization", authorization);
                request.SetRequestHeader("X-Emby-Authorization", authorization);
                request.SetRequestHeader("Accept", "application/json");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.SetRequestHeader("X-Emby-Token", token);
                }

                await request.SendRequestAsync(cancellationToken);
                bool success = request.result == UnityWebRequest.Result.Success
                    && request.responseCode >= 200
                    && request.responseCode < 300;
                if (!success)
                {
                    string responseBody = request.downloadHandler != null ? request.downloadHandler.text : null;
                    string message = request.responseCode == 401 || request.responseCode == 403
                        ? "用户名、密码或登录会话无效。"
                        : "Jellyfin 请求失败：" + (string.IsNullOrWhiteSpace(request.error) ? request.responseCode.ToString() : request.error);
                    throw new JellyfinApiException(message, request.responseCode, url, Truncate(responseBody));
                }

                return request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            }
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 800)
            {
                return value;
            }
            return value.Substring(0, 800);
        }
    }
}
