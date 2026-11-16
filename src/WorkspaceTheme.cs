using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The workspace (Studio's object tree) as an Apple-style sidebar window: one glass window with
    /// the title and an object count, outline rows (chevron, type icon, name, eye), the selection in
    /// the accent colour, line-icon toolbar buttons, thin scroll bars, and a corner grip to resize.
    /// </summary>
    internal static class WorkspaceTheme
    {
        public static void Apply(Undo undo)
        {
            WorkspaceRow.ClearCache();
            Studio.TreeNodeCtrl ctrl = null;
            try { ctrl = Studio.Studio.Instance.treeNodeCtrl; } catch (System.Exception) { }
            if (ctrl == null) ctrl = Object.FindObjectOfType<Studio.TreeNodeCtrl>();
            if (ctrl == null) { StudioThemePlugin.Log.LogWarning("Workspace not found"); return; }
            var scroll = ctrl.GetComponent<ScrollRect>();
            var bar = ctrl.transform.parent as RectTransform;   // "Image Bar": the window
            if (scroll == null || bar == null) return;

            // Each part on its own, so one failing doesn't leave the others unstyled.
            try
            {
                var window = WorkspaceWindow.Attach(bar, scroll);
                if (window != null) undo.Add(window.Remove);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the workspace window: " + e); }

            try
            {
                var plugins = PluginButtons.Create(bar);
                undo.Add(plugins.Remove);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style plugin buttons: " + e); }

            try
            {
                var rows = WorkspaceRows.Attach(scroll.content, Traverse.Create(ctrl).Field("m_ObjectNode").GetValue<GameObject>());
                if (rows != null) undo.Add(rows.Remove);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the workspace rows: " + e); }
        }
    }

    /// <summary>The window: glass, title, count, toolbar, scroll bars, resizing.</summary>
    internal class WorkspaceWindow : MonoBehaviour
    {
        private const float MinWidth = 200f, MinHeight = 260f;
        // Toolbar: square buttons in rows along the bottom, right-aligned, Delete on its own at the left.
        private const float Btn = 30f, BtnGap = 4f, Inset = 10f, HBar = 12f;

        private RectTransform _bar, _scrollRt, _vbar, _hbar, _close, _viewport, _delete;
        private Vector2 _viewMin0, _viewMax0;
        private float _defaultW;
        private GUITree.PreferredSizeFitter _barFitter;
        private float _w0, _h0, _fitterW0;
        private readonly Dictionary<RectTransform, Vector2> _origPos = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector2> _origSize = new Dictionary<RectTransform, Vector2>();
        private readonly List<RectTransform> _toolbar = new List<RectTransform>();
        // Buttons other plugins put in the toolbar (Material Editor...), by where they'd be at Studio's size.
        private readonly Dictionary<RectTransform, Vector2> _extraBase = new Dictionary<RectTransform, Vector2>();
        private readonly Undo _undo = new Undo();
        private Text _count;
        private RectTransform _countRt, _grip;
        private float _w, _h, _next;
        private bool _removed;

        public static WorkspaceWindow Attach(RectTransform bar, ScrollRect scroll)
        {
            if (bar.GetComponent<WorkspaceWindow>() != null) return null;
            var w = bar.gameObject.AddComponent<WorkspaceWindow>();
            try { w.Init(bar, scroll); }
            catch
            {
                w.Remove();   // put back whatever was changed before the failure
                throw;
            }
            return w;
        }

        private void Init(RectTransform bar, ScrollRect scroll)
        {
            _bar = bar;
            _scrollRt = (RectTransform)scroll.transform;
            _w0 = _w = _scrollRt.sizeDelta.x;
            _h0 = _h = _scrollRt.sizeDelta.y;

            // Glass behind the title bar and the body together, as one window.
            var barImage = bar.GetComponent<Image>();
            if (barImage != null) ControlStyler.Swap(barImage, barImage.sprite, barImage.type, Palette.White(0f), _undo);
            var bodyImage = scroll.GetComponent<Image>();
            if (bodyImage != null) ControlStyler.Swap(bodyImage, bodyImage.sprite, bodyImage.type, Palette.White(0f), _undo);
            var glassGo = Child(bar, "Window");
            glassGo.transform.SetAsFirstSibling();
            var glass = glassGo.AddComponent<GlassRect>();
            glass.sprite = Sprites.Panel;
            glass.padding = 0f;
            glass.corner = Palette.BackdropRadius;
            glass.around = new[] { bar, _scrollRt };

            // Title and count.
            var title = bar.Find("TextMeshPro Title");
            var titleText = title != null ? title.GetComponent<TMPro.TMP_Text>() : null;
            if (titleText != null)
            {
                var l = ThemedLabel.Attach(titleText, true, 13, 0.95f);
                if (l != null) { l.Inset(10f); _undo.Add(l.Remove); }
            }
            var countAnchor = TextAnchor.MiddleRight;
            if (titleText != null) countAnchor = (TextAnchor)((int)Labels.AnchorOf(titleText) / 3 * 3 + 2);   // title's row, right side
            var countGo = Child(bar, "Count");
            _countRt = (RectTransform)countGo.transform;
            _countRt.anchorMin = _countRt.anchorMax = new Vector2(0f, 1f);
            _countRt.pivot = new Vector2(1f, 1f);
            _countRt.sizeDelta = new Vector2(110f, 30f);
            _count = Labels.Configure(countGo.AddComponent<Text>(), Fonts.Regular, countAnchor, 11, 8);
            _count.color = Palette.White(0.5f);

            // Close and the toolbar.
            _close = bar.Find("Button Close") as RectTransform;
            Icon(_close, "close", "Close", null);
            Icon(bar.Find("Button Delete"), "trash", "Delete", Palette.Hex("FF453A"));
            Icon(bar.Find("Button Folder"), "folder_plus", "New folder", null);
            Icon(bar.Find("Button Camera"), "video_plus", "New camera", null);
            Icon(bar.Find("Button Route"), "route", "New route", null);
            Icon(bar.Find("Button Remove"), "unlink", "Unparent", null);
            Icon(bar.Find("Button Parent"), "link", "Parent", null);
            Icon(bar.Find("Button Copy"), "copy", "Copy", null);
            Icon(bar.Find("Button Duplicate"), "duplicate", "Duplicate", null);
            foreach (var n in new[] { "Button Delete", "Button Folder", "Button Camera", "Button Route", "Button Remove", "Button Parent", "Button Copy", "Button Duplicate" })
            {
                var t = bar.Find(n) as RectTransform;
                if (t != null) _toolbar.Add(t);
            }

            _delete = bar.Find("Button Delete") as RectTransform;
            _viewport = scroll.viewport;
            if (_viewport != null) { _viewMin0 = _viewport.offsetMin; _viewMax0 = _viewport.offsetMax; }

            // Thin scroll bars.
            _vbar = scroll.verticalScrollbar != null ? (RectTransform)scroll.verticalScrollbar.transform : null;
            _hbar = scroll.horizontalScrollbar != null ? (RectTransform)scroll.horizontalScrollbar.transform : null;
            Thin(scroll.verticalScrollbar, Sprites.ScrollThumb);
            Thin(scroll.horizontalScrollbar, Sprites.ScrollThumbH);

            // Remember Studio's layout for everything resizing moves.
            Remember(_scrollRt); Remember(_vbar); Remember(_hbar); Remember(_close);
            foreach (var t in _toolbar) Remember(t);
            _barFitter = bar.GetComponent<GUITree.PreferredSizeFitter>();
            if (_barFitter != null) _fitterW0 = _barFitter.preferredWidth;

            // Resize grip in the bottom-left corner: the window hangs from its top-right corner (under
            // the show/hide switches), so that corner stays put and the free one is dragged.
            var gripGo = Child(bar, "Resize Grip");
            _grip = (RectTransform)gripGo.transform;
            _grip.anchorMin = _grip.anchorMax = new Vector2(0f, 1f);
            _grip.pivot = new Vector2(0f, 0f);
            _grip.sizeDelta = new Vector2(12f, 12f);
            var gripImage = gripGo.AddComponent<Image>();
            gripImage.sprite = Sprites.ResizeGrip;
            gripImage.color = Palette.White(0.4f);
            gripGo.AddComponent<ResizeGrip>().Window = this;

            TrackPluginButtons();

            // As wide as the show/hide switches unless resized.
            _defaultW = _w0;
            Vector3 cmin, cmax;
            if (TopBar.CapsuleWorldRect(out cmin, out cmax) && _bar.lossyScale.x > 0f)
                _defaultW = Mathf.Max(MinWidth, (cmax.x - cmin.x) / _bar.lossyScale.x);
            var saved = StudioThemePlugin.WorkspaceSize.Value;
            if (saved.x > 0f && saved.y > 0f) Resize(saved.x, saved.y);
            else Resize(_defaultW, _h0);

            PlaceUnderSwitches();
        }

        /// <summary>
        /// Starting place: right edge lined up with the show/hide switches' capsule, top just under it.
        /// (The window can still be dragged anywhere by its title bar.)
        /// </summary>
        private void PlaceUnderSwitches()
        {
            Vector3 min, max;
            if (!TopBar.CapsuleWorldRect(out min, out max)) return;
            var orig = _bar.anchoredPosition;
            _undo.Add(() => { if (_bar != null) _bar.anchoredPosition = orig; });
            float gap = 8f * _bar.lossyScale.y;
            // The bar's pivot is its top-right corner, so its position is exactly that corner.
            _bar.position = new Vector3(max.x, min.y - gap, _bar.position.z);
        }

        private GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            _undo.Add(() => { if (go != null) Destroy(go); });
            return go;
        }

        private void Icon(Transform t, string icon, string tip, Color? tint)
        {
            var button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) return;
            var b = ThemedButton.Attach(button, ButtonKind.Push, icon, null);
            if (b == null) return;
            b.Tooltip = () => tip;
            b.IconTint = tint;
            _undo.Add(b.Remove);
        }

        private void Thin(Scrollbar bar, Sprite thumbSprite)
        {
            if (bar == null) return;
            var track = bar.GetComponent<Image>();
            if (track != null) ControlStyler.Swap(track, track.sprite, track.type, Palette.White(0f), _undo);
            var thumb = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
            if (thumb != null) ControlStyler.Swap(thumb, thumbSprite, Image.Type.Sliced, Palette.White(0.35f), _undo);
        }

        private void Remember(RectTransform rt)
        {
            if (rt == null || _origPos.ContainsKey(rt)) return;
            _origPos[rt] = rt.anchoredPosition;
            _origSize[rt] = rt.sizeDelta;
        }

        /// <summary>Resize to this body size (title bar width follows), keep it on screen, lay out.</summary>
        public void Resize(float w, float h)
        {
            var canvas = WorkspaceWindow.CanvasRect(_bar);
            float maxW = canvas.rect.width - 40f, maxH = canvas.rect.height - 120f;
            _w = Mathf.Clamp(w, MinWidth, Mathf.Max(MinWidth, maxW));
            if (Mathf.Abs(_w - _defaultW) < 10f) _w = _defaultW;   // snaps to the switches' width
            _h = Mathf.Clamp(h, MinHeight, Mathf.Max(MinHeight, maxH));
            Layout();
        }

        public Vector2 Size { get { return new Vector2(_w, _h); } }

        /// <summary>The top canvas's rect (Studio's canvases hang under a plain Transform, so not transform.root).</summary>
        internal static RectTransform CanvasRect(Transform t)
        {
            var c = t.GetComponentInParent<Canvas>();
            if (c != null) c = c.rootCanvas;
            return c != null ? (RectTransform)c.transform : (RectTransform)t;
        }


        public void SaveSize()
        {
            StudioThemePlugin.WorkspaceSize.Value = Mathf.Approximately(_w, _defaultW) && Mathf.Approximately(_h, _h0)
                ? Vector2.zero : new Vector2(_w, _h);
        }

        /// <summary>
        /// Lay the window out for its size: toolbar rows along the bottom (as many buttons per row as
        /// fit), the horizontal scroll bar just above them, and the list filling the rest.
        /// </summary>
        private void Layout()
        {
            float dw = _w - _w0, dh = _h - _h0;
            if (_barFitter != null) _barFitter.preferredWidth = _fitterW0 + dw;
            Set(_scrollRt, Vector2.zero, new Vector2(dw, dh));
            if (_close != null) Set(_close, new Vector2(dw, 0f), Vector2.zero);

            // Toolbar, in Studio's reading order (top row first, left to right), plugin buttons included.
            var items = new List<RectTransform>();
            foreach (var t in _toolbar) if (t != null && t != _delete && t.gameObject.activeSelf) items.Add(t);
            foreach (var kv in _extraBase) if (kv.Key != null && kv.Key.gameObject.activeSelf) items.Add(kv.Key);
            items.Sort((a, b) =>
            {
                var pa = _origPos[a]; var pb = _origPos[b];
                if (Mathf.Abs(pa.y - pb.y) > 4f) return pb.y.CompareTo(pa.y);
                return pa.x.CompareTo(pb.x);
            });
            int cols = Mathf.Max(2, Mathf.FloorToInt((_w - 2f * Inset + BtnGap) / (Btn + BtnGap)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((items.Count + 1) / (float)cols));   // + Delete
            float scrollTop = _scrollRt.anchoredPosition.y;                               // window coordinates
            float bottom = scrollTop - _h + Inset;                                        // toolbar's bottom edge
            float right = _w - Inset;
            for (int i = 0; i < items.Count; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = row < rows - 1 ? cols : items.Count - (rows - 1) * cols;
                float x = right - (inRow - col) * (Btn + BtnGap) + BtnGap;               // right-aligned rows
                float top = bottom + (rows - row) * (Btn + BtnGap) - BtnGap;
                Place(items[i], x, top);
            }
            if (_delete != null) Place(_delete, Inset, bottom + Btn);
            float toolbarTop = bottom + rows * (Btn + BtnGap) - BtnGap;

            // Horizontal scroll bar above the toolbar; the list down to it (scroll view coordinates).
            float hBottom = toolbarTop - scrollTop + 4f;
            if (_hbar != null)
            {
                _hbar.sizeDelta = new Vector2(_w - 30f, HBar);
                _hbar.anchoredPosition = new Vector2(_origPos[_hbar].x, hBottom + _hbar.pivot.y * HBar);
            }
            float listBottomInset = _h + hBottom + HBar + 2f;   // from the scroll view's bottom edge
            if (_viewport != null)
                _viewport.offsetMin = new Vector2(_viewMin0.x, listBottomInset);
            if (_vbar != null)
            {
                float top = -_viewMax0.y;   // the list's top inset (4)
                _vbar.anchoredPosition = new Vector2(_origPos[_vbar].x + dw, _origPos[_vbar].y);
                _vbar.sizeDelta = new Vector2(_origSize[_vbar].x, Mathf.Max(20f, _h - top - listBottomInset));
            }

            // Count: right-aligned, left of the close button.
            float closeX = _close != null ? _close.anchoredPosition.x : _w - 30f;
            _countRt.anchoredPosition = new Vector2(closeX - 4f, 0f);

            // Grip at the body's bottom-left corner.
            float bodyBottom = _scrollRt.anchoredPosition.y - _scrollRt.sizeDelta.y;
            _grip.anchoredPosition = new Vector2(_scrollRt.anchoredPosition.x, bodyBottom);
        }

        /// <summary>Put a toolbar button's top-left corner at (x, top) in window coordinates, Btn square.</summary>
        private void Place(RectTransform rt, float x, float top)
        {
            Remember(rt);
            rt.sizeDelta = new Vector2(Btn, Btn);
            rt.anchoredPosition = new Vector2(x + rt.pivot.x * Btn, top - (1f - rt.pivot.y) * Btn);
        }

        private void Set(RectTransform rt, Vector2 move, Vector2 grow)
        {
            rt.anchoredPosition = _origPos[rt] + move;
            rt.sizeDelta = _origSize[rt] + grow;
        }

        private void LateUpdate()
        {
            if (_removed || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            TrackPluginButtons();
            int n = 0;
            try { n = Studio.Studio.Instance.dicInfo.Count; } catch (System.Exception) { }
            var text = n == 1 ? "1 object" : n + " objects";
            if (_count.text != text) _count.text = text;
        }

        /// <summary>Plugin buttons in the toolbar move with the resize like Studio's own (right column).</summary>
        private void TrackPluginButtons()
        {
            var offset = new Vector2(_w - _w0, -(_h - _h0));
            bool added = false;
            for (int i = 0; i < _bar.childCount; i++)
            {
                var t = _bar.GetChild(i) as RectTransform;
                if (t == null || _extraBase.ContainsKey(t) || t.name.StartsWith(ThemedButton.OwnPrefix)
                    || PluginButtons.StudioWorkspaceButtons.Contains(t.name) || t.GetComponent<Button>() == null) continue;
                // Where it would be at Studio's layout (plugins place theirs next to Studio's buttons), for ordering.
                _extraBase[t] = t.anchoredPosition - offset;
                Remember(t);
                _origPos[t] = _extraBase[t];
                added = true;
            }
            if (added && _countRt != null) Layout();
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_viewport != null) { _viewport.offsetMin = _viewMin0; _viewport.offsetMax = _viewMax0; }
            if (_barFitter != null) _barFitter.preferredWidth = _fitterW0;
            foreach (var kv in _origPos) if (kv.Key != null) kv.Key.anchoredPosition = kv.Value;
            foreach (var kv in _origSize) if (kv.Key != null) kv.Key.sizeDelta = kv.Value;
            _undo.Run();
            Destroy(this);
        }
    }

    /// <summary>Drag the corner to resize the workspace. The size is remembered between sessions.</summary>
    internal class ResizeGrip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public WorkspaceWindow Window;
        private Vector2 _start, _startSize;

        public void OnBeginDrag(PointerEventData e)
        {
            _start = Local(e);
            _startSize = Window.Size;
        }

        public void OnDrag(PointerEventData e)
        {
            // Top-right corner fixed: dragging the bottom-left corner left or down makes it bigger.
            var d = Local(e) - _start;
            Window.Resize(_startSize.x - d.x, _startSize.y - d.y);
        }

        public void OnEndDrag(PointerEventData e) { Window.SaveSize(); }

        public void OnPointerEnter(PointerEventData e) { GetComponent<Image>().color = Palette.White(0.8f); TooltipView.Show((RectTransform)transform, "Drag to resize"); }
        public void OnPointerExit(PointerEventData e) { GetComponent<Image>().color = Palette.White(0.4f); TooltipView.Hide(transform); }

        private Vector2 Local(PointerEventData e)
        {
            var canvas = WorkspaceWindow.CanvasRect(transform);
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, e.position, e.pressEventCamera, out p);
            return p;
        }
    }

    /// <summary>Puts a <see cref="WorkspaceRow"/> on every tree row Studio creates.</summary>
    internal class WorkspaceRows : MonoBehaviour
    {
        private GameObject _template;
        private readonly List<WorkspaceRow> _rows = new List<WorkspaceRow>();
        private float _next;
        private int _count = -1;
        private bool _removed;

        public static WorkspaceRows Attach(RectTransform content, GameObject template)
        {
            if (content == null || content.GetComponent<WorkspaceRows>() != null) return null;
            var r = content.gameObject.AddComponent<WorkspaceRows>();
            r._template = template;
            r.Scan();
            return r;
        }

        private void LateUpdate()
        {
            if (_removed) return;
            // New rows are added as Studio creates objects; check right away when the count changes.
            if (transform.childCount == _count && Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            _count = transform.childCount;
            Scan();
        }

        private void Scan()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.gameObject == _template) continue;   // rows of collapsed folders are styled too
                var node = child.GetComponent<Studio.TreeNodeObject>();
                if (node == null) continue;
                var row = WorkspaceRow.Attach(node);
                if (row != null) _rows.Add(row);
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var r in _rows) if (r != null) r.Remove();
            _rows.Clear();
            Destroy(this);
        }
    }

    /// <summary>
    /// One tree row. Studio's select button stays the click target; behind the whole row goes a
    /// full-width rounded highlight (accent when selected, faint on hover), a type icon sits before
    /// the name, and the open/close and visibility pictures become a chevron and an eye. Studio's
    /// selection colours are intercepted and shown in the accent instead.
    /// </summary>
    internal class WorkspaceRow : MonoBehaviour, IColorHook, IPointerEnterHandler, IPointerExitHandler, IRightClick
    {
        private const float IconRoom = 16f;
        private static readonly Color SelectedSingle = new Color(91f / 255f, 164f / 255f, 82f / 255f);
        private static readonly Color SelectedMulti = new Color(94f / 255f, 139f / 255f, 100f / 255f);

        private Studio.TreeNodeObject _node;
        private RectTransform _nodeRt, _rt;
        private Button _button;
        private Image _image;
        private Text _text;
        private Image _highlight, _type;
        private Image _state, _eye;
        private bool _typeKnown;

        private Selectable.Transition _origTransition;
        private Color _gameColor;
        private Font _origFont;
        private Color _origTextColor;
        private Sprite _stateGame, _eyeGame, _stateMine, _eyeMine;
        private Color _stateColor, _eyeColor;
        private float _gameX;
        private bool _shifted, _selected, _hover, _writing, _removed;
        private readonly Vector3[] _corners = new Vector3[4];

        // The four pictures every row uses, looked up once rather than per row per frame.
        private static Sprite s_open, s_closed, s_eye, s_eyeOff;

        private static Sprite Pic(ref Sprite cache, string icon, float fraction)
        {
            if (cache == null) cache = Sprites.IconPadded(icon, fraction);
            return cache;
        }

        /// <summary>Forget the cached pictures (the theme's sprites were rebuilt).</summary>
        public static void ClearCache() { s_open = s_closed = s_eye = s_eyeOff = null; }

        public static WorkspaceRow Attach(Studio.TreeNodeObject node)
        {
            var button = node.buttonSelect;
            if (button == null) return null;
            var existing = button.GetComponent<WorkspaceRow>();
            if (existing != null)
            {
                if (existing._node != null) return null;
                // A copy of a themed row (another plugin duplicated it): clear what it carried over.
                foreach (Transform t in button.transform) if (t.name.StartsWith(ThemedButton.OwnPrefix)) Destroy(t.gameObject);
                foreach (Transform t in node.transform) if (t.name.StartsWith(ThemedButton.OwnPrefix)) Destroy(t.gameObject);
                DestroyImmediate(existing);
            }
            var row = button.gameObject.AddComponent<WorkspaceRow>();
            row.Init(node, button);
            return row;
        }

        private void Init(Studio.TreeNodeObject node, Button button)
        {
            _node = node;
            _nodeRt = (RectTransform)node.transform;
            _button = button;
            _rt = (RectTransform)button.transform;
            _image = node.imageSelect != null ? node.imageSelect : button.image;
            _text = button.GetComponentInChildren<Text>(true);

            _origTransition = button.transition;
            button.transition = Selectable.Transition.None;

            if (_image != null)
            {
                _gameColor = _image.color;
                _selected = IsSelectedColor(_gameColor);
                ColorHooks.Registry[_image] = this;
                Write(Palette.White(0f));
            }
            if (_text != null)
            {
                _origFont = _text.font;
                _origTextColor = _text.color;
            }

            // Highlight: on the row itself, behind the chevron, eye and name.
            _highlight = MakeImage("Highlight", _nodeRt);
            _highlight.transform.SetAsFirstSibling();
            var hrt = _highlight.rectTransform;
            hrt.anchorMin = hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            _highlight.sprite = Sprites.Row;
            _highlight.type = Image.Type.Sliced;

            // Type icon: in the room the name is moved right by.
            _type = MakeImage("Type", _rt);
            var trt = _type.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(1f, 0.5f);
            trt.anchoredPosition = new Vector2(-2f, 0f);
            trt.sizeDelta = new Vector2(13f, 13f);
            _type.preserveAspect = true;
            UpdateType();

            _state = node.imageState;
            if (_state != null) { _stateGame = _state.sprite; _stateColor = _state.color; }
            _eye = node.imageVisible;
            if (_eye != null) { _eyeGame = _eye.sprite; _eyeColor = _eye.color; }

            Refresh();
        }

        private Image MakeImage(string name, Transform parent)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Studio registers an object just after creating its row, so this is retried until known.</summary>
        private void UpdateType()
        {
            Studio.ObjectCtrlInfo info = null;
            try { Studio.Studio.Instance.dicInfo.TryGetValue(_node, out info); } catch (System.Exception) { }
            string icon;
            switch (info != null ? info.GetType().Name : "")
            {
                case "OCIChar": case "OCICharFemale": case "OCICharMale": icon = "t_user"; break;
                case "OCIItem": icon = "t_box"; break;
                case "OCILight": icon = "t_bulb"; break;
                case "OCIFolder": icon = "t_folder"; break;
                case "OCIRoute": icon = "t_route"; break;
                case "OCIRoutePoint": icon = "t_point"; break;
                case "OCICamera": icon = "t_camera"; break;
                default: icon = info != null ? "t_box" : null; break;
            }
            _typeKnown = info != null;
            _type.sprite = icon != null ? Sprites.Icon(icon) : null;
            _type.enabled = icon != null;
        }

        private static bool IsSelectedColor(Color c)
        {
            return Near(c, SelectedSingle) || Near(c, SelectedMulti);
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.03f && Mathf.Abs(a.g - b.g) < 0.03f && Mathf.Abs(a.b - b.b) < 0.03f;
        }

        public Color InterceptGameColor(Color value)
        {
            if (_writing || _removed) return value;
            _gameColor = value;
            bool sel = IsSelectedColor(value);
            if (sel != _selected) { _selected = sel; Refresh(); }
            return Palette.White(0f);
        }

        private void Write(Color c)
        {
            _writing = true;
            try { _image.color = c; }
            finally { _writing = false; }
        }

        /// <summary>Right-click: the object's menu (focus, show / hide, rename, its panels, duplicate, delete).</summary>
        public void OnRightClick(Vector2 screen)
        {
            if (_removed || _node == null) return;
            if (!StudioThemePlugin.CharacterMenu.Value || CharacterMenu.Instance == null) return;
            Studio.ObjectCtrlInfo info = null;
            try { Studio.Studio.Instance.dicInfo.TryGetValue(_node, out info); } catch (System.Exception) { }
            if (info == null) return;
            // Select it first, as Finder does, so the panels follow and the menu acts on what's highlighted.
            try
            {
                var ctrl = Studio.Studio.Instance.treeNodeCtrl;
                var sel = ctrl.selectNodes;
                if (sel == null || System.Array.IndexOf(sel, _node) < 0) ctrl.SelectSingle(_node, false);
            }
            catch (System.Exception) { }
            CharacterMenu.Instance.OpenFor(info, screen);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; Refresh(); }

        private void OnDisable() { if (_hover) { _hover = false; Refresh(); } }

        private void LateUpdate()
        {
            if (_removed || _node == null) return;
            if (!_typeKnown) { UpdateType(); if (_typeKnown) Refresh(); }

            // Room for the type icon: Studio places the select button; the theme moves it right.
            float x = _rt.anchoredPosition.x;
            if (!_shifted || Mathf.Abs(x - (_gameX + IconRoom)) > 0.01f)
            {
                _gameX = x;
                _rt.anchoredPosition = new Vector2(x + IconRoom, _rt.anchoredPosition.y);
                _shifted = true;
            }

            // Highlight spans the list, whatever the row's indent.
            var viewport = _nodeRt.parent != null ? _nodeRt.parent.parent as RectTransform : null;
            if (viewport != null)
            {
                viewport.GetWorldCorners(_corners);
                float left = _nodeRt.InverseTransformPoint(_corners[0]).x + 3f;
                float right = _nodeRt.InverseTransformPoint(_corners[2]).x - 3f;
                var hrt = _highlight.rectTransform;
                var pos = new Vector2(left, _rt.anchoredPosition.y);
                var size = new Vector2(Mathf.Max(20f, right - left), _rt.rect.height);
                if (hrt.anchoredPosition != pos) hrt.anchoredPosition = pos;
                if (hrt.sizeDelta != size) hrt.sizeDelta = size;
            }

            // Chevron and eye follow Studio's state (Studio swaps its own pictures; the theme keeps its own).
            if (_state != null)
            {
                bool open = _node.treeState == Studio.TreeNodeObject.TreeState.Open;
                var want = open ? Pic(ref s_open, "chevron", 0.55f) : Pic(ref s_closed, "chevron_right", 0.55f);
                if (_state.sprite != want)
                {
                    if (_state.sprite != _stateMine) _stateGame = _state.sprite;
                    _state.sprite = _stateMine = want;
                    _state.preserveAspect = true;
                }
                var c = _node.childCount == 0 ? Palette.White(0f) : _selected ? Palette.OnAccent() : Palette.White(0.6f);
                if (_state.color != c) _state.color = c;
            }
            if (_eye != null)
            {
                var want = _node.visible ? Pic(ref s_eye, "eye", 0.6f) : Pic(ref s_eyeOff, "eye_off", 0.6f);
                if (_eye.sprite != want)
                {
                    if (_eye.sprite != _eyeMine) _eyeGame = _eye.sprite;
                    _eye.sprite = _eyeMine = want;
                    _eye.preserveAspect = true;
                }
                var c = _selected ? Palette.OnAccent() : Palette.White(_node.visible ? 0.55f : 0.35f);
                if (_eye.color != c) _eye.color = c;
            }

            if (_text != null)
            {
                var want = TextColor();
                if (_text.color != want) _text.color = want;
                var font = _selected ? Fonts.Semibold : Fonts.Regular;
                if (_text.font != font) _text.font = font;
            }
        }

        private Color TextColor()
        {
            if (_selected) return Palette.OnAccent();
            return Palette.White(_node != null && !_node.visible ? 0.45f : 0.9f);
        }

        private void Refresh()
        {
            if (_removed) return;
            _highlight.color = _selected ? Palette.Accent : _hover ? Palette.White(Palette.HoverAlpha) : Palette.White(0f);
            if (_text != null)
            {
                _text.color = TextColor();
                _text.font = _selected ? Fonts.Semibold : Fonts.Regular;
            }
            if (_selected) _type.color = Palette.OnAccent();
            else
            {
                // Studio's colour for this kind of object, brightened for dark glass.
                var b = _node != null ? _node.baseColor : Color.white;
                bool grey = Mathf.Abs(b.r - b.g) < 0.06f && Mathf.Abs(b.g - b.b) < 0.06f;
                _type.color = grey ? Palette.White(0.75f) : Color.Lerp(new Color(b.r, b.g, b.b, 1f), Color.white, 0.35f);
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_image != null) { ColorHooks.Remove(_image, this); _image.color = _gameColor; }
            if (_button != null) _button.transition = _origTransition;
            if (_text != null) { _text.font = _origFont; _text.color = _origTextColor; }
            if (_shifted && _rt != null) _rt.anchoredPosition = new Vector2(_gameX, _rt.anchoredPosition.y);
            if (_state != null) { _state.sprite = _stateGame; _state.color = _stateColor; }
            if (_eye != null) { _eye.sprite = _eyeGame; _eye.color = _eyeColor; }
            if (_highlight != null) Destroy(_highlight.gameObject);
            if (_type != null) Destroy(_type.gameObject);
            Destroy(this);
        }
    }
}
