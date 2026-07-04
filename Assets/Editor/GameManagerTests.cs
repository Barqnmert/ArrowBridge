using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArrowBridge.EditorTools
{
    /// <summary>
    /// Exercises GameManager's counting/bridge-integration and lives logic directly. The
    /// ArrowExited/ArrowBlocked events are C# events and can only be raised from inside
    /// ArrowController, so this calls GameManager's private handlers via reflection instead of
    /// simulating real click input (which would require Play mode).
    ///
    /// By design, the actual Won state only fires once the character's walk-across animation
    /// completes (a Coroutine callback) — Coroutines never tick outside Play mode, so that final
    /// transition genuinely cannot be observed from an edit-mode batch script. This test covers
    /// everything up to that point; the walk-then-win sequence itself needs a Play mode check by
    /// a human.
    /// </summary>
    public static class GameManagerTests
    {
        [MenuItem("Tools/Arrow Bridge/Run GameManager Self-Test")]
        public static void RunSelfTest()
        {
            bool allPassed = true;
            allPassed &= RunProgressScenario();
            allPassed &= RunLivesScenario();

            Debug.Log(allPassed ? "GameManager self-test: PASS" : "GameManager self-test: FAIL");
        }

        private static bool RunProgressScenario()
        {
            var bridgeObject = new GameObject("BridgeBuilderTest");
            var bridge = bridgeObject.AddComponent<BridgeBuilder>();
            bridge.Configure(-6f, 6f, 0f, 30);

            var gmObject = new GameObject("GameManagerTest");
            var gm = gmObject.AddComponent<GameManager>();
            gm.Configure(new Vector3(7f, 0f, 0f), 30, 3);

            var handleExited = typeof(GameManager).GetMethod("HandleArrowExited", BindingFlags.NonPublic | BindingFlags.Instance);

            bool passed = true;
            for (int i = 0; i < 30; i++)
            {
                handleExited.Invoke(gm, new object[] { null });
            }

            passed &= Check(gm.RemovedCount == 30, $"RemovedCount expected 30, got {gm.RemovedCount}");
            passed &= Check(bridge.SegmentsBuilt == 30, $"BridgeBuilder.SegmentsBuilt expected 30, got {bridge.SegmentsBuilt}");
            passed &= Check(!gm.IsWon, "IsWon should still be false here — it only flips once the (Play-mode-only) walk animation finishes.");

            Object.DestroyImmediate(gmObject);
            Object.DestroyImmediate(bridgeObject);
            return passed;
        }

        private static bool RunLivesScenario()
        {
            var gmObject = new GameObject("GameManagerLivesTest");
            var gm = gmObject.AddComponent<GameManager>();
            gm.Configure(new Vector3(7f, 0f, 0f), 30, 3);

            bool failFired = false;
            void OnFailed() => failFired = true;
            GameManager.LevelFailed += OnFailed;

            var handleBlocked = typeof(GameManager).GetMethod("HandleArrowBlocked", BindingFlags.NonPublic | BindingFlags.Instance);

            bool passed = true;
            passed &= Check(gm.RemainingLives == 3, $"RemainingLives should start at 3, got {gm.RemainingLives}");

            handleBlocked.Invoke(gm, new object[] { null });
            passed &= Check(gm.RemainingLives == 2 && !gm.IsFailed, $"After 1 wrong move: expected 2 lives / not failed, got {gm.RemainingLives} / failed={gm.IsFailed}");

            handleBlocked.Invoke(gm, new object[] { null });
            passed &= Check(gm.RemainingLives == 1 && !gm.IsFailed, $"After 2 wrong moves: expected 1 life / not failed, got {gm.RemainingLives} / failed={gm.IsFailed}");

            handleBlocked.Invoke(gm, new object[] { null });
            passed &= Check(gm.RemainingLives == 0 && gm.IsFailed, $"After 3 wrong moves: expected 0 lives / failed, got {gm.RemainingLives} / failed={gm.IsFailed}");
            passed &= Check(failFired, "LevelFailed event should have fired on the 3rd wrong move");

            // Further wrong moves after failing must be inert.
            handleBlocked.Invoke(gm, new object[] { null });
            passed &= Check(gm.RemainingLives == 0, "RemainingLives must not go below 0 / change after fail");

            GameManager.LevelFailed -= OnFailed;
            Object.DestroyImmediate(gmObject);
            return passed;
        }

        private static bool Check(bool condition, string message)
        {
            if (!condition) Debug.LogError(message);
            return condition;
        }
    }
}
