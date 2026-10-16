using System;
using System.Threading.Tasks;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>An ad network behind one small interface (LevelPlay / AdMob live in Kitefall.Online).</summary>
    public interface IAdsProvider
    {
        string Name { get; }
        void Initialize(bool personalized, bool childDirected);
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        /// <summary>Shows a rewarded ad; the callback gets true only if the reward was earned.</summary>
        void ShowRewarded(string placement, Action<bool> done);
        void ShowInterstitial(Action done);
    }

    /// <summary>Editor / offline stand-in: a 3-second fake ad overlay. Never used in release builds with an ad network.</summary>
    public sealed class DebugAdsProvider : IAdsProvider
    {
        public static Action<string, float, Action<bool>> ShowOverlay;
        public string Name => "Debug ads (no network)";
        public bool IsRewardedReady => true;
        public bool IsInterstitialReady => true;
        public void Initialize(bool personalized, bool childDirected) => Debug.Log($"[Ads] Debug provider (personalised={personalized}, child={childDirected})");

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (ShowOverlay != null) ShowOverlay("Rewarded ad (test) – " + placement, 3f, done);
            else done?.Invoke(true);
        }

        public void ShowInterstitial(Action done)
        {
            if (ShowOverlay != null) ShowOverlay("Interstitial ad (test)", 2f, _ => done?.Invoke());
            else done?.Invoke();
        }
    }

    public static class AdsRegistry
    {
        public static Func<IAdsProvider> Factory;
    }

    /// <summary>
    /// All ad decisions go through Core.AdsPolicy first (spec 13): never during matches,
    /// rewarded caps per day, interstitial pacing. The server re-checks caps when granting rewards.
    /// </summary>
    public sealed class AdsService
    {
        private readonly IBackendService _backend;
        private readonly AdsConfig _config;
        public IAdsProvider Provider { get; private set; }

        public AdsService(IBackendService backend, AdsConfig config)
        {
            _backend = backend;
            _config = config ?? new AdsConfig();
        }

        public void Initialize()
        {
            Provider = AdsRegistry.Factory?.Invoke() ?? new DebugAdsProvider();
            PlayerProfile p = _backend.Profile;
            Provider.Initialize(p != null && p.personalizedAdsConsent && !p.under13, p != null && p.under13);
        }

        public bool CanShowRewarded(RewardedPlacement placement, AdContext context)
        {
            PlayerProfile p = _backend.Profile;
            return p != null && Provider != null && Provider.IsRewardedReady && AdsPolicy.CanShowRewarded(p, placement, context, _config, _backend.NowUtc);
        }

        public int RemainingToday(RewardedPlacement placement)
        {
            PlayerProfile p = _backend.Profile;
            if (p == null) return 0;
            EconomyService.RollDaily(p, _backend.NowUtc);
            return Mathf.Max(0, _config.CapFor(placement) - p.DailyCount(AdsPolicy.KeyFor(placement)));
        }

        /// <summary>Shows a rewarded ad, then asks the server to grant the reward.</summary>
        public Task<OpResult> ShowRewardedAsync(RewardedPlacement placement, AdContext context, string itemId = "")
        {
            var tcs = new TaskCompletionSource<OpResult>();
            if (!CanShowRewarded(placement, context))
            {
                tcs.SetResult(OpResult.Fail("No ad available right now."));
                return tcs.Task;
            }
            Provider.ShowRewarded(placement.ToString(), async earned =>
            {
                if (!earned) { tcs.TrySetResult(OpResult.Fail("Ad closed before the reward.")); return; }
                OpResult r = await _backend.RunAsync(EconomyCommands.Rewarded, placement.ToString(), itemId ?? "");
                tcs.TrySetResult(r);
            });
            return tcs.Task;
        }

        /// <summary>Call when the player taps Next / Home on the results screen.</summary>
        public Task MaybeShowInterstitialAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            PlayerProfile p = _backend.Profile;
            if (p == null || Provider == null || !Provider.IsInterstitialReady || !AdsPolicy.CanShowInterstitial(p, _config, _backend.NowUtc, true))
            {
                tcs.SetResult(false);
                return tcs.Task;
            }
            Provider.ShowInterstitial(async () =>
            {
                await _backend.RunAsync(EconomyCommands.Interstitial);
                tcs.TrySetResult(true);
            });
            return tcs.Task;
        }
    }
}
