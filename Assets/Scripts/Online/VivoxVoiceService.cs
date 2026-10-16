#if KITEFALL_VIVOX && KITEFALL_UGS_AUTH
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Services;
using Unity.Services.Authentication;
using Unity.Services.Vivox;
using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>
    /// Voice chat with Unity Vivox (upgrade F7): a party channel (lobby + match) and a team channel (Team Battle only).
    /// No voice with strangers in FFA / LKF / Duel. Push-to-talk by default (open mic is a setting), speaker toggle,
    /// per-player mute, "mute all strangers" on. The microphone permission is asked only when the player first taps the mic.
    /// Players under 13 never get voice. Vivox login uses the Unity Authentication player id.
    /// NOTE: compile-checked against stubs only – check Vivox pricing / free tier and test on 2 phones before launch.
    /// </summary>
    public sealed class VivoxVoiceService : MonoBehaviour
    {
        private static VivoxVoiceService _instance;
        private bool _loggedIn, _loggingIn;
        private string _partyChannel, _teamChannel;
        private readonly HashSet<string> _joined = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            OnlineConfig config = OnlineConfig.Load();
            if (!config.enableOnline) return;
            var go = new GameObject("KitefallVoice");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<VivoxVoiceService>();
            VoiceRegistry.Available = () => _instance != null && _instance.CanTalk;
            VoiceRegistry.SetTransmit = on => _instance?.Transmit(on);
            VoiceRegistry.JoinChannel = name => _instance?.SetPartyChannel(name);
            VoiceRegistry.LeaveAll = () => _ = _instance?.LeaveAllAsync();
            VoiceRegistry.SetSpeaker = on => { if (on) VivoxService.Instance.UnmuteOutputDevice(); else VivoxService.Instance.MuteOutputDevice(); };
            VoiceRegistry.MutePlayer = (playerId, mute) => _instance?.Mute(playerId, mute);
        }

        private static bool Under13 => Game.Profile != null && Game.Profile.under13;

        private bool CanTalk => _loggedIn && !Under13 && _joined.Count > 0;

        private async Task EnsureLoginAsync()
        {
            if (_loggedIn || _loggingIn || Under13 || !AuthenticationService.Instance.IsSignedIn) return;
            _loggingIn = true;
            try
            {
                await VivoxService.Instance.InitializeAsync();
                await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = Game.Profile?.displayName ?? "Player" });
                VivoxService.Instance.MuteInputDevice(); // push-to-talk starts muted
                _loggedIn = true;
            }
            catch (Exception e) { Debug.LogWarning("[Voice] Login failed: " + e.Message); }
            finally { _loggingIn = false; }
        }

        private async void SetPartyChannel(string partyId)
        {
            if (Under13) return;
            await EnsureLoginAsync();
            if (!_loggedIn) return;
            if (!string.IsNullOrEmpty(_partyChannel)) await LeaveAsync(_partyChannel);
            _partyChannel = string.IsNullOrEmpty(partyId) ? null : "kf_" + partyId;
            if (_partyChannel != null) await JoinAsync(_partyChannel);
        }

        /// <summary>Team channel only in Team Battle; joined on match start, left at the end (polled – no extra hooks).</summary>
        private async void Update()
        {
            if (Under13) return;
            var session = Game.Matches != null ? Game.Matches.Session : null;
            string want = null;
            if (session != null && session.IsOnline && session.Setup.Rules.mode == GameMode.TeamBattle && session.LocalIndex >= 0)
                want = $"kf_team_{session.Setup.Seed}_{session.Setup.Participants[session.LocalIndex].team}";
            if (want == _teamChannel) return;
            string old = _teamChannel;
            _teamChannel = want;
            await EnsureLoginAsync();
            if (!_loggedIn) return;
            if (!string.IsNullOrEmpty(old)) await LeaveAsync(old);
            if (!string.IsNullOrEmpty(want)) await JoinAsync(want);
        }

        private async Task JoinAsync(string channel)
        {
            try
            {
                await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                _joined.Add(channel);
            }
            catch (Exception e) { Debug.LogWarning("[Voice] Join failed: " + e.Message); }
        }

        private async Task LeaveAsync(string channel)
        {
            try { await VivoxService.Instance.LeaveChannelAsync(channel); }
            catch (Exception e) { Debug.LogWarning("[Voice] Leave failed: " + e.Message); }
            _joined.Remove(channel);
        }

        private async Task LeaveAllAsync()
        {
            foreach (string c in new List<string>(_joined)) await LeaveAsync(c);
        }

        private void Transmit(bool on)
        {
            if (!CanTalk) return;
            if (on && !MicPermission()) return;
            if (on) VivoxService.Instance.UnmuteInputDevice();
            else VivoxService.Instance.MuteInputDevice();
        }

        /// <summary>Asks for the microphone only when the player first taps the mic (spec F7).</summary>
        private static bool MicPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                return false;
            }
#endif
            return true;
        }

        private void Mute(string playerId, bool mute)
        {
            foreach (var channel in VivoxService.Instance.ActiveChannels)
                foreach (VivoxParticipant p in channel.Value)
                    if (p.PlayerId == playerId) { if (mute) p.MutePlayerLocally(); else p.UnmutePlayerLocally(); }
        }
    }
}
#endif
