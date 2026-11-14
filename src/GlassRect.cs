using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// A 9-sliced sprite drawn around a set of RectTransforms, padded outwards. Used for the glass
    /// behind the main menu column: it sits on the column's own object (a parent draws before its
    /// children), so no new sibling is added and nothing in the game's hierarchy shifts.
    /// </summary>
    internal class GlassRect : MaskableGraphic
    {
        public Sprite sprite;
        public float padding = 6f;
        public float corner = Palette.BackdropRadius;
        public RectTransform[] around;

        private Rect _lastBounds;
        private readonly Vector3[] _corners = new Vector3[4];

        public override Texture mainTexture
        {
            get { return sprite != null ? sprite.texture : s_WhiteTexture; }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        private void LateUpdate()
        {
            var b = Bounds();
            if (b != _lastBounds)
            {
                _lastBounds = b;
                SetVerticesDirty();
            }
        }

        /// <summary>Union of the tracked rects in this object's local space (or its own rect).</summary>
        private Rect Bounds()
        {
            if (around == null || around.Length == 0) return rectTransform.rect;
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            var corners = _corners;
            foreach (var rt in around)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = rectTransform.InverseTransformPoint(corners[i]);
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                }
            }
            return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : rectTransform.rect;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sprite == null) return;

            var r = Bounds();
            r.xMin -= padding; r.yMin -= padding; r.xMax += padding; r.yMax += padding;
            float c = Mathf.Min(corner, r.width / 2f, r.height / 2f);

            var outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            var border = sprite.border;
            float tw = sprite.texture.width, th = sprite.texture.height;

            float[] xs = { r.xMin, r.xMin + c, r.xMax - c, r.xMax };
            float[] ys = { r.yMin, r.yMin + c, r.yMax - c, r.yMax };
            float[] us = { outer.x, outer.x + border.x / tw, outer.z - border.z / tw, outer.z };
            float[] vs = { outer.y, outer.y + border.y / th, outer.w - border.w / th, outer.w };

            var col = (Color32)color;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    vh.AddVert(new Vector3(xs[x], ys[y]), col, new Vector2(us[x], vs[y]));

            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int i = y * 4 + x;
                    vh.AddTriangle(i, i + 4, i + 5);
                    vh.AddTriangle(i, i + 5, i + 1);
                }
        }
    }
}
