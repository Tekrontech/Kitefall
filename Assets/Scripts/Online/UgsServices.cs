using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.Services;
using UnityEngine;

namespace Kitefall.Online
{
#if KITEFALL_UGS_REMOTECONFIG && KITEFALL_UGS_AUTH
    /// <summary>
    /// Remote Config: key "balance" (JSON) overrides BalanceConfig – deploy Assets/RemoteConfig/kitefall.rc.
    /// Per-country ad caps can be done with Game Overrides on the same key (spec 13.4).
    /// </summary>
    public sealed class UgsRemoteConfigProvider : IRemoteConfigProvider
    {
        private struct UserAttributes { }
        private struct AppAttributes { }

        public async Task<string> FetchBalanceJsonAsync()
        {
            if (Unity.Services.Core.UnityServices.State != Unity.Services.Core.ServicesInitializationState.Initialized)
                await Unity.Services.Core.UnityServices.InitializeAsync();
            if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn) return null;
            var result = await Unity.Services.RemoteConfig.RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
            string json = result.GetJson("balance", "");
            return string.IsNullOrEmpty(json) || json == "{}" ? null : json;
        }
    }
#endif

#if KITEFALL_UGS_ANALYTICS
    /// <summary>Unity Analytics 6: data collection starts only with consent (under-13 / declined = off).</summary>
    public sealed class UgsAnalyticsProvider : IAnalyticsProvider
    {
        private bool _on;

        public void Initialize(bool consent)
        {
            if (!consent) return;
            Unity.Services.Analytics.AnalyticsService.Instance.StartDataCollection();
            _on = true;
        }

        public void Track(string eventName, Dictionary<string, object> parameters)
        {
            if (!_on) return;
            var e = new Unity.Services.Analytics.CustomEvent(eventName);
            foreach (var kv in parameters) e[kv.Key] = kv.Value;
            Unity.Services.Analytics.AnalyticsService.Instance.RecordEvent(e);
        }
    }
#endif
}
