using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Apple-style slider: thin track, accent fill up to the knob, round white knob. Studio's slider,
    /// handle and track objects all stay (and still take the clicks); the track sprite is swapped, the
    /// handle image goes transparent, and the fill and knob are drawn by two added children.
    /// </summary>
    internal class ThemedSlider : MonoBehaviour
    {
        private const float KnobSize = 14f;

        private Slider _slider;
        private Image _track;
        private Sprite _trackSprite;
        private Image.Type _trackType;
        private Color _trackColor;
        private Image _handle;
        private Color _handleColor;

        private RectTransform _fill;
        private Image _fillImage;
        private Image _knob;
        private bool _removed;

        public static ThemedSlider Attach(Slider slider)
        {
            if (slider == null || slider.handleRect == null || slider.GetComponent<ThemedSlider>() != null) return null;
            var themed = slider.gameObject.AddComponent<ThemedSlider>();
            if (!themed.Init(slider)) { Destroy(themed); return null; }
            return themed;
        }

        private bool Init(Slider slider)
        {
            _slider = slider;
            var bg = slider.transform.Find("Background");
            _track = bg != null ? bg.GetComponent<Image>() : null;
            _handle = slider.handleRect.GetComponent<Image>();
            if (_track == null || _handle == null) return false;

            _trackSprite = _track.sprite;
            _trackType = _track.type;
            _trackColor = _track.color;
            _track.sprite = Sprites.Track;
            _track.type = Image.Type.Sliced;
            _track.color = Palette.White(Palette.TrackAlpha);

            _handleColor = _handle.color;
            _handle.color = Palette.White(0f);    // still the drag target

            // Fill: from the left end of the track to the knob.
            var fillGo = new GameObject(ThemedButton.OwnPrefix + "Fill", typeof(RectTransform));
            fillGo.layer = gameObject.layer;
            fillGo.transform.SetParent(_track.transform, false);
            _fill = (RectTransform)fillGo.transform;
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = Vector2.zero;
            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.sprite = Sprites.Track;
            _fillImage.type = Image.Type.Sliced;
            _fillImage.raycastTarget = false;

            // Knob: a fixed-size circle centred on Studio's (narrow) handle.
            var knobGo = new GameObject(ThemedButton.OwnPrefix + "Knob", typeof(RectTransform));
            knobGo.layer = gameObject.layer;
            knobGo.transform.SetParent(_handle.transform, false);
            var krt = (RectTransform)knobGo.transform;
            krt.anchorMin = krt.anchorMax = new Vector2(0.5f, 0.5f);
            krt.sizeDelta = new Vector2(KnobSize, KnobSize);
            _knob = knobGo.AddComponent<Image>();
            _knob.sprite = Sprites.Knob;
            _knob.raycastTarget = false;

            Refresh();
            return true;
        }

        public void Refresh()
        {
            if (_removed) return;
            bool on = _slider.IsInteractable();
            _fillImage.color = on ? Palette.Accent : Palette.White(Palette.DisabledAlpha);
            _knob.color = on ? Color.white : Palette.White(0.6f);
        }

        private void LateUpdate()
        {
            if (_removed || _slider == null) return;
            bool horizontal = _slider.direction == Slider.Direction.LeftToRight;
            _fill.gameObject.SetActive(horizontal);
            if (!horizontal) return;

            var trackRt = (RectTransform)_track.transform;
            var handleRt = _slider.handleRect;
            Vector3 centre = handleRt.TransformPoint(handleRt.rect.center);
            float x = trackRt.InverseTransformPoint(centre).x - trackRt.rect.xMin;
            _fill.sizeDelta = new Vector2(Mathf.Max(0f, x), 0f);
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_track != null)
            {
                _track.sprite = _trackSprite;
                _track.type = _trackType;
                _track.color = _trackColor;
            }
            if (_handle != null) _handle.color = _handleColor;
            if (_fill != null) Destroy(_fill.gameObject);
            if (_knob != null) Destroy(_knob.gameObject);
            Destroy(this);
        }
    }
}
