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

        [Serializable] private sealed class MapEntry { public string slot; public string guid; public bool guess; }
        [Serializable] private sealed class NameEntry { public string slot; public string original; }
        [Serializable]
        private sealed class MapFile
        {
            public int version;
            public List<MapEntry> entries = new List<MapEntry>();
            public List<NameEntry> installedFrom = new List<NameEntry>(); // original file name of each installed image
            public List<string> blocked = new List<string>();             // "guid|slot": the user sent this image back from that slot
            public List<string> manual = new List<string>();
        }

        private readonly Dictionary<string, string> _assigned = new Dictionary<string, string>(); // slot id -> asset guid (not installed yet)
        private readonly HashSet<string> _guess = new HashSet<string>();      // pending matches AUTO must not install (by-order guesses)
        private readonly Dictionary<string, string> _installedFrom = new Dictionary<string, string>();
        private readonly HashSet<string> _blocked = new HashSet<string>();
        private readonly HashSet<string> _manual = new HashSet<string>();     // slots the user filled by hand (pending or installed)
        private const string DuplicatesFolder = "ArtDuplicates"; // outside Assets: not imported, not in the game
        private readonly Dictionary<string, bool> _fold = new Dictionary<string, bool>();
        private Vector2 _scroll;
        private string _search = "";
        private bool _onlyMissing, _onlyInstalled;
        private string _status = "";
        private bool _showLeft;

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
                "Copy images (any names) into " + IncomingFolder + " – AUTO matches them by name, by # number and by what the name describes, " +
                "then installs them. Review with 'Only installed' and press 'Wrong' on a misplaced image. Leftovers: drag onto a slot, then INSTALL.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Incoming folder", GUILayout.Height(26))) OpenIncoming();
                if (GUILayout.Button("Auto-match by name", GUILayout.Height(26))) { AutoMatch(); MatchByDescription(); }
                if (GUILayout.Button("Match by # number", GUILayout.Height(26))) MatchByNumber();
                if (GUILayout.Button("Import CSV…", GUILayout.Height(26))) ImportCsv();
                if (GUILayout.Button("Match leftovers by order", GUILayout.Height(26))) MatchLeftoversByOrder();
                if (GUILayout.Button("Export missing list", GUILayout.Height(26))) ExportMissing();
                if (GUILayout.Button("Clear matches", GUILayout.Height(26)) && EditorUtility.DisplayDialog("Clear", "Forget all matches that are not installed yet?", "Clear", "Cancel"))
                {
                    _assigned.Clear(); _guess.Clear(); Save();
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
                _onlyInstalled = GUILayout.Toggle(_onlyInstalled, "Only installed", GUILayout.Width(105));
                GUILayout.Label("Search", GUILayout.Width(46));
                _search = GUILayout.TextField(_search, GUILayout.Width(180));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button("★ AUTO: match + install everything in Incoming", GUILayout.Height(30))) AutoInstallAll();
                GUI.backgroundColor = Color.white;
                IncomingWatcher.Enabled = GUILayout.Toggle(IncomingWatcher.Enabled, "Run AUTO when new images arrive", GUILayout.Width(230));
            }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            List<string> leftovers = UnusedIncoming();
            if (leftovers.Count > 0)
            {
                _showLeft = EditorGUILayout.Foldout(_showLeft, $"Not recognised yet: {leftovers.Count} image(s) in Incoming (drag one onto a slot below)", true);
                if (_showLeft)
                    foreach (string l in leftovers)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<Texture2D>(l), typeof(Texture2D), false, GUILayout.Width(220));
                            GUILayout.Label(l.Substring(Mathf.Min(l.Length, IncomingFolder.Length + 1)), EditorStyles.miniLabel);
                        }
            }

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
                if (_onlyInstalled && installedPath == null) continue;
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
                    _guess.Remove(s.Id);
                    if (picked != null) _manual.Add(s.Id); else _manual.Remove(s.Id);
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
                        string tag = _guess.Contains(s.Id) ? "<color=#E08040>Guess (by order) – check, then INSTALL:</color> " : "<color=#E0B040>Waiting to install:</color> ";
                        GUILayout.Label(tag + Path.GetFileName(src) + SizeWarning(current, s), Rich());
                    }
                    else if (installedPath != null)
                    {
                        string from = _installedFrom.TryGetValue(s.Id, out string o) ? "  from \"" + o + "\"" : "";
                        GUILayout.Label("<color=#50C878>✔ Installed</color>" + from + SizeWarning(current, s), Rich());
                    }
                    else GUILayout.Label("<color=#C05050>Missing</color> – the game uses its built-in drawing for now", Rich());
                }
                if (waiting && GUILayout.Button("✕", GUILayout.Width(26))) { _assigned.Remove(s.Id); _guess.Remove(s.Id); Save(); }
                if (!waiting && installedPath != null && GUILayout.Button(new GUIContent("Wrong", "Send this image back to Incoming – AUTO will not put it in this slot again"), GUILayout.Width(52)))
                    SendBack(s, installedPath);
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
            // Look inside glued names ("zarafacefinal") only – in normal names "workshop" must not count as "shop".
            bool glued = have.Count <= 1;
            float got = 0f, total = 0f;
            bool distinctive = false;
            foreach (string t in want)
            {
                float w = t.Length >= 3 && !char.IsDigit(t[0]) ? 1f : 0.5f;
                total += w;
                if (have.Contains(t) || glued && t.Length >= 4 && compactFile.Contains(t)) { got += w; if (w >= 1f) distinctive = true; }
            }
            if (!distinctive || total <= 0f) return 0f;
            // Extra words in the file name that the slot does not have make the match a bit less sure.
            int extra = 0;
            foreach (string t in have) if (!want.Contains(t) && !Regex.IsMatch(t, "^[0-9]+$")) extra++;
            return got / total - Mathf.Min(0.1f, 0.02f * extra);
        }

        private void AutoMatch() => AutoMatch(null);

        private void AutoMatch(List<string> files)
        {
            files = files ?? UnusedIncoming();
            var candidates = new List<(float score, string slot, string path)>();
            foreach (string path in files)
            {
                string name = NameWithFolders(path);
                float aspect = SourceAspect(path);
                foreach (ArtSlot s in ArtSlots.All)
                {
                    if (_assigned.ContainsKey(s.Id) || InstalledPath(s) != null) continue;
                    if (_blocked.Contains(AssetDatabase.AssetPathToGUID(path) + "|" + s.Id)) continue;
                    float sc = Score(s, name);
                    if (sc <= 0f) continue;
                    if (!AspectFits(s, aspect)) sc -= 0.3f; // a wide picture is not an icon
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
            _lastMatched = matched;
        }

        private int _lastMatched;

        /// <summary>File name plus its sub-folders inside Incoming ("Characters/Zara/face" counts as "characters zara face").</summary>
        private static string NameWithFolders(string path)
        {
            string rel = path.StartsWith(IncomingFolder + "/") ? path.Substring(IncomingFolder.Length + 1) : Path.GetFileName(path);
            rel = rel.Substring(0, rel.Length - Path.GetExtension(rel).Length);
            return rel.Replace('/', ' ');
        }

        private static float SourceAspect(string path)
        {
            int w = 0, h = 0;
#if UNITY_2021_2_OR_NEWER
            if (AssetImporter.GetAtPath(path) is TextureImporter ti) ti.GetSourceTextureWidthAndHeight(out w, out h);
#endif
            if (w <= 0 || h <= 0)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) { w = tex.width; h = tex.height; }
            }
            return w > 0 && h > 0 ? w / (float)h : 0f;
        }

        private static bool AspectFits(ArtSlot s, float aspect, float tolerance = 0.25f)
        {
            if (aspect <= 0f) return true;
            Vector2Int want = ParseSize(s.Size);
            float wa = want.x / (float)Mathf.Max(1, want.y);
            return Mathf.Abs(aspect - wa) <= tolerance * wa;
        }

        /// <summary>
        /// Last resort for files whose names say nothing (e.g. "mj_83749.png"): the leftovers, sorted by file name, fill the
        /// missing slots in Excel order (a slot is skipped when the picture's shape does not fit). Always check before INSTALL.
        /// </summary>
        private void MatchLeftoversByOrder()
        {
            List<string> files = UnusedIncoming();
            files.Sort(NaturalCompare);
            int f = 0, matched = 0;
            foreach (ArtSlot s in ArtSlots.All)
            {
                if (f >= files.Count) break;
                if (_assigned.ContainsKey(s.Id) || InstalledPath(s) != null) continue;
                if (!AspectFits(s, SourceAspect(files[f]))) continue;
                _assigned[s.Id] = AssetDatabase.AssetPathToGUID(files[f++]);
                _guess.Add(s.Id);
                matched++;
            }
            Save();
            _status = $"By order: {matched} image(s) placed in Excel order. CHECK every yellow row before INSTALL – order matching can be wrong.";
        }

        private static int NaturalCompare(string a, string b)
        {
            string pa = Regex.Replace(a.ToLowerInvariant(), "[0-9]+", m => m.Value.PadLeft(8, '0'));
            string pb = Regex.Replace(b.ToLowerInvariant(), "[0-9]+", m => m.Value.PadLeft(8, '0'));
            return string.CompareOrdinal(pa, pb);
        }

        // ---- Fully automatic -------------------------------------------------------

        /// <summary>Runs on its own after images are copied into Incoming (see <see cref="IncomingWatcher"/>), or from the menu.</summary>
        [MenuItem("Kitefall/Art/Auto Install All Images", priority = 42)]
        public static void AutoInstallAll()
        {
            var w = GetWindow<ArtMapperWindow>("Kitefall Image Mapper");
            w.minSize = new Vector2(760, 480);
            w.Load();
            // By-order guesses hold files hostage: let AUTO try those files properly first.
            foreach (string g in new List<string>(w._guess)) w._assigned.Remove(g);
            w._guess.Clear();
            int dupes = w.RemoveDuplicates();
            int strong = w.MatchStrong();
            int before = w._assigned.Count - w._guess.Count;
            w.AutoMatch();
            int byName = w._assigned.Count - w._guess.Count - before;
            w.MatchByNumber();
            int byNumber = w._assigned.Count - w._guess.Count - before - byName;
            int byDescription = w.MatchByDescription();
            int byShape = w.FillLoadingScreensByShape();
            int total = w._assigned.Count - w._guess.Count;
            int left = w.UnusedIncoming().Count;
            if (total > 0) w.Install(confirm: false, includeGuesses: false);
            IncomingWatcher.MarkDone();
            w._status = $"AUTO: {total} image(s) installed ({strong} sure matches, {byName} by name, {byNumber} by # number, {byDescription} by picture description, {byShape} as loading screens). " +
                        (dupes > 0 ? $"{dupes} duplicate copies moved out to the '{DuplicatesFolder}' folder next to Assets. " : "") +
                        "Check with 'Only installed' – press 'Wrong' on any image in the wrong place. " +
                        (left > 0 ? $"{left} image(s) not recognised – their names are listed at the top. Rename them like the Excel 'File name', " +
                                    "drag them onto a slot, or press 'Match leftovers by order'." : "Every image found its slot.");
            Debug.Log("[Kitefall Art] " + w._status);
            w.Repaint();
        }

        /// <summary>Files named only with the Excel row number, e.g. "45.png", "image (45).jpg", "img_045.png".</summary>
        private void MatchByNumber()
        {
            int matched = 0;
            foreach (string path in UnusedIncoming())
            {
                string name = Path.GetFileNameWithoutExtension(path);
                // Only names that are just a number ("45", "image (45)", "img_045") – never "loading_03_river".
                bool words = false;
                foreach (string t in Tokens(name)) if (!Regex.IsMatch(t, "^[0-9]+$")) words = true;
                MatchCollection nums = Regex.Matches(name, "[0-9]+");
                if (words || nums.Count != 1 || !int.TryParse(nums[0].Value, out int n)) continue;
                foreach (ArtSlot s in ArtSlots.All)
                {
                    if (s.Number != n || _assigned.ContainsKey(s.Id) || InstalledPath(s) != null) continue;
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

        private void Install(bool confirm = true, bool includeGuesses = true)
        {
            int replace = 0;
            foreach (var kv in _assigned)
                if (ArtSlots.TryGet(kv.Key, out ArtSlot s) && InstalledPath(s) != null && InstalledPath(s) != AssetDatabase.GUIDToAssetPath(kv.Value)) replace++;
            if (replace > 0 && confirm && !EditorUtility.DisplayDialog("Replace images",
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
                    if (!includeGuesses && _guess.Contains(kv.Key)) continue;
                    string src = AssetDatabase.GUIDToAssetPath(kv.Value);
                    if (src.StartsWith(IncomingFolder + "/")) _installedFrom[s.Id] = Path.GetFileName(src);
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
                _guess.Remove(id);
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
                // UI kit skins stretch as 9-slice: keep the rounded corners / rim unscaled.
                if (s.Category == "UI kit" && !s.Id.StartsWith("ui_icon_")) ti.spriteBorder = new Vector4(44, 44, 44, 44);
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

        // ---- Description / duplicates / send back -------------------------------------

        /// <summary>Names that describe the picture ("Golden Tailor's Chamber" -> bg_wardrobe), checked against each slot's prompt.</summary>
        private int MatchByDescription()
        {
            var candidates = new List<(float score, string slot, string path)>();
            foreach (string path in UnusedIncoming())
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                string name = NameWithFolders(path);
                float aspect = SourceAspect(path);
                foreach (ArtSlot s in ArtSlots.All)
                {
                    if (_assigned.ContainsKey(s.Id) || InstalledPath(s) != null || _blocked.Contains(guid + "|" + s.Id)) continue;
                    if (!AspectFits(s, aspect, 0.35f)) continue;
                    float sc = ArtDescriptionMatcher.Score(s.Id, name);
                    if (s.BuildOnly) sc *= 0.85f; // prefer pictures the player actually sees
                    if (sc >= 0.45f) candidates.Add((sc, s.Id, path));
                }
            }
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var used = new HashSet<string>();
            int matched = 0;
            foreach (var c in candidates)
            {
                if (used.Contains(c.path) || _assigned.ContainsKey(c.slot)) continue;
                _assigned[c.slot] = AssetDatabase.AssetPathToGUID(c.path);
                _guess.Remove(c.slot);
                used.Add(c.path);
                matched++;
            }
            Save();
            _status = $"By description: {matched} image(s) matched from what their names describe.";
            return matched;
        }

        /// <summary>Loading screens are interchangeable: wide festival scenes nobody claimed fill the empty ones.</summary>
        private int FillLoadingScreensByShape()
        {
            List<string> files = UnusedIncoming();
            files.Sort(NaturalCompare);
            int f = 0, matched = 0;
            foreach (ArtSlot s in ArtSlots.All)
            {
                if (s.Category != "Loading screens" || _assigned.ContainsKey(s.Id) || InstalledPath(s) != null) continue;
                while (f < files.Count && !AspectFits(s, SourceAspect(files[f]), 0.35f)) f++;
                if (f >= files.Count) break;
                _assigned[s.Id] = AssetDatabase.AssetPathToGUID(files[f++]);
                _guess.Remove(s.Id);
                matched++;
            }
            Save();
            return matched;
        }

        /// <summary>Same picture twice ("x - Copy.png", "x (1).png"): keeps one, moves the rest out of the project.</summary>
        private int RemoveDuplicates()
        {
            if (!AssetDatabase.IsValidFolder(IncomingFolder)) return 0;
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IncomingFolder })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            // Shortest / cleanest name first, so "x.png" is kept and "x - Copy.png" goes.
            paths.Sort((a, b) =>
            {
                int ca = Regex.IsMatch(a, @"( - Copy|\([0-9]+\))", RegexOptions.IgnoreCase) ? 1 : 0;
                int cb = Regex.IsMatch(b, @"( - Copy|\([0-9]+\))", RegexOptions.IgnoreCase) ? 1 : 0;
                return ca != cb ? ca - cb : a.Length != b.Length ? a.Length - b.Length : string.CompareOrdinal(a, b);
            });
            var seen = new HashSet<string>();
            var pending = new HashSet<string>(_assigned.Values);
            int moved = 0;
            // "x - Copy.png" / "x (1).png" whose original "x.png" is here or already installed is a copy even if the bytes differ.
            var originals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in paths) if (!IsCopyName(p)) originals.Add(Path.GetFileNameWithoutExtension(p));
            foreach (string o in _installedFrom.Values) originals.Add(Path.GetFileNameWithoutExtension(o));
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                foreach (string p in paths)
                {
                    string hash;
                    try { hash = BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(p))); }
                    catch (IOException) { continue; }
                    bool copyOfOriginal = IsCopyName(p) && originals.Contains(BaseName(p));
                    if (seen.Add(hash) && !copyOfOriginal) continue;
                    if (pending.Contains(AssetDatabase.AssetPathToGUID(p))) continue;
                    Directory.CreateDirectory(DuplicatesFolder);
                    string dest = Path.Combine(DuplicatesFolder, Path.GetFileName(p));
                    if (File.Exists(dest)) dest = Path.Combine(DuplicatesFolder, Guid.NewGuid().ToString("N").Substring(0, 6) + "_" + Path.GetFileName(p));
                    File.Copy(p, dest);
                    AssetDatabase.DeleteAsset(p);
                    moved++;
                }
            }
            return moved;
        }

        private static bool IsCopyName(string path) => Regex.IsMatch(Path.GetFileNameWithoutExtension(path), @"( - Copy( \([0-9]+\))?| \([0-9]+\))$", RegexOptions.IgnoreCase);

        private static string BaseName(string path) =>
            Regex.Replace(Path.GetFileNameWithoutExtension(path), @"( - Copy( \([0-9]+\))?| \([0-9]+\))$", "", RegexOptions.IgnoreCase);

        /// <summary>How sure we are that a file name belongs to a slot (0..1+): exact id, kite+palette, slot words, description.</summary>
        private static float Certainty(ArtSlot s, string name)
        {
            string skin = ArtDescriptionMatcher.SkinFromName(name);
            if (skin != null && (s.Id == skin || s.Id.StartsWith(skin + "_"))) return 1f;
            return Mathf.Max(Score(s, name), ArtDescriptionMatcher.Score(s.Id, name));
        }

        /// <summary>
        /// Sure matches first (exact slot name, kite + palette, ≥ 0.9 certain). If AUTO earlier put a clearly worse image in that
        /// slot, that image goes back to Incoming to be matched again. Images placed by hand (drag) are never moved.
        /// </summary>
        private int MatchStrong()
        {
            int matched = 0;
            foreach (string path in UnusedIncoming())
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                string name = NameWithFolders(path);
                float aspect = SourceAspect(path);
                ArtSlot best = default;
                float bestScore = 0f;
                foreach (ArtSlot s in ArtSlots.All)
                {
                    if (_assigned.ContainsKey(s.Id) || _blocked.Contains(guid + "|" + s.Id) || !AspectFits(s, aspect, 0.35f)) continue;
                    float c = Certainty(s, name);
                    if (c > bestScore) { bestScore = c; best = s; }
                }
                if (bestScore < 0.9f) continue;
                string installed = InstalledPath(best);
                if (installed != null)
                {
                    if (_manual.Contains(best.Id) || !_installedFrom.TryGetValue(best.Id, out string oldName)) continue;
                    if (Certainty(best, Path.GetFileNameWithoutExtension(oldName)) > bestScore - 0.25f) continue;
                    if (!ReturnToIncoming(best, installed, block: false)) continue;
                }
                _assigned[best.Id] = guid;
                _guess.Remove(best.Id);
                matched++;
            }
            Save();
            return matched;
        }

        /// <summary>"Wrong" button: the installed image goes back to Incoming under its original name and never returns to this slot.</summary>
        private void SendBack(ArtSlot s, string installedPath)
        {
            if (!ReturnToIncoming(s, installedPath, block: true)) return;
            Save();
            Art.ClearCache();
            _status = $"#{s.Number} {s.Id}: image sent back to Incoming. Drag the right image onto this slot, or press AUTO.";
        }

        private bool ReturnToIncoming(ArtSlot s, string installedPath, bool block)
        {
            EnsureFolder(IncomingFolder);
            string name = _installedFrom.TryGetValue(s.Id, out string o) ? o : "returned_from_slot" + Path.GetExtension(installedPath);
            string target = AssetDatabase.GenerateUniqueAssetPath(IncomingFolder + "/" + name);
            string guid = AssetDatabase.AssetPathToGUID(installedPath);
            IncomingWatcher.Suspend();
            string err = AssetDatabase.MoveAsset(installedPath, target);
            if (!string.IsNullOrEmpty(err)) { _status = "Could not move it back: " + err; return false; }
            if (block) _blocked.Add(guid + "|" + s.Id);
            _installedFrom.Remove(s.Id);
            _manual.Remove(s.Id);
            return true;
        }

        /// <summary>Writes Assets/Art/missing_images.csv: every slot without an image (# matches the Excel sheet, where the prompt is).</summary>
        private void ExportMissing()
        {
            EnsureFolder("Assets/Art");
            var sb = new StringBuilder("#,File name,Category,Size\n");
            int n = 0;
            foreach (ArtSlot s in ArtSlots.All)
            {
                if (InstalledPath(s) != null || _assigned.ContainsKey(s.Id)) continue;
                sb.Append(s.Number).Append(',').Append(s.Id).Append(".png,").Append(s.Category).Append(',').Append(s.Size).Append('\n');
                n++;
            }
            const string path = "Assets/Art/missing_images.csv";
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.ImportAsset(path);
            EditorUtility.RevealInFinder(path);
            _status = $"{n} image(s) still missing – list saved to {path}. Find each # in the Excel sheet for its AI prompt.";
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
            _assigned.Clear(); _guess.Clear(); _installedFrom.Clear(); _blocked.Clear(); _manual.Clear();
            if (!File.Exists(MappingFile)) return;
            try
            {
                var map = JsonUtility.FromJson<MapFile>(File.ReadAllText(MappingFile));
                foreach (MapEntry e in map.entries)
                {
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(e.guid))) continue;
                    _assigned[e.slot] = e.guid;
                    // Files from the first version did not record guesses: treat their pending matches as guesses (never auto-installed).
                    if (e.guess || map.version < 2) _guess.Add(e.slot);
                }
                foreach (NameEntry n in map.installedFrom) _installedFrom[n.slot] = n.original;
                foreach (string b in map.blocked) _blocked.Add(b);
                foreach (string m in map.manual) _manual.Add(m);
            }
            catch (Exception ex) { Debug.LogWarning("[Kitefall Art] Could not read " + MappingFile + ": " + ex.Message); }
        }

        private void Save()
        {
            EnsureFolder("Assets/Art");
            var map = new MapFile { version = 2 };
            foreach (var kv in _assigned) map.entries.Add(new MapEntry { slot = kv.Key, guid = kv.Value, guess = _guess.Contains(kv.Key) });
            foreach (var kv in _installedFrom) map.installedFrom.Add(new NameEntry { slot = kv.Key, original = kv.Value });
            map.blocked.AddRange(_blocked);
            map.manual.AddRange(_manual);
            File.WriteAllText(MappingFile, JsonUtility.ToJson(map, true));
        }
    }

    /// <summary>Starts the AUTO install shortly after new images land in Assets/Art/Incoming (one run per copy batch).</summary>
    [InitializeOnLoad]
    public sealed class IncomingWatcher : AssetPostprocessor
    {
        private const string Pref = "Kitefall.Art.AutoInstall";
        private const string DonePref = "Kitefall.Art.LastIncomingCount";
        private static bool _queued;
        private static double _suspendUntil;

        /// <summary>Stops AUTO for a moment (the "Wrong" button moves a file into Incoming on purpose).</summary>
        public static void Suspend() => _suspendUntil = EditorApplication.timeSinceStartup + 3.0;

        /// <summary>Images copied in before this tool existed (or while Unity was closed) are picked up when the editor loads.</summary>
        static IncomingWatcher()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Enabled || Application.isBatchMode || !AssetDatabase.IsValidFolder("Assets/Art/Incoming")) return;
                int count = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Incoming" }).Length;
                if (count > 0 && count != EditorPrefs.GetInt(DonePref, -1)) ArtMapperWindow.AutoInstallAll();
            };
        }

        /// <summary>Remembers how many images were left, so unrecognised leftovers do not re-open the window on every load.</summary>
        public static void MarkDone() =>
            EditorPrefs.SetInt(DonePref, AssetDatabase.IsValidFolder("Assets/Art/Incoming") ? AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Incoming" }).Length : 0);

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(Pref, true);
            set => EditorPrefs.SetBool(Pref, value);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!Enabled || _queued || Application.isBatchMode || EditorApplication.timeSinceStartup < _suspendUntil) return;
            foreach (string path in imported)
            {
                if (!path.StartsWith("Assets/Art/Incoming/")) continue;
                if (AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(Texture2D)) continue;
                _queued = true;
                // Wait until the whole copy has been imported and the editor is idle.
                EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
                {
                    _queued = false;
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    ArtMapperWindow.AutoInstallAll();
                };
                return;
            }
        }
    }
}
