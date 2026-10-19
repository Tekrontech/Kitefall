using System;
using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    /// <summary>International update (D57): words setting, new kites / arenas / characters, World Kite Calendar.</summary>
    public class InternationalTests
    {
        [TearDown] public void Reset() => Terms.Words = KiteWords.International;

        [Test]
        public void KiteWords_InternationalByDefault_SouthAsianOnRequest()
        {
            Terms.Words = KiteWords.International;
            Assert.AreEqual("Release", Terms.Release);
            Assert.AreEqual("Spool", Terms.Spool);
            Assert.AreEqual("Cotton line", SpecDefaults.NameOf("sooti"));
            Assert.AreEqual("Guddi", SpecDefaults.NameOf("guddi"), "kite class names are proper names");
            Terms.Words = KiteWords.SouthAsian;
            Assert.AreEqual("Dheel", Terms.Release);
            Assert.AreEqual("Charkhi", Terms.Spool);
            Assert.AreEqual("Sooti", SpecDefaults.NameOf("sooti"));
        }

        [Test]
        public void NewKites_HaveStatsInsideExistingRanges_AndEightSkins()
        {
            foreach (string id in new[] { "delta", "box", "volantin" })
            {
                SpecDefaults.KiteInfo k = SpecDefaults.Kites.Find(x => x.Stats.id == id);
                Assert.IsNotNull(k, id);
                Assert.That(k.Stats.speed, Is.InRange(1f, 5f));
                Assert.That(k.Stats.classBase, Is.InRange(0.92f, 1.10f));
                Assert.AreEqual(8, Catalog.Cosmetics.FindAll(c => c.type == CosmeticType.KiteSkin && c.forItem == id).Count, id);
            }
        }

        [Test]
        public void EveryArena_HasNameRegionAndBuildsRoofs()
        {
            foreach (ArenaType t in (ArenaType[])Enum.GetValues(typeof(ArenaType)))
            {
                Assert.IsFalse(string.IsNullOrEmpty(ArenaLayout.DisplayName(t)));
                Assert.IsFalse(string.IsNullOrEmpty(ArenaLayout.Region(t)));
                ArenaLayout a = ArenaLayout.Generate(t, 42, 8, true, false, null);
                Assert.AreEqual(8, a.Anchors.Count, t.ToString());
                Assert.IsNotNull(a.Roofs, t.ToString());
            }
            Assert.AreEqual(6, (int)ArenaType.RioHillside, "new arenas are appended – old values never move");
        }

        [Test]
        public void Calendar_CoversFestivalsAcrossTheYear_AndWrapsNewYear()
        {
            Assert.AreEqual("pipas", KiteCalendar.Current(new DateTime(2027, 1, 3)).Id);
            Assert.AreEqual("fiestas_patrias", KiteCalendar.Current(new DateTime(2026, 9, 18)).Id);
            Assert.IsNull(KiteCalendar.Current(new DateTime(2026, 10, 16)));
            Assert.IsNotNull(KiteCalendar.Next(new DateTime(2026, 10, 16)));
            var regions = new System.Collections.Generic.HashSet<string>();
            foreach (KiteFestival f in KiteCalendar.Festivals) regions.Add(f.Region);
            Assert.GreaterOrEqual(regions.Count, 8);
        }

        [Test]
        public void Characters_ComeFromManyRegions()
        {
            var regions = new System.Collections.Generic.HashSet<string>();
            foreach (CosmeticItem c in CharacterCatalog.Characters) regions.Add(c.region);
            Assert.GreaterOrEqual(regions.Count, 15);
            foreach (CosmeticItem c in CharacterCatalog.Characters)
                Assert.IsNotNull(CharacterCatalog.Outfit(c.forItem), c.id + " default outfit");
        }
    }
}
