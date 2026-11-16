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

            // Parts another plugin copied from an already-themed panel (FK & IK copies Studio's FK page).
            RepairCopies(root, undo);

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
                if (image.name == "Image Background" && image.GetComponent<ThemedPanel>() == null && !InTemplate(image) && !Sprites.IsOurs(image.sprite))
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
            foreach (var input in root.GetComponentsInChildren<TMPro.TMP_InputField>(true))
                if (!InTemplate(input)) StyleTmpInput(input, undo);

            foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
                StyleDropdown(dropdown, undo);

            // Rows of On / Half / Off picture buttons become small sliders.
            var stateButtons = StateSlider.Build(root, undo);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (!headerButtons.Contains(button) && !stateButtons.Contains(button) && !InTemplate(button) && !IsListRow(button)) StyleButton(button, undo);

            // Labels last, so text inside themed buttons is already handled.
            foreach (var tmp in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (InParent<ThemedButton>(tmp) != null || InTemplate(tmp)) continue;
                if (InParent<TMPro.TMP_InputField>(tmp) != null) continue;   // done by StyleTmpInput
                bool header = tmp.transform.parent != null && Headers.Contains(tmp.transform.parent);
                bool title = tmp.name.Contains("Title");
                var label = ThemedLabel.Attach(tmp, header || title);
                if (label != null)
                {
                    undo.Add(label.Remove);
                    if (!header && !title && ShouldLeftAlign(tmp.rectTransform, (int)Labels.AnchorOf(tmp) % 3)) label.AlignLeft();
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
                if (InParent<Dropdown>(text) == null && InParent<Toggle>(text) == null) FitLegacySize(text, false, undo);
                if (text.raycastTarget && InParent<Selectable>(text) == null)
                {
                    // A plain label: clicks go through to the buttons it may overlap.
                    text.raycastTarget = false;
                    undo.Add(() => { if (text != null) text.raycastTarget = true; });
                }
                if (ShouldLeftAlign(text.rectTransform, (int)text.alignment % 3))
                {
                    var old = text.alignment;
                    text.alignment = (TextAnchor)((int)old / 3 * 3);
                    undo.Add(() => { if (text != null) text.alignment = old; });
                }
                InsetFromEdge(text, undo);
                FitLegacyLabel(text, undo);
            }
        }

        // ---------------------------------------------------------------- copies

        /// <summary>
        /// Another plugin copied a themed panel and put its own picture on the copy (FK & IK copies
        /// Studio's FK page, then paints "FK & IK" on it): glass again, and the copied title says the
        /// copy's name. Dead parts of themed controls that can't take over in a copy are cleared so the
        /// controls are themed afresh.
        /// </summary>
        private static void RepairCopies(Transform root, Undo undo)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name.StartsWith(ThemedButton.OwnPrefix) || Sprites.IsOurs(image.sprite)) continue;
                if (image.sprite != null && BakedArt.PanelSprites.Contains(image.sprite.name)) continue;   // Studio's own panel
                if (image.GetComponent<Button>() != null || image.GetComponent<ThemedPanel>() != null) continue;
                Text title = null;
                foreach (Transform c in image.transform)
                {
                    if (c.name != ThemedButton.OwnPrefix + "Caption") continue;
                    var t = c.GetComponent<Text>();
                    if (t != null && (title == null || t.rectTransform.anchoredPosition.y > title.rectTransform.anchoredPosition.y)) title = t;
                }
                if (title == null) continue;

                Swap(image, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
                var name = CopyTitle(image.name);
                if (name != null)
                {
                    var old = title.text;
                    title.text = name;
                    var t = title;
                    undo.Add(() => { if (t != null) t.text = old; });
                }
                var captions = new List<GameObject>();
                foreach (Transform c in image.transform)
                    if (c.name == ThemedButton.OwnPrefix + "Caption") captions.Add(c.gameObject);
                undo.Add(() => { foreach (var go in captions) if (go != null) Object.DestroyImmediate(go); });
            }

            foreach (var s in root.GetComponentsInChildren<StateSlider>(true)) s.ClearIfCopy();
            foreach (var f in root.GetComponentsInChildren<PanelFit>(true)) f.ClearIfCopy();
        }

        /// <summary>The copy's own name as a title ("FK & IK"), unless it's just "... (Clone)" or one of Studio's "00_FK" names.</summary>
        private static string CopyTitle(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains("(Clone)")) return null;
            if (name.Length > 3 && char.IsDigit(name[0]) && char.IsDigit(name[1]) && name[2] == '_') return null;
            return name.Trim();
        }

        /// <summary>
        /// A plugin's legacy one-line label that shrinks to fit (FK & IK's "Right Hand (FK)" next to
        /// "Hair (FK)"): one size like every other label, long text ending in "…" and scrolling on hover.
        /// </summary>
        private static void FitLegacyLabel(Text text, Undo undo)
        {
            if (!text.resizeTextForBestFit || text.transform.parent == null) return;
            // Only labels made while the game runs (other plugins' copies and controls). Studio's own,
            // loaded with the scene, stay where they are: plugins copy those and expect them unwrapped.
            if (text.GetInstanceID() > 0 || !StudioThemePlugin.ThemePlugins.Value) return;
            if (text.transform.parent.GetComponent<Marquee>() != null) return;
            if (text.transform.parent.GetComponent<LayoutGroup>() != null || text.GetComponent<LayoutElement>() != null) return;
            if (InParent<Button>(text) != null || InParent<Dropdown>(text) != null || InParent<InputField>(text) != null) return;
            foreach (var mb in text.GetComponents<MonoBehaviour>())
                if (mb != null && mb != text && mb.GetType().Name.StartsWith("TextSlide")) return;
            var rt = text.rectTransform;
            float h = rt.rect.height;
            if (h <= 1f || h > 30f) return;
            bool letters = false;
            foreach (var ch in text.text ?? "") if (char.IsLetter(ch)) { letters = true; break; }
            if (!letters) return;

            var parent = rt.parent;
            int index = rt.GetSiblingIndex();
            var aMin = rt.anchorMin; var aMax = rt.anchorMax; var pivot = rt.pivot; var oMin = rt.offsetMin; var oMax = rt.offsetMax;
            bool best = text.resizeTextForBestFit;
            int size = text.fontSize;
            var overflow = text.horizontalOverflow;
            var align = text.alignment;

            text.fontSize = Labels.SizeFor(h);
            var m = Marquee.Wrap(text);
            if (m == null) { text.fontSize = size; return; }
            var frame = m.gameObject;
            undo.Add(() =>
            {
                if (text == null) return;
                if (parent != null) { rt.SetParent(parent, false); rt.SetSiblingIndex(index); }
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.offsetMin = oMin; rt.offsetMax = oMax;
                text.resizeTextForBestFit = best;
                text.fontSize = size;
                text.horizontalOverflow = overflow;
                text.alignment = align;
                if (frame != null) Object.DestroyImmediate(frame);
            });
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
        internal static bool InTemplate(Component c)
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
            if (Sprites.IsOurs(box.sprite) || Sprites.IsOurs(mark.sprite)) return;   // a copy of a themed one

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
            if (image != null && Sprites.IsOurs(image.sprite)) return;   // a copy of a themed one
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

        /// <summary>
        /// A TextMeshPro field (numbers in Config, the gizmo panel, MoarCamz): rounded background, its own
        /// text recoloured where it is (not mirrored, so typing and the caret behave as before).
        /// </summary>
        private static void StyleTmpInput(TMPro.TMP_InputField input, Undo undo)
        {
            var image = input.image != null ? input.image : input.GetComponent<Image>();
            if (image != null && Sprites.IsOurs(image.sprite)) return;   // a copy of a themed one
            if (image != null) Swap(image, Sprites.Field, Image.Type.Sliced, Color.white, undo);
            Recolor(input.textComponent, Palette.White(0.95f), undo);
            Recolor(input.placeholder as TMPro.TMP_Text, Palette.White(0.35f), undo);
            float h = ((RectTransform)input.transform).rect.height;
            FitFieldText(input.textComponent, h, undo);
            FitFieldText(input.placeholder as TMPro.TMP_Text, h, undo);

            var sel = input.selectionColor;
            var caret = input.caretColor;
            var custom = input.customCaretColor;
            var accent = Palette.Accent;
            input.selectionColor = new Color(accent.r, accent.g, accent.b, 0.4f);
            input.customCaretColor = true;
            input.caretColor = Color.white;
            undo.Add(() => { if (input != null) { input.selectionColor = sel; input.caretColor = caret; input.customCaretColor = custom; } });
        }

        /// <summary>
        /// Field text centred in the field and no taller than it allows (Studio's numbers sat low and filled
        /// the box once the rounded background was on).
        /// </summary>
        private static void FitFieldText(TMPro.TMP_Text text, float fieldHeight, Undo undo)
        {
            if (text == null || fieldHeight <= 1f) return;
            var align = text.alignment;
            var auto = text.enableAutoSizing;
            var size = text.fontSize; var max = text.fontSizeMax; var min = text.fontSizeMin;
            float cap = Mathf.Clamp(Mathf.Round(fieldHeight * 0.6f), 10f, 15f);
            int v = (int)align;
            int horizontal = v & 0xFF;
            text.alignment = (TMPro.TextAlignmentOptions)(horizontal | 512);   // Middle
            if (auto) { text.fontSizeMax = Mathf.Min(max, cap); text.fontSizeMin = Mathf.Min(min, text.fontSizeMax); }
            else if (size > cap) text.fontSize = cap;
            undo.Add(() =>
            {
                if (text == null) return;
                text.alignment = align;
                text.enableAutoSizing = auto;
                text.fontSizeMax = max; text.fontSizeMin = min; text.fontSize = size;
            });
        }

        /// <summary>A legacy one-line label: system size (Studio's were up to 18 next to 13 elsewhere), centred vertically.</summary>
        private static void FitLegacySize(Text text, bool middle, Undo undo)
        {
            float h = text.rectTransform.rect.height;
            if (h <= 1f || h > 34f) return;
            var size = text.fontSize; var max = text.resizeTextMaxSize; var align = text.alignment;
            int cap = Labels.SizeFor(Mathf.Min(h, 22f));
            if (text.resizeTextForBestFit) text.resizeTextMaxSize = Mathf.Min(max, cap);
            else if (size > cap) text.fontSize = cap;
            if (middle) text.alignment = (TextAnchor)(3 + (int)align % 3);
            undo.Add(() => { if (text != null) { text.fontSize = size; text.resizeTextMaxSize = max; text.alignment = align; } });
        }

        /// <summary>
        /// A centred label at the left of a panel row (Config's "BGM", "Backg. Color"): left-aligned like
        /// every other row label. Labels inside toggles, buttons and titles that span the panel stay centred.
        /// </summary>
        private static bool ShouldLeftAlign(RectTransform rt, int column)
        {
            if (column != 1) return false;
            if (InParent<Toggle>(rt) != null || InParent<Button>(rt) != null || InParent<Selectable>(rt) != null) return false;
            var panel = PanelOf(rt);
            if (panel == null) return false;
            float gap = EdgeGap(rt);
            return gap < 60f && rt.rect.width < panel.rect.width * 0.5f;
        }

        private static void Recolor(TMPro.TMP_Text text, Color color, Undo undo)
        {
            if (text == null) return;
            var old = text.color;
            text.color = color;
            undo.Add(() => { if (text != null) text.color = old; });
        }

        private static void StyleDropdown(Dropdown dropdown, Undo undo)
        {
            var image = dropdown.image != null ? dropdown.image : dropdown.GetComponent<Image>();
            if (image != null && Sprites.IsOurs(image.sprite)) return;
            if (image != null) Swap(image, Sprites.Field, Image.Type.Sliced, Color.white, undo);
            if (dropdown.captionText != null)
            {
                StyleLegacyText(dropdown.captionText, Palette.White(0.95f), undo);
                FitLegacySize(dropdown.captionText, true, undo);
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
            // TextMeshPro text is recoloured where it is (not mirrored): the dropdown copies and re-reads
            // these labels itself, and extra objects in its items are best avoided.
            Recolor(dropdown.captionText, Palette.White(0.95f), undo);

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
            Recolor(dropdown.itemText, Palette.White(Palette.TextAlpha), undo);
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
                var text = Labels.Configure(go.AddComponent<Text>(), c.Title || c.Tint != null ? Fonts.Semibold : Fonts.Regular,
                    c.Left ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, c.Title ? 12 : 11, 8);
                text.text = c.Text;
                text.color = c.Tint != null ? Palette.Hex(c.Tint) : Palette.White(c.Title ? 0.85f : 0.6f);
                // At once, so re-applying in the same frame doesn't find it (and take the panel for a copy).
                undo.Add(() => { if (go != null) Object.DestroyImmediate(go); });
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

        /// <summary>A row of a list the theme styles as a list (its rows look like list rows, not buttons).</summary>
        private static bool IsListRow(Button button)
        {
            var content = button.transform.parent;
            if (content == null) return false;
            var scroll = InParent<ScrollRect>(content);
            return scroll != null && scroll.content == content && scroll.GetComponent<ThemedPanel>() != null;
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
