using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// Studio draws every FK node white out of the box. This preset gives each part its own colour,
    /// using the usual rigging convention (left blue, right red, centre yellow) in Apple's system colours,
    /// so you can tell at a glance which handle moves what. Applied once when Studio's colours are
    /// still the factory white, or any time from F1 (Studio saves them with its own settings).
    /// </summary>
    internal static class FkColours
    {
        public static readonly Color Hair = Palette.Hex("BF5AF2");      // purple
        public static readonly Color Neck = Palette.Hex("FF9F0A");      // orange: head and neck
        public static readonly Color Breast = Palette.Hex("FF6482");    // pink
        public static readonly Color Body = Palette.Hex("FFD60A");      // yellow: spine, hips, legs (centre)
        public static readonly Color RightHand = Palette.Hex("FF453A"); // red: right
        public static readonly Color LeftHand = Palette.Hex("0A84FF");  // blue: left
        public static readonly Color Skirt = Palette.Hex("64D2FF");     // cyan

        /// <summary>Apply if Studio's character FK colours are all still white (never customised).</summary>
        public static void ApplyIfFactory()
        {
            var o = Studio.Studio.optionSystem;
            if (o == null) return;
            if (IsWhite(o.colorFKHair) && IsWhite(o.colorFKNeck) && IsWhite(o.colorFKBreast) && IsWhite(o.colorFKBody)
                && IsWhite(o.colorFKRightHand) && IsWhite(o.colorFKLeftHand) && IsWhite(o.colorFKSkirt))
                Apply();
        }

        public static void Apply()
        {
            var o = Studio.Studio.optionSystem;
            if (o == null) return;
            o.colorFKHair = Hair;
            o.colorFKNeck = Neck;
            o.colorFKBreast = Breast;
            o.colorFKBody = Body;
            o.colorFKRightHand = RightHand;
            o.colorFKLeftHand = LeftHand;
            o.colorFKSkirt = Skirt;
            try { Studio.Studio.Instance.UpdateCharaFKColor(); } catch (System.Exception) { }
            StudioThemePlugin.Log.LogInfo("FK colours set: hair purple, head and neck orange, chest pink, body yellow, right hand red, left hand blue, skirt cyan");
        }

        private static bool IsWhite(Color c) { return c.r > 0.98f && c.g > 0.98f && c.b > 0.98f; }
    }
}
