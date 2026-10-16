using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>All configs the economy needs, bundled.</summary>
    [Serializable]
    public class EconomyBundle
    {
        public EconomyConfig economy = new EconomyConfig();
        public AdsConfig ads = new AdsConfig();
        public RankSettings ranks = new RankSettings();
    }

    /// <summary>
    /// One entry point for every player-facing economy action, by name.
    /// The local (offline) backend and the Cloud Code server module both call <see cref="Execute"/>,
    /// so the rules are identical. Clients never change a profile themselves.
    /// </summary>
    public static class EconomyCommands
    {
        public const string Welcome = "welcome";
        public const string SetProfile = "set_profile";
        public const string AcceptTerms = "accept_terms";
        public const string DailyLogin = "daily_login";
        public const string SootiRefill = "sooti_refill";
        public const string DailyChest = "daily_chest";
        public const string ClaimMission = "claim_mission";
        public const string SetLoadout = "set_loadout";
        public const string CraftKite = "craft_kite";
        public const string Repair = "repair";
        public const string MakeSpool = "make_spool";
        public const string CraftCharkhi = "craft_charkhi";
        public const string ApplyTuning = "apply_tuning";
        public const string ClearTuning = "clear_tuning";
        public const string Salvage = "salvage";
        public const string Rewarded = "rewarded";
        public const string Interstitial = "interstitial_shown";
        public const string BuyCosmetic = "buy_cosmetic";
        public const string Equip = "equip";
        public const string BuyPassGems = "buy_pass_gems";
        public const string ClaimPass = "claim_pass";
        public const string GrantPurchase = "grant_purchase";
        public const string ClaimInbox = "claim_inbox";
        public const string AddFriend = "add_friend";
        public const string RemoveFriend = "remove_friend";
        public const string Block = "block";
        public const string Unblock = "unblock";
        public const string RankedQuit = "ranked_quit";
        public const string Link = "link_account";
        public const string SetMode = "set_mode";
        public const string EquipCharacter = "equip_character";
        public const string EquipOutfit = "equip_outfit";
        public const string EquipAccessory = "equip_accessory";
        public const string SetEmoteSlot = "set_emote_slot";
        public const string SetAutoPrep = "set_auto_prep";

        public static OpResult Execute(PlayerProfile p, string op, string[] args, long nowUtc, EconomyBundle cfg)
        {
            args = args ?? new string[0];
            string A(int i) => i < args.Length ? args[i] : "";
            int I(int i) { int.TryParse(A(i), out int v); return v; }
            bool B(int i) => A(i) == "1" || A(i).Equals("true", StringComparison.OrdinalIgnoreCase);
            EconomyConfig c = cfg.economy;

            switch (op)
            {
                case Welcome:
                {
                    OpResult r = EconomyService.GrantWelcome(p, c);
                    if (r.Ok) p.inbox.Add(new InboxMessage { id = "welcome", title = "Welcome to Kitefall!", body = "Read the wind, win the cross, cut the line. Here are 100 coins to get started.", coins = 100, timeUtc = nowUtc });
                    return r;
                }
                case SetProfile: return EconomyService.SetProfileInfo(p, A(0), A(1), A(2), c, nowUtc, B(3));
                case AcceptTerms:
                    p.termsAccepted = true;
                    p.ageConfirmed = true;
                    p.under13 = B(0);
                    p.personalizedAdsConsent = !p.under13 && B(1);
                    return OpResult.Success();
                case DailyLogin: return EconomyService.ClaimDailyLogin(p, nowUtc);
                case SootiRefill: return EconomyService.ClaimSootiRefill(p, c, nowUtc);
                case DailyChest: return EconomyService.OpenDailyChest(p, c, nowUtc, false);
                case ClaimMission: return EconomyService.ClaimMission(p, A(0), c, nowUtc);
                case SetLoadout:
                {
                    var l = new SavedLoadout { kite = A(0), tail = A(1), dor = A(2), charkhi = A(3), stack = new List<string>() };
                    for (int i = 4; i < args.Length; i++) if (!string.IsNullOrEmpty(args[i])) l.stack.Add(args[i]);
                    if (l.stack.Count == 0) l.stack.Add(l.kite);
                    if (!p.HasCharkhi(l.charkhi)) return OpResult.Fail("You don't own that charkhi.");
                    if (p.level < Catalog.DorUnlockLevel(l.dor)) return OpResult.Fail("Dor not unlocked yet.");
                    if (p.level < Catalog.KiteUnlockLevel(l.kite)) return OpResult.Fail("Kite not unlocked yet.");
                    p.loadout = l;
                    return OpResult.Success();
                }
                case SetMode: p.lastMode = A(0); return OpResult.Success();
                case CraftKite: return EconomyService.CraftKite(p, A(0), c);
                case Repair: return EconomyService.Repair(p, A(0), c, false);
                case MakeSpool: return EconomyService.MakeDorSpool(p, A(0), c);
                case CraftCharkhi: return EconomyService.CraftCharkhi(p, A(0), c);
                case ApplyTuning: return EconomyService.ApplyTuning(p, A(0), A(1), I(2), c);
                case ClearTuning: return EconomyService.ClearTuning(p, A(0), I(1));
                case Salvage: return EconomyService.Salvage(p, A(0), c);
                case Rewarded:
                    if (!Enum.TryParse(A(0), out RewardedPlacement placement)) return OpResult.Fail("Unknown placement.");
                    return EconomyService.RewardedAd(p, placement, A(1), c, cfg.ads, nowUtc);
                case Interstitial: AdsPolicy.RecordInterstitial(p, nowUtc); return OpResult.Success();
                case BuyCosmetic: return EconomyService.BuyCosmetic(p, A(0), B(1));
                case Equip: return EconomyService.Equip(p, A(0));
                case EquipCharacter: return EconomyService.EquipCharacter(p, A(0));
                case EquipOutfit: return EconomyService.EquipOutfit(p, A(0));
                case EquipAccessory: return EconomyService.EquipAccessory(p, I(0), A(1));
                case SetEmoteSlot: return EconomyService.SetEmoteSlot(p, I(0), A(1));
                case SetAutoPrep: p.autoPrep = B(0); return OpResult.Success();
                case BuyPassGems: return EconomyService.BuyPremiumPassWithGems(p, c);
                case ClaimPass: return EconomyService.ClaimPassReward(p, I(0), B(1), c);
                case GrantPurchase: return EconomyService.GrantPurchase(p, A(0));
                case ClaimInbox:
                {
                    InboxMessage m = p.inbox.Find(x => x.id == A(0));
                    if (m == null || m.claimed) return OpResult.Fail("Nothing to claim.");
                    m.claimed = true;
                    p.coins += m.coins;
                    p.gems += m.gems;
                    return OpResult.Success().Log($"+{m.coins} coins" + (m.gems > 0 ? $", +{m.gems} gems" : ""));
                }
                case AddFriend:
                    if (string.IsNullOrEmpty(A(0)) || A(0) == p.playerId) return OpResult.Fail("Enter a friend's player ID.");
                    if (!p.friends.Contains(A(0))) p.friends.Add(A(0));
                    return OpResult.Success();
                case RemoveFriend: p.friends.Remove(A(0)); return OpResult.Success();
                case Block:
                    if (!p.blocked.Contains(A(0))) p.blocked.Add(A(0));
                    p.friends.Remove(A(0));
                    return OpResult.Success();
                case Unblock: p.blocked.Remove(A(0)); return OpResult.Success();
                case RankedQuit: RankRules.ApplyQuit(p, nowUtc, cfg.ranks); return OpResult.Success().Log($"−{cfg.ranks.quitPenalty} rank points, queue ban");
                case Link:
                    p.guest = false;
                    p.linkedProvider = A(0);
                    return OpResult.Success().Log("Account linked – your progress is safe.");
                default:
                    return OpResult.Fail("Unknown operation: " + op);
            }
        }
    }
}
