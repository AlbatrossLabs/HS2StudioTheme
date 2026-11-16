using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    internal static class Hooks
    {
        /// <summary>
        /// A dropdown's list must be the top thing on screen, or clicks meant for it land on whatever
        /// sits underneath (MoarCamz's settings card under the camera selector).
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(TMPro.TMP_Dropdown), "Show")]
        private static void DropdownShow(TMPro.TMP_Dropdown __instance)
        {
            if (!StudioThemePlugin.Enabled.Value || __instance == null) return;
            try
            {
                var list = __instance.transform.Find("Dropdown List");
                var canvas = list != null ? list.GetComponent<Canvas>() : null;
                if (canvas == null) return;
                var root = __instance.GetComponentInParent<Canvas>();
                if (root != null) root = root.rootCanvas;
                canvas.overrideSorting = true;
                if (root != null) canvas.sortingLayerID = root.sortingLayerID;
                canvas.sortingOrder = 32000;
                var blocker = root != null ? root.transform.Find("Blocker") : null;
                var bc = blocker != null ? blocker.GetComponent<Canvas>() : null;
                if (bc != null) { bc.sortingLayerID = canvas.sortingLayerID; bc.sortingOrder = 31999; }
            }
            catch (System.Exception) { }
        }

        /// <summary>Studio makes the exit dialog when it's asked for (System › End, closing the window): theme it then.</summary>
        [HarmonyPostfix, HarmonyPatch(typeof(ExitDialog), "GameEnd")]
        private static void ExitDialogGameEnd()
        {
            try { ExitDialogTheme.StyleAll(); } catch (System.Exception) { }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ExitDialog), "Initialize")]
        private static void ExitDialogInitialize(ExitDialog __instance)
        {
            try { ExitDialogTheme.Style(__instance); } catch (System.Exception) { }
        }

        /// <summary>
        /// A pose puts the character on its saved animation at the saved moment; left alone, the animation
        /// plays on from there and moves whatever the pose doesn't hold. Pause it there (speed 0).
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(Studio.PauseCtrl), "Load", new[] { typeof(Studio.OCIChar), typeof(string) })]
        private static void PoseLoaded(Studio.OCIChar __0, bool __result)
        {
            try
            {
                if (__result && __0 != null && StudioThemePlugin.FreezeOnPose.Value) __0.animeSpeed = 0f;
            }
            catch (System.Exception) { }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Studio.RootButtonCtrl), "Start")]
        private static void RootButtonCtrlStart(Studio.RootButtonCtrl __instance)
        {
            StudioThemePlugin.Instance.OnRootStarted(__instance);
        }

        /// <summary>
        /// Studio marks the open menu and row by setting the button image to green (and white when it
        /// closes). For themed buttons, that write is noted as the selected state and the colour that
        /// actually lands is the theme's.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch(typeof(Graphic), nameof(Graphic.color), MethodType.Setter)]
        private static void GraphicColorSet(Graphic __instance, ref Color value)
        {
            if (ColorHooks.Registry.Count == 0) return;
            IColorHook hook;
            if (ColorHooks.Registry.TryGetValue(__instance, out hook) && hook != null)
            {
                var multi = hook as IGraphicColorHook;
                value = multi != null ? multi.Intercept(__instance, value) : hook.InterceptGameColor(value);
            }
        }
    }
}
