using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>
    /// Offline / development backend: runs the same Core.EconomyCommands locally and saves to a file.
    /// Good for playing and testing in the editor. NOT secure – a player could edit the file.
    /// For release, enable UGS (docs/ONLINE_SETUP.md) so the server owns the profile (spec rule 2).
    /// </summary>
    public sealed class LocalBackendService : IBackendService
    {
        private readonly EconomyBundle _config;
        private readonly string _path;
        private PlayerProfile _profile;

        public string Name => "Offline (local save)";
        public bool IsOnline => false;
        public bool IsSignedIn { get; private set; }
        public PlayerProfile Profile => _profile;
        public long NowUtc => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public event Action ProfileChanged;

        public LocalBackendService(EconomyBundle config)
        {
            _config = config ?? new EconomyBundle();
            _path = Path.Combine(Application.persistentDataPath, "kitefall_profile.json");
        }

        public Task<bool> InitializeAsync()
        {
            try
            {
                if (File.Exists(_path)) _profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(_path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocalBackend] Save file unreadable, starting fresh. " + e.Message);
            }
            IsSignedIn = _profile != null && !string.IsNullOrEmpty(_profile.playerId);
            return Task.FromResult(true);
        }

        public Task<OpResult> SignInAsync(SignInMethod method)
        {
            if (_profile == null || string.IsNullOrEmpty(_profile.playerId))
            {
                _profile = new PlayerProfile();
                EconomyService.CreateProfile(_profile, "local_" + Guid.NewGuid().ToString("N").Substring(0, 10), NowUtc);
            }
            if (method != SignInMethod.Guest)
            {
                // Real Google / Apple sign-in needs the online backend; offline we just mark the provider.
                _profile.guest = false;
                _profile.linkedProvider = method.ToString();
            }
            IsSignedIn = true;
            Save();
            return Task.FromResult(OpResult.Success());
        }

        public Task<OpResult> LinkAccountAsync(SignInMethod method) => RunAsync(EconomyCommands.Link, method.ToString());

        public Task SignOutAsync()
        {
            IsSignedIn = false;
            return Task.CompletedTask;
        }

        public Task<OpResult> RunAsync(string op, params string[] args)
        {
            if (_profile == null) return Task.FromResult(OpResult.Fail("Not signed in."));
            OpResult r = EconomyCommands.Execute(_profile, op, args, NowUtc, _config);
            if (r.Ok) Save();
            return Task.FromResult(r);
        }

        public Task<OpResult> SubmitMatchAsync(MatchResult result, MatchRewardContext context)
        {
            if (_profile == null) return Task.FromResult(OpResult.Fail("Not signed in."));
            context.NowUtc = NowUtc;
            OpResult r = EconomyService.ApplyMatchResult(_profile, result, context, _config.economy, _config.ranks);
            if (r.Ok) Save();
            return Task.FromResult(r);
        }

        public Task<List<LeaderboardEntry>> GetLeaderboardAsync(LeaderboardScope scope, int count)
        {
            // Offline sample board (clearly labelled in the UI): the player among deterministic sample flyers.
            var list = new List<LeaderboardEntry>();
            var rng = new DeterministicRandom((int)scope * 97 + 3);
            int n = scope == LeaderboardScope.Friends ? Math.Min(count, _profile?.friends.Count ?? 0) : count;
            for (int i = 0; i < n; i++)
            {
                string name = scope == LeaderboardScope.Friends ? _profile.friends[i] : MatchFactory.BotNames[i % MatchFactory.BotNames.Length] + (i >= MatchFactory.BotNames.Length ? (i / MatchFactory.BotNames.Length).ToString() : "");
                list.Add(new LeaderboardEntry
                {
                    playerId = "sample_" + i,
                    name = name,
                    country = scope == LeaderboardScope.Country ? _profile?.country : MatchFactory.BotCountries[rng.Range(0, MatchFactory.BotCountries.Length)],
                    points = scope == LeaderboardScope.Friends ? rng.Range(0, 1500) : 3200 - i * 45 + rng.Range(0, 30)
                });
            }
            if (_profile != null) list.Add(new LeaderboardEntry { playerId = _profile.playerId, name = _profile.displayName, country = _profile.country, points = _profile.rankPoints, isMe = true });
            list.Sort((a, b) => b.points.CompareTo(a.points));
            for (int i = 0; i < list.Count; i++) list[i].rank = i + 1;
            return Task.FromResult(list);
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(_profile));
            }
            catch (Exception e)
            {
                Debug.LogError("[LocalBackend] Could not save profile: " + e.Message);
            }
            ProfileChanged?.Invoke();
        }

        /// <summary>Deletes the local save (Settings ▸ Account ▸ Reset).</summary>
        public void DeleteSave()
        {
            if (File.Exists(_path)) File.Delete(_path);
            _profile = null;
            IsSignedIn = false;
        }
    }
}
