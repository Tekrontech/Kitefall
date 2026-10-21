using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kitefall.Core;
using Kitefall.Match;
using UnityEditor;
using UnityEngine;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Kitefall ▸ Characters ▸ Setup Models: finds the artist's 3D characters in Assets/Art/Characters (&lt;id&gt;.fbx / .glb /
    /// .gltf, Mixamo-style rig), prepares their import settings and writes Resources/KitefallCharacters/CharacterModels.asset,
    /// which the game reads to show the selected character on Home, in the Wardrobe and in matches. Runs by itself whenever
    /// files in that folder change. FBX wins over GLB and is imported as **Humanoid**, so one set of Mixamo clips in
    /// Assets/Art/Characters/Anim/ (idle.fbx, walk.fbx … pull.fbx, release.fbx, reel.fbx, tie.fbx, toss.fbx, tug.fbx) moves every
    /// character; animation files copy rookie_f's avatar. A model's own clips win over the shared ones. GLB files written by
    /// the old code character builder (flat palette texture) are skipped – those characters use the in-code body instead.
    /// </summary>
    public static class CharacterModelSetup
    {
        public const string Folder = "Assets/Art/Characters";
        private const string LibraryPath = "Assets/Resources/KitefallCharacters/CharacterModels.asset";
        private const string GltfPackage = "com.unity.cloud.gltfast";

        public static readonly string[] ClipNames =
        {
            "fly_hold", "idle", "walk", "run", "jump", "climb", "victory", "clap", "wave", "dance", "sad",
            "pull", "release", "reel", "tie", "toss", "tug"
        };
        private static readonly HashSet<string> Looping = new HashSet<string>
            { "fly_hold", "idle", "walk", "run", "climb", "dance", "clap", "wave", "pull", "release", "reel", "tie", "tug" };
        /// <summary>Mixamo download names → game clip names ("Waving" is not a prefix of "wave").</summary>
        private static readonly (string from, string to)[] Aliases =
        {
            ("waving", "wave"), ("dancing", "dance"), ("defeated", "sad"), ("releasing", "release"), ("letting_go", "release"),
            ("tying", "tie"), ("throwing", "toss"), ("pulling", "pull"), ("reeling", "reel"), ("tugging", "tug"), ("clapping", "clap"),
            ("cheering", "victory"), ("climbing", "climb"), ("jumping", "jump"), ("running", "run"), ("walking", "walk")
        };
        private const string RookieSource = "rookie_f";

        private static bool _running;

        [MenuItem("Kitefall/Characters/Setup Models")]
        public static void SetupMenu()
        {
            string report = Setup(out int found, out bool needsGltf);
            if (needsGltf && EditorUtility.DisplayDialog("Kitefall – Characters",
                    "GLB characters were found but Unity cannot open .glb files yet.\n\nInstall the free glTFast package now? " +
                    "(After it installs, Setup Models runs again by itself.)", "Install glTFast", "Later"))
            {
                UnityEditor.PackageManager.Client.Add(GltfPackage);
                return;
            }
            EditorUtility.DisplayDialog("Kitefall – Characters", report, "OK");
        }

        [MenuItem("Kitefall/Characters/Open Characters Folder")]
        public static void OpenFolder()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(Folder);
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
                if (!AssetDatabase.IsValidFolder(Folder)) return $"No characters yet. Put the files in {Folder} (one file per character, named by id, e.g. rookie_f.glb).";

                // 1. Import settings (FBX: Generic rig + looping clips; GLB: Mecanim clips when glTFast allows it).
                var models = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // id → path
                var animFiles = new List<string>();
                var skipped = new List<string>();
                foreach (string path in AllFiles())
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (IsAnimFile(path)) { animFiles.Add(path); continue; }
                    if (ext == ".fbx") PrepareFbx(path, false, null);
                    else PrepareGltf(path);

                    if (!(AssetDatabase.LoadMainAssetAtPath(path) is GameObject))
                    {
                        if (ext != ".fbx") needsGltf = true;
                        continue;
                    }
                    string id = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    if (ext != ".fbx" && IsCodeBuiltGlb(path)) { skipped.Add(id); continue; }
                    if (!models.TryGetValue(id, out string have) || ext == ".fbx" && !have.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) models[id] = path;
                }

                // 2. Shared clips from animation-only files (Humanoid, avatar copied from rookie_f's FBX when there is one).
                Avatar source = models.TryGetValue(RookieSource, out string rookie) ? HumanAvatar(rookie) : null;
                if (source == null) source = models.Values.Select(HumanAvatar).FirstOrDefault(a => a != null);
                foreach (string path in animFiles)
                    if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) PrepareFbx(path, true, source);
                var sharedHuman = new Dictionary<string, AnimationClip>();
                var sharedGeneric = new Dictionary<string, AnimationClip>();
                foreach (string path in animFiles)
                    foreach (var kv in ClipsIn(path))
                    {
                        var into = kv.Value.humanMotion ? sharedHuman : sharedGeneric;
                        if (!into.ContainsKey(kv.Key)) into[kv.Key] = kv.Value;
                    }

                // 3. The library asset.
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                var lib = AssetDatabase.LoadAssetAtPath<CharacterModelLibrary>(LibraryPath);
                if (lib == null)
                {
                    lib = ScriptableObject.CreateInstance<CharacterModelLibrary>();
                    AssetDatabase.CreateAsset(lib, LibraryPath);
                }
                lib.entries.Clear();
                var lines = new List<string>();
                var known = new HashSet<string>(CharacterCatalog.Characters.Select(c => c.id), StringComparer.OrdinalIgnoreCase);
                foreach (var kv in models.OrderBy(k => k.Key))
                {
                    var clips = ClipsIn(kv.Value);
                    // Humanoid clips only drive Humanoid models (and generic clips generic ones).
                    bool human = HumanAvatar(kv.Value) != null;
                    foreach (var s in human ? sharedHuman : sharedGeneric) if (!clips.ContainsKey(s.Key)) clips[s.Key] = s.Value;
                    var entry = new CharacterModelLibrary.Entry
                    {
                        id = kv.Key,
                        model = AssetDatabase.LoadMainAssetAtPath(kv.Value) as GameObject,
                        names = clips.Keys.ToArray(),
                        clips = clips.Values.ToArray()
                    };
                    lib.entries.Add(entry);
                    found++;
                    string missing = string.Join(", ", ClipNames.Where(c => !clips.ContainsKey(c)));
                    lines.Add($"• {kv.Key} ({Path.GetExtension(kv.Value)}{(human ? ", Humanoid" : ", generic")}): {clips.Count} clips" +
                              (missing.Length > 0 ? $" – missing {missing}" : "") +
                              (known.Contains(kv.Key) ? "" : "  ⚠ not a character id (check the file name)"));
                }
                foreach (string id in skipped)
                    lines.Add($"• {id}: code-built GLB skipped – uses the in-code body until an artist model ({id}.fbx) is added");
                if (animFiles.Count > 0)
                    lines.Add($"• Shared animations: {animFiles.Count} file(s) – {string.Join(", ", sharedHuman.Keys.Concat(sharedGeneric.Keys).Distinct())}");
                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();
                CharacterModelLibrary.Reload();

                string head = found == 0
                    ? "No usable character models found." + (needsGltf ? " .glb files need the glTFast package (or convert them to FBX with Blender)." : "")
                    : $"{found} character model(s) ready. Equip a character in the Wardrobe and it shows on Home and in matches.";
                string report = head + (lines.Count > 0 ? "\n\n" + string.Join("\n", lines) : "");
                Debug.Log("[Kitefall] Characters: " + report);
                return report;
            }
            finally { _running = false; }
        }

        private static IEnumerable<string> AllFiles()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".fbx" || ext == ".glb" || ext == ".gltf") yield return path;
            }
        }

        /// <summary>Clips in one file, keyed by game clip name (idle, fly_hold …).</summary>
        private static Dictionary<string, AnimationClip> ClipsIn(string path)
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is AnimationClip clip) || clip.name.StartsWith("__preview__")) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                // Animation files are named after the clip (Mixamo calls every clip "mixamo.com").
                string key = IsAnimFile(path) ? ClipKey(file, file) ?? ClipKey(clip.name, file) : ClipKey(clip.name, file);
                if (key != null && !result.ContainsKey(key)) result[key] = clip;
            }
            return result;
        }

        /// <summary>"Armature|fly_hold.001" → "fly_hold"; an animation-only file "anim_walk" names its single clip.</summary>
        public static string ClipKey(string clipName, string fileName)
        {
            string n = clipName;
            int bar = n.LastIndexOf('|');
            if (bar >= 0) n = n.Substring(bar + 1);
            n = n.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            if (n.StartsWith("anim_")) n = n.Substring(5);
            foreach (string c in ClipNames) if (n.StartsWith(c)) return c;
            foreach (var (from, to) in Aliases) if (n.StartsWith(from)) return to;
            if (n.Contains("idle")) return "idle";
            if (n == "flyhold" || n.StartsWith("fly")) return "fly_hold";
            string f = fileName.ToLowerInvariant();
            if (f.StartsWith("anim_"))
                foreach (string c in ClipNames) if (f.Substring(5).StartsWith(c)) return c;
            return null;
        }

        /// <summary>
        /// FBX → Humanoid. Models build their own avatar; animation files copy the source avatar (rookie_f) when there is one,
        /// else build their own (a Mixamo "without skin" download has the full skeleton). The hand IK still finds the Mixamo bones.
        /// </summary>
        private static void PrepareFbx(string path, bool animation, Avatar source)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            bool changed = false;
            if (mi.animationType != ModelImporterAnimationType.Human)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            ModelImporterAvatarSetup setup = animation && source != null ? ModelImporterAvatarSetup.CopyFromOther : ModelImporterAvatarSetup.CreateFromThisModel;
            if (mi.avatarSetup != setup) { mi.avatarSetup = setup; changed = true; }
            if (setup == ModelImporterAvatarSetup.CopyFromOther && mi.sourceAvatar != source) { mi.sourceAvatar = source; changed = true; }
            if (!mi.importAnimation) { mi.importAnimation = true; changed = true; }
            ModelImporterClipAnimation[] clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            bool clipChanged = false;
            foreach (ModelImporterClipAnimation c in clips)
            {
                string key = ClipKey(c.name, Path.GetFileNameWithoutExtension(path));
                bool loop = key != null && Looping.Contains(key);
                if (c.loopTime != loop) { c.loopTime = loop; clipChanged = true; }
            }
            if (clipChanged) { mi.clipAnimations = clips; changed = true; }
            if (changed) mi.SaveAndReimport();
        }

        private static bool IsAnimFile(string path) =>
            path.Replace('\\', '/').IndexOf(Folder + "/Anim/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Path.GetFileName(path).StartsWith("anim_", StringComparison.OrdinalIgnoreCase);

        /// <summary>The valid human avatar of an imported model, or null (generic / glTF / failed mapping).</summary>
        private static Avatar HumanAvatar(string path)
        {
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Avatar a && a.isHuman && a.isValid) return a;
            return null;
        }

        /// <summary>
        /// A GLB written by the old in-code character builder: its generator says so and it only carries a tiny palette texture
        /// (the AI-made rookie_f went through the same tool for rigging but has a real 3 MB texture, so it is kept).
        /// </summary>
        public static bool IsCodeBuiltGlb(string path)
        {
            try
            {
                byte[] d = File.ReadAllBytes(path);
                if (d.Length < 20 || BitConverter.ToUInt32(d, 0) != 0x46546C67) return false; // "glTF"
                int len = (int)BitConverter.ToUInt32(d, 12);
                string json = System.Text.Encoding.UTF8.GetString(d, 20, Math.Min(len, d.Length - 20));
                if (json.IndexOf("procedural character builder", StringComparison.OrdinalIgnoreCase) < 0) return false;
                // Largest embedded image (bufferView byteLength of images): palette textures are a few KB.
                long biggest = 0;
                var viewArray = System.Text.RegularExpressions.Regex.Match(json, "\"bufferViews\"\\s*:\\s*\\[(.*?)\\]");
                var views = System.Text.RegularExpressions.Regex.Matches(viewArray.Success ? viewArray.Groups[1].Value : "", "\"byteLength\"\\s*:\\s*(\\d+)");
                var images = System.Text.RegularExpressions.Regex.Match(json, "\"images\"\\s*:\\s*\\[(.*?)\\]");
                if (images.Success)
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(images.Groups[1].Value, "\"bufferView\"\\s*:\\s*(\\d+)"))
                    {
                        int view = int.Parse(m.Groups[1].Value);
                        if (view < views.Count) biggest = Math.Max(biggest, long.Parse(views[view].Groups[1].Value));
                    }
                return biggest < 256 * 1024;
            }
            catch { return false; }
        }

        /// <summary>glTFast importer (when installed): ask for Mecanim clips. Legacy clips also work, so this is best-effort.</summary>
        private static void PrepareGltf(string path)
        {
            AssetImporter imp = AssetImporter.GetAtPath(path);
            if (imp == null || imp.GetType().Name.IndexOf("Gltf", StringComparison.OrdinalIgnoreCase) < 0) return;
            try
            {
                const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                object settings = imp.GetType().GetField("importSettings", all)?.GetValue(imp);
                if (settings == null) return;
                FieldInfo method = settings.GetType().GetField("animationMethod", all);
                if (method == null || !method.FieldType.IsEnum) return;
                object mecanim = Enum.Parse(method.FieldType, "Mecanim");
                if (Equals(method.GetValue(settings), mecanim)) return;
                method.SetValue(settings, mecanim);
                EditorUtility.SetDirty(imp);
                imp.SaveAndReimport();
            }
            catch (Exception e) { Debug.LogWarning("[Kitefall] Characters: could not set glTF animation import for " + path + ": " + e.Message); }
        }

        /// <summary>glTFast (the .glb importer) is part of the project, either loaded or listed in Packages/manifest.json.</summary>
        public static bool GltfInstalled()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("glTFast", StringComparison.OrdinalIgnoreCase))) return true;
            try { return File.Exists("Packages/manifest.json") && File.ReadAllText("Packages/manifest.json").Contains(GltfPackage); }
            catch { return false; }
        }

        /// <summary>
        /// Fully automatic: when the editor opens (and again just before Play) the models are checked. glTFast is installed by itself
        /// when .glb characters exist, .glb files imported before glTFast was there are re-imported, and the library is rebuilt
        /// whenever it is missing or out of date – no menu click needed.
        /// </summary>
        [InitializeOnLoad]
        private static class AutoSetup
        {
            private const string AskedKey = "Kitefall.Characters.GltfRequested";

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
                bool anyGlb = files.Any(f => !f.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
                if (anyGlb && !GltfInstalled())
                {
                    if (SessionState.GetBool(AskedKey, false)) return;
                    SessionState.SetBool(AskedKey, true);
                    Debug.Log("[Kitefall] Characters: installing glTFast so Unity can open the .glb character files …");
                    UnityEditor.PackageManager.Client.Add(GltfPackage);
                    return;
                }
                // .glb files imported before glTFast existed are plain files: import them again now.
                bool reimported = false;
                foreach (string f in files)
                    if (!f.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) && !(AssetDatabase.LoadMainAssetAtPath(f) is GameObject))
                    {
                        AssetDatabase.ImportAsset(f, ImportAssetOptions.ForceUpdate);
                        reimported = true;
                    }
                if (reimported || Stale(files))
                {
                    string report = Setup(out int found, out _);
                    if (found == 0) Debug.LogWarning("[Kitefall] Characters: " + report);
                }
            }

            /// <summary>Library missing, or a model file has no entry.</summary>
            private static bool Stale(List<string> files)
            {
                var lib = AssetDatabase.LoadAssetAtPath<CharacterModelLibrary>(LibraryPath);
                if (lib == null) return true;
                var ids = new HashSet<string>(lib.entries.Where(e => e != null && e.model != null).Select(e => e.id), StringComparer.OrdinalIgnoreCase);
                return files.Where(f => !IsAnimFile(f) && (f.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || !IsCodeBuiltGlb(f)))
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Any(n => !ids.Contains(n));
            }
        }

        /// <summary>New or changed files in the characters folder → set up again automatically.</summary>
        private sealed class Watcher : AssetPostprocessor
        {
            private static bool _queued;

            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool touches = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                    .Any(p => p.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase) &&
                              (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)));
                if (!touches || _queued || _running) return;
                _queued = true;
                EditorApplication.delayCall += () =>
                {
                    _queued = false;
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    Setup(out _, out bool needsGltf);
                    if (needsGltf) Debug.LogWarning("[Kitefall] Characters: .glb files need the glTFast package – run Kitefall ▸ Characters ▸ Setup Models to install it.");
                };
            }
        }
    }
}
