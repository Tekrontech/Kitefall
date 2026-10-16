using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>
    /// Keys for third-party services. Put a copy in a Resources folder named "KitefallOnlineConfig"
    /// (Kitefall ▸ Online ▸ Create Online Config). Never commit real keys to a public repo (spec 17.8).
    /// </summary>
    [CreateAssetMenu(fileName = "KitefallOnlineConfig", menuName = "Kitefall/Config/Online Config", order = 40)]
    public sealed class OnlineConfig : ScriptableObject
    {
        [Header("Unity Gaming Services")]
        [Tooltip("UGS environment name (e.g. production, development).")]
        public string environment = "development";
        public string cloudCodeModule = "KitefallEconomy";
        public string rankedLeaderboardId = "ranked";
        public string rankedQueue = "ranked-duel";

        [Header("LevelPlay (ads)")]
        public string levelPlayAppKeyAndroid = "";
        public string levelPlayAppKeyIos = "";
        public string rewardedAdUnitAndroid = "";
        public string interstitialAdUnitAndroid = "";
        public string rewardedAdUnitIos = "";
        public string interstitialAdUnitIos = "";

        [Header("Online play")]
        [Tooltip("Use the online backend when the UGS packages are installed.")]
        public bool enableOnline = true;

        public static OnlineConfig Load()
        {
            var c = Resources.Load<OnlineConfig>("KitefallOnlineConfig");
            return c != null ? c : CreateInstance<OnlineConfig>();
        }

        public string LevelPlayAppKey => Application.platform == RuntimePlatform.IPhonePlayer ? levelPlayAppKeyIos : levelPlayAppKeyAndroid;
        public string RewardedAdUnit => Application.platform == RuntimePlatform.IPhonePlayer ? rewardedAdUnitIos : rewardedAdUnitAndroid;
        public string InterstitialAdUnit => Application.platform == RuntimePlatform.IPhonePlayer ? interstitialAdUnitIos : interstitialAdUnitAndroid;
    }
}
