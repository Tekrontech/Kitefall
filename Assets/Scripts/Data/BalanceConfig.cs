using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Data
{
    /// <summary>
    /// Every remaining Core tunable in one asset (contest extras, contact, sweet zone, phases, crates,
    /// economy, ads, ranks, matchmaking, tail rope, line). Remote Config can overwrite it with JSON (spec 7.6, 13.4).
    /// </summary>
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Kitefall/Config/Balance Config", order = 24)]
    public sealed class BalanceConfig : ScriptableObject
    {
        public ContestSettings contest = new ContestSettings();
        public ContactSettings contact = new ContactSettings();
        public SweetZoneSettings sweetZone = new SweetZoneSettings();
        public PhaseSettings phases = new PhaseSettings();
        public CrateSettings crates = new CrateSettings();
        public LineSettings line = new LineSettings();
        public TailRopeSettings tail = new TailRopeSettings();
        public EconomyConfig economy = new EconomyConfig();
        public AdsConfig ads = new AdsConfig();
        public RankSettings ranks = new RankSettings();
        public MatchmakingSettings matchmaking = new MatchmakingSettings();

        [Header("App")]
        [Tooltip("Builds older than this must update (force-update screen, spec 3.1).")]
        public string minimumVersion = "0.1.0";
        public string termsUrl = "https://example.com/kitefall/terms";
        public string privacyUrl = "https://example.com/kitefall/privacy";
        public string storeUrl = "https://play.google.com/store/apps/details?id=com.kitefall.game";

        public EconomyBundle Bundle() => new EconomyBundle { economy = economy, ads = ads, ranks = ranks };
    }
}
