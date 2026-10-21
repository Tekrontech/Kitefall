#if KITEFALL_NGO && KITEFALL_UGS_MULTIPLAYER && KITEFALL_UGS_AUTH
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>
    /// Squad / party (upgrade F2) on Unity Multiplayer Services: a private session with lobby data only (no Relay, no Netcode).
    /// Max 4, leader = creator. Member look + ready flag are player properties; mode and the match code are session properties.
    /// PLAY: the leader opens a private match session sized for the mode and writes its code into the party; members join it
    /// automatically, the host starts as soon as the whole party is connected and fills the rest with labelled bots.
    /// The party session stays open, so the squad returns to the lobby together after the match.
    /// NOTE: compile-checked against stubs only – verify in the 2-phone smoke test (milestone 15).
    /// </summary>
    public sealed class OnlinePartyService : IPartyService
    {
        private const string PropMode = "kf_mode";
        private const string PropMatch = "kf_match";
        private const string PropLook = "kf_look";
        private const string PropReady = "kf_ready";

        private ISession _party;
        private readonly List<PartyMember> _members = new List<PartyMember>();
        private string _lastMatchCode = "";

        public bool InParty => _party != null;
        public bool IsLeader => _party != null && _party.IsHost;
        public string PartyId => _party != null ? "party_" + _party.Id : "";
        public string Code => _party != null ? _party.Code : "";
        public GameMode Mode { get; private set; } = GameMode.TeamBattle;
        public IReadOnlyList<PartyMember> Members => _members;
        public bool Online => true;
        public event Action Changed;

        public async Task<OpResult> CreateAsync()
        {
            try
            {
                var options = new SessionOptions
                {
                    MaxPlayers = PartyRules.MaxMembers,
                    IsPrivate = true,
                    PlayerProperties = LocalProperties(true),
                    SessionProperties = new Dictionary<string, SessionProperty> { { PropMode, new SessionProperty(Mode.ToString(), VisibilityPropertyOptions.Member) } }
                };
                Attach(await MultiplayerService.Instance.CreateSessionAsync(options));
                return OpResult.Success().Log("Party created – share code " + Code);
            }
            catch (Exception e) { return OpResult.Fail("Could not create the party: " + e.Message); }
        }

        public async Task<OpResult> JoinAsync(string code)
        {
            if (string.IsNullOrEmpty(code)) return OpResult.Fail("Enter the 6-letter party code.");
            try
            {
                Attach(await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions { PlayerProperties = LocalProperties(false) }));
                return OpResult.Success().Log("Joined the party");
            }
            catch (Exception e) { return OpResult.Fail("Could not join party " + code + ": " + e.Message); }
        }

        public async Task LeaveAsync()
        {
            ISession s = _party;
            Detach();
            if (s != null) { try { await s.LeaveAsync(); } catch (Exception e) { Debug.LogWarning("[Party] " + e.Message); } }
            VoiceRegistry.JoinChannel?.Invoke(null);
            Changed?.Invoke();
        }

        public async Task SetReadyAsync(bool ready)
        {
            if (_party == null) return;
            _party.CurrentPlayer.SetProperty(PropReady, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member));
            await _party.SaveCurrentPlayerDataAsync();
        }

        public async Task<OpResult> SetModeAsync(GameMode mode)
        {
            if (!IsLeader) return OpResult.Fail("Only the party leader picks the mode.");
            Mode = mode;
            IHostSession host = _party.AsHost();
            host.SetProperty(PropMode, new SessionProperty(mode.ToString(), VisibilityPropertyOptions.Member));
            await host.SavePropertiesAsync();
            Changed?.Invoke();
            return OpResult.Success();
        }

        public async Task<OpResult> PlayAsync()
        {
            if (!IsLeader) return OpResult.Fail("Only the party leader can start.");
            string err = PartyRules.CanQueue(Mode, _members.Count);
            if (err != null) return OpResult.Fail(err);
            foreach (PartyMember m in _members) if (!m.ready && !m.leader) return OpResult.Fail(m.name + " is not ready yet.");
            if (OnlineHooks.Matchmaker == null) return OpResult.Fail("Online matches are not available.");

            ModeRules rules = ModeRules.For(Mode);
            var req = new MatchRequest
            {
                Mode = Mode,
                Players = Mode == GameMode.LastKiteFlying ? 12 : rules.maxPlayers,
                PrivateDuel = false
            };
            if (OnlineHooks.Matchmaker is SessionMatchmaker sm) sm.AutoStartAt = _members.Count;
            var done = new TaskCompletionSource<OpResult>();
            OnlineHooks.Matchmaker.CreatePrivate(req,
                async code =>
                {
                    IHostSession host = _party.AsHost();
                    host.SetProperty(PropMatch, new SessionProperty(code, VisibilityPropertyOptions.Member));
                    await host.SavePropertiesAsync();
                    done.TrySetResult(OpResult.Success());
                },
                session => Game.Flow.BeginOnlineMatch(session),
                fail => { done.TrySetResult(OpResult.Fail(fail)); Game.UI.Toast(fail, Kitefall.UI.Theme.Bad); });
            return await done.Task;
        }

        // ---- Session plumbing -----------------------------------------------------

        private Dictionary<string, PlayerProperty> LocalProperties(bool leader)
        {
            PlayerProfile p = Game.Profile;
            var look = new PartyMember
            {
                playerId = AuthenticationService.Instance.PlayerId,
                name = p?.displayName ?? "Player",
                characterId = p?.characterId,
                outfitId = p?.outfitId,
                accessories = p?.accessories.ToArray(),
                rankPoints = p?.rankPoints ?? 0,
                leader = leader,
                ready = leader
            };
            return new Dictionary<string, PlayerProperty>
            {
                { PropLook, new PlayerProperty(JsonUtility.ToJson(look), VisibilityPropertyOptions.Member) },
                { PropReady, new PlayerProperty(leader ? "1" : "0", VisibilityPropertyOptions.Member) }
            };
        }

        private void Attach(ISession s)
        {
            Detach();
            _party = s;
            _party.Changed += OnChanged;
            _party.SessionPropertiesChanged += OnChanged;
            _party.PlayerPropertiesChanged += OnChanged;
            _party.RemovedFromSession += OnRemoved;
            VoiceRegistry.JoinChannel?.Invoke(PartyId);
            OnChanged();
        }

        private void Detach()
        {
            if (_party == null) return;
            _party.Changed -= OnChanged;
            _party.SessionPropertiesChanged -= OnChanged;
            _party.PlayerPropertiesChanged -= OnChanged;
            _party.RemovedFromSession -= OnRemoved;
            _party = null;
            _members.Clear();
        }

        private void OnRemoved()
        {
            Detach();
            Changed?.Invoke();
        }

        private void OnChanged()
        {
            if (_party == null) return;
            _members.Clear();
            foreach (IReadOnlyPlayer pl in _party.Players)
            {
                PartyMember m = null;
                if (pl.Properties != null && pl.Properties.TryGetValue(PropLook, out PlayerProperty look) && !string.IsNullOrEmpty(look.Value))
                {
                    try { m = JsonUtility.FromJson<PartyMember>(look.Value); }
                    catch (Exception e) { m = null; Debug.LogWarning("[Party] Member look unreadable, showing a default card: " + e.Message); }
                }
                m = m ?? new PartyMember { playerId = pl.Id, name = "Player" };
                m.playerId = pl.Id;
                m.leader = _members.Count == 0 && IsHostPlayer(pl.Id);
                m.ready = pl.Properties != null && pl.Properties.TryGetValue(PropReady, out PlayerProperty r) && r.Value == "1" || m.leader;
                _members.Add(m);
            }
            if (_party.Properties != null)
            {
                if (_party.Properties.TryGetValue(PropMode, out SessionProperty mode) && Enum.TryParse(mode.Value, out GameMode gm)) Mode = gm;
                // Members follow the leader into the match.
                if (!IsLeader && _party.Properties.TryGetValue(PropMatch, out SessionProperty match) && !string.IsNullOrEmpty(match.Value) && match.Value != _lastMatchCode)
                {
                    _lastMatchCode = match.Value;
                    OnlineHooks.Matchmaker?.JoinPrivate(match.Value, session => Game.Flow.BeginOnlineMatch(session), fail => Game.UI.Toast(fail, Kitefall.UI.Theme.Bad));
                }
            }
            Changed?.Invoke();
        }

        private bool IsHostPlayer(string playerId) => _party != null && (_party.IsHost ? playerId == AuthenticationService.Instance.PlayerId : playerId == _party.Host);
    }
}
#endif
