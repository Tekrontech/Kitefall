using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Kitefall.UI;
using UnityEditor;
using UnityEngine;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Kitefall > Art > Image Mapper. The artist's files can have ANY name: drop them in Assets/Art/Incoming, then
    /// drag each image onto its slot (or Auto-match / Match by # / Import CSV), and press Install. Install renames each
    /// image to its slot id, moves it to Assets/Resources/KitefallArt (app icon + store art go to Assets/Art/Build),
    /// sets mobile import settings and sets the app icon. The game picks the images up by slot id (see <see cref="Art"/>).
    /// Slots = rows of docs/Kitefall_Image_Assets.xlsx (generated into ArtSlots.Generated.cs).
    /// </summary>
    public sealed class ArtMapperWindow : EditorWindow
    {
        private const string IncomingFolder = "Assets/Art/Incoming";
        private const string RuntimeFolder = "Assets/Resources/" + Art.ResourceFolder;
        private const string BuildFolder = "Assets/Art/Build";
        private const string MappingFile = "Assets/Art/art_mapping.json";
        private static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".webp" };

        [Serializable] private sealed class MapEntry { public string slot; public string guid; }
        [Serializable] private sealed class MapFile { public List<MapEntry> entries = new List<MapEntry>(); }

        private readonly Dictionary<string, string> _assigned = new Dictionary<string, string>(); // slot id -> asset guid (not installed yet)
        private readonly Dictionary<string, bool> _fold = new Dictionary<string, bool>();
        private Vector2 _scroll;
        private string _search = "";
        private bool _onlyMissing;
        private string _status = "";

        [MenuItem("Kitefall/Art/Image Mapper", priority = 40)]
        public static void Open()
        {
            var w = GetWindow<ArtMapperWindow>("Kitefall Image Mapper");
            w.minSize = new Vector2(760, 480);
            w.Load();
        }

        [MenuItem("Kitefall/Art/Open Incoming Folder", priority = 41)]
        public static void OpenIncoming()
        {
            EnsureFolder(IncomingFolder);
            EditorUtility.RevealInFinder(IncomingFolder);
        }

        private void OnEnable() => Load();

        // ---- UI ----------------------------------------------------------------

        private void OnGUI()
        {
            int installed = 0, pending = _assigned.Count;
            foreach (ArtSlot s in ArtSlots.All) if (InstalledPath(s) != null) installed++;

            EditorGUILayout.HelpBox(
                "1) Put your images (any file names) in " + IncomingFolder + ".   2) Drag each image onto its slot, or use Auto-match / Match by # / Import CSV.   " +
                "3) Check the matches, then press INSTALL. Images are renamed to the slot name and moved – nothing is lost.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Incoming folder", GUILayout.Height(26))) OpenIncoming();
                if (GUILayout.Button("Auto-match by name", GUILayout.Height(26))) AutoMatch();
                if (GUILayout.Button("Match by # number", GUILayout.Height(26))) MatchByNumber();
                if (GUILayout.Button("Import CSV…", GUILayout.Height(26))) ImportCsv();
                if (GUILayout.Button("Clear matches", GUILayout.Height(26)) && EditorUtility.DisplayDialog("Clear", "Forget all matches that are not installed yet?", "Clear", "Cancel"))
                {
                    _assigned.Clear(); Save();
                }
                GUI.backgroundColor = new Color(0.5f, 1f, 0.6f);
                using (new EditorGUI.DisabledScope(pending == 0))
                    if (GUILayout.Button($"INSTALL {pending} image(s)", GUILayout.Height(26), GUILayout.Width(170))) Install();
                GUI.backgroundColor = Color.white;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"Installed {installed} / {ArtSlots.All.Length}   ·   waiting to install {pending}   ·   unused in Incoming {UnusedIncoming().Count}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _onlyMissing = GUILayout.Toggle(_onlyMissing, "Only missing", GUILayout.Width(100));
                GUILayout.Label("Search", GUILayout.Width(46));
                _search = GUILayout.TextField(_search, GUILayout.Width(180));
            }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string category = null;
            bool open = true;
            foreach (ArtSlot s in ArtSlots.All)
            {
                if (s.Category != category)
                {
                    category = s.Category;
                    if (!_fold.TryGetValue(category, out open)) open = true;
                    open = EditorGUILayout.Foldout(open, $"{category}  ({CountInstalled(category)}/{CountSlots(category)})", true, EditorStyles.foldoutHeader);
                    _fold[category] = open;
                }
                if (!open) continue;
                if (!string.IsNullOrEmpty(_search) && s.Id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0 && s.Number.ToString() != _search.Trim()) continue;
                string installedPath = InstalledPath(s);
                bool waiting = _assigned.ContainsKey(s.Id);
                if (_onlyMissing && (installedPath != null || waiting)) continue;
                Row(s, installedPath, waiting);
            }
            EditorGUILayout.EndScrollView();

            // Drop images anywhere on the window onto the slot whose name matches best.
            Event e = Event.current;
            if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && DragAndDrop.objectReferences.Length > 1)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    var list = new List<string>();
                    foreach (UnityEngine.Object o in DragAndDrop.objectReferences)
                        if (o is Texture2D) list.Add(AssetDatabase.GetAssetPath(o));
                    AutoMatch(list);
                }
                e.Use();
            }
        }

        private void Row(ArtSlot s, string installedPath, bool waiting)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Texture2D current = null;
                if (waiting) current = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(_assigned[s.Id]));
                else if (installedPath != null) current = AssetDatabase.LoadAssetAtPath<Texture2D>(installedPath);

                var picked = (Texture2D)EditorGUILayout.ObjectField(current, typeof(Texture2D), false, GUILayout.Width(56), GUILayout.Height(56));
                if (picked != current)
                {
                    if (picked == null) _assigned.Remove(s.Id);
                    else _assigned[s.Id] = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked));
                    Save();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label($"#{s.Number}   {s.Id}", EditorStyles.boldLabel);
                    GUILayout.Label($"Size {s.Size}{(s.BuildOnly ? "   ·   build only (not in the game download)" : "")}", EditorStyles.miniLabel);
                    if (waiting)
                    {
                        string src = AssetDatabase.GUIDToAssetPath(_assigned[s.Id]);
                        GUILayout.Label("<color=#E0B040>Waiting to install:</color> " + Path.GetFileName(src) + SizeWarning(current, s), Rich());
                    }
                    else if (installedPath != null) GUILayout.Label("<color=#50C878>✔ Installed</color>" + SizeWarning(current, s), Rich());
                    else GUILayout.Label("<color=#C05050>Missing</color> – the game uses its built-in drawing for now", Rich());
                }
                if (waiting && GUILayout.Button("✕", GUILayout.Width(26))) { _assigned.Remove(s.Id); Save(); }
            }
        }

        private static GUIStyle _rich;
        private static GUIStyle Rich() => _rich ?? (_rich = new GUIStyle(EditorStyles.label) { richText = true });

        private static string SizeWarning(Texture2D tex, ArtSlot s)
        {
            if (tex == null) return "";
            Vector2Int want = ParseSize(s.Size);
            float wa = want.x / (float)Mathf.Max(1, want.y), ha = tex.width / (float)Mathf.Max(1, tex.height);
            if (Mathf.Abs(wa - ha) > 0.08f * wa) return $"   <color=#E08040>(shape {tex.width}x{tex.height} – expected {s.Size})</color>";
            return "";
        }

        private int CountSlots(string category) { int n = 0; foreach (ArtSlot s in ArtSlots.All) if (s.Category == category) n++; return n; }
        private int CountInstalled(string category) { int n = 0; foreach (ArtSlot s in ArtSlots.All) if (s.Category == category && InstalledPath(s) != null) n++; return n; }

        // ---- Matching --------------------------------------------------------------

        private static readonly HashSet<string> Stop = new HashSet<string>
        {
            "png", "jpg", "jpeg", "final", "img", "image", "copy", "new", "the", "a", "of", "and", "with", "kitefall", "mj", "midjourney",
            "leonardo", "dalle", "sd", "sdxl", "ai", "4k", "hd", "upscaled", "upscale", "edit", "edited", "art", "v1", "v2", "v3", "version"
        };

        private static readonly Dictionary<string, string> Synonyms = new Dictionary<string, string>
        {
            { "background", "bg" }, { "backdrop", "bg" }, { "character", "char" }, { "face", "portrait" }, { "avatar", "portrait" },
            { "full", "fullbody" }, { "body", "fullbody" }, { "tail", "dum" }, { "string", "dor" }, { "thread", "dor" }, { "spool", "charkhi" },
            { "reel", "charkhi" }, { "coin", "coins" }, { "gem", "gems" }, { "badge", "rank" }, { "lobby", "squad" }, { "chest", "daily" },
            { "seaglass", "sea" }, { "nightbazaar", "night" }, { "lkf", "last" }
        };

        private static List<string> Tokens(string name)
        {
            name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            name = Regex.Replace(name, "([A-Za-z])([0-9])|([0-9])([A-Za-z])", m => m.Value[0] + " " + m.Value.Substring(1));
            var list = new List<string>();
            foreach (string raw in Regex.Split(name.ToLowerInvariant(), "[^a-z0-9]+"))
            {
                if (raw.Length == 0 || Stop.Contains(raw)) continue;
                list.Add(Synonyms.TryGetValue(raw, out string syn) ? syn : raw);
            }
            return list;
        }

        private static string Compact(string s) => Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]", "");

        private static float Score(ArtSlot slot, string fileName)
        {
            if (Compact(slot.Id) == Compact(fileName)) return 10f;
            List<string> want = Tokens(slot.Id);
            var have = new HashSet<string>(Tokens(fileName));
            string compactFile = Compact(fileName);
            float got = 0f, total = 0f;
            bool distinctive = false;
            foreach (string t in want)
            {
                float w = t.Length >= 3 && !char.IsDigit(t[0]) ? 1f : 0.5f;
                total += w;
                if (have.Contains(t) || t.Length >= 4 && compactFile.Contains(t)) { got += w; if (w >= 1f) distinctive = true; }
            }
            if (!distinctive || total <= 0f) return 0f;
            // Extra words in the file name that the slot does not have make the match a bit less sure.
            int extra = 0;
            foreach (string t in have) if (!want.Contains(t) && !Regex.IsMatch(t, "^[0-9]+$")) extra++;
            return got / total - 0.04f * extra;
        }

        private void AutoMatch() => AutoMatch(null);

        private void AutoMatch(List<string> files)
        {
            files = files ?? UnusedIncoming();
            var candidates = new List<(float score, string slot, string path)>();
            foreach (string path in files)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                foreach (ArtSlot s in ArtSlots.All)
                {
                    if (_assigned.ContainsKey(s.Id) || InstalledPath(s) != null) continue;
                    float sc = Score(s, name);
                    if (sc >= 0.5f) candidates.Add((sc, s.Id, path));
                }
            }
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var usedFiles = new HashSet<string>();
            int matched = 0;
            foreach (var c in candidates)
            {
                if (usedFiles.Contains(c.path) || _assigned.ContainsKey(c.slot)) continue;
                _assigned[c.slot] = AssetDatabase.AssetPathToGUID(c.path);
                usedFiles.Add(c.path);
                matched++;
            }
            Save();
            _status = $"Auto-match: {matched} image(s) matched, {files.Count - matched} left. Check the yellow rows, then INSTALL. Drag the rest by hand.";
        }

        /// <summary>Files named only with the Excel row number, e.g. "45.png", "image (45).jpg", "img_045.png".</summary>
        private void MatchByNumber()
        {
            int matched = 0;
            foreach (string path in UnusedIncoming())
            {
                MatchCollection nums = Regex.Matches(Path.GetFileNameWithoutExtension(path), "[0-9]+");
                if (nums.Count != 1 || !int.TryParse(nums[0].Value, out int n)) continue;
                foreach (ArtSlot s in ArtSlots.All)
                {
                    if (s.Number != n || _assigned.ContainsKey(s.Id)) continue;
                    _assigned[s.Id] = AssetDatabase.AssetPathToGUID(path);
                    matched++;
                    break;
                }
            }
            Save();
            _status = $"Match by #: {matched} image(s) matched by their row number in the Excel sheet.";
        }

        /// <summary>
        /// The Excel sheet saved as CSV (columns "File name" and "Your file name"), or any CSV with two columns:
        /// slot (file name, slot id or # number) and your file name.
        /// </summary>
        private void ImportCsv()
        {
            string csv = EditorUtility.OpenFilePanel("Kitefall image list (CSV)", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(csv)) return;
            List<List<string>> rows = ParseCsv(File.ReadAllText(csv));
            if (rows.Count == 0) return;
            int slotCol = 0, yourCol = 1, start = 0;
            for (int i = 0; i < rows[0].Count; i++)
            {
                string h = rows[0][i].Trim().ToLowerInvariant();
                if (h == "file name") { slotCol = i; start = 1; }
                if (h.StartsWith("your file")) { yourCol = i; start = 1; }
            }
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                byName[Path.GetFileName(p)] = guid;
                byName[Path.GetFileNameWithoutExtension(p)] = guid;
            }
            int matched = 0, notFound = 0;
            var missing = new StringBuilder();
            for (int r = start; r < rows.Count; r++)
            {
                List<string> row = rows[r];
                if (row.Count <= Mathf.Max(slotCol, yourCol)) continue;
                string slotKey = row[slotCol].Trim(), yours = row[yourCol].Trim();
                if (slotKey.Length == 0 || yours.Length == 0) continue;
                ArtSlot? slot = FindSlot(slotKey);
                if (slot == null) continue;
                if (byName.TryGetValue(yours, out string g) || byName.TryGetValue(Path.GetFileNameWithoutExtension(yours), out g))
                {
                    _assigned[slot.Value.Id] = g;
                    matched++;
                }
                else { notFound++; if (missing.Length < 400) missing.Append(yours).Append(", "); }
            }
            Save();
            _status = $"CSV: {matched} matched." + (notFound > 0 ? $" {notFound} file(s) not found in the project: {missing}" : "");
        }

        private static ArtSlot? FindSlot(string key)
        {
            string stem = Path.GetFileNameWithoutExtension(key);
            int.TryParse(key, out int n);
            foreach (ArtSlot s in ArtSlots.All)
                if (s.Id.Equals(stem, StringComparison.OrdinalIgnoreCase) || n > 0 && s.Number == n) return s;
            return null;
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            char sep = text.IndexOf(';') >= 0 && text.IndexOf(',') < 0 ? ';' : ',';
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else cell.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == sep) { row.Add(cell.ToString()); cell.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        // ---- Install ---------------------------------------------------------------

        private void Install()
        {
            int replace = 0;
            foreach (var kv in _assigned)
                if (ArtSlots.TryGet(kv.Key, out ArtSlot s) && InstalledPath(s) != null && InstalledPath(s) != AssetDatabase.GUIDToAssetPath(kv.Value)) replace++;
            if (replace > 0 && !EditorUtility.DisplayDialog("Replace images",
                    $"{replace} slot(s) already have an installed image. The old image will be deleted and replaced by the new one.", "Replace", "Cancel"))
                return;

            EnsureFolder(RuntimeFolder);
            EnsureFolder(BuildFolder);
            int ok = 0;
            var errors = new StringBuilder();
            var done = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in _assigned)
                {
                    if (!ArtSlots.TryGet(kv.Key, out ArtSlot s)) continue;
                    string src = AssetDatabase.GUIDToAssetPath(kv.Value);
                    if (string.IsNullOrEmpty(src) || !File.Exists(src)) { errors.Append($"#{s.Number} {s.Id}: file is gone\n"); continue; }
                    string ext = Path.GetExtension(src).ToLowerInvariant();
                    string target = (s.BuildOnly ? BuildFolder : RuntimeFolder) + "/" + s.Id + ext;
                    if (src != target)
                    {
                        foreach (string e in ImageExt)
                        {
                            string old = (s.BuildOnly ? BuildFolder : RuntimeFolder) + "/" + s.Id + e;
                            if (old != src && File.Exists(old)) AssetDatabase.DeleteAsset(old);
                        }
                        string err = AssetDatabase.MoveAsset(src, target);
                        if (!string.IsNullOrEmpty(err)) { errors.Append($"#{s.Number} {s.Id}: {err}\n"); continue; }
                    }
                    done.Add(kv.Key);
                    ok++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            foreach (string id in done)
            {
                _assigned.Remove(id);
                if (ArtSlots.TryGet(id, out ArtSlot s)) ApplyImportSettings(s, InstalledPath(s));
            }
            SetAppIcon();
            Save();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Art.ClearCache();
            _status = $"Installed {ok} image(s)." + (errors.Length > 0 ? "\nProblems:\n" + errors : "");
            Debug.Log("[Kitefall Art] " + _status);
        }

        private static void ApplyImportSettings(ArtSlot s, string path)
        {
            if (path == null || !(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            Vector2Int size = ParseSize(s.Size);
            int max = Mathf.Min(2048, Mathf.NextPowerOfTwo(Mathf.Max(size.x, size.y)));
            bool surface = s.Category == "Kite skins" || s.Category == "Environment textures";
            bool picture = s.Category.Contains("background") || s.Category.Contains("Loading") || s.Category == "Arenas" ||
                           s.Category == "Game modes" || s.Category == "Onboarding" || s.Id == "splash_screen" || s.Id == "pass_banner";

            if (s.BuildOnly)
            {
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = Mathf.Max(max, 1024);
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            }
            else
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = surface;
                ti.wrapMode = s.Category == "Environment textures" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.maxTextureSize = max;
                ti.textureCompression = TextureImporterCompression.Compressed;
                TextureImporterPlatformSettings android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = max;
                android.format = picture ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(android);
            }
            ti.SaveAndReimport();
        }

        /// <summary>Sets the default app icon from app_icon (the adaptive layers are set in Player Settings > Icon by hand).</summary>
        private static void SetAppIcon()
        {
            if (!ArtSlots.TryGet("app_icon", out ArtSlot slot)) return;
            string path = InstalledPath(slot);
            var tex = path != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
            if (tex == null) return;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
#else
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { tex });
#endif
        }

        // ---- Helpers ---------------------------------------------------------------

        private static string InstalledPath(ArtSlot s)
        {
            string folder = s.BuildOnly ? BuildFolder : RuntimeFolder;
            foreach (string e in ImageExt)
            {
                string p = folder + "/" + s.Id + e;
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private List<string> UnusedIncoming()
        {
            var list = new List<string>();
            if (!AssetDatabase.IsValidFolder(IncomingFolder)) return list;
            var used = new HashSet<string>(_assigned.Values);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IncomingFolder }))
                if (!used.Contains(guid)) list.Add(AssetDatabase.GUIDToAssetPath(guid));
            return list;
        }

        private static Vector2Int ParseSize(string size)
        {
            System.Text.RegularExpressions.Match m = Regex.Match(size ?? "", "([0-9]+)x([0-9]+)");
            return m.Success ? new Vector2Int(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : new Vector2Int(512, 512);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private void Load()
        {
            _assigned.Clear();
            if (!File.Exists(MappingFile)) return;
            try
            {
                var map = JsonUtility.FromJson<MapFile>(File.ReadAllText(MappingFile));
                foreach (MapEntry e in map.entries)
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(e.guid))) _assigned[e.slot] = e.guid;
            }
            catch (Exception ex) { Debug.LogWarning("[Kitefall Art] Could not read " + MappingFile + ": " + ex.Message); }
        }

        private void Save()
        {
            EnsureFolder("Assets/Art");
            var map = new MapFile();
            foreach (var kv in _assigned) map.entries.Add(new MapEntry { slot = kv.Key, guid = kv.Value });
            File.WriteAllText(MappingFile, JsonUtility.ToJson(map, true));
        }
    }
}
