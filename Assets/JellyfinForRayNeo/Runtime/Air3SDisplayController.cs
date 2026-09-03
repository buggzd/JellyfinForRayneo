using System;
using FfalconXR.Native;
using UnityEngine;

namespace JellyfinForRayNeo
{
    public enum Air3SDisplayMode
    {
        Mirror2D,
        StereoVirtualScreen
    }

    [DefaultExecutionOrder(-1200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class Air3SDisplayController : MonoBehaviour
    {
        public const string Mirror2DPreference = "mirror_2d";
        public const string StereoScreenPreference = "stereo_screen";
        public const string EditorPreferenceKey = "JellyfinForRayNeo.DisplayMode";
        // SBS divides only the transport frame. RayNeo expands each half onto a
        // complete 16:9 eye display, so projection must never use the squeezed
        // half-frame aspect ratio.
        public const float PerEyeAspect = 16f / 9f;
        public const float ReferenceCanvasHeight = 1080f;
        public const float DefaultScreenDistance = 4.5f;

        private const float NativePreferencePollInterval = 0.35f;
        private const float HardwareStatePollInterval = 0.1f;
        private const float HardwareReconcileInterval = 1f;
        private const float HardwareTransitionTimeout = 1.5f;
        private const float FailedTransitionRetryDelay = 5f;

        [SerializeField]
        private Air3SDisplayMode _defaultMode = Air3SDisplayMode.Mirror2D;

        [SerializeField]
        [Range(0.05f, 0.075f)]
        private float _interpupillaryDistance = 0.064f;

        [SerializeField]
        [Range(10f, 60f)]
        private float _verticalFieldOfView = 27f;

        [SerializeField]
        [Range(2f, 8f)]
        private float _screenDistance = DefaultScreenDistance;

        private Camera _monoCamera;
        private Camera _leftEyeCamera;
        private Camera _rightEyeCamera;
        private int _sceneCullingMask;
        private Air3SDisplayMode _requestedMode;
        private Air3SDisplayMode _activeMode;
        private bool _initialized;
        private bool _transitioning;
        private bool _nativeSdkInitialized;
        private bool _lastGlassesConnected;
        private float _transitionDeadline;
        private float _nextTransitionCheckAt;
        private float _nextNativePreferencePollAt;
        private float _nextHardwareReconcileAt;
        private float _nextFailedTransitionRetryAt;
        private string _lastPublishedStatus;

        public Camera MonoCamera => _monoCamera != null ? _monoCamera : GetComponent<Camera>();

        public Camera LeftEyeCamera => _leftEyeCamera;

        public Camera RightEyeCamera => _rightEyeCamera;

        public Air3SDisplayMode RequestedMode => _requestedMode;

        public Air3SDisplayMode ActiveMode => _activeMode;

        public bool IsTransitioning => _transitioning;

        public float InterpupillaryDistance => _interpupillaryDistance;

        public float ScreenDistance => _screenDistance;

        public float CanvasWorldScale => CalculateCanvasWorldScale(
            _screenDistance,
            _verticalFieldOfView,
            ReferenceCanvasHeight);

        private void Awake()
        {
            EnsureCameras();
            _requestedMode = ReadInitialMode();
            _activeMode = Air3SDisplayMode.Mirror2D;
            _initialized = true;
            ConfigureMonoOutput();
            ReconcileOutput(true);
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (Time.unscaledTime >= _nextNativePreferencePollAt)
            {
                _nextNativePreferencePollAt = Time.unscaledTime + NativePreferencePollInterval;
                if (TryReadPhonePreference(out Air3SDisplayMode phoneMode)
                    && phoneMode != _requestedMode)
                {
                    SetMode(phoneMode, false);
                }
            }
#endif

            if (_transitioning && Time.unscaledTime >= _nextTransitionCheckAt)
            {
                _nextTransitionCheckAt = Time.unscaledTime + HardwareStatePollInterval;
                PollHardwareTransition();
                return;
            }

            if (Time.unscaledTime >= _nextHardwareReconcileAt)
            {
                _nextHardwareReconcileAt = Time.unscaledTime + HardwareReconcileInterval;
                ReconcileOutput(false);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!_initialized)
            {
                return;
            }

            if (paused)
            {
                _transitioning = false;
                SwitchTo2DBestEffort();
                ConfigureMonoOutput();
                PublishPhoneStatus(false, "应用已暂停，眼镜已恢复为 2D 模式。");
            }
            else
            {
                ReconcileOutput(true);
            }
        }

        private void OnDisable()
        {
            if (_initialized && Application.isPlaying)
            {
                _transitioning = false;
                SwitchTo2DBestEffort();
            }
        }

        private void OnDestroy()
        {
            if (_initialized)
            {
                _transitioning = false;
                SwitchTo2DBestEffort();
            }
        }

        public void SetMode(Air3SDisplayMode mode)
        {
            SetMode(mode, true);
        }

        public static bool TryParsePreference(string value, out Air3SDisplayMode mode)
        {
            string normalized = value != null ? value.Trim().ToLowerInvariant() : string.Empty;
            switch (normalized)
            {
                case Mirror2DPreference:
                case "2d":
                case "mono":
                    mode = Air3SDisplayMode.Mirror2D;
                    return true;
                case StereoScreenPreference:
                case "3d":
                case "stereo":
                    mode = Air3SDisplayMode.StereoVirtualScreen;
                    return true;
                default:
                    mode = Air3SDisplayMode.Mirror2D;
                    return false;
            }
        }

        public static string ToPreferenceValue(Air3SDisplayMode mode)
        {
            return mode == Air3SDisplayMode.StereoVirtualScreen
                ? StereoScreenPreference
                : Mirror2DPreference;
        }

        public static float CalculateCanvasWorldScale(
            float screenDistance,
            float verticalFieldOfView,
            float referencePixelHeight = ReferenceCanvasHeight)
        {
            if (screenDistance <= 0f || referencePixelHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(screenDistance),
                    "Screen distance and reference height must be positive.");
            }

            float clampedFieldOfView = Mathf.Clamp(verticalFieldOfView, 1f, 179f);
            // Size against one complete eye frustum. Halving this for SBS would
            // leave the virtual screen occupying only part of each eye display.
            float worldHeight = 2f
                * screenDistance
                * Mathf.Tan(clampedFieldOfView * 0.5f * Mathf.Deg2Rad);
            return worldHeight / referencePixelHeight;
        }

        private void SetMode(Air3SDisplayMode mode, bool persistEditorPreference)
        {
            bool changed = mode != _requestedMode;
            _requestedMode = mode;
            _nextFailedTransitionRetryAt = 0f;

#if UNITY_EDITOR
            if (persistEditorPreference)
            {
                PlayerPrefs.SetString(EditorPreferenceKey, ToPreferenceValue(mode));
                PlayerPrefs.Save();
            }
#endif

            if (changed || !_transitioning)
            {
                ReconcileOutput(true);
            }
        }

        private Air3SDisplayMode ReadInitialMode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (TryReadPhonePreference(out Air3SDisplayMode phoneMode))
            {
                return phoneMode;
            }
#endif

#if UNITY_EDITOR
            string stored = PlayerPrefs.GetString(
                EditorPreferenceKey,
                ToPreferenceValue(_defaultMode));
            if (TryParsePreference(stored, out Air3SDisplayMode editorMode))
            {
                return editorMode;
            }
#endif

            return _defaultMode;
        }

        private void ReconcileOutput(bool force)
        {
            if (!_initialized || _transitioning)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TryIsGlassesConnected(out bool glassesConnected) || !glassesConnected)
            {
                _lastGlassesConnected = false;
                ConfigureMonoOutput();
                _activeMode = Air3SDisplayMode.Mirror2D;
                PublishPhoneStatus(
                    false,
                    _requestedMode == Air3SDisplayMode.StereoVirtualScreen
                        ? "立体屏幕已保存，连接眼镜后自动启用。"
                        : "镜像 2D 已保存，连接眼镜后自动启用。");
                return;
            }

            bool connectionChanged = !_lastGlassesConnected;
            _lastGlassesConnected = true;
            bool expected3D = _requestedMode == Air3SDisplayMode.StereoVirtualScreen;
            if (!force
                && !connectionChanged
                && Time.unscaledTime < _nextFailedTransitionRetryAt)
            {
                return;
            }

            if (!force
                && TryReadHardware3DState(out bool hardware3D)
                && hardware3D == expected3D
                && _activeMode == _requestedMode)
            {
                return;
            }

            BeginHardwareTransition();
#else
            ConfigureOutput(_requestedMode);
            _activeMode = _requestedMode;
#endif
        }

