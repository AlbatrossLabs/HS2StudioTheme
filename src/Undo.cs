using System;
using System.Collections.Generic;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// Everything the theme changed in the settings panels, in order, so turning it off can put each
    /// change back in reverse. A failing step is logged and skipped so the rest still restore.
    /// </summary>
    internal class Undo
    {
        private readonly List<Action> _steps = new List<Action>();

        public void Add(Action restore) { _steps.Add(restore); }

        public void Run()
        {
            for (int i = _steps.Count - 1; i >= 0; i--)
            {
                try { _steps[i](); }
                catch (Exception e) { StudioThemePlugin.Log.LogDebug("Restore step failed: " + e.Message); }
            }
            _steps.Clear();
        }
    }
}
