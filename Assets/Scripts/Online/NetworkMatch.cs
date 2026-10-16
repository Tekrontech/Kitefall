#if KITEFALL_NGO && KITEFALL_UGS_MULTIPLAYER
using System;
using System.Collections.Generic;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Kitefall.Online
{
    /// <summary>What a client tells the host when it connects (its identity and loadout ids).</summary>
    [Serializable]
    public sealed class HelloDto
    {
        public string playerId;
        public string displayName;
        public string country;
        public int rankPoints;
        public List<ParticipantLoadout> stack = new List<ParticipantLoadout>();
        // Upgrade v1.4 – visual only (the Cloud Code module only lets players equip what they own).
        public string characterId;
        public string outfitId;
        public string[] accessories;
        public bool autoPrep;
        public string partyId;
    }

    /// <summary>Everything a client needs to build the same arena and roster as the host.</summary>
    [Serializable]
    public sealed class StartDto
    {
        public ModeRules rules;
        public int arena;
        public int seed;
        public float timeOfDay;
        public List<ParticipantSetup> participants = new List<ParticipantSetup>();
        // Upgrade v1.4
        public bool useRoofs;
        public bool useRitual;
        public bool fixedRoofs;
        public float warmupTime;
        public float warmupMin = 10f;
        public RoofSettings roofs = new RoofSettings();
        public PrepSettings prep = new PrepSettings();

        public static StartDto From(MatchSetup s) => new StartDto
        {
            rules = s.Rules, arena = (int)s.Arena, seed = s.Seed, timeOfDay = s.TimeOfDay01, participants = s.Participants,
            useRoofs = s.UseRoofGraph, useRitual = s.UseLaunchRitual, fixedRoofs = s.FixedRoofs, warmupTime = s.WarmupTime, warmupMin = s.WarmupMin,
            roofs = s.Roofs, prep = s.Prep
        };

        public void ApplyTo(MatchSetup s)
        {
            s.Rules = rules;
            s.TimeOfDay01 = timeOfDay;
            s.Participants = participants;
            s.UseRoofGraph = useRoofs;
            s.UseLaunchRitual = useRitual;
            s.FixedRoofs = fixedRoofs;
            s.WarmupTime = warmupTime;
            s.WarmupMin = warmupMin;
            if (roofs != null) s.Roofs = roofs;
            if (prep != null) s.Prep = prep;
        }
    }

    [Serializable] public sealed class LoadoutChangeDto { public string tail; public string charkhi; }

    /// <summary>Named messages over Netcode for GameObjects (no NetworkObjects / prefabs needed).</summary>
    public static class NetMsg
    {
        public const string Hello = "kf_hello";
        public const string Start = "kf_start";
        public const string Input = "kf_input";
        public const string Snapshot = "kf_snap";
        public const string Result = "kf_result";
        public const string Quit = "kf_quit";
        public const string Loadout = "kf_loadout";

        public static NetworkManager Net => NetworkManager.Singleton;

        /// <summary>Creates the NetworkManager + UnityTransport once (Multiplayer Services starts it for us).</summary>
        public static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;
            var go = new GameObject("NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, TickRate = 30, ConnectionApproval = false };
            go.SetActive(true);
        }

        public static void Send(string name, ulong client, byte[] data, NetworkDelivery delivery)
        {
            if (Net == null || !Net.IsListening) return;
            using (var w = new FastBufferWriter(data.Length + 8, Allocator.Temp, data.Length + 64))
            {
                w.WriteValueSafe(data);
                Net.CustomMessagingManager.SendNamedMessage(name, client, w, delivery);
            }
        }

        public static void SendToAll(string name, byte[] data, NetworkDelivery delivery)
        {
            if (Net == null || !Net.IsListening) return;
            using (var w = new FastBufferWriter(data.Length + 8, Allocator.Temp, data.Length + 64))
            {
                w.WriteValueSafe(data);
                Net.CustomMessagingManager.SendNamedMessageToAll(name, w, delivery);
            }
        }

        public static byte[] Read(FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte[] data);
            return data;
        }

        public static void Handle(string name, Action<ulong, byte[]> handler)
        {
            Net.CustomMessagingManager.UnregisterNamedMessageHandler(name);
            Net.CustomMessagingManager.RegisterNamedMessageHandler(name, (sender, reader) => handler(sender, Read(reader)));
        }

        public static NetworkDelivery SnapshotDelivery(int bytes) =>
            bytes > 1100 ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;

        /// <summary>Host side: a clean participant from a client's hello – stats rebuilt from item ids, cosmetics checked against the catalogue.</summary>
        public static void ApplyCosmetics(ParticipantSetup ps, HelloDto h)
        {
            ps.characterId = Catalog.Cosmetic(h.characterId)?.type == CosmeticType.Character ? h.characterId : CharacterCatalog.DefaultCharacter;
            ps.outfitId = string.IsNullOrEmpty(h.outfitId) || Catalog.Cosmetic(h.outfitId)?.type == CosmeticType.Outfit ? h.outfitId ?? "" : "";
            ps.accessories = new string[4];
            if (h.accessories != null)
                for (int i = 0; i < 4 && i < h.accessories.Length; i++)
                {
                    CosmeticItem a = Catalog.Cosmetic(h.accessories[i]);
                    if (a != null && a.type == CosmeticType.Accessory && a.slot == i) ps.accessories[i] = a.id;
                }
            ps.autoPrep = h.autoPrep;
            ps.partyId = h.partyId ?? "";
        }

        /// <summary>Upgrade v1.4 flags for an online match (rooftops, ritual, a short warmup once the room is full).</summary>
        public static void ApplyUpgrade(MatchSetup setup)
        {
            setup.UseRoofGraph = true;
            setup.UseLaunchRitual = true;
            setup.WarmupTime = setup.Rules.warmup && !setup.Rules.ranked ? (Game.Balance != null ? Game.Balance.warmupMin : 10f) : 0f;
        }

        public static HelloDto LocalHello()
        {
            PlayerProfile p = Game.Profile;
            var hello = new HelloDto { playerId = p.playerId, displayName = p.displayName, country = p.country, rankPoints = p.rankPoints };
            hello.characterId = p.characterId;
            hello.outfitId = p.outfitId;
            hello.accessories = p.accessories.ToArray();
            hello.autoPrep = (Game.Settings != null && Game.Settings.Data.autoPrep) || p.autoPrep || p.matches < 5;
            hello.partyId = Game.Party != null ? Game.Party.PartyId : "";
            foreach (string kite in p.loadout.stack.Count > 0 ? p.loadout.stack : new List<string> { p.loadout.kite })
                hello.stack.Add(EconomyService.BuildMatchLoadout(p, p.loadout, kite));
            return hello;
        }
    }

    /// <summary>
    /// The match host (player-hosted via Relay): runs the authoritative MatchSimulation, applies remote inputs,
    /// broadcasts a snapshot every tick, reports the result to the Cloud Code module. Spec 14.1 / rule 2.
    /// </summary>
    public sealed class HostSession : IMatchSession
    {
        private readonly OfflineMatchSession _inner;
        private readonly Dictionary<ulong, int> _clients;
        private readonly string _matchId;
        private bool _reported;

        public HostSession(MatchSetup setup, Dictionary<ulong, int> clients, string matchId)
        {
            _clients = clients;
            _matchId = matchId;
            _inner = new OfflineMatchSession(setup, 0) { IsOnline = true };
            _inner.Stepped += Broadcast;
            NetMsg.Handle(NetMsg.Input, (sender, data) =>
            {
                if (!_clients.TryGetValue(sender, out int index)) return;
                KiteInput input = NetCodec.DecodeInput(data, out _);
                _inner.SetRemoteInput(index, input);
            });
            NetMsg.Handle(NetMsg.Quit, (sender, data) =>
            {
                if (_clients.TryGetValue(sender, out int index)) _inner.Simulation.Forfeit(index);
            });
            NetMsg.Handle(NetMsg.Loadout, (sender, data) =>
            {
                if (!_clients.TryGetValue(sender, out int index)) return;
                var dto = JsonUtility.FromJson<LoadoutChangeDto>(NetCodec.Utf8(data));
                _inner.Simulation.TryChangeLoadout(index, SpecDefaults.Tail(dto.tail), dto.tail, SpecDefaults.Charkhi(dto.charkhi), dto.charkhi);
            });
            NetMsg.Net.OnClientDisconnectCallback += OnDisconnect;
        }

        public void OnReconnect(ulong clientId, int index)
        {
            _clients[clientId] = index;
            _inner.Simulation.SetConnected(index, true);
        }

        private void OnDisconnect(ulong clientId)
        {
            if (_clients.TryGetValue(clientId, out int index)) _inner.Simulation.SetConnected(index, false);
        }

        private void Broadcast(MatchSnapshot snap)
        {
            byte[] data = NetCodec.EncodeSnapshot(snap);
            NetMsg.SendToAll(NetMsg.Snapshot, data, NetMsg.SnapshotDelivery(data.Length));
            if (_inner.IsOver && !_reported) Report();
        }

        private async void Report()
        {
            _reported = true;
            MatchResult result = _inner.Result;
            if (result == null) return;
            MatchResultDto dto = MatchResultDto.From(result, _matchId);
            NetMsg.SendToAll(NetMsg.Result, NetCodec.Utf8(JsonUtility.ToJson(dto)), NetworkDelivery.ReliableFragmentedSequenced);
            await Game.Backend.SubmitMatchAsync(result, new MatchRewardContext { MatchId = _matchId, Mode = result.Mode, MyIndex = 0 });
            ReportDone = true;
        }

        public bool ReportDone { get; private set; }

        public MatchSetup Setup => _inner.Setup;
        public ArenaLayout Arena => _inner.Arena;
        public int LocalIndex => 0;
        public bool IsOnline => true;
        public MatchSnapshot Previous => _inner.Previous;
        public MatchSnapshot Current => _inner.Current;
        public float Alpha => _inner.Alpha;
        public bool IsOver => _inner.IsOver && ReportDone;
        public MatchResult Result => _inner.Result;
        public IReadOnlyList<RoundRecord> Rounds => _inner.Rounds;
        public void SetLocalInput(KiteInput input) => _inner.SetLocalInput(input);
        public void SendCommand(PlayerCommand command, int arg) => _inner.SendCommand(command, arg);
        public void Update(float deltaTime) => _inner.Update(deltaTime);
        public List<MatchEvent> DrainEvents() => _inner.DrainEvents();
        public bool RequestLoadoutChange(string tailId, string charkhiId) => _inner.RequestLoadoutChange(tailId, charkhiId);
        public void Quit() => _inner.Quit();

        public void Dispose()
        {
            if (NetMsg.Net != null) NetMsg.Net.OnClientDisconnectCallback -= OnDisconnect;
            SessionMatchmaker.LeaveCurrent();
        }
    }

    /// <summary>A client: sends inputs at 20 Hz, draws the host's snapshots (interpolated).</summary>
    public sealed class ClientSession : IMatchSession
    {
        private MatchSnapshot _prev = new MatchSnapshot();
        private MatchSnapshot _curr = new MatchSnapshot();
        private readonly List<MatchEvent> _pending = new List<MatchEvent>();
        private float _sinceSnapshot;
        private float _sendTimer;
        private KiteInput _input;
        private bool _diveLatch;
        private int _tick;
        private MatchResult _result;
        private readonly List<RoundRecord> _rounds = new List<RoundRecord>();

        public MatchSetup Setup { get; }
        public ArenaLayout Arena { get; }
        public int LocalIndex { get; }
        public bool IsOnline => true;
        public MatchSnapshot Previous => _prev;
        public MatchSnapshot Current => _curr;
        public float Alpha => Mathf.Clamp01(_sinceSnapshot / MatchSimulation.Dt);
        public bool IsOver => _result != null;
        public MatchResult Result => _result;
        public IReadOnlyList<RoundRecord> Rounds => _result != null ? _result.Rounds : _rounds;

        public ClientSession(StartDto start, int localIndex)
        {
            MatchSetup setup = MatchFactory.NewSetup(start.rules.mode, (ArenaType)start.arena, start.seed);
            Game.Db.Configure(setup);
            start.ApplyTo(setup);
            Setup = setup;
            // Same deterministic world as the host (rooftop network + waiting plaza included).
            Arena = ArenaLayout.Generate(setup.Arena, setup.Seed, setup.Participants.Count, setup.UseRoofGraph, setup.Rules.IsDuel, setup.Roofs);
            if (setup.UseRoofGraph && setup.WarmupTime > 0f) Arena.BuildWaitingRoof(setup.Participants.Count);
            LocalIndex = localIndex;
            _curr.Kites = new KiteSnap[setup.Participants.Count];
            _prev.Kites = new KiteSnap[setup.Participants.Count];
            for (int i = 0; i < _curr.Kites.Length; i++)
            {
                _curr.Kites[i] = new KiteSnap { Status = KiteStatus.Waiting, Anchor = Arena.Anchors[i], Connected = true };
                _prev.Kites[i] = _curr.Kites[i];
            }

            NetMsg.Handle(NetMsg.Snapshot, (sender, data) =>
            {
                var snap = new MatchSnapshot();
                NetCodec.DecodeSnapshot(data, snap);
                if (snap.Tick <= _curr.Tick && _curr.Tick > 0) return; // late / duplicate packet
                _prev = _curr;
                _curr = snap;
                _sinceSnapshot = 0f;
                _pending.AddRange(snap.Events);
            });
            NetMsg.Handle(NetMsg.Result, (sender, data) =>
            {
                var dto = JsonUtility.FromJson<MatchResultDto>(NetCodec.Utf8(data));
                _result = dto.ToResult();
            });
        }

        public void SetLocalInput(KiteInput input)
        {
            if (input.DiveHeld) _diveLatch = true;
            _input = input;
        }

        private PlayerCommand _command;
        private int _commandArg;
        private byte _commandSeq;
        private int _commandRepeats;

        /// <summary>Commands ride in the next 10 input packets (unreliable channel); the host runs each sequence number once.</summary>
        public void SendCommand(PlayerCommand command, int arg)
        {
            _command = command;
            _commandArg = arg;
            _commandSeq++;
            _commandRepeats = 10;
        }

        public void Update(float deltaTime)
        {
            _sinceSnapshot += deltaTime;
            _sendTimer += deltaTime;
            if (_sendTimer < MatchSimulation.Dt) return;
            _sendTimer = 0f;
            KiteInput i = _input;
            i.DiveHeld |= _diveLatch;
            _diveLatch = false;
            if (_commandRepeats > 0)
            {
                _commandRepeats--;
                i.Command = _command;
                i.CommandArg = (short)_commandArg;
                i.CommandSeq = _commandSeq;
            }
            NetMsg.Send(NetMsg.Input, NetworkManager.ServerClientId, NetCodec.EncodeInput(++_tick, i), NetworkDelivery.UnreliableSequenced);
        }

        public List<MatchEvent> DrainEvents()
        {
            var list = new List<MatchEvent>(_pending);
            _pending.Clear();
            return list;
        }

        public bool RequestLoadoutChange(string tailId, string charkhiId)
        {
            NetMsg.Send(NetMsg.Loadout, NetworkManager.ServerClientId, NetCodec.Utf8(JsonUtility.ToJson(new LoadoutChangeDto { tail = tailId, charkhi = charkhiId })), NetworkDelivery.ReliableSequenced);
            return true;
        }

        public void Quit()
        {
            NetMsg.Send(NetMsg.Quit, NetworkManager.ServerClientId, new byte[] { 1 }, NetworkDelivery.ReliableSequenced);
            if (_result != null) return;
            // Leaving early: show a local "you quit" result (the host reports the real one to the server).
            _result = new MatchResult { Mode = Setup.Rules.mode };
            for (int i = 0; i < Setup.Participants.Count; i++)
            {
                ParticipantSetup p = Setup.Participants[i];
                _result.Participants.Add(new ParticipantResult { Index = i, PlayerId = p.playerId, DisplayName = p.displayName, IsBot = p.isBot, Quit = i == LocalIndex, Placement = i == LocalIndex ? Setup.Participants.Count : 1 });
            }
        }

        public void Dispose() => SessionMatchmaker.LeaveCurrent();
    }
}
#endif
