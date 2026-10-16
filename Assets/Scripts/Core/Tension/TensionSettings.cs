using System;

namespace Kitefall.Core
{
    /// <summary>
    /// Tension tunables (spec 9.1 / 9.2 / 14.6 CombatConfig). Built from the CombatConfig asset.
    /// Defaults equal the spec starting values so tests and the server use the same numbers.
    /// </summary>
    [Serializable]
    public class TensionSettings
    {
        /// <summary>Tension gained per second while pulling.</summary>
        public float pullRate = 35f;
        /// <summary>Tension lost per second while releasing (dheel).</summary>
        public float releaseRate = 40f;
        /// <summary>Value tension drifts toward when idle.</summary>
        public float idleTarget = 40f;
        /// <summary>Points per second of idle drift. NOT in spec – assumption, see docs/DECISIONS.md.</summary>
        public float idleDriftRate = 20f;
        /// <summary>"Strong wind: rises 10% faster".</summary>
        public float strongWindRiseMult = 1.10f;
        /// <summary>Upper edge of the green zone.</summary>
        public float safeZoneMax = 60f;
        /// <summary>Upper edge of the yellow zone. At or above this = red / danger.</summary>
        public float strongZoneMax = 85f;
        /// <summary>Seconds tension may stay above dor Strength before a self-snap.</summary>
        public float snapHoldTime = 0.6f;
    }
}
