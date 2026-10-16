using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Dum / tail (spec 8.2 / 14.6).</summary>
    [CreateAssetMenu(fileName = "Tail", menuName = "Kitefall/Dum (Tail)", order = 1)]
    public sealed class TailData : ScriptableObject
    {
        public string id = "short_ribbon";
        public string displayName = "Short ribbon";

        [Tooltip("Added to the sweet zone width (tension points).")]
        public int sweetZoneMod;
        [Tooltip("Turn speed change as a fraction: 0.15 = +15%, -0.05 = -5%.")]
        [Range(-0.5f, 0.5f)] public float turnMod;
        [Tooltip("Added to the kite's stability stat.")]
        public float stabilityMod = 1;
        [Tooltip("Rope segments at High quality (milestone 2).")]
        [Min(0)] public int ropeSegments = 10;

        public TailStats ToStats() => new TailStats
        {
            id = id,
            sweetZoneMod = sweetZoneMod,
            turnMod = turnMod,
            stabilityMod = stabilityMod,
            ropeSegments = ropeSegments
        };
    }
}
