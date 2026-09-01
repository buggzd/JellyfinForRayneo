using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            Assert.NotNull(canvas.GetComponent<GraphicRaycaster>());

            Transform login = FindDescendant(canvas.transform, "Login Screen");
            Transform home = FindDescendant(canvas.transform, "Home Screen");
            Transform phoneHint = FindDescendant(canvas.transform, "Phone Connection Hint");
            Assert.NotNull(login);
            Assert.NotNull(home);
            Assert.NotNull(phoneHint);
            Assert.IsTrue(login.gameObject.activeInHierarchy);
            Assert.IsFalse(home.gameObject.activeSelf);
            Assert.AreEqual(0, login.GetComponentsInChildren<InputField>(true).Length);
            Assert.AreEqual(
                CompanionLoginState.LoginRequired,
                CompanionLoginRuntime.Current.State);
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
            Assert.IsTrue(EventSystem.current.GetComponents<BaseInputModule>().Any());
            yield return null;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
        }
    }
}
