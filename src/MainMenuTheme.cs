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

                if (Fields[i] == "system" && opens != null) StyleSystemPanels(opens.transform);
                if (Fields[i] == "sound" && opens != null) SoundTheme.Apply(opens.transform, _undo);

                var list = FindList(opens, ListPaths[i]);
                if (list != null) _panels.Add(ThemedPanel.Attach(list));
                else StudioThemePlugin.Log.LogWarning("List not found for " + Fields[i] + " (" + ListPaths[i] + ")");
            }

            try { TopBar.Apply(_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the top bar: " + e); }

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
