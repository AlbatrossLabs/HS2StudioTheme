using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// Clicks on a camera slot's pill: a click goes to the saved view (Studio's own button does that), a click
    /// on an empty slot saves the current view there instead, and a right-click opens Go to / Save.
    /// Saving presses Studio's (hidden) save button for the slot, so MoarCamz's slots work the same way.
    /// </summary>
    internal class SlotClicks : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IRightClick
    {
        private CameraBar _bar;
        private ThemedButton _themed;
        private Button _load, _save;
        private bool _saveOnUp;

        public int Number { get; private set; }

        /// <summary>Set up (a copy made by Instantiate isn't: its fields don't come along).</summary>
        public bool Live { get { return _bar != null && _load != null; } }

        /// <summary>Nothing saved in it yet (MoarCamz greys those; Studio's own ten always have a view).</summary>
        public bool Empty { get { return _themed != null && _themed.SlotEmpty; } }

        public void Init(CameraBar bar, ThemedButton themed, Button load, Button save, int number)
        {
            _bar = bar; _themed = themed; _load = load; _save = save; Number = number;
        }

        public void Go() { if (_load != null && !Empty) _load.onClick.Invoke(); }

        public void DoSave() { if (_save != null) _save.onClick.Invoke(); }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !Empty || _save == null) return;
            e.eligibleForClick = false;   // not a load: save on release instead
            _saveOnUp = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_saveOnUp) return;
            _saveOnUp = false;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, e.position, e.pressEventCamera)) DoSave();
        }

        public void OnRightClick(Vector2 screen)
        {
            if (_bar != null) _bar.OpenSlotMenu(this);
        }
    }
}
