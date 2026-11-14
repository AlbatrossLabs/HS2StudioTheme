using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Restyles every standard control under one of Studio's settings panels: panel backgrounds,
    /// section headers, check boxes and radio buttons, sliders, text fields, dropdowns, buttons and
    /// labels. Each change is recorded in an <see cref="Undo"/> so the panel can be put back exactly.
    /// Positions and sizes are never changed.
    /// </summary>
    internal static class ControlStyler
    {
        private const string HeaderSprite = "sp_sn_31_00_10";
        private static readonly HashSet<Transform> Headers = new HashSet<Transform>();

        public static void StylePanel(Transform root, Undo undo)
        {
            if (root == null) return;
            _root = root;
            Headers.Clear();

            // Studio's panel pictures, by name, before anything is swapped (some have text painted in).
            var panelImages = new List<KeyValuePair<Image, Sprite>>();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && BakedArt.PanelSprites.Contains(image.sprite.name) && !InTemplate(image)
                    && image.GetComponent<Button>() == null)
                    panelImages.Add(new KeyValuePair<Image, Sprite>(image, image.sprite));

            // Panels: scrolling lists and plain backgrounds.
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (IsDropdownPart(scroll.transform)) continue;
                var panel = ThemedPanel.Attach(scroll);
                if (panel != null) undo.Add(panel.Remove);
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name == "Image Background" && image.GetComponent<ThemedPanel>() == null && !InTemplate(image))
                    Swap(image, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
            foreach (var kv in panelImages)
            {
                var image = kv.Key;
                if (image.GetComponent<ThemedPanel>() == null && image.sprite != Sprites.Panel)
                    Swap(image, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
                BakedArt.Caption[] captions;
                if (BakedArt.PanelCaptions.TryGetValue(kv.Value.name, out captions))
                    AddCaptions(image, captions, kv.Value, undo);
            }

            // Arrow pictures Studio swaps to show a state (sort direction): line chevrons that follow.
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponent<Button>() != null || InTemplate(image)) continue;
                var follower = IconFollower.Attach(image);
                if (follower != null) undo.Add(follower.Remove);
            }

            var headerButtons = new HashSet<Button>();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                StyleHeader(image, headerButtons, undo);

            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
                if (!IsDropdownPart(toggle.transform) && !InTemplate(toggle)) StyleToggle(toggle, undo);

            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                if (InTemplate(slider)) continue;
                var s = ThemedSlider.Attach(slider);
                if (s != null) undo.Add(s.Remove);
            }

            foreach (var input in root.GetComponentsInChildren<InputField>(true))
                if (!InTemplate(input)) StyleInput(input, undo);

            foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
                StyleDropdown(dropdown, undo);

            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (!headerButtons.Contains(button) && !InTemplate(button)) StyleButton(button, undo);

            // Labels last, so text inside themed buttons is already handled.
            foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (InParent<ThemedButton>(tmp) != null || InTemplate(tmp)) continue;
                bool header = tmp.transform.parent != null && Headers.Contains(tmp.transform.parent);
                bool title = tmp.name.Contains("Title");
                var label = ThemedLabel.Attach(tmp, header || title);
                if (label != null)
                {
                    undo.Add(label.Remove);
                    float gap = EdgeGap(tmp.rectTransform);
                    if (gap < 8f && Labels.AnchorOf(tmp) == TextAnchor.MiddleLeft || gap < 8f && Labels.AnchorOf(tmp) == TextAnchor.UpperLeft)
                        label.Inset(10f - gap);
                }
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                if (InParent<ThemedButton>(text) != null || InTemplate(text)) continue;
                if (InParent<InputField>(text) != null) continue;   // done by StyleInput
                StyleLegacyText(text, Palette.White(Palette.TextAlpha), undo);
                InsetFromEdge(text, undo);
            }
        }

        private static Transform _root;

        /// <summary>
        /// Like GetComponentInParent, but also on inactive objects. (Unity 2018's version skips them, and
        /// most of Studio's panels are inactive when the theme is applied.)
        /// </summary>
        internal static T InParent<T>(Component c) where T : Component
        {
            for (var t = c.transform; t != null; t = t.parent)
            {
                var found = t.GetComponent<T>();
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Hidden objects parked under a list for the game to copy (Logo's "node"). Left as they are,
        /// so each copy comes out plain and gets themed as a list row when it appears.
        /// </summary>
        private static bool InTemplate(Component c)
        {
            for (var t = c.transform; t != null && t != _root; t = t.parent)
            {
                if (t.gameObject.activeSelf || t.parent == null) continue;
                // Hidden children of a list are templates, except whole panels that happen to sit under
                // a list object (Kinematics keeps FK / IK / Face... next to its own list).
                if (t.parent.GetComponent<ScrollRect>() != null && !IsPanelPage(t)) return true;
                // A hidden list node outside any list (Save pose's "Button"): also a template.
                // (Hidden nodes inside a list's content are pooled rows, not templates.)
                if (t.parent.GetComponent<LayoutGroup>() == null && IsListNode(t)) return true;
            }
            return false;
        }

        private static bool IsPanelPage(Transform t)
        {
            var image = t.GetComponent<Image>();
            if (image != null && image.sprite != null && BakedArt.PanelSprites.Contains(image.sprite.name)) return true;
            var rt = t as RectTransform;
            return rt != null && rt.rect.height > 60f && t.childCount > 2 && t.GetComponent<Button>() == null;
        }

        private static bool IsListNode(Transform t)
        {
            foreach (var mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                var n = mb.GetType().Name;
                if (n == "StudioNode" || n == "ListNode") return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- pieces

        private static bool IsHeader(Transform t)
        {
            // Section headers (Screen Effect's "Image Color Grading", State's "Image Clothing Details"...)
            // all use Studio's header bar picture.
            var image = t.GetComponent<Image>();
            return image != null && image.sprite != null && image.sprite.name == HeaderSprite;
        }

        private static void StyleHeader(Image image, HashSet<Button> headerButtons, Undo undo)
        {
            var t = image.transform;
            if (!IsHeader(t) || image.GetComponent<ThemedPanel>() != null || InTemplate(image)) return;
            Headers.Add(t);
            Swap(image, Sprites.Row, Image.Type.Sliced, Palette.White(Palette.HeaderAlpha), undo);

            var button = t.GetComponentInChildren<Button>(true);
            if (button == null) return;
            headerButtons.Add(button);

            // The section a header folds is the object right after it.
            int i = t.GetSiblingIndex();
            var section = i + 1 < t.parent.childCount ? t.parent.GetChild(i + 1).gameObject : null;
            var chevron = DisclosureChevron.Attach(button, section);
            if (chevron != null) undo.Add(chevron.Remove);
        }

        private static void StyleToggle(Toggle toggle, Undo undo)
        {
            var box = toggle.targetGraphic as Image;
            var mark = toggle.graphic as Image;
            if (box == null || mark == null) return;

            // Only check-box-sized toggles; anything bigger is a tab or picture and keeps its look.
            var r = ((RectTransform)box.transform).rect;
            if (r.width > 26f || r.height > 26f) return;

            bool radio = toggle.group != null || (box.sprite != null && box.sprite.name.StartsWith("radio"));
            Swap(box, radio ? Sprites.RadioOff : Sprites.CheckOff, Image.Type.Simple, Color.white, undo);
            Swap(mark, radio ? Sprites.RadioOn : Sprites.CheckOn, Image.Type.Simple, Color.white, undo);
            Set(box, true, (img, v) => img.preserveAspect = v, img => img.preserveAspect, undo);
            Set(mark, true, (img, v) => img.preserveAspect = v, img => img.preserveAspect, undo);

            // The tick covers the box, so it has to be the same size as the box.
            var mrt = (RectTransform)mark.transform;
            if (mrt.parent == box.transform)
            {
                var oldMin = mrt.anchorMin; var oldMax = mrt.anchorMax; var oldOMin = mrt.offsetMin; var oldOMax = mrt.offsetMax;
                mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
                mrt.offsetMin = mrt.offsetMax = Vector2.zero;
                undo.Add(() => { if (mrt != null) { mrt.anchorMin = oldMin; mrt.anchorMax = oldMax; mrt.offsetMin = oldOMin; mrt.offsetMax = oldOMax; } });
            }
        }

        private static void StyleInput(InputField input, Undo undo)
        {
            var image = input.image != null ? input.image : input.GetComponent<Image>();
            if (image != null) Swap(image, Sprites.Field, Image.Type.Sliced, Color.white, undo);

            if (input.textComponent != null)
                StyleLegacyText(input.textComponent, Palette.White(0.95f), undo);
            var placeholder = input.placeholder as Text;
            if (placeholder != null) StyleLegacyText(placeholder, Palette.White(0.35f), undo);

            var sel = input.selectionColor;
            var caret = input.caretColor;
            var custom = input.customCaretColor;
            var accent = Palette.Accent;
            input.selectionColor = new Color(accent.r, accent.g, accent.b, 0.4f);
            input.customCaretColor = true;
            input.caretColor = Color.white;
            undo.Add(() => { if (input != null) { input.selectionColor = sel; input.caretColor = caret; input.customCaretColor = custom; } });
        }

        private static void StyleDropdown(Dropdown dropdown, Undo undo)
        {
            var image = dropdown.image != null ? dropdown.image : dropdown.GetComponent<Image>();
            if (image != null) Swap(image, Sprites.Field, Image.Type.Sliced, Color.white, undo);
            if (dropdown.captionText != null) StyleLegacyText(dropdown.captionText, Palette.White(0.95f), undo);

            var arrow = dropdown.transform.Find("Arrow");
            var arrowImage = arrow != null ? arrow.GetComponent<Image>() : null;
            if (arrowImage != null)
            {
                Swap(arrowImage, Sprites.Icon("chevron"), Image.Type.Simple, Palette.White(0.7f), undo);
                Set(arrowImage, true, (img, v) => img.preserveAspect = v, img => img.preserveAspect, undo);
                var art = (RectTransform)arrow;
                var oldSize = art.sizeDelta;
                art.sizeDelta = new Vector2(12f, 12f);
                undo.Add(() => { if (art != null) art.sizeDelta = oldSize; });
            }

            // The popup list is copied from the template each time it opens, so style the template.
            var template = dropdown.template;
            if (template == null) return;
            var templateImage = template.GetComponent<Image>();
            if (templateImage != null) Swap(templateImage, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
            var scroll = template.GetComponent<ScrollRect>();
            if (scroll != null && scroll.verticalScrollbar != null)
            {
                var track = scroll.verticalScrollbar.GetComponent<Image>();
                if (track != null) Swap(track, track.sprite, track.type, Palette.White(0f), undo);
                var thumb = scroll.verticalScrollbar.handleRect != null ? scroll.verticalScrollbar.handleRect.GetComponent<Image>() : null;
                if (thumb != null) Swap(thumb, Sprites.ScrollThumb, Image.Type.Sliced, Palette.White(0.35f), undo);
            }
            if (dropdown.itemText != null) StyleLegacyText(dropdown.itemText, Palette.White(Palette.TextAlpha), undo);

            var item = template.GetComponentInChildren<Toggle>(true);
            if (item != null)
            {
                var bg = item.targetGraphic as Image;
                if (bg != null)
                {
                    Swap(bg, Sprites.Row, Image.Type.Sliced, Color.white, undo);
                    var colors = item.colors;
                    var themed = colors;
                    themed.normalColor = Palette.White(0f);
                    themed.highlightedColor = Palette.White(Palette.HoverAlpha);
                    themed.pressedColor = Palette.White(Palette.PressAlpha);
                    themed.disabledColor = Palette.White(0f);
                    themed.colorMultiplier = 1f;
                    item.colors = themed;
                    undo.Add(() => { if (item != null) item.colors = colors; });
                }
                var check = item.graphic as Image;
                if (check != null)
                {
                    Swap(check, Sprites.Icon("check"), Image.Type.Simple, Palette.Accent, undo);
                    Set(check, true, (img, v) => img.preserveAspect = v, img => img.preserveAspect, undo);
                }
            }
        }

        private static void StyleButton(Button button, Undo undo)
        {
            if (button.GetComponent<ThemedButton>() != null || button.GetComponent<DisclosureChevron>() != null) return;
            var image = button.image;
            if (image == null) return;

            bool hasText = button.GetComponentInChildren<TMPro.TMP_Text>(true) != null
                           || button.GetComponentInChildren<Text>(true) != null;
            string sprite = image.sprite != null ? image.sprite.name : "";

            if (hasText)
            {
                var b = ThemedButton.Attach(button, ButtonKind.Push, null, null);
                if (b != null) undo.Add(b.Remove);
            }
            else if (BakedArt.Buttons.ContainsKey(sprite))
            {
                // Studio's label or icon is painted into the picture: draw it ourselves, and follow the
                // picture if Studio swaps it (expand/collapse arrows, repeat on/off).
                var baked = BakedArt.Buttons[sprite];
                var b = ThemedButton.Attach(button, ButtonKind.Push, baked.Icon, baked.Label);
                if (b != null) { b.MarkBaked(); undo.Add(b.Remove); }
            }
            else if (button.name.Contains("Default"))
            {
                var b = ThemedButton.Attach(button, ButtonKind.Push, "reset", null);
                if (b != null) { b.Tooltip = () => "Reset to default"; undo.Add(b.Remove); }
            }
            else if (IsSwatch(button))
            {
                // Studio shows the chosen colour by tinting this image, so only the shape changes.
                var oldSprite = image.sprite;
                var oldType = image.type;
                image.sprite = Sprites.Swatch;
                image.type = Image.Type.Sliced;
                undo.Add(() => { if (image != null) { image.sprite = oldSprite; image.type = oldType; } });
            }
            // Any other picture button keeps its own picture.
        }

        /// <summary>A TextMeshPro dropdown (Studio's camera selector): field, chevron, and a themed popup list.</summary>
        internal static void StyleTmpDropdown(TMPro.TMP_Dropdown dropdown, Undo undo)
        {
            var image = dropdown.image != null ? dropdown.image : dropdown.GetComponent<Image>();
            if (image != null) Swap(image, Sprites.Field, Image.Type.Sliced, Color.white, undo);
            if (dropdown.captionText != null)
            {
                var caption = ThemedLabel.Attach(dropdown.captionText, false, 12, 0.95f);
                if (caption != null) undo.Add(caption.Remove);
            }

            var arrow = dropdown.transform.Find("Arrow");
            var arrowImage = arrow != null ? arrow.GetComponent<Image>() : null;
            if (arrowImage != null)
            {
                Swap(arrowImage, Sprites.Icon("chevron"), Image.Type.Simple, Palette.White(0.7f), undo);
                Set(arrowImage, true, (img, v) => img.preserveAspect = v, img => img.preserveAspect, undo);
                var art = (RectTransform)arrow;
                var oldSize = art.sizeDelta;
                art.sizeDelta = new Vector2(12f, 12f);
                undo.Add(() => { if (art != null) art.sizeDelta = oldSize; });
            }

            var template = dropdown.template;
            if (template == null) return;
            var templateImage = template.GetComponent<Image>();
            if (templateImage != null) Swap(templateImage, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
            var scroll = template.GetComponent<ScrollRect>();
            if (scroll != null && scroll.verticalScrollbar != null)
            {
                var track = scroll.verticalScrollbar.GetComponent<Image>();
                if (track != null) Swap(track, track.sprite, track.type, Palette.White(0f), undo);
                var thumb = scroll.verticalScrollbar.handleRect != null ? scroll.verticalScrollbar.handleRect.GetComponent<Image>() : null;
                if (thumb != null) Swap(thumb, Sprites.ScrollThumb, Image.Type.Sliced, Palette.White(0.35f), undo);
            }
            // Items are copied from the template when the list opens; the label's state is serialised,
            // so each copy shows its own text in the system font.
            if (dropdown.itemText != null)
            {
                var item = ThemedLabel.Attach(dropdown.itemText, false, 12, Palette.TextAlpha);
                if (item != null) undo.Add(item.Remove);
            }
            var toggle = template.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                var bg = toggle.targetGraphic as Image;
                if (bg != null)
                {
                    Swap(bg, Sprites.Row, Image.Type.Sliced, Color.white, undo);
                    var colors = toggle.colors;
                    var themed = colors;
                    themed.normalColor = Palette.White(0f);
                    themed.highlightedColor = Palette.White(Palette.HoverAlpha);
                    themed.pressedColor = Palette.White(Palette.PressAlpha);
                    themed.disabledColor = Palette.White(0f);
                    themed.colorMultiplier = 1f;
                    toggle.colors = themed;
                    undo.Add(() => { if (toggle != null) toggle.colors = colors; });
                }
                var check = toggle.graphic as Image;
                if (check != null)
                {
                    Swap(check, Sprites.Icon("check"), Image.Type.Simple, Palette.Accent, undo);
                    Set(check, true, (img, v) => img.preserveAspect = v, img => img.preserveAspect, undo);
                }
            }
        }

        private static bool IsSwatch(Button button)
        {
            if (button.image != null && button.image.sprite != null && button.image.sprite.name == "sp_sn_25_00_01") return true;
            if (button.name.Contains("Color")) return true;
            var parent = button.transform.parent;
            return parent != null && parent.name.Contains("Color") && button.name == "Button";
        }

        /// <summary>
        /// How far a label starts from the left edge of the glass panel it sits on. Studio's labels
        /// often start right at the panel edge (its old pictures had a frame); on glass they need room.
        /// </summary>
        private static float EdgeGap(RectTransform rt)
        {
            var panel = PanelOf(rt);
            if (panel == null) return 100f;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float left = panel.InverseTransformPoint(corners[0]).x;
            return left - panel.rect.xMin;
        }

        private static RectTransform PanelOf(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                var img = p.GetComponent<Image>();
                if (img != null && img.sprite == Sprites.Panel) return (RectTransform)p;
            }
            return null;
        }

        /// <summary>Move a left-aligned legacy label that touches the panel edge 10 units in (its right edge stays).</summary>
        private static void InsetFromEdge(Text text, Undo undo)
        {
            var a = text.alignment;
            if (a != TextAnchor.MiddleLeft && a != TextAnchor.UpperLeft && a != TextAnchor.LowerLeft) return;
            var rt = text.rectTransform;
            if (rt.anchorMin != rt.anchorMax) return;   // only free-standing labels, not stretched ones
            float gap = EdgeGap(rt);
            if (gap >= 8f) return;
            float shift = 10f - gap;
            var pos = rt.anchoredPosition;
            var size = rt.sizeDelta;
            rt.anchoredPosition = new Vector2(pos.x + shift * (1f - rt.pivot.x), pos.y);
            rt.sizeDelta = new Vector2(Mathf.Max(20f, size.x - shift), size.y);
            undo.Add(() => { if (rt != null) { rt.anchoredPosition = pos; rt.sizeDelta = size; } });
        }

        /// <summary>Write out the text Studio painted into a panel picture, at the same place.</summary>
        private static void AddCaptions(Image image, BakedArt.Caption[] captions, Sprite original, Undo undo)
        {
            var rt = (RectTransform)image.transform;
            var native = original.rect.size;
            // Captions are in the original picture's pixels. A stretched picture scales them with the
            // panel; a 9-sliced one keeps its title band at the top at its own size.
            bool sliced = original.border != Vector4.zero;
            float w = rt.rect.width, h = rt.rect.height;
            float sx = w > 1f && native.x > 1f ? w / native.x : 1f;
            float sy = !sliced && h > 1f && native.y > 1f ? h / native.y : 1f;

            foreach (var c in captions)
            {
                var go = new GameObject(ThemedButton.OwnPrefix + "Caption", typeof(RectTransform));
                go.layer = image.gameObject.layer;
                go.transform.SetParent(rt, false);
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                var crt = (RectTransform)go.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                bool fullWidth = c.X0 <= 0f && c.X1 >= native.x;
                float x0 = fullWidth ? 0f : c.X0 * sx, x1 = fullWidth ? w : c.X1 * sx;
                crt.anchoredPosition = new Vector2(x0 + (c.Left ? 0f : 4f), -c.Y0 * sy);
                crt.sizeDelta = new Vector2(x1 - x0 - (c.Left ? 0f : 8f), Mathf.Max(14f, (c.Y1 - c.Y0) * sy));
                var text = Labels.Configure(go.AddComponent<Text>(), c.Title ? Fonts.Semibold : Fonts.Regular,
                    c.Left ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, c.Title ? 12 : 11, 8);
                text.text = c.Text;
                text.color = Palette.White(c.Title ? 0.85f : 0.6f);
                undo.Add(() => { if (go != null) Object.Destroy(go); });
            }
        }

        private static void StyleLegacyText(Text text, Color color, Undo undo)
        {
            var font = text.font;
            var old = text.color;
            text.font = Fonts.Regular;
            text.color = color;
            undo.Add(() => { if (text != null) { text.font = font; text.color = old; } });
        }

        private static bool IsDropdownPart(Transform t)
        {
            var dd = InParent<Dropdown>(t);
            return dd != null && dd.template != null && t.IsChildOf(dd.template);
        }

        // ---------------------------------------------------------------- recorded edits

        internal static void Swap(Image image, Sprite sprite, Image.Type type, Color color, Undo undo)
        {
            if (image == null || sprite == null) return;
            var oldSprite = image.sprite;
            var oldType = image.type;
            var oldColor = image.color;
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            undo.Add(() => { if (image != null) { image.sprite = oldSprite; image.type = oldType; image.color = oldColor; } });
        }

        internal static void Set<T>(Image image, T value, System.Action<Image, T> set, System.Func<Image, T> get, Undo undo)
        {
            var old = get(image);
            set(image, value);
            undo.Add(() => { if (image != null) set(image, old); });
        }
    }
}
