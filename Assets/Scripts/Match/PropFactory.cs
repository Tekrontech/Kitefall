using System.Collections.Generic;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Modelled rooftop world (upgrade spec 0.1, 10.1): houses with windows and doors, parapets with gaps where links attach,
    /// stairwell huts, water tanks, charpais, clothes lines, plant pots, dish antennas, flags, and the links between roofs –
    /// planks, rope bridges, ladders, stairs and jump gaps. All procedural, original geometry with generated textures.
    /// </summary>
    public static class PropFactory
    {
        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);
        private static readonly Color Metal = new Color(0.55f, 0.57f, 0.6f);

        // ---- Houses ------------------------------------------------------------------

        /// <summary>A house block: textured walls (plaster or brick), window rows, a door, a roof slab and a coping trim.</summary>
        public static GameObject House(Transform parent, ArenaBlock b, Color wall, int variant, bool detailed)
        {
            var root = new GameObject("House");
            root.transform.SetParent(parent, false);
            Vector3 c = new Vector3(b.Center.X, b.Center.Y, b.Center.Z);
            Vector3 size = new Vector3(b.Size.X, b.Size.Y, b.Size.Z);
            bool brick = variant % 3 == 1;
            Texture2D tex = brick ? ProceduralTextures.Brick(Color.Lerp(wall, new Color(0.72f, 0.38f, 0.28f), 0.6f)) : ProceduralTextures.Plaster(wall);
            Material wallMat = Materials.Lit(Color.white, 0.05f, tex, "wall_" + tex.name);
            var walls = MeshKit.Part(root.transform, "Walls", MeshKit.Box(size), wallMat, c);
            ScaleUv(walls.GetComponent<MeshFilter>(), size, 4f);
            // Roof slab + coping
            Material slab = Materials.Lit(Color.white, 0.05f, ProceduralTextures.Concrete(new Color(0.72f, 0.7f, 0.66f)), "roofslab");
            MeshKit.Part(root.transform, "Roof", MeshKit.Box(new Vector3(size.x + 0.3f, 0.2f, size.z + 0.3f)), slab, new Vector3(c.x, b.Max.Y + 0.1f, c.z));
            if (!detailed) return root;

            // Windows: dark glass insets with light frames, a few lit at dusk; one door facing the street (−Z).
            Material glass = Materials.Lit(new Color(0.16f, 0.22f, 0.3f), 0.85f);
            Material frame = Materials.Lit(Color.Lerp(wall, Color.white, 0.6f), 0.2f);
            Material shutter = Materials.Lit(Color.HSVToRGB((variant * 0.17f) % 1f, 0.45f, 0.6f), 0.15f);
            int floors = Mathf.Max(1, Mathf.FloorToInt(size.y / 3.1f));
            var glassParts = new List<CombineInstance>();
            var frameParts = new List<CombineInstance>();
            var rng = new DeterministicRandom(variant * 977 + (int)(c.x * 3f) + (int)(c.z * 7f));
            for (int f = 0; f < floors; f++)
            {
                float y = b.Min.Y + 1.7f + f * 3.1f;
                if (y > b.Max.Y - 0.8f) break;
                AddWindows(glassParts, frameParts, new Vector3(c.x, y, b.Min.Z - 0.02f), Vector3.right, size.x, rng, f == 0);
                AddWindows(glassParts, frameParts, new Vector3(c.x, y, b.Max.Z + 0.02f), Vector3.left, size.x, rng, false);
                AddWindows(glassParts, frameParts, new Vector3(b.Min.X - 0.02f, y, c.z), Vector3.back, size.z, rng, false);
                AddWindows(glassParts, frameParts, new Vector3(b.Max.X + 0.02f, y, c.z), Vector3.forward, size.z, rng, false);
            }
            if (glassParts.Count > 0) MeshKit.Part(root.transform, "Glass", MeshKit.Combine(glassParts), glass);
            if (frameParts.Count > 0) MeshKit.Part(root.transform, "Frames", MeshKit.Combine(frameParts), frame);
            // Door + awning
            MeshKit.Part(root.transform, "Door", MeshKit.Box(new Vector3(1.1f, 2.1f, 0.08f)), shutter, new Vector3(c.x + size.x * 0.25f, b.Min.Y + 1.05f, b.Min.Z - 0.05f));
            MeshKit.Part(root.transform, "Awning", MeshKit.Box(new Vector3(1.8f, 0.08f, 0.8f)), shutter, new Vector3(c.x + size.x * 0.25f, b.Min.Y + 2.4f, b.Min.Z - 0.4f), Quaternion.Euler(12f, 0f, 0f));
            return root;
        }

        private static void AddWindows(List<CombineInstance> glass, List<CombineInstance> frames, Vector3 center, Vector3 along, float width, DeterministicRandom rng, bool ground)
        {
            int count = Mathf.Max(1, Mathf.FloorToInt(width / 2.6f));
            Vector3 normal = Vector3.Cross(Vector3.up, along);
            Quaternion rot = Quaternion.LookRotation(-normal, Vector3.up);
            for (int i = 0; i < count; i++)
            {
                if (ground && i == count - 1) continue; // door side
                if (rng.Chance(0.15f)) continue;
                float off = (i - (count - 1) * 0.5f) * (width / count);
                Vector3 p = center + along * off;
                glass.Add(new CombineInstance { mesh = MeshKit.Cached("win_glass", () => MeshKit.Box(new Vector3(0.9f, 1.2f, 0.04f))), transform = Matrix4x4.TRS(p, rot, Vector3.one) });
                frames.Add(new CombineInstance { mesh = MeshKit.Cached("win_frame", () => WindowFrame()), transform = Matrix4x4.TRS(p, rot, Vector3.one) });
            }
        }

        private static Mesh WindowFrame()
        {
            var parts = new List<CombineInstance>
            {
                Ci(MeshKit.Box(new Vector3(1.1f, 0.1f, 0.1f)), new Vector3(0f, 0.65f, 0f)),
                Ci(MeshKit.Box(new Vector3(1.2f, 0.12f, 0.22f)), new Vector3(0f, -0.66f, -0.06f)),
                Ci(MeshKit.Box(new Vector3(0.1f, 1.3f, 0.1f)), new Vector3(-0.5f, 0f, 0f)),
                Ci(MeshKit.Box(new Vector3(0.1f, 1.3f, 0.1f)), new Vector3(0.5f, 0f, 0f)),
                Ci(MeshKit.Box(new Vector3(0.05f, 1.2f, 0.06f)), Vector3.zero),
            };
            return MeshKit.Combine(parts);
        }

        private static CombineInstance Ci(Mesh m, Vector3 p, Quaternion? r = null, Vector3? s = null) =>
            new CombineInstance { mesh = m, transform = Matrix4x4.TRS(p, r ?? Quaternion.identity, s ?? Vector3.one) };

        private static void ScaleUv(MeshFilter mf, Vector3 size, float metersPerTile)
        {
            Mesh m = Object.Instantiate(mf.sharedMesh);
            var uv = new List<Vector2>();
            m.GetUVs(0, uv);
            for (int i = 0; i < uv.Count; i++) uv[i] /= metersPerTile * 0.5f;
            m.SetUVs(0, uv);
            mf.sharedMesh = m;
        }

        // ---- Rooftops ----------------------------------------------------------------

        /// <summary>Parapet walls around a roof with gaps where links attach (spec 10.1).</summary>
        public static void Parapets(Transform parent, RoofNode roof, IList<Vector3> gaps, Material m)
        {
            float y = roof.Height + 0.45f;
            Vector3 c = new Vector3(roof.Center.X, y, roof.Center.Z);
            float hx = roof.SizeX * 0.5f, hz = roof.SizeZ * 0.5f;
            Edge(parent, c + new Vector3(0f, 0f, hz), Vector3.right, roof.SizeX, gaps, m);
            Edge(parent, c + new Vector3(0f, 0f, -hz), Vector3.right, roof.SizeX, gaps, m);
            Edge(parent, c + new Vector3(hx, 0f, 0f), Vector3.forward, roof.SizeZ, gaps, m);
            Edge(parent, c + new Vector3(-hx, 0f, 0f), Vector3.forward, roof.SizeZ, gaps, m);
        }

        private static void Edge(Transform parent, Vector3 mid, Vector3 along, float length, IList<Vector3> gaps, Material m)
        {
            // Split the wall around any gap point that lies on this edge.
            var cuts = new List<float>();
            foreach (Vector3 g in gaps)
            {
                Vector3 d = new Vector3(g.x, mid.y, g.z) - mid;
                float t = Vector3.Dot(d, along);
                float perp = (d - along * t).magnitude;
                if (perp < 0.6f && Mathf.Abs(t) < length * 0.5f) cuts.Add(t);
            }
            cuts.Sort();
            float start = -length * 0.5f;
            foreach (float cut in cuts)
            {
                Segment(parent, mid, along, start, cut - 0.8f, m);
                start = cut + 0.8f;
            }
            Segment(parent, mid, along, start, length * 0.5f, m);
        }

        private static void Segment(Transform parent, Vector3 mid, Vector3 along, float a, float b, Material m)
        {
            if (b - a < 0.2f) return;
            Vector3 center = mid + along * ((a + b) * 0.5f);
            Vector3 size = along == Vector3.right ? new Vector3(b - a, 0.9f, 0.22f) : new Vector3(0.22f, 0.9f, b - a);
            MeshKit.Part(parent, "Parapet", MeshKit.Box(size), m, center);
            MeshKit.Part(parent, "Coping", MeshKit.Box(size + new Vector3(along == Vector3.right ? 0f : 0.1f, -0.8f, along == Vector3.right ? 0.1f : 0f)), Materials.Lit(new Color(0.88f, 0.86f, 0.8f), 0.1f), center + Vector3.up * 0.5f);
        }

        /// <summary>Lived-in roof clutter: water tank, charpai, clothes line, pots, dish, stairwell hut (seeded).</summary>
        public static void RoofClutter(Transform parent, Vector3 center, float sx, float sz, float height, int seed, bool keepCenterFree)
        {
            var rng = new DeterministicRandom(seed);
            Vector3 corner(float fx, float fz) => new Vector3(center.x + fx * (sx * 0.5f - 1.2f), height, center.z + fz * (sz * 0.5f - 1.2f));
            int pick = rng.Range(0, 4);
            if (pick != 0) WaterTank(parent, corner(-1f, 1f), rng.Range(0, 3));
            if (rng.Chance(0.6f)) StairHut(parent, corner(1f, 1f) + new Vector3(-0.6f, 0f, -0.6f), rng.Range(0, 4));
            if (rng.Chance(0.55f)) Charpai(parent, corner(-1f, -1f) + new Vector3(0.6f, 0f, 0.3f), rng.Range(0f, 90f));
            if (rng.Chance(0.6f)) ClothesLine(parent, corner(-1f, 0.2f), corner(1f, 0.2f), rng);
            for (int i = 0; i < rng.Range(1, 4); i++) PlantPot(parent, corner(1f, -1f) + new Vector3(-i * 0.6f, 0f, 0.2f), rng);
            if (rng.Chance(0.4f)) Dish(parent, corner(1f, 0f), rng.Range(0f, 360f));
            if (!keepCenterFree && rng.Chance(0.4f)) Flag(parent, center + new Vector3(0f, height - center.y, 0f), Color.HSVToRGB(rng.NextFloat(), 0.7f, 0.9f));
        }

        public static void WaterTank(Transform parent, Vector3 pos, int style)
        {
            Material tank = Materials.Lit(style == 0 ? new Color(0.12f, 0.12f, 0.13f) : style == 1 ? new Color(0.25f, 0.45f, 0.75f) : new Color(0.9f, 0.88f, 0.8f), 0.35f);
            Material steel = Materials.Lit(Metal, 0.6f);
            Transform t = new GameObject("WaterTank").transform;
            t.SetParent(parent, false);
            t.position = pos;
            for (int i = 0; i < 4; i++)
                MeshKit.Part(t, "Leg", MeshKit.Cached("tankleg", () => MeshKit.Box(new Vector3(0.08f, 1.2f, 0.08f))), steel, new Vector3(i % 2 == 0 ? -0.5f : 0.5f, 0.6f, i < 2 ? -0.5f : 0.5f));
            MeshKit.Part(t, "Tank", MeshKit.Cached("tankbody", () => MeshKit.Cylinder(0.72f, 1.3f, 20)), tank, new Vector3(0f, 1.85f, 0f));
            MeshKit.Part(t, "Lid", MeshKit.Cached("tanklid", () => MeshKit.Cylinder(0.3f, 0.12f, 14)), tank, new Vector3(0f, 2.55f, 0f));
            for (int r = 0; r < 3; r++) MeshKit.Part(t, "Rib", MeshKit.Cached("tankrib", () => MeshKit.Torus(0.73f, 0.025f, 20, 4)), tank, new Vector3(0f, 1.4f + r * 0.45f, 0f));
        }

        public static void StairHut(Transform parent, Vector3 pos, int variant)
        {
            Material wall = Materials.Lit(Color.white, 0.05f, ProceduralTextures.Plaster(Color.HSVToRGB((variant * 0.21f + 0.08f) % 1f, 0.18f, 0.88f)), "hut" + variant);
            Material door = Materials.Lit(Color.HSVToRGB((variant * 0.31f) % 1f, 0.5f, 0.55f), 0.2f);
            Transform t = new GameObject("StairHut").transform;
            t.SetParent(parent, false);
            t.position = pos;
            MeshKit.Part(t, "Hut", MeshKit.Box(new Vector3(2.2f, 2.4f, 2.2f)), wall, new Vector3(0f, 1.2f, 0f));
            MeshKit.Part(t, "HutRoof", MeshKit.Box(new Vector3(2.5f, 0.15f, 2.5f)), Materials.Lit(new Color(0.6f, 0.58f, 0.55f), 0.1f), new Vector3(0f, 2.48f, 0f));
            MeshKit.Part(t, "HutDoor", MeshKit.Box(new Vector3(0.9f, 1.9f, 0.06f)), door, new Vector3(0f, 0.95f, -1.12f));
        }

        public static void Charpai(Transform parent, Vector3 pos, float yaw)
        {
            Material wood = Materials.Lit(Wood, 0.25f);
            Material weave = Materials.Lit(new Color(0.9f, 0.82f, 0.62f), 0.1f);
            Transform t = new GameObject("Charpai").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < 4; i++)
                MeshKit.Part(t, "Leg", MeshKit.Cached("charpaileg", () => MeshKit.Cylinder(0.05f, 0.45f, 8, 0.04f)), wood, new Vector3(i % 2 == 0 ? -0.45f : 0.45f, 0.22f, i < 2 ? -0.95f : 0.95f));
            MeshKit.Part(t, "FrameA", MeshKit.Cached("charpairail", () => MeshKit.Box(new Vector3(0.07f, 0.07f, 2f))), wood, new Vector3(-0.45f, 0.45f, 0f));
            MeshKit.Part(t, "FrameB", MeshKit.Cached("charpairail", () => MeshKit.Box(new Vector3(0.07f, 0.07f, 2f))), wood, new Vector3(0.45f, 0.45f, 0f));
            MeshKit.Part(t, "Weave", MeshKit.Cached("charpaiweave", () => MeshKit.Box(new Vector3(0.86f, 0.03f, 1.9f))), weave, new Vector3(0f, 0.44f, 0f));
        }

        public static void ClothesLine(Transform parent, Vector3 a, Vector3 b, DeterministicRandom rng)
        {
            a.y += 1.8f;
            b.y += 1.8f;
            Material pole = Materials.Lit(Metal, 0.5f);
            MeshKit.Part(parent, "LinePoleA", MeshKit.Cached("linepole", () => MeshKit.Cylinder(0.03f, 1.8f, 8)), pole, a - Vector3.up * 0.9f);
            MeshKit.Part(parent, "LinePoleB", MeshKit.Cached("linepole", () => MeshKit.Cylinder(0.03f, 1.8f, 8)), pole, b - Vector3.up * 0.9f);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 8; i++) { float t = i / 8f; Vector3 p = Vector3.Lerp(a, b, t); p.y -= Mathf.Sin(t * Mathf.PI) * 0.15f; pts.Add(p); }
            MeshKit.Part(parent, "Line", MeshKit.Rope(pts, 0.01f, 4), Materials.Lit(new Color(0.9f, 0.9f, 0.88f), 0.1f));
            int cloths = rng.Range(2, 6);
            for (int i = 0; i < cloths; i++)
            {
                float t = (i + 0.5f) / cloths;
                Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.15f + 0.3f);
                Color c = Color.HSVToRGB(rng.NextFloat(), rng.Range(0.3f, 0.8f), rng.Range(0.6f, 0.95f));
                MeshKit.Part(parent, "Cloth", MeshKit.Cached("cloth", () => MeshKit.Box(new Vector3(0.55f, 0.6f, 0.02f))), Materials.Lit(c, 0.05f), p, Quaternion.LookRotation(Vector3.Cross(Vector3.up, (b - a).normalized)));
            }
        }

        public static void PlantPot(Transform parent, Vector3 pos, DeterministicRandom rng)
        {
            MeshKit.Part(parent, "Pot", MeshKit.Cached("pot", () => MeshKit.Cylinder(0.18f, 0.32f, 12, 0.24f)), Materials.Lit(new Color(0.72f, 0.38f, 0.22f), 0.15f), pos + Vector3.up * 0.16f);
            MeshKit.Part(parent, "Plant", MeshKit.Cached("plant", () => MeshKit.Ellipsoid(0.3f, 0.32f, 0.3f, 10, 7)), Materials.Lit(Color.HSVToRGB(rng.Range(0.25f, 0.38f), 0.6f, 0.55f), 0.1f), pos + Vector3.up * 0.55f);
        }

        public static void Dish(Transform parent, Vector3 pos, float yaw)
        {
            Material white = Materials.Lit(new Color(0.92f, 0.92f, 0.9f), 0.4f);
            Transform t = new GameObject("Dish").transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            MeshKit.Part(t, "Mast", MeshKit.Cached("dishmast", () => MeshKit.Cylinder(0.03f, 1f, 8)), white, new Vector3(0f, 0.5f, 0f));
            MeshKit.Part(t, "Dish", MeshKit.Cached("dishbowl", () => MeshKit.Ellipsoid(0.35f, 0.1f, 0.35f, 14, 5, -90f, 0f)), white, new Vector3(0f, 1.05f, 0.05f), Quaternion.Euler(-60f, 0f, 0f));
        }

        public static void Flag(Transform parent, Vector3 pos, Color color)
        {
            MeshKit.Part(parent, "FlagPole", MeshKit.Cached("flagpole", () => MeshKit.Cylinder(0.035f, 4f, 8)), Materials.Lit(new Color(0.85f, 0.85f, 0.85f), 0.5f), pos + Vector3.up * 2f);
            MeshKit.Part(parent, "Flag", MeshKit.Cached("flagcloth", () => MeshKit.Box(new Vector3(1.1f, 0.7f, 0.02f), new Vector3(0.55f, 0f, 0f))), Materials.Lit(color, 0.05f), pos + Vector3.up * 3.6f);
        }

        // ---- Links between roofs ---------------------------------------------------------

        public static void Link(Transform parent, RoofLink link, RoofGraph g)
        {
            Vector3 a = new Vector3(link.PointA.X, link.PointA.Y, link.PointA.Z);
            Vector3 b = new Vector3(link.PointB.X, link.PointB.Y, link.PointB.Z);
            Material wood = Materials.Lit(Color.white, 0.2f, ProceduralTextures.Wood(Wood), "linkwood");
            Material rope = Materials.Lit(new Color(0.78f, 0.68f, 0.48f), 0.1f);
            Material steel = Materials.Lit(Metal, 0.55f);
            Vector3 d = b - a;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            Quaternion rot = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized) : Quaternion.identity;
            var root = new GameObject("Link_" + link.Kind).transform;
            root.SetParent(parent, false);
            switch (link.Kind)
            {
                case RoofLinkKind.Plank:
                {
                    Vector3 mid = (a + b) * 0.5f + Vector3.up * 0.06f;
                    float len = d.magnitude + 0.8f;
                    MeshKit.Part(root, "Plank", MeshKit.Box(new Vector3(0.55f, 0.08f, len)), wood, mid, Quaternion.LookRotation(d.normalized));
                    break;
                }
                case RoofLinkKind.RopeBridge:
                {
                    int slats = Mathf.Max(4, Mathf.RoundToInt(d.magnitude / 0.45f));
                    var left = new List<Vector3>();
                    var right = new List<Vector3>();
                    Vector3 side = Vector3.Cross(Vector3.up, flat.normalized) * 0.55f;
                    for (int i = 0; i <= slats; i++)
                    {
                        float t = i / (float)slats;
                        Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * Mathf.Sin(t * Mathf.PI) * d.magnitude * 0.05f;
                        if (i < slats) MeshKit.Part(root, "Slat", MeshKit.Cached("slat", () => MeshKit.Box(new Vector3(1.1f, 0.05f, 0.28f))), wood, p + Vector3.up * 0.02f, rot);
                        left.Add(p - side + Vector3.up * 1.0f);
                        right.Add(p + side + Vector3.up * 1.0f);
                    }
                    MeshKit.Part(root, "HandRopeL", MeshKit.Rope(left, 0.025f), rope);
                    MeshKit.Part(root, "HandRopeR", MeshKit.Rope(right, 0.025f), rope);
                    foreach (Vector3 end in new[] { a, b })
                        foreach (float s in new[] { -1f, 1f })
                            MeshKit.Part(root, "Post", MeshKit.Cached("bridgepost", () => MeshKit.Cylinder(0.06f, 1.2f, 8)), wood, end + side * s + Vector3.up * 0.6f);
                    break;
                }
                case RoofLinkKind.Ladder:
                {
                    // Lean the ladder from the lower edge up to the higher roof.
                    Vector3 low = a.y < b.y ? a : b, high = a.y < b.y ? b : a;
                    Vector3 bottom = new Vector3(Mathf.Lerp(low.x, high.x, 0.65f), low.y, Mathf.Lerp(low.z, high.z, 0.65f));
                    Vector3 top = high + Vector3.up * 0.9f;
                    Vector3 axis = top - bottom;
                    Quaternion lr = Quaternion.FromToRotation(Vector3.up, axis.normalized);
                    Vector3 side = Vector3.Cross(axis.normalized, Vector3.up).normalized * 0.25f;
                    foreach (float s in new[] { -1f, 1f })
                        MeshKit.Part(root, "Rail", MeshKit.Box(new Vector3(0.06f, axis.magnitude, 0.06f)), steel, (bottom + top) * 0.5f + side * s, lr);
                    int rungs = Mathf.Max(3, Mathf.RoundToInt(axis.magnitude / 0.32f));
                    for (int i = 1; i < rungs; i++)
                        MeshKit.Part(root, "Rung", MeshKit.Cached("rung", () => MeshKit.Box(new Vector3(0.5f, 0.04f, 0.04f))), steel, Vector3.Lerp(bottom, top, i / (float)rungs), Quaternion.FromToRotation(Vector3.right, side.normalized));
                    // A small walkway from the ladder foot to the lower roof edge.
                    MeshKit.Part(root, "Walk", MeshKit.Box(new Vector3(0.6f, 0.06f, Vector3.Distance(low, bottom) + 0.4f)), wood, (low + bottom) * 0.5f + Vector3.up * 0.03f, rot);
                    break;
                }
                case RoofLinkKind.Stairs:
                {
                    Vector3 low = a.y < b.y ? a : b, high = a.y < b.y ? b : a;
                    Vector3 run = high - low;
                    int steps = Mathf.Max(4, Mathf.RoundToInt(Mathf.Abs(run.y) / 0.22f));
                    Material concrete = Materials.Lit(Color.white, 0.05f, ProceduralTextures.Concrete(new Color(0.75f, 0.72f, 0.68f)), "stairs");
                    Quaternion sr = Quaternion.LookRotation(new Vector3(run.x, 0f, run.z).normalized);
                    for (int i = 0; i < steps; i++)
                    {
                        Vector3 p = low + new Vector3(run.x, 0f, run.z) * ((i + 0.5f) / steps);
                        float h = run.y * (i + 1f) / steps;
                        MeshKit.Part(root, "Step", MeshKit.Box(new Vector3(1.2f, Mathf.Max(0.1f, h), new Vector3(run.x, 0f, run.z).magnitude / steps + 0.05f)), concrete, new Vector3(p.x, low.y + h * 0.5f, p.z), sr);
                    }
                    Vector3 rail = Vector3.Cross(Vector3.up, new Vector3(run.x, 0f, run.z).normalized) * 0.65f;
                    var pts = new List<Vector3> { low + rail + Vector3.up * 0.95f, high + rail + Vector3.up * 0.95f };
                    MeshKit.Part(root, "Rail", MeshKit.Rope(pts, 0.03f), steel);
                    break;
                }
                case RoofLinkKind.Jump:
                {
                    // Painted hazard edges show the gap is jumpable.
                    Material paint = Materials.Lit(new Color(1f, 0.82f, 0.15f), 0.2f);
                    foreach (Vector3 p in new[] { a, b })
                        MeshKit.Part(root, "JumpMark", MeshKit.Cached("jumpmark", () => MeshKit.Box(new Vector3(1.2f, 0.03f, 0.25f))), paint, p + Vector3.up * 0.02f, rot);
                    break;
                }
            }
        }

        /// <summary>A target dummy kite on a pole (waiting plaza, training ground).</summary>
        public static void DummyPole(Transform parent, Vector3 pos, Color color)
        {
            MeshKit.Part(parent, "DummyPole", MeshKit.Cached("dummypole", () => MeshKit.Cylinder(0.06f, 6f, 8)), Materials.Lit(new Color(0.5f, 0.35f, 0.2f), 0.2f), pos + Vector3.up * 3f);
            Transform kite = KiteVisualFactory.Build("guddi", "", parent).transform;
            kite.position = pos + Vector3.up * 6.3f;
            kite.rotation = Quaternion.Euler(-10f, 180f, 0f);
            kite.localScale = Vector3.one * 0.9f;
            MeshKit.Part(parent, "Target", MeshKit.Cached("dummyring", () => MeshKit.Torus(0.7f, 0.05f, 24, 5)), Materials.Lit(color, 0.4f), pos + Vector3.up * 6.3f, Quaternion.Euler(90f, 0f, 0f));
        }

        /// <summary>String of festival lights between two points (waiting plaza).</summary>
        public static void FairyLights(Transform parent, Vector3 a, Vector3 b, DeterministicRandom rng)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= 10; i++) { float t = i / 10f; Vector3 p = Vector3.Lerp(a, b, t); p.y -= Mathf.Sin(t * Mathf.PI) * 0.6f; pts.Add(p); }
            MeshKit.Part(parent, "LightWire", MeshKit.Rope(pts, 0.012f, 4), Materials.Lit(new Color(0.15f, 0.15f, 0.15f), 0.3f));
            for (int i = 1; i < 10; i++)
                MeshKit.Part(parent, "Bulb", MeshKit.Cached("bulb", () => MeshKit.Ellipsoid(0.07f, 0.09f, 0.07f, 8, 5)), Materials.Lit(Color.HSVToRGB(rng.NextFloat(), 0.6f, 1f), 0.9f), pts[i] - Vector3.up * 0.08f);
        }
    }
}
