using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>Diagnostics: the first few times a column opens, writes what's in it to the log.</summary>
    internal static class ColumnDiag
    {
        private static int _dumps;

        public static void Dump(Transform container, string[] panels)
        {
            if (_dumps >= 4 || container == null) return;
            _dumps++;
            var sb = new StringBuilder("Column view check (" + container.name + "):\n");
            foreach (var name in panels)
            {
                var t = container.Find(name) as RectTransform;
                if (t == null) { sb.Append("  " + name + ": not found\n"); continue; }
                sb.Append("  " + name + ": active " + t.gameObject.activeInHierarchy + ", " + Rect(t) + Alpha(t) + "\n");
                var group = t.GetComponent<Studio.AnimeGroupList>();
                if (group != null)
                {
                    var tr = HarmonyLib.Traverse.Create(group);
                    var root = tr.Field("transformRoot").GetValue<Transform>();
                    var prefab = tr.Field("objectPrefab").GetValue<GameObject>();
                    sb.Append("    group list: init " + tr.Field("isInit").GetValue<bool>() + ", sex " + group.sex
                        + ", root " + (root != null ? root.name + " (" + root.childCount + " children)" : "none")
                        + ", prefab " + (prefab != null ? prefab.name + (prefab.scene.IsValid() ? " (in scene)" : " (asset)") : "none") + "\n");
                }
                var scroll = t.GetComponent<ScrollRect>();
                var content = scroll != null ? scroll.content : null;
                if (scroll != null && scroll.viewport != null) sb.Append("    viewport " + Rect(scroll.viewport) + Masks(scroll.viewport) + "\n");
                if (content == null) continue;
                sb.Append("    content " + Rect(content) + ", " + content.childCount + " children\n");
                for (int i = 0; i < content.childCount && i < 3; i++)
                {
                    var c = content.GetChild(i) as RectTransform;
                    if (c == null) continue;
                    sb.Append("      [" + i + "] " + c.name + ": active " + c.gameObject.activeSelf + ", scale " + c.localScale + ", " + Rect(c) + Alpha(c));
                    var text = c.GetComponentInChildren<Text>(true);
                    if (text != null) sb.Append(", text '" + text.text + "' a=" + text.color.a + " font " + (text.font != null ? text.font.name : "none") + " size " + text.fontSize);
                    var tmp = c.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (tmp != null) sb.Append(", tmp '" + tmp.text + "' a=" + tmp.color.a);
                    var img = c.GetComponent<Image>();
                    if (img != null) sb.Append(", image a=" + img.color.a + " " + (img.sprite != null ? img.sprite.name : "no sprite"));
                    sb.Append(", themed " + (c.GetComponentInChildren<ThemedButton>(true) != null));
                    sb.Append("\n");
                }
            }
            StudioThemePlugin.Log.LogInfo(sb.ToString());
        }

        private static string Rect(RectTransform t)
        {
            var r = t.rect;
            return "pos " + t.anchoredPosition + " size " + r.width.ToString("0") + "x" + r.height.ToString("0") + " scale " + t.lossyScale.x.ToString("0.###");
        }

        private static string Alpha(Transform t)
        {
            float a = 1f;
            foreach (var g in t.GetComponentsInParent<CanvasGroup>(true)) a *= g.alpha;
            return a < 0.99f ? ", canvas group alpha " + a : "";
        }

        private static string Masks(Transform t)
        {
            var s = "";
            if (t.GetComponent<RectMask2D>() != null) s += " RectMask2D";
            var m = t.GetComponent<Mask>();
            if (m != null) s += " Mask(showGraphic " + m.showMaskGraphic + ")";
            return s;
        }
    }
}
