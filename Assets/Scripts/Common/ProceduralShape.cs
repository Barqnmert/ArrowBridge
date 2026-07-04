using UnityEngine;
using UnityEngine.UI;

namespace ArrowBridge
{
    /// <summary>
    /// Re-creates a procedural sprite for this object's SpriteRenderer or UI Image every time the
    /// scene loads. This exists because ShapeSpriteFactory sprites are in-memory objects, not
    /// assets: any sprite assigned at scene-build time silently dies the next time the project is
    /// opened, leaving renderers blank (this is exactly why the hearts/progress bar vanished after
    /// the scene was built in a separate batch-mode session). Shape parameters are serialized here
    /// instead, and the actual pixels are regenerated on demand. Tint via the renderer/Image color
    /// (sprites are generated white).
    /// </summary>
    [ExecuteAlways]
    public class ProceduralShape : MonoBehaviour
    {
        public enum ShapeKind
        {
            RoundedRect,
            SharpRect, // corner radius 0
            Capsule,
            Triangle,
            Heart
        }

        [SerializeField] private ShapeKind kind = ShapeKind.RoundedRect;
        [SerializeField] private float worldWidth = 1f;
        [SerializeField] private float worldHeight = 1f;
        [SerializeField] private int heartTexturePixels = 96;

        private void Awake()
        {
            Apply();
        }

        public void Init(ShapeKind newKind, float newWorldWidth, float newWorldHeight, int newHeartTexturePixels = 96)
        {
            kind = newKind;
            worldWidth = newWorldWidth;
            worldHeight = newWorldHeight;
            heartTexturePixels = newHeartTexturePixels;
            Apply();
        }

        public void Apply()
        {
            Sprite sprite;
            switch (kind)
            {
                case ShapeKind.SharpRect:
                    sprite = ShapeSpriteFactory.CreateRoundedRectWorld(worldWidth, worldHeight, Color.white, 0);
                    break;
                case ShapeKind.Capsule:
                    sprite = ShapeSpriteFactory.CreateCapsuleWorld(worldWidth, worldHeight, Color.white);
                    break;
                case ShapeKind.Triangle:
                    sprite = ShapeSpriteFactory.CreateTriangleWorld(worldWidth, worldHeight, Color.white);
                    break;
                case ShapeKind.Heart:
                    sprite = ShapeSpriteFactory.CreateHeart(heartTexturePixels, Color.white);
                    break;
                default:
                    sprite = ShapeSpriteFactory.CreateRoundedRectWorld(worldWidth, worldHeight, Color.white);
                    break;
            }

            if (TryGetComponent<SpriteRenderer>(out var spriteRenderer)) spriteRenderer.sprite = sprite;
            if (TryGetComponent<Image>(out var image)) image.sprite = sprite;
        }
    }
}
