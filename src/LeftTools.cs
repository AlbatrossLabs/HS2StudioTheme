using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Studio's tool buttons at the bottom left (object and map controls, camera target, gizmo axes,
    /// camera free / locked, gimmicks, move handle, center point, undo, redo): small glass squares
    /// with line icons, in the same places. On/off states (Studio's green) show in the accent colour;
    /// the camera button's icon follows Studio's free / locked picture.
    /// </summary>
    internal static class LeftTools
    {
        public static void Apply(Undo undo)
        {
            var scene = Object.FindObjectOfType<StudioScene>();
            var center = scene != null && scene.cameraInfo != null ? scene.cameraInfo.imageCenter : null;
            var group = center != null ? center.transform.parent : null;
            if (group == null) { StudioThemePlugin.Log.LogWarning("Left tool buttons not found"); return; }

            for (int i = 0; i < group.childCount; i++)
            {
                var t = group.GetChild(i);
                if (t.name.EndsWith("(StudioAPI)")) continue;   // plugins' buttons: PluginButtons
                var button = t.GetComponent<Button>();
                var image = button != null ? button.image : null;
                BakedArt.Button art;
                if (image == null) continue;
                if (!BakedArt.TryButton(image.sprite, out art) || art.Icon == null)
                {
                    // A button the theme has no icon for: keep its picture, give it glass and a tooltip.
                    var p = ThemedButton.AttachPicture(button, ButtonKind.Tool, image.sprite);
                    if (p == null) continue;
                    var tip = PluginButtons.KkapiTooltip(t.gameObject) ?? t.name.Replace("Button ", "");
                    p.Tooltip = () => tip;
                    undo.Add(p.Remove);
                    PluginButtons.SuppressKkapiTooltip(t.gameObject, undo);
                    continue;
                }
                var b = ThemedButton.Attach(button, ButtonKind.Tool, art.Icon, null);
                if (b == null) continue;
                b.MarkBaked();
                undo.Add(b.Remove);
                PluginButtons.SuppressKkapiTooltip(t.gameObject, undo);   // ours replaces KKAPI's
            }

            // Folders, reordering and drag-out for the whole toolbar (plugins' buttons included).
            try { ToolbarFolders.Attach(group, undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't set up toolbar folders: " + e); }
        }
    }
}