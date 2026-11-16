using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;

namespace HS2StudioTheme
{
    /// <summary>
    /// Saves the theme's per-scene data in the scene file, through KKAPI: the cameras' culling distances
    /// (<see cref="CameraClip"/>) and the names given to objects from the right-click menu (<see cref="SceneNames"/>).
    /// </summary>
    internal class ClipSceneData : SceneCustomFunctionController
    {
        /// <summary>Called once at start-up; kept apart so the theme still loads without KKAPI.</summary>
        public static void Register()
        {
            StudioSaveLoadApi.RegisterExtraBehaviour<ClipSceneData>(StudioThemePlugin.GUID);
        }

        protected override void OnSceneSave()
        {
            var clip = CameraClip.Serialize();
            var names = SceneNames.Serialize();
            if (clip == null && names == null) { SetExtendedData(null); return; }
            var data = new PluginData { version = 1 };
            if (clip != null) data.data["clip"] = clip;
            if (names != null) data.data["names"] = names;
            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            if (operation != SceneOperationKind.Import) { CameraClip.Clear(); SceneNames.Clear(); }
            if (operation == SceneOperationKind.Clear) return;
            var data = GetExtendedData();
            if (data == null || data.data == null) return;
            System.Func<int, int?> remap = key =>
            {
                ObjectCtrlInfo oci;
                if (loadedItems != null && loadedItems.TryGetValue(key, out oci) && oci != null && oci.objectInfo != null) return oci.objectInfo.dicKey;
                return operation == SceneOperationKind.Load ? key : (int?)null;
            };
            object value;
            if (data.data.TryGetValue("clip", out value)) CameraClip.Deserialize(value as string, operation == SceneOperationKind.Load, remap);
            if (data.data.TryGetValue("names", out value)) SceneNames.Deserialize(value as string, remap);
        }
    }
}
