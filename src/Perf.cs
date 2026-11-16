using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// F1 › Log performance: times every per-frame method of the theme (and its colour hook) and writes the
    /// heaviest ones to the log every 5 seconds, in milliseconds per frame. Patched in only while it's on.
    /// </summary>
    internal static class Perf
    {
        private class Stat { public long Ticks, Max; public int Calls; }

        private static Harmony _harmony;
        private static readonly Dictionary<MethodBase, Stat> Stats = new Dictionary<MethodBase, Stat>();
        private static float _since;
        private static int _frame0;
        private static int _lastFrame;
        private static long _frameTicks, _worstFrame;

        public static void Set(bool on)
        {
            if (on && _harmony == null)
            {
                _harmony = new Harmony(StudioThemePlugin.GUID + ".perf");
                var pre = new HarmonyMethod(typeof(Perf), nameof(Pre));
                var post = new HarmonyMethod(typeof(Perf), nameof(Post));
                int n = 0;
                foreach (var t in typeof(Perf).Assembly.GetTypes())
                {
                    if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                    foreach (var name in new[] { "Update", "LateUpdate" })
                    {
                        var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                        if (m == null) continue;
                        try { _harmony.Patch(m, pre, post); n++; } catch (Exception e) { StudioThemePlugin.Log.LogDebug("Perf: " + m.Name + ": " + e.Message); }
                    }
                }
                var hook = AccessTools.Method(typeof(Hooks), "GraphicColorSet");
                if (hook != null) { try { _harmony.Patch(hook, pre, post); n++; } catch (Exception) { } }
                Reset();
                StudioThemePlugin.Log.LogInfo("Performance log on: timing " + n + " methods, a report every 5 seconds");
            }
            else if (!on && _harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
                Stats.Clear();
                StudioThemePlugin.Log.LogInfo("Performance log off");
            }
        }

        private static void Reset()
        {
            Stats.Clear();
            _since = Time.unscaledTime;
            _frame0 = Time.frameCount;
            _frameTicks = 0;
            _worstFrame = 0;
        }

        private static void Pre(out long __state) { __state = Stopwatch.GetTimestamp(); }

        private static void Post(long __state, MethodBase __originalMethod)
        {
            long dt = Stopwatch.GetTimestamp() - __state;
            Stat s;
            if (!Stats.TryGetValue(__originalMethod, out s)) Stats[__originalMethod] = s = new Stat();
            s.Ticks += dt;
            s.Calls++;
            if (dt > s.Max) s.Max = dt;

            int f = Time.frameCount;
            if (f != _lastFrame)
            {
                if (_frameTicks > _worstFrame) _worstFrame = _frameTicks;
                _frameTicks = 0;
                _lastFrame = f;
            }
            _frameTicks += dt;

            if (Time.unscaledTime - _since >= 5f) Report();
        }

        private static void Report()
        {
            int frames = Math.Max(1, Time.frameCount - _frame0);
            double ms = 1000.0 / Stopwatch.Frequency;
            long total = Stats.Values.Sum(s => s.Ticks);
            var sb = new StringBuilder();
            sb.AppendFormat("Studio Theme performance over {0} frames: {1:0.000} ms per frame on average, worst frame {2:0.00} ms\n",
                frames, total * ms / frames, _worstFrame * ms);
            foreach (var kv in Stats.OrderByDescending(kv => kv.Value.Ticks).Take(14))
                sb.AppendFormat("  {0,-34} {1,7:0.000} ms/frame  {2,6:0.0} calls/frame  max {3:0.00} ms\n",
                    kv.Key.DeclaringType.Name + "." + kv.Key.Name, kv.Value.Ticks * ms / frames, kv.Value.Calls / (double)frames, kv.Value.Max * ms);
            StudioThemePlugin.Log.LogInfo(sb.ToString());
            Reset();
        }
    }
}
