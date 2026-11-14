using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// One small glass tooltip, shown under (or above, near the bottom of the screen) whatever the
    /// pointer is over. Lives on the same canvas as the button it describes.
    /// </summary>
    internal static class TooltipView
    {
        private static GameObject _go;
        private static RectTransform _rt;
        private static Text _text;
        private static Transform _target;

        public static void Show(RectTransform target, string text)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;
            Ensure(canvas.transform);

            _target = target;
            _text.text = text;
            float w = Mathf.Ceil(_text.preferredWidth) + 16f;
            _rt.sizeDelta = new Vector2(w, 22f);

            // Below the target, centred; above it if that would leave the screen.
            var canvasRt = (RectTransform)canvas.transform;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 bottom = canvasRt.InverseTransformPoint((corners[0] + corners[3]) / 2f);
            Vector2 top = canvasRt.InverseTransformPoint((corners[1] + corners[2]) / 2f);
            var area = canvasRt.rect;

            bool above = bottom.y - 28f < area.yMin;
            _rt.pivot = new Vector2(0.5f, above ? 0f : 1f);
            var pos = above ? top + new Vector2(0f, 6f) : bottom - new Vector2(0f, 6f);
            pos.x = Mathf.Clamp(pos.x, area.xMin + w / 2f + 4f, area.xMax - w / 2f - 4f);
            _rt.localPosition = pos;

            _go.transform.SetAsLastSibling();
            _go.SetActive(true);
        }

        public static void Hide(Transform target)
        {
            if (_go != null && _target == target) _go.SetActive(false);
        }

        public static void Destroy()
        {
            if (_go != null) Object.Destroy(_go);
            _go = null;
            _target = null;
        }

        private static void Ensure(Transform canvas)
        {
            if (_go != null && _go.transform.parent == canvas) return;
            if (_go != null) Object.Destroy(_go);

            _go = new GameObject(ThemedButton.OwnPrefix + "Tooltip", typeof(RectTransform));
            _go.layer = canvas.gameObject.layer;
            _go.transform.SetParent(canvas, false);
            _rt = (RectTransform)_go.transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);

            var bg = _go.AddComponent<Image>();
            bg.sprite = Sprites.Field;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.08f, 0.08f, 0.09f, 0.96f);
            bg.raycastTarget = false;

            var textGo = new GameObject(ThemedButton.OwnPrefix + "Tooltip Text", typeof(RectTransform));
            textGo.layer = _go.layer;
            textGo.transform.SetParent(_go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _text = textGo.AddComponent<Text>();
            _text.font = Fonts.Regular;
            _text.fontSize = 12;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.color = Palette.White(0.95f);
            _text.raycastTarget = false;

            _go.SetActive(false);
        }
    }
}
