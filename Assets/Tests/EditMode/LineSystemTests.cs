using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class LineSpoolTests
    {
        [Test]
        public void Dheel_AddsReleaseSpeed()
        {
            var spool = new LineSpool(600, 8, 10, 600, 10, 60);
            spool.Step(LineAction.Dheel, 1f);
            Assert.AreEqual(70f, spool.LineOut, 0.001f);
        }

        [Test]
        public void Reel_RemovesReelSpeed()
        {
            var spool = new LineSpool(600, 8, 10, 600, 10, 60);
            spool.Step(LineAction.Reel, 1f);
            Assert.AreEqual(52f, spool.LineOut, 0.001f);
        }

        [Test]
        public void PullAndIdle_DoNotChangeLine()
        {
            var spool = new LineSpool(600, 8, 10, 600, 10, 60);
            spool.Step(LineAction.Pull, 1f);
            spool.Step(LineAction.Idle, 1f);
            Assert.AreEqual(60f, spool.LineOut, 0.001f);
        }

        [Test]
        public void LineOut_LimitedByCapacity()
        {
            var spool = new LineSpool(450, 14, 14, 10000, 10, 440);
            spool.Step(LineAction.Dheel, 5f);
            Assert.AreEqual(450f, spool.LineOut, 0.001f);
            Assert.IsTrue(spool.IsAtMax);
        }

        [Test]
        public void LineOut_LimitedByDorStock()
        {
            var spool = new LineSpool(1000, 6, 9, 300, 10, 60);
            Assert.AreEqual(300f, spool.MaxLineOut, 0.001f);
            spool.Step(LineAction.Dheel, 100f);
            Assert.AreEqual(300f, spool.LineOut, 0.001f);
        }

        [Test]
        public void LineOut_NeverBelowMinimum()
        {
            var spool = new LineSpool(600, 8, 10, 600, 10, 20);
            spool.Step(LineAction.Reel, 100f);
            Assert.AreEqual(10f, spool.LineOut, 0.001f);
            Assert.IsTrue(spool.IsAtMin);
        }
    }

    public class LineActionResolverTests
    {
        [Test]
        public void Nothing_IsIdle()
        {
            var r = new LineActionResolver();
            Assert.AreEqual(LineAction.Idle, r.Resolve(false, false, false));
        }

        [Test]
        public void HoldPull_IsPull()
        {
            var r = new LineActionResolver();
            Assert.AreEqual(LineAction.Pull, r.Resolve(true, false, false));
        }

        [Test]
        public void ReleasingPull_StartsDheel_UntilReel()
        {
            var r = new LineActionResolver();
            r.Resolve(true, false, false);
            Assert.AreEqual(LineAction.Dheel, r.Resolve(false, false, false));
            Assert.AreEqual(LineAction.Dheel, r.Resolve(false, false, false));
            Assert.AreEqual(LineAction.Reel, r.Resolve(false, true, false));
            Assert.AreEqual(LineAction.Idle, r.Resolve(false, false, false), "REEL cancels dheel");
        }

        [Test]
        public void Dheel_StopsWhenCharkhiIsEmpty()
        {
            var r = new LineActionResolver();
            r.Resolve(true, false, false);
            Assert.AreEqual(LineAction.Dheel, r.Resolve(false, false, false));
            Assert.AreEqual(LineAction.Idle, r.Resolve(false, false, true));
        }

        [Test]
        public void PullAgain_CancelsDheel()
        {
            var r = new LineActionResolver();
            r.Resolve(true, false, false);
            r.Resolve(false, false, false);
            Assert.AreEqual(LineAction.Pull, r.Resolve(true, false, false));
        }

        [Test]
        public void PullBeatsReel_WhenBothHeld()
        {
            var r = new LineActionResolver();
            Assert.AreEqual(LineAction.Pull, r.Resolve(true, true, false));
        }

        [Test]
        public void WithoutAutoDheel_ReleasingPullIsIdle()
        {
            var r = new LineActionResolver(dheelOnPullRelease: false);
            r.Resolve(true, false, false);
            Assert.AreEqual(LineAction.Idle, r.Resolve(false, false, false));
        }
    }

    public class LoadoutStatsTests
    {
        [Test]
        public void AllRounder_UsesBaseValues()
        {
            var s = TestData.AllRounder;
            Assert.AreEqual(180f, s.TurnRate, 0.001f);
            Assert.AreEqual(15, s.SweetZone);
            Assert.AreEqual(4f, s.Stability, 0.001f);
            Assert.AreEqual(95, s.Strength);
            Assert.AreEqual(600, s.CapacityMeters);
        }

        [Test]
        public void NoTail_And_Reshmi_StackTurnRate()
        {
            var s = new LoadoutStats(TestData.Guddi, TestData.NoTail, TestData.Reshmi, TestData.Lakri);
            Assert.AreEqual(180f * 1.15f * 1.05f, s.TurnRate, 0.01f);
            Assert.AreEqual(10, s.SweetZone);          // 15 − 5
            Assert.AreEqual(1.10f, s.TensionRiseMult, 0.0001f);
        }

        [Test]
        public void Lachakdaar_WidensSweetZone()
        {
            var s = new LoadoutStats(TestData.Guddi, TestData.ShortRibbon, TestData.Lachakdaar, TestData.Lakri);
            Assert.AreEqual(19, s.SweetZone);
        }

        [Test]
        public void Stability_NeverBelowMinimum()
        {
            var s = new LoadoutStats(TestData.Pipa, TestData.NoTail, TestData.Sooti, TestData.Lakri); // 2 − 2 = 0
            Assert.AreEqual(0.5f, s.Stability, 0.0001f);
        }

        [Test]
        public void ZeroMultipliers_FallBackToOne()
        {
            var s = new LoadoutStats(TestData.Guddi, TestData.ShortRibbon, default, default);
            Assert.AreEqual(1f, s.TensionRiseMult);
            Assert.AreEqual(1f, s.PullTensionRiseMult);
            Assert.AreEqual(1f, s.ReleaseTensionDropMult);
        }
    }
}
