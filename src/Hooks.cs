using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    internal static class Hooks
    {
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
                value = hook.InterceptGameColor(value);
        }
    }
}
