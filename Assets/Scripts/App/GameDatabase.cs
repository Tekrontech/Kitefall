using System.Collections.Generic;
using Kitefall.Core;
using Kitefall.Data;
using UnityEngine;

namespace Kitefall.App
{
    /// <summary>
    /// Index of every data asset the game uses. One asset, referenced by GameApp.
    /// <see cref="ApplyToCore"/> pushes the asset values into Core so match rules and economy
    /// use exactly what is in the Inspector (or Remote Config).
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Kitefall/Game Database", order = 30)]
    public sealed class GameDatabase : ScriptableObject
    {
        public List<KiteClassData> kites = new List<KiteClassData>();
        public List<TailData> tails = new List<TailData>();
        public List<DorData> dors = new List<DorData>();
        public List<CharkhiData> charkhis = new List<CharkhiData>();
        public List<TuningData> tunings = new List<TuningData>();
        public List<ArenaData> arenas = new List<ArenaData>();

        public CombatConfig combat;
        public FlightConfig flight;
        public CameraConfig camera;
        public BalanceConfig balance;
        public WindConfig defaultWind;

        public KiteClassData Kite(string id) => kites.Find(k => k != null && k.id == id);
        public TailData Tail(string id) => tails.Find(k => k != null && k.id == id);
        public DorData Dor(string id) => dors.Find(k => k != null && k.id == id);
        public CharkhiData Charkhi(string id) => charkhis.Find(k => k != null && k.id == id);
        public TuningData Tuning(string id) => tunings.Find(k => k != null && k.id == id);
        public ArenaData Arena(ArenaType type) => arenas.Find(a => a != null && a.type == type);

        public void ApplyToCore()
        {
            foreach (KiteClassData k in kites) if (k != null) SpecDefaults.Register(k.ToStats(), k.displayName);
            foreach (TailData t in tails) if (t != null) SpecDefaults.Register(t.ToStats(), t.displayName);
            foreach (DorData d in dors) if (d != null) SpecDefaults.Register(d.ToStats(), d.displayName);
            foreach (CharkhiData c in charkhis) if (c != null) SpecDefaults.Register(c.ToStats(), c.displayName);
            foreach (TuningData t in tunings) if (t != null) SpecDefaults.Register(t.ToStats(), t.displayName);
        }

        /// <summary>Fills a match setup's tunables from the assets.</summary>
        public void Configure(MatchSetup setup)
        {
            BalanceConfig b = balance;
            if (combat != null)
            {
                setup.Tension = combat.ToTensionSettings();
                setup.Contest = combat.ToContestSettings(b != null ? b.contest : null);
                setup.Contact = combat.ToContactSettings(b != null ? b.contact : null);
                setup.DheelOnPullRelease = combat.dheelOnPullRelease;
            }
            if (flight != null) setup.Flight = flight.settings;
            if (b != null)
            {
                setup.SweetZone = b.sweetZone;
                setup.Phases = b.phases;
                setup.Crates = b.crates;
                setup.Line = b.line;
            }
            ArenaData arena = Arena(setup.Arena);
            WindConfig wind = arena != null && arena.wind != null ? arena.wind : defaultWind;
            if (wind != null) setup.Wind = wind.settings;
        }
    }
}
