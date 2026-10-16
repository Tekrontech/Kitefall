using Kitefall.Core;
using Kitefall.Kite;
using UnityEngine;
using UnityEngine.UI;

namespace Kitefall.UI
{
    /// <summary>
    /// Vertical tension bar on the right edge (spec 10): green / yellow / red zones,
    /// a marker for the current tension and a white line at the dor's Strength (snap limit).
    /// Zone sizes come from CombatConfig, so the bar always matches the rules.
    /// The sweet zone band is added in milestone 5.
    /// </summary>
    public sealed class TensionBarView : MonoBehaviour
    {
        [SerializeField] private KiteController kite;

        [Header("Bar parts (children of the bar)")]
        [SerializeField] private RectTransform safeZone;
        [SerializeField] private RectTransform strongZone;
        [SerializeField] private RectTransform dangerZone;
        [SerializeField] private RectTransform strengthLine;
        [SerializeField] private RectTransform marker;
        [SerializeField] private Image markerImage;
        [SerializeField] private Image snapWarning;
        [SerializeField] private Text valueLabel;

        [Header("Colours")]
        [SerializeField] private Color safeColor = new Color(0.30f, 0.80f, 0.40f);
        [SerializeField] private Color strongColor = new Color(0.98f, 0.80f, 0.20f);
        [SerializeField] private Color dangerColor = new Color(0.92f, 0.25f, 0.20f);

        private float _laidOutSafe = -1f;
        private float _laidOutStrong = -1f;
        private float _laidOutStrength = -1f;

        private void Update()
        {
            if (kite == null || kite.Simulation == null) return;
            TensionModel tension = kite.Simulation.Tension;

            LayoutZones(tension);

            float t01 = tension.Tension / 100f;
            SetVerticalAnchor(marker, t01);
            if (markerImage != null) markerImage.color = ColorFor(tension.Zone);
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(tension.Tension).ToString();

            if (snapWarning != null)
            {
                // Flash faster as the snap timer runs out.
                float danger01 = tension.SnapHoldTime > 0f ? Mathf.Clamp01(tension.OverStrengthTime / tension.SnapHoldTime) : 0f;
                bool show = tension.IsAboveStrength || tension.IsSnapped;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, danger01));
                var c = snapWarning.color;
                c.a = show ? Mathf.Lerp(0.3f, 1f, pulse) : 0f;
                snapWarning.color = c;
            }
        }

        private void LayoutZones(TensionModel tension)
        {
            float safeMax = kite.CombatConfig != null ? kite.CombatConfig.safeZoneMax : 60f;
            float strongMax = kite.CombatConfig != null ? kite.CombatConfig.strongZoneMax : 85f;
            if (Mathf.Approximately(safeMax, _laidOutSafe) && Mathf.Approximately(strongMax, _laidOutStrong)
                && Mathf.Approximately(tension.Strength, _laidOutStrength)) return;

            _laidOutSafe = safeMax;
            _laidOutStrong = strongMax;
            _laidOutStrength = tension.Strength;

            SetVerticalRange(safeZone, 0f, safeMax / 100f, safeColor);
            SetVerticalRange(strongZone, safeMax / 100f, strongMax / 100f, strongColor);
            SetVerticalRange(dangerZone, strongMax / 100f, 1f, dangerColor);
            SetVerticalAnchor(strengthLine, Mathf.Clamp01(tension.Strength / 100f));
        }

        private Color ColorFor(TensionZone zone)
        {
            switch (zone)
            {
                case TensionZone.Danger: return dangerColor;
                case TensionZone.Strong: return strongColor;
                default: return safeColor;
            }
        }

        private static void SetVerticalRange(RectTransform rect, float min01, float max01, Color color)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, min01);
            rect.anchorMax = new Vector2(1f, max01);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.GetComponent<Image>();
            if (image != null) image.color = new Color(color.r, color.g, color.b, 0.55f);
        }

        private static void SetVerticalAnchor(RectTransform rect, float y01)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(rect.anchorMin.x, y01);
            rect.anchorMax = new Vector2(rect.anchorMax.x, y01);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
        }
    }
}
