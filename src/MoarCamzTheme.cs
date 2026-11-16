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
        private RectTransform _add, _del, _settings;
        private readonly List<RectTransform> _cardParts = new List<RectTransform>();
        private bool _cardMoved;
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
            _add = controls.Find("Add Cam Button") as RectTransform;
            _del = controls.Find("Del Cam Button") as RectTransform;
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
            _settings = settings as RectTransform;
            try { AddClipRow(); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't add the culling distance row: " + e.Message); }
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
                _cardParts.AddRange(parts);
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
            foreach (var b in _controlButtons)
                if (b != null && b.gameObject.activeSelf && b != _add && b != _del && b.localScale != Vector3.zero) visible.Add(b);
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
            if (_removed) return;
            SyncClipRow();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            LayoutControls();   // Center only shows with a center target; MoarCamz shows and hides it
        }

        // ---------------------------------------------------------------- + after the numbers

        /// <summary>
        /// Put MoarCamz's + just right of the camera numbers, starting at <paramref name="x"/> (bar coordinates),
        /// centred on <paramref name="y"/>. Returns where the next thing on the row can start.
        /// </summary>
        public float PlaceAdd(RectTransform bar, float x, float y)
        {
            if (_add == null || !_add.gameObject.activeInHierarchy) return x + 2f;
            _add.GetWorldCorners(_corners);
            float w = bar.InverseTransformPoint(_corners[2]).x - bar.InverseTransformPoint(_corners[0]).x;
            var world = bar.TransformPoint(new Vector3(x + w / 2f, y, 0f));
            var local = _add.parent.InverseTransformPoint(world);
            var r = _add.rect;
            local += new Vector3((_add.pivot.x - 0.5f) * r.width, (_add.pivot.y - 0.5f) * r.height, 0f);
            if ((_add.localPosition - local).sqrMagnitude > 0.01f) _add.localPosition = local;
            x += w + 4f;
            // MoarCamz's - (shown only with extra slots) right after it.
            if (_del != null && _del.gameObject.activeInHierarchy)
            {
                var dworld = bar.TransformPoint(new Vector3(x + w / 2f, y, 0f));
                var dlocal = _del.parent.InverseTransformPoint(dworld);
                var dr = _del.rect;
                dlocal += new Vector3((_del.pivot.x - 0.5f) * dr.width, (_del.pivot.y - 0.5f) * dr.height, 0f);
                if ((_del.localPosition - dlocal).sqrMagnitude > 0.01f) _del.localPosition = dlocal;
                x += w + 4f;
            }
            return x + 2f;
        }

        private readonly Vector3[] _corners = new Vector3[4];

        // ---------------------------------------------------------------- the card under the bar

        /// <summary>Move the settings card so it's centred on <paramref name="centreX"/> with its top at <paramref name="top"/> (bar coordinates).</summary>
        public void AlignCard(RectTransform bar, float centreX, float top)
        {
            if (_settings == null || !_settings.gameObject.activeInHierarchy || _cardParts.Count == 0) return;
            bool any = false;
            float minX = 0f, maxX = 0f, maxY = 0f;
            foreach (var p in _cardParts)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                p.GetWorldCorners(_corners);
                for (int i = 0; i < 4; i++)
                {
                    var q = bar.InverseTransformPoint(_corners[i]);
                    if (!any) { minX = maxX = q.x; maxY = q.y; any = true; }
                    else { minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); maxY = Mathf.Max(maxY, q.y); }
                }
            }
            if (!any) return;
            float pad = 10f * _settings.localScale.y;   // the glass's padding, in bar units
            var delta = new Vector3(centreX - (minX + maxX) / 2f, top - (maxY + pad), 0f);
            if (delta.sqrMagnitude < 0.25f) return;
            if (!_cardMoved)
            {
                _cardMoved = true;
                var p0 = _settings.localPosition;
                var s = _settings;
                _undo.Add(() => { if (s != null) s.localPosition = p0; });
            }
            _settings.localPosition += delta;
        }

        // ---------------------------------------------------------------- culling distance row

        private TMPro.TMP_InputField _clipField;
        private Slider _clipSlider;
        private bool _syncing;
        private float _shownClip = -1f;

        /// <summary>
        /// "Clip:" under "FOV:", built from copies of the FOV row (so it looks the same): the camera's culling
        /// distance as a number and a slider. Clicking "Clip:" puts Studio's default back.
        /// </summary>
        private void AddClipRow()
        {
            object plugin = null;
            if (HarmonyLib.AccessTools.TypeByName("MoarCamz.MoarCamzPlugin") != null)
                plugin = HarmonyLib.Traverse.CreateWithType("MoarCamz.MoarCamzPlugin").Property("Instance").GetValue();
            if (plugin == null) return;
            var t = HarmonyLib.Traverse.Create(plugin);
            var fovButton = t.Field("FOVButton").GetValue<Button>();
            var disButton = t.Field("DistanceButton").GetValue<Button>();
            var fov = t.Field("FOV").GetValue<TMPro.TMP_InputField>();
            var slider = t.Field("FOVSlider").GetValue<Slider>();
            if (fovButton == null || disButton == null || fov == null || slider == null) return;
            var down = fovButton.transform.position - disButton.transform.position;   // one row

            var b = Copy(fovButton.gameObject, down);
            var button = b.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(CameraClip.Reset);
            foreach (var txt in b.GetComponentsInChildren<TMPro.TMP_Text>(true)) txt.text = "Clip:";
            foreach (var txt in b.GetComponentsInChildren<Text>(true)) txt.text = "Clip:";

            var f = Copy(fov.gameObject, down);
            _clipField = f.GetComponent<TMPro.TMP_InputField>();
            _clipField.onEndEdit = new TMPro.TMP_InputField.SubmitEvent();
            _clipField.onSubmit = new TMPro.TMP_InputField.SubmitEvent();
            _clipField.onValueChanged = new TMPro.TMP_InputField.OnChangeEvent();
            _clipField.contentType = TMPro.TMP_InputField.ContentType.DecimalNumber;
            _clipField.onEndEdit.AddListener(v =>
            {
                float value;
                if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) CameraClip.Set(value);
                _shownClip = -1f;
            });

            var s = Copy(slider.gameObject, down);
            _clipSlider = s.GetComponent<Slider>();
            _clipSlider.onValueChanged = new Slider.SliderEvent();
            _clipSlider.wholeNumbers = false;
            _clipSlider.minValue = 0f;
            _clipSlider.maxValue = 1f;
            _clipSlider.onValueChanged.AddListener(v => { if (!_syncing) CameraClip.Set(CameraClip.FromSlider(v)); });

            _clipButton = button;
            SyncClipRow();
        }

        private Button _clipButton;

        private GameObject Copy(GameObject source, Vector3 offset)
        {
            var go = Instantiate(source, source.transform.parent);
            go.name = source.name + " Clip";
            go.transform.position = source.transform.position + offset;
            _undo.Add(() => { if (go != null) Destroy(go); });
            return go;
        }

        private void SyncClipRow()
        {
            if (_clipField == null || _clipSlider == null) return;
            if (_clipButton != null)
            {
                var themed = _clipButton.GetComponent<ThemedButton>();
                if (themed != null && themed.Tooltip == null)
                    themed.Tooltip = () => "Culling distance: anything closer to this camera isn't drawn (saved per camera with the scene). Click to reset.";
            }
            float c = CameraClip.Current;
            if (Mathf.Abs(c - _shownClip) < 1e-5f) return;
            _shownClip = c;
            if (!_clipField.isFocused) _clipField.text = CameraClip.Format(c);
            _syncing = true;
            try { _clipSlider.value = CameraClip.ToSlider(c); }
            finally { _syncing = false; }
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
