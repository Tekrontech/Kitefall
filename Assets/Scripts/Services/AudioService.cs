using System.Collections.Generic;
using Kitefall.App;
using UnityEngine;

namespace Kitefall.Services
{
    // New values go at the end (saved / sent numbers keep their meaning).
    public enum Sfx { Click, Snap, Cut, Whoosh, Gust, Spark, Win, Lose, Crowd, CrowdOoh, Confirm }

    /// <summary>
    /// Layered wind, line hiss/strain, contest buzz, one-shots and music (spec 15).
    /// Every sound is first looked up as a real file in Resources/KitefallAudio/&lt;name&gt; (see docs/AUDIO_LIST.md);
    /// a missing file falls back to the code-built sound in <see cref="ProceduralAudio"/>, so the game always has sound.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public const string Folder = "KitefallAudio/";

        // File names (without extension) in Assets/Resources/KitefallAudio – keep in sync with docs/AUDIO_LIST.md.
        public const string WindLoop = "wind_loop", TensionLoop = "tension_loop", ContestLoop = "contest_loop", DholLoop = "dhol_loop";
        public const string MusicHome = "music_home";
        public static readonly string[] MusicMatch = { "music_match_1", "music_match_2" };

        private static readonly Dictionary<Sfx, string> SfxFiles = new Dictionary<Sfx, string>
        {
            { Sfx.Click, "ui_click" }, { Sfx.Confirm, "ui_confirm" }, { Sfx.Snap, "snap" }, { Sfx.Cut, "cut" },
            { Sfx.Crowd, "crowd_cheer" }, { Sfx.CrowdOoh, "crowd_ooh" },
            // Optional extras (not in the main list, still picked up when a file with this name is added).
            { Sfx.Whoosh, "whoosh" }, { Sfx.Gust, "gust" }, { Sfx.Spark, "spark" }, { Sfx.Win, "win" }, { Sfx.Lose, "lose" },
        };

        private AudioSource _wind, _hiss, _buzz, _dhol, _music;
        private readonly List<AudioSource> _oneShots = new List<AudioSource>();
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private readonly Dictionary<int, AudioClip> _musicClips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<string, AudioClip> _fileMusic = new Dictionary<string, AudioClip>();
        private readonly HashSet<Sfx> _fromFile = new HashSet<Sfx>();
        private SettingsService _settings;
        private float _windTarget = 0.3f, _hissTarget, _buzzTarget;
        private bool _dholOn;
        private float _lastOoh = -10f;

        // Music: recorded tracks are mastered loud, the code-built ones are quiet – each gets its own level.
        private float _musicGain = 0.35f;
        private AudioClip _pendingMusic;
        private float _pendingGain;

        public void Init(SettingsService settings)
        {
            _settings = settings;
            int files = 0;
            _wind = Loop(Load(WindLoop, ProceduralAudio.Wind, ref files));
            _hiss = Loop(Load(TensionLoop, ProceduralAudio.LineHiss, ref files));
            _buzz = Loop(Load(ContestLoop, ProceduralAudio.ContestBuzz, ref files));
            _dhol = Loop(Load(DholLoop, null, ref files)); // no code-built drum loop: silent until a file is added
            _music = Loop(null);
            Sound(Sfx.Click, ProceduralAudio.Click, ref files);
            Sound(Sfx.Confirm, ProceduralAudio.Confirm, ref files);
            Sound(Sfx.Snap, ProceduralAudio.Snap, ref files);
            Sound(Sfx.Cut, ProceduralAudio.Cut, ref files);
            Sound(Sfx.Whoosh, ProceduralAudio.Whoosh, ref files);
            Sound(Sfx.Gust, ProceduralAudio.Gust, ref files);
            Sound(Sfx.Spark, ProceduralAudio.Spark, ref files);
            Sound(Sfx.Win, () => ProceduralAudio.Stinger(true), ref files);
            Sound(Sfx.Lose, () => ProceduralAudio.Stinger(false), ref files);
            Sound(Sfx.Crowd, ProceduralAudio.Crowd, ref files);
            Sound(Sfx.CrowdOoh, ProceduralAudio.CrowdOoh, ref files);
            for (int i = 0; i < 8; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _oneShots.Add(s);
            }
            if (files > 0) Debug.Log($"[Kitefall] Audio: {files} sound file(s) from Resources/{Folder}, the rest are built in code.");
        }

        /// <summary>A recorded clip from Resources/KitefallAudio, or the code-built fallback (may be null).</summary>
        private static AudioClip Load(string name, System.Func<AudioClip> fallback, ref int files)
        {
            AudioClip clip = Resources.Load<AudioClip>(Folder + name);
            if (clip != null) { files++; return clip; }
            return fallback?.Invoke();
        }

