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
        private int _lastCount = -1;
        private float _nextScan;
        private bool _removed;

        public static ThemedPanel Attach(ScrollRect scroll)
        {
            if (scroll == null) return null;
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
