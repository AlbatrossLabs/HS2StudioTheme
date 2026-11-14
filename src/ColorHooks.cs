using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>Something that turns Studio's colour for one image into the theme's.</summary>
    internal interface IColorHook
    {
        Color InterceptGameColor(Color value);
    }

    /// <summary>Images whose colour writes are intercepted (see Hooks.GraphicColorSet).</summary>
    internal static class ColorHooks
    {
        public static readonly Dictionary<Graphic, IColorHook> Registry = new Dictionary<Graphic, IColorHook>();

        public static void Remove(Graphic g, IColorHook owner)
        {
            IColorHook registered;
            if (g != null && Registry.TryGetValue(g, out registered) && ReferenceEquals(registered, owner)) Registry.Remove(g);
        }
    }
}
