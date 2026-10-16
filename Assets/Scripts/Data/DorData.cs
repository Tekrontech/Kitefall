using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>
    /// Dor / string type (spec 7.2 / 14.6). Names like "Tez Dhaar" are gameplay stats only –
    /// never real coated/metal/nylon string (spec rule 4).
    /// </summary>
    [CreateAssetMenu(fileName = "Dor", menuName = "Kitefall/Dor (String)", order = 2)]
    public sealed class DorData : ScriptableObject
    {
        public string id = "sooti";
        public string displayName = "Sooti";

        [Header("Contest")]
        [Tooltip("DorCut – power multiplier (0.95–1.10).")]
        public float dorCut = 1.00f;
        [Tooltip("DorSpeed – how fast this dor moves the contest meter (0.85–1.20).")]
        public float dorSpeed = 1.00f;
        [Tooltip("Snap limit on the tension bar. Above this for longer than snapHoldTime = self-snap.")]
        [Range(0, 100)] public int strength = 95;
        public float dheelCounterBonus = 1.15f;
        public float longRangeAngleBonus = 1.10f;

        [Header("Handling")]
        [Tooltip("Multiplier on every tension rise. 1.10 = tension rises 10% faster.")]
        public float tensionRiseMult = 1.00f;
        [Tooltip("Multiplier on dor wear. 1.30 = wears 30% faster.")]
        public float wearMult = 1.00f;
        [Tooltip("Kite agility change as a fraction. 0.05 = +5%.")]
        [Range(-0.5f, 0.5f)] public float agilityMod;
        public int sweetZoneMod;

        [Header("Progression")]
        [Min(1)] public int unlockLevel = 1;

        public DorStats ToStats() => new DorStats
        {
            id = id,
            dorCut = dorCut,
            dorSpeed = dorSpeed,
            strength = strength,
            tensionRiseMult = tensionRiseMult,
            wearMult = wearMult,
            agilityMod = agilityMod,
            sweetZoneMod = sweetZoneMod,
            dheelCounterBonus = dheelCounterBonus,
            longRangeAngleBonus = longRangeAngleBonus,
            unlockLevel = unlockLevel
        };
    }
}
