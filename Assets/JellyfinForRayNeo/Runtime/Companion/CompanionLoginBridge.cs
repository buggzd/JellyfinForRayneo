using System;
using System.Collections.Concurrent;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using FfalconXR.Native;
#endif

namespace JellyfinForRayNeo
{
    public enum CompanionLoginState
    {
        Offline,
        Initializing,
        LoginRequired,
        Connecting,
        QuickConnectWaiting,
        Ready
    }

    [Serializable]
    public sealed class CompanionLoginRequest
    {
        [SerializeField] private string serverUrl;
        [SerializeField] private string username;
        [SerializeField] private string password;

        public string ServerUrl => serverUrl;
        public string UserName => username;
        public string Password => password;

        public static bool TryCreate(
            string serverUrl,
            string username,
            string password,
            out CompanionLoginRequest request,
            out string validationMessage)
        {
            request = new CompanionLoginRequest
            {
                serverUrl = serverUrl,
                username = username,
                password = password
            };

            if (request.TryNormalize(out validationMessage))
            {
                return true;
            }

            request.ClearPassword();
            request = null;
            return false;
        }

        public void ClearPassword()
        {
            password = null;
        }

        internal bool TryNormalize(out string validationMessage)
        {
            serverUrl = serverUrl != null ? serverUrl.Trim() : string.Empty;
            username = username != null ? username.Trim() : string.Empty;
            password = password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                validationMessage = "请输入 Jellyfin 服务器地址。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                validationMessage = "请输入 Jellyfin 用户名。";
                return false;
            }

