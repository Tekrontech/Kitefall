using System;

namespace Kitefall.Core
{
    /// <summary>Plain-data copy of a kite class (spec 8.1). Filled from KiteClassData (ScriptableObject).</summary>
    [Serializable]
    public struct KiteClassStats
    {
        public string id;
        /// <summary>Speed stat 1–5.</summary>
        public float speed;
        /// <summary>Agility stat 1–5 (used later by contests / bots).</summary>
        public float agility;
        /// <summary>Stability stat 1–5. Higher = self-rights faster, wobbles less.</summary>
        public float stability;
        /// <summary>ClassBase multiplier used in contest Power (0.90–1.10).</summary>
        public float classBase;
        /// <summary>Turn rate in degrees per second.</summary>
        public float turnRate;
        /// <summary>Sweet zone width in tension points.</summary>
        public int sweetZoneBase;
    }

    /// <summary>Plain-data copy of a dum / tail (spec 8.2).</summary>
    [Serializable]
    public struct TailStats
    {
        public string id;
        public int sweetZoneMod;
        /// <summary>Turn speed change as a fraction: +0.15 = 15% faster, -0.05 = 5% slower.</summary>
        public float turnMod;
        public float stabilityMod;
        public int ropeSegments;

        public static TailStats None => new TailStats { id = "none", sweetZoneMod = 0, turnMod = 0f, stabilityMod = 0f, ropeSegments = 0 };
    }

    /// <summary>Plain-data copy of a dor / string type (spec 7.2).</summary>
    [Serializable]
    public struct DorStats
    {
        public string id;
        public float dorCut;
        public float dorSpeed;
        /// <summary>Snap limit on the tension bar (0–100).</summary>
        public int strength;
        /// <summary>Multiplier on every tension rise (Reshmi 1.10, Tez Dhaar 1.15).</summary>
        public float tensionRiseMult;
        public float wearMult;
        /// <summary>Kite agility change as a fraction (Reshmi +0.05, Mazboot -0.08).</summary>
        public float agilityMod;
        public int sweetZoneMod;
        public float dheelCounterBonus;
        public float longRangeAngleBonus;
        public int unlockLevel;
    }

    /// <summary>Plain-data copy of a charkhi / spool (spec 7.3).</summary>
    [Serializable]
    public struct CharkhiStats
    {
        public string id;
        public int capacityMeters;
        /// <summary>Reel-in speed in m/s.</summary>
        public float reelSpeed;
        /// <summary>Line release (dheel) speed in m/s.</summary>
        public float releaseSpeed;
        /// <summary>Multiplier on tension fall while releasing (Speed charkhi 1.20).</summary>
        public float releaseTensionDropMult;
        public int dheelTensionFloor;
        public float kheenchPowerMult;
        /// <summary>Multiplier on tension rise while pulling (Gear charkhi 1.15).</summary>
        public float pullTensionRiseMult;
        public float launchDelay;
        public int unlockLevel;
    }
}
