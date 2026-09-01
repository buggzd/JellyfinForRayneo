using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class LoginView
    {
        private readonly GameObject _root;
        private readonly Text _stateLabel;
        private readonly Text _message;

        public LoginView(Transform parent)
        {
            RectTransform rootRect = UiFactory.CreateRect("Login Screen", parent);
            UiFactory.Stretch(rootRect);
            _root = rootRect.gameObject;

            Image card = UiFactory.CreatePanel("Phone Connection Card", rootRect, UiTheme.Surface);
            UiFactory.SetRect(
                card.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1040f, 700f));

            Text eyebrow = UiFactory.CreateText(
                "Eyebrow",
                card.transform,
                "RAYNEO AIR  ·  JELLYFIN COMPANION",
                22,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                eyebrow.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -48f),
                new Vector2(900f, 42f));

            Text title = UiFactory.CreateText(
                "Title",
                card.transform,
                "请在手机上连接 Jellyfin",
                50,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -106f),
                new Vector2(900f, 74f));

            Text hint = UiFactory.CreateText(
                "Phone Connection Hint",
                card.transform,
                "地址、用户名和密码输入已移至手机屏幕，眼镜中无需使用遥控器打字。",
                24,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                hint.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -176f),
                new Vector2(900f, 62f));

            CreateStep(card.transform, "1", "摘下眼镜或查看手机屏幕", -270f);
            CreateStep(card.transform, "2", "输入 Jellyfin 地址与帐号并点击连接", -362f);
            CreateStep(card.transform, "3", "连接成功后回到眼镜浏览海报墙", -454f);

            Image statusPanel = UiFactory.CreatePanel(
                "Companion Status",
                card.transform,
                new Color(0.06f, 0.07f, 0.11f, 0.98f));
            UiFactory.SetRect(
                statusPanel.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 42f),
                new Vector2(900f, 112f));

            _stateLabel = UiFactory.CreateText(
                "Connection State",
                statusPanel.transform,
                "等待手机操作",
                23,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                _stateLabel.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(840f, 36f));

            _message = UiFactory.CreateText(
                "Message",
                statusPanel.transform,
                "请在手机端完成连接。",
                21,
                UiTheme.TextSecondary,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                _message.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 22f),
                new Vector2(840f, 46f));
        }

        public void Show(bool visible)
        {
            _root.SetActive(visible);
        }

        public void SetBusy(bool busy)
        {
            _stateLabel.text = busy ? "正在连接 Jellyfin…" : "等待手机操作";
            _stateLabel.color = busy ? UiTheme.AccentBright : UiTheme.TextSecondary;
        }

        public void SetMessage(string message, bool isError)
        {
            _message.text = message ?? string.Empty;
            _message.color = isError ? UiTheme.Danger : UiTheme.TextSecondary;
        }

        private static void CreateStep(Transform parent, string number, string description, float y)
        {
            Image row = UiFactory.CreatePanel(
                "Phone Step " + number,
                parent,
                new Color(0.08f, 0.09f, 0.14f, 0.96f));
            UiFactory.SetRect(
                row.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, y),
                new Vector2(820f, 70f));

            Text numberLabel = UiFactory.CreateText(
                "Step Number",
                row.transform,
                number,
                28,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                numberLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(38f, 0f),
                new Vector2(56f, 56f));

            Text descriptionLabel = UiFactory.CreateText(
                "Step Description",
                row.transform,
                description,
                24,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                descriptionLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(440f, 0f),
                new Vector2(700f, 56f));
        }
    }
}
