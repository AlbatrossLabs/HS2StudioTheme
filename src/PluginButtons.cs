using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Buttons other plugins add with 32-pixel pictures: the left toolbar (KKAPI's toolbar buttons:
    /// QuickAccessBox, Timeline, PoseEditor, VideoExport...) and the workspace toolbar (Material
    /// Editor, Custom Masking, the accessory controller). Known ones get a line icon in the same
    /// style as the rest of the theme; unknown ones are left as they are. Plugins add these at their
    /// own time, so this looks again every second.
    /// </summary>
    internal class PluginButtons : MonoBehaviour
    {
        // Matched against the plugin's GUID, name and button ID (toolbar), or the type that handles
        // the click (workspace), lower case. First match wins, so the generic ones come last.
        private static readonly string[][] Known =
        {
            new[] { "poseeditor", "p_pose", "Pose editor" },
            new[] { "hs2pe", "p_pose", "Pose editor" },
            new[] { "timeline", "p_timeline", "Timeline" },
            new[] { "videoexport", "p_record", "Video export" },
            new[] { "nodesconstraints", "p_constraints", "Node constraints" },
            new[] { "quickaccessbox", "p_search", "Quick access search" },
            new[] { "hooah", "p_components", "Hooah" },
            new[] { "objectmap", "p_map", "Object map" },
            new[] { "extramoveaxis", "p_axis", "Extra gizmos" },
            new[] { "transformorientation", "p_orient", "Local / world orientation" },
            new[] { "materialeditor", "p_material", "Material Editor" },
            new[] { "custommasking", "p_mask", "Custom masking" },
            new[] { "movecontroller", "p_accs", "Accessory controller" },
            new[] { "graphics", "p_graphics", "Graphics" },
        };

        internal static readonly HashSet<string> StudioWorkspaceButtons = new HashSet<string>
        {
            "Button Close", "Button Delete", "Button Folder", "Button Camera", "Button Route",
            "Button Remove", "Button Parent", "Button Copy", "Button Duplicate",
        };

        private Transform _workspace;
        private readonly HashSet<Button> _done = new HashSet<Button>();
        private readonly Undo _undo = new Undo();
        private float _next;
        private bool _removed;
        private System.Type _toolbarManager;
        private bool _toolbarLooked;

        public static PluginButtons Create(Transform workspaceBar)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + "Plugin Buttons");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<PluginButtons>();
            p._workspace = workspaceBar;
            return p;
        }

        private void LateUpdate()
        {
            if (_removed || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { ScanToolbar(); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogDebug("Toolbar scan: " + e.Message); }
            try { ScanWorkspace(); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogDebug("Workspace button scan: " + e.Message); }
        }

        private static string[] Match(string key)
        {
            foreach (var k in Known)
                if (key.Contains(k[0])) return k;
            return null;
        }

        // ------------------------------------------------------------------ left toolbar (KKAPI)

        private void ScanToolbar()
        {
            if (!_toolbarLooked)
            {
                _toolbarLooked = true;
                _toolbarManager = Reflect.Type("KKAPI.Studio.UI.Toolbars.ToolbarManager");
            }
            if (_toolbarManager == null) return;
            var set = AccessTools.Field(_toolbarManager, "_Buttons")?.GetValue(null) as IEnumerable;
            if (set == null) return;

            var controls = new List<object>();
            foreach (var c in set) controls.Add(c);
            foreach (var c in controls)
            {
                var t = Traverse.Create(c);
                var button = t.Property("ButtonObject").GetValue<Button>();
                if (button == null || _done.Contains(button)) continue;
                // KKAPI also lists Studio's own buttons (LeftTools themes those): only its own copies.
                if (!button.name.EndsWith("(StudioAPI)")) { _done.Add(button); continue; }
                var owner = t.Property("Owner").GetValue<BepInEx.BaseUnityPlugin>();
                var id = t.Property("ButtonID").GetValue<string>() ?? "";
                var hover = t.Property("HoverText").GetValue<string>();
                string pluginName = owner != null && owner.Info != null ? owner.Info.Metadata.Name : null;
                string key = id;
                if (owner != null && owner.Info != null)
                    key = owner.Info.Metadata.GUID + " " + owner.Info.Metadata.Name + " " + id;
                _done.Add(button);

                // KKAPI copies Studio's "camera target" button for each of these, after the theme has
                // themed that one, so a copy arrives wearing its icon: start each one over.
                var copied = button.GetComponent<ThemedButton>();
                if (copied != null) copied.Remove();
                var picture = Picture(t.Property("IconTex").GetValue<Texture2D>());
                if (picture == null && button.image != null && copied == null) picture = button.image.sprite;
                if (picture != null && button.image != null) button.image.sprite = picture;   // its own, for turning the theme off

                var known = Match(key.ToLowerInvariant());
                ThemedButton b = known != null
                    ? ThemedButton.Attach(button, ButtonKind.Tool, known[1], null)
                    : picture != null ? ThemedButton.AttachPicture(button, ButtonKind.Tool, picture) : null;
                if (b == null) continue;
                _undo.Add(b.Remove);
                if (known != null && known[1] == "p_record") b.IconTint = Palette.Hex("FF453A");

                // One tooltip, ours: what it does, and which plugin it comes from.
                string what = known != null ? known[2] : !string.IsNullOrEmpty(hover) ? hover : !string.IsNullOrEmpty(id) ? id : pluginName;
                string tip = string.IsNullOrEmpty(pluginName) || what == pluginName ? what : what + "  ·  " + pluginName;
                if (!string.IsNullOrEmpty(tip)) { var tt = tip; b.Tooltip = () => tt; }
                SuppressKkapiTooltip(button.gameObject, _undo);
            }
        }

        // ------------------------------------------------------------------ workspace toolbar

        private void ScanWorkspace()
        {
            if (_workspace == null) return;
            for (int i = 0; i < _workspace.childCount; i++)
            {
                var child = _workspace.GetChild(i);
                if (StudioWorkspaceButtons.Contains(child.name) || child.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                var button = child.GetComponent<Button>();
                if (button == null || _done.Contains(button)) continue;
                var key = ClickHandlers(button);
                if (key.Length == 0) continue;   // listeners not added yet: look again next time
                _done.Add(button);
                var known = Match(key);
                if (known == null) continue;
                // A copy of a themed Studio button carries that button's theme (and its icon): start over.
                var copied = button.GetComponent<ThemedButton>();
                if (copied != null) copied.Remove();
                Theme(button, ButtonKind.Push, known, true);
            }
        }

        private void Theme(Button button, ButtonKind kind, string[] known, bool tooltip)
        {
            var b = ThemedButton.Attach(button, kind, known[1], null);
            if (b == null) return;
            if (tooltip) { var tip = known[2]; b.Tooltip = () => tip; }
            if (known[1] == "p_record") b.IconTint = Palette.Hex("FF453A");
            _undo.Add(b.Remove);
        }

        private readonly Dictionary<Texture2D, Sprite> _pictures = new Dictionary<Texture2D, Sprite>();

        private Sprite Picture(Texture2D tex)
        {
            if (tex == null) return null;
            Sprite s;
            if (!_pictures.TryGetValue(tex, out s) || s == null)
                _pictures[tex] = s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return s;
        }

        /// <summary>
        /// KKAPI draws its own tooltip for toolbar buttons (and Studio's own) on top of the theme's; take
        /// the button out of KKAPI's list so only one shows. Put back when the theme is turned off.
        /// </summary>
        internal static void SuppressKkapiTooltip(GameObject go, Undo undo)
        {
            try
            {
                var type = Reflect.Type("KKAPI.Utilities.GlobalTooltips");
                var dict = type != null ? AccessTools.Field(type, "_tooltips")?.GetValue(null) as IDictionary : null;
                if (dict == null || !dict.Contains(go)) return;
                var tip = dict[go];
                dict.Remove(go);
                undo.Add(() => { try { if (go != null && !dict.Contains(go)) dict[go] = tip; } catch (System.Exception) { } });
            }
            catch (System.Exception) { }
        }

        /// <summary>The text KKAPI shows for this object, if it has one.</summary>
        internal static string KkapiTooltip(GameObject go)
        {
            try
            {
                var type = Reflect.Type("KKAPI.Utilities.GlobalTooltips");
                var dict = type != null ? AccessTools.Field(type, "_tooltips")?.GetValue(null) as IDictionary : null;
                if (dict == null || !dict.Contains(go)) return null;
                return Traverse.Create(dict[go]).Property("Text").GetValue<string>();
            }
            catch (System.Exception) { return null; }
        }

        /// <summary>Types of the code that handles this button's clicks (runtime listeners), lower case.</summary>
        private static string ClickHandlers(Button button)
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                var calls = AccessTools.Field(typeof(UnityEventBase), "m_Calls").GetValue(button.onClick);
                var runtime = AccessTools.Field(calls.GetType(), "m_RuntimeCalls").GetValue(calls) as IList;
                if (runtime != null)
                    foreach (var call in runtime)
                    {
                        if (call == null) continue;
                        var f = AccessTools.Field(call.GetType(), "Delegate");
                        var d = f != null ? f.GetValue(call) as System.Delegate : null;
                        if (d == null) continue;
                        foreach (var one in d.GetInvocationList())
                        {
                            if (one.Method != null && one.Method.DeclaringType != null) sb.Append(one.Method.DeclaringType.FullName).Append(' ');
                            if (one.Target != null) sb.Append(one.Target.GetType().FullName).Append(' ');
                        }
                    }
            }
            catch (System.Exception) { }
            return sb.ToString().ToLowerInvariant();
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            _undo.Run();
            Destroy(gameObject);
        }
    }
}
