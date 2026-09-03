using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JellyfinForRayNeo.Tests
{
    public sealed class MainScenePlayModeTests
    {
        private static readonly string[] SessionKeys =
        {
            "JellyfinForRayNeo.Session.ServerUrl",
            "JellyfinForRayNeo.Session.ServerName",
            "JellyfinForRayNeo.Session.ServerVersion",
            "JellyfinForRayNeo.Session.ServerId",
            "JellyfinForRayNeo.Session.AccessToken",
            "JellyfinForRayNeo.Session.UserId",
            "JellyfinForRayNeo.Session.UserName"
        };

        private readonly Dictionary<string, string> _savedSession = new Dictionary<string, string>();
        private bool _hadDisplayModePreference;
        private string _savedDisplayModePreference;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _savedSession.Clear();
            foreach (string key in SessionKeys)
            {
                if (PlayerPrefs.HasKey(key))
                {
                    _savedSession[key] = PlayerPrefs.GetString(key);
                }
            }

            _hadDisplayModePreference = PlayerPrefs.HasKey(
                Air3SDisplayController.EditorPreferenceKey);
            _savedDisplayModePreference = PlayerPrefs.GetString(
                Air3SDisplayController.EditorPreferenceKey,
                string.Empty);
            PlayerPrefs.SetString(
                Air3SDisplayController.EditorPreferenceKey,
                Air3SDisplayController.Mirror2DPreference);

            new JellyfinSessionStore().ClearSession();
            AsyncOperation load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            Assert.NotNull(load, "Main scene must be present in Build Settings.");
            while (!load.isDone)
            {
                yield return null;
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            new JellyfinSessionStore().ClearSession();
            foreach (KeyValuePair<string, string> pair in _savedSession)
            {
                PlayerPrefs.SetString(pair.Key, pair.Value);
            }
            if (_hadDisplayModePreference)
            {
                PlayerPrefs.SetString(
                    Air3SDisplayController.EditorPreferenceKey,
                    _savedDisplayModePreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(Air3SDisplayController.EditorPreferenceKey);
            }
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainScene_StartsJellyfinApplication()
        {
            GameObject application = GameObject.Find("Jellyfin for RayNeo Application");
            Assert.NotNull(application);
            Assert.NotNull(application.GetComponent<JellyfinAppController>());
            Assert.AreEqual("Main", SceneManager.GetActiveScene().name);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoSavedSession_ShowsQuietLucentStartupWithoutConnectionUi()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            Assert.NotNull(canvas);
            Assert.AreEqual("Jellyfin Spatial Canvas", canvas.name);
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
            BaseRaycaster raycaster = canvas.GetComponent<BaseRaycaster>();
            Assert.NotNull(raycaster);
            Assert.IsInstanceOf<GraphicRaycaster>(raycaster);
            Assert.IsFalse(
                EventSystem.current.sendNavigationEvents,
                "Built-in axis navigation must stay disabled so one remote gesture moves focus once.");
            Air3SDisplayController display = Object.FindObjectOfType<Air3SDisplayController>();
            Assert.NotNull(display);
            Assert.AreSame(display.MonoCamera, canvas.worldCamera);
            Assert.AreEqual(display.CanvasWorldScale, canvas.transform.localScale.x, 0.000001f);

            Transform startup = FindDescendant(canvas.transform, "Startup Screen");
            Transform home = FindDescendant(canvas.transform, "Home Screen");
            Transform startupBrand = FindDescendant(canvas.transform, "Startup Brand");
            Transform startupWordmark = FindDescendant(canvas.transform, "Startup Wordmark");
            Transform startupSpark = FindDescendant(canvas.transform, "Startup Spark Pulse");
            Transform startupHorizon = FindDescendant(canvas.transform, "Startup Horizon");
            Transform loadingCard = FindDescendant(canvas.transform, "Loading Card");
            Transform loadingSignal = FindDescendant(canvas.transform, "Loading Signal Plate");
            Transform loadingDetail = FindDescendant(canvas.transform, "Loading Detail");
            Transform toast = FindDescendant(canvas.transform, "Toast");
            Transform toastSurface = FindDescendant(canvas.transform, "Toast Surface");
            Transform toastAccent = FindDescendant(canvas.transform, "Toast Accent");
            Transform homeContent = FindDescendant(canvas.transform, "Home Content");
            Transform homeViewport = FindDescendant(canvas.transform, "Home Viewport");
            Assert.NotNull(startup);
            Assert.NotNull(home);
            Assert.NotNull(startupBrand);
            Assert.NotNull(startupBrand.GetComponent<UiItemReveal>());
            Assert.NotNull(startupWordmark);
            Assert.AreEqual("LUCENT", startupWordmark.GetComponent<Text>().text);
            Assert.NotNull(startupSpark);
            Assert.NotNull(startupSpark.GetComponent<UiSignalPulse>());
            Assert.NotNull(startupHorizon);
            Assert.NotNull(startupHorizon.GetComponent<UiAmbientFloat>());
            Assert.IsNull(FindDescendant(canvas.transform, "Login Screen"));
            Assert.IsNull(FindDescendant(canvas.transform, "Phone Connection Hint"));
            Assert.IsNull(FindDescendant(canvas.transform, "Phone Connection Card"));
            Assert.IsNull(FindDescendant(canvas.transform, "Phone Connection Visual"));
            Assert.IsNull(FindDescendant(canvas.transform, "Phone Signal Ring 1"));
            Assert.IsNull(FindDescendant(canvas.transform, "Phone Step 1"));
            Assert.IsNull(FindDescendant(canvas.transform, "Connection Signal"));
            Assert.NotNull(loadingCard);
            Assert.NotNull(loadingCard.GetComponent<UiGradient>());
            Assert.NotNull(loadingSignal);
            Assert.NotNull(loadingDetail);
            Assert.NotNull(toast);
            Assert.NotNull(toast.GetComponent<UiViewMotion>());
            Assert.NotNull(toastSurface);
            Assert.NotNull(toastSurface.GetComponent<Outline>());
            Assert.NotNull(toastAccent);
            Assert.NotNull(homeContent);
            Assert.NotNull(homeViewport);
            Assert.IsTrue(startup.gameObject.activeInHierarchy);
            Assert.IsFalse(home.gameObject.activeSelf);
            Assert.AreEqual(0, startup.GetComponentsInChildren<Selectable>(true).Length);
            Assert.AreEqual(0, startup.GetComponentsInChildren<InputField>(true).Length);
            Assert.IsFalse(
                startup.GetComponentsInChildren<Text>(true).Any(text =>
                    text.text.Contains("等待")
                    || text.text.Contains("连接")
                    || text.text.Contains("手机")),
                "The glasses startup must not describe a phone connection workflow.");
            Assert.IsTrue(
                homeContent.GetComponent<VerticalLayoutGroup>().childControlHeight,
                "Home shelves must honor their LayoutElement height instead of collapsing posters into strips.");
            AssertTransparentDragSurface(homeViewport);
            Assert.AreEqual(
                CompanionLoginState.LoginRequired,
                CompanionLoginRuntime.Current.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhoneVolumeChange_ShowsReusableNonInteractiveGlassesOverlay()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            Assert.NotNull(canvas);
            Transform overlay = FindDescendant(canvas.transform, "Volume Overlay");
            Transform percentage = FindDescendant(canvas.transform, "Volume Percentage");
            RectTransform fill = FindDescendant(canvas.transform, "Volume Fill") as RectTransform;
            Assert.NotNull(overlay);
            Assert.NotNull(percentage);
            Assert.NotNull(fill);
            Assert.IsFalse(overlay.gameObject.activeSelf);

            GameObject focusSentinel = new GameObject(
                "Volume Focus Sentinel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            focusSentinel.transform.SetParent(canvas.transform, false);
            EventSystem.current.SetSelectedGameObject(focusSentinel);

            CompanionVolumeRuntime.Submit(47);
            yield return null;
            yield return null;

            Assert.IsTrue(overlay.gameObject.activeInHierarchy);
            Assert.AreEqual("47%", percentage.GetComponent<Text>().text);
            Assert.AreSame(focusSentinel, EventSystem.current.currentSelectedGameObject);
            Assert.That(fill.anchorMax.x, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.47f));
            CanvasGroup group = overlay.GetComponent<CanvasGroup>();
            Assert.NotNull(group);
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);
            Assert.IsTrue(
                overlay.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget),
                "Volume feedback must never intercept glasses navigation or playback input.");

            int overlayInstanceId = overlay.gameObject.GetInstanceID();
            CompanionVolumeRuntime.Submit(100);
            yield return null;
            yield return null;

            Assert.AreEqual(overlayInstanceId, overlay.gameObject.GetInstanceID());
            Assert.AreEqual("100%", percentage.GetComponent<Text>().text);
            Assert.AreSame(focusSentinel, EventSystem.current.currentSelectedGameObject);

            yield return new WaitForSecondsRealtime(2.8f);
            yield return null;
            Assert.IsFalse(overlay.gameObject.activeSelf);
            Assert.AreSame(focusSentinel, EventSystem.current.currentSelectedGameObject);

            Object.Destroy(focusSentinel);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyStates_UseSharedGlassCardsAndLayeredMotion()
        {
            GameObject host = new GameObject("Empty State Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinApiClient api = new JellyfinApiClient("empty-state-test-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "empty-state-token",
                UserId = "empty-state-user",
                DeviceId = "empty-state-test-device"
            });
            JellyfinImageCache imageCache = new JellyfinImageCache();

            HomeView home = new HomeView(host.transform, api, imageCache);
            home.SetSections(new List<JellyfinHomeSection>(), CancellationToken.None);

            BrowseView browse = new BrowseView(host.transform, api, imageCache);
            browse.SetPage(
                JellyfinBrowseState.ForSearch(),
                new JellyfinQueryResult
                {
                    TotalRecordCount = 0,
                    Items = new List<JellyfinItem>()
                },
                CancellationToken.None);

            EpisodeBrowserView episodes = new EpisodeBrowserView(host.transform, api, imageCache);
            episodes.Show(
                new JellyfinItem { Id = "empty-series", Name = "空剧集", Type = "Series" },
                new List<JellyfinItem>(),
                CancellationToken.None);
            yield return null;

            AssertModernEmptyState(host.transform, "Home Empty State");
            AssertModernEmptyState(host.transform, "Browse Empty State");
            AssertModernEmptyState(host.transform, "Episode Empty State");

            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeShelves_AcceptDragFromTransparentBackground()
        {
            GameObject host = new GameObject("Home Drag Test Host", typeof(RectTransform));
            JellyfinApiClient api = new JellyfinApiClient("play-mode-test-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "play-mode-token",
                UserId = "play-mode-user",
                DeviceId = "play-mode-test-device"
            });
            HomeView home = new HomeView(
                host.transform,
                api,
                new JellyfinImageCache());
            home.SetSections(
                new List<JellyfinHomeSection>
                {
                    new JellyfinHomeSection
                    {
                        Key = "latest-movies",
                        Title = "可拖拽",
                        Items = new List<JellyfinItem>
                        {
                            new JellyfinItem { Id = "item-1", Name = "测试影片", Type = "Movie" }
                        }
                    }
                },
                CancellationToken.None);
            yield return null;

            Transform shelf = FindDescendant(host.transform, "Shelf - 可拖拽");
            Assert.NotNull(shelf);
            Transform viewport = FindDescendant(shelf, "Viewport");
            Assert.NotNull(viewport);
            AssertTransparentDragSurface(viewport);

            AxisRoutingScrollRect scroll = shelf.GetComponent<AxisRoutingScrollRect>();
            Assert.NotNull(scroll);
            Assert.IsTrue(scroll.horizontal);
            Assert.AreSame(viewport, scroll.viewport);
            ScrollRect homeScroll = FindDescendant(host.transform, "Home Screen").GetComponent<ScrollRect>();
            Assert.AreSame(
                homeScroll,
                scroll.ParentScrollRect,
                "Vertical drags that begin over a horizontal shelf must continue scrolling the home page.");

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeView_UsesLucentHeroAndExpandableNavigation()
        {
            GameObject host = new GameObject("Lucent Home Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinSession session = new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                ServerName = "海面以下",
                AccessToken = "lucent-home-token",
                UserId = "lucent-home-user",
                UserName = "泠",
                DeviceId = "lucent-home-device"
            };
            JellyfinApiClient api = new JellyfinApiClient(session.DeviceId);
            api.SetSession(session);
            JellyfinImageCache imageCache = new JellyfinImageCache();
            HomeView home = new HomeView(host.transform, api, imageCache);
            JellyfinItem featured = new JellyfinItem
            {
                Id = "lucent-featured",
                Name = "深海回声",
                OriginalTitle = "Echoes Below",
                Type = "Movie",
                ProductionYear = 2026,
                UserData = new JellyfinUserData
                {
                    PlayedPercentage = 38d,
                    PlaybackPositionTicks = AppConstants.TicksPerSecond * 120L
                }
            };
            JellyfinItem playedItem = null;
            long playedPosition = -1L;
            bool libraryRequested = false;
            home.PlayRequested += (item, position) =>
            {
                playedItem = item;
                playedPosition = position;
            };
            home.LibraryRequested += () => libraryRequested = true;
            home.SetHeader(session);
            home.SetSections(
                new List<JellyfinHomeSection>
                {
                    new JellyfinHomeSection
                    {
                        Key = "resume",
                        Title = "继续观看",
                        Items = new List<JellyfinItem> { featured }
                    }
                },
                CancellationToken.None);
            home.Show(true);
            yield return new WaitForSecondsRealtime(0.08f);
            Canvas.ForceUpdateCanvases();

            Transform navigation = FindDescendant(host.transform, "Lucent Side Navigation");
            Transform homeButton = FindDescendant(navigation, "Navigation Home");
            Transform searchButton = FindDescendant(navigation, "Navigation Search");
            Transform profileName = FindDescendant(navigation, "Profile Copy Value");
            Transform serverName = FindDescendant(navigation, "Server Copy Value");
            Transform hero = FindDescendant(host.transform, "Featured Hero");
            Transform grain = FindDescendant(hero, "Film Grain");
            Transform progress = FindDescendant(hero, "Hero Progress");
            Assert.NotNull(navigation);
            Assert.NotNull(navigation.GetComponent<UiSideRailMotion>());
            Assert.NotNull(homeButton);
            Assert.Greater(
                FindDescendant(homeButton, "Active Indicator").GetComponent<Image>().color.a,
                0.5f);
            Assert.AreEqual("泠", profileName.GetComponent<Text>().text);
            Assert.AreEqual("海面以下", serverName.GetComponent<Text>().text);
            Assert.NotNull(hero);
            Assert.NotNull(grain);
            Assert.NotNull(FindDescendant(hero, "Hero Backdrop").GetComponent<UiHeroBreath>());
            Assert.IsTrue(progress.gameObject.activeInHierarchy);
            Assert.AreEqual(
                0.38f,
                FindDescendant(progress, "Hero Progress Fill")
                    .GetComponent<RectTransform>().anchorMax.x,
                0.001f);

            FindDescendant(hero, "Hero Action").GetComponent<Button>().onClick.Invoke();
            Assert.AreSame(featured, playedItem);
            Assert.AreEqual(featured.UserData.PlaybackPositionTicks, playedPosition);
            FindDescendant(navigation, "Navigation Library").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(libraryRequested);

            EventSystem.current.SetSelectedGameObject(searchButton.gameObject);
            yield return new WaitForSecondsRealtime(0.24f);
            Assert.IsTrue(navigation.GetComponent<UiSideRailMotion>().IsExpanded);
            Assert.Greater(
                navigation.GetComponent<RectTransform>().rect.width,
                UiTheme.SideRailWidth + 80f);

            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EpisodeBrowser_PreservesLandscapeEpisodeCards()
        {
            GameObject host = new GameObject("Episode Layout Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinApiClient api = new JellyfinApiClient("episode-layout-test-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "episode-layout-token",
                UserId = "episode-layout-user",
                DeviceId = "episode-layout-test-device"
            });
            JellyfinImageCache imageCache = new JellyfinImageCache();
            EpisodeBrowserView browser = new EpisodeBrowserView(host.transform, api, imageCache);
            browser.Show(
                new JellyfinItem { Id = "series", Name = "测试剧集", Type = "Series" },
                new List<JellyfinItem>
                {
                    CreateEpisode(1, "第一集"),
                    CreateEpisode(2, "第二集"),
                    CreateEpisode(3, "第三集")
                },
                CancellationToken.None);
            yield return null;

            RectTransform seasons = FindDescendant(host.transform, "Seasons") as RectTransform;
            RectTransform shelf = FindDescendant(host.transform, "Season - 测试第一季") as RectTransform;
            Assert.NotNull(seasons);
            Assert.NotNull(shelf);
            LayoutRebuilder.ForceRebuildLayoutImmediate(seasons);
            Canvas.ForceUpdateCanvases();

            RectTransform viewport = FindDescendant(shelf, "Viewport") as RectTransform;
            RectTransform artwork = FindDescendant(shelf, "Artwork Frame") as RectTransform;
            Transform header = FindDescendant(host.transform, "Header");
            Transform activeSeason = FindDescendant(host.transform, "Active Season");
            PosterCardView card = shelf.GetComponentInChildren<PosterCardView>(true);
            AxisRoutingScrollRect horizontalScroll = shelf.GetComponent<AxisRoutingScrollRect>();
            Assert.IsTrue(seasons.GetComponent<VerticalLayoutGroup>().childControlHeight);
            Assert.That(shelf.rect.height, Is.EqualTo(360f).Within(0.5f));
            Assert.NotNull(viewport);
            Assert.That(viewport.rect.height, Is.GreaterThan(250f));
            AssertTransparentDragSurface(viewport);
            Assert.NotNull(artwork);
            Assert.That(artwork.rect.width / artwork.rect.height, Is.EqualTo(16f / 9f).Within(0.01f));
            Assert.NotNull(header);
            Assert.NotNull(header.GetComponent<UiGradient>());
            Assert.NotNull(activeSeason);
            Assert.IsTrue(activeSeason.gameObject.activeInHierarchy);
            Assert.NotNull(card);
            Assert.AreEqual(PosterCardView.LandscapeHeight, card.GetComponent<LayoutElement>().preferredHeight);
            Assert.NotNull(horizontalScroll);
            Assert.IsTrue(horizontalScroll.ParentScrollRect.vertical);

            browser.Hide();
            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AxisRoutingScrollRect_RoutesDragByDominantDirection()
        {
            GameObject root = new GameObject("Nested Scroll Test", typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(800f, 600f);

            ScrollRect parentScroll = root.AddComponent<ScrollRect>();
            parentScroll.viewport = rootRect;
            parentScroll.horizontal = false;
            parentScroll.vertical = true;
            parentScroll.movementType = ScrollRect.MovementType.Unrestricted;
            RectTransform parentContent = CreateTestRect(
                "Parent Content",
                rootRect,
                new Vector2(800f, 1400f));
            parentScroll.content = parentContent;

            RectTransform childViewport = CreateTestRect(
                "Child Viewport",
                parentContent,
                new Vector2(800f, 300f));
            AxisRoutingScrollRect childScroll = childViewport.gameObject.AddComponent<AxisRoutingScrollRect>();
            childScroll.viewport = childViewport;
            childScroll.horizontal = true;
            childScroll.vertical = false;
            childScroll.movementType = ScrollRect.MovementType.Unrestricted;
            RectTransform childContent = CreateTestRect(
                "Child Content",
                childViewport,
                new Vector2(1600f, 300f));
            childScroll.content = childContent;
            childScroll.ConfigureParent(parentScroll);
            yield return null;

            Vector2 parentStart = parentContent.anchoredPosition;
            Vector2 childStart = childContent.anchoredPosition;
            PerformDrag(
                childContent.gameObject,
                new Vector2(400f, 500f),
                new Vector2(390f, 430f),
                new Vector2(390f, 250f));
            Assert.That(
                (parentContent.anchoredPosition - parentStart).sqrMagnitude,
                Is.GreaterThan(1f),
                "A vertical drag over the shelf must move the parent page.");
            Assert.That(childContent.anchoredPosition, Is.EqualTo(childStart));

            parentContent.anchoredPosition = parentStart;
            childContent.anchoredPosition = childStart;
            PerformDrag(
                childContent.gameObject,
                new Vector2(600f, 300f),
                new Vector2(520f, 290f),
                new Vector2(280f, 290f));
            Assert.That(
                (childContent.anchoredPosition - childStart).sqrMagnitude,
                Is.GreaterThan(1f),
                "A horizontal drag must remain on the shelf.");
            Assert.That(parentContent.anchoredPosition, Is.EqualTo(parentStart));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DetailView_EmbedsEpisodesAndPlaysNextEpisodeDirectly()
        {
            GameObject host = new GameObject("Integrated Detail Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinApiClient api = new JellyfinApiClient("integrated-detail-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "integrated-detail-token",
                UserId = "integrated-detail-user",
                DeviceId = "integrated-detail-device"
            });
            JellyfinImageCache imageCache = new JellyfinImageCache();
            DetailView detail = new DetailView(host.transform);
            JellyfinItem episodeOne = CreateEpisode(1, "第一集");
            episodeOne.UserData.Played = true;
            JellyfinItem episodeTwo = CreateEpisode(2, "第二集");
            episodeTwo.UserData.PlaybackPositionTicks = AppConstants.TicksPerSecond * 45L;
            episodeTwo.UserData.PlayedPercentage = 35d;
            episodeTwo.UserData.LastPlayedDate = "2026-09-02T10:00:00Z";
            JellyfinItem episodeThree = CreateEpisode(3, "第三集");
            JellyfinItem playedItem = null;
            long playedPosition = -1L;
            detail.PlayRequested += (item, position) =>
            {
                playedItem = item;
                playedPosition = position;
            };

            detail.Show(
                new JellyfinItem
                {
                    Id = "series",
                    Name = "测试剧集",
                    Type = "Series",
                    Overview = new string('介', 220),
                    UserData = new JellyfinUserData()
                },
                api,
                imageCache,
                CancellationToken.None,
                new List<JellyfinItem> { episodeThree, episodeOne, episodeTwo });
            yield return null;
            Canvas.ForceUpdateCanvases();

            Transform shelf = FindDescendant(host.transform, "Episode Shelf");
            Assert.NotNull(shelf);
            Transform viewport = FindDescendant(shelf, "Episode Viewport");
            Transform artwork = FindDescendant(shelf, "Artwork Frame");
            Button continueButton = FindDescendant(host.transform, "Continue").GetComponent<Button>();
            Text overview = FindDescendant(host.transform, "Overview").GetComponent<Text>();
            Assert.IsTrue(shelf.gameObject.activeInHierarchy);
            Assert.NotNull(viewport);
            AssertTransparentDragSurface(viewport);
            Assert.NotNull(artwork);
            Assert.That(
                ((RectTransform)artwork).rect.width / ((RectTransform)artwork).rect.height,
                Is.EqualTo(16f / 9f).Within(0.01f));
            Assert.AreEqual("继续 S1E2 · 第二集", continueButton.GetComponentInChildren<Text>().text);
            Assert.AreEqual("Hero Information", overview.transform.parent.name);
            Assert.AreEqual(VerticalWrapMode.Truncate, overview.verticalOverflow);
            StringAssert.EndsWith("…", overview.text);
            Assert.LessOrEqual(overview.text.Length, 150);
            Assert.IsNull(FindDescendant(host.transform, "Next Episode"));
            Assert.IsNull(FindDescendant(host.transform, "Episodes"));

            AxisRoutingScrollRect episodeScroll = viewport.GetComponent<AxisRoutingScrollRect>();
            ScrollRect detailScroll = FindDescendant(host.transform, "Detail Scroll").GetComponent<ScrollRect>();
            Assert.AreSame(detailScroll, episodeScroll.ParentScrollRect);
            continueButton.onClick.Invoke();
            Assert.AreSame(episodeTwo, playedItem);
            Assert.AreEqual(episodeTwo.UserData.PlaybackPositionTicks, playedPosition);

            PosterCardView thirdCard = shelf
                .GetComponentsInChildren<PosterCardView>(true)
                .First(card => FindDescendant(card.transform, "Title").GetComponent<Text>().text == "S1E3 · 第三集");
            thirdCard.GetComponent<Button>().onClick.Invoke();
            Assert.AreSame(episodeThree, playedItem);
            Assert.AreEqual(0L, playedPosition);

            detail.Hide();
            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DetailView_UsesScrollableContentDrivenLayout()
        {
            GameObject host = new GameObject("Detail Layout Test Host", typeof(RectTransform));
            DetailView detail = new DetailView(host.transform);
            yield return null;

            Transform scrollTransform = FindDescendant(host.transform, "Detail Scroll");
            Transform viewport = FindDescendant(host.transform, "Detail Viewport");
            Transform content = FindDescendant(host.transform, "Detail Content");
            Transform overview = FindDescendant(host.transform, "Overview");
            Transform actions = FindDescendant(host.transform, "Detail Actions");
            Transform continueButton = FindDescendant(host.transform, "Continue");
            Transform metadataChips = FindDescendant(host.transform, "Metadata Chips");
            Transform detailTabs = FindDescendant(host.transform, "Detail Tabs");
            Transform tabStage = FindDescendant(host.transform, "Detail Tab Stage");
            Transform informationPanel = FindDescendant(host.transform, "Detail Information Panel");
            Assert.NotNull(scrollTransform);
            Assert.NotNull(viewport);
            Assert.NotNull(content);
            Assert.NotNull(overview);
            Assert.NotNull(actions);
            Assert.NotNull(continueButton);
            Assert.NotNull(metadataChips);
            Assert.NotNull(detailTabs);
            Assert.NotNull(tabStage);
            Assert.NotNull(tabStage.GetComponent<Outline>());
            Assert.NotNull(informationPanel);

            ScrollRect scroll = scrollTransform.GetComponent<ScrollRect>();
            Assert.NotNull(scroll);
            Assert.IsTrue(scroll.vertical);
            Assert.IsFalse(scroll.horizontal);
            Assert.AreSame(viewport, scroll.viewport);
            Assert.AreSame(content, scroll.content);
            AssertTransparentDragSurface(viewport);
            Assert.NotNull(content.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(
                ContentSizeFitter.FitMode.PreferredSize,
                content.GetComponent<ContentSizeFitter>().verticalFit);
            Assert.AreEqual(
                VerticalWrapMode.Truncate,
                overview.GetComponent<Text>().verticalOverflow,
                "The hero summary must stay compact like Jellyfin Web.");
            Assert.AreEqual("Hero Information", overview.parent.name);
            Assert.Less(
                overview.GetSiblingIndex(),
                actions.GetSiblingIndex(),
                "The compact summary must lead into progress and playback actions like the template.");
            Assert.IsTrue(
                actions.GetComponent<HorizontalLayoutGroup>().childControlWidth,
                "Action buttons must use their LayoutElement widths instead of Unity's 100 px default.");
            Assert.AreEqual(0f, actions.GetComponent<LayoutElement>().flexibleHeight);
            Assert.AreEqual(420f, continueButton.GetComponent<LayoutElement>().preferredWidth);
            Assert.IsTrue(metadataChips.GetComponent<HorizontalLayoutGroup>().childControlWidth);
            Assert.AreEqual(0f, metadataChips.GetComponent<LayoutElement>().flexibleHeight);
            Assert.NotNull(detailTabs.GetComponent<HorizontalLayoutGroup>());
            Assert.AreEqual(
                ContentSizeFitter.FitMode.PreferredSize,
                informationPanel.GetComponent<ContentSizeFitter>().verticalFit);
            Assert.NotNull(FindDescendant(host.transform, "Favorite"));
            Assert.NotNull(FindDescendant(host.transform, "Played"));

            detail.Hide();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PosterCards_KeepPortraitAndLandscapeArtworkRatios()
        {
            GameObject host = new GameObject("Poster Card Test Host", typeof(RectTransform));
            PosterCardView portrait = PosterCardView.Create(host.transform);
            PosterCardView landscape = PosterCardView.Create(host.transform, true);
            yield return null;

            RectTransform portraitArtwork = FindDescendant(portrait.transform, "Artwork Frame") as RectTransform;
            RectTransform landscapeArtwork = FindDescendant(landscape.transform, "Artwork Frame") as RectTransform;
            Assert.NotNull(portraitArtwork);
            Assert.NotNull(landscapeArtwork);
            Assert.That(
                portraitArtwork.rect.width / portraitArtwork.rect.height,
                Is.EqualTo(2f / 3f).Within(0.01f));
            Assert.That(
                landscapeArtwork.rect.width / landscapeArtwork.rect.height,
                Is.EqualTo(16f / 9f).Within(0.01f));

            JellyfinImageCache imageCache = new JellyfinImageCache();
            landscape.Bind(
                new JellyfinItem
                {
                    Name = "没有剧照的第二集",
                    Type = "Episode",
                    MediaType = "Video",
                    ParentIndexNumber = 1,
                    IndexNumber = 2,
                    UserData = new JellyfinUserData()
                },
                new JellyfinApiClient("placeholder-test-device"),
                imageCache,
                null,
                CancellationToken.None);
            yield return null;

            Transform placeholderLayer = FindDescendant(
                landscape.transform,
                "Artwork Placeholder Layer");
            UiArtworkPlaceholderMotion placeholderMotion =
                landscapeArtwork.GetComponent<UiArtworkPlaceholderMotion>();
            Assert.NotNull(placeholderLayer);
            Assert.NotNull(placeholderMotion);
            Assert.IsFalse(placeholderMotion.IsLoading);
            Assert.AreEqual(
                "S1E2",
                FindDescendant(placeholderLayer, "Artwork Placeholder")
                    .GetComponent<Text>().text);
            Assert.AreEqual(
                "暂无画面",
                FindDescendant(placeholderLayer, "Artwork Placeholder Caption")
                    .GetComponent<Text>().text);
            Assert.IsFalse(
                FindDescendant(placeholderLayer, "Artwork Placeholder Shimmer")
                    .gameObject.activeSelf);

            placeholderMotion.ShowLoading();
            Assert.IsTrue(placeholderMotion.IsLoading);
            Assert.IsTrue(
                FindDescendant(placeholderLayer, "Artwork Placeholder Shimmer")
                    .gameObject.activeSelf);
            placeholderMotion.Complete(0.04f);
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.IsFalse(placeholderLayer.gameObject.activeSelf);

            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrowseView_RendersPagedCourseFoldersForDirectionalNavigation()
        {
            GameObject host = new GameObject("Browse Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinApiClient api = new JellyfinApiClient("browse-test-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "browse-token",
                UserId = "browse-user",
                DeviceId = "browse-test-device"
            });
            JellyfinImageCache imageCache = new JellyfinImageCache();
            BrowseView browse = new BrowseView(host.transform, api, imageCache);
            JellyfinItem selected = null;
            browse.ItemSelected += item => selected = item;
            JellyfinItem folder = new JellyfinItem
            {
                Id = "folder",
                Name = "2023透视",
                Type = "Folder",
                ChildCount = 23,
                UserData = new JellyfinUserData { UnplayedItemCount = 8 }
            };
            browse.SetPage(
                JellyfinBrowseState.ForLibrary(new JellyfinItem
                {
                    Id = "courses",
                    Name = "网课",
                    Type = "CollectionFolder",
                    CollectionType = "homevideos"
                }),
                new JellyfinQueryResult
                {
                    TotalRecordCount = 31,
                    StartIndex = 0,
                    Items = new List<JellyfinItem>
                    {
                        folder,
                        new JellyfinItem
                        {
                            Id = "video",
                            Name = "第一堂课",
                            Type = "Video",
                            MediaType = "Video",
                            UserData = new JellyfinUserData()
                        }
                    }
                },
                CancellationToken.None);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Transform viewport = FindDescendant(host.transform, "Browse Viewport");
            GridLayoutGroup grid = FindDescendant(host.transform, "Browse Grid")
                .GetComponent<GridLayoutGroup>();
            PosterCardView[] cards = host.GetComponentsInChildren<PosterCardView>(true);
            Assert.NotNull(viewport);
            AssertTransparentDragSurface(viewport);
            Assert.AreEqual(4, grid.constraintCount);
            Assert.AreEqual(PosterCardView.LandscapeWidth, grid.cellSize.x);
            Assert.AreEqual(2, cards.Length);
            Assert.IsTrue(cards.All(card =>
                card.GetComponent<LayoutElement>().preferredHeight == PosterCardView.LandscapeHeight));
            Assert.AreEqual(
                "文件夹",
                FindDescendant(cards[0].transform, "Type Badge Label").GetComponent<Text>().text);
            Assert.AreEqual(
                "8 未看",
                FindDescendant(cards[0].transform, "Status Badge Label").GetComponent<Text>().text);
            Assert.IsFalse(FindDescendant(host.transform, "Previous Page").GetComponent<Button>().interactable);
            Assert.IsTrue(FindDescendant(host.transform, "Next Page").GetComponent<Button>().interactable);

            cards[0].GetComponent<Button>().onClick.Invoke();
            Assert.AreSame(folder, selected);

            browse.Hide();
            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrowseView_SearchUsesGlassesAlphabetWithoutTextInput()
        {
            GameObject host = new GameObject("Initial Search Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinApiClient api = new JellyfinApiClient("initial-search-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "initial-search-token",
                UserId = "initial-search-user",
                DeviceId = "initial-search-device"
            });
            JellyfinImageCache imageCache = new JellyfinImageCache();
            BrowseView browse = new BrowseView(host.transform, api, imageCache);
            string selectedInitial = null;
            browse.SearchInitialSelected += initial => selectedInitial = initial;
            browse.SetPage(
                JellyfinBrowseState.ForSearch(),
                new JellyfinQueryResult(),
                CancellationToken.None);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Transform alphabet = FindDescendant(host.transform, "Search Alphabet");
            Transform keyboard = FindDescendant(alphabet, "Search Keyboard");
            Transform navigation = FindDescendant(host.transform, "Lucent Side Navigation");
            Assert.NotNull(alphabet);
            Assert.IsTrue(alphabet.gameObject.activeInHierarchy);
            Assert.NotNull(alphabet.GetComponent<UiGradient>());
            Assert.NotNull(alphabet.GetComponent<Outline>());
            Assert.NotNull(alphabet.GetComponent<Shadow>());
            Assert.NotNull(keyboard);
            Assert.AreEqual(10, keyboard.GetComponent<GridLayoutGroup>().constraintCount);
            Assert.AreEqual(28, alphabet.GetComponentsInChildren<Button>(true).Length);
            Assert.AreEqual(0, host.GetComponentsInChildren<InputField>(true).Length);
            Assert.AreEqual(
                "选择首字母  ·  中文拼音 / ENGLISH",
                FindDescendant(host.transform, "Browse Count").GetComponent<Text>().text);
            RectTransform alphabetRect = alphabet.GetComponent<RectTransform>();
            RectTransform lastInitial = FindDescendant(host.transform, "Search Initial Other")
                .GetComponent<RectTransform>();
            Bounds lastInitialBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                alphabetRect,
                lastInitial);
            Assert.LessOrEqual(lastInitialBounds.max.x, alphabetRect.rect.xMax + 0.5f);
            Assert.Less(
                lastInitialBounds.center.y,
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    alphabetRect,
                    FindDescendant(host.transform, "Search Initial A") as RectTransform).center.y,
                "The initials should read as a remote-first keyboard instead of one compressed row.");
            Assert.NotNull(navigation);
            Assert.Greater(
                FindDescendant(
                    FindDescendant(navigation, "Navigation Search"),
                    "Active Indicator")
                    .GetComponent<Image>().color.a,
                0.5f);

            DirectionalFocusNavigator navigator = new DirectionalFocusNavigator();
            navigator.SetScope(browse.FocusRoot);
            Assert.IsTrue(navigator.SelectPreferred("Search Initial A"));
            Assert.AreEqual("Search Initial A", EventSystem.current.currentSelectedGameObject.name);
            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Right));
            Assert.AreEqual("Search Initial B", EventSystem.current.currentSelectedGameObject.name);
            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Submit));
            Assert.AreEqual("B", selectedInitial);

            browse.Hide();
            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DetailView_ProvidesExpandableOverviewSeasonsAndSimilarItems()
        {
            GameObject host = new GameObject("Related Detail Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            JellyfinApiClient api = new JellyfinApiClient("related-detail-device");
            api.SetSession(new JellyfinSession
            {
                ServerUrl = "http://127.0.0.1:8096",
                AccessToken = "related-token",
                UserId = "related-user",
                DeviceId = "related-detail-device"
            });
            JellyfinImageCache imageCache = new JellyfinImageCache();
            DetailView detail = new DetailView(host.transform);
            JellyfinItem selected = null;
            long chapterPosition = -1L;
            detail.RelatedItemSelected += item => selected = item;
            detail.PlayRequested += (item, position) => chapterPosition = position;
            JellyfinItem season = new JellyfinItem
            {
                Id = "season-1",
                Name = "第一季",
                Type = "Season",
                UserData = new JellyfinUserData()
            };
            JellyfinItem similar = new JellyfinItem
            {
                Id = "similar",
                Name = "相似影片",
                Type = "Movie",
                UserData = new JellyfinUserData()
            };
            detail.Show(
                new JellyfinItem
                {
                    Id = "series-related",
                    Name = "测试剧集",
                    Type = "Movie",
                    Overview = new string('介', 240),
                    Chapters = new List<JellyfinChapter>
                    {
                        new JellyfinChapter
                        {
                            Name = "第二幕",
                            StartPositionTicks = AppConstants.TicksPerSecond * 403L
                        }
                    },
                    UserData = new JellyfinUserData()
                },
                api,
                imageCache,
                CancellationToken.None,
                new List<JellyfinItem>(),
                new List<JellyfinItem> { season },
                new List<JellyfinItem> { similar });
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.IsTrue(FindDescendant(host.transform, "Seasons Shelf").gameObject.activeInHierarchy);
            Assert.IsTrue(
                FindDescendant(host.transform, "Detail Episodes Panel").gameObject.activeInHierarchy);
            Assert.IsFalse(
                FindDescendant(host.transform, "Detail Related Panel").gameObject.activeInHierarchy);
            Assert.IsFalse(FindDescendant(host.transform, "Similar Shelf").gameObject.activeInHierarchy);
            Button chapter = FindDescendant(host.transform, "Chapter - 1").GetComponent<Button>();
            Assert.AreEqual("6:43  ·  第二幕", chapter.GetComponentInChildren<Text>().text);
            chapter.onClick.Invoke();
            Assert.AreEqual(AppConstants.TicksPerSecond * 403L, chapterPosition);
            Button overviewToggle = FindDescendant(host.transform, "Overview Toggle").GetComponent<Button>();
            Assert.IsTrue(overviewToggle.gameObject.activeInHierarchy);
            overviewToggle.onClick.Invoke();
            Assert.IsTrue(FindDescendant(host.transform, "Expanded Overview Card").gameObject.activeInHierarchy);
            Assert.IsTrue(
                FindDescendant(host.transform, "Detail Information Panel").gameObject.activeInHierarchy,
                "Expanding the full overview should reveal its information tab panel.");

            FindDescendant(host.transform, "Detail Tab Related").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(
                FindDescendant(host.transform, "Detail Related Panel").gameObject.activeInHierarchy);
            Assert.IsTrue(FindDescendant(host.transform, "Similar Shelf").gameObject.activeInHierarchy);
            PosterCardView similarCard = FindDescendant(host.transform, "Similar Shelf")
                .GetComponentInChildren<PosterCardView>(true);
            similarCard.GetComponent<Button>().onClick.Invoke();
            Assert.AreSame(similar, selected);

            detail.Hide();
            imageCache.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainScene_ConfiguresSelectableAir3SDisplayModes()
        {
            Air3SDisplayController display = Object.FindObjectOfType<Air3SDisplayController>();
            Assert.NotNull(display);
            Assert.AreSame(display.MonoCamera, Camera.main);
            Assert.AreEqual(Air3SDisplayMode.Mirror2D, display.ActiveMode);
            Assert.IsTrue(display.MonoCamera.enabled);
            Assert.IsFalse(display.LeftEyeCamera.enabled);
            Assert.IsFalse(display.RightEyeCamera.enabled);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), display.MonoCamera.rect);
            Assert.AreEqual(Air3SDisplayController.PerEyeAspect, display.MonoCamera.aspect, 0.001f);
            Canvas displayCanvas = Object.FindObjectOfType<Canvas>();
            AssertPerEyeCanvasFillsViewport(display.MonoCamera, displayCanvas);

            Assert.NotNull(EventSystem.current);
            BaseInputModule[] inputModules = EventSystem.current.GetComponents<BaseInputModule>();
            Assert.IsTrue(
                inputModules.Any(module => module is StandaloneInputModule && module.enabled));
            Assert.IsFalse(
                inputModules.Any(module =>
                    module.GetType().FullName == "FfalconXR.InputModule.XRInputModule"
                    && module.enabled));

            RayNeoEditorInputSimulator simulator =
                Object.FindObjectOfType<RayNeoEditorInputSimulator>();
            Assert.NotNull(simulator);
            Assert.IsTrue(simulator.enabled);

            display.SetMode(Air3SDisplayMode.StereoVirtualScreen);
            yield return null;

            Assert.AreEqual(Air3SDisplayMode.StereoVirtualScreen, display.ActiveMode);
            Assert.IsFalse(display.MonoCamera.enabled);
            Assert.IsTrue(display.LeftEyeCamera.enabled);
            Assert.IsTrue(display.RightEyeCamera.enabled);
            Assert.AreEqual(new Rect(0f, 0f, 0.5f, 1f), display.LeftEyeCamera.rect);
            Assert.AreEqual(new Rect(0.5f, 0f, 0.5f, 1f), display.RightEyeCamera.rect);
            Assert.AreEqual(
                Air3SDisplayController.PerEyeAspect,
                display.LeftEyeCamera.aspect,
                0.001f,
                "Each squeezed SBS half must retain a 16:9 projection to avoid horizontal cropping.");
            Assert.AreEqual(
                Air3SDisplayController.PerEyeAspect,
                display.RightEyeCamera.aspect,
                0.001f);
            Assert.AreEqual(
                -display.InterpupillaryDistance * 0.5f,
                display.LeftEyeCamera.transform.localPosition.x,
                0.0001f);
            Assert.AreEqual(
                display.InterpupillaryDistance * 0.5f,
                display.RightEyeCamera.transform.localPosition.x,
                0.0001f);
            Assert.AreEqual(
                -display.LeftEyeCamera.projectionMatrix.m02,
                display.RightEyeCamera.projectionMatrix.m02,
                0.0001f,
                "The off-axis frustums must converge on the full-size virtual screen.");
            Assert.AreNotEqual(0f, display.LeftEyeCamera.projectionMatrix.m02);
            AssertPerEyeCanvasFillsViewport(display.LeftEyeCamera, displayCanvas);
            AssertPerEyeCanvasFillsViewport(display.RightEyeCamera, displayCanvas);
            Assert.IsFalse(HasHeadTrackingComponent(display.MonoCamera));
            Assert.IsFalse(HasHeadTrackingComponent(display.LeftEyeCamera));
            Assert.IsFalse(HasHeadTrackingComponent(display.RightEyeCamera));

            display.SetMode(Air3SDisplayMode.Mirror2D);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteNavigation_UsesTheAdjacentVisualRowAndVisibleFallback()
        {
            GameObject canvasObject = new GameObject(
                "Navigation Test Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform scope = CreateTestRect(
                "Navigation Scope",
                canvasObject.transform,
                new Vector2(1920f, 1080f));
            scope.gameObject.AddComponent<RectMask2D>();

            Button current = CreateNavigationButton(
                "Current",
                scope,
                new Vector2(700f, 250f));
            Button sameRowPrevious = CreateNavigationButton(
                "Same Row Previous",
                scope,
                new Vector2(490f, 250f));
            Button adjacentRow = CreateNavigationButton(
                "Adjacent Row",
                scope,
                new Vector2(-700f, 40f));
            CreateNavigationButton(
                "Far Aligned Row",
                scope,
                new Vector2(700f, -300f));
            Button visibleDuplicate = CreateNavigationButton(
                "Preferred Duplicate",
                scope,
                new Vector2(-300f, 0f));
            CreateNavigationButton(
                "Preferred Duplicate",
                scope,
                new Vector2(-300f, 4000f));
            Button outsideScope = CreateNavigationButton(
                "Underlying Page Control",
                canvasObject.transform,
                new Vector2(0f, 0f));
            Canvas.ForceUpdateCanvases();
            yield return null;

            DirectionalFocusNavigator navigator = new DirectionalFocusNavigator();
            navigator.SetScope(scope);
            EventSystem.current.SetSelectedGameObject(current.gameObject);

            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Left));
            Assert.AreSame(
                sameRowPrevious.gameObject,
                EventSystem.current.currentSelectedGameObject,
                "Horizontal movement must stay on the current visual row.");

            EventSystem.current.SetSelectedGameObject(current.gameObject);
            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Down));
            Assert.AreSame(
                adjacentRow.gameObject,
                EventSystem.current.currentSelectedGameObject,
                "Vertical movement must enter the immediately adjacent row before a farther aligned row.");

            Assert.IsTrue(navigator.SelectPreferred("Preferred Duplicate"));
            Assert.AreSame(
                visibleDuplicate.gameObject,
                EventSystem.current.currentSelectedGameObject,
                "Selection recovery must prefer an on-screen control over a clipped duplicate.");

            EventSystem.current.SetSelectedGameObject(outsideScope.gameObject);
            navigator.SetScope(scope);
            Assert.IsNull(
                EventSystem.current.currentSelectedGameObject,
                "Reapplying the same scope must evict stale focus from an underlying page.");

            Object.Destroy(canvasObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteNavigation_PrefersContentFlowOverFixedOverlay()
        {
            GameObject canvasObject = new GameObject(
                "Navigation Context Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            RectTransform scope = CreateTestRect(
                "Navigation Context Scope",
                canvasObject.transform,
                new Vector2(1920f, 1080f));
            RectTransform viewport = CreateTestRect(
                "Navigation Context Viewport",
                scope,
                new Vector2(1920f, 1080f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateTestRect(
                "Navigation Context Content",
                viewport,
                new Vector2(1920f, 1080f));
            ScrollRect scroll = scope.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;

            Button current = CreateNavigationButton(
                "Current Content Item",
                content,
                new Vector2(520f, 250f));
            Button nextContentRow = CreateNavigationButton(
                "Next Content Row",
                content,
                new Vector2(-520f, 40f));
            Button fixedOverlay = CreateNavigationButton(
                "Fixed Overlay Intruder",
                scope,
                new Vector2(520f, 150f));
            current.gameObject.AddComponent<FocusScale>();
            nextContentRow.gameObject.AddComponent<FocusScale>();
            fixedOverlay.gameObject.AddComponent<FocusScale>();
            Canvas.ForceUpdateCanvases();
            yield return null;

            DirectionalFocusNavigator navigator = new DirectionalFocusNavigator();
            navigator.SetScope(scope);
            EventSystem.current.SetSelectedGameObject(current.gameObject);

            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Down));
            Assert.AreSame(
                nextContentRow.gameObject,
                EventSystem.current.currentSelectedGameObject,
                "A fixed header or overlay must not intercept movement between adjacent content rows.");

            Object.Destroy(canvasObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerRemoteNavigation_IsScopedAndConsumesHorizontalSeek()
        {
            GameObject host = new GameObject("Player Navigation Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            GameObject underlying = new GameObject(
                "Underlying Details Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            underlying.transform.SetParent(host.transform, false);

            PlayerView player = new PlayerView(host.transform);
            Transform playerRoot = FindDescendant(host.transform, "Player Screen");
            Assert.NotNull(playerRoot);
            playerRoot.GetComponent<UiViewMotion>().SetVisibleImmediately(true);
            Canvas.ForceUpdateCanvases();
            yield return null;

            DirectionalFocusNavigator navigator = new DirectionalFocusNavigator();
            EventSystem.current.SetSelectedGameObject(underlying);
            Assert.IsFalse(player.ControlsVisible);
            Assert.IsTrue(player.HandleRemoteCommand(
                CompanionRemoteCommand.Left,
                navigator));
            Assert.IsFalse(
                player.ControlsVisible,
                "Seeking while controls are hidden must keep the full-screen bars hidden.");
            Assert.AreNotSame(
                underlying,
                EventSystem.current.currentSelectedGameObject,
                "Opening playback input scope must immediately clear underlying page focus.");
            Assert.IsTrue(
                FindDescendant(playerRoot, "Seek Feedback").gameObject.activeInHierarchy,
                "Horizontal remote input must be consumed by player seek feedback.");

            Assert.IsTrue(player.HandleRemoteCommand(
                CompanionRemoteCommand.Down,
                navigator));
            Assert.IsTrue(player.ControlsVisible);
            Assert.AreEqual(
                "Play Pause",
                EventSystem.current.currentSelectedGameObject.name);

            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Up));
            Assert.AreEqual("Back", EventSystem.current.currentSelectedGameObject.name);
            Assert.IsTrue(navigator.Handle(CompanionRemoteCommand.Down));
            Assert.AreEqual("Play Pause", EventSystem.current.currentSelectedGameObject.name);

            Assert.AreNotSame(
                underlying,
                EventSystem.current.currentSelectedGameObject,
                "Playback focus must never escape into the page behind the video.");

            RectTransform videoSurface = FindDescendant(playerRoot, "Video Surface")
                as RectTransform;
            Assert.NotNull(videoSurface);
            Assert.AreEqual(
                AspectRatioFitter.AspectMode.FitInParent,
                videoSurface.GetComponent<AspectRatioFitter>().aspectMode);
            Assert.AreEqual(1920f, videoSurface.rect.width, 1f);
            Assert.AreEqual(1080f, videoSurface.rect.height, 1f);

            player.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerSeek_PreservesRewindTargetUntilEngineConfirms()
        {
            GameObject host = new GameObject("Player Seek Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            FakePlaybackEngine hardwareEngine = new FakePlaybackEngine();
            FakePlaybackEngine libVlcEngine = new FakePlaybackEngine();
            FakePlaybackEngine softwareEngine = new FakePlaybackEngine();
            PlayerView player = new PlayerView(
                host.transform,
                hardwareEngine,
                libVlcEngine,
                softwareEngine);
            Task playback = player.PrepareAndPlayAsync(
                new JellyfinPlaybackPlan
                {
                    Item = new JellyfinItem { Id = "seek-test", Name = "Seek Test" },
                    Url = "https://example.invalid/video.mp4",
                    Tier = PlaybackTier.HardwareDirect,
                    StartPositionTicks = 100L * AppConstants.TicksPerSecond
                },
                CancellationToken.None);
            while (!playback.IsCompleted)
            {
                yield return null;
            }
            Assert.IsFalse(playback.IsFaulted, playback.Exception?.ToString());

            hardwareEngine.PositionSeconds = 100d;
            player.Update();
            DirectionalFocusNavigator navigator = new DirectionalFocusNavigator();

            Assert.IsTrue(player.HandleRemoteCommand(
                CompanionRemoteCommand.Left,
                navigator));
            Assert.AreEqual(90d, hardwareEngine.LastSeekSeconds, 0.001d);
            Assert.AreEqual(
                90L * AppConstants.TicksPerSecond,
                player.CurrentPositionTicks,
                "The stale pre-seek engine position must not overwrite a rewind target.");

            Assert.IsTrue(player.HandleRemoteCommand(
                CompanionRemoteCommand.Left,
                navigator));
            Assert.AreEqual(
                80d,
                hardwareEngine.LastSeekSeconds,
                0.001d,
                "Rapid gestures must accumulate from the pending seek target.");
            Assert.AreEqual(
                80L * AppConstants.TicksPerSecond,
                player.CurrentPositionTicks);

            hardwareEngine.PositionSeconds = 80.4d;
            player.Update();
            Assert.AreEqual(
                80.4d * AppConstants.TicksPerSecond,
                player.CurrentPositionTicks,
                AppConstants.TicksPerSecond * 0.01d,
                "Once the decoder confirms the seek, reporting should follow its real clock.");

            hardwareEngine.PositionSeconds = 81.25d;
            player.Update();
            Assert.AreEqual(
                81.25d * AppConstants.TicksPerSecond,
                player.CurrentPositionTicks,
                AppConstants.TicksPerSecond * 0.01d);

            player.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DetailView_UsesReadableGlassHeroAndLayeredMotion()
        {
            GameObject host = new GameObject("Detail Motion Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            DetailView detail = new DetailView(host.transform);
            detail.Show(
                new JellyfinItem
                {
                    Id = "detail-motion-item",
                    Name = "动效详情",
                    OriginalTitle = "Motion Detail",
                    Type = "Movie",
                    MediaType = "Video",
                    Overview = "用于验证亮色背景下的玻璃信息层和分层入场动效。",
                    Genres = new List<string> { "剧情" },
                    UserData = new JellyfinUserData()
                },
                null,
                null,
                CancellationToken.None);
            yield return null;

            Transform detailRoot = FindDescendant(host.transform, "Detail Screen");
            Transform backdrop = FindDescendant(detailRoot, "Backdrop");
            Transform backdropShade = FindDescendant(detailRoot, "Backdrop Content Shade");
            Transform glass = FindDescendant(detailRoot, "Hero Information Glass");
            Transform heroGlow = FindDescendant(detailRoot, "Hero Information Glow");
            Transform heroAccent = FindDescendant(detailRoot, "Hero Information Accent");
            Transform poster = FindDescendant(detailRoot, "Poster Frame");
            Transform posterPlaceholder = FindDescendant(
                detailRoot,
                "Poster Placeholder Layer");
            Transform information = FindDescendant(detailRoot, "Hero Information");
            Transform keyArtShade = FindDescendant(detailRoot, "Key Art Left Shade");
            Transform keyArtSpectrum = FindDescendant(detailRoot, "Key Art Spectrum");
            Transform detailTabs = FindDescendant(detailRoot, "Detail Tabs");
            Transform tabStage = FindDescendant(detailRoot, "Detail Tab Stage");
            Transform episodesPanel = FindDescendant(detailRoot, "Detail Episodes Panel");
            Transform informationPanel = FindDescendant(detailRoot, "Detail Information Panel");
            Transform facts = FindDescendant(detailRoot, "Details Card");
            Transform continueButton = FindDescendant(detailRoot, "Continue");
            Assert.NotNull(backdrop);
            Assert.NotNull(backdrop.GetComponent<UiHeroBreath>());
            Assert.NotNull(backdropShade);
            Assert.NotNull(backdropShade.GetComponent<UiGradient>());
            Assert.NotNull(glass);
            Assert.IsFalse(glass.GetComponent<Image>().raycastTarget);
            Assert.IsTrue(glass.GetComponent<LayoutElement>().ignoreLayout);
            Assert.NotNull(heroGlow);
            Assert.NotNull(heroGlow.GetComponent<UiAmbientFloat>());
            Assert.NotNull(heroAccent);
            Assert.NotNull(heroAccent.GetComponent<UiGradient>());
            Assert.NotNull(poster.GetComponent<Shadow>());
            Assert.NotNull(poster.GetComponent<UiItemReveal>());
            Assert.NotNull(poster.GetComponent<UiArtworkPlaceholderMotion>());
            Assert.IsTrue(poster.GetComponent<LayoutElement>().ignoreLayout);
            Assert.NotNull(posterPlaceholder);
            Assert.IsTrue(posterPlaceholder.gameObject.activeSelf);
            Assert.NotNull(information.GetComponent<UiItemReveal>());
            Assert.IsTrue(information.GetComponent<LayoutElement>().ignoreLayout);
            Assert.Less(
                ((RectTransform)information).anchoredPosition.x,
                ((RectTransform)poster).anchoredPosition.x,
                "The information stack must stay on the left of the full key art.");
            Assert.GreaterOrEqual(((RectTransform)poster).anchorMin.x, 0.3f);
            Assert.NotNull(keyArtShade);
            Assert.NotNull(keyArtShade.GetComponent<UiGradient>());
            Assert.NotNull(keyArtSpectrum);
            Assert.NotNull(keyArtSpectrum.GetComponent<UiAmbientFloat>());
            Assert.NotNull(detailTabs);
            Assert.NotNull(FindDescendant(detailTabs, "Detail Tab Episodes"));
            Assert.NotNull(FindDescendant(detailTabs, "Detail Tab Related"));
            Assert.NotNull(FindDescendant(detailTabs, "Detail Tab Information"));
            Assert.IsFalse(
                FindDescendant(detailTabs, "Detail Tab Episodes").GetComponent<Button>().interactable);
            Assert.IsFalse(
                FindDescendant(detailTabs, "Detail Tab Related").GetComponent<Button>().interactable);
            Assert.IsTrue(
                FindDescendant(detailTabs, "Detail Tab Information").GetComponent<Button>().interactable);
            Assert.NotNull(tabStage);
            Assert.IsFalse(episodesPanel.gameObject.activeInHierarchy);
            Assert.IsTrue(informationPanel.gameObject.activeInHierarchy);
            Assert.Greater(
                FindDescendant(
                        FindDescendant(detailTabs, "Detail Tab Information"),
                        "Tab Indicator")
                    .GetComponent<Image>().color.a,
                0.5f);
            Assert.NotNull(facts.GetComponent<UiScrollReveal>());
            Assert.NotNull(continueButton);
            Assert.AreEqual(
                300f,
                continueButton.GetComponent<LayoutElement>().preferredWidth,
                0.1f,
                "Movie playback should use a compact primary action.");

            detail.SetInteractionEnabled(false);
            CanvasGroup detailGroup = detailRoot.GetComponent<CanvasGroup>();
            Assert.IsFalse(detailRoot.GetComponent<UiViewMotion>().InteractionAllowed);
            Assert.IsFalse(detailGroup.interactable);
            Assert.IsFalse(
                detailGroup.blocksRaycasts,
                "The page behind playback must not receive pointer or remote input.");
            detail.SetInteractionEnabled(true);
            Assert.IsTrue(detailRoot.GetComponent<UiViewMotion>().InteractionAllowed);

            detail.Hide();
            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerView_ProvidesDecodeTrackAndSubtitleControls()
        {
            GameObject host = new GameObject("Player UI Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            PlayerView player = new PlayerView(host.transform);
            yield return null;

            Transform topGradient = FindDescendant(host.transform, "Top Edge Gradient");
            Transform bottomGradient = FindDescendant(host.transform, "Bottom Edge Gradient");
            RectTransform topControls = FindDescendant(host.transform, "Top Controls")
                as RectTransform;
            RectTransform bottomControls = FindDescendant(host.transform, "Playback Controls")
                as RectTransform;
            Transform seekFeedback = FindDescendant(host.transform, "Seek Feedback");
            Transform trackMenu = FindDescendant(host.transform, "Track Menu");
            Assert.NotNull(topGradient);
            Assert.NotNull(topGradient.GetComponent<UiGradient>());
            Assert.NotNull(bottomGradient);
            Assert.NotNull(bottomGradient.GetComponent<UiGradient>());
            Assert.NotNull(topControls);
            Assert.NotNull(topControls.GetComponent<Outline>());
            Assert.NotNull(topControls.GetComponent<Shadow>());
            Assert.Less(topControls.rect.width, 1920f);
            Assert.NotNull(bottomControls);
            Assert.NotNull(bottomControls.GetComponent<Outline>());
            Assert.NotNull(bottomControls.GetComponent<Shadow>());
            Assert.Less(bottomControls.rect.width, 1920f);
            Assert.NotNull(seekFeedback);
            Assert.NotNull(seekFeedback.GetComponent<UiGradient>());
            Assert.NotNull(trackMenu);
            Assert.NotNull(trackMenu.GetComponent<Outline>());
            Assert.NotNull(FindDescendant(host.transform, "Decode Mode"));
            Assert.NotNull(FindDescendant(host.transform, "Decode Status Capsule"));
            Assert.NotNull(FindDescendant(host.transform, "Audio Tracks"));
            Assert.NotNull(FindDescendant(host.transform, "Subtitle Tracks"));
            Assert.NotNull(FindDescendant(host.transform, "Subtitle Overlay"));

            player.Dispose();
            Object.Destroy(host);
            yield return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
        }

        private static bool HasHeadTrackingComponent(Camera camera)
        {
            return camera.GetComponents<Component>().Any(component =>
                component != null
                && (component.GetType().Name.Contains("TrackedPose")
                    || component.GetType().Name.Contains("HeadTracked")));
        }

        private static void AssertPerEyeCanvasFillsViewport(Camera camera, Canvas canvas)
        {
            Assert.NotNull(camera);
            Assert.NotNull(canvas);
            RectTransform rect = canvas.transform as RectTransform;
            Assert.NotNull(rect);
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 bottomLeft = camera.WorldToViewportPoint(corners[0]);
            Vector3 topRight = camera.WorldToViewportPoint(corners[2]);
            Assert.AreEqual(0f, bottomLeft.x, 0.001f);
            Assert.AreEqual(0f, bottomLeft.y, 0.001f);
            Assert.AreEqual(1f, topRight.x, 0.001f);
            Assert.AreEqual(1f, topRight.y, 0.001f);
        }

        private static JellyfinItem CreateEpisode(int index, string name)
        {
            return new JellyfinItem
            {
                Id = "episode-" + index,
                Name = name,
                Type = "Episode",
                MediaType = "Video",
                SeasonName = "测试第一季",
                ParentIndexNumber = 1,
                IndexNumber = index,
                UserData = new JellyfinUserData()
            };
        }

        private static RectTransform CreateTestRect(string name, Transform parent, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static Button CreateNavigationButton(
            string name,
            Transform parent,
            Vector2 position)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(180f, 64f);
            Button button = gameObject.GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
            return button;
        }

        private static void AssertModernEmptyState(Transform parent, string name)
        {
            Transform state = FindDescendant(parent, name);
            Assert.NotNull(state, name + " must exist.");
            Assert.IsTrue(state.GetComponent<UiViewMotion>().IsVisible);

            Transform card = FindDescendant(state, "State Card");
            Transform signal = FindDescendant(state, "Library Signal");
            Transform pulse = FindDescendant(state, "Status Signal");
            Transform title = FindDescendant(state, "State Title");
            Assert.NotNull(card);
            Assert.NotNull(card.GetComponent<UiGradient>());
            Assert.NotNull(card.GetComponent<Outline>());
            Assert.NotNull(signal);
            Assert.NotNull(pulse);
            Assert.NotNull(pulse.GetComponent<UiSignalPulse>());
            Assert.IsFalse(string.IsNullOrWhiteSpace(title.GetComponent<Text>().text));
        }

        private static void PerformDrag(
            GameObject dragTarget,
            Vector2 pressPosition,
            Vector2 beginPosition,
            Vector2 dragPosition)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pressPosition = pressPosition,
                position = beginPosition,
                delta = beginPosition - pressPosition
            };
            ExecuteEvents.ExecuteHierarchy(
                dragTarget,
                eventData,
                ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.ExecuteHierarchy(
                dragTarget,
                eventData,
                ExecuteEvents.beginDragHandler);
            eventData.delta = dragPosition - beginPosition;
            eventData.position = dragPosition;
            ExecuteEvents.ExecuteHierarchy(
                dragTarget,
                eventData,
                ExecuteEvents.dragHandler);
            ExecuteEvents.ExecuteHierarchy(
                dragTarget,
                eventData,
                ExecuteEvents.endDragHandler);
        }

        private static void AssertTransparentDragSurface(Transform viewport)
        {
            Image surface = viewport.GetComponent<Image>();
            Assert.NotNull(surface, "A ScrollRect viewport needs a Graphic to receive ray drag events on empty space.");
            Assert.IsTrue(surface.raycastTarget);
            Assert.AreEqual(0f, surface.color.a, 0.0001f);
        }

        private sealed class FakePlaybackEngine : IPlaybackEngine
        {
            public event Action<string> Failed
            {
                add { }
                remove { }
            }

            public event Action Completed
            {
                add { }
                remove { }
            }

            public Texture OutputTexture => null;
            public bool FlipOutputVertically => false;
            public bool IsPrepared { get; private set; }
            public bool IsPlaying { get; private set; }
            public bool IsPaused { get; private set; }
            public bool CanSeek => IsPrepared;
            public double PositionSeconds { get; set; }
            public double DurationSeconds { get; set; } = 600d;
            public double LastSeekSeconds { get; private set; } = -1d;

            public Task PrepareAndPlayAsync(
                JellyfinPlaybackPlan plan,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PositionSeconds = Math.Max(
                    0d,
                    plan.StartPositionTicks / (double)AppConstants.TicksPerSecond);
                IsPrepared = true;
                IsPlaying = true;
                IsPaused = false;
                return Task.CompletedTask;
            }

            public void Update()
            {
            }

            public void Play()
            {
                IsPlaying = IsPrepared;
                IsPaused = false;
            }

            public void Pause()
            {
                IsPlaying = false;
                IsPaused = IsPrepared;
            }

            public void Seek(double seconds)
            {
                LastSeekSeconds = seconds;
            }

            public void Stop()
            {
                IsPrepared = false;
                IsPlaying = false;
                IsPaused = false;
            }

            public void Dispose()
            {
                Stop();
            }
        }
    }
}
