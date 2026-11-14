using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// Restyles Studio's main menu (add / anim / sound / system) and the list each one opens, in an
    /// Apple-style look: tinted glass panels, rounded rows, line icons and the system font.
    /// Nothing is moved or replaced: the game's own buttons, panels and click handlers are restyled in
    /// place, and turning the theme off puts every original sprite, colour and transition back.
    /// </summary>
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInProcess("StudioNEOV2")]
    public class StudioThemePlugin : BaseUnityPlugin
    {
        public const string GUID = "com.albatrosslabs.hs2.studiotheme";
        public const string PluginName = "Studio Theme";
        public const string Version = "0.8.1";

        internal static StudioThemePlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<Color> Accent;
        internal static ConfigEntry<float> GlassOpacity;
        internal static ConfigEntry<bool> MenuLabels;
        internal static ConfigEntry<string> FontName;
        internal static ConfigEntry<Vector2> WorkspaceSize;

        private Studio.RootButtonCtrl _root;
        private readonly MainMenuTheme _theme = new MainMenuTheme();

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Use the theme. Off puts Studio's original look back straight away.");
            Accent = Config.Bind("Look", "Accent colour", Palette.AppleBlue,
                "Colour of the selected button and row. Apple blue is 0A84FF; Albatross mint is 6EE7B7.");
            GlassOpacity = Config.Bind("Look", "Glass opacity", 0.86f,
                new ConfigDescription("How solid the panels are. Lower lets more of the scene show through; higher reads better over bright scenes.",
                    new AcceptableValueRange<float>(0.5f, 1f)));
            MenuLabels = Config.Bind("Look", "Labels under menu icons", true,
                "Show Add / Anim / Sound / System under the main menu icons.");
            FontName = Config.Bind("Look", "Font", "Segoe UI",
                "A font installed in Windows. Nothing is bundled: Japanese text falls back to Yu Gothic UI or Meiryo.");

            WorkspaceSize = Config.Bind("Layout", "Workspace size", Vector2.zero,
                "Size of the workspace list, set by dragging its corner grip. 0, 0 = Studio's size.");

            Enabled.SettingChanged += (s, e) => Reapply();
            GlassOpacity.SettingChanged += (s, e) => { Sprites.Clear(); Reapply(); };
            MenuLabels.SettingChanged += (s, e) => Reapply();
            FontName.SettingChanged += (s, e) => { Fonts.Clear(); Reapply(); };
            Accent.SettingChanged += (s, e) => { Sprites.Clear(); Reapply(); };   // check boxes bake the accent in

            Harmony.CreateAndPatchAll(typeof(Hooks), GUID);
        }

        /// <summary>Called once Studio's main menu has started.</summary>
        internal void OnRootStarted(Studio.RootButtonCtrl root)
        {
            _root = root;
            StartCoroutine(ApplyLater());
        }

        private IEnumerator ApplyLater()
        {
            // Let the game's own layout pass (AutoLayoutCtrl runs at the end of the first frame) and
            // other plugins' extra rows settle before restyling.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;
            Reapply();
        }

        private void Reapply()
        {
            _theme.Remove();
            if (Enabled.Value && _root != null)
            {
                try { _theme.Apply(_root); }
                catch (System.Exception e)
                {
                    Log.LogError("Couldn't apply the theme, putting the original look back: " + e);
                    _theme.Remove();
                }
            }
        }

        private void OnDestroy()
        {
            _theme.Remove();
        }
    }
}
