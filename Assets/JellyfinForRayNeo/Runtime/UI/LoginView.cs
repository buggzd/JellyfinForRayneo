using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    /// <summary>
    /// A quiet brand surface used before a Jellyfin session becomes available.
    /// Authentication and connection guidance intentionally remain on the phone.
    /// </summary>
    public sealed class LoginView
    {
        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly UiSignalPulse _brandPulse;

        public Transform FocusRoot => _root.transform;

        public LoginView(Transform parent)
        {
            RectTransform rootRect = UiFactory.CreateRect("Startup Screen", parent);
            UiFactory.Stretch(rootRect);
            _root = rootRect.gameObject;
            _motion = UiFactory.AddViewMotion(_root, 0f, 0.994f);
            _motion.EnterDuration = 0.46f;
            _motion.ExitDuration = 0.28f;
            UiFactory.CreateAmbientBackdrop(rootRect);

            Image horizon = UiFactory.CreateGlowPanel(
                "Startup Horizon",
                rootRect,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.13f));
            UiFactory.SetRect(
                horizon.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f),
                new Vector2(1120f, 610f));
            UiAmbientFloat horizonMotion = horizon.gameObject.AddComponent<UiAmbientFloat>();
            horizonMotion.Amplitude = new Vector2(18f, 8f);
            horizonMotion.Speed = 0.055f;
            horizonMotion.ScalePulse = 0.025f;

            RectTransform brand = UiFactory.CreateRect("Startup Brand", rootRect);
            UiFactory.SetRect(
                brand,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(760f, 250f));

            Image sparkPulse = UiFactory.CreateGlowPanel(
                "Startup Spark Pulse",
                brand,
                new Color(UiTheme.AccentBright.r, UiTheme.AccentBright.g, UiTheme.AccentBright.b, 0.38f));
            UiFactory.SetRect(
                sparkPulse.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-200f, 44f),
                new Vector2(72f, 72f));
            _brandPulse = sparkPulse.gameObject.AddComponent<UiSignalPulse>();
            _brandPulse.CycleSeconds = 3.8f;
            _brandPulse.StartScale = 0.58f;
            _brandPulse.EndScale = 1.26f;
            _brandPulse.MinimumAlpha = 0.05f;

            Image spark = UiFactory.CreateRoundedPanel(
                "Startup Spark",
                brand,
                UiTheme.AccentBright);
            spark.raycastTarget = false;
            UiFactory.SetRect(
                spark.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-200f, 44f),
                new Vector2(12f, 12f));
            Shadow sparkGlow = spark.gameObject.AddComponent<Shadow>();
            sparkGlow.effectColor = new Color(0.40f, 0.88f, 1f, 0.82f);
            sparkGlow.effectDistance = Vector2.zero;

            Text wordmark = UiFactory.CreateText(
                "Startup Wordmark",
                brand,
                "LUCENT",
                72,
                UiTheme.TextPrimary,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                wordmark.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 34f),
                new Vector2(620f, 94f));

            Text subtitle = UiFactory.CreateText(
                "Startup Subtitle",
                brand,
                "MEDIA  /  LIGHT",
                16,
                UiTheme.TextMuted,
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                subtitle.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f),
                new Vector2(360f, 32f));

            Image lightLine = UiFactory.CreateGradientPanel(
                "Startup Light Line",
                brand,
                Color.clear,
                new Color(UiTheme.AccentBright.r, UiTheme.AccentBright.g, UiTheme.AccentBright.b, 0.34f),
                true);
            UiFactory.SetRect(
                lightLine.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -62f),
                new Vector2(270f, 1f));

            Text platform = UiFactory.CreateText(
                "Startup Platform",
                brand,
                "JELLYFIN  ·  RAYNEO",
                13,
                new Color(UiTheme.TextSecondary.r, UiTheme.TextSecondary.g, UiTheme.TextSecondary.b, 0.46f),
                TextAnchor.MiddleCenter);
            UiFactory.SetRect(
                platform.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -91f),
                new Vector2(360f, 28f));

            UiFactory.AddItemReveal(brand.gameObject, 0.08f);
            UiFactory.CreateFilmGrain(rootRect, 0.025f);
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
            _brandPulse.CycleSeconds = busy ? 1.8f : 3.8f;
            _brandPulse.SetBaseColor(busy
                ? new Color(UiTheme.AccentBright.r, UiTheme.AccentBright.g, UiTheme.AccentBright.b, 0.52f)
                : new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.38f));
        }

        public void SetMessage(string message, bool isError)
        {
            // Status and actionable errors are published to the phone companion.
            // The glasses deliberately remain a non-interactive LUCENT brand surface.
        }
    }
}
