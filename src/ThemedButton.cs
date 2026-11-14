using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    internal enum ButtonKind
    {
        /// <summary>A row in one of the lists the main menu opens: clear until hovered or selected.</summary>
        Row,
        /// <summary>One of the four main menu buttons: icon with a label underneath.</summary>
        Tile,
        /// <summary>An ordinary push button in a settings panel: a soft filled capsule, centred label or icon.</summary>
        Push,
        /// <summary>One of the six show/hide switches at the top right: icon, tinted while its panel shows.</summary>
        ViewToggle,
        /// <summary>A camera slot's load button: numbered pill, coloured by MoarCamz's slot state.</summary>
        Slot,
        /// <summary>A camera slot's save strip: a thin tab that brightens on hover.</summary>
        Tab,
    }

    /// <summary>
    /// Restyles one of Studio's buttons in place. The button keeps its Image (still the click target
    /// and still the thing Studio colours green when selected); the Image gets a rounded sprite and
    /// the theme's colours, and a label in the system font is laid over the game's own text, which is
    /// kept (invisible) so translation plugins and other code keep working on it.
    /// <para>The state is serialised so that when another plugin copies a themed button (Instantiate),
    /// the copy knows Studio's original look and adopts itself instead of showing the template's text.</para>
    /// </summary>
    internal class ThemedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>Themed images, so the colour hook can find them.</summary>
        public static readonly Dictionary<Graphic, ThemedButton> Registry = new Dictionary<Graphic, ThemedButton>();

        internal const string OwnPrefix = "Theme ";

        [SerializeField] private bool _themed;
        [SerializeField] private ButtonKind _kind;
        [SerializeField] private Button _button;
        [SerializeField] private Image _image;

        [SerializeField] private Sprite _origSprite;
        [SerializeField] private Image.Type _origType;
        [SerializeField] private bool _origPreserveAspect;
        [SerializeField] private Selectable.Transition _origTransition;
        [SerializeField] private Color _gameColor = Color.white;

        [SerializeField] private Graphic _source;     // the game's label, hidden
        [SerializeField] private Color _sourceColor = Color.white;
        [SerializeField] private bool _sourceInPlace;  // legacy Text: restyled where it is (keeps scrolling-text effects)
        [SerializeField] private Font _sourceFont;
        [SerializeField] private Text _label;
        [SerializeField] private Image _icon;
        [SerializeField] private Sprite _themeSprite;   // what the image should show; put back if something swaps it
        [SerializeField] private int _toggleIndex = -1;

        /// <summary>Text for the hover tooltip, if any.</summary>
        [NonSerialized] public Func<string> Tooltip;
        /// <summary>Extra "on" state the game shows some other way (a playing indicator, a repeat sprite).</summary>
        [NonSerialized] public Func<bool> ActiveWhen;
        [NonSerialized] private bool _activeExtra;

        /// <summary>The picture the game last gave this button (kept while the theme draws its own).</summary>
        public Sprite GameSprite { get { return _origSprite; } }

        private bool Sel { get { return _selected || _activeExtra; } }
        [NonSerialized] private bool _toggleOn;
        [NonSerialized] private float _nextScan;
        [NonSerialized] private readonly List<KeyValuePair<Graphic, Color>> _hiddenExtras = new List<KeyValuePair<Graphic, Color>>();

        [NonSerialized] private bool _live;
        [NonSerialized] private string _lastText;
        [NonSerialized] private bool _hover, _pressed, _selected, _writing, _dirty, _removed, _lastInteractable;

        public bool Removed { get { return _removed; } }

        public static ThemedButton Attach(Button button, ButtonKind kind, string iconKey, string fixedLabel)
        {
            return Attach(button, kind, iconKey, fixedLabel, -1);
        }

        public static ThemedButton Attach(Button button, ButtonKind kind, string iconKey, string fixedLabel, int toggleIndex)
        {
            if (button == null) return null;
            var image = button.image != null ? button.image : button.GetComponent<Image>();
            if (image == null) return null;

            foreach (var existing in button.GetComponents<ThemedButton>())
            {
                if (existing._removed) continue;
                if (!existing._live && existing._themed) existing.Adopt();   // an inactive copy
                if (existing._live) return existing;
            }

            var themed = button.gameObject.AddComponent<ThemedButton>();
            themed._toggleIndex = toggleIndex;
            themed.Init(button, image, kind, iconKey, fixedLabel);
            return themed;
        }

        private void Awake()
        {
            // A copy of a themed button made by Instantiate: take over with the template's originals.
            if (_themed && !_live) Adopt();
        }

        private void Adopt()
        {
            if (_button == null || _image == null) { _themed = false; _removed = true; Destroy(this); return; }
            _live = true;
            _selected = false;           // a copy isn't selected by whoever owned the template
            _gameColor = Color.white;
            _lastText = null;
            _lastInteractable = _button.IsInteractable();
            if (_kind == ButtonKind.Slot && _label != null) _label.text = SlotNumber();
            Registry[_image] = this;
            _dirty = true;
        }

        private void Init(Button button, Image image, ButtonKind kind, string iconKey, string fixedLabel)
        {
            _themed = true;
            _live = true;
            _kind = kind;
            _button = button;
            _image = image;

            _origSprite = image.sprite;
            _origType = image.type;
            _origPreserveAspect = image.preserveAspect;
            _origTransition = button.transition;
            _gameColor = image.color;
            _selected = Palette.IsGameSelected(_gameColor);
            _lastInteractable = button.IsInteractable();

            // Hover and press are drawn by this component, not by a colour tint on top.
            button.transition = Selectable.Transition.None;
            image.canvasRenderer.SetColor(Color.white);

            _themeSprite = kind == ButtonKind.Tile ? Sprites.Tile : Sprites.Row;
            image.sprite = _themeSprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            Registry[image] = this;

            var iconSprite = string.IsNullOrEmpty(iconKey) ? null : Sprites.Icon(iconKey);

            if (kind == ButtonKind.Tile)
            {
                bool labelled = !string.IsNullOrEmpty(fixedLabel);
                if (iconSprite != null)
                {
                    _icon = MakeIcon(iconSprite,
                        labelled ? new Vector2(0.15f, 0.40f) : new Vector2(0.2f, 0.2f),
                        labelled ? new Vector2(0.85f, 0.90f) : new Vector2(0.8f, 0.8f));
                }
                else
                {
                    // No icon: keep the game's own picture so the button still says what it is.
                    _themeSprite = null;
                    image.sprite = _origSprite;
                    image.type = _origType;
                }
                if (labelled)
                {
                    _label = MakeLabel(new Vector2(0f, 0.06f), new Vector2(1f, 0.40f), Vector2.zero, Vector2.zero,
                        TextAnchor.MiddleCenter, 12, 8);
                    _label.text = fixedLabel;
                }
            }
            else if (kind == ButtonKind.ViewToggle)
            {
                if (iconSprite != null)
                {
                    _icon = MakeIcon(iconSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    ((RectTransform)_icon.transform).sizeDelta = new Vector2(18f, 18f);
                }
                _toggleOn = ToggleState();
            }
            else if (kind == ButtonKind.Slot)
            {
                _label = MakeLabel(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 13, 8);
                _label.text = SlotNumber();
            }
            else if (kind == ButtonKind.Tab)
            {
                // nothing inside: the tab is its own shape
            }
            else
            {
                _source = FindSourceLabel();
                if (_source is Text)
                {
                    // Legacy text (Studio's list nodes, with their scrolling-name effect): restyle it in place.
                    var legacy = (Text)_source;
                    _sourceInPlace = true;
                    _sourceColor = legacy.color;
                    _sourceFont = legacy.font;
                    legacy.font = Fonts.Regular;
                }
                else if (_source != null)
                {
                    _sourceColor = _source.color;
                    HideSource();
                    if (kind == ButtonKind.Row)
                        _label = MakeLabel(Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-4f, 0f),
                            TextAnchor.MiddleLeft, 13, 8);
                    else
                        _label = MakeLabel(Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f),
                            TextAnchor.MiddleCenter, 12, 8);
                    MirrorText();
                }
                else if (iconSprite != null)
                {
                    // Icon-only push button (Studio's "def" reset buttons, the sound players), icon centred and square.
                    _icon = MakeIcon(iconSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    var rt = (RectTransform)_icon.transform;
                    float h = ((RectTransform)transform).rect.height;
                    var size = h <= 24f ? Mathf.Min(h - 6f, 14f) : Mathf.Min(h * 0.45f, 22f);
                    rt.sizeDelta = new Vector2(size, size);
                }
            }

            Refresh();
        }

        // ---------------------------------------------------------------- state

        /// <summary>Studio set the button colour: note whether that means selected, return the theme's colour.</summary>
        public Color InterceptGameColor(Color value)
        {
            if (_writing || _removed) return value;
            _gameColor = value;
            bool selected = Palette.IsGameSelected(value);
            if (selected != _selected)
            {
                _selected = selected;
                _dirty = true;
            }
            return Background();
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); ShowTooltip(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; _pressed = false; Refresh(); TooltipView.Hide(transform); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { _pressed = true; Refresh(); } }
        public void OnPointerUp(PointerEventData e) { _pressed = false; Refresh(); }

        private void OnEnable() { _dirty = true; }

        private void OnDisable()
        {
            _hover = false;
            _pressed = false;
            TooltipView.Hide(transform);
        }

        private void ShowTooltip()
        {
            if (Tooltip == null) return;
            string text = null;
            try { text = Tooltip(); } catch { }
            if (!string.IsNullOrEmpty(text)) TooltipView.Show((RectTransform)transform, text);
        }

        private bool ToggleState()
        {
            try
            {
                var flags = Studio.Studio.Instance.workInfo.visibleFlags;
                return _toggleIndex >= 0 && _toggleIndex < flags.Length && flags[_toggleIndex];
            }
            catch { return true; }
        }

        /// <summary>Studio's slot objects are named 00, 01... (MoarCamz names its extra slots the same way).</summary>
        private string SlotNumber()
        {
            int n;
            var parent = transform.parent;
            if (parent != null && int.TryParse(parent.name, out n)) return (n + 1).ToString();
            return "";
        }

        /// <summary>MoarCamz puts its own number text on extra slots; the theme's label replaces it.</summary>
        private void HideExtraText()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g == _image || g.name.StartsWith(OwnPrefix) || !(g is Text || g is TMPro.TMP_Text)) continue;
                if (g.color.a <= 0f) continue;
                _hiddenExtras.Add(new KeyValuePair<Graphic, Color>(g, g.color));
                var c = g.color; c.a = 0f; g.color = c;
            }
        }

        private void LateUpdate()
        {
            if (_removed || !_live) return;

            bool interactable = _button != null && _button.IsInteractable();
            if (interactable != _lastInteractable)
            {
                _lastInteractable = interactable;
                _dirty = true;
            }

            // Something swapped the picture (Studio's on/off sprites, MoarCamz's blank slot): keep ours,
            // and remember theirs as the one to put back.
            if (_themeSprite != null && _image.sprite != _themeSprite)
            {
                _origSprite = _image.sprite;
                _image.sprite = _themeSprite;
                _image.type = Image.Type.Sliced;
            }

            if (_kind == ButtonKind.ViewToggle)
            {
                bool on = ToggleState();
                if (on != _toggleOn) { _toggleOn = on; _dirty = true; if (_hover) ShowTooltip(); }
            }

            if (_kind == ButtonKind.Slot && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                if (_label != null) _label.text = SlotNumber();
                HideExtraText();
            }

            if (ActiveWhen != null)
            {
                bool active = false;
                try { active = ActiveWhen(); } catch { }
                if (active != _activeExtra) { _activeExtra = active; _dirty = true; }
            }

            if (_source != null && !_sourceInPlace)
            {
                MirrorText();
                if (_source.color.a > 0f)
                {
                    // Something recoloured the game's label: keep that as its colour, hide it again.
                    _sourceColor = _source.color;
                    HideSource();
                }
            }

            if (_dirty) Refresh();
        }

        public void Refresh()
        {
            if (_removed || _image == null) return;
            _dirty = false;

            Write(Background());

            bool enabled = _button != null && _button.IsInteractable();
            Color fg;
            var slot = _kind == ButtonKind.Slot ? SlotStateOf(_gameColor) : SlotState.Saved;
            if (Sel) fg = Palette.OnAccent();
            else if (_kind == ButtonKind.Slot && slot == SlotState.Empty) fg = Palette.White(0.35f);
            else if (_kind == ButtonKind.ViewToggle) fg = _toggleOn ? Color.white : Palette.White(0.45f);
            else if (!enabled) fg = Palette.White(Palette.DisabledAlpha);
            else if (_kind == ButtonKind.Tile) fg = Palette.White(Palette.SubtleTextAlpha);
            else fg = Palette.White(Palette.TextAlpha);

            if (_label != null)
            {
                _label.color = fg;
                _label.font = Sel ? Fonts.Semibold : Fonts.Regular;
            }
            if (_sourceInPlace && _source != null)
            {
                var legacy = (Text)_source;
                legacy.color = fg;
                legacy.font = Sel ? Fonts.Semibold : Fonts.Regular;
            }
            if (_icon != null)
            {
                if (_kind == ButtonKind.ViewToggle) _icon.color = Palette.White(_toggleOn ? 1f : 0.45f);
                else _icon.color = Sel ? Palette.OnAccent() : Palette.White(enabled ? 0.9f : Palette.DisabledAlpha);
            }
        }

        private enum SlotState { Empty, Saved, Current, Recent }

        /// <summary>MoarCamz colours each slot every frame: grey empty, white saved, green the current view, blue last loaded.</summary>
        private static SlotState SlotStateOf(Color c)
        {
            if (Palette.IsGameSelected(c)) return SlotState.Current;
            if (c.b > 0.9f && c.r < 0.2f && c.g < 0.2f) return SlotState.Recent;
            if (Mathf.Abs(c.r - 0.5f) < 0.05f && Mathf.Abs(c.g - 0.5f) < 0.05f && Mathf.Abs(c.b - 0.5f) < 0.05f) return SlotState.Empty;
            return SlotState.Saved;
        }

        private Color Background()
        {
            bool enabled = _button != null && _button.IsInteractable();
            if (Sel) return Palette.Accent;
            float rest = _kind == ButtonKind.Push ? Palette.PushAlpha : 0f;
            if (_kind == ButtonKind.Tab) rest = 0.14f;
            if (_kind == ButtonKind.Slot)
            {
                var state = SlotStateOf(_gameColor);
                if (state == SlotState.Recent && !_hover && !_pressed) return Tinted(0.30f);
                rest = state == SlotState.Saved || state == SlotState.Recent ? 0.08f : 0f;
            }
            if (_kind == ButtonKind.ViewToggle && _toggleOn)
                return Tinted(_pressed ? 0.5f : _hover ? 0.42f : 0.32f);
            if (_pressed && enabled) return Palette.White(rest + Palette.PressAlpha);
            if (_hover && enabled) return Palette.White(rest + Palette.HoverAlpha);
            return Palette.White(enabled ? rest : rest * 0.5f);
        }

        private static Color Tinted(float alpha)
        {
            var a = Palette.Accent;
            return new Color(a.r, a.g, a.b, alpha);
        }

        private void Write(Color c)
        {
            _writing = true;
            try { _image.color = c; }
            finally { _writing = false; }
        }

        // ---------------------------------------------------------------- label

        private Graphic FindSourceLabel()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g == _image || g.name.StartsWith(OwnPrefix)) continue;
                if (g is TMPro.TMP_Text || g is Text) return g;
            }
            return null;
        }

        private void HideSource()
        {
            var c = _sourceColor;
            c.a = 0f;
            _source.color = c;
        }

        private void MirrorText()
        {
            string text = TextOf(_source);
            if (text != _lastText)
            {
                _lastText = text;
                if (_label != null) _label.text = Labels.Plain(text);
            }
        }

        internal static string TextOf(Graphic g)
        {
            var tmp = g as TMPro.TMP_Text;
            if (tmp != null) return tmp.text;
            var legacy = g as Text;
            return legacy != null ? legacy.text : null;
        }

        private GameObject NewChild(string name)
        {
            var go = new GameObject(OwnPrefix + name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            return go;
        }

        private Image MakeIcon(Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = NewChild("Icon");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var icon = go.AddComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private Text MakeLabel(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            TextAnchor align, int maxSize, int minSize)
        {
            var go = NewChild("Label");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return Labels.Configure(go.AddComponent<Text>(), Fonts.Regular, align, maxSize, minSize);
        }

        // ---------------------------------------------------------------- removal

        /// <summary>Put the button back exactly as Studio had it.</summary>
        public void Remove()
        {
            if (_removed) return;
            _removed = true;

            if (_image != null)
            {
                Registry.Remove(_image);
                _image.sprite = _origSprite;
                _image.type = _origType;
                _image.preserveAspect = _origPreserveAspect;
                _image.color = _gameColor;
            }
            if (_button != null) _button.transition = _origTransition;
            if (_source != null) _source.color = _sourceColor;
            if (_sourceInPlace && _source != null && _sourceFont != null) ((Text)_source).font = _sourceFont;
            foreach (var kv in _hiddenExtras) if (kv.Key != null) kv.Key.color = kv.Value;
            _hiddenExtras.Clear();
            TooltipView.Hide(transform);
            if (_label != null) Destroy(_label.gameObject);
            if (_icon != null) Destroy(_icon.gameObject);
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (_image != null)
            {
                ThemedButton registered;
                if (Registry.TryGetValue(_image, out registered) && registered == this) Registry.Remove(_image);
            }
        }
    }
}
