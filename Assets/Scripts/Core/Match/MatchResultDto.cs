using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    [Serializable] public class KeyValueFloat { public string key; public float value; }

    [Serializable]
    public class ParticipantResultDto
    {
        public int index;
        public string playerId;
        public string displayName;
        public bool isBot;
        public int team;
        public int placement;
        public bool won;
        public bool quit;
        public int roundsWon, score, cuts, assists, selfSnaps, crashes, contestsWon, contestsLost, rankPoints;
        public float sweetFraction;
        public List<string> kitesLost = new List<string>();
        public List<KeyValueFloat> durabilityEnd = new List<KeyValueFloat>();
        public List<KeyValueFloat> dorLost = new List<KeyValueFloat>();
        public List<KeyValueFloat> dorUsed = new List<KeyValueFloat>();
        public List<string> dorsUsed = new List<string>();
        public List<string> charkhisUsed = new List<string>();
    }

    /// <summary>
    /// JSON-friendly copy of a <see cref="MatchResult"/> (no dictionaries) – what a match host or dedicated
    /// server sends to the Cloud Code module. Round / contest details stay on the server that ran the match.
    /// </summary>
    [Serializable]
    public class MatchResultDto
    {
        public string matchId;
        public string mode;
        public int seed;
        public float duration;
        public int winnerIndex = -1;
        public int winningTeam = -1;
        public bool draw;
        public List<ParticipantResultDto> participants = new List<ParticipantResultDto>();
        /// <summary>Round and contest details for the results "why" screen on clients (not used by the server).</summary>
        public List<RoundRecord> rounds = new List<RoundRecord>();
        public List<ContestSummary> contests = new List<ContestSummary>();

        public static MatchResultDto From(MatchResult r, string matchId)
        {
            var dto = new MatchResultDto
            {
                matchId = matchId, mode = r.Mode.ToString(), seed = r.Seed, duration = r.Duration,
                winnerIndex = r.WinnerIndex, winningTeam = r.WinningTeam, draw = r.Draw,
                rounds = new List<RoundRecord>(r.Rounds), contests = new List<ContestSummary>(r.Contests)
            };
            foreach (ParticipantResult p in r.Participants)
            {
                var d = new ParticipantResultDto
                {
                    index = p.Index, playerId = p.PlayerId, displayName = p.DisplayName, isBot = p.IsBot, team = p.Team,
                    placement = p.Placement, won = p.Won, quit = p.Quit, roundsWon = p.RoundsWon, score = p.Score, cuts = p.Cuts,
                    assists = p.Assists, selfSnaps = p.SelfSnaps, crashes = p.Crashes, contestsWon = p.ContestsWon,
                    contestsLost = p.ContestsLost, rankPoints = p.RankPoints, sweetFraction = p.SweetFraction,
                    kitesLost = new List<string>(p.KitesLost), dorsUsed = new List<string>(p.DorsUsed), charkhisUsed = new List<string>(p.CharkhisUsed)
                };
                foreach (var kv in p.DurabilityEnd) d.durabilityEnd.Add(new KeyValueFloat { key = kv.Key, value = kv.Value });
                foreach (var kv in p.DorLost) d.dorLost.Add(new KeyValueFloat { key = kv.Key, value = kv.Value });
                foreach (var kv in p.DorUsed) d.dorUsed.Add(new KeyValueFloat { key = kv.Key, value = kv.Value });
                dto.participants.Add(d);
            }
            return dto;
        }

        public MatchResult ToResult()
        {
            Enum.TryParse(mode, out GameMode m);
            var r = new MatchResult { Mode = m, Seed = seed, Duration = duration, WinnerIndex = winnerIndex, WinningTeam = winningTeam, Draw = draw };
            if (rounds != null) r.Rounds.AddRange(rounds);
            if (contests != null) r.Contests.AddRange(contests);
            foreach (ParticipantResultDto d in participants)
            {
                var p = new ParticipantResult
                {
                    Index = d.index, PlayerId = d.playerId, DisplayName = d.displayName, IsBot = d.isBot, Team = d.team,
                    Placement = d.placement, Won = d.won, Quit = d.quit, RoundsWon = d.roundsWon, Score = d.score, Cuts = d.cuts,
                    Assists = d.assists, SelfSnaps = d.selfSnaps, Crashes = d.crashes, ContestsWon = d.contestsWon,
                    ContestsLost = d.contestsLost, RankPoints = d.rankPoints, SweetFraction = d.sweetFraction,
                    KitesLost = new List<string>(d.kitesLost ?? new List<string>()),
                    DorsUsed = new List<string>(d.dorsUsed ?? new List<string>()),
                    CharkhisUsed = new List<string>(d.charkhisUsed ?? new List<string>())
                };
                if (d.durabilityEnd != null) foreach (var kv in d.durabilityEnd) p.DurabilityEnd[kv.key] = kv.value;
                if (d.dorLost != null) foreach (var kv in d.dorLost) p.DorLost[kv.key] = kv.value;
                if (d.dorUsed != null) foreach (var kv in d.dorUsed) p.DorUsed[kv.key] = kv.value;
                r.Participants.Add(p);
            }
            return r;
        }

        /// <summary>
        /// Server-side sanity checks for a reported result (relay matches are player-hosted, so the server
        /// caps what a report can claim). Returns null if plausible.
        /// </summary>
        public string Validate(ModeRules rules)
        {
            if (participants == null || participants.Count < 1 || participants.Count > Math.Max(2, rules.maxPlayers)) return "bad participant count";
            float maxMinutes = (rules.IsDuel ? rules.roundMaxTime * rules.maxRounds + 120f : rules.matchTime + 120f) / 60f;
            if (duration < 5f || duration / 60f > maxMinutes) return "implausible duration";
            int winners = 0;
            foreach (ParticipantResultDto p in participants)
            {
                if (p.won) winners++;
                if (p.cuts < 0 || p.cuts > (rules.IsDuel ? rules.maxRounds : 200)) return "implausible cuts";
                if (rules.IsDuel && p.roundsWon > rules.roundsToWin) return "implausible rounds";
                if (p.dorLost != null) foreach (var kv in p.dorLost) if (kv.value < 0 || kv.value > 1100) return "implausible dor loss";
            }
            if (rules.IsDuel && !draw && winners != 1) return "duel needs exactly one winner";
            return null;
        }
    }
}
