using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// MoarCamz's own controls, which hang off the camera bar: its four buttons (add / remove slot,
    /// center on target, show settings) become icon buttons in one row at the left end of the bar, and
    /// its camera settings become one glass card: rounded fields, the themed slider, capsule buttons,
    /// and the drag pads (XZ, Y, distance) as rounded tiles with line icons. MoarCamz's green "on"
    /// colour shows as the accent, like everywhere else.
    /// </summary>
    internal class MoarCamzTheme : MonoBehaviour
    {
        // MoarCamz's prefabs are scaled 0.65 under the camera bar; positions below are in their own units.
        private static readonly string[] ControlOrder = { "Toggle UI", "Toggle Center", "Del Cam Button", "Add Cam Button" };
        private const float ControlSize = 36f, ControlStride = 42f;

        private readonly Undo _undo = new Undo();
        private CameraBar _bar;
        private readonly List<RectTransform> _controlButtons = new List<RectTransform>();
        private float _controlsY;
        private bool _removed;
        private float _next;

        /// <summary>Theme MoarCamz's controls under <paramref name="bar"/> once both of its prefabs exist.</summary>
        public static MoarCamzTheme TryApply(Transform bar, CameraBar cameraBar, Undo undo)
        {
            Transform controls = null, settings = null;
            for (int i = 0; i < bar.childCount; i++)
            {
                var c = bar.GetChild(i);
                if (c.name.StartsWith("MoarCamzControls")) controls = c;
                else if (c.name.StartsWith("CameraSettings")) settings = c;
            }
            if (controls == null || settings == null || bar.GetComponent<MoarCamzTheme>() != null) return null;
            var t = bar.gameObject.AddComponent<MoarCamzTheme>();
            undo.Add(t.Remove);   // first, so a failure part-way is still put back
            t._bar = cameraBar;
            t.Init((RectTransform)controls, settings);
            return t;
        }

        private void Init(RectTransform controls, Transform settings)
        {

            // ---- the four buttons: icons, one row
            IconButton(controls.Find("Add Cam Button"), "m_plus", "Add a camera slot");
            IconButton(controls.Find("Del Cam Button"), "m_minus", "Remove the last camera slot");
            IconButton(controls.Find("Toggle Center"), "l_target", "Keep the camera on its center target");
            IconButton(controls.Find("Toggle UI"), "v_option", "Camera settings");
            // + shows a hidden slot first (only three show by default); - hides an empty one first.
            Wrap(controls.Find("Add Cam Button"), () => _bar != null && _bar.RevealSlot());
            Wrap(controls.Find("Del Cam Button"), () => _bar != null && _bar.HideSlot());
            var bg = controls.GetComponent<Image>();
            if (bg != null) ControlStyler.Swap(bg, bg.sprite, bg.type, Palette.White(0f), _undo);
            foreach (var n in ControlOrder)
            {
                var b = controls.Find(n) as RectTransform;
                if (b == null) continue;
                _controlButtons.Add(b);
                var p0 = b.anchoredPosition; var s0 = b.sizeDelta;
                _undo.Add(() => { if (b != null) { b.anchoredPosition = p0; b.sizeDelta = s0; } });
            }
            // The camera bar's row is centred 23 below its top; this prefab sits at y -20, scaled 0.65.
            float scale = Mathf.Approximately(controls.localScale.y, 0f) ? 1f : controls.localScale.y;
            _controlsY = (-23f - controls.anchoredPosition.y) / scale;
            LayoutControls();

            // ---- the settings card
            var root = settings.GetComponent<Image>();
            if (root != null) ControlStyler.Swap(root, root.sprite, root.type, Palette.White(0f), _undo);
            IconButton(settings.Find("Distance/PositionLockButton"), "l_cam_lock", "Lock the camera position");
            IconButton(settings.Find("AdditionalOptions/LockOnButton"), "l_target", "Lock on to the center target");
            Pad(settings.Find("ScrollNav/FastNav/XZScroll"), "m_pad_xz");
            Pad(settings.Find("ScrollNav/SlowNav/XScroll"), "m_pad_x");
            Pad(settings.Find("ScrollNav/SlowNav/ZScroll"), "m_pad_z");
            Pad(settings.Find("ScrollNav/YScroll"), "m_pad_y");
            Pad(settings.Find("ScrollNav/DScroll"), "m_pad_d");
            ControlStyler.StylePanel(settings, _undo);   // fields, slider, text buttons, labels

            // Glass behind everything in the card.
            var parts = new List<RectTransform>();
            foreach (var g in settings.GetComponentsInChildren<Graphic>(true))
            {
                if (g.transform == settings || g.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                if (g.GetComponentInParent<Selectable>() != null && g.GetComponent<Selectable>() == null) continue;   // parts of a control
                if (g.transform.parent != null && g.transform.parent.name == "Text Area") continue;
                parts.Add(g.rectTransform);
            }
            if (parts.Count > 0)
            {
                var go = new GameObject(ThemedButton.OwnPrefix + "Card", typeof(RectTransform));
                go.layer = settings.gameObject.layer;
                go.transform.SetParent(settings, false);
                go.transform.SetAsFirstSibling();
                var glass = go.AddComponent<GlassRect>();
                glass.sprite = Sprites.Panel;
                glass.padding = 10f;
                glass.corner = Palette.PanelRadius;
                glass.color = Color.white;
                glass.raycastTarget = false;
                glass.around = parts.ToArray();
                _undo.Add(() => { if (go != null) Destroy(go); });
            }
        }

        private void IconButton(Transform t, string icon, string tip)
        {
            var button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) return;
            // The "+" / "-" are text: hide it, the icon says it.
            foreach (var text in t.GetComponentsInChildren<Text>(true))
            {
                var c = text.color;
                text.color = new Color(c.r, c.g, c.b, 0f);
                var txt = text;
                _undo.Add(() => { if (txt != null) txt.color = c; });
            }
            var b = ThemedButton.AttachIcon(button, ButtonKind.Push, icon);
            if (b == null) return;
            b.Tooltip = () => tip;
            _undo.Add(b.Remove);
        }

        /// <summary>Run <paramref name="first"/> on click; only if it returns false, MoarCamz's own handler.</summary>
        private void Wrap(Transform t, System.Func<bool> first)
        {
            var button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) return;
            var original = button.onClick;
            var mine = new Button.ButtonClickedEvent();
            mine.AddListener(() =>
            {
                bool handled = false;
                try { handled = first(); } catch (System.Exception e) { StudioThemePlugin.Log.LogWarning(e); }
                if (!handled) original.Invoke();
            });
            button.onClick = mine;
            _undo.Add(() => { if (button != null) button.onClick = original; });
        }

        /// <summary>A drag pad: MoarCamz's arrow picture becomes a soft rounded tile with a line icon (it stays the drag target).</summary>
        private void Pad(Transform t, string icon)
        {
            var image = t != null ? t.GetComponent<Image>() : null;
            if (image == null) return;
            ControlStyler.Swap(image, Sprites.Row, Image.Type.Sliced, Palette.White(0.12f), _undo);
            var go = new GameObject(ThemedButton.OwnPrefix + "Pad Icon", typeof(RectTransform));
            go.layer = t.gameObject.layer;
            go.transform.SetParent(t, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Mathf.Min(((RectTransform)t).rect.width, ((RectTransform)t).rect.height) * 0.55f;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = Sprites.Icon(icon);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Palette.White(0.8f);
            _undo.Add(() => { if (go != null) Destroy(go); });
        }

        /// <summary>The visible buttons in one row ending at MoarCamz's own spot (next to the slots).</summary>
        private void LayoutControls()
        {
            var visible = new List<RectTransform>();
            foreach (var b in _controlButtons) if (b != null && b.gameObject.activeSelf) visible.Add(b);
            for (int i = 0; i < visible.Count; i++)
            {
                var b = visible[i];
                var pos = new Vector2(-(visible.Count - 1 - i) * ControlStride, _controlsY);
                if (b.anchoredPosition != pos) b.anchoredPosition = pos;
                var size = new Vector2(ControlSize, ControlSize);
                if (b.sizeDelta != size) b.sizeDelta = size;
            }
        }

        private void LateUpdate()
        {
            if (_removed || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            LayoutControls();   // Remove only shows with extra slots; MoarCamz shows and hides it
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
