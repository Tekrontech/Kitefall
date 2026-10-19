using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>Which kite words the player sees (Settings ▸ Gameplay ▸ Kite words). Gameplay is identical in both.</summary>
    public enum KiteWords { International = 0, SouthAsian = 1 }

    /// <summary>
    /// Kitefall is an international game (CLAUDE.md, D57). Plain English words by default – Release / Pull / Line / Spool /
    /// Tail – with the classic South Asian words (Dheel / Kheench / Dor / Charkhi / Dum) one setting away.
    /// Item names follow the same choice: "Cotton line" vs "Sooti". Kite class names are proper names and never change.
    /// </summary>
    public static class Terms
    {
        public static KiteWords Words = KiteWords.International;
        public static bool SouthAsian => Words == KiteWords.SouthAsian;

        public static string Release => SouthAsian ? "Dheel" : "Release";
        public static string Pull => SouthAsian ? "Kheench" : "Pull";
        public static string Line => SouthAsian ? "Dor" : "Line";
        public static string Spool => SouthAsian ? "Charkhi" : "Spool";
        public static string Tail => SouthAsian ? "Dum" : "Tail";
        /// <summary>Shout when a line is cut.</summary>
        public static string CutCall => SouthAsian ? "BO KATA!" : "CUT!";

        public static string ReleaseUpper => Release.ToUpperInvariant();
        public static string PullUpper => Pull.ToUpperInvariant();
        public static string LineLower => Line.ToLowerInvariant();
        public static string SpoolLower => Spool.ToLowerInvariant();
        public static string TailLower => Tail.ToLowerInvariant();

        /// <summary>International names for loadout items (ids stay the same, saves and servers are unaffected).</summary>
        public static readonly Dictionary<string, string> International = new Dictionary<string, string>
        {
            // Lines (dor)
            { "sooti", "Cotton line" }, { "reshmi", "Silk-spun line" }, { "tez_dhaar", "Fighter line" },
            { "mazboot", "Strong line" }, { "lambi", "Long line" }, { "lachakdaar", "Springy line" },
            // Spools (charkhi)
            { "lakri", "Wooden spool" }, { "speed", "Speed spool" }, { "bari", "Big drum spool" },
            { "brake", "Brake spool" }, { "gear", "Geared spool" }
        };

        /// <summary>Display name for an item / kite id in the chosen words.</summary>
        public static string NameOf(string id, string localName)
        {
            if (!SouthAsian && id != null && International.TryGetValue(id, out string n)) return n;
            return localName;
        }
    }
}
