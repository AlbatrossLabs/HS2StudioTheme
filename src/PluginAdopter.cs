using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Themes what other plugins add to Studio's panels after the theme went on: a new page next to
    /// Kinematics' FK / IK pages (FK & IK), extra rows in Joint Adjustment, a plugin's own controls in
    /// a settings panel. Watches the panels <see cref="ControlStyler"/> styled; when Studio's object
    /// count changes, anything new under them gets the same treatment (check boxes, sliders, buttons,
    /// labels, glass). Rows of Studio's lists are left to the lists, which theme their own.
    /// </summary>
    internal class PluginAdopter : MonoBehaviour
    {
        private static PluginAdopter s_instance;

        private readonly List<Transform> _roots = new List<Transform>();
        private readonly HashSet<int> _known = new HashSet<int>();
        private readonly List<Transform> _scratch = new List<Transform>();
        private Undo _undo;
        private int _count = -1;
        private float _next;
        private bool _styling;

        /// <summary>Keep an eye on <paramref name="root"/> (called for every panel the theme styles).</summary>
        public static void Watch(Transform root, Undo undo)
        {
            if (root == null || !StudioThemePlugin.ThemePlugins.Value) return;
            if (s_instance == null)
            {
                var go = new GameObject("HS2StudioTheme Plugin Adopter");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<PluginAdopter>();
                undo.Add(Stop);
            }
            var a = s_instance;
            if (a._styling) return;   // its own pass: already under a watched root
            a._undo = undo;
            foreach (var r in a._roots) if (r != null && root.IsChildOf(r)) return;
            a._roots.Add(root);
            root.GetComponentsInChildren(true, a._scratch);
            foreach (var t in a._scratch) a._known.Add(t.GetInstanceID());
            a._count = -1;
        }

        public static void Stop()
        {
            if (s_instance != null) Destroy(s_instance.gameObject);
            s_instance = null;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            int count = 0;
            var seen = new HashSet<Transform>();
            _roots.RemoveAll(r => r == null);
            foreach (var r in _roots) if (seen.Add(r.root)) count += r.root.hierarchyCount;
            if (count == _count) return;
            _count = count;
            try { Sweep(); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't theme a plugin's new controls: " + e); }
        }

        private void Sweep()
        {
            var fresh = new HashSet<int>();
            var tops = new List<Transform>();
            foreach (var root in _roots)
            {
                root.GetComponentsInChildren(true, _scratch);
                foreach (var t in _scratch)
                {
                    int id = t.GetInstanceID();
                    if (!_known.Add(id)) continue;
                    fresh.Add(id);
                    var p = t.parent;
                    if (p != null && fresh.Contains(p.GetInstanceID())) continue;   // inside something already new
                    if (Skip(t)) continue;
                    tops.Add(t);
                }
            }
            if (tops.Count == 0) return;

            _styling = true;
            try
            {
                foreach (var t in tops)
                {
                    if (t == null) continue;
                    StudioThemePlugin.Log.LogDebug("Theming plugin UI: " + Path(t));
                    ControlStyler.StylePanel(t, _undo);
                    FitLikeSiblings(t);
                }
            }
            finally { _styling = false; }
            // What styling added is the theme's own: not new next time.
            foreach (var root in _roots)
            {
                root.GetComponentsInChildren(true, _scratch);
                foreach (var t in _scratch) _known.Add(t.GetInstanceID());
            }
        }

        /// <summary>Left alone: the theme's own objects, rows of lists (themed by the list), dropdown popups.</summary>
        private static bool Skip(Transform t)
        {
            if (!(t is RectTransform)) return true;
            if (t.name.StartsWith(ThemedButton.OwnPrefix) || t.name == "Dropdown List" || t.name == "Blocker") return true;
            if (t.name.EndsWith("Input Caret")) return true;   // made by a text field the first time it's used
            if (t.name.StartsWith("TMP SubMeshUI") || t.name.StartsWith("TMP UI SubObject")) return true;   // TextMesh Pro fallback-font parts
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.name.StartsWith(ThemedButton.OwnPrefix)) return true;
                var scroll = p.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content != null && t.IsChildOf(scroll.content)) return true;
                if (p.GetComponent<Dropdown>() != null || p.GetComponent<TMPro.TMP_Dropdown>() != null) return true;
            }
            return false;
        }

        /// <summary>A new page beside panels the theme fits to their contents (Kinematics' pages) gets fitted too.</summary>
        private void FitLikeSiblings(Transform t)
        {
            var parent = t.parent;
            if (parent == null) return;
            bool siblingsFit = false;
            foreach (Transform s in parent)
            {
                if (s == t) continue;
                var f = s.GetComponent<PanelFit>();
                if (f != null && f.Live) { siblingsFit = true; break; }
            }
            if (!siblingsFit) return;
            var mine = t.GetComponent<PanelFit>();
            if (mine != null) { if (mine.Live) return; DestroyImmediate(mine); }
            var fit = PanelFit.Attach(t as RectTransform);
            if (fit != null) _undo.Add(fit.Remove);
        }

        private static string Path(Transform t)
        {
            var s = t.name;
            for (var p = t.parent; p != null && s.Length < 160; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
