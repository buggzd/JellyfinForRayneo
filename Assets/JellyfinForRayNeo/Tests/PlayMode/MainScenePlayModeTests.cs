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
            Assert.AreEqual(
                "FfalconXR.InputModule.XRGraphicRaycaster",
                raycaster.GetType().FullName,
                "RayNeo's custom ray input requires XRGraphicRaycaster instead of Unity's screen-point raycaster.");
            Assert.IsNull(
                canvas.GetComponent<GraphicRaycaster>(),
                "A standard GraphicRaycaster would make the Editor mouse and RayNeo laser target different UI positions.");

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

            ScrollRect scroll = shelf.GetComponent<ScrollRect>();
            Assert.NotNull(scroll);
            Assert.IsTrue(scroll.horizontal);
            Assert.AreSame(viewport, scroll.viewport);

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
        public IEnumerator MainScene_ContainsRayNeoHeadRayRig()
        {
            GameObject rig = GameObject.Find("RayNeo Air XR Rig");
            Assert.NotNull(rig, "Install the official RayNeo Air SDK before running PlayMode tests.");

            Transform head = FindDescendant(rig.transform, "Head");
            Transform laser = FindDescendant(rig.transform, "LaserBeam");
            Assert.NotNull(head);
            Assert.NotNull(laser);
            Assert.AreEqual("MainCamera", head.tag);
            Assert.AreSame(head.GetComponent<Camera>(), Camera.main);
            Assert.NotNull(EventSystem.current);
            BaseInputModule[] inputModules = EventSystem.current.GetComponents<BaseInputModule>();
            Assert.IsTrue(
                inputModules.Any(module =>
                    module.GetType().FullName == "JellyfinForRayNeo.RayNeoEditorInputModule"
                    && module.enabled),
                "Editor play mode must use the input module that supports dragging while the mouse is captured.");
            Assert.IsFalse(
                inputModules.Any(module =>
                    module.GetType().FullName == "FfalconXR.InputModule.XRInputModule"
                    && module.enabled),
                "The official module ignores drag events while the Editor cursor is locked.");

            RayNeoEditorInputSimulator simulator =
                Object.FindObjectOfType<RayNeoEditorInputSimulator>();
            Assert.NotNull(simulator);
            Assert.IsTrue(simulator.enabled);
            Assert.IsTrue(
                simulator.GetComponents<Component>().Any(component =>
                    component != null
                    && component.GetType().FullName
                        == "FfalconXR.InputModule.UnityInputKeyHandler"),
                "The Editor simulator must forward the left mouse button through the same input chain as the ray module.");
            yield return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
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
