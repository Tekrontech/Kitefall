using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Match
{
    /// <summary>
    /// Artist world models (houses and rooftop props) from Assets/Art/World: &lt;name&gt;.fbx / .glb / .gltf / .prefab, e.g.
    /// house_03.fbx, water_tank.glb. Written by Kitefall ▸ World ▸ Setup World Assets into Resources/KitefallWorld/WorldAssets
    /// (and again by itself whenever that folder changes). A name without a model keeps the generated (code-built) geometry,
    /// so the game always works. Names, sizes and budgets: docs/WORLD_ASSET_LIST.md.
    /// </summary>
    public sealed class WorldAssetLibrary : ScriptableObject
    {
        public const string ResourcePath = "KitefallWorld/WorldAssets";
        public const int HouseVariants = 8;

        // ---- Names (keep in sync with docs/WORLD_ASSET_LIST.md and WorldAssetSetup.Names) ----
        public const string RoofParapet = "roof_parapet", WaterTank = "water_tank", Charpai = "charpai", Railing = "railing",
            CablePole = "cable_pole", Ladder = "ladder", Plank = "plank", RopeBridge = "rope_bridge", Stairwell = "stairwell",
            Stairs = "stairs", ClothesLine = "clothes_line", PlantPot = "plant_pot", DishAntenna = "dish_antenna",
            FlagPole = "flag_pole", Tree = "tree", PalmTree = "palm_tree", MarketStall = "market_stall";

        public static string House(int i) => "house_" + (i + 1).ToString("00");

        [Serializable]
        public sealed class Entry
        {
            public string id;
            public GameObject prefab;
            /// <summary>Renderer bounds size in metres when it was set up (for the report / docs; placement measures again).</summary>
            public Vector3 size;
            public int triangles;
        }

        public List<Entry> entries = new List<Entry>();

        private static WorldAssetLibrary _loaded;
        private static bool _tried;
        private static Dictionary<string, GameObject> _map;
        private static List<GameObject> _houses;

        public static WorldAssetLibrary Instance
        {
            get
            {
                if (!_tried || _loaded == null && _map == null)
                {
                    _tried = true;
                    _loaded = Resources.Load<WorldAssetLibrary>(ResourcePath);
                    _map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
                    _houses = new List<GameObject>();
                    if (_loaded != null)
                        foreach (Entry e in _loaded.entries)
                            if (e != null && e.prefab != null && !string.IsNullOrEmpty(e.id)) _map[e.id] = e.prefab;
                    for (int i = 0; i < HouseVariants; i++)
                        if (_map.TryGetValue(House(i), out GameObject h)) _houses.Add(h);
                }
                return _loaded;
            }
        }

        public static GameObject Get(string id)
        {
            if (string.IsNullOrEmpty(id) || Instance == null) return null;
            return _map.TryGetValue(id, out GameObject g) ? g : null;
        }

        public static bool Has(string id) => Get(id) != null;

        /// <summary>How many house_NN models exist (0 = generated houses everywhere).</summary>
        public static int HouseCount { get { _ = Instance; return _houses?.Count ?? 0; } }

        /// <summary>The house model for a block variant (any house_NN that exists, picked by variant).</summary>
        public static GameObject HouseFor(int variant)
        {
            if (HouseCount == 0) return null;
            return _houses[Mathf.Abs(variant) % _houses.Count];
        }

        /// <summary>Forget the cached library (the setup tool calls this after rebuilding it).</summary>
        public static void Reload() { _tried = false; _loaded = null; _map = null; _houses = null; }

        // ---- Placement -----------------------------------------------------------------------------------------------

        public enum Fit
        {
            /// <summary>The model's own size (artists model in metres).</summary>
            Natural,
            /// <summary>Uniform scale so the height matches size.y (or the larger of x/z when size.y ≤ 0).</summary>
            Uniform,
            /// <summary>Stretch each axis to size (an axis ≤ 0 keeps the model's own size) – house blocks, parapets.</summary>
            Box,
        }

        // Keyed by the original material itself (Unity 6.6 made GetInstanceID obsolete); null = a renderer with no material.
        private static readonly Dictionary<Material, Material> Fixed = new Dictionary<Material, Material>();
        private static Material _fixedNone;

        /// <summary>
        /// Places a copy of the named model so its renderer bounds' bottom-centre sits at <paramref name="bottomCentre"/>, turned by
        /// <paramref name="rotation"/> and fitted to <paramref name="size"/> (metres, in the rotated frame: x = width, y = height,
        /// z = depth / length). Colliders are removed (the world is visual only – the server owns gameplay). Returns null when the
        /// model is missing, so the caller builds its generated version instead.
        /// </summary>
        public static Transform Place(string id, Transform parent, Vector3 bottomCentre, Quaternion rotation, Vector3 size, Fit fit) =>
            Place(Get(id), id, parent, bottomCentre, rotation, size, fit);

        public static Transform Place(GameObject prefab, string name, Transform parent, Vector3 bottomCentre, Quaternion rotation, Vector3 size, Fit fit)
        {
            if (prefab == null) return null;
            // Measure at the origin with the model's own rotation and scale (a Blender FBX often turns its root −90° on X).
            GameObject inst = UnityEngine.Object.Instantiate(prefab, Vector3.zero, prefab.transform.rotation);
            inst.name = prefab.name;
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) b = new Bounds(Vector3.zero, Vector3.one);

            Vector3 scale = Vector3.one;
            Vector3 s = b.size;
            switch (fit)
            {
                case Fit.Uniform:
                {
                    float k = size.y > 0f && s.y > 1e-3f ? size.y / s.y : Mathf.Max(size.x, size.z) / Mathf.Max(1e-3f, Mathf.Max(s.x, s.z));
                    scale = Vector3.one * k;
                    break;
                }
                case Fit.Box:
                    scale = new Vector3(size.x > 0f && s.x > 1e-3f ? size.x / s.x : 1f,
                                        size.y > 0f && s.y > 1e-3f ? size.y / s.y : 1f,
                                        size.z > 0f && s.z > 1e-3f ? size.z / s.z : 1f);
                    break;
            }

            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.position = bottomCentre;
            holder.rotation = rotation;
            var fitNode = new GameObject("Fit").transform;
            fitNode.SetParent(holder, false);
            fitNode.localScale = scale;
            fitNode.localPosition = -Vector3.Scale(new Vector3(b.center.x, b.min.y, b.center.z), scale);
            inst.transform.SetParent(fitNode, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = prefab.transform.rotation;
            inst.transform.localScale = prefab.transform.localScale;

            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true)) FixMaterials(r);
            return holder;
        }

        /// <summary>The model's renderer bounds size at its own scale (metres), or zero when missing.</summary>
        public static Vector3 SizeOf(string id)
        {
            if (Instance == null) return Vector3.zero;
            foreach (Entry e in _loaded.entries)
                if (e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e.size;
            return Vector3.zero;
        }

        public static Vector3 SizeOf(GameObject prefab)
        {
            if (prefab == null || Instance == null) return Vector3.zero;
            foreach (Entry e in _loaded.entries)
                if (e != null && e.prefab == prefab) return e.size;
            return Vector3.zero;
        }

        /// <summary>
        /// Models imported with the Built-in "Standard" shader (or a shader this pipeline cannot draw) would show pink in URP:
        /// swap such materials for the game's lit material with the same colour and main texture.
        /// </summary>
        private static void FixMaterials(Renderer r)
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m != null && m.shader != null && m.shader.isSupported && !(Materials.Urp && m.shader.name == "Standard")) continue;
                Material repl = m != null ? (Fixed.TryGetValue(m, out Material known) ? known : null) : _fixedNone;
                if (repl == null)
                {
                    Color c = m != null && m.HasProperty("_Color") ? m.GetColor("_Color") : m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    Texture tex = m != null && m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    repl = Materials.Lit(c, 0.1f, tex, "world_" + (m != null ? m.name + "_" + Fixed.Count : "none"));
                    if (m != null) Fixed[m] = repl;
                    else _fixedNone = repl;
                }
                mats[i] = repl;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }
}
