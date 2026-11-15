using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// A one-line label that keeps its size instead of shrinking to fit. Text that's too long ends
    /// in "…"; with the pointer over the label (or the button it's on), the whole text scrolls
    /// across and back. The label's Text sits inside a clipping frame that takes the label's place,
    /// so code that positions the label positions the frame (see <see cref="Labels.Frame"/>).
    /// State is serialised so copies of a themed button or label keep working.
    /// </summary>
    internal class Marquee : MonoBehaviour
    {
        private const float Speed = 32f, BackSpeed = 90f, PauseStart = 0.9f, PauseEnd = 1.1f;
        private const string Ellipsis = "…";

        [SerializeField] private Text _text;
        [SerializeField] private TextAnchor _align;

        [System.NonSerialized] private string _full, _shown;
        [System.NonSerialized] private float _fullW, _frameW, _offset, _clock;
        [System.NonSerialized] private Font _font;
        [System.NonSerialized] private int _size;
        [System.NonSerialized] private bool _over, _scrolling;
        [System.NonSerialized] private int _phase;
        private RectTransform _frame;

        /// <summary>Put <paramref name="t"/> in a clipping frame that takes over its place and size.</summary>
        public static Marquee Wrap(Text t)
        {
            if (t == null || t.transform.parent == null) return null;
            if (t.transform.parent.GetComponent<Marquee>() != null) return t.transform.parent.GetComponent<Marquee>();
            var trt = (RectTransform)t.transform;
            var go = new GameObject(ThemedButton.OwnPrefix + "Clip", typeof(RectTransform));
            go.layer = t.gameObject.layer;
            var frame = (RectTransform)go.transform;
            frame.SetParent(trt.parent, false);
            frame.SetSiblingIndex(trt.GetSiblingIndex());
            frame.anchorMin = trt.anchorMin; frame.anchorMax = trt.anchorMax;
            frame.pivot = trt.pivot;
            frame.offsetMin = trt.offsetMin; frame.offsetMax = trt.offsetMax;
            go.AddComponent<RectMask2D>();
            trt.SetParent(frame, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.pivot = new Vector2(0f, 0.5f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            t.resizeTextForBestFit = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var m = go.AddComponent<Marquee>();
            m._text = t;
            m._align = t.alignment;
            return m;
        }

        private void Awake() { _frame = (RectTransform)transform; }

        public string FullText { get { return _full ?? (_text != null ? _text.text : null); } }

        private void OnDisable()
        {
            if (_scrolling) StopScroll();
        }

        private void LateUpdate()
        {
            if (_text == null) return;
            if (_frame == null) _frame = (RectTransform)transform;

            float frameW = _frame.rect.width;
            bool changed = _text.text != _shown;
            if (changed) _full = _text.text;
            if (changed || _text.font != _font || _text.fontSize != _size || Mathf.Abs(frameW - _frameW) > 0.5f)
            {
                _font = _text.font;
                _size = _text.fontSize;
                _frameW = frameW;
                Measure();
            }
            if (!_over) return;

            bool hover = Hovered();
            if (hover && !_scrolling) StartScroll();
            else if (!hover && _scrolling) StopScroll();
            if (_scrolling) Animate();
        }

        private void Measure()
        {
            _fullW = Width(_full);
            _over = _frameW > 1f && _fullW > _frameW + 0.5f;
            _scrolling = false;
            var rt = _text.rectTransform;
            if (!_over)
            {
                _text.alignment = _align;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                Show(_full);
                return;
            }
            // Left-aligned from here on (centred text that overflows would start off the left edge).
            _text.alignment = (TextAnchor)((int)_align / 3 * 3);
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            Truncated();
        }

        private void Truncated()
        {
            var rt = _text.rectTransform;
            rt.sizeDelta = new Vector2(_frameW, 0f);
            rt.anchoredPosition = Vector2.zero;
            // Longest start of the text that fits with "…" after it.
            int lo = 0, hi = _full.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Width(_full.Substring(0, mid).TrimEnd() + Ellipsis) <= _frameW) lo = mid; else hi = mid - 1;
            }
            Show(_full.Substring(0, lo).TrimEnd() + Ellipsis);
        }

        private void StartScroll()
        {
            _scrolling = true;
            _phase = 0;
            _clock = 0f;
            _offset = 0f;
            var rt = _text.rectTransform;
            rt.sizeDelta = new Vector2(_fullW + 2f, 0f);
            rt.anchoredPosition = Vector2.zero;
            Show(_full);
        }

        private void StopScroll()
        {
            _scrolling = false;
            if (_text != null && _over) Truncated();
        }

        private void Animate()
        {
            float dt = Time.unscaledDeltaTime, end = -(_fullW - _frameW + 2f);
            _clock += dt;
            switch (_phase)
            {
                case 0: if (_clock >= PauseStart) { _phase = 1; _clock = 0f; } break;
                case 1:
                    _offset = Mathf.Max(end, _offset - Speed * dt);
                    if (_offset <= end) { _phase = 2; _clock = 0f; }
                    break;
                case 2: if (_clock >= PauseEnd) { _phase = 3; _clock = 0f; } break;
                case 3:
                    _offset = Mathf.Min(0f, _offset + BackSpeed * dt);
                    if (_offset >= 0f) { _phase = 0; _clock = 0f; }
                    break;
            }
            _text.rectTransform.anchoredPosition = new Vector2(_offset, 0f);
        }

        private void Show(string s)
        {
            _shown = s ?? "";
            if (_text.text != _shown) _text.text = _shown;
        }

        private float Width(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            var settings = _text.GetGenerationSettings(new Vector2(10000f, _text.rectTransform.rect.height));
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.resizeTextForBestFit = false;
            settings.richText = false;
            return _text.cachedTextGeneratorForLayout.GetPreferredWidth(s, settings) / _text.pixelsPerUnit;
        }

        private bool Hovered()
        {
            var target = _frame.parent as RectTransform ?? _frame;
            var canvas = _text.canvas;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(target, Input.mousePosition, cam);
        }
    }
}
