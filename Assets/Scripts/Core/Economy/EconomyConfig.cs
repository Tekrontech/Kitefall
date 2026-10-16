using System;

namespace Kitefall.Core
{
    public static class PartIds
    {
        public const string Sail = "sail";
        public const string Sticks = "sticks";
        public const string Line = "line";
        public const string Wood = "wood";
        public const string Gears = "gears";
        public static readonly string[] All = { Sail, Sticks, Line, Wood, Gears };
    }

    /// <summary>All economy numbers (spec 4.3, 12.2, 13.1). Overridable by Remote Config.</summary>
    [Serializable]
    public class EconomyConfig
    {
        // Per-match rewards (spec 12.2)
        public int coinsWin = 180;
        public int coinsLoss = 70;
        public int partsWinMin = 5;
        public int partsWinMax = 7;
        public int partsLossMin = 2;
        public int partsLossMax = 3;
        public int partsPerCut = 1;
        public int passXpWin = 300;
        public int passXpLoss = 140;
        public int masteryXpWin = 100;
        public int masteryXpLoss = 50;
        public int playerXpWin = 120;
        public int playerXpLoss = 60;
        /// <summary>Bot / practice matches give this share of rewards.</summary>
        public float tutorialRewardMult = 0.5f;

        // Welcome (spec 3.1 step 7)
        public int welcomeGuddi = 3;
        public int welcomeRokkaku = 1;
        public int welcomeCoins = 500;
        public int welcomeParts = 6;
        public float welcomeSooti = 600f;

        // Workshop (spec 4.3)
        public int craftKiteSail = 3;
        public int craftKiteSticks = 2;
        public int craftKiteCoins = 200;
        public float repairCoinsPerPercent = 3f;
        public int spoolMeters = 200;
        public int spoolLineParts = 2;
        public int spoolCoins = 80;
        public int craftCharkhiWood = 3;
        public int craftCharkhiGears = 2;
        public int craftCharkhiCoins = 250;
        public int salvageParts = 3;
        public float sootiDailyRefill = 300f;
        /// <summary>A dor type needs at least this many metres to be taken into a match (spec 4.2).</summary>
        public float minDorForMatch = 100f;

        // Mastery (spec 7.4)
        public int masteryXpPerLevel = 500;
        public int masteryMax = 10;
        public int tuningSlot1Mastery = 3;
        public int tuningSlot2Mastery = 7;

        // Player level
        public int levelXpBase = 400;
        public int levelXpStep = 150;
        public int levelMax = 60;

        // Season pass (spec 4.4)
        public int passLevels = 50;
        public int passXpPerLevel = 1000;
        public int premiumPassGems = 950;

        // Rewarded ads (spec 13.1)
        public int adDailyChestCoins = 100;
        public int adDorRefill = 200;
        public int adFreeGems = 5;
        public int adPassXp = 150;

        // Name change (spec 4.4: once / 30 days)
        public long nameChangeCooldownSeconds = 30L * 24 * 3600;

        public int XpForLevel(int level) => levelXpBase + (level - 1) * levelXpStep;
    }
}
