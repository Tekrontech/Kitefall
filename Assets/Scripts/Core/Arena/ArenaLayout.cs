using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    public enum ArenaType
    {
        OldCityRooftops,
        CoastalCliffs,
        RiverFestival,
        RoyalField,
        MountainValley,
        StormArchipelago
    }

    public enum BlockKind { House, Tower, Cliff, Tree, Pole, Stall }

    /// <summary>A solid box in the arena (building, cliff…).</summary>
    public struct ArenaBlock
    {
        public Vector3 Min;
        public Vector3 Max;
        public BlockKind Kind;
        public int Variant;

        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Size => Max - Min;

        public bool Contains(Vector3 p) =>
            p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;
    }

    /// <summary>
    /// Deterministic arena: flyer rooftops (anchors) and solid blocks. Generated from the arena type + match seed,
    /// so the server and all clients build the same world without sending it (spec 11).
    /// Downwind is roughly +Z; the match wind is randomised a little around it.
    /// </summary>
    public sealed class ArenaLayout
    {
        public ArenaType Type { get; }
        public int Seed { get; }
        public List<Vector3> Anchors { get; } = new List<Vector3>();
        public List<ArenaBlock> Blocks { get; } = new List<ArenaBlock>();
        /// <summary>Centre of the flying area (crates and storm zone use it).</summary>
        public Vector3 SkyCenter { get; private set; }
        /// <summary>Base wind direction for this arena (deg, toward).</summary>
        public float BaseWindDirection { get; private set; }
        /// <summary>Base wind speed multiplier for this arena (coast = strong steady wind).</summary>
        public float WindStrength { get; private set; } = 1f;
        public float WindVariation { get; private set; } = 1f;
        public float GroundHeight => 0f;

        private ArenaLayout(ArenaType type, int seed)
        {
            Type = type;
            Seed = seed;
        }

        public bool IsSolid(Vector3 p)
        {
            if (p.Y < GroundHeight) return true;
            for (int i = 0; i < Blocks.Count; i++)
                if (Blocks[i].Contains(p)) return true;
            return false;
        }

        public static string DisplayName(ArenaType type)
        {
            switch (type)
            {
                case ArenaType.OldCityRooftops: return "Old City Rooftops";
                case ArenaType.CoastalCliffs: return "Coastal Cliffs";
                case ArenaType.RiverFestival: return "River Festival";
                case ArenaType.RoyalField: return "Royal Field";
                case ArenaType.MountainValley: return "Mountain Valley";
                default: return "Storm Archipelago";
            }
        }

        public static ArenaLayout Generate(ArenaType type, int seed, int playerCount)
        {
            var a = new ArenaLayout(type, seed);
            var rng = new DeterministicRandom(seed * 7919 + (int)type);
            playerCount = Math.Max(1, playerCount);

            float roofHeight;
            switch (type)
            {
                case ArenaType.CoastalCliffs: roofHeight = 22f; a.WindStrength = 1.3f; a.WindVariation = 0.4f; break;
                case ArenaType.MountainValley: roofHeight = 30f; a.WindStrength = 1.1f; a.WindVariation = 1.4f; break;
                case ArenaType.RoyalField: roofHeight = 3f; a.WindStrength = 1f; a.WindVariation = 0.8f; break;
                case ArenaType.RiverFestival: roofHeight = 6f; a.WindStrength = 1f; a.WindVariation = 1f; break;
                case ArenaType.StormArchipelago: roofHeight = 8f; a.WindStrength = 1.2f; a.WindVariation = 1.2f; break;
                default: roofHeight = 10f; a.WindStrength = 1f; a.WindVariation = 1.3f; break;
            }
            a.BaseWindDirection = 0f;

            // ---- Flyer positions: a row across the wind (duel = 2 roofs ~44 m apart), more rows for big modes.
            int perRow = playerCount <= 8 ? playerCount : 10;
            float spacing = playerCount <= 2 ? 44f : 26f;
            for (int i = 0; i < playerCount; i++)
            {
                int row = i / perRow;
                int col = i % perRow;
                int inRow = Math.Min(perRow, playerCount - row * perRow);
                float x = (col - (inRow - 1) * 0.5f) * spacing;
                float z = -row * 30f;
                float h = roofHeight + rng.Range(-1.5f, 1.5f);
                if (type == ArenaType.RoyalField) h = roofHeight;
                a.Anchors.Add(new Vector3(x, h + 1.35f, z));
                // The flyer's own building / platform.
                a.Blocks.Add(Box(new Vector3(x, h * 0.5f, z), new Vector3(9f, h, 9f), type == ArenaType.CoastalCliffs ? BlockKind.Cliff : BlockKind.House, rng.Range(0, 4)));
            }

            float width = Math.Max(120f, perRow * spacing + 60f);

            // ---- Neighbourhood around the flyers (upwind / sides only, so launches are clear).
            switch (type)
            {
                case ArenaType.OldCityRooftops:
                case ArenaType.RiverFestival:
                case ArenaType.StormArchipelago:
                    for (float x = -width * 0.6f; x <= width * 0.6f; x += 12f)
                        for (float z = -60f; z <= -14f; z += 12f)
                        {
                            if (rng.Chance(0.15f)) continue;
                            if (NearAnchor(a, x, z, 11f)) continue;
                            float h = rng.Range(6f, roofHeight + 4f);
                            a.Blocks.Add(Box(new Vector3(x + rng.Range(-1f, 1f), h * 0.5f, z), new Vector3(10f, h, 10f), BlockKind.House, rng.Range(0, 4)));
                        }
                    break;
                case ArenaType.CoastalCliffs:
                    a.Blocks.Add(Box(new Vector3(0f, roofHeight * 0.5f - 1f, -40f), new Vector3(width + 80f, roofHeight - 2f, 70f), BlockKind.Cliff, 0));
                    break;
                case ArenaType.MountainValley:
                    a.Blocks.Add(Box(new Vector3(0f, roofHeight * 0.5f - 1f, -45f), new Vector3(width + 80f, roofHeight - 2f, 80f), BlockKind.Cliff, 1));
                    for (int i = 0; i < 16; i++)
                    {
                        float side = i % 2 == 0 ? -1f : 1f;
                        float h = rng.Range(60f, 140f);
                        a.Blocks.Add(Box(new Vector3(side * rng.Range(width * 0.6f + 120f, width * 0.6f + 260f), h * 0.5f, rng.Range(0f, 600f)), new Vector3(rng.Range(60f, 120f), h, rng.Range(60f, 120f)), BlockKind.Cliff, 1));
                    }
                    break;
                case ArenaType.RoyalField:
                    for (int i = 0; i < 24; i++)
                        a.Blocks.Add(Box(new Vector3(rng.Range(-width, width), 4f, rng.Range(-80f, -20f)), new Vector3(3f, 8f, 3f), BlockKind.Tree, 0));
                    break;
            }

            // ---- Downwind obstacles: low enough to fly over, high enough to crash into when you get careless.
            int downwindCount = type == ArenaType.RoyalField ? 6 : type == ArenaType.CoastalCliffs ? 0 : 28;
            for (int i = 0; i < downwindCount; i++)
            {
                float z = rng.Range(60f, 420f);
                float x = rng.Range(-width * 0.8f, width * 0.8f);
                float h = type == ArenaType.RoyalField ? rng.Range(4f, 7f) : rng.Range(5f, 13f);
                var kind = type == ArenaType.RoyalField ? BlockKind.Stall : BlockKind.House;
                a.Blocks.Add(Box(new Vector3(x, h * 0.5f, z), new Vector3(rng.Range(8f, 14f), h, rng.Range(8f, 14f)), kind, rng.Range(0, 4)));
            }
            if (type == ArenaType.OldCityRooftops)
            {
                // Power-cable poles between roofs (spec 11).
                for (int i = 0; i < 8; i++)
                    a.Blocks.Add(Box(new Vector3(rng.Range(-width * 0.5f, width * 0.5f), 7f, rng.Range(25f, 140f)), new Vector3(0.5f, 14f, 0.5f), BlockKind.Pole, 0));
            }

            Vector3 centroid = Vector3.Zero;
            foreach (Vector3 anchor in a.Anchors) centroid += anchor;
            centroid /= a.Anchors.Count;
            a.SkyCenter = new Vector3(centroid.X, 60f, centroid.Z + 140f);
            return a;
        }

        private static bool NearAnchor(ArenaLayout a, float x, float z, float range)
        {
            foreach (Vector3 anchor in a.Anchors)
                if (Math.Abs(anchor.X - x) < range && Math.Abs(anchor.Z - z) < range) return true;
            return false;
        }

        private static ArenaBlock Box(Vector3 center, Vector3 size, BlockKind kind, int variant) => new ArenaBlock
        {
            Min = center - size * 0.5f,
            Max = center + size * 0.5f,
            Kind = kind,
            Variant = variant
        };
    }
}
