using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Studio's rows of tiny On / Half / Off (and None / Some / Lots) picture buttons, drawn as a
    /// three-stop slider instead: a thin track through the buttons, a dot at each stop, a knob in the
    /// accent colour on the current state, and the state's name beside it. Studio's buttons stay
    /// exactly where they were as the (now invisible) click targets, so clicking a stop does what
    /// clicking the button always did. Studio's green "selected" colour is read, never shown.
    /// </summary>
    internal class StateSlider : MonoBehaviour, IGraphicColorHook
    {
        private static readonly Dictionary<string, int> StateOf = new Dictionary<string, int>
        {
            { "sp_sn_19_00_00", 0 }, { "sp_sn_19_00_01", 1 }, { "sp_sn_19_00_02", 2 },
            { "sp_sn_20_00_00", 10 }, { "sp_sn_20_00_01", 11 }, { "sp_sn_20_00_02", 12 },
        };
        private static readonly string[] Words = { "On", "Half", "Off" };
        private static readonly string[] Amounts = { "None", "Some", "Lots" };
        private const float Dot = 6f, KnobSize = 14f;

        private class Stop
        {
            public Button Button;
            public Image Image, DotImage;
            public int State;
            public Selectable.Transition Transition;
            public Color GameColor;
            public bool Selected;
            public Vector3 Centre;
            public Vector2 OrigPos;
        }

        private readonly List<Stop> _stops = new List<Stop>();
        private readonly Dictionary<Graphic, Stop> _byImage = new Dictionary<Graphic, Stop>();
        private readonly List<GameObject> _added = new List<GameObject>();
        private Image _knob, _ghost;
        private Text _word;
        private bool _writing, _removed, _dirty = true, _shifted;
        private readonly Dictionary<RectTransform, KeyValuePair<Vector2, Vector2>> _clipped = new Dictionary<RectTransform, KeyValuePair<Vector2, Vector2>>();
        private Stop _hover;
        private Transform _parent;

        /// <summary>Find the state button rows under <paramref name="root"/> and turn each into a slider. Returns the buttons handled.</summary>
        public static HashSet<Button> Build(Transform root, Undo undo)
        {
            var handled = new HashSet<Button>();
            // One slider per row: same parent, same height.
            var groups = new Dictionary<string, List<Button>>();
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                var img = b.image;
                if (img == null || img.sprite == null || !StateOf.ContainsKey(img.sprite.name) || b.transform.parent == null) continue;
                if (ControlStyler.InTemplate(b) || b.GetComponent<StopHover>() != null) continue;
                var key = b.transform.parent.GetInstanceID() + ":" + Mathf.RoundToInt(b.transform.localPosition.y);
                List<Button> list;
                if (!groups.TryGetValue(key, out list)) groups[key] = list = new List<Button>();
                list.Add(b);
            }
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                var parent = kv.Value[0].transform.parent;
                var host = new GameObject(ThemedButton.OwnPrefix + "State Slider", typeof(RectTransform));
                host.layer = parent.gameObject.layer;
                host.transform.SetParent(parent, false);
                host.AddComponent<LayoutElement>().ignoreLayout = true;
                var s = host.AddComponent<StateSlider>();
                s._parent = parent;
                undo.Add(s.Remove);
                s.Init(kv.Value);
                foreach (var b in kv.Value) handled.Add(b);
            }
            return handled;
        }

        private void Init(List<Button> buttons)
        {
            buttons.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));
            foreach (var b in buttons)
            {
                var stop = new Stop
                {
                    Button = b,
                    Image = b.image,
                    State = StateOf[b.image.sprite.name],
                    Transition = b.transition,
                    GameColor = b.image.color,
                };
                stop.Selected = Palette.IsGameSelected(stop.GameColor);
                var rt = (RectTransform)b.transform;
                stop.Centre = rt.localPosition + (Vector3)rt.rect.center;
                b.transition = Selectable.Transition.None;
                _stops.Add(stop);
                _byImage[stop.Image] = stop;
                ColorHooks.Registry[stop.Image] = this;
                Write(stop.Image, Color.clear);   // invisible, still the click target
                var hover = b.gameObject.AddComponent<StopHover>();
                hover.Slider = this;
            }

            // Right-aligned: the last stop and the state's name end at the row's right edge, so the
            // label (accessory names are long) gets the rest of the row.
            var rowRt = (RectTransform)_parent;
            float target = rowRt.rect.xMax - 4f - 40f - 10f;
            float shift = target - _stops[_stops.Count - 1].Centre.x;
            if (shift > 3f && shift < 120f)
            {
                foreach (var stop in _stops)
                {
                    var brt = (RectTransform)stop.Button.transform;
                    stop.OrigPos = brt.anchoredPosition;
                    brt.anchoredPosition = stop.OrigPos + new Vector2(shift, 0f);
                    stop.Centre.x += shift;
                }
                _shifted = true;
            }

            // Track from the first stop to the last, behind everything.
            var first = _stops[0].Centre;
            var last = _stops[_stops.Count - 1].Centre;
            var track = NewImage("Track", Sprites.Track, Image.Type.Sliced, Palette.White(0.22f));
            var trt = track.rectTransform;
            trt.sizeDelta = new Vector2(last.x - first.x, 20f);
            trt.localPosition = (first + last) / 2f;

            foreach (var stop in _stops)
            {
                stop.DotImage = NewImage("Stop", Sprites.Knob, Image.Type.Simple, Palette.White(0.5f));
                stop.DotImage.rectTransform.sizeDelta = new Vector2(Dot, Dot);
                stop.DotImage.rectTransform.localPosition = stop.Centre;
            }

            _ghost = NewImage("Hover", Sprites.Knob, Image.Type.Simple, Palette.White(0.35f));
            _ghost.rectTransform.sizeDelta = new Vector2(KnobSize, KnobSize);
            _knob = NewImage("Knob", Sprites.Knob, Image.Type.Simple, Palette.Accent);
            _knob.rectTransform.sizeDelta = new Vector2(KnobSize, KnobSize);

            // The state's name, to the right of the last stop if the row has room for it.
            var row = (RectTransform)_parent;
            float room = row.rect.xMax - (last.x + 10f);
            if (room >= 26f)
            {
                var go = new GameObject(ThemedButton.OwnPrefix + "State", typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(_parent, false);
                _added.Add(go);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(Mathf.Min(room, 44f), 20f);
                rt.localPosition = new Vector3(last.x + 10f, last.y, 0f);
                _word = Labels.Configure(go.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleLeft, 11, 8);
                _word.color = Palette.White(0.7f);
                _word.raycastTarget = false;
            }

            // Our graphics stay last among the siblings (they don't take clicks, so Studio's invisible
            // buttons still get them). Moving them in front would shift the children's indices, and
            // MoreAccessories finds its toggle's buttons by index (GetChild(1) / (2)).
            foreach (var go in _added)
                foreach (var g in go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            ClipLabels();
            Refresh();
        }

        /// <summary>
        /// Labels in the row that run under the slider (an accessory's long name, widened by a plugin)
        /// end just before the first stop, so the text doesn't sit under the knob.
        /// </summary>
        private void ClipLabels()
        {
            if (_stops.Count == 0 || _parent == null) return;
            var brt = (RectTransform)_stops[0].Button.transform;
            float limit = _stops[0].Centre.x - brt.rect.width / 2f - 6f;
            foreach (Transform c in _parent)
            {
                var rt = c as RectTransform;
                if (rt == null || rt == transform || _added.Contains(c.gameObject) || c.GetComponent<Selectable>() != null) continue;
                if (rt.anchorMin.x != rt.anchorMax.x) continue;
                if (c.GetComponentInChildren<Text>(true) == null && c.GetComponentInChildren<TMPro.TMP_Text>(true) == null) continue;
                float left = rt.localPosition.x + rt.rect.xMin, right = rt.localPosition.x + rt.rect.xMax;
                if (right <= limit + 0.5f || left >= limit - 20f) continue;
                if (!_clipped.ContainsKey(rt)) _clipped[rt] = new KeyValuePair<Vector2, Vector2>(rt.sizeDelta, rt.anchoredPosition);
                float w = limit - left, old = rt.rect.width;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x + (w - old), rt.sizeDelta.y);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x + rt.pivot.x * (w - old), rt.anchoredPosition.y);
            }
        }

        private Image NewImage(string name, Sprite sprite, Image.Type type, Color color)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(_parent, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            _added.Add(go);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public Color InterceptGameColor(Color value) { return value; }   // Intercept (with the image) is used instead

        /// <summary>Studio sets a stop's colour: record whether it's the selected one, show nothing.</summary>
        public Color Intercept(Graphic g, Color value)
        {
            Stop stop;
            if (_writing || _removed || !_byImage.TryGetValue(g, out stop)) return value;
            stop.GameColor = value;
            bool sel = Palette.IsGameSelected(value);
            if (sel != stop.Selected) { stop.Selected = sel; _dirty = true; }
            return Color.clear;
        }

        private void Write(Image img, Color c)
        {
            _writing = true;
            try { img.color = c; }
            finally { _writing = false; }
        }

        internal void SetHover(Button b, bool on)
        {
            var stop = _stops.Find(s => s.Button == b);
            if (on) _hover = stop;
            else if (_hover == stop) _hover = null;
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_removed) return;
            if (_stops.Count == 0) { RepairCopy(); return; }   // a plugin copied a themed row (extra accessory slots)
            if (_dirty) Refresh();
            if (Time.frameCount % 30 == 0) ClipLabels();   // a plugin may widen a label again when the names change
            foreach (var stop in _stops)
            {
                var c = Palette.White(stop.Button.interactable ? 0.5f : 0.15f);
                if (stop.DotImage.color != c) stop.DotImage.color = c;
            }
        }

        private void Refresh()
        {
            _dirty = false;
            if (_knob == null || _ghost == null) return;
            Stop current = null;
            foreach (var s in _stops) if (s.Selected) current = s;
            _knob.enabled = current != null;
            if (current != null) _knob.rectTransform.localPosition = current.Centre;
            bool ghost = _hover != null && _hover != current && _hover.Button.interactable;
            _ghost.enabled = ghost;
            if (ghost) _ghost.rectTransform.localPosition = _hover.Centre;
            if (_word != null)
            {
                var show = _hover != null && _hover.Button.interactable ? _hover : current;
                _word.text = show == null ? "" : show.State >= 10 ? Amounts[show.State - 10] : Words[show.State];
                _word.color = Palette.White(show == current ? 0.75f : 0.45f);
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var stop in _stops)
            {
                if (stop.Image != null) { ColorHooks.Remove(stop.Image, this); stop.Image.color = stop.GameColor; }
                if (stop.Button != null)
                {
                    stop.Button.transition = stop.Transition;
                    if (_shifted) ((RectTransform)stop.Button.transform).anchoredPosition = stop.OrigPos;
                    var h = stop.Button.GetComponent<StopHover>();
                    if (h != null) Destroy(h);
                }
            }
            foreach (var kv in _clipped)
                if (kv.Key != null) { kv.Key.sizeDelta = kv.Value.Key; kv.Key.anchoredPosition = kv.Value.Value; }
            _clipped.Clear();
            foreach (var go in _added) if (go != null) Destroy(go);
            Destroy(gameObject);
        }

        /// <summary>
        /// A copy another plugin made of a themed row (its slider parts came along, but not the slider's
        /// state): take the copy apart so the row's buttons get a slider of their own.
        /// </summary>
        /// <summary>A copied row found while running: clear the copy and give the row a slider of its own.</summary>
        private void RepairCopy()
        {
            var row = transform.parent;
            ClearIfCopy();
            if (row == null) return;
            var undo = new Undo();
            Build(row, undo);
            Clones.Track(row.gameObject, undo.Run);
        }

        public void ClearIfCopy()
        {
            if (_removed || _stops.Count > 0) return;
            _removed = true;
            var row = transform.parent;
            if (row != null)
                foreach (Transform t in row)
                {
                    var hover = t.GetComponent<StopHover>();
                    if (hover == null) continue;
                    DestroyImmediate(hover);
                    var b = t.GetComponent<Button>();
                    if (b != null && b.image != null && b.image.color.a <= 0f) b.image.color = Color.white;
                }
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private class StopHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public StateSlider Slider;
            public void OnPointerEnter(PointerEventData e) { if (Slider != null) Slider.SetHover(GetComponent<Button>(), true); }
            public void OnPointerExit(PointerEventData e) { if (Slider != null) Slider.SetHover(GetComponent<Button>(), false); }
        }
    }
}