        private void BeginHardwareTransition()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ConfigureTransitionBlackFrame();
            PublishPhoneStatus(
                false,
                _requestedMode == Air3SDisplayMode.StereoVirtualScreen
                    ? "正在切换到左右眼立体画面…"
                    : "正在切换到双眼镜像画面…");
            try
            {
                EnsureNativeSdkInitialized();
                if (_requestedMode == Air3SDisplayMode.StereoVirtualScreen)
                {
                    NativeModule.Instance.Switch3DMode();
                }
                else
                {
                    NativeModule.Instance.Switch2DMode();
                }

                _transitioning = true;
                _transitionDeadline = Time.unscaledTime + HardwareTransitionTimeout;
                _nextTransitionCheckAt = Time.unscaledTime;
                PollHardwareTransition();
            }
            catch (Exception exception)
            {
                FailHardwareTransition("眼镜显示模式切换失败：" + exception.Message);
            }
#endif
        }

        private void PollHardwareTransition()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!TryIsGlassesConnected(out bool glassesConnected) || !glassesConnected)
            {
                _transitioning = false;
                _lastGlassesConnected = false;
                ConfigureMonoOutput();
                _activeMode = Air3SDisplayMode.Mirror2D;
                PublishPhoneStatus(false, "眼镜已断开；重新连接后会应用所选模式。");
                return;
            }

            bool expected3D = _requestedMode == Air3SDisplayMode.StereoVirtualScreen;
            if (TryReadHardware3DState(out bool hardware3D) && hardware3D == expected3D)
            {
                _transitioning = false;
                ConfigureOutput(_requestedMode);
                _activeMode = _requestedMode;
                _nextFailedTransitionRetryAt = 0f;
                PublishPhoneStatus(
                    true,
                    _requestedMode == Air3SDisplayMode.StereoVirtualScreen
                        ? "立体屏幕已启用：左眼只显示左视图，右眼只显示右视图。"
                        : "镜像 2D 已启用：双眼显示同一幅完整画面。");
                return;
            }

            if (Time.unscaledTime >= _transitionDeadline)
            {
                FailHardwareTransition("眼镜没有确认显示模式，已安全回退到镜像 2D。");
            }
