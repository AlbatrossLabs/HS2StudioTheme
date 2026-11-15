using System.Collections.Generic;

namespace HS2StudioTheme
{
    /// <summary>
    /// What Studio paints into its pictures, by sprite name, so the theme can draw it itself:
    /// panel backgrounds (some with titles and section labels painted in), and buttons whose
    /// label or icon is part of the picture. Sprite names come from StudioNEOV2_Data/sharedassets2.
    /// </summary>
    internal static class BakedArt
    {
        /// <summary>A caption painted into a panel picture, in that picture's pixels.</summary>
        internal struct Caption
        {
            public string Text;
            public float Y0, Y1, X0, X1;
            public bool Left, Title;
            /// <summary>Hex colour for the text (axis letters), or null for the usual white.</summary>
            public string Tint;

            public Caption(string text, float y0, float y1, float x0, float x1, bool left = false, bool title = false, string tint = null)
            {
                Text = text; Y0 = y0; Y1 = y1; X0 = x0; X1 = x1; Left = left; Title = title; Tint = tint;
            }
        }

        /// <summary>A button picture: the label or icon it shows, and a tooltip for icons.</summary>
        internal struct Button
        {
            public string Label, Icon, Tooltip;
            public Button(string label, string icon = null, string tooltip = null) { Label = label; Icon = icon; Tooltip = tooltip; }
        }

        /// <summary>Panel and section backgrounds: drawn as glass.</summary>
        public static readonly HashSet<string> PanelSprites = new HashSet<string>
        {
            "sp_sn_04_00_00", "sp_sn_05_00_00", "sp_sn_12_00_00", "sp_sn_13_00_00", "sp_sn_14_00_00", "sp_sn_14_00_01",
            "sp_sn_15_00_00", "sp_sn_16_00_00", "sp_sn_23_00_00", "sp_sn_23_00_01", "sp_sn_23_00_02",
            "sp_sn_23_00_03", "sp_sn_23_00_05", "sp_sn_23_00_06", "sp_sn_24_00_00", "sp_sn_24_00_05",
            "sp_sn_25_00_05", "sp_sn_25_00_08", "sp_sn_25_00_10", "sp_sn_26_00_00", "sp_sn_26_00_01",
            "sp_sn_26_00_02", "sp_sn_29_00_01", "sp_sn_29_02_00", "sp_sn_41_00_00", "sp_sn_46_00_00",
            "sp_sn_51_00_00", "sp_sn_55_00_00", "sp_sn_59_00_02", "sp_sn_59_00_03", "sp_sn_60_00_00",
            "sp_sn_62_00_00", "sp_sn_64_00_00", "sp_sn_65_00_00", "sp_sn_66_00_00",
            "sp_sn_39_00_00", "sp_sn_39_00_01",
        };

