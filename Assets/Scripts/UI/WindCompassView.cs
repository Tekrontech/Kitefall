using Kitefall.Kite;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>Wind compass below the score (spec 10): arrow shows where the wind blows, relative to the camera.</summary>
    public sealed class WindCompassView : MonoBehaviour
    {
        [SerializeField] private KiteController kite;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Text label;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color strongColor = new Color(1f, 0.45f, 0.3f);

        private void Update()
        {
            if (kite == null || kite.Simulation == null) return;
            var wind = kite.LastWind;

            float cameraYaw = viewCamera != null ? viewCamera.transform.eulerAngles.y : 0f;
            float relative = wind.DirectionDeg - cameraYaw;
            if (arrow != null)
            {
                // UI rotates counter-clockwise for positive z, compass angles are clockwise.
                arrow.localEulerAngles = new Vector3(0f, 0f, -relative);
                var image = arrow.GetComponent<Image>();
                if (image != null) image.color = wind.IsStrong ? strongColor : normalColor;
            }

            if (label != null) label.text = wind.IsStrong ? $"{wind.Speed:0.0} m/s  STRONG" : $"{wind.Speed:0.0} m/s";
        }
    }
}
