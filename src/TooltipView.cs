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

            // Also above when another button sits right under this one (toolbar rows): the tip would cover it.
            bool above = bottom.y - 28f < area.yMin || ButtonBelow(target);
            _rt.pivot = new Vector2(0.5f, above ? 0f : 1f);
            var pos = above ? top + new Vector2(0f, 6f) : bottom - new Vector2(0f, 6f);
            pos.x = Mathf.Clamp(pos.x, area.xMin + w / 2f + 4f, area.xMax - w / 2f - 4f);
            _rt.localPosition = pos;

            _go.transform.SetAsLastSibling();
            _go.SetActive(true);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// A list row's full name: beside the list (right of it, or left near the screen's edge), level with
        /// the row, so it never covers the names above and below.
        /// </summary>
        public static void ShowBeside(RectTransform target, string text)
        {
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;
            Ensure(canvas.transform);

            _target = target;
            _text.text = text;
            float w = Mathf.Ceil(_text.preferredWidth) + 16f;
            _rt.sizeDelta = new Vector2(w, 22f);

            var canvasRt = (RectTransform)canvas.transform;
            var list = target.GetComponentInParent<ScrollRect>();
            var edge = list != null ? (RectTransform)list.transform : target;
            edge.GetWorldCorners(Corners);
            float right = canvasRt.InverseTransformPoint(Corners[2]).x, left = canvasRt.InverseTransformPoint(Corners[0]).x;
            target.GetWorldCorners(Corners);
            float y = canvasRt.InverseTransformPoint((Corners[0] + Corners[1]) / 2f).y;
            var area = canvasRt.rect;

            bool toRight = right + 8f + w <= area.xMax - 4f;
            _rt.pivot = new Vector2(toRight ? 0f : 1f, 0.5f);
            _rt.localPosition = new Vector2(toRight ? right + 8f : left - 8f, Mathf.Clamp(y, area.yMin + 12f, area.yMax - 12f));

            _go.transform.SetAsLastSibling();
            _go.SetActive(true);
        }

        private static bool ButtonBelow(RectTransform target)
        {
            var parent = target.parent;
            if (parent == null) return false;
            target.GetWorldCorners(Corners);
            Vector2 tMin = parent.InverseTransformPoint(Corners[0]), tMax = parent.InverseTransformPoint(Corners[2]);
            float h = tMax.y - tMin.y;
            foreach (Transform s in parent)
            {
                if (s == target || !s.gameObject.activeInHierarchy || s.GetComponent<Selectable>() == null) continue;
                var rt = s as RectTransform;
                if (rt == null || rt.lossyScale.x == 0f) continue;
                rt.GetWorldCorners(Corners);
                Vector2 sMin = parent.InverseTransformPoint(Corners[0]), sMax = parent.InverseTransformPoint(Corners[2]);
                bool overlapsX = sMin.x < tMax.x && sMax.x > tMin.x;
                bool justBelow = sMax.y <= tMin.y + 1f && sMax.y > tMin.y - h;
                if (overlapsX && justBelow) return true;
            }
            return false;
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

            // Always drawn on top, whatever canvases the panels below have of their own.
            var own = _go.AddComponent<Canvas>();
            own.overrideSorting = true;
            own.sortingOrder = 32000;

            // A lighter, solid capsule with a hairline, so it reads clearly over the dark glass.
            var bg = _go.AddComponent<Image>();
            bg.sprite = Sprites.Field;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.24f, 0.24f, 0.26f, 1f);
            bg.raycastTarget = false;
            var outline = _go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

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