#endif
        }

        private void FailHardwareTransition(string message)
        {
            _transitioning = false;
            SwitchTo2DBestEffort();
            ConfigureMonoOutput();
            _activeMode = Air3SDisplayMode.Mirror2D;
            _nextFailedTransitionRetryAt = Time.unscaledTime + FailedTransitionRetryDelay;
            PublishPhoneStatus(false, message);
            Debug.LogWarning(message);
        }

        private void EnsureCameras()
        {
            _monoCamera = GetComponent<Camera>();
            _monoCamera.gameObject.name = "RayNeo Air 3S Display Camera";
            _monoCamera.gameObject.tag = "MainCamera";
            ConfigureBaseCamera(_monoCamera);
            _sceneCullingMask = _monoCamera.cullingMask;

            _leftEyeCamera = EnsureEyeCamera("RayNeo Air 3S Left Eye Camera");
            _rightEyeCamera = EnsureEyeCamera("RayNeo Air 3S Right Eye Camera");
            ConfigureEyeCameras();
        }

        private Camera EnsureEyeCamera(string objectName)
        {
            Transform existing = transform.Find(objectName);
            GameObject eyeObject = existing != null ? existing.gameObject : new GameObject(objectName);
            eyeObject.transform.SetParent(transform, false);
            eyeObject.tag = "Untagged";
            Camera eyeCamera = eyeObject.GetComponent<Camera>();
            if (eyeCamera == null)
            {
                eyeCamera = eyeObject.AddComponent<Camera>();
            }

            eyeCamera.CopyFrom(_monoCamera);
            eyeCamera.stereoTargetEye = StereoTargetEyeMask.None;
            eyeCamera.enabled = false;
            return eyeCamera;
        }

        private void ConfigureBaseCamera(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = _verticalFieldOfView;
            camera.aspect = PerEyeAspect;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
        }

        private void ConfigureEyeCameras()
        {
            if (_leftEyeCamera == null || _rightEyeCamera == null)
            {
                return;
            }

            float halfIpd = _interpupillaryDistance * 0.5f;
            ConfigureEyeCamera(
                _leftEyeCamera,
                -halfIpd,
                new Rect(0f, 0f, 0.5f, 1f),
                _monoCamera.depth);
            ConfigureEyeCamera(
                _rightEyeCamera,
                halfIpd,
                new Rect(0.5f, 0f, 0.5f, 1f),
                _monoCamera.depth + 1f);
        }

        private void ConfigureEyeCamera(
            Camera camera,
            float localX,
            Rect viewport,
            float depth)
        {
            camera.transform.localPosition = new Vector3(localX, 0f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.transform.localScale = Vector3.one;
            camera.rect = viewport;
            camera.aspect = PerEyeAspect;
            camera.fieldOfView = _verticalFieldOfView;
            camera.depth = depth;
            camera.cullingMask = _sceneCullingMask;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.projectionMatrix = CreateOffAxisProjection(localX);
        }

        private Matrix4x4 CreateOffAxisProjection(float eyeOffset)
        {
            float near = _monoCamera.nearClipPlane;
            float far = _monoCamera.farClipPlane;
            float halfHeight = near
                * Mathf.Tan(_verticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * PerEyeAspect;
            float frustumCenter = -eyeOffset * near / Mathf.Max(_screenDistance, 0.01f);
            return Matrix4x4.Frustum(
                frustumCenter - halfWidth,
                frustumCenter + halfWidth,
                -halfHeight,
                halfHeight,
                near,
                far);
        }

        private void ConfigureOutput(Air3SDisplayMode mode)
        {
            if (mode == Air3SDisplayMode.StereoVirtualScreen)
            {
                ConfigureStereoOutput();
            }
            else
            {
                ConfigureMonoOutput();
            }
        }

        private void ConfigureMonoOutput()
        {
            ConfigureBaseCamera(_monoCamera);
            _monoCamera.ResetProjectionMatrix();
            _monoCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _monoCamera.cullingMask = _sceneCullingMask;
            _monoCamera.enabled = true;
            if (_leftEyeCamera != null)
            {
                _leftEyeCamera.enabled = false;
            }
            if (_rightEyeCamera != null)
            {
                _rightEyeCamera.enabled = false;
            }
        }

        private void ConfigureStereoOutput()
        {
            ConfigureEyeCameras();
            _monoCamera.enabled = false;
            _leftEyeCamera.enabled = true;
            _rightEyeCamera.enabled = true;
        }

        private void ConfigureTransitionBlackFrame()
        {
            if (_leftEyeCamera != null)
            {
                _leftEyeCamera.enabled = false;
            }
            if (_rightEyeCamera != null)
            {
                _rightEyeCamera.enabled = false;
            }
            ConfigureBaseCamera(_monoCamera);
            _monoCamera.ResetProjectionMatrix();
            _monoCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _monoCamera.cullingMask = 0;
            _monoCamera.enabled = true;
        }

        private void EnsureNativeSdkInitialized()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_nativeSdkInitialized)
            {
                return;
            }

            NativeModule.Instance.Initialize();
            _nativeSdkInitialized = true;
#endif
        }

        private void SwitchTo2DBestEffort()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureNativeSdkInitialized();
                NativeModule.Instance.Switch2DMode();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RayNeo 2D mode could not be restored: " + exception.Message);
            }