        private void Sound(Sfx sfx, System.Func<AudioClip> fallback, ref int files)
        {
            int before = files;
            AudioClip clip = SfxFiles.TryGetValue(sfx, out string file) ? Load(file, fallback, ref files) : fallback();
            if (files > before) _fromFile.Add(sfx);
            if (clip != null) _clips[sfx] = clip;
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
            // Lines start crossing: the watching crowd goes "ooh" (recorded file only – the code-built crowd is kept for events).
            float buzz = Mathf.Clamp01(buzz01);
            if (_buzzTarget < 0.15f && buzz > 0.5f && _fromFile.Contains(Sfx.CrowdOoh) && Time.unscaledTime - _lastOoh > 8f)
            {
                _lastOoh = Time.unscaledTime;
                Play(Sfx.CrowdOoh, 0.55f);
            }
            _windTarget = Mathf.Clamp01(wind01);
            _hissTarget = Mathf.Clamp01(hiss01);
            _buzzTarget = buzz;
        }

        /// <summary>
        /// Kept for existing callers: in a match this plays a match track (arena style), otherwise the home / menu music.
        /// </summary>
        public void PlayMusic(int style)
        {
            if (Game.Matches != null && Game.Matches.InMatch) PlayMatchMusic(style);
            else PlayHomeMusic();
        }

        /// <summary>Menus: music_home, else the code-built old-city loop.</summary>
        public void PlayHomeMusic()
        {
            _dholOn = false;
            AudioClip file = FileMusic(MusicHome);
            if (file != null) SwitchMusic(file, 0.7f);
            else SwitchMusic(CodeMusic(0), 0.35f);
        }

        /// <summary>Matches: a random music_match_N track (+ the dhol layer), else the arena's code-built loop.</summary>
        public void PlayMatchMusic(int style)
        {
            var tracks = new List<AudioClip>();
            foreach (string n in MusicMatch)
            {
                AudioClip c = FileMusic(n);
                if (c != null) tracks.Add(c);
            }
            _dholOn = _dhol != null && _dhol.clip != null;
            if (_dholOn && !_dhol.isPlaying) _dhol.Play();
            if (tracks.Count > 0) SwitchMusic(tracks[Random.Range(0, tracks.Count)], 0.6f);
            else SwitchMusic(CodeMusic(style), 0.35f);
        }

        public void StopMusic()
        {
            _pendingMusic = null;
            _dholOn = false;
            _music.Stop();
        }

        private AudioClip FileMusic(string name)
        {
            if (!_fileMusic.TryGetValue(name, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(Folder + name); // loaded on first use – music is the biggest audio
                _fileMusic[name] = clip;
            }
            return clip;
        }

        private AudioClip CodeMusic(int style)
        {
            if (!_musicClips.TryGetValue(style, out AudioClip clip))
            {
                clip = ProceduralAudio.Music(style, 7);
                _musicClips[style] = clip;
            }
            return clip;
        }

        /// <summary>Starts a track; if another one is playing it fades out first (short, ~0.5 s).</summary>
        private void SwitchMusic(AudioClip clip, float gain)
        {
            if (clip == null) return;
            if (_music.clip == clip && _music.isPlaying) { _pendingMusic = null; _musicGain = gain; return; }
            if (_music.isPlaying && _music.volume > 0.02f)
            {
                _pendingMusic = clip;
                _pendingGain = gain;
                return;
            }
            StartMusic(clip, gain);
        }

        private void StartMusic(AudioClip clip, float gain)
        {
            _pendingMusic = null;
            _musicGain = gain;
            _music.clip = clip;
            _music.volume = 0f;
            _music.Play();
        }

        private void Update()
        {
            if (_wind == null) return;
            float dt = Time.unscaledDeltaTime;
            float k = 1f - Mathf.Exp(-4f * dt);
            _wind.volume = Mathf.Lerp(_wind.volume, (0.15f + 0.6f * _windTarget) * Sfx01, k);
            _wind.pitch = 0.8f + 0.4f * _windTarget;
            _hiss.volume = Mathf.Lerp(_hiss.volume, 0.35f * _hissTarget * Sfx01, k);
            _hiss.pitch = 0.8f + 0.6f * _hissTarget;
            _buzz.volume = Mathf.Lerp(_buzz.volume, 0.5f * _buzzTarget * Sfx01, k);
            // Festival drum under the match music, a little louder while lines are crossing.
            _dhol.volume = Mathf.Lerp(_dhol.volume, _dholOn ? (0.18f + 0.17f * _buzzTarget) * Music01 : 0f, k);
            if (!_dholOn && _dhol.isPlaying && _dhol.volume < 0.005f) _dhol.Stop();
            if (_pendingMusic != null)
            {
                _music.volume = Mathf.MoveTowards(_music.volume, 0f, dt * 1.6f);
                if (_music.volume <= 0.001f) StartMusic(_pendingMusic, _pendingGain);
            }
            else _music.volume = Mathf.Lerp(_music.volume, _musicGain * Music01, k);
        }
    }
}
