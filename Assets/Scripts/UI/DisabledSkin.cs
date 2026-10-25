using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// Locked / unavailable buttons look clearly disabled in the glossy style: grey "locked" skin and dimmed label, instead of
    /// Unity's default darkening (which turned orange buttons brown). Restores the normal skin when the button is enabled again.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class DisabledSkin : MonoBehaviour
    {
        private Button _button;
        private Image _image;
        private Sprite _normal;
        private Color _normalColor;
        private Text[] _labels;
        private Color[] _labelColors;
        private bool _shownDisabled;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
        }

        private void LateUpdate()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button == null) return;
            bool off = !_button.interactable;
            if (off == _shownDisabled) return;
            _shownDisabled = off;
            if (off)
            {
                _normal = _image.sprite;
                _normalColor = _image.color;
                _image.sprite = Glossy.Sprite(Glossy.Style.Locked);
                _image.color = Color.white;
                _labels = GetComponentsInChildren<Text>(true);
                _labelColors = new Color[_labels.Length];
                // Dark labels (amber / green skins) would vanish on the grey skin: all disabled labels use the muted grey.
                Color muted = Glossy.TextOn(Glossy.Style.Locked);
                for (int i = 0; i < _labels.Length; i++) { _labelColors[i] = _labels[i].color; muted.a = _labels[i].color.a; _labels[i].color = muted; }
            }
            else
            {
                _image.sprite = _normal;
                _image.color = _normalColor;
                if (_labels != null)
                    for (int i = 0; i < _labels.Length; i++) if (_labels[i] != null) _labels[i].color = _labelColors[i];
            }
        }
    }
}
