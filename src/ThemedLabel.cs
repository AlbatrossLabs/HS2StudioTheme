using System;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Shows a TextMeshPro label in the system font. The game's TextMeshPro text stays where it is,
    /// transparent, so translation and game code keep writing to it; this mirrors whatever it says.
    /// State is serialised so copies (dropdown items made from a template) show their own text.
    /// </summary>
    internal class ThemedLabel : MonoBehaviour
    {
        [SerializeField] private bool _themed;
        [SerializeField] private TMPro.TMP_Text _source;
        [SerializeField] private Color _sourceColor;
        [SerializeField] private Text _label;

        [NonSerialized] private bool _live, _removed;
        [NonSerialized] private string _last;

        public static ThemedLabel Attach(TMPro.TMP_Text source, bool strong)
        {
            return Attach(source, strong, 0, strong ? 0.95f : Palette.TextAlpha);
        }

        /// <param name="size">Font size, or 0 to fit Studio's row height.</param>
        public static ThemedLabel Attach(TMPro.TMP_Text source, bool strong, int size, float alpha)
        {
            if (source == null) return null;
            // Already hidden (by a themed button mirroring it, or by the game): nothing to show.
            if (source.color.a <= 0f) return null;
            foreach (var existing in source.GetComponents<ThemedLabel>())
            {
                if (existing._removed) continue;
                if (!existing._live && existing._themed) existing.Adopt();
                if (existing._live) return null;
            }
            var themed = source.gameObject.AddComponent<ThemedLabel>();
            themed.Init(source, strong, size, alpha);
            return themed;
        }

        private void Awake()
        {
            if (_themed && !_live) Adopt();
        }

        private void Adopt()
        {
            if (_source == null || _label == null) { _themed = false; _removed = true; Destroy(this); return; }
            _live = true;
            _last = null;
            Clones.Track(this, Remove);
        }

        private void Init(TMPro.TMP_Text source, bool strong, int size, float alpha)
        {
            _themed = true;
            _live = true;
            _source = source;
            _sourceColor = source.color;

            var go = new GameObject(ThemedButton.OwnPrefix + "Label", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            if (size <= 0)
            {
                float h = ((RectTransform)transform).rect.height;
                size = Labels.SizeFor(h > 1f ? h : 20f);
            }
            _label = Labels.Configure(go.AddComponent<Text>(), strong ? Fonts.Semibold : Fonts.Regular,
                Labels.AnchorOf(source), size, Mathf.Min(8, size));
            _label.color = Palette.White(alpha);
            // One-line labels keep their size and scroll (long ones end in "…" until hovered).
            float lh = ((RectTransform)transform).rect.height;
            // (Numbers keep shrinking to fit: they sit in tight boxes and must stay whole.)
            if ((lh <= 1f || lh < size * 2.2f) && !IsNumeric(source.text)) Marquee.Wrap(_label);

            Hide();
            Mirror();
        }

        private void Hide()
        {
            var c = _sourceColor;
            c.a = 0f;
            _source.color = c;
        }

        private void Mirror()
        {
            var text = _source.text;
            if (text != _last)
            {
                _last = text;
                _label.text = Labels.Plain(text);
            }
        }

        private void LateUpdate()
        {
            if (_removed || !_live || _source == null) return;
            Mirror();
            if (_source.color.a > 0f)
            {
                _sourceColor = _source.color;
                Hide();
            }
            if (_label.enabled != _source.enabled) _label.enabled = _source.enabled;
        }

        /// <summary>Start the text further from the left (the game's text object stays where it is).</summary>
        private static bool IsNumeric(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (var ch in s)
                if (char.IsLetter(ch)) return false;
            return true;
        }

        public void Inset(float left)
        {
            if (_label == null || left <= 0f) return;
            var rt = Labels.Frame(_label);
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_source != null) _source.color = _sourceColor;
            if (_label != null) Destroy(Labels.Frame(_label).gameObject);
            Destroy(this);
        }
    }
}
