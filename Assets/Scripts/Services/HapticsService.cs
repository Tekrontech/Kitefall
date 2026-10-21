using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>Light pulse in the sweet zone, strong jolt on a cut (spec 15). Android uses the vibrator directly for short pulses.</summary>
    public sealed class HapticsService
    {
        private readonly SettingsService _settings;
        private float _lastPulse;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private int _sdk;
        private bool _warned;
#endif

        public HapticsService(SettingsService settings)
        {
            _settings = settings;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
            }
            catch (System.Exception e) { Debug.LogWarning("[Haptics] " + e.Message); }
#endif
        }

        private bool Enabled => _settings == null || _settings.Data.haptics;

        public void Light() => Vibrate(12, 60);
        public void Strong() => Vibrate(60, 255);

        /// <summary>Rate-limited pulse used while tension is inside the sweet zone.</summary>
        public void SweetPulse()
        {
            if (Time.unscaledTime - _lastPulse < 0.35f) return;
            _lastPulse = Time.unscaledTime;
            Vibrate(8, 40);
        }

        private void Vibrate(long ms, int amplitude)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null) return;
            try
            {
                if (_sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", effect);
                }
                else _vibrator.Call("vibrate", ms);
            }
            catch (System.Exception e) { if (!_warned) { _warned = true; Debug.LogWarning("[Haptics] Vibration failed, haptics off for this session: " + e.Message); } }
#elif UNITY_IOS && !UNITY_EDITOR
            if (ms >= 40) Handheld.Vibrate();
#endif
        }
    }
}
