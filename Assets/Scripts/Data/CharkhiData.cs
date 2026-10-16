using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Charkhi / spool (spec 7.3 / 14.6).</summary>
    [CreateAssetMenu(fileName = "Charkhi", menuName = "Kitefall/Charkhi (Spool)", order = 3)]
    public sealed class CharkhiData : ScriptableObject
    {
        public string id = "lakri";
        public string displayName = "Lakri";

        [Header("Line")]
        [Min(10)] public int capacityMeters = 600;
        [Tooltip("Reel-in speed, m/s.")]
        [Min(0)] public float reelSpeed = 8f;
        [Tooltip("Release (dheel) speed, m/s.")]
        [Min(0)] public float releaseSpeed = 10f;

        [Header("Tension")]
        [Tooltip("Multiplier on tension fall while releasing. 1.20 = falls 20% faster.")]
        public float releaseTensionDropMult = 1.00f;
        [Tooltip("Multiplier on tension rise while pulling. 1.15 = rises 15% faster.")]
        public float pullTensionRiseMult = 1.00f;
        [Tooltip("During Dheel Cut tension never drops below this (Brake charkhi = 55). 0 = off. Used from milestone 5.")]
        [Range(0, 100)] public int dheelTensionFloor;

        [Header("Contest")]
        [Tooltip("Kheench Cut power multiplier (Gear charkhi = 1.05).")]
        public float kheenchPowerMult = 1.00f;

        [Header("Other")]
        [Tooltip("Extra launch time in seconds (Bari = 1).")]
        [Min(0)] public float launchDelay;
        [Min(1)] public int unlockLevel = 1;

        public CharkhiStats ToStats() => new CharkhiStats
        {
            id = id,
            capacityMeters = capacityMeters,
            reelSpeed = reelSpeed,
            releaseSpeed = releaseSpeed,
            releaseTensionDropMult = releaseTensionDropMult,
            dheelTensionFloor = dheelTensionFloor,
            kheenchPowerMult = kheenchPowerMult,
            pullTensionRiseMult = pullTensionRiseMult,
            launchDelay = launchDelay,
            unlockLevel = unlockLevel
        };
    }
}
