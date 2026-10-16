using Kitefall.Services;
using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>
    /// Registers every installed online service with the Runtime before the first scene loads.
    /// Each block only compiles when its package is installed (versionDefines in Kitefall.Online.asmdef).
    /// </summary>
    public static class OnlineBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            OnlineConfig config = OnlineConfig.Load();
            if (!config.enableOnline) return;
#if KITEFALL_UGS_AUTH && KITEFALL_UGS_CLOUDCODE
            BackendRegistry.OnlineFactory = () => new UgsBackendService(config);
            Debug.Log("[Kitefall.Online] UGS backend available.");
#endif
#if KITEFALL_UGS_REMOTECONFIG && KITEFALL_UGS_AUTH
            RemoteConfigRegistry.Factory = () => new UgsRemoteConfigProvider();
#endif
#if KITEFALL_UGS_ANALYTICS
            AnalyticsRegistry.Factory = () => new UgsAnalyticsProvider();
#endif
#if KITEFALL_LEVELPLAY
            if (!string.IsNullOrEmpty(config.LevelPlayAppKey)) AdsRegistry.Factory = () => new LevelPlayAdsProvider(config);
#endif
#if KITEFALL_IAP
            PurchaseRegistry.Factory = () => new UnityIapProvider();
#endif
#if KITEFALL_NGO && KITEFALL_UGS_MULTIPLAYER
            Kitefall.Match.OnlineHooks.Matchmaker = new SessionMatchmaker(config);
#endif
        }
    }
}
