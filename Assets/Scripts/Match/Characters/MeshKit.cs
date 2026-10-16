using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Procedural mesh building blocks for characters, props and rooftops: lofted cross-sections (torso, garments),
    /// tapered limbs, ellipsoids (head, hands, hair caps), tori (charkhi rims, turbans) and boxes.
    /// Everything is original geometry generated in code – no imported art.
    /// </summary>
    public static class MeshKit
    {
        /// <summary>One cross-section of a loft: an ellipse at height y with radii (rx, rz), shifted by (ox, oz).</summary>
        public struct Ring
        {
            public float Y, Rx, Rz, Ox, Oz;
            public Ring(float y, float rx, float rz, float oz = 0f, float ox = 0f) { Y = y; Rx = rx; Rz = rz; Oz = oz; Ox = ox; }
        }

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out Mesh m) && m != null) return m;
            m = build();
            m.name = key;
            Cache[key] = m;
            return m;
        }

        /// <summary>Lofts ellipse rings (bottom → top) into a smooth closed surface with optional caps.</summary>
        public static Mesh Loft(IList<Ring> rings, int segments = 16, bool capBottom = true, bool capTop = true, float startAngle = 0f, float sweep = 360f)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            bool closed = sweep >= 359.9f;
            int cols = closed ? segments : segments + 1;
            for (int r = 0; r < rings.Count; r++)
            {
                Ring ring = rings[r];
                for (int s = 0; s < cols; s++)
                {
                    float a = (startAngle + sweep * s / segments) * Mathf.Deg2Rad;
                    v.Add(new Vector3(ring.Ox + Mathf.Sin(a) * ring.Rx, ring.Y, ring.Oz + Mathf.Cos(a) * ring.Rz));
                    uv.Add(new Vector2(s / (float)segments, r / (float)Mathf.Max(1, rings.Count - 1)));
                }
            }
            for (int r = 0; r + 1 < rings.Count; r++)
                for (int s = 0; s < segments; s++)
                {
                    int s1 = closed ? (s + 1) % segments : s + 1;
                    int a0 = r * cols + s, a1 = r * cols + s1, b0 = (r + 1) * cols + s, b1 = (r + 1) * cols + s1;
                    t.Add(a0); t.Add(a1); t.Add(b0);
                    t.Add(a1); t.Add(b1); t.Add(b0);
                }
            if (closed && capBottom) Cap(v, uv, t, rings[0], 0, segments, false);
            if (closed && capTop) Cap(v, uv, t, rings[rings.Count - 1], (rings.Count - 1) * cols, segments, true);
            return Finish(v, uv, t);
        }

        private static void Cap(List<Vector3> v, List<Vector2> uv, List<int> t, Ring ring, int start, int segments, bool top)
        {
            int c = v.Count;
            v.Add(new Vector3(ring.Ox, ring.Y, ring.Oz));
            uv.Add(new Vector2(0.5f, top ? 1f : 0f));
            for (int s = 0; s < segments; s++)
            {
                int a = start + s, b = start + (s + 1) % segments;
                if (top) { t.Add(c); t.Add(a); t.Add(b); }
                else { t.Add(c); t.Add(b); t.Add(a); }
            }
        }

        /// <summary>Tapered limb along −Y from the origin (joint at the top), rounded ends.</summary>
        public static Mesh Limb(float length, float rTop, float rBottom, int segments = 12, float flattenZ = 1f)
        {
            var rings = new List<Ring>();
            int capRings = 3;
            for (int i = capRings; i >= 1; i--)
            {
                float a = i / (float)capRings * Mathf.PI * 0.5f;
                rings.Add(new Ring(-length - Mathf.Sin(a) * rBottom * 0.8f, Mathf.Cos(a) * rBottom, Mathf.Cos(a) * rBottom * flattenZ));
            }
            for (int i = 0; i <= 4; i++)
            {
                float k = i / 4f;
                float r = Mathf.Lerp(rBottom, rTop, k) * (1f + Mathf.Sin(k * Mathf.PI) * 0.08f);
                rings.Add(new Ring(-length + length * k, r, r * flattenZ));
            }
            for (int i = 1; i <= capRings; i++)
            {
                float a = i / (float)capRings * Mathf.PI * 0.5f;
                rings.Add(new Ring(Mathf.Sin(a) * rTop * 0.8f, Mathf.Cos(a) * rTop + 0.001f, (Mathf.Cos(a) * rTop + 0.001f) * flattenZ));
            }
            return Loft(rings, segments);
        }

        /// <summary>Ellipsoid centred at the origin. <paramref name="latMin"/>/<paramref name="latMax"/> in degrees (−90…90) cut caps (hair).</summary>
        public static Mesh Ellipsoid(float rx, float ry, float rz, int segments = 16, int stacks = 10, float latMin = -90f, float latMax = 90f)
        {
            var rings = new List<Ring>();
            for (int i = 0; i <= stacks; i++)
            {
                float lat = Mathf.Lerp(latMin, latMax, i / (float)stacks) * Mathf.Deg2Rad;
                float c = Mathf.Max(0.0005f, Mathf.Cos(lat));
                rings.Add(new Ring(Mathf.Sin(lat) * ry, c * rx, c * rz));
            }
            return Loft(rings, segments, latMin > -89f, latMax < 89f);
        }

        /// <summary>Torus in the XZ plane (charkhi rim, turban wraps, rope coils).</summary>
        public static Mesh Torus(float radius, float tube, int segments = 20, int sides = 8)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    v.Add(c + n * tube);
                    uv.Add(new Vector2(i / (float)segments, j / (float)sides));
                }
            }
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = i * (sides + 1) + j, a1 = a0 + 1, b0 = (i + 1) * (sides + 1) + j, b1 = b0 + 1;
                    t.Add(a0); t.Add(a1); t.Add(b0);
                    t.Add(a1); t.Add(b1); t.Add(b0);
                }
            return Finish(v, uv, t);
        }

        /// <summary>Axis-aligned box centred on <paramref name="center"/> (cheap props: planks, rungs, parapets).</summary>
        public static Mesh Box(Vector3 size, Vector3 center = default)
        {
            Vector3 h = size * 0.5f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            void Face(Vector3 o, Vector3 u, Vector3 w)
            {
                int s = v.Count;
                v.Add(center + o - u - w); v.Add(center + o + u - w); v.Add(center + o + u + w); v.Add(center + o - u + w);
                float su = u.magnitude * 2f, sw = w.magnitude * 2f;
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su, 0)); uv.Add(new Vector2(su, sw)); uv.Add(new Vector2(0, sw));
                t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
            Face(new Vector3(0, 0, h.z), new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0));
            Face(new Vector3(0, 0, -h.z), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0));
            Face(new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), new Vector3(0, h.y, 0));
            Face(new Vector3(-h.x, 0, 0), new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0));
            Face(new Vector3(0, h.y, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z));
            Face(new Vector3(0, -h.y, 0), new Vector3(-h.x, 0, 0), new Vector3(0, 0, h.z));
            return Finish(v, uv, t, false);
        }

        /// <summary>Cylinder along Y centred at the origin.</summary>
        public static Mesh Cylinder(float radius, float height, int segments = 16, float radiusTop = -1f) =>
            Loft(new[] { new Ring(-height * 0.5f, radius, radius), new Ring(height * 0.5f, radiusTop < 0f ? radius : radiusTop, radiusTop < 0f ? radius : radiusTop) }, segments);

        /// <summary>A rope/line strip between points (rope bridge hand lines, cables): thin tube along a polyline.</summary>
        public static Mesh Rope(IList<Vector3> points, float radius, int sides = 5)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 dir = (i + 1 < points.Count ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                Vector3 side = Vector3.Cross(dir, Vector3.up);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                side.Normalize();
                Vector3 up = Vector3.Cross(side, dir);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    v.Add(points[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                    uv.Add(new Vector2(s / (float)sides, i));
                }
            }
            for (int i = 0; i + 1 < points.Count; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = i * (sides + 1) + s, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    t.Add(a0); t.Add(b0); t.Add(a1);
                    t.Add(a1); t.Add(b0); t.Add(b1);
                }
            return Finish(v, uv, t);
        }

        /// <summary>Combines child meshes that share one material into a single mesh (fewer draw calls for static props).</summary>
        public static Mesh Combine(List<CombineInstance> parts)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Finish(List<Vector3> v, List<Vector2> uv, List<int> t, bool smooth = true)
        {
            var m = new Mesh();
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Adds a child with this mesh and material; returns its transform.</summary>
        public static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos = default, Quaternion? localRot = null, Vector3? localScale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }
    }
}
