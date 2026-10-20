using Kitefall.Core;
using Kitefall.Data;
using Kitefall.Services;
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

            // Arenas without a data asset yet (added in an update) use their built-in look.
            ArenaLook look = ArenaLook.For(layout.Type);
            Color ground = data != null ? data.groundColor : look.Ground;
            Color[] houses = data != null && data.buildingColors != null && data.buildingColors.Length > 0
                ? data.buildingColors : look.Buildings;
            Color far = data != null ? data.farColor : look.Far;
            bool water = data != null ? data.water : look.Water;
            Color waterColor = data != null ? data.waterColor : look.WaterColor;

            // ---- Ground / water
            GameObject g = Prim(PrimitiveType.Plane, root.transform, "Ground");
            g.transform.localScale = new Vector3(260f, 1f, 260f);
            g.transform.position = new Vector3(0f, 0f, 400f);
            g.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(water ? waterColor : ground, water ? 0.75f : 0.05f);
            if (look.SeaDownwind)
            {
                // Beach / harbour: sand or quay where the flyers stand, open sea where the kites fly.
                GameObject sea = Prim(PrimitiveType.Cube, root.transform, "Sea");
                sea.transform.position = new Vector3(0f, 0.02f, 700f);
                sea.transform.localScale = new Vector3(3000f, 0.04f, 1200f);
                sea.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(look.WaterColor, 0.85f);
            }
            if (layout.Type == ArenaType.RiverFestival)
            {
                GameObject river = Prim(PrimitiveType.Cube, root.transform, "River");
                river.transform.position = new Vector3(0f, 0.02f, 160f);
                river.transform.localScale = new Vector3(2000f, 0.04f, 70f);
                river.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(waterColor, 0.8f);
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
                    case BlockKind.Tree when b.Variant == 1:
                    {
                        // Palm: tall thin trunk, flat fan of leaves.
                        GameObject trunk = Prim(PrimitiveType.Cylinder, root.transform, "Palm");
                        trunk.transform.position = new Vector3(b.Center.X, b.Size.Y * 0.5f, b.Center.Z);
                        trunk.transform.localScale = new Vector3(0.45f, b.Size.Y * 0.5f, 0.45f);
                        trunk.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.55f, 0.42f, 0.28f));
                        for (int leaf = 0; leaf < 5; leaf++)
                        {
                            GameObject frond = Prim(PrimitiveType.Cube, root.transform, "Frond");
                            frond.transform.position = new Vector3(b.Center.X, b.Size.Y, b.Center.Z);
                            frond.transform.rotation = Quaternion.Euler(18f, leaf * 72f, 0f);
                            frond.transform.localScale = new Vector3(0.9f, 0.08f, 4.2f);
                            frond.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.24f, 0.55f, 0.27f));
                        }
                        break;
                    }
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
                    case BlockKind.House:
                    {
                        // Modelled houses near the flyers, cheaper boxes far away (upgrade 12: draw-call budget).
                        Color c = houses[Mathf.Abs(b.Variant) % houses.Length];
                        bool roof = layout.Roofs != null && layout.Roofs.RoofAt(b.Center, 0.5f) >= 0;
                        float dist = new Vector2(b.Center.X, b.Center.Z).magnitude;
                        PropFactory.House(root.transform, b, c, b.Variant + (int)(b.Center.X * 0.37f), dist < 200f);
                        if (!roof)
                        {
                            Rooftop(root.transform, b);
                            if (dist < 140f) PropFactory.RoofClutter(root.transform, ToUnity(b.Center), b.Size.X, b.Size.Z, b.Max.Y, (int)(b.Center.X * 13f + b.Center.Z * 7f), false);
                        }
                        break;
                    }
                    default:
                    {
                        GameObject box = Prim(PrimitiveType.Cube, root.transform, b.Kind.ToString());
                        box.transform.position = ToUnity(b.Center);
                        box.transform.localScale = ToUnity(b.Size);
                        Color c = b.Kind == BlockKind.Cliff ? (b.Variant == 2 ? new Color(0.42f, 0.6f, 0.3f) : new Color(0.55f, 0.5f, 0.44f))
                            : b.Kind == BlockKind.Stall ? Color.HSVToRGB((b.Variant * 0.23f) % 1f, 0.55f, 0.9f)
                            : houses[Mathf.Abs(b.Variant) % houses.Length];
                        Texture2D tex = b.Kind == BlockKind.Cliff ? ProceduralTextures.Concrete(c) : null;
                        box.GetComponent<MeshRenderer>().sharedMaterial = tex != null ? Materials.Lit(Color.white, 0.05f, tex, "cliff" + tex.name) : Materials.Lit(c, 0.05f);
                        if (layout.Roofs != null && b.Kind == BlockKind.Cliff && layout.Roofs.RoofAt(b.Center, 0.5f) >= 0)
                        {
                            // Cliff ledges used as roofs get a flat stone cap.
                            GameObject cap = Prim(PrimitiveType.Cube, root.transform, "LedgeCap");
                            cap.transform.position = new Vector3(b.Center.X, b.Max.Y + 0.1f, b.Center.Z);
                            cap.transform.localScale = new Vector3(b.Size.X + 0.4f, 0.2f, b.Size.Z + 0.4f);
                            cap.GetComponent<MeshRenderer>().sharedMaterial = Materials.Lit(new Color(0.68f, 0.64f, 0.58f), 0.1f);
                        }
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

            if (layout.Roofs != null) BuildRoofs(layout, root.transform);

            SetupSky(timeOfDay01, far);
            // Everything here is static: combine for static batching (upgrade 12: ≤ 150 draw calls on Medium).
            StaticBatchingUtility.Combine(root);
            return root;
        }

        /// <summary>Flat 2.6 × 1.7 m rug facing up, UVs 0..1 (the picture shows once).</summary>
        private static Mesh RugMesh()
        {
            const float w = 1.3f, d = 0.85f;
            var m = new Mesh { name = "rug" };
            m.vertices = new[] { new Vector3(-w, 0f, -d), new Vector3(-w, 0f, d), new Vector3(w, 0f, d), new Vector3(w, 0f, -d) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The rooftop network (upgrade 10.1): parapets with gaps at links, lived-in clutter, trait hints and the links.</summary>
        private static void BuildRoofs(ArenaLayout layout, Transform parent)
        {
            RoofGraph g = layout.Roofs;
            var root = new GameObject("Rooftops").transform;
            root.SetParent(parent, false);
            Material parapet = ProceduralTextures.ArtScale(Materials.Lit(Color.white, 0.05f, ProceduralTextures.Art("tex_plaster_wall", ProceduralTextures.Plaster(new Color(0.86f, 0.82f, 0.74f))), "parapet"), 2f);
            Material floor = ProceduralTextures.ArtScale(Materials.Lit(Color.white, 0.05f, ProceduralTextures.Art("tex_roof_floor", ProceduralTextures.Tiles(new Color(0.78f, 0.72f, 0.64f))), "rooftiles"), 3f);
            // The woven rug (Image Mapper tex_fabric_rug) is a small rug laid on some roofs, never the whole floor.
            Texture2D rugTex = Kitefall.UI.Art.Texture("tex_fabric_rug");
            Material rug = rugTex != null ? Materials.Lit(Color.white, 0.02f, rugTex, "fabricrug") : null;
            foreach (RoofNode r in g.Roofs)
            {
                var gaps = new System.Collections.Generic.List<Vector3>();
                foreach (int l in r.Links)
                {
                    RoofLink link = g.Link(l);
                    gaps.Add(ToUnity(link.A == r.Id ? link.PointA : link.PointB));
                }
                Vector3 c = ToUnity(r.Center);
                MeshKit.Part(root, "RoofFloor", MeshKit.Box(new Vector3(r.SizeX - 0.3f, 0.06f, r.SizeZ - 0.3f)), floor, c + Vector3.up * 0.23f);
                PropFactory.Parapets(root, r, gaps, parapet);
                if (rug != null && r.Id % 2 == 0 && r.SizeX > 5f && r.SizeZ > 5f)
                {
                    // One rug in a corner, away from the launch spot in the middle; UVs 0..1 so the whole rug picture shows once.
                    GameObject rugGo = MeshKit.Part(root, "Rug", MeshKit.Cached("rugquad", RugMesh), rug, c + new Vector3(-r.SizeX * 0.25f, 0.27f, r.SizeZ * 0.25f)).gameObject;
                    rugGo.transform.localRotation = Quaternion.Euler(0f, (r.Id * 37) % 4 * 90f, 0f);
                }
                PropFactory.RoofClutter(root, c, r.SizeX, r.SizeZ, r.Height + 0.25f, r.Id * 31 + layout.Seed, true);
                if (r.Has(RoofTrait.High)) PropFactory.Flag(root, c + new Vector3(r.SizeX * 0.5f - 0.6f, 0.25f, r.SizeZ * 0.5f - 0.6f), new Color(0.95f, 0.75f, 0.2f));
                if (r.Has(RoofTrait.CrateSpot))
                    MeshKit.Part(root, "CrateMark", MeshKit.Cached("cratemark", () => MeshKit.Torus(1.6f, 0.08f, 28, 4)), Materials.Lit(new Color(1f, 0.6f, 0.15f), 0.3f), c + new Vector3(0f, 0.28f, 2.2f));
                if (r.Has(RoofTrait.Cables))
                {
                    Vector3 a = c + new Vector3(-r.SizeX * 0.6f, 4.2f, -r.SizeZ * 0.3f), b = c + new Vector3(r.SizeX * 0.6f, 4.6f, r.SizeZ * 0.4f);
                    Cable(root, a, b);
                    Cable(root, a + Vector3.up * 0.4f, b + Vector3.up * 0.3f);
                    MeshKit.Part(root, "CablePole", MeshKit.Cached("cablepole", () => MeshKit.Cylinder(0.12f, 5f, 8)), Materials.Lit(new Color(0.3f, 0.3f, 0.32f), 0.4f), a - Vector3.up * 2.2f);
                }
            }
            foreach (RoofLink link in g.Links) PropFactory.Link(root, link, g);
        }

        /// <summary>The waiting plaza (upgrade F3): decks joined by bridges, dummy kites on poles, flags and festival lights.</summary>
        public static GameObject BuildWaitingArea(WaitingArea area, ArenaType type, Transform parent)
        {
            var root = new GameObject("WaitingPlaza");
            root.transform.SetParent(parent, false);
            var rng = new DeterministicRandom(17);
            Material deck = ProceduralTextures.ArtScale(Materials.Lit(Color.white, 0.15f, ProceduralTextures.Art("tex_wood_balcony", ProceduralTextures.Wood(new Color(0.62f, 0.44f, 0.28f))), "deck"), 2f);
            Material walls = ProceduralTextures.ArtScale(Materials.Lit(Color.white, 0.05f, ProceduralTextures.Art("tex_plaster_wall", ProceduralTextures.Plaster(new Color(0.92f, 0.86f, 0.74f))), "plazawall"), 2f);
            foreach (WalkRect r in area.Surfaces)
            {
                Vector3 c = ToUnity(r.Center);
                if (r.IsBridge)
                {
                    MeshKit.Part(root.transform, "Bridge", MeshKit.Box(new Vector3(r.SizeX, 0.15f, r.SizeZ)), deck, c - Vector3.up * 0.07f);
                    continue;
                }
                MeshKit.Part(root.transform, "PlazaBuilding", MeshKit.Box(new Vector3(r.SizeX, c.y, r.SizeZ)), walls, new Vector3(c.x, c.y * 0.5f - 0.15f, c.z));
                MeshKit.Part(root.transform, "Deck", MeshKit.Box(new Vector3(r.SizeX, 0.15f, r.SizeZ)), deck, c - Vector3.up * 0.07f);
            }
            for (int i = 0; i < area.DummyPoles.Count; i++) PropFactory.DummyPole(root.transform, ToUnity(area.DummyPoles[i]), Color.HSVToRGB(i * 0.2f % 1f, 0.7f, 0.95f));
            for (int i = 0; i < area.Flags.Count; i++)
            {
                PropFactory.Flag(root.transform, ToUnity(area.Flags[i]), Color.HSVToRGB(rng.NextFloat(), 0.75f, 0.95f));
                if (i + 1 < area.Flags.Count) PropFactory.FairyLights(root.transform, ToUnity(area.Flags[i]) + Vector3.up * 3.7f, ToUnity(area.Flags[i + 1]) + Vector3.up * 3.7f, rng);
            }
            foreach (WalkRect r in area.Surfaces)
                if (!r.IsBridge && rng.Chance(0.6f)) PropFactory.Charpai(root.transform, ToUnity(r.Center) + new Vector3(r.SizeX * 0.3f, 0f, r.SizeZ * 0.3f), rng.Range(0f, 90f));
            StaticBatchingUtility.Combine(root);
            return root;
        }

        private static void Rooftop(Transform parent, ArenaBlock b)
        {
            // Parapet wall: four thin boxes on the roof edge.
            Material m = Materials.Lit(new Color(0.8f, 0.76f, 0.68f));
            float y = b.Max.Y + 0.4f;
            Vector3 c = ToUnity(b.Center);
            float sx = b.Size.X, sz = b.Size.Z;
            Wall(parent, new Vector3(c.x, y, b.Max.Z), new Vector3(sx, 0.8f, 0.25f), m);
            Wall(parent, new Vector3(c.x, y, b.Min.Z), new Vector3(sx, 0.8f, 0.25f), m);
            Wall(parent, new Vector3(b.Max.X, y, c.z), new Vector3(0.25f, 0.8f, sz), m);
            Wall(parent, new Vector3(b.Min.X, y, c.z), new Vector3(0.25f, 0.8f, sz), m);
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
            // Globals for the kite paper shader (no pipeline dependency).
            Shader.SetGlobalVector("_KF_SunDir", -sun.transform.forward);
            Color sc = sun.color * sun.intensity;
            Shader.SetGlobalVector("_KF_SunColor", new Vector4(sc.r, sc.g, sc.b, 1f));
            Color amb = Color.Lerp(new Color(0.5f, 0.58f, 0.72f), new Color(0.62f, 0.52f, 0.5f), golden);
            Shader.SetGlobalVector("_KF_Ambient", new Vector4(amb.r, amb.g, amb.b, 1f));
            KitefallGraphics.ApplySunHint(sun, golden);
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

    /// <summary>Built-in colours per arena, used when the project has no ArenaData asset for it (and by the editor to create one).</summary>
    public struct ArenaLook
    {
        public Color Ground, Far, WaterColor;
        public Color[] Buildings;
        public bool Water, SeaDownwind;

        private static Color H(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.gray;

        public static ArenaLook For(ArenaType type)
        {
            var l = new ArenaLook
            {
                Ground = new Color(0.55f, 0.5f, 0.4f), Far = new Color(0.45f, 0.5f, 0.6f), WaterColor = new Color(0.18f, 0.45f, 0.62f),
                Buildings = new[] { new Color(0.86f, 0.8f, 0.7f), new Color(0.66f, 0.4f, 0.3f), new Color(0.93f, 0.88f, 0.76f), new Color(0.55f, 0.66f, 0.74f) }
            };
            switch (type)
            {
                case ArenaType.RioHillside: // bright painted hillside houses
                    l.Ground = H("#7A8F4E"); l.Far = H("#5E7F6A");
                    l.Buildings = new[] { H("#F2C14E"), H("#E86F51"), H("#4FA3C7"), H("#F4EFE6"), H("#7BC67E") };
                    break;
                case ArenaType.SantiagoRooftops: // terracotta city under the Andes
                    l.Ground = H("#8C7A62"); l.Far = H("#8A8FA6");
                    l.Buildings = new[] { H("#D9A273"), H("#B5654A"), H("#EDE0C8"), H("#9DB4C0") };
                    break;
                case ArenaType.MediterraneanHarbour: // white walls, blue doors, sea
                    l.Ground = H("#CDBB98"); l.Far = H("#6F8FB0"); l.WaterColor = H("#1E6FA8"); l.SeaDownwind = true;
                    l.Buildings = new[] { H("#F7F5EF"), H("#F2EEE4"), H("#2F6FB5"), H("#E9E2D0") };
                    break;
                case ArenaType.AtlanticBeach: // sand, dunes, striped beach huts
                    l.Ground = H("#E2CFA0"); l.Far = H("#8FA9BF"); l.WaterColor = H("#2D6E8E"); l.SeaDownwind = true;
                    l.Buildings = new[] { H("#F4F1E8"), H("#D94F4F"), H("#3C7DBF") };
                    break;
                case ArenaType.CaribbeanShore: // turquoise water, pastel cottages
                    l.Ground = H("#EAD9AE"); l.Far = H("#5FA8A0"); l.WaterColor = H("#21A6A6"); l.SeaDownwind = true;
                    l.Buildings = new[] { H("#FFD6E0"), H("#BDE8F5"), H("#FFF1B8"), H("#C8F2C2") };
                    break;
                case ArenaType.BaliTerraces: // green terraces, stone and thatch
                    l.Ground = H("#5E8F3E"); l.Far = H("#4E6E5A");
                    l.Buildings = new[] { H("#A9876A"), H("#C9B48F"), H("#6E5A44") };
                    break;
            }
            return l;
        }
    }
}
