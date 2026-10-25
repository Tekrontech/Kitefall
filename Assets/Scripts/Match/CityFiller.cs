using System.Collections.Generic;
using Kitefall.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kitefall.Match
{
    /// <summary>
    /// Fills the empty ground around the arena with a dense, lived-in town so the view from the roof looks like a real city:
    /// blocks of 2–7 storey houses with windows, shutters and balconies (one facade texture, tinted per arena palette),
    /// narrow alleys, roof parapets, water tanks and stair huts, fading into the haze. Visual only – the server never sees it,
    /// and it keeps clear of the flyers' roofs, the sea / river and the low airspace right downwind of the launch roofs.
    /// Everything is merged into a few big meshes (one per material and distance band) so it stays cheap to draw.
    /// When artist houses exist (house_01…house_08 in Assets/Art/World, see <see cref="WorldAssetLibrary"/>) the near band uses
    /// them instead – capped at a few hundred instances – and the far band stays merged generated meshes. The arena root is
    /// static-batched afterwards (ArenaBuilder).
    /// </summary>
    public static class CityFiller
    {
        private const float Cell = 18f, Alley = 3f, Floor = 3f;
        /// <summary>Most artist models (houses + their roof props) placed in the near band; the rest stay generated.</summary>
        public const int MaxNearModels = 300, MaxNearModelsMobile = 180;

        private sealed class Bucket
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();
            public Material Mat;
            public bool Shadows;
        }

        private struct Rect2 { public float x0, z0, x1, z1; }

        public static void Build(Transform parent, ArenaLayout layout, ArenaLook look, Color[] palette, Color far)
        {
            float density, radius;
            int minFloors, maxFloors;
            switch (layout.Type)
            {
                case ArenaType.OldCityRooftops:
                case ArenaType.RiverFestival:
                case ArenaType.SantiagoRooftops:
                case ArenaType.MediterraneanHarbour:
                    density = 0.9f; minFloors = 2; maxFloors = 6; break;
                case ArenaType.RioHillside:
                    density = 0.85f; minFloors = 1; maxFloors = 4; break;
                case ArenaType.CaribbeanShore:
                case ArenaType.AtlanticBeach:
                case ArenaType.BaliTerraces:
                    density = 0.35f; minFloors = 1; maxFloors = 2; break;
                default:
                    return; // open field, cliffs, mountains, islands: no town
            }
            bool mobile = Application.isMobilePlatform;
            radius = mobile ? 320f : 430f;
            float nearBand = mobile ? 150f : 210f;

            // Flyers' centre and the typical roof height.
            Vector3 centre = Vector3.zero;
            foreach (System.Numerics.Vector3 a in layout.Anchors) centre += new Vector3(a.X, 0f, a.Z);
            if (layout.Anchors.Count > 0) centre /= layout.Anchors.Count;
            float roofRef = 0f;
            int houses = 0;
            var taken = new List<Rect2>();
            foreach (ArenaBlock b in layout.Blocks)
            {
                if (b.Kind == BlockKind.House) { roofRef += b.Max.Y; houses++; }
                float pad = b.Kind == BlockKind.Tree || b.Kind == BlockKind.Pole ? 1.5f : 3f;
                taken.Add(new Rect2 { x0 = b.Min.X - pad, z0 = b.Min.Z - pad, x1 = b.Max.X + pad, z1 = b.Max.Z + pad });
            }
            roofRef = houses > 0 ? roofRef / houses : 10f;
            float launchZ = centre.z;
            if (layout.Roofs != null)
            {
                // Generous clearance round every flyer roof (camera sits a few metres behind the flyer).
                foreach (RoofNode r in layout.Roofs.Roofs)
                    taken.Add(new Rect2 { x0 = r.Center.X - r.SizeX * 0.5f - 8f, z0 = r.Center.Z - r.SizeZ * 0.5f - 8f, x1 = r.Center.X + r.SizeX * 0.5f + 8f, z1 = r.Center.Z + r.SizeZ * 0.5f + 8f });
                launchZ = layout.Roofs.MaxZ;
            }
            else
                foreach (System.Numerics.Vector3 a in layout.Anchors) launchZ = Mathf.Max(launchZ, a.Z);

            // Materials: one facade + one plain trim per palette colour, shared roof / tank materials.
            Texture2D facadeTex = ProceduralTextures.Facade();
            Texture2D trimTex = ProceduralTextures.Plaster(new Color(0.93f, 0.91f, 0.87f));
            Texture2D roofTex = ProceduralTextures.Concrete(new Color(0.7f, 0.68f, 0.64f));
            int colours = Mathf.Max(1, palette.Length);
            var facadeNear = new Bucket[colours];
            var facadeFar = new Bucket[colours];
            var trim = new Bucket[colours];
            for (int i = 0; i < colours; i++)
            {
                Color c = palette.Length > 0 ? palette[i] : new Color(0.86f, 0.8f, 0.7f);
                Material fm = Materials.Lit(c, 0.05f, facadeTex, "cityfacade" + ColorUtility.ToHtmlStringRGB(c), doubleSided: false);
                facadeNear[i] = new Bucket { Mat = fm, Shadows = true };
                facadeFar[i] = new Bucket { Mat = fm, Shadows = false };
                trim[i] = new Bucket { Mat = Materials.Lit(c, 0.05f, trimTex, "citytrim" + ColorUtility.ToHtmlStringRGB(c), doubleSided: false), Shadows = true };
            }
            var roofNear = new Bucket { Mat = Materials.Lit(Color.white, 0.05f, roofTex, "cityroof", doubleSided: false), Shadows = true };
            var roofFar = new Bucket { Mat = roofNear.Mat, Shadows = false };
            var tankBlue = new Bucket { Mat = Materials.Lit(new Color(0.16f, 0.36f, 0.72f), 0.35f, null, "citytankblue", doubleSided: false), Shadows = true };
            var tankDark = new Bucket { Mat = Materials.Lit(new Color(0.12f, 0.12f, 0.13f), 0.3f, null, "citytankdark", doubleSided: false), Shadows = true };
            Color haze = Color.Lerp(far, new Color(0.85f, 0.85f, 0.88f), 0.25f);
            var silhouette = new Bucket { Mat = Materials.Lit(haze, 0f, facadeTex, "cityfar" + ColorUtility.ToHtmlStringRGB(haze), doubleSided: false), Shadows = false };

            var rng = new System.Random(layout.Seed * 7919 + (int)layout.Type);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Artist houses / props for the near band (instances, capped); created under the Town root below.
            var root = new GameObject("Town").transform;
            root.SetParent(parent, false);
            int modelBudget = WorldAssetLibrary.HouseCount > 0 ? (mobile ? MaxNearModelsMobile : MaxNearModels) : 0;
            GameObject tankModel = WorldAssetLibrary.Get(WorldAssetLibrary.WaterTank);
            GameObject hutModel = WorldAssetLibrary.Get(WorldAssetLibrary.Stairwell);

            for (float cx = -radius; cx <= radius; cx += Cell)
                for (float cz = -radius; cz <= radius; cz += Cell)
                {
                    float d = Mathf.Sqrt(cx * cx + cz * cz);
                    if (d > radius) continue;
                    float x = centre.x + cx, z = centre.z + cz;
                    if (Wet(layout.Type, look, x, z)) continue;
                    if (rng.NextDouble() > density) continue;

                    // 1, 2 or 4 lots per block, separated by alleys.
                    int split = rng.NextDouble() < 0.35 ? 1 : rng.NextDouble() < 0.5 ? 2 : 4;
                    float inner = Cell - Alley;
                    for (int lot = 0; lot < split; lot++)
                    {
                        float lx0 = x - inner * 0.5f, lz0 = z - inner * 0.5f, lw = inner, ld = inner;
                        if (split == 2) { lw = inner * 0.5f - 0.6f; lx0 += lot * (inner * 0.5f + 0.6f); }
                        else if (split == 4) { lw = ld = inner * 0.5f - 0.6f; lx0 += lot % 2 * (inner * 0.5f + 0.6f); lz0 += lot / 2 * (inner * 0.5f + 0.6f); }
                        // A little irregularity: set back from the street, uneven widths.
                        float sx = R(0f, 1.2f), sz = R(0f, 1.2f);
                        var rect = new Rect2 { x0 = lx0 + sx, z0 = lz0 + sz, x1 = lx0 + lw - R(0f, 1f), z1 = lz0 + ld - R(0f, 1f) };
                        if (rect.x1 - rect.x0 < 4f || rect.z1 - rect.z0 < 4f) continue;
                        if (Overlaps(taken, rect)) continue;

                        int floors = Mathf.RoundToInt(R(minFloors, maxFloors + (d > 220f ? 1.5f : 0f)));
                        float h = floors * Floor + R(0.2f, 0.6f);
                        // Low town right downwind of the launch roofs: the kites fly over it, nothing pokes into the low airspace.
                        if (z > launchZ - 5f && d < 260f) h = Mathf.Min(h, Mathf.Max(2f * Floor, roofRef * 0.85f));
                        int ci = rng.Next(colours);
                        bool near = d < nearBand;
                        Vector3 min = new Vector3(rect.x0, 0f, rect.z0), max = new Vector3(rect.x1, h, rect.z1);
                        float uOff = (float)rng.Next(4) * 3f;
                        int variant = rng.Next(1000); // which house model (drawn for every lot, so lots further on do not shift)
                        bool modelled = near && modelBudget > 0 &&
                                        PropFactory.PlaceHouse(WorldAssetLibrary.HouseFor(variant), root, new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f), max - min, variant) != null;
                        if (modelled) modelBudget--;
                        else
                        {
                            AddWalls(near ? facadeNear[ci] : facadeFar[ci], min, max, 12f, 12f, uOff);
                            AddTop(near ? roofNear : roofFar, min, max, 8f);
                        }
                        if (!near) continue;

                        // Roof life: parapet lip, water tank, stair hut.
                        const float t = 0.22f, ph = 0.8f;
                        Bucket tr = trim[ci];
                        AddBox(tr, new Vector3(min.x, h, min.z), new Vector3(max.x, h + ph, min.z + t), 4f);
                        AddBox(tr, new Vector3(min.x, h, max.z - t), new Vector3(max.x, h + ph, max.z), 4f);
                        AddBox(tr, new Vector3(min.x, h, min.z + t), new Vector3(min.x + t, h + ph, max.z - t), 4f);
                        AddBox(tr, new Vector3(max.x - t, h, min.z + t), new Vector3(max.x, h + ph, max.z - t), 4f);
                        if (rng.NextDouble() < 0.45)
                        {
                            Vector3 p = new Vector3(R(min.x + 1.5f, max.x - 1.5f), h, R(min.z + 1.5f, max.z - 1.5f));
                            bool blue = rng.NextDouble() < 0.6;
                            float tr0 = R(0.55f, 0.85f), th = R(1.1f, 1.6f);
                            if (tankModel != null && modelBudget > 0 && WorldAssetLibrary.Place(tankModel, "WaterTank", root, p, Quaternion.Euler(0f, R(0f, 360f), 0f),
                                    new Vector3(0f, th + 0.9f, 0f), WorldAssetLibrary.Fit.Uniform) != null)
                                modelBudget--;
                            else
                            {
                                AddCylinder(blue ? tankBlue : tankDark, p + Vector3.up * 0.9f, tr0, th, 10);
                                AddBox(tankDark, p + new Vector3(-0.6f, 0f, -0.6f), p + new Vector3(0.6f, 0.9f, 0.6f), 4f);
                            }
                        }
                        if (rng.NextDouble() < 0.3)
                        {
                            Vector3 p = new Vector3(rng.NextDouble() < 0.5 ? min.x + 0.4f : max.x - 2.6f, h, rng.NextDouble() < 0.5 ? min.z + 0.4f : max.z - 2.9f);
                            if (hutModel != null && modelBudget > 0 && WorldAssetLibrary.Place(hutModel, "StairHut", root, p + new Vector3(1.1f, 0f, 1.25f), Quaternion.identity,
                                    new Vector3(0f, 2.5f, 0f), WorldAssetLibrary.Fit.Uniform) != null)
                                modelBudget--;
                            else
                            {
                                AddBox(tr, p, p + new Vector3(2.2f, 2.4f, 2.5f), 4f);
                                AddTop(roofNear, p, p + new Vector3(2.2f, 2.4f, 2.5f), 8f);
                            }
                        }
                    }
                }

            // Distant skyline ring (replaces plain grey boxes): tall, faded blocks with window rows, lost in the haze.
            for (int i = 0; i < 70; i++)
            {
                float a = i / 70f * Mathf.PI * 2f + R(-0.03f, 0.03f);
                float dd = R(radius + 180f, radius + 520f);
                Vector3 c = centre + new Vector3(Mathf.Sin(a) * dd, 0f, Mathf.Cos(a) * dd);
                if (Wet(layout.Type, look, c.x, c.z)) continue;
                float w = R(30f, 70f), dep = R(25f, 60f), h = layout.Type == ArenaType.BaliTerraces ? R(8f, 20f) : R(18f, 55f);
                AddWalls(silhouette, new Vector3(c.x - w * 0.5f, 0f, c.z - dep * 0.5f), new Vector3(c.x + w * 0.5f, h, c.z + dep * 0.5f), 12f, 12f, 0f);
                AddTop(silhouette, new Vector3(c.x - w * 0.5f, 0f, c.z - dep * 0.5f), new Vector3(c.x + w * 0.5f, h, c.z + dep * 0.5f), 40f);
            }

            var all = new List<Bucket>();
            all.AddRange(facadeNear); all.AddRange(facadeFar); all.AddRange(trim);
            all.Add(roofNear); all.Add(roofFar); all.Add(tankBlue); all.Add(tankDark); all.Add(silhouette);
            foreach (Bucket b in all) Emit(root, b);
        }

        /// <summary>Sea, river and open water stay open.</summary>
        private static bool Wet(ArenaType type, ArenaLook look, float x, float z)
        {
            if (look.SeaDownwind && z > 92f) return true;
            if (type == ArenaType.RiverFestival && Mathf.Abs(z - 160f) < 46f) return true;
            return false;
        }

        private static bool Overlaps(List<Rect2> taken, Rect2 r)
        {
            foreach (Rect2 t in taken)
                if (r.x0 < t.x1 && r.x1 > t.x0 && r.z0 < t.z1 && r.z1 > t.z0) return true;
            return false;
        }

        // ---- Mesh building (faces wound clockwise as seen from outside, Unity's front face) ----------------------------

        private static void Quad(Bucket b, Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector2 uvBl, Vector2 uvTr)
        {
            int s = b.V.Count;
            Vector3 n = Vector3.Cross(tl - bl, br - bl).normalized;
            b.V.Add(bl); b.V.Add(br); b.V.Add(tr); b.V.Add(tl);
            for (int i = 0; i < 4; i++) b.N.Add(n);
            b.UV.Add(uvBl); b.UV.Add(new Vector2(uvTr.x, uvBl.y)); b.UV.Add(uvTr); b.UV.Add(new Vector2(uvBl.x, uvTr.y));
            b.T.Add(s); b.T.Add(s + 3); b.T.Add(s + 2);
            b.T.Add(s); b.T.Add(s + 2); b.T.Add(s + 1);
        }

        /// <summary>Four walls; UVs in metres / scale so window rows line up with storeys (v = 0 at the ground).</summary>
        private static void AddWalls(Bucket b, Vector3 min, Vector3 max, float uScale, float vScale, float uOff)
        {
            float v0 = min.y / vScale, v1 = max.y / vScale;
            float wx = max.x - min.x, wz = max.z - min.z;
            Quad(b, new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector2(uOff / uScale, v0), new Vector2((uOff + wx) / uScale, v1));
            Quad(b, new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector2(uOff / uScale, v0), new Vector2((uOff + wx) / uScale, v1));
            Quad(b, new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z),
                new Vector2(uOff / uScale, v0), new Vector2((uOff + wz) / uScale, v1));
            Quad(b, new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z),
                new Vector2(uOff / uScale, v0), new Vector2((uOff + wz) / uScale, v1));
        }

        private static void AddTop(Bucket b, Vector3 min, Vector3 max, float scale)
        {
            float y = max.y;
            Quad(b, new Vector3(min.x, y, min.z), new Vector3(max.x, y, min.z), new Vector3(min.x, y, max.z), new Vector3(max.x, y, max.z),
                new Vector2(min.x / scale, min.z / scale), new Vector2(max.x / scale, max.z / scale));
        }

        private static void AddBox(Bucket b, Vector3 min, Vector3 max, float scale)
        {
            AddWalls(b, min, max, scale, scale, 0f);
            AddTop(b, min, max, scale);
        }

        private static void AddCylinder(Bucket b, Vector3 baseCentre, float r, float h, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = baseCentre + new Vector3(Mathf.Sin(a0) * r, 0f, Mathf.Cos(a0) * r);
                Vector3 p1 = baseCentre + new Vector3(Mathf.Sin(a1) * r, 0f, Mathf.Cos(a1) * r);
                // Seen from outside, going round clockwise from above means p1 is to the left of p0.
                Quad(b, p1, p0, p1 + Vector3.up * h, p0 + Vector3.up * h, new Vector2(0f, 0f), new Vector2(1f, 1f));
                int s = b.V.Count;
                Vector3 top = baseCentre + Vector3.up * h;
                b.V.Add(top); b.V.Add(p0 + Vector3.up * h); b.V.Add(p1 + Vector3.up * h);
                for (int k = 0; k < 3; k++) { b.N.Add(Vector3.up); b.UV.Add(Vector2.zero); }
                b.T.Add(s); b.T.Add(s + 1); b.T.Add(s + 2);
            }
        }

        private static void Emit(Transform root, Bucket b)
        {
            if (b.V.Count == 0) return;
            var mesh = new Mesh { name = "town_" + b.Mat.name, indexFormat = b.V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(b.V);
            mesh.SetNormals(b.N);
            mesh.SetUVs(0, b.UV);
            mesh.SetTriangles(b.T, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = b.Mat;
            mr.shadowCastingMode = b.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }
    }
}
