using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    public enum MatchPhase { Intro, Launch, Round, RoundEnd, LoadoutBreak, Results }

    public enum RoundEndReason { Cut, SelfSnap, Crash, Timer, Forfeit }

    public enum MatchEventType
    {
        PhaseChanged,
        RoundPhaseChanged,
        GustWarning,
        GustStarted,
        ArenaEvent,
        Launched,
        PerfectLaunch,
        ContactStarted,
        AttackLocked,
        ContestEnded,
        KiteCut,
        SelfSnap,
        Crash,
        Respawned,
        CrateSpawned,
        CrateCollected,
        StormShrink,
        ScoreChanged,
        Assist,
        RoundEnded,
        MatchEnded,
        Eliminated,
        DiveStarted,
        Disconnected,
        Reconnected
    }

    /// <summary>Something that happened this tick – drives effects, sounds, HUD messages and analytics.</summary>
    public struct MatchEvent
    {
        public MatchEventType Type;
        /// <summary>Main kite (victim, collector…), -1 if none.</summary>
        public int A;
        /// <summary>Second kite (cutter, opponent…), -1 if none.</summary>
        public int B;
        public float Value;
        public int Code;
        public Vector3 Position;

        public override string ToString() => $"{Type} A={A} B={B} v={Value:0.##} code={Code}";
    }

    /// <summary>Frozen copy of a finished contest for the results screen.</summary>
    [System.Serializable]
    public sealed class ContestSummary
    {
        public int A, B;
        public int Winner = -1;
        public int Loser = -1;
        public ContestOutcome Outcome;
        public AttackType AttackA, AttackB;
        public bool LateWin;
        public float Duration;
        public float FinalM;
        public float AngleDeg;
        public float Time;
        // Mean factors (geometric) per side
        public float PositionA, PositionB, AngleA, AngleB, WindA, WindB, CounterA, CounterB, InputA, InputB, DiveA, DiveB, ConditionA, ConditionB, PhaseA, PhaseB, ClassA, ClassB, DorA, DorB, CharkhiA, CharkhiB, MultiA, MultiB;
        public float SweetA, SweetB, RedA, RedB, PowerA, PowerB;

        public static ContestSummary From(Contest c, float time)
        {
            return new ContestSummary
            {
                A = c.A, B = c.B, Winner = c.Winner, Loser = c.Loser, Outcome = c.Outcome,
                AttackA = c.AttackA, AttackB = c.AttackB, LateWin = c.LateWin, Duration = c.Elapsed, FinalM = c.M,
                AngleDeg = c.AngleDeg, Time = time,
                PositionA = c.StatsA.Mean(c.StatsA.LogPosition), PositionB = c.StatsB.Mean(c.StatsB.LogPosition),
                AngleA = c.StatsA.Mean(c.StatsA.LogAngle), AngleB = c.StatsB.Mean(c.StatsB.LogAngle),
                WindA = c.StatsA.Mean(c.StatsA.LogWind), WindB = c.StatsB.Mean(c.StatsB.LogWind),
                CounterA = c.StatsA.Mean(c.StatsA.LogCounter), CounterB = c.StatsB.Mean(c.StatsB.LogCounter),
                InputA = c.StatsA.Mean(c.StatsA.LogInput), InputB = c.StatsB.Mean(c.StatsB.LogInput),
                DiveA = c.StatsA.Mean(c.StatsA.LogDive), DiveB = c.StatsB.Mean(c.StatsB.LogDive),
                ConditionA = c.StatsA.Mean(c.StatsA.LogCondition), ConditionB = c.StatsB.Mean(c.StatsB.LogCondition),
                PhaseA = c.StatsA.Mean(c.StatsA.LogPhase), PhaseB = c.StatsB.Mean(c.StatsB.LogPhase),
                ClassA = c.StatsA.Mean(c.StatsA.LogClass), ClassB = c.StatsB.Mean(c.StatsB.LogClass),
                DorA = c.StatsA.Mean(c.StatsA.LogDor), DorB = c.StatsB.Mean(c.StatsB.LogDor),
                CharkhiA = c.StatsA.Mean(c.StatsA.LogCharkhi), CharkhiB = c.StatsB.Mean(c.StatsB.LogCharkhi),
                MultiA = c.StatsA.Mean(c.StatsA.LogMulti), MultiB = c.StatsB.Mean(c.StatsB.LogMulti),
                SweetA = c.StatsA.SweetFraction, SweetB = c.StatsB.SweetFraction,
                RedA = c.StatsA.RedFraction, RedB = c.StatsB.RedFraction,
                PowerA = c.StatsA.AveragePower, PowerB = c.StatsB.AveragePower
            };
        }

        public bool Involves(int kite) => A == kite || B == kite;
        public AttackType AttackOf(int kite) => kite == A ? AttackA : AttackB;
    }

    [System.Serializable]
    public sealed class RoundRecord
    {
        public int Number;
        public int Winner = -1;
        public int Loser = -1;
        public RoundEndReason Reason;
        public float Duration;
        public ContestSummary Deciding;
    }

    public sealed class ParticipantResult
    {
        public int Index;
        public string PlayerId;
        public string DisplayName;
        public bool IsBot;
        public int Team;
        public int Placement;
        public bool Won;
        public bool Quit;
        public int RoundsWon;
        public int Score;
        public int Cuts;
        public int Assists;
        public int SelfSnaps;
        public int Crashes;
        public int ContestsWon;
        public int ContestsLost;
        /// <summary>Kites lost (count −1 each) – only in loss modes.</summary>
        public List<string> KitesLost = new List<string>();
        /// <summary>Durability at the end per kite id used.</summary>
        public Dictionary<string, float> DurabilityEnd = new Dictionary<string, float>();
        /// <summary>Dor meters lost to cuts, per dor id.</summary>
        public Dictionary<string, float> DorLost = new Dictionary<string, float>();
        /// <summary>Dor meters used (flown), per dor id.</summary>
        public Dictionary<string, float> DorUsed = new Dictionary<string, float>();
        public List<string> DorsUsed = new List<string>();
        public List<string> CharkhisUsed = new List<string>();
        public float SweetFraction;
        public int RankPoints;
    }

    public sealed class MatchResult
    {
        public GameMode Mode;
        public int Seed;
        public float Duration;
        public int WinnerIndex = -1;
        public int WinningTeam = -1;
        public bool Draw;
        public List<RoundRecord> Rounds = new List<RoundRecord>();
        public List<ParticipantResult> Participants = new List<ParticipantResult>();
        public List<ContestSummary> Contests = new List<ContestSummary>();
        public int[] TeamScores;

        public ParticipantResult For(int index) => Participants[index];
    }
}
