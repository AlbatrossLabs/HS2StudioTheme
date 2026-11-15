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
        /// <summary>A floating tool button (plugins' left toolbar): a small glass square with a line icon.</summary>
        Tool,
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
    internal class ThemedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IColorHook
    {
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
        [SerializeField] private bool _baked;          // label/icon come from Studio's picture (BakedArt); follow its swaps

        /// <summary>Text for the hover tooltip, if any.</summary>
        [NonSerialized] public Func<string> Tooltip;
        /// <summary>Extra "on" state the game shows some other way (a playing indicator, a repeat sprite).</summary>
        [NonSerialized] public Func<bool> ActiveWhen;
        /// <summary>Colour for the icon when not selected (Delete's red); null = the usual white.</summary>
        [NonSerialized] public Color? IconTint;
        /// <summary>Colour for the label when not selected (HSUS's red Reset Transform); null = the usual.</summary>
        [NonSerialized] public Color? LabelTint;

        /// <summary>The icon image, if the button has one (toolbar folders show these in miniature).</summary>
        public Image IconImage { get { return _icon; } }
        [NonSerialized] private bool _activeExtra;

        /// <summary>The picture the game last gave this button (kept while the theme draws its own).</summary>
        public Sprite GameSprite { get { return _origSprite; } }

        private bool Sel { get { return _selected || _activeExtra; } }

        /// <summary>Selected (by Studio's green or an extra state).</summary>
        public bool IsSelected { get { return Sel; } }

        /// <summary>What the button currently says.</summary>
        public string LabelText
        {
            get
            {
                if (_label != null) return Labels.FullText(_label);
                if (_sourceInPlace && _source != null) return Labels.Plain(((Text)_source).text);
                return "";
            }
        }

        [NonSerialized] private bool _muted;
        [NonSerialized] private Image _chevron;

        /// <summary>
        /// Finder-style: a selection in a column to the left of the one you're working in shows grey
        /// instead of the accent colour.
        /// </summary>
        public void SetMuted(bool muted)
        {
            if (muted == _muted) return;
            _muted = muted;
            _dirty = true;
        }

        /// <summary>A chevron at the right end: this row opens another column.</summary>
        public void ShowChevron()
        {
            if (_chevron != null || _removed) return;
            var go = NewChild("Chevron");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-4f, 0f);
            rt.sizeDelta = new Vector2(10f, 10f);
            _chevron = go.AddComponent<Image>();
            _chevron.sprite = Sprites.Icon("chevron_right");
            _chevron.preserveAspect = true;
            _chevron.raycastTarget = false;
            if (_label != null)
            {
                var lrt = Labels.Frame(_label);
                lrt.offsetMax = new Vector2(-16f, lrt.offsetMax.y);
            }
            _dirty = true;
        }
        [NonSerialized] private bool _toggleOn;
        [NonSerialized] private float _nextScan;
        [NonSerialized] private readonly List<KeyValuePair<Graphic, Color>> _hiddenExtras = new List<KeyValuePair<Graphic, Color>>();

        [NonSerialized] private bool _live;
        [NonSerialized] private string _lastText;
        [NonSerialized] private bool _hover, _pressed, _selected, _writing, _dirty, _removed, _lastInteractable, _gameYellow;

        public bool Removed { get { return _removed; } }

        public static ThemedButton Attach(Button button, ButtonKind kind, string iconKey, string fixedLabel)
        {
            return Attach(button, kind, iconKey, fixedLabel, -1);
        }

        [NonSerialized] private static bool s_iconOnly;
        [NonSerialized] private static Sprite s_iconSprite;
        [NonSerialized] private bool _iconKeepColor;

        /// <summary>A plugin's own picture as the icon, in its own colours (plugins the theme has no icon for).</summary>
        public static ThemedButton AttachPicture(Button button, ButtonKind kind, Sprite picture)
        {
            s_iconOnly = true;
            s_iconSprite = picture;
            try { return Attach(button, kind, null, null, -1); }
            finally { s_iconOnly = false; s_iconSprite = null; }
        }

        /// <summary>Icon only, ignoring any text in the button (MoarCamz's "+" / "-": the caller hides it).</summary>
        public static ThemedButton AttachIcon(Button button, ButtonKind kind, string iconKey)
        {
            s_iconOnly = true;
            try { return Attach(button, kind, iconKey, null, -1); }
            finally { s_iconOnly = false; }
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
            if (_baked) FollowBaked(_origSprite);
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
            ColorHooks.Registry[_image] = this;
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

            _themeSprite = kind == ButtonKind.Tile ? Sprites.Tile : kind == ButtonKind.Tool ? Sprites.Panel : Sprites.Row;
            image.sprite = _themeSprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.preserveAspect = false;
            ColorHooks.Registry[image] = this;

            var iconSprite = s_iconSprite != null ? s_iconSprite : string.IsNullOrEmpty(iconKey) ? null : Sprites.Icon(iconKey);
            _iconKeepColor = s_iconSprite != null;

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
                _source = kind == ButtonKind.Tool || s_iconOnly ? null : FindSourceLabel();
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
                else if (!string.IsNullOrEmpty(fixedLabel))
                {
                    // Studio's text is painted into the picture: write it out instead.
                    _label = MakeLabel(Vector2.zero, Vector2.one, new Vector2(3f, 0f), new Vector2(-3f, 0f),
                        TextAnchor.MiddleCenter, 12, 7);
                    _label.text = fixedLabel;
                }
                else if (iconSprite != null)
                {
                    // Icon-only push button (Studio's "def" reset buttons, the sound players), icon centred and square.
                    _icon = MakeIcon(iconSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                    var rt = (RectTransform)_icon.transform;
                    float h = ((RectTransform)transform).rect.height;
                    var size = kind == ButtonKind.Tool ? h * (s_iconSprite != null ? 0.8f : 0.56f) : h <= 24f ? Mathf.Min(h - 6f, 14f) : Mathf.Min(h * 0.45f, 22f);
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
            bool yellow = value.r > 0.9f && value.g > 0.9f && value.b < 0.2f;   // Timeline: "this scene has Timeline data"
            if (yellow != _gameYellow) { _gameYellow = yellow; _dirty = true; }
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

        /// <summary>Button whose label or icon Studio paints into its picture: keep following the picture.</summary>
        public void MarkBaked()
        {
            _baked = true;
            FollowBaked(_origSprite);
        }

        private string _bakedTip;

        private void FollowBaked(Sprite sprite)
        {
            BakedArt.Button b;
            if (!BakedArt.TryButton(sprite, out b)) return;
            if (b.Label != null && _label != null) _label.text = b.Label;
            if (b.Icon != null && _icon != null) _icon.sprite = Sprites.Icon(b.Icon);
            _bakedTip = b.Tooltip;
            if (Tooltip == null && b.Tooltip != null) Tooltip = () => _bakedTip;
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
                if (_baked) FollowBaked(_origSprite);
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
            if (Sel && _muted) fg = Color.white;
            else if (Sel) fg = Palette.OnAccent();
            else if (_kind == ButtonKind.Slot && slot == SlotState.Empty) fg = Palette.White(0.35f);
            else if (_kind == ButtonKind.ViewToggle) fg = _toggleOn ? Color.white : Palette.White(0.45f);
            else if (!enabled) fg = Palette.White(Palette.DisabledAlpha);
            else if (_kind == ButtonKind.Tile) fg = Palette.White(Palette.SubtleTextAlpha);
            else fg = Palette.White(Palette.TextAlpha);
            if (!Sel && enabled && LabelTint.HasValue) fg = LabelTint.Value;

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
            if (_chevron != null)
                _chevron.color = Sel && !_muted ? Palette.OnAccent() : Palette.White(Sel ? 0.9f : 0.45f);
            if (_icon != null)
            {
                if (_kind == ButtonKind.ViewToggle) _icon.color = Palette.White(_toggleOn ? 1f : 0.45f);
                else if (_iconKeepColor) _icon.color = new Color(1f, 1f, 1f, enabled ? 1f : Palette.DisabledAlpha);
                else if (!Sel && _gameYellow && enabled) _icon.color = Palette.Hex("FFD60A");
                else if (!Sel && IconTint.HasValue && enabled) _icon.color = IconTint.Value;
                else _icon.color = Sel ? Palette.OnAccent() : Palette.White(enabled ? 0.9f : Palette.DisabledAlpha);
            }
        }

        private enum SlotState { Empty, Saved, Current, Recent }

        /// <summary>A camera slot with nothing saved in it (MoarCamz greys those).</summary>
        public bool SlotEmpty { get { return _kind == ButtonKind.Slot && SlotStateOf(_gameColor) == SlotState.Empty; } }

        /// <summary>MoarCamz colours each slot every frame: grey empty, white saved, green the current view, blue last loaded.</summary>
        private static SlotState SlotStateOf(Color c)
        {
            if (Palette.IsGameSelected(c)) return SlotState.Current;
            if (c.b > 0.9f && c.r < 0.2f && c.g < 0.2f) return SlotState.Recent;
            if (Mathf.Abs(c.r - 0.5f) < 0.05f && Mathf.Abs(c.g - 0.5f) < 0.05f && Mathf.Abs(c.b - 0.5f) < 0.05f) return SlotState.Empty;
            return SlotState.Saved;
        }

        /// <summary>
        /// Some of Studio's list rows are also Masks (to clip their scrolling names). Unity's mask
        /// drops every pixel of a fully transparent graphic, which would hide the row's text, so a
        /// masked row is never fully transparent (2/255 is invisible but still masks).
        /// </summary>
        private Color Background()
        {
            var c = BackgroundRaw();
            if (c.a < 0.008f && GetComponent<Mask>() != null) c.a = 0.008f;
            return c;
        }

        private Color BackgroundRaw()
        {
            bool enabled = _button != null && _button.IsInteractable();
            if (Sel) return _muted ? Palette.White(0.16f) : Palette.Accent;
            if (_kind == ButtonKind.Tool)
            {
                // Glass (the sprite carries the tint): a shade darker at rest, full on hover, darker pressed.
                float v = _pressed && enabled ? 0.75f : _hover && enabled ? 1f : 0.88f;
                return new Color(v, v, v, 1f);
            }
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
            var t = Labels.Configure(go.AddComponent<Text>(), Fonts.Regular, align, maxSize, minSize);
            // Rows and push buttons keep their text size and scroll long text instead of shrinking it.
            if (_kind == ButtonKind.Row || _kind == ButtonKind.Push && ((RectTransform)transform).rect.width >= 48f) Marquee.Wrap(t);
            return t;
        }

        // ---------------------------------------------------------------- removal

        /// <summary>Put the button back exactly as Studio had it.</summary>
        public void Remove()
        {
            if (_removed) return;
            _removed = true;

            if (_image != null)
            {
                ColorHooks.Remove(_image, this);
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
            if (_label != null) Destroy(Labels.Frame(_label).gameObject);
            if (_icon != null) Destroy(_icon.gameObject);
            if (_chevron != null) Destroy(_chevron.gameObject);
            Destroy(this);
        }

        private void OnDestroy()
        {
            ColorHooks.Remove(_image, this);
        }
    }
}
