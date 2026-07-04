using UnityEditor;
using UnityEngine;

namespace ArrowBridge.EditorTools
{
    /// <summary>Directly exercises BridgeBuilder.AddSegment (pure, no coroutines) 30 times and checks the resulting deck/truss counts and positions.</summary>
    public static class BridgeBuilderTests
    {
        [MenuItem("Tools/Arrow Bridge/Run Bridge Builder Self-Test")]
        public static void RunSelfTest()
        {
            var go = new GameObject("BridgeBuilderTest");
            var bridge = go.AddComponent<BridgeBuilder>();
            bridge.Configure(-6f, 6f, 0f, 30);

            bool allPassed = true;

            float expectedSegmentWidth = 12f / 30f;
            float expectedFirstCenterX = -6f + expectedSegmentWidth * 0.5f;
            allPassed &= Approximately(bridge.GetNextSegmentWorldPosition().x, expectedFirstCenterX, "First segment center X");

            for (int i = 0; i < 30; i++) bridge.AddSegment();

            allPassed &= Check(bridge.SegmentsBuilt == 30, $"SegmentsBuilt expected 30, got {bridge.SegmentsBuilt}");

            int segmentChildren = 0, trussChildren = 0;
            foreach (Transform child in go.transform)
            {
                if (child.name.StartsWith("BridgeSegment_")) segmentChildren++;
                if (child.name.StartsWith("Truss_")) trussChildren++;
            }
            allPassed &= Check(segmentChildren == 30, $"Expected 30 deck segments, got {segmentChildren}");
            allPassed &= Check(trussChildren == 10, $"Expected 10 truss decorations (every 3rd of 30), got {trussChildren}");

            float expectedLastCenterX = -6f + expectedSegmentWidth * 29.5f;
            allPassed &= Approximately(go.transform.Find("BridgeSegment_29").position.x, expectedLastCenterX, "Last segment center X");

            // Calling AddSegment beyond the total must be a no-op, not throw or overshoot.
            bridge.AddSegment();
            allPassed &= Check(bridge.SegmentsBuilt == 30, "AddSegment beyond total should be a no-op");

            Object.DestroyImmediate(go);

            Debug.Log(allPassed ? "BridgeBuilder self-test: PASS" : "BridgeBuilder self-test: FAIL");
        }

        private static bool Check(bool condition, string message)
        {
            if (!condition) Debug.LogError(message);
            return condition;
        }

        private static bool Approximately(float a, float b, string label)
        {
            if (Mathf.Abs(a - b) > 0.001f)
            {
                Debug.LogError($"{label}: expected {b}, got {a}");
                return false;
            }
            return true;
        }
    }
}