            validationMessage = null;
            return true;
        }
    }

    [Serializable]
    public sealed class CompanionQuickConnectRequest
    {
        [SerializeField] private string serverUrl;

        public string ServerUrl => serverUrl;

        public static bool TryCreate(
            string serverUrl,
            out CompanionQuickConnectRequest request,
            out string validationMessage)
        {
            request = new CompanionQuickConnectRequest
            {
                serverUrl = serverUrl
            };

            if (request.TryNormalize(out validationMessage))
            {
                return true;
            }

            request = null;
            return false;
        }

        internal bool TryNormalize(out string validationMessage)
        {
            serverUrl = serverUrl != null ? serverUrl.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                validationMessage = "请先选择或输入 Jellyfin 服务器地址。";
                return false;
            }

            validationMessage = null;
            return true;
        }
    }

    [Serializable]
    public sealed class CompanionSessionRequest
    {
        [SerializeField] private string serverUrl;
        [SerializeField] private string serverName;
        [SerializeField] private string serverVersion;
        [SerializeField] private string serverId;
        [SerializeField] private string accessToken;
        [SerializeField] private string userId;
        [SerializeField] private string userName;
        [SerializeField] private string deviceId;

        public string ServerUrl => serverUrl;
        public string ServerName => serverName;
        public string ServerVersion => serverVersion;
        public string ServerId => serverId;
        public string AccessToken => accessToken;
        public string UserId => userId;
        public string UserName => userName;
        public string DeviceId => deviceId;

        internal bool TryNormalize(out string validationMessage)
        {
            serverUrl = serverUrl != null ? serverUrl.Trim() : string.Empty;
            serverName = serverName != null ? serverName.Trim() : string.Empty;
            serverVersion = serverVersion != null ? serverVersion.Trim() : string.Empty;
            serverId = serverId != null ? serverId.Trim() : string.Empty;
            accessToken = accessToken != null ? accessToken.Trim() : string.Empty;
            userId = userId != null ? userId.Trim() : string.Empty;
            userName = userName != null ? userName.Trim() : string.Empty;
            deviceId = deviceId != null ? deviceId.Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(serverUrl)
                || string.IsNullOrWhiteSpace(accessToken)
                || string.IsNullOrWhiteSpace(userId)
                || string.IsNullOrWhiteSpace(deviceId))
            {
                validationMessage = "手机端保存的 Jellyfin 会话不完整，请重新登录。";
                return false;
            }

            validationMessage = null;
            return true;
        }

        public JellyfinSession ToSession()
        {
            return new JellyfinSession
            {
                ServerUrl = serverUrl,
                ServerName = serverName,
                ServerVersion = serverVersion,
                ServerId = serverId,
                AccessToken = accessToken,
                UserId = userId,
                UserName = userName,
                DeviceId = deviceId
            };
        }
    }

    public sealed class CompanionLoginSnapshot
    {
        public CompanionLoginSnapshot(
            CompanionLoginState state,
            string message,
            bool isError,
            string serverUrl,
            string userName,
            string quickConnectCode = null)
        {
            State = state;
            Message = message ?? string.Empty;
            IsError = isError;
            ServerUrl = serverUrl ?? string.Empty;
            UserName = userName ?? string.Empty;
            QuickConnectCode = quickConnectCode ?? string.Empty;
        }

        public CompanionLoginState State { get; }
        public string Message { get; }
        public bool IsError { get; }
        public string ServerUrl { get; }
        public string UserName { get; }
        public string QuickConnectCode { get; }
    }

    public static class CompanionLoginRuntime
    {
        private static CompanionLoginSnapshot _current = CreateOfflineSnapshot();

        internal static event Action<CompanionLoginRequest> LoginSubmitted;
        internal static event Action<CompanionQuickConnectRequest> QuickConnectSubmitted;
        internal static event Action QuickConnectCancelled;

        public static CompanionLoginSnapshot Current => _current;

        public static bool SubmitLogin(string serverUrl, string username, string password)
        {
            if (!CompanionLoginRequest.TryCreate(
                    serverUrl,
                    username,
                    password,
                    out CompanionLoginRequest request,
                    out _))
            {
                return false;
            }

            Action<CompanionLoginRequest> handler = LoginSubmitted;
            if (handler == null)
            {
                request.ClearPassword();
                return false;
            }

            handler(request);
            return true;
        }

        public static bool SubmitQuickConnect(string serverUrl)
        {
            if (!CompanionQuickConnectRequest.TryCreate(
                    serverUrl,
                    out CompanionQuickConnectRequest request,
                    out _))
            {
                return false;
            }

            Action<CompanionQuickConnectRequest> handler = QuickConnectSubmitted;
            if (handler == null)
            {
                return false;
            }

            handler(request);
            return true;
        }

        public static bool CancelQuickConnect()
        {
            Action handler = QuickConnectCancelled;
            if (handler == null)
            {
                return false;
            }

            handler();
            return true;
        }

        internal static void SetSnapshot(CompanionLoginSnapshot snapshot)
        {
            _current = snapshot ?? CreateOfflineSnapshot();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            LoginSubmitted = null;
            QuickConnectSubmitted = null;
            QuickConnectCancelled = null;
            _current = CreateOfflineSnapshot();
        }

        private static CompanionLoginSnapshot CreateOfflineSnapshot()
        {
            return new CompanionLoginSnapshot(
                CompanionLoginState.Offline,
                "应用尚未运行。",
                false,
                "http://",
                string.Empty);
        }
    }

    public sealed class CompanionLoginBridge : IDisposable
    {
        public const int LoginMessageType = 1000;
        public const int QuickConnectMessageType = 1001;
        public const int CancelQuickConnectMessageType = 1002;
        public const int GlassesMessageType = 103;

        private readonly ConcurrentQueue<CompanionLoginRequest> _pendingRequests =
            new ConcurrentQueue<CompanionLoginRequest>();
        private readonly ConcurrentQueue<CompanionQuickConnectRequest> _pendingQuickConnectRequests =
            new ConcurrentQueue<CompanionQuickConnectRequest>();
        private readonly ConcurrentQueue<CompanionSessionRequest> _pendingSessions =
            new ConcurrentQueue<CompanionSessionRequest>();
        private readonly ConcurrentQueue<bool> _pendingQuickConnectCancellations =
            new ConcurrentQueue<bool>();
        private readonly ConcurrentQueue<string> _pendingValidationErrors =
            new ConcurrentQueue<string>();
        private bool _disposed;

#if UNITY_ANDROID && !UNITY_EDITOR
        private string _lastNativeSessionPayload;
        private bool _nativeSessionReadErrorReported;
        private bool _nativeLoginListenerRegistered;
        private bool _nativeQuickConnectListenerRegistered;
        private bool _nativeCancelListenerRegistered;
        private bool _nativeGlassesListenerRegistered;
#endif

        public CompanionLoginBridge()
        {
            CompanionLoginRuntime.LoginSubmitted += QueueRuntimeLogin;
            CompanionLoginRuntime.QuickConnectSubmitted += QueueRuntimeQuickConnect;
            CompanionLoginRuntime.QuickConnectCancelled += QueueRuntimeQuickConnectCancellation;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                NativeModule.Instance.RegesitNativeMsgDispatchListener(LoginMessageType, OnNativeMessage);
                _nativeLoginListenerRegistered = true;
                NativeModule.Instance.RegesitNativeMsgDispatchListener(
                    QuickConnectMessageType,
                    OnNativeQuickConnectMessage);
                _nativeQuickConnectListenerRegistered = true;
                NativeModule.Instance.RegesitNativeMsgDispatchListener(
                    CancelQuickConnectMessageType,
                    OnNativeQuickConnectCancellation);
                _nativeCancelListenerRegistered = true;
                NativeModule.Instance.RegesitNativeMsgDispatchListener(
                    GlassesMessageType,
                    OnNativeGlassesMessage);
                _nativeGlassesListenerRegistered = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RayNeo companion listener could not be registered: " + exception.Message);
            }
#endif
        }

        public event Action<CompanionLoginRequest> LoginRequested;
        public event Action<CompanionQuickConnectRequest> QuickConnectRequested;
        public event Action QuickConnectCancelRequested;
        public event Action<CompanionSessionRequest> SessionAvailable;

        public static bool TryParsePayload(
            string payload,
            out CompanionLoginRequest request,
            out string validationMessage)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                validationMessage = "手机端没有发送登录信息，请重试。";
                return false;
            }

            try
            {
                CompanionLoginRequest parsed = JsonUtility.FromJson<CompanionLoginRequest>(payload);
                if (parsed == null)
                {
                    validationMessage = "手机端发送的登录信息为空，请重试。";
                    return false;
                }
                if (!parsed.TryNormalize(out validationMessage))
                {
                    parsed.ClearPassword();
                    return false;
                }

                request = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                validationMessage = "手机端发送的登录信息格式无效，请重试。";
                return false;
            }
        }

        public static bool TryParseQuickConnectPayload(
            string payload,
            out CompanionQuickConnectRequest request,
            out string validationMessage)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                validationMessage = "手机端没有发送服务器地址，请重试。";
                return false;
            }

            try
            {
                CompanionQuickConnectRequest parsed =
                    JsonUtility.FromJson<CompanionQuickConnectRequest>(payload);
                if (parsed == null)
                {
                    validationMessage = "手机端发送的快速登录信息为空，请重试。";
                    return false;
                }
                if (!parsed.TryNormalize(out validationMessage))
                {
                    return false;
                }

                request = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                validationMessage = "手机端发送的快速登录信息格式无效，请重试。";
                return false;
            }
        }

        public static bool TryParseSessionPayload(
            string payload,
            out CompanionSessionRequest request,
            out string validationMessage)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(payload))
            {
                validationMessage = "手机端没有可用的 Jellyfin 会话。";
                return false;
            }

            try
            {
                CompanionSessionRequest parsed =
                    JsonUtility.FromJson<CompanionSessionRequest>(payload);
                if (parsed == null)
                {
                    validationMessage = "手机端保存的 Jellyfin 会话为空，请重新登录。";
                    return false;
                }
                if (!parsed.TryNormalize(out validationMessage))
                {
                    return false;
                }

                request = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                validationMessage = "手机端保存的 Jellyfin 会话格式无效，请重新登录。";
                return false;
            }
        }

        public static bool TryParseGlassesEvent(string[] values, out bool connected)
        {
            connected = false;
            if (values == null || values.Length == 0 || string.IsNullOrWhiteSpace(values[0]))
            {
                return false;
            }

            string value = values[0].Trim();
            if (string.Equals(value, "1", StringComparison.Ordinal))
            {
                connected = true;
                return true;
            }

            return string.Equals(value, "2", StringComparison.Ordinal);
        }

        public void Pump()
        {
            if (_disposed)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            PollNativeSession();
#endif

            while (_pendingValidationErrors.TryDequeue(out string validationMessage))
            {
                CompanionLoginSnapshot current = CompanionLoginRuntime.Current;
                PublishState(
                    CompanionLoginState.LoginRequired,
                    validationMessage,
                    true,
                    current.ServerUrl,
                    current.UserName);
            }

            while (_pendingQuickConnectCancellations.TryDequeue(out _))
            {
                while (_pendingQuickConnectRequests.TryDequeue(out _))
                {
                }
                QuickConnectCancelRequested?.Invoke();
            }

            while (_pendingRequests.TryDequeue(out CompanionLoginRequest request))
            {
                try
                {
                    LoginRequested?.Invoke(request);
                }
                finally
                {
                    request.ClearPassword();
                }
            }

            while (_pendingQuickConnectRequests.TryDequeue(
                       out CompanionQuickConnectRequest quickConnectRequest))
            {
                QuickConnectRequested?.Invoke(quickConnectRequest);
            }

            while (_pendingSessions.TryDequeue(out CompanionSessionRequest session))
            {
                SessionAvailable?.Invoke(session);
            }
        }

        public void ClearNativeSession()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _lastNativeSessionPayload = null;
            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity?.Call("clearNativeSession");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Android companion session could not be cleared: " + exception.Message);
            }
