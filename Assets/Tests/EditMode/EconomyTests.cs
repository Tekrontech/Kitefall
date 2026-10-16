using Kitefall.Core;
using NUnit.Framework;

namespace Kitefall.Tests
{
    public class EconomyTests
    {
        private const long Now = 1_800_000_000L;
        private static readonly EconomyConfig C = new EconomyConfig();
        private static readonly RankSettings R = new RankSettings();

        private static PlayerProfile NewPlayer()
        {
            var p = new PlayerProfile();
            EconomyService.CreateProfile(p, "player_1", Now - 10 * 86400);
            EconomyService.GrantWelcome(p, C);
            return p;
        }

        private static MatchResult Result(GameMode mode, bool won, int cuts = 0, string lostKite = null, float dorLost = 0f)
        {
            var me = new ParticipantResult { Index = 0, Won = won, Cuts = cuts, DisplayName = "me", Placement = won ? 1 : 2 };
            me.DorsUsed.Add("sooti");
            me.CharkhisUsed.Add("lakri");
            me.DorUsed["sooti"] = 80f;
            me.DurabilityEnd["guddi"] = 72f;
            if (lostKite != null) { me.KitesLost.Add(lostKite); me.DorLost["sooti"] = dorLost; }
            var other = new ParticipantResult { Index = 1, Won = !won, DisplayName = "them", Placement = won ? 2 : 1 };
            var m = new MatchResult { Mode = mode, Seed = 42, WinnerIndex = won ? 0 : 1 };
            m.Participants.Add(me);
            m.Participants.Add(other);
            return m;
        }

        private static MatchRewardContext Ctx(string id, int oppRank = 0) => new MatchRewardContext { MatchId = id, MyIndex = 0, NowUtc = Now, OpponentRankPoints = oppRank };

        [Test]
        public void Welcome_MatchesSpec_AndOnlyOnce()
        {
            PlayerProfile p = NewPlayer();
            Assert.AreEqual(3, p.Kite("guddi").count);
            Assert.AreEqual(1, p.Kite("rokkaku").count);
            Assert.AreEqual(500, p.coins);
            Assert.AreEqual(600f, p.DorMeters("sooti"));
            Assert.IsTrue(p.HasCharkhi("lakri"));
            Assert.IsFalse(EconomyService.GrantWelcome(p, C).Ok);
        }

        [Test]
        public void WinRewards_FollowSpec122()
        {
            PlayerProfile p = NewPlayer();
            int coins = p.coins;
            OpResult r = EconomyService.ApplyMatchResult(p, Result(GameMode.FreeForAll, true, cuts: 2), Ctx("m1"), C, R);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(coins + 180, p.coins);
            int parts = 0;
            foreach (ItemCount ic in r.Reward.parts) parts += ic.count;
            Assert.That(parts, Is.InRange(5 + 2, 7 + 2));
            Assert.AreEqual(300, p.passXp);
            Assert.AreEqual(1, p.wins);
        }

        [Test]
        public void LossRewards_FollowSpec122()
        {
            PlayerProfile p = NewPlayer();
            int coins = p.coins;
            OpResult r = EconomyService.ApplyMatchResult(p, Result(GameMode.QuickDuel, false), Ctx("m1"), C, R);
            Assert.AreEqual(coins + 70, p.coins);
            Assert.AreEqual(140, p.passXp);
            int parts = 0;
            foreach (ItemCount ic in r.Reward.parts) parts += ic.count;
            Assert.That(parts, Is.InRange(2, 3));
        }

        [Test]
        public void SameMatch_CannotBeRewardedTwice()
        {
            PlayerProfile p = NewPlayer();
            Assert.IsTrue(EconomyService.ApplyMatchResult(p, Result(GameMode.QuickDuel, true), Ctx("dup"), C, R).Ok);
            Assert.IsFalse(EconomyService.ApplyMatchResult(p, Result(GameMode.QuickDuel, true), Ctx("dup"), C, R).Ok);
        }

