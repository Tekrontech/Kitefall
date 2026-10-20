using System.Collections.Generic;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.UI
{
    /// <summary>One image the game can use (a row of docs/Kitefall_Image_Assets.xlsx).</summary>
    public readonly struct ArtSlot
    {
        public readonly int Number;
        public readonly string Id;
        public readonly string Category;
        public readonly string Size;
        /// <summary>App icon / store art: not packed into Resources, only used by the build.</summary>
        public readonly bool BuildOnly;

        public ArtSlot(int number, string id, string category, string size, bool buildOnly)
        {
            Number = number; Id = id; Category = category; Size = size; BuildOnly = buildOnly;
        }

        public string ResourcePath => Art.ResourceFolder + "/" + Id;
    }

    public static partial class ArtSlots
    {
        private static Dictionary<string, ArtSlot> _byId;

        public static bool TryGet(string id, out ArtSlot slot)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, ArtSlot>();
                foreach (ArtSlot s in All) _byId[s.Id] = s;
            }
            if (_byId.TryGetValue(id, out slot)) return true;
            // Short keys: "skin_guddi_0" finds "skin_guddi_0_sunset".
            foreach (ArtSlot s in All)
                if (s.Id.Length > id.Length && s.Id.StartsWith(id) && s.Id[id.Length] == '_') { slot = s; _byId[id] = s; return true; }
            return false;
        }
    }

    /// <summary>
    /// Hand-made images (the artist's files) with a code-drawn fallback. The Image Mapper (Kitefall > Art > Image Mapper)
    /// renames the artist's files to the slot ids and moves them to Assets/Resources/KitefallArt, so any file name works.
    /// Images load on first use only; a missing image returns null and the game keeps its generated look.
    /// </summary>
    public static class Art
    {
        public const string ResourceFolder = "KitefallArt";
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Sprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Cache.TryGetValue(key, out Sprite s)) return s;
            s = null;
            if (ArtSlots.TryGet(key, out ArtSlot slot) && !slot.BuildOnly)
            {
                s = Resources.Load<Sprite>(slot.ResourcePath);
                if (s == null)
                {
                    // Imported as a plain texture: wrap it.
                    Texture2D t = Resources.Load<Texture2D>(slot.ResourcePath);
                    if (t != null) s = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            Cache[key] = s;
            return s;
        }

        public static Texture2D Texture(string key)
        {
            Sprite s = Sprite(key);
            return s != null ? s.texture : null;
        }

        public static bool Has(string key) => Sprite(key) != null;

        /// <summary>Drops cached lookups (the mapper calls this after installing images in play mode).</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>Card / icon image for a shop or wardrobe item.</summary>
        public static Sprite ForCosmetic(CosmeticItem item)
        {
            if (item == null) return null;
            switch (item.type)
            {
                case CosmeticType.KiteSkin: return Sprite(item.id);
                case CosmeticType.Character: return Sprite("char_" + item.id + "_portrait");
                case CosmeticType.Outfit: return Sprite("outfit_" + item.id);
                case CosmeticType.Accessory: return Sprite(item.id);
                case CosmeticType.Emote: return Sprite("emote_" + item.id);
                case CosmeticType.DumEffect:
                case CosmeticType.CutEffect:
                case CosmeticType.DorSkin:
                case CosmeticType.CharkhiSkin: return Sprite(item.id);
                default: return null;
            }
        }

        public static Sprite Portrait(string characterId) => Sprite("char_" + characterId + "_portrait");

        /// <summary>Arena card picture (arena_old_city_card …), or null.</summary>
        public static Sprite ForArena(ArenaType type)
        {
            switch (type)
            {
                case ArenaType.OldCityRooftops: return Sprite("arena_old_city_card");
                case ArenaType.CoastalCliffs: return Sprite("arena_coastal_cliffs_card");
                case ArenaType.RiverFestival: return Sprite("arena_river_festival_card");
                case ArenaType.RoyalField: return Sprite("arena_royal_field_card");
                case ArenaType.MountainValley: return Sprite("arena_mountain_valley_card");
                case ArenaType.StormArchipelago: return Sprite("arena_storm_archipelago_card");
                case ArenaType.RioHillside: return Sprite("arena_rio_hillside_card");
                case ArenaType.SantiagoRooftops: return Sprite("arena_santiago_rooftops_card");
                case ArenaType.MediterraneanHarbour: return Sprite("arena_mediterranean_harbour_card");
                case ArenaType.AtlanticBeach: return Sprite("arena_atlantic_beach_card");
                case ArenaType.CaribbeanShore: return Sprite("arena_caribbean_shore_card");
                default: return Sprite("arena_bali_terraces_card");
            }
        }

        /// <summary>Mode card picture (mode_quick_duel_card …), or null.</summary>
        public static Sprite ForMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.QuickDuel: return Sprite("mode_quick_duel_card");
                case GameMode.RankedDuel: return Sprite("mode_ranked_duel_card");
                case GameMode.FreeForAll: return Sprite("mode_ffa_card");
                case GameMode.PrivateRoom: return Sprite("mode_private_room_card");
                case GameMode.TeamBattle: return Sprite("mode_team_battle_card");
                case GameMode.LastKiteFlying: return Sprite("mode_last_kite_flying_card");
                case GameMode.Training: return Sprite("mode_training_card");
                case GameMode.Tutorial: return Sprite("mode_tutorial_card");
                default: return null;
            }
        }

        /// <summary>Daily mission badge: Catalog mission id "play3" → mission_play3.</summary>
        public static Sprite ForMission(string missionId) => string.IsNullOrEmpty(missionId) ? null : Sprite("mission_" + missionId);

        /// <summary>Tuning medallion: SpecDefaults tuning id "waxed_coat" → tuning_waxed_coat.</summary>
        public static Sprite ForTuning(string tuningId) => string.IsNullOrEmpty(tuningId) ? null : Sprite("tuning_" + tuningId);

        /// <summary>Gem pack / No ads / Premium pass store tile.</summary>
        public static Sprite ForProduct(StoreProduct product)
        {
            if (product == null) return null;
            if (product.noAds) return Sprite("shop_no_ads");
            if (product.premiumPass) return Sprite("shop_premium_pass");
            switch (product.productId)
            {
                case "kitefall.gems.small": return Sprite("shop_gems_small");
                case "kitefall.gems.medium": return Sprite("shop_gems_medium");
                case "kitefall.gems.large": return Sprite("shop_gems_large");
                // Packs added later: pick by size.
                default: return product.gems <= 0 ? null : Sprite(product.gems < 200 ? "shop_gems_small" : product.gems < 800 ? "shop_gems_medium" : "shop_gems_large");
            }
        }

        /// <summary>Roof hopping path icon (link_plank …); jumps have no picture.</summary>
        public static Sprite ForLink(RoofLinkKind kind)
        {
            switch (kind)
            {
                case RoofLinkKind.Plank: return Sprite("link_plank");
                case RoofLinkKind.RopeBridge: return Sprite("link_rope_bridge");
                case RoofLinkKind.Ladder: return Sprite("link_ladder");
                case RoofLinkKind.Stairs: return Sprite("link_stairs");
                default: return null;
            }
        }

        public static Sprite Rank(RankTier tier) => Sprite(tier == RankTier.SkyLegend ? "rank_sky_legend" : "rank_" + tier.ToString().ToLowerInvariant());

        /// <summary>Full-screen background for a menu screen class name (null = keep the 3D scene / colour).</summary>
        public static Sprite ForScreen(string screenClass)
        {
            switch (screenClass)
            {
                case "ShopScreen": return Sprite("bg_shop");
                case "HangarScreen":
                case "WorkshopScreen": return Sprite("bg_hangar");
                case "WardrobeScreen": return Sprite("bg_wardrobe");
                case "SquadLobbyScreen": return Sprite("bg_squad_lobby");
                case "SeasonPassScreen": return Sprite("bg_pass");
                case "SettingsScreen":
                case "ProfileScreen":
                case "FriendsScreen":
                case "InboxScreen":
                case "LeaderboardScreen": return Sprite("bg_settings");
                default: return null;
            }
        }
    }
}
