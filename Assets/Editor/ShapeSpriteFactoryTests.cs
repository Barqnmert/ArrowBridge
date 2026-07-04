using UnityEditor;
using UnityEngine;

namespace ArrowBridge.EditorTools
{
    /// <summary>
    /// Automated stand-in for "look at it and check the colors are right" — since this tool has
    /// no way to see the rendered Game view, it instead samples actual pixels out of the
    /// generated textures and asserts they match the intended palette color / shape boundary.
    /// Run via Tools > Arrow Bridge > Run Shape Factory Self-Test (also usable with
    /// -executeMethod in batchmode; logs a single PASS/FAIL line at the end).
    /// </summary>
    public static class ShapeSpriteFactoryTests
    {
        private const float ColorTolerance = 0.02f;

        [MenuItem("Tools/Arrow Bridge/Run Shape Factory Self-Test")]
        public static void RunSelfTest()
        {
            bool allPassed = true;

            allPassed &= CheckRoundedRect();
            allPassed &= CheckTriangle();

            if (allPassed)
                Debug.Log("ShapeSpriteFactory self-test: PASS");
            else
                Debug.LogError("ShapeSpriteFactory self-test: FAIL");
        }

        private static bool CheckRoundedRect()
        {
            const int width = 128;
            const int height = 64;
            const int radius = 10;
            Color expected = GamePalette.BridgePrimary;

            var sprite = ShapeSpriteFactory.CreateRoundedRect(width, height, radius, expected);
            var texture = sprite.texture;

            bool ok = true;
            ok &= ExpectInside(texture, width / 2, height / 2, expected, "RoundedRect center");
            ok &= ExpectOutside(texture, 0, 0, "RoundedRect corner pixel (0,0)");
            ok &= ExpectOutside(texture, width - 1, 0, "RoundedRect corner pixel (w-1,0)");
            ok &= ExpectOutside(texture, 0, height - 1, "RoundedRect corner pixel (0,h-1)");
            ok &= ExpectInside(texture, width / 2, 2, expected, "RoundedRect bottom-edge midpoint (flat side, no rounding)");
            return ok;
        }

        private static bool CheckTriangle()
        {
            const int width = 64;
            const int height = 64;
            Color expected = GamePalette.ArrowAccent;

            var sprite = ShapeSpriteFactory.CreateTriangle(width, height, expected);
            var texture = sprite.texture;

            bool ok = true;
            ok &= ExpectInside(texture, width / 2, height / 2, expected, "Triangle center");
            ok &= ExpectOutside(texture, 5, height - 1, "Triangle above the sloped top edge");
            ok &= ExpectOutside(texture, 5, 0, "Triangle below the sloped bottom edge");
            return ok;
        }

        private static bool ExpectInside(Texture2D texture, int x, int y, Color expectedColor, string label)
        {
            Color pixel = texture.GetPixel(x, y);
            if (pixel.a < 0.9f)
            {
                Debug.LogError($"[{label}] expected opaque (alpha>=0.9) but got alpha={pixel.a:F3}");
                return false;
            }
            if (!ColorsClose(pixel, expectedColor))
            {
                Debug.LogError($"[{label}] expected color {expectedColor} but got {pixel}");
                return false;
            }
            return true;
        }

        private static bool ExpectOutside(Texture2D texture, int x, int y, string label)
        {
            Color pixel = texture.GetPixel(x, y);
            if (pixel.a > 0.1f)
            {
                Debug.LogError($"[{label}] expected transparent (alpha<=0.1) but got alpha={pixel.a:F3}");
                return false;
            }
            return true;
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < ColorTolerance
                && Mathf.Abs(a.g - b.g) < ColorTolerance
                && Mathf.Abs(a.b - b.b) < ColorTolerance;
        }
    }
}
