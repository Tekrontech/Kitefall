using System;

namespace Kitefall.Core
{
    /// <summary>Squad rules (upgrade F2), pure so the host, the app and tests agree.</summary>
    public static class PartyRules
    {
        public const int MaxMembers = 4;

        /// <summary>Duels need a party of 1 (upgrade 3): "Leave party or choose another mode".</summary>
        public static string CanQueue(GameMode mode, int partySize)
        {
            if (partySize <= 1) return null;
            if (mode == GameMode.QuickDuel || mode == GameMode.RankedDuel) return "Duels are 1 vs 1 – leave the party or choose another mode.";
            if (partySize > MaxMembers) return "A party has at most 4 players.";
            return null;
        }

        public static string NewCode(Random rng)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var c = new char[6];
            for (int i = 0; i < c.Length; i++) c[i] = alphabet[rng.Next(alphabet.Length)];
            return new string(c);
        }

        public static string ShareLink(string code) => "kitefall://party/" + code;

        /// <summary>Parses "kitefall://party/ABC123" or a bare code.</summary>
        public static string CodeFromLink(string link)
        {
            if (string.IsNullOrEmpty(link)) return "";
            int i = link.LastIndexOf('/');
            return (i >= 0 ? link.Substring(i + 1) : link).Trim().ToUpperInvariant();
        }
    }
}
