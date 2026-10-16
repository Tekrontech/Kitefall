using Kitefall.Core;
using Kitefall.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kitefall.Match
{
    /// <summary>
    /// Turns a deterministic Core.ArenaLayout into scene objects (placeholder art, original).
    /// Nearby roofs get detail; distant city is silhouettes; the sky is ~70 % of the screen (spec 15).
    /// </summary>
    public static class ArenaBuilder
    {
        public static GameObject Build(ArenaLayout layout, ArenaData data, float timeOfDay01, Transform parent)
        {
            var root = new GameObject("Arena_" + layout.Type);
            root.transform.SetParent(parent, false);

            Color ground = data != null ? data.groundColor : new Color(0.55f, 0.5f, 0.4f);
            Color[] houses = data != null && data.buildingColors != null && data.buildingColors.Length > 0
                ? data.buildingColors : new[] { new Color(0.86f, 0.8f, 0.7f) };
            Color far = data != null ? data.farColor : new Color(0.45f, 0.5f, 0.6f);
            bool water = data != null && data.water;

            // ---- Ground / water
            GameObject g = Prim(PrimitiveType.Plane, root.transform, "Ground");
            g.transform.localScale = new Vector3(260f, 1f, 260f);
            g.transform.position = new Vector3(0f, 0f, 400f);
            g.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(water ? data.waterColor : ground, water ? 0.75f : 0.05f);
            if (layout.Type == ArenaType.RiverFestival)
            {
                GameObject river = Prim(PrimitiveType.Cube, root.transform, "River");
                river.transform.position = new Vector3(0f, 0.02f, 160f);
                river.transform.localScale = new Vector3(2000f, 0.04f, 70f);
                river.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(data != null ? data.waterColor : new Color(0.2f, 0.45f, 0.6f), 0.8f);
            }
            if (layout.Type == ArenaType.StormArchipelago)
            {
                var rng = new DeterministicRandom(layout.Seed);
                for (int i = 0; i < 9; i++)
                {
                    GameObject island = Prim(PrimitiveType.Cylinder, root.transform, "Island");
                    island.transform.position = new Vector3(rng.Range(-400f, 400f), 0.5f, rng.Range(-100f, 700f));
                    float r = rng.Range(40f, 120f);
                    island.transform.localScale = new Vector3(r, 0.5f, r);
                    island.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.8f, 0.75f, 0.55f));
                }
            }

            // ---- Blocks
            Vector3 lastPole = Vector3.zero;
            bool hasPole = false;
            foreach (ArenaBlock b in layout.Blocks)
            {
                switch (b.Kind)
                {
                    case BlockKind.Tree:
                    {
                        GameObject trunk = Prim(PrimitiveType.Cylinder, root.transform, "Tree");
                        trunk.transform.position = new Vector3(b.Center.X, b.Size.Y * 0.25f, b.Center.Z);
                        trunk.transform.localScale = new Vector3(0.6f, b.Size.Y * 0.25f, 0.6f);
                        trunk.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.4f, 0.28f, 0.18f));
                        GameObject crown = Prim(PrimitiveType.Sphere, trunk.transform.parent, "Crown");
                        crown.transform.position = new Vector3(b.Center.X, b.Size.Y * 0.7f, b.Center.Z);
                        crown.transform.localScale = new Vector3(b.Size.X * 1.6f, b.Size.Y * 0.7f, b.Size.Z * 1.6f);
                        crown.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.22f, 0.5f, 0.25f));
                        break;
                    }
                    case BlockKind.Pole:
                    {
                        GameObject pole = Prim(PrimitiveType.Cylinder, root.transform, "Pole");
                        pole.transform.position = ToUnity(b.Center);
                        pole.transform.localScale = new Vector3(0.35f, b.Size.Y * 0.5f, 0.35f);
                        pole.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.25f, 0.25f, 0.28f));
                        Vector3 top = new Vector3(b.Center.X, b.Max.Y, b.Center.Z);
                        if (hasPole) Cable(root.transform, lastPole, top);
                        lastPole = top;
                        hasPole = true;
                        break;
                    }
                    default:
                    {
                        GameObject box = Prim(PrimitiveType.Cube, root.transform, b.Kind.ToString());
                        box.transform.position = ToUnity(b.Center);
                        box.transform.localScale = ToUnity(b.Size);
                        Color c = b.Kind == BlockKind.Cliff ? new Color(0.55f, 0.5f, 0.44f)
                            : b.Kind == BlockKind.Stall ? Color.HSVToRGB((b.Variant * 0.23f) % 1f, 0.55f, 0.9f)
                            : houses[Mathf.Abs(b.Variant) % houses.Length];
                        box.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(c, 0.05f);
                        if (b.Kind == BlockKind.House) Rooftop(box.transform, b);
                        break;
                    }
                }
            }

            // ---- Distant silhouettes
            var silRng = new DeterministicRandom(layout.Seed + 9);
            Material farMat = Materials.Lit(far, 0f);
            for (int i = 0; i < 56; i++)
            {
                float a = i / 56f * Mathf.PI * 2f;
                float d = silRng.Range(700f, 1000f);
                float h = layout.Type == ArenaType.MountainValley ? silRng.Range(120f, 320f) : silRng.Range(20f, 70f);
                float w = silRng.Range(40f, 90f);
                GameObject s = Prim(PrimitiveType.Cube, root.transform, "Far");
                s.transform.position = new Vector3(Mathf.Sin(a) * d, h * 0.5f, 300f + Mathf.Cos(a) * d);
                s.transform.localScale = new Vector3(w, h, w);
                MeshRenderer mr = s.GetComponent<MeshRenderer>();
                mr.sharedMaterial = farMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            SetupSky(timeOfDay01, far);
            return root;
        }

        private static void Rooftop(Transform house, ArenaBlock b)
        {
            // Parapet wall: four thin boxes on the roof edge (child of world, not of the scaled box).
            Material m = Materials.Lit(new Color(0.8f, 0.76f, 0.68f));
            float y = b.Max.Y + 0.4f;
            Vector3 c = ToUnity(b.Center);
            float sx = b.Size.X, sz = b.Size.Z;
            Wall(house.parent, new Vector3(c.x, y, b.Max.Z), new Vector3(sx, 0.8f, 0.25f), m);
            Wall(house.parent, new Vector3(c.x, y, b.Min.Z), new Vector3(sx, 0.8f, 0.25f), m);
            Wall(house.parent, new Vector3(b.Max.X, y, c.z), new Vector3(0.25f, 0.8f, sz), m);
            Wall(house.parent, new Vector3(b.Min.X, y, c.z), new Vector3(0.25f, 0.8f, sz), m);
        }

        private static void Wall(Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject w = Prim(PrimitiveType.Cube, parent, "Parapet");
            w.transform.position = pos;
            w.transform.localScale = scale;
            w.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        private static void Cable(Transform parent, Vector3 a, Vector3 b)
        {
            var go = new GameObject("Cable");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Materials.Unlit();
            lr.startColor = lr.endColor = new Color(0.1f, 0.1f, 0.1f);
            lr.widthMultiplier = 0.06f;
            lr.positionCount = 12;
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * Vector3.Distance(a, b) * 0.04f;
                lr.SetPosition(i, p);
            }
        }

        public static void SetupSky(float timeOfDay01, Color fog)
        {
            Material sky = Materials.Skybox(timeOfDay01);
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1400f;
            RenderSettings.fogColor = Color.Lerp(fog, new Color(0.85f, 0.8f, 0.75f), 0.5f);

            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            float golden = Mathf.Abs(timeOfDay01 - 0.5f) * 2f;
            float elevation = Mathf.Lerp(65f, 12f, golden);
            float yaw = Mathf.Lerp(-80f, 80f, timeOfDay01);
            sun.transform.rotation = Quaternion.Euler(elevation, yaw, 0f);
            sun.color = Color.Lerp(new Color(1f, 0.97f, 0.92f), new Color(1f, 0.72f, 0.45f), golden);
            sun.intensity = Mathf.Lerp(1.25f, 0.95f, golden);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
