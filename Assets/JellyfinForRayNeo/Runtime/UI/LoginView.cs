using System;
using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class LoginView
    {
        private readonly GameObject _root;
        private readonly InputField _serverInput;
        private readonly InputField _usernameInput;
        private readonly InputField _passwordInput;
        private readonly Button _loginButton;
        private readonly Text _loginButtonLabel;
        private readonly Text _message;

        public event Action<string, string, string> LoginRequested;

        public LoginView(Transform parent)
        {
            RectTransform rootRect = UiFactory.CreateRect("Login Screen", parent);
            UiFactory.Stretch(rootRect);
            _root = rootRect.gameObject;

            Image card = UiFactory.CreatePanel("Login Card", rootRect, UiTheme.Surface);
            UiFactory.SetRect(
                card.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(920f, 760f));

            Text eyebrow = UiFactory.CreateText(
                "Eyebrow",
                card.transform,
                "RAYNEO AIR  ·  第三方客户端",
                22,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(eyebrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(820f, 42f));

            Text title = UiFactory.CreateText("Title", card.transform, "连接 Jellyfin", 54, UiTheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(820f, 80f));

            Text subtitle = UiFactory.CreateText(
                "Subtitle",
                card.transform,
                "在眼镜中浏览海报墙，并同步你的继续观看与播放进度",
                24,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -174f), new Vector2(820f, 52f));

            _serverInput = CreateLabeledInput(card.transform, "服务器地址", "例如：http://192.168.1.20:8096", -252f, InputField.ContentType.Standard);
            _serverInput.keyboardType = TouchScreenKeyboardType.URL;
            _usernameInput = CreateLabeledInput(card.transform, "用户名", "Jellyfin 用户名", -374f, InputField.ContentType.Standard);
            _passwordInput = CreateLabeledInput(card.transform, "密码", "密码不会保存在设备上", -496f, InputField.ContentType.Password);

            _loginButton = UiFactory.CreateButton("Login Button", card.transform, "登录并加载媒体库", UiTheme.Accent, UiTheme.TextPrimary, 30);
            UiFactory.SetRect(
                _loginButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 82f),
                new Vector2(760f, 70f));
            _loginButtonLabel = _loginButton.GetComponentInChildren<Text>();
            _loginButton.onClick.AddListener(() => LoginRequested?.Invoke(_serverInput.text, _usernameInput.text, _passwordInput.text));

            _message = UiFactory.CreateText("Message", card.transform, string.Empty, 22, UiTheme.TextSecondary, TextAnchor.MiddleCenter);
            UiFactory.SetRect(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(800f, 46f));
        }

        public void Show(bool visible)
        {
            _root.SetActive(visible);
            _passwordInput.text = string.Empty;
        }

        public void SetInitialValues(string serverUrl, string username)
        {
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                _serverInput.text = serverUrl;
            }
            if (!string.IsNullOrWhiteSpace(username))
            {
                _usernameInput.text = username;
            }
        }

        public void SetBusy(bool busy)
        {
            _loginButton.interactable = !busy;
            _serverInput.interactable = !busy;
            _usernameInput.interactable = !busy;
            _passwordInput.interactable = !busy;
            _loginButtonLabel.text = busy ? "正在连接…" : "登录并加载媒体库";
        }

        public void SetMessage(string message, bool isError)
        {
            _message.text = message ?? string.Empty;
            _message.color = isError ? UiTheme.Danger : UiTheme.TextSecondary;
        }

        private static InputField CreateLabeledInput(
            Transform parent,
            string label,
            string placeholder,
            float y,
            InputField.ContentType contentType)
        {
            Text labelText = UiFactory.CreateText(label + " Label", parent, label, 22, UiTheme.TextSecondary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetRect(labelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(760f, 34f));

            InputField input = UiFactory.CreateInputField(label + " Input", parent, placeholder, contentType);
            UiFactory.SetRect(
                input.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, y - 42f),
                new Vector2(760f, 66f));
            return input;
        }
    }
}
