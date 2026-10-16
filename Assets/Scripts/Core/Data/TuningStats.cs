using System;

namespace Kitefall.Core
{
    /// <summary>Which number a tuning changes (spec 7.4).</summary>
    public enum TuningStat
    {
        DorSpeed,      // percent
        Strength,      // absolute points
        Agility,       // percent (turn rate)
        DorCut,        // percent
        ReelSpeed,     // percent
        Capacity,      // absolute metres
        SweetZone,     // absolute points
        KheenchPower   // percent
    }

    public enum TuningTarget { Dor, Charkhi }

    /// <summary>One side of a tuning. Percent stats use fractions: 0.05 = +5%.</summary>
    [Serializable]
    public struct StatModifier
    {
        public TuningStat stat;
        public float value;

        public StatModifier(TuningStat stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    /// <summary>A sidegrade tuning: always one plus and one minus (spec 7.4).</summary>
    [Serializable]
    public struct TuningStats
    {
        public string id;
        public TuningTarget target;
        public StatModifier plus;
        public StatModifier minus;
    }
}
