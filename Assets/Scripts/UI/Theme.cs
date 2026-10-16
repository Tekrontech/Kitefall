using UnityEngine;

namespace Kitefall.UI
{
    /// <summary>Colours and sizes used by every screen. Change here to restyle the whole game.</summary>
    public static class Theme
    {
        public static readonly Color Background = Hex("#0E1A2B");
        public static readonly Color Panel = new Color(0.07f, 0.10f, 0.17f, 0.90f);
        public static readonly Color PanelLight = new Color(0.14f, 0.19f, 0.30f, 0.95f);
        public static readonly Color Card = new Color(0.11f, 0.16f, 0.26f, 0.96f);
        public static readonly Color CardSelected = Hex("#2D4C7A");
        public static readonly Color Accent = Hex("#FF8C1A");
        public static readonly Color Accent2 = Hex("#21A6A6");
        public static readonly Color Good = Hex("#3BB273");
        public static readonly Color Bad = Hex("#E5484D");
        public static readonly Color Warn = Hex("#F5C400");
        public static readonly Color Text = Color.white;
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.65f);
        public static readonly Color Coin = Hex("#FFD24D");
        public static readonly Color Gem = Hex("#C77DFF");
        public static readonly Color Disabled = new Color(0.35f, 0.38f, 0.45f, 0.9f);
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.6f);

        public static readonly Color[] RarityColors = { Hex("#9AA5B1"), Hex("#3B82F6"), Hex("#A855F7"), Hex("#F59E0B") };

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
