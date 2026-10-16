using System.Linq;
using RotateRings.Gameplay.Data;
using RotateRings.Gameplay.Scope;
using RotateRings.Gameplay.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// One-click setup of everything the Gameplay scene needs: 3D URP renderer, materials, pooled view prefabs,
    /// sample level JSON files, gameplay UI and the scene itself. Levels are edited in Edit Mode with
    /// Tools ▸ Rotate Rings ▸ Level Editor.
    /// </summary>
    public static class GameplaySceneBuilder
    {
        const string Root = "Assets/_Game";
        const string SettingsFolder = Root + "/Settings";
        const string MaterialsFolder = Root + "/Materials";
        const string PrefabsFolder = Root + "/Prefabs";
        const string ScenesFolder = Root + "/Scenes";
        const string GameplayScenePath = ScenesFolder + "/Gameplay.unity";
        const string RendererPath = SettingsFolder + "/GameplayUniversalRenderer.asset";
        const string TmpEssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        static readonly Color Background = new(0.98f, 0.94f, 0.85f);
        static readonly Color Brown = new(0.47f, 0.27f, 0.17f);
        static readonly Color Cream = new(1f, 0.97f, 0.9f);
        static readonly Color Pill = new(0.98f, 0.88f, 0.74f);
        static readonly Color Green = new(0.29f, 0.71f, 0.15f);
        static readonly Color Praise = new(0.33f, 0.78f, 0.13f);

        /// <summary>Assets the scene references, loaded after NewScene (which unloads unreferenced assets).</summary>
        class SharedAssets
        {
            public int RendererIndex;
            public LevelCatalog Catalog;
            public PieceView PiecePrefab;
            public LinkView LinkPrefab;
            public ShatterFxView ShatterPrefab;
            public FloatingTextView TextPrefab;

            public static SharedAssets Load(int rendererIndex) => new()
            {
                RendererIndex = rendererIndex,
                Catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(SampleLevelGenerator.CatalogPath),
                PiecePrefab = LoadPrefab<PieceView>("PieceView"),
                LinkPrefab = LoadPrefab<LinkView>("LinkView"),
                ShatterPrefab = LoadPrefab<ShatterFxView>("ShatterFx"),
                TextPrefab = LoadPrefab<FloatingTextView>("FloatingText"),
            };

            static T LoadPrefab<T>(string name) where T : Component =>
                AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/{name}.prefab").GetComponent<T>();
        }

        [MenuItem("Tools/Rotate Rings/Build Gameplay Scene")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[Rotate Rings] Importing TMP Essential Resources first, the build continues afterwards.");
                void Continue(string _)
                {
                    AssetDatabase.importPackageCompleted -= Continue;
                    EditorApplication.delayCall += Build;
                }

                AssetDatabase.importPackageCompleted += Continue;
                AssetDatabase.ImportPackage(TmpEssentialsPackage, false);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (var folder in new[] { SettingsFolder, MaterialsFolder, PrefabsFolder, ScenesFolder })
                EditorFolders.Ensure(folder);

            // A Single-mode NewScene unloads unreferenced assets, so the scene is opened before creating them.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rendererIndex = PrepareSharedAssets();
            BuildGameplayScene(scene, SharedAssets.Load(rendererIndex));

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != GameplayScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(GameplayScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            Debug.Log("[Rotate Rings] Gameplay scene built. Press Play, or edit levels with Tools ▸ Rotate Rings ▸ Level Editor.");
        }

        static int PrepareSharedAssets()
        {
            SampleLevelGenerator.Generate();
            var rendererIndex = SetupUniversalRenderer();

            var tubeMaterial = CreateMaterial("RingLit", "Universal Render Pipeline/Lit");
            var shardMaterial = CreateMaterial("ShardParticle", "Universal Render Pipeline/Particles/Simple Lit");

            DeleteIfExists($"{PrefabsFolder}/RingView.prefab");
            DeleteIfExists($"{PrefabsFolder}/ConnectorView.prefab");
            CreatePiecePrefab(tubeMaterial);
            CreateLinkPrefab(tubeMaterial);
            CreateShatterPrefab(shardMaterial);
            CreateFloatingTextPrefab();

            AssetDatabase.SaveAssets();
            return rendererIndex;
        }

        static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        // ---------- Rendering ----------

        /// <summary>The project shipped with the 2D renderer; 3D lit tubes need a Universal renderer.</summary>
        static int SetupUniversalRenderer()
        {
            // This project assigns the pipeline through Quality settings, so the Graphics default can be empty.
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                Debug.LogError("[Rotate Rings] The current render pipeline is not URP.");
                return 0;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(rendererData);
            }

            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            var index = -1;
            for (var i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == rendererData)
                    index = i;
            }

            if (index < 0)
            {
                index = list.arraySize;
                list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = rendererData;
            }

            serialized.FindProperty("m_DefaultRendererIndex").intValue = index;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return index;
        }

        static Material CreateMaterial(string name, string shaderName)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find(shaderName);
            material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.55f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Prefabs ----------

        static void CreatePiecePrefab(Material material)
        {
            var root = new GameObject("PieceView");
            var view = root.AddComponent<PieceView>();
            var mover = Child(root.transform, "Mover");
            var body = Child(mover, "Body");
            var filter = body.gameObject.AddComponent<MeshFilter>();
            var renderer = body.gameObject.AddComponent<MeshRenderer>();
            ConfigureRenderer(renderer, material);

            SetFields(view, ("mover", mover), ("bodyFilter", filter), ("bodyRenderer", renderer));
            SavePrefab(root);
        }

        static void CreateLinkPrefab(Material material)
        {
            var root = new GameObject("LinkView");
            var view = root.AddComponent<LinkView>();
            var stem = Primitive(PrimitiveType.Cylinder, "Stem", root.transform, material);
            var head = Primitive(PrimitiveType.Cube, "Head", root.transform, material);

            SetFields(view,
                ("stem", stem.transform), ("stemRenderer", stem.GetComponent<Renderer>()),
                ("head", head.transform), ("headRenderer", head.GetComponent<Renderer>()));
            SavePrefab(root);
        }

        static void CreateShatterPrefab(Material material)
        {
            var root = new GameObject("ShatterFx");
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 2f;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startRotation3D = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.9f;
            main.maxParticles = 1000;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f)));

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var view = root.AddComponent<ShatterFxView>();
            SetFields(view, ("particles", particles));
            SavePrefab(root);
        }

        static void CreateFloatingTextPrefab()
        {
            var root = new GameObject("FloatingText");
            var text = root.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = "Great!";
            text.fontSize = 6f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Praise;
            text.rectTransform.sizeDelta = new Vector2(6f, 1.5f);
            text.GetComponent<MeshRenderer>().sortingOrder = 10;

            var view = root.AddComponent<FloatingTextView>();
            SetFields(view, ("label", text));
            SavePrefab(root);
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            ConfigureRenderer(go.GetComponent<MeshRenderer>(), material);
            return go;
        }

        static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void SavePrefab(GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }

        // ---------- Scene ----------

        static void BuildGameplayScene(UnityEngine.SceneManagement.Scene scene, SharedAssets assets)
        {
            var camera = CreateCameraAndLight(assets.RendererIndex, 5.2f);
            CreateEventSystem();

            var canvas = CreateCanvas();
            var hud = CreateHud(canvas);
            var winPanel = CreateWinPanel(canvas);

            var scope = new GameObject("GameplayLifetimeScope").AddComponent<GameplayLifetimeScope>();
            SetFields(scope, ("levelCatalog", assets.Catalog));
            SetViewAndUiRefs(scope, assets, camera, hud, winPanel);

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        static void SetViewAndUiRefs(Object scope, SharedAssets assets, Camera camera, GameplayHudView hud, WinPanelView winPanel)
        {
            SetFields(scope,
                ("view.camera", camera),
                ("view.piecePrefab", assets.PiecePrefab),
                ("view.linkPrefab", assets.LinkPrefab),
                ("view.shatterFxPrefab", assets.ShatterPrefab),
                ("view.floatingTextPrefab", assets.TextPrefab),
                ("ui.hud", hud),
                ("ui.winPanel", winPanel));
        }

        static Camera CreateCameraAndLight(int rendererIndex, float size)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            cameraGo.transform.position = new Vector3(0f, 0.35f, -10f);
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.SetRenderer(rendererIndex);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.58f, 0.56f);
            return camera;
        }

        static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        static GameplayHudView CreateHud(RectTransform canvas)
        {
            var hud = UiRect("HUD", canvas);
            Stretch(hud);

            var level = Label("LevelLabel", hud, "Level\n1", 52f, Brown, TextAlignmentOptions.Left);
            Anchor(level.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(320f, 160f), new Vector2(0f, 1f));

            var pill = UiRect("ScorePill", hud);
            Anchor(pill, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(520f, 130f), new Vector2(0.5f, 1f));
            RoundedImage(pill, Pill);

            var score = Label("ScoreLabel", pill, "0", 80f, Brown, TextAlignmentOptions.Center);
            Stretch(score.rectTransform);

            var view = hud.gameObject.AddComponent<GameplayHudView>();
            SetFields(view, ("levelLabel", level), ("scoreLabel", score));
            return view;
        }

        static WinPanelView CreateWinPanel(RectTransform canvas)
        {
            var container = UiRect("WinPanel", canvas);
            Stretch(container);

            var root = UiRect("Root", container);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var card = UiRect("Card", root);
            Anchor(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 980f), new Vector2(0.5f, 0.5f));
            RoundedImage(card, Cream);

            var title = Label("Title", card, "You Did It!", 110f, new Color(0.93f, 0.4f, 0.13f), TextAlignmentOptions.Center);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 160f), new Vector2(0.5f, 1f));

            var caption = Label("ScoreCaption", card, "SCORE", 56f, Brown, TextAlignmentOptions.Center);
            Anchor(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -270f), new Vector2(600f, 80f), new Vector2(0.5f, 1f));

            var score = Label("ScoreValue", card, "0", 130f, Brown, TextAlignmentOptions.Center);
            Anchor(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(700f, 170f), new Vector2(0.5f, 1f));

            var next = TextButton("NextButton", card, "Next Level", Green);
            Anchor(next, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(620f, 150f), new Vector2(0.5f, 0f));

            var replay = TextButton("ReplayButton", card, "Replay", new Color(0.95f, 0.6f, 0.15f));
            Anchor(replay, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(620f, 130f), new Vector2(0.5f, 0f));

            var view = container.gameObject.AddComponent<WinPanelView>();
            SetFields(view,
                ("root", root.gameObject), ("canvasGroup", group), ("card", card), ("scoreLabel", score),
                ("replayButton", replay.GetComponent<Button>()), ("nextButton", next.GetComponent<Button>()));
            return view;
        }

        static RectTransform TextButton(string name, RectTransform parent, string text, Color color)
        {
            var rect = UiRect(name, parent);
            var image = RoundedImage(rect, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = Label("Label", rect, text, 64f, Color.white, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            return rect;
        }

        static TextMeshProUGUI Label(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var rect = UiRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        static Image RoundedImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.25f;
            image.color = color;
            return image;
        }

        static RectTransform UiRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void SetFields(Object target, params (string path, Object value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (path, value) in fields)
            {
                var property = serialized.FindProperty(path);
                if (property == null)
                {
                    Debug.LogError($"[Rotate Rings] {target.GetType().Name} has no serialized field '{path}'.");
                    continue;
                }

                if (value == null)
                    Debug.LogError($"[Rotate Rings] {target.GetType().Name}.{path} is being set to null.");

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
