using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Right-click a character in the scene (a click, not a camera drag) for a context menu: the
    /// workspace's utilities for that character (select, focus the camera, show / hide, duplicate,
    /// delete), shortcuts into the panels you'd open for them (state, kinematics, animations, outfit),
    /// FK / IK switches, and a Clothing flyout with On / Half / Off for each part. Everything goes
    /// through Studio's own calls, so the workspace, the panels and undo stay in step.
    /// </summary>
    internal class CharacterMenu : MonoBehaviour
    {
        private static readonly string[] Parts = { "Top", "Bottom", "Bra", "Underwear", "Gloves", "Pantyhose", "Socks", "Shoes" };
        private static readonly string[] States = { "On", "Half", "Off" };
        private const float Width = 250f, RowH = 26f, Pad = 8f, SegW = 46f;
        private const float MenuW = 210f, ItemH = 24f, SepH = 9f;
        private const float ClickPixels = 5f, ClickSeconds = 0.4f;

        private GameObject _canvasGo, _menu, _flyout;
        private RectTransform _panel;
        private Vector3 _downPos;
        private float _downTime;
        private bool _down;
        private Studio.OCIChar _char;

        public static CharacterMenu Create(Undo undo)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + "Character Menu");
            DontDestroyOnLoad(go);
            var m = go.AddComponent<CharacterMenu>();
            undo.Add(() => { if (go != null) Destroy(go); });
            return m;
        }

        private void Update()
        {
            if (_menu != null && Input.GetKeyDown(KeyCode.Escape)) Close();
            if (!StudioThemePlugin.CharacterMenu.Value) return;

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
                if (!click) return;
                if (PointerOverUi()) { StudioThemePlugin.Log.LogDebug("Right-click over the UI: no character menu"); return; }
                var chara = Pick(Input.mousePosition);
                StudioThemePlugin.Log.LogDebug("Right-click in the scene: " + (chara != null ? chara.charInfo.fileParam.fullname : "no character under the pointer"));
                if (chara != null) Open(chara, Input.mousePosition);
                else Close();
            }
        }

        private static bool PointerOverUi()
        {
            try { if (Studio.Studio.Instance != null && Studio.Studio.Instance.isInputNow) return true; } catch (System.Exception) { }
            var es = EventSystem.current;
            if (es == null) return false;
            // Something visible or clickable under the pointer. (Plugins sometimes leave invisible
            // full-screen catchers around; those don't count.)
            var data = new PointerEventData(es) { position = Input.mousePosition };
            Hits.Clear();
            es.RaycastAll(data, Hits);
            foreach (var hit in Hits)
            {
                if (hit.gameObject == null) continue;
                if (hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
                var g = hit.gameObject.GetComponent<Graphic>();
                if (g != null && g.color.a * g.canvasRenderer.GetAlpha() > 0.02f) return true;
            }
            return false;
        }

        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        private static Camera _camera;

        /// <summary>Studio's scene camera (its CameraControl's camera; Camera.main isn't always set).</summary>
        private static Camera SceneCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled) return _camera;
            _camera = null;
            try
            {
                var ctrl = Traverse.Create(Studio.Studio.Instance).Property("cameraCtrl").GetValue() as Component;
                if (ctrl != null) _camera = ctrl.GetComponent<Camera>() ?? ctrl.GetComponentInChildren<Camera>();
            }
            catch (System.Exception) { }
            if (_camera == null) _camera = Camera.main;
            if (_camera == null)
            {
                // Last resort: the lowest full-screen camera (the scene, under any overlay cameras).
                foreach (var c in Camera.allCameras)
                    if (c.targetTexture == null && (_camera == null || c.depth < _camera.depth)) _camera = c;
            }
            return _camera;
        }

        /// <summary>The nearest visible character whose body or head is under the pointer.</summary>
        private static Studio.OCIChar Pick(Vector3 screen)
        {
            var cam = SceneCamera();
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

        private struct Item
        {
            public string Label, Shortcut;
            public System.Action Act;
            public bool Danger, Separator, KeepOpen;
            public static Item Sep() { return new Item { Separator = true }; }
        }

        private void Open(Studio.OCIChar chara, Vector3 screen)
        {
            Close();
            _char = chara;
            EnsureCanvas();
            var node = chara.treeNodeObject;
            var info = chara.oiCharInfo;

            var items = new List<Item>
            {
                new Item { Label = "Select in workspace", Act = () => Select() },
                new Item { Label = "Focus camera", Shortcut = "F", Act = FocusCamera },
                new Item { Label = node != null && !node.visible ? "Show" : "Hide", Act = ToggleVisible },
                Item.Sep(),
                new Item { Label = "Current state…", Act = () => OpenPanel(0) },
                new Item { Label = "Kinematics…", Act = () => OpenPanel(1) },
                new Item { Label = "Animations…", Act = () => OpenPanel(2) },
                new Item { Label = "Outfit…", Act = () => OpenPanel(4) },
                new Item { Label = "Clothing", Shortcut = "›", KeepOpen = true, Act = OpenClothing },
                Item.Sep(),
                new Item { Label = (info.enableFK ? "✓  " : "     ") + "FK", Act = () => Kinematic(Studio.OICharInfo.KinematicMode.FK) },
                new Item { Label = (info.enableIK ? "✓  " : "     ") + "IK", Act = () => Kinematic(Studio.OICharInfo.KinematicMode.IK) },
                Item.Sep(),
                new Item { Label = "Duplicate", Shortcut = "Ctrl+D", Act = Duplicate },
                new Item { Label = "Delete", Shortcut = "Del", Danger = true, Act = Delete },
            };

            float height = Pad + 22f + Pad / 2f;
            foreach (var it in items) height += it.Separator ? SepH : ItemH;
            height += Pad;

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

            _panel = Panel(_menu.transform, MenuW, height, ScreenToCanvas(screen) + new Vector2(4f, -4f));

            var title = Label(_panel, chara.charInfo.fileParam.fullname, true, TextAnchor.MiddleLeft);
            ((Text)title.GetComponent<Text>()).color = Palette.White(0.55f);
            Place(title, Pad + 4f, -Pad, MenuW - 2f * Pad, 22f);

            float y = -Pad - 22f - Pad / 2f;
            foreach (var it in items)
            {
                if (it.Separator)
                {
                    var line = NewRect(_panel, "Separator");
                    Place(line, Pad + 4f, y - SepH / 2f, MenuW - 2f * Pad - 8f, 1f);
                    var li = line.AddComponent<Image>();
                    li.color = Palette.White(0.12f);
                    li.raycastTarget = false;
                    y -= SepH;
                    continue;
                }
                MenuRow(it, y);
                y -= ItemH;
            }
        }

        private void MenuRow(Item it, float y)
        {
            var go = NewRect(_panel, it.Label);
            Place(go, Pad / 2f, y, MenuW - Pad, ItemH);
            var img = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var act = it.Act;
            bool keep = it.KeepOpen;
            button.onClick.AddListener(() =>
            {
                try { act(); } catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Character menu: " + e.Message); }
                if (!keep) Close();
            });
            var b = ThemedButton.Attach(button, ButtonKind.Row, null, it.Label);
            if (b != null && it.Danger) b.LabelTint = Palette.Hex("FF6961");
            if (!string.IsNullOrEmpty(it.Shortcut))
            {
                var sc = Label(go.transform, it.Shortcut, false, TextAnchor.MiddleRight);
                var rt = (RectTransform)sc.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(0f, 0f); rt.offsetMax = new Vector2(-10f, 0f);
                sc.GetComponent<Text>().color = Palette.White(0.4f);
            }
        }

        /// <summary>The Clothing flyout: each part worn, with On / Half / Off, plus All. Stays open for several changes.</summary>
        private void OpenClothing()
        {
            if (_flyout != null) { Destroy(_flyout); _flyout = null; return; }
            var cha = _char.charInfo;
            var rows = new List<int>();
            for (int k = 0; k < Parts.Length; k++)
                if (cha.IsClothesStateKind(k)) rows.Add(k);
            float height = Pad + (rows.Count + 1) * RowH + Pad;

            // To the right of the menu, or to its left when there's no room.
            var canvasRt = (RectTransform)_canvasGo.transform;
            var pos = _panel.anchoredPosition + new Vector2(MenuW + 4f, 0f);
            if (pos.x + Width > canvasRt.rect.width - 4f) pos.x = _panel.anchoredPosition.x - Width - 4f;
            var panel = Panel(_menu.transform, Width, height, pos);
            _flyout = panel.gameObject;

            float y = -Pad;
            foreach (var k in rows) { Row(panel, Parts[k], k, y); y -= RowH; }
            Row(panel, "All", -1, y);
        }

        /// <summary>A glass panel with its top-left at <paramref name="topLeft"/> (canvas units from bottom-left), kept on screen.</summary>
        private RectTransform Panel(Transform parent, float w, float h, Vector2 topLeft)
        {
            var go = NewRect(parent, "Panel");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            var size = ((RectTransform)_canvasGo.transform).rect.size;
            topLeft.x = Mathf.Clamp(topLeft.x, 4f, size.x - w - 4f);
            topLeft.y = Mathf.Clamp(topLeft.y, h + 4f, size.y - 4f);
            rt.anchoredPosition = topLeft;
            var bg = go.AddComponent<Image>();
            bg.sprite = Sprites.Panel;
            bg.type = Image.Type.Sliced;
            return rt;
        }

        private Vector2 ScreenToCanvas(Vector3 screen)
        {
            var canvasRt = (RectTransform)_canvasGo.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out local);
            return local + canvasRt.rect.size * 0.5f;
        }

        // ------------------------------------------------------------------ actions (Studio's own calls)

        private bool Select()
        {
            var node = _char != null ? _char.treeNodeObject : null;
            if (node == null) return false;
            var ctrl = Studio.Studio.Instance.treeNodeCtrl;
            var selected = ctrl.selectNodes;
            if (selected != null && selected.Length == 1 && selected[0] == node) return true;   // already: leave the panels as they are
            ctrl.SelectSingle(node, false);   // (true would deselect a node that's already selected)
            return true;
        }

        private void FocusCamera()
        {
            if (_char == null || _char.guideObject == null) return;
            // Studio's F key: the camera's target moves to the selection.
            var cam = Traverse.Create(Studio.Studio.Instance).Property("cameraCtrl");
            cam.Property("targetPos").SetValue(_char.guideObject.transform.position);
        }

        private void ToggleVisible()
        {
            var node = _char != null ? _char.treeNodeObject : null;
            if (node != null) node.SetVisible(!node.visible);
        }

        /// <summary>Select the character, open Anim, and the given row of its list (0 state, 1 kinematics, 2 animations, 4 outfit).</summary>
        private void OpenPanel(int row)
        {
            var chara = _char;
            if (!Select()) return;
            StartCoroutine(OpenPanelLater(chara, row));
        }

        /// <summary>
        /// Studio rebuilds the Manipulate panels a frame after a selection changes, which would undo
        /// a page opened straight away; so: select, wait, open Anim, wait, open the page.
        /// </summary>
        private System.Collections.IEnumerator OpenPanelLater(Studio.OCIChar chara, int row)
        {
            yield return null;
            var root = Studio.Studio.Instance.rootButtonCtrl;
            if (root != null && root.select != 1) root.OnClick(1);
            yield return null;
            Studio.MPCharCtrl mp = null;
            foreach (var m in Resources.FindObjectsOfTypeAll<Studio.MPCharCtrl>())
                if (m.gameObject.scene.IsValid()) { mp = m; break; }
            if (mp == null) yield break;
            if (mp.ociChar != chara) mp.ociChar = chara;
            if (!mp.active) mp.active = true;
            mp.OnClickRoot(row);
        }

        private void Kinematic(Studio.OICharInfo.KinematicMode mode)
        {
            bool on = mode == Studio.OICharInfo.KinematicMode.FK ? _char.oiCharInfo.enableFK : _char.oiCharInfo.enableIK;
            _char.ActiveKinematicMode(mode, !on, false);
            var mp = Object.FindObjectOfType<Studio.MPCharCtrl>();
            try { if (mp != null && mp.ociChar == _char) AccessTools.Method(typeof(Studio.MPCharCtrl), "UpdateInfo")?.Invoke(mp, null); } catch (System.Exception) { }
        }

        private void Duplicate()
        {
            if (Select()) Studio.Studio.Instance.Duplicate();
        }

        private void Delete()
        {
            if (!Select()) return;
            var ws = Object.FindObjectOfType<Studio.WorkspaceCtrl>();
            if (ws != null) ws.OnClickDelete();
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
            _flyout = null;
            _panel = null;
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
