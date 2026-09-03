using System;
using System.Collections.Generic;
using UnityEngine;

namespace JellyfinForRayNeo
{
    internal enum GlassesWebMessageType
    {
        Unknown,
        ManageLogin,
        Logout,
        PlaybackState
    }

    [Serializable]
    internal sealed class GlassesWebMessage
    {
        private const int MaximumPayloadLength = 8192;
        private const int MaximumItemIdLength = 128;
        private const int MaximumTitleLength = 180;
        private const int MaximumSubtitleLength = 240;

        [SerializeField] private string type;
        [SerializeField] private string state;
        [SerializeField] private string itemId;
        [SerializeField] private string title;
        [SerializeField] private string subtitle;
        [SerializeField] private string playMethod;
        [SerializeField] private long positionTicks;
        [SerializeField] private long durationTicks;

        public GlassesWebMessageType Type { get; private set; }
        public string State => state;
        public string ItemId => itemId;
        public string Title => title;
        public string Subtitle => subtitle;
        public string PlayMethod => playMethod;
        public long PositionTicks => positionTicks;
        public long DurationTicks => durationTicks;

        public static bool TryParse(string payload, out GlassesWebMessage message)
        {
            message = null;
            if (string.IsNullOrWhiteSpace(payload)
                || payload.Length > MaximumPayloadLength)
            {
                return false;
            }

            try
            {
                GlassesWebMessage parsed = JsonUtility.FromJson<GlassesWebMessage>(payload);
                if (parsed == null || !parsed.TryNormalize())
                {
                    return false;
                }

                message = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool TryNormalize()
        {
            switch ((type ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "manage_login":
                    Type = GlassesWebMessageType.ManageLogin;
                    break;
                case "logout":
                    Type = GlassesWebMessageType.Logout;
                    break;
                case "playback_state":
                    Type = GlassesWebMessageType.PlaybackState;
                    break;
                default:
                    return false;
            }

            state = Normalize(state, 32).ToLowerInvariant();
            itemId = Normalize(itemId, MaximumItemIdLength);
            title = Normalize(title, MaximumTitleLength);
            subtitle = Normalize(subtitle, MaximumSubtitleLength);
            playMethod = Normalize(playMethod, 32);
            positionTicks = Math.Max(0L, positionTicks);
            durationTicks = Math.Max(0L, durationTicks);

            return Type != GlassesWebMessageType.PlaybackState
                || IsPlaybackState(state);
        }

        private static bool IsPlaybackState(string value)
        {
            switch (value)
            {
                case "preparing":
                case "buffering":
                case "playing":
                case "paused":
                case "ended":
                case "error":
                case "stopped":
                    return true;
                default:
                    return false;
            }
        }

        private static string Normalize(string value, int maximumLength)
        {
            string normalized = value != null ? value.Trim() : string.Empty;
            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength);
        }
    }

    internal interface IGlassesWebViewHost
    {
        bool IsSupported { get; }
        bool Show();
        void Hide();
        bool SendCommand(string command);
        void RefreshBootstrapState();
    }

    [DisallowMultipleComponent]
    public sealed class GlassesWebViewPresenter : MonoBehaviour
    {
        private const int MaximumPendingCommands = 32;

        private readonly Queue<string> _pendingCommands = new Queue<string>();
        private IGlassesWebViewHost _host;
        private bool _active;
        private bool _showRequested;

        public bool IsActive => _active;

        internal event Action<GlassesWebMessage> MessageReceived;

        internal int PendingCommandCount => _pendingCommands.Count;

        private void Awake()
        {
            if (_host == null)
            {
                _host = new AndroidGlassesWebViewHost();
            }
        }

        private void Update()
        {
            if (_showRequested
                && !_active
                && _host != null
                && _host.IsSupported)
            {
                _active = _host.Show();
            }
            PumpPendingCommands();
        }

        private void OnDestroy()
        {
            if (_active)
            {
                _host?.Hide();
            }
            _active = false;
            _showRequested = false;
            _pendingCommands.Clear();
        }

        public bool Show()
        {
            _showRequested = true;
            if (_active)
            {
                return true;
            }
            if (_host == null || !_host.IsSupported)
            {
                return false;
            }

            _active = _host.Show();
            return _active;
        }

        public void Hide()
        {
            _showRequested = false;
            if (!_active)
            {
                return;
            }

            _host?.Hide();
            _active = false;
            _pendingCommands.Clear();
        }

        public bool DispatchRemoteCommand(CompanionRemoteCommand command)
        {
            return QueueCommand(ToWebCommand(command));
        }

        public bool DispatchVolume(int percentage)
        {
            return QueueCommand("volume:" + Mathf.Clamp(percentage, 0, 100));
        }

        public void RefreshBootstrapState()
        {
            if (_active)
            {
                _host?.RefreshBootstrapState();
            }
        }

        public void OnGlassesWebMessage(string payload)
        {
            if (!GlassesWebMessage.TryParse(payload, out GlassesWebMessage message))
            {
                Debug.LogWarning("Glasses WebView sent an invalid native message.");
                return;
            }

            MessageReceived?.Invoke(message);
        }

        internal void InitializeForTests(IGlassesWebViewHost host)
        {
            if (_active)
            {
                throw new InvalidOperationException(
                    "The glasses WebView host cannot be replaced while active.");
            }
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        internal void PumpPendingCommands()
        {
            if (!_active || _host == null)
            {
                return;
            }

            while (_pendingCommands.Count > 0)
            {
                string command = _pendingCommands.Peek();
                if (!_host.SendCommand(command))
                {
                    return;
                }
                _pendingCommands.Dequeue();
            }
        }

        internal static string ToWebCommand(CompanionRemoteCommand command)
        {
            switch (command)
            {
                case CompanionRemoteCommand.Up:
                    return "up";
                case CompanionRemoteCommand.Down:
                    return "down";
                case CompanionRemoteCommand.Left:
                    return "left";
                case CompanionRemoteCommand.Right:
                    return "right";
                case CompanionRemoteCommand.Submit:
                    return "enter";
                case CompanionRemoteCommand.Back:
                    return "back";
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private bool QueueCommand(string command)
        {
            if (!_active || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            while (_pendingCommands.Count >= MaximumPendingCommands)
            {
                _pendingCommands.Dequeue();
            }
            _pendingCommands.Enqueue(command);
            PumpPendingCommands();
            return true;
        }
    }

    internal sealed class AndroidGlassesWebViewHost : IGlassesWebViewHost
    {
        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public bool Show()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CallActivity(activity => activity.Call<bool>("showGlassesWebView"), false);
#else
            return false;
#endif
        }

        public void Hide()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CallActivity(
                activity =>
                {
                    activity.Call("hideGlassesWebView");
                    return true;
                },
                false);
#endif
        }

        public bool SendCommand(string command)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CallActivity(
                activity => activity.Call<bool>("dispatchGlassesWebCommand", command),
                false);
#else
            return false;
#endif
        }

        public void RefreshBootstrapState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CallActivity(
                activity =>
                {
                    activity.Call("refreshGlassesWebBootstrap");
                    return true;
                },
                false);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static T CallActivity<T>(Func<AndroidJavaObject, T> call, T fallback)
        {
            try
            {
                using (AndroidJavaClass unityPlayer =
                       new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    return activity == null ? fallback : call(activity);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Glasses WebView bridge call failed: " + exception.Message);
                return fallback;
            }
        }
#endif
    }
}