#endif
        }

        public void PublishState(
            CompanionLoginState state,
            string message,
            bool isError = false,
            string serverUrl = null,
            string userName = null,
            string quickConnectCode = null)
        {
            if (_disposed)
            {
                return;
            }

            CompanionLoginSnapshot snapshot = new CompanionLoginSnapshot(
                state,
                message,
                isError,
                serverUrl,
                userName,
                quickConnectCode);
            CompanionLoginRuntime.SetSnapshot(snapshot);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity?.Call(
                        "setCompanionState",
                        StateKey(state),
                        snapshot.Message,
                        snapshot.IsError,
                        snapshot.ServerUrl,
                        snapshot.UserName,
                        snapshot.QuickConnectCode);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Android companion state could not be updated: " + exception.Message);
            }
#endif
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CompanionLoginRuntime.LoginSubmitted -= QueueRuntimeLogin;
            CompanionLoginRuntime.QuickConnectSubmitted -= QueueRuntimeQuickConnect;
            CompanionLoginRuntime.QuickConnectCancelled -= QueueRuntimeQuickConnectCancellation;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_nativeLoginListenerRegistered)
            {
                try
                {
                    NativeModule.Instance.RmNativeMsgDispatchListener(LoginMessageType);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("RayNeo companion listener could not be removed: " + exception.Message);
                }
                _nativeLoginListenerRegistered = false;
            }
            if (_nativeQuickConnectListenerRegistered)
            {
                try
                {
                    NativeModule.Instance.RmNativeMsgDispatchListener(QuickConnectMessageType);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("RayNeo quick connect listener could not be removed: " + exception.Message);
                }
                _nativeQuickConnectListenerRegistered = false;
            }
            if (_nativeCancelListenerRegistered)
            {
                try
                {
                    NativeModule.Instance.RmNativeMsgDispatchListener(CancelQuickConnectMessageType);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("RayNeo quick connect cancel listener could not be removed: " + exception.Message);
                }
                _nativeCancelListenerRegistered = false;
            }
            if (_nativeGlassesListenerRegistered)
            {
                try
                {
                    NativeModule.Instance.RmNativeMsgDispatchListener(GlassesMessageType);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("RayNeo glasses listener could not be removed: " + exception.Message);
                }
                _nativeGlassesListenerRegistered = false;
            }
