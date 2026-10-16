using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Services
{
    public enum Sfx { Click, Snap, Cut, Whoosh, Gust, Spark, Win, Lose, Crowd }

    /// <summary>Layered wind, line hiss/strain, contest buzz, one-shots and music (spec 15).</summary>
    public sealed class AudioService : MonoBehaviour
    {
        private AudioSource _wind, _hiss, _buzz, _music;
        private readonly List<AudioSource> _oneShots = new List<AudioSource>();
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly Dictionary<int, AudioClip> _musicClips = new Dictionary<int, AudioClip>();
        private SettingsService _settings;
        private float _windTarget = 0.3f, _hissTarget, _buzzTarget;

        public void Init(SettingsService settings)
        {
            _settings = settings;
            _wind = Loop(ProceduralAudio.Wind());
            _hiss = Loop(ProceduralAudio.LineHiss());
            _buzz = Loop(ProceduralAudio.ContestBuzz());
            _music = Loop(null);
            _clips[Sfx.Click] = ProceduralAudio.Click();
            _clips[Sfx.Snap] = ProceduralAudio.Snap();
            _clips[Sfx.Cut] = ProceduralAudio.Cut();
            _clips[Sfx.Whoosh] = ProceduralAudio.Whoosh();
            _clips[Sfx.Gust] = ProceduralAudio.Gust();
            _clips[Sfx.Spark] = ProceduralAudio.Spark();
            _clips[Sfx.Win] = ProceduralAudio.Stinger(true);
            _clips[Sfx.Lose] = ProceduralAudio.Stinger(false);
            _clips[Sfx.Crowd] = ProceduralAudio.Crowd();
            for (int i = 0; i < 8; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _oneShots.Add(s);
            }
        }

        private AudioSource Loop(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = 0f;
            s.playOnAwake = false;
            if (clip != null) s.Play();
            return s;
        }

        private float Sfx01 => _settings != null ? _settings.Data.sfxVolume : 1f;
        private float Music01 => _settings != null ? _settings.Data.musicVolume : 0.6f;

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(sfx, out AudioClip clip)) return;
            foreach (AudioSource s in _oneShots)
            {
                if (s.isPlaying) continue;
                s.pitch = pitch;
                s.PlayOneShot(clip, volume * Sfx01);
                return;
            }
            _oneShots[0].PlayOneShot(clip, volume * Sfx01);
        }

        /// <summary>Wind loudness 0–1 (wind speed), line hiss 0–1 (tension), contest buzz 0–1.</summary>
        public void SetAmbience(float wind01, float hiss01, float buzz01)
        {
            _windTarget = Mathf.Clamp01(wind01);
            _hissTarget = Mathf.Clamp01(hiss01);
            _buzzTarget = Mathf.Clamp01(buzz01);
        }

        public void PlayMusic(int style)
        {
            if (!_musicClips.TryGetValue(style, out AudioClip clip))
            {
                clip = ProceduralAudio.Music(style, 7);
                _musicClips[style] = clip;
            }
            if (_music.clip == clip && _music.isPlaying) return;
            _music.clip = clip;
            _music.Play();
        }

        public void StopMusic() => _music.Stop();

        private void Update()
        {
            if (_wind == null) return;
            float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime);
            _wind.volume = Mathf.Lerp(_wind.volume, (0.15f + 0.6f * _windTarget) * Sfx01, k);
            _wind.pitch = 0.8f + 0.4f * _windTarget;
            _hiss.volume = Mathf.Lerp(_hiss.volume, 0.35f * _hissTarget * Sfx01, k);
            _hiss.pitch = 0.8f + 0.6f * _hissTarget;
            _buzz.volume = Mathf.Lerp(_buzz.volume, 0.5f * _buzzTarget * Sfx01, k);
            _music.volume = Mathf.Lerp(_music.volume, 0.35f * Music01, k);
        }
    }
}