        [Test]
        public void LossMode_RemovesKiteAndDor_QuickDuelOnlyDurability()
        {
            PlayerProfile p = NewPlayer();
            EconomyService.ApplyMatchResult(p, Result(GameMode.FreeForAll, false, lostKite: "guddi", dorLost: 50f), Ctx("ffa"), C, R);
            Assert.AreEqual(2, p.Kite("guddi").count);
            Assert.AreEqual(600f - 80f - 50f, p.DorMeters("sooti"), 0.01f);

            PlayerProfile q = NewPlayer();
            EconomyService.ApplyMatchResult(q, Result(GameMode.QuickDuel, false), Ctx("qd"), C, R);
            Assert.AreEqual(3, q.Kite("guddi").count);
            Assert.AreEqual(600f, q.DorMeters("sooti"), 0.01f);
            Assert.AreEqual(72f, q.Kite("guddi").durability, 0.01f);
        }

        [Test]
        public void DoubleRewards_Once_NoRankPoints()
        {
            PlayerProfile p = NewPlayer();
            EconomyService.ApplyMatchResult(p, Result(GameMode.RankedDuel, true), Ctx("r1"), C, R);
            int coins = p.coins, rank = p.rankPoints;
            Assert.IsTrue(EconomyService.RewardedAd(p, RewardedPlacement.DoubleRewards, null, C, new AdsConfig(), Now).Ok);
            Assert.AreEqual(coins + 180, p.coins);
            Assert.AreEqual(rank, p.rankPoints);
            Assert.IsFalse(EconomyService.DoubleLastReward(p).Ok);
        }

        [Test]
        public void Repair_CostScalesWithMissingDurability()
        {
            PlayerProfile p = NewPlayer();
            p.Kite("guddi").durability = 70f;
            Assert.AreEqual(90, EconomyService.RepairCost(p, "guddi", C));
            int coins = p.coins;
            Assert.IsTrue(EconomyService.Repair(p, "guddi", C, false).Ok);
            Assert.AreEqual(coins - 90, p.coins);
            Assert.AreEqual(100f, p.Kite("guddi").durability);
        }

        [Test]
        public void CraftKite_NeedsSailSticksCoins()
        {
            PlayerProfile p = NewPlayer();
            int sail = p.Part(PartIds.Sail);
            Assert.IsTrue(EconomyService.CraftKite(p, "pipa", C).Ok);
            Assert.AreEqual(1, p.Kite("pipa").count);
            Assert.AreEqual(sail - 3, p.Part(PartIds.Sail));
            p.coins = 0;
            Assert.IsFalse(EconomyService.CraftKite(p, "pipa", C).Ok);
        }

        [Test]
        public void LockedItems_NeedLevel()
        {
            PlayerProfile p = NewPlayer();
            Assert.IsFalse(EconomyService.MakeDorSpool(p, "reshmi", C).Ok);
            Assert.IsFalse(EconomyService.CraftCharkhi(p, "speed", C).Ok);
            p.level = 5;
            Assert.IsTrue(EconomyService.MakeDorSpool(p, "reshmi", C).Ok);
            Assert.AreEqual(200f, p.DorMeters("reshmi"));
            Assert.IsTrue(EconomyService.CraftCharkhi(p, "speed", C).Ok);
        }

        [Test]
        public void Tuning_SlotsUnlockAtMastery3And7_AndAreSidegrades()
        {
            PlayerProfile p = NewPlayer();
            Assert.IsFalse(EconomyService.ApplyTuning(p, "sooti", "waxed_coat", 0, C).Ok);
            p.Mastery("sooti").level = 3;
            Assert.IsTrue(EconomyService.ApplyTuning(p, "sooti", "waxed_coat", 0, C).Ok);
            Assert.IsFalse(EconomyService.ApplyTuning(p, "sooti", "double_twist", 1, C).Ok);
            Assert.IsFalse(EconomyService.ApplyTuning(p, "sooti", "oiled_axle", 0, C).Ok, "Charkhi tuning does not fit a dor");
            p.Mastery("sooti").level = 7;
            Assert.IsTrue(EconomyService.ApplyTuning(p, "sooti", "double_twist", 1, C).Ok);

            ParticipantLoadout l = EconomyService.BuildMatchLoadout(p, p.loadout, "guddi");
            LoadoutStats s = l.Compose();
            Assert.AreEqual(95 - 1 + 2, s.Strength);
            Assert.AreEqual(1.00f, s.DorSpeed, 0.002f); // tuning percents add: +5 % − 5 % = 0
        }