#endif

            while (_pendingRequests.TryDequeue(out CompanionLoginRequest request))
            {
                request.ClearPassword();
            }
            while (_pendingQuickConnectRequests.TryDequeue(out _))
            {
            }
            while (_pendingSessions.TryDequeue(out _))
            {
            }
            while (_pendingQuickConnectCancellations.TryDequeue(out _))
            {
            }
            while (_pendingValidationErrors.TryDequeue(out _))
            {
            }
        }

        private void QueueRuntimeLogin(CompanionLoginRequest request)
        {
            if (_disposed || request == null)
            {
                request?.ClearPassword();
                return;
            }
            _pendingRequests.Enqueue(request);
        }

        private void QueueRuntimeQuickConnect(CompanionQuickConnectRequest request)
        {
            if (_disposed || request == null)
            {
                return;
            }
            _pendingQuickConnectRequests.Enqueue(request);
        }

        private void QueueRuntimeQuickConnectCancellation()
        {
            if (!_disposed)
            {
                _pendingQuickConnectCancellations.Enqueue(true);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PollNativeSession()
        {
            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    string payload = activity?.Call<string>("getPendingSessionJson");
                    if (string.IsNullOrWhiteSpace(payload)
                        || string.Equals(payload, _lastNativeSessionPayload, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _nativeSessionReadErrorReported = false;
                    _lastNativeSessionPayload = payload;
                    if (TryParseSessionPayload(
                            payload,
                            out CompanionSessionRequest session,
                            out string validationMessage))
                    {
                        _pendingSessions.Enqueue(session);
                    }
                    else
                    {
                        _pendingValidationErrors.Enqueue(validationMessage);
                    }
                }
            }
            catch (Exception exception)
            {
                if (!_nativeSessionReadErrorReported)
                {
                    _nativeSessionReadErrorReported = true;
                    Debug.LogWarning("Android companion session could not be read: " + exception.Message);
                }
            }
        }

        private void OnNativeMessage(string[] values)
        {
            string payload = values != null && values.Length > 0 ? values[0] : null;
            if (TryParsePayload(payload, out CompanionLoginRequest request, out string validationMessage))
            {
                _pendingRequests.Enqueue(request);
            }
            else
            {
                _pendingValidationErrors.Enqueue(validationMessage);
            }
        }

        private void OnNativeQuickConnectMessage(string[] values)
        {
            string payload = values != null && values.Length > 0 ? values[0] : null;
            if (TryParseQuickConnectPayload(
                    payload,
                    out CompanionQuickConnectRequest request,
                    out string validationMessage))
            {
                _pendingQuickConnectRequests.Enqueue(request);
            }
            else
            {
                _pendingValidationErrors.Enqueue(validationMessage);
            }
        }

        private void OnNativeQuickConnectCancellation(string[] values)
        {
            _pendingQuickConnectCancellations.Enqueue(true);
        }

        private void OnNativeGlassesMessage(string[] values)
        {
            if (!TryParseGlassesEvent(values, out bool connected))
            {
                return;
            }

            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    activity?.Call("setGlassesPresentationState", connected);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RayNeo glasses state could not be published: " + exception.Message);
            }
        }
#endif

        private static string StateKey(CompanionLoginState state)
        {
            switch (state)
            {
                case CompanionLoginState.Initializing:
                    return "initializing";
                case CompanionLoginState.LoginRequired:
                    return "login_required";
                case CompanionLoginState.Connecting:
                    return "connecting";
                case CompanionLoginState.QuickConnectWaiting:
                    return "quick_connect_waiting";
                case CompanionLoginState.Ready:
                    return "ready";
                default:
                    return "offline";
            }
        }
    }
}
