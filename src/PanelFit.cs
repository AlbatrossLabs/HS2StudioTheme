using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Grows a panel's glass downwards when its contents reach (or pass) its bottom edge, so the last
    /// row has the same breathing room as the first. Studio's old pictures had a frame that hid tight
    /// bottoms; plugins that add rows (Joint Adjustment) can also run past the end.
    /// </summary>
    internal class PanelFit : MonoBehaviour
    {
        private const float Bottom = 10f;   // space under the last row

        private RectTransform _rt;
        private Vector2 _origSize;
        private float _next;
        private bool _removed;
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly Vector3[] _corners = new Vector3[4];

        // Rows a plugin hung under the panel's bottom edge (HS2PE's Crotch / Ankles), re-pinned to the top.
        private class Pinned { public RectTransform Rt; public Vector2 AMin, AMax, OMin, OMax; public RawImage Bg; public Color BgColor; }
        private readonly List<Pinned> _pinned = new List<Pinned>();

        public static PanelFit Attach(RectTransform panel)
        {
            if (panel == null || panel.GetComponent<PanelFit>() != null) return null;
            if (panel.anchorMin != panel.anchorMax || panel.pivot.y != 1f) return null;   // only top-anchored, fixed-size panels
            var f = panel.gameObject.AddComponent<PanelFit>();
            f._rt = panel;
            f._origSize = panel.sizeDelta;
            return f;
        }

        private void OnEnable() { _next = 0f; }

        /// <summary>A copy another plugin made of a fitted panel: drop it (the copy can get its own).</summary>
        public void ClearIfCopy()
        {
            if (_rt == null) DestroyImmediate(this);
        }

        /// <summary>Whether this is a working one (not a copy's leftover).</summary>
        public bool Live { get { return _rt != null && !_removed; } }

        private void LateUpdate()
        {
            if (_removed || Time.unscaledTime < _next) return;
            if (_rt == null) { Destroy(this); return; }   // a copy another plugin made
            _next = Time.unscaledTime + 0.5f;

            PinBottomChildren();

            // Lowest visible content (the panel's own picture and the theme's captions don't count).
            float lowest = 0f;
            bool any = false;
            GetComponentsInChildren(false, _graphics);
            foreach (var g in _graphics)
            {
                if (g.transform == transform || g.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                if (g.transform.parent != null && g.transform.parent.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                g.rectTransform.GetWorldCorners(_corners);
                float y = _rt.InverseTransformPoint(_corners[0]).y;
                if (!any || y < lowest) { lowest = y; any = true; }
            }
            if (!any) return;

            // Local y runs from 0 at the top (pivot) downwards.
            float needed = Mathf.Max(_origSize.y, -lowest + Bottom);
            if (Mathf.Abs(_rt.sizeDelta.y - needed) > 0.5f)
                _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, needed);
        }

        /// <summary>
        /// A child anchored to the panel's bottom moves down whenever the panel grows, so it's moved to
        /// the same place measured from the top (where it sits at Studio's height), and its own
        /// background (HS2PE's grey strip) cleared so the rows sit on the panel's glass.
        /// </summary>
        private void PinBottomChildren()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i) as RectTransform;
                if (c == null || c.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                if (c.anchorMin.y != 0f || c.anchorMax.y != 0f) continue;
                if (_pinned.Exists(p => p.Rt == c)) continue;
                var pin = new Pinned { Rt = c, AMin = c.anchorMin, AMax = c.anchorMax, OMin = c.offsetMin, OMax = c.offsetMax };
                c.anchorMin = new Vector2(pin.AMin.x, 1f);
                c.anchorMax = new Vector2(pin.AMax.x, 1f);
                c.offsetMin = new Vector2(pin.OMin.x, pin.OMin.y - _origSize.y);
                c.offsetMax = new Vector2(pin.OMax.x, pin.OMax.y - _origSize.y);
                pin.Bg = c.GetComponent<RawImage>();
                if (pin.Bg != null) { pin.BgColor = pin.Bg.color; pin.Bg.color = Color.clear; }
                _pinned.Add(pin);
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var p in _pinned)
            {
                if (p.Rt == null) continue;
                p.Rt.anchorMin = p.AMin; p.Rt.anchorMax = p.AMax;
                p.Rt.offsetMin = p.OMin; p.Rt.offsetMax = p.OMax;
                if (p.Bg != null) p.Bg.color = p.BgColor;
            }
            if (_rt != null) _rt.sizeDelta = _origSize;
            Destroy(this);
        }
    }
}
