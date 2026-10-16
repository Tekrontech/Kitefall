using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class TensionModelTests
    {
        private const float Dt = 0.02f; // 50 Hz, same as Unity FixedUpdate default.

        private static void Run(TensionModel model, LineAction action, float seconds, bool strongWind = false)
        {
            int steps = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < steps; i++) model.Step(action, strongWind, Dt);
        }

        [Test]
        public void StartsAtIdleTarget()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            Assert.AreEqual(40f, model.Tension, 0.001f);
            Assert.AreEqual(TensionZone.Safe, model.Zone);
        }

        [Test]
        public void Pull_Raises35PerSecond()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(0f);
            Run(model, LineAction.Pull, 1f);
            Assert.AreEqual(35f, model.Tension, 0.01f);
        }

        [Test]
        public void Release_Lowers40PerSecond()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(80f);
            Run(model, LineAction.Dheel, 1f);
            Assert.AreEqual(40f, model.Tension, 0.01f);
        }

        [TestCase(90f)]
        [TestCase(5f)]
        public void Idle_DriftsToTarget_WithoutOvershoot(float start)
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(start);
            Run(model, LineAction.Idle, 10f);
            Assert.AreEqual(40f, model.Tension, 0.001f);
        }

        [Test]
        public void Reel_DriftsLikeIdle()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(70f);
            Run(model, LineAction.Reel, 10f);
            Assert.AreEqual(40f, model.Tension, 0.001f);
        }

        [Test]
        public void Tension_IsClampedBetween0And100()
        {
            var model = new TensionModel(new TensionSettings(), 200); // Strength above 100 → never snaps.
            Run(model, LineAction.Pull, 10f);
            Assert.AreEqual(100f, model.Tension, 0.001f);
            Run(model, LineAction.Dheel, 10f);
            Assert.AreEqual(0f, model.Tension, 0.001f);
        }

        [Test]
        public void StrongWind_RisesTenPercentFaster()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(0f);
            Run(model, LineAction.Pull, 1f, strongWind: true);
            Assert.AreEqual(38.5f, model.Tension, 0.01f);
        }

        [Test]
        public void DorAndCharkhiMultipliers_Stack()
        {
            // Tez Dhaar (rise ×1.15) on Gear charkhi (pull rise ×1.15): 35 × 1.15 × 1.15 = 46.29 / s
            var model = new TensionModel(new TensionSettings(), 92, riseMult: 1.15f, pullRiseMult: 1.15f);
            model.Reset(0f);
            Run(model, LineAction.Pull, 1f);
            Assert.AreEqual(46.2875f, model.Tension, 0.02f);
        }

        [Test]
        public void SpeedCharkhi_ReleaseDrops20PercentFaster()
        {
            var model = new TensionModel(new TensionSettings(), 95, releaseDropMult: 1.2f);
            model.Reset(100f);
            Run(model, LineAction.Dheel, 1f);
            Assert.AreEqual(52f, model.Tension, 0.01f);
        }

        [TestCase(0f, TensionZone.Safe)]
        [TestCase(59.9f, TensionZone.Safe)]
        [TestCase(60f, TensionZone.Strong)]
        [TestCase(84.9f, TensionZone.Strong)]
        [TestCase(85f, TensionZone.Danger)]
        [TestCase(100f, TensionZone.Danger)]
        public void Zones_MatchSpec(float tension, TensionZone expected)
        {
            var model = new TensionModel(new TensionSettings(), 95);
            Assert.AreEqual(expected, model.ZoneFor(tension));
        }

        [Test]
        public void ZoneChanged_IsReportedOnce()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(59f);
            var first = model.Step(LineAction.Pull, false, 0.1f);   // 59 → 62.5
            var second = model.Step(LineAction.Pull, false, 0.1f);  // 62.5 → 66
            Assert.IsTrue(first.ZoneChanged);
            Assert.IsFalse(second.ZoneChanged);
            Assert.AreEqual(TensionZone.Strong, model.Zone);
        }

        [Test]
        public void SelfSnap_OnlyAfterHoldTimeAboveStrength()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(100f);
            Run(model, LineAction.Pull, 0.56f);
            Assert.IsFalse(model.IsSnapped, "Must not snap before 0.6 s");

            bool snappedEvent = false;
            for (int i = 0; i < 10 && !snappedEvent; i++)
                snappedEvent = model.Step(LineAction.Pull, false, Dt).SnappedThisStep;

            Assert.IsTrue(snappedEvent);
            Assert.IsTrue(model.IsSnapped);
        }

        [Test]
        public void SnapTimer_ResetsWhenTensionDropsBelowStrength()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(100f);
            Run(model, LineAction.Pull, 0.3f);  // 0.3 s above Strength
            Run(model, LineAction.Dheel, 0.2f); // 100 → 92: ends below 95 before 0.6 s total
            Assert.AreEqual(0f, model.OverStrengthTime, 0.0001f);
            Run(model, LineAction.Pull, 0.3f);  // back above 95 for a short time only
            Assert.IsFalse(model.IsSnapped);
        }

        [Test]
        public void HigherStrength_SurvivesWhereLowerSnaps()
        {
            // Hold tension at 96.5 for 2 s: Sooti (Strength 95) snaps, Mazboot (Strength 98) survives.
            var holdAt96 = new TensionSettings { idleTarget = 96.5f, idleDriftRate = 1000f };
            var sooti = new TensionModel(holdAt96, 95);
            var mazboot = new TensionModel(holdAt96, 98);
            Run(sooti, LineAction.Idle, 2f);
            Run(mazboot, LineAction.Idle, 2f);
            Assert.IsTrue(sooti.IsSnapped);
            Assert.IsFalse(mazboot.IsSnapped);
        }

        [Test]
        public void AfterSnap_StepDoesNothing()
        {
            var model = new TensionModel(new TensionSettings(), 95);
            model.Reset(100f);
            Run(model, LineAction.Pull, 1f);
            Assert.IsTrue(model.IsSnapped);
            float t = model.Tension;
            Run(model, LineAction.Dheel, 1f);
            Assert.AreEqual(t, model.Tension, 0.0001f);
        }
    }
}
