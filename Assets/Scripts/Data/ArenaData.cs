using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    public enum MusicStyle { OldCity, Coastal, River, Royal, Mountain, Island }

    /// <summary>One arena (spec 11). Layout itself is generated deterministically by Core.ArenaLayout.</summary>
    [CreateAssetMenu(fileName = "Arena", menuName = "Kitefall/Arena", order = 10)]
    public sealed class ArenaData : ScriptableObject
    {
        public ArenaType type = ArenaType.OldCityRooftops;
        public string displayName = "Old City Rooftops";
        [TextArea] public string description = "Shifting wind between roofs, power cables.";
        [Tooltip("Launch arenas are playable now; Update arenas unlock later.")]
        public bool availableAtLaunch = true;
        public WindConfig wind;

        [Header("Look")]
        public Color groundColor = new Color(0.55f, 0.50f, 0.40f);
        public Color[] buildingColors = { new Color(0.86f, 0.80f, 0.70f), new Color(0.66f, 0.40f, 0.30f), new Color(0.93f, 0.88f, 0.76f), new Color(0.55f, 0.66f, 0.74f) };
        public Color farColor = new Color(0.45f, 0.50f, 0.60f);
        public bool water;
        public Color waterColor = new Color(0.18f, 0.45f, 0.62f);
        public MusicStyle music = MusicStyle.OldCity;
    }
}
