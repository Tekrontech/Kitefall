using System;
using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    /// <summary>Audit B1 (Sooti never blocks play) and R7 (launch scope freeze – hidden, not deleted).</summary>
    public class LaunchScopeTests
    {
        private const long Now = 1_800_000_000L;
        private static readonly EconomyBundle Cfg = new EconomyBundle();

        [TearDown] public void Reset() => LaunchContent.ShowComingSoon = false;

        /// <summary>A heavy-use match result: lots of Sooti used and cut away, a kite lost in loss modes.</summary>
        private static MatchResult HeavyResult(GameMode mode, bool won, int seed)
        {
            var me = new ParticipantResult { Index = 0, Won = won, DisplayName = "me", Placement = won ? 1 : 2 };
            me.DorsUsed.Add("sooti");
            me.CharkhisUsed.Add("lakri");
            me.DorUsed["sooti"] = 260f;
            me.DorLost["sooti"] = 140f;
            me.DurabilityEnd["guddi"] = 40f;
            if (ModeRules.For(mode).lossMode) me.KitesLost.Add("guddi");
            var other = new ParticipantResult { Index = 1, Won = !won, DisplayName = "them", Placement = won ? 2 : 1 };
            var m = new MatchResult { Mode = mode, Seed = seed, WinnerIndex = won ? 0 : 1 };
            m.Participants.Add(me);
            m.Participants.Add(other);
            return m;
        }

        [Test]
        public void NewProfile_Plays30MatchesInARow_NeverBlocked_NeverNegative()
        {
            var p = new PlayerProfile();
            EconomyService.CreateProfile(p, "player_b1", Now);
            Assert.IsTrue(EconomyCommands.Execute(p, EconomyCommands.Welcome, null, Now, Cfg).Ok);
            GameMode[] modes = { GameMode.QuickDuel, GameMode.RankedDuel, GameMode.LastKiteFlying, GameMode.FreeForAll, GameMode.TeamBattle, GameMode.PrivateRoom };
            long now = Now;
            bool sawLow = false;
            for (int i = 0; i < 30; i++)
            {
                if (i % 10 == 0)
                {
                    // First launch of a new day: the app claims the free refill through the normal command path.
                    if (EconomyService.SootiRefillDue(p, now)) Assert.IsTrue(EconomyCommands.Execute(p, EconomyCommands.SootiRefill, null, now, Cfg).Ok);
                    Assert.IsFalse(EconomyService.SootiRefillDue(p, now));
                }
                GameMode mode = modes[i % modes.Length];
                Assert.IsNull(EconomyService.ValidateLoadout(p, p.loadout, Cfg.economy), $"match {i + 1} ({mode}) was blocked");
                sawLow |= EconomyService.SootiLow(p, p.loadout, mode, Cfg.economy);
                ParticipantLoadout l = EconomyService.BuildMatchLoadout(p, p.loadout, p.loadout.kite, mode);
                Assert.GreaterOrEqual(l.dorStockMeters, EconomyService.SootiMatchFloor, "the kite always gets a usable spool");

                OpResult r = EconomyService.ApplyMatchResult(p, HeavyResult(mode, i % 3 == 0, 100 + i),
                    new MatchRewardContext { MatchId = "b1_" + i, Mode = mode, MyIndex = 0, NowUtc = now }, Cfg.economy, Cfg.ranks);
                Assert.IsTrue(r.Ok, r.Error);
                Assert.GreaterOrEqual(p.DorMeters("sooti"), 0f);
                foreach (KiteOwned k in p.kites) Assert.GreaterOrEqual(k.count, 0);
                now += 3 * 3600; // 30 matches over ~4 days
            }
            Assert.IsTrue(sawLow, "test covers a low stock in Ranked / LKF (warning only, never a block)");
            Assert.AreEqual(30, p.matches);
        }

        [Test]
        public void Sooti_UnlimitedOutsideRankedAndLkf()
        {
            foreach (GameMode m in (GameMode[])Enum.GetValues(typeof(GameMode)))
                Assert.AreEqual(m != GameMode.RankedDuel && m != GameMode.LastKiteFlying, EconomyService.SootiUnlimited(m), m.ToString());
            var p = new PlayerProfile();
            EconomyService.CreateProfile(p, "x", Now);
            EconomyService.GrantWelcome(p, Cfg.economy);
            float before = p.DorMeters("sooti");
            EconomyService.ApplyMatchResult(p, HeavyResult(GameMode.TeamBattle, false, 1), new MatchRewardContext { MatchId = "t", MyIndex = 0, NowUtc = Now }, Cfg.economy, Cfg.ranks);
            Assert.AreEqual(before, p.DorMeters("sooti"), 0.01f);
            EconomyService.ApplyMatchResult(p, HeavyResult(GameMode.RankedDuel, false, 2), new MatchRewardContext { MatchId = "r", MyIndex = 0, NowUtc = Now }, Cfg.economy, Cfg.ranks);
            Assert.AreEqual(before - 400f, p.DorMeters("sooti"), 0.01f);
        }

        [Test]
        public void LaunchSet_IsTwoArenasThreeKitesFourCharacters_AllStillInData()
        {
            Assert.AreEqual(2, LaunchContent.Arenas.Length);
            Assert.AreEqual(3, LaunchContent.Kites.Length);
            Assert.AreEqual(4, LaunchContent.Characters.Length);
            foreach (string k in LaunchContent.Kites) Assert.IsNotNull(SpecDefaults.KiteInfoOf(k), k);
            foreach (string c in LaunchContent.Characters) Assert.IsNotNull(Catalog.Cosmetic(c), c);
            // Hidden, not deleted.
            Assert.AreEqual(19, CharacterCatalog.Characters.Count);
            Assert.AreEqual(10, SpecDefaults.Kites.Count);
            foreach (string k in new[] { "delta", "box", "volantin" })
            {
                Assert.IsFalse(LaunchContent.IsLaunchKite(k));
                Assert.IsFalse(LaunchContent.IsLaunchCosmetic(Catalog.Cosmetic($"skin_{k}_0")), "skins of hidden kites are hidden");
            }
            foreach (string c in new[] { "mateus", "valentina", "diego", "emma", "jack", "amara", "youssef", "elif", "nikos" })
                Assert.IsFalse(LaunchContent.IsLaunchCharacter(c), c);
            Assert.AreEqual(2, LaunchContent.PickerArenas().Count);
            Assert.AreEqual(3, LaunchContent.PickerKites().Count);
            LaunchContent.ShowComingSoon = true;
            Assert.AreEqual(Enum.GetValues(typeof(ArenaType)).Length, LaunchContent.PickerArenas().Count, "Coming soon shows everything (locked)");
            Assert.AreEqual(10, LaunchContent.PickerKites().Count);
        }

        [Test]
        public void Arenas_FallBackToLaunch_LkfKeepsStorm_TrainingKeepsItsArena()
        {
            Assert.AreEqual(ArenaType.StormArchipelago, LaunchContent.ArenaForMatch(GameMode.LastKiteFlying, ArenaType.OldCityRooftops));
            Assert.AreEqual(ArenaType.RiverFestival, LaunchContent.ArenaForMatch(GameMode.QuickDuel, ArenaType.RiverFestival));
            Assert.AreEqual(ArenaType.OldCityRooftops, LaunchContent.ArenaForMatch(GameMode.FreeForAll, ArenaType.BaliTerraces));
            LaunchContent.ShowComingSoon = true;
            Assert.AreEqual(ArenaType.OldCityRooftops, LaunchContent.ArenaForMatch(GameMode.TeamBattle, ArenaType.RioHillside), "Coming soon arenas are locked");
            Assert.AreEqual(ArenaType.OldCityRooftops, LaunchContent.ArenaForMatch(GameMode.Training, ArenaType.OldCityRooftops));
        }

        [Test]
        public void Bots_UseLaunchKitesAndCharactersOnly()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                MatchSetup setup = MatchFactory.NewSetup(GameMode.FreeForAll, ArenaType.OldCityRooftops, seed);
                MatchFactory.FillWithBots(setup, 8, seed % 2 == 0 ? BotDifficulty.Medium : BotDifficulty.Hard, seed % 3 == 0 ? "reshmi" : "tez_dhaar");
                foreach (ParticipantSetup b in setup.Participants)
                {
                    Assert.IsTrue(LaunchContent.IsLaunchCharacter(b.characterId), b.characterId);
                    foreach (ParticipantLoadout l in b.stack) Assert.IsTrue(LaunchContent.IsLaunchKite(l.kiteId), l.kiteId);
                }
            }
        }

        [Test]
        public void ExistingProfile_KeepsHiddenItems_ButCannotBuyNewOnes()
        {
            var p = new PlayerProfile();
            EconomyService.CreateProfile(p, "old", Now);
            EconomyService.GrantWelcome(p, Cfg.economy);
            p.gems = 5000;
            p.coins = 5000;
            p.level = 30;
            foreach (string part in PartIds.All) p.AddPart(part, 50);
            Assert.IsFalse(EconomyService.BuyCosmetic(p, "mateus", false).Ok, "hidden character cannot be bought");
            Assert.IsFalse(EconomyService.CraftKite(p, "delta", Cfg.economy).Ok, "hidden kite cannot be crafted new");
            Assert.IsTrue(EconomyService.BuyCosmetic(p, "skin_guddi_3", false).Ok, "launch skins still sell");
            // A save from before the freeze: owns Mateus and a Delta kite.
            PlayerProfile.AddCount(p.cosmetics, "mateus", 1);
            p.KiteOrAdd("delta").count = 1;
            Assert.IsTrue(EconomyService.EquipCharacter(p, "mateus").Ok);
            Assert.IsTrue(EconomyService.CraftKite(p, "delta", Cfg.economy).Ok, "a class you already have keeps working");
            Assert.IsTrue(EconomyCommands.Execute(p, EconomyCommands.SetLoadout, new[] { "delta", "short_ribbon", "sooti", "lakri" }, Now, Cfg).Ok);
            Assert.IsNull(EconomyService.ValidateLoadout(p, p.loadout, Cfg.economy));
        }
    }
}
