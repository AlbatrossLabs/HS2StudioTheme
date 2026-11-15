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
            public static Item Sep() { return new Item { Separator = true }; }
        }

        private const float ItemH = 24f, SepH = 9f, Pad = 6f;
        private static GlassMenu _open;
        private GameObject _canvasGo;

        /// <summary>Show a menu with its top-left (or top-right if it would leave the screen) at a screen point.</summary>
        public static void Show(Vector2 screenTopLeft, float width, IList<Item> items)
        {
            CloseOpen();
            var go = new GameObject(ThemedButton.OwnPrefix + "Glass Menu", typeof(RectTransform));
            DontDestroyOnLoad(go);
            _open = go.AddComponent<GlassMenu>();
            _open.Build(screenTopLeft, width, items);
        }

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
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
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
            foreach (var it in items) height += it.Separator ? SepH : ItemH;

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
