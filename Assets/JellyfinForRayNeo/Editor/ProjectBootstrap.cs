using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace JellyfinForRayNeo.Editor
{
    public static class ProjectBootstrap
    {
        private const string MainScenePath = "Assets/JellyfinForRayNeo/Scenes/Main.unity";
        private const string RayNeoRigPath = "Packages/com.ffalcon.plugin.xr/Runtime/Prefab/XR Plugin.prefab";

        [MenuItem("Jellyfin for RayNeo/Configure Project and Scene")]
        public static void ConfigureProject()
        {
            ConfigurePlayerSettings();
            ConfigureXrLoader();
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

        private static void ConfigureXrLoader()
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

            bool assigned = XRPackageMetadataStore.AssignLoader(
                generalSettings.AssignedSettings,
                "Google.XR.Cardboard.XRLoader",
                BuildTargetGroup.Android);
            if (!assigned)
            {
                Debug.LogWarning("Cardboard XR loader could not be assigned automatically. Check Project Settings > XR Plug-in Management > Android.");
            }

            if (!generalSettings.AssignedSettings.activeLoaders.Any(
                    loader => loader != null && loader.GetType().FullName == "Google.XR.Cardboard.XRLoader"))
            {
                XRLoader cardboardLoader = AssetDatabase.FindAssets("t:XRLoader")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<XRLoader>)
                    .FirstOrDefault(loader => loader != null && loader.GetType().FullName == "Google.XR.Cardboard.XRLoader");
                if (cardboardLoader == null
                    || !generalSettings.AssignedSettings.TrySetLoaders(new List<XRLoader> { cardboardLoader }))
                {
                    Debug.LogWarning("Cardboard XR loader asset exists but could not be persisted in Android XR settings.");
                }
            }

            generalSettings.InitManagerOnStart = true;
            generalSettings.AssignedSettings.automaticLoading = true;
            generalSettings.AssignedSettings.automaticRunning = true;
            EditorUtility.SetDirty(perBuildTarget);
            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(generalSettings.AssignedSettings);
            AssetDatabase.SaveAssets();
        }

        private static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath) ?? "Assets/JellyfinForRayNeo/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RayNeoRigPath);
            if (rigPrefab != null)
            {
                GameObject rig = PrefabUtility.InstantiatePrefab(rigPrefab, scene) as GameObject;
                if (rig != null)
                {
                    rig.name = "RayNeo Air XR Rig";
                }
            }
            else
            {
                CreatePreviewCamera();
                Debug.LogWarning("RayNeo XR rig was not found. Run scripts/install-rayneo-sdk.sh and configure the project again.");
            }

            if (Camera.main == null)
            {
                CreatePreviewCamera();
            }

            GameObject application = new GameObject("Jellyfin for RayNeo Application");
            application.AddComponent<JellyfinAppController>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
            Selection.activeGameObject = application;
        }

        private static void CreatePreviewCamera()
        {
            GameObject cameraObject = new GameObject("Editor Preview Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 27f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }
    }
}
