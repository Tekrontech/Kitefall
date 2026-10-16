using System.Collections.Generic;
using System.IO;
using Kitefall.App;
using Kitefall.Core;
using Kitefall.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// Kitefall ▸ Build Game Scene: creates every data asset, the GameDatabase and Assets/Scenes/Main.unity
    /// (GameApp + camera + light). Press Play in Main to run the complete game.
    /// </summary>
    public static class GameSceneBuilder
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string DatabasePath = KitefallDefaultData.Root + "/GameDatabase.asset";

        [MenuItem("Kitefall/Build Game Scene (complete game)", priority = -100)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Kitefall", "Stop Play mode first.", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(MainScenePath) && !EditorUtility.DisplayDialog("Rebuild Main scene?", MainScenePath + " exists. Rebuild it? (Data assets are kept.)", "Rebuild", "Cancel")) return;
            Build();
        }

        [MenuItem("Kitefall/Fix Game Scene Links", priority = -98)]
        public static void FixLinksMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Kitefall", "Stop Play mode first.", "OK"); return; }
            if (!File.Exists(MainScenePath)) { Build(); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(MainScenePath);
            GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null) db = EnsureDatabase();
            if (db == null) { Debug.LogError("[Kitefall] Could not create " + DatabasePath + ". Check the Console for earlier errors."); return; }

            GameApp app = Object.FindAnyObjectByType<GameApp>();
            if (app == null) app = new GameObject("GameApp").AddComponent<GameApp>();
            Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            var so = new SerializedObject(app);
            so.FindProperty("database").objectReferenceValue = db;
            if (cam != null) so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = app;
            Debug.Log("[Kitefall] GameApp linked to " + DatabasePath + " and Main scene saved – press Play.");
        }

        [MenuItem("Kitefall/Open Game Scene", priority = -99)]
        public static void OpenMenu()
        {
            if (!File.Exists(MainScenePath)) { Build(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(MainScenePath);
        }

        public static void Build()
        {
            GameDatabase db = EnsureDatabase();
            KitefallDefaultData.EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 20, -20);
                cam.farClipPlane = 2500f;
            }
            var app = new GameObject("GameApp").AddComponent<GameApp>();
            var so = new SerializedObject(app);
            so.FindProperty("database").objectReferenceValue = db;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(app);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            if (db == null) Debug.LogError("[Kitefall] GameDatabase could not be created – check earlier Console errors, then run Kitefall ▸ Fix Game Scene Links.");

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == MainScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            ProjectSetup.ApplyMobilePlayerSettings();
            Selection.activeObject = db;
            Debug.Log("[Kitefall] Main scene built: " + MainScenePath + " – press Play to start the game.");
        }

        /// <summary>Creates (or updates the lists of) the GameDatabase asset.</summary>
        public static GameDatabase EnsureDatabase()
        {
            KitefallDefaultData.Defaults d = KitefallDefaultData.EnsureDefaults(overwrite: false);
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.kites = Load<KiteClassData>("Kites");
            db.tails = Load<TailData>("Tails");
            db.dors = Load<DorData>("Dors");
            db.charkhis = Load<CharkhiData>("Charkhis");
            db.tunings = Load<TuningData>("Tunings");
            db.arenas = EnsureArenas(d.Wind);
            db.combat = d.Combat;
            db.flight = d.Flight;
            db.camera = d.Camera;
            db.balance = d.Balance;
            db.defaultWind = d.Wind;
            bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            db.litTemplate = Template("Lit", (urp ? Shader.Find("Universal Render Pipeline/Lit") : null) ?? Shader.Find("Standard"), m =>
            {
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            });
            db.unlitTemplate = Template("Unlit", Shader.Find("Sprites/Default"), null);
            db.particleTemplate = Template("Particle", (urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null) ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"), m =>
            {
                if (m.HasProperty("_Surface")) { m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
            });
            db.skyboxTemplate = Template("Sky", Shader.Find("Skybox/Procedural"), null);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static Material Template(string name, Shader shader, System.Action<Material> setup)
        {
            if (shader == null) return null;
            KitefallDefaultData.EnsureFolder("Assets/Materials/Runtime");
            string path = $"Assets/Materials/Runtime/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null && m.shader == shader) return m;
            if (m != null) AssetDatabase.DeleteAsset(path);
            m = new Material(shader) { name = name };
            setup?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static List<T> Load<T>(string folder) where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { KitefallDefaultData.Root + "/" + folder }))
            {
                T a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }

        private static List<ArenaData> EnsureArenas(WindConfig oldCityWind)
        {
            var list = new List<ArenaData>();
            list.Add(Arena(ArenaType.OldCityRooftops, "Shifting wind between roofs, power cables.", true, Hex("#8C7B62"), false, MusicStyle.OldCity, oldCityWind));
            list.Add(Arena(ArenaType.CoastalCliffs, "Strong steady sea wind.", true, Hex("#2D6E8E"), true, MusicStyle.Coastal, Wind("Wind_Coastal", 8.5f, 6f)));
            list.Add(Arena(ArenaType.RiverFestival, "Layered wind over the river, height battles.", false, Hex("#6F8F5A"), false, MusicStyle.River, Wind("Wind_River", 6f, 14f)));
            list.Add(Arena(ArenaType.RoyalField, "Open field, big vs small kites.", false, Hex("#7FA35A"), false, MusicStyle.Royal, Wind("Wind_RoyalField", 6.5f, 10f)));
            list.Add(Arena(ArenaType.MountainValley, "Updrafts and sudden gusts.", false, Hex("#7D7466"), false, MusicStyle.Mountain, Wind("Wind_Mountain", 7f, 25f)));
            list.Add(Arena(ArenaType.StormArchipelago, "Big Last Kite Flying map with a storm zone.", false, Hex("#2A6F9E"), true, MusicStyle.Island, Wind("Wind_Archipelago", 7.5f, 18f)));
            return list;
        }

        private static WindConfig Wind(string name, float speed, float variation)
        {
            string path = $"{KitefallDefaultData.Root}/Config/{name}.asset";
            var w = AssetDatabase.LoadAssetAtPath<WindConfig>(path);
            if (w != null) return w;
            w = ScriptableObject.CreateInstance<WindConfig>();
            w.settings.baseSpeed = speed;
            w.settings.directionVariationDeg = variation;
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        private static ArenaData Arena(ArenaType type, string desc, bool launch, Color ground, bool water, MusicStyle music, WindConfig wind)
        {
            string path = $"{KitefallDefaultData.Root}/Arenas/{type}.asset";
            var a = AssetDatabase.LoadAssetAtPath<ArenaData>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<ArenaData>();
            a.type = type;
            a.displayName = ArenaLayout.DisplayName(type);
            a.description = desc;
            a.availableAtLaunch = launch;
            a.groundColor = ground;
            a.water = water;
            a.music = music;
            a.wind = wind;
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        private static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out Color c) ? c : Color.gray;
    }
}
