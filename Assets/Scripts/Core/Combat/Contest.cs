using System;
using System.Numerics;

namespace Kitefall.Core
{
    public enum ContestOutcome
    {
        Running,
        /// <summary>A wins – B's line is cut.</summary>
        AWins,
        /// <summary>B wins – A's line is cut.</summary>
        BWins,
        /// <summary>3 s passed with −40 ≤ M ≤ +40.</summary>
        Neutral,
        /// <summary>A's own line broke (over-tension).</summary>
        SnapA,
        SnapB,
        /// <summary>Ended for another reason (round over, kite cut in another contest…).</summary>
        Cancelled
    }

    /// <summary>Every factor of the Power formula for one side on one tick (spec 9.5).</summary>
    public struct PowerBreakdown
    {
        public float ClassBase;
        public float DorCut;
        public float Input;
        public float Position;
        public float Angle;
        public float Wind;
        public float Counter;
        public float Charkhi;
        public float Condition;
        public float Phase;
        public float DiveBurst;
        public float Multi;
        public float Total;

        public float Product() => ClassBase * DorCut * Input * Position * Angle * Wind * Counter * Charkhi * Condition * Phase * DiveBurst * Multi;
    }

    /// <summary>What one kite brings to a contest on this tick.</summary>
    public struct ContestantView
    {
        public float ClassBase;
        public float DorCut;
        public float DorSpeed;
        public float DheelCounterBonus;
        public float LongRangeAngleBonus;
        public float KheenchPowerMult;
        public float Tension;
        public SweetZoneBand Sweet;
        public float Height;
        public float LineOut;
        public float Durability;
        public Vector3 KitePosition;
        /// <summary>How many contests this kite is in right now (2 = "Multi" penalty).</summary>
        public int ContestCount;
        /// <summary>Extra wind factor (Upwind roof: 1.03 for the first 60 s). 0 = none.</summary>
        public float WindBonus;
    }

    /// <summary>Things that are the same for both sides.</summary>
    public struct ContestContext
    {
        public bool Storm;
        public bool Gust;
        /// <summary>Unit vector the wind blows toward (horizontal).</summary>
        public Vector3 WindDirection;
    }

    /// <summary>Running totals for the results "why" screen (spec 9.7).</summary>
    public sealed class ContestSideStats
    {
        public int Ticks;
        public int SweetTicks;
        public int RedTicks;
        public double LogPosition, LogAngle, LogWind, LogCounter, LogInput, LogDive, LogCondition, LogPhase, LogClass, LogDor, LogCharkhi, LogMulti;
        public double SumPower;

        public void Add(in PowerBreakdown p, bool inSweet, bool inRed)
        {
            Ticks++;
            if (inSweet) SweetTicks++;
            if (inRed) RedTicks++;
            LogPosition += Math.Log(p.Position);
            LogAngle += Math.Log(p.Angle);
            LogWind += Math.Log(p.Wind);
            LogCounter += Math.Log(p.Counter);
            LogInput += Math.Log(p.Input);
            LogDive += Math.Log(p.DiveBurst);
            LogCondition += Math.Log(p.Condition);
            LogPhase += Math.Log(p.Phase);
            LogClass += Math.Log(p.ClassBase);
            LogDor += Math.Log(p.DorCut);
            LogCharkhi += Math.Log(p.Charkhi);
            LogMulti += Math.Log(p.Multi);
            SumPower += p.Total;
        }

        public float SweetFraction => Ticks == 0 ? 0f : SweetTicks / (float)Ticks;
        public float RedFraction => Ticks == 0 ? 0f : RedTicks / (float)Ticks;
        public float AveragePower => Ticks == 0 ? 0f : (float)(SumPower / Ticks);

        /// <summary>Geometric mean of a factor (log sum / ticks).</summary>
        public float Mean(double logSum) => Ticks == 0 ? 1f : (float)Math.Exp(logSum / Ticks);
    }

    /// <summary>One cut contest between kites A and B (spec 9.3 CUT_CONTEST).</summary>
    public sealed class Contest
    {
        public int A;
        public int B;
        public float StartTime;
        public float Elapsed;
        /// <summary>Contest meter, −100 … +100. Positive = toward A winning.</summary>
        public float M;
        public AttackType AttackA;
        public AttackType AttackB;
        public bool LockedA;
        public bool LockedB;
        public float AngleDeg;
        public Vector3 Point;
        public float AlongA;
        public float AlongB;
        public float SweetPhaseA;
        public float SweetPhaseB;
        public bool LateWin;
        public ContestOutcome Outcome = ContestOutcome.Running;
        public PowerBreakdown LastA;
        public PowerBreakdown LastB;
        public readonly ContestSideStats StatsA = new ContestSideStats();
        public readonly ContestSideStats StatsB = new ContestSideStats();

        public bool IsOver => Outcome != ContestOutcome.Running;
        public bool Involves(int kite) => A == kite || B == kite;
        public int Other(int kite) => kite == A ? B : A;

        /// <summary>Index of the winner, or -1 (neutral / snap / cancelled).</summary>
        public int Winner => Outcome == ContestOutcome.AWins ? A : Outcome == ContestOutcome.BWins ? B : -1;
        /// <summary>Index of the kite whose line was cut, or -1.</summary>
        public int Loser => Outcome == ContestOutcome.AWins ? B : Outcome == ContestOutcome.BWins ? A : -1;

        public AttackType AttackOf(int kite) => kite == A ? AttackA : AttackB;
        public ContestSideStats StatsOf(int kite) => kite == A ? StatsA : StatsB;
        /// <summary>Meter from <paramref name="kite"/>'s point of view (+100 = they win).</summary>
        public float MeterFor(int kite) => kite == A ? M : -M;
    }
}
