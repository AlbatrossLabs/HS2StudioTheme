using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The main menu column (add / anim / sound / system) and the first list each one opens.
    /// </summary>
    internal class MainMenuTheme
    {
        private static readonly string[] Fields = { "add", "manipulate", "sound", "system" };
        private static readonly string[] IconKeys = { "add", "anim", "sound", "system" };
        private static readonly string[] Labels = { "Add", "Anim", "Sound", "System" };

        // Where each menu's list sits under the object it opens. System's list is that object itself.
        private static readonly string[] ListPaths = { "Scroll View Add Group", "00_Chara/00_Root", "Scroll View", "" };

        private readonly List<ThemedButton> _tiles = new List<ThemedButton>();
        private readonly List<ThemedPanel> _panels = new List<ThemedPanel>();
        private GlassRect _backdrop;
        private readonly Undo _undo = new Undo();

        public void Apply(Studio.RootButtonCtrl root)
        {
            var rootT = Traverse.Create(root);
            var columnButtons = new List<RectTransform>();

            for (int i = 0; i < Fields.Length; i++)
            {
                var info = rootT.Field(Fields[i]).GetValue();
                if (info == null)
                {
                    StudioThemePlugin.Log.LogWarning("Main menu entry not found: " + Fields[i]);
                    continue;
                }
                var infoT = Traverse.Create(info);
                var button = infoT.Field("button").GetValue<Button>();
                var opens = infoT.Field("objRoot").GetValue<GameObject>();

                var tile = ThemedButton.Attach(button, ButtonKind.Tile, IconKeys[i],
                    StudioThemePlugin.MenuLabels.Value ? Labels[i] : null);
                if (tile != null)
                {
                    _tiles.Add(tile);
                    columnButtons.Add((RectTransform)button.transform);
                }

                var list = FindList(opens, ListPaths[i]);
                var listPanel = list != null ? ThemedPanel.Attach(list) : null;
                if (listPanel != null) _panels.Add(listPanel);
                else if (list == null) StudioThemePlugin.Log.LogWarning("List not found for " + Fields[i] + " (" + ListPaths[i] + ")");

                // What each list opens.
                if (Fields[i] == "system" && opens != null) StyleSystemPanels(opens.transform);
                if (Fields[i] == "sound" && opens != null) SoundTheme.Apply(opens.transform, _undo);
                if (Fields[i] == "manipulate" && opens != null) StyleAnimPanels(opens.transform);
                if (Fields[i] == "add" && opens != null) StyleAddPanels(opens.transform, listPanel);
            }

            try { TopBar.Apply(_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the top bar: " + e); }

            try { LeftTools.Apply(_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the tool buttons: " + e); }

            try { CharacterMenu.Create(_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't set up the character menu: " + e); }

            try { GuideTheme.Apply(_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the gizmo panel: " + e); }

            try { WorkspaceTheme.Apply(_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the workspace: " + e); }

            // Glass behind the whole column, drawn by the column itself so no sibling is added.
            if (columnButtons.Count > 0)
            {
                var column = columnButtons[0].parent;
                if (column != null && column.GetComponent<Graphic>() == null)
                {
                    _backdrop = column.gameObject.AddComponent<GlassRect>();
                    _backdrop.sprite = Sprites.Backdrop;
                    _backdrop.around = columnButtons.ToArray();
                    _backdrop.padding = 6f;
                    _backdrop.color = Color.white;
                }
            }

            // The lists end where the column's glass ends.
            if (_backdrop != null)
                foreach (var p in _panels) if (p != null) p.FitBottomTo(_backdrop);
        }

        /// <summary>
        /// Everything Add opens: the girl and boy character lists (with card preview and Load /
        /// Change character), the item group → category → item columns, and the map, light,
        /// background and frame lists.
        /// </summary>
        private void StyleAddPanels(Transform add, ThemedPanel addList)
        {
            for (int i = 0; i < add.childCount; i++)
            {
                var t = add.GetChild(i);
                if (t.name == "Scroll View Add Group") continue;
                try { ControlStyler.StylePanel(t, _undo); }
                catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style " + t.name + ": " + e); }
            }

            try
            {
                var columns = ColumnView.Attach(add.Find("02_Item"), addList,
                    new[] { "Scroll View Group", "Scroll View Category", "Scroll View Item" }, new[] { "Group", "Category", "Item" });
                if (columns != null) _undo.Add(columns.Remove);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't set up the item columns: " + e); }
        }

        /// <summary>
        /// Everything Anim opens: the character panels (State, Kinematics, Animations, Voice, Outfit,
        /// Joint), the item / light / folder / route / camera panels, and the floating windows that go
        /// with them (animation controller, outfit panel, route controller).
        /// </summary>
        private void StyleAnimPanels(Transform manipulate)
        {
            var targets = new List<Transform>();
            for (int i = 0; i < manipulate.childCount; i++) targets.Add(manipulate.GetChild(i));
            var menu = manipulate.parent;
            if (menu != null)
                foreach (var name in new[] { "03_00_Anime Control", "02_01_Panel", "02_05_00_Route Control" })
                {
                    var t = menu.Find(name);
                    if (t != null) targets.Add(t);
                }
            foreach (var t in targets)
            {
                try { ControlStyler.StylePanel(t, _undo); }
                catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style " + t.name + ": " + e); }
            }

            // Kinematics' panels and Joint Adjustment: give the last row room at the bottom.
            var chara = manipulate.Find("00_Chara");
            if (chara != null)
            {
                foreach (var path in new[] { "02_Kinematic/00_FK", "02_Kinematic/01_IK", "02_Kinematic/02_LookAt",
                    "02_Kinematic/03_Neck", "02_Kinematic/05_Etc", "02_Kinematic/06_Hand", "02_Kinematic/07_Pause", "06_Joint" })
                {
                    var fit = PanelFit.Attach(chara.Find(path) as RectTransform);
                    if (fit != null) _undo.Add(fit.Remove);
                }
            }

            // The animation picker as a column view.
            try
            {
                var root = manipulate.Find("00_Chara/00_Root");
                var columns = ColumnView.Attach(manipulate.Find("03_Anime"), root != null ? root.GetComponent<ThemedPanel>() : null,
                    new[] { "Group Panel", "Category Panel", "Anime Panel" }, new[] { "Group", "Category", "Animation" });
                if (columns != null) _undo.Add(columns.Remove);
                KinematicsNotice.Attach(columns, _undo);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't set up the animation columns: " + e); }
        }

        /// <summary>
        /// The panels System's list opens (Config, Screen Effect, Option, Light, Logo...): every child
        /// of the System object that is a panel of its own.
        /// </summary>
        private void StyleSystemPanels(Transform system)
        {
            for (int i = 0; i < system.childCount; i++)
            {
                var panel = system.GetChild(i);
                if (panel.GetComponent<Canvas>() == null && panel.GetComponent<CanvasGroup>() == null) continue;
                try { ControlStyler.StylePanel(panel, _undo); }
                catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style " + panel.name + ": " + e); }
            }
        }

        private static ScrollRect FindList(GameObject opens, string path)
        {
            if (opens == null) return null;
            var t = string.IsNullOrEmpty(path) ? opens.transform : opens.transform.Find(path);
            return t != null ? t.GetComponent<ScrollRect>() : null;
        }

        /// <summary>Recolour after the accent changes.</summary>
        public void Refresh()
        {
            foreach (var t in _tiles) if (t != null) t.Refresh();
            foreach (var p in _panels) if (p != null) p.Refresh();
        }

        public void Remove()
        {
            TooltipView.Destroy();
            _undo.Run();
            foreach (var t in _tiles) if (t != null) t.Remove();
            foreach (var p in _panels) if (p != null) p.Remove();
            _tiles.Clear();
            _panels.Clear();
            // Immediately, so re-applying in the same frame can add a fresh one.
            if (_backdrop != null) Object.DestroyImmediate(_backdrop);
            _backdrop = null;
        }
    }
}
