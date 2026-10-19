using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>Roof traits (upgrade spec 10.2). A roof has 0–2; never High + Upwind together.</summary>
    [Flags]
    public enum RoofTrait
    {
        None = 0,
        High = 1,
        Sheltered = 2,
        Upwind = 4,
        CrateSpot = 8,
        Edge = 16,
        Cables = 32
    }

    public enum RoofLinkKind { Plank, RopeBridge, Ladder, Stairs, Jump }

    /// <summary>One rooftop players can launch from.</summary>
    public sealed class RoofNode
    {
        public int Id;
        /// <summary>Centre of the roof surface (y = roof height).</summary>
        public Vector3 Center;
        /// <summary>Roof size (x, z), m.</summary>
        public float SizeX, SizeZ;
        public float Height => Center.Y;
        public RoofTrait Traits;
        public int Capacity = 1;
        public List<int> Links = new List<int>();
        /// <summary>For mirrored maps: the id of the identical roof on the other side, else -1.</summary>
        public int MirrorOf = -1;

        public bool Has(RoofTrait t) => (Traits & t) != 0;
        public int TraitCount
        {
            get
            {
                int n = 0, v = (int)Traits;
                while (v != 0) { n += v & 1; v >>= 1; }
                return n;
            }
        }

        /// <summary>Where the flyer stands to launch (1.35 m above the roof, like the old anchors). Slot 1 = second teammate.</summary>
        public Vector3 LaunchSpot(int slot = 0) => new Vector3(Center.X + (Capacity > 1 ? (slot == 0 ? -1.3f : 1.3f) : 0f), Center.Y + 1.35f, Center.Z);

        public bool ContainsXZ(Vector3 p, float margin = 0f) =>
            Math.Abs(p.X - Center.X) <= SizeX * 0.5f + margin && Math.Abs(p.Z - Center.Z) <= SizeZ * 0.5f + margin;
    }

    /// <summary>A walkable connection between two roofs.</summary>
    public sealed class RoofLink
    {
        public int Id;
        public int A, B;
        public RoofLinkKind Kind;
        /// <summary>Gap between the roof edges, m (≤ 18).</summary>
        public float Length;
        public float TravelTime;
        /// <summary>Edge points where the link touches roof A and roof B.</summary>
        public Vector3 PointA, PointB;

        public int Other(int roof) => roof == A ? B : A;
        public bool Touches(int roof) => roof == A || roof == B;
    }

    /// <summary>Tunable numbers for roofs (Remote Config via BalanceConfig).</summary>
    [Serializable]
    public class RoofSettings
    {
        public float minRoofs = 6f;
        public float roofsPerPlayer = 2.5f;
        public float spacing = 21f;
        /// <summary>Distance between rows along the wind (smaller keeps back roofs in the fight).</summary>
        public float rowSpacing = 16.5f;
        public float maxLinkLength = 18f;
        public int maxLinksPerRoof = 3;

        // Trait effects (spec 10.2)
        public float highStartBonus = 15f;
        public float highGustMultiplier = 1.25f;
        public float shelteredGustMultiplier = 0.6f;
        public float shelteredStartPenalty = 10f;
        public float upwindWindBonus = 0.08f;
        public float upwindBonusDuration = 120f;
        /// <summary>Balance tuning: Upwind roofs are never High (spec rule), so they get a small launch height help.</summary>
        public float upwindStartBonus = 6f;
        public float upwindApproachRange = 20f;
        public float normalApproachRange = 15f;
        public float crateSpotRadius = 40f;
        public float cableSnagChance = 0.35f;

        // Selection & hopping (spec 10.3, 10.4)
        public float selectTime = 15f;
        public float selectTimeDuel = 8f;
        public float packUpMin = 3f;
        public float packUpMax = 5f;
        public float groundedAutoPrep = 25f;
        public float teamHalfLockTime = 120f;
    }

    /// <summary>
    /// The rooftop network of an arena (upgrade spec 10.1). Generated from the arena + seed, so every client builds
    /// the same graph. Every roof is reachable from every other, 1–3 links per roof, links ≤ 18 m.
    /// </summary>
    public sealed class RoofGraph
    {
        public List<RoofNode> Roofs { get; } = new List<RoofNode>();
        public List<RoofLink> Links { get; } = new List<RoofLink>();
        public bool Mirrored { get; private set; }
        public float MinHeight { get; private set; }
        public float MaxHeight { get; private set; }
        public float MinX { get; private set; }
        public float MaxX { get; private set; }
        public float MinZ { get; private set; }
        public float MaxZ { get; private set; }

        public RoofNode Roof(int id) => Roofs[id];
        public RoofLink Link(int id) => Links[id];

        public static int RoofCountFor(int players, RoofSettings s) =>
            Math.Max((int)s.minRoofs, (int)Math.Ceiling(Math.Max(1, players) * s.roofsPerPlayer));

        public static void HeightRange(ArenaType type, out float min, out float max)
        {
            switch (type)
            {
                case ArenaType.CoastalCliffs: min = 17f; max = 26f; break;
                case ArenaType.MountainValley: min = 24f; max = 34f; break;
                case ArenaType.RoyalField: min = 2f; max = 7f; break;
                case ArenaType.RiverFestival: min = 4f; max = 11f; break;
                case ArenaType.StormArchipelago: min = 5f; max = 13f; break;
                case ArenaType.RioHillside: min = 8f; max = 22f; break;
                case ArenaType.SantiagoRooftops: min = 6f; max = 14f; break;
                case ArenaType.MediterraneanHarbour: min = 4f; max = 12f; break;
                case ArenaType.AtlanticBeach: min = 2f; max = 6f; break;
                case ArenaType.CaribbeanShore: min = 3f; max = 9f; break;
                case ArenaType.BaliTerraces: min = 12f; max = 26f; break;
                default: min = 6f; max = 17f; break;
            }
        }

        public static RoofGraph Generate(ArenaType type, int seed, int players, bool mirror, RoofSettings s = null)
        {
            s = s ?? new RoofSettings();
            var g = new RoofGraph { Mirrored = mirror };
            var rng = new DeterministicRandom(seed * 4513 + (int)type * 71 + 11);
            HeightRange(type, out float hMin, out float hMax);
            g.MinHeight = hMin;
            g.MaxHeight = hMax;

            int count = RoofCountFor(players, s);
            if (mirror && count % 2 == 1) count++;
            int rows = count <= 8 ? 2 : count <= 15 ? 3 : count <= 32 ? 4 : 5;
            int cols = (int)Math.Ceiling(count / (float)rows);
            if (mirror && cols % 2 == 1) cols++;

            // Cells used: fill row by row, centred. For mirrored maps only the left half is generated, then copied.
            var cells = new List<(int r, int c)>();
            int halfCols = mirror ? cols / 2 : cols;
            int needed = mirror ? count / 2 : count;
            for (int r = 0; r < rows && cells.Count < needed; r++)
                for (int c = 0; c < halfCols && cells.Count < needed; c++)
                    cells.Add((r, c));

            // Heights: one third low, one third mid, one third high (≥ 30 % low and ≥ 30 % high).
            var heights = new List<float>();
            for (int i = 0; i < cells.Count; i++)
            {
                int band = i % 3;
                float lo = hMin + (hMax - hMin) * band / 3f, hi = hMin + (hMax - hMin) * (band + 1) / 3f;
                heights.Add(type == ArenaType.RoyalField ? (float)Math.Round(rng.Range(lo, hi)) : rng.Range(lo, hi));
            }
            Shuffle(heights, rng);

            float spacing = s.spacing;
            float x0 = -(cols - 1) * 0.5f * spacing;
            float rowSpacing = s.rowSpacing;
            float z0 = -rowSpacing * (rows - 1) - 8f; // rows go upwind (−Z) from the front row near z = −8
            var map = new Dictionary<(int, int), int>();
            for (int i = 0; i < cells.Count; i++)
            {
                (int r, int c) = cells[i];
                var node = new RoofNode
                {
                    Id = g.Roofs.Count,
                    Center = new Vector3(x0 + c * spacing + (r % 2 == 1 && !mirror ? spacing * 0.5f : 0f) + rng.Range(-2f, 2f), heights[i], z0 + (rows - 1 - r) * rowSpacing + rng.Range(-1.5f, 1.5f)),
                    SizeX = rng.Range(9f, 12.5f),
                    SizeZ = rng.Range(9f, 12.5f)
                };
                if (mirror && c == halfCols - 1) node.Center = new Vector3(Math.Min(node.Center.X, -spacing * 0.5f), node.Center.Y, node.Center.Z);
                map[(r, c)] = node.Id;
                g.Roofs.Add(node);
            }

            // Links between neighbouring cells: spanning tree first (connected), then extras up to 3 per roof.
            var candidates = new List<(int a, int b, float w)>();
            foreach (var kv in map)
            {
                (int r, int c) = kv.Key;
                foreach ((int dr, int dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
                {
                    if (!map.TryGetValue((r + dr, c + dc), out int other)) continue;
                    bool diagonal = dr != 0 && dc != 0;
                    float gap = Gap(g.Roofs[kv.Value], g.Roofs[other]);
                    if (gap > s.maxLinkLength) continue;
                    candidates.Add((kv.Value, other, gap + (diagonal ? 6f : 0f) + rng.Range(0f, 3f)));
                }
            }
            candidates.Sort((p, q) => p.w.CompareTo(q.w));
            var parent = new int[g.Roofs.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var used = new HashSet<(int, int)>();
            foreach (var cand in candidates)
            {
                int ra = Find(cand.a), rb = Find(cand.b);
                if (ra == rb) continue;
                if (g.Roofs[cand.a].Links.Count >= s.maxLinksPerRoof || g.Roofs[cand.b].Links.Count >= s.maxLinksPerRoof) continue;
                parent[ra] = rb;
                AddLink(g, cand.a, cand.b, s);
                used.Add((cand.a, cand.b));
            }
            // Any component left over (blocked by the 3-link cap) joins through its best candidate ignoring the cap.
            foreach (var cand in candidates)
            {
                int ra = Find(cand.a), rb = Find(cand.b);
                if (ra == rb) continue;
                parent[ra] = rb;
                AddLink(g, cand.a, cand.b, s);
                used.Add((cand.a, cand.b));
            }
            foreach (var cand in candidates)
            {
                if (used.Contains((cand.a, cand.b))) continue;
                if (g.Roofs[cand.a].Links.Count >= s.maxLinksPerRoof || g.Roofs[cand.b].Links.Count >= s.maxLinksPerRoof) continue;
                if (!rng.Chance(0.45f)) continue;
                AddLink(g, cand.a, cand.b, s);
            }

            if (mirror) MirrorRight(g, s, map, halfCols);
            g.UpdateBounds();
            AssignTraits(g, type, rng);
            return g;
        }

        private static void MirrorRight(RoofGraph g, RoofSettings s, Dictionary<(int, int), int> map, int halfCols)
        {
            int leftCount = g.Roofs.Count;
            for (int i = 0; i < leftCount; i++)
            {
                RoofNode l = g.Roofs[i];
                var r = new RoofNode
                {
                    Id = g.Roofs.Count,
                    Center = new Vector3(-l.Center.X, l.Center.Y, l.Center.Z),
                    SizeX = l.SizeX,
                    SizeZ = l.SizeZ,
                    MirrorOf = l.Id
                };
                l.MirrorOf = r.Id;
                g.Roofs.Add(r);
            }
            int leftLinks = g.Links.Count;
            for (int i = 0; i < leftLinks; i++)
            {
                RoofLink l = g.Links[i];
                AddLink(g, g.Roofs[l.A].MirrorOf, g.Roofs[l.B].MirrorOf, s);
            }
            // Bridges across the middle: each inner-column roof to its mirror (mirror pairs stay symmetric).
            foreach (var kv in map)
            {
                if (kv.Key.Item2 != halfCols - 1) continue;
                RoofNode l = g.Roofs[kv.Value];
                AddLink(g, l.Id, l.MirrorOf, s);
            }
        }

        private void UpdateBounds()
        {
            MinX = MinZ = float.MaxValue;
            MaxX = MaxZ = float.MinValue;
            foreach (RoofNode r in Roofs)
            {
                MinX = Math.Min(MinX, r.Center.X - r.SizeX * 0.5f);
                MaxX = Math.Max(MaxX, r.Center.X + r.SizeX * 0.5f);
                MinZ = Math.Min(MinZ, r.Center.Z - r.SizeZ * 0.5f);
                MaxZ = Math.Max(MaxZ, r.Center.Z + r.SizeZ * 0.5f);
            }
        }

        private static void AssignTraits(RoofGraph g, ArenaType type, DeterministicRandom rng)
        {
            float width = Math.Max(1f, g.MaxX - g.MinX), depth = Math.Max(1f, g.MaxZ - g.MinZ);
            var crateCandidates = new List<int>();
            foreach (RoofNode r in g.Roofs)
            {
                if (r.MirrorOf >= 0 && r.MirrorOf < r.Id) continue; // mirrored roofs copy their twin below
                RoofTrait t = RoofTrait.None;
                if (r.Height >= g.MaxHeight - 3f) t |= RoofTrait.High;
                // Upwind = smallest Z quarter (wind blows toward +Z).
                if (r.Center.Z <= g.MinZ + depth * 0.25f && (t & RoofTrait.High) == 0) t |= RoofTrait.Upwind;
                if (r.Center.X <= g.MinX + width * 0.15f || r.Center.X >= g.MaxX - width * 0.15f) t |= RoofTrait.Edge;
                int taller = 0;
                foreach (RoofNode o in g.Roofs)
                    if (o != r && Vector2.Distance(new Vector2(o.Center.X, o.Center.Z), new Vector2(r.Center.X, r.Center.Z)) <= 24f && o.Height > r.Height + 2f) taller++;
                if (taller >= 3 && (t & RoofTrait.High) == 0) t |= RoofTrait.Sheltered;
                if ((type == ArenaType.OldCityRooftops || type == ArenaType.SantiagoRooftops) && rng.Chance(0.2f)) t |= RoofTrait.Cables;
                r.Traits = Limit(t);
                if (r.TraitCount < 2) crateCandidates.Add(r.Id);
            }
            // 1–2 crate spots.
            int crates = g.Roofs.Count >= 12 ? 2 : 1;
            for (int i = 0; i < crates && crateCandidates.Count > 0; i++)
            {
                int pick = crateCandidates[rng.Range(0, crateCandidates.Count)];
                crateCandidates.Remove(pick);
                g.Roofs[pick].Traits |= RoofTrait.CrateSpot;
            }
            foreach (RoofNode r in g.Roofs)
                if (r.MirrorOf >= 0 && r.MirrorOf < r.Id) r.Traits = g.Roofs[r.MirrorOf].Traits;
        }

        /// <summary>Keeps at most 2 traits and never High + Upwind (priority: High, Sheltered, Upwind, Cables, Edge).</summary>
        private static RoofTrait Limit(RoofTrait t)
        {
            if ((t & RoofTrait.High) != 0) t &= ~RoofTrait.Upwind;
            RoofTrait result = RoofTrait.None;
            int n = 0;
            foreach (RoofTrait k in new[] { RoofTrait.High, RoofTrait.Sheltered, RoofTrait.Upwind, RoofTrait.Cables, RoofTrait.Edge, RoofTrait.CrateSpot })
            {
                if ((t & k) == 0 || n >= 2) continue;
                result |= k;
                n++;
            }
            return result;
        }

        private static float Gap(RoofNode a, RoofNode b)
        {
            float dx = Math.Abs(b.Center.X - a.Center.X) - (a.SizeX + b.SizeX) * 0.5f;
            float dz = Math.Abs(b.Center.Z - a.Center.Z) - (a.SizeZ + b.SizeZ) * 0.5f;
            dx = Math.Max(0f, dx);
            dz = Math.Max(0f, dz);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static void AddLink(RoofGraph g, int a, int b, RoofSettings s)
        {
            if (a == b) return;
            foreach (int l in g.Roofs[a].Links) if (g.Links[l].Touches(b)) return;
            RoofNode ra = g.Roofs[a], rb = g.Roofs[b];
            float gap = Gap(ra, rb);
            float dh = Math.Abs(ra.Height - rb.Height);
            RoofLinkKind kind;
            if (dh > 4f) kind = gap < 8f ? RoofLinkKind.Ladder : RoofLinkKind.Stairs;
            else if (gap < 2.5f) kind = RoofLinkKind.Jump;
            else if (gap < 10f) kind = RoofLinkKind.Plank;
            else kind = RoofLinkKind.RopeBridge;
            float extra = kind == RoofLinkKind.RopeBridge ? 1.5f : kind == RoofLinkKind.Ladder ? 1f : kind == RoofLinkKind.Jump ? -0.5f : 0f;
            var link = new RoofLink
            {
                Id = g.Links.Count,
                A = a,
                B = b,
                Kind = kind,
                Length = gap,
                TravelTime = KMath.Clamp(2.5f + gap * 0.25f + dh * 0.35f + extra, 3f, 10f),
                PointA = EdgePoint(ra, rb),
                PointB = EdgePoint(rb, ra)
            };
            g.Links.Add(link);
            ra.Links.Add(link.Id);
            rb.Links.Add(link.Id);
        }

        /// <summary>Point on the edge of roof <paramref name="from"/> facing roof <paramref name="to"/>.</summary>
        private static Vector3 EdgePoint(RoofNode from, RoofNode to)
        {
            float dx = to.Center.X - from.Center.X, dz = to.Center.Z - from.Center.Z;
            float tx = Math.Abs(dx) < 1e-3f ? float.MaxValue : from.SizeX * 0.5f / Math.Abs(dx);
            float tz = Math.Abs(dz) < 1e-3f ? float.MaxValue : from.SizeZ * 0.5f / Math.Abs(dz);
            float t = Math.Min(tx, tz);
            return new Vector3(from.Center.X + dx * t, from.Center.Y, from.Center.Z + dz * t);
        }

        private static void Shuffle<T>(List<T> list, DeterministicRandom rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---- Queries ---------------------------------------------------------

        public bool IsConnected()
        {
            if (Roofs.Count == 0) return true;
            var seen = new bool[Roofs.Count];
            var stack = new Stack<int>();
            stack.Push(0);
            seen[0] = true;
            int n = 1;
            while (stack.Count > 0)
            {
                int r = stack.Pop();
                foreach (int l in Roofs[r].Links)
                {
                    int o = Links[l].Other(r);
                    if (seen[o]) continue;
                    seen[o] = true;
                    n++;
                    stack.Push(o);
                }
            }
            return n == Roofs.Count;
        }

        /// <summary>The link between two roofs, or null.</summary>
        public RoofLink LinkBetween(int a, int b)
        {
            foreach (int l in Roofs[a].Links) if (Links[l].Touches(b)) return Links[l];
            return null;
        }

        /// <summary>Shortest path (fewest seconds) as a list of link ids, empty if already there.</summary>
        public List<int> Path(int from, int to)
        {
            var result = new List<int>();
            if (from == to) return result;
            var dist = new float[Roofs.Count];
            var via = new int[Roofs.Count];
            var done = new bool[Roofs.Count];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; via[i] = -1; }
            dist[from] = 0f;
            for (int it = 0; it < Roofs.Count; it++)
            {
                int best = -1;
                for (int i = 0; i < dist.Length; i++) if (!done[i] && (best < 0 || dist[i] < dist[best])) best = i;
                if (best < 0 || dist[best] == float.MaxValue) break;
                done[best] = true;
                foreach (int l in Roofs[best].Links)
                {
                    int o = Links[l].Other(best);
                    float d = dist[best] + Links[l].TravelTime;
                    if (d < dist[o]) { dist[o] = d; via[o] = l; }
                }
            }
            int cur = to;
            while (cur != from && via[cur] >= 0)
            {
                result.Insert(0, via[cur]);
                cur = Links[via[cur]].Other(cur);
            }
            return cur == from ? result : new List<int>();
        }

        /// <summary>The roof whose surface contains this point (x, z), or -1.</summary>
        public int RoofAt(Vector3 p, float margin = 0.5f)
        {
            for (int i = 0; i < Roofs.Count; i++) if (Roofs[i].ContainsXZ(p, margin)) return i;
            return -1;
        }
    }
}
