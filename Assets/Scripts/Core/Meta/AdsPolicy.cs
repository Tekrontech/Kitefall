using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    public enum RewardedPlacement
    {
        DoubleRewards,
        FreeRepair,
        DorRefill,
        FreeKite,
        DailyChestDouble,
        FreeGems,
        PassBonusXp
    }

    public enum AdContext
    {
        Home, Results, Hangar, Workshop, Shop, SeasonPass,
        // Never show ads here (spec 13.3):
        Match, Launch, LoadoutBreak, Contest, Matchmaking, Loading, AppOpen
    }

    [Serializable]
    public class AdsConfig
    {
        public int interstitialMinMatches = 2;
        public float interstitialMinMinutes = 8f;
        public int interstitialDailyCap = 8;
        public int newPlayerGraceDays = 3;
        public int newPlayerGraceMatches = 10;
        public int rewardedHeavyUserThreshold = 3;
        public int capDoubleRewards = 10;
        public int capFreeRepair = 3;
        public int capDorRefill = 3;
        public int capFreeKite = 2;
        public int capDailyChestDouble = 1;
        public int capFreeGems = 2;
        public int capPassBonusXp = 2;

        public int CapFor(RewardedPlacement p)
        {
            switch (p)
            {
                case RewardedPlacement.DoubleRewards: return capDoubleRewards;
                case RewardedPlacement.FreeRepair: return capFreeRepair;
                case RewardedPlacement.DorRefill: return capDorRefill;
                case RewardedPlacement.FreeKite: return capFreeKite;
                case RewardedPlacement.DailyChestDouble: return capDailyChestDouble;
                case RewardedPlacement.FreeGems: return capFreeGems;
                default: return capPassBonusXp;
            }
        }
    }

    /// <summary>
    /// Ad rules (spec 13) as pure functions, so the server and client agree and tests can prove them.
    /// Principle: never during a match or on the path into competition.
    /// </summary>
    public static class AdsPolicy
    {
        public const string RewardedKeyPrefix = "ad_";
        public const string InterstitialKey = "ad_interstitial";
        public const string RewardedTotalKey = "ad_rewarded_total";

        private static readonly HashSet<AdContext> Never = new HashSet<AdContext>
        {
            AdContext.Match, AdContext.Launch, AdContext.LoadoutBreak, AdContext.Contest,
            AdContext.Matchmaking, AdContext.Loading, AdContext.AppOpen
        };

        public static bool AdsAllowedIn(AdContext context) => !Never.Contains(context);

        public static string KeyFor(RewardedPlacement p) => RewardedKeyPrefix + p;

        public static bool CanShowRewarded(PlayerProfile p, RewardedPlacement placement, AdContext context, AdsConfig cfg, long nowUtc)
        {
            if (!AdsAllowedIn(context)) return false;
            EconomyService.RollDaily(p, nowUtc);
            return p.DailyCount(KeyFor(placement)) < cfg.CapFor(placement);
        }

        /// <summary>
        /// Interstitial only after a match when the player taps Next/Home (spec 13.2):
        /// ≥ 2 matches and ≥ 8 min since the last one, max 8/day, none in the first 3 days or 10 matches,
        /// skipped right after a Ranked loss, halved frequency for heavy rewarded users, never with No-ads / Premium Pass.
        /// </summary>
        public static bool CanShowInterstitial(PlayerProfile p, AdsConfig cfg, long nowUtc, bool leavingResults)
        {
            if (!leavingResults) return false;
            if (p.noAds || p.passPremium) return false;
            EconomyService.RollDaily(p, nowUtc);

            if (nowUtc - p.createdUtc < cfg.newPlayerGraceDays * 86400L) return false;
            if (p.matches < cfg.newPlayerGraceMatches) return false;
            if (p.skipNextInterstitial) return false;
            if (p.DailyCount(InterstitialKey) >= cfg.interstitialDailyCap) return false;

            int minMatches = cfg.interstitialMinMatches;
            float minMinutes = cfg.interstitialMinMinutes;
            if (p.DailyCount(RewardedTotalKey) >= cfg.rewardedHeavyUserThreshold)
            {
                minMatches *= 2;
                minMinutes *= 2f;
            }
            if (p.matchesSinceInterstitial < minMatches) return false;
            if (nowUtc - p.lastInterstitialUtc < minMinutes * 60f) return false;
            return true;
        }

        public static void RecordInterstitial(PlayerProfile p, long nowUtc)
        {
            p.matchesSinceInterstitial = 0;
            p.lastInterstitialUtc = nowUtc;
            p.AddDaily(InterstitialKey);
        }

        public static void RecordRewarded(PlayerProfile p, RewardedPlacement placement, long nowUtc)
        {
            EconomyService.RollDaily(p, nowUtc);
            p.AddDaily(KeyFor(placement));
            p.AddDaily(RewardedTotalKey);
        }

        /// <summary>Called after every match (feeds the interstitial counters).</summary>
        public static void OnMatchFinished(PlayerProfile p, bool rankedLoss)
        {
            if (p.skipNextInterstitial && !rankedLoss) p.skipNextInterstitial = false;
            else if (rankedLoss) p.skipNextInterstitial = true;
            p.matchesSinceInterstitial++;
        }
    }
}
