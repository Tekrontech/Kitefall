using System.Collections.Generic;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Original, code-generated kite models: each class gets its own silhouette (spec 8.1 – inspired by, not copied).
    /// Local axes: +Y nose, +X right wing, +Z back of the sail.
    /// </summary>
    public static class KiteVisualFactory
    {
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>Visual scale – kites are drawn larger than life so they read on a phone screen.</summary>
        public const float VisualScale = 1.6f;

        public struct Look
        {
            public Color Primary;
            public Color Secondary;
            public int Pattern;
        }

        public static Look LookFor(string kiteId, string skinId)
        {
            CosmeticItem skin = string.IsNullOrEmpty(skinId) ? null : Catalog.Cosmetic(skinId);
            if (skin != null)
                return new Look { Primary = Hex(skin.primaryHex), Secondary = Hex(skin.secondaryHex), Pattern = skin.pattern };
            switch (kiteId)
            {
                case "pipa": return new Look { Primary = Hex("#1E90FF"), Secondary = Hex("#FFD700"), Pattern = 1 };
                case "rokkaku": return new Look { Primary = Hex("#F4F1E8"), Secondary = Hex("#C8102E"), Pattern = 4 };
                case "pakpao": return new Look { Primary = Hex("#FF6F91"), Secondary = Hex("#FFF3B0"), Pattern = 2 };
                case "gudiparan": return new Look { Primary = Hex("#2E8B57"), Secondary = Hex("#F5DEB3"), Pattern = 5 };
                case "chula": return new Look { Primary = Hex("#8B0000"), Secondary = Hex("#FFD700"), Pattern = 4 };
                case "wau": return new Look { Primary = Hex("#00897B"), Secondary = Hex("#FFB300"), Pattern = 3 };
                default: return new Look { Primary = Hex("#F25A3C"), Secondary = Hex("#FFE08A"), Pattern = 2 };
            }
        }

        /// <summary>Builds the kite object (sail + frame). Returns the root; the tail attach point is child "TailPoint".</summary>
        public static GameObject Build(string kiteId, string skinId, Transform parent)
        {
            Look look = LookFor(kiteId, skinId);
            var root = new GameObject("Kite_" + kiteId);
            root.transform.SetParent(parent, false);

            var sail = new GameObject("Sail");
            sail.transform.SetParent(root.transform, false);
            sail.transform.localScale = Vector3.one * VisualScale;
            Mesh mesh = MeshFor(kiteId, out float tailY);
            sail.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = sail.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.KitePaper(PatternTexture(look), "sail_" + kiteId + "_" + skinId);

            // Bamboo frame: spine + bow.
            Material stick = Materials.Lit(new Color(0.74f, 0.6f, 0.36f), 0.1f);
            Stick(sail.transform, new Vector3(0f, (0.6f + tailY) * 0.5f, -0.01f), new Vector3(0.02f, 0.6f - tailY, 0.02f), stick);
            Stick(sail.transform, new Vector3(0f, kiteId == "rokkaku" ? 0.25f : 0f, 0.04f), new Vector3(kiteId == "chula" ? 1.1f : 0.95f, 0.02f, 0.02f), stick);

            var tail = new GameObject("TailPoint");
            tail.transform.SetParent(root.transform, false);
            tail.transform.localPosition = new Vector3(0f, tailY * VisualScale, 0f);
            return root;
        }

        private static void Stick(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(parent, false);
            s.transform.localPosition = pos;
            s.transform.localScale = scale;
            s.GetComponent<MeshRenderer>().sharedMaterial = mat;
            s.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---- Meshes ----------------------------------------------------------

        public static Mesh MeshFor(string kiteId, out float tailY)
        {
            Vector2[] outline;
            switch (kiteId)
            {
                case "rokkaku": // tall hexagon
                    outline = new[] { V(-0.42f, 0.6f), V(0.42f, 0.6f), V(0.5f, 0.15f), V(0.42f, -0.55f), V(-0.42f, -0.55f), V(-0.5f, 0.15f) };
                    tailY = -0.55f;
                    break;
                case "pipa": // long sharp diamond
                    outline = new[] { V(0, 0.6f), V(0.42f, 0.05f), V(0, -0.55f), V(-0.42f, 0.05f) };
                    tailY = -0.55f;
                    break;
                case "pakpao": // small diamond with wide wings
                    outline = new[] { V(0, 0.45f), V(0.5f, 0.0f), V(0, -0.45f), V(-0.5f, 0.0f) };
                    tailY = -0.45f;
                    break;
                case "gudiparan": // big rounded diamond
                    outline = new[] { V(0, 0.62f), V(0.35f, 0.35f), V(0.5f, 0f), V(0.3f, -0.35f), V(0, -0.6f), V(-0.3f, -0.35f), V(-0.5f, 0f), V(-0.35f, 0.35f) };
                    tailY = -0.6f;
                    break;
                case "chula": // five-point star
                    outline = Star(5, 0.62f, 0.3f);
                    tailY = -0.5f;
                    break;
                case "wau": // moon kite: crescent bottom, wide wings
                    outline = new[] { V(0, 0.6f), V(0.18f, 0.3f), V(0.55f, 0.15f), V(0.25f, -0.05f), V(0.42f, -0.45f), V(0, -0.3f), V(-0.42f, -0.45f), V(-0.25f, -0.05f), V(-0.55f, 0.15f), V(-0.18f, 0.3f) };
                    tailY = -0.3f;
                    break;
                default: // guddi / patang: square diamond
                    outline = new[] { V(0, 0.55f), V(0.5f, 0.05f), V(0, -0.45f), V(-0.5f, 0.05f) };
                    tailY = -0.45f;
                    break;
            }
            string key = kiteId ?? "guddi";
            if (Meshes.TryGetValue(key, out Mesh m) && m != null) return m;
            m = FanMesh(outline, 0.06f);
            m.name = "Kite_" + key;
            Meshes[key] = m;
            return m;
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private static Vector2[] Star(int points, float outer, float inner)
        {
            var list = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                list[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return list;
        }

        /// <summary>Double-sided fan mesh with a slight belly (centre pushed back).</summary>
        private static Mesh FanMesh(Vector2[] outline, float belly)
        {
            int n = outline.Length;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = verts.Count;
                verts.Add(new Vector3(0f, 0f, belly));
                uvs.Add(new Vector2(0.5f, 0.5f));
                foreach (Vector2 p in outline)
                {
                    verts.Add(new Vector3(p.x, p.y, 0f));
                    uvs.Add(new Vector2(p.x + 0.5f, p.y + 0.5f));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + 1 + i, b = start + 1 + (i + 1) % n;
                    if (side == 0) { tris.Add(start); tris.Add(b); tris.Add(a); }
                    else { tris.Add(start); tris.Add(a); tris.Add(b); }
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Skin pattern textures (paper look) --------------------------------

        public static Texture2D PatternTexture(Look look)
        {
            string key = ColorUtility.ToHtmlStringRGB(look.Primary) + ColorUtility.ToHtmlStringRGB(look.Secondary) + look.Pattern;
            if (Textures.TryGetValue(key, out Texture2D t) && t != null) return t;
            const int size = 64;
            t = new Texture2D(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, name = "skin_" + key };
            var px = new Color[size * size];
            var rng = new System.Random(key.GetHashCode());
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f), v = y / (size - 1f);
                    bool sec;
                    switch (look.Pattern)
                    {
                        case 1: sec = ((int)(v * 8f)) % 2 == 0; break;                                  // stripes
                        case 2: sec = u > 0.5f; break;                                                   // halves
                        case 3: sec = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f) < 0.22f; break;          // centre diamond
                        case 4: sec = (u - 0.5f) * (u - 0.5f) + (v - 0.55f) * (v - 0.55f) < 0.03f; break;  // circle emblem
                        case 5: sec = u < 0.1f || u > 0.9f || v < 0.1f || v > 0.9f; break;               // border
                        default: sec = v < 0.22f; break;                                                 // tail patch ("tukkal")
                    }
                    Color c = sec ? look.Secondary : look.Primary;
                    float grain = 0.94f + (float)rng.NextDouble() * 0.06f; // paper grain
                    px[y * size + x] = c * grain;
                }
            t.SetPixels(px);
            t.Apply(true);
            Textures[key] = t;
            return t;
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
    }
}
