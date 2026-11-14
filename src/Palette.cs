using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>Colours and sizes. Dark-mode values from Apple's macOS palette.</summary>
    internal static class Palette
    {
        public static readonly Color AppleBlue = Hex("0A84FF");
        public static readonly Color GlassTint = Hex("1C1C1E");   // window background, dark mode

        public static Color Accent { get { return StudioThemePlugin.Accent.Value; } }

        public static Color White(float alpha) { return new Color(1f, 1f, 1f, alpha); }

        public const float BorderAlpha = 0.14f;   // hairline round the glass
        public const float HoverAlpha = 0.09f;    // white wash on hover
        public const float PressAlpha = 0.16f;    // white wash while pressed
        public const float PushAlpha = 0.10f;     // resting fill of a push button
        public const float FieldAlpha = 0.07f;    // text fields, dropdowns
        public const float HeaderAlpha = 0.07f;   // section header band
        public const float TrackAlpha = 0.20f;    // slider track
        public const float TextAlpha = 0.88f;     // row text
        public const float SubtleTextAlpha = 0.70f; // menu labels, icons not selected
        public const float DisabledAlpha = 0.35f;

        // Corner radii in UI units (Studio's canvas is 1920×1080 reference).
        public const float PanelRadius = 12f;
        public const float BackdropRadius = 14f;
        public const float RowRadius = 5f;
        public const float TileRadius = 10f;

        public static Color Hex(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString("#" + hex, out c);
            return c;
        }

        /// <summary>Studio's own "selected" colour is pure green.</summary>
        public static bool IsGameSelected(Color c)
        {
            return c.g > 0.9f && c.r < 0.2f && c.b < 0.2f;
        }

        /// <summary>White or black text, whichever reads better on the accent.</summary>
        public static Color OnAccent()
        {
            var a = Accent;
            var luminance = 0.2126f * a.r + 0.7152f * a.g + 0.0722f * a.b;
            return luminance > 0.6f ? new Color(0.07f, 0.07f, 0.08f, 1f) : Color.white;
        }
    }
}
