using System;
using UnityEditor;
using UnityEngine;

namespace JellyfinForRayNeo.Editor
{
    [InitializeOnLoad]
    public sealed class CompanionSimulatorWindow : EditorWindow
    {
        private string _serverUrl = "http://";
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string _localMessage = string.Empty;
        private CompanionLoginState _lastObservedState = (CompanionLoginState)(-1);
        private double _nextRepaintAt;

        static CompanionSimulatorWindow()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Jellyfin for RayNeo/Companion Simulator")]
        public static void Open()
        {
            CompanionSimulatorWindow window = GetWindow<CompanionSimulatorWindow>();
            window.titleContent = new GUIContent("RayNeo Phone");
            window.minSize = new Vector2(390f, 600f);
            window.Show();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || Application.isBatchMode)
            {
                return;
            }

            EditorApplication.delayCall += Open;
        }

        private void OnEnable()
        {
            EditorApplication.update += RefreshPeriodically;
        }

        private void OnDisable()
        {
            _password = string.Empty;
            EditorApplication.update -= RefreshPeriodically;
        }

        private void OnGUI()
        {
            CompanionLoginSnapshot snapshot = CompanionLoginRuntime.Current;
            SynchronizeNonSecretFields(snapshot);

            EditorGUILayout.Space(12f);
            GUILayout.Label("RayNeo 手机伴侣模拟器", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox(
                "这个窗口模拟手机原生登录页；Game View 模拟眼镜画面。进入 Play Mode 后在这里输入 Jellyfin 信息。",
                MessageType.Info);

            DrawStatus(snapshot);

            EditorGUILayout.Space(10f);
            bool canSubmit = Application.isPlaying
                && snapshot.State == CompanionLoginState.LoginRequired;
            using (new EditorGUI.DisabledScope(!canSubmit))
            {
                _serverUrl = EditorGUILayout.TextField("服务器地址", _serverUrl);

                EditorGUILayout.Space(8f);
                if (GUILayout.Button("申请 Jellyfin 快速登录码", GUILayout.Height(34f)))
                {
                    SubmitQuickConnect();
                }

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("或使用帐号密码", EditorStyles.boldLabel);
                _username = EditorGUILayout.TextField("用户名", _username);
                _password = EditorGUILayout.PasswordField("密码", _password);

                EditorGUILayout.Space(12f);
                if (GUILayout.Button("连接并在眼镜中打开", GUILayout.Height(38f)))
                {
                    SubmitLogin();
                }
            }

            if (snapshot.State == CompanionLoginState.QuickConnectWaiting
                && !string.IsNullOrWhiteSpace(snapshot.QuickConnectCode))
            {
                EditorGUILayout.Space(10f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("快速登录码", snapshot.QuickConnectCode);
                    EditorGUILayout.HelpBox(
                        "在已登录的 Jellyfin App 中打开“快速连接”，或使用下方网页完成授权。",
                        MessageType.Info);
                    if (GUILayout.Button("在浏览器中打开授权页", GUILayout.Height(32f)))
                    {
                        Application.OpenURL(BuildQuickConnectUrl(
                            snapshot.ServerUrl,
                            snapshot.QuickConnectCode));
                    }
                    if (GUILayout.Button("取消快速登录", GUILayout.Height(28f)))
                    {
                        CompanionLoginRuntime.CancelQuickConnect();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_localMessage))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_localMessage, MessageType.Warning);
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "调试眼镜交互时保持 Game View 聚焦，继续使用 RayNeo SDK 的 Ctrl/鼠标/WASD 模拟头部射线与点击。模拟器不会保存密码。",
                MessageType.None);
        }

        private void DrawStatus(CompanionLoginSnapshot snapshot)
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Unity 状态", StateLabel(snapshot.State));
                if (!string.IsNullOrWhiteSpace(snapshot.ServerUrl))
                {
                    EditorGUILayout.LabelField("服务器", snapshot.ServerUrl);
                }
                if (!string.IsNullOrWhiteSpace(snapshot.UserName))
                {
                    EditorGUILayout.LabelField("用户", snapshot.UserName);
                }

                MessageType type = snapshot.IsError ? MessageType.Error : MessageType.None;
                EditorGUILayout.HelpBox(snapshot.Message, type);
            }
        }

        private void SubmitLogin()
        {
            _localMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(_serverUrl))
            {
                _localMessage = "请输入 Jellyfin 服务器地址。";
                return;
            }
            if (string.IsNullOrWhiteSpace(_username))
            {
                _localMessage = "请输入 Jellyfin 用户名。";
                return;
            }

            bool submitted = CompanionLoginRuntime.SubmitLogin(
                _serverUrl,
                _username,
                _password);
            _password = string.Empty;
            GUI.FocusControl(null);

            if (!submitted)
            {
                _localMessage = "登录桥尚未就绪，请确认 Main 场景正在 Play Mode 中运行。";
            }
        }

        private void SubmitQuickConnect()
        {
            _localMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(_serverUrl))
            {
                _localMessage = "请先输入 Jellyfin 服务器地址。";
                return;
            }

            if (!CompanionLoginRuntime.SubmitQuickConnect(_serverUrl))
            {
                _localMessage = "快速登录桥尚未就绪，请确认 Main 场景正在 Play Mode 中运行。";
            }
            GUI.FocusControl(null);
        }

        private void SynchronizeNonSecretFields(CompanionLoginSnapshot snapshot)
        {
            if (snapshot == null || snapshot.State == _lastObservedState)
            {
                return;
            }

            _lastObservedState = snapshot.State;
            if (!string.IsNullOrWhiteSpace(snapshot.ServerUrl))
            {
                _serverUrl = snapshot.ServerUrl;
            }
            if (snapshot.UserName != null)
            {
                _username = snapshot.UserName;
            }
            if (snapshot.State != CompanionLoginState.LoginRequired)
            {
                _localMessage = string.Empty;
            }
        }

        private void RefreshPeriodically()
        {
            if (EditorApplication.timeSinceStartup < _nextRepaintAt)
            {
                return;
            }

            _nextRepaintAt = EditorApplication.timeSinceStartup + 0.2d;
            Repaint();
        }

        private static string StateLabel(CompanionLoginState state)
        {
            switch (state)
            {
                case CompanionLoginState.Initializing:
                    return "正在初始化";
                case CompanionLoginState.LoginRequired:
                    return "等待手机登录";
                case CompanionLoginState.Connecting:
                    return "正在连接";
                case CompanionLoginState.QuickConnectWaiting:
                    return "等待授权登录码";
                case CompanionLoginState.Ready:
                    return "已连接";
                default:
                    return "未运行";
            }
        }

        private static string BuildQuickConnectUrl(string serverUrl, string code)
        {
            return (serverUrl ?? string.Empty).TrimEnd('/')
                + "/web/#/quickconnect?code="
                + Uri.EscapeDataString(code ?? string.Empty);
        }
    }
}
