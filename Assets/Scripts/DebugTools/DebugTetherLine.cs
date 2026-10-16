using Kitefall.Kite;
using UnityEngine;

namespace Kitefall.DebugTools
{
    /// <summary>
    /// TEMPORARY line drawing for milestone 1: a simple curve from the flyer to the kite that sags
    /// more when tension is low. Milestone 3 replaces this with LineRenderer3D + ContactDetector.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class DebugTetherLine : MonoBehaviour
    {
        [SerializeField] private KiteController kite;
        [SerializeField, Range(2, 64)] private int segments = 24;
        [Tooltip("Sag at zero tension as a fraction of line length.")]
        [SerializeField, Range(0f, 0.3f)] private float maxSagFraction = 0.08f;
        [Tooltip("Line colour by tension 0 → 100 (white → yellow at 60 → red at 85).")]
        [SerializeField] private Gradient tensionColors = DefaultGradient();

        private LineRenderer _line;

        private static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.95f, 0.95f, 0.95f), 0f),
                    new GradientColorKey(new Color(0.98f, 0.85f, 0.3f), 0.60f),
                    new GradientColorKey(new Color(0.95f, 0.25f, 0.2f), 0.85f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = segments + 1;
        }

        private void LateUpdate()
        {
            if (kite == null || kite.Simulation == null || kite.Anchor == null) return;

            bool flying = kite.Simulation.Status == Core.KiteStatus.Flying;
            _line.enabled = flying;
            if (!flying) return;

            Vector3 start = kite.Anchor.position;
            Vector3 end = kite.KiteVisual.position;
            float tension01 = kite.Simulation.Tension.Tension / 100f;
            float sag = Vector3.Distance(start, end) * maxSagFraction * (1f - tension01);
            Vector3 control = (start + end) * 0.5f + Vector3.down * sag * 2f;

            if (_line.positionCount != segments + 1) _line.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                // Quadratic Bézier: start → control → end.
                Vector3 p = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
                _line.SetPosition(i, p);
            }

            if (tensionColors != null)
            {
                Color c = tensionColors.Evaluate(tension01);
                _line.startColor = c;
                _line.endColor = c;
            }
        }
    }
}
