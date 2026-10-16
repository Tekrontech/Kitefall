using System;

namespace Kitefall.Core
{
    /// <summary>Wind tunables. Each arena gets its own WindConfig asset (spec 11 "wind twist").</summary>
    [Serializable]
    public class WindSettings
    {
        /// <summary>Direction the wind blows TOWARD, degrees clockwise from world +Z (seen from above).</summary>
        public float baseDirectionDeg = 0f;
        /// <summary>Average wind speed at the flyer's height, m/s.</summary>
        public float baseSpeed = 6f;
        /// <summary>How far the direction wanders from base (±degrees).</summary>
        public float directionVariationDeg = 15f;
        /// <summary>How much speed wanders as a fraction of base (0.25 = ±25%).</summary>
        public float speedVariation = 0.25f;
        /// <summary>Main cycle length of the wandering, seconds.</summary>
        public float variationPeriod = 40f;
        /// <summary>Extra speed per 100 m of height as a fraction (0.2 = +20% at 100 m up).</summary>
        public float heightGainPer100m = 0.2f;
        public float minSpeed = 1f;
        public float maxSpeed = 18f;
        /// <summary>At or above this speed the wind counts as "strong" (tension rises 10% faster). Assumption.</summary>
        public float strongWindSpeed = 9f;
        /// <summary>Seed for the deterministic wind pattern (the server will send one per match).</summary>
        public int seed = 1234;
    }
}