        public static readonly Dictionary<string, Caption[]> PanelCaptions = new Dictionary<string, Caption[]>
        {
            { "sp_sn_13_00_00", new[] { new Caption("Character", 13, 26, 0, 96), new Caption("Type", 13, 26, 96, 192), new Caption("Line", 114, 126, 0, 192) } },
            { "sp_sn_14_00_00", new[] { new Caption("Playlist", 5, 21, 0, 192, title: true) } },
            { "sp_sn_15_00_00", new[] { new Caption("Voice registration", 5, 21, 0, 128, title: true), new Caption("Name", 29, 42, 4, 40, left: true) } },
            { "sp_sn_23_00_00", new[] { new Caption("FK", 4, 20, 0, 152, title: true) } },
            { "sp_sn_23_00_01", new[] { new Caption("IK", 4, 20, 0, 152, title: true) } },
            { "sp_sn_23_00_02", new[] { new Caption("Gaze", 4, 20, 0, 152, title: true) } },
            { "sp_sn_23_00_03", new[] { new Caption("Neck", 4, 20, 0, 152, title: true) } },
            { "sp_sn_23_00_05", new[] { new Caption("Face", 5, 21, 0, 152, title: true), new Caption("Eyebrows", 25, 39, 0, 152), new Caption("Eyes", 89, 103, 0, 152), new Caption("Mouth", 194, 206, 0, 152) } },
            { "sp_sn_23_00_06", new[] { new Caption("Save pose", 5, 21, 0, 128, title: true), new Caption("Name", 29, 42, 4, 40, left: true) } },
            { "sp_sn_24_00_05", new[] { new Caption("Animation controller", 5, 21, 0, 152, title: true) } },
            { "sp_sn_26_00_00", new[] { new Caption("Directional light (all)", 4, 21, 0, 152, title: true) } },
            { "sp_sn_26_00_01", new[] { new Caption("Point light (all)", 4, 21, 0, 152, title: true) } },
            { "sp_sn_26_00_02", new[] { new Caption("Spot light (all)", 4, 21, 0, 152, title: true) } },
            { "sp_sn_29_02_00", new[] { new Caption("Light controller", 5, 21, 0, 152, title: true) } },
            { "sp_sn_46_00_00", new[] { new Caption("Load outfit", 5, 21, 0, 128, title: true) } },
            { "sp_sn_51_00_00", new[] { new Caption("Folder name", 5, 21, 0, 128, title: true), new Caption("Name", 29, 42, 4, 40, left: true) } },
            { "sp_sn_55_00_00", new[] { new Caption("Import external image", 5, 21, 0, 168, title: true) } },
            { "sp_sn_60_00_00", new[] { new Caption("Hands", 5, 21, 0, 152, title: true) } },
            { "sp_sn_62_00_00", new[] { new Caption("Joint correction", 4, 21, 0, 152, title: true) } },
            { "sp_sn_39_00_00", new[] { new Caption("Girls", 5, 21, 0, 128, title: true) } },
            { "sp_sn_39_00_01", new[] { new Caption("Boys", 5, 21, 0, 128, title: true) } },
            // The gizmo panel (bottom left): Position / Rotation / Scale rows, X Y Z in Apple's red, green, blue.
            { "sp_sn_12_00_00", new[] {
                new Caption("Position", 4, 18, 4, 39, left: true), new Caption("Rotation", 24, 38, 4, 39, left: true), new Caption("Scale", 44, 58, 4, 39, left: true),
                new Caption("X", 4, 18, 36, 56, tint: "FF453A"), new Caption("Y", 4, 18, 116, 136, tint: "30D158"), new Caption("Z", 4, 18, 196, 216, tint: "0A84FF"),
                new Caption("X", 24, 38, 36, 56, tint: "FF453A"), new Caption("Y", 24, 38, 116, 136, tint: "30D158"), new Caption("Z", 24, 38, 196, 216, tint: "0A84FF"),
                new Caption("X", 44, 58, 36, 56, tint: "FF453A"), new Caption("Y", 44, 58, 116, 136, tint: "30D158"), new Caption("Z", 44, 58, 196, 216, tint: "0A84FF") } },
        };

