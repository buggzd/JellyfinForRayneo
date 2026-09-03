using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace JellyfinForRayNeo.Tests
{
    public sealed class JellyfinModelTests
    {
        [Test]
        public void AuthenticationResponse_DeserializesUserAndToken()
        {
            const string json = "{\"User\":{\"Id\":\"user-id\",\"Name\":\"Ray User\"},\"AccessToken\":\"access-token\",\"ServerId\":\"server-id\"}";
            JellyfinAuthenticationResult result = JsonConvert.DeserializeObject<JellyfinAuthenticationResult>(json);

            Assert.NotNull(result);
            Assert.NotNull(result.User);
            Assert.AreEqual("user-id", result.User.Id);
            Assert.AreEqual("Ray User", result.User.Name);
            Assert.AreEqual("access-token", result.AccessToken);
        }

        [Test]
        public void QuickConnectResponse_DeserializesCodeAndSecret()
        {
            const string json =
                "{\"Authenticated\":false,\"Secret\":\"internal-secret\",\"Code\":\"482731\",\"DeviceName\":\"RayNeo Air\"}";

            JellyfinQuickConnectResult result =
                JsonConvert.DeserializeObject<JellyfinQuickConnectResult>(json);

            Assert.NotNull(result);
            Assert.IsFalse(result.Authenticated);
            Assert.AreEqual("internal-secret", result.Secret);
            Assert.AreEqual("482731", result.Code);
            Assert.AreEqual("RayNeo Air", result.DeviceName);
        }

        [Test]
        public void ItemResponse_DeserializesPosterAndResumeMetadata()
        {
            const string json = "{\"Id\":\"item-id\",\"Name\":\"Example\",\"Type\":\"Movie\",\"MediaType\":\"Video\",\"ImageTags\":{\"Primary\":\"image-tag\"},\"UserData\":{\"PlaybackPositionTicks\":120000000,\"PlayedPercentage\":42.5}}";
            JellyfinItem item = JsonConvert.DeserializeObject<JellyfinItem>(json);

            Assert.AreEqual("image-tag", item.ImageTags["Primary"]);
            Assert.AreEqual(120000000L, item.UserData.PlaybackPositionTicks);
            Assert.AreEqual(42.5d, item.UserData.PlayedPercentage.Value, 0.001d);
            Assert.IsTrue(item.IsPlayable);
        }

        [Test]
        public void FolderMetadata_ExposesBrowsingAndUnplayedBadges()
        {
            const string json =
                "{\"Id\":\"folder-id\",\"Name\":\"课程\",\"Type\":\"Folder\",\"CollectionType\":\"homevideos\",\"ChildCount\":23,\"UserData\":{\"UnplayedItemCount\":7}}";

            JellyfinItem item = JsonConvert.DeserializeObject<JellyfinItem>(json);

            Assert.IsTrue(item.IsBrowsableContainer);
            Assert.IsFalse(item.IsPlayable);
            Assert.AreEqual("homevideos", item.CollectionType);
            Assert.AreEqual(23, item.VisibleChildCount);
            Assert.AreEqual(7, item.UserData.UnplayedItemCount);
        }

        [Test]
        public void BrowseQuery_KeepsCourseFoldersNonRecursiveAndFolderFirst()
        {
            JellyfinBrowseState state = JellyfinBrowseState.ForLibrary(new JellyfinItem
            {
                Id = "course-library",
                Name = "网课",
                Type = "CollectionFolder",
                CollectionType = "homevideos"
            });

            JellyfinItemsQuery query = BrowseCatalogService.BuildQuery(state);

            Assert.IsFalse(query.Recursive);
            Assert.IsNull(query.IncludeItemTypes);
            Assert.AreEqual("IsFolder,SortName", query.SortBy);
            Assert.AreEqual("Ascending", query.SortOrder);
            Assert.IsTrue(state.PreferLandscape);
        }

        [Test]
        public void BrowseQuery_UsesServerPagingSearchAndFavoriteFilter()
        {
            JellyfinBrowseState state = JellyfinBrowseState.ForSearch();
            state.SearchTerm = " 星球大战 ";
            state.StartIndex = 30;
            state.Filter = JellyfinBrowseFilter.Favorite;
            state.Sort = JellyfinBrowseSort.CommunityRating;

            JellyfinItemsQuery query = BrowseCatalogService.BuildQuery(state);

            Assert.AreEqual("星球大战", query.SearchTerm);
            Assert.AreEqual(30, query.StartIndex);
            Assert.AreEqual(JellyfinBrowseState.DefaultPageSize, query.Limit);
            Assert.IsTrue(query.Recursive);
            StringAssert.Contains("Movie", query.IncludeItemTypes);
            Assert.AreEqual("IsFavorite", query.Filters);
            Assert.AreEqual("CommunityRating,SortName", query.SortBy);
        }

        [Test]
        public void InitialSearch_MatchesEnglishAndChinesePinyinTitles()
        {
            JellyfinItem chinese = new JellyfinItem
            {
                Name = "星球大战",
                OriginalTitle = "Star Wars"
            };
            JellyfinItem english = new JellyfinItem { Name = "Avatar" };
            JellyfinItem numeric = new JellyfinItem { Name = "2046" };

            Assert.AreEqual('X', JellyfinTitleInitials.InitialFor(chinese.Name));
            Assert.AreEqual('C', JellyfinTitleInitials.InitialFor("重庆森林"));
            Assert.AreEqual('C', JellyfinTitleInitials.InitialFor("长安三万里"));
            Assert.AreEqual('P', JellyfinTitleInitials.InitialFor("便宜没好货"));
            Assert.IsTrue(JellyfinTitleInitials.Matches(chinese, "x"));
            Assert.IsTrue(JellyfinTitleInitials.Matches(chinese, "s"));
            Assert.IsTrue(JellyfinTitleInitials.Matches(english, "A"));
            Assert.IsTrue(JellyfinTitleInitials.Matches(numeric, "#"));
            Assert.IsFalse(JellyfinTitleInitials.Matches(english, "B"));
        }

        [Test]
        public void InitialSearch_NormalizesAlphabetAndAllSelection()
        {
            JellyfinBrowseState state = JellyfinBrowseState.ForSearch(" l ");

            Assert.AreEqual("L", state.SearchInitial);
            Assert.AreEqual("*", JellyfinTitleInitials.NormalizeSelection("*"));
            Assert.IsNull(JellyfinTitleInitials.NormalizeSelection("星球"));
            Assert.IsTrue(JellyfinTitleInitials.Matches(
                new JellyfinItem { Name = "流浪地球" },
                JellyfinTitleInitials.All));
        }

        [Test]
        public void InitialSearch_PaginatesOnlyMatchingTitles()
        {
            JellyfinBrowseState state = JellyfinBrowseState.ForSearch("X");
            state.StartIndex = 1;
            state.PageSize = 1;
            List<JellyfinItem> catalog = new List<JellyfinItem>
            {
                new JellyfinItem { Id = "a", Name = "Avatar" },
                new JellyfinItem { Id = "x1", Name = "星球大战" },
                new JellyfinItem { Id = "l", Name = "流浪地球" },
                new JellyfinItem { Id = "x2", Name = "小时代" }
            };

            JellyfinQueryResult result = BrowseCatalogService.BuildInitialPage(
                catalog,
                state);

            Assert.AreEqual(2, result.TotalRecordCount);
            Assert.AreEqual(1, result.StartIndex);
            Assert.AreEqual(1, result.Items.Count);
            Assert.AreEqual("x2", result.Items[0].Id);
        }

        [Test]
        public void ItemResponse_DeserializesExpandedDetailMetadata()
        {
            const string json = "{\"OriginalTitle\":\"Original\",\"Tags\":[\"Drama\"],\"ProductionLocations\":[\"Japan\"],\"Studios\":[{\"Name\":\"Studio A\"}],\"People\":[{\"Name\":\"Director A\",\"Type\":\"Director\"}],\"ProviderIds\":{\"Tmdb\":\"123\"},\"CriticRating\":91,\"PremiereDate\":\"2025-04-01T00:00:00.0000000Z\",\"MediaSources\":[{\"MediaStreams\":[{\"Type\":\"Video\",\"Codec\":\"h264\",\"Width\":1920,\"Height\":1080,\"VideoRange\":\"SDR\"}]}]}";
            JellyfinItem item = JsonConvert.DeserializeObject<JellyfinItem>(json);

            Assert.AreEqual("Original", item.OriginalTitle);
            Assert.AreEqual("Drama", item.Tags[0]);
            Assert.AreEqual("Studio A", item.Studios[0].Name);
            Assert.AreEqual("Director", item.People[0].Type);
            Assert.AreEqual("123", item.ProviderIds["Tmdb"]);
            Assert.AreEqual(1920, item.MediaSources[0].MediaStreams[0].Width);
            Assert.AreEqual("SDR", item.MediaSources[0].MediaStreams[0].VideoRange);
        }

        [Test]
        public void OverviewMarkup_IsConvertedToReadablePlainText()
        {
            const string overview = "<p>第一行<br>第二行 &amp; 内容</p><ul><li>条目一</li><li>条目二</li></ul>";

            string result = JellyfinText.ToPlainText(overview);

            Assert.AreEqual("第一行\n第二行 & 内容\n• 条目一\n• 条目二", result);
            StringAssert.DoesNotContain("<br>", result);
        }

        [Test]
        public void RayNeoPlaybackProfile_RequestsAndroidFriendlyHlsFallback()
        {
            JellyfinDeviceProfile profile = JellyfinDeviceProfile.CreateRayNeoAirProfile(20_000_000);

            Assert.AreEqual("RayNeo Air / Unity Android", profile.Name);
            Assert.AreEqual("mp4,m4v,mov", profile.DirectPlayProfiles[0].Container);
            Assert.AreEqual("hls", profile.TranscodingProfiles[0].Protocol);
            Assert.AreEqual("h264", profile.TranscodingProfiles[0].VideoCodec);
            Assert.AreEqual("2", profile.TranscodingProfiles[0].MaxAudioChannels);
        }

        [Test]
        public void SessionRequiresServerTokenUserAndDevice()
        {
            JellyfinSession session = new JellyfinSession
            {
                ServerUrl = "http://localhost:8096",
                AccessToken = "token",
                UserId = "user",
                DeviceId = "device"
            };

            Assert.IsTrue(session.IsValid);
            session.AccessToken = string.Empty;
            Assert.IsFalse(session.IsValid);
        }

        [Test]
        public void EpisodePlaybackResolver_PrefersMostRecentResume()
        {
            JellyfinItem episodeOne = CreateEpisode(1, played: false);
            JellyfinItem episodeTwo = CreateEpisode(2, played: false, positionTicks: 300_000_000L);
            episodeTwo.UserData.LastPlayedDate = "2026-09-01T12:00:00Z";
            JellyfinItem episodeThree = CreateEpisode(3, played: false, positionTicks: 180_000_000L);
            episodeThree.UserData.LastPlayedDate = "2026-09-02T12:00:00Z";

            JellyfinItem selected = EpisodePlaybackResolver.Select(
                new List<JellyfinItem> { episodeOne, episodeTwo, episodeThree });

            Assert.AreSame(episodeThree, selected);
        }

        [Test]
        public void EpisodePlaybackResolver_SelectsFirstUnplayedEpisode()
        {
            JellyfinItem episodeOne = CreateEpisode(1, played: true);
            JellyfinItem episodeTwo = CreateEpisode(2, played: false);
            JellyfinItem episodeThree = CreateEpisode(3, played: false);

            JellyfinItem selected = EpisodePlaybackResolver.Select(
                new List<JellyfinItem> { episodeThree, episodeOne, episodeTwo });

            Assert.AreSame(episodeTwo, selected);
            Assert.AreEqual("S1E2", EpisodePlaybackResolver.EpisodeCode(selected));
            episodeTwo.SeriesName = "测试剧集";
            Assert.AreEqual("测试剧集  S1E2", episodeTwo.Subtitle);
        }

        private static JellyfinItem CreateEpisode(
            int index,
            bool played,
            long positionTicks = 0L)
        {
            return new JellyfinItem
            {
                Id = "episode-" + index,
                Name = "第 " + index + " 集",
                Type = "Episode",
                MediaType = "Video",
                ParentIndexNumber = 1,
                IndexNumber = index,
                UserData = new JellyfinUserData
                {
                    Played = played,
                    PlaybackPositionTicks = positionTicks
                }
            };
        }
    }
}
