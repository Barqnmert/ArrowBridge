using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArrowBridge.EditorTools
{
    /// <summary>
    /// Throwaway visual sign-off scene: drops a handful of sample rounded rects / triangles /
    /// capsules in every palette color side by side, so the palette + shape system can be
    /// eyeballed in the Editor before it gets used all over the real level.
    /// </summary>
    public static class ShapePreviewBuilder
    {
        private const string PreviewScenePath = "Assets/Scenes/ShapePreview.unity";

        [MenuItem("Tools/Arrow Bridge/Build Shape Preview Scene")]
        public static void BuildPreviewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GamePalette.Background;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            (string label, Color color)[] swatches =
            {
                ("Water", GamePalette.Water),
                ("Land", GamePalette.Land),
                ("ArrowAccent", GamePalette.ArrowAccent),
                ("ArrowBody", GamePalette.ArrowBody),
                ("BridgePrimary", GamePalette.BridgePrimary),
                ("BridgeAccentDark", GamePalette.BridgeAccentDark),
                ("Character", GamePalette.Character),
                ("ProgressTrack", GamePalette.ProgressTrack),
            };

            float spacing = 1.4f;
            float startX = -(swatches.Length - 1) * spacing * 0.5f;

            for (int i = 0; i < swatches.Length; i++)
            {
                float x = startX + i * spacing;

                CreateSpriteObject($"RoundedRect_{swatches[i].label}",
                    new Vector3(x, 1.5f, 0f),
                    ShapeSpriteFactory.CreateRoundedRectWorld(1f, 1f, swatches[i].color));

                CreateSpriteObject($"Triangle_{swatches[i].label}",
                    new Vector3(x, 0f, 0f),
                    ShapeSpriteFactory.CreateTriangleWorld(1f, 1f, swatches[i].color));

                CreateSpriteObject($"Capsule_{swatches[i].label}",
                    new Vector3(x, -1.5f, 0f),
                    ShapeSpriteFactory.CreateCapsuleWorld(0.6f, 1f, swatches[i].color));
            }

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Arrow Bridge: Shape preview scene built at " + PreviewScenePath +
                       " — open it in the Editor to eyeball palette colors and shape rounding.");
        }

        private static void CreateSpriteObject(string name, Vector3 position, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;
            string parent = System.IO.Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(assetFolderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
