using UnityEngine;

namespace JellyfinForRayNeo
{
    internal static class UiTheme
    {
        // LUCENT: content is the backdrop and controls should read as reflected light.
        public static readonly Color Background = new Color(0.008f, 0.027f, 0.051f, 0.998f);
        public static readonly Color Surface = new Color(0.027f, 0.071f, 0.106f, 0.92f);
        public static readonly Color SurfaceRaised = new Color(0.071f, 0.129f, 0.169f, 0.90f);
        public static readonly Color SurfaceGlass = new Color(0.055f, 0.125f, 0.169f, 0.66f);
        public static readonly Color SurfaceSoft = new Color(0.126f, 0.216f, 0.263f, 0.48f);
        public static readonly Color Border = new Color(0.78f, 0.94f, 0.985f, 0.16f);
        public static readonly Color Focus = new Color(0.863f, 0.976f, 1f, 0.98f);
        public static readonly Color TextPrimary = new Color(0.961f, 0.992f, 1f, 1f);
        public static readonly Color TextSecondary = new Color(0.82f, 0.91f, 0.945f, 0.64f);
        public static readonly Color TextMuted = new Color(0.72f, 0.84f, 0.89f, 0.38f);
        public static readonly Color Accent = new Color(0.396f, 0.871f, 1f, 1f);
        public static readonly Color AccentBright = new Color(0.651f, 0.937f, 1f, 1f);
        public static readonly Color AccentSecondary = new Color(0.545f, 0.718f, 1f, 1f);
        public static readonly Color GlowTeal = new Color(0.25f, 0.82f, 1f, 0.17f);
        public static readonly Color GlowViolet = new Color(0.43f, 0.34f, 1f, 0.11f);
        public static readonly Color Success = new Color(0.57f, 0.95f, 0.76f, 1f);
        public static readonly Color Danger = new Color(0.95f, 0.28f, 0.38f, 1f);
        public static readonly Color ProgressTrack = new Color(0.84f, 0.95f, 0.98f, 0.16f);

        public const float SideRailWidth = 92f;
        public const float SideRailExpandedWidth = 304f;
        public const float ContentLeft = 158f;
        public const float ContentRight = 76f;

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
