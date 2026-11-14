using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// The three players Sound opens (BGM, environment sound, external sound): glass panel with a
    /// title, transport buttons with line icons (Play tinted while something plays, Repeat tinted
    /// while on), a disclosure chevron for the track list, and the track list as themed rows.
    /// </summary>
    internal static class SoundTheme
    {
        public static void Apply(Transform sound, Undo undo)
        {
            if (sound == null) return;
            for (int i = 0; i < sound.childCount; i++)
            {
                var player = sound.GetChild(i);
                if (!player.name.EndsWith(" Player")) continue;
                try { StylePlayer(player, undo); }
                catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't style " + player.name + ": " + e); }
            }
        }

        private static void StylePlayer(Transform player, Undo undo)
        {
            // Studio's panel picture has the title painted in, so the theme writes its own.
            var bg = player.GetComponent<Image>();
            if (bg != null)
            {
                ControlStyler.Swap(bg, Sprites.Panel, Image.Type.Sliced, Color.white, undo);
                AddTitle(player, TitleOf(player.name), undo);
            }

            var control = FindControl(player);

            var play = Find(player, "Button Play");
            if (play != null)
            {
                var b = ThemedButton.Attach(play, ButtonKind.Push, "play", null);
                if (b != null)
                {
                    // Studio shows a small "playing" picture inside Play; the theme hides it and tints Play instead.
                    var playing = play.transform.Find("Image");
                    var playingImage = playing != null ? playing.GetComponent<Image>() : null;
                    if (playingImage != null)
                    {
                        ControlStyler.Swap(playingImage, playingImage.sprite, playingImage.type, Palette.White(0f), undo);
                        b.ActiveWhen = () => playingImage != null && playingImage.enabled;
                    }
                    b.Tooltip = () => "Play";
                    undo.Add(b.Remove);
                }
            }
            Icon(player, "Button Stop", "stop", "Stop", undo);
            Icon(player, "Button Pause", "pause", "Pause", undo);

            var repeat = Find(player, "Button Repeat");
            if (repeat != null)
            {
                var b = ThemedButton.Attach(repeat, ButtonKind.Push, "repeat", null);
                if (b != null)
                {
                    // Studio shows repeat on/off by swapping between two pictures.
                    var sprites = control != null ? Traverse.Create(control).Field("spriteRepeat").GetValue<Sprite[]>() : null;
                    if (sprites != null && sprites.Length > 1)
                        b.ActiveWhen = () => b.GameSprite == sprites[1];
                    b.Tooltip = () => "Repeat";
                    undo.Add(b.Remove);
                }
            }

            // The track list opens below the player; its fold button becomes a chevron.
            var expansion = Find(player, "Button Expansion");
            var list = control != null ? Traverse.Create(control).Field("objBottom").GetValue<GameObject>() : null;
            if (expansion != null)
            {
                var chevron = DisclosureChevron.Attach(expansion, list);
                if (chevron != null) undo.Add(chevron.Remove);
            }

            // Track list panel, rows, scroll thumb and any remaining text.
            ControlStyler.StylePanel(player, undo);
        }

        private static void Icon(Transform player, string name, string icon, string tip, Undo undo)
        {
            var button = Find(player, name);
            if (button == null) return;
            var b = ThemedButton.Attach(button, ButtonKind.Push, icon, null);
            if (b == null) return;
            b.Tooltip = () => tip;
            undo.Add(b.Remove);
        }

        private static Button Find(Transform player, string name)
        {
            var t = player.Find(name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Component FindControl(Transform player)
        {
            foreach (var c in player.GetComponents<MonoBehaviour>())
            {
                var n = c.GetType().Name;
                if (n == "BGMControl" || n == "ENVControl" || n == "OutsideSoundControl") return c;
            }
            return null;
        }

        private static string TitleOf(string name)
        {
            if (name.Contains("BGM")) return "BGM";
            if (name.Contains("ENV")) return "Environment sound";
            if (name.Contains("Outside")) return "External sound";
            return name.Replace(" Player", "");
        }

        private static void AddTitle(Transform player, string title, Undo undo)
        {
            var go = new GameObject(ThemedButton.OwnPrefix + "Title", typeof(RectTransform));
            go.layer = player.gameObject.layer;
            go.transform.SetParent(player, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(12f, -24f);
            rt.offsetMax = new Vector2(-12f, -4f);
            var text = Labels.Configure(go.AddComponent<Text>(), Fonts.Semibold, TextAnchor.MiddleLeft, 12, 9);
            text.text = title;
            text.color = Palette.White(0.7f);
            undo.Add(() => { if (go != null) Object.Destroy(go); });
        }
    }
}
