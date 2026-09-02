using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace JellyfinForRayNeo.Editor
{
    public static class ProjectBootstrap
    {
        private const string MainScenePath = "Assets/JellyfinForRayNeo/Scenes/Main.unity";

        [MenuItem("Jellyfin for RayNeo/Configure Project and Scene")]
        public static void ConfigureProject()
        {
            ConfigurePlayerSettings();
            ConfigureAir3SDisplay();
            CreateMainScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Jellyfin for RayNeo project configuration completed.");
        }

        [MenuItem("Jellyfin for RayNeo/Configure Android Input")]
        public static void ConfigureAndroidInput()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                throw new InvalidOperationException("Unity PlayerSettings asset could not be loaded.");
            }

            SerializedObject settings = new SerializedObject(assets[0]);
            SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler == null)
            {
                throw new InvalidOperationException("Unity active input handler setting was not found.");
            }

            inputHandler.intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Android input configured for the legacy Input Manager.");
        }

        private static void ConfigurePlayerSettings()
        {
            ConfigureAndroidInput();
            PlayerSettings.companyName = "JellyfinForRayNeo";
            PlayerSettings.productName = "Jellyfin for RayNeo";
            PlayerSettings.bundleVersion = AppConstants.ClientVersion;
            PlayerSettings.runInBackground = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.jellyfinforrayneo.client");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);
        }

        private static void ConfigureAir3SDisplay()
        {
            const string xrDirectory = "Assets/XR";
            const string xrSettingsPath = xrDirectory + "/XRGeneralSettings.asset";

            XRGeneralSettingsPerBuildTarget perBuildTarget;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBuildTarget)
                || perBuildTarget == null)
            {
                Directory.CreateDirectory(xrDirectory);
                perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                perBuildTarget.name = "XR General Settings Per Build Target";
                AssetDatabase.CreateAsset(perBuildTarget, xrSettingsPath);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBuildTarget, true);
            }

            XRGeneralSettings generalSettings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null)
            {
                generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                generalSettings.name = "Android XR Settings";
                perBuildTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, generalSettings);
                AssetDatabase.AddObjectToAsset(generalSettings, AssetDatabase.GetAssetPath(perBuildTarget));
            }

            if (generalSettings.AssignedSettings == null)
            {
                XRManagerSettings manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                manager.name = "Android XR Providers";
                generalSettings.AssignedSettings = manager;
                AssetDatabase.AddObjectToAsset(manager, AssetDatabase.GetAssetPath(perBuildTarget));
            }

            if (!generalSettings.AssignedSettings.TrySetLoaders(new List<XRLoader>()))
            {
                Debug.LogWarning("Android XR loaders could not be cleared automatically.");
            }

            generalSettings.InitManagerOnStart = false;
            generalSettings.AssignedSettings.automaticLoading = false;
            generalSettings.AssignedSettings.automaticRunning = false;
            EditorUtility.SetDirty(perBuildTarget);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(generalSettings.AssignedSettings);
            AssetDatabase.SaveAssets();
        }

        private static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath) ?? "Assets/JellyfinForRayNeo/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateDisplayCamera();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            GameObject application = new GameObject("Jellyfin for RayNeo Application");
            application.AddComponent<JellyfinAppController>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
            Selection.activeGameObject = application;
        }

        private static void CreateDisplayCamera()
        {
            GameObject cameraObject = new GameObject(
                "RayNeo Air 3S Display Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(Air3SDisplayController));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 27f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
        }
    }
}
