using System.Numerics;
using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class KiteFlightSimTests
    {
        private const float Dt = 0.02f;
        private const float AnchorHeight = 10f;

        /// <summary>Settings with wobble off so every test is exactly repeatable.</summary>
        private static FlightSettings Calm() => new FlightSettings { wobbleDegPerSec = 0f };

        private static KiteFlightSim NewSim(FlightSettings s = null)
        {
            var sim = new KiteFlightSim(s ?? Calm(), TestData.AllRounder, AnchorHeight);
            sim.Reset(0f, 60f);
            return sim;
        }

        private static void Fly(KiteFlightSim sim, float steerX, float steerY, LineAction action, float seconds,
            float tension01 = 0.6f, float lineOut = 60f, WindSample? wind = null)
        {
            var input = new FlightInput
            {
                SteerX = steerX, SteerY = steerY, Action = action,
                Wind = wind ?? TestData.SteadyWind(), Tension01 = tension01, LineOut = lineOut
            };
            int steps = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                input.Time = i * Dt;
                sim.Step(input, Dt);
            }
        }

        [Test]
        public void Reset_PutsKiteDownwindAtStartElevation()
        {
            var sim = NewSim();
            Assert.AreEqual(0f, sim.AzimuthDeg, 0.001f);
            Assert.AreEqual(35f, sim.ElevationDeg, 0.001f);
            Assert.AreEqual(60f, sim.Radius, 0.001f);
            Assert.Greater(sim.LocalPosition.Z, 0f, "Kite should be downwind (+Z) of the flyer");
        }

        [Test]
        public void SteerUp_IncreasesElevation()
        {
            var sim = NewSim();
            float before = sim.ElevationDeg;
            Fly(sim, 0, 1, LineAction.Pull, 1.5f);
            Assert.Greater(sim.ElevationDeg, before);
        }

        [Test]
        public void SteerDown_DecreasesElevation()
        {
            var sim = NewSim();
            float before = sim.ElevationDeg;
            Fly(sim, 0, -1, LineAction.Pull, 1.5f);
            Assert.Less(sim.ElevationDeg, before);
        }

        [Test]
        public void SteerRight_MovesRight_SteerLeft_MovesLeft()
        {
            var right = NewSim();
            Fly(right, 1, 0, LineAction.Pull, 1.5f);
            Assert.Greater(right.AzimuthDeg, 0f);
            Assert.Greater(right.LocalPosition.X, 0f);

            var left = NewSim();
            Fly(left, -1, 0, LineAction.Pull, 1.5f);
            Assert.Less(left.AzimuthDeg, 0f);
            Assert.Less(left.LocalPosition.X, 0f);
        }

        [Test]
        public void Heading_TurnsAtLoadoutTurnRate()
        {
            var sim = NewSim();
            // Guddi + short ribbon = 180°/s → after 0.24 s (12 ticks) heading is 43.2° toward the right.
            Fly(sim, 1, 0, LineAction.Idle, 0.24f);
            Assert.AreEqual(43.2f, sim.HeadingDeg, 0.01f);
        }

        [Test]
        public void NoInput_HeadingSelfRights()
        {
            var sim = NewSim();
            Fly(sim, 1, 0, LineAction.Idle, 0.5f);
            Assert.Greater(sim.HeadingDeg, 45f);
            Fly(sim, 0, 0, LineAction.Idle, 5f);
            Assert.AreEqual(0f, sim.HeadingDeg, 0.01f);
        }

        [Test]
        public void Pull_IsFasterThanDheel()
        {
            var pull = NewSim();
            Fly(pull, 0, 1, LineAction.Pull, 2f);
            var dheel = NewSim();
            Fly(dheel, 0, 1, LineAction.Dheel, 2f);
            Assert.Greater(pull.Speed, dheel.Speed * 2f);
        }

        [Test]
        public void StrongerWind_MakesKiteFaster()
        {
            var calm = NewSim();
            Fly(calm, 0, 1, LineAction.Idle, 2f, wind: TestData.SteadyWind(3f));
            var windy = NewSim();
            Fly(windy, 0, 1, LineAction.Idle, 2f, wind: TestData.SteadyWind(12f));
            Assert.Greater(windy.Speed, calm.Speed);
        }

        [Test]
        public void Kite_StaysInsideWindWindow()
        {
            var sim = NewSim();
            Fly(sim, 1, 0, LineAction.Pull, 30f);
            Assert.LessOrEqual(sim.AzimuthDeg, 75f + 0.01f);
            Assert.LessOrEqual(sim.ElevationDeg, 82f + 0.01f);
        }

        [Test]
        public void WindShift_PushesKiteBackIntoWindow()
        {
            var sim = NewSim();
            // Wind suddenly blows toward 120°: kite (at 0°) is now 120° off, window is ±75°.
            Fly(sim, 0, 0, LineAction.Idle, 5f, wind: TestData.SteadyWind(6f, 120f));
            float rel = KMath.DeltaAngle(120f, sim.AzimuthDeg);
            Assert.LessOrEqual(System.Math.Abs(rel), 75f + 0.01f);
        }

        [Test]
        public void Distance_NeverExceedsLineOut()
        {
            var sim = NewSim();
            float lineOut = 60f;
            for (int i = 0; i < 2000; i++)
            {
                lineOut = i < 1000 ? 60f + i * 0.1f : 160f - (i - 1000) * 0.12f;
                var input = new FlightInput
                {
                    SteerX = KMath.Sin(i * 0.05f), SteerY = KMath.Cos(i * 0.031f),
                    Action = (LineAction)(i / 50 % 4),
                    Wind = TestData.SteadyWind(8f), Tension01 = 0.5f, LineOut = lineOut, Time = i * Dt
                };
                sim.Step(input, Dt);
                if (sim.IsCrashed) sim.Reset(0f, lineOut);
                Assert.LessOrEqual(sim.Radius, lineOut + 0.001f);
                Assert.AreEqual(sim.Radius, sim.LocalPosition.Length(), 0.01f);
            }
        }

        [Test]
        public void DivingIntoGround_Crashes()
        {
            var sim = NewSim();
            Fly(sim, 0, -1, LineAction.Pull, 20f);
            Assert.IsTrue(sim.IsCrashed);
            Assert.Less(sim.Height, 0.5f + 0.5f);
        }

        [Test]
        public void Crashed_KiteDoesNotMove()
        {
            var sim = NewSim();
            Fly(sim, 0, -1, LineAction.Pull, 20f);
            Vector3 p = sim.LocalPosition;
            Fly(sim, 0, 1, LineAction.Pull, 2f);
            Assert.AreEqual(p, sim.LocalPosition);
        }

        [Test]
        public void NoseDirection_IsUnitAndTangent()
        {
            var sim = NewSim();
            Fly(sim, 0.7f, 0.7f, LineAction.Pull, 1f);
            Vector3 nose = sim.NoseDirection;
            Assert.AreEqual(1f, nose.Length(), 0.001f);
            Assert.AreEqual(0f, Vector3.Dot(nose, sim.Direction), 0.001f);
        }

        [Test]
        public void LongRandomFlight_ProducesNoNaN()
        {
            var sim = new KiteFlightSim(new FlightSettings(), TestData.AllRounder, AnchorHeight, seed: 7);
            sim.Reset(0, 60);
            var rng = new System.Random(5);
            for (int i = 0; i < 20000; i++)
            {
                var input = new FlightInput
                {
                    SteerX = (float)(rng.NextDouble() * 2 - 1), SteerY = (float)(rng.NextDouble() * 2 - 1),
                    Action = (LineAction)rng.Next(4), Wind = TestData.SteadyWind((float)rng.NextDouble() * 15f, (float)rng.NextDouble() * 360f),
                    Tension01 = (float)rng.NextDouble(), LineOut = 10f + (float)rng.NextDouble() * 500f, Time = i * Dt
                };
                sim.Step(input, Dt);
                Assert.IsFalse(float.IsNaN(sim.AzimuthDeg) || float.IsNaN(sim.ElevationDeg) || float.IsNaN(sim.Radius) || float.IsNaN(sim.Speed));
                if (sim.IsCrashed) sim.Reset(input.Wind.DirectionDeg, input.LineOut);
            }
        }
    }

    public class KiteSimulationTests
    {
        private const float Dt = 0.02f;

        private static KiteSimulation NewKite(float stock = 600f)
        {
            var sim = new KiteSimulation(TestData.AllRounder, new TensionSettings(), new FlightSettings { wobbleDegPerSec = 0 }, 10f, stock);
            sim.Reset(0f);
            return sim;
        }

        [Test]
        public void HoldingPullTooLong_SelfSnaps()
        {
            var kite = NewKite();
            var input = new KiteInput { PullHeld = true, SteerY = 0.3f };
            bool snapped = false;
            for (int i = 0; i < 500 && !snapped; i++)
                snapped = kite.Step(input, TestData.SteadyWind(), i * Dt, Dt).SelfSnapped;

            Assert.IsTrue(snapped);
            Assert.AreEqual(KiteStatus.SelfSnapped, kite.Status);
            // 40 → 95 at 35/s ≈ 1.57 s, + 0.6 s hold. Must not happen much earlier than ~2 s.
        }

        [Test]
        public void PullThenRelease_FeedsLineOut()
        {
            var kite = NewKite();
            float start = kite.Spool.LineOut;
            var pull = new KiteInput { PullHeld = true };
            var none = new KiteInput();
            for (int i = 0; i < 25; i++) kite.Step(pull, TestData.SteadyWind(), i * Dt, Dt);
            for (int i = 0; i < 50; i++) kite.Step(none, TestData.SteadyWind(), i * Dt, Dt);
            Assert.AreEqual(LineAction.Dheel, kite.CurrentAction);
            Assert.AreEqual(start + 10f, kite.Spool.LineOut, 0.01f); // 1 s × 10 m/s (Lakri)
        }

        [Test]
        public void Reel_ShortensLine()
        {
            var kite = NewKite();
            float start = kite.Spool.LineOut;
            var reel = new KiteInput { ReelHeld = true };
            for (int i = 0; i < 50; i++) kite.Step(reel, TestData.SteadyWind(), i * Dt, Dt);
            Assert.AreEqual(start - 8f, kite.Spool.LineOut, 0.01f);
        }

        [Test]
        public void Reset_AfterSnap_FliesAgain()
        {
            var kite = NewKite();
            var input = new KiteInput { PullHeld = true };
            for (int i = 0; i < 500; i++) kite.Step(input, TestData.SteadyWind(), i * Dt, Dt);
            Assert.AreNotEqual(KiteStatus.Flying, kite.Status);
            kite.Reset(0f);
            Assert.AreEqual(KiteStatus.Flying, kite.Status);
            Assert.AreEqual(40f, kite.Tension.Tension, 0.001f);
        }
    }
}
