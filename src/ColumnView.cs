using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// A chain of Studio's lists (Anim > Animations: group → category → animation; Add > Item:
    /// group → category → item) as a Finder-style column view: narrower columns with a title each,
    /// chevrons on rows that open another column, the path you took shown grey in earlier columns
    /// (only the current pick is accent), and a breadcrumb under the columns. Studio's own lists,
    /// rows and clicks are unchanged.
    /// </summary>
    internal class ColumnView : MonoBehaviour
    {
        private const float Gap = 6f, Inset = 6f, BarWidth = 8f;
        private string[] Panels;
        private string[] Titles;
        private float _x, _bottom, _right;

        private readonly List<ThemedPanel> _chain = new List<ThemedPanel>();
        private readonly List<GameObject> _added = new List<GameObject>();
        private readonly Undo _undo = new Undo();
        private GameObject _crumbGo;
        private Text _crumb;
        private bool _removed;

        /// <param name="container">The object holding the column panels.</param>
        /// <param name="rootList">The list that opens the chain (its pick turns grey once you're in a column).</param>
        public static ColumnView Attach(Transform container, ThemedPanel rootList, string[] panels, string[] titles)
        {
            if (container == null || container.GetComponent<ColumnView>() != null) return null;
            var c = container.gameObject.AddComponent<ColumnView>();
            c.Panels = panels;
            c.Titles = titles;
            c.Init(rootList);
            return c;
        }

        private void Init(ThemedPanel rootList)
        {
            _chain.Add(rootList);

            for (int i = 0; i < Panels.Length; i++)
            {
                var t = transform.Find(Panels[i]) as RectTransform;
                if (t == null) continue;
                if (i == 0) _x = t.anchoredPosition.x;
                Narrow(t);
                AddTitle(t, Titles[i]);
                var panel = t.GetComponent<ThemedPanel>();
                if (panel != null)
                {
                    if (i < Panels.Length - 1) panel.ChevronRows = true;
                    _chain.Add(panel);
                }
            }
            AddBreadcrumb();
        }

        /// <summary>
        /// Column width = 6 + Studio's row area + an 8-wide scroll bar, columns 6 apart, left to right
        /// from where the first column was.
        /// </summary>
        private void Narrow(RectTransform column)
        {
            var size = column.sizeDelta;
            var pos = column.anchoredPosition;
            var viewport = column.Find("Viewport") as RectTransform;
            float rows = size.x - 30f;
            bool fixedViewport = viewport != null && viewport.anchorMin == viewport.anchorMax;
            if (viewport != null) rows = fixedViewport ? viewport.sizeDelta.x : size.x + viewport.sizeDelta.x;
            float width = Inset + rows + BarWidth;

            column.sizeDelta = new Vector2(width, size.y);
            column.anchoredPosition = new Vector2(_x, pos.y);
            _x += width + Gap;
            _right = _x - Gap;
            _bottom = Mathf.Max(_bottom, size.y);
            _undo.Add(() => { if (column != null) { column.sizeDelta = size; column.anchoredPosition = pos; } });

            if (viewport != null)
            {
                var vpos = viewport.anchoredPosition;
                var vmin = viewport.offsetMin;
                var vmax = viewport.offsetMax;
                if (fixedViewport) viewport.anchoredPosition = new Vector2(Inset, vpos.y);
                else
                {
                    viewport.offsetMin = new Vector2(Inset, vmin.y);
                    viewport.offsetMax = new Vector2(-BarWidth, vmax.y);
                }
                _undo.Add(() =>
                {
                    if (viewport == null) return;
                    if (fixedViewport) viewport.anchoredPosition = vpos;
                    else { viewport.offsetMin = vmin; viewport.offsetMax = vmax; }
                });
            }

            var scroll = column.GetComponent<ScrollRect>();
            var bar = scroll != null ? scroll.verticalScrollbar : null;
            if (bar != null)
            {
                var brt = (RectTransform)bar.transform;
                var bsize = brt.sizeDelta;
                brt.sizeDelta = new Vector2(BarWidth, bsize.y);
                _undo.Add(() => { if (brt != null) brt.sizeDelta = bsize; });
                var thumb = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
                if (thumb != null)
                {
                    var tsprite = thumb.sprite;
                    thumb.sprite = Sprites.ScrollThumbNarrow;
                    _undo.Add(() => { if (thumb != null) thumb.sprite = tsprite; });
                }
            }
        }

        private void AddTitle(RectTransform column, string title)
        {
            var go = NewChild(column, "Column Title");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(Inset + 4f, -3f);
            rt.sizeDelta = new Vector2(column.sizeDelta.x - 2f * Inset - 4f, 15f);
            var text = Labels.Configure(go.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleLeft, 10, 8);
            text.text = title;
            text.color = Palette.White(0.55f);
        }

        /// <summary>The breadcrumb bar under the columns (other notices line up under it).</summary>
        internal RectTransform Breadcrumb { get { return _crumbGo != null ? (RectTransform)_crumbGo.transform : null; } }

        private void AddBreadcrumb()
        {
            _crumbGo = NewChild(transform, "Breadcrumb");
            var rt = (RectTransform)_crumbGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var first = transform.Find(Panels[0]) as RectTransform;
            float left = first != null ? first.anchoredPosition.x : 0f;
            float top = first != null ? first.anchoredPosition.y : 0f;
            rt.anchoredPosition = new Vector2(left, top - _bottom - 6f);
            rt.sizeDelta = new Vector2(Mathf.Max(100f, _right - left), 22f);
            var bg = _crumbGo.AddComponent<Image>();
            bg.sprite = Sprites.Panel;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            var textGo = NewChild(_crumbGo.transform, "Breadcrumb Text");
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 0f);
            trt.offsetMax = new Vector2(-10f, 0f);
            _crumb = Labels.Configure(textGo.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleLeft, 11, 8);
            _crumb.color = Palette.White(0.7f);
            _crumbGo.SetActive(false);
        }

        private GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            _added.Add(go);
            return go;
        }

        private readonly StringBuilder _sb = new StringBuilder();

        private bool _reported;

        /// <summary>A copy made by another plugin (Instantiate) has none of the setup: drop it.</summary>
        private void Start() { if (Panels == null) Destroy(this); }

        private void LateUpdate()
        {
            if (_removed) return;
            try { UpdateColumns(); }
            catch (System.Exception e)
            {
                // Log once with the details rather than every frame.
                if (!_reported) { _reported = true; StudioThemePlugin.Log.LogWarning("Column view (" + name + "): " + e); }
            }
        }

        private void UpdateColumns()
        {

            // The deepest open column with a selection keeps the accent; earlier ones go grey.
            int deepest = -1;
            for (int i = 0; i < _chain.Count; i++)
                if (Selected(_chain[i]) != null) deepest = i;

            _sb.Length = 0;
            int parts = 0;
            for (int i = 0; i < _chain.Count; i++)
            {
                var panel = _chain[i];
                if (panel == null) continue;
                bool open = panel.isActiveAndEnabled;
                if (panel.Rows != null)
                    foreach (var row in panel.Rows)
                        if (row != null) row.SetMuted(open && i < deepest);

                var pick = Selected(panel);
                if (pick != null)
                {
                    if (parts++ > 0) _sb.Append("  ›  ");
                    _sb.Append(pick.LabelText);
                }
            }

            if (_crumbGo == null || _crumb == null) return;   // removed by someone else
            bool show = parts > 1;
            if (_crumbGo.activeSelf != show) _crumbGo.SetActive(show);
            if (show && _crumb.text != _sb.ToString()) _crumb.text = _sb.ToString();
        }

        private static ThemedButton Selected(ThemedPanel panel)
        {
            if (panel == null || !panel.isActiveAndEnabled) return null;
            var rows = panel.Rows;
            if (rows == null) return null;
            foreach (var row in rows)
                if (row != null && row.isActiveAndEnabled && row.IsSelected) return row;
            return null;
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            _undo.Run();
            foreach (var go in _added) if (go != null) Destroy(go);
            _added.Clear();
            foreach (var p in _chain)
                if (p != null) foreach (var row in p.Rows) if (row != null) row.SetMuted(false);
            Destroy(this);
        }
    }
}
