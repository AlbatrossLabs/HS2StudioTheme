using System;
using System.Collections.Generic;

namespace HS2StudioTheme
{
    /// <summary>Types of other plugins looked up by name, once (AccessTools.TypeByName scans every loaded type).</summary>
    internal static class Reflect
    {
        private static readonly Dictionary<string, Type> Cache = new Dictionary<string, Type>();

        public static Type Type(string name)
        {
            Type t;
            if (!Cache.TryGetValue(name, out t)) Cache[name] = t = HarmonyLib.AccessTools.TypeByName(name);
            return t;
        }
    }
}
