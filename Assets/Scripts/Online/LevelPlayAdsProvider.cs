#if KITEFALL_LEVELPLAY
using System;
using Kitefall.Services;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>
    /// Unity LevelPlay mediation (spec 13.4). Policy (when ads may show, daily caps) stays in Core.AdsPolicy;
    /// this class only loads and shows. Keys come from OnlineConfig – never hard-code them.
    /// </summary>
    public sealed class LevelPlayAdsProvider : IAdsProvider
    {
        private readonly OnlineConfig _config;
        private LevelPlayRewardedAd _rewarded;
        private LevelPlayInterstitialAd _interstitial;
        private Action<bool> _rewardDone;
        private Action _interstitialDone;
        private bool _earned;

        public LevelPlayAdsProvider(OnlineConfig config) => _config = config;

        public string Name => "LevelPlay";
        public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady();
        public bool IsInterstitialReady => _interstitial != null && _interstitial.IsAdReady();

        public void Initialize(bool personalized, bool childDirected)
        {
            // Privacy must be set before Init (GDPR consent, COPPA for under-13).
            LevelPlayPrivacySettings.SetGDPRConsent(personalized);
            LevelPlayPrivacySettings.SetCOPPA(childDirected);
            if (childDirected) LevelPlay.SetMetaData("is_child_directed", "true");
            LevelPlay.OnInitSuccess += OnInit;
            LevelPlay.OnInitFailed += e => Debug.LogWarning("[LevelPlay] Init failed: " + e);
            LevelPlay.Init(_config.LevelPlayAppKey);
        }

        private void OnInit(LevelPlayConfiguration configuration)
        {
            if (!string.IsNullOrEmpty(_config.RewardedAdUnit))
            {
                _rewarded = new LevelPlayRewardedAd(_config.RewardedAdUnit);
                _rewarded.OnAdRewarded += (info, reward) => _earned = true;
                _rewarded.OnAdClosed += info => FinishRewarded();
                _rewarded.OnAdDisplayFailed += (info, error) => FinishRewarded();
                _rewarded.OnAdLoadFailed += error => Debug.LogWarning("[LevelPlay] Rewarded load failed: " + error);
                _rewarded.LoadAd();
            }
            if (!string.IsNullOrEmpty(_config.InterstitialAdUnit))
            {
                _interstitial = new LevelPlayInterstitialAd(_config.InterstitialAdUnit);
                _interstitial.OnAdClosed += info => FinishInterstitial();
                _interstitial.OnAdDisplayFailed += (info, error) => FinishInterstitial();
                _interstitial.LoadAd();
            }
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (!IsRewardedReady) { done?.Invoke(false); return; }
            _earned = false;
            _rewardDone = done;
            _rewarded.ShowAd(placement);
        }

        public void ShowInterstitial(Action done)
        {
            if (!IsInterstitialReady) { done?.Invoke(); return; }
            _interstitialDone = done;
            _interstitial.ShowAd();
        }

        private void FinishRewarded()
        {
            Action<bool> cb = _rewardDone;
            _rewardDone = null;
            cb?.Invoke(_earned);
            _rewarded.LoadAd();
        }

        private void FinishInterstitial()
        {
            Action cb = _interstitialDone;
            _interstitialDone = null;
            cb?.Invoke();
            _interstitial.LoadAd();
        }
    }
}
#endif
