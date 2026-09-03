using UnityEngine;
using UnityEngine.UI;

namespace JellyfinForRayNeo
{
    internal sealed class VolumeOverlayView
    {
        private const float VisibleDuration = 2.4f;
        private const float FillSmoothTime = 0.09f;

        private readonly GameObject _root;
        private readonly UiViewMotion _motion;
        private readonly Text _percentageLabel;
        private readonly RectTransform _levelFill;
        private readonly Image[] _meterSegments;
        private float _displayedLevel;
        private float _targetLevel;
        private float _fillVelocity;
        private float _hideAt;

        public VolumeOverlayView(Transform parent)
        {
            RectTransform root = UiFactory.CreateRect("Volume Overlay", parent);
            UiFactory.SetRect(
                root,
                Vector2.one,
                Vector2.one,
                Vector2.one,
                new Vector2(-54f, -48f),
                new Vector2(590f, 146f));
            _root = root.gameObject;

            Image ambientGlow = UiFactory.CreateGlowPanel(
                "Volume Ambient Glow",
                root,
                new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.20f));
            UiFactory.SetRect(
                ambientGlow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -4f),
                new Vector2(700f, 260f));

            Image shadow = UiFactory.CreateRoundedPanel(
                "Volume Shadow",
                root,
                new Color(0f, 0f, 0f, 0.58f));
            shadow.raycastTarget = false;
            UiFactory.SetRect(
                shadow.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f),
                new Vector2(-4f, -4f));

            Image surface = UiFactory.CreateRoundedPanel(
                "Volume Surface",
                root,
                Color.white);
            surface.raycastTarget = false;
            UiFactory.Stretch(surface.rectTransform, 2f, 2f, 2f, 6f);
            UiGradient surfaceGradient = surface.gameObject.AddComponent<UiGradient>();
            surfaceGradient.StartColor = new Color(0.075f, 0.090f, 0.118f, 0.985f);
            surfaceGradient.EndColor = new Color(0.025f, 0.032f, 0.052f, 0.975f);
            surfaceGradient.Horizontal = true;
            Outline surfaceOutline = surface.gameObject.AddComponent<Outline>();
            surfaceOutline.effectColor = new Color(
                UiTheme.Accent.r,
                UiTheme.Accent.g,
                UiTheme.Accent.b,
                0.24f);
            surfaceOutline.effectDistance = new Vector2(1f, -1f);

            Image accent = UiFactory.CreateGradientPanel(
                "Volume Accent",
                surface.transform,
                UiTheme.AccentBright,
                UiTheme.AccentSecondary,
                true);
            UiFactory.Stretch(accent.rectTransform, 20f, 20f, 0f, 137f);

            Image iconPlate = UiFactory.CreateRoundedPanel(
                "Volume Icon Plate",
                surface.transform,
                new Color(1f, 1f, 1f, 0.075f));
            iconPlate.raycastTarget = false;
            UiFactory.SetRect(
                iconPlate.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(67f, 0f),
                new Vector2(86f, 86f));
            Outline iconOutline = iconPlate.gameObject.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0.90f, 0.97f, 1f, 0.13f);
            iconOutline.effectDistance = new Vector2(1f, -1f);

            _meterSegments = new Image[4];
            for (int index = 0; index < _meterSegments.Length; index++)
            {
                Image segment = UiFactory.CreateRoundedPanel(
                    "Volume Meter Segment " + (index + 1),
                    iconPlate.transform,
                    UiTheme.AccentBright);
                segment.raycastTarget = false;
                float height = 15f + index * 10f;
                UiFactory.SetRect(
                    segment.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0f),
                    new Vector2(-23f + index * 15.5f, -22f),
                    new Vector2(8f, height));
                _meterSegments[index] = segment;
            }

            Text caption = UiFactory.CreateText(
                "Volume Caption",
                surface.transform,
                "媒体音量",
                22,
                UiTheme.TextPrimary,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                caption.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(128f, 28f),
                new Vector2(220f, 34f));

            Text source = UiFactory.CreateText(
                "Volume Source",
                surface.transform,
                "PHONE  ·  MEDIA",
                12,
                UiTheme.AccentBright,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            UiFactory.SetRect(
                source.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(129f, 52f),
                new Vector2(210f, 22f));

            _percentageLabel = UiFactory.CreateText(
                "Volume Percentage",
                surface.transform,
                "0%",
                31,
                UiTheme.TextPrimary,
                TextAnchor.MiddleRight,
                FontStyle.Bold);
            UiFactory.SetRect(
                _percentageLabel.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-29f, 28f),
                new Vector2(132f, 42f));

            Image track = UiFactory.CreateRoundedPanel(
                "Volume Track",
                surface.transform,
                new Color(1f, 1f, 1f, 0.12f));
            track.raycastTarget = false;
            UiFactory.Stretch(track.rectTransform, 128f, 30f, 92f, 30f);

            Image fill = UiFactory.CreateRoundedPanel(
                "Volume Fill",
                track.transform,
                Color.white);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            UiGradient fillGradient = fill.gameObject.AddComponent<UiGradient>();
            fillGradient.StartColor = UiTheme.AccentBright;
            fillGradient.EndColor = UiTheme.AccentSecondary;
            fillGradient.Horizontal = true;
            _levelFill = fill.rectTransform;

            Image shine = UiFactory.CreateGradientPanel(
                "Volume Fill Shine",
                fill.transform,
                new Color(1f, 1f, 1f, 0.30f),
                new Color(1f, 1f, 1f, 0f));
            UiFactory.Stretch(shine.rectTransform, 3f, 3f, 2f, 10f);

            _motion = UiFactory.AddViewMotion(_root, 30f, 0.965f);
            _motion.EnterDuration = 0.22f;
            _motion.ExitDuration = 0.20f;
            _motion.SetInteractionAllowed(false);
            _motion.SetVisibleImmediately(false);
            ApplyLevel(0f);
            UpdateMeterSegments(0f);
        }

        public void Show(int percentage)
        {
            int clamped = CompanionVolume.ClampPercentage(percentage);
            _targetLevel = clamped / 100f;
            _percentageLabel.text = clamped + "%";
            _percentageLabel.color = clamped > 0 ? UiTheme.TextPrimary : UiTheme.TextSecondary;
            UpdateMeterSegments(_targetLevel);

            if (!_motion.IsVisible)
            {
                _displayedLevel = Mathf.Max(0f, _targetLevel - 0.16f);
                _fillVelocity = 0f;
                ApplyLevel(_displayedLevel);
                _motion.Show();
            }

            _root.transform.SetAsLastSibling();
            _hideAt = Time.unscaledTime + VisibleDuration;
        }

        public void Tick()
        {
            if (_root.activeInHierarchy
                && !Mathf.Approximately(_displayedLevel, _targetLevel))
            {
                _displayedLevel = Mathf.SmoothDamp(
                    _displayedLevel,
                    _targetLevel,
                    ref _fillVelocity,
                    FillSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
                if (Mathf.Abs(_displayedLevel - _targetLevel) < 0.001f)
                {
                    _displayedLevel = _targetLevel;
                }
                ApplyLevel(_displayedLevel);
            }

            if (_motion.IsVisible && Time.unscaledTime >= _hideAt)
            {
                _motion.Hide();
            }
        }

        private void ApplyLevel(float level)
        {
            Vector2 anchorMax = _levelFill.anchorMax;
            anchorMax.x = Mathf.Clamp01(level);
            _levelFill.anchorMax = anchorMax;
            _levelFill.offsetMin = Vector2.zero;
            _levelFill.offsetMax = Vector2.zero;
        }

        private void UpdateMeterSegments(float level)
        {
            int activeCount = level <= 0f
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt(level * _meterSegments.Length), 1, _meterSegments.Length);
            for (int index = 0; index < _meterSegments.Length; index++)
            {
                _meterSegments[index].color = index < activeCount
                    ? Color.Lerp(UiTheme.AccentBright, UiTheme.AccentSecondary,
                        index / (float)(_meterSegments.Length - 1))
                    : new Color(0.72f, 0.78f, 0.84f, 0.18f);
            }
        }
    }
}
