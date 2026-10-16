using System;

namespace Kitefall.Core
{
    public enum RankTier { Bronze, Silver, Gold, Platinum, Diamond, Master, SkyLegend }

    [Serializable]
    public class RankSettings
    {
        public int[] tierStart = { 0, 300, 700, 1200, 1800, 2500 };
        public int winBase = 30;
        public int winMin = 25;
        public int winMax = 35;
        public int lossBase = 20;
        public int lossMin = 15;
        public int lossMax = 25;
        /// <summary>Points difference that moves the gain/loss by 1.</summary>
        public int pointsPerStep = 60;
        public int quitPenalty = 35;
        public int quitBanMinutes = 10;
        public int thirdQuitBanMinutes = 60;
        public int streakStart = 3;
        public int streakBonus = 5;
        /// <summary>Season soft reset keeps this share of points above Silver.</summary>
        public float softResetKeep = 0.5f;
        public int skyLegendTop = 500;
        public int rankedUnlockMatches = 10;
    }

    /// <summary>Ranks (spec 12.3): Bronze → … → Master → Sky Legend (global top 500).</summary>
    public static class RankRules
    {
        public static RankTier TierOf(int points, RankSettings s, int globalPosition = -1)
        {
            if (globalPosition > 0 && globalPosition <= s.skyLegendTop && points >= s.tierStart[5]) return RankTier.SkyLegend;
            for (int i = s.tierStart.Length - 1; i >= 0; i--)
                if (points >= s.tierStart[i]) return (RankTier)i;
            return RankTier.Bronze;
        }

        public static string TierName(RankTier t) => t == RankTier.SkyLegend ? "Sky Legend" : t.ToString();

        /// <summary>Rank points for a Ranked Duel result. Beating a stronger opponent gives more.</summary>
        public static int Delta(bool won, int myPoints, int opponentPoints, int winStreakBefore, RankSettings s)
        {
            int steps = (opponentPoints - myPoints) / Math.Max(1, s.pointsPerStep);
            if (won)
            {
                int gain = Clamp(s.winBase + steps, s.winMin, s.winMax);
                // Win streak 3+: +5 per win (spec 12.3). "max 5" read as: bonus capped at +5.
                if (winStreakBefore + 1 >= s.streakStart) gain += s.streakBonus;
                return gain;
            }
            return -Clamp(s.lossBase - steps, s.lossMin, s.lossMax);
        }

        /// <summary>Ranked quit: −35, 10 min queue ban; 3rd quit in a day = 1 hour.</summary>
        public static void ApplyQuit(PlayerProfile p, long nowUtc, RankSettings s)
        {
            p.rankPoints = Math.Max(0, p.rankPoints - s.quitPenalty);
            p.quitTimesUtc.RemoveAll(t => nowUtc - t > 86400);
            p.quitTimesUtc.Add(nowUtc);
            int minutes = p.quitTimesUtc.Count >= 3 ? s.thirdQuitBanMinutes : s.quitBanMinutes;
            p.queueBanUntilUtc = Math.Max(p.queueBanUntilUtc, nowUtc + minutes * 60L);
            p.winStreak = 0;
        }

        public static bool IsRankedUnlocked(PlayerProfile p, RankSettings s) =>
            p.matches >= s.rankedUnlockMatches || TierOf(p.rankPoints, s) >= RankTier.Silver;

        public static int SoftReset(int points, RankSettings s)
        {
            int silver = s.tierStart[1];
            if (points <= silver) return points;
            return silver + (int)((points - silver) * s.softResetKeep);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
