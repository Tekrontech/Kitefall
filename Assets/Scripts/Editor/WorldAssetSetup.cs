using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitefall.Match;
using UnityEditor;
using UnityEngine;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Kitefall ▸ World ▸ Setup World Assets: finds the artist's world models in Assets/Art/World (&lt;name&gt;.fbx / .glb /
    /// .gltf / .obj / .prefab – house_01 … house_08, roof_parapet, water_tank, charpai, … see docs/WORLD_ASSET_LIST.md),
    /// prepares their import settings (readable meshes for static batching, no cameras / lights / colliders) and writes
    /// Resources/KitefallWorld/WorldAssets.asset, which ArenaBuilder / CityFiller / PropFactory read. Runs by itself whenever
    /// files in that folder change. A .prefab wins over .fbx, which wins over .glb / .gltf / .obj, when one name has several.
    /// </summary>
    public static class WorldAssetSetup
    {
        public const string Folder = "Assets/Art/World";
        private const string LibraryPath = "Assets/Resources/KitefallWorld/WorldAssets.asset";
        private static readonly string[] Extensions = { ".prefab", ".fbx", ".glb", ".gltf", ".obj" };

        /// <summary>Every name the game looks for, with its mobile triangle budget (docs/WORLD_ASSET_LIST.md).</summary>
        public static readonly Dictionary<string, int> Names = BuildNames();

        private static Dictionary<string, int> BuildNames()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < WorldAssetLibrary.HouseVariants; i++) d[WorldAssetLibrary.House(i)] = 2500;
            d[WorldAssetLibrary.RoofParapet] = 150;
            d[WorldAssetLibrary.WaterTank] = 600;
            d[WorldAssetLibrary.Charpai] = 400;
            d[WorldAssetLibrary.Railing] = 250;
            d[WorldAssetLibrary.CablePole] = 300;
            d[WorldAssetLibrary.Ladder] = 300;
            d[WorldAssetLibrary.Plank] = 60;
            d[WorldAssetLibrary.RopeBridge] = 1200;
            d[WorldAssetLibrary.Stairwell] = 600;
            d[WorldAssetLibrary.Stairs] = 600;
            d[WorldAssetLibrary.ClothesLine] = 500;
            d[WorldAssetLibrary.PlantPot] = 300;
            d[WorldAssetLibrary.DishAntenna] = 300;
            d[WorldAssetLibrary.FlagPole] = 200;
            d[WorldAssetLibrary.Tree] = 1500;
            d[WorldAssetLibrary.PalmTree] = 1200;
            d[WorldAssetLibrary.MarketStall] = 1200;
            return d;
        }

        private static bool _running;

        [MenuItem("Kitefall/World/Setup World Assets")]
        public static void SetupMenu()
        {
            string report = Setup(out _, out bool needsGltf);
            if (needsGltf && !CharacterModelSetup.GltfInstalled() && EditorUtility.DisplayDialog("Kitefall – World",
                    "GLB / glTF models were found but Unity cannot open them yet.\n\nInstall the free glTFast package now? " +
                    "(Then run Setup World Assets again.)", "Install glTFast", "Later"))
            {
                UnityEditor.PackageManager.Client.Add("com.unity.cloud.gltfast");
                return;
            }
            EditorUtility.DisplayDialog("Kitefall – World", report, "OK");
        }

        [MenuItem("Kitefall/World/Open World Folder")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(Folder);
        }

        /// <summary>"House-3" / "house 3" / "HOUSE_03" → "house_03"; other names lower-case with underscores.</summary>
        public static string Normalize(string fileName)
        {
            string n = fileName.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            if (n.StartsWith("house_") && int.TryParse(n.Substring(6), out int k) && k >= 1) n = "house_" + k.ToString("00");
            return n;
        }

        private static int Rank(string path)
        {
            int i = Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant());
            return i < 0 ? 99 : i;
        }

        /// <summary>Builds the library. Returns a short report for the owner.</summary>
        public static string Setup(out int found, out bool needsGltf)
        {
            found = 0;
            needsGltf = false;
            if (_running) return "Busy.";
            _running = true;
            try
            {
                if (!AssetDatabase.IsValidFolder(Folder))
                    return $"No world models yet. Put the files in {Folder} (one file per name, e.g. house_01.fbx, water_tank.glb – list in docs/WORLD_ASSET_LIST.md).";

                var picked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // name → path
                var unknown = new List<string>();
                foreach (string path in AllFiles())
                {
                    Prepare(path);
                    if (!(AssetDatabase.LoadMainAssetAtPath(path) is GameObject))
                    {
                        string ext = Path.GetExtension(path).ToLowerInvariant();
                        if (ext == ".glb" || ext == ".gltf") needsGltf = true;
                        continue;
                    }
                    string name = Normalize(Path.GetFileNameWithoutExtension(path));
                    if (!Names.ContainsKey(name)) { unknown.Add(Path.GetFileName(path)); continue; }
                    if (!picked.TryGetValue(name, out string have) || Rank(path) < Rank(have)) picked[name] = path;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                var lib = AssetDatabase.LoadAssetAtPath<WorldAssetLibrary>(LibraryPath);
                if (lib == null)
                {
                    lib = ScriptableObject.CreateInstance<WorldAssetLibrary>();
                    AssetDatabase.CreateAsset(lib, LibraryPath);
                }
                lib.entries.Clear();
                var lines = new List<string>();
                foreach (var kv in picked.OrderBy(k => k.Key))
                {
                    var prefab = AssetDatabase.LoadMainAssetAtPath(kv.Value) as GameObject;
                    Measure(prefab, out Vector3 size, out int tris);
                    lib.entries.Add(new WorldAssetLibrary.Entry { id = kv.Key, prefab = prefab, size = size, triangles = tris });
                    found++;
                    int budget = Names[kv.Key];
                    string warn = tris > budget * 3 / 2 ? $"  ⚠ {tris} triangles – budget {budget} (heavy on phones)" : "";
                    if (size.y > 200f || size.x > 200f) warn += "  ⚠ very large – exported in centimetres? (set Scale Factor or export in metres)";
                    lines.Add($"• {kv.Key} ({Path.GetExtension(kv.Value)}): {size.x:0.0} × {size.y:0.0} × {size.z:0.0} m, {tris} tris{warn}");
                }
                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();
                WorldAssetLibrary.Reload();

                string missing = string.Join(", ", Names.Keys.Where(n => !picked.ContainsKey(n)));
                string head = found == 0
                    ? "No usable world models found." + (needsGltf ? " .glb / .gltf files need the glTFast package (or convert them to FBX with Blender)." : "")
                    : $"{found} world model(s) ready. Start a match on a town arena (e.g. Old City Rooftops) to see them.";
                string report = head
                                + (lines.Count > 0 ? "\n\n" + string.Join("\n", lines) : "")
                                + (unknown.Count > 0 ? "\n\nNot used (name not in the list – check spelling): " + string.Join(", ", unknown) : "")
                                + (missing.Length > 0 ? "\n\nStill generated in code: " + missing : "");
                Debug.Log("[Kitefall] World: " + report);
                return report;
            }
            finally { _running = false; }
        }

        private static IEnumerable<string> AllFiles()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) >= 0) yield return path;
            }
        }

        /// <summary>Renderer bounds (model's own rotation / scale, metres) and triangle count.</summary>
        private static void Measure(GameObject prefab, out Vector3 size, out int tris)
        {
            size = Vector3.zero;
            tris = 0;
            if (prefab == null) return;
            GameObject inst = UnityEngine.Object.Instantiate(prefab, Vector3.zero, prefab.transform.rotation);
            try
            {
                bool any = false;
                Bounds b = default;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                if (any) size = b.size;
                foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += (int)(mf.sharedMesh.GetIndexCount(0) / 3);
                foreach (SkinnedMeshRenderer sk in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (sk.sharedMesh != null) tris += (int)(sk.sharedMesh.GetIndexCount(0) / 3);
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }

        /// <summary>
        /// Model files: readable meshes (the arena is static-batched at runtime, which needs that), no cameras / lights /
        /// animation / colliders. A .prefab: the same for the model files it uses.
        /// </summary>
        private static void Prepare(string path)
        {
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                    if (dep != path && AssetImporter.GetAtPath(dep) is ModelImporter) Prepare(dep);
                return;
            }
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return; // glTFast / others: their defaults are fine
            bool changed = false;
            if (!mi.isReadable) { mi.isReadable = true; changed = true; }
            if (mi.importCameras) { mi.importCameras = false; changed = true; }
            if (mi.importLights) { mi.importLights = false; changed = true; }
            if (mi.addCollider) { mi.addCollider = false; changed = true; }
            if (mi.importAnimation) { mi.importAnimation = false; changed = true; }
            if (mi.animationType != ModelImporterAnimationType.None) { mi.animationType = ModelImporterAnimationType.None; changed = true; }
            if (changed) mi.SaveAndReimport();
        }

        /// <summary>When the editor opens (and before Play): rebuild the library if it is missing or a model has no entry.</summary>
        [InitializeOnLoad]
        private static class AutoSetup
        {
            static AutoSetup()
            {
                if (Application.isBatchMode) return;
                EditorApplication.delayCall += Check;
                EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Check(); };
            }

            private static void Check()
            {
                if (_running || !AssetDatabase.IsValidFolder(Folder)) return;
                List<string> files = AllFiles().ToList();
                if (files.Count == 0) return;
                var lib = AssetDatabase.LoadAssetAtPath<WorldAssetLibrary>(LibraryPath);
                var have = lib == null ? new HashSet<string>() : new HashSet<string>(lib.entries.Where(e => e != null && e.prefab != null).Select(e => e.id), StringComparer.OrdinalIgnoreCase);
                bool stale = lib == null || files.Select(f => Normalize(Path.GetFileNameWithoutExtension(f)))
                    .Any(n => Names.ContainsKey(n) && !have.Contains(n));
                if (stale) Setup(out _, out _);
            }
        }

        /// <summary>New or changed files in the world folder → set up again automatically.</summary>
        private sealed class Watcher : AssetPostprocessor
        {
            private static bool _queued;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool touches = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                    .Any(p => p.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase) &&
                              Array.IndexOf(Extensions, Path.GetExtension(p).ToLowerInvariant()) >= 0);
                if (!touches || _queued || _running) return;
                _queued = true;
                EditorApplication.delayCall += () =>
                {
                    _queued = false;
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    Setup(out _, out bool needsGltf);
                    if (needsGltf) Debug.LogWarning("[Kitefall] World: .glb / .gltf files need the glTFast package – run Kitefall ▸ World ▸ Setup World Assets to install it.");
                };
            }
        }
    }
}
