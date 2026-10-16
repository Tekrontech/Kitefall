using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    public enum RoofSelectMode
    {
        /// <summary>Everyone picks any free roof (FFA, LKF, Training, Private "Free").</summary>
        Free,
        /// <summary>Team Battle: own half of the map, up to 2 teammates per roof, never enemies together.</summary>
        TeamHalves,
        /// <summary>Quick Duel: players pick one of 3 mirrored pairs.</summary>
        DuelPairs,
        /// <summary>Ranked Duel / Private "Fixed": the server assigns (mirrored pair for duels).</summary>
        Assigned
    }

    /// <summary>
    /// Rooftop selection (upgrade spec 10.3). The server/host decides conflicts: first request wins, taken roofs are locked.
    /// Pure C# – the same rules run offline, on the host and in tests.
    /// </summary>
    public sealed class RoofClaims
    {
        public RoofGraph Graph { get; }
        public RoofSelectMode Mode { get; }
        public int PlayerCount => _claims.Length;
        /// <summary>For DuelPairs: the left roofs of the offered mirrored pairs.</summary>
        public List<int> OfferedPairs { get; } = new List<int>();

        private readonly int[] _claims;
        private readonly int[] _teams;
        private readonly DeterministicRandom _rng;

        public RoofClaims(RoofGraph graph, RoofSelectMode mode, IReadOnlyList<int> teams, int seed)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Mode = mode;
            _claims = new int[teams.Count];
            _teams = new int[teams.Count];
            for (int i = 0; i < teams.Count; i++) { _claims[i] = -1; _teams[i] = teams[i]; }
            _rng = new DeterministicRandom(seed * 389 + 7);

            if (mode == RoofSelectMode.DuelPairs || mode == RoofSelectMode.Assigned && graph.Mirrored)
            {
                var left = new List<int>();
                foreach (RoofNode r in graph.Roofs) if (r.MirrorOf > r.Id) left.Add(r.Id);
                for (int i = 0; i < 3 && left.Count > 0; i++)
                {
                    int pick = left[_rng.Range(0, left.Count)];
                    left.Remove(pick);
                    OfferedPairs.Add(pick);
                }
            }
        }

        public int ClaimOf(int player) => player >= 0 && player < _claims.Length ? _claims[player] : -1;

        public int CapacityOf(int roof) => Mode == RoofSelectMode.TeamHalves ? 2 : 1;

        public int CountOn(int roof)
        {
            int n = 0;
            foreach (int c in _claims) if (c == roof) n++;
            return n;
        }

        /// <summary>Slot (0/1) of this player on its roof, for teammates sharing one.</summary>
        public int SlotOf(int player)
        {
            int roof = ClaimOf(player), slot = 0;
            for (int i = 0; i < player; i++) if (_claims[i] == roof) slot++;
            return slot;
        }

        /// <summary>Team half: team 0 = left (x &lt; 0), team 1 = right.</summary>
        public bool InTeamHalf(int roof, int team)
        {
            float x = Graph.Roof(roof).Center.X;
            return team % 2 == 0 ? x <= 0.5f : x >= -0.5f;
        }

        public bool CanClaim(int player, int roof)
        {
            if (player < 0 || player >= _claims.Length || roof < 0 || roof >= Graph.Roofs.Count) return false;
            if (_claims[player] == roof) return true;
            switch (Mode)
            {
                case RoofSelectMode.Assigned:
                    return false;
                case RoofSelectMode.DuelPairs:
                    // Either duelist may pick one of the offered pairs (by its left or right roof).
                    return OfferedPairs.Contains(roof) || OfferedPairs.Contains(Graph.Roof(roof).MirrorOf);
                case RoofSelectMode.TeamHalves:
                    if (!InTeamHalf(roof, _teams[player])) return false;
                    for (int i = 0; i < _claims.Length; i++)
                        if (i != player && _claims[i] == roof && _teams[i] != _teams[player]) return false;
                    return CountOn(roof) < CapacityOf(roof);
                default:
                    return CountOn(roof) < CapacityOf(roof);
            }
        }

        /// <summary>Claims a roof. Returns false when it is taken or not allowed (the UI shows a lock).</summary>
        public bool TryClaim(int player, int roof)
        {
            if (!CanClaim(player, roof)) return false;
            if (Mode == RoofSelectMode.DuelPairs)
            {
                int leftRoof = OfferedPairs.Contains(roof) ? roof : Graph.Roof(roof).MirrorOf;
                AssignDuelPair(leftRoof);
                return true;
            }
            _claims[player] = roof;
            return true;
        }

        private void AssignDuelPair(int leftRoof)
        {
            int right = Graph.Roof(leftRoof).MirrorOf;
            for (int i = 0; i < _claims.Length; i++) _claims[i] = i % 2 == 0 ? leftRoof : right;
        }

        /// <summary>End of the selection timer: everyone without a roof gets the free roof with the fewest players nearby.</summary>
        public void AssignRemaining()
        {
            if (Mode == RoofSelectMode.DuelPairs || Mode == RoofSelectMode.Assigned && Graph.Mirrored)
            {
                if (_claims.Length > 0 && _claims[0] >= 0) return;
                AssignDuelPair(OfferedPairs.Count > 0 ? OfferedPairs[_rng.Range(0, OfferedPairs.Count)] : FirstMirrorLeft());
                return;
            }
            for (int p = 0; p < _claims.Length; p++)
            {
                if (_claims[p] >= 0) continue;
                int best = -1;
                float bestScore = float.MaxValue;
                for (int r = 0; r < Graph.Roofs.Count; r++)
                {
                    if (!CanClaim(p, r)) continue;
                    float crowd = 0f;
                    for (int q = 0; q < _claims.Length; q++)
                    {
                        if (_claims[q] < 0) continue;
                        float d = Vector3.Distance(Graph.Roof(r).Center, Graph.Roof(_claims[q]).Center);
                        crowd += 1f / (1f + d * 0.1f);
                    }
                    crowd += _rng.Range(0f, 0.01f);
                    if (crowd < bestScore) { bestScore = crowd; best = r; }
                }
                if (best < 0) best = p % Graph.Roofs.Count; // only if the map is too small (never with 2.5 roofs per player)
                _claims[p] = best;
            }
        }

        private int FirstMirrorLeft()
        {
            foreach (RoofNode r in Graph.Roofs) if (r.MirrorOf > r.Id) return r.Id;
            return 0;
        }

        /// <summary>Greedy farthest-point spread (default anchors before selection, bots' first choice).</summary>
        public static List<int> SpreadAssignment(RoofGraph g, int players)
        {
            var result = new List<int>();
            if (g.Roofs.Count == 0) return result;
            if (g.Mirrored && players == 2)
            {
                foreach (RoofNode r in g.Roofs)
                    if (r.MirrorOf > r.Id) { result.Add(r.Id); result.Add(r.MirrorOf); return result; }
            }
            int first = 0;
            for (int i = 1; i < g.Roofs.Count; i++) if (g.Roofs[i].Center.X < g.Roofs[first].Center.X) first = i;
            result.Add(first);
            while (result.Count < players)
            {
                int best = -1;
                float bestD = -1f;
                for (int r = 0; r < g.Roofs.Count; r++)
                {
                    if (result.Contains(r) && result.Count < g.Roofs.Count) continue;
                    float d = float.MaxValue;
                    foreach (int t in result) d = Math.Min(d, Vector3.Distance(g.Roofs[r].Center, g.Roofs[t].Center));
                    if (d > bestD) { bestD = d; best = r; }
                }
                result.Add(best);
            }
            return result;
        }
    }
}
