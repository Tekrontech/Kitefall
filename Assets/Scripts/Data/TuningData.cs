using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Tuning sidegrade (spec 7.4 / 14.6). Percent stats use fractions (0.05 = +5 %).</summary>
    [CreateAssetMenu(fileName = "Tuning", menuName = "Kitefall/Tuning", order = 4)]
    public sealed class TuningData : ScriptableObject
    {
        public string id = "waxed_coat";
        public string displayName = "Waxed coat";
        public TuningTarget target = TuningTarget.Dor;
        public StatModifier plus = new StatModifier(TuningStat.DorSpeed, 0.05f);
        public StatModifier minus = new StatModifier(TuningStat.Strength, -1f);

        public TuningStats ToStats() => new TuningStats { id = id, target = target, plus = plus, minus = minus };
    }
}
