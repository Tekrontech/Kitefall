using Kitefall.Core;

namespace Kitefall.Tests
{
    /// <summary>Spec values (sections 7 and 8) used by the tests – mirrors the default data assets.</summary>
    internal static class TestData
    {
        public static KiteClassStats Guddi => new KiteClassStats
            { id = "guddi", speed = 3, agility = 4, stability = 3, classBase = 1.00f, turnRate = 180, sweetZoneBase = 15 };

        public static KiteClassStats Pipa => new KiteClassStats
            { id = "pipa", speed = 5, agility = 4, stability = 2, classBase = 0.92f, turnRate = 240, sweetZoneBase = 12 };

        public static TailStats ShortRibbon => new TailStats
            { id = "short_ribbon", sweetZoneMod = 0, turnMod = 0f, stabilityMod = 1, ropeSegments = 10 };

        public static TailStats NoTail => new TailStats
            { id = "none", sweetZoneMod = -5, turnMod = 0.15f, stabilityMod = -2, ropeSegments = 0 };

        public static DorStats Sooti => new DorStats
            { id = "sooti", dorCut = 1.00f, dorSpeed = 1.00f, strength = 95, tensionRiseMult = 1f, wearMult = 1f, agilityMod = 0f, sweetZoneMod = 0, dheelCounterBonus = 1.15f, longRangeAngleBonus = 1.10f };

        public static DorStats Reshmi => new DorStats
            { id = "reshmi", dorCut = 0.95f, dorSpeed = 1.20f, strength = 93, tensionRiseMult = 1.10f, wearMult = 1f, agilityMod = 0.05f, sweetZoneMod = 0, dheelCounterBonus = 1.15f, longRangeAngleBonus = 1.10f };

        public static DorStats Lachakdaar => new DorStats
            { id = "lachakdaar", dorCut = 0.97f, dorSpeed = 0.95f, strength = 96, tensionRiseMult = 1f, wearMult = 1f, agilityMod = 0f, sweetZoneMod = 4, dheelCounterBonus = 1.20f, longRangeAngleBonus = 1.10f };

        public static CharkhiStats Lakri => new CharkhiStats
            { id = "lakri", capacityMeters = 600, reelSpeed = 8, releaseSpeed = 10, releaseTensionDropMult = 1f, pullTensionRiseMult = 1f, kheenchPowerMult = 1f };

        public static CharkhiStats Speed => new CharkhiStats
            { id = "speed", capacityMeters = 450, reelSpeed = 14, releaseSpeed = 14, releaseTensionDropMult = 1.20f, pullTensionRiseMult = 1f, kheenchPowerMult = 1f };

        public static CharkhiStats Gear => new CharkhiStats
            { id = "gear", capacityMeters = 550, reelSpeed = 10, releaseSpeed = 9, releaseTensionDropMult = 1f, pullTensionRiseMult = 1.15f, kheenchPowerMult = 1.05f };

        public static LoadoutStats AllRounder => new LoadoutStats(Guddi, ShortRibbon, Sooti, Lakri);

        /// <summary>Calm, steady wind – no variation – so tests are easy to reason about.</summary>
        public static WindSample SteadyWind(float speed = 6f, float direction = 0f) => new WindSample(direction, speed, false);
    }
}