        [Test]
        public void RewardedCaps_PerDay()
        {
            PlayerProfile p = NewPlayer();
            var ads = new AdsConfig();
            for (int i = 0; i < 2; i++) Assert.IsTrue(EconomyService.RewardedAd(p, RewardedPlacement.FreeGems, null, C, ads, Now).Ok);
            Assert.IsFalse(EconomyService.RewardedAd(p, RewardedPlacement.FreeGems, null, C, ads, Now).Ok);
            Assert.IsTrue(EconomyService.RewardedAd(p, RewardedPlacement.FreeGems, null, C, ads, Now + 86400).Ok, "Resets next day");
        }

        [Test]
        public void Shop_IsCosmeticOnly_AndChecksGems()
        {
            PlayerProfile p = NewPlayer();
            CosmeticItem skin = Catalog.Cosmetics.Find(c => c.type == CosmeticType.KiteSkin && !c.passOnly && c.rarity == Rarity.Rare);
            Assert.IsFalse(EconomyService.BuyCosmetic(p, skin.id, false).Ok);
            p.gems = 1000;
            Assert.IsTrue(EconomyService.BuyCosmetic(p, skin.id, false).Ok);
            Assert.AreEqual(1000 - skin.gemPrice, p.gems);
            Assert.IsTrue(EconomyService.Equip(p, skin.id).Ok);
            Assert.AreEqual(skin.id, p.KiteOrAdd(skin.forItem).skin);
        }

        [Test]
        public void SeasonPass_LevelsAndClaims()
        {
            PlayerProfile p = NewPlayer();
            Assert.AreEqual(1, EconomyService.PassLevel(p, C));
            Assert.IsTrue(EconomyService.ClaimPassReward(p, 1, false, C).Ok);
            Assert.IsFalse(EconomyService.ClaimPassReward(p, 1, false, C).Ok);
            Assert.IsFalse(EconomyService.ClaimPassReward(p, 1, true, C).Ok, "Premium needed");
            Assert.IsFalse(EconomyService.ClaimPassReward(p, 5, false, C).Ok, "Level not reached");
            p.passXp = 4000;
            Assert.IsTrue(EconomyService.ClaimPassReward(p, 5, false, C).Ok);
            Assert.AreEqual(100, Catalog.PassRewards.Count, "50 levels × free + premium");
        }

        [Test]
        public void DailyLogin_SevenDayCycle()
        {
            PlayerProfile p = NewPlayer();
            for (int d = 0; d < 7; d++) Assert.IsTrue(EconomyService.ClaimDailyLogin(p, Now + d * 86400).Ok);
            Assert.AreEqual(6, p.loginStreakIndex);
            Assert.IsFalse(EconomyService.ClaimDailyLogin(p, Now + 6 * 86400).Ok);
            EconomyService.ClaimDailyLogin(p, Now + 7 * 86400);
            Assert.AreEqual(0, p.loginStreakIndex, "Cycle restarts");
            EconomyService.ClaimDailyLogin(p, Now + 10 * 86400);
            Assert.AreEqual(0, p.loginStreakIndex, "Missed a day → restart");
        }

        [Test]
        public void SootiRefill_OncePerDay()
        {
            PlayerProfile p = NewPlayer();
            Assert.IsTrue(EconomyService.ClaimSootiRefill(p, C, Now).Ok);
            Assert.AreEqual(900f, p.DorMeters("sooti"));
            Assert.IsFalse(EconomyService.ClaimSootiRefill(p, C, Now + 60).Ok);
        }

