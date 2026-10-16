using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>One kite in a participant's stack, with the raw stats it is built from.</summary>
    [Serializable]
    public struct ParticipantLoadout
    {
        public string kiteId;
        public string tailId;
        public string dorId;
        public string charkhiId;
        public string skinId;
        public KiteClassStats kite;
        public TailStats tail;
        public DorStats dor;
        public CharkhiStats charkhi;
        public TuningStats[] tunings;
        /// <summary>Kite durability at match start, 0–100.</summary>
        public float durability;
        /// <summary>Metres of this dor the player owns.</summary>
        public float dorStockMeters;

        public LoadoutStats Compose() => new LoadoutStats(kite, tail, dor, charkhi, tunings);
    }

    [Serializable]
    public sealed class ParticipantSetup
    {
        public string playerId;
        public string displayName;
        public string country;
        public bool isBot;
        public BotDifficulty botDifficulty;
        public int team;
        public bool captain;
        public int rankPoints;
        /// <summary>Kites in order (FFA stack of 3; duels repeat the first).</summary>
        public List<ParticipantLoadout> stack = new List<ParticipantLoadout>();
    }

    /// <summary>Everything needed to start a match. The server builds this and sends the same to all clients.</summary>
    public sealed class MatchSetup
    {
        public ModeRules Rules;
        public ArenaType Arena = ArenaType.OldCityRooftops;
        public int Seed = 1;
        public List<ParticipantSetup> Participants = new List<ParticipantSetup>();

        public TensionSettings Tension = new TensionSettings();
        public FlightSettings Flight = new FlightSettings();
        public LineSettings Line = new LineSettings();
        public WindSettings Wind = new WindSettings();
        public ContactSettings Contact = new ContactSettings();
        public ContestSettings Contest = new ContestSettings();
        public SweetZoneSettings SweetZone = new SweetZoneSettings();
        public PhaseSettings Phases = new PhaseSettings();
        public CrateSettings Crates = new CrateSettings();
        public bool DheelOnPullRelease = true;

        /// <summary>Always-available free Guddi (spec 8.3) – used when a stack runs out.</summary>
        public ParticipantLoadout FreeGuddi;
        /// <summary>Basic gear for Last Kite Flying (Sooti + Lakri).</summary>
        public DorStats BasicDor;
        public CharkhiStats BasicCharkhi;
        /// <summary>What sky crates can give (spec 7.7).</summary>
        public List<DorStats> CrateDors = new List<DorStats>();
        public List<CharkhiStats> CrateCharkhis = new List<CharkhiStats>();

        /// <summary>0–1 time of day for visuals (randomised per match, spec 11).</summary>
        public float TimeOfDay01 = 0.75f;
    }
}
