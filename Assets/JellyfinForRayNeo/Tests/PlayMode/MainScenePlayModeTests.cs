using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
        public IEnumerator NoSavedSession_ShowsPhoneConnectionWaitingUi()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            Assert.NotNull(canvas);
            Assert.AreEqual("Jellyfin Spatial Canvas", canvas.name);
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode);
            BaseRaycaster raycaster = canvas.GetComponent<BaseRaycaster>();
            Assert.NotNull(raycaster);
            Assert.IsInstanceOf<GraphicRaycaster>(raycaster);
            Air3SDisplayController display = Object.FindObjectOfType<Air3SDisplayController>();
            Assert.NotNull(display);
            Assert.AreSame(display.MonoCamera, canvas.worldCamera);
            Assert.AreEqual(display.CanvasWorldScale, canvas.transform.localScale.x, 0.000001f);

            Transform login = FindDescendant(canvas.transform, "Login Screen");
            Transform home = FindDescendant(canvas.transform, "Home Screen");
            Transform phoneHint = FindDescendant(canvas.transform, "Phone Connection Hint");
            Transform homeContent = FindDescendant(canvas.transform, "Home Content");
            Transform homeViewport = FindDescendant(canvas.transform, "Home Viewport");
            Assert.NotNull(login);
            Assert.NotNull(home);
            Assert.NotNull(phoneHint);
            Assert.NotNull(homeContent);
            Assert.NotNull(homeViewport);
            Assert.IsTrue(login.gameObject.activeInHierarchy);
            Assert.IsFalse(home.gameObject.activeSelf);
            Assert.AreEqual(0, login.GetComponentsInChildren<InputField>(true).Length);
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
            PosterCardView card = shelf.GetComponentInChildren<PosterCardView>(true);
            AxisRoutingScrollRect horizontalScroll = shelf.GetComponent<AxisRoutingScrollRect>();
            Assert.IsTrue(seasons.GetComponent<VerticalLayoutGroup>().childControlHeight);
            Assert.That(shelf.rect.height, Is.EqualTo(360f).Within(0.5f));
            Assert.NotNull(viewport);
            Assert.That(viewport.rect.height, Is.GreaterThan(250f));
            AssertTransparentDragSurface(viewport);
            Assert.NotNull(artwork);
            Assert.That(artwork.rect.width / artwork.rect.height, Is.EqualTo(16f / 9f).Within(0.01f));
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
            Assert.NotNull(scrollTransform);
            Assert.NotNull(viewport);
            Assert.NotNull(content);
            Assert.NotNull(overview);
            Assert.NotNull(actions);
            Assert.NotNull(continueButton);
            Assert.NotNull(metadataChips);

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
            Assert.Greater(
                overview.GetSiblingIndex(),
                actions.GetSiblingIndex(),
                "The compact summary must appear immediately after the playback actions.");
            Assert.IsTrue(
                actions.GetComponent<HorizontalLayoutGroup>().childControlWidth,
                "Action buttons must use their LayoutElement widths instead of Unity's 100 px default.");
            Assert.AreEqual(0f, actions.GetComponent<LayoutElement>().flexibleHeight);
            Assert.AreEqual(420f, continueButton.GetComponent<LayoutElement>().preferredWidth);
            Assert.IsTrue(metadataChips.GetComponent<HorizontalLayoutGroup>().childControlWidth);
            Assert.AreEqual(0f, metadataChips.GetComponent<LayoutElement>().flexibleHeight);
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
            Assert.AreEqual(5, grid.constraintCount);
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
            Assert.IsTrue(FindDescendant(host.transform, "Similar Shelf").gameObject.activeInHierarchy);
            Button chapter = FindDescendant(host.transform, "Chapter - 1").GetComponent<Button>();
            Assert.AreEqual("6:43  ·  第二幕", chapter.GetComponentInChildren<Text>().text);
            chapter.onClick.Invoke();
            Assert.AreEqual(AppConstants.TicksPerSecond * 403L, chapterPosition);
            Button overviewToggle = FindDescendant(host.transform, "Overview Toggle").GetComponent<Button>();
            Assert.IsTrue(overviewToggle.gameObject.activeInHierarchy);
            overviewToggle.onClick.Invoke();
            Assert.IsTrue(FindDescendant(host.transform, "Expanded Overview Card").gameObject.activeInHierarchy);

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
        public IEnumerator PlayerView_ProvidesDecodeTrackAndSubtitleControls()
        {
            GameObject host = new GameObject("Player UI Test Host", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            PlayerView player = new PlayerView(host.transform);
            yield return null;

            Assert.NotNull(FindDescendant(host.transform, "Decode Mode"));
            Assert.NotNull(FindDescendant(host.transform, "Audio Tracks"));
            Assert.NotNull(FindDescendant(host.transform, "Subtitle Tracks"));
            Assert.NotNull(FindDescendant(host.transform, "Subtitle Overlay"));
            Assert.NotNull(FindDescendant(host.transform, "Track Menu"));

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
    }
}
