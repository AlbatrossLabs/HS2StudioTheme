using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>Shared text helpers: legacy Text set-up, TextMeshPro alignment, tag stripping.</summary>
    internal static class Labels
    {
        private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);

        /// <summary>TextMeshPro rich-text tags (size=80%, mark, sprite...) mean nothing to legacy Text.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.IndexOf('<') >= 0 ? Tags.Replace(text, "") : text;
        }

        public static Text Configure(Text t, Font font, TextAnchor align, int maxSize, int minSize)
        {
            t.font = font;
            t.fontSize = maxSize;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = maxSize;
            t.resizeTextMinSize = minSize;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = false;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>The rect that places a label: its scrolling frame if it has one (see <see cref="Marquee"/>), else itself.</summary>
        public static RectTransform Frame(Text t)
        {
            if (t == null) return null;
            var parent = t.transform.parent;
            if (parent != null && parent.GetComponent<Marquee>() != null) return (RectTransform)parent;
            return (RectTransform)t.transform;
        }

        /// <summary>What a label says in full, even while it's showing a shortened "…" version.</summary>
        public static string FullText(Text t)
        {
            if (t == null) return null;
            var parent = t.transform.parent;
            var m = parent != null ? parent.GetComponent<Marquee>() : null;
            return m != null ? m.FullText : t.text;
        }

        /// <summary>Apple-ish body size for a line of this height: 13 for Studio's 20-unit rows.</summary>
        public static int SizeFor(float height)
        {
            return Mathf.Clamp(Mathf.RoundToInt(height * 0.65f), 10, 14);
        }

        /// <summary>TextMeshPro's alignment flags to the nearest legacy anchor.</summary>
        public static TextAnchor AnchorOf(TMPro.TMP_Text tmp)
        {
            int v = (int)tmp.alignment;
            int col = (v & 1) != 0 ? 0 : (v & 4) != 0 ? 2 : (v & (2 | 32)) != 0 ? 1 : 0;
            int row = (v & 256) != 0 ? 0 : (v & 1024) != 0 ? 2 : 1;
            return (TextAnchor)(row * 3 + col);
        }
    }
}