        [Test]
        public void Missions_ProgressFromMatches()
        {
            PlayerProfile p = NewPlayer();
            EconomyService.EnsureMissions(p, Now);
            Assert.AreEqual(3, p.missions.Count);
            for (int i = 0; i < 3; i++) EconomyService.ApplyMatchResult(p, Result(GameMode.FreeForAll, true, cuts: 2), Ctx("x" + i), C, R);
            foreach (MissionProgress m in p.missions)
            {
                MissionTemplate t = Catalog.Mission(m.id);
                if (t.stat == "matches" || t.stat == "wins" || t.stat == "cuts") Assert.AreEqual(t.target, m.progress);
            }
        }

        [Test]
        public void Validation_DorBelow100m_Blocked_FreeGuddiAlwaysAllowed()
        {
            PlayerProfile p = NewPlayer();
            p.Kite("guddi").count = 0;
            Assert.IsNull(EconomyService.ValidateLoadout(p, p.loadout, C), "Free Guddi is always available");
            p.AddDor("sooti", -550f);
            Assert.IsNotNull(EconomyService.ValidateLoadout(p, p.loadout, C));
        }

        [TestCase("Ali", true)]
        [TestCase("ab", false)]
        [TestCase("ThisNameIsWayTooLong", false)]
        [TestCase("Sh1t_kite", false)]
        [TestCase("Kite King", true)]
        public void Names(string name, bool ok)
        {
            Assert.AreEqual(ok, NameRules.Validate(name) == null);
        }

        [Test]
        public void NameChange_Once30Days()
        {
            PlayerProfile p = NewPlayer();
            Assert.IsTrue(EconomyService.SetProfileInfo(p, "First", "Asia", "PK", C, Now, true).Ok);
            Assert.IsTrue(EconomyService.SetProfileInfo(p, "Second", "Asia", "PK", C, Now, false).Ok);
            Assert.IsFalse(EconomyService.SetProfileInfo(p, "Third", "Asia", "PK", C, Now + 86400, false).Ok);
            Assert.IsTrue(EconomyService.SetProfileInfo(p, "Third", "Asia", "PK", C, Now + 31 * 86400, false).Ok);
        }
    }

    public class RankTests
    {
        private static readonly RankSettings R = new RankSettings();

        [Test]
        public void WinAndLoss_InSpecRanges()
        {
            for (int diff = -2000; diff <= 2000; diff += 100)
            {
                Assert.That(RankRules.Delta(true, 1000, 1000 + diff, 0, R), Is.InRange(25, 35));
                Assert.That(RankRules.Delta(false, 1000, 1000 + diff, 0, R), Is.InRange(-25, -15));
            }
        }

        [Test]
        public void StreakBonus_FromThirdWin()
        {
            Assert.AreEqual(30, RankRules.Delta(true, 500, 500, 1, R));
            Assert.AreEqual(35, RankRules.Delta(true, 500, 500, 2, R));
        }

        [Test]
        public void Quit_Minus35_AndBans()
        {
            var p = new PlayerProfile { rankPoints = 100 };
            RankRules.ApplyQuit(p, 1000, R);
            Assert.AreEqual(65, p.rankPoints);
            Assert.AreEqual(1000 + 600, p.queueBanUntilUtc);
            RankRules.ApplyQuit(p, 2000, R);
            RankRules.ApplyQuit(p, 3000, R);
            Assert.AreEqual(3000 + 3600, p.queueBanUntilUtc);
        }

        [Test]
        public void Tiers()
        {
            Assert.AreEqual(RankTier.Bronze, RankRules.TierOf(0, R));
            Assert.AreEqual(RankTier.Silver, RankRules.TierOf(300, R));
            Assert.AreEqual(RankTier.Master, RankRules.TierOf(2600, R));
            Assert.AreEqual(RankTier.SkyLegend, RankRules.TierOf(2600, R, 12));
        }

        [Test]
        public void RankedUnlock_SilverOr10Matches()
        {
            Assert.IsFalse(RankRules.IsRankedUnlocked(new PlayerProfile { matches = 9 }, R));
            Assert.IsTrue(RankRules.IsRankedUnlocked(new PlayerProfile { matches = 10 }, R));
            Assert.IsTrue(RankRules.IsRankedUnlocked(new PlayerProfile { rankPoints = 300 }, R));
        }

