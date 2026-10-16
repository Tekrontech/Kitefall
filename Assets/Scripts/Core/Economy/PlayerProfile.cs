using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    [Serializable] public class ItemCount { public string id; public int count; }

    [Serializable]
    public class KiteOwned
    {
        public string id;
        public int count;
        public float durability = 100f;
        public string skin = "";
    }

    [Serializable] public class MeterStock { public string id; public float meters; }

    [Serializable]
    public class MasteryEntry
    {
        public string id;
        public int level = 1;
        public int xp;
        /// <summary>Applied tuning ids – slot 0 (mastery 3) and slot 1 (mastery 7).</summary>
        public List<string> tunings = new List<string>();
    }

    [Serializable]
    public class SavedLoadout
    {
        public string kite = "guddi";
        public string tail = "short_ribbon";
        public string dor = "sooti";
        public string charkhi = "lakri";
        /// <summary>FFA / Team stack: kite class ids in order.</summary>
        public List<string> stack = new List<string> { "guddi", "guddi", "guddi" };
    }

    [Serializable]
    public class MatchHistoryEntry
    {
        public string mode;
        public bool won;
        public bool draw;
        public int placement;
        public int cuts;
        public int rankDelta;
        public string opponent;
        public long timeUtc;
    }

    [Serializable] public class DailyCounter { public string key; public int count; }

    [Serializable]
    public class MissionProgress
    {
        public string id;
        public int progress;
        public bool claimed;
    }

    [Serializable]
    public class InboxMessage
    {
        public string id;
        public string title;
        public string body;
        public int coins;
        public int gems;
        public bool claimed;
        public long timeUtc;
    }

    [Serializable]
    public class RewardRecord
    {
        public string matchId;
        public int coins;
        public List<ItemCount> parts = new List<ItemCount>();
        public bool doubled;
    }

    /// <summary>
    /// Everything the server stores about a player (spec 12, 14). Only <see cref="EconomyService"/>
    /// changes it, and in production that runs on the server (Cloud Code) – rule 2.
    /// Uses lists, not dictionaries, so Unity's JsonUtility can save it.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public int version = 1;
        public string playerId = "";
        public string displayName = "";
        public string region = "Asia";
        public string country = "PK";
        public bool guest = true;
        public string linkedProvider = "";

        // First launch
        public bool termsAccepted;
        public bool ageConfirmed;
        public bool under13;
        public bool personalizedAdsConsent;
        public bool tutorialDone;
        public bool firstDuelDone;
        public bool welcomeGranted;
        public long createdUtc;
        public long lastNameChangeUtc;

        // Progress & currencies
        public int level = 1;
        public int xp;
        public int coins;
        public int gems;
        public List<ItemCount> parts = new List<ItemCount>();
        public List<KiteOwned> kites = new List<KiteOwned>();
        public List<MeterStock> dors = new List<MeterStock>();
        public List<string> charkhis = new List<string>();
        public List<MasteryEntry> mastery = new List<MasteryEntry>();
        public List<ItemCount> cosmetics = new List<ItemCount>();
        public List<string> equippedCosmetics = new List<string>();
        public SavedLoadout loadout = new SavedLoadout();
        public string lastMode = "QuickDuel";

        // Ranked
        public int rankPoints;
        public int seasonId = 1;
        public int bestTier;
        public int rankedMatches;
        public int winStreak;
        public int bestStreak;
        public List<long> quitTimesUtc = new List<long>();
        public long queueBanUntilUtc;

        // Stats
        public int matches;
        public int wins;
        public int cuts;
        public List<ItemCount> kiteUses = new List<ItemCount>();
        public List<ItemCount> dorUses = new List<ItemCount>();
        public List<MatchHistoryEntry> history = new List<MatchHistoryEntry>();

        // Season pass & monetisation
        public int passXp;
        public bool passPremium;
        public int passSeason = 1;
        public List<int> passClaimedFree = new List<int>();
        public List<int> passClaimedPremium = new List<int>();
        public bool noAds;

        // Daily
        public int loginStreakIndex;
        public long lastLoginDay = -1;
        public long lastSootiRefillDay = -1;
        public long lastDailyChestDay = -1;
        public long countersDay = -1;
        public List<DailyCounter> daily = new List<DailyCounter>();
        public long missionsDay = -1;
        public List<MissionProgress> missions = new List<MissionProgress>();

        // Ads bookkeeping (spec 13.2)
        public int matchesSinceInterstitial;
        public long lastInterstitialUtc;
        public bool skipNextInterstitial;

        // Social
        public List<string> friends = new List<string>();
        public List<string> blocked = new List<string>();
        public List<InboxMessage> inbox = new List<InboxMessage>();

        public RewardRecord lastReward;
        /// <summary>Match ids already rewarded (anti-replay, last 50).</summary>
        public List<string> rewardedMatches = new List<string>();

        // ---- Helpers ---------------------------------------------------------

        public KiteOwned Kite(string id)
        {
            foreach (KiteOwned k in kites) if (k.id == id) return k;
            return null;
        }

        public KiteOwned KiteOrAdd(string id)
        {
            KiteOwned k = Kite(id);
            if (k == null) { k = new KiteOwned { id = id }; kites.Add(k); }
            return k;
        }

        public float DorMeters(string id)
        {
            foreach (MeterStock d in dors) if (d.id == id) return d.meters;
            return 0f;
        }

        public void AddDor(string id, float meters)
        {
            foreach (MeterStock d in dors)
                if (d.id == id) { d.meters = Math.Max(0f, d.meters + meters); return; }
            dors.Add(new MeterStock { id = id, meters = Math.Max(0f, meters) });
        }

        public int Part(string id) => Count(parts, id);
        public void AddPart(string id, int n) => AddCount(parts, id, n);
        public int Cosmetic(string id) => Count(cosmetics, id);
        public bool HasCharkhi(string id) => charkhis.Contains(id);

        public MasteryEntry Mastery(string id)
        {
            foreach (MasteryEntry m in mastery) if (m.id == id) return m;
            var e = new MasteryEntry { id = id };
            mastery.Add(e);
            return e;
        }

        public int DailyCount(string key)
        {
            foreach (DailyCounter c in daily) if (c.key == key) return c.count;
            return 0;
        }

        public void AddDaily(string key, int n = 1)
        {
            foreach (DailyCounter c in daily)
                if (c.key == key) { c.count += n; return; }
            daily.Add(new DailyCounter { key = key, count = n });
        }

        public static int Count(List<ItemCount> list, string id)
        {
            foreach (ItemCount c in list) if (c.id == id) return c.count;
            return 0;
        }

        public static void AddCount(List<ItemCount> list, string id, int n)
        {
            foreach (ItemCount c in list)
                if (c.id == id) { c.count = Math.Max(0, c.count + n); return; }
            if (n > 0) list.Add(new ItemCount { id = id, count = n });
        }

        public string FavouriteKite() => Top(kiteUses);
        public string FavouriteDor() => Top(dorUses);

        private static string Top(List<ItemCount> list)
        {
            string best = null; int n = -1;
            foreach (ItemCount c in list) if (c.count > n) { n = c.count; best = c.id; }
            return best;
        }

        public float WinRate => matches == 0 ? 0f : wins / (float)matches;
    }
}
