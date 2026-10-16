#if KITEFALL_UGS_AUTH && KITEFALL_UGS_CLOUDCODE
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.Core;
using Kitefall.Services;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
#if KITEFALL_UGS_LEADERBOARDS
using Unity.Services.Leaderboards;
#endif
using LeaderboardEntry = Kitefall.Services.LeaderboardEntry;

namespace Kitefall.Online
{
    /// <summary>Same shape as the Cloud Code module's response (JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class EconomyResponseDto
    {
        public bool ok;
        public string error;
        public List<string> ledger = new List<string>();
        public int rankDelta;
        public long nowUtc;
        public PlayerProfile profile;
    }

    /// <summary>
    /// Google Play Games / Apple sign-in need their platform plugins to produce a token. Set these from your
    /// plugin integration (see docs/ONLINE_SETUP.md); without them only Guest sign-in is available.
    /// </summary>
    public static class SocialSignIn
    {
        public static Func<Task<string>> GooglePlayGamesAuthCode;
        public static Func<Task<string>> AppleIdToken;
    }

    /// <summary>
    /// Online backend (spec 14): Authentication + Cloud Code module "KitefallEconomy" (server-authoritative
    /// economy on Cloud Save protected data) + Leaderboards. The client never writes the profile itself.
    /// </summary>
    public sealed class UgsBackendService : IBackendService
    {
        private readonly OnlineConfig _config;
        private PlayerProfile _profile;
        private long _serverOffset;
        private bool _initialised;

        public string Name => "Unity Gaming Services (" + _config.environment + ")";
        public bool IsOnline => _initialised;
        public bool IsSignedIn => _initialised && AuthenticationService.Instance.IsSignedIn && _profile != null;
        public PlayerProfile Profile => _profile;
        public long NowUtc => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _serverOffset;
        public event Action ProfileChanged;

