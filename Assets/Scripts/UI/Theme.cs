using UnityEngine;

namespace Kitefall.UI
{
    /// <summary>
    /// Colours and sizes used by every screen. Change here to restyle the whole game.
    /// "Sky Ops" look (v1.5): dark slate glass with cut corners, amber call-to-action, cyan selection, condensed display type.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("#070B14");
        public static readonly Color Panel = new Color(0.07f, 0.10f, 0.16f, 0.92f);
        public static readonly Color PanelLight = new Color(0.17f, 0.22f, 0.33f, 0.95f);
        public static readonly Color Card = new Color(0.10f, 0.14f, 0.22f, 0.95f);
        public static readonly Color CardSelected = Hex("#1490C0");
        /// <summary>Call-to-action amber (PLAY, Buy, Claim chest…).</summary>
        public static readonly Color Accent = Hex("#FFA51F");
        /// <summary>Selection cyan (selected tab, chosen mode…).</summary>
        public static readonly Color Accent2 = Hex("#22C3EE");
        public static readonly Color Good = Hex("#2FD27F");
        public static readonly Color Bad = Hex("#FF3B52");
        public static readonly Color Warn = Hex("#FFC21A");
        public static readonly Color Text = Hex("#F4F7FB");
        public static readonly Color TextDim = Hex("#A9B8CF");
        /// <summary>Dark text on amber / green buttons.</summary>
        public static readonly Color TextOnAccent = Hex("#2A1300");
        public static readonly Color TextOnGood = Hex("#04220F");
        public static readonly Color Coin = Hex("#FFD24D");
        public static readonly Color Gem = Hex("#B565FF");
        public static readonly Color GemBlue = Hex("#3FB6FF");
        public static readonly Color Disabled = new Color(0.17f, 0.20f, 0.26f, 0.95f);
        public static readonly Color Overlay = new Color(0.01f, 0.02f, 0.05f, 0.72f);

        /// <summary>Common, Rare, Epic, Legendary (+ Mythic for event items).</summary>
        public static readonly Color[] RarityColors = { Hex("#7C8799"), Hex("#2F86F0"), Hex("#9A4DF5"), Hex("#FFA515"), Hex("#FF3B52") };

        public const int TitleSize = 54;
        public const int HeaderSize = 38;
        public const int BodySize = 28;
        public const int SmallSize = 22;

        /// <summary>Tension zone colours; colour-blind mode uses blue / orange / magenta.</summary>
        public static Color ZoneSafe(bool colorBlind) => colorBlind ? Hex("#3B82F6") : Hex("#4CC66D");
        public static Color ZoneStrong(bool colorBlind) => colorBlind ? Hex("#F59E0B") : Hex("#FAD02C");
        public static Color ZoneDanger(bool colorBlind) => colorBlind ? Hex("#D946EF") : Hex("#EB4034");
        public static Color Sweet(bool colorBlind) => colorBlind ? Color.white : Hex("#7CF7FF");

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
        }
    }
}
