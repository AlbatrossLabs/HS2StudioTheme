using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Current State's accessory slots: Studio shows all twenty, greying out the ones the character
    /// has nothing in. The empty ones fold away (out of the layout, so the list closes up) and come
    /// back as soon as the slot has something in it.
    /// </summary>
    internal class EmptyRows : MonoBehaviour
    {
        private class Row
        {
            public RectTransform Rt;
            public LayoutElement Layout;
            public bool OrigIgnore, Hidden;
            public Button[] Buttons;
        }

        private readonly List<Row> _rows = new List<Row>();
        private float _next;
        private bool _removed;

        private int _count = -1;

        public static EmptyRows Attach(Transform container)
        {
            if (container == null || container.GetComponent<EmptyRows>() != null) return null;
            var e = container.gameObject.AddComponent<EmptyRows>();
            e.Collect();
            return e;
        }

        /// <summary>The rows, again whenever the count changes (plugins add slots past Studio's twenty).</summary>
        private void Collect()
        {
            _count = transform.childCount;
            foreach (Transform t in transform)
            {
                if (_rows.Exists(r => r.Rt == t)) continue;
                var le = t.GetComponent<LayoutElement>();
                if (le == null || t.name.StartsWith(ThemedButton.OwnPrefix)) continue;
                var buttons = t.GetComponentsInChildren<Button>(true);
                if (buttons.Length == 0) continue;
                _rows.Add(new Row { Rt = (RectTransform)t, Layout = le, OrigIgnore = le.ignoreLayout, Buttons = buttons });
            }
            _rows.RemoveAll(r => r.Rt == null);
        }

        private void OnEnable() { _next = 0f; }

        private void LateUpdate()
        {
            if (_removed) return;
            if (transform.childCount != _count) { Collect(); _next = 0f; }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            bool changed = false;
            foreach (var r in _rows)
            {
                if (r.Rt == null) continue;
                bool empty = true;
                foreach (var b in r.Buttons) if (b != null && b.interactable) { empty = false; break; }
                if (empty == r.Hidden) continue;
                r.Hidden = empty;
                r.Layout.ignoreLayout = empty || r.OrigIgnore;
                r.Rt.localScale = empty ? Vector3.zero : Vector3.one;
                changed = true;
            }
            if (changed) LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (var r in _rows)
            {
                if (r.Rt == null) continue;
                r.Layout.ignoreLayout = r.OrigIgnore;
                r.Rt.localScale = Vector3.one;
            }
            if (this != null) LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
            Destroy(this);
        }
    }
}
