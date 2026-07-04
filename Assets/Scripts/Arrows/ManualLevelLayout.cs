using System.Collections.Generic;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>A fixed bent (or straight) path of cells, tail to head, plus the single direction it exits in.</summary>
    public readonly struct ArrowPlacement
    {
        public readonly IReadOnlyList<Vector2Int> PathCells;
        public readonly ArrowDirection ExitDirection;

        public ArrowPlacement(IReadOnlyList<Vector2Int> pathCells, ArrowDirection exitDirection)
        {
            PathCells = pathCells;
            ExitDirection = exitDirection;
        }
    }

    /// <summary>
    /// The single fixed level: a dense, gap-free tessellation of the whole puzzle board into 30
    /// snake-shaped arrow paths. Baked via the LevelLayoutBaker dev tool and confirmed solvable by
    /// LevelSolvabilityVerifier — deliberately NOT randomized at build/run time, so every
    /// playthrough sees the exact same, pre-verified board.
    /// </summary>
    public static class ManualLevelLayout
    {
        // Portrait (9:16) board: 10 columns x 12 rows, sitting above the bridge/water strip.
        public const int MinX = -5;
        public const int MaxX = 4;
        public const int MinY = 3;
        public const int MaxY = 14;

        // Baked by LevelLayoutBaker for the 10x12 portrait board (succeeded at attempt 260,
        // ~44% head-flip variety), then confirmed solvable by LevelSolvabilityVerifier.
        public static readonly ArrowPlacement[] Placements =
        {
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-4, 3), new Vector2Int(-5, 3) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(2, 3), new Vector2Int(1, 3), new Vector2Int(0, 3), new Vector2Int(-1, 3), new Vector2Int(-2, 3), new Vector2Int(-3, 3) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(4, 4), new Vector2Int(4, 3), new Vector2Int(3, 3) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(3, 4), new Vector2Int(2, 4), new Vector2Int(1, 4), new Vector2Int(0, 4) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-1, 4), new Vector2Int(-2, 4), new Vector2Int(-3, 4), new Vector2Int(-4, 4) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-3, 5), new Vector2Int(-4, 5), new Vector2Int(-5, 5), new Vector2Int(-5, 4) }, ArrowDirection.Down),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(0, 5), new Vector2Int(-1, 5), new Vector2Int(-2, 5) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(4, 5), new Vector2Int(3, 5), new Vector2Int(2, 5), new Vector2Int(1, 5) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-2, 6), new Vector2Int(-1, 6), new Vector2Int(0, 6), new Vector2Int(1, 6), new Vector2Int(2, 6), new Vector2Int(3, 6), new Vector2Int(4, 6) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-3, 6), new Vector2Int(-4, 6) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-5, 6), new Vector2Int(-5, 7) }, ArrowDirection.Up),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-1, 7), new Vector2Int(-2, 7), new Vector2Int(-3, 7), new Vector2Int(-4, 7) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(3, 7), new Vector2Int(2, 7), new Vector2Int(1, 7), new Vector2Int(0, 7) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(4, 8), new Vector2Int(4, 7) }, ArrowDirection.Down),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-3, 8), new Vector2Int(-2, 8), new Vector2Int(-1, 8), new Vector2Int(0, 8), new Vector2Int(1, 8), new Vector2Int(2, 8), new Vector2Int(3, 8) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-4, 8), new Vector2Int(-5, 8), new Vector2Int(-5, 9), new Vector2Int(-4, 9) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-3, 9), new Vector2Int(-2, 9), new Vector2Int(-1, 9) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(0, 9), new Vector2Int(1, 9), new Vector2Int(2, 9), new Vector2Int(3, 9) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(4, 9), new Vector2Int(4, 10), new Vector2Int(3, 10), new Vector2Int(2, 10), new Vector2Int(1, 10) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(0, 10), new Vector2Int(-1, 10), new Vector2Int(-2, 10), new Vector2Int(-3, 10), new Vector2Int(-4, 10), new Vector2Int(-5, 10) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-4, 11), new Vector2Int(-5, 11) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-1, 11), new Vector2Int(-2, 11), new Vector2Int(-3, 11) }, ArrowDirection.Left),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(0, 11), new Vector2Int(1, 11), new Vector2Int(2, 11), new Vector2Int(3, 11), new Vector2Int(4, 11) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-2, 12), new Vector2Int(-1, 12), new Vector2Int(0, 12), new Vector2Int(1, 12), new Vector2Int(2, 12), new Vector2Int(3, 12), new Vector2Int(4, 12) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-3, 12), new Vector2Int(-4, 12), new Vector2Int(-5, 12), new Vector2Int(-5, 13) }, ArrowDirection.Up),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-4, 13), new Vector2Int(-3, 13), new Vector2Int(-2, 13), new Vector2Int(-1, 13), new Vector2Int(0, 13) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(1, 13), new Vector2Int(2, 13), new Vector2Int(3, 13), new Vector2Int(4, 13) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(3, 14), new Vector2Int(4, 14) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-1, 14), new Vector2Int(0, 14), new Vector2Int(1, 14), new Vector2Int(2, 14) }, ArrowDirection.Right),
            new ArrowPlacement(new Vector2Int[] { new Vector2Int(-2, 14), new Vector2Int(-3, 14), new Vector2Int(-4, 14), new Vector2Int(-5, 14) }, ArrowDirection.Left),
        };
    }
}
