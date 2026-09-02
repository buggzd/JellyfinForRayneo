using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    public sealed class LoginView
    {
        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly Text _stateLabel;
        private readonly Text _message;
        private readonly UiSignalPulse _statusPulse;

        public Transform FocusRoot => _root.transform;

        public LoginView(Transform parent)
        {
            RectTransform rootRect = UiFactory.CreateRect("Login Screen", parent);
            UiFactory.Stretch(rootRect);
            _root = rootRect.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 18f, 0.992f);
            UiFactory.CreateAmbientBackdrop(rootRect);

            Image card = UiFactory.CreateRoundedPanel("Phone Connection Card", rootRect, UiTheme.SurfaceGlass);
            UiFactory.SetRect(
                card.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(1120f, 700f));
            Outline cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = UiTheme.Border;
            cardOutline.effectDistance = new Vector2(1f, -1f);
            Shadow cardShadow = card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            cardShadow.effectDistance = new Vector2(0f, -12f);

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
                new Vector2(980f, 42f));

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
                new Vector2(980f, 74f));

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
                new Vector2(980f, 62f));

            GameObject phoneVisual = CreatePhoneVisual(card.transform);
            UiFactory.AddItemReveal(phoneVisual, 0.04f);
            CreateStep(card.transform, "1", "查看手机上的伴侣配置页", 230f, -274f, 0.08f);
            CreateStep(card.transform, "2", "发现服务器并完成 Jellyfin 登录", 230f, -362f, 0.12f);
            CreateStep(card.transform, "3", "连接完成后自动进入媒体库", 230f, -450f, 0.16f);

            Image statusPanel = UiFactory.CreateRoundedPanel(
                "Companion Status",
                card.transform,
                new Color(0.06f, 0.07f, 0.11f, 0.98f));
            UiFactory.SetRect(
                statusPanel.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 34f),
                new Vector2(1040f, 104f));
            statusPanel.raycastTarget = false;
            Outline statusOutline = statusPanel.gameObject.AddComponent<Outline>();
            statusOutline.effectColor = UiTheme.Border;
            statusOutline.effectDistance = new Vector2(1f, -1f);
            UiFactory.AddItemReveal(statusPanel.gameObject, 0.20f);

            Image statusGlow = UiFactory.CreateGlowPanel(
                "Connection Signal",
                statusPanel.transform,
                new Color(0.48f, 0.94f, 0.88f, 0.62f));
            UiFactory.SetRect(
                statusGlow.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(48f, 0f),
                new Vector2(52f, 52f));
            _statusPulse = statusGlow.gameObject.AddComponent<UiSignalPulse>();
            _statusPulse.CycleSeconds = 1.9f;
            _statusPulse.StartScale = 0.72f;
            _statusPulse.EndScale = 1.32f;
            _statusPulse.MinimumAlpha = 0.10f;

            _stateLabel = UiFactory.CreateText(
                "Connection State",
                statusPanel.transform,
                "等待手机操作",
                23,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _stateLabel.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(82f, -14f),
                new Vector2(-114f, 36f));

            _message = UiFactory.CreateText(
                "Message",
                statusPanel.transform,
                "请在手机端完成连接。",
                21,
                UiTheme.TextSecondary,
                TextAnchor.MiddleLeft);
            UiFactory.SetRect(
                _message.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(82f, 14f),
                new Vector2(-114f, 42f));
        }

        public void Show(bool visible)
        {
            if (visible)
            {
                _root.transform.SetAsLastSibling();
                _motion.Show();
            }
            else
            {
                _motion.Hide();
            }
        }

        public void SetBusy(bool busy)
        {
            _stateLabel.text = busy ? "正在连接 Jellyfin…" : "等待手机操作";
            _stateLabel.color = busy ? UiTheme.AccentBright : UiTheme.TextSecondary;
            _statusPulse.SetBaseColor(busy
                ? new Color(0.48f, 0.94f, 0.88f, 0.72f)
                : new Color(0.67f, 0.56f, 1f, 0.52f));
            UiFactory.RevealGraphic(_stateLabel, 0.20f);
        }

        public void SetMessage(string message, bool isError)
        {
            _message.text = message ?? string.Empty;
            _message.color = isError ? UiTheme.Danger : UiTheme.TextSecondary;
            _statusPulse.SetBaseColor(isError
                ? new Color(0.95f, 0.28f, 0.38f, 0.68f)
                : new Color(0.48f, 0.94f, 0.88f, 0.62f));
            UiFactory.RevealGraphic(_message, 0.24f);
        }

        private static GameObject CreatePhoneVisual(Transform parent)
        {
            Image visual = UiFactory.CreateRoundedPanel(
                "Phone Connection Visual",
                parent,
                new Color(0.045f, 0.052f, 0.078f, 0.90f));
            visual.raycastTarget = false;
            UiFactory.SetRect(
                visual.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-350f, -264f),
                new Vector2(280f, 276f));
            Outline visualOutline = visual.gameObject.AddComponent<Outline>();
            visualOutline.effectColor = UiTheme.Border;
            visualOutline.effectDistance = new Vector2(1f, -1f);

            for (int index = 0; index < 3; index++)
            {
                float size = 198f - index * 42f;
                Image glow = UiFactory.CreateGlowPanel(
                    "Phone Signal Ring " + (index + 1),
                    visual.transform,
                    index == 1
                        ? new Color(0.48f, 0.31f, 1f, 0.24f)
                        : new Color(0.16f, 0.78f, 0.73f, 0.22f));
                UiFactory.SetRect(
                    glow.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 18f),
                    new Vector2(size, size));
                UiSignalPulse pulse = glow.gameObject.AddComponent<UiSignalPulse>();
                pulse.CycleSeconds = 3.1f;
                pulse.Phase = index * 0.31f;
                pulse.StartScale = 0.72f;
                pulse.EndScale = 1.18f;
                pulse.MinimumAlpha = 0f;
            }

            Image phone = UiFactory.CreateRoundedPanel(
                "Phone Silhouette",
                visual.transform,
                new Color(0.015f, 0.021f, 0.034f, 0.98f));
            phone.raycastTarget = false;
            UiFactory.SetRect(
                phone.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f),
                new Vector2(112f, 186f));
            Outline phoneOutline = phone.gameObject.AddComponent<Outline>();
            phoneOutline.effectColor = new Color(0.82f, 0.91f, 0.96f, 0.32f);
            phoneOutline.effectDistance = new Vector2(1f, -1f);

            Image screen = UiFactory.CreateGradientPanel(
                "Phone Screen",
                phone.transform,
                new Color(0.17f, 0.58f, 0.55f, 0.90f),
                new Color(0.33f, 0.20f, 0.58f, 0.92f));
            UiFactory.Stretch(screen.rectTransform, 12f, 12f, 18f, 18f);

            Text mark = UiFactory.CreateText(
                "Phone Mark",
                screen.transform,
                "J",
                40,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(mark.rectTransform);

            Text label = UiFactory.CreateText(
                "Phone Visual Label",
                visual.transform,
                "手机端配置  ·  自动同步",
                19,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.SetRect(
                label.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(248f, 34f));
            return visual.gameObject;
        }

        private static void CreateStep(
            Transform parent,
            string number,
            string description,
            float x,
            float y,
            float revealDelay)
        {
            Image row = UiFactory.CreateRoundedPanel(
                "Phone Step " + number,
                parent,
                new Color(0.07f, 0.08f, 0.12f, 0.92f));
            row.raycastTarget = false;
            UiFactory.SetRect(
                row.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(x, y),
                new Vector2(580f, 70f));
            Outline rowOutline = row.gameObject.AddComponent<Outline>();
            rowOutline.effectColor = UiTheme.Border;
            rowOutline.effectDistance = new Vector2(1f, -1f);

            Image numberBadge = UiFactory.CreateRoundedPanel(
                "Step Badge",
                row.transform,
                new Color(0.19f, 0.45f, 0.42f, 0.78f));
            numberBadge.raycastTarget = false;
            UiFactory.SetRect(
                numberBadge.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(14f, 0f),
                new Vector2(48f, 48f));

            Text numberLabel = UiFactory.CreateText(
                "Step Number",
                numberBadge.transform,
                number,
                23,
                UiTheme.AccentBright,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UiFactory.Stretch(numberLabel.rectTransform);

            Text descriptionLabel = UiFactory.CreateText(
                "Step Description",
                row.transform,
                description,
                24,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.Stretch(descriptionLabel.rectTransform, 82f, 20f, 6f, 6f);
            UiFactory.AddItemReveal(row.gameObject, revealDelay);
        }
    }
}
