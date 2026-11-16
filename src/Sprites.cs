using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HS2StudioTheme
{
    /// <summary>
    /// Every sprite the theme draws with, generated at start-up (no image files except the four icons).
    /// Rounded shapes are drawn at 2 texture pixels per UI unit and 9-sliced, so corners stay crisp
    /// at any panel size.
    /// </summary>
    internal static class Sprites
    {
        private const int PxPerUnit = 2;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly List<Object> Owned = new List<Object>();

        /// <summary>Glass panel: tinted fill at the configured opacity with a hairline border.</summary>
        public static Sprite Panel { get { return Get("panel", () => Rounded(Palette.PanelRadius, Glass(), Palette.White(Palette.BorderAlpha))); } }

        /// <summary>The glass behind the main menu column.</summary>
        public static Sprite Backdrop { get { return Get("backdrop", () => Rounded(Palette.BackdropRadius, Glass(), Palette.White(Palette.BorderAlpha))); } }

        /// <summary>Plain white rounded row, tinted by the Image colour.</summary>
        public static Sprite Row { get { return Get("row", () => Rounded(Palette.RowRadius, Color.white, Color.clear)); } }

        /// <summary>Plain white rounded tile for the main menu buttons.</summary>
        public static Sprite Tile { get { return Get("tile", () => Rounded(Palette.TileRadius, Color.white, Color.clear)); } }

        /// <summary>Thin scroll thumb centred in Studio's 20-unit-wide scrollbar.</summary>
        public static Sprite ScrollThumb { get { return Get("thumb", () => ScrollThumbSprite(20)); } }

        /// <summary>The same thumb for an 8-unit-wide scroll bar (the narrowed animation columns).</summary>
        public static Sprite ScrollThumbNarrow { get { return Get("thumb8", () => ScrollThumbSprite(8)); } }

        /// <summary>Rounded text field / dropdown: faint fill, hairline border.</summary>
        public static Sprite Field { get { return Get("field", () => Rounded(Palette.RowRadius, Palette.White(Palette.FieldAlpha), Palette.White(Palette.BorderAlpha))); } }

        /// <summary>Colour swatch: white (tinted by the colour), with a dark hairline so light colours still show an edge.</summary>
        public static Sprite Swatch { get { return Get("swatch", () => Rounded(4f, Color.white, new Color(0f, 0f, 0f, 0.35f))); } }

        /// <summary>A 4-unit-high pill centred in a 20-unit-high texture: slider track and fill.</summary>
        public static Sprite Track { get { return Get("track", TrackSprite); } }

        /// <summary>Slider knob: white circle with a soft dark rim.</summary>
        public static Sprite Knob { get { return Get("knob", () => Badge(14f, 14f, 7f, Color.white, new Color(0f, 0f, 0f, 0.25f), null, 0f)); } }

        public static Sprite CheckOff { get { return Get("check_off", () => Badge(20f, 14f, 3.5f, Palette.White(0.06f), Palette.White(0.45f), null, 0f)); } }
        public static Sprite CheckOn { get { return Get("check_on", () => Badge(20f, 14f, 3.5f, Palette.Accent, Color.clear, "check", 10f)); } }
        public static Sprite RadioOff { get { return Get("radio_off", () => Badge(20f, 14f, 7f, Palette.White(0.06f), Palette.White(0.45f), null, 0f)); } }
        public static Sprite RadioOn { get { return Get("radio_on", () => RadioOnSprite()); } }

        public static Sprite Icon(string key)
        {
            return Get("icon_" + key, () => LoadIcon(key));
        }

        /// <summary>An icon drawn at <paramref name="fraction"/> of its square, so it can fill a bigger button image.</summary>
        public static Sprite IconPadded(string key, float fraction)
        {
            return Get("icon_" + key + "_" + fraction, () =>
            {
                var src = Icon(key);
                if (src == null) return null;
                const int n = 64;
                var tex = NewTexture(n, n);
                var px = new Color[n * n];
                float inner = n * fraction, off = (n - inner) / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f - off) / inner, v = (y + 0.5f - off) / inner;
                        px[y * n + x] = u < 0f || u > 1f || v < 0f || v > 1f ? Color.clear
                            : new Color(1f, 1f, 1f, src.texture.GetPixelBilinear(u, v).a);
                    }
                tex.SetPixels(px);
                tex.Apply(false, true);
                return MakeSprite(tex, Vector4.zero, 100f);
            });
        }

        /// <summary>Horizontal scroll thumb: a 4-unit-high pill centred in a 20-unit-high bar.</summary>
        public static Sprite ScrollThumbH { get { return Get("thumbH", ScrollThumbHSprite); } }

        /// <summary>Three short diagonal lines for a window's bottom-left resize corner.</summary>
        public static Sprite ResizeGrip { get { return Get("grip", ResizeGripSprite); } }

        private static Sprite ScrollThumbHSprite()
        {
            // ScrollThumbSprite(20) turned on its side.
            const int w = 12, h = 20;
            const float pillHalf = 2f, radius = 2f;
            var tex = NewTexture(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float qy = Mathf.Abs(y + 0.5f - h / 2f) - (pillHalf - radius);
                    float qx = Mathf.Abs(x + 0.5f - w / 2f) - (w / 2f - 1f - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return MakeSprite(tex, new Vector4(5, h / 2, 5, h / 2), 100f);
        }

        private static Sprite ResizeGripSprite()
        {
            const int n = 24;   // 12 units at 2 px per unit
            var tex = NewTexture(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Lines across the bottom-left corner, far enough in to clear the window's rounded corner.
                    float a = 0f;
                    foreach (var c in new[] { 18f, 24f, 30f })
                    {
                        float d = Mathf.Abs((x + 0.5f) + (y + 0.5f) - c) / 1.4142f;   // distance to x + y = c
                        a = Mathf.Max(a, Mathf.Clamp01(1.6f - d));
                    }
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return MakeSprite(tex, Vector4.zero, 200f);
        }

        /// <summary>Forget generated sprites (after the opacity changes). Old ones are destroyed.</summary>
        public static void Clear()
        {
            foreach (var o in Owned) if (o != null) Object.Destroy(o);
            Owned.Clear();
            Cache.Clear();
        }

        private static Color Glass()
        {
            var c = Palette.GlassTint;
            c.a = StudioThemePlugin.GlassOpacity.Value;
            return c;
        }

        private static Sprite Get(string key, System.Func<Sprite> make)
        {
            Sprite s;
            if (Cache.TryGetValue(key, out s) && s != null) return s;
            s = make();
            if (s != null) s.name = OwnName + key;
            Cache[key] = s;
            return s;
        }

        private const string OwnName = "theme_";

        /// <summary>One of the theme's own pictures (so a copied, already-themed control isn't themed twice).</summary>
        public static bool IsOurs(Sprite s) { return s != null && s.name.StartsWith(OwnName); }

        /// <summary>A rounded rectangle, 9-sliced so the corner keeps <paramref name="radius"/> UI units.</summary>
        private static Sprite Rounded(float radius, Color fill, Color border)
        {
            int r = Mathf.CeilToInt(radius * PxPerUnit);
            int size = r * 2 + 4;
            var tex = NewTexture(size, size);
            var px = new Color[size * size];
            float half = size / 2f;
            float borderWidth = 1f * PxPerUnit;   // one UI unit

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded rectangle's edge (negative inside).
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - r);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - r);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;

                    float shape = Mathf.Clamp01(0.5f - d);
                    float edge = border.a > 0f ? Mathf.Clamp01(d + borderWidth + 0.5f) : 0f;

                    var c = Over(border, fill, edge);
                    c.a *= shape;
                    px[y * size + x] = c;
                }

            tex.SetPixels(px);
            tex.Apply(false, true);
            int b = r + 1;
            return MakeSprite(tex, new Vector4(b, b, b, b), 100f * PxPerUnit);
        }

        /// <summary>A 4-unit-wide pill centred in a 20-unit-wide texture. Left and right borders take
        /// the whole width, so the thumb never stretches sideways.</summary>
        private static Sprite ScrollThumbSprite(int w)
        {
            const int h = 12;
            const float pillHalf = 2f, radius = 2f;
            var tex = NewTexture(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - w / 2f) - (pillHalf - radius);
                    float qy = Mathf.Abs(y + 0.5f - h / 2f) - (h / 2f - 1f - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return MakeSprite(tex, new Vector4(w / 2, 5, w / 2, 5), 100f);
        }

        private static Sprite TrackSprite()
        {
            int w = 4 * PxPerUnit, h = 20 * PxPerUnit;
            float pillHalf = 2f * PxPerUnit, r = 2f * PxPerUnit;
            var tex = NewTexture(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = BoxDistance(x + 0.5f - w / 2f, y + 0.5f - h / 2f, w / 2f, pillHalf, r);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            int bx = w / 2, by = h / 2;
            return MakeSprite(tex, new Vector4(bx, by, bx, by), 100f * PxPerUnit);
        }

        /// <summary>
        /// A centred rounded box (<paramref name="box"/> units) on a transparent square of
        /// <paramref name="size"/> units, optionally with a white icon on top. Not sliced: used at its own size.
        /// </summary>
        private static Sprite Badge(float size, float box, float radius, Color fill, Color border, string iconKey, float iconSize)
        {
            int n = Mathf.RoundToInt(size * PxPerUnit);
            var tex = NewTexture(n, n);
            var px = new Color[n * n];
            float half = box * PxPerUnit / 2f, r = radius * PxPerUnit, bw = 1f * PxPerUnit;

            Texture2D icon = null;
            if (iconKey != null)
            {
                var s = Icon(iconKey);
                if (s != null) icon = s.texture;
            }
            float iconHalf = iconSize * PxPerUnit / 2f;

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                    float d = BoxDistance(dx, dy, half, half, r);
                    float shape = Mathf.Clamp01(0.5f - d);
                    float edge = border.a > 0f ? Mathf.Clamp01(d + bw + 0.5f) : 0f;
                    var c = Over(border, fill, edge);
                    c.a *= shape;

                    if (icon != null && Mathf.Abs(dx) < iconHalf && Mathf.Abs(dy) < iconHalf)
                    {
                        var ic = icon.GetPixelBilinear((dx + iconHalf) / (2f * iconHalf), (dy + iconHalf) / (2f * iconHalf));
                        c = Over(new Color(1f, 1f, 1f, ic.a), c, 1f);
                    }
                    px[y * n + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return MakeSprite(tex, Vector4.zero, 100f * PxPerUnit);
        }

        private static Sprite RadioOnSprite()
        {
            // Accent circle with a white dot.
            int n = 20 * PxPerUnit;
            var tex = NewTexture(n, n);
            var px = new Color[n * n];
            float outer = 7f * PxPerUnit, dot = 3f * PxPerUnit;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dist = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
                    var c = Palette.Accent;
                    c.a = Mathf.Clamp01(outer - dist + 0.5f);
                    c = Over(Palette.OnAccent(), c, Mathf.Clamp01(dot - dist + 0.5f));
                    px[y * n + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return MakeSprite(tex, Vector4.zero, 100f * PxPerUnit);
        }

        /// <summary>Signed distance from a point (relative to the centre) to a rounded box.</summary>
        private static float BoxDistance(float px, float py, float halfW, float halfH, float r)
        {
            float qx = Mathf.Abs(px) - (halfW - r);
            float qy = Mathf.Abs(py) - (halfH - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static Sprite LoadIcon(string key)
        {
            var asm = typeof(Sprites).Assembly;
            using (var stream = asm.GetManifestResourceStream("HS2StudioTheme.icon_" + key + ".png"))
            {
                if (stream == null)
                {
                    StudioThemePlugin.Log.LogWarning("Icon missing from the plugin: " + key);
                    return null;
                }
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var tex = NewTexture(2, 2);
                tex.LoadImage(bytes, false);   // readable: badges draw icons into their own textures
                return MakeSprite(tex, Vector4.zero, 100f);
            }
        }

        private static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontSave;
            Owned.Add(tex);
            return tex;
        }

        private static Sprite MakeSprite(Texture2D tex, Vector4 border, float ppu)
        {
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                ppu, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.DontSave;
            Owned.Add(s);
            return s;
        }

        /// <summary>Paint <paramref name="top"/> over <paramref name="bottom"/> with coverage <paramref name="k"/>.</summary>
        private static Color Over(Color top, Color bottom, float k)
        {
            float ta = top.a * k;
            float a = ta + bottom.a * (1f - ta);
            if (a <= 0f) return new Color(bottom.r, bottom.g, bottom.b, 0f);
            var rgb = (new Vector3(top.r, top.g, top.b) * ta + new Vector3(bottom.r, bottom.g, bottom.b) * bottom.a * (1f - ta)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }
    }
}
