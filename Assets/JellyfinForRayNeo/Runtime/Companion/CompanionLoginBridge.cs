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

    public sealed class CompanionLoginSnapshot
    {
        public CompanionLoginSnapshot(
            CompanionLoginState state,
            string message,
            bool isError,
            string serverUrl,
            string userName)
        {
            State = state;
            Message = message ?? string.Empty;
            IsError = isError;
            ServerUrl = serverUrl ?? string.Empty;
            UserName = userName ?? string.Empty;
        }

        public CompanionLoginState State { get; }
        public string Message { get; }
        public bool IsError { get; }
        public string ServerUrl { get; }
        public string UserName { get; }
    }

    public static class CompanionLoginRuntime
    {
        private static CompanionLoginSnapshot _current = CreateOfflineSnapshot();

        internal static event Action<CompanionLoginRequest> LoginSubmitted;

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

        internal static void SetSnapshot(CompanionLoginSnapshot snapshot)
        {
            _current = snapshot ?? CreateOfflineSnapshot();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            LoginSubmitted = null;
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

        private readonly ConcurrentQueue<CompanionLoginRequest> _pendingRequests =
            new ConcurrentQueue<CompanionLoginRequest>();
        private readonly ConcurrentQueue<string> _pendingValidationErrors =
            new ConcurrentQueue<string>();
        private bool _disposed;

#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _nativeListenerRegistered;
#endif

        public CompanionLoginBridge()
        {
            CompanionLoginRuntime.LoginSubmitted += QueueRuntimeLogin;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                NativeModule.Instance.RegesitNativeMsgDispatchListener(LoginMessageType, OnNativeMessage);
                _nativeListenerRegistered = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RayNeo companion listener could not be registered: " + exception.Message);
            }
#endif
        }

        public event Action<CompanionLoginRequest> LoginRequested;

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

        public void Pump()
        {
            if (_disposed)
            {
                return;
            }

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
        }

        public void PublishState(
            CompanionLoginState state,
            string message,
            bool isError = false,
            string serverUrl = null,
            string userName = null)
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
                userName);
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
                        snapshot.UserName);
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

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_nativeListenerRegistered)
            {
                try
                {
                    NativeModule.Instance.UnRegistNativeMsgDispatchListener(LoginMessageType);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("RayNeo companion listener could not be removed: " + exception.Message);
                }
                _nativeListenerRegistered = false;
            }
#endif

            while (_pendingRequests.TryDequeue(out CompanionLoginRequest request))
            {
                request.ClearPassword();
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

#if UNITY_ANDROID && !UNITY_EDITOR
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
                case CompanionLoginState.Ready:
                    return "ready";
                default:
                    return "offline";
            }
        }
    }
}
