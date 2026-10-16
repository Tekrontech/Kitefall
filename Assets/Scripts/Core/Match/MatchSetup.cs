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
        // ---- Upgrade v1.4 (cosmetics are visual only – never stats)
        public string characterId = "rookie_m";
        public string outfitId = "";
        /// <summary>Accessory ids by slot: Head, Eyes, Back, Waist ("" = none).</summary>
        public string[] accessories = new string[4];
        /// <summary>Plays the launch ritual automatically with "Good" results (setting / new players).</summary>
        public bool autoPrep;
        /// <summary>Training dummies: 0 static, 1 slow, 2 fast.</summary>
        public int dummyPattern;
        /// <summary>Training dummies: 0 low, 1 mid, 2 high.</summary>
        public int dummyHeight = 1;
        /// <summary>Party (squad) id – party members are teammates in Team Battle.</summary>
        public string partyId = "";
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

        // ---- Upgrade v1.4 ------------------------------------------------------
        /// <summary>Rooftop network: roof choice before the match and roof hopping (spec F9).</summary>
        public bool UseRoofGraph;
        /// <summary>Launch ritual instead of "HOLD to launch" (spec F10).</summary>
        public bool UseLaunchRitual;
        /// <summary>Waiting rooftop before the match, seconds (0 = skip; spec F3).</summary>
        public float WarmupTime;
        public float WarmupMin = 10f;
        /// <summary>Private room: host chose fixed roofs (no selection, no hopping).</summary>
        public bool FixedRoofs;
        public RoofSettings Roofs = new RoofSettings();
        public PrepSettings Prep = new PrepSettings();
        /// <summary>Training ground options (GameMode.Training only).</summary>
        public TrainingOptions Training;
    }
}
