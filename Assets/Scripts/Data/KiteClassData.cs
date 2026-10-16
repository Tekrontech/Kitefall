using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>Kite class (spec 8.1 / 14.6). One asset per class: Guddi, Pipa, Rokkaku…</summary>
    [CreateAssetMenu(fileName = "KiteClass", menuName = "Kitefall/Kite Class", order = 0)]
    public sealed class KiteClassData : ScriptableObject
    {
        public string id = "guddi";
        public string displayName = "Guddi";

        [Header("Stats (1–5, equal total budget)")]
        [Range(1, 5)] public float speed = 3;
        [Range(1, 5)] public float agility = 4;
        [Range(1, 5)] public float stability = 3;

        [Header("Combat / flight")]
        [Tooltip("ClassBase multiplier in the contest Power formula (0.90–1.10).")]
        [Range(0.8f, 1.2f)] public float classBase = 1.00f;
        [Tooltip("Turn rate in degrees per second.")]
        [Min(1)] public float turnRate = 180f;
        [Tooltip("Sweet zone width in tension points.")]
        [Min(1)] public int sweetZoneBase = 15;

        [Header("Visual")]
        public GameObject prefab;

        public KiteClassStats ToStats() => new KiteClassStats
        {
            id = id,
            speed = speed,
            agility = agility,
            stability = stability,
            classBase = classBase,
            turnRate = turnRate,
            sweetZoneBase = sweetZoneBase
        };
    }
}
