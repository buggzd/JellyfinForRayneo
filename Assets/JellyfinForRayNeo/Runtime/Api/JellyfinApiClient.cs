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
        private const int DefaultMaxBitrate = 20_000_000;
        private const string ItemFields = "PrimaryImageAspectRatio,Overview,Genres,MediaSources,MediaStreams,DateCreated";

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
            if (string.IsNullOrWhiteSpace(seriesId))
            {
                throw new ArgumentException("A Jellyfin series id is required.", nameof(seriesId));
            }

            Dictionary<string, string> query = CommonItemQuery(limit);
            query["isMissing"] = "false";
            query["sortBy"] = "ParentIndexNumber,IndexNumber,SortName";
            query["sortOrder"] = "Ascending";
            string url = BuildSessionUrl(
                "/Shows/" + Uri.EscapeDataString(seriesId) + "/Episodes",
                query);
            return SendJsonAsync<JellyfinQueryResult>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public Task<JellyfinItem> GetItemAsync(string itemId, CancellationToken cancellationToken)
        {
            string url = BuildSessionUrl("/Items/" + Uri.EscapeDataString(itemId), new Dictionary<string, string>
            {
                { "userId", RequireSession().UserId }
            });
            return SendJsonAsync<JellyfinItem>(UnityWebRequest.kHttpVerbGET, url, null, true, cancellationToken);
        }

        public async Task<JellyfinPlaybackPlan> GetPlaybackPlanAsync(
            JellyfinItem item,
            long startPositionTicks,
            CancellationToken cancellationToken)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                throw new ArgumentException("A playable Jellyfin item is required.", nameof(item));
            }

            JellyfinSession session = RequireSession();
            JellyfinPlaybackInfoRequest body = new JellyfinPlaybackInfoRequest
            {
                UserId = session.UserId,
                StartTimeTicks = Math.Max(0L, startPositionTicks),
                MaxStreamingBitrate = DefaultMaxBitrate,
                MaxAudioChannels = 2,
                DeviceProfile = JellyfinDeviceProfile.CreateRayNeoAirProfile(DefaultMaxBitrate)
            };

            string endpoint = BuildSessionUrl("/Items/" + Uri.EscapeDataString(item.Id) + "/PlaybackInfo", null);
            JellyfinPlaybackInfoResponse response = await SendJsonAsync<JellyfinPlaybackInfoResponse>(
                UnityWebRequest.kHttpVerbPOST,
                endpoint,
                body,
                true,
                cancellationToken);

            if (response == null || response.MediaSources == null || response.MediaSources.Count == 0)
            {
                string reason = response != null ? response.ErrorCode : null;
                throw new JellyfinApiException(
                    "服务器没有返回可播放的媒体源" + (string.IsNullOrEmpty(reason) ? "。" : "：" + reason),
                    0,
                    endpoint);
            }

            JellyfinMediaSource source = response.MediaSources[0];
            bool unityDirectPlay = source.SupportsDirectPlay && IsUnityDirectPlayContainer(source.Container);
            string streamUrl;
            string playMethod;

            if (unityDirectPlay)
            {
                streamUrl = BuildDirectStreamUrl(item.Id, source.Id, startPositionTicks);
                playMethod = "DirectPlay";
            }
            else if (!string.IsNullOrWhiteSpace(source.TranscodingUrl))
            {
                streamUrl = JellyfinUrl.AppendApiKey(
                    JellyfinUrl.Combine(session.ServerUrl, source.TranscodingUrl),
                    session.AccessToken);
                playMethod = "Transcode";
            }
            else if (!string.IsNullOrWhiteSpace(source.DirectStreamUrl))
            {
                streamUrl = JellyfinUrl.AppendApiKey(
                    JellyfinUrl.Combine(session.ServerUrl, source.DirectStreamUrl),
                    session.AccessToken);
                playMethod = "DirectStream";
            }
            else
            {
                streamUrl = BuildDirectStreamUrl(item.Id, source.Id, startPositionTicks);
                playMethod = "DirectPlay";
            }

            return new JellyfinPlaybackPlan
            {
                Item = item,
                MediaSource = source,
                Url = streamUrl,
                PlaySessionId = response.PlaySessionId,
                PlayMethod = playMethod,
                StartPositionTicks = Math.Max(0L, startPositionTicks)
            };
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
                PlaySessionId = plan.PlaySessionId
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
                PlaySessionId = plan.PlaySessionId
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

        private string BuildDirectStreamUrl(string itemId, string mediaSourceId, long startPositionTicks)
        {
            JellyfinSession session = RequireSession();
            Dictionary<string, string> query = new Dictionary<string, string>
            {
                { "static", "true" },
                { "deviceId", session.DeviceId },
                { "api_key", session.AccessToken }
            };
            if (!string.IsNullOrWhiteSpace(mediaSourceId))
            {
                query["mediaSourceId"] = mediaSourceId;
            }
            if (startPositionTicks > 0)
            {
                query["startTimeTicks"] = startPositionTicks.ToString();
            }

            return JellyfinUrl.WithQuery(
                JellyfinUrl.Combine(session.ServerUrl, "/Videos/" + Uri.EscapeDataString(itemId) + "/stream"),
                query);
        }

        private Dictionary<string, string> CommonItemQuery(int limit)
        {
            JellyfinSession session = RequireSession();
            return new Dictionary<string, string>
            {
                { "userId", session.UserId },
                { "limit", Math.Max(1, limit <= 0 ? DefaultItemLimit : limit).ToString() },
                { "fields", ItemFields },
                { "enableImages", "true" },
                { "enableUserData", "true" },
                { "imageTypeLimit", "1" },
                { "enableImageTypes", "Primary,Backdrop,Thumb" }
            };
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

        private static bool IsUnityDirectPlayContainer(string container)
        {
            if (string.IsNullOrWhiteSpace(container))
            {
                return false;
            }

            string value = container.Trim().ToLowerInvariant();
            return value == "mp4" || value == "m4v" || value == "mov";
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
            CancellationToken cancellationToken)
        {
            JellyfinSession session = requiresSession ? RequireSession() : _session;
            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.timeout = 30;
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
