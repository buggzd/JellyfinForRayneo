using UnityEngine;

namespace JellyfinForRayNeo
{
    internal static class UiTheme
    {
        public static readonly Color Background = new Color(0.006f, 0.009f, 0.017f, 0.995f);
        public static readonly Color Surface = new Color(0.045f, 0.052f, 0.073f, 0.94f);
        public static readonly Color SurfaceRaised = new Color(0.086f, 0.098f, 0.128f, 0.98f);
        public static readonly Color SurfaceGlass = new Color(0.052f, 0.061f, 0.082f, 0.86f);
        public static readonly Color SurfaceSoft = new Color(0.105f, 0.119f, 0.148f, 0.72f);
        public static readonly Color Border = new Color(0.82f, 0.91f, 0.96f, 0.14f);
        public static readonly Color Focus = new Color(0.94f, 0.985f, 1f, 0.98f);
        public static readonly Color TextPrimary = new Color(0.985f, 0.988f, 1f, 1f);
        public static readonly Color TextSecondary = new Color(0.72f, 0.74f, 0.80f, 1f);
        public static readonly Color TextMuted = new Color(0.54f, 0.56f, 0.63f, 1f);
        public static readonly Color Accent = new Color(0.36f, 0.86f, 0.81f, 1f);
        public static readonly Color AccentBright = new Color(0.48f, 0.94f, 0.88f, 1f);
        public static readonly Color AccentSecondary = new Color(0.67f, 0.56f, 1f, 1f);
        public static readonly Color GlowTeal = new Color(0.16f, 0.78f, 0.73f, 0.14f);
        public static readonly Color GlowViolet = new Color(0.48f, 0.31f, 1f, 0.13f);
        public static readonly Color Success = new Color(0.42f, 0.89f, 0.69f, 1f);
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
