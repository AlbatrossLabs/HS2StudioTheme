using System;
using System.Collections.Generic;

namespace HS2StudioTheme
{
    /// <summary>
    /// Themed parts that came from another plugin copying a themed object (Instantiate) and took over
    /// in the copy. They aren't in any Undo list, so turning the theme off puts them back from here.
    /// </summary>
    internal static class Clones
    {
        private static readonly List<KeyValuePair<UnityEngine.Object, Action>> List = new List<KeyValuePair<UnityEngine.Object, Action>>();

        private static int s_cleanAt = 256;

        public static void Track(UnityEngine.Object owner, Action remove)
        {
            if (List.Count >= s_cleanAt)
            {
                List.RemoveAll(kv => kv.Key == null);
                s_cleanAt = Math.Max(256, List.Count * 2);
            }
            List.Add(new KeyValuePair<UnityEngine.Object, Action>(owner, remove));
        }

        public static void RemoveAll()
        {
            var all = List.ToArray();
            List.Clear();
            foreach (var kv in all)
            {
                if (kv.Key == null) continue;
                try { kv.Value(); }
                catch (Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't restore a copied control: " + e.Message); }
            }
        }
    }
}
