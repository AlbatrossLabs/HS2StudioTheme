using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// iPhone-style folders for the tool buttons at the bottom left (Studio's and every plugin's).
    /// Drag a button onto another to put both in a folder; drag more onto the folder to add them;
    /// click the folder to open it; drag a button out of an open folder to take it back out. Dragging
    /// a button or folder to an empty spot moves it there. The buttons fill the toolbar's own grid
    /// from its first cell, so taking buttons into folders closes up the gaps. Layout is saved in F1
    /// › Toolbar folders; turning the theme off puts every button back where Studio / KKAPI had it.
    /// </summary>
    internal class ToolbarFolders : MonoBehaviour
    {
        private class Folder
        {
            public string Id, Name;
            public readonly List<string> Members = new List<string>();
            public RectTransform Rt;
            public Image Glass;
            public readonly List<Image> Minis = new List<Image>();
        }

        private const float Cell = 40f, PanelPad = 8f, TitleH = 24f;
        private const int PanelColumns = 4;

        private readonly Dictionary<string, RectTransform> _buttons = new Dictionary<string, RectTransform>();
        private readonly Dictionary<RectTransform, Vector2> _natural = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector2> _assigned = new Dictionary<RectTransform, Vector2>();   // last position written
        private readonly Dictionary<RectTransform, Vector2> _target = new Dictionary<RectTransform, Vector2>();     // where it's gliding to
        private Vector2 _origin;
        private float _dx = Cell, _dy = Cell;
        private int _rows = 5;
        private Vector2Int _previewCell = NoCell;
        private static readonly Vector2Int NoCell = new Vector2Int(-1, -1);
        // Each top-level button or folder's cell (column, row), as the user placed it. Buttons not in it yet
        // (no layout saved, or a plugin's new button) go to their own natural cell, else the first free one.
        private readonly Dictionary<string, Vector2Int> _cellOf = new Dictionary<string, Vector2Int>();
        private List<string> _legacyOrder;   // 0.11 / 0.12 layouts: an order, turned into cells once the grid is known
        private readonly Dictionary<string, Folder> _folders = new Dictionary<string, Folder>();
        private Vector2 _size = new Vector2(32f, 32f);
        private RectTransform _rt, _panel;
        private InputField _title;
        private Folder _open;
        private RectTransform _dragging, _hoverTarget;
        private Vector2 _grab;
        private bool _removed, _layoutDirty = true;

        public static ToolbarFolders Attach(Transform group, Undo undo)
        {
            if (group == null || group.GetComponent<ToolbarFolders>() != null) return null;
            var f = group.gameObject.AddComponent<ToolbarFolders>();
            f._rt = (RectTransform)group;
            undo.Add(f.Remove);
            f.Load();
            f.ResetKkapiPositionsOnce();
            f.Scan();
            return f;
        }

        // ------------------------------------------------------------------ model

        private void Load()
        {
            _cellOf.Clear();
            _folders.Clear();
            _legacyOrder = null;
            _gridSaved = false;
            _kkapiReset = false;
            var text = StudioThemePlugin.ToolbarLayout.Value ?? "";
            foreach (var part in text.Split(';'))
            {
                if (part.StartsWith("cells="))
                {
                    foreach (var item in part.Substring(6).Split('|'))
                    {
                        int colon = item.LastIndexOf(':');
                        if (colon <= 0) continue;
                        var xy = item.Substring(colon + 1).Split(',');
                        int c, r;
                        if (xy.Length == 2 && int.TryParse(xy[0], out c) && int.TryParse(xy[1], out r) && c >= 0 && r >= 0)
                            _cellOf[item.Substring(0, colon)] = new Vector2Int(c, r);
                    }
                }
                else if (part == "kk=1") _kkapiReset = true;
                else if (part.StartsWith("grid="))
                {
                    var v = part.Substring(5).Split(',');
                    float ox, oy, dx, dy; int rows;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var fl = System.Globalization.NumberStyles.Float;
                    if (v.Length == 5 && float.TryParse(v[0], fl, inv, out ox) && float.TryParse(v[1], fl, inv, out oy) && float.TryParse(v[2], fl, inv, out dx)
                        && float.TryParse(v[3], fl, inv, out dy) && int.TryParse(v[4], out rows) && dx > 4f && dy > 4f && rows > 0)
                    {
                        _origin = new Vector2(ox, oy); _dx = dx; _dy = dy; _rows = rows;
                        _gridSaved = true; _latticeSet = true; _lattice0 = _origin;
                    }
                }
                else if (part.StartsWith("order="))
                    _legacyOrder = part.Substring(6).Split('|').Where(x => x.Length > 0).ToList();
                else if (part.StartsWith("folder:"))
                {
                    int eq = part.IndexOf('=');
                    if (eq < 0) continue;
                    var head = part.Substring(7, eq - 7).Split('~');
                    var f = new Folder { Id = head[0], Name = head.Length > 1 ? head[1] : "Folder" };
                    f.Members.AddRange(part.Substring(eq + 1).Split('|').Where(x => x.Length > 0));
                    if (f.Members.Count > 0) _folders[f.Id] = f;
                }
            }
        }

        private void Save()
        {
            var sb = new StringBuilder("cells=").Append(string.Join("|",
                _cellOf.Select(kv => kv.Key + ":" + kv.Value.x + "," + kv.Value.y).ToArray()));
            if (_kkapiReset) sb.Append(";kk=1");
            if (_gridSaved)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append(";grid=").Append(_origin.x.ToString("R", inv)).Append(',').Append(_origin.y.ToString("R", inv)).Append(',')
                  .Append(_dx.ToString("R", inv)).Append(',').Append(_dy.ToString("R", inv)).Append(',').Append(_rows);
            }
            foreach (var f in _folders.Values)
                sb.Append(";folder:").Append(f.Id).Append('~').Append(f.Name.Replace(";", "").Replace("|", "").Replace("=", "").Replace("~", ""))
                  .Append('=').Append(string.Join("|", f.Members.ToArray()));
            StudioThemePlugin.ToolbarLayout.Value = sb.ToString();
        }

        private static bool IsFolderKey(string key) { return key.StartsWith("@"); }

        private Folder FolderOf(string key)
        {
            foreach (var f in _folders.Values) if (f.Members.Contains(key)) return f;
            return null;
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>Pick up buttons as they appear (plugins add theirs late) and notice KKAPI re-placing them.</summary>
        private void Scan()
        {
            for (int i = 0; i < _rt.childCount; i++)
            {
                var t = _rt.GetChild(i) as RectTransform;
                if (t == null || t.name.StartsWith(ThemedButton.OwnPrefix) || t.GetComponent<Button>() == null) continue;
                RectTransform known;
                if (_buttons.TryGetValue(t.name, out known) && known == t) continue;
                if (known != null && known != t) continue;   // a second object with the same name: keep the first
                _buttons[t.name] = t;
                _natural[t] = t.anchoredPosition;
                _size = t.sizeDelta;
                if (t.GetComponent<ToolbarDrag>() == null) t.gameObject.AddComponent<ToolbarDrag>().Owner = this;
                MuteKkapiDrag(t);
                _latticeSet = false;
                _layoutDirty = true;
            }
            if (!_latticeSet) InitLattice();
            if (Time.frameCount % 60 == 0) foreach (var b in _buttons.Values) if (b != null) MuteKkapiDrag(b);   // KKAPI adds it in edit mode
            // Someone else moved a button (KKAPI re-lays out every button when one is added, shown or hidden):
            // where KKAPI put it is its natural cell. Put everything back in the same frame, without gliding.
            foreach (var t in _buttons.Values)
            {
                Vector2 a;
                if (t == null || t == _dragging) continue;
                if (_assigned.TryGetValue(t, out a) && (t.anchoredPosition - a).sqrMagnitude > 0.5f)
                {
                    if (OnLattice(t.anchoredPosition)) _natural[t] = t.anchoredPosition;
                    _target.Remove(t);
                    _layoutDirty = true;
                    _snapNext = true;
                }
            }
        }

        private Vector2 _lattice0;
        private bool _latticeSet, _snapNext;

        /// <summary>
        /// The toolbar's pitch and one grid point, from Studio's own buttons (their places never change).
        /// Places off this lattice (a button caught mid-move) are never taken as anyone's natural cell.
        /// </summary>
        private void InitLattice()
        {
            var own = _buttons.Where(kv => kv.Value != null && !kv.Key.EndsWith("(StudioAPI)")).Select(kv => _natural[kv.Value]).ToList();
            if (own.Count == 0) own = _natural.Values.ToList();
            if (own.Count == 0) return;
            _dx = Step(own.Select(p => p.x));
            _dy = Step(own.Select(p => p.y));
            _lattice0 = own[0];
            _latticeSet = true;
        }

        private bool OnLattice(Vector2 p)
        {
            if (!_latticeSet) return true;
            float fx = (p.x - _lattice0.x) / _dx, fy = (p.y - _lattice0.y) / _dy;
            return Mathf.Abs(fx - Mathf.Round(fx)) < 0.15f && Mathf.Abs(fy - Mathf.Round(fy)) < 0.15f;
        }

        private bool Visible(string key)
        {
            RectTransform t;
            return _buttons.TryGetValue(key, out t) && t != null && t.gameObject.activeSelf;
        }

        /// <summary>Studio swaps "Axis" and "Target" in the same spot: treat them as one slot.</summary>
        private RectTransform Resolve(string key)
        {
            RectTransform t;
            if (key == "Button Axis" && !Visible(key) && Visible("Button Target")) key = "Button Target";
            return _buttons.TryGetValue(key, out t) && t != null && t.gameObject.activeSelf ? t : null;
        }

        // ------------------------------------------------------------------ layout

        private void LateUpdate()
        {
            if (_removed) return;
            Scan();   // every frame: cheap, and puts back a KKAPI re-layout before it's ever drawn
            if (_dragging == null && (_layoutDirty || Time.frameCount % 30 == 0)) Layout();
            if (_dragging != null && _pendingCell != NoCell) UpdateDragPreview();   // the push waits for a short hold
            Glide();
            if (_open != null && Input.GetMouseButtonDown(0) && !PointerOver(_panel) && !PointerOver(_open.Rt) && _dragging == null) Close();
            if (_open != null && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>
        /// The toolbar's grid, from where Studio / KKAPI put the buttons: its top-left cell, the spacing, and
        /// how many rows a column has. The lowest row Studio uses is left out: the gizmo panel covers it.
        /// </summary>
        private void ComputeGrid()
        {
            if (_gridSaved) return;   // fixed once there's a layout: nothing KKAPI does later can shift it
            if (!_latticeSet) InitLattice();
            // Buttons KKAPI was told to put somewhere (dragged in its edit mode) don't define the grid.
            var pinned = KkapiPinned();
            var pts = _natural.Where(kv => kv.Key != null && kv.Key.gameObject.activeSelf && OnLattice(kv.Value) && !pinned.Contains(kv.Key.name))
                .Select(kv => kv.Value).ToList();
            if (pts.Count == 0) pts = _natural.Where(kv => kv.Key != null && kv.Key.gameObject.activeSelf && OnLattice(kv.Value)).Select(kv => kv.Value).ToList();
            if (pts.Count == 0) return;
            float minX = pts.Min(p => p.x), maxY = pts.Max(p => p.y), minY = pts.Min(p => p.y);
            _origin = new Vector2(minX, maxY);
            _rows = Mathf.Max(1, Mathf.RoundToInt((maxY - minY) / _dy));   // natural rows minus the bottom one
        }

        private bool _gridSaved;

        /// <summary>Names of KKAPI buttons that have a position of their own in KKAPI (not placed automatically).</summary>
        private static HashSet<string> KkapiPinned()
        {
            var names = new HashSet<string>();
            try
            {
                var manager = HarmonyLib.AccessTools.TypeByName("KKAPI.Studio.UI.Toolbars.ToolbarManager");
                var set = manager != null ? HarmonyLib.AccessTools.Field(manager, "_Buttons")?.GetValue(null) as System.Collections.IEnumerable : null;
                if (set == null) return names;
                foreach (var b in set)
                {
                    var t = HarmonyLib.Traverse.Create(b);
                    var button = t.Property("ButtonObject").GetValue<Button>();
                    var desired = t.Property("DesiredPosition").GetValue();
                    if (button != null && desired != null) names.Add(button.name);
                }
            }
            catch (System.Exception) { }
            return names;
        }

        /// <summary>
        /// KKAPI's own drag handler (its toolbar edit mode) sits on the same buttons: left on, every drag
        /// here would also move the button in KKAPI's layout. Off while the folders run, back on after.
        /// </summary>
        private void MuteKkapiDrag(RectTransform t)
        {
            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null || !mb.enabled || mb.GetType().Name != "DragHelper") continue;
                mb.enabled = false;
                _mutedDrag.Add(mb);
            }
        }

        private readonly List<MonoBehaviour> _mutedDrag = new List<MonoBehaviour>();

        private bool _kkapiReset;

        /// <summary>
        /// Before 0.13.4 every drag here also went to KKAPI's own drag handler, which saved the drop spot as
        /// that button's KKAPI position (and shifted KKAPI's whole layout). Once: let KKAPI place its
        /// buttons automatically again, so the grid is measured from its normal layout.
        /// </summary>
        private void ResetKkapiPositionsOnce()
        {
            if (_kkapiReset) return;
            _kkapiReset = true;
            try
            {
                var manager = HarmonyLib.AccessTools.TypeByName("KKAPI.Studio.UI.Toolbars.ToolbarManager");
                var set = manager != null ? HarmonyLib.AccessTools.Field(manager, "_Buttons")?.GetValue(null) as System.Collections.IEnumerable : null;
                if (set == null) return;
                int n = 0;
                foreach (var b in set)
                {
                    var t = HarmonyLib.Traverse.Create(b);
                    if (t.Property("DesiredPosition").GetValue() == null) continue;
                    t.Method("ResetDesiredPosition").GetValue();
                    n++;
                }
                if (n > 0) StudioThemePlugin.Log.LogInfo("Toolbar: let KKAPI place " + n + " button(s) automatically again");
                Save();
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogDebug("KKAPI position reset: " + e.Message); }
        }

        /// <summary>The smallest gap between distinct coordinates (the grid's pitch), 40 if there's only one.</summary>
        private static float Step(IEnumerable<float> values)
        {
            var v = values.Select(x => Mathf.Round(x)).Distinct().OrderBy(x => x).ToList();
            float best = float.MaxValue;
            for (int i = 1; i < v.Count; i++) { float d = v[i] - v[i - 1]; if (d > 4f && d < best) best = d; }
            return best == float.MaxValue ? Cell : best;
        }

        private Vector2 CellPos(Vector2Int c) { return new Vector2(_origin.x + c.x * _dx, _origin.y - c.y * _dy); }

        /// <summary>The cell a button placed at <paramref name="pos"/> (its top-left) is in.</summary>
        private Vector2Int CellAt(Vector2 pos)
        {
            return new Vector2Int(Mathf.RoundToInt((pos.x - _origin.x) / _dx), Mathf.RoundToInt((_origin.y - pos.y) / _dy));
        }

        private bool Valid(Vector2Int c) { return c.x >= 0 && c.y >= 0 && c.y < _rows; }

        /// <summary>The next cell in Studio's order: down the column, then the top of the next one.</summary>
        private Vector2Int Next(Vector2Int c) { return c.y + 1 < _rows ? new Vector2Int(c.x, c.y + 1) : new Vector2Int(c.x + 1, 0); }

        /// <summary>The cell under the pointer (toolbar-local), kept inside the grid and at most one column past the last used.</summary>
        private Vector2Int SlotAt(Vector2 p, Dictionary<string, Vector2Int> placed)
        {
            var c = CellAt(p - new Vector2(_size.x / 2f, -_size.y / 2f));   // the pointer as a button's centre
            int maxCol = placed.Count > 0 ? placed.Values.Max(v => v.x) + 1 : 0;
            return new Vector2Int(Mathf.Clamp(c.x, 0, maxCol), Mathf.Clamp(c.y, 0, _rows - 1));
        }

        /// <summary>Buttons and folders shown on the toolbar itself (not inside a folder).</summary>
        private List<string> TopKeys()
        {
            var keys = new List<string>();
            foreach (var k in _buttons.Keys)
            {
                if (k == "Button Target" && _buttons.ContainsKey("Button Axis")) continue;   // one slot for the pair
                if (FolderOf(k) == null && Resolve(k) != null) keys.Add(k);
            }
            keys = keys.OrderBy(k => Mathf.Round(_natural[_buttons[k]].x / 4f)).ThenByDescending(k => _natural[_buttons[k]].y).ToList();
            foreach (var f in _folders.Values) if (FolderVisible(f)) keys.Add("@" + f.Id);
            return keys;
        }

        /// <summary>
        /// Where everything goes: placed buttons in their cells; the rest in their natural cell if it's free,
        /// else the first free one. Cells saved for buttons that aren't showing (a plugin not loaded yet)
        /// stay free for them.
        /// </summary>
        private Dictionary<string, Vector2Int> Assign()
        {
            var keys = TopKeys();
            if (_legacyOrder != null && _rows > 0)
            {
                // An older saved order: lay it out in that order, from the first cell.
                var c = Vector2Int.zero;
                foreach (var k in _legacyOrder) { _cellOf[k] = c; c = Next(c); }
                _legacyOrder = null;
            }
            var res = new Dictionary<string, Vector2Int>();
            var taken = new HashSet<Vector2Int>();
            foreach (var kv in _cellOf)
                if (!keys.Contains(kv.Key) && FolderOf(kv.Key) == null && Valid(kv.Value)) taken.Add(kv.Value);
            foreach (var k in keys)
            {
                Vector2Int c;
                if (_cellOf.TryGetValue(k, out c) && Valid(c) && taken.Add(c)) res[k] = c;
            }
            foreach (var k in keys)
            {
                if (res.ContainsKey(k)) continue;
                var c = NoCell;
                RectTransform t;
                if (!IsFolderKey(k) && _buttons.TryGetValue(k, out t) && t != null)
                {
                    var n = CellAt(_natural[t]);
                    if (Valid(n) && !taken.Contains(n)) c = n;
                }
                if (c == NoCell) { c = Vector2Int.zero; while (taken.Contains(c)) c = Next(c); }
                taken.Add(c);
                res[k] = c;
            }
            return res;
        }

        private bool FolderVisible(Folder f) { return f.Members.Any(m => Resolve(m) != null); }

        private RectTransform RtOf(string key)
        {
            if (IsFolderKey(key)) { Folder f; return _folders.TryGetValue(key.Substring(1), out f) ? f.Rt : null; }
            return Resolve(key);
        }

        private void Layout()
        {
            _layoutDirty = false;
            bool snap = _snapNext;
            _snapNext = false;
            ComputeGrid();
            Apply(Assign(), null, snap);
            foreach (var f in _folders.Values)
            {
                if (f.Rt != null && !FolderVisible(f)) f.Rt.gameObject.SetActive(false);
                if (f == _open) continue;
                foreach (var m in f.Members) { var t = Resolve(m); if (t != null) Hide(t); }
            }
            if (_open != null) LayoutPanel();
        }

        /// <summary>Send each entry to its cell (the dragged one stays under the pointer).</summary>
        private void Apply(Dictionary<string, Vector2Int> placed, string skip, bool snap = false)
        {
            foreach (var kv in placed)
            {
                if (kv.Key == skip) continue;
                if (IsFolderKey(kv.Key))
                {
                    Folder f;
                    if (!_folders.TryGetValue(kv.Key.Substring(1), out f)) continue;
                    bool fresh = f.Rt == null;
                    EnsureFolderView(f);
                    f.Rt.sizeDelta = _size;
                    f.Rt.gameObject.SetActive(true);
                    Put(f.Rt, CellPos(kv.Value), fresh || snap);
                }
                else Put(Resolve(kv.Key), CellPos(kv.Value), snap);
            }
        }

        /// <summary>
        /// The layout with <paramref name="key"/> dropped in <paramref name="cell"/>: an empty cell just takes it;
        /// an occupied one pushes its button along (down the column, then on to the next) up to the first gap.
        /// </summary>
        private Dictionary<string, Vector2Int> WithDrop(Dictionary<string, Vector2Int> placed, string key, Vector2Int cell)
        {
            var byCell = new Dictionary<Vector2Int, string>();
            foreach (var kv in placed) if (kv.Key != key) byCell[kv.Value] = kv.Key;
            // Cells kept for buttons that aren't showing count as occupied, and get pushed along too.
            foreach (var kv in _cellOf)
                if (kv.Key != key && !placed.ContainsKey(kv.Key) && FolderOf(kv.Key) == null && Valid(kv.Value) && !byCell.ContainsKey(kv.Value))
                    byCell[kv.Value] = kv.Key;
            string cur = key, other;
            var c = cell;
            int guard = 0;
            while (byCell.TryGetValue(c, out other) && guard++ < 500) { byCell[c] = cur; cur = other; c = Next(c); }
            byCell[c] = cur;
            var res = new Dictionary<string, Vector2Int>();
            foreach (var kv in byCell) res[kv.Value] = kv.Key;
            return res;
        }

        /// <summary>Send a button (or folder) to a place: it glides there; <paramref name="snap"/> puts it there at once.</summary>
        private void Put(RectTransform t, Vector2 pos, bool snap)
        {
            if (t == null) return;
            bool wasHidden = t.localScale == Vector3.zero;
            if (t.localScale != Vector3.one && t != _hoverTarget) t.localScale = Vector3.one;
            if (snap || wasHidden || (t.anchoredPosition - pos).sqrMagnitude < 0.25f)
            {
                t.anchoredPosition = pos;
                _assigned[t] = pos;
                _target.Remove(t);
            }
            else _target[t] = pos;
        }

        /// <summary>Move gliding buttons a step towards their places.</summary>
        private void Glide()
        {
            if (_target.Count == 0) return;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f);
            foreach (var t in _target.Keys.ToList())
            {
                if (t == null || t == _dragging) { _target.Remove(t); continue; }
                var goal = _target[t];
                var pos = Vector2.Lerp(t.anchoredPosition, goal, k);
                if ((pos - goal).sqrMagnitude < 0.25f) { pos = goal; _target.Remove(t); }
                t.anchoredPosition = pos;
                _assigned[t] = pos;
            }
        }

        /// <summary>A button inside a closed folder: shrunk to nothing (no picture, no clicks), left where it is.</summary>
        private void Hide(RectTransform t)
        {
            _target.Remove(t);
            if (t.localScale != Vector3.zero) t.localScale = Vector3.zero;
            _assigned[t] = t.anchoredPosition;
        }

        // ------------------------------------------------------------------ folder view

        private void EnsureFolderView(Folder f)
        {
            if (f.Rt == null)
            {
                var go = new GameObject(ThemedButton.OwnPrefix + "Folder " + f.Id, typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(_rt, false);
                f.Rt = (RectTransform)go.transform;
                f.Rt.anchorMin = f.Rt.anchorMax = new Vector2(0f, 1f);
                f.Rt.pivot = new Vector2(0f, 1f);
                f.Glass = go.AddComponent<Image>();
                f.Glass.sprite = Sprites.Panel;
                f.Glass.type = Image.Type.Sliced;
                var button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
                var cb = button.colors; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); button.colors = cb;
                var folder = f;
                button.onClick.AddListener(() => { if (_open == folder) Close(); else Open(folder); });
                go.AddComponent<ToolbarDrag>().Owner = this;
                var tip = go.AddComponent<FolderTip>();
                tip.Text = () => folder.Name + "  ·  " + string.Join(", ", folder.Members.Select(Pretty).ToArray());
            }
            // 2 x 2 miniatures of the first four members' icons.
            foreach (var m in f.Minis) if (m != null) Destroy(m.gameObject);
            f.Minis.Clear();
            int n = 0;
            foreach (var key in f.Members)
            {
                if (n >= 4) break;
                var t = Resolve(key);
                if (t == null) continue;
                var src = IconOf(t);
                if (src == null) continue;
                var go = new GameObject(ThemedButton.OwnPrefix + "Mini", typeof(RectTransform));
                go.transform.SetParent(f.Rt, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(11f, 11f);
                rt.anchoredPosition = new Vector2(n % 2 == 0 ? -6.5f : 6.5f, n < 2 ? 6.5f : -6.5f);
                var img = go.AddComponent<Image>();
                img.sprite = src.sprite;
                img.color = src.color.a > 0.01f ? new Color(src.color.r, src.color.g, src.color.b, 1f) : Color.white;
                img.preserveAspect = true;
                img.raycastTarget = false;
                f.Minis.Add(img);
                n++;
            }
        }

        private static Image IconOf(RectTransform t)
        {
            var b = t.GetComponent<ThemedButton>();
            if (b != null && b.IconImage != null) return b.IconImage;
            var img = t.GetComponent<Image>();
            return img;
        }

        /// <summary>A readable name for a button: its tooltip if themed, else its object name.</summary>
        private string Pretty(string key)
        {
            var t = Resolve(key);
            var b = t != null ? t.GetComponent<ThemedButton>() : null;
            string tip = null;
            try { if (b != null && b.Tooltip != null) tip = b.Tooltip(); } catch (System.Exception) { }
            if (!string.IsNullOrEmpty(tip))
            {
                int cut = tip.IndexOf("  ");
                return cut > 0 ? tip.Substring(0, cut) : tip;
            }
            return key.Replace("Button ", "").Replace(" (StudioAPI)", "");
        }

        private void Open(Folder f)
        {
            Close();
            _open = f;
            if (_panel == null)
            {
                var go = new GameObject(ThemedButton.OwnPrefix + "Folder Panel", typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(_rt, false);
                _panel = (RectTransform)go.transform;
                _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 1f);
                _panel.pivot = new Vector2(0f, 1f);
                var bg = go.AddComponent<Image>();
                bg.sprite = Sprites.Panel;
                bg.type = Image.Type.Sliced;

                // The folder's name, editable in place.
                var tgo = new GameObject(ThemedButton.OwnPrefix + "Folder Name", typeof(RectTransform));
                tgo.transform.SetParent(_panel, false);
                var trt = (RectTransform)tgo.transform;
                trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.offsetMin = new Vector2(PanelPad, -TitleH - 4f); trt.offsetMax = new Vector2(-PanelPad, -4f);
                var textGo = new GameObject(ThemedButton.OwnPrefix + "Text", typeof(RectTransform));
                textGo.transform.SetParent(tgo.transform, false);
                var xrt = (RectTransform)textGo.transform;
                xrt.anchorMin = Vector2.zero; xrt.anchorMax = Vector2.one; xrt.offsetMin = xrt.offsetMax = Vector2.zero;
                var text = Labels.Configure(textGo.AddComponent<Text>(), Fonts.Semibold, TextAnchor.MiddleLeft, 12, 9);
                text.color = Palette.White(0.9f);
                text.supportRichText = false;
                tgo.AddComponent<Image>().color = Color.clear;
                _title = tgo.AddComponent<InputField>();
                _title.textComponent = text;
                _title.lineType = InputField.LineType.SingleLine;
                _title.characterLimit = 24;
                _title.onEndEdit.AddListener(v =>
                {
                    if (_open == null) return;
                    v = v.Trim();
                    if (v.Length > 0) { _open.Name = v; Save(); }
                    _title.text = _open.Name;
                });
            }
            _panel.gameObject.SetActive(true);
            _title.text = f.Name;
            _panel.SetAsLastSibling();
            foreach (var m in f.Members) { var t = Resolve(m); if (t != null) t.SetAsLastSibling(); }
            LayoutPanel();
        }

        /// <summary>The open folder's panel beside the folder, its buttons in a grid inside it.</summary>
        private void LayoutPanel()
        {
            var members = _open.Members.Select(Resolve).Where(t => t != null).ToList();
            int cols = Mathf.Clamp(members.Count, 1, PanelColumns);
            int rows = Mathf.Max(1, Mathf.CeilToInt(members.Count / (float)cols));
            var size = new Vector2(PanelPad * 2f + cols * Cell - (Cell - _size.x), TitleH + 4f + PanelPad + rows * Cell - (Cell - _size.y));
            size.x = Mathf.Max(size.x, 120f);
            // To the right of the toolbar's column the folder is in, top edge level with the folder's.
            var pos = _open.Rt.anchoredPosition + new Vector2(_size.x + 10f, size.y - _size.y > 0 ? (size.y - _size.y) / 2f : 0f);
            _panel.sizeDelta = size;
            _panel.anchoredPosition = pos;
            for (int i = 0; i < members.Count; i++)
            {
                int c = i % cols, r = i / cols;
                Put(members[i], pos + new Vector2(PanelPad + c * Cell, -(TitleH + 4f) - r * Cell), true);
            }
        }

        private void Close()
        {
            if (_open == null) return;
            if (_title != null && _title.isFocused) _title.DeactivateInputField();
            _open = null;
            if (_panel != null) _panel.gameObject.SetActive(false);
            _layoutDirty = true;
        }

        // ------------------------------------------------------------------ drag and drop

        internal void BeginDrag(RectTransform t, PointerEventData e)
        {
            _dragging = t;
            _target.Remove(t);
            t.SetAsLastSibling();
            _grab = Local(e.position) - t.anchoredPosition;
            _previewCell = NoCell;
            _pendingCell = NoCell;
            _dragScreen = e.position;
            ComputeGrid();
        }

        internal void Drag(RectTransform t, PointerEventData e)
        {
            if (_dragging != t) return;
            t.anchoredPosition = Local(e.position) - _grab;
            _assigned[t] = t.anchoredPosition;
            _dragScreen = e.position;
            UpdateDragPreview();
        }

        private Vector2 _dragScreen;
        private Vector2Int _pendingCell = NoCell;
        private float _pendingSince;
        private const float PushDelay = 0.35f;   // hold this long near an occupied cell's edge before it makes room

        /// <summary>
        /// iPhone-style: over the middle of a button or folder, that one grows (let go to put them in a
        /// folder) and nothing moves. Over an empty cell, it lands there. Near the edge of a taken cell,
        /// the buttons make room after a moment, so moving towards a button to stack doesn't push it away.
        /// Positions are judged on the layout as it was when the drag started, not on buttons sliding aside.
        /// </summary>
        private void UpdateDragPreview()
        {
            var t = _dragging;
            if (t == null) return;
            var key = KeyOf(t);
            var placed = Assign();
            var p = Local(_dragScreen);

            RectTransform target = null;
            var cell = NoCell;
            bool overOwnPanel = _open != null && PointerOver(_panel, _dragScreen) && FolderOf(key) == _open;
            if (_open != null && PointerOver(_panel, _dragScreen) && FolderOf(key) != _open && !IsFolderKey(key))
                target = _open.Rt;
            else if (!overOwnPanel)
            {
                var slot = SlotAt(p, placed);
                string occupant = null;
                foreach (var kv in placed) if (kv.Key != key && kv.Value == slot) { occupant = kv.Key; break; }
                if (occupant == null) { cell = slot; _pendingCell = NoCell; }
                else
                {
                    var c = CellPos(slot) + new Vector2(_size.x / 2f, -_size.y / 2f);
                    bool middle = Mathf.Abs(p.x - c.x) <= _dx * 0.32f && Mathf.Abs(p.y - c.y) <= _dy * 0.32f;
                    bool canMerge = !IsFolderKey(key);   // folders don't go into folders
                    if (middle && canMerge) { target = RtOf(occupant); _pendingCell = NoCell; }
                    else
                    {
                        if (_pendingCell != slot) { _pendingCell = slot; _pendingSince = Time.unscaledTime; }
                        if (Time.unscaledTime - _pendingSince >= PushDelay) cell = slot;
                    }
                }
            }

            if (target != _hoverTarget)
            {
                if (_hoverTarget != null) _hoverTarget.localScale = Vector3.one;
                _hoverTarget = target;
                if (_hoverTarget != null) _hoverTarget.localScale = new Vector3(1.15f, 1.15f, 1f);
            }
            if (cell != _previewCell)
            {
                _previewCell = cell;
                Apply(cell != NoCell ? WithDrop(placed, key, cell) : placed, key);
            }
        }

        internal void EndDrag(RectTransform t, PointerEventData e)
        {
            if (_dragging != t) return;
            _dragScreen = e.position;
            UpdateDragPreview();
            var target = _hoverTarget;
            if (_hoverTarget != null) _hoverTarget.localScale = Vector3.one;
            _hoverTarget = null;
            _dragging = null;
            try { Drop(t, e.position, target); }
            catch (System.Exception ex) { StudioThemePlugin.Log.LogWarning("Toolbar drop: " + ex.Message); }
            _previewCell = NoCell;
            _pendingCell = NoCell;
            Save();
            _layoutDirty = true;
            Layout();
        }

        private string KeyOf(RectTransform t)
        {
            foreach (var f in _folders.Values) if (f.Rt == t) return "@" + f.Id;
            if (t.name == "Button Target" && _buttons.ContainsKey("Button Axis")) return "Button Axis";
            return t.name;
        }

        private void Drop(RectTransform dragged, Vector2 screen, RectTransform target)
        {
            var key = KeyOf(dragged);
            var from = IsFolderKey(key) ? null : FolderOf(key);
            var placed = Assign();
            Freeze(placed);   // from the first change on, every cell is the user's
            string targetKey = target != null ? KeyOf(target) : null;

            if (targetKey != null && IsFolderKey(targetKey))
            {
                // Onto a folder (or its open panel): into it. Its old cell stays empty.
                var f = _folders[targetKey.Substring(1)];
                if (from != f)
                {
                    RemoveEverywhere(key);
                    f.Members.Add(key);
                }
            }
            else if (targetKey != null)
            {
                // Onto the middle of a button: a new folder holding both, in that button's cell.
                Vector2Int cell;
                bool has = _cellOf.TryGetValue(targetKey, out cell);
                RemoveEverywhere(key);
                RemoveEverywhere(targetKey);
                var f = new Folder { Id = NewId() };
                f.Members.Add(targetKey);
                f.Members.Add(key);
                f.Name = DefaultName(f);
                _folders[f.Id] = f;
                if (has) _cellOf["@" + f.Id] = cell;
            }
            else if (from != null && _open == from && PointerOver(_panel, screen))
            {
                // Let go inside its own folder: stays in it.
            }
            else
            {
                // Anywhere else: into the cell under the pointer (pushing along whatever is there).
                var cell = SlotAt(Local(screen), placed);
                var result = WithDrop(placed, key, cell);
                RemoveEverywhere(key);
                foreach (var kv in result) _cellOf[kv.Key] = kv.Value;
                if (from != null && _open == from && from.Members.Count == 0) Close();
            }
            Tidy();
        }

        /// <summary>Make the current layout the saved one (cells for buttons not showing are kept).</summary>
        private void Freeze(Dictionary<string, Vector2Int> placed)
        {
            foreach (var kv in placed) _cellOf[kv.Key] = kv.Value;
            _gridSaved = true;   // the grid as it is now is the one from here on (saved with the layout)
        }

        private void RemoveEverywhere(string key)
        {
            _cellOf.Remove(key);
            foreach (var f in _folders.Values) f.Members.Remove(key);
        }

        /// <summary>Folders with one button left dissolve into that button (in the folder's cell); empty ones go.</summary>
        private void Tidy()
        {
            foreach (var f in _folders.Values.ToList())
            {
                if (f.Members.Count >= 2) continue;
                Vector2Int cell;
                bool has = _cellOf.TryGetValue("@" + f.Id, out cell);
                _cellOf.Remove("@" + f.Id);
                if (f.Members.Count == 1 && has) _cellOf[f.Members[0]] = cell;
                if (_open == f) Close();
                if (f.Rt != null) { _target.Remove(f.Rt); _assigned.Remove(f.Rt); Destroy(f.Rt.gameObject); }
                _folders.Remove(f.Id);
            }
            foreach (var f in _folders.Values) if (f.Rt != null) EnsureFolderView(f);
        }

        private string NewId()
        {
            int i = 1;
            while (_folders.ContainsKey("f" + i)) i++;
            return "f" + i;
        }

        private static string DefaultName(Folder f)
        {
            bool plugins = f.Members.All(m => m.EndsWith("(StudioAPI)"));
            bool studio = f.Members.All(m => !m.EndsWith("(StudioAPI)"));
            return plugins ? "Plugins" : studio ? "Tools" : "Folder";
        }

        // ------------------------------------------------------------------ helpers

        private Vector2 Local(Vector2 screen)
        {
            var canvas = _rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen, cam, out p);
            return p;
        }

        private bool PointerOver(RectTransform t) { return PointerOver(t, Input.mousePosition); }

        private bool PointerOver(RectTransform t, Vector2 screen)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return false;
            var canvas = t.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(t, screen, cam);
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var kv in _natural)
            {
                if (kv.Key == null) continue;
                kv.Key.anchoredPosition = kv.Value;
                kv.Key.localScale = Vector3.one;
                var d = kv.Key.GetComponent<ToolbarDrag>();
                if (d != null) Destroy(d);
            }
            foreach (var f in _folders.Values) if (f.Rt != null) Destroy(f.Rt.gameObject);
            if (_panel != null) Destroy(_panel.gameObject);
            foreach (var mb in _mutedDrag) if (mb != null) mb.enabled = true;
            Destroy(this);
        }

        /// <summary>Drag handling on each toolbar button and folder. A drag never counts as a click.</summary>
        internal class ToolbarDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public ToolbarFolders Owner;
            public void OnBeginDrag(PointerEventData e)
            {
                if (Owner == null || e.button != PointerEventData.InputButton.Left) return;
                e.eligibleForClick = false;
                Owner.BeginDrag((RectTransform)transform, e);
            }
            public void OnDrag(PointerEventData e) { if (Owner != null) Owner.Drag((RectTransform)transform, e); }
            public void OnEndDrag(PointerEventData e) { if (Owner != null) Owner.EndDrag((RectTransform)transform, e); }
        }

        /// <summary>Tooltip for a folder: its name and what's in it.</summary>
        private class FolderTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public System.Func<string> Text;
            public void OnPointerEnter(PointerEventData e) { TooltipView.Show((RectTransform)transform, Text()); }
            public void OnPointerExit(PointerEventData e) { TooltipView.Hide(transform); }
        }
    }
}
