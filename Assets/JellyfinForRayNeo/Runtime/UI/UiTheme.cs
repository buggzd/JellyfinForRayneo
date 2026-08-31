using UnityEngine;

namespace JellyfinForRayNeo
{
    internal static class UiTheme
    {
        public static readonly Color Background = new Color(0.018f, 0.022f, 0.037f, 0.985f);
        public static readonly Color Surface = new Color(0.075f, 0.082f, 0.115f, 0.96f);
        public static readonly Color SurfaceRaised = new Color(0.12f, 0.13f, 0.18f, 0.98f);
        public static readonly Color TextPrimary = new Color(0.97f, 0.98f, 1f, 1f);
        public static readonly Color TextSecondary = new Color(0.67f, 0.70f, 0.78f, 1f);
        public static readonly Color Accent = new Color(0.35f, 0.21f, 0.95f, 1f);
        public static readonly Color AccentBright = new Color(0.52f, 0.40f, 1f, 1f);
        public static readonly Color Danger = new Color(0.95f, 0.28f, 0.38f, 1f);
        public static readonly Color ProgressTrack = new Color(1f, 1f, 1f, 0.18f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }
    }
}
