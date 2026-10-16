using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class WindModelTests
    {
        [Test]
        public void SameSeed_SameWind()
        {
            var a = new WindModel(new WindSettings { seed = 42 });
            var b = new WindModel(new WindSettings { seed = 42 });
            for (float t = 0; t < 120; t += 3.3f)
            {
                Assert.AreEqual(a.Sample(t, 20).Speed, b.Sample(t, 20).Speed, 0.00001f);
                Assert.AreEqual(a.Sample(t, 20).DirectionDeg, b.Sample(t, 20).DirectionDeg, 0.00001f);
            }
        }

        [Test]
        public void DifferentSeed_DifferentWind()
        {
            var a = new WindModel(new WindSettings { seed = 1 });
            var b = new WindModel(new WindSettings { seed = 2 });
            bool anyDifferent = false;
            for (float t = 0; t < 60; t += 1f)
                if (System.Math.Abs(a.Sample(t, 0).DirectionDeg - b.Sample(t, 0).DirectionDeg) > 0.01f) anyDifferent = true;
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void Wind_StaysWithinConfiguredLimits()
        {
            var s = new WindSettings { baseDirectionDeg = 90, directionVariationDeg = 15, baseSpeed = 6, speedVariation = 0.25f, minSpeed = 1, maxSpeed = 18, heightGainPer100m = 0 };
            var model = new WindModel(s);
            for (float t = 0; t < 600; t += 0.5f)
            {
                var w = model.Sample(t, 0);
                Assert.That(w.DirectionDeg, Is.InRange(75f - 0.01f, 105f + 0.01f));
                Assert.That(w.Speed, Is.InRange(4.5f - 0.01f, 7.5f + 0.01f));
            }
        }

        [Test]
        public void Wind_IsStrongerHigherUp()
        {
            var model = new WindModel(new WindSettings { speedVariation = 0, heightGainPer100m = 0.2f, baseSpeed = 6 });
            Assert.AreEqual(6f, model.Sample(10, 0).Speed, 0.001f);
            Assert.AreEqual(7.2f, model.Sample(10, 100).Speed, 0.001f);
        }

        [Test]
        public void StrongFlag_UsesThreshold()
        {
            var model = new WindModel(new WindSettings { speedVariation = 0, heightGainPer100m = 0, baseSpeed = 10, strongWindSpeed = 9 });
            Assert.IsTrue(model.Sample(0, 0).IsStrong);
            model.SpeedMultiplier = 0.5f;
            Assert.IsFalse(model.Sample(0, 0).IsStrong);
        }
    }
}
