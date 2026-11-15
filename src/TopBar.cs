using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The top of the screen: Studio's six show/hide switches as one capsule in the top-right corner,
    /// and the camera bar (slots plus the camera selector) as one capsule centred at the top.
    /// </summary>
    internal static class TopBar
    {
        // StudioScene.drawInfo fields, in the order of Studio's visibleFlags.
        private static readonly string[] ImageFields = { "imageMenu", "imageWork", "imageMove", "imageColor", "imageOption", "imageCamera" };
        private static readonly string[] Icons = { "v_menu", "v_work", "v_gizmo", "v_color", "v_option", "v_camera" };
        private static readonly string[] Names = { "Main menu", "Workspace", "Move gizmos", "Color picker", "Map and object controls", "Camera bar" };

        private const float ToggleW = 34f, ToggleH = 30f, Gap = 2f, Pad = 4f, Margin = 8f;

        /// <summary>The six show/hide switches, once themed (the workspace lines up under them).</summary>
        internal static readonly List<RectTransform> Toggles = new List<RectTransform>();

        /// <summary>World-space corners of the switches' glass capsule, or false if there isn't one.</summary>
        internal static bool CapsuleWorldRect(out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            max = new Vector3(float.MinValue, float.MinValue, 0f);
            var corners = new Vector3[4];
            bool any = false;
            foreach (var t in Toggles)
            {
                if (t == null) continue;
                t.GetWorldCorners(corners);
                min = Vector3.Min(min, corners[0]);
                max = Vector3.Max(max, corners[2]);
                any = true;
            }
            if (!any) return false;
            float pad = Pad * Toggles[0].lossyScale.x;
            min -= new Vector3(pad, pad, 0f);
            max += new Vector3(pad, pad, 0f);
            return true;
        }

        public static void Apply(Undo undo)
        {
            var scene = Object.FindObjectOfType<StudioScene>();
            if (scene == null)
            {
                StudioThemePlugin.Log.LogWarning("Studio's top bar not found");
                return;
            }
            var draw = Traverse.Create(scene).Field("drawInfo").GetValue();
            if (draw == null) return;
            var drawT = Traverse.Create(draw);

            var toggles = Toggles;
            toggles.Clear();
            for (int i = 0; i < ImageFields.Length; i++)
            {
                var image = drawT.Field(ImageFields[i]).GetValue<Image>();
                var button = image != null ? image.GetComponent<Button>() : null;
                if (button == null) continue;

                var rt = (RectTransform)button.transform;
                Place(rt, new Vector2(-(Margin + Pad) - (ImageFields.Length - 1 - i) * (ToggleW + Gap), -(Margin + Pad)),
                    new Vector2(ToggleW, ToggleH), undo);

                var themed = ThemedButton.Attach(button, ButtonKind.ViewToggle, Icons[i], null, i);
                if (themed == null) continue;
                int index = i;
                themed.Tooltip = () => Names[index] + ": " + (Shown(index) ? "shown" : "hidden");
                undo.Add(themed.Remove);
                toggles.Add(rt);
            }

            if (toggles.Count > 0)
            {
                var column = toggles[0].parent;
                if (column != null && column.GetComponent<Graphic>() == null)
                {
                    var glass = column.gameObject.AddComponent<GlassRect>();
                    glass.sprite = Sprites.Backdrop;
                    glass.around = toggles.ToArray();
                    glass.padding = Pad;
                    glass.corner = Palette.BackdropRadius;
                    glass.color = Color.white;
                    undo.Add(() => { if (glass != null) Object.DestroyImmediate(glass); });
                }
            }

            var cameraBar = drawT.Field("objCamera").GetValue<GameObject>();
            if (cameraBar != null)
            {
                var bar = CameraBar.Attach((RectTransform)cameraBar.transform, undo);
                if (bar != null) undo.Add(bar.Remove);
            }
        }

        private static bool Shown(int index)
        {
            try { return Studio.Studio.Instance.workInfo.visibleFlags[index]; }
            catch (System.Exception) { return true; }
        }

        /// <summary>Anchor to the top-right corner at this offset and size, recording the old layout.</summary>
        private static void Place(RectTransform rt, Vector2 position, Vector2 size, Undo undo)
        {
            var aMin = rt.anchorMin; var aMax = rt.anchorMax; var pivot = rt.pivot;
            var pos = rt.anchoredPosition; var sz = rt.sizeDelta;
            rt.anchorMin = rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.one;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            undo.Add(() =>
            {
                if (rt == null) return;
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
                rt.anchoredPosition = pos; rt.sizeDelta = sz;
            });
        }
    }

    /// <summary>
    /// Keeps the camera bar centred at the top of the screen and drawn as one glass capsule. Works on
    /// Studio's ten slots or on MoarCamz's scrolling slot strip with its extra buttons, and re-measures
    /// twice a second so slots added later are themed and the bar stays centred.
    /// </summary>
    internal class CameraBar : MonoBehaviour
    {
        private const string Selector = "Image Camera Setting";

        // One row the height of the show/hide switches: Studio's slots start 8 below the bar's top;
        // the theme fits them (and the camera selector) into 30 and moves the bar 4 down, so with
        // the capsule's 4 of padding it lines up with the switches' capsule (8 from the top, 38 tall).
        private const float RowTop = -8f, RowHeight = 30f, Drop = 4f, Pad = 4f;
        private RectTransform _row;
        private MoarCamzTheme _moarCamz;

        // Camera slots: three show, plus any that have a view saved, plus as many as + has asked for.
        private const int MinSlots = 3;
        private int _revealed, _shown;
        private readonly HashSet<GameObject> _hiddenSlots = new HashSet<GameObject>();
        private readonly List<KeyValuePair<int, ThemedButton>> _slots = new List<KeyValuePair<int, ThemedButton>>();
        private float _scrollRight0 = float.NaN;
        private bool _moarCamzFailed;
        private readonly HashSet<RectTransform> _fitted = new HashSet<RectTransform>();

        private RectTransform _rt;
        private Vector2 _origPos;
        private GlassRect _glass;
        private readonly List<RectTransform> _items = new List<RectTransform>();
        private readonly List<ThemedButton> _buttons = new List<ThemedButton>();
        private readonly Undo _undo = new Undo();
        private float _next;
        private bool _removed;

        public static CameraBar Attach(RectTransform bar, Undo undo)
        {
            if (bar == null || bar.GetComponent<CameraBar>() != null) return null;
            var c = bar.gameObject.AddComponent<CameraBar>();
            c.Init(bar);
            return c;
        }

        private void Init(RectTransform bar)
        {
            _rt = bar;
            _origPos = bar.anchoredPosition;

            var rowGo = new GameObject(ThemedButton.OwnPrefix + "Camera Row", typeof(RectTransform));
            rowGo.layer = gameObject.layer;
            rowGo.transform.SetParent(transform, false);
            _row = (RectTransform)rowGo.transform;
            _row.anchorMin = _row.anchorMax = new Vector2(0f, 1f);
            _row.pivot = new Vector2(0f, 1f);
            _undo.Add(() => { if (rowGo != null) Destroy(rowGo); });

            if (GetComponent<Graphic>() == null)
            {
                _glass = gameObject.AddComponent<GlassRect>();
                _glass.sprite = Sprites.Backdrop;
                _glass.padding = Pad;
                _glass.corner = Palette.PanelRadius;
                _glass.color = Color.white;
                _glass.around = new[] { _row };
            }

            StyleSelector();
            Scan();
            Centre();
        }

        /// <summary>The camera selector: transparent box (the capsule is behind it), small title, themed dropdown.</summary>
        private void StyleSelector()
        {
            var selector = transform.Find(Selector);
            if (selector == null) return;
            var box = selector.GetComponent<Image>();
            if (box != null) ControlStyler.Swap(box, box.sprite, box.type, Palette.White(0f), _undo);

            // One line: the dropdown alone, centred in the row (its "Camera" title would need a second line).
            var title = selector.Find("TextMeshPro Title");
            var titleText = title != null ? title.GetComponent<TMPro.TMP_Text>() : null;
            if (titleText != null)
            {
                var c = titleText.color;
                titleText.color = new Color(c.r, c.g, c.b, 0f);
                _undo.Add(() => { if (titleText != null) titleText.color = c; });
            }
            Fit(selector as RectTransform, null, new Vector2(128f, RowHeight));
            Fit(selector.Find("Image Background") as RectTransform, Vector2.zero, new Vector2(-8f, -6f));
            var dropdown = selector.GetComponentInChildren<TMPro.TMP_Dropdown>(true);
            if (dropdown == null) return;

            // Studio's dropdown (pick one of the scene's camera objects) is hidden and stays the model:
            // the theme shows its choice on a button that opens a glass menu, with "Add camera" in it.
            _dropdown = dropdown;
            var ddGo = dropdown.gameObject;
            var cg = ddGo.GetComponent<CanvasGroup>() ?? ddGo.AddComponent<CanvasGroup>();
            float a0 = cg.alpha; bool b0 = cg.blocksRaycasts, i0 = cg.interactable;
            cg.alpha = 0f; cg.blocksRaycasts = false; cg.interactable = false;
            _undo.Add(() => { if (cg != null) { cg.alpha = a0; cg.blocksRaycasts = b0; cg.interactable = i0; } });

            var go = new GameObject(ThemedButton.OwnPrefix + "Camera Switch", typeof(RectTransform));
            go.layer = selector.gameObject.layer;
            go.transform.SetParent(selector, false);
            _undo.Add(() => { if (go != null) Destroy(go); });
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 24f);
            var img = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => OpenCameraMenu(rt));
            var themed = ThemedButton.Attach(button, ButtonKind.Push, null, "Free camera");
            if (themed != null) themed.Tooltip = () => "Switch to a camera in the scene, or add one";
            _switchLabel = go.GetComponentInChildren<Text>(true);
            var chevron = new GameObject(ThemedButton.OwnPrefix + "Chevron", typeof(RectTransform));
            chevron.transform.SetParent(go.transform, false);
            var crt = (RectTransform)chevron.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-6f, 0f);
            crt.sizeDelta = new Vector2(10f, 10f);
            var ci = chevron.AddComponent<Image>();
            ci.sprite = Sprites.Icon("chevron");
            ci.preserveAspect = true;
            ci.raycastTarget = false;
            ci.color = Palette.White(0.6f);
        }

        private TMPro.TMP_Dropdown _dropdown;
        private Text _switchLabel;

        /// <summary>The name shown for the dropdown's choice ("-" is Studio's free camera).</summary>
        private static string CameraName(TMPro.TMP_Dropdown d, int i)
        {
            if (d == null || i < 0 || i >= d.options.Count) return "Free camera";
            var t = d.options[i].text;
            return i == 0 || string.IsNullOrEmpty(t) || t == "-" ? "Free camera" : t;
        }

        private void UpdateSwitchLabel()
        {
            if (_switchLabel == null || _dropdown == null) return;
            var want = CameraName(_dropdown, _dropdown.value);
            if (Labels.FullText(_switchLabel) != want) _switchLabel.text = want;
        }

        private void OpenCameraMenu(RectTransform anchor)
        {
            var items = new List<GlassMenu.Item>();
            if (_dropdown != null)
                for (int i = 0; i < _dropdown.options.Count; i++)
                {
                    int index = i;
                    items.Add(new GlassMenu.Item
                    {
                        Label = CameraName(_dropdown, i),
                        Check = _dropdown.value == i,
                        Act = () => { if (_dropdown.value != index) _dropdown.value = index; else _dropdown.onValueChanged.Invoke(index); },
                    });
                }
            items.Add(GlassMenu.Item.Sep());
            items.Add(new GlassMenu.Item { Label = "Add camera to the scene", Act = AddSceneCamera });

            // Just under the button, left edges lined up.
            anchor.GetWorldCorners(_corners);
            var canvas = anchor.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]) + new Vector2(0f, -4f);
            GlassMenu.Show(screen, 190f, items);
        }

        /// <summary>The workspace's own "new camera" button.</summary>
        private static void AddSceneCamera()
        {
            var scene = GameObject.Find("StudioScene");
            var t = scene != null ? scene.transform.Find("Canvas Object List/Image Bar/Button Camera") : null;
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b != null) b.onClick.Invoke();
        }

        /// <summary>Set a rect's position (null = keep) and size, remembering Studio's for turning the theme off.</summary>
        private void Fit(RectTransform rt, Vector2? pos, Vector2 size)
        {
            if (rt == null) return;
            if (_fitted.Add(rt))
            {
                var p0 = rt.anchoredPosition;
                var s0 = rt.sizeDelta;
                _undo.Add(() => { if (rt != null) { rt.anchoredPosition = p0; rt.sizeDelta = s0; } });
            }
            if (pos.HasValue) rt.anchoredPosition = pos.Value;
            rt.sizeDelta = size;
        }

        private void LateUpdate()
        {
            if (_removed) return;
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.5f;
                Scan();
            }
            Centre();
        }

        /// <summary>Theme new slots and work out which rects make up the bar.</summary>
        private void Scan()
        {
            _items.Clear();
            if (_moarCamz == null && !_moarCamzFailed)
            {
                try { _moarCamz = MoarCamzTheme.TryApply(transform, this, _undo); }
                catch (System.Exception e)
                {
                    _moarCamzFailed = true;
                    StudioThemePlugin.Log.LogWarning("Couldn't style MoarCamz's controls: " + e);
                }
            }

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.name != "Button Load" && button.name != "Button Save") continue;
                int n;
                if (button.transform.parent == null || !int.TryParse(button.transform.parent.name, out n)) continue;

                bool load = button.name == "Button Load";
                var brt = (RectTransform)button.transform;
                if (!_fitted.Contains(brt))
                {
                    // Save strip 6 tall on top, load pill 22 tall under it: 30 in all (Studio: 12 + 4 + 32).
                    if (load) Fit(brt, new Vector2(0f, -8f), new Vector2(32f, 22f));
                    else Fit(brt, new Vector2(0f, 0f), new Vector2(32f, 6f));
                }
                var themed = ThemedButton.Attach(button, load ? ButtonKind.Slot : ButtonKind.Tab, null, null);
                if (themed == null) continue;
                if (!load)
                {
                    var slot = button.transform.parent;
                    themed.Tooltip = () => "Save view to " + (int.Parse(slot.name) + 1);
                }
                if (!_buttons.Contains(themed)) _buttons.Add(themed);
            }

            UpdateSlots();
            UpdateSwitchLabel();

            // MoarCamz's scrolling strip, or Studio's own slots.
            var scroll = GetComponentInChildren<ScrollRect>();
            if (scroll != null && scroll.isActiveAndEnabled)
            {
                // MoarCamz's strip is 48 tall: trim it to the row.
                var srt = (RectTransform)scroll.transform;
                if (_fitted.Add(srt))
                {
                    var min0 = srt.offsetMin;
                    _undo.Add(() => { if (srt != null) srt.offsetMin = min0; });
                }
                float want = srt.offsetMax.y - RowHeight;
                if (Mathf.Abs(srt.offsetMin.y - want) > 0.1f) srt.offsetMin = new Vector2(srt.offsetMin.x, want);
                // ...and end it after the last slot showing (it's sized for ten).
                if (float.IsNaN(_scrollRight0))
                {
                    _scrollRight0 = srt.offsetMax.x;
                    var max0 = srt.offsetMax;
                    _undo.Add(() => { if (srt != null) srt.offsetMax = max0; });
                }
                float right = Mathf.Min(_scrollRight0, SlotsRight(srt.offsetMin.x) + 2f);
                if (Mathf.Abs(srt.offsetMax.x - right) > 0.1f) srt.offsetMax = new Vector2(right, srt.offsetMax.y);
                _items.Add(scroll.viewport != null ? scroll.viewport : srt);
            }
            else
                foreach (var b in _buttons)
                    if (b != null && b.isActiveAndEnabled) _items.Add((RectTransform)b.transform);

            var selector = transform.Find(Selector) as RectTransform;
            if (selector != null && selector.gameObject.activeInHierarchy)
            {
                // The camera selector follows the last slot showing.
                float x = (scroll != null && scroll.isActiveAndEnabled ? ((RectTransform)scroll.transform).offsetMax.x : SlotsRight(0f)) + 8f;
                if (_slots.Count > 0) Fit(selector, new Vector2(x, selector.anchoredPosition.y), selector.sizeDelta);
                _items.Add(selector);
            }

            // Anything else on the top row (MoarCamz's add / remove / centre buttons).
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                int n;
                if (!child.gameObject.activeInHierarchy || child == selector || int.TryParse(child.name, out n)) continue;
                if (scroll != null && (child == scroll.transform || scroll.transform.IsChildOf(child))) continue;
                foreach (var b in child.GetComponentsInChildren<Button>())
                {
                    var brt = (RectTransform)b.transform;
                    if (OnTopRow(brt)) _items.Add(brt);
                }
            }

        }

        private readonly Vector3[] _corners = new Vector3[4];

        /// <summary>Show the first N slots (N = 3, the last one with a view saved, or what + asked for); hide the rest.</summary>
        private void UpdateSlots()
        {
            _slots.Clear();
            foreach (var b in _buttons)
            {
                int n;
                if (b == null || b.name != "Button Load" || b.transform.parent == null || !int.TryParse(b.transform.parent.name, out n)) continue;
                _slots.Add(new KeyValuePair<int, ThemedButton>(n, b));
            }
            _slots.Sort((a, b) => a.Key.CompareTo(b.Key));
            int lastUsed = -1;
            for (int i = 0; i < _slots.Count; i++) if (!_slots[i].Value.SlotEmpty) lastUsed = i;
            _shown = Mathf.Min(_slots.Count, Mathf.Max(MinSlots, Mathf.Max(lastUsed + 1, _revealed)));
            for (int i = 0; i < _slots.Count; i++)
            {
                var go = _slots[i].Value.transform.parent.gameObject;
                bool want = i < _shown;
                if (want && !go.activeSelf && _hiddenSlots.Remove(go)) go.SetActive(true);
                else if (!want && go.activeSelf)
                {
                    go.SetActive(false);
                    if (_hiddenSlots.Add(go)) _undo.Add(() => { if (go != null && _hiddenSlots.Remove(go)) go.SetActive(true); });
                }
            }
        }

        /// <summary>Right edge (bar coordinates) of the last slot showing, or <paramref name="fallback"/>.</summary>
        private float SlotsRight(float fallback)
        {
            float right = fallback;
            bool any = false;
            for (int i = 0; i < _slots.Count && i < _shown; i++)
            {
                var rt = (RectTransform)_slots[i].Value.transform;
                rt.GetWorldCorners(_corners);
                float x = _rt.InverseTransformPoint(_corners[2]).x;
                right = any ? Mathf.Max(right, x) : x;
                any = true;
            }
            return right;
        }

        /// <summary>MoarCamz's +: show one more slot. True if a hidden one was there to show (no new slot needed).</summary>
        internal bool RevealSlot()
        {
            bool hidden = _shown < _slots.Count;
            _revealed = _shown + 1;
            UpdateSlots();
            return hidden;
        }

        /// <summary>MoarCamz's -: hide the last slot showing if it's empty and more are hidden. True if handled here.</summary>
        internal bool HideSlot()
        {
            if (_shown >= _slots.Count) return false;   // nothing hidden: MoarCamz removes its last slot
            if (_shown > MinSlots && _slots[_shown - 1].Value.SlotEmpty) { _revealed = _shown - 1; UpdateSlots(); }
            return true;
        }

        private bool OnTopRow(RectTransform rt)
        {
            rt.GetWorldCorners(_corners);
            float top = _rt.InverseTransformPoint(_corners[1]).y;
            return top > -40f;   // the slot row sits 8 units below the top; MoarCamz's settings panel is far lower
        }

        /// <summary>Move the bar so its contents are centred on the screen's top middle.</summary>
        private void Centre()
        {
            bool any = false;
            float min = 0f, max = 0f;
            foreach (var rt in _items)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(_corners);
                for (int i = 0; i < 4; i++)
                {
                    float x = _rt.InverseTransformPoint(_corners[i]).x;
                    if (!any) { min = max = x; any = true; }
                    else { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
                }
            }
            if (!any) return;

            // The capsule: the items' width, the switches' height.
            var pos = new Vector2(min, RowTop);
            var size = new Vector2(max - min, RowHeight);
            if (_row.anchoredPosition != pos) _row.anchoredPosition = pos;
            if (_row.sizeDelta != size) _row.sizeDelta = size;

            // Child positions are relative to the bar, so this offset doesn't depend on where the bar is now.
            var target = new Vector2(-(min + max) / 2f, _origPos.y - Drop);
            if ((_rt.anchoredPosition - target).sqrMagnitude > 0.25f) _rt.anchoredPosition = target;
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var b in _buttons) if (b != null) b.Remove();
            _buttons.Clear();
            _undo.Run();
            if (_glass != null) DestroyImmediate(_glass);
            if (_rt != null) _rt.anchoredPosition = _origPos;
            Destroy(this);
        }
    }
}
