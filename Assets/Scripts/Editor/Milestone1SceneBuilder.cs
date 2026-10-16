using System.IO;
using Kitefall.CameraRig;
using Kitefall.DebugTools;
using Kitefall.Input;
using Kitefall.Kite;
using Kitefall.UI;
using Kitefall.Wind;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kitefall.EditorTools
{
    /// <summary>
    /// One click builds the complete milestone 1 test scene:
    /// menu  Kitefall ▸ Milestone 1 ▸ Build Flight Test Scene.
    /// Everything (materials, kite mesh, city blockout, HUD, touch controls) is generated,
    /// so there are no hand-made scene files that can break.
    /// </summary>
    public static class Milestone1SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M1_FlightTest.unity";
        private const string MaterialFolder = "Assets/Materials/M1";
        private const string MeshFolder = "Assets/Art/Meshes";
        private const string KiteMeshPath = MeshFolder + "/KiteDiamond.asset";

        [MenuItem("Kitefall/Milestone 1/Build Flight Test Scene", priority = 0)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Kitefall", "Stop Play mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild scene?",
                    ScenePath + " already exists. Rebuild it? (Data assets are kept.)", "Rebuild", "Cancel")) return;

            Build();
        }

        [MenuItem("Kitefall/Milestone 1/Open Flight Test Scene", priority = 1)]
        public static void OpenMenu()
        {
            if (!File.Exists(ScenePath))
            {
                if (EditorUtility.DisplayDialog("Kitefall", "The scene does not exist yet. Build it now?", "Build", "Cancel")) Build();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            KitefallDefaultData.Defaults data = KitefallDefaultData.EnsureDefaults(overwrite: false);
            KitefallDefaultData.EnsureFolder(MaterialFolder);
            KitefallDefaultData.EnsureFolder(MeshFolder);
            KitefallDefaultData.EnsureFolder("Assets/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---- Lighting & sky (golden-hour feel – spec 15)
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.name = "Sun";
                sun.color = new Color(1f, 0.9f, 0.76f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.92f);
            RenderSettings.fogStartDistance = 180f;
            RenderSettings.fogEndDistance = 1100f;

            // ---- Environment blockout (placeholder art, original)
            BuildEnvironment(out Transform flyerRoof);

            // ---- Flyer + anchor (where the line leaves the hands)
            GameObject flyer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            flyer.name = "Flyer (placeholder)";
            RemoveCollider(flyer);
            flyer.transform.position = flyerRoof.position + Vector3.up * 1f;
            flyer.GetComponent<MeshRenderer>().sharedMaterial = Mat("Flyer", new Color(0.2f, 0.32f, 0.55f));
            var anchor = new GameObject("LineAnchor").transform;
            anchor.SetParent(flyer.transform, false);
            anchor.localPosition = new Vector3(0.35f, 0.35f, 0.35f);

            // ---- Systems
            var windGo = new GameObject("WindField");
            windGo.transform.position = flyerRoof.position;
            var wind = windGo.AddComponent<WindField>();
            Set(wind, "config", data.Wind);

            var inputGo = new GameObject("PlayerInput");
            var input = inputGo.AddComponent<KiteInputReader>();

            new GameObject("Bootstrap").AddComponent<PerformanceBootstrap>();

            // ---- Kite
            GameObject kiteGo = BuildKite();
            var kite = kiteGo.AddComponent<KiteController>();
            Set(kite, "kiteClass", data.Guddi);
            Set(kite, "tail", data.ShortRibbon);
            Set(kite, "dor", data.Sooti);
            Set(kite, "charkhi", data.Lakri);
            Set(kite, "combatConfig", data.Combat);
            Set(kite, "flightConfig", data.Flight);
            Set(kite, "windField", wind);
            Set(kite, "inputReader", input);
            Set(kite, "anchor", anchor);
            Set(kite, "kiteVisual", kiteGo.transform);
            // Start the kite roughly where it will launch so the scene view makes sense.
            kiteGo.transform.position = anchor.position + new Vector3(0f, 34f, 49f);

            // ---- Temporary line (milestone 3 replaces it)
            var tetherGo = new GameObject("TetherLine (debug)");
            var line = tetherGo.AddComponent<LineRenderer>();
            line.sharedMaterial = UnlitMat("Tether");
            line.widthMultiplier = 0.05f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            var tether = tetherGo.AddComponent<DebugTetherLine>();
            Set(tether, "kite", kite);

            // ---- Camera
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) cam = new GameObject("Main Camera").AddComponent<UnityEngine.Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1500f;
            var follow = cam.gameObject.AddComponent<KiteCameraController>();
            Set(follow, "kite", kite);
            Set(follow, "config", data.Camera);
            cam.transform.position = anchor.position + new Vector3(0f, 25f, 35f);
            cam.transform.LookAt(kiteGo.transform);

            // ---- HUD + touch controls
            BuildHud(kite, cam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = kiteGo;
            Debug.Log("[Kitefall] Milestone 1 scene built: " + ScenePath + " – press Play.");
        }

        // =====================================================================
        // Environment
        // =====================================================================

        private static void BuildEnvironment(out Transform flyerRoof)
        {
            var root = new GameObject("Environment (blockout)").transform;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            RemoveCollider(ground);
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(240f, 1f, 240f); // 2.4 km
            ground.GetComponent<MeshRenderer>().sharedMaterial = Mat("Ground", new Color(0.55f, 0.52f, 0.42f));

            Material[] house =
            {
                Mat("House_Plaster", new Color(0.86f, 0.80f, 0.70f)),
                Mat("House_Brick", new Color(0.66f, 0.40f, 0.30f)),
                Mat("House_Cream", new Color(0.93f, 0.88f, 0.76f)),
                Mat("House_Blue", new Color(0.55f, 0.66f, 0.74f))
            };
            Material far = Mat("City_Silhouette", new Color(0.45f, 0.50f, 0.60f));
            var rng = new System.Random(7);

            // Rooftop neighbourhood around the flyer. The area straight downwind (+Z) stays open for flying.
            flyerRoof = null;
            for (int ix = -3; ix <= 3; ix++)
            {
                for (int iz = -3; iz <= 1; iz++)
                {
                    bool isFlyerHouse = ix == 0 && iz == 0;
                    float h = isFlyerHouse ? 10f : 7f + (float)rng.NextDouble() * 7f;
                    var pos = new Vector3(ix * 12f, h * 0.5f, iz * 12f);
                    Box($"House_{ix}_{iz}", root, pos, new Vector3(10f, h, 10f), house[rng.Next(house.Length)]);
                    if (isFlyerHouse)
                    {
                        var top = new GameObject("FlyerRoofTop").transform;
                        top.SetParent(root, false);
                        top.position = new Vector3(0f, h, 0f);
                        flyerRoof = top;
                    }
                }
            }

            // Distant city silhouettes (spec 15: "distant city as silhouettes").
            for (int i = 0; i < 48; i++)
            {
                float angle = i / 48f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.1f;
                float dist = 380f + (float)rng.NextDouble() * 220f;
                float h = 18f + (float)rng.NextDouble() * 45f;
                float w = 20f + (float)rng.NextDouble() * 35f;
                var pos = new Vector3(Mathf.Sin(angle) * dist, h * 0.5f, Mathf.Cos(angle) * dist);
                var b = Box("Far_" + i, root, pos, new Vector3(w, h, w), far);
                b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool local = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            RemoveCollider(go);
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = position; else go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // =====================================================================
        // Kite
        // =====================================================================

        private static GameObject BuildKite()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(KiteMeshPath);
            if (mesh == null)
            {
                mesh = KiteMeshFactory.CreateDiamond();
                AssetDatabase.CreateAsset(mesh, KiteMeshPath);
            }

            var kite = new GameObject("Kite");
            kite.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = kite.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat("Kite_Sail", new Color(0.95f, 0.35f, 0.25f), 0.35f);

            // Bamboo frame: spine and bow (KiteMeshFactory default size 1.6 × 1.8, spine at 62%).
            Material stick = Mat("Kite_Stick", new Color(0.75f, 0.62f, 0.38f));
            Box("Spine", kite.transform, new Vector3(0f, -0.216f, -0.02f), new Vector3(0.025f, 1.8f, 0.025f), stick, local: true);
            Box("Bow", kite.transform, new Vector3(0f, 0f, 0.05f), new Vector3(1.6f, 0.025f, 0.025f), stick, local: true);
            return kite;
        }

        // =====================================================================
        // HUD
        // =====================================================================

        private static void BuildHud(KiteController kite, UnityEngine.Camera cam)
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform root = canvasGo.GetComponent<RectTransform>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // ---- Tension bar (right edge, vertical)
            RectTransform bar = Rect("TensionBar", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, 80f), new Vector2(56f, 480f));
            Image warning = Img("SnapWarning", bar, square, new Color(1f, 0.15f, 0.1f, 0f));
            Stretch(warning.rectTransform, -10f);
            Image bg = Img("Background", bar, square, new Color(0f, 0f, 0f, 0.45f));
            Stretch(bg.rectTransform, 0f);
            Image safe = Img("SafeZone", bar, square, Color.green);
            Image strong = Img("StrongZone", bar, square, Color.yellow);
            Image danger = Img("DangerZone", bar, square, Color.red);
            RectTransform strength = Img("StrengthLine", bar, square, Color.white).rectTransform;
            strength.anchorMin = new Vector2(-0.2f, 0.95f);
            strength.anchorMax = new Vector2(1.2f, 0.95f);
            strength.sizeDelta = new Vector2(0f, 4f);
            Image marker = Img("Marker", bar, square, Color.white);
            marker.rectTransform.anchorMin = new Vector2(-0.35f, 0.4f);
            marker.rectTransform.anchorMax = new Vector2(1.35f, 0.4f);
            marker.rectTransform.sizeDelta = new Vector2(0f, 12f);
            Text value = Txt("Value", bar, "40", 30, TextAnchor.LowerCenter);
            Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(140f, 40f));
            Text title = Txt("Title", bar, "TENSION", 20, TextAnchor.UpperCenter);
            Place(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(160f, 30f));

            var barView = bar.gameObject.AddComponent<TensionBarView>();
            Set(barView, "kite", kite);
            Set(barView, "safeZone", safe.rectTransform);
            Set(barView, "strongZone", strong.rectTransform);
            Set(barView, "dangerZone", danger.rectTransform);
            Set(barView, "strengthLine", strength);
            Set(barView, "marker", marker.rectTransform);
            Set(barView, "markerImage", marker);
            Set(barView, "snapWarning", warning);
            Set(barView, "valueLabel", value);

            // ---- Wind compass (top centre)
            RectTransform compass = Rect("WindCompass", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(110f, 110f));
            compass.gameObject.AddComponent<Image>().sprite = circle;
            compass.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
            RectTransform arrow = Img("Arrow", compass, square, Color.white).rectTransform;
            Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 70f));
            RectTransform head = Img("Head", arrow, square, Color.white).rectTransform;
            Place(head, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
            head.localEulerAngles = new Vector3(0f, 0f, 45f);
            Text windLabel = Txt("Label", compass, "6.0 m/s", 24, TextAnchor.UpperCenter);
            Place(windLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(300f, 34f));
            var compassView = compass.gameObject.AddComponent<WindCompassView>();
            Set(compassView, "kite", kite);
            Set(compassView, "viewCamera", cam);
            Set(compassView, "arrow", arrow);
            Set(compassView, "label", windLabel);

            // ---- Debug read-out (top left)
            RectTransform panel = Rect("DebugPanel", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(760f, 390f));
            panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            panel.GetComponent<Image>().raycastTarget = false;
            Text debugText = Txt("Text", panel, "", 22, TextAnchor.UpperLeft);
            Stretch(debugText.rectTransform, 14f);
            Text banner = Txt("StatusBanner", root, "", 46, TextAnchor.MiddleCenter);
            Place(banner.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 160f));
            banner.color = new Color(1f, 0.45f, 0.35f);
            var hud = panel.gameObject.AddComponent<FlightDebugHud>();
            Set(hud, "kite", kite);
            Set(hud, "text", debugText);
            Set(hud, "statusBanner", banner);

            // ---- Touch controls (spec 10). They drive a virtual gamepad read by KiteInputReader.
            RectTransform stickBase = Rect("SteerJoystick", root, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(250f, 230f), new Vector2(300f, 300f));
            stickBase.gameObject.AddComponent<Image>().sprite = circle;
            stickBase.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.18f);
            stickBase.GetComponent<Image>().raycastTarget = false;
            Image knob = Img("Knob", stickBase, circle, new Color(1f, 1f, 1f, 0.55f));
            knob.raycastTarget = true; // OnScreenStick needs pointer events on the knob.
            Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            SetString(stick, "m_ControlPath", KiteInputReader.SteerStickPath);
            SetFloat(stick, "m_MovementRange", 110f);

            TouchButton(root, "PullButton", "PULL", circle, new Vector2(-230f, 210f), 230f, new Color(0.95f, 0.55f, 0.15f, 0.8f), KiteInputReader.PullButtonPath, 44);
            TouchButton(root, "ReelButton", "REEL", circle, new Vector2(-230f, 420f), 140f, new Color(0.25f, 0.55f, 0.95f, 0.8f), KiteInputReader.ReelButtonPath, 30);

            RectTransform reset = Rect("ResetButton", root, Vector2.one, Vector2.one, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(170f, 64f));
            reset.gameObject.AddComponent<Image>().sprite = square;
            reset.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            Text resetText = Txt("Label", reset, "RESET", 26, TextAnchor.MiddleCenter);
            Stretch(resetText.rectTransform, 0f);
            SetString(reset.gameObject.AddComponent<OnScreenButton>(), "m_ControlPath", KiteInputReader.ResetButtonPath);
        }

        private static void TouchButton(RectTransform root, string name, string label, Sprite sprite, Vector2 pos, float size, Color color, string controlPath, int fontSize)
        {
            RectTransform rect = Rect(name, root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            Text text = Txt("Label", rect, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 0f);
            SetString(rect.gameObject.AddComponent<OnScreenButton>(), "m_ControlPath", controlPath);
        }

        // =====================================================================
        // UI helpers
        // =====================================================================

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Positive inset = smaller than the parent, negative = bigger.
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Txt(string name, Transform parent, string content, int size, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = LoadFont();
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Font LoadFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // =====================================================================
        // Materials (work with URP and with the Built-in pipeline)
        // =====================================================================

        private static Material Mat(string name, Color color, float smoothness = 0.15f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = (urp ? Shader.Find("Universal Render Pipeline/Lit") : null) ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name, color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material UnlitMat(string name)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            // Sprites/Default uses vertex colours, so LineRenderer colours work in every pipeline.
            mat = new Material(Shader.Find("Sprites/Default")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // =====================================================================
        // Serialized field helpers (fields are private [SerializeField])
        // =====================================================================

        private static SerializedProperty Find(Object target, string field, out SerializedObject so)
        {
            so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogError($"[Kitefall] Field '{field}' not found on {target.GetType().Name}. Wire it by hand in the Inspector.", target);
            return p;
        }

        private static void Set(Object target, string field, Object value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    /// <summary>Warns once if the project is not set to the new Input System.</summary>
    [InitializeOnLoad]
    internal static class InputSystemCheck
    {
        static InputSystemCheck()
        {
#if !ENABLE_INPUT_SYSTEM
            Debug.LogWarning("[Kitefall] Active Input Handling must be 'Input System Package (New)' or 'Both'. " +
                             "Edit ▸ Project Settings ▸ Player ▸ Other Settings ▸ Active Input Handling.");
#endif
        }
    }
}
