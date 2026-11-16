using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The fold button on a Screen Effect / Option section header. Studio swaps the button's sprite
    /// between two triangles; the theme leaves that alone (transparent) and draws a chevron that points
    /// down while the section below is open and right while it's folded.
    /// </summary>
    internal class DisclosureChevron : MonoBehaviour
    {
        private Image _image;
        private Color _imageColor;
        private GameObject _section;
        private RectTransform _chevron;
        private Image _chevronImage;
        private bool _removed, _lastOpen;

        public static DisclosureChevron Attach(Button button, GameObject section)
        {
            if (button == null || button.image == null || button.GetComponent<DisclosureChevron>() != null) return null;
            var c = button.gameObject.AddComponent<DisclosureChevron>();
            c.Init(button.image, section);
            return c;
        }

        private void Init(Image image, GameObject section)
        {
            _image = image;
            _section = section;
            _imageColor = image.color;
            image.color = Palette.White(0f);   // still the click target

            var go = new GameObject(ThemedButton.OwnPrefix + "Chevron", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _chevron = (RectTransform)go.transform;
            _chevron.anchorMin = _chevron.anchorMax = new Vector2(0.5f, 0.5f);
            _chevron.sizeDelta = new Vector2(12f, 12f);
            // A fold button that spans the header (Screen Effect): the chevron at its right end.
            var r = ((RectTransform)transform).rect;
            if (r.width > r.height * 3f)
            {
                _chevron.anchorMin = _chevron.anchorMax = new Vector2(1f, 0.5f);
                _chevron.anchoredPosition = new Vector2(-14f, 0f);
            }
            _chevronImage = go.AddComponent<Image>();
            _chevronImage.sprite = Sprites.Icon("chevron");
            _chevronImage.preserveAspect = true;
            _chevronImage.raycastTarget = false;
            _chevronImage.color = Palette.White(0.7f);

            _lastOpen = !Open();
            LateUpdate();
        }

        private bool Open() { return _section == null || _section.activeSelf; }

        private void LateUpdate()
        {
            if (_removed) return;
            bool open = Open();
            if (open == _lastOpen) return;
            _lastOpen = open;
            _chevron.localEulerAngles = new Vector3(0f, 0f, open ? 0f : 90f);
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_image != null) _image.color = _imageColor;
            if (_chevron != null) Destroy(_chevron.gameObject);
            Destroy(this);
        }
    }
}
