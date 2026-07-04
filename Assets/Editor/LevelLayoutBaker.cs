using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArrowBridge.EditorTools
{
    /// <summary>
    /// DEV TOOL (not part of the game): tessellates the whole puzzle board with a single
    /// boustrophedon ("mow the lawn") Hamiltonian path, cuts it into 30 contiguous pieces (each
    /// automatically a valid connected, branch-free, 90°-turn path — a slice of a Hamiltonian
    /// path always is), then retries with different cuts/orientations until
    /// LevelSolvabilityVerifier confirms the result is fully solvable. Prints the winning layout
    /// as C# array literal text to paste into ManualLevelLayout.cs. Board dimensions come from
    /// ManualLevelLayout's bounds constants, so re-baking after a board resize needs no edits here.
    /// </summary>
    public static class LevelLayoutBaker
    {
        private const int TotalArrows = 30;
        private const int MaxAttempts = 2000;

        [MenuItem("Tools/Arrow Bridge/[DEV] Bake Manual Level Layout")]
        public static void Bake()
        {
            int minX = ManualLevelLayout.MinX, maxX = ManualLevelLayout.MaxX;
            int minY = ManualLevelLayout.MinY, maxY = ManualLevelLayout.MaxY;
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int totalCells = width * height;
            int minLength = Mathf.Max(2, totalCells / TotalArrows - 2);
            int maxLength = totalCells / TotalArrows + 3;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var rng = new System.Random(Environment.TickCount + attempt * 7919 + 1);

                var lengths = RandomizeCutLengths(rng, totalCells, TotalArrows, minLength, maxLength);
                if (lengths == null) continue;

                // Head placement: putting every head at its segment's forward (master-path) end is
                // PROVABLY solvable — remove pieces in reverse path order and each exit ray stays
                // inside the already-freed path suffix. Random head flips add visual variety but
                // can deadlock dense boards (tall boards especially), so the flip chance decays
                // toward the guaranteed all-forward configuration as attempts burn down.
                float flipProbability = 0.5f * (1f - (float)attempt / MaxAttempts);

                var masterPath = BuildBoustrophedon(width, height, minX, minY, rng);
                var placements = CutIntoPlacements(masterPath, lengths, rng, flipProbability);

                if (LevelSolvabilityVerifier.TrySolve(placements, minX, maxX, minY, maxY, out _))
                {
                    LogPlacements(placements);
                    Debug.Log($"Arrow Bridge: bake succeeded after {attempt + 1} attempt(s) for a {width}x{height} board.");
                    return;
                }
            }

            Debug.LogError($"Arrow Bridge: bake failed after {MaxAttempts} attempts — could not find a solvable dense tessellation.");
        }

        private static List<Vector2Int> BuildBoustrophedon(int width, int height, int minX, int minY, System.Random rng)
        {
            var path = new List<Vector2Int>(width * height);
            bool rowMajor = rng.Next(2) == 0;

            if (rowMajor)
            {
                for (int row = 0; row < height; row++)
                {
                    bool leftToRight = row % 2 == 0;
                    for (int i = 0; i < width; i++)
                    {
                        int col = leftToRight ? i : width - 1 - i;
                        path.Add(new Vector2Int(minX + col, minY + row));
                    }
                }
            }
            else
            {
                for (int col = 0; col < width; col++)
                {
                    bool bottomToTop = col % 2 == 0;
                    for (int i = 0; i < height; i++)
                    {
                        int row = bottomToTop ? i : height - 1 - i;
                        path.Add(new Vector2Int(minX + col, minY + row));
                    }
                }
            }
            return path;
        }

        /// <summary>Lengths summing to exactly totalCells, each within [minLen,maxLen], with randomized variety.</summary>
        private static int[] RandomizeCutLengths(System.Random rng, int totalCells, int count, int minLen, int maxLen)
        {
            if (totalCells < count * minLen || totalCells > count * maxLen) return null;

            int baseLen = totalCells / count;
            int remainder = totalCells - baseLen * count;
            var lengths = new int[count];
            for (int i = 0; i < count; i++) lengths[i] = baseLen;
            for (int i = 0; i < remainder; i++) lengths[i]++;

            for (int swap = 0; swap < count * 3; swap++)
            {
                int a = rng.Next(count);
                int b = rng.Next(count);
                if (a == b) continue;
                if (lengths[a] + 1 <= maxLen && lengths[b] - 1 >= minLen)
                {
                    lengths[a]++;
                    lengths[b]--;
                }
            }

            for (int i = lengths.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (lengths[i], lengths[j]) = (lengths[j], lengths[i]);
            }
            return lengths;
        }

        private static ArrowPlacement[] CutIntoPlacements(List<Vector2Int> masterPath, int[] lengths, System.Random rng, float flipProbability)
        {
            var placements = new ArrowPlacement[lengths.Length];
            int cursor = 0;

            for (int i = 0; i < lengths.Length; i++)
            {
                int len = lengths[i];
                var segment = masterPath.GetRange(cursor, len);
                cursor += len;

                bool headAtSegmentEnd = len == 1 || rng.NextDouble() >= flipProbability;
                List<Vector2Int> orderedPath;
                ArrowDirection exitDirection;

                if (headAtSegmentEnd)
                {
                    orderedPath = segment;
                    exitDirection = DirectionBetween(segment[segment.Count - 2], segment[segment.Count - 1]);
                }
                else
                {
                    orderedPath = new List<Vector2Int>(segment);
                    orderedPath.Reverse();
                    exitDirection = DirectionBetween(segment[1], segment[0]);
                }

                placements[i] = new ArrowPlacement(orderedPath, exitDirection);
            }
            return placements;
        }

        private static ArrowDirection DirectionBetween(Vector2Int from, Vector2Int to)
        {
            Vector2Int delta = to - from;
            if (delta == new Vector2Int(1, 0)) return ArrowDirection.Right;
            if (delta == new Vector2Int(-1, 0)) return ArrowDirection.Left;
            if (delta == new Vector2Int(0, 1)) return ArrowDirection.Up;
            if (delta == new Vector2Int(0, -1)) return ArrowDirection.Down;
            throw new InvalidOperationException($"Non-adjacent master path step from {from} to {to}.");
        }

        private static void LogPlacements(ArrowPlacement[] placements)
        {
            var sb = new StringBuilder();
            sb.AppendLine("public static readonly ArrowPlacement[] Placements =");
            sb.AppendLine("{");
            foreach (var placement in placements)
            {
                var cellStrings = new List<string>(placement.PathCells.Count);
                foreach (var cell in placement.PathCells) cellStrings.Add($"new Vector2Int({cell.x}, {cell.y})");
                sb.AppendLine($"    new ArrowPlacement(new Vector2Int[] {{ {string.Join(", ", cellStrings)} }}, ArrowDirection.{placement.ExitDirection}),");
            }
            sb.AppendLine("};");
            Debug.Log(sb.ToString());
        }
    }
}
