using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    public enum CosmeticType { KiteSkin, DumEffect, CutEffect, DorSkin, CharkhiSkin, Avatar, Character, Outfit, Accessory, Emote }
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>A cosmetic item. Rarity changes visuals only – never stats (rule 1).</summary>
    [Serializable]
    public class CosmeticItem
    {
        public string id;
        public string name;
        public CosmeticType type;
        public Rarity rarity;
        /// <summary>For kite skins: which class it fits ("" = any).</summary>
        public string forItem = "";
        public string primaryHex = "#F25A3C";
        public string secondaryHex = "#FFE08A";
        /// <summary>0 = plain, 1 = stripes, 2 = halves, 3 = diamond, 4 = star, 5 = border.</summary>
        public int pattern;
        public int gemPrice;
        public int coinPrice;
        /// <summary>Only from the season pass / events.</summary>
        public bool passOnly;

        // ---- Characters / outfits / accessories / emotes (upgrade v1.4, visual only)
        public string region = "";
        public string skinHex = "";
        public string hairHex = "";
        /// <summary>Hair style (characters), garment style (outfits), model variant (accessories).</summary>
        public int style;
        public float height = 1f;
        public float build = 1f;
        /// <summary>Accessory slot (<see cref="AccessorySlot"/>).</summary>
        public int slot;
        public int unlockLevel = 1;
        public bool loop;
        public float duration;
    }

    [Serializable]
    public class PassReward
    {
        public int level;
        public bool premium;
        public int coins;
        public int gems;
        public string partId;
        public int parts;
        public string cosmeticId;
    }

    [Serializable]
    public class StoreProduct
    {
        public string productId;
        public string name;
        public int gems;
        public bool noAds;
        public bool premiumPass;
        /// <summary>Fallback display price (the store shows the real local price).</summary>
        public string displayPrice;
    }

    [Serializable]
    public class MissionTemplate
    {
        public string id;
        public string text;
        public string stat;
        public int target;
        public int coins;
        public int passXp;
    }

    /// <summary>All catalog content in one place (spec 4.4, 12.4, 13). Generated, original names and colours.</summary>
    public static class Catalog
    {
        /// <summary>Player level that unlocks each kite class (Update classes later, spec 8.1).</summary>
        public static int KiteUnlockLevel(string kiteId)
        {
            switch (kiteId)
            {
                case "pakpao": return 6;
                case "gudiparan": return 12;
                case "chula": return 16;
                case "wau": return 22;
                default: return 1;
            }
        }

        public static int DorUnlockLevel(string dorId) => SpecDefaults.Dor(dorId).unlockLevel;
        public static int CharkhiUnlockLevel(string id) => SpecDefaults.Charkhi(id).unlockLevel;

        public static readonly List<CosmeticItem> Cosmetics = BuildCosmetics();
        public static readonly List<PassReward> PassRewards = BuildPass(50);

        public static readonly List<StoreProduct> Products = new List<StoreProduct>
        {
            new StoreProduct { productId = "kitefall.gems.small", name = "Pouch of gems", gems = 80, displayPrice = "$0.99" },
            new StoreProduct { productId = "kitefall.gems.medium", name = "Bag of gems", gems = 450, displayPrice = "$4.99" },
            new StoreProduct { productId = "kitefall.gems.large", name = "Chest of gems", gems = 1000, displayPrice = "$9.99" },
            new StoreProduct { productId = "kitefall.noads", name = "No ads", noAds = true, displayPrice = "$2.99" },
            new StoreProduct { productId = "kitefall.pass.premium", name = "Premium Season Pass", premiumPass = true, displayPrice = "$4.99" },
        };

        public static readonly List<MissionTemplate> Missions = new List<MissionTemplate>
        {
            new MissionTemplate { id = "play3", text = "Play 3 matches", stat = "matches", target = 3, coins = 120, passXp = 200 },
            new MissionTemplate { id = "win2", text = "Win 2 matches", stat = "wins", target = 2, coins = 150, passXp = 250 },
            new MissionTemplate { id = "cut5", text = "Cut 5 kites", stat = "cuts", target = 5, coins = 150, passXp = 250 },
            new MissionTemplate { id = "ffa1", text = "Finish a Free-for-All", stat = "ffa", target = 1, coins = 100, passXp = 200 },
            new MissionTemplate { id = "sweet", text = "Win a contest with 70 % sweet zone", stat = "sweet70", target = 1, coins = 120, passXp = 200 },
            new MissionTemplate { id = "nosnap", text = "Win a match without a self-snap", stat = "cleanwin", target = 1, coins = 120, passXp = 200 },
            new MissionTemplate { id = "assist", text = "Get 3 assists", stat = "assists", target = 3, coins = 100, passXp = 150 },
            new MissionTemplate { id = "duel3", text = "Play 3 duels", stat = "duels", target = 3, coins = 100, passXp = 150 },
        };

        /// <summary>7-day login cycle (spec 3.2).</summary>
        public static readonly int[] LoginCoins = { 100, 150, 0, 200, 250, 0, 400 };
        public static readonly int[] LoginParts = { 0, 2, 4, 0, 3, 6, 5 };
        public static readonly int[] LoginGems = { 0, 0, 5, 0, 0, 10, 20 };

        public static CosmeticItem Cosmetic(string id)
        {
            foreach (CosmeticItem c in Cosmetics) if (c.id == id) return c;
            return null;
        }

        public static StoreProduct Product(string id)
        {
            foreach (StoreProduct p in Products) if (p.productId == id) return p;
            return null;
        }

        public static MissionTemplate Mission(string id)
        {
            foreach (MissionTemplate m in Missions) if (m.id == id) return m;
            return null;
        }

        /// <summary>Featured shop items for a day (spec 4.4 "Featured (daily)").</summary>
        public static List<CosmeticItem> FeaturedFor(long day, int count = 4)
        {
            var rng = new DeterministicRandom((int)(day * 2654435761L % int.MaxValue));
            var pool = Cosmetics.FindAll(c => !c.passOnly && (c.gemPrice > 0 || c.coinPrice > 0));
            var list = new List<CosmeticItem>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.Range(0, pool.Count);
                list.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return list;
        }

        private static List<CosmeticItem> BuildCosmetics()
        {
            var list = new List<CosmeticItem>();
            string[] kites = { "guddi", "pipa", "rokkaku", "pakpao", "gudiparan", "chula", "wau" };
            var palettes = new[]
            {
                new[] { "Sunset", "#F25A3C", "#FFE08A" }, new[] { "Monsoon", "#2F6FDB", "#9FD8FF" },
                new[] { "Basant", "#F5C400", "#3BA55C" }, new[] { "Night Bazaar", "#3A2A6B", "#FF7AC8" },
                new[] { "Sea Glass", "#21A6A6", "#E8FFF6" }, new[] { "Saffron", "#FF8C1A", "#FFFFFF" },
                new[] { "Jade", "#1F8F5F", "#D9F2B4" }, new[] { "Ember", "#B22222", "#1A1A1A" },
            };
            int n = 0;
            foreach (string kite in kites)
            {
                for (int i = 0; i < palettes.Length; i++)
                {
                    Rarity r = i < 3 ? Rarity.Common : i < 6 ? Rarity.Rare : i == 6 ? Rarity.Epic : Rarity.Legendary;
                    list.Add(new CosmeticItem
                    {
                        id = $"skin_{kite}_{i}",
                        name = $"{SpecDefaults.NameOf(kite)} · {palettes[i][0]}",
                        type = CosmeticType.KiteSkin,
                        rarity = r,
                        forItem = kite,
                        primaryHex = palettes[i][1],
                        secondaryHex = palettes[i][2],
                        pattern = (i + n) % 6,
                        gemPrice = r == Rarity.Common ? 60 : r == Rarity.Rare ? 150 : r == Rarity.Epic ? 300 : 600,
                        coinPrice = r == Rarity.Common ? 2500 : 0,
                        passOnly = i == 7
                    });
                }
                n++;
            }
            string[] dumFx = { "Ribbon sparkle", "Rainbow trail", "Firefly tail", "Petal drift" };
            for (int i = 0; i < dumFx.Length; i++)
                list.Add(new CosmeticItem { id = "dumfx_" + i, name = dumFx[i], type = CosmeticType.DumEffect, rarity = (Rarity)Math.Min(3, i), primaryHex = palettes[i][1], secondaryHex = palettes[i][2], gemPrice = 120 + i * 80 });
            string[] cutFx = { "Paper confetti", "Lantern burst", "Feather swirl", "Firework" };
            for (int i = 0; i < cutFx.Length; i++)
                list.Add(new CosmeticItem { id = "cutfx_" + i, name = cutFx[i], type = CosmeticType.CutEffect, rarity = (Rarity)Math.Min(3, i), primaryHex = palettes[i + 2][1], secondaryHex = palettes[i + 2][2], gemPrice = 150 + i * 100 });
            string[] dorSkins = { "Indigo dye", "Rose dye", "Gold thread look", "Glow (visual only)" };
            for (int i = 0; i < dorSkins.Length; i++)
                list.Add(new CosmeticItem { id = "dorskin_" + i, name = dorSkins[i], type = CosmeticType.DorSkin, rarity = (Rarity)Math.Min(3, i), primaryHex = palettes[(i + 1) % 8][1], secondaryHex = palettes[(i + 1) % 8][2], gemPrice = 80 + i * 60 });
            string[] charkhiSkins = { "Painted wood", "Brass rim", "Truck art", "Mirror work" };
            for (int i = 0; i < charkhiSkins.Length; i++)
                list.Add(new CosmeticItem { id = "charkhiskin_" + i, name = charkhiSkins[i], type = CosmeticType.CharkhiSkin, rarity = (Rarity)Math.Min(3, i), primaryHex = palettes[(i + 3) % 8][1], secondaryHex = palettes[(i + 3) % 8][2], gemPrice = 90 + i * 70 });
            list.AddRange(CharacterCatalog.All());
            return list;
        }

        private static List<PassReward> BuildPass(int levels)
        {
            var list = new List<PassReward>();
            var passSkins = Cosmetics.FindAll(c => c.passOnly && c.type == CosmeticType.KiteSkin);
            passSkins.AddRange(Cosmetics.FindAll(c => c.passOnly && c.type != CosmeticType.KiteSkin));
            int skin = 0;
            for (int lvl = 1; lvl <= levels; lvl++)
            {
                // Free track: coins / parts, a cosmetic every 10 levels.
                var free = new PassReward { level = lvl, premium = false };
                if (lvl % 10 == 0 && skin < passSkins.Count) free.cosmeticId = passSkins[skin++].id;
                else if (lvl % 2 == 0) { free.partId = PartIds.All[lvl / 2 % PartIds.All.Length]; free.parts = 3; }
                else free.coins = 100 + lvl * 5;
                list.Add(free);

                // Premium track: cosmetics every 5, gems every 3, parts otherwise.
                var prem = new PassReward { level = lvl, premium = true };
                if (lvl % 5 == 0 && skin < passSkins.Count) prem.cosmeticId = passSkins[skin++].id;
                else if (lvl % 3 == 0) prem.gems = 15;
                else { prem.partId = PartIds.All[lvl % PartIds.All.Length]; prem.parts = 4; prem.coins = 150; }
                list.Add(prem);
            }
            return list;
        }
    }
}