        public UgsBackendService(OnlineConfig config) => _config = config;

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(_config.environment));
                _initialised = true;
                // Returning players: a cached session token signs in silently.
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    await LoadProfileAsync();
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UGS] Initialisation failed: " + e.Message);
                _initialised = false;
                return false;
            }
        }

        public async Task<OpResult> SignInAsync(SignInMethod method)
        {
            if (!_initialised && !await InitializeAsync()) return OpResult.Fail("No connection – retry.");
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    switch (method)
                    {
                        case SignInMethod.Google:
                            if (SocialSignIn.GooglePlayGamesAuthCode == null) return OpResult.Fail("Google sign-in needs the Google Play Games plugin – use Guest for now.");
                            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(await SocialSignIn.GooglePlayGamesAuthCode());
                            break;
                        case SignInMethod.Apple:
                            if (SocialSignIn.AppleIdToken == null) return OpResult.Fail("Apple sign-in needs the Sign in with Apple plugin – use Guest for now.");
                            await AuthenticationService.Instance.SignInWithAppleAsync(await SocialSignIn.AppleIdToken());
                            break;
                        default:
                            await AuthenticationService.Instance.SignInAnonymouslyAsync();
                            break;
                    }
                }
                await LoadProfileAsync();
                if (method != SignInMethod.Guest && _profile != null && _profile.guest)
                    await RunAsync(EconomyCommands.Link, method.ToString());
                return OpResult.Success();
            }
            catch (Exception e)
            {
                return OpResult.Fail("Sign-in failed: " + e.Message);
            }
        }

        public async Task<OpResult> LinkAccountAsync(SignInMethod method)
        {
            try
            {
                switch (method)
                {
                    case SignInMethod.Google:
                        if (SocialSignIn.GooglePlayGamesAuthCode == null) return OpResult.Fail("Google Play Games plugin not set up.");
                        await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(await SocialSignIn.GooglePlayGamesAuthCode());
                        break;
                    case SignInMethod.Apple:
                        if (SocialSignIn.AppleIdToken == null) return OpResult.Fail("Sign in with Apple plugin not set up.");
                        await AuthenticationService.Instance.LinkWithAppleAsync(await SocialSignIn.AppleIdToken());
                        break;
                    default:
                        return OpResult.Fail("Choose Google or Apple.");
                }
                return await RunAsync(EconomyCommands.Link, method.ToString());
            }
            catch (Exception e)
            {
                return OpResult.Fail("Linking failed: " + e.Message);
            }
        }

        public Task SignOutAsync()
        {
            AuthenticationService.Instance.SignOut();
            _profile = null;
            return Task.CompletedTask;
        }

        public async Task RefreshProfileAsync() => await LoadProfileAsync();

        private async Task LoadProfileAsync()
        {
            EconomyResponseDto r = await Call("GetProfile", new Dictionary<string, object>());
            if (r != null && r.profile != null) SetProfile(r);
        }

        public async Task<OpResult> RunAsync(string op, params string[] args)
        {
            EconomyResponseDto r = await Call("Run", new Dictionary<string, object> { { "op", op }, { "args", args ?? new string[0] } });
            return ToResult(r);
        }

        public async Task<OpResult> SubmitMatchAsync(MatchResult result, MatchRewardContext context)
        {
            MatchResultDto dto = MatchResultDto.From(result, context.MatchId);
            EconomyResponseDto r = await Call("ReportMatch", new Dictionary<string, object> { { "resultJson", JsonUtility.ToJson(dto) } });
            return ToResult(r);
        }

        public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(LeaderboardScope scope, int count)
        {
            var list = new List<LeaderboardEntry>();
#if KITEFALL_UGS_LEADERBOARDS
            try
            {
                string me = AuthenticationService.Instance.PlayerId;
                if (scope == LeaderboardScope.Friends)
                {
                    var ids = new List<string>(_profile != null ? _profile.friends : new List<string>());
                    ids.Add(me);
                    var res = await LeaderboardsService.Instance.GetScoresByPlayerIdsAsync(_config.rankedLeaderboardId, ids);
                    foreach (var e in res.Results) list.Add(Entry(e.Rank, e.PlayerId, e.PlayerName, e.Score, e.Metadata, me));
                }
                else
                {
                    var res = await LeaderboardsService.Instance.GetScoresAsync(_config.rankedLeaderboardId, new Unity.Services.Leaderboards.GetScoresOptions { Limit = scope == LeaderboardScope.Country ? 500 : count, IncludeMetadata = true });
                    foreach (var e in res.Results)
                    {
                        LeaderboardEntry entry = Entry(e.Rank, e.PlayerId, e.PlayerName, e.Score, e.Metadata, me);
                        if (scope == LeaderboardScope.Country && _profile != null && entry.country != _profile.country) continue;
                        list.Add(entry);
                        if (list.Count >= count) break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UGS] Leaderboard: " + e.Message);
            }
#endif
            return list;
        }

        private static LeaderboardEntry Entry(int rank, string id, string name, double score, string metadata, string me)
        {
            var entry = new LeaderboardEntry { rank = rank + 1, playerId = id, name = name, points = (int)score, isMe = id == me };
            if (!string.IsNullOrEmpty(metadata))
            {
                var meta = JsonUtility.FromJson<LeaderboardMeta>(metadata);
                if (meta != null)
                {
                    if (!string.IsNullOrEmpty(meta.name)) entry.name = meta.name;
                    entry.country = meta.country;
                }
            }
            return entry;
        }

        [Serializable] private sealed class LeaderboardMeta { public string country; public string name; }

        private async Task<EconomyResponseDto> Call(string function, Dictionary<string, object> args)
        {
            try
            {
                string json = await CloudCodeService.Instance.CallModuleEndpointAsync(_config.cloudCodeModule, function, args);
                return JsonUtility.FromJson<EconomyResponseDto>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] {function} failed: {e.Message}");
                return new EconomyResponseDto { ok = false, error = "Server unavailable – " + e.Message };
            }
        }

        private OpResult ToResult(EconomyResponseDto r)
        {
            if (r == null) return OpResult.Fail("No response.");
            SetProfile(r);
            OpResult result = r.ok ? OpResult.Success() : OpResult.Fail(r.error);
            result.RankDelta = r.rankDelta;
            if (r.ledger != null) result.Ledger.AddRange(r.ledger);
            return result;
        }

        private void SetProfile(EconomyResponseDto r)
        {
            if (r.nowUtc > 0) _serverOffset = r.nowUtc - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (r.profile == null || string.IsNullOrEmpty(r.profile.playerId)) return;
            _profile = r.profile;
            ProfileChanged?.Invoke();
        }
    }
}
#endif
