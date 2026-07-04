using System.Collections.Generic;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Helpers for building the 3D world (islands, water, truss bridge) out of colored box
    /// meshes — the 3D counterpart of ShapeSpriteFactory. Materials are cached per color and use
    /// the Standard shader so directional lighting gives the flat-colored geometry real depth.
    /// Runtime-safe (no UnityEditor dependency): BridgeBuilder calls it during play.
    /// </summary>
    public static class Decor3DFactory
    {
        private static readonly Dictionary<Color, Material> MaterialCache = new();

        public static Material GetMaterial(Color color)
        {
            if (MaterialCache.TryGetValue(color, out var cached) && cached != null) return cached;

            var material = new Material(Shader.Find("Standard"))
            {
                name = $"Decor_{ColorUtility.ToHtmlStringRGB(color)}",
                color = color
            };
            material.SetFloat("_Glossiness", 0.15f); // matte, toy-like finish
            MaterialCache[color] = material;
            return material;
        }

        /// <summary>An axis-aligned colored box. No collider — world dressing never blocks input rays.</summary>
        public static GameObject CreateBox(string name, Vector3 center, Vector3 size, Color color, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            if (Application.isPlaying) Object.Destroy(box.GetComponent<Collider>());
            else Object.DestroyImmediate(box.GetComponent<Collider>());

            if (parent != null) box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(color);
            return box;
        }

        /// <summary>A box stretched between two points — the workhorse for truss diagonals, rails and posts.</summary>
        public static GameObject CreateBeam(string name, Vector3 from, Vector3 to, float thickness, Color color, Transform parent = null)
        {
            Vector3 center = (from + to) * 0.5f;
            Vector3 span = to - from;
            float length = span.magnitude;

            var beam = CreateBox(name, center, new Vector3(length, thickness, thickness), color, parent);
            if (length > 0.0001f)
            {
                beam.transform.rotation = Quaternion.FromToRotation(Vector3.right, span.normalized);
            }
            return beam;
        }
    }
}