        [Test]
        public void Matchmaking_WidensAfter10s_BotsAfter20s_NeverRanked()
        {
            var mm = new MatchmakingSettings();
            Assert.IsFalse(MatchmakingRules.CanMatch(0, 750, 5f, R, mm));   // Bronze vs Gold
            Assert.IsTrue(MatchmakingRules.CanMatch(0, 750, 11f, R, mm));
            Assert.IsFalse(MatchmakingRules.ShouldFillWithBots(ModeRules.For(GameMode.QuickDuel), 19f, mm));
            Assert.IsTrue(MatchmakingRules.ShouldFillWithBots(ModeRules.For(GameMode.QuickDuel), 20f, mm));
            Assert.IsFalse(MatchmakingRules.ShouldFillWithBots(ModeRules.For(GameMode.RankedDuel), 60f, mm));
        }
    }

    public class AdsPolicyTests
    {
        private const long Now = 1_800_000_000L;
        private static readonly AdsConfig A = new AdsConfig();

        private static PlayerProfile Veteran() => new PlayerProfile { createdUtc = Now - 30 * 86400, matches = 50, matchesSinceInterstitial = 5, lastInterstitialUtc = Now - 3600 };

        [Test]
        public void NeverDuringGameplay()
        {
            Assert.IsFalse(AdsPolicy.AdsAllowedIn(AdContext.Match));
            Assert.IsFalse(AdsPolicy.AdsAllowedIn(AdContext.Matchmaking));
            Assert.IsFalse(AdsPolicy.AdsAllowedIn(AdContext.AppOpen));
            Assert.IsTrue(AdsPolicy.AdsAllowedIn(AdContext.Results));
        }

        [Test]
        public void Interstitial_OnlyWhenLeavingResults()
        {
            Assert.IsTrue(AdsPolicy.CanShowInterstitial(Veteran(), A, Now, true));
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(Veteran(), A, Now, false));
        }

        [Test]
        public void Interstitial_NewPlayerGrace()
        {
            PlayerProfile p = Veteran();
            p.createdUtc = Now - 2 * 86400;
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
            p = Veteran();
            p.matches = 9;
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
        }

        [Test]
        public void Interstitial_Needs2MatchesAnd8Minutes()
        {
            PlayerProfile p = Veteran();
            p.matchesSinceInterstitial = 1;
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
            p = Veteran();
            p.lastInterstitialUtc = Now - 7 * 60;
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
        }

        [Test]
        public void Interstitial_DailyCap8()
        {
            PlayerProfile p = Veteran();
            EconomyService.RollDaily(p, Now);
            for (int i = 0; i < 8; i++) p.AddDaily(AdsPolicy.InterstitialKey);
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
        }

        [Test]
        public void Interstitial_SkippedAfterRankedLoss_ThenAllowedNextMatch()
        {
            PlayerProfile p = Veteran();
            AdsPolicy.OnMatchFinished(p, rankedLoss: true);
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
            AdsPolicy.OnMatchFinished(p, rankedLoss: false);
            Assert.IsTrue(AdsPolicy.CanShowInterstitial(p, A, Now, true));
        }

        [Test]
        public void Interstitial_HalvedForHeavyRewardedUsers()
        {
            PlayerProfile p = Veteran();
            p.matchesSinceInterstitial = 3;
            EconomyService.RollDaily(p, Now);
            p.AddDaily(AdsPolicy.RewardedTotalKey, 3);
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
            p.matchesSinceInterstitial = 4;
            Assert.IsTrue(AdsPolicy.CanShowInterstitial(p, A, Now, true));
        }

        [Test]
        public void NoAds_And_PremiumPass_RemoveInterstitials()
        {
            PlayerProfile p = Veteran();
            p.noAds = true;
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
            p = Veteran();
            p.passPremium = true;
            Assert.IsFalse(AdsPolicy.CanShowInterstitial(p, A, Now, true));
            Assert.IsTrue(AdsPolicy.CanShowRewarded(p, RewardedPlacement.FreeGems, AdContext.Shop, A, Now), "Rewarded buttons stay");
        }
    }
}
