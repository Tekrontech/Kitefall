using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Matches file names that DESCRIBE the picture ("Golden Kite Bazaar at Sunset", "Gilded Teal Lotus Mandala Tile" –
    /// the titles AI tools give their downloads) against what each slot should show (its prompt in the image list).
    /// Rare words count more than common ones (bazaar, tailor, teal, lotus beat kite, festival, golden).
    /// </summary>
    internal static class ArtDescriptionMatcher
    {
        private static readonly HashSet<string> Stop = new HashSet<string>(
            ("the a an of and with on in at over for from to by its into as is are this that png jpg jpeg webp copy final image img new " +
             "version kitefall screen game art style painted hand flat front view also used where random behind tips card shop thumbnail " +
             "texture sheet design artwork fills edge symmetrical colour color scheme fine ink linework rich ornament high quality " +
             "detailed illustration render digital generated upscaled").Split(' '));

        /// <summary>Time of day and praise words: every Kitefall picture is warm golden hour, so they say little.</summary>
        private static readonly HashSet<string> Low = new HashSet<string>(
            "gold hour sunset twilight glow warm light glowing shining beautiful majestic classic classical ornate elegant vintage traditional".Split(' '));

        private static readonly Dictionary<string, string> Syn = new Dictionary<string, string>
        {
            { "golden", "gold" }, { "gilded", "gold" }, { "gilt", "gold" }, { "festive", "festival" }, { "rooftops", "roof" }, { "rooftop", "roof" },
            { "roofs", "roof" }, { "kites", "kite" }, { "textile", "pattern" }, { "tile", "pattern" }, { "tiles", "pattern" }, { "patterns", "pattern" },
            { "mandalas", "mandala" }, { "sunrise", "sunset" }, { "dusk", "twilight" }, { "evening", "twilight" }, { "chamber", "room" },
            { "atelier", "workshop" }, { "tailors", "tailor" }, { "market", "bazaar" }, { "stall", "bazaar" }, { "bazaars", "bazaar" },
            { "red", "crimson" }, { "vermilion", "crimson" }, { "scarlet", "crimson" }, { "ruby", "crimson" }, { "emerald", "green" },
            { "jade", "green" }, { "lime", "green" }, { "mint", "teal" }, { "turquoise", "teal" }, { "aqua", "teal" }, { "cyan", "teal" },
            { "navy", "blue" }, { "indigo", "blue" }, { "sapphire", "blue" }, { "purple", "violet" }, { "amethyst", "violet" },
            { "pink", "rose" }, { "magenta", "rose" }, { "orange", "saffron" }, { "amber", "saffron" }, { "mustard", "yellow" },
            { "lemon", "yellow" }, { "charcoal", "black" }, { "white", "ivory" }, { "cream", "ivory" }, { "lattice", "jali" },
            { "flowers", "floral" }, { "flower", "floral" }, { "blossom", "floral" }, { "ocean", "sea" }, { "coastal", "sea" }, { "coast", "sea" },
            { "cliffs", "cliff" }, { "riverside", "river" }, { "mountains", "mountain" }, { "valley", "mountain" }, { "stormy", "storm" },
            { "islands", "island" }, { "palace", "royal" }, { "town", "city" }, { "spool", "charkhi" }, { "reel", "charkhi" },
            { "thread", "dor" }, { "string", "dor" }, { "tail", "dum" }, { "coins", "coin" }, { "gems", "gem" }, { "jewels", "gem" },
            { "shield", "badge" }, { "emblem", "badge" }, { "crest", "badge" }, { "parchment", "frame" },
            { "hexagonal", "hexagonal" }, { "beeswax", "wax" }, { "waxed", "wax" }, { "twisted", "twist" }, { "medallion", "medallion" }
        };

        private static Dictionary<string, HashSet<string>> _docs;
        private static Dictionary<string, int> _df;

        public static List<string> Tokens(string text)
        {
            text = Regex.Replace(text ?? "", "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
            var list = new List<string>();
            foreach (string raw in Regex.Split(text, "[^a-z]+"))
            {
                if (raw.Length < 3 || Stop.Contains(raw)) continue;
                string w = Syn.TryGetValue(raw, out string s) ? s : raw;
                w = Stem(w);
                if (Syn.TryGetValue(w, out string s2)) w = s2;
                list.Add(w);
            }
            return list;
        }

        /// <summary>Tiny stemmer: loosely→loose, coiled→coil, kites→kite, hanging→hang.</summary>
        private static string Stem(string w)
        {
            if (w.Length > 5 && w.EndsWith("ly")) w = w.Substring(0, w.Length - 2);
            else if (w.Length > 5 && w.EndsWith("ing")) w = w.Substring(0, w.Length - 3);
            else if (w.Length > 5 && w.EndsWith("ed")) w = w.Substring(0, w.Length - 2);
            if (w.Length > 4 && w.EndsWith("s") && !w.EndsWith("ss")) w = w.Substring(0, w.Length - 1);
            return w;
        }

        private static readonly string[] Kites = { "guddi", "pipa", "rokkaku", "pakpao", "gudiparan", "chula", "wau" };
        private static readonly string[][] Palettes =
        {
            new[] { "sunset" }, new[] { "monsoon" }, new[] { "basant" }, new[] { "night", "bazaar" },
            new[] { "sea", "glass" }, new[] { "saffron" }, new[] { "jade" }, new[] { "ember" }
        };

        /// <summary>"chula_kite_sail_sea_glass_teal_mint_paisley_rare_1024" → "skin_chula_4" (kite name + palette name in the file name).</summary>
        public static string SkinFromName(string fileName)
        {
            var words = new HashSet<string>(Regex.Split((fileName ?? "").ToLowerInvariant(), "[^a-z]+"));
            string kite = null;
            foreach (string k in Kites) if (words.Contains(k)) { if (kite != null) return null; kite = k; }
            if (kite == null) return null;
            int found = -1;
            for (int i = 0; i < Palettes.Length; i++)
            {
                bool all = true;
                foreach (string w in Palettes[i]) if (!words.Contains(w)) all = false;
                if (!all) continue;
                if (found >= 0 && !(i == 3 && found == 0)) return null; // two palettes named: unclear
                found = i;
            }
            return found >= 0 ? "skin_" + kite + "_" + found : null;
        }

        private static void Build()
        {
            if (_docs != null) return;
            _docs = new Dictionary<string, HashSet<string>>();
            _df = new Dictionary<string, int>();
            foreach (var kv in ArtSlotKeywords.ById)
            {
                var set = new HashSet<string>(Tokens(kv.Value));
                _docs[kv.Key] = set;
                foreach (string t in set) _df[t] = _df.TryGetValue(t, out int n) ? n + 1 : 1;
            }
        }

        private static float Weight(string t)
        {
            if (Low.Contains(t)) return 0.3f;
            _df.TryGetValue(t, out int df);
            return (float)Math.Log((_docs.Count + 1.0) / (df + 1.0)) + 0.1f;
        }

        /// <summary>0..1: how much of the file name's meaning is found in what the slot should show.</summary>
        public static float Score(string slotId, string fileName)
        {
            Build();
            if (!_docs.TryGetValue(slotId, out HashSet<string> doc)) return 0f;
            var words = new HashSet<string>(Tokens(fileName));
            if (words.Count == 0) return 0f;
            float total = 0f, got = 0f;
            foreach (string t in words)
            {
                float w = Weight(t);
                total += w;
                if (doc.Contains(t)) got += w;
            }
            return total > 0f ? got / total : 0f;
        }
    }
}
