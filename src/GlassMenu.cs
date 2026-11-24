using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// A small glass pop-up menu (macOS style): rows with an optional tick and shortcut text,
    /// separators, closed by a click outside, Esc, or choosing a row. Used where Studio's own
    /// dropdowns misbehave (the camera selector).
    /// </summary>
    internal class GlassMenu : MonoBehaviour
    {
        public struct Item
        {
            public string Label, Right;
            public bool Check, Separator, Disabled;
            public System.Action Act;
            /// <summary>A slider row instead of a button: <see cref="Value"/> 0..1, <see cref="Slide"/> on change, <see cref="ValueText"/> shown at the right.</summary>
            public bool Slider;
            public float Value;
            public System.Action<float> Slide;
            public System.Func<string> ValueText;
            public static Item Sep() { return new Item { Separator = true }; }
        }

        private const float ItemH = 24f, SepH = 9f, Pad = 6f, SliderH = 42f;
        private static GlassMenu _open;
        private GameObject _canvasGo;

        /// <summary>
        /// Show a menu with its top-left (or, with <paramref name="alignRight"/>, its top-right) at a screen point.
        /// <paramref name="like"/>: a canvas to match in scale, so the menu is the same size as the UI it opens from.
        /// </summary>
        public static void Show(Vector2 screenPoint, float width, IList<Item> items, Canvas like = null, bool alignRight = false)
        {
            CloseOpen();
            var go = new GameObject(ThemedButton.OwnPrefix + "Glass Menu", typeof(RectTransform));
            DontDestroyOnLoad(go);
            _open = go.AddComponent<GlassMenu>();
            _open._like = like != null ? like.rootCanvas : null;
            _open._alignRight = alignRight;
            _open.Build(screenPoint, width, items);
        }

        private Canvas _like;
        private float _scale = 1f;
        private bool _alignRight;

        public static void CloseOpen() { CloseOpen(null); }

        public static void CloseOpen(string why)
        {
            if (_open != null)
            {
                if (why != null) StudioThemePlugin.Log.LogDebug("Glass menu closed: " + why);
                Destroy(_open.gameObject);
            }
            _open = null;
        }

        private float _shownAt;

        private struct Row { public RectTransform Rt; public System.Action Act; public bool Disabled; }
        private readonly List<Row> _rows = new List<Row>();
        private RectTransform _panel;
        private int _downRow = -2;   // -2: no press; -1: pressed outside the panel; >= 0: pressed on that row

        /// <summary>
        /// Clicks are read straight from the mouse here, not through Unity's UI events: in Studio a click on
        /// a row could land on the full-screen catcher behind the menu instead (the menu closed without doing
        /// anything). A press and release on the same row chooses it; a click outside the menu closes it.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { CloseOpen("Esc"); return; }
            if (_panel == null) return;
            var mouse = (Vector2)Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) _downRow = RowAt(mouse);
            if (!Input.GetMouseButtonUp(0) || _downRow == -2) return;
            int up = RowAt(mouse), down = _downRow;
            _downRow = -2;
            if (Time.unscaledTime - _shownAt < 0.25f) return;   // the click that opened the menu
            if (up == -1 && down == -1) { CloseOpen("a click outside"); return; }
            if (up >= 0 && up == down && !_rows[up].Disabled)
            {
                var act = _rows[up].Act;
                CloseOpen();
                try { if (act != null) act(); } catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Menu: " + e.Message); }
            }
        }

        /// <summary>The row under a screen point; -3 on the menu but not on a row (a slider, a gap), -1 outside it.</summary>
        private int RowAt(Vector2 screen)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(_panel, screen, null)) return -1;   // (rows scrolled out of view lie outside it)
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Rt != null && _rows[i].Rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_rows[i].Rt, screen, null))
                    return i;
            return -3;
        }

        private void Build(Vector2 screen, float width, IList<Item> items)
        {
            _canvasGo = gameObject;
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31500;
            // A fixed scale worked out here (Studio's own, or 1080p-relative), so positions can be computed at
            // once: a new canvas hasn't sized itself yet in this frame, and measuring it put the menu in the
            // wrong place (off to the side, or out of sight, so the next click just closed it).
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scale = _like != null && _like.scaleFactor > 0.01f ? _like.scaleFactor
                : Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(Screen.width / 1920f, 2f), Mathf.Log(Screen.height / 1080f, 2f), 0.5f));
            scaler.scaleFactor = _scale;
            _canvasGo.AddComponent<GraphicRaycaster>();

            // Clicking anywhere else closes it.
            var layer = NewRect(transform, "Layer");
            var lrt = (RectTransform)layer.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            layer.AddComponent<Image>().color = Color.clear;
            _shownAt = Time.unscaledTime;   // the catcher only blocks clicks to Studio; Update reads them

            float height = Pad * 2f;
            foreach (var it in items) height += it.Separator ? SepH : it.Slider ? SliderH : ItemH;

            var panel = NewRect(transform, "Panel");
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.pivot = new Vector2(0f, 1f);
            // Canvas units from the bottom-left corner: screen pixels over the scale.
            var size = new Vector2(Screen.width, Screen.height) / _scale;
            var p = screen / _scale;
            if (_alignRight) p.x -= width;
            p.x = Mathf.Clamp(p.x, 4f, size.x - width - 4f);
            // Taller than the room below the point (a scene with many cameras): it scrolls.
            float room = Mathf.Max(160f, p.y - 8f);
            float shown = Mathf.Min(height, room);
            p.y = Mathf.Clamp(p.y, shown + 4f, size.y - 4f);
            prt.sizeDelta = new Vector2(width, shown);
            prt.anchoredPosition = p;
            var bg = panel.AddComponent<Image>();
            bg.sprite = Sprites.Panel;
            bg.type = Image.Type.Sliced;
            _panel = prt;

            if (height > shown + 0.5f)
            {
                var viewport = NewRect(panel.transform, "Viewport");
                var vrt = (RectTransform)viewport.transform;
                vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
                vrt.offsetMin = vrt.offsetMax = Vector2.zero;
                viewport.AddComponent<RectMask2D>();
                var content = NewRect(viewport.transform, "Content");
                var crt = (RectTransform)content.transform;
                crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
                crt.pivot = new Vector2(0.5f, 1f);
                crt.sizeDelta = new Vector2(0f, height);
                var scroll = panel.AddComponent<ScrollRect>();
                scroll.viewport = vrt; scroll.content = crt;
                scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = ItemH;
                panel = content;   // rows go in the scrolling content
            }

            float y = -Pad;
            foreach (var it in items)
            {
                if (it.Separator)
                {
                    var line = NewRect(panel.transform, "Separator");
                    Place(line, Pad + 6f, y - SepH / 2f, width - 2f * Pad - 12f, 1f);
                    var li = line.AddComponent<Image>();
                    li.color = Palette.White(0.12f);
                    li.raycastTarget = false;
                    y -= SepH;
                    continue;
                }
                if (it.Slider)
                {
                    BuildSlider(panel.transform, it, y, width);
                    y -= SliderH;
                    continue;
                }
                var row = NewRect(panel.transform, it.Label);
                Place(row, Pad / 2f, y, width - Pad, ItemH);
                var img = row.AddComponent<Image>();
                var button = row.AddComponent<Button>();
                button.targetGraphic = img;
                button.interactable = !it.Disabled;
                var act = it.Act;
                _rows.Add(new Row { Rt = (RectTransform)row.transform, Act = act, Disabled = it.Disabled });   // clicked through Update
                var b = ThemedButton.Attach(button, ButtonKind.Row, null, (it.Check ? "✓  " : "     ") + it.Label);
                if (b == null) continue;
                b.AlignLabelLeft(8f);   // menu rows read from the left, like a Mac menu
                if (!string.IsNullOrEmpty(it.Right))
                {
                    var sc = NewRect(row.transform, "Right");
                    var rt = (RectTransform)sc.transform;
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(-10f, 0f);
                    var t = Labels.Configure(sc.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleRight, 11, 8);
                    t.text = it.Right;
                    t.color = Palette.White(0.4f);
                    t.raycastTarget = false;
                }
                y -= ItemH;
            }
        }

        /// <summary>A labelled slider row: the label and its value on one line, a themed slider under it.</summary>
        private static void BuildSlider(Transform panel, Item it, float y, float width)
        {
            var row = NewRect(panel, it.Label);
            Place(row, Pad + 8f, y - 2f, width - 2f * Pad - 16f, SliderH - 4f);

            var label = NewRect(row.transform, "Label");
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.offsetMin = new Vector2(0f, -18f); lrt.offsetMax = Vector2.zero;
            var lt = Labels.Configure(label.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleLeft, 12, 8);
            lt.text = it.Label;
            lt.color = Palette.White(Palette.TextAlpha);
            lt.raycastTarget = false;

            var value = NewRect(row.transform, "Value");
            var vrt = (RectTransform)value.transform;
            vrt.anchorMin = lrt.anchorMin; vrt.anchorMax = lrt.anchorMax; vrt.pivot = lrt.pivot;
            vrt.offsetMin = lrt.offsetMin; vrt.offsetMax = lrt.offsetMax;
            var vt = Labels.Configure(value.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleRight, 11, 8);
            vt.color = Palette.White(0.55f);
            vt.raycastTarget = false;

            // A plain uGUI slider (track, fill area, handle), then themed like every other slider.
            var sgo = NewRect(row.transform, "Slider");
            var srt = (RectTransform)sgo.transform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.offsetMin = new Vector2(2f, 2f); srt.offsetMax = new Vector2(-2f, 18f);
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sgo.transform, false);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = new Vector2(0f, 0.5f); brt.anchorMax = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(0f, 4f);
            bg.AddComponent<Image>().color = Color.white;
            var area = NewRect(sgo.transform, "Handle Area");
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(7f, 0f); art.offsetMax = new Vector2(-7f, 0f);
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(area.transform, false);
            var hrt = (RectTransform)handle.transform;
            hrt.sizeDelta = new Vector2(14f, 0f);
            var himg = handle.AddComponent<Image>();
            var slider = sgo.AddComponent<Slider>();
            slider.handleRect = hrt;
            slider.targetGraphic = himg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(it.Value);
            ThemedSlider.Attach(slider);

            var text = it.ValueText;
            var slide = it.Slide;
            vt.text = text != null ? text() : "";
            slider.onValueChanged.AddListener(v =>
            {
                try { if (slide != null) slide(v); } catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Menu slider: " + e.Message); }
                if (text != null) vt.text = text();
            });
        }

        private static GameObject NewRect(Transform parent, string name)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Place(GameObject go, float x, float y, float w, float h)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
