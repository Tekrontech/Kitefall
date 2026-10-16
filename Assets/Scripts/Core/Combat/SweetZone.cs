using System;

namespace Kitefall.Core
{
    [Serializable]
    public class SweetZoneSettings
    {
        /// <summary>Lowest and highest centre of the moving band on the tension bar.</summary>
        public float centerMin = 38f;
        public float centerMax = 80f;
        /// <summary>Seconds for one full up-and-down cycle.</summary>
        public float cycleSeconds = 2.6f;
        /// <summary>Storm phase makes the band narrower (spec 6.1: sweet zone −3).</summary>
        public int stormWidthPenalty = 3;
    }

    /// <summary>
    /// The moving band on the tension bar during a contest (spec 9.2). Keeping tension inside it gives full power.
    /// Deterministic: centre depends only on contest time and a phase from the match seed.
    /// </summary>
    public readonly struct SweetZoneBand
    {
        public readonly float Center;
        public readonly float Width;

        public SweetZoneBand(float center, float width)
        {
            Center = center;
            Width = width;
        }

        public float Min => Center - Width * 0.5f;
        public float Max => Center + Width * 0.5f;
        public bool Contains(float tension) => tension >= Min && tension <= Max;

        public static SweetZoneBand At(SweetZoneSettings s, float contestTime, float phase01, int width)
        {
            float mid = (s.centerMin + s.centerMax) * 0.5f;
            float amp = (s.centerMax - s.centerMin) * 0.5f;
            float cycle = s.cycleSeconds <= 0.01f ? 1f : s.cycleSeconds;
            float angle = (contestTime / cycle + phase01) * 6.2831853f;
            return new SweetZoneBand(mid + amp * KMath.Sin(angle), Math.Max(1, width));
        }
    }
}
