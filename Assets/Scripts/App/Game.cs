using Kitefall.Core;
using Kitefall.Data;
using Kitefall.Match;
using Kitefall.Services;
using Kitefall.UI;

namespace Kitefall.App
{
    /// <summary>Global access to the running services (set up once by GameApp).</summary>
    public static class Game
    {
        public static GameApp App;
        public static GameDatabase Db;
        public static SettingsService Settings;
        public static IBackendService Backend;
        public static AudioService Audio;
        public static HapticsService Haptics;
        public static AdsService Ads;
        public static PurchaseService Purchases;
        public static AnalyticsService Analytics;
        public static RemoteConfigService Remote;
        public static ShareService Share;
        public static UIManager UI;
        public static AppFlow Flow;
        public static MatchDirector Matches;
        /// <summary>Squad / party (upgrade F2). Local (offline) by default; Kitefall.Online replaces it.</summary>
        public static IPartyService Party;

        public static PlayerProfile Profile => Backend?.Profile;
        public static BalanceConfig Balance => Db != null ? Db.balance : null;
        public static EconomyConfig Economy => Balance != null ? Balance.economy : new EconomyConfig();
        public static RankSettings Ranks => Balance != null ? Balance.ranks : new RankSettings();
        public static long Now => Backend != null ? Backend.NowUtc : System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static bool ColorBlind => Settings != null && Settings.Data.colorBlind;
    }
}
