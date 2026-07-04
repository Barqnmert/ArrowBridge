using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ArrowBridge.EditorTools
{
    /// <summary>
    /// Independently proves ManualLevelLayout is solvable, without touching the scene or Play
    /// mode. Uses a greedy removal simulation: repeatedly remove ANY arrow whose exit path is
    /// currently clear. Removing an arrow only ever frees cells, never blocks another arrow's
    /// path, so this greedy approach is complete — if a solution exists, this loop finds it.
    /// Also sanity-checks the raw layout data first: every piece must be a valid connected path
    /// (no gaps/branches between consecutive cells), no two pieces may overlap, and — since this
    /// level is a dense tessellation — every board cell must belong to exactly one piece.
    /// </summary>
    public static class LevelSolvabilityVerifier
    {
        private const int ExtensionSearchLimit = 200;

        [MenuItem("Tools/Arrow Bridge/Verify Level Solvable")]
        public static void Verify()
        {
            var placements = ManualLevelLayout.Placements;
            int minX = ManualLevelLayout.MinX, maxX = ManualLevelLayout.MaxX;
            int minY = ManualLevelLayout.MinY, maxY = ManualLevelLayout.MaxY;

            if (!ValidateRawLayout(placements, minX, maxX, minY, maxY))
            {
                Debug.LogError("LevelSolvabilityVerifier: FAIL - layout data itself is invalid (see errors above).");
                return;
            }

            bool solved = TrySolve(placements, minX, maxX, minY, maxY, out var stuckIndices);

            if (solved)
            {
                Debug.Log($"LevelSolvabilityVerifier: PASS - all {placements.Length} arrows are removable.");
            }
            else
            {
                Debug.LogError($"LevelSolvabilityVerifier: FAIL - {stuckIndices.Count} arrow(s) permanently stuck: indices [{string.Join(", ", stuckIndices)}]");
            }
        }

        /// <summary>
        /// Runs the greedy removal simulation without logging — reused by LevelLayoutBaker to
        /// test candidate tessellations before committing to one.
        /// </summary>
        public static bool TrySolve(ArrowPlacement[] placements, int minX, int maxX, int minY, int maxY, out List<int> stuckIndices)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var placement in placements)
                foreach (var cell in placement.PathCells)
                    occupied.Add(cell);

            var remainingIndices = Enumerable.Range(0, placements.Length).ToList();
            bool progressMadeThisPass = true;

            while (progressMadeThisPass && remainingIndices.Count > 0)
            {
                progressMadeThisPass = false;
                for (int i = remainingIndices.Count - 1; i >= 0; i--)
                {
                    var placement = placements[remainingIndices[i]];
                    if (!IsExitPathClear(placement, occupied, minX, maxX, minY, maxY)) continue;

                    foreach (var cell in placement.PathCells) occupied.Remove(cell);
                    remainingIndices.RemoveAt(i);
                    progressMadeThisPass = true;
                }
            }

            stuckIndices = remainingIndices;
            return remainingIndices.Count == 0;
        }

        private static bool ValidateRawLayout(ArrowPlacement[] placements, int minX, int maxX, int minY, int maxY)
        {
            bool valid = true;
            var seenCells = new HashSet<Vector2Int>();

            for (int i = 0; i < placements.Length; i++)
            {
                var path = placements[i].PathCells;
                for (int j = 0; j < path.Count; j++)
                {
                    var cell = path[j];
                    if (cell.x < minX || cell.x > maxX || cell.y < minY || cell.y > maxY)
                    {
                        Debug.LogError($"LevelSolvabilityVerifier: arrow {i} has a cell {cell} outside board bounds.");
                        valid = false;
                    }
                    if (!seenCells.Add(cell))
                    {
                        Debug.LogError($"LevelSolvabilityVerifier: arrow {i} overlaps another arrow at cell {cell}.");
                        valid = false;
                    }
                    if (j > 0)
                    {
                        var prev = path[j - 1];
                        int manhattanDistance = Mathf.Abs(cell.x - prev.x) + Mathf.Abs(cell.y - prev.y);
                        if (manhattanDistance != 1)
                        {
                            Debug.LogError($"LevelSolvabilityVerifier: arrow {i} has a non-adjacent step between {prev} and {cell}.");
                            valid = false;
                        }
                    }
                }
            }

            if (valid)
            {
                int totalCells = (maxX - minX + 1) * (maxY - minY + 1);
                if (seenCells.Count != totalCells)
                {
                    Debug.LogError($"LevelSolvabilityVerifier: tessellation incomplete — {seenCells.Count}/{totalCells} board cells covered.");
                    valid = false;
                }
            }
            return valid;
        }

        /// <summary>
        /// True if, starting just past this piece's head and stepping in its exit direction, we
        /// reach off-board space before hitting another still-present piece's cell. Off-board
        /// space is infinite and always empty, so reaching it guarantees the piece can fully exit.
        /// </summary>
        private static bool IsExitPathClear(ArrowPlacement placement, HashSet<Vector2Int> occupied, int minX, int maxX, int minY, int maxY)
        {
            Vector2Int dir = ArrowDirections.ToVector(placement.ExitDirection);
            Vector2Int cursor = placement.PathCells[placement.PathCells.Count - 1];

            for (int i = 0; i < ExtensionSearchLimit; i++)
            {
                cursor += dir;
                bool withinBounds = cursor.x >= minX && cursor.x <= maxX && cursor.y >= minY && cursor.y <= maxY;
                if (!withinBounds) return true;
                if (occupied.Contains(cursor)) return false;
            }
            return true;
        }
    }
}
