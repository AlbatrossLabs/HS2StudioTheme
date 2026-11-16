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

    /// <summary>A hook that looks after several images and needs to know which one is being written.</summary>
    internal interface IGraphicColorHook : IColorHook
    {
        Color Intercept(Graphic g, Color value);
    }

    /// <summary>Images whose colour writes are intercepted (see Hooks.GraphicColorSet).</summary>
    internal static class ColorHooks
    {
        // Looked up on every colour write in the game's UI: compare by reference (Unity's own == asks the engine).
        public static readonly Dictionary<Graphic, IColorHook> Registry = new Dictionary<Graphic, IColorHook>(new RefComparer());

        private sealed class RefComparer : IEqualityComparer<Graphic>
        {
            public bool Equals(Graphic a, Graphic b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Graphic g) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(g); }
        }

        public static void Remove(Graphic g, IColorHook owner)
        {
            IColorHook registered;
            if (g != null && Registry.TryGetValue(g, out registered) && ReferenceEquals(registered, owner)) Registry.Remove(g);
        }
    }
}