#endif
        }

        private static bool TryReadHardware3DState(out bool is3D)
        {
            is3D = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                is3D = NativeModule.Instance.Is3DMode;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
#else
            return true;
#endif
        }

        private static bool TryReadPhonePreference(out Air3SDisplayMode mode)
        {
            mode = Air3SDisplayMode.Mirror2D;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    string value = activity?.Call<string>("getRayNeoDisplayMode");
                    return TryParsePreference(value, out mode);
                }
            }
            catch (Exception)
            {
                return false;
            }
#else
            return false;
#endif
        }

        private static bool TryIsGlassesConnected(out bool connected)
        {
            connected = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    connected = activity != null
                        && activity.Call<bool>("isRayNeoDisplayConnected");
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
#else
            return true;
#endif
        }

        private void PublishPhoneStatus(bool requestedModeApplied, string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string status = ToPreferenceValue(_requestedMode)
                + "|"
                + ToPreferenceValue(_activeMode)
                + "|"
                + requestedModeApplied
                + "|"
                + message;
            if (string.Equals(status, _lastPublishedStatus, StringComparison.Ordinal))
            {
                return;
            }

            _lastPublishedStatus = status;
            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity?.Call(
                        "setRayNeoDisplayModeState",
                        ToPreferenceValue(_requestedMode),
                        ToPreferenceValue(_activeMode),
                        requestedModeApplied,
                        message ?? string.Empty);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RayNeo display mode status could not be published: " + exception.Message);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _interpupillaryDistance = Mathf.Clamp(_interpupillaryDistance, 0.05f, 0.075f);
            _verticalFieldOfView = Mathf.Clamp(_verticalFieldOfView, 10f, 60f);
            _screenDistance = Mathf.Clamp(_screenDistance, 2f, 8f);
            if (!Application.isPlaying || !_initialized)
            {
                return;
            }

            ConfigureBaseCamera(_monoCamera);
            ConfigureEyeCameras();
            ConfigureOutput(_activeMode);
        }
#endif
    }
}
