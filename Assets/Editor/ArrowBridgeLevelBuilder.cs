using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowBridge.EditorTools
{
    /// <summary>
    /// Builds the entire Arrow Bridge prototype scene from code: world layout, the fixed 30-arrow
    /// puzzle, the bridge builder, the character and the palette-consistent UI. Re-run via
    /// Tools > Arrow Bridge > Build Level any time; it fully rebuilds the scene from scratch and
    /// overwrites Assets/Scenes/ArrowBridgeLevel.unity.
    /// </summary>
    public static class ArrowBridgeLevelBuilder
    {
        private const string ScenePath = "Assets/Scenes/ArrowBridgeLevel.unity";
        private const string ArrowPrefabPath = "Assets/Prefabs/Arrow.prefab";

        private const float LandHalfWidth = 1f; // KaraA/B are 2 units wide
        private const float KaraAX = -7f;
        private const float KaraBX = 7f;
        private const float GroundSurfaceY = 0f; // top of land/water, and where the bridge sits
        private const float BridgeStartX = -6f; // KaraA's right edge
        private const float BridgeEndX = 6f;    // KaraB's left edge
        private const int TotalLives = 3;

        [MenuItem("Tools/Arrow Bridge/Build Level")]
        public static void BuildLevel()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildLighting();
            var grid = BuildGridManager();
            BuildLand(KaraAX);
            BuildLand(KaraBX);
            BuildWater();
            BuildBridgeBuilder();
            BuildPlayer();
            BuildGameManager();
            BuildUI();

            var arrowPrefab = GetOrCreateArrowPrefab();
            BuildArrows(arrowPrefab);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            VerifyTmpFontsAssigned();
            Debug.Log("Arrow Bridge: Level build complete (palette-consistent visuals + bridge-building mechanic). Scene saved to " + ScenePath);
        }

        private static void VerifyTmpFontsAssigned()
        {
            // Include inactive objects — the win panel (and everything under it) starts deactivated.
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int missing = 0;
            foreach (var text in texts)
            {
                if (text.font == null)
                {
                    missing++;
                    Debug.LogError($"Arrow Bridge: TMP_Text '{text.name}' has no font assigned.");
                }
            }
            Debug.Log(missing == 0
                ? $"Arrow Bridge: TMP font check PASS ({texts.Length} TMP_Text objects, all with a font assigned)."
                : $"Arrow Bridge: TMP font check FAIL ({missing}/{texts.Length} missing a font).");
        }

        private const float CameraDistance = 20f;
        private const float CameraTiltDegrees = 10f;
        private const float CameraFocusX = -0.5f; // board columns -5..4 center on -0.5
        private const float CameraFocusY = 6.5f;  // frames the board (y 3..14) plus the bridge strip (y ~0)

        private static void BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GamePalette.Background;

            // Slight downward tilt gives the 3D water/islands/bridge depth while the z=0 gameplay
            // plane stays fully readable. Camera height compensates the tilt so the framed center
            // on the plane stays at CameraFocusY.
            float tiltCompensation = CameraDistance * Mathf.Tan(CameraTiltDegrees * Mathf.Deg2Rad);
            cameraObject.transform.position = new Vector3(CameraFocusX, CameraFocusY + tiltCompensation, -CameraDistance);
            cameraObject.transform.rotation = Quaternion.Euler(CameraTiltDegrees, 0f, 0f);

            var controller = cameraObject.AddComponent<CameraController>();
            controller.Configure(new Vector2(-5f, 0f), new Vector2(4f, 13f), 8f, 26f);
        }

        private static void BuildLighting()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = Color.white;
            lightObject.transform.rotation = Quaternion.Euler(45f, -32f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.72f);
        }

        private static ArrowGridManager BuildGridManager()
        {
            var gridObject = new GameObject("GridManager");
            var grid = gridObject.AddComponent<ArrowGridManager>();
            grid.Configure(ManualLevelLayout.MinX, ManualLevelLayout.MaxX, ManualLevelLayout.MinY, ManualLevelLayout.MaxY, 1f);
            return grid;
        }

        private static void BuildLand(float centerX)
        {
            // Layered 3D islet: grass slab whose top sits flush with the bridge deck line (y=0),
            // over a chunkier rock body descending toward the water.
            var island = new GameObject(centerX < 0 ? "KaraA" : "KaraB");
            island.transform.position = new Vector3(centerX, 0f, 0f);

            Decor3DFactory.CreateBox("Grass",
                new Vector3(centerX, GroundSurfaceY - 0.35f, 0.7f), new Vector3(3.2f, 0.7f, 3.4f),
                GamePalette.IslandGrass, island.transform);
            Decor3DFactory.CreateBox("Rock",
                new Vector3(centerX, GroundSurfaceY - 1.6f, 0.7f), new Vector3(2.6f, 1.9f, 2.8f),
                GamePalette.IslandRock, island.transform);
        }

        private static void BuildWater()
        {
            var water = new GameObject("Water");
            // Broad river slab, top surface a touch below the deck line.
            Decor3DFactory.CreateBox("Surface",
                new Vector3(0f, GroundSurfaceY - 1.05f, 1.2f), new Vector3(15f, 1.6f, 9f),
                GamePalette.Water, water.transform);
        }

        private static BridgeBuilder BuildBridgeBuilder()
        {
            var bridgeObject = new GameObject("BridgeBuilder");
            var bridgeBuilder = bridgeObject.AddComponent<BridgeBuilder>();
            bridgeBuilder.Configure(BridgeStartX, BridgeEndX, GroundSurfaceY, ManualLevelLayout.Placements.Length);
            return bridgeBuilder;
        }

        private const string CharacterModelPath = "Assets/Characters/Knight.glb";
        private const string CharacterAnimatorPath = "Assets/Characters/PlayerAnimator.controller";
        private const float CharacterWalkDepthZ = 0.6f; // walks near the deck's center line

        private static void BuildPlayer()
        {
            var player = new GameObject("Player") { tag = "Player" };
            var character = player.AddComponent<PlayerCharacter>();

            Animator animator = null;
            Transform modelRoot = null;

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (modelAsset != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "KnightModel";
                model.transform.SetParent(player.transform, false);
                model.transform.localScale = Vector3.one * 0.75f;
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // glTF forward is +Z; face along the bridge (+X)
                modelRoot = model.transform;

                animator = model.GetComponentInChildren<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = GetOrCreateCharacterAnimator();
            }
            else
            {
                Debug.LogWarning("Arrow Bridge: character model not found at " + CharacterModelPath + " — using a placeholder capsule.");
                var placeholder = new GameObject("PlaceholderBody");
                placeholder.transform.SetParent(player.transform, false);
                var renderer = placeholder.AddComponent<SpriteRenderer>();
                renderer.color = GamePalette.Character;
                renderer.sortingOrder = 10;
                placeholder.AddComponent<ProceduralShape>().Init(ProceduralShape.ShapeKind.Capsule, 0.6f, 0.9f);
                modelRoot = placeholder.transform;
            }

            character.Configure(new Vector3(BridgeStartX, GroundSurfaceY, CharacterWalkDepthZ), animator, modelRoot);
        }

        /// <summary>Idle&lt;-&gt;Run animator driven by an IsWalking bool, built from the clips embedded in the glb. Loop flags are baked into writable clip copies (imported sub-asset clips are read-only and glTF has no loop concept).</summary>
        private static RuntimeAnimatorController GetOrCreateCharacterAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(CharacterAnimatorPath);
            if (existing != null) return existing;

            AnimationClip idleClip = null, runClip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath))
            {
                if (asset is AnimationClip clip)
                {
                    if (clip.name == "Idle") idleClip = clip;
                    else if (clip.name == "Running_A") runClip = clip;
                }
            }

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(CharacterAnimatorPath);
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            var stateMachine = controller.layers[0].stateMachine;

            var idleState = stateMachine.AddState("Idle");
            idleState.motion = MakeLoopingCopy(idleClip, "IdleLoop", controller);
            var walkState = stateMachine.AddState("Walk");
            walkState.motion = MakeLoopingCopy(runClip, "RunLoop", controller);
            stateMachine.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "IsWalking");
            toWalk.hasExitTime = false;
            toWalk.duration = 0.15f;

            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "IsWalking");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip MakeLoopingCopy(AnimationClip source, string name, Object owner)
        {
            if (source == null) return null;
            var copy = Object.Instantiate(source);
            copy.name = name;
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.AddObjectToAsset(copy, owner);
            return copy;
        }

        private static void BuildGameManager()
        {
            var gameManagerObject = new GameObject("GameManager");
            var gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManager.Configure(new Vector3(KaraBX, GroundSurfaceY, CharacterWalkDepthZ), ManualLevelLayout.Placements.Length, TotalLives);
        }

        private static GameObject GetOrCreateArrowPrefab()
        {
            EnsureFolder("Assets/Prefabs");

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            if (existing != null) return existing;

            var template = new GameObject("Arrow");
            template.AddComponent<ArrowController>(); // colliders/visuals are built dynamically in Configure()
            PrefabUtility.SaveAsPrefabAsset(template, ArrowPrefabPath);
            Object.DestroyImmediate(template);

            return AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        }

        private static void BuildArrows(GameObject arrowPrefab)
        {
            var placements = ManualLevelLayout.Placements;
            var arrowsRoot = new GameObject("Arrows");

            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(arrowPrefab);
                instance.transform.SetParent(arrowsRoot.transform);
                instance.name = $"Arrow_{i:00}";

                var controller = instance.GetComponent<ArrowController>();
                controller.Configure(placement.PathCells, placement.ExitDirection);
            }
        }

        // ----- UI -----

        private static void BuildUI()
        {
            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // The game is now committed to 9:16 portrait (mobile), so the reference resolution is
            // portrait too; 0.5 matching keeps the HUD sane if the view briefly isn't 9:16.
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }

            // No arrow counter / progress bar by design: the bridge visibly growing across the
            // water is the progress indicator. HUD is just the hearts + result panels.
            var lifePips = CreateLifePips(canvasObject.transform);
            var (winPanel, winText, restartButton) = CreateResultPanel(canvasObject.transform, "WinPanel", "Tamamlandı!", "Tekrar Oyna");
            var (failPanel, failText, failRestartButton) = CreateResultPanel(canvasObject.transform, "FailPanel", "Can bitti!", "Tekrar Dene");

            var uiManagerObject = new GameObject("UIManager");
            var uiManager = uiManagerObject.AddComponent<UIManager>();
            uiManager.Configure(lifePips,
                winPanel, winText, restartButton,
                failPanel, failText, failRestartButton);
        }

        /// <summary>A row of hearts pinned to the top-left corner, one per life. UIManager recolors them as lives are lost.</summary>
        private static List<Image> CreateLifePips(Transform parent)
        {
            const float heartSize = 72f;
            const float heartSpacing = 88f;
            const int heartTexturePixels = 96;

            var heartsRoot = new GameObject("LifeHearts");
            heartsRoot.transform.SetParent(parent, false);
            var rootRect = heartsRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(40f, -40f);
            rootRect.sizeDelta = new Vector2(TotalLives * heartSpacing, heartSize);

            var pips = new List<Image>(TotalLives);
            for (int i = 0; i < TotalLives; i++)
            {
                var heartObject = new GameObject($"LifeHeart_{i + 1}");
                heartObject.transform.SetParent(heartsRoot.transform, false);
                var heartRect = heartObject.AddComponent<RectTransform>();
                heartRect.anchorMin = new Vector2(0f, 0.5f);
                heartRect.anchorMax = new Vector2(0f, 0.5f);
                heartRect.pivot = new Vector2(0f, 0.5f);
                heartRect.sizeDelta = new Vector2(heartSize, heartSize);
                heartRect.anchoredPosition = new Vector2(i * heartSpacing, 0f);

                var heartImage = heartObject.AddComponent<Image>();
                heartImage.color = GamePalette.LifeFull;
                heartObject.AddComponent<ProceduralShape>().Init(ProceduralShape.ShapeKind.Heart, 1f, 1f, heartTexturePixels);
                pips.Add(heartImage);
            }
            return pips;
        }

        private static (GameObject panel, TMP_Text messageText, Button restartButton) CreateResultPanel(Transform parent, string panelName, string message, string buttonLabel)
        {
            var panelObject = new GameObject(panelName);
            panelObject.transform.SetParent(parent, false);
            var panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 500f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, 0.95f);
            panelObject.AddComponent<ProceduralShape>().Init(ProceduralShape.ShapeKind.RoundedRect, 7f, 5f);

            var messageText = CreateTMPText(panelObject.transform, "Message", message,
                new Vector2(0.5f, 0.68f), Vector2.zero, 72, TextAlignmentOptions.Center);

            var restartButton = CreateButton(panelObject.transform, "RestartButton", buttonLabel, new Vector2(0.5f, 0.3f));

            panelObject.SetActive(false);
            return (panelObject, messageText, restartButton);
        }

        private static TMP_Text CreateTMPText(Transform parent, string name, string content, Vector2 anchor, Vector2 anchoredPosition, int fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(900f, 140f);

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = GetDefaultTMPFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = GamePalette.UIText;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 110f);
            rect.anchoredPosition = Vector2.zero;

            var buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = GamePalette.BridgePrimary;
            buttonObject.AddComponent<ProceduralShape>().Init(ProceduralShape.ShapeKind.RoundedRect, 3.6f, 1.1f);
            var button = buttonObject.AddComponent<Button>();

            var labelText = CreateTMPText(buttonObject.transform, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero, 40, TextAlignmentOptions.Center);
            labelText.color = Color.white;
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.anchoredPosition = Vector2.zero;

            return button;
        }

        private static TMP_FontAsset cachedDefaultFont;

        private static TMP_FontAsset GetDefaultTMPFont()
        {
            if (cachedDefaultFont != null) return cachedDefaultFont;

            EnsureTMPEssentialsImported();
            cachedDefaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (cachedDefaultFont == null)
                Debug.LogWarning("Arrow Bridge: default TMP font asset not found after import; TMP texts may render without a font.");
            return cachedDefaultFont;
        }

        private static void EnsureTMPEssentialsImported()
        {
            const string marker = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<Object>(marker) != null) return;

            string packagePath = Path.Combine(EditorApplication.applicationContentsPath,
                "Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");

            if (File.Exists(packagePath))
            {
                AssetDatabase.ImportPackage(packagePath, false);
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning("Arrow Bridge: TMP Essential Resources.unitypackage not found at " + packagePath);
            }
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetFolderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
