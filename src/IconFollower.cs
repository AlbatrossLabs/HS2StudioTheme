using UnityEngine;
using UnityEngine.UI;

namespace HS2StudioTheme
{
    /// <summary>
    /// A small arrow picture Studio swaps to show a state (sort direction on the character lists):
    /// shown as the matching line chevron, following every swap Studio makes.
    /// </summary>
    internal class IconFollower : MonoBehaviour
    {
        private Image _image;
        private Sprite _game, _mine;
        private Color _color;
        private bool _preserve, _removed;

        public static IconFollower Attach(Image image)
        {
            if (image == null || image.GetComponent<IconFollower>() != null) return null;
            if (image.sprite == null || IconKey(image.sprite.name) == null) return null;
            var f = image.gameObject.AddComponent<IconFollower>();
            f._image = image;
            f._game = image.sprite;
            f._color = image.color;
            f._preserve = image.preserveAspect;
            f.Follow();
            return f;
        }

        /// <summary>Studio's triangle pictures and the chevron each one becomes.</summary>
        internal static string IconKey(string sprite)
        {
            switch (sprite)
            {
                case "sp_sn_09_00_03": return "chevron";
                case "sp_sn_09_00_04": return "chevron_right";
                case "sp_sn_09_00_05": return "chevron_up";
                case "sp_sn_09_00_06": return "chevron_left";
                default: return null;
            }
        }

        private void LateUpdate()
        {
            if (!_removed && _image.sprite != _mine) Follow();
        }

        private void Follow()
        {
            if (_image.sprite != _mine && _image.sprite != null) _game = _image.sprite;
            var key = _game != null ? IconKey(_game.name) : null;
            if (key == null) return;   // a picture we don't know: leave Studio's
            _mine = Sprites.Icon(key);
            _image.sprite = _mine;
            _image.preserveAspect = true;
            _image.color = Palette.White(0.7f);
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            if (_image != null)
            {
                _image.sprite = _game;
                _image.color = _color;
                _image.preserveAspect = _preserve;
            }
            Destroy(this);
        }
    }
}
