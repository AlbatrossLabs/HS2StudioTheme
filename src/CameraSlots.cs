using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// Clearing and removing camera slots, for the slot menu. Only with MoarCamz: it keeps each slot's view
    /// (its MoarCamzData list is indexed by slot) and knows what an empty slot is; Studio's own ten slots
    /// always hold a view and can't be removed. Reached by reflection, so the theme doesn't need MoarCamz.
    /// </summary>
    internal static class CameraSlots
    {
        private static object Plugin
        {
            get
            {
                try { return AccessTools.TypeByName("MoarCamz.MoarCamzPlugin") != null ? Traverse.CreateWithType("MoarCamz.MoarCamzPlugin").Property("Instance").GetValue() : null; }
                catch { return null; }
            }
        }

        public static bool Available { get { return List != null; } }

        private static IList List
        {
            get { var p = Plugin; return p != null ? Traverse.Create(p).Property("MoarCamz").GetValue() as IList : null; }
        }

        /// <summary>Make slot <paramref name="index"/> (0-based) empty: MoarCamz's "new camera" view, no center target.</summary>
        public static bool Clear(int index)
        {
            var p = Plugin;
            var list = List;
            if (p == null || list == null || index < 0 || index >= list.Count) return false;
            var empty = Traverse.Create(p).Field("newCam").GetValue();   // Studio.CameraControl.CameraData
            if (empty == null) return false;
            var data = list[index];
            Traverse.Create(data).Method("Copy", new[] { empty.GetType() }).GetValue(empty);
            Traverse.Create(data).Method("ClearExtended").GetValue();
            SyncStudio(index, data);
            return true;
        }

        /// <summary>
        /// Take slot <paramref name="index"/> out: every later slot's view moves down one, and the last slot goes
        /// (an extra MoarCamz slot is deleted; one of Studio's ten is emptied).
        /// </summary>
        public static bool Remove(int index)
        {
            var p = Plugin;
            var list = List;
            if (p == null || list == null || index < 0 || index >= list.Count) return false;
            var dataType = list[index].GetType();
            for (int j = index; j < list.Count - 1; j++)
            {
                Traverse.Create(list[j]).Method("Copy", new[] { dataType }).GetValue(list[j + 1]);
                SyncStudio(j, list[j]);
            }
            int last = list.Count - 1;
            if (list.Count > 10) Traverse.Create(p).Method("DelCameraSlot", new[] { dataType }).GetValue(list[last]);
            else Clear(last);
            return true;
        }

        /// <summary>Studio keeps its first ten slots' views in the scene too: keep those in step.</summary>
        private static void SyncStudio(int index, object data)
        {
            if (index >= 10) return;
            try
            {
                // (Through reflection: naming Studio's CameraData type pulls in Cinemachine.)
                var views = Traverse.Create(Studio.Studio.Instance.sceneInfo).Field("cameraData").GetValue() as IList;
                if (views == null || index >= views.Count || views[index] == null) return;
                var t = Traverse.Create(data);
                var v = Traverse.Create(views[index]);
                v.Field("pos").SetValue(t.Property("Position").GetValue<Vector3>());
                v.Field("rotate").SetValue(t.Property("Rotation").GetValue<Vector3>());
                v.Field("distance").SetValue(t.Property("Distance").GetValue<Vector3>());
                v.Field("parse").SetValue(t.Property("FOV").GetValue<float>());
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogDebug("Camera slot sync: " + e.Message); }
        }
    }
}
