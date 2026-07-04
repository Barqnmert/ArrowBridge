using System.Collections.Generic;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Generates every visual in the prototype from a tiny set of primitive shapes — a rounded
    /// rectangle, a triangle, and a heart (lives HUD) composed from those same circle/triangle
    /// distance fields — drawn pixel-by-pixel into a Texture2D and wrapped as a Sprite.
    /// No external art assets, no default Unity primitives. Runtime-safe (no UnityEditor
    /// dependency) so both the level builder and gameplay scripts can call it identically.
    ///
    /// Each distinct (size, color) combination gets its own texture at a fixed pixel corner
    /// radius — stretching one shared sprite would squash the rounding on longer shapes, so we
    /// regenerate (and cache) instead.
    /// </summary>
    public static class ShapeSpriteFactory
    {
        public const float PixelsPerUnit = 64f;
        public const int CornerRadiusPixels = 10;

        private static readonly Dictionary<(int, int, int, Color), Sprite> RoundedRectCache = new();
        private static readonly Dictionary<(int, int, Color), Sprite> TriangleCache = new();
        private static readonly Dictionary<(int, Color), Sprite> HeartCache = new();

        // ----- World-unit convenience wrappers (what gameplay code should call) -----

        public static Sprite CreateRoundedRectWorld(float worldWidth, float worldHeight, Color color, int cornerRadiusPx = CornerRadiusPixels)
        {
            int widthPx = Mathf.Max(1, Mathf.RoundToInt(worldWidth * PixelsPerUnit));
            int heightPx = Mathf.Max(1, Mathf.RoundToInt(worldHeight * PixelsPerUnit));
            return CreateRoundedRect(widthPx, heightPx, cornerRadiusPx, color);
        }

        public static Sprite CreateTriangleWorld(float worldWidth, float worldHeight, Color color)
        {
            int widthPx = Mathf.Max(1, Mathf.RoundToInt(worldWidth * PixelsPerUnit));
            int heightPx = Mathf.Max(1, Mathf.RoundToInt(worldHeight * PixelsPerUnit));
            return CreateTriangle(widthPx, heightPx, color);
        }

        /// <summary>Rounded rect with radius = half of its shorter side, i.e. a pill/capsule shape.</summary>
        public static Sprite CreateCapsuleWorld(float worldWidth, float worldHeight, Color color)
        {
            int widthPx = Mathf.Max(1, Mathf.RoundToInt(worldWidth * PixelsPerUnit));
            int heightPx = Mathf.Max(1, Mathf.RoundToInt(worldHeight * PixelsPerUnit));
            int radius = Mathf.Min(widthPx, heightPx) / 2;
            return CreateRoundedRect(widthPx, heightPx, radius, color);
        }

        /// <summary>A classic heart (two circle lobes over a downward triangle) for the lives HUD. Square texture, sizePx per side.</summary>
        public static Sprite CreateHeart(int sizePx, Color color)
        {
            var key = (sizePx, color);
            if (HeartCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

            var texture = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Heart_{sizePx}"
            };

            float s = sizePx;
            float lobeRadius = 0.26f * s;
            Vector2 lobeLeft = new Vector2(0.29f * s, 0.62f * s);
            Vector2 lobeRight = new Vector2(0.71f * s, 0.62f * s);
            // CCW triangle (apex -> right -> left) so "left of each edge" means inside.
            Vector2 apex = new Vector2(0.5f * s, 0.03f * s);
            Vector2 baseRight = new Vector2(0.95f * s, 0.66f * s);
            Vector2 baseLeft = new Vector2(0.05f * s, 0.66f * s);

            var pixels = new Color[sizePx * sizePx];
            for (int y = 0; y < sizePx; y++)
            {
                for (int x = 0; x < sizePx; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float dLeftLobe = Vector2.Distance(p, lobeLeft) - lobeRadius;
                    float dRightLobe = Vector2.Distance(p, lobeRight) - lobeRadius;
                    float triInside = Mathf.Min(
                        EdgeSignedDistance(apex, baseRight, p),
                        Mathf.Min(EdgeSignedDistance(baseRight, baseLeft, p), EdgeSignedDistance(baseLeft, apex, p)));
                    float dTriangle = -triInside;
                    float d = Mathf.Min(dTriangle, Mathf.Min(dLeftLobe, dRightLobe)); // union
                    float alpha = Mathf.Clamp01(0.5f - d);
                    pixels[y * sizePx + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            HeartCache[key] = sprite;
            return sprite;
        }

        // ----- Pixel-level generators -----

        public static Sprite CreateRoundedRect(int widthPx, int heightPx, int cornerRadiusPx, Color color)
        {
            var key = (widthPx, heightPx, cornerRadiusPx, color);
            if (RoundedRectCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

            var texture = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"RoundedRect_{widthPx}x{heightPx}_r{cornerRadiusPx}"
            };

            Vector2 half = new Vector2(widthPx, heightPx) * 0.5f;
            float radius = Mathf.Min(cornerRadiusPx, Mathf.Min(half.x, half.y));

            var pixels = new Color[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - half;
                    float distance = SignedDistanceRoundedBox(p, half, radius);
                    float alpha = Mathf.Clamp01(0.5f - distance); // ~1px soft edge
                    pixels[y * widthPx + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, widthPx, heightPx), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            RoundedRectCache[key] = sprite;
            return sprite;
        }

        /// <summary>A right-pointing triangle (apex at the right-middle, base along the left edge). Pivot sits on the base so parents can position it like an arrowhead flush against a shaft.</summary>
        public static Sprite CreateTriangle(int widthPx, int heightPx, Color color)
        {
            var key = (widthPx, heightPx, color);
            if (TriangleCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

            var texture = new Texture2D(widthPx, heightPx, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"Triangle_{widthPx}x{heightPx}"
            };

            Vector2 bottom = new Vector2(0f, 0f);
            Vector2 apex = new Vector2(widthPx, heightPx * 0.5f);
            Vector2 top = new Vector2(0f, heightPx);

            var pixels = new Color[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    // CCW winding (bottom -> apex -> top): "left of edge" is inside for all three.
                    float d0 = EdgeSignedDistance(bottom, apex, p);
                    float d1 = EdgeSignedDistance(apex, top, p);
                    float d2 = EdgeSignedDistance(top, bottom, p);
                    float minDistance = Mathf.Min(d0, Mathf.Min(d1, d2));
                    float alpha = Mathf.Clamp01(0.5f + minDistance); // ~1px soft edge
                    pixels[y * widthPx + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, widthPx, heightPx), new Vector2(0f, 0.5f), PixelsPerUnit);
            sprite.name = texture.name;
            TriangleCache[key] = sprite;
            return sprite;
        }

        /// <summary>Rounded-box signed distance field (Inigo Quilez). Negative = inside, positive = outside.</summary>
        private static float SignedDistanceRoundedBox(Vector2 p, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - halfSize.x + radius, Mathf.Abs(p.y) - halfSize.y + radius);
            float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            return outside + inside - radius;
        }

        /// <summary>Perpendicular signed distance from p to the line through a-&gt;b; positive = left of the directed edge.</summary>
        private static float EdgeSignedDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 edge = b - a;
            float length = edge.magnitude;
            if (length < 0.0001f) return 0f;
            Vector2 leftNormal = new Vector2(-edge.y, edge.x) / length;
            return Vector2.Dot(leftNormal, p - a);
        }
    }
}
