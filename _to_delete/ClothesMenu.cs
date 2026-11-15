using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Right-click a character in the scene (a click, not a camera drag) for a small glass menu of
    /// clothing states: each part the character is wearing with On / Half / Off, plus All. The current
    /// state of each part shows in the accent colour; states a piece of clothing doesn't have are dimmed.
    /// Changes go through Studio (OCIChar.SetClothesState), the same as its own State panel.
    /// </summary>
    internal class ClothesMenu : MonoBehaviour
    {
        private static readonly string[] Parts = { "Top", "Bottom", "Bra", "Underwear", "Gloves", "Pantyhose", "Socks", "Shoes" };
        private static readonly string[] States = { "On", "Half", "Off" };
        private const float Width = 250f, RowH = 26f, Pad = 8f, SegW = 46f;
        private const float ClickPixels = 5f, ClickSeconds = 0.4f;

        private GameObject _canvasGo, _menu;
        private Vector3 _downPos;
        private float _downTime;
        private bool _down;
        private Studio.OCIChar _char;

        public static ClothesMenu Create(Undo undo)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + "Clothes Menu");
            DontDestroyOnLoad(go);
            var m = go.AddComponent<ClothesMenu>();
            undo.Add(() => { if (go != null) Destroy(go); });
            return m;
        }

        private void Update()
        {
            if (_menu != null && Input.GetKeyDown(KeyCode.Escape)) Close();
            if (!StudioThemePlugin.ClothesMenu.Value) return;

            if (Input.GetMouseButtonDown(1))
            {
                if (_menu != null) { Close(); _down = false; return; }   // a right-click while open just closes it
                _down = !PointerOverUi();
                _downPos = Input.mousePosition;
                _downTime = Time.unscaledTime;
            }
            else if (Input.GetMouseButtonUp(1) && _down)
            {
                _down = false;
                bool click = (Input.mousePosition - _downPos).magnitude <= ClickPixels && Time.unscaledTime - _downTime <= ClickSeconds;
                if (!click || PointerOverUi()) return;
                var chara = Pick(Input.mousePosition);
                if (chara != null) Open(chara, Input.mousePosition);
                else Close();
            }
        }

        private static bool PointerOverUi()
        {
            try { if (Studio.Studio.Instance != null && Studio.Studio.Instance.isInputNow) return true; } catch (System.Exception) { }
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>The nearest visible character whose body or head is under the pointer.</summary>
        private static Studio.OCIChar Pick(Vector3 screen)
        {
            var cam = Camera.main;   // Studio's main camera
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(screen);
            Studio.OCIChar best = null;
            float bestDist = float.MaxValue;
            foreach (var info in Studio.Studio.Instance.dicObjectCtrl.Values)
            {
                var c = info as Studio.OCIChar;
                if (c == null || c.charInfo == null || !c.charInfo.visibleAll) continue;
                foreach (var part in new[] { c.charInfo.objBody, c.charInfo.objHead })
                {
                    if (part == null) continue;
                    foreach (var r in part.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                    {
                        float d;
                        if (!r.enabled || !r.bounds.IntersectRay(ray, out d) || d >= bestDist) continue;
                        bestDist = d;
                        best = c;
                    }
                }
            }
            return best;
        }

        private void Open(Studio.OCIChar chara, Vector3 screen)
        {
            Close();
            _char = chara;
            EnsureCanvas();

            var cha = chara.charInfo;
            var rows = new List<int>();
            for (int k = 0; k < Parts.Length; k++)
                if (cha.IsClothesStateKind(k)) rows.Add(k);
            float height = Pad + 24f + (rows.Count + 1) * RowH + Pad;

            // A clear full-screen layer under the menu: clicking anywhere else closes it.
            _menu = new GameObject(ThemedButton.OwnPrefix + "Menu Layer", typeof(RectTransform));
            _menu.transform.SetParent(_canvasGo.transform, false);
            var layer = (RectTransform)_menu.transform;
            layer.anchorMin = Vector2.zero; layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            var catcher = _menu.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            var closer = _menu.AddComponent<Button>();
            closer.transition = Selectable.Transition.None;
            closer.onClick.AddListener(Close);

            var panel = NewRect(_menu.transform, "Panel");
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = Vector2.zero;
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(Width, height);
            var bg = panel.AddComponent<Image>();
            bg.sprite = Sprites.Panel;
            bg.type = Image.Type.Sliced;

            // Place it at the pointer, kept on screen (canvas units).
            var canvasRt = (RectTransform)_canvasGo.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out local);
            var size = canvasRt.rect.size;
            var p = local + size * 0.5f + new Vector2(6f, -6f);
            p.x = Mathf.Clamp(p.x, 4f, size.x - Width - 4f);
            p.y = Mathf.Clamp(p.y, height + 4f, size.y - 4f);
            prt.anchoredPosition = p;

            var title = Label(panel.transform, cha.fileParam.fullname + "  ·  Clothes", true, TextAnchor.MiddleLeft);
            Place(title, Pad + 2f, -Pad, Width - 2f * Pad, 24f);

            float y = -Pad - 24f;
            foreach (var k in rows) { Row(panel.transform, Parts[k], k, y); y -= RowH; }
            Row(panel.transform, "All", -1, y);
        }

        private void Row(Transform panel, string name, int kind, float y)
        {
            var label = Label(panel, name, false, TextAnchor.MiddleLeft);
            Place(label, Pad + 2f, y, 90f, RowH);
            float x = Width - Pad - States.Length * SegW;
            var seg = NewRect(panel, "Segment");
            Place(seg, x - 2f, y - 2f, States.Length * SegW + 4f, RowH - 4f);
            var segImg = seg.AddComponent<Image>();
            segImg.sprite = Sprites.Row;
            segImg.type = Image.Type.Sliced;
            segImg.color = Palette.White(0.07f);
            segImg.raycastTarget = false;
            for (int s = 0; s < States.Length; s++)
            {
                int state = s;
                var go = NewRect(panel, States[s]);
                Place(go, x + s * SegW, y - 3f, SegW, RowH - 6f);
                var img = go.AddComponent<Image>();
                var button = go.AddComponent<Button>();
                button.targetGraphic = img;
                var cha = _char.charInfo;
                if (kind >= 0) button.interactable = cha.IsClothesStateType(kind, (byte)state);
                button.onClick.AddListener(() => Set(kind, state));
                var b = ThemedButton.Attach(button, ButtonKind.Push, null, States[s]);
                if (b != null)
                    b.ActiveWhen = () => _char != null && Current(kind) == state;
            }
        }

        private int Current(int kind)
        {
            var st = _char.charInfo.fileStatus.clothesState;
            if (kind >= 0) return kind < st.Length ? st[kind] : -1;
            // All: the state every worn part shares, if they agree.
            int shared = -1;
            for (int k = 0; k < Parts.Length && k < st.Length; k++)
            {
                if (!_char.charInfo.IsClothesStateKind(k)) continue;
                if (shared == -1) shared = st[k];
                else if (shared != st[k]) return -1;
            }
            return shared;
        }

        private void Set(int kind, int state)
        {
            if (_char == null) return;
            try
            {
                if (kind >= 0) _char.SetClothesState(kind, (byte)state);
                else _char.SetClothesStateAll(state);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't change clothing: " + e.Message); }
        }

        private void Close()
        {
            if (_menu != null) Destroy(_menu);
            _menu = null;
            _char = null;
        }

        private void EnsureCanvas()
        {
            if (_canvasGo != null) return;
            _canvasGo = new GameObject(ThemedButton.OwnPrefix + "Clothes Canvas", typeof(RectTransform));
            _canvasGo.transform.SetParent(transform, false);
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGo.AddComponent<GraphicRaycaster>();
        }

        private static GameObject NewRect(Transform parent, string name)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject Label(Transform parent, string text, bool strong, TextAnchor anchor)
        {
            var go = NewRect(parent, "Label");
            var t = Labels.Configure(go.AddComponent<Text>(), strong ? Fonts.Semibold : Fonts.Regular, anchor, strong ? 12 : 12, 9);
            t.text = text;
            t.color = Palette.White(strong ? 0.95f : 0.85f);
            t.raycastTarget = false;
            return go;
        }

        /// <summary>Top-left anchored rect at (x, y) from the panel's top-left.</summary>
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
