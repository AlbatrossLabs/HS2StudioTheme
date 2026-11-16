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

        // Serialised, so a copy another plugin makes of a themed slider (FK & IK's Size) keeps working.
        [SerializeField] private bool _themed;
        [SerializeField] private Slider _slider;
        [SerializeField] private Image _track;
        [SerializeField] private Sprite _trackSprite;
        [SerializeField] private Image.Type _trackType;
        [SerializeField] private Color _trackColor;
        [SerializeField] private Image _handle;
        [SerializeField] private Color _handleColor;

        [SerializeField] private RectTransform _fill;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _knob;
        [System.NonSerialized] private bool _removed, _live;

        public static ThemedSlider Attach(Slider slider)
        {
            if (slider == null || slider.handleRect == null) return null;
            foreach (var existing in slider.GetComponents<ThemedSlider>())
            {
                if (existing._removed) continue;
                if (!existing._live) existing.Adopt();
                if (existing._live) return null;
            }
            var themed = slider.gameObject.AddComponent<ThemedSlider>();
            if (!themed.Init(slider)) { Destroy(themed); return null; }
            return themed;
        }

        private void Awake()
        {
            if (_themed && !_live) Adopt();
        }

        /// <summary>A copy made by Instantiate: carry on with the copied parts, or tidy up if they didn't come along.</summary>
        private void Adopt()
        {
            if (_slider == null || _track == null || _handle == null || _fill == null || _fillImage == null || _knob == null)
            {
                // Couldn't take over: drop whatever half-copy there is, leave the slider plain-ish.
                _removed = true;
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    if (t != transform && t.name.StartsWith(ThemedButton.OwnPrefix) && (t.name.EndsWith("Fill") || t.name.EndsWith("Knob"))) Destroy(t.gameObject);
                if (_handle != null) _handle.color = _handleColor.a > 0f ? _handleColor : Color.white;
                Destroy(this);
                return;
            }
            _live = true;
            Clones.Track(this, Remove);
            Refresh();
        }

        private bool Init(Slider slider)
        {
            _themed = true;
            _live = true;
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
            if (_removed || !_live || _slider == null) return;
            bool on = _slider.IsInteractable();
            _fillImage.color = on ? Palette.Accent : Palette.White(Palette.DisabledAlpha);
            _knob.color = on ? Color.white : Palette.White(0.6f);
        }

        private void LateUpdate()
        {
            if (_removed || !_live || _slider == null) return;
            bool horizontal = _slider.direction == Slider.Direction.LeftToRight;
            if (_fill.gameObject.activeSelf != horizontal) _fill.gameObject.SetActive(horizontal);
            if (!horizontal) return;

            var trackRt = (RectTransform)_track.transform;
            var handleRt = _slider.handleRect;
            Vector3 centre = handleRt.TransformPoint(handleRt.rect.center);
            float x = trackRt.InverseTransformPoint(centre).x - trackRt.rect.xMin;
            var size = new Vector2(Mathf.Max(0f, x), 0f);
            if ((_fill.sizeDelta - size).sqrMagnitude > 0.0001f) _fill.sizeDelta = size;   // a write rebuilds the canvas
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
