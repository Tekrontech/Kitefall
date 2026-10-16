#if KITEFALL_NGO && UNITY_SERVER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Match;
using Kitefall.Services;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

namespace Kitefall.Online
{
    /// <summary>
    /// Dedicated server for Ranked (spec 14: "Ranked: dedicated server authority").
    /// Build with the Dedicated Server build target. Command line:
    ///   -port 7777 -mode RankedDuel -players 2 -wait 60 -seed 123 -arena OldCityRooftops
    /// Env vars for reporting results: KITEFALL_PROJECT_ID, KITEFALL_ENVIRONMENT_ID, KITEFALL_SA_KEY_ID, KITEFALL_SA_SECRET
    /// (a service account with Cloud Code access – never put these in the client).
    /// How players reach the server (IP/port allocation) depends on your hosting provider – see docs/ONLINE_SETUP.md.
    /// </summary>
    public sealed class DedicatedServer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => ServerRegistry.Start = db => new GameObject("DedicatedServer").AddComponent<DedicatedServer>();

        private OfflineMatchSession _session;
        private readonly Dictionary<ulong, int> _clients = new Dictionary<ulong, int>();
        private readonly List<(ulong client, HelloDto hello)> _hellos = new List<(ulong, HelloDto)>();
        private GameMode _mode = GameMode.RankedDuel;
        private ArenaType _arena = ArenaType.OldCityRooftops;
        private int _players = 2;
        private float _wait = 60f;
        private int _seed;
        private bool _reported;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            ushort port = 7777;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-port": ushort.TryParse(args[i + 1], out port); break;
                    case "-mode": Enum.TryParse(args[i + 1], out _mode); break;
                    case "-arena": Enum.TryParse(args[i + 1], out _arena); break;
                    case "-players": int.TryParse(args[i + 1], out _players); break;
                    case "-wait": float.TryParse(args[i + 1], out _wait); break;
                    case "-seed": int.TryParse(args[i + 1], out _seed); break;
                }
            }
            if (_seed == 0) _seed = UnityEngine.Random.Range(1, int.MaxValue);

            NetMsg.EnsureNetworkManager();
            var transport = (UnityTransport)NetMsg.Net.NetworkConfig.NetworkTransport;
            transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            NetMsg.Net.StartServer();
            NetMsg.Handle(NetMsg.Hello, OnHello);
            NetMsg.Net.OnClientDisconnectCallback += id => { if (_session != null && _clients.TryGetValue(id, out int i)) _session.Simulation.SetConnected(i, false); };
            Debug.Log($"[Server] Listening on {port} · {_mode} · {_players} players");
            StartCoroutine(Lobby());
        }

        private void OnHello(ulong sender, byte[] data)
        {
            var hello = JsonUtility.FromJson<HelloDto>(NetCodec.Utf8(data));
            if (_session != null)
            {
                int idx = _session.Setup.Participants.FindIndex(p => p.playerId == hello.playerId);
                if (idx >= 0) { _clients[sender] = idx; _session.Simulation.SetConnected(idx, true); SendStart(sender); }
                return;
            }
            if (_hellos.Exists(h => h.hello.playerId == hello.playerId)) return;
            _hellos.Add((sender, hello));
        }

        private IEnumerator Lobby()
        {
            float waited = 0f;
            ModeRules rules = ModeRules.For(_mode);
            while (_hellos.Count < _players && waited < _wait) { waited += Time.unscaledDeltaTime; yield return null; }
            if (_hellos.Count == 0 || (rules.ranked && _hellos.Count < _players)) { Debug.Log("[Server] Not enough players – shutting down."); Application.Quit(); yield break; }

            MatchSetup setup = MatchFactory.NewSetup(_mode, _arena, _seed);
            Game.Db.Configure(setup);
            NetMsg.ApplyUpgrade(setup);
            foreach (var (client, h) in _hellos)
            {
                var ps = new ParticipantSetup { playerId = h.playerId, displayName = h.displayName, country = h.country, rankPoints = h.rankPoints };
                NetMsg.ApplyCosmetics(ps, h);
                // TODO(hosting): load the real profile from Cloud Save with the service account instead of trusting the hello.
                foreach (ParticipantLoadout l in h.stack)
                {
                    ParticipantLoadout clean = SpecDefaults.Loadout(l.kiteId, l.tailId, l.dorId, l.charkhiId, Mathf.Clamp(l.durability, 1f, 100f), Mathf.Clamp(l.dorStockMeters, 0f, 5000f));
                    ps.stack.Add(clean);
                    if (setup.Rules.IsDuel) { for (int k = 0; k < 4; k++) ps.stack.Add(clean); break; }
                }
                _clients[client] = setup.Participants.Count;
                setup.Participants.Add(ps);
            }
            MatchFactory.FillWithBots(setup, setup.Rules.IsDuel ? 2 : _players, BotDifficulty.Medium, "sooti");
            _session = new OfflineMatchSession(setup, -1) { IsOnline = true };
            _session.Stepped += snap =>
            {
                byte[] d = NetCodec.EncodeSnapshot(snap);
                NetMsg.SendToAll(NetMsg.Snapshot, d, NetMsg.SnapshotDelivery(d.Length));
            };
            NetMsg.Handle(NetMsg.Input, (sender, d) => { if (_clients.TryGetValue(sender, out int i)) _session.SetRemoteInput(i, NetCodec.DecodeInput(d, out _)); });
            NetMsg.Handle(NetMsg.Quit, (sender, d) => { if (_clients.TryGetValue(sender, out int i)) _session.Simulation.Forfeit(i); });
            foreach (ulong c in _clients.Keys) SendStart(c);
            Debug.Log("[Server] Match started.");
        }

        private void SendStart(ulong client)
        {
            MatchSetup s = _session.Setup;
            StartDto dto = StartDto.From(s);
            NetMsg.Send(NetMsg.Start, client, NetCodec.Utf8(JsonUtility.ToJson(dto)), NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void Update()
        {
            if (_session == null) return;
            _session.Update(Time.deltaTime);
            if (_session.IsOver && !_reported)
            {
                _reported = true;
                MatchResultDto dto = MatchResultDto.From(_session.Result, $"server_{_seed}");
                string json = JsonUtility.ToJson(dto);
                NetMsg.SendToAll(NetMsg.Result, NetCodec.Utf8(json), NetworkDelivery.ReliableFragmentedSequenced);
                StartCoroutine(Report(json));
            }
        }

        /// <summary>Reports the result to the Cloud Code module with a service account (Ranked points are only accepted this way).</summary>
        private IEnumerator Report(string resultJson)
        {
            string project = Environment.GetEnvironmentVariable("KITEFALL_PROJECT_ID");
            string env = Environment.GetEnvironmentVariable("KITEFALL_ENVIRONMENT_ID");
            string keyId = Environment.GetEnvironmentVariable("KITEFALL_SA_KEY_ID");
            string secret = Environment.GetEnvironmentVariable("KITEFALL_SA_SECRET");
            if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(keyId)) { Debug.LogError("[Server] Service account env vars missing – result not reported."); yield return Shutdown(); yield break; }

            string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyId + ":" + secret));
            using (var tokenReq = new UnityWebRequest($"https://services.api.unity.com/auth/v1/token-exchange?projectId={project}&environmentId={env}", "POST"))
            {
                tokenReq.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
                tokenReq.downloadHandler = new DownloadHandlerBuffer();
                tokenReq.SetRequestHeader("Authorization", "Basic " + basic);
                tokenReq.SetRequestHeader("Content-Type", "application/json");
                yield return tokenReq.SendWebRequest();
                if (tokenReq.result != UnityWebRequest.Result.Success) { Debug.LogError("[Server] Token exchange failed: " + tokenReq.error); yield return Shutdown(); yield break; }
                string token = JsonUtility.FromJson<TokenResponse>(tokenReq.downloadHandler.text).accessToken;

                var body = "{\"params\":{\"resultJson\":" + Quote(resultJson) + "}}";
                using (var call = new UnityWebRequest($"https://cloud-code.services.api.unity.com/v1/projects/{project}/modules/KitefallEconomy/ReportMatch", "POST"))
                {
                    call.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    call.downloadHandler = new DownloadHandlerBuffer();
                    call.SetRequestHeader("Authorization", "Bearer " + token);
                    call.SetRequestHeader("Content-Type", "application/json");
                    yield return call.SendWebRequest();
                    Debug.Log("[Server] ReportMatch: " + (call.result == UnityWebRequest.Result.Success ? "ok" : call.error + " " + call.downloadHandler.text));
                }
            }
            yield return Shutdown();
        }

        private IEnumerator Shutdown()
        {
            yield return new WaitForSecondsRealtime(5f);
            NetMsg.Net.Shutdown();
            Application.Quit();
        }

        private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        [Serializable] private sealed class TokenResponse { public string accessToken; }
    }
}
#endif
