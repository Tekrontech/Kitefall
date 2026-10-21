using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>Outcome of an economy operation, with a human-readable ledger.</summary>
    public sealed class OpResult
    {
        public bool Ok;
        public string Error;
        public readonly List<string> Ledger = new List<string>();
        public RewardRecord Reward;
        public int RankDelta;
        public bool LevelUp;

        public static OpResult Fail(string error) => new OpResult { Ok = false, Error = error };
        public static OpResult Success() => new OpResult { Ok = true };
        public OpResult Log(string line) { Ledger.Add(line); return this; }
    }

    /// <summary>Context the server knows about a finished match.</summary>
    public struct MatchRewardContext
    {
        public string MatchId;
        public GameMode Mode;
        public int MyIndex;
        public int OpponentRankPoints;
        public bool VersusBotsOnly;
        public long NowUtc;
    }

    /// <summary>
    /// Every change to a <see cref="PlayerProfile"/> goes through here (spec rule 2: server-authoritative economy).
    /// In production this runs inside the Cloud Code module; offline it runs locally.
    /// Pure functions: profile in, profile changed, result out.
    /// </summary>
    public static class EconomyService
    {
        public static long Day(long utc) => utc / 86400L;

        /// <summary>Resets daily counters when a new UTC day starts.</summary>
        public static void RollDaily(PlayerProfile p, long nowUtc)
        {
            long day = Day(nowUtc);
            if (p.countersDay != day)
            {
                p.countersDay = day;
                p.daily.Clear();
            }
        }

        // =====================================================================
        // Account
        // =====================================================================

        public static OpResult CreateProfile(PlayerProfile p, string playerId, long nowUtc)
        {
            p.playerId = playerId;
            p.createdUtc = nowUtc;
            p.displayName = "Flyer" + Math.Abs(playerId.GetHashCode() % 10000).ToString("0000");
            return OpResult.Success();
        }

        /// <summary>Spec 3.1 step 7: 3 Guddi, 1 Rokkaku, 500 coins, starter parts, 600 m Sooti, Lakri.</summary>
        public static OpResult GrantWelcome(PlayerProfile p, EconomyConfig c)
        {
            if (p.welcomeGranted) return OpResult.Fail("Welcome rewards already claimed.");
            p.welcomeGranted = true;
            p.KiteOrAdd("guddi").count += c.welcomeGuddi;
            p.KiteOrAdd("rokkaku").count += c.welcomeRokkaku;
            p.coins += c.welcomeCoins;
            foreach (string part in PartIds.All) p.AddPart(part, c.welcomeParts);
            p.AddDor("sooti", c.welcomeSooti);
            if (!p.charkhis.Contains("lakri")) p.charkhis.Add("lakri");
            p.loadout = new SavedLoadout();
            return OpResult.Success().Log($"Welcome: {c.welcomeGuddi} Guddi, {c.welcomeRokkaku} Rokkaku, {c.welcomeCoins} coins, starter parts, {c.welcomeSooti} m Sooti, Lakri charkhi");
        }

        public static OpResult SetProfileInfo(PlayerProfile p, string name, string region, string country, EconomyConfig c, long nowUtc, bool firstTime)
        {
            string err = NameRules.Validate(name);
            if (err != null) return OpResult.Fail(err);
            if (!firstTime && p.displayName != name.Trim())
            {
                if (nowUtc - p.lastNameChangeUtc < c.nameChangeCooldownSeconds) return OpResult.Fail("You can change your name once every 30 days.");
                p.lastNameChangeUtc = nowUtc;
            }
            p.displayName = name.Trim();
            if (!string.IsNullOrEmpty(region)) p.region = region;
            if (!string.IsNullOrEmpty(country)) p.country = country;
            return OpResult.Success();
        }

        // =====================================================================
        // Daily
        // =====================================================================

        /// <summary>7-day login cycle (spec 3.2). Returns Fail if already claimed today.</summary>
        public static OpResult ClaimDailyLogin(PlayerProfile p, long nowUtc)
        {
            long day = Day(nowUtc);
            if (p.lastLoginDay == day) return OpResult.Fail("Already claimed today.");
            p.loginStreakIndex = p.lastLoginDay == day - 1 ? (p.loginStreakIndex + 1) % 7 : 0;
            p.lastLoginDay = day;
            int i = p.loginStreakIndex;
            var r = OpResult.Success();
            if (Catalog.LoginCoins[i] > 0) { p.coins += Catalog.LoginCoins[i]; r.Log($"+{Catalog.LoginCoins[i]} coins"); }
            if (Catalog.LoginParts[i] > 0)
            {
                string part = PartIds.All[(int)(day % PartIds.All.Length)];
                p.AddPart(part, Catalog.LoginParts[i]);
                r.Log($"+{Catalog.LoginParts[i]} {part}");
            }
            if (Catalog.LoginGems[i] > 0) { p.gems += Catalog.LoginGems[i]; r.Log($"+{Catalog.LoginGems[i]} gems"); }
            return r.Log($"Day {i + 1} of 7");
        }

        /// <summary>The basic cotton line (Sooti). Free and never blocks play (audit B1).</summary>
        public const string BasicDor = "sooti";

        /// <summary>
        /// Audit B1: Sooti is unlimited (not used up) in every mode except Ranked Duel and Last Kite Flying.
        /// Only there do its metres count – and even there a low stock never blocks a match.
        /// </summary>
        public static bool SootiUnlimited(GameMode mode) => mode != GameMode.RankedDuel && mode != GameMode.LastKiteFlying;

        /// <summary>True when today's free Sooti refill has not been given yet (the app claims it on the first launch of the day).</summary>
        public static bool SootiRefillDue(PlayerProfile p, long nowUtc) => p.lastSootiRefillDay != Day(nowUtc);

        /// <summary>Free Sooti refill of 300 m, once per day (spec 4.2). Claimed automatically on the first launch of the day.</summary>
        public static OpResult ClaimSootiRefill(PlayerProfile p, EconomyConfig c, long nowUtc)
        {
            long day = Day(nowUtc);
            if (p.lastSootiRefillDay == day) return OpResult.Fail("Free Sooti refill already used today.");
            p.lastSootiRefillDay = day;
            p.AddDor("sooti", c.sootiDailyRefill);
            return OpResult.Success().Log($"+{c.sootiDailyRefill} m Sooti");
        }

        public static OpResult OpenDailyChest(PlayerProfile p, EconomyConfig c, long nowUtc, bool doubledByAd)
        {
            long day = Day(nowUtc);
            if (p.lastDailyChestDay == day && !doubledByAd) return OpResult.Fail("Daily chest already opened.");
            if (doubledByAd && p.lastDailyChestDay != day) return OpResult.Fail("Open the chest first.");
            p.lastDailyChestDay = day;
            p.coins += c.adDailyChestCoins;
            p.AddPart(PartIds.All[(int)(day % 5)], 3);
            return OpResult.Success().Log($"+{c.adDailyChestCoins} coins, +3 {PartIds.All[(int)(day % 5)]}");
        }

        /// <summary>Three daily missions picked from the day number.</summary>
        public static void EnsureMissions(PlayerProfile p, long nowUtc)
        {
            long day = Day(nowUtc);
            if (p.missionsDay == day && p.missions.Count == 3) return;
            p.missionsDay = day;
            p.missions.Clear();
            var rng = new DeterministicRandom((int)(day % int.MaxValue) * 31 + p.playerId.GetHashCode());
            var pool = new List<MissionTemplate>(Catalog.Missions);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int k = rng.Range(0, pool.Count);
                p.missions.Add(new MissionProgress { id = pool[k].id });
                pool.RemoveAt(k);
            }
        }

        public static OpResult ClaimMission(PlayerProfile p, string missionId, EconomyConfig c, long nowUtc)
        {
            EnsureMissions(p, nowUtc);
            MissionProgress m = p.missions.Find(x => x.id == missionId);
            MissionTemplate t = Catalog.Mission(missionId);
            if (m == null || t == null) return OpResult.Fail("Unknown mission.");
            if (m.claimed) return OpResult.Fail("Already claimed.");
            if (m.progress < t.target) return OpResult.Fail("Not finished yet.");
            m.claimed = true;
            p.coins += t.coins;
            var r = OpResult.Success().Log($"+{t.coins} coins");
            AddPassXp(p, t.passXp, c, r);
            return r;
        }

        // =====================================================================
        // Match result (spec 12.2, 8.3, 9.6, 12.3)
        // =====================================================================

        public static OpResult ApplyMatchResult(PlayerProfile p, MatchResult m, MatchRewardContext ctx, EconomyConfig c, RankSettings ranks)
        {
            if (m == null || ctx.MyIndex < 0 || ctx.MyIndex >= m.Participants.Count) return OpResult.Fail("Bad match result.");
            if (m.Mode == GameMode.Training) return OpResult.Fail("Training gives no rewards.");
            if (!string.IsNullOrEmpty(ctx.MatchId))
            {
                if (p.rewardedMatches.Contains(ctx.MatchId)) return OpResult.Fail("This match was already rewarded.");
                p.rewardedMatches.Add(ctx.MatchId);
                if (p.rewardedMatches.Count > 50) p.rewardedMatches.RemoveAt(0);
            }

            RollDaily(p, ctx.NowUtc);
            EnsureMissions(p, ctx.NowUtc);
            ParticipantResult me = m.Participants[ctx.MyIndex];
            ModeRules rules = ModeRules.For(m.Mode == GameMode.Tutorial ? GameMode.QuickDuel : m.Mode);
            bool won = me.Won;
            var r = OpResult.Success();
            var rng = new DeterministicRandom(m.Seed * 7 + ctx.MyIndex);
            float mult = m.Mode == GameMode.Tutorial ? c.tutorialRewardMult : 1f;

            // ---- Currencies
            int coins = (int)((won ? c.coinsWin : c.coinsLoss) * mult);
            int partCount = (won ? rng.Range(c.partsWinMin, c.partsWinMax + 1) : rng.Range(c.partsLossMin, c.partsLossMax + 1)) + me.Cuts * c.partsPerCut;
            partCount = (int)(partCount * mult);
            p.coins += coins;
            r.Log($"+{coins} coins");
            var reward = new RewardRecord { matchId = ctx.MatchId, coins = coins };
            for (int i = 0; i < partCount; i++)
            {
                string part = PartIds.All[rng.Range(0, PartIds.All.Length)];
                p.AddPart(part, 1);
                PlayerProfile.AddCount(reward.parts, part, 1);
            }
            foreach (ItemCount ic in reward.parts) r.Log($"+{ic.count} {ic.id}");
            r.Reward = reward;
            p.lastReward = reward;

            AddPassXp(p, (int)((won ? c.passXpWin : c.passXpLoss) * mult), c, r);
            AddPlayerXp(p, (int)((won ? c.playerXpWin : c.playerXpLoss) * mult), c, r);
            foreach (string dor in me.DorsUsed) AddMastery(p, dor, won ? c.masteryXpWin : c.masteryXpLoss, c);
            foreach (string ch in me.CharkhisUsed) AddMastery(p, ch, won ? c.masteryXpWin : c.masteryXpLoss, c);

            // ---- Consumption (only the server ever lowers counts / meters).
            // Spec 5: dor is used up only in modes with kite/dor loss. Audit B1: Sooti only in Ranked Duel / Last Kite Flying.
            if (rules.lossMode && m.Mode != GameMode.Tutorial)
            foreach (var kv in me.DorUsed)
            {
                if (kv.Key == BasicDor && SootiUnlimited(m.Mode)) continue;
                float lost = me.DorLost.TryGetValue(kv.Key, out float l) ? l : 0f;
                float total = kv.Value + lost;
                p.AddDor(kv.Key, -total);
                r.Log($"−{total:0} m {SpecDefaults.NameOf(kv.Key)}" + (lost > 0 ? $" ({lost:0} m cut away)" : ""));
            }
            foreach (string kiteId in me.KitesLost)
            {
                if (kiteId == "guddi" && p.Kite("guddi") != null && p.Kite("guddi").count <= 0) continue; // free Guddi
                KiteOwned k = p.Kite(kiteId);
                if (k != null && k.count > 0) { k.count--; r.Log($"−1 {SpecDefaults.NameOf(kiteId)} kite"); }
            }
            foreach (var kv in me.DurabilityEnd)
            {
                KiteOwned k = p.Kite(kv.Key);
                if (k == null) continue;
                bool lostThisKite = me.KitesLost.Contains(kv.Key);
                k.durability = lostThisKite ? 100f : KMath.Clamp(kv.Value, 0f, 100f);
                if (k.durability <= 0f) { if (k.count > 0) k.count--; k.durability = 100f; r.Log($"{SpecDefaults.NameOf(kv.Key)} destroyed (0 % durability)"); }
            }

            // ---- Rank (Ranked Duel only)
            if (m.Mode == GameMode.RankedDuel)
            {
                int delta = me.Quit ? -ranks.quitPenalty : RankRules.Delta(won, p.rankPoints, ctx.OpponentRankPoints, p.winStreak, ranks);
                p.rankPoints = Math.Max(0, p.rankPoints + delta);
                p.rankedMatches++;
                r.RankDelta = delta;
                r.Log($"{(delta >= 0 ? "+" : "")}{delta} rank points");
                int tier = (int)RankRules.TierOf(p.rankPoints, ranks);
                if (tier > p.bestTier) p.bestTier = tier;
                if (me.Quit) RankRules.ApplyQuit(p, ctx.NowUtc, ranks);
            }

            // ---- Stats, history, missions
            if (m.Mode != GameMode.Tutorial)
            {
                p.matches++;
                if (won) { p.wins++; p.winStreak++; p.bestStreak = Math.Max(p.bestStreak, p.winStreak); }
                else if (!m.Draw) p.winStreak = 0;
            }
            p.cuts += me.Cuts;
            if (me.DurabilityEnd.Count > 0) foreach (var kv in me.DurabilityEnd) PlayerProfile.AddCount(p.kiteUses, kv.Key, 1);
            foreach (string d in me.DorsUsed) PlayerProfile.AddCount(p.dorUses, d, 1);

            string opponent = "";
            foreach (ParticipantResult o in m.Participants) if (o.Index != me.Index) { opponent = o.DisplayName; break; }
            p.history.Insert(0, new MatchHistoryEntry
            {
                mode = m.Mode.ToString(), won = won, draw = m.Draw, placement = me.Placement, cuts = me.Cuts,
                rankDelta = r.RankDelta, opponent = m.Participants.Count > 2 ? $"{m.Participants.Count} players" : opponent, timeUtc = ctx.NowUtc
            });
            if (p.history.Count > 20) p.history.RemoveRange(20, p.history.Count - 20);

            Progress(p, "matches", 1);
            if (won) Progress(p, "wins", 1);
            Progress(p, "cuts", me.Cuts);
            Progress(p, "assists", me.Assists);
            if (m.Mode == GameMode.FreeForAll) Progress(p, "ffa", 1);
            if (rules.IsDuel) Progress(p, "duels", 1);
            if (won && me.SelfSnaps == 0) Progress(p, "cleanwin", 1);
            foreach (ContestSummary cs in m.Contests)
                if (cs.Winner == me.Index && (cs.A == me.Index ? cs.SweetA : cs.SweetB) >= 0.7f) { Progress(p, "sweet70", 1); break; }

            AdsPolicy.OnMatchFinished(p, m.Mode == GameMode.RankedDuel && !won && !m.Draw);
            if (m.Mode == GameMode.Tutorial) p.tutorialDone = true;
            else p.firstDuelDone = true;
            return r;
        }

        /// <summary>Rewarded ad on the results screen: coins + parts ×2, never rank points (spec 13.1).</summary>
        public static OpResult DoubleLastReward(PlayerProfile p)
        {
            RewardRecord rr = p.lastReward;
            if (rr == null) return OpResult.Fail("Nothing to double.");
            if (rr.doubled) return OpResult.Fail("Already doubled.");
            rr.doubled = true;
            p.coins += rr.coins;
            var r = OpResult.Success().Log($"+{rr.coins} coins");
            foreach (ItemCount ic in rr.parts) { p.AddPart(ic.id, ic.count); r.Log($"+{ic.count} {ic.id}"); }
            return r;
        }

        private static void Progress(PlayerProfile p, string stat, int amount)
        {
            if (amount <= 0) return;
            foreach (MissionProgress m in p.missions)
            {
                MissionTemplate t = Catalog.Mission(m.id);
                if (t != null && t.stat == stat && !m.claimed) m.progress = Math.Min(t.target, m.progress + amount);
            }
        }

        // =====================================================================
        // XP, levels, mastery, season pass
        // =====================================================================

        public static void AddPlayerXp(PlayerProfile p, int xp, EconomyConfig c, OpResult r)
        {
            if (xp <= 0) return;
            p.xp += xp;
            while (p.level < c.levelMax && p.xp >= c.XpForLevel(p.level))
            {
                p.xp -= c.XpForLevel(p.level);
                p.level++;
                r?.Log($"Level up! Level {p.level}");
                if (r != null) r.LevelUp = true;
            }
        }

        public static void AddMastery(PlayerProfile p, string itemId, int xp, EconomyConfig c)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            MasteryEntry m = p.Mastery(itemId);
            m.xp += xp;
            while (m.level < c.masteryMax && m.xp >= c.masteryXpPerLevel)
            {
                m.xp -= c.masteryXpPerLevel;
                m.level++;
            }
        }

        public static int PassLevel(PlayerProfile p, EconomyConfig c) => Math.Min(c.passLevels, 1 + p.passXp / Math.Max(1, c.passXpPerLevel));

        public static void AddPassXp(PlayerProfile p, int xp, EconomyConfig c, OpResult r)
        {
            if (xp <= 0) return;
            int before = PassLevel(p, c);
            p.passXp += xp;
            r?.Log($"+{xp} Season Pass XP");
            int after = PassLevel(p, c);
            if (after > before) r?.Log($"Season Pass level {after}!");
        }

        public static OpResult ClaimPassReward(PlayerProfile p, int level, bool premium, EconomyConfig c)
        {
            if (level < 1 || level > c.passLevels) return OpResult.Fail("Bad level.");
            if (PassLevel(p, c) < level) return OpResult.Fail("Level not reached yet.");
            if (premium && !p.passPremium) return OpResult.Fail("Premium pass required.");
            List<int> claimed = premium ? p.passClaimedPremium : p.passClaimedFree;
            if (claimed.Contains(level)) return OpResult.Fail("Already claimed.");
            PassReward reward = Catalog.PassRewards.Find(x => x.level == level && x.premium == premium);
            if (reward == null) return OpResult.Fail("No reward.");
            claimed.Add(level);
            var r = OpResult.Success();
            if (reward.coins > 0) { p.coins += reward.coins; r.Log($"+{reward.coins} coins"); }
            if (reward.gems > 0) { p.gems += reward.gems; r.Log($"+{reward.gems} gems"); }
            if (reward.parts > 0) { p.AddPart(reward.partId, reward.parts); r.Log($"+{reward.parts} {reward.partId}"); }
            if (!string.IsNullOrEmpty(reward.cosmeticId)) GiveCosmetic(p, reward.cosmeticId, r);
            return r;
        }

        // =====================================================================
        // Workshop (spec 4.3) – all instant, no timers
        // =====================================================================

        public static OpResult CraftKite(PlayerProfile p, string kiteId, EconomyConfig c)
        {
            // Scope freeze (R7): hidden classes cannot be crafted new; a profile that already has one may keep crafting it.
            if (!LaunchContent.IsLaunchKite(kiteId) && p.Kite(kiteId) == null) return OpResult.Fail(LaunchContent.ComingSoonLabel + ".");
            if (p.level < Catalog.KiteUnlockLevel(kiteId)) return OpResult.Fail($"Unlocks at level {Catalog.KiteUnlockLevel(kiteId)}.");
            if (p.Part(PartIds.Sail) < c.craftKiteSail || p.Part(PartIds.Sticks) < c.craftKiteSticks || p.coins < c.craftKiteCoins)
                return OpResult.Fail($"Needs {c.craftKiteSail} sail, {c.craftKiteSticks} sticks and {c.craftKiteCoins} coins.");
            p.AddPart(PartIds.Sail, -c.craftKiteSail);
            p.AddPart(PartIds.Sticks, -c.craftKiteSticks);
            p.coins -= c.craftKiteCoins;
            KiteOwned k = p.KiteOrAdd(kiteId);
            if (k.count == 0) k.durability = 100f;
            k.count++;
            return OpResult.Success().Log($"+1 {SpecDefaults.NameOf(kiteId)}");
        }

        public static int RepairCost(PlayerProfile p, string kiteId, EconomyConfig c)
        {
            KiteOwned k = p.Kite(kiteId);
            if (k == null) return 0;
            return (int)Math.Ceiling((100f - k.durability) * c.repairCoinsPerPercent);
        }

        public static OpResult Repair(PlayerProfile p, string kiteId, EconomyConfig c, bool freeByAd)
        {
            KiteOwned k = p.Kite(kiteId);
            if (k == null) return OpResult.Fail("You don't own this kite.");
            if (k.durability >= 100f) return OpResult.Fail("Already at full durability.");
            int cost = freeByAd ? 0 : RepairCost(p, kiteId, c);
            if (p.coins < cost) return OpResult.Fail($"Needs {cost} coins.");
            p.coins -= cost;
            k.durability = 100f;
            return OpResult.Success().Log(freeByAd ? "Repaired (free)" : $"Repaired for {cost} coins");
        }

        public static OpResult MakeDorSpool(PlayerProfile p, string dorId, EconomyConfig c)
        {
            int level = Catalog.DorUnlockLevel(dorId);
            if (p.level < level) return OpResult.Fail($"Unlocks at level {level}.");
            int lineParts = c.spoolLineParts + level / 5;
            if (p.Part(PartIds.Line) < lineParts || p.coins < c.spoolCoins) return OpResult.Fail($"Needs {lineParts} line parts and {c.spoolCoins} coins.");
            p.AddPart(PartIds.Line, -lineParts);
            p.coins -= c.spoolCoins;
            p.AddDor(dorId, c.spoolMeters);
            return OpResult.Success().Log($"+{c.spoolMeters} m {SpecDefaults.NameOf(dorId)}");
        }

        public static OpResult CraftCharkhi(PlayerProfile p, string charkhiId, EconomyConfig c)
        {
            int level = Catalog.CharkhiUnlockLevel(charkhiId);
            if (p.level < level) return OpResult.Fail($"Unlocks at level {level}.");
            if (p.HasCharkhi(charkhiId)) return OpResult.Fail("Already owned.");
            if (p.Part(PartIds.Wood) < c.craftCharkhiWood || p.Part(PartIds.Gears) < c.craftCharkhiGears || p.coins < c.craftCharkhiCoins)
                return OpResult.Fail($"Needs {c.craftCharkhiWood} wood, {c.craftCharkhiGears} gears and {c.craftCharkhiCoins} coins.");
            p.AddPart(PartIds.Wood, -c.craftCharkhiWood);
            p.AddPart(PartIds.Gears, -c.craftCharkhiGears);
            p.coins -= c.craftCharkhiCoins;
            p.charkhis.Add(charkhiId);
            return OpResult.Success().Log($"Crafted {SpecDefaults.NameOf(charkhiId)} charkhi");
        }

        /// <summary>Applies a tuning to a dor or charkhi slot (spec 7.4: slots unlock at mastery 3 and 7).</summary>
        public static OpResult ApplyTuning(PlayerProfile p, string itemId, string tuningId, int slot, EconomyConfig c)
        {
            TuningStats t = SpecDefaults.Tuning(tuningId);
            if (string.IsNullOrEmpty(t.id)) return OpResult.Fail("Unknown tuning.");
            bool isDor = SpecDefaults.Dors.Exists(d => d.Key == itemId);
            bool isCharkhi = SpecDefaults.Charkhis.Exists(d => d.Key == itemId);
            if (!isDor && !isCharkhi) return OpResult.Fail("Tunings fit dors and charkhis only.");
            if (isDor && t.target != TuningTarget.Dor || isCharkhi && t.target != TuningTarget.Charkhi) return OpResult.Fail("This tuning does not fit that item.");
            MasteryEntry m = p.Mastery(itemId);
            int need = slot == 0 ? c.tuningSlot1Mastery : c.tuningSlot2Mastery;
            if (slot < 0 || slot > 1) return OpResult.Fail("Bad slot.");
            if (m.level < need) return OpResult.Fail($"Slot {slot + 1} unlocks at mastery {need}.");
            while (m.tunings.Count <= slot) m.tunings.Add("");
            int other = slot == 0 ? 1 : 0;
            if (m.tunings.Count > other && m.tunings[other] == tuningId) return OpResult.Fail("Already in the other slot.");
            m.tunings[slot] = tuningId;
            return OpResult.Success().Log($"{SpecDefaults.NameOf(tuningId)} applied to {SpecDefaults.NameOf(itemId)}");
        }

        public static OpResult ClearTuning(PlayerProfile p, string itemId, int slot)
        {
            MasteryEntry m = p.Mastery(itemId);
            if (slot < m.tunings.Count) m.tunings[slot] = "";
            return OpResult.Success();
        }

        /// <summary>Duplicate skin → parts (spec 4.3).</summary>
        public static OpResult Salvage(PlayerProfile p, string cosmeticId, EconomyConfig c)
        {
            if (p.Cosmetic(cosmeticId) < 2) return OpResult.Fail("Only duplicates can be salvaged.");
            PlayerProfile.AddCount(p.cosmetics, cosmeticId, -1);
            var rng = new DeterministicRandom(cosmeticId.GetHashCode() ^ p.cosmetics.Count);
            var r = OpResult.Success();
            for (int i = 0; i < c.salvageParts; i++)
            {
                string part = PartIds.All[rng.Range(0, PartIds.All.Length)];
                p.AddPart(part, 1);
                r.Log($"+1 {part}");
            }
            return r;
        }

        // =====================================================================
        // Rewarded ads (spec 13.1) – the server checks the daily caps
        // =====================================================================

        public static OpResult RewardedAd(PlayerProfile p, RewardedPlacement placement, string itemId, EconomyConfig c, AdsConfig ads, long nowUtc)
        {
            AdContext ctx = placement == RewardedPlacement.DoubleRewards ? AdContext.Results : AdContext.Home;
            if (!AdsPolicy.CanShowRewarded(p, placement, ctx, ads, nowUtc)) return OpResult.Fail("Daily limit reached.");
            OpResult r;
            switch (placement)
            {
                case RewardedPlacement.DoubleRewards: r = DoubleLastReward(p); break;
                case RewardedPlacement.FreeRepair: r = Repair(p, itemId, c, true); break;
                case RewardedPlacement.DorRefill:
                    p.AddDor("sooti", c.adDorRefill);
                    r = OpResult.Success().Log($"+{c.adDorRefill} m Sooti");
                    break;
                case RewardedPlacement.FreeKite:
                {
                    string kiteId = string.IsNullOrEmpty(itemId) ? "guddi" : itemId;
                    if (!LaunchContent.IsLaunchKite(kiteId) && p.Kite(kiteId) == null) return OpResult.Fail(LaunchContent.ComingSoonLabel + ".");
                    KiteOwned k = p.KiteOrAdd(kiteId);
                    if (k.count > 0) return OpResult.Fail("You still have this kite.");
                    if (p.level < Catalog.KiteUnlockLevel(k.id)) return OpResult.Fail("Kite not unlocked yet.");
                    k.count = 1;
                    k.durability = 100f;
                    r = OpResult.Success().Log($"+1 {SpecDefaults.NameOf(k.id)}");
                    break;
                }
                case RewardedPlacement.DailyChestDouble: r = OpenDailyChest(p, c, nowUtc, true); break;
                case RewardedPlacement.FreeGems:
                    p.gems += c.adFreeGems;
                    r = OpResult.Success().Log($"+{c.adFreeGems} gems");
                    break;
                default:
                    r = OpResult.Success();
                    AddPassXp(p, c.adPassXp, c, r);
                    break;
            }
            if (r.Ok) AdsPolicy.RecordRewarded(p, placement, nowUtc);
            return r;
        }

        // =====================================================================
        // Shop (cosmetics only – rule 1)
        // =====================================================================

        public static OpResult BuyCosmetic(PlayerProfile p, string cosmeticId, bool payWithCoins)
        {
            CosmeticItem item = Catalog.Cosmetic(cosmeticId);
            if (item == null) return OpResult.Fail("Unknown item.");
            if (item.passOnly) return OpResult.Fail("Season Pass reward only.");
            if (!LaunchContent.IsLaunchCosmetic(item)) return OpResult.Fail(LaunchContent.ComingSoonLabel + "."); // scope freeze R7
            if (OwnsCosmetic(p, cosmeticId) && IsUnique(item)) return OpResult.Fail("Already owned.");
            if (p.level < item.unlockLevel) return OpResult.Fail($"Unlocks at level {item.unlockLevel}.");
            if (payWithCoins)
            {
                if (item.coinPrice <= 0) return OpResult.Fail("Gems only.");
                if (p.coins < item.coinPrice) return OpResult.Fail("Not enough coins.");
                p.coins -= item.coinPrice;
            }
            else
            {
                if (p.gems < item.gemPrice) return OpResult.Fail("Not enough gems.");
                p.gems -= item.gemPrice;
            }
            var r = OpResult.Success();
            GiveCosmetic(p, cosmeticId, r);
            return r;
        }

        /// <summary>Characters, outfits, accessories and emotes are owned once.</summary>
        public static bool IsUnique(CosmeticItem item) =>
            item.type == CosmeticType.Character || item.type == CosmeticType.Outfit || item.type == CosmeticType.Accessory || item.type == CosmeticType.Emote;

        /// <summary>Owned = bought / granted, or free at the player's level (rookies, starter emotes, level unlocks).</summary>
        public static bool OwnsCosmetic(PlayerProfile p, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (p.Cosmetic(id) > 0) return true;
            return CharacterCatalog.IsFree(Catalog.Cosmetic(id), p.level);
        }

        public static OpResult EquipCharacter(PlayerProfile p, string id)
        {
            CosmeticItem item = Catalog.Cosmetic(id);
            if (item == null || item.type != CosmeticType.Character) return OpResult.Fail("Unknown character.");
            if (!OwnsCosmetic(p, id)) return OpResult.Fail(p.level < item.unlockLevel ? $"Unlocks at level {item.unlockLevel}." : "Not owned.");
            p.characterId = id;
            return OpResult.Success();
        }

        public static OpResult EquipOutfit(PlayerProfile p, string id)
        {
            if (string.IsNullOrEmpty(id)) { p.outfitId = ""; return OpResult.Success(); }
            CosmeticItem item = Catalog.Cosmetic(id);
            if (item == null || item.type != CosmeticType.Outfit) return OpResult.Fail("Unknown outfit.");
            if (!OwnsCosmetic(p, id) && CharacterCatalog.DefaultOutfitOf(p.characterId) != id) return OpResult.Fail("Not owned.");
            p.outfitId = id;
            return OpResult.Success();
        }

        public static OpResult EquipAccessory(PlayerProfile p, int slot, string id)
        {
            if (slot < 0 || slot > 3) return OpResult.Fail("Bad slot.");
            while (p.accessories.Count < 4) p.accessories.Add("");
            if (string.IsNullOrEmpty(id)) { p.accessories[slot] = ""; return OpResult.Success(); }
            CosmeticItem item = Catalog.Cosmetic(id);
            if (item == null || item.type != CosmeticType.Accessory || item.slot != slot) return OpResult.Fail("Doesn't fit that slot.");
            if (!OwnsCosmetic(p, id)) return OpResult.Fail("Not owned.");
            p.accessories[slot] = id;
            return OpResult.Success();
        }

        public static OpResult SetEmoteSlot(PlayerProfile p, int slot, string id)
        {
            if (slot < 0 || slot > 7) return OpResult.Fail("Bad slot.");
            while (p.emoteSlots.Count < 8) p.emoteSlots.Add("");
            if (!string.IsNullOrEmpty(id))
            {
                CosmeticItem item = Catalog.Cosmetic(id);
                if (item == null || item.type != CosmeticType.Emote) return OpResult.Fail("Unknown emote.");
                if (!OwnsCosmetic(p, id)) return OpResult.Fail("Not owned.");
                int existing = p.emoteSlots.IndexOf(id);
                if (existing >= 0) p.emoteSlots[existing] = p.emoteSlots[slot];
            }
            p.emoteSlots[slot] = id ?? "";
            return OpResult.Success();
        }

        public static OpResult Equip(PlayerProfile p, string cosmeticId)
        {
            CosmeticItem item = Catalog.Cosmetic(cosmeticId);
            if (item != null && item.type == CosmeticType.Character) return EquipCharacter(p, cosmeticId);
            if (item != null && item.type == CosmeticType.Outfit) return EquipOutfit(p, cosmeticId);
            if (item != null && item.type == CosmeticType.Accessory) return EquipAccessory(p, item.slot, cosmeticId);
            if (item == null || p.Cosmetic(cosmeticId) <= 0) return OpResult.Fail("Not owned.");
            p.equippedCosmetics.RemoveAll(id =>
            {
                CosmeticItem e = Catalog.Cosmetic(id);
                return e != null && e.type == item.type && e.forItem == item.forItem;
            });
            p.equippedCosmetics.Add(cosmeticId);
            if (item.type == CosmeticType.KiteSkin) p.KiteOrAdd(item.forItem).skin = cosmeticId;
            return OpResult.Success();
        }

        public static OpResult BuyPremiumPassWithGems(PlayerProfile p, EconomyConfig c)
        {
            if (p.passPremium) return OpResult.Fail("Already premium.");
            if (p.gems < c.premiumPassGems) return OpResult.Fail("Not enough gems.");
            p.gems -= c.premiumPassGems;
            p.passPremium = true;
            return OpResult.Success().Log("Premium Season Pass unlocked");
        }

        /// <summary>Grants a validated store purchase (the server validates the receipt first).</summary>
        public static OpResult GrantPurchase(PlayerProfile p, string productId)
        {
            StoreProduct prod = Catalog.Product(productId);
            if (prod == null) return OpResult.Fail("Unknown product.");
            var r = OpResult.Success();
            if (prod.gems > 0) { p.gems += prod.gems; r.Log($"+{prod.gems} gems"); }
            if (prod.noAds) { p.noAds = true; r.Log("Ads removed (rewarded buttons stay)"); }
            if (prod.premiumPass) { p.passPremium = true; r.Log("Premium Season Pass unlocked"); }
            return r;
        }

        private static void GiveCosmetic(PlayerProfile p, string id, OpResult r)
        {
            PlayerProfile.AddCount(p.cosmetics, id, 1);
            CosmeticItem item = Catalog.Cosmetic(id);
            r.Log($"New: {(item != null ? item.name : id)}");
        }

        // =====================================================================
        // Hangar validation (spec 4.2)
        // =====================================================================

        /// <summary>
        /// Checks if a loadout may be taken into a match; returns null if OK.
        /// Sooti (the free cotton line) never blocks (audit B1) – see <see cref="SootiLow"/> for the non-blocking warning.
        /// </summary>
        public static string ValidateLoadout(PlayerProfile p, SavedLoadout l, EconomyConfig c)
        {
            KiteOwned kite = p.Kite(l.kite);
            bool freeGuddi = l.kite == "guddi";
            if (!freeGuddi && (kite == null || kite.count <= 0)) return $"No {SpecDefaults.NameOf(l.kite)} left – craft one or pick Guddi.";
            if (l.dor != BasicDor && p.DorMeters(l.dor) < c.minDorForMatch)
                return $"Less than {c.minDorForMatch:0} m of {SpecDefaults.NameOf(l.dor)} – make a spool or pick {SpecDefaults.NameOf(BasicDor)}.";
            if (!p.HasCharkhi(l.charkhi)) return $"You don't own the {SpecDefaults.NameOf(l.charkhi)} charkhi.";
            if (p.level < Catalog.DorUnlockLevel(l.dor)) return $"{SpecDefaults.NameOf(l.dor)} unlocks at level {Catalog.DorUnlockLevel(l.dor)}.";
            return null;
        }

        /// <summary>
        /// Ranked Duel / Last Kite Flying only: the Sooti stock is below the match minimum. Shown as a warning with a
        /// one-tap refill – the match still starts (audit B1). Last Kite Flying always starts on Sooti.
        /// </summary>
        public static bool SootiLow(PlayerProfile p, SavedLoadout l, GameMode mode, EconomyConfig c)
        {
            if (SootiUnlimited(mode)) return false;
            bool usesSooti = mode == GameMode.LastKiteFlying || l == null || l.dor == BasicDor;
            return usesSooti && p.DorMeters(BasicDor) < c.minDorForMatch;
        }

        /// <summary>Metres of Sooti a kite always gets in a match, whatever the stock (Sooti never blocks play).</summary>
        public const float SootiMatchFloor = 100f;
        /// <summary>Full spool of Sooti in the modes where it is unlimited.</summary>
        public const float SootiUnlimitedStock = 600f;

        /// <summary>
        /// Builds the match loadout (with tunings and durability) from the saved loadout.
        /// With a mode, Sooti gets a full spool where it is unlimited; it never goes below <see cref="SootiMatchFloor"/>.
        /// </summary>
        public static ParticipantLoadout BuildMatchLoadout(PlayerProfile p, SavedLoadout l, string kiteId, GameMode? mode = null)
        {
            var tunings = new List<string>();
            foreach (string item in new[] { l.dor, l.charkhi })
                foreach (string t in p.Mastery(item).tunings)
                    if (!string.IsNullOrEmpty(t)) tunings.Add(t);
            KiteOwned k = p.Kite(kiteId);
            bool free = kiteId == "guddi" && (k == null || k.count <= 0);
            float durability = free || k == null ? 100f : k.durability;
            float stock = p.DorMeters(l.dor);
            if (l.dor == BasicDor)
                stock = Math.Max(stock, mode.HasValue && SootiUnlimited(mode.Value) ? SootiUnlimitedStock : SootiMatchFloor);
            ParticipantLoadout loadout = SpecDefaults.Loadout(kiteId, l.tail, l.dor, l.charkhi, durability, stock, tunings.ToArray());
            loadout.skinId = k != null ? k.skin : "";
            return loadout;
        }
    }
}
