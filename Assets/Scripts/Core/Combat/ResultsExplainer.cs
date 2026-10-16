using System.Collections.Generic;
using System.Text;

namespace Kitefall.Core
{
    /// <summary>
    /// Milestone 9 – the results "why" breakdown (spec 9.7, rule 3: every lost contest must be explainable).
    /// Compares the mean factors of both sides and names the biggest differences.
    /// </summary>
    public static class ResultsExplainer
    {
        public struct Factor
        {
            public string Name;
            /// <summary>My factor ÷ their factor. &gt; 1 = in my favour.</summary>
            public float Ratio;
            public string Positive;
            public string Negative;
        }

        public static List<Factor> Factors(ContestSummary c, int me)
        {
            bool a = c.A == me;
            float P(float x, float y) => y <= 0f ? 1f : x / y;
            var list = new List<Factor>
            {
                new Factor { Name = "height", Ratio = a ? P(c.PositionA, c.PositionB) : P(c.PositionB, c.PositionA), Positive = "height advantage", Negative = "opponent was above you" },
                new Factor { Name = "counter", Ratio = a ? P(c.CounterA, c.CounterB) : P(c.CounterB, c.CounterA), Positive = "counter attack", Negative = "their attack countered yours" },
                new Factor { Name = "sweet", Ratio = a ? P(c.InputA, c.InputB) : P(c.InputB, c.InputA), Positive = "better sweet-zone control", Negative = "tension outside the sweet zone" },
                new Factor { Name = "angle", Ratio = a ? P(c.AngleA, c.AngleB) : P(c.AngleB, c.AngleA), Positive = "good crossing angle", Negative = "poor crossing angle" },
                new Factor { Name = "wind", Ratio = a ? P(c.WindA, c.WindB) : P(c.WindB, c.WindA), Positive = "wind on your side", Negative = "opponent had the wind" },
                new Factor { Name = "dive", Ratio = a ? P(c.DiveA, c.DiveB) : P(c.DiveB, c.DiveA), Positive = "dive burst", Negative = "their dive burst" },
                new Factor { Name = "condition", Ratio = a ? P(c.ConditionA, c.ConditionB) : P(c.ConditionB, c.ConditionA), Positive = "kite in better condition", Negative = "damaged kite (durability < 30 %)" },
                new Factor { Name = "kite", Ratio = a ? P(c.ClassA * c.DorA * c.CharkhiA, c.ClassB * c.DorB * c.CharkhiB) : P(c.ClassB * c.DorB * c.CharkhiB, c.ClassA * c.DorA * c.CharkhiA), Positive = "stronger build for this fight", Negative = "their build was stronger here" },
                new Factor { Name = "multi", Ratio = a ? P(c.MultiA, c.MultiB) : P(c.MultiB, c.MultiA), Positive = "opponent was fighting two kites", Negative = "you were fighting two kites" },
            };
            list.Sort((x, y) => System.Math.Abs(System.Math.Log(y.Ratio)).CompareTo(System.Math.Abs(System.Math.Log(x.Ratio))));
            return list;
        }

        /// <summary>One-line explanation of a contest for the player <paramref name="me"/>.</summary>
        public static string Explain(ContestSummary c, int me)
        {
            if (c == null) return string.Empty;
            bool a = c.A == me;
            float mySweet = a ? c.SweetA : c.SweetB;
            AttackType mine = c.AttackOf(me);
            AttackType theirs = c.AttackOf(a ? c.B : c.A);

            if (c.Outcome == ContestOutcome.SnapA || c.Outcome == ContestOutcome.SnapB)
            {
                bool iSnapped = (c.Outcome == ContestOutcome.SnapA) == a;
                return iSnapped
                    ? "Your line snapped: tension stayed above your dor's Strength too long. Release earlier when the bar flashes red."
                    : "Opponent's line snapped from over-tension – you kept your nerve.";
            }

            bool won = c.Winner == me;
            bool lost = c.Loser == me;
            List<Factor> factors = Factors(c, me);
            var sb = new StringBuilder();

            if (won || lost)
            {
                sb.Append(won ? "You won: " : "You lost: ");
                int added = 0;
                foreach (Factor f in factors)
                {
                    bool favours = f.Ratio > 1.005f;
                    bool hurts = f.Ratio < 0.995f;
                    if (won && favours || lost && hurts)
                    {
                        if (added > 0) sb.Append(" + ");
                        sb.Append(won ? f.Positive : f.Negative);
                        if (++added == 2) break;
                    }
                }
                if (added == 0) sb.Append(won ? "steady pressure" : "the meter just went their way");
                sb.Append(" · sweet zone ").Append((int)(mySweet * 100f + 0.5f)).Append('%');
                if (lost) sb.Append(". ").Append(Tip(mine, theirs, factors, mySweet));
                return sb.ToString();
            }

            if (c.Outcome == ContestOutcome.Neutral) return "Neutral – nobody pulled the meter past 40 in 3 s. Both kites lost a little durability.";
            return string.Empty;
        }

        public static string Tip(AttackType mine, AttackType theirs, List<Factor> factors, float mySweet)
        {
            if (AttackRules.Beats(theirs, mine))
                return $"{AttackRules.DisplayName(theirs)} beats {AttackRules.DisplayName(mine)} – try {AttackRules.DisplayName(AttackRules.CounterOf(theirs))} next time.";
            if (mySweet < 0.5f) return "Keep the tension marker inside the moving sweet zone during the contest.";
            if (factors.Count > 0 && factors[0].Name == "height") return "Get above your opponent before the lines cross.";
            if (factors.Count > 0 && factors[0].Name == "angle") return "Cross their line at a wide angle (60–120°).";
            return "Read the wind and attack from the upwind side.";
        }

        /// <summary>Text for how a duel round ended.</summary>
        public static string ExplainRound(RoundRecord r, int me)
        {
            bool iWon = r.Winner == me;
            switch (r.Reason)
            {
                case RoundEndReason.Cut:
                    return r.Deciding != null ? Explain(r.Deciding, me) : (iWon ? "You cut their line." : "Your line was cut.");
                case RoundEndReason.SelfSnap:
                    return iWon ? "Opponent's line snapped from over-tension." : "Your line snapped – you stayed above Strength for more than 0.6 s.";
                case RoundEndReason.Crash:
                    return iWon ? "Opponent crashed their kite." : "You crashed – keep height and watch the roofs.";
                case RoundEndReason.Timer:
                    if (r.Winner < 0) return "Time up – same height and durability: draw.";
                    return iWon ? "Time up – your kite was higher / in better shape." : "Time up – their kite was higher / in better shape.";
                case RoundEndReason.Forfeit:
                    return iWon ? "Opponent disconnected." : "You were disconnected too long.";
            }
            return string.Empty;
        }
    }
}
