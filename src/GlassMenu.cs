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
        private bool _alignRight;

        public static void CloseOpen()
        {
            if (_open != null) Destroy(_open.gameObject);
            _open = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseOpen();
        }

        private void Build(Vector2 screen, float width, IList<Item> items)
        {
            _canvasGo = gameObject;
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31500;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            if (_like != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = _like.scaleFactor;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            _canvasGo.AddComponent<GraphicRaycaster>();

            // Clicking anywhere else closes it.
            var layer = NewRect(transform, "Layer");
            var lrt = (RectTransform)layer.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            layer.AddComponent<Image>().color = Color.clear;
            var closer = layer.AddComponent<Button>();
            closer.transition = Selectable.Transition.None;
            closer.onClick.AddListener(CloseOpen);

            float height = Pad * 2f;
            foreach (var it in items) height += it.Separator ? SepH : it.Slider ? SliderH : ItemH;

            var panel = NewRect(transform, "Panel");
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(width, height);
            var canvasRt = (RectTransform)transform;
            Canvas.ForceUpdateCanvases();
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out local);
            var size = canvasRt.rect.size;
            var p = local + size * 0.5f;
            if (_alignRight) p.x -= width;
            p.x = Mathf.Clamp(p.x, 4f, size.x - width - 4f);
            p.y = Mathf.Clamp(p.y, height + 4f, size.y - 4f);
            prt.anchoredPosition = p;
            var bg = panel.AddComponent<Image>();
            bg.sprite = Sprites.Panel;
            bg.type = Image.Type.Sliced;

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
                button.onClick.AddListener(() =>
                {
                    CloseOpen();
                    try { if (act != null) act(); } catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Menu: " + e.Message); }
                });
                var b = ThemedButton.Attach(button, ButtonKind.Row, null, (it.Check ? "✓  " : "     ") + it.Label);
                if (b == null) continue;
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
