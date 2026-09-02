using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    internal sealed class EmptyStateView
    {
        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly Image _glow;
        private readonly Image _accentLine;
        private readonly Image _centerPoster;
        private readonly Image _statusDot;
        private readonly Text _eyebrow;
        private readonly Text _title;
        private readonly Text _message;

        public GameObject Root => _root;

        public EmptyStateView(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            RectTransform root = UiFactory.CreateRect(name, parent);
            UiFactory.SetRect(
                root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                anchoredPosition,
                size);
            _root = root.gameObject;

            _glow = UiFactory.CreateGlowPanel(
                "State Glow",
                root,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.12f));
            UiFactory.SetRect(
                _glow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-270f, 0f),
                new Vector2(520f, 360f));
            UiAmbientFloat glowMotion = _glow.gameObject.AddComponent<UiAmbientFloat>();
            glowMotion.Amplitude = new Vector2(10f, 7f);
            glowMotion.Speed = 0.12f;
            glowMotion.Phase = 0.6f;

            Image shadow = UiFactory.CreateRoundedPanel(
                "State Card Shadow",
                root,
                new Color(0f, 0f, 0f, 0.48f));
            shadow.raycastTarget = false;
            UiFactory.SetRect(
                shadow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -11f),
                new Vector2(size.x - 28f, size.y - 50f));

            Image card = UiFactory.CreateRoundedPanel("State Card", root, Color.white);
            card.raycastTarget = false;
            UiFactory.SetRect(
                card.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(size.x - 28f, size.y - 50f));
            UiGradient cardGradient = card.gameObject.AddComponent<UiGradient>();
            cardGradient.StartColor = new Color(0.076f, 0.087f, 0.116f, 0.96f);
            cardGradient.EndColor = new Color(0.027f, 0.033f, 0.052f, 0.92f);
            cardGradient.Horizontal = true;
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = UiTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            _accentLine = UiFactory.CreateRoundedPanel(
                "State Accent",
                card.transform,
                UiTheme.AccentBright);
            _accentLine.raycastTarget = false;
            UiFactory.SetRect(
                _accentLine.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(7f, 0f),
                new Vector2(5f, size.y - 112f));

            Image iconPlate = UiFactory.CreateRoundedPanel(
                "Library Signal",
                card.transform,
                new Color(0.105f, 0.12f, 0.16f, 0.92f));
            iconPlate.raycastTarget = false;
            UiFactory.SetRect(
                iconPlate.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(112f, 0f),
                new Vector2(126f, 126f));
            Outline iconOutline = iconPlate.gameObject.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0.82f, 0.91f, 0.96f, 0.12f);
            iconOutline.effectDistance = new Vector2(1f, -1f);

            Image iconGlow = UiFactory.CreateGlowPanel(
                "Icon Glow",
                iconPlate.transform,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.16f));
            UiFactory.Stretch(iconGlow.rectTransform, 5f, 5f, 5f, 5f);

            Color[] posterColors =
            {
                new Color(0.29f, 0.35f, 0.47f, 0.76f),
                UiTheme.Accent,
                new Color(0.49f, 0.39f, 0.79f, 0.90f)
            };
            for (int index = 0; index < posterColors.Length; index++)
            {
                Image poster = UiFactory.CreateRoundedPanel(
                    "Signal Poster " + (index + 1),
                    iconPlate.transform,
                    posterColors[index]);
                poster.raycastTarget = false;
                UiFactory.SetRect(
                    poster.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2((index - 1) * 25f, (index == 1 ? 5f : -2f)),
                    new Vector2(index == 1 ? 38f : 31f, index == 1 ? 66f : 56f));
                if (index == 1)
                {
                    _centerPoster = poster;
                }
            }

            Image signalRing = UiFactory.CreateGlowPanel(
                "Status Signal",
                iconPlate.transform,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.24f));
            UiFactory.SetRect(
                signalRing.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-18f, 18f),
                new Vector2(38f, 38f));
            UiSignalPulse signalPulse = signalRing.gameObject.AddComponent<UiSignalPulse>();
            signalPulse.CycleSeconds = 2.8f;
            signalPulse.StartScale = 0.62f;
            signalPulse.EndScale = 1.18f;

            _statusDot = UiFactory.CreateRoundedPanel(
                "Status Dot",
                iconPlate.transform,
                UiTheme.AccentBright);
            _statusDot.raycastTarget = false;
            UiFactory.SetRect(
                _statusDot.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-18f, 18f),
                new Vector2(10f, 10f));

            _eyebrow = UiFactory.CreateText(
                "State Eyebrow",
                card.transform,
                string.Empty,
                15,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _eyebrow.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(202f, 72f),
                new Vector2(-238f, 28f));

            _title = UiFactory.CreateText(
                "State Title",
                card.transform,
                string.Empty,
                35,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                _title.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(202f, 20f),
                new Vector2(-238f, 54f));

            _message = UiFactory.CreateText(
                "State Message",
                card.transform,
                string.Empty,
                21,
                UiTheme.TextSecondary,
                TextAnchor.MiddleLeft);
            _message.lineSpacing = 1.15f;
            UiFactory.SetRect(
                _message.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(202f, -53f),
                new Vector2(-238f, 74f));

            _motion = UiFactory.AddViewMotion(_root, 16f, 0.985f);
            _motion.EnterDuration = 0.36f;
            _motion.ExitDuration = 0.16f;
            _motion.SetVisibleImmediately(false);
        }

        public void SetContent(
            string eyebrow,
            string title,
            string message,
            Color accent)
        {
            _eyebrow.text = eyebrow ?? string.Empty;
            _title.text = title ?? string.Empty;
            _message.text = message ?? string.Empty;
            _eyebrow.color = accent;
            _accentLine.color = accent;
            _centerPoster.color = accent;
            _statusDot.color = accent;
            _glow.color = new Color(accent.r, accent.g, accent.b, 0.12f);
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                if (!_motion.IsVisible)
                {
                    _motion.Show(0.05f);
                }
                return;
            }

            if (_motion.IsVisible)
            {
                _motion.Hide();
            }
        }
    }
}
