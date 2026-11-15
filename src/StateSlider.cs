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
        }

        private readonly List<Stop> _stops = new List<Stop>();
        private readonly Dictionary<Graphic, Stop> _byImage = new Dictionary<Graphic, Stop>();
        private readonly List<GameObject> _added = new List<GameObject>();
        private Image _knob, _ghost;
        private Text _word;
        private bool _writing, _removed, _dirty = true;
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

            // Behind Studio's (invisible) buttons, so they still get the clicks.
            int index = _stops[0].Button.transform.GetSiblingIndex();
            for (int i = _added.Count - 1; i >= 0; i--) _added[i].transform.SetSiblingIndex(index);
            Refresh();
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
            if (_dirty) Refresh();
            foreach (var stop in _stops)
            {
                var c = Palette.White(stop.Button.interactable ? 0.5f : 0.15f);
                if (stop.DotImage.color != c) stop.DotImage.color = c;
            }
        }

        private void Refresh()
        {
            _dirty = false;
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
                    var h = stop.Button.GetComponent<StopHover>();
                    if (h != null) Destroy(h);
                }
            }
            foreach (var go in _added) if (go != null) Destroy(go);
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