        public static readonly Dictionary<string, Button> Buttons = new Dictionary<string, Button>
        {
            // Clothing state
            { "sp_sn_19_00_00", new Button("On") }, { "sp_sn_19_00_01", new Button("Half") }, { "sp_sn_19_00_02", new Button("Off") },
            // Fluids
            { "sp_sn_20_00_00", new Button("None") }, { "sp_sn_20_00_01", new Button("Some") }, { "sp_sn_20_00_02", new Button("Lots") },
            // Gaze / neck modes
            { "sp_sn_23_00_07", new Button("Front") }, { "sp_sn_23_00_08", new Button("Camera") }, { "sp_sn_23_00_09", new Button("Away") },
            { "sp_sn_23_00_10", new Button("Fixed") }, { "sp_sn_23_00_11", new Button("Control") }, { "sp_sn_23_00_12", new Button("Anim") },
            // Kinematics
            { "sp_sn_23_00_13", new Button("Copy from animation") },
            { "sp_sn_23_00_14", new Button("Apply pose to IK") }, { "sp_sn_23_00_15", new Button("Apply pose to FK") },
            // Lists and files
            { "sp_sn_15_00_01", new Button("Save") }, { "sp_sn_15_00_02", new Button("Load") },
            { "sp_sn_15_00_03", new Button("Import") }, { "sp_sn_15_00_04", new Button("Delete") },
            { "sp_sn_14_00_12", new Button("Clear list") },
            { "sp_sn_40_00_00", new Button("Name") }, { "sp_sn_40_00_01", new Button("Date") },
            { "sp_sn_57_00_00", new Button("From anim") },   // 個別参照: take this part from the animation
            { "sp_sn_32_00_03", new Button("Reset") },
            { "sp_sn_47_00_00", new Button("Change outfit") },
            { "sp_sn_42_00_00", new Button("Load") }, { "sp_sn_42_00_01", new Button("Change character") },
            // Gizmo panel: reset buttons and the move / rotate / scale modes (11_01 = the selected picture)
            { "sp_sn_12_00_02", new Button(null, "reset", "Reset") },
            { "sp_sn_11_00_00", new Button(null, "g_move", "Move  (W)") }, { "sp_sn_11_01_00", new Button(null, "g_move", "Move  (W)") },
            { "sp_sn_11_00_01", new Button(null, "g_rotate", "Rotate  (E)") }, { "sp_sn_11_01_01", new Button(null, "g_rotate", "Rotate  (E)") },
            { "sp_sn_11_00_02", new Button(null, "g_scale", "Scale  (R)") }, { "sp_sn_11_01_02", new Button(null, "g_scale", "Scale  (R)") },
            // Left tool buttons
            { "sp_sn_10_00_06", new Button(null, "l_obj", "Object controls") },
            { "sp_sn_54_00_00", new Button(null, "l_map", "Map controls") },
            { "sp_sn_10_00_00", new Button(null, "l_target", "Show the camera target  (F puts it on the selection)") },
            { "sp_sn_10_00_01", new Button(null, "l_axis", "Show gizmo axes  (Q)") },
            { "sp_sn_10_00_03", new Button(null, "l_cam_free", "Camera: free") },
            { "sp_sn_10_00_04", new Button(null, "l_cam_lock", "Camera: locked (hold Alt to move it)") },
            { "sp_sn_10_00_02", new Button(null, "l_joint", "Joints") },
            { "sp_sn_10_00_07", new Button(null, "l_gimmick", "Gimmicks") },
            { "sp_sn_67_00_00", new Button(null, "l_trans", "Move handle  (J)") },
            { "sp_sn_67_00_01", new Button(null, "l_center", "Center point  (K)") },
            { "sp_sn_11_00_03", new Button(null, "l_undo", "Undo  (Ctrl+Z)") },
            { "sp_sn_11_00_04", new Button(null, "l_redo", "Redo  (Ctrl+Shift+Z)") },
            // Icons
            { "sp_sn_25_00_02", new Button(null, "reset", "Reset to default") },
            { "sp_sn_24_00_01", new Button(null, "restart", "Restart") },
            { "sp_sn_24_00_02", new Button(null, "restart_all", "Restart all") },
            { "sp_sn_24_00_03", new Button(null, "copy", "Copy") },
            { "sp_sn_24_00_04", new Button(null, "paste", "Paste") },
            { "sp_sn_66_00_01", new Button(null, "play", "Start") },
            { "sp_sn_66_00_02", new Button(null, "play_all", "Start all") },
            { "sp_sn_66_00_04", new Button(null, "stop", "Stop all") },
            { "sp_sn_14_00_03", new Button(null, "play", "Play") },
            { "sp_sn_14_00_04", new Button(null, "stop", "Stop") },
            { "sp_sn_14_00_05", new Button(null, "repeat", "Repeat") },
            { "sp_sn_14_00_08", new Button(null, "play_all", "Play all") },
            { "sp_sn_14_00_09", new Button(null, "stop", "Stop all") },
            { "sp_sn_14_00_10", new Button(null, "close", "Remove") },
            { "sp_sn_14_00_11", new Button(null, "save", "Save") },
            { "sp_sn_13_00_03", new Button(null, "import", "Import") },
            { "sp_sn_09_00_01", new Button(null, "close", "Close") },
            { "sp_sn_09_00_03", new Button(null, "chevron", null) },
            { "sp_sn_09_00_04", new Button(null, "chevron_right", null) },
            { "sp_sn_09_00_06", new Button(null, "chevron_left", null) },
            { "sp_sn_09_00_05", new Button(null, "chevron_up", null) },
        };

        public static bool TryButton(UnityEngine.Sprite sprite, out Button button)
        {
            button = default(Button);
            return sprite != null && Buttons.TryGetValue(sprite.name, out button);
        }
    }
}
