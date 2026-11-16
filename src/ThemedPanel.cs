using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// One of the scrolling lists the main menu opens: glass background, a thin scroll thumb, and
    /// every button in the list restyled as a row. Rows other plugins add later (Search..., extra
    /// tabs) are picked up when the list opens and while it's open.
    /// </summary>
    internal class ThemedPanel : MonoBehaviour
    {
        private ScrollRect _scroll;
        private Image _bg;
        private Sprite _bgSprite;
        private Image.Type _bgType;
        private Color _bgColor;

        private Image _track;
        private Color _trackColor;
        private Image _thumb;
        private Sprite _thumbSprite;
        private Image.Type _thumbType;
        private Color _thumbColor;

        private readonly List<ThemedButton> _rows = new List<ThemedButton>();

        private bool _chevronRows;

        public List<ThemedButton> Rows { get { return _rows; } }

        /// <summary>Rows in this list open another column: give them chevrons (now and as they appear).</summary>
        public bool ChevronRows
        {
            get { return _chevronRows; }
            set
            {
                _chevronRows = value;
                if (value) foreach (var r in _rows) if (r != null) r.ShowChevron();
            }
        }

        public ScrollRect Scroll { get { return _scroll; } }

        private GlassRect _fitTo;
        private Vector2 _origSize, _origBarSize;
        private RectTransform _barRt;
        private int _lastCount = -1;
        private float _nextScan;
        private bool _removed;

        public static ThemedPanel Attach(ScrollRect scroll)
        {
            if (scroll == null) return null;
            foreach (var existing in scroll.GetComponents<ThemedPanel>())
                if (!existing._removed) return null;
            var panel = scroll.gameObject.AddComponent<ThemedPanel>();
            panel.Init(scroll);
            return panel;
        }

        private void Init(ScrollRect scroll)
        {
            _scroll = scroll;

            _bg = GetComponent<Image>();
            if (_bg != null)
            {
                _bgSprite = _bg.sprite;
                _bgType = _bg.type;
                _bgColor = _bg.color;
                _bg.sprite = Sprites.Panel;
                _bg.type = Image.Type.Sliced;
                _bg.fillCenter = true;
                _bg.color = Color.white;
            }

            var bar = scroll.verticalScrollbar;
            if (bar != null)
            {
                _track = bar.GetComponent<Image>();
                if (_track != null)
                {
                    // Still there for dragging; just not drawn.
                    _trackColor = _track.color;
                    _track.color = Palette.White(0f);
                }
                _thumb = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
                if (_thumb != null)
                {
                    _thumbSprite = _thumb.sprite;
                    _thumbType = _thumb.type;
                    _thumbColor = _thumb.color;
                    _thumb.sprite = Sprites.ScrollThumb;
                    _thumb.type = Image.Type.Sliced;
                    _thumb.color = Palette.White(0.35f);
                }
            }

            Scan();
        }

        private void OnEnable()
        {
            if (!_removed) Scan();
        }

        /// <summary>
        /// Make the list end where the main menu column's glass ends. The top stays put (the panels
        /// a list opens line up with it); the scroll bar keeps Studio's 20-unit margins.
        /// </summary>
        public void FitBottomTo(GlassRect column)
        {
            _fitTo = column;
            var rt = (RectTransform)transform;
            _origSize = rt.sizeDelta;
            var bar = _scroll.verticalScrollbar;
            _barRt = bar != null ? (RectTransform)bar.transform : null;
            if (_barRt != null) { _origBarSize = _barRt.sizeDelta; _origBarPos = _barRt.anchoredPosition; }

            // Studio's lists leave 20 units above the first row and 25 under the last (room for its old
            // picture's frame and title band); on glass 10 each is plenty, and every row of System fits.
            _viewport = _scroll.viewport;
            if (_viewport != null && _viewport.anchorMin == Vector2.zero && _viewport.anchorMax == Vector2.one)
            {
                _origViewMin = _viewport.offsetMin;
                _origViewMax = _viewport.offsetMax;
                _viewport.offsetMin = new Vector2(_origViewMin.x, Margin);
                _viewport.offsetMax = new Vector2(_origViewMax.x, -Margin);
                _viewportFitted = true;
            }
            if (_barRt != null && _barRt.anchorMin.y == 1f && _barRt.anchorMax.y == 1f)
            {
                _barRt.anchoredPosition = new Vector2(_origBarPos.x, -Margin);
                _barFitted = true;
            }
        }

        private const float Margin = 10f;
        private RectTransform _viewport;
        private Vector2 _origViewMin, _origViewMax, _origBarPos;
        private bool _viewportFitted, _barFitted;

        private void LateUpdate()
        {
            if (_removed || _fitTo == null) return;
            var rt = (RectTransform)transform;
            // Only for Studio's fixed-size lists (anchors together, pivot at the top).
            if (rt.anchorMin.y != rt.anchorMax.y || rt.pivot.y != 1f) return;

            float bottomLocal = rt.InverseTransformPoint(new Vector3(0f, _fitTo.WorldBottom(), 0f)).y;
            float height = Mathf.Max(120f, -bottomLocal);
            if (Mathf.Abs(rt.sizeDelta.y - height) > 0.5f)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
                if (_barRt != null)
                    _barRt.sizeDelta = new Vector2(_barRt.sizeDelta.x, _barFitted ? height - 2f * Margin : height - (_origSize.y - _origBarSize.y));
            }
        }

        private void Update()
        {
            if (_removed || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            var content = _scroll != null ? _scroll.content : null;
            if (content != null && content.childCount != _lastCount) Scan();
        }

        /// <summary>Theme any button directly in the list that isn't themed yet.</summary>
        private void Scan()
        {
            var content = _scroll != null ? _scroll.content : null;
            if (content == null) return;
            _lastCount = content.childCount;
            for (int i = 0; i < content.childCount; i++)
            {
                var button = content.GetChild(i).GetComponent<Button>();
                if (button == null) continue;
                var row = ThemedButton.Attach(button, ButtonKind.Row, null, null);
                if (row != null && !_rows.Contains(row)) _rows.Add(row);
                if (row != null && _chevronRows) row.ShowChevron();
            }
        }

        public void Refresh()
        {
            foreach (var row in _rows) if (row != null) row.Refresh();
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var row in _rows) if (row != null) row.Remove();
            _rows.Clear();

            if (_fitTo != null)
            {
                ((RectTransform)transform).sizeDelta = _origSize;
                if (_barRt != null) { _barRt.sizeDelta = _origBarSize; _barRt.anchoredPosition = _origBarPos; }
                if (_viewportFitted && _viewport != null) { _viewport.offsetMin = _origViewMin; _viewport.offsetMax = _origViewMax; }
            }

            if (_bg != null)
            {
                _bg.sprite = _bgSprite;
                _bg.type = _bgType;
                _bg.color = _bgColor;
            }
            if (_track != null) _track.color = _trackColor;
            if (_thumb != null)
            {
                _thumb.sprite = _thumbSprite;
                _thumb.type = _thumbType;
                _thumb.color = _thumbColor;
            }
            Destroy(this);
        }
    }
}
