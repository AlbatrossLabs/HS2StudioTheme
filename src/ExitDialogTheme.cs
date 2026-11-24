using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The "Exit the game?" dialog (System › End, or closing the window): a glass card on a softer dim,
    /// the question in the system font, and two capsule buttons (the yes in the accent colour). Studio's
    /// texts stay where they are, restyled in place, so translation plugins keep working on them.
    /// </summary>
    internal static class ExitDialogTheme
    {
        private static Undo s_undo;
        private static readonly HashSet<int> Done = new HashSet<int>();

        /// <summary>With the theme: style the dialog now if it exists, and each time Studio makes or opens one.</summary>
        public static void Apply(Undo themeUndo)
        {
            s_undo = new Undo();
            Done.Clear();
            themeUndo.Add(Remove);
            StyleAll();
        }

        private static void Remove()
        {
            if (s_undo != null) s_undo.Run();
            s_undo = null;
            Done.Clear();
        }

        /// <summary>Every exit dialog in the scene not styled yet (Studio makes a new one when it's asked for).</summary>
        public static void StyleAll()
        {
            if (s_undo == null) return;
            foreach (var d in Resources.FindObjectsOfTypeAll<ExitDialog>())
                if (d != null && d.gameObject.scene.IsValid()) Style(d);
        }

        public static void Style(ExitDialog dialog)
        {
            if (s_undo == null || dialog == null || !Done.Add(dialog.GetInstanceID())) return;
            try { StyleOne(dialog, s_undo); }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style the exit dialog: " + e.Message); }
        }

        /// <summary>
        /// Studio's own yes / no scenes: "Exit Studio Neo V2?" (Studio.ExitScene, loaded when the window is
        /// closed or End is chosen) and its confirmations (Studio.CheckScene: delete, load, reset...).
        /// Each is a new scene every time, so each is styled as it starts.
        /// </summary>
        /// <summary>What the next confirmation asks, set by the button that opens it (Studio only passes a picture).</summary>
        internal static string PendingQuestion, PendingYes;
        internal static float PendingAt = -10f;

        public static void Ask(string question, string yes)
        {
            PendingQuestion = question; PendingYes = yes; PendingAt = Time.unscaledTime;
        }

        public static void StyleStudioScene(MonoBehaviour scene, string yesField, string noField, string question, Sprite questionPicture)
        {
            string yesLabel = "Yes", noLabel = "No";
            bool destructive = false;
            if (question == null && PendingQuestion != null && Time.unscaledTime - PendingAt < 3f)
            {
                question = PendingQuestion; yesLabel = PendingYes; noLabel = "Cancel"; destructive = true;
                PendingQuestion = null;
            }
            if (s_undo == null || scene == null || !Done.Add(scene.GetInstanceID())) return;
            try
            {
                var t = HarmonyLib.Traverse.Create(scene);
                var yes = t.Field(yesField).GetValue<Studio.VoiceNode>();
                var no = t.Field(noField).GetValue<Studio.VoiceNode>();
                if (yes == null || no == null || yes.buttonUI == null || no.buttonUI == null)
                {
                    StudioThemePlugin.Log.LogWarning(scene.GetType().Name + ": its buttons weren't found");
                    return;
                }
                BuildCard(scene, yes.buttonUI, no.buttonUI, question, questionPicture, yesLabel, noLabel, destructive, s_undo);
            }
            catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style " + scene.GetType().Name + ": " + e.Message); }
        }

        /// <summary>
        /// Studio's box is a picture (a bevelled grey frame with the question painted in, Japanese behind it)
        /// with the buttons laid over it. Everything of Studio's but the buttons is hidden and a glass card of
        /// the theme's own is drawn in the middle: the question as real text (or, for confirmations that only
        /// have a picture of their question, that picture), and Studio's buttons moved onto it.
        /// </summary>
        private static void BuildCard(MonoBehaviour scene, Button yes, Button no, string question, Sprite questionPicture, string yesLabel, string noLabel, bool destructive, Undo undo)
        {
            var canvas = yes.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            var holder = yes.transform.parent as RectTransform;
            while (holder != null && !no.transform.IsChildOf(holder)) holder = holder.parent as RectTransform;
            if (root == null || holder == null) return;
            // Its own scene's canvas only (never Studio's main UI).
            if (root.gameObject.scene != scene.gameObject.scene)
            {
                StudioThemePlugin.Log.LogWarning(scene.GetType().Name + ": its canvas is shared, left as it is");
                return;
            }
            StudioThemePlugin.Log.LogDebug(scene.GetType().Name + ": themed in " + root.name + " (scene " + root.gameObject.scene.name + ")");

            // Studio's pictures and texts go (buttons keep theirs for the theme to restyle).
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g.GetComponentInParent<Button>() != null || g.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                var rt = g.rectTransform;
                bool full = rt.rect.width >= root.rect.width * 0.95f && rt.rect.height >= root.rect.height * 0.95f;
                var c0 = g.color;
                g.color = full ? new Color(0f, 0f, 0f, 0.45f) : new Color(c0.r, c0.g, c0.b, 0f);   // a full-screen dim stays, softer
                if (full && g is Image) { var im = (Image)g; var sp = im.sprite; im.sprite = null; undo.Add(() => { if (im != null) im.sprite = sp; }); }
                undo.Add(() => { if (g != null) g.color = c0; });
            }

            // The card, centred on the screen, under the buttons.
            var cardGo = new GameObject(ThemedButton.OwnPrefix + "Dialog", typeof(RectTransform));
            var card = (RectTransform)cardGo.transform;
            card.SetParent(root, false);
            card.SetSiblingIndex(holder.IsChildOf(root) ? RootIndex(root, holder) : root.childCount - 1);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(CardW, CardH);
            var glass = cardGo.AddComponent<Image>();
            glass.sprite = Sprites.Panel; glass.type = Image.Type.Sliced; glass.raycastTarget = false;
            undo.Add(() => { if (cardGo != null) Object.Destroy(cardGo); });

            if (!string.IsNullOrEmpty(question))
            {
                var textGo = new GameObject(ThemedButton.OwnPrefix + "Question", typeof(RectTransform));
                var trt = (RectTransform)textGo.transform;
                trt.SetParent(card, false);
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
                trt.anchoredPosition = new Vector2(0f, -40f);
                trt.sizeDelta = new Vector2(CardW - 32f, 30f);
                var text = textGo.AddComponent<Text>();
                text.font = Fonts.Semibold; text.fontSize = 15; text.color = Palette.White(0.95f);
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
                text.text = question;
            }
            else if (questionPicture != null)
            {
                var picGo = new GameObject(ThemedButton.OwnPrefix + "Question", typeof(RectTransform));
                var prt = (RectTransform)picGo.transform;
                prt.SetParent(card, false);
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
                prt.anchoredPosition = new Vector2(0f, -42f);
                prt.sizeDelta = new Vector2(CardW - 40f, 44f);
                var pic = picGo.AddComponent<Image>();
                pic.sprite = questionPicture; pic.preserveAspect = true; pic.raycastTarget = false;
            }

            // Apple order: Cancel on the left, the action on the right (red when it destroys something).
            OnCard(no, card, new Vector2(destructive ? -70f : 70f, -34f), false, noLabel, false, undo);
            OnCard(yes, card, new Vector2(destructive ? 70f : -70f, -34f), true, yesLabel, destructive, undo);
        }

        /// <summary>Which of the root's children holds <paramref name="t"/>: the card goes just before it.</summary>
        private static int RootIndex(Transform root, Transform t)
        {
            while (t.parent != root) t = t.parent;
            return t.GetSiblingIndex();
        }

        /// <summary>A button moved onto the card (by world position, wherever it sits), as a capsule with a real label.</summary>
        private static void OnCard(Button button, RectTransform card, Vector2 offset, bool primary, string label, bool destructive, Undo undo)
        {
            var t = (RectTransform)button.transform;
            var a0 = t.anchorMin; var a1 = t.anchorMax; var pv = t.pivot; var p0 = t.anchoredPosition; var s0 = t.sizeDelta;
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f);
            t.sizeDelta = new Vector2(124f * card.lossyScale.x / Mathf.Max(0.0001f, t.lossyScale.x), 32f * card.lossyScale.y / Mathf.Max(0.0001f, t.lossyScale.y));
            t.position = card.TransformPoint(offset);
            undo.Add(() => { if (t != null) { t.anchorMin = a0; t.anchorMax = a1; t.pivot = pv; t.anchoredPosition = p0; t.sizeDelta = s0; } });

            // Studio's labels are painted into the button picture or a text that may not mirror: the theme's own.
            foreach (var g in t.GetComponentsInChildren<Graphic>(true))
            {
                if (g.gameObject == t.gameObject || g.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                var c0 = g.color;
                g.color = new Color(c0.r, c0.g, c0.b, 0f);
                undo.Add(() => { if (g != null) g.color = c0; });
            }
            var themed = ThemedButton.Attach(button, ButtonKind.Push, null, label);
            if (themed == null) return;
            if (destructive) themed.EmphasizeDestructive(); else themed.Emphasize(primary);
            undo.Add(themed.Remove);
        }

        private static void StyleOne(ExitDialog dialog, Undo undo)
        {
            // Found through the dialog's own fields, not by path (Studio's copy may be laid out differently).
            var t = HarmonyLib.Traverse.Create(dialog);
            var yes = t.Field("_yes").GetValue<Button>();
            var no = t.Field("_no").GetValue<Button>();
            if (yes == null || no == null)
            {
                StudioThemePlugin.Log.LogWarning("Exit dialog: its buttons weren't found");
                return;
            }
            StyleCard("Exit dialog", yes, no, undo);
        }

        private const float CardW = 340f, CardH = 132f;

        private static void StyleCard(string what, Button yes, Button no, Undo undo)
        {
            // The card: the nearest parent holding both buttons.
            var card = yes.transform.parent as RectTransform;
            while (card != null && !no.transform.IsChildOf(card)) card = card.parent as RectTransform;
            StudioThemePlugin.Log.LogDebug(what + ": card " + (card != null ? card.name : "none") + " under " + (card != null && card.parent != null ? card.parent.name : "none"));
            if (card == null) return;

            // The dim behind: a full-screen image the card sits on.
            var dimRt = card.parent as RectTransform;
            var dim = dimRt != null ? dimRt.GetComponent<Image>() : null;
            if (dim != null && dimRt.anchorMin == Vector2.zero && dimRt.anchorMax == Vector2.one)
                ControlStyler.Swap(dim, dim.sprite, dim.type, new Color(0f, 0f, 0f, 0.45f), undo);

            var cardImage = card.GetComponent<Image>();
            if (cardImage != null) ControlStyler.Swap(cardImage, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
            else
            {
                // No picture of its own (the frame is a child): glass behind everything in the card.
                var bg = new GameObject(ThemedButton.OwnPrefix + "Dialog Glass", typeof(RectTransform));
                var bgRt = (RectTransform)bg.transform;
                bgRt.SetParent(card, false);
                bgRt.SetAsFirstSibling();
                bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
                var img = bg.AddComponent<Image>();
                img.sprite = Sprites.Panel; img.type = Image.Type.Sliced; img.raycastTarget = false;
                undo.Add(() => { if (bg != null) Object.Destroy(bg); });
            }
            if (card.anchorMin == card.anchorMax) Resize(card, new Vector2(CardW, CardH), undo);

            // Studio's other pictures in the card (bevelled frames, a title strip) give way to the glass.
            foreach (var image in card.GetComponentsInChildren<Image>(true))
            {
                if (image == cardImage || image.GetComponentInParent<Button>() != null || image.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                if (image.sprite == null || Sprites.IsOurs(image.sprite)) continue;
                // Only frames and backgrounds (most of the card); a picture of the question stays.
                var r = image.rectTransform.rect;
                if (r.width < card.rect.width * 0.8f || r.height < card.rect.height * 0.6f) continue;
                var c0 = image.color;
                image.color = new Color(c0.r, c0.g, c0.b, 0f);
                undo.Add(() => { if (image != null) image.color = c0; });
            }

            // The question: the card's text that isn't a button's, near the top.
            Graphic question = null;
            foreach (var text in card.GetComponentsInChildren<Text>(true))
                if (text.GetComponentInParent<Button>() == null)
                {
                    question = text;
                    var font = text.font; var size = text.fontSize; var color = text.color; var best = text.resizeTextForBestFit; var align = text.alignment;
                    text.font = Fonts.Semibold;
                    text.fontSize = 15;
                    text.resizeTextForBestFit = false;
                    text.color = Palette.White(0.95f);
                    text.alignment = TextAnchor.MiddleCenter;
                    undo.Add(() => { if (text != null) { text.font = font; text.fontSize = size; text.color = color; text.resizeTextForBestFit = best; text.alignment = align; } });
                    break;
                }
            if (question == null)
                foreach (var tmp in card.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (tmp.GetComponentInParent<Button>() != null) continue;
                    var label = ThemedLabel.Attach(tmp, true, 15, 0.95f);
                    if (label != null) undo.Add(label.Remove);
                    question = tmp;
                    break;
                }
            StudioThemePlugin.Log.LogDebug(what + ": question " + (question != null ? question.GetType().Name + " '" + question.name + "'" : "none (a picture?)"));
            if (question != null)
            {
                var qrt = question.rectTransform;
                // Its box: the top of the card, full width.
                if (qrt.parent != card && qrt.parent is RectTransform && qrt.parent != yes.transform && qrt.parent.parent == card) qrt = (RectTransform)qrt.parent;
                Place(qrt, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(CardW - 32f, 30f), undo);
            }

            Button(yes, new Vector2(-70f, 34f), true, undo);
            Button(no, new Vector2(70f, 34f), false, undo);
        }

        /// <summary>Anchors, pivot, position and size at once, all put back on undo.</summary>
        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Undo undo)
        {
            var a0 = rt.anchorMin; var a1 = rt.anchorMax; var pv = rt.pivot; var p0 = rt.anchoredPosition; var s0 = rt.sizeDelta;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            undo.Add(() => { if (rt != null) { rt.anchorMin = a0; rt.anchorMax = a1; rt.pivot = pv; rt.anchoredPosition = p0; rt.sizeDelta = s0; } });
        }

        private static void Button(Button button, Vector2 pos, bool primary, Undo undo)
        {
            var t = (RectTransform)button.transform;
            // A button inside a holder of its own (only it in there) moves with the holder.
            var holder = t.parent as RectTransform;
            bool own = holder != null && holder.GetComponentsInChildren<Button>(true).Length == 1 && holder.GetComponent<Button>() == null;
            if (own)
            {
                Place(holder, new Vector2(0.5f, 0f), pos, new Vector2(124f, 32f), undo);
                Place(t, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 32f), undo);
            }
            else Place(t, new Vector2(0.5f, 0f), pos, new Vector2(124f, 32f), undo);
            // The game's own picture for the button goes; the theme draws the capsule.
            var themed = ThemedButton.Attach(button, ButtonKind.Push, null, null);
            if (themed == null) return;
            themed.Emphasize(primary);
            undo.Add(themed.Remove);
            var label = t.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                var size = label.fontSize; var best = label.resizeTextForBestFit;
                label.fontSize = 13;
                label.resizeTextForBestFit = false;
                undo.Add(() => { if (label != null) { label.fontSize = size; label.resizeTextForBestFit = best; } });
            }
        }

        private static void Resize(RectTransform rt, Vector2 size, Undo undo)
        {
            var s0 = rt.sizeDelta;
            rt.sizeDelta = size;
            undo.Add(() => { if (rt != null) rt.sizeDelta = s0; });
        }
    }
}
