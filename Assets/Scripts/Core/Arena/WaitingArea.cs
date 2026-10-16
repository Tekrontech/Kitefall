using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kitefall.Core
{
    /// <summary>A flat walkable rectangle (roof top or bridge deck).</summary>
    public struct WalkRect
    {
        public float MinX, MaxX, MinZ, MaxZ, Y;
        public bool IsBridge;

        public bool Contains(float x, float z, float margin = 0f) =>
            x >= MinX - margin && x <= MaxX + margin && z >= MinZ - margin && z <= MaxZ + margin;

        public Vector3 Center => new Vector3((MinX + MaxX) * 0.5f, Y, (MinZ + MaxZ) * 0.5f);
        public float SizeX => MaxX - MinX;
        public float SizeZ => MaxZ - MinZ;

        public static WalkRect Around(Vector3 c, float sx, float sz, bool bridge = false) => new WalkRect
        {
            MinX = c.X - sx * 0.5f, MaxX = c.X + sx * 0.5f, MinZ = c.Z - sz * 0.5f, MaxZ = c.Z + sz * 0.5f, Y = c.Y, IsBridge = bridge
        };
    }

    /// <summary>
    /// Pre-match waiting plaza (upgrade spec F3): 3–5 flat roofs joined by small bridges, dummy target kites on poles and flags.
    /// Placed far upwind of the arena so it never touches the match world.
    /// </summary>
    public sealed class WaitingArea
    {
        public static readonly Vector3 Origin = new Vector3(0f, 12f, -220f);

        public List<WalkRect> Surfaces { get; } = new List<WalkRect>();
        public List<Vector3> SpawnPoints { get; } = new List<Vector3>();
        public List<Vector3> DummyPoles { get; } = new List<Vector3>();
        public List<Vector3> Flags { get; } = new List<Vector3>();

        public static WaitingArea Build(int seed, int players)
        {
            var w = new WaitingArea();
            var rng = new DeterministicRandom(seed * 131 + 77);
            int roofs = players <= 4 ? 3 : players <= 10 ? 4 : 5;
            float y = Origin.Y;
            var centres = new List<Vector3>();
            for (int i = 0; i < roofs; i++)
            {
                float x = Origin.X + (i - (roofs - 1) * 0.5f) * 22f;
                float z = Origin.Z + (i % 2 == 0 ? 0f : 6f);
                var c = new Vector3(x, y, z);
                centres.Add(c);
                w.Surfaces.Add(WalkRect.Around(c, rng.Range(14f, 17f), rng.Range(14f, 17f)));
                w.Flags.Add(new Vector3(c.X - 6f, y, c.Z + 6f));
                w.DummyPoles.Add(new Vector3(c.X + 5f, y, c.Z - 5f));
            }
            for (int i = 0; i + 1 < roofs; i++)
            {
                WalkRect a = w.Surfaces[i], b = w.Surfaces[i + 1];
                float z0 = Math.Max(a.MinZ, b.MinZ) + 1f, z1 = Math.Min(a.MaxZ, b.MaxZ) - 1f;
                float zc = (z0 + z1) * 0.5f;
                w.Surfaces.Add(new WalkRect { MinX = a.MaxX - 0.5f, MaxX = b.MinX + 0.5f, MinZ = zc - 1.2f, MaxZ = zc + 1.2f, Y = y, IsBridge = true });
            }
            for (int i = 0; i < Math.Max(1, players); i++)
            {
                Vector3 c = centres[i % roofs];
                int k = i / roofs;
                w.SpawnPoints.Add(new Vector3(c.X - 3f + (k % 3) * 3f, y, c.Z - 3f + (k / 3) * 3f));
            }
            return w;
        }

        public bool IsWalkable(float x, float z)
        {
            foreach (WalkRect r in Surfaces) if (r.Contains(x, z)) return true;
            return false;
        }
    }

    /// <summary>Character walking on flat surfaces (Warmup plaza and roof tops). Pure, deterministic, server-side.</summary>
    public static class CharacterMotion
    {
        public const float WalkSpeed = 2.2f;
        public const float RunSpeed = 4.6f;

        /// <summary>Moves <paramref name="pos"/> along the world-space stick (x, z) and keeps it on a surface (invisible guard rails).</summary>
        /// <returns>Speed actually moved, m/s.</returns>
        public static float Step(ref Vector3 pos, ref float yawDeg, float stickX, float stickZ, float dt, IList<WalkRect> surfaces)
        {
            float mag = KMath.Sqrt(stickX * stickX + stickZ * stickZ);
            if (mag < 0.12f || dt <= 0f) return 0f;
            if (mag > 1f) { stickX /= mag; stickZ /= mag; mag = 1f; }
            float speed = mag > 0.7f ? RunSpeed : WalkSpeed * KMath.Clamp01(mag / 0.7f) + 0.4f;
            yawDeg = KMath.Atan2(stickX, stickZ) * KMath.Rad2Deg;
            float nx = pos.X + stickX / mag * speed * dt;
            float nz = pos.Z + stickZ / mag * speed * dt;
            Vector3 before = pos;
            if (OnAny(nx, nz, surfaces, out float y)) pos = new Vector3(nx, y, nz);
            else if (OnAny(nx, pos.Z, surfaces, out y)) pos = new Vector3(nx, y, pos.Z); // slide along the rail
            else if (OnAny(pos.X, nz, surfaces, out y)) pos = new Vector3(pos.X, y, nz);
            return Vector3.Distance(before, pos) / dt;
        }

        private static bool OnAny(float x, float z, IList<WalkRect> surfaces, out float y)
        {
            for (int i = 0; i < surfaces.Count; i++)
                if (surfaces[i].Contains(x, z, -0.4f)) { y = surfaces[i].Y; return true; }
            y = 0f;
            return false;
        }
    }
}
