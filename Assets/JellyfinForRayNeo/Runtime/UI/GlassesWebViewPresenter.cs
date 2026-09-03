using System;
using System.Collections.Generic;
using UnityEngine;

namespace JellyfinForRayNeo
{
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
