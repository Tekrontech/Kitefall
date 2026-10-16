using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Kitefall.Server
{
    /// <summary>Response sent back to the game (the client caches the returned profile).</summary>
    public sealed class EconomyResponse
    {
        public bool ok;
        public string error;
        public List<string> ledger = new List<string>();
        public int rankDelta;
        public long nowUtc;
        public PlayerProfile profile;
    }

    /// <summary>
    /// Kitefall server economy (spec rule 2, 14). Profiles are stored as Cloud Save PROTECTED player data:
    /// players can read them but only this module can write them, so kites, dor meters, durability,
    /// parts and rank cannot be changed from a hacked client.
    /// Module name (for CallModuleEndpointAsync) = "KitefallEconomy".
    /// </summary>
    public sealed class KitefallEconomy
    {
        private const string ProfileKey = "kitefall_profile";
        public const string RankedLeaderboard = "ranked";

        /// <summary>
        /// Player-hosted (Relay) Ranked results are refused by default – spec 14 wants a dedicated server
        /// for Ranked. Set to true only while testing without a dedicated server.
        /// </summary>
        private const bool AllowPlayerHostedRanked = false;

        private readonly ILogger<KitefallEconomy> _log;

        public KitefallEconomy(ILogger<KitefallEconomy> log) => _log = log;

        /// <summary>Loads (or creates) the caller's profile.</summary>
        [CloudCodeFunction("GetProfile")]
        public async Task<string> GetProfile(IExecutionContext ctx, IGameApiClient api)
        {
            PlayerProfile p = await Load(ctx, api, ctx.PlayerId);
            return Respond(OpResult.Success(), p);
        }

        /// <summary>Every player action (craft, repair, shop, rewarded ads…) – see Core.EconomyCommands.</summary>
        [CloudCodeFunction("Run")]
        public async Task<string> Run(IExecutionContext ctx, IGameApiClient api, string op, string[] args)
        {
            PlayerProfile p = await Load(ctx, api, ctx.PlayerId);
            if (op == EconomyCommands.GrantPurchase && !await ReceiptValid(ctx, args))
                return Respond(OpResult.Fail("Purchase could not be verified."), p);
            OpResult r = EconomyCommands.Execute(p, op, args, Now, Config());
            if (r.Ok) await Save(ctx, api, ctx.PlayerId, p);
            return Respond(r, p);
        }

        /// <summary>
        /// Match result from a match host (player-hosted Relay), from a single player's bot match, or from a
        /// dedicated server (service account, no PlayerId). Applies rewards to every human in the result.
        /// Only reports that pass <see cref="MatchResultDto.Validate"/> are accepted; each match id is rewarded once per player.
        /// </summary>
        [CloudCodeFunction("ReportMatch")]
        public async Task<string> ReportMatch(IExecutionContext ctx, IGameApiClient api, string resultJson)
        {
            MatchResultDto dto = JsonConvert.DeserializeObject<MatchResultDto>(resultJson);
            if (dto == null) return Respond(OpResult.Fail("Bad result."), null);
            Enum.TryParse(dto.mode, out GameMode mode);
            ModeRules rules = ModeRules.For(mode == GameMode.Tutorial ? GameMode.QuickDuel : mode);
            string invalid = mode == GameMode.Tutorial ? null : dto.Validate(rules);
            if (invalid != null)
            {
                _log.LogWarning("Rejected match {Id}: {Why}", dto.matchId, invalid);
                return Respond(OpResult.Fail(invalid), null);
            }

            bool fromServer = string.IsNullOrEmpty(ctx.PlayerId);
            if (rules.ranked && !fromServer && !AllowPlayerHostedRanked)
                return Respond(OpResult.Fail("Ranked results are only accepted from the match server."), null);
            if (!fromServer && !dto.participants.Exists(x => x.playerId == ctx.PlayerId))
                return Respond(OpResult.Fail("Reporter was not in the match."), null);

            MatchResult result = dto.ToResult();
            OpResult mine = OpResult.Fail("You were not rewarded for this match.");
            PlayerProfile myProfile = null;
            foreach (ParticipantResult pr in result.Participants)
            {
                if (pr.IsBot || string.IsNullOrEmpty(pr.PlayerId)) continue;
                PlayerProfile p = await Load(ctx, api, pr.PlayerId);
                int opponentRank = 0;
                foreach (ParticipantResult o in result.Participants) if (o.Index != pr.Index) { opponentRank = o.RankPoints; break; }
                OpResult r = EconomyService.ApplyMatchResult(p, result, new MatchRewardContext
                {
                    MatchId = dto.matchId, Mode = mode, MyIndex = pr.Index, OpponentRankPoints = opponentRank, NowUtc = Now
                }, Config().economy, Config().ranks);
                if (!r.Ok) continue;
                await Save(ctx, api, pr.PlayerId, p);
                if (mode == GameMode.RankedDuel) await SubmitScore(ctx, api, pr.PlayerId, p);
                if (pr.PlayerId == ctx.PlayerId) { mine = r; myProfile = p; }
            }
            if (fromServer) mine = OpResult.Success();
            return Respond(mine, myProfile);
        }

        // ---------------------------------------------------------------------

        private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>Balance numbers. Load Remote Config here to tune the economy without a server deploy.</summary>
        private static EconomyBundle Config() => new EconomyBundle();

        private static string Token(IExecutionContext ctx) => ctx.ServiceToken;

        private async Task<PlayerProfile> Load(IExecutionContext ctx, IGameApiClient api, string playerId)
        {
            try
            {
                var res = await api.CloudSaveData.GetProtectedItemsAsync(ctx, Token(ctx), ctx.ProjectId, playerId, new List<string> { ProfileKey });
                if (res.Data?.Results != null && res.Data.Results.Count > 0)
                {
                    object value = res.Data.Results[0].Value;
                    string json = value is string s ? s : JsonConvert.SerializeObject(value);
                    PlayerProfile p = JsonConvert.DeserializeObject<PlayerProfile>(json);
                    if (p != null) return p;
                }
            }
            catch (Exception e)
            {
                _log.LogInformation("No profile for {Player} yet ({Message})", playerId, e.Message);
            }
            var fresh = new PlayerProfile();
            EconomyService.CreateProfile(fresh, playerId, Now);
            return fresh;
        }

        private static async Task Save(IExecutionContext ctx, IGameApiClient api, string playerId, PlayerProfile p)
        {
            JObject value = JObject.FromObject(p);
            await api.CloudSaveData.SetProtectedItemAsync(ctx, Token(ctx), ctx.ProjectId, playerId, new SetItemBody(ProfileKey, value));
        }

        private async Task SubmitScore(IExecutionContext ctx, IGameApiClient api, string playerId, PlayerProfile p)
        {
            try
            {
                var metadata = new Dictionary<string, string> { { "country", p.country }, { "name", p.displayName } };
                await api.Leaderboards.AddLeaderboardPlayerScoreAsync(ctx, Token(ctx), Guid.Parse(ctx.ProjectId), RankedLeaderboard, playerId,
                    new Unity.Services.Leaderboards.Model.AddLeaderboardScore(p.rankPoints, metadata));
            }
            catch (Exception e)
            {
                _log.LogWarning("Leaderboard update failed: {Message}", e.Message);
            }
        }

        /// <summary>
        /// Store receipt check. Real Google Play / App Store validation needs your store credentials
        /// (Cloud Code Secret Manager) – see docs/ONLINE_SETUP.md. Until then only test receipts are accepted,
        /// and never in the "production" environment.
        /// </summary>
        private static Task<bool> ReceiptValid(IExecutionContext ctx, string[] args)
        {
            string receipt = args != null && args.Length > 1 ? args[1] : "";
            bool production = string.Equals(ctx.EnvironmentName, "production", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(!production && !string.IsNullOrEmpty(receipt));
        }

        private static string Respond(OpResult r, PlayerProfile p)
        {
            var resp = new EconomyResponse { ok = r.Ok, error = r.Error, rankDelta = r.RankDelta, nowUtc = Now, profile = p };
            resp.ledger.AddRange(r.Ledger);
            return JsonConvert.SerializeObject(resp);
        }
    }
}
