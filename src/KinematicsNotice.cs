using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// In Anim > Animations: when the character has FK or IK turned on, Studio's animation can't move
    /// the posed parts, so picking an animation looks like it did nothing. A bar under the breadcrumb
    /// says so, with a button that turns FK and IK off for that character (and any other selected ones).
    /// </summary>
    internal class KinematicsNotice : MonoBehaviour
    {
        private ColumnView _columns;
        private GameObject _bar;
        private Text _text;
        private readonly List<Studio.OCIChar> _chars = new List<Studio.OCIChar>();
        private Studio.MPCharCtrl _panel;
        private float _next;
        private bool _removed;

        public static KinematicsNotice Attach(ColumnView columns, Undo undo)
        {
            if (columns == null || columns.GetComponent<KinematicsNotice>() != null) return null;
            var n = columns.gameObject.AddComponent<KinematicsNotice>();
            n._columns = columns;
            undo.Add(n.Remove);
            n.Build();
            return n;
        }

        private void Build()
        {
            _bar = new GameObject(ThemedButton.OwnPrefix + "Kinematics Notice", typeof(RectTransform));
            _bar.layer = gameObject.layer;
            _bar.transform.SetParent(transform, false);
            _bar.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)_bar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var bg = _bar.AddComponent<Image>();
            bg.sprite = Sprites.Panel;
            bg.type = Image.Type.Sliced;

            var textGo = new GameObject(ThemedButton.OwnPrefix + "Text", typeof(RectTransform));
            textGo.transform.SetParent(_bar.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 2f); trt.offsetMax = new Vector2(-96f, -2f);
            _text = Labels.Configure(textGo.AddComponent<Text>(), Fonts.Regular, TextAnchor.MiddleLeft, 11, 8);
            _text.color = Palette.Hex("FFD60A");
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var buttonGo = new GameObject(ThemedButton.OwnPrefix + "Turn Off", typeof(RectTransform));
            buttonGo.transform.SetParent(_bar.transform, false);
            var brt = (RectTransform)buttonGo.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-6f, 0f);
            brt.sizeDelta = new Vector2(84f, 22f);
            var img = buttonGo.AddComponent<Image>();
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(TurnOff);
            var themed = ThemedButton.Attach(button, ButtonKind.Push, null, "Turn off");
            if (themed != null) themed.Tooltip = () => "Turn FK and IK off so the animation moves the whole body";
            _bar.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_removed || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.3f;

            // Under the breadcrumb, as wide.
            var crumb = _columns != null ? _columns.Breadcrumb : null;
            if (crumb != null)
            {
                var rt = (RectTransform)_bar.transform;
                var pos = crumb.anchoredPosition - new Vector2(0f, crumb.sizeDelta.y + 6f);
                var size = new Vector2(Mathf.Max(220f, crumb.sizeDelta.x), 34f);
                if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
                if (rt.sizeDelta != size) rt.sizeDelta = size;
            }

            Collect();
            bool fk = false, ik = false;
            foreach (var c in _chars) { fk |= c.oiCharInfo.enableFK; ik |= c.oiCharInfo.enableIK; }
            bool show = fk || ik;
            if (_bar.activeSelf != show) _bar.SetActive(show);
            if (!show) return;
            string what = fk && ik ? "FK and IK are" : fk ? "FK is" : "IK is";
            string who = _chars.Count == 1 ? _chars[0].charInfo.fileParam.fullname : "the selected characters";
            var msg = what + " on for " + who + ": animations won't move the posed parts.";
            if (_text.text != msg) _text.text = msg;
        }

        /// <summary>The character in the Anim panel, plus any other selected characters.</summary>
        private void Collect()
        {
            _chars.Clear();
            try
            {
                if (_panel == null) _panel = Object.FindObjectOfType<Studio.MPCharCtrl>();
                if (_panel != null && _panel.ociChar != null) _chars.Add(_panel.ociChar);
                var selected = Studio.Studio.Instance.treeNodeCtrl.selectObjectCtrl;
                if (selected != null)
                    foreach (var o in selected)
                    {
                        var c = o as Studio.OCIChar;
                        if (c != null && !_chars.Contains(c)) _chars.Add(c);
                    }
            }
            catch (System.Exception) { }
        }

        private void TurnOff()
        {
            Collect();
            foreach (var c in _chars)
            {
                try
                {
                    if (c.oiCharInfo.enableFK) c.ActiveKinematicMode(Studio.OICharInfo.KinematicMode.FK, false, false);
                    if (c.oiCharInfo.enableIK) c.ActiveKinematicMode(Studio.OICharInfo.KinematicMode.IK, false, false);
                }
                catch (System.Exception e) { StudioThemePlugin.Log.LogWarning("Couldn't turn FK / IK off: " + e.Message); }
            }
            // Studio's Kinematics panel reads the switches when it refreshes.
            try { if (_panel != null) AccessTools.Method(typeof(Studio.MPCharCtrl), "UpdateInfo")?.Invoke(_panel, null); } catch (System.Exception) { }
            _next = 0f;
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_bar != null) Destroy(_bar);
            Destroy(this);
        }
    }
}
