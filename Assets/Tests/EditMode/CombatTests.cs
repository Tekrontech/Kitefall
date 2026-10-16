using System.Collections.Generic;
using System.Numerics;
using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class ContestResolverTests
    {
        private static ContestantView View(float classBase, float height, float tension, float sweetCenter, int width = 15) => new ContestantView
        {
            ClassBase = classBase, DorCut = 1f, DorSpeed = 1f, DheelCounterBonus = 1.15f, LongRangeAngleBonus = 1.10f,
            KheenchPowerMult = 1f, Tension = tension, Sweet = new SweetZoneBand(sweetCenter, width), Height = height,
            LineOut = 100f, Durability = 100f, KitePosition = Vector3.Zero, ContestCount = 1
        };

        private static readonly ContestContext Calm = new ContestContext { WindDirection = Vector3.UnitZ };

        [Test]
        public void WorkedExample_FromSpec95()
        {
            // A = Guddi 5 m above, Dive Cut, in sweet zone. B = Rokkaku, Dheel Cut, also in its sweet zone. Angle 80°.
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            ContestantView a = View(1.00f, 15f, 50f, 50f);
            ContestantView b = View(1.05f, 10f, 70f, 70f);
            PowerBreakdown pa = r.ComputePower(a, b, AttackType.Dive, AttackType.Dheel, 0f, 80f, Calm);
            PowerBreakdown pb = r.ComputePower(b, a, AttackType.Dheel, AttackType.Dive, 0f, 80f, Calm);
            Assert.AreEqual(1.739f, pa.Total, 0.002f);
            Assert.AreEqual(0.904f, pb.Total, 0.002f);
        }

        [Test]
        public void WorkedExample_MeterMovesAboutFivePerTick_AndAWinsInAboutOneSecond()
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            var c = new Contest { A = 0, B = 1, AngleDeg = 80f, AttackA = AttackType.Dive, AttackB = AttackType.Dheel, LockedA = true, LockedB = true };
            ContestantView a = View(1.00f, 15f, 50f, 50f);
            ContestantView b = View(1.05f, 10f, 70f, 70f);
            r.Tick(c, a, b, Calm, 0.05f);
            Assert.AreEqual(5f, c.M, 0.6f);
            int ticks = 1;
            while (!c.IsOver && ticks < 100) { r.Tick(c, a, b, Calm, 0.05f); ticks++; }
            Assert.AreEqual(ContestOutcome.AWins, c.Outcome);
            Assert.That(ticks * 0.05f, Is.InRange(0.6f, 1.4f));
        }

        [Test]
        public void PanickingIntoRed_DropsInputToQuarter()
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            ContestantView a = View(1f, 15f, 90f, 50f);
            Assert.AreEqual(0.25f, r.InputFactor(a), 1e-5f);
            ContestantView outside = View(1f, 15f, 70f, 40f);
            Assert.AreEqual(0.55f, r.InputFactor(outside), 1e-5f);
        }

        [TestCase(AttackType.Kheench, AttackType.Dive, 1.15f)]
        [TestCase(AttackType.Dive, AttackType.Dheel, 1.15f)]
        [TestCase(AttackType.Dheel, AttackType.Kheench, 1.15f)]
        [TestCase(AttackType.Dive, AttackType.Kheench, 0.87f)]
        [TestCase(AttackType.Kheench, AttackType.Kheench, 1.00f)]
        [TestCase(AttackType.Hold, AttackType.Dive, 1.00f)]
        public void CounterTriangle(AttackType mine, AttackType theirs, float expected)
        {
            Assert.AreEqual(expected, AttackRules.CounterMultiplier(mine, theirs, 1.15f, 0.87f, 1.15f), 1e-5f);
        }

        [Test]
        public void Lachakdaar_DheelCounterBonusIs120()
        {
            Assert.AreEqual(1.20f, AttackRules.CounterMultiplier(AttackType.Dheel, AttackType.Kheench, 1.15f, 0.87f, 1.20f), 1e-5f);
        }

        [TestCase(80f, 1.10f)]
        [TestCase(120f, 1.10f)]
        [TestCase(20f, 0.90f)]
        [TestCase(165f, 0.90f)]
        [TestCase(45f, 1.00f)]
        public void AngleFactor(float angle, float expected)
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            Assert.AreEqual(expected, r.AngleFactor(angle, View(1f, 0f, 50f, 50f)), 1e-5f);
        }

        [Test]
        public void Lambi_LongLine_UsesLongRangeBonus()
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            ContestantView v = View(1f, 0f, 50f, 50f);
            v.LineOut = 600f;
            v.LongRangeAngleBonus = 1.15f;
            Assert.AreEqual(1.15f, r.AngleFactor(90f, v), 1e-5f);
        }

        [Test]
        public void PositionFactor_IsClamped()
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            PowerBreakdown p = r.ComputePower(View(1f, 100f, 50f, 50f), View(1f, 0f, 50f, 50f), AttackType.Hold, AttackType.Hold, 0f, 45f, Calm);
            Assert.AreEqual(1.15f, p.Position, 1e-5f);
        }

        [Test]
        public void EqualPlayers_EndNeutral_AndNoiseStaysSmall()
        {
            int neutral = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(seed));
                var c = new Contest { AttackA = AttackType.Hold, AttackB = AttackType.Hold };
                ContestantView v = View(1f, 10f, 50f, 50f);
                while (!c.IsOver) r.Tick(c, v, v, Calm, 0.05f);
                if (c.Outcome == ContestOutcome.Neutral) neutral++;
            }
            Assert.Greater(neutral, 45, "With equal power only noise moves the meter (< 5 % of outcome).");
        }

        [Test]
        public void WindFactor_FavoursUpwindKite_AndGust()
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            ContestantView upwind = View(1f, 0f, 50f, 50f);
            upwind.KitePosition = new Vector3(0f, 0f, -10f); // wind blows to +Z, so −Z is upwind
            ContestantView downwind = View(1f, 0f, 50f, 50f);
            downwind.KitePosition = new Vector3(0f, 0f, 10f);
            Assert.AreEqual(1.05f, r.WindFactor(upwind, downwind, Calm), 1e-4f);
            Assert.AreEqual(0.95f, r.WindFactor(downwind, upwind, Calm), 1e-4f);
            var gust = new ContestContext { WindDirection = Vector3.UnitZ, Gust = true };
            Assert.AreEqual(1.05f * 1.08f, r.WindFactor(upwind, downwind, gust), 1e-4f);
        }

        [Test]
        public void SameSeed_SameContest()
        {
            float Run(int seed)
            {
                var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(seed));
                var c = new Contest();
                for (int i = 0; i < 30; i++) r.Tick(c, View(1f, 11f, 50f, 50f), View(1f, 10f, 50f, 50f), Calm, 0.05f);
                return c.M;
            }
            Assert.AreEqual(Run(9), Run(9));
        }

        [Test]
        public void DurabilityLoss_Between3And8_DheelSaves()
        {
            var r = new ContestResolver(new ContestSettings(), new DeterministicRandom(1));
            var stats = new ContestSideStats();
            Assert.AreEqual(3f, r.DurabilityLoss(stats, AttackType.Dheel, ContestOutcome.AWins), 1e-5f);
            Assert.AreEqual(5f, r.DurabilityLoss(stats, AttackType.Kheench, ContestOutcome.Neutral), 1e-5f);
            var p = new PowerBreakdown { Position = 1, Angle = 1, Wind = 1, Counter = 1, Input = 1, DiveBurst = 1, Condition = 1, Phase = 1, ClassBase = 1, DorCut = 1, Charkhi = 1, Multi = 1 };
            for (int i = 0; i < 10; i++) stats.Add(p, false, true);
            Assert.AreEqual(8f, r.DurabilityLoss(stats, AttackType.Kheench, ContestOutcome.AWins), 1e-5f);
        }
    }

    public class SweetZoneTests
    {
        [Test]
        public void Band_MovesBetweenMinAndMax()
        {
            var s = new SweetZoneSettings();
            float min = 999, max = -999;
            for (float t = 0; t < 5; t += 0.05f)
            {
                SweetZoneBand b = SweetZoneBand.At(s, t, 0.3f, 15);
                min = System.Math.Min(min, b.Center);
                max = System.Math.Max(max, b.Center);
                Assert.AreEqual(15f, b.Width);
            }
            Assert.AreEqual(s.centerMin, min, 0.5f);
            Assert.AreEqual(s.centerMax, max, 0.5f);
        }

        [Test]
        public void Contains_UsesHalfWidth()
        {
            var b = new SweetZoneBand(50f, 10f);
            Assert.IsTrue(b.Contains(54f));
            Assert.IsFalse(b.Contains(56f));
        }
    }

    public class LineGeometryTests
    {
        [Test]
        public void CrossingSegments_HaveZeroDistance()
        {
            float d = LineGeometry.SegmentDistance(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 1, 0), out _, out _, out _, out _);
            Assert.AreEqual(0f, d, 1e-5f);
        }

        [Test]
        public void SkewSegments_DistanceIsGap()
        {
            float d = LineGeometry.SegmentDistance(new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, -1, 2), new Vector3(0, 1, 2), out _, out _, out _, out _);
            Assert.AreEqual(2f, d, 1e-5f);
        }

        [Test]
        public void ParallelSegments()
        {
            float d = LineGeometry.SegmentDistance(new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(2, 3, 0), new Vector3(8, 3, 0), out _, out _, out _, out _);
            Assert.AreEqual(3f, d, 1e-5f);
        }

        [Test]
        public void BuildLine_StartsAtAnchor_EndsAtKite_AndSagsWhenSlack()
        {
            var pts = new Vector3[9];
            LineGeometry.BuildLine(Vector3.Zero, new Vector3(0, 50, 50), 0f, 0.06f, pts);
            Assert.AreEqual(Vector3.Zero, pts[0]);
            Assert.AreEqual(new Vector3(0, 50, 50), pts[8]);
            Assert.Less(pts[4].Y, 25f, "Slack line sags below the straight line");
            LineGeometry.BuildLine(Vector3.Zero, new Vector3(0, 50, 50), 1f, 0.06f, pts);
            Assert.AreEqual(25f, pts[4].Y, 1e-3f);
        }

        [Test]
        public void CrossingAngle()
        {
            Assert.AreEqual(90f, LineGeometry.CrossingAngle(Vector3.UnitX, Vector3.UnitY), 1e-3f);
            Assert.AreEqual(0f, LineGeometry.CrossingAngle(Vector3.UnitX, Vector3.UnitX * 3), 1e-2f);
        }
    }

    public class ContactDetectorTests
    {
        private static Vector3[] Line(Vector3 a, Vector3 b)
        {
            var pts = new Vector3[5];
            LineGeometry.BuildLine(a, b, 1f, 0f, pts);
            return pts;
        }

        [Test]
        public void TouchingLines_MakeContact_OnceUntilReleased()
        {
            var d = new ContactDetector(new ContactSettings(), 2);
            var lines = new List<Vector3[]> { Line(new Vector3(-20, 0, 0), new Vector3(20, 40, 40)), Line(new Vector3(20, 0, 0), new Vector3(-20, 40, 40)) };
            var active = new List<bool> { true, true };
            var events = new List<ContactEvent>();
            d.Step(0f, lines, active, null, events);
            Assert.AreEqual(1, events.Count);
            Assert.That(events[0].AngleDeg, Is.GreaterThan(30f));
            events.Clear();
            d.Step(0.05f, lines, active, null, events);
            Assert.AreEqual(0, events.Count, "Blocked while in a contest");
        }

        [Test]
        public void FarLines_NoContact_ButApproachIsTracked()
        {
            var d = new ContactDetector(new ContactSettings(), 2);
            var lines = new List<Vector3[]> { Line(new Vector3(0, 0, 0), new Vector3(0, 40, 40)), Line(new Vector3(10, 0, 0), new Vector3(10, 40, 40)) };
            var events = new List<ContactEvent>();
            d.Step(0f, lines, new List<bool> { true, true }, null, events);
            Assert.AreEqual(0, events.Count);
            Assert.IsTrue(d.IsApproaching(0));
            Assert.AreEqual(1, d.NearestOther(0));
        }

        [Test]
        public void LinesPassingThroughBetweenTicks_StillMakeContact()
        {
            var d = new ContactDetector(new ContactSettings(), 2);
            var a = Line(new Vector3(0, 0, 0), new Vector3(0, 40, 40));
            var events = new List<ContactEvent>();
            var active = new List<bool> { true, true };
            d.Step(0f, new List<Vector3[]> { a, Line(new Vector3(-30, 20, 20.0f), new Vector3(30, 21.5f, 20.0f)) }, active, null, events);
            Assert.AreEqual(0, events.Count);
            d.Step(0.05f, new List<Vector3[]> { a, Line(new Vector3(-30, 18, 20.0f), new Vector3(30, 18.5f, 20.0f)) }, active, null, events);
            Assert.AreEqual(1, events.Count);
        }

        [Test]
        public void NeutralCooldown_BlocksRecontact()
        {
            var d = new ContactDetector(new ContactSettings(), 2);
            var lines = new List<Vector3[]> { Line(new Vector3(-20, 0, 0), new Vector3(20, 40, 40)), Line(new Vector3(20, 0, 0), new Vector3(-20, 40, 40)) };
            var active = new List<bool> { true, true };
            var events = new List<ContactEvent>();
            d.Step(0f, lines, active, null, events);
            d.Release(0, 1, 3f, cooldown: true);
            events.Clear();
            d.Step(3.5f, lines, active, null, events);
            Assert.AreEqual(0, events.Count);
            d.Step(4.1f, lines, active, null, events);
            Assert.AreEqual(1, events.Count);
        }

        [Test]
        public void TeamFilter_PreventsContact()
        {
            var d = new ContactDetector(new ContactSettings(), 2);
            var lines = new List<Vector3[]> { Line(new Vector3(-20, 0, 0), new Vector3(20, 40, 40)), Line(new Vector3(20, 0, 0), new Vector3(-20, 40, 40)) };
            var events = new List<ContactEvent>();
            d.Step(0f, lines, new List<bool> { true, true }, (x, y) => false, events);
            Assert.AreEqual(0, events.Count);
        }
    }

    public class TailRopeTests
    {
        [Test]
        public void NoWind_HangsDown_AndKeepsLength()
        {
            var rope = new TailRope(new TailRopeSettings { flutter = 0f }, 10, 3f);
            rope.Reset(new Vector3(0, 10, 0), Vector3.UnitX);
            for (int i = 0; i < 600; i++) rope.Step(new Vector3(0, 10, 0), Vector3.Zero, i * 0.02f, 0.02f);
            Assert.Less(rope[10].Y, 7.3f, "Tail end hangs below the kite");
            for (int i = 0; i < 10; i++) Assert.AreEqual(0.3f, Vector3.Distance(rope[i], rope[i + 1]), 0.03f);
        }

        [Test]
        public void Wind_PushesTailDownwind()
        {
            var rope = new TailRope(new TailRopeSettings { flutter = 0f }, 10, 3f);
            rope.Reset(new Vector3(0, 10, 0), -Vector3.UnitY);
            for (int i = 0; i < 600; i++) rope.Step(new Vector3(0, 10, 0), new Vector3(0, 0, 12f), i * 0.02f, 0.02f);
            Assert.Greater(rope[10].Z, 1.5f);
        }

        [Test]
        public void FollowsMovingKite()
        {
            var rope = new TailRope(new TailRopeSettings(), 6, 2f);
            rope.Reset(Vector3.Zero, -Vector3.UnitY);
            for (int i = 0; i < 300; i++) rope.Step(new Vector3(i * 0.1f, 0, 0), Vector3.Zero, i * 0.02f, 0.02f);
            Assert.Less(Vector3.Distance(rope[0], new Vector3(29.9f, 0, 0)), 0.01f);
            Assert.Less(Vector3.Distance(rope[6], rope[0]), 2.1f);
        }
    }

    public class RoundPhaseTests
    {
        [Test]
        public void Phases_FollowSpecTimings()
        {
            var c = new RoundPhaseController(new PhaseSettings());
            c.Reset(1, true);
            Assert.AreEqual(RoundPhase.Calm, c.PhaseAt(30f));
            Assert.AreEqual(RoundPhase.Gust, c.PhaseAt(90f));
            Assert.AreEqual(RoundPhase.Storm, c.PhaseAt(150f));
        }

        [Test]
        public void QuickDuel_HasNoStorm()
        {
            var c = new RoundPhaseController(new PhaseSettings());
            c.Reset(1, false);
            Assert.AreEqual(RoundPhase.Gust, c.PhaseAt(150f));
        }

        [Test]
        public void Gusts_Only_InGustPhase_Every15To20s_WithWarning()
        {
            var c = new RoundPhaseController(new PhaseSettings());
            c.Reset(3, true);
            var gustTimes = new List<float>();
            int warnings = 0;
            for (float t = 0.05f; t < 180f; t += 0.05f)
            {
                c.Step(t, out _, out bool warn, out bool gust, out _);
                if (warn) warnings++;
                if (gust) gustTimes.Add(t);
            }
            Assert.That(gustTimes.Count, Is.InRange(3, 5));
            Assert.AreEqual(gustTimes.Count, warnings);
            foreach (float g in gustTimes) Assert.That(g, Is.InRange(60f, 120f));
            for (int i = 1; i < gustTimes.Count; i++) Assert.That(gustTimes[i] - gustTimes[i - 1], Is.InRange(14.9f, 20.1f));
        }

        [Test]
        public void StormWind_IsStronger()
        {
            var c = new RoundPhaseController(new PhaseSettings());
            c.Reset(1, true);
            c.Step(150f, out _, out _, out _, out _);
            Assert.AreEqual(1.5f, c.WindMultiplier, 1e-4f);
        }
    }
}
