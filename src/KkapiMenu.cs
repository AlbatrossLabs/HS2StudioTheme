using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// KKAPI has its own workspace right-click menu (an IMGUI window other plugins add items to). With the
    /// theme's menu on, the two would open together: this hides KKAPI's and puts its items (Select children,
    /// and whatever other plugins registered) at the bottom of the theme's menu, so nothing is lost.
    /// Reflection only, so the theme still runs without KKAPI.
    /// </summary>
    internal static class KkapiMenu
    {
        internal struct Entry
        {
            public string Label;
            public Action Act;
            public bool Disabled, Separator;
        }

        private static FieldInfo _sorted, _onGui, _title, _contents;
        private static MethodInfo _hide, _show, _isSep;
        private static Type _entryType;

        /// <summary>When KKAPI last opened the theme's menu (to drop the theme's own duplicate open).</summary>
        internal static float OpenedAt = -10f;
        internal static object OpenedNode;

        /// <summary>True once the hook is in: workspace right-clicks come through KKAPI.</summary>
        internal static bool Hooked { get; private set; }

        public static void Patch(Harmony harmony)
        {
            var menus = Reflect.Type("KKAPI.Studio.StudioContextMenus");
            var global = Reflect.Type("KKAPI.Utilities.GlobalContextMenu");
            if (menus == null || global == null) return;
            var show = AccessTools.Method(menus, "OnShowWorkspaceContextMenu");
            _sorted = AccessTools.Field(menus, "_workspaceMenuItemsSorted");
            _entryType = AccessTools.Inner(global, "Entry");
            _onGui = _entryType != null ? AccessTools.Field(_entryType, "_onGuiLayout") : null;
            _isSep = _entryType != null ? AccessTools.Method(_entryType, "IsSeparator") : null;
            _hide = AccessTools.Method(global, "Hide");
            _show = AccessTools.Method(global, "Show", new[] { typeof(Vector2), typeof(string), _entryType != null ? _entryType.MakeArrayType() : typeof(object) });
            _title = AccessTools.Field(global, "_title");
            _contents = AccessTools.Field(global, "_contents");
            if (show == null || _sorted == null || _onGui == null || _hide == null) return;
            harmony.Patch(show, postfix: new HarmonyMethod(typeof(KkapiMenu), nameof(Shown)));
            Hooked = true;
            StudioThemePlugin.Log.LogDebug("KKAPI's workspace menu is merged into the theme's.");
        }

        private static void Shown(Studio.TreeNodeObject __0)
        {
            try
            {
                if (!StudioThemePlugin.Enabled.Value || !StudioThemePlugin.CharacterMenu.Value || CharacterMenu.Instance == null) return;
                Studio.ObjectCtrlInfo info = null;
                if (__0 == null || !Studio.Studio.Instance.dicInfo.TryGetValue(__0, out info) || info == null) return;

                // What KKAPI was about to show, before hiding it (kept for "More…" when an item draws itself).
                var title = _title != null ? _title.GetValue(null) as string : null;
                var contents = _contents != null ? _contents.GetValue(null) as Array : null;
                var entries = Translate(_sorted.GetValue(null) as Array, out bool custom);
                _hide.Invoke(null, null);

                if (custom && contents != null && _show != null)
                {
                    var pos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                    entries.Add(new Entry { Separator = true });
                    entries.Add(new Entry { Label = "More…", Act = () => { try { _show.Invoke(null, new object[] { pos, title, contents }); } catch (Exception) { } } });
                }

                OpenedAt = Time.unscaledTime;
                OpenedNode = __0;
                CharacterMenu.Instance.OpenFor(info, Input.mousePosition, entries);
            }
            catch (Exception e)
            {
                StudioThemePlugin.Log.LogDebug("KKAPI menu: " + e.Message);
            }
        }

        private static List<Entry> Translate(Array sorted, out bool custom)
        {
            custom = false;
            var list = new List<Entry>();
            if (sorted == null) return list;
            foreach (var entry in sorted)
            {
                if (_isSep != null && (bool)_isSep.Invoke(entry, null))
                {
                    if (list.Count > 0 && !list[list.Count - 1].Separator) list.Add(new Entry { Separator = true });
                    continue;
                }
                var draw = _onGui.GetValue(entry) as Delegate;
                var target = draw != null ? draw.Target : null;
                var t = target != null ? target.GetType() : null;
                var nameF = t != null ? AccessTools.Field(t, "name") : null;
                var clickF = t != null ? AccessTools.Field(t, "onClick") : null;
                if (nameF == null || clickF == null) { custom = true; continue; }   // draws itself
                var content = nameF.GetValue(target) as GUIContent;
                var click = clickF.GetValue(target) as Action;
                var stateF = AccessTools.Field(t, "onCheckState");
                var state = stateF != null ? stateF.GetValue(target) as Delegate : null;
                int s = 0;
                try { if (state != null) s = Convert.ToInt32(state.DynamicInvoke()); } catch (Exception) { }
                if (s == 2) continue;                                               // Hidden
                list.Add(new Entry { Label = content != null ? content.text : "?", Act = click, Disabled = s == 1 });
            }
            while (list.Count > 0 && list[list.Count - 1].Separator) list.RemoveAt(list.Count - 1);
            return list;
        }
    }
}
