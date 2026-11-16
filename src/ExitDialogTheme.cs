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
            var card = yes.transform.parent as RectTransform;
            StudioThemePlugin.Log.LogDebug("Exit dialog: card " + (card != null ? card.name : "none") + " under " + (card != null && card.parent != null ? card.parent.name : "none"));
            if (card == null) return;

            // The dim behind: the full-screen image the card sits on.
            var dimRt = card.parent as RectTransform;
            var dim = dimRt != null ? dimRt.GetComponent<Image>() : null;
            if (dim != null && dimRt.anchorMin == Vector2.zero && dimRt.anchorMax == Vector2.one)
                ControlStyler.Swap(dim, dim.sprite, dim.type, new Color(0f, 0f, 0f, 0.45f), undo);

            var cardImage = card.GetComponent<Image>();
            if (cardImage != null) ControlStyler.Swap(cardImage, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
            Resize(card, new Vector2(340f, 132f), undo);

            // The question: the card's text that isn't a button's.
            Text titleText = null;
            foreach (var text in card.GetComponentsInChildren<Text>(true))
                if (text.GetComponentInParent<Button>() == null) { titleText = text; break; }
            if (titleText != null)
            {
                var font = titleText.font; var size = titleText.fontSize; var color = titleText.color;
                var best = titleText.resizeTextForBestFit;
                titleText.font = Fonts.Semibold;
                titleText.fontSize = 15;
                titleText.resizeTextForBestFit = false;
                titleText.color = Palette.White(0.95f);
                undo.Add(() => { if (titleText != null) { titleText.font = font; titleText.fontSize = size; titleText.color = color; titleText.resizeTextForBestFit = best; } });
                // Its box: the top of the card.
                var titleRt = titleText.transform.parent != card ? titleText.transform.parent as RectTransform : titleText.rectTransform;
                if (titleRt != null && titleRt.anchorMin == titleRt.anchorMax)
                {
                    var p0 = titleRt.anchoredPosition;
                    if (titleRt.anchorMin.y >= 0.99f) titleRt.anchoredPosition = new Vector2(p0.x, -26f);
                    undo.Add(() => { if (titleRt != null) titleRt.anchoredPosition = p0; });
                }
            }
            foreach (var tmp in card.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (tmp.GetComponentInParent<Button>() != null) continue;
                var c0 = tmp.color;
                tmp.color = Palette.White(0.95f);
                undo.Add(() => { if (tmp != null) tmp.color = c0; });
            }

            Button(yes, new Vector2(-70f, 34f), true, undo);
            Button(no, new Vector2(70f, 34f), false, undo);
        }

        private static void Button(Button button, Vector2 pos, bool primary, Undo undo)
        {
            var t = (RectTransform)button.transform;
            if (t.anchorMin == t.anchorMax)
            {
                var p0 = t.anchoredPosition; var s0 = t.sizeDelta;
                t.anchoredPosition = new Vector2(pos.x, t.anchorMin.y <= 0.01f ? pos.y : p0.y);
                t.sizeDelta = new Vector2(124f, 32f);
                undo.Add(() => { if (t != null) { t.anchoredPosition = p0; t.sizeDelta = s0; } });
            }
            // The game's own picture for the button (if it has one) goes; the theme draws the capsule.
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
