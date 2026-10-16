using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>
    /// Milestone 4 – the contest formula, exactly as spec 9.5:
    /// <code>
    /// Power = ClassBase × DorCut × Input × Position × Angle × Wind × Counter × Charkhi × Condition × PhaseMult × DiveBurst × Multi
    /// ΔM per tick = K × DorSpeed_leading × (Power_A − Power_B) + noise
    /// </code>
    /// Pure C#: the server runs this exact code.
    /// </summary>
    public sealed class ContestResolver
    {
        private readonly ContestSettings _s;
        private readonly DeterministicRandom _rng;

        public ContestSettings Settings => _s;

        public ContestResolver(ContestSettings settings, DeterministicRandom rng)
        {
            _s = settings ?? new ContestSettings();
            _rng = rng ?? new DeterministicRandom(1);
        }

        public PowerBreakdown ComputePower(in ContestantView me, in ContestantView them, AttackType mine, AttackType theirs,
            float elapsed, float angleDeg, in ContestContext ctx)
        {
            var p = new PowerBreakdown
            {
                ClassBase = me.ClassBase,
                DorCut = me.DorCut,
                Input = InputFactor(me),
                Position = KMath.Clamp(1f + _s.positionPerMeter * (me.Height - them.Height), _s.positionMin, _s.positionMax),
                Angle = AngleFactor(angleDeg, me),
                Wind = WindFactor(me, them, ctx),
                Counter = AttackRules.CounterMultiplier(mine, theirs, _s.counterWin, _s.counterLose, me.DheelCounterBonus),
                Charkhi = mine == AttackType.Kheench ? me.KheenchPowerMult : 1f,
                Condition = me.Durability < _s.lowDurability ? _s.lowDurabilityMult : 1f,
                Phase = ctx.Storm ? _s.stormPowerMult : 1f,
                DiveBurst = mine == AttackType.Dive && elapsed < _s.diveBurstTime ? _s.diveBurst : 1f,
                Multi = me.ContestCount > 1 ? _s.multiContestMult : 1f
            };
            p.Total = p.Product();
            return p;
        }

        public float InputFactor(in ContestantView me)
        {
            if (me.Sweet.Contains(me.Tension)) return _s.inputSweet;
            return me.Tension >= _s.redZone ? _s.inputRed : _s.inputOutside;
        }

        public float AngleFactor(float angleDeg, in ContestantView me)
        {
            float folded = angleDeg > 90f ? 180f - angleDeg : angleDeg;
            if (angleDeg >= _s.angleGoodMin && angleDeg <= _s.angleGoodMax)
                return me.LineOut > _s.longRangeMeters ? me.LongRangeAngleBonus : _s.angleGood;
            if (folded < _s.angleBadBelow) return _s.angleBad;
            return 1f;
        }

        /// <summary>0.95–1.05 favouring the kite on the side the wind comes from; ×1.08 for that side during a gust.</summary>
        public float WindFactor(in ContestantView me, in ContestantView them, in ContestContext ctx)
        {
            Vector3 fromWind = -ctx.WindDirection;
            Vector3 offset = me.KitePosition - them.KitePosition;
            offset.Y = 0f;
            float len = offset.Length();
            float side = len < 0.01f ? 0f : KMath.Clamp(Vector3.Dot(offset / len, fromWind), -1f, 1f);
            float wind = 1f + _s.windSideBonus * side;
            if (ctx.Gust && side > 0f) wind *= _s.gustWindMult;
            return wind;
        }

        /// <summary>Advances the contest one tick. Returns the outcome (Running while undecided).</summary>
        public ContestOutcome Tick(Contest c, in ContestantView a, in ContestantView b, in ContestContext ctx, float dt)
        {
            if (c.IsOver) return c.Outcome;

            PowerBreakdown pa = ComputePower(a, b, c.AttackA, c.AttackB, c.Elapsed, c.AngleDeg, ctx);
            PowerBreakdown pb = ComputePower(b, a, c.AttackB, c.AttackA, c.Elapsed, c.AngleDeg, ctx);
            c.LastA = pa;
            c.LastB = pb;
            c.StatsA.Add(pa, a.Sweet.Contains(a.Tension), a.Tension >= _s.redZone);
            c.StatsB.Add(pb, b.Sweet.Contains(b.Tension), b.Tension >= _s.redZone);

            float diff = pa.Total - pb.Total;
            float leadingSpeed = diff >= 0f ? a.DorSpeed : b.DorSpeed;
            float noise = _rng.Range(-_s.noise, _s.noise);
            // K is defined per 1/20 s tick; scale if a different tick length is used.
            float tickScale = dt * _s.tickRate;
            c.M = KMath.Clamp(c.M + (_s.contestK * leadingSpeed * diff + noise) * tickScale, -100f, 100f);
            c.Elapsed += dt;

            if (c.M >= 100f) c.Outcome = ContestOutcome.AWins;
            else if (c.M <= -100f) c.Outcome = ContestOutcome.BWins;
            else if (c.Elapsed >= _s.contestMaxTime - 1e-4f)
            {
                if (c.M > _s.lateWinThreshold) { c.Outcome = ContestOutcome.AWins; c.LateWin = true; }
                else if (c.M < -_s.lateWinThreshold) { c.Outcome = ContestOutcome.BWins; c.LateWin = true; }
                else c.Outcome = ContestOutcome.Neutral;
            }
            return c.Outcome;
        }

        /// <summary>Kite durability lost in a finished contest (spec 8.3: −3 to −8 %). More time in the red = more damage; Dheel saves durability.</summary>
        public float DurabilityLoss(ContestSideStats stats, AttackType attack, ContestOutcome outcome)
        {
            if (outcome == ContestOutcome.Neutral) return _s.neutralDurabilityLoss;
            if (attack == AttackType.Dheel) return _s.durabilityLossMin;
            return KMath.Lerp(_s.durabilityLossMin, _s.durabilityLossMax, stats.RedFraction);
        }
    }
}
