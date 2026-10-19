using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>One real-world kite festival the game celebrates (World Kite Calendar, D57).</summary>
    public sealed class KiteFestival
    {
        public string Id;
        public string Name;
        public string Region;
        public int StartMonth, StartDay, EndMonth, EndDay;
        /// <summary>Arena shown first on the home screen during the festival.</summary>
        public ArenaType Arena;

        public bool Contains(DateTime d)
        {
            int v = d.Month * 100 + d.Day, a = StartMonth * 100 + StartDay, b = EndMonth * 100 + EndDay;
            return a <= b ? v >= a && v <= b : v >= a || v <= b; // windows may wrap over New Year
        }
    }

    /// <summary>
    /// Festivals from around the world, so every region gets its own moment in the season (cosmetic banners only –
    /// no gameplay or price changes). Dates are approximate windows; moving holidays use a typical window.
    /// </summary>
    public static class KiteCalendar
    {
        public static readonly List<KiteFestival> Festivals = new List<KiteFestival>
        {
            F("uttarayan", "Makar Sankranti Kite Days", "India · Pakistan", 1, 10, 1, 20, ArenaType.OldCityRooftops),
            F("basant", "Basant Spring Festival", "South Asia", 2, 1, 2, 28, ArenaType.OldCityRooftops),
            F("clean_monday", "Clean Monday Kites", "Greece", 3, 1, 3, 20, ArenaType.MediterraneanHarbour),
            F("good_friday", "Good Friday Kites", "Bermuda · Caribbean", 3, 21, 4, 25, ArenaType.CaribbeanShore),
            F("kodomo", "Children's Day Carp & Kites", "Japan", 4, 26, 5, 10, ArenaType.RiverFestival),
            F("beach", "Summer Beach Festivals", "Europe · Americas", 6, 15, 8, 31, ArenaType.AtlanticBeach),
            F("bali", "Bali Kite Festival", "Indonesia", 7, 1, 8, 15, ArenaType.BaliTerraces),
            F("fiestas_patrias", "Fiestas Patrias Volantines", "Chile", 9, 1, 9, 30, ArenaType.SantiagoRooftops),
            F("barriletes", "Giant Kites Day", "Guatemala", 10, 25, 11, 5, ArenaType.RiverFestival),
            F("pipas", "Pipas Summer Holidays", "Brazil", 12, 1, 1, 9, ArenaType.RioHillside),
        };

        /// <summary>Festival running on this date, or null (then the banner says "World Kite Season").</summary>
        public static KiteFestival Current(DateTime date)
        {
            foreach (KiteFestival f in Festivals) if (f.Contains(date)) return f;
            return null;
        }

        /// <summary>Next festival after this date (for "coming up" text).</summary>
        public static KiteFestival Next(DateTime date)
        {
            for (int i = 1; i <= 366; i++)
            {
                DateTime d = date.AddDays(i);
                foreach (KiteFestival f in Festivals)
                    if (f.Contains(d) && !f.Contains(date)) return f;
            }
            return Festivals[0];
        }

        private static KiteFestival F(string id, string name, string region, int sm, int sd, int em, int ed, ArenaType arena) =>
            new KiteFestival { Id = id, Name = name, Region = region, StartMonth = sm, StartDay = sd, EndMonth = em, EndDay = ed, Arena = arena };
    }
}
