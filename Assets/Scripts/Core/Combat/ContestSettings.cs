using System;

namespace Kitefall.Core
{
    /// <summary>Contest numbers – spec 9.5 and 14.6 CombatConfig. Defaults = spec starting values.</summary>
    [Serializable]
    public class ContestSettings
    {
        public float tickRate = 20f;
        public float contestK = 6f;
        public float contestMaxTime = 3f;
        public float lateWinThreshold = 40f;
        public float attackLockWindow = 0.3f;
        /// <summary>A dive started at most this long before contact counts as a Dive Cut.</summary>
        public float diveWindowBeforeContact = 1f;
        public float counterWin = 1.15f;
        public float counterLose = 0.87f;
        public float diveBurst = 1.25f;
        public float diveBurstTime = 1f;
        /// <summary>Noise is uniform(−noise, +noise) per tick.</summary>
        public float noise = 0.5f;
        public float multiContestMult = 0.8f;

        // Input factor
        public float inputSweet = 1.00f;
        public float inputOutside = 0.55f;
        public float inputRed = 0.25f;
        public float redZone = 85f;

        // Position factor
        public float positionPerMeter = 0.02f;
        public float positionMin = 0.85f;
        public float positionMax = 1.15f;

        // Angle factor
        public float angleGoodMin = 60f;
        public float angleGoodMax = 120f;
        public float angleBadBelow = 30f;
        public float angleGood = 1.10f;
        public float angleBad = 0.90f;
        public float longRangeMeters = 500f;

        // Wind factor
        public float windSideBonus = 0.05f;
        public float gustWindMult = 1.08f;

        // Phase / condition
        public float stormPowerMult = 1.10f;
        public float lowDurability = 30f;
        public float lowDurabilityMult = 0.90f;

        // Durability per contest (spec 8.3: −3 to −8 %)
        public float durabilityLossMin = 3f;
        public float durabilityLossMax = 8f;
        public float neutralDurabilityLoss = 5f;
    }
}
