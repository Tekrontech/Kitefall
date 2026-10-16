using System;

namespace Kitefall.Core
{
    [Serializable]
    public class MatchmakingSettings
    {
        public float widenAfter = 10f;
        public float botFillAfter = 20f;
        public int startTierRange = 1;
        public int widenedTierRange = 2;
        public int targetPingMs = 120;
        public int maxPingMs = 250;
    }

    /// <summary>Matchmaking rules (spec 6): same region, rank ±1 tier, ±2 after 10 s, bots after 20 s (not Ranked).</summary>
    public static class MatchmakingRules
    {
        public static readonly string[] Regions = { "Asia", "Middle East", "Europe", "Americas" };

        public static int TierRange(float waitSeconds, MatchmakingSettings s) =>
            waitSeconds >= s.widenAfter ? s.widenedTierRange : s.startTierRange;

        public static bool CanMatch(int myPoints, int theirPoints, float waitSeconds, RankSettings ranks, MatchmakingSettings s)
        {
            int a = (int)RankRules.TierOf(myPoints, ranks);
            int b = (int)RankRules.TierOf(theirPoints, ranks);
            return Math.Abs(a - b) <= TierRange(waitSeconds, s);
        }

        public static bool ShouldFillWithBots(ModeRules rules, float waitSeconds, MatchmakingSettings s) =>
            rules.allowBots && !rules.ranked && waitSeconds >= s.botFillAfter;

        /// <summary>Ping above 250 ms: warning and no Ranked queue for that region (spec 14.1).</summary>
        public static bool AllowedInRanked(int pingMs, MatchmakingSettings s) => pingMs <= s.maxPingMs;

        public static string RegionFromCountry(string country)
        {
            switch (country)
            {
                case "AE": case "SA": case "QA": case "KW": case "OM": case "BH": case "IR": case "IQ": case "EG": case "TR": return "Middle East";
                case "GB": case "DE": case "FR": case "ES": case "IT": case "NL": case "PL": case "SE": case "NO": case "PT": case "RU": return "Europe";
                case "US": case "CA": case "BR": case "MX": case "AR": case "CO": case "CL": case "PE": return "Americas";
                default: return "Asia";
            }
        }
    }
}
