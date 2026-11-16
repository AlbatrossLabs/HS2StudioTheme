using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// Near clip ("culling") distance per camera: the free camera and each camera object in the scene keep
    /// their own, set from the camera menu. Anything closer to the camera than this isn't drawn, so a camera
    /// can look through a wall or past a prop in front of it. Saved with the scene (<see cref="ClipSceneData"/>).
    /// </summary>
    internal static class CameraClip
    {
        public const float Min = 0.01f, Max = 500f;
        private static readonly Dictionary<int, float> ByCamera = new Dictionary<int, float>();
        private static float _free = -1f, _default = -1f;
        private static Camera _camera;

        /// <summary>Studio's own near clip (0.1), read from the camera before the theme changes it.</summary>
        public static float Default { get { return _default > 0f ? _default : 0.1f; } }

        /// <summary>The scene camera being looked through, or -1 for the free camera.</summary>
        private static int ActiveKey()
        {
            try
            {
                var s = Studio.Studio.Instance;
                var c = s != null ? s.ociCamera : null;
                return c != null && c.objectInfo != null ? c.objectInfo.dicKey : -1;
            }
            catch { return -1; }
        }

        public static float Current
        {
            get
            {
                float v;
                int key = ActiveKey();
                if (key < 0) return _free > 0f ? _free : Default;
                return ByCamera.TryGetValue(key, out v) ? v : Default;
            }
        }

        public static bool IsCustom
        {
            get { int key = ActiveKey(); return key < 0 ? _free > 0f : ByCamera.ContainsKey(key); }
        }

        public static void Set(float value)
        {
            value = Mathf.Clamp(value, Min, Max);
            int key = ActiveKey();
            if (key < 0) _free = value; else ByCamera[key] = value;
            Apply();
        }

        public static void Reset()
        {
            int key = ActiveKey();
            if (key < 0) _free = -1f; else ByCamera.Remove(key);
            Apply();
        }

        /// <summary>Slider position (0..1, logarithmic) for a distance, and back.</summary>
        public static float ToSlider(float v) { return Mathf.InverseLerp(Mathf.Log(Min), Mathf.Log(Max), Mathf.Log(Mathf.Clamp(v, Min, Max))); }
        public static float FromSlider(float t) { return Mathf.Exp(Mathf.Lerp(Mathf.Log(Min), Mathf.Log(Max), t)); }

        public static string Format(float v) { return v < 1f ? v.ToString("0.00", CultureInfo.InvariantCulture) : v.ToString("0.0", CultureInfo.InvariantCulture); }

        /// <summary>
        /// Every frame: the camera in use gets its distance. Studio's camera is driven by Cinemachine, which
        /// copies its lens (Studio's CameraControl.lensSettings) onto the camera each frame, so the distance
        /// goes into that lens; the camera itself is set too, so it shows straight away.
        /// </summary>
        public static void Apply()
        {
            object ctrl = null;
            try { ctrl = HarmonyLib.Traverse.Create(Studio.Studio.Instance).Property("cameraCtrl").GetValue(); }
            catch { ctrl = null; }
            if (ctrl == null) return;

            if (_lensField == null || _lensField.DeclaringType == null || !_lensField.DeclaringType.IsInstanceOfType(ctrl))
            {
                _lensField = HarmonyLib.AccessTools.Field(ctrl.GetType(), "lensSettings");
                _nearField = _lensField != null ? HarmonyLib.AccessTools.Field(_lensField.FieldType, "NearClipPlane") : null;
                _camera = null;
            }
            if (_camera == null)
            {
                try { _camera = HarmonyLib.Traverse.Create(ctrl).Property("mainCmaera").GetValue<Camera>(); } catch { }
                if (_camera == null) { var c = ctrl as Component; if (c != null) _camera = c.GetComponent<Camera>(); }
                if (_camera == null) _camera = Camera.main;
            }

            object lens = _lensField != null && _nearField != null ? _lensField.GetValue(ctrl) : null;
            if (_default < 0f)
            {
                if (lens != null) _default = (float)_nearField.GetValue(lens);
                else if (_camera != null) _default = _camera.nearClipPlane;
                if (_default <= 0f) _default = 0.1f;
            }
            float want = Current;
            if (lens != null && Mathf.Abs((float)_nearField.GetValue(lens) - want) > 1e-5f)
            {
                _nearField.SetValue(lens, want);   // the boxed copy...
                _lensField.SetValue(ctrl, lens);   // ...back into Studio's camera control
                // Studio copies its lens into the state Cinemachine reads only when the field of view is set:
                // set it to what it is.
                try
                {
                    var fov = HarmonyLib.Traverse.Create(ctrl).Property("fieldOfView");
                    fov.SetValue(fov.GetValue<float>());
                }
                catch (System.Exception e) { if (!_fovWarned) { _fovWarned = true; StudioThemePlugin.Log.LogWarning("Culling distance: " + e.Message); } }
            }
            if (_camera != null && Mathf.Abs(_camera.nearClipPlane - want) > 1e-5f) _camera.nearClipPlane = want;
            // Studio's second camera (drawn with the main one) gets the same distance.
            if (_sub == null) { try { _sub = HarmonyLib.Traverse.Create(ctrl).Property("subCamera").GetValue<Camera>(); } catch { } }
            if (_sub != null && _sub != _camera && Mathf.Abs(_sub.nearClipPlane - want) > 1e-5f)
            {
                if (_subDefault < 0f) _subDefault = _sub.nearClipPlane;
                _sub.nearClipPlane = want;
            }
        }

        private static Camera _sub;
        private static float _subDefault = -1f;

        private static System.Reflection.FieldInfo _lensField, _nearField;
        private static bool _fovWarned;

        /// <summary>Theme off: Studio's own distance back.</summary>
        public static void Restore()
        {
            if (_default <= 0f) return;
            try
            {
                var ctrl = HarmonyLib.Traverse.Create(Studio.Studio.Instance).Property("cameraCtrl").GetValue();
                if (ctrl != null && _lensField != null && _nearField != null)
                {
                    var lens = _lensField.GetValue(ctrl);
                    _nearField.SetValue(lens, _default);
                    _lensField.SetValue(ctrl, lens);
                    var fov = HarmonyLib.Traverse.Create(ctrl).Property("fieldOfView");
                    fov.SetValue(fov.GetValue<float>());
                }
            }
            catch { }
            if (_camera != null) _camera.nearClipPlane = _default;
            if (_sub != null && _subDefault > 0f) _sub.nearClipPlane = _subDefault;
        }

        // ---------------------------------------------------------------- scene data

        public static void Clear()
        {
            ByCamera.Clear();
            _free = -1f;
        }

        /// <summary>"free=0.5;12=1.2;30=0.05": camera object keys as saved in the scene.</summary>
        public static string Serialize()
        {
            if (_free <= 0f && ByCamera.Count == 0) return null;
            var parts = new List<string>();
            if (_free > 0f) parts.Add("free=" + _free.ToString("R", CultureInfo.InvariantCulture));
            parts.AddRange(ByCamera.Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
            return string.Join(";", parts.ToArray());
        }

        /// <param name="remap">Saved camera key to the key it has now (imports renumber objects), or null if gone.</param>
        public static void Deserialize(string text, bool withFree, System.Func<int, int?> remap)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                float v;
                if (eq <= 0 || !float.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                var k = part.Substring(0, eq);
                int key;
                if (k == "free") { if (withFree) _free = Mathf.Clamp(v, Min, Max); }
                else if (int.TryParse(k, out key))
                {
                    var now = remap(key);
                    if (now.HasValue) ByCamera[now.Value] = Mathf.Clamp(v, Min, Max);
                }
            }
        }
    }
}
