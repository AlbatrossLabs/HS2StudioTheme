using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace HS2StudioTheme
{
    /// <summary>
    /// Renaming objects from the right-click menu. Folders, routes and cameras have a name Studio saves
    /// itself; characters, items and lights only show one in the workspace, so their new names are kept
    /// here and saved with the scene (<see cref="ClipSceneData"/>), then put back on load.
    /// </summary>
    internal static class SceneNames
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();

        public static string NameOf(Studio.ObjectCtrlInfo oci)
        {
            var node = oci != null ? oci.treeNodeObject : null;
            return node != null ? node.textName : "";
        }

        public static void Rename(Studio.ObjectCtrlInfo oci, string name)
        {
            if (oci == null || name == null) return;
            name = name.Trim();
            if (name.Length == 0) return;
            var own = AccessTools.Property(oci.GetType(), "name");
            if (own != null && own.CanWrite && own.PropertyType == typeof(string)) own.SetValue(oci, name, null);
            else if (oci.objectInfo != null) Names[oci.objectInfo.dicKey] = name;
            if (oci.treeNodeObject != null) oci.treeNodeObject.textName = name;
        }

        public static void Clear() { Names.Clear(); }

        /// <summary>"key=base64;key=base64".</summary>
        public static string Serialize()
        {
            var live = Studio.Studio.Instance != null ? Studio.Studio.Instance.dicObjectCtrl : null;
            var parts = Names.Where(kv => live == null || live.ContainsKey(kv.Key))
                .Select(kv => kv.Key + "=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value))).ToArray();
            return parts.Length == 0 ? null : string.Join(";", parts);
        }

        public static void Deserialize(string text, Func<int, int?> remap)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                int key;
                if (eq <= 0 || !int.TryParse(part.Substring(0, eq), out key)) continue;
                string name;
                try { name = Encoding.UTF8.GetString(Convert.FromBase64String(part.Substring(eq + 1))); }
                catch { continue; }
                var now = remap(key);
                if (!now.HasValue) continue;
                Names[now.Value] = name;
            }
            Apply();
        }

        /// <summary>Show the kept names in the workspace.</summary>
        public static void Apply()
        {
            var dic = Studio.Studio.Instance != null ? Studio.Studio.Instance.dicObjectCtrl : null;
            if (dic == null) return;
            foreach (var kv in Names)
            {
                Studio.ObjectCtrlInfo oci;
                if (dic.TryGetValue(kv.Key, out oci) && oci != null && oci.treeNodeObject != null && oci.treeNodeObject.textName != kv.Value)
                    oci.treeNodeObject.textName = kv.Value;
            }
        }
    }
}
