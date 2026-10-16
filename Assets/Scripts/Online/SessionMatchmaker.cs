#if KITEFALL_NGO && KITEFALL_UGS_MULTIPLAYER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>
    /// Online matchmaking with Unity Multiplayer Services sessions + Relay (spec 6, 14):
    ///  • Quick Duel / FFA / Team / LKF: quick-join a session of the same mode and region, or create one;
    ///    the host starts when full, or after 20 s fills the empty seats with labelled bots (never Ranked).
    ///  • Ranked Duel: Matchmaker queue (rank ±1 tier → ±2 after 10 s is configured on the queue).
    ///  • Private room: create with a join code / join by code.
    /// The session brings up Netcode for GameObjects; the host then runs the authoritative simulation.
    /// </summary>
    public sealed class SessionMatchmaker : IOnlineMatchmaker
    {
        private static ISession _session;
        private readonly OnlineConfig _config;
        private Runner _runner;
        private CancellationTokenSource _cts;
        private MatchRequest _request;
        private Action<IMatchSession> _ready;
        private Action<string> _failed;
        private bool _private;
        private bool _startNow;

        public string Status { get; private set; } = "";
        /// <summary>Party match (upgrade F2): the host starts once this many players are connected (0 = off).</summary>
        public int AutoStartAt;

        public SessionMatchmaker(OnlineConfig config) => _config = config;

        public static void LeaveCurrent()
        {
            ISession s = _session;
            _session = null;
            if (s != null) _ = s.LeaveAsync();
        }

        // ---- Entry points -------------------------------------------------------

        public async void Find(MatchRequest request, Action<IMatchSession> ready, Action<string> failed)
        {
            Begin(request, ready, failed, false);
            ModeRules rules = ModeRules.For(request.Mode);
            string region = Game.Profile != null ? Game.Profile.region : "Asia";
            try
            {
                NetMsg.EnsureNetworkManager();
                var options = new SessionOptions
                {
                    MaxPlayers = rules.mode == GameMode.LastKiteFlying ? 12 : rules.maxPlayers,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { "mode", new SessionProperty(request.Mode.ToString(), VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                        { "region", new SessionProperty(region, VisibilityPropertyOptions.Public, PropertyIndex.String2) }
                    }
                }.WithRelayNetwork();

                if (rules.ranked)
                {
                    Status = "Ranked queue · same region, rank ±1 tier";
                    _session = await MultiplayerService.Instance.MatchmakeSessionAsync(new MatchmakerOptions { QueueName = _config.rankedQueue }, options, _cts.Token);
                }
                else
                {
                    Status = $"Looking for {rules.displayName} in {region}…";
                    var quick = new QuickJoinOptions
                    {
                        Filters = new List<FilterOption>
                        {
                            new FilterOption(FilterField.StringIndex1, request.Mode.ToString(), FilterOperation.Equal),
                            new FilterOption(FilterField.StringIndex2, region, FilterOperation.Equal),
                            new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
                        },
                        CreateSession = true
                    };
                    _session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
                }
                OnSessionReady();
            }
            catch (Exception e)
            {
                Fail("Matchmaking failed: " + e.Message);
            }
        }

        public async void CreatePrivate(MatchRequest request, Action<string> codeReady, Action<IMatchSession> ready, Action<string> failed)
        {
            Begin(request, ready, failed, true);
            try
            {
                NetMsg.EnsureNetworkManager();
                int max = request.PrivateDuel ? 2 : Mathf.Clamp(request.Players, 2, 8);
                _session = await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = max, IsPrivate = true }.WithRelayNetwork());
                codeReady?.Invoke(_session.Code);
                Status = "Room " + _session.Code;
                OnSessionReady();
            }
            catch (Exception e)
            {
                Fail("Could not create the room: " + e.Message);
            }
        }

        public async void JoinPrivate(string code, Action<IMatchSession> ready, Action<string> failed)
        {
            Begin(new MatchRequest { Mode = GameMode.PrivateRoom }, ready, failed, true);
            try
            {
                NetMsg.EnsureNetworkManager();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                OnSessionReady();
            }
            catch (Exception e)
            {
                Fail("Could not join room " + code + ": " + e.Message);
            }
        }

        public void StartPrivateNow() => _startNow = true;

        public void Cancel()
        {
            _cts?.Cancel();
            if (_runner != null) UnityEngine.Object.Destroy(_runner.gameObject);
            _runner = null;
            LeaveCurrent();
        }

        // ---- Internals ------------------------------------------------------------

        private void Begin(MatchRequest request, Action<IMatchSession> ready, Action<string> failed, bool priv)
        {
            Cancel();
            _cts = new CancellationTokenSource();
            _request = request;
            _ready = ready;
            _failed = failed;
            _private = priv;
            _startNow = false;
            var go = new GameObject("OnlineMatchRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        private void Fail(string message)
        {
            Status = message;
            Cancel();
            _failed?.Invoke(message);
        }

        private void OnSessionReady()
        {
            if (_session == null) { Fail("No session."); return; }
            _runner.StartCoroutine(_session.IsHost ? HostLobby() : ClientLobby());
        }

        /// <summary>Host: collect hellos, then start (full room, 20 s bot fill, or the private-room Start button).</summary>
        private IEnumerator HostLobby()
        {
            while (NetMsg.Net == null || !NetMsg.Net.IsListening) yield return null;
            var hellos = new List<HelloDto> { NetMsg.LocalHello() };
            var clients = new Dictionary<ulong, int>();
            var pendingClients = new Dictionary<ulong, HelloDto>();
            HostSession running = null;
            NetMsg.Handle(NetMsg.Hello, (sender, data) =>
            {
                var hello = JsonUtility.FromJson<HelloDto>(NetCodec.Utf8(data));
                if (running != null)
                {
                    // Reconnect of a known player (spec 14.2); new players cannot join a running match.
                    int idx = running.Setup.Participants.FindIndex(p => p.playerId == hello.playerId);
                    if (idx >= 0) { running.OnReconnect(sender, idx); SendStart(sender, running.Setup); }
                    return;
                }
                if (pendingClients.ContainsKey(sender)) return;
                pendingClients[sender] = hello;
                hellos.Add(hello);
            });

            ModeRules rules = ModeRules.For(_request.Mode == GameMode.PrivateRoom ? (_request.PrivateDuel ? GameMode.QuickDuel : GameMode.FreeForAll) : _request.Mode);
            float waited = 0f;
            var mm = Game.Balance != null ? Game.Balance.matchmaking : new MatchmakingSettings();
            while (true)
            {
                int max = _session.MaxPlayers;
                Status = $"{hellos.Count}/{max} players · {(int)waited}s" + (rules.ranked ? "" : $" · bots join after {mm.botFillAfter:0}s");
                if (hellos.Count >= max) break;
                if (_startNow) break;
                if (AutoStartAt > 0 && hellos.Count >= AutoStartAt) { AutoStartAt = 0; break; }
                if (!_private && MatchmakingRules.ShouldFillWithBots(rules, waited, mm)) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // Build the authoritative setup on the host.
            MatchRequest req = _request.Clone();
            if (req.Seed == 0) req.Seed = UnityEngine.Random.Range(1, int.MaxValue);
            MatchSetup setup = MatchFactory.NewSetup(req.Mode, req.Mode == GameMode.LastKiteFlying ? ArenaType.StormArchipelago : req.Arena, req.Seed);
            Game.Db.Configure(setup);
            if (req.Mode == GameMode.PrivateRoom && req.PrivateDuel)
            {
                setup.Rules = ModeRules.For(GameMode.QuickDuel);
                setup.Rules.mode = GameMode.PrivateRoom;
                setup.Rules.lossMode = false;
                setup.Rules.durabilityLoss = false;
            }
            if (req.MatchMinutes > 0 && !setup.Rules.IsDuel) setup.Rules.matchTime = req.MatchMinutes * 60f;
            NetMsg.ApplyUpgrade(setup);
            setup.FixedRoofs = req.Mode == GameMode.PrivateRoom && req.FixedRoofs;
            int index = 0;
            foreach (HelloDto h in hellos)
            {
                var ps = new ParticipantSetup
                {
                    playerId = h.playerId, displayName = h.displayName, country = h.country, rankPoints = h.rankPoints,
                    team = setup.Rules.teamCount > 1 ? index % setup.Rules.teamCount : 0,
                    captain = setup.Rules.teamCount > 1 && index < setup.Rules.teamCount
                };
                NetMsg.ApplyCosmetics(ps, h);
                // Stats are rebuilt from item ids on the host – a client cannot send boosted numbers.
                foreach (ParticipantLoadout l in h.stack)
                {
                    var tunings = new List<string>();
                    if (l.tunings != null) foreach (TuningStats t in l.tunings) if (!string.IsNullOrEmpty(t.id)) tunings.Add(t.id);
                    ParticipantLoadout clean = SpecDefaults.Loadout(l.kiteId, l.tailId, l.dorId, l.charkhiId,
                        Mathf.Clamp(l.durability, 1f, 100f), Mathf.Clamp(l.dorStockMeters, 0f, 5000f), tunings.ToArray());
                    clean.skinId = l.skinId;
                    ps.stack.Add(clean);
                    if (setup.Rules.IsDuel) { for (int k = 0; k < 4; k++) ps.stack.Add(clean); break; }
                }
                setup.Participants.Add(ps);
                index++;
            }
            MatchFactory.AssignPartyTeams(setup);
            int target = req.Players > 0 ? req.Players : setup.Rules.mode == GameMode.LastKiteFlying ? 12 : setup.Rules.maxPlayers;
            if (setup.Rules.IsDuel) target = 2;
            MatchFactory.FillWithBots(setup, target, BotDifficulty.Medium, hellos[0].stack.Count > 0 ? hellos[0].stack[0].dorId : "sooti");

            int clientIndex = 1;
            foreach (var kv in pendingClients) clients[kv.Key] = clientIndex++;
            foreach (ulong c in clients.Keys) SendStart(c, setup);

            running = new HostSession(setup, clients, $"{_session.Id}_{req.Seed}");
            Status = "Starting";
            _ready?.Invoke(running);
        }

        private static void SendStart(ulong client, MatchSetup setup)
        {
            StartDto dto = StartDto.From(setup);
            NetMsg.Send(NetMsg.Start, client, NetCodec.Utf8(JsonUtility.ToJson(dto)), NetworkDelivery.ReliableFragmentedSequenced);
        }

        /// <summary>Client: say hello, wait for the start message.</summary>
        private IEnumerator ClientLobby()
        {
            float wait = 0f;
            while (NetMsg.Net == null || !NetMsg.Net.IsConnectedClient)
            {
                wait += Time.unscaledDeltaTime;
                Status = "Connecting to host…";
                if (wait > 20f) { Fail("Could not connect to the host."); yield break; }
                yield return null;
            }
            StartDto start = null;
            NetMsg.Handle(NetMsg.Start, (sender, data) => start = JsonUtility.FromJson<StartDto>(NetCodec.Utf8(data)));
            NetMsg.Send(NetMsg.Hello, NetworkManager.ServerClientId, NetCodec.Utf8(JsonUtility.ToJson(NetMsg.LocalHello())), NetworkDelivery.ReliableFragmentedSequenced);
            Status = "Waiting for the host to start…";
            while (start == null)
            {
                if (!NetMsg.Net.IsConnectedClient) { Fail("Host left."); yield break; }
                yield return null;
            }
            string me = AuthenticationService.Instance.PlayerId;
            int local = start.participants.FindIndex(p => p.playerId == me);
            if (local < 0) { Fail("The match is full."); yield break; }
            _ready?.Invoke(new ClientSession(start, local));
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
#endif
