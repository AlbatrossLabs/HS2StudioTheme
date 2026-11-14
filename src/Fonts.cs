using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// The Windows system font, used as-is: nothing is bundled. The name list is also the fallback
    /// chain for characters the first font lacks, so Japanese text lands in Yu Gothic UI or Meiryo.
    /// </summary>
    internal static class Fonts
    {
        private static Font _regular, _semibold;

        public static Font Regular
        {
            get
            {
                if (_regular == null)
                    _regular = Create(StudioThemePlugin.FontName.Value, "Segoe UI");
                return _regular;
            }
        }

        public static Font Semibold
        {
            get
            {
                if (_semibold == null)
                {
                    var name = StudioThemePlugin.FontName.Value;
                    _semibold = Create(name + " Semibold", "Segoe UI Semibold", name, "Segoe UI");
                }
                return _semibold;
            }
        }

        public static void Clear()
        {
            _regular = null;
            _semibold = null;
        }

        private static Font Create(params string[] preferred)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var n in preferred)
                if (!string.IsNullOrEmpty(n) && !names.Contains(n)) names.Add(n);
            foreach (var n in new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "MS UI Gothic", "Arial" })
                if (!names.Contains(n)) names.Add(n);

            var font = Font.CreateDynamicFontFromOSFont(names.ToArray(), 16);
            font.hideFlags = HideFlags.DontSave;
            return font;
        }
    }
}
