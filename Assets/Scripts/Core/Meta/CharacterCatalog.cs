using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>Accessory slots (upgrade spec F1). Index = slot in <see cref="PlayerProfile.accessories"/>.</summary>
    public enum AccessorySlot { Head = 0, Eyes = 1, Back = 2, Waist = 3 }

    /// <summary>
    /// Characters, outfits, accessories and emotes (upgrade spec F1, F6). Visual only – never stats.
    /// Added to <see cref="Catalog.Cosmetics"/> so buying, owning and equipping reuse the existing server rules.
    /// Look fields: skin/hair colours, hair style, height and build (all inside humanoid limits).
    /// </summary>
    public static class CharacterCatalog
    {
        public const string DefaultCharacter = "rookie_m";
        public static readonly string[] StarterEmotes = { "bo_kata", "thumbs_up", "taunt_wave", "victory_dance" };

        public static readonly List<CosmeticItem> Characters = new List<CosmeticItem>
        {
            Char("rookie_m", "Ali", "Rookie", "#C68B59", "#1E1A17", 0, 1.00f, 1.00f, 1, 0, "kurta_street"),
            Char("rookie_f", "Sana", "Rookie", "#D9A27A", "#2A1C14", 3, 0.96f, 0.95f, 1, 0, "basant_yellow"),
            Char("zara", "Zara", "Lahore", "#CF9A72", "#3B2316", 4, 0.97f, 0.95f, 3, 0, "night_bazaar"),
            Char("rafay", "Rafay", "Karachi", "#B97A4E", "#141210", 1, 1.03f, 1.06f, 5, 0, "coastal_linen"),
            Char("lucas", "Lucas", "Lisbon", "#F0C8A8", "#8A5A2B", 2, 1.04f, 1.02f, 1, 400, "monsoon_jacket"),
            Char("aiko", "Aiko", "Osaka", "#F2D0B0", "#101014", 5, 0.95f, 0.93f, 1, 400, "festival_lights"),
            Char("niran", "Niran", "Chiang Mai", "#D8A878", "#151311", 1, 0.99f, 0.98f, 1, 400, "rooftop_hoodie"),
            Char("farid", "Farid", "Kabul", "#D4A07A", "#2B1D12", 6, 1.02f, 1.04f, 1, 400, "mountain_parka"),
            Char("siti", "Siti", "Kuala Lumpur", "#C99569", "#1A1412", 7, 0.96f, 0.94f, 1, 400, "storm_runner"),
            Char("arjun", "Arjun", "Ahmedabad", "#A9744C", "#121010", 0, 1.01f, 1.03f, 1, 400, "royal_sherwani"),
        };

        public static readonly List<CosmeticItem> Outfits = new List<CosmeticItem>
        {
            Outfit("kurta_street", "Street Kurta", Rarity.Common, 0, "#EDE6D6", "#5B6B7A", 0, 0),
            Outfit("basant_yellow", "Basant Yellow", Rarity.Common, 0, "#F5C400", "#3BA55C", 0, 0),
            Outfit("rooftop_hoodie", "Rooftop Hoodie", Rarity.Common, 2, "#3C4A5C", "#F25A3C", 120, 3000),
            Outfit("oldcity_waistcoat", "Old City Waistcoat", Rarity.Rare, 3, "#F4EFE6", "#7A2E2E", 220, 0),
            Outfit("monsoon_jacket", "Monsoon Jacket", Rarity.Rare, 1, "#2F6FDB", "#22303F", 220, 0),
            Outfit("festival_lights", "Festival Lights", Rarity.Epic, 4, "#FF7AC8", "#FFD84A", 380, 0),
            Outfit("coastal_linen", "Coastal Linen", Rarity.Rare, 0, "#E8FFF6", "#21A6A6", 200, 0),
            Outfit("royal_sherwani", "Royal Sherwani", Rarity.Epic, 3, "#F2E3C0", "#B08A2E", 420, 0),
            Outfit("mountain_parka", "Mountain Parka", Rarity.Rare, 1, "#B22222", "#2E2E2E", 240, 0),
            Outfit("storm_runner", "Storm Runner", Rarity.Epic, 2, "#1F8F5F", "#D9F2B4", 360, 0),
            Outfit("night_bazaar", "Night Bazaar", Rarity.Legendary, 4, "#3A2A6B", "#FF8C1A", 700, 0),
            Outfit("champion_kit", "Champion Kit", Rarity.Legendary, 5, "#FFD84A", "#141414", 0, 0, passOnly: true),
        };

        public static readonly List<CosmeticItem> Accessories = new List<CosmeticItem>
        {
            Acc("acc_topi", "Prayer cap (topi)", AccessorySlot.Head, 0, Rarity.Common, "#F4EFE6", 60),
            Acc("acc_cap", "Rooftop cap", AccessorySlot.Head, 1, Rarity.Common, "#F25A3C", 60),
            Acc("acc_turban", "Festival turban", AccessorySlot.Head, 2, Rarity.Epic, "#FF8C1A", 260),
            Acc("acc_headband", "Sweat band", AccessorySlot.Head, 3, Rarity.Rare, "#2F6FDB", 120),
            Acc("acc_shades", "Sun shades", AccessorySlot.Eyes, 0, Rarity.Rare, "#141414", 140),
            Acc("acc_goggles", "Wind goggles", AccessorySlot.Eyes, 1, Rarity.Epic, "#21A6A6", 240),
            Acc("acc_kitebag", "Kite bag", AccessorySlot.Back, 0, Rarity.Common, "#7A5230", 80),
            Acc("acc_scarf", "Long scarf", AccessorySlot.Back, 1, Rarity.Rare, "#B22222", 150),
            Acc("acc_spare_kite", "Spare kite on the back", AccessorySlot.Back, 2, Rarity.Epic, "#F5C400", 280),
            Acc("acc_pouch", "Belt pouch", AccessorySlot.Waist, 0, Rarity.Common, "#5B3A1E", 70),
            Acc("acc_sash", "Festival sash", AccessorySlot.Waist, 1, Rarity.Rare, "#3BA55C", 150),
            Acc("acc_holster", "Charkhi holster", AccessorySlot.Waist, 2, Rarity.Epic, "#8A6A2E", 260),
        };

        public static readonly List<CosmeticItem> Emotes = new List<CosmeticItem>
        {
            Emote("bo_kata", "Bo Kata!", Rarity.Common, 0, false, 2.2f),
            Emote("thumbs_up", "Thumbs up", Rarity.Common, 0, false, 1.6f),
            Emote("taunt_wave", "Wave", Rarity.Common, 0, false, 1.8f),
            Emote("victory_dance", "Victory dance", Rarity.Common, 0, true, 3.2f),
            Emote("clap", "Clap", Rarity.Common, 60, false, 2f),
            Emote("bow", "Bow", Rarity.Rare, 120, false, 2f),
            Emote("spin", "Spin", Rarity.Rare, 140, false, 1.8f),
            Emote("laugh", "Laugh", Rarity.Rare, 120, false, 2.2f),
            Emote("shrug", "Shrug", Rarity.Common, 60, false, 1.5f),
            Emote("dhol_beat", "Dhol beat", Rarity.Epic, 260, true, 3.5f),
            Emote("salute", "Salute", Rarity.Rare, 120, false, 1.8f),
            Emote("facepalm", "Facepalm", Rarity.Rare, 120, false, 1.8f),
        };

        public static IEnumerable<CosmeticItem> All()
        {
            foreach (CosmeticItem c in Characters) yield return c;
            foreach (CosmeticItem c in Outfits) yield return c;
            foreach (CosmeticItem c in Accessories) yield return c;
            foreach (CosmeticItem c in Emotes) yield return c;
        }

        public static CosmeticItem Character(string id) => Find(Characters, id) ?? Characters[0];
        public static CosmeticItem Outfit(string id) => Find(Outfits, id);
        public static CosmeticItem Accessory(string id) => Find(Accessories, id);
        public static CosmeticItem EmoteItem(string id) => Find(Emotes, id);

        public static int EmoteIndex(string id)
        {
            for (int i = 0; i < Emotes.Count; i++) if (Emotes[i].id == id) return i;
            return -1;
        }

        public static string EmoteId(int index) => index >= 0 && index < Emotes.Count ? Emotes[index].id : "";

        /// <summary>The outfit a character wears when none is equipped.</summary>
        public static string DefaultOutfitOf(string characterId) => Character(characterId).forItem;

        /// <summary>Free from the start, or unlocked by level without paying.</summary>
        public static bool IsFree(CosmeticItem item, int level)
        {
            if (item == null) return false;
            if (item.passOnly) return false;
            if (item.gemPrice > 0 || item.coinPrice > 0) return false;
            return level >= item.unlockLevel;
        }

        private static CosmeticItem Find(List<CosmeticItem> list, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (CosmeticItem c in list) if (c.id == id) return c;
            return null;
        }

        private static CosmeticItem Char(string id, string name, string region, string skin, string hair, int hairStyle, float height, float build, int level, int gems, string defaultOutfit) => new CosmeticItem
        {
            id = id, name = name, type = CosmeticType.Character, rarity = gems > 0 ? Rarity.Epic : Rarity.Common,
            region = region, skinHex = skin, hairHex = hair, style = hairStyle, height = height, build = build,
            unlockLevel = level, gemPrice = gems, forItem = defaultOutfit
        };

        private static CosmeticItem Outfit(string id, string name, Rarity rarity, int style, string primary, string secondary, int gems, int coins, bool passOnly = false) => new CosmeticItem
        {
            id = id, name = name, type = CosmeticType.Outfit, rarity = rarity, style = style,
            primaryHex = primary, secondaryHex = secondary, gemPrice = gems, coinPrice = coins, passOnly = passOnly, unlockLevel = 1
        };

        private static CosmeticItem Acc(string id, string name, AccessorySlot slot, int style, Rarity rarity, string color, int gems) => new CosmeticItem
        {
            id = id, name = name, type = CosmeticType.Accessory, rarity = rarity, slot = (int)slot, style = style,
            primaryHex = color, secondaryHex = "#FFFFFF", gemPrice = gems, unlockLevel = 1
        };

        private static CosmeticItem Emote(string id, string name, Rarity rarity, int gems, bool loop, float duration) => new CosmeticItem
        {
            id = id, name = name, type = CosmeticType.Emote, rarity = rarity, gemPrice = gems, loop = loop, duration = duration, unlockLevel = 1
        };
    }
}
