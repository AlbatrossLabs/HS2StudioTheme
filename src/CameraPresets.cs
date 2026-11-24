using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// A Graphics plugin (ore.graphics) preset per camera: each camera object and each saved view (slot) can
    /// have one, and switching to that camera loads it, so a scene can have a day camera and a night camera,
    /// or a soft-focus close-up. Picked from the camera menu (the camera in use) or a slot's right-click menu.
    /// Saved with the scene (<see cref="ClipSceneData"/>).
    ///
    /// A preset is Graphics' whole look (lights, sky, shadows, post-processing), loaded the way Graphics' own
    /// preset list does it (PresetManager.CurrentPreset = name), all through reflection so the theme doesn't
    /// need Graphics. A camera with no preset of its own is on Default: Graphics' default look for Studio (what it
    /// loads when Studio starts). Default is only loaded once some camera in the scene has a preset, so a scene
    /// that doesn't use presets keeps its own Graphics settings.
    ///
    /// Public for Camera Sweep, which calls <see cref="BeginSweep"/>, <see cref="ApplyForSlot"/>,
    /// <see cref="ApplyForCamera"/> and <see cref="EndSweep"/> by reflection while it shoots each camera.
    /// </summary>
    public static class CameraPresets
    {
        private const string GraphicsGuid = "ore.graphics";
        private const int Unknown = int.MinValue;

        private static readonly Dictionary<int, string> ByCamera = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> BySlot = new Dictionary<int, string>();
        private static string _free;          // the free camera's preset: the last view's, or picked for it
        private static int _lastKey = Unknown; // the camera last seen (-1 = the free camera)
        private static int _suspended;        // during a Camera Sweep: it says which preset each shot gets
        private static bool _sweepChanged;
        private static readonly HashSet<string> _missingWarned = new HashSet<string>();

        /// <summary>The view (slot index) last loaded or saved, while the free camera is in use; -1 = none.</summary>
        public static int LastSlot { get; private set; } = -1;

        // ---------------------------------------------------------------- Graphics
        //
        // Two generations of the Graphics plugin, both "ore.graphics":
        //  3.x  HS2PostProcessing.Plugin: Presets.List(), LoadLook(name), LoadDefaultForGameMode(),
        //       WritePreset() / ReadPreset(text); its window remembers the loaded name in MainWindow._lastPreset.
        //  0.5  Graphics.Graphics.PresetManager: PresetNames, CurrentPreset = name, LoadDefaultForCurrentGameMode(),
        //       GetExtendedData() / Load(PluginData).

        private static object _graphics;   // the plugin instance (3.x) or its PresetManager (0.5)
        private static bool _v3;
        private static float _lookedAt = -10f;

        /// <summary>Graphics (3.x plugin, or 0.5's PresetManager), or null without Graphics.</summary>
        private static object Graphics
        {
            get
            {
                if (_graphics != null) return _graphics;
                // Graphics finishes setting up a little after Studio starts: until then, look again at most once a second.
                if (Time.realtimeSinceStartup - _lookedAt < 1f) return null;
                _lookedAt = Time.realtimeSinceStartup;
                try
                {
                    BepInEx.PluginInfo info;
                    if (!Chainloader.PluginInfos.TryGetValue(GraphicsGuid, out info) || info == null || info.Instance == null) return null;
                    var plugin = info.Instance;
                    var t = Traverse.Create(plugin);
                    if (t.Method("LoadLook", new[] { typeof(string) }).MethodExists())
                    {
                        if (t.Property("Presets").GetValue() == null) return null;   // not set up yet
                        _v3 = true;
                        _graphics = plugin;
                    }
                    else
                    {
                        _v3 = false;
                        _graphics = t.Property("PresetManager").GetValue();
                    }
                    if (_graphics != null)
                        StudioThemePlugin.Log.LogInfo("Camera Graphics presets: using Graphics " + info.Metadata.Version + (_v3 ? "" : " (0.5 presets)"));
                }
                catch (System.Exception e) { StudioThemePlugin.Log.LogDebug("Graphics presets: " + e.Message); }
                return _graphics;
            }
        }

        /// <summary>Graphics is installed (the preset menus show).</summary>
        public static bool Available { get { return Graphics != null; } }

        /// <summary>Graphics' presets, A to Z.</summary>
        public static string[] Names
        {
            get
            {
                var g = Graphics;
                if (g == null) return new string[0];
                try
                {
                    IEnumerable<string> names;
                    if (_v3)
                    {
                        var store = Traverse.Create(g).Property("Presets").GetValue();
                        names = store != null ? Traverse.Create(store).Method("List").GetValue<List<string>>() : null;
                    }
                    else names = Traverse.Create(g).Property("PresetNames").GetValue<string[]>();
                    return (names ?? new string[0]).Where(n => !string.IsNullOrEmpty(n)).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToArray();
                }
                catch { return new string[0]; }
            }
        }

        private static string _lastLoaded;   // what this class loaded last (null after Default)

        /// <summary>The preset Graphics last loaded (its own list's choice), or null.</summary>
        public static string Loaded
        {
            get
            {
                var g = Graphics;
                if (g == null) return null;
                try
                {
                    if (!_v3) return Traverse.Create(g).Property("CurrentPreset").GetValue<string>();
                    var window = Traverse.Create(g).Field("_window").GetValue();
                    var name = window != null ? Traverse.Create(window).Field("_lastPreset").GetValue<string>() : null;
                    return string.IsNullOrEmpty(name) ? _lastLoaded : name;
                }
                catch { return _lastLoaded; }
            }
        }

        /// <summary>Tell Graphics' own window which preset is loaded (null = none), so its preset list matches.</summary>
        private static void MarkLoaded(string name)
        {
            _lastLoaded = name;
            var g = Graphics;
            if (g == null) return;
            try
            {
                if (_v3)
                {
                    var window = Traverse.Create(g).Field("_window").GetValue();
                    if (window != null)
                    {
                        Traverse.Create(window).Field("_lastPreset").SetValue(name);
                        Traverse.Create(window).Field("_dirty").SetValue(true);
                    }
                }
                else Traverse.Create(g).Field("_currentPresetName").SetValue(name);
            }
            catch { }
        }

        private static bool _defaultLoaded;   // the last thing loaded was Graphics' default (and nothing since)

        /// <summary>Any camera or view in the scene has a preset: then cameras without one load Default.</summary>
        public static bool UsesPresets { get { return _free != null || ByCamera.Count > 0 || BySlot.Count > 0; } }

        /// <summary>A camera's look: its preset, or (null) Default when the scene uses presets. True if something was loaded.</summary>
        private static bool Apply(string name)
        {
            if (name != null) return Load(name);
            return UsesPresets && LoadDefault();
        }

        /// <summary>Graphics' default look for Studio, unless it's what's loaded already. True if it was loaded now.</summary>
        private static bool LoadDefault()
        {
            var g = Graphics;
            if (g == null) return false;
            if (_defaultLoaded && Loaded == null) return false;
            try
            {
                Traverse.Create(g).Method(_v3 ? "LoadDefaultForGameMode" : "LoadDefaultForCurrentGameMode").GetValue();
                MarkLoaded(null);   // Graphics' list: no preset ticked
                _defaultLoaded = true;
                StudioThemePlugin.Log.LogDebug("Graphics default look loaded for the camera");
                return true;
            }
            catch (System.Exception e)
            {
                StudioThemePlugin.Log.LogWarning("Couldn't load Graphics' default look: " + e.Message);
                return false;
            }
        }

        /// <summary>Load a preset, unless it's the one loaded already. True if it was loaded now.</summary>
        private static bool Load(string name)
        {
            var g = Graphics;
            if (g == null || string.IsNullOrEmpty(name)) return false;
            if (name == Loaded && !_defaultLoaded) return false;
            if (!Names.Contains(name))
            {
                if (_missingWarned.Add(name)) StudioThemePlugin.Log.LogWarning("Graphics preset \"" + name + "\" isn't in the presets folder: the camera keeps the current look");
                return false;
            }
            try
            {
                if (_v3) Traverse.Create(g).Method("LoadLook", new[] { typeof(string) }).GetValue(name);
                else Traverse.Create(g).Property("CurrentPreset").SetValue(name);
                MarkLoaded(name);
                _defaultLoaded = false;
                StudioThemePlugin.Log.LogDebug("Graphics preset loaded for the camera: " + name);
                return true;
            }
            catch (System.Exception e)
            {
                StudioThemePlugin.Log.LogWarning("Couldn't load Graphics preset \"" + name + "\": " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        // ---------------------------------------------------------------- the camera in use

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

        /// <summary>The camera in use's preset, or null.</summary>
        public static string ForCurrent
        {
            get
            {
                int key = ActiveKey();
                if (key >= 0) { string v; return ByCamera.TryGetValue(key, out v) ? v : null; }
                return _free;
            }
        }

        /// <summary>What the camera menu calls the camera in use's preset.</summary>
        public static string CurrentTitle
        {
            get { return ActiveKey() < 0 && LastSlot >= 0 ? "Graphics preset, view " + (LastSlot + 1) : "Graphics preset"; }
        }

        /// <summary>
        /// Give the camera in use a preset (null = Default) and load it. On the free camera it also goes to the
        /// view last loaded or saved, so picking one right after going to view 3 is view 3's.
        /// </summary>
        public static void SetForCurrent(string name)
        {
            int key = ActiveKey();
            if (key >= 0)
            {
                if (name == null) ByCamera.Remove(key); else ByCamera[key] = name;
            }
            else
            {
                _free = name;
                if (LastSlot >= 0) { if (name == null) BySlot.Remove(LastSlot); else BySlot[LastSlot] = name; }
            }
            _lastKey = key;
            if (name != null) Load(name); else LoadDefault();
        }

        public static string ForSlot(int index) { string v; return BySlot.TryGetValue(index, out v) ? v : null; }

        /// <summary>Give a saved view a preset (null = Default); loaded now if that view is the one in use.</summary>
        public static void SetForSlot(int index, string name)
        {
            if (name == null) BySlot.Remove(index); else BySlot[index] = name;
            if (index == LastSlot && ActiveKey() < 0)
            {
                _free = name;
                if (name != null) Load(name); else LoadDefault();
            }
        }

        /// <summary>
        /// From the camera bar every frame, and right after Studio changes camera: a different camera than last
        /// time loads its preset. (Only on a change, so a preset changed in Graphics' own window stays.)
        /// </summary>
        public static void CameraChanged()
        {
            int key = ActiveKey();
            if (key == _lastKey) return;
            bool first = _lastKey == Unknown;
            _lastKey = key;
            if (first || _suspended > 0 || Time.frameCount == _slotLoadingFrame || !Available) return;
            if (key >= 0) LastSlot = -1;
            Apply(key >= 0 ? (ByCamera.ContainsKey(key) ? ByCamera[key] : null) : _free);
        }

        // ---------------------------------------------------------------- views (slots)

        public static void SaveSlot(int no)
        {
            LastSlot = no;
            if (_free != null) BySlot[no] = _free;   // a view saved with no preset keeps the one it had
        }

        private static int _slotLoadingFrame = -1;

        /// <summary>Studio is about to load a view: the camera change inside it waits for the view's own preset.</summary>
        public static void SlotLoading() { _slotLoadingFrame = Time.frameCount; }

        public static void LoadSlot(int no)
        {
            _slotLoadingFrame = -1;
            LastSlot = no;
            _free = ForSlot(no);
            _lastKey = -1;   // a view is the free camera
            if (_suspended == 0) Apply(_free);
        }

        public static void SlotCleared(int no)
        {
            BySlot.Remove(no);
            if (LastSlot == no) LastSlot = -1;
        }

        public static void SlotRemoved(int no)
        {
            var moved = BySlot.Where(kv => kv.Key > no).OrderBy(kv => kv.Key).ToList();
            BySlot.Remove(no);
            foreach (var kv in moved) { BySlot.Remove(kv.Key); BySlot[kv.Key - 1] = kv.Value; }
            if (LastSlot == no) LastSlot = -1; else if (LastSlot > no) LastSlot--;
        }

        // ---------------------------------------------------------------- Camera Sweep

        private sealed class Snapshot { public object Data; public string Name; }

        /// <summary>
        /// A sweep starts: cameras no longer load their presets on their own (the sweep asks per shot), and the
        /// look now is remembered for <see cref="EndSweep"/>. Null if there's nothing to do (no Graphics, or no
        /// camera has a preset).
        /// </summary>
        public static object BeginSweep()
        {
            var g = Graphics;
            if (g == null || !UsesPresets) return null;
            _suspended++;
            _sweepChanged = false;
            var snap = new Snapshot { Name = Loaded };
            try { snap.Data = Traverse.Create(g).Method(_v3 ? "WritePreset" : "GetExtendedData").GetValue(); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't remember the Graphics settings before the sweep: " + e.Message); }
            return snap;
        }

        /// <summary>A sweep shot of saved view <paramref name="index"/>: its preset (or Default). True if something was loaded (let it settle).</summary>
        public static bool ApplyForSlot(int index)
        {
            bool loaded = Apply(ForSlot(index));
            _sweepChanged |= loaded;
            return loaded;
        }

        /// <summary>A sweep shot of the camera object with key <paramref name="dicKey"/>. True if a preset was loaded.</summary>
        public static bool ApplyForCamera(int dicKey)
        {
            string name;
            bool loaded = Apply(ByCamera.TryGetValue(dicKey, out name) ? name : null);
            _sweepChanged |= loaded;
            return loaded;
        }

        /// <summary>The sweep ended: the look from before it comes back (with any unsaved Graphics changes).</summary>
        public static void EndSweep(object token)
        {
            var snap = token as Snapshot;
            if (snap == null) return;
            if (_suspended > 0) _suspended--;
            _lastKey = ActiveKey();
            if (!_sweepChanged) return;
            var g = Graphics;
            if (g == null) return;
            try
            {
                _defaultLoaded = false;
                if (snap.Data != null)
                {
                    if (_v3) Traverse.Create(g).Method("ReadPreset", new[] { typeof(string) }).GetValue(snap.Data);
                    else
                    {
                        var load = AccessTools.GetDeclaredMethods(g.GetType())
                            .FirstOrDefault(x => x.Name == "Load" && x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.Name == "PluginData");
                        if (load != null) load.Invoke(g, new[] { snap.Data });
                    }
                }
                else if (snap.Name != null) Load(snap.Name);
                MarkLoaded(snap.Name);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't put the Graphics settings back after the sweep: " + e.Message); }
        }

        // ---------------------------------------------------------------- scene data

        public static void Clear()
        {
            ByCamera.Clear();
            BySlot.Clear();
            _free = null;
            LastSlot = -1;
            _lastKey = Unknown;   // the scene's own Graphics settings stay until the camera changes
            _defaultLoaded = false;
        }

        /// <summary>"free=Night;12=Day;s3=Close" (camera object keys, s = view index); names are escaped.</summary>
        public static string Serialize()
        {
            if (_free == null && ByCamera.Count == 0 && BySlot.Count == 0) return null;
            var parts = new List<string>();
            if (_free != null) parts.Add("free=" + Escape(_free));
            parts.AddRange(ByCamera.Select(kv => kv.Key + "=" + Escape(kv.Value)));
            parts.AddRange(BySlot.Select(kv => "s" + kv.Key + "=" + Escape(kv.Value)));
            return string.Join(";", parts.ToArray());
        }

        public static void Deserialize(string text, bool withFree, System.Func<int, int?> remap)
        {
            _lastKey = Unknown;
            if (string.IsNullOrEmpty(text)) return;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var k = part.Substring(0, eq);
                var v = Unescape(part.Substring(eq + 1));
                if (string.IsNullOrEmpty(v)) continue;
                int key;
                if (k == "free") { if (withFree) _free = v; }
                else if (k.Length > 1 && k[0] == 's' && int.TryParse(k.Substring(1), out key)) { if (withFree) BySlot[key] = v; }
                else if (int.TryParse(k, out key))
                {
                    var now = remap(key);
                    if (now.HasValue) ByCamera[now.Value] = v;
                }
            }
        }

        private static string Escape(string s) { return System.Uri.EscapeDataString(s); }
        private static string Unescape(string s) { try { return System.Uri.UnescapeDataString(s); } catch { return s; } }
    }

    /// <summary>The list of Graphics presets to pick from, shared by the camera menu, a view's menu and the camera card.</summary>
    internal static class PresetMenu
    {
        /// <summary>What a camera's preset shows as: its name (shortened), or Default.</summary>
        public static string Short(string name)
        {
            if (name == null) return "Default";
            return name.Length > 16 ? name.Substring(0, 15) + "\u2026" : name;
        }

        public static List<GlassMenu.Item> Items(string current, System.Action<string> set)
        {
            var loaded = CameraPresets.Loaded;
            var items = new List<GlassMenu.Item>
            {
                new GlassMenu.Item { Label = "Loads when you switch to this camera", Disabled = true },
                new GlassMenu.Item { Label = "Default", Right = "Graphics' startup look", Check = current == null, Act = () => set(null) },
                GlassMenu.Item.Sep(),
            };
            foreach (var name in CameraPresets.Names)
            {
                var n = name;
                items.Add(new GlassMenu.Item { Label = n, Check = n == current, Right = n == loaded && n != current ? "loaded" : null, Act = () => set(n) });
            }
            if (items.Count == 3) items.Add(new GlassMenu.Item { Label = "No presets in Graphics' presets folder", Disabled = true });
            return items;
        }
    }
}
