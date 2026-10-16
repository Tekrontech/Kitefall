using System;
using System.Text;

namespace Kitefall.Core
{
    /// <summary>Display name rules (spec 3.1: 3–16 chars, profanity filter).</summary>
    public static class NameRules
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;

        // Small starter list – the server can extend it via Remote Config. Matching ignores case, spaces and leetspeak.
        private static readonly string[] Blocked =
        {
            "fuck", "shit", "bitch", "cunt", "dick", "pussy", "nigg", "fag", "rape", "nazi", "hitler",
            "kutta", "kamina", "harami", "chutiya", "madarchod", "behenchod", "bhosdi", "gaand", "randi", "lund",
            "admin", "moderator", "kitefallstaff"
        };

        public static string Validate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Enter a name.";
            name = name.Trim();
            if (name.Length < MinLength || name.Length > MaxLength) return $"Name must be {MinLength}–{MaxLength} characters.";
            foreach (char c in name)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == ' ' || c == '-' || c == '.')) return "Only letters, numbers, space, _ - . are allowed.";
            string norm = Normalize(name);
            foreach (string bad in Blocked)
                if (norm.Contains(bad)) return "That name is not allowed.";
            return null;
        }

        private static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.ToLowerInvariant())
            {
                char c = ch;
                switch (c)
                {
                    case '0': c = 'o'; break;
                    case '1': c = 'i'; break;
                    case '3': c = 'e'; break;
                    case '4': c = 'a'; break;
                    case '5': c = 's'; break;
                    case '7': c = 't'; break;
                    case '@': c = 'a'; break;
                    case '$': c = 's'; break;
                }
                if (char.IsLetter(c)) sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
