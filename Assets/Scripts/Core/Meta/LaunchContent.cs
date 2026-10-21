using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>
    /// Scope freeze (audit R7): the ONE place that says what players see at launch. Nothing is deleted – content that is
    /// not in the launch set stays in the data (saves, servers, tests and the art list keep working) but is hidden from
    /// pickers, the shop, the wardrobe, random arena choice and bots. With <see cref="ShowComingSoon"/> on, hidden items
    /// are shown but locked and marked "Coming soon".
    /// A saved profile that already owns or wears a hidden item keeps it: pickers still list what the player has, and only
    /// places that MUST pick something (arena, bot looks) fall back to a launch item.
    /// </summary>
    public static class LaunchContent
    {
        /// <summary>Show hidden content as locked "Coming soon" entries (Settings ▸ Developer). Default off.</summary>
        public static bool ShowComingSoon;

        public const string ComingSoonLabel = "Coming soon";

        /// <summary>Launch arenas, in picker order.</summary>
        public static readonly ArenaType[] Arenas = { ArenaType.OldCityRooftops, ArenaType.RiverFestival };

        /// <summary>
        /// Launch kite classes. Guddi is the starter (always free) and the balanced middle of every stat; Rokkaku is in the
        /// welcome kit and is the steady, strong-wind kite; Pipa is the fast, agile kite. Together they cover the three basic
        /// play styles (balanced / heavy / quick) with simple stats, so new players learn the core rock-paper-scissors before
        /// the specialist classes arrive. These are also the classes marked Launch = true in <see cref="SpecDefaults.Kites"/>.
        /// </summary>
        public static readonly string[] Kites = { "guddi", "rokkaku", "pipa" };

        /// <summary>Launch characters: the two free rookies and the next two original flyers (free level unlocks).</summary>
        public static readonly string[] Characters = { "rookie_f", "rookie_m", "zara", "rafay" };

        // ---- Arenas -----------------------------------------------------------------------------------------------

        public static bool IsLaunchArena(ArenaType a) => System.Array.IndexOf(Arenas, a) >= 0;

        /// <summary>
        /// Arena a match of this mode really uses. Last Kite Flying always plays on Storm Archipelago (an original arena,
        /// allowed for that mode only); Tutorial / Training keep what they ask for; every other mode gets a launch arena
        /// (a hidden saved / festival choice falls back to the first launch arena).
        /// </summary>
        public static ArenaType ArenaForMatch(GameMode mode, ArenaType requested)
        {
            if (mode == GameMode.LastKiteFlying) return ArenaType.StormArchipelago;
            if (mode == GameMode.Tutorial || mode == GameMode.Training) return requested;
            return ArenaOrDefault(requested); // Coming soon arenas are locked, never played

        }

        /// <summary>A launch arena: the given one if it is in the launch set, otherwise the first launch arena.</summary>
        public static ArenaType ArenaOrDefault(ArenaType a) => IsLaunchArena(a) ? a : Arenas[0];

        /// <summary>Arenas for pickers: the launch set, then (only with Coming soon on) every other arena, locked.</summary>
        public static List<ArenaType> PickerArenas()
        {
            var list = new List<ArenaType>(Arenas);
            if (ShowComingSoon)
                foreach (ArenaType a in (ArenaType[])System.Enum.GetValues(typeof(ArenaType)))
                    if (!list.Contains(a)) list.Add(a);
            return list;
        }

        // ---- Kites ------------------------------------------------------------------------------------------------

        public static bool IsLaunchKite(string kiteId) => System.Array.IndexOf(Kites, kiteId) >= 0;

        /// <summary>Kite classes for pickers / crafting: launch kites, plus hidden ones the player already has (or Coming soon).</summary>
        public static List<SpecDefaults.KiteInfo> PickerKites(System.Func<string, bool> owned = null)
        {
            var list = new List<SpecDefaults.KiteInfo>();
            foreach (SpecDefaults.KiteInfo k in SpecDefaults.Kites)
                if (IsLaunchKite(k.Stats.id) || ShowComingSoon || owned != null && owned(k.Stats.id)) list.Add(k);
            return list;
        }

        /// <summary>Closest launch kite for a hidden class (bots never fly hidden kites).</summary>
        public static string LaunchKiteFor(string kiteId)
        {
            if (IsLaunchKite(kiteId)) return kiteId;
            switch (kiteId)
            {
                case "pakpao": case "volantin": return "pipa";           // quick turns
                case "gudiparan": case "chula": case "box": return "rokkaku"; // heavy, stable
                default: return "guddi";                                  // wau, delta: balanced
            }
        }

        // ---- Characters & cosmetics ----------------------------------------------------------------------------------

        public static bool IsLaunchCharacter(string id) => System.Array.IndexOf(Characters, id) >= 0;

        /// <summary>
        /// True if a cosmetic belongs to the launch set: characters in <see cref="Characters"/>, kite skins of launch kites,
        /// everything else (outfits, accessories, emotes, effects) is not frozen.
        /// </summary>
        public static bool IsLaunchCosmetic(CosmeticItem item)
        {
            if (item == null) return false;
            if (item.type == CosmeticType.Character) return IsLaunchCharacter(item.id);
            if (item.type == CosmeticType.KiteSkin) return string.IsNullOrEmpty(item.forItem) || IsLaunchKite(item.forItem);
            return true;
        }

        /// <summary>Should a list (shop, wardrobe) show this item? Launch items, items the player owns / wears, or Coming soon.</summary>
        public static bool IsListed(CosmeticItem item, bool ownedOrEquipped = false) =>
            item != null && (IsLaunchCosmetic(item) || ownedOrEquipped || ShowComingSoon);

        /// <summary>Shown only as a locked "Coming soon" entry (cannot be bought or picked).</summary>
        public static bool IsComingSoon(CosmeticItem item, bool ownedOrEquipped = false) =>
            item != null && !IsLaunchCosmetic(item) && !ownedOrEquipped;

        /// <summary>Launch characters as catalog items (bots and random picks).</summary>
        public static List<CosmeticItem> LaunchCharacters()
        {
            var list = new List<CosmeticItem>();
            foreach (CosmeticItem c in CharacterCatalog.Characters) if (IsLaunchCharacter(c.id)) list.Add(c);
            return list;
        }
    }
}
