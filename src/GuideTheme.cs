using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The gizmo panel at the bottom left (Studio's "Guide Input"): glass, Position / Rotation / Scale
    /// rows with red / green / blue axis letters, rounded fields, reset icons, and the move / rotate /
    /// scale buttons as one segmented control with the current mode in the accent colour. HSUS's
    /// "Improved Transform Operations" (Copy / Paste / Reset Transform) are themed when they appear.
    /// </summary>
    internal class GuideTheme : MonoBehaviour
    {
        private static readonly string[] ModeButtons = { "Button Move", "Button Rotation", "Button Scale" };

        private readonly Undo _undo = new Undo();
        private readonly List<RectTransform> _modes = new List<RectTransform>();
        private GlassRect _segment;
        private bool _hsusDone, _removed;
        private float _next;

        public static GuideTheme Apply(Undo undo)
        {
            var scene = GameObject.Find("StudioScene");
            var panel = scene != null ? scene.transform.Find("Canvas Guide Input/Guide Input") : null;
            if (panel == null) { StudioThemePlugin.Log.LogWarning("Gizmo panel not found"); return null; }
            if (panel.GetComponent<GuideTheme>() != null) return null;
            var g = panel.gameObject.AddComponent<GuideTheme>();
            undo.Add(g.Remove);   // first, so a failure part-way is still put back
            g.Init();
            return g;
        }

        private void Init()
        {
            ControlStyler.StylePanel(transform, _undo);

            // One capsule behind the three mode buttons; the selected mode is Studio's "_01_" picture.
            foreach (var n in ModeButtons)
            {
                var t = transform.Find(n) as RectTransform;
                if (t == null) continue;
                _modes.Add(t);
                var b = t.GetComponent<ThemedButton>();
                if (b != null)
                {
                    var themed = b;
                    Sprite seen = null; bool on = false;
                    themed.ActiveWhen = () =>
                    {
                        var sp = themed.GameSprite;
                        if (!ReferenceEquals(sp, seen)) { seen = sp; on = sp != null && sp.name.StartsWith("sp_sn_11_01"); }   // name allocates
                        return on;
                    };
                }
            }
            if (_modes.Count > 0)
            {
                var go = new GameObject(ThemedButton.OwnPrefix + "Mode Segment", typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.transform.SetSiblingIndex(_modes[0].GetSiblingIndex());   // behind the buttons
                _segment = go.AddComponent<GlassRect>();
                _segment.sprite = Sprites.Panel;
                _segment.padding = 4f;
                _segment.corner = Palette.TileRadius;
                _segment.color = Color.white;
                _segment.raycastTarget = false;
                _segment.around = _modes.ToArray();
                _undo.Add(() => { if (go != null) Destroy(go); });
            }
            ThemeHsus();
        }

        private void LateUpdate()
        {
            if (_removed || _hsusDone || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            ThemeHsus();
        }

        /// <summary>HSUS adds "Additional Operations" (three text buttons on a grey box) next to the fields.</summary>
        private void ThemeHsus()
        {
            var extra = transform.Find("Additional Operations");
            if (extra == null) return;
            _hsusDone = true;
            var box = extra.GetComponent<Image>();
            if (box != null) ControlStyler.Swap(box, Sprites.Panel, Image.Type.Sliced, Color.white, _undo);
            foreach (var button in extra.GetComponentsInChildren<Button>(true))
            {
                var b = button.GetComponent<ThemedButton>();   // already themed if HSUS was first
                if (b == null)
                {
                    b = ThemedButton.Attach(button, ButtonKind.Push, null, null);
                    if (b == null) continue;
                    _undo.Add(b.Remove);
                }
                if (button.name.IndexOf("Reset", System.StringComparison.OrdinalIgnoreCase) >= 0) { b.LabelTint = Palette.Hex("FF6961"); b.Refresh(); }
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            _undo.Run();
            Destroy(this);
        }
    }
}
