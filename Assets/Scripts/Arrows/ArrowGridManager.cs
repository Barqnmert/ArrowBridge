using System.Collections.Generic;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Tracks which grid cells inside the puzzle area are occupied by which arrow, and answers
    /// bounds/occlusion queries used by ArrowController while dragging.
    /// </summary>
    public class ArrowGridManager : MonoBehaviour
    {
        public static ArrowGridManager Instance { get; private set; }

        [Header("Puzzle Board Bounds (grid cells, inclusive)")]
        [SerializeField] private int minX = -8;
        [SerializeField] private int maxX = 8;
        [SerializeField] private int minY = 3;
        [SerializeField] private int maxY = 8;
        [SerializeField] private float cellSize = 1f;

        [Header("Exit Feel")]
        [Tooltip("Extra cells beyond the strict tessellation bounds before a piece counts as \"fully exited\". Purely a feel/timing knob — the real occupancy/collision bounds above are unaffected.")]
        [SerializeField] private int exitBoundaryMargin = 1;

        public int MinX => minX;
        public int MaxX => maxX;
        public int MinY => minY;
        public int MaxY => maxY;
        public float CellSize => cellSize;
        public int ExitBoundaryMargin => exitBoundaryMargin;

        private readonly Dictionary<Vector2Int, ArrowController> occupancy = new Dictionary<Vector2Int, ArrowController>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(int newMinX, int newMaxX, int newMinY, int newMaxY, float newCellSize)
        {
            minX = newMinX;
            maxX = newMaxX;
            minY = newMinY;
            maxY = newMaxY;
            cellSize = newCellSize;

            // Awake() does not run for components added via editor scripts outside Play mode, so
            // the level builder needs Instance set explicitly here (Awake still covers Play mode).
            Instance = this;
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);
        }

        public bool IsCellOccupied(Vector2Int cell, ArrowController excluding)
        {
            return occupancy.TryGetValue(cell, out var occupant) && occupant != excluding;
        }

        public void RegisterArrow(ArrowController arrow, IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells) occupancy[cell] = arrow;
        }

        public void UnregisterCells(IEnumerable<Vector2Int> cells)
        {
            foreach (var cell in cells) occupancy.Remove(cell);
        }

        /// <summary>True when every given cell has fully left the board (plus ExitBoundaryMargin) on the side matching direction.</summary>
        public bool IsFullyOutsideBounds(IEnumerable<Vector2Int> cells, ArrowDirection direction)
        {
            int marginedMaxX = maxX + exitBoundaryMargin;
            int marginedMinX = minX - exitBoundaryMargin;
            int marginedMaxY = maxY + exitBoundaryMargin;
            int marginedMinY = minY - exitBoundaryMargin;

            foreach (var cell in cells)
            {
                switch (direction)
                {
                    case ArrowDirection.Right when cell.x <= marginedMaxX:
                    case ArrowDirection.Left when cell.x >= marginedMinX:
                    case ArrowDirection.Up when cell.y <= marginedMaxY:
                    case ArrowDirection.Down when cell.y >= marginedMinY:
                        return false;
                }
            }
            return true;
        }

        /// <summary>True when the cell lies beyond the margin-expanded board on ANY side — i.e. the first cell where an exiting head is visually "out".</summary>
        public bool IsCellOutsideExpandedBounds(Vector2Int cell)
        {
            return cell.x < minX - exitBoundaryMargin || cell.x > maxX + exitBoundaryMargin
                || cell.y < minY - exitBoundaryMargin || cell.y > maxY + exitBoundaryMargin;
        }

        /// <summary>World-space bounding box of the puzzle area including ExitBoundaryMargin — used to route exit animations around the outside of it.</summary>
        public (Vector2 min, Vector2 max) GetExpandedWorldBounds()
        {
            float halfCell = cellSize * 0.5f;
            Vector2 min = new Vector2((minX - exitBoundaryMargin) * cellSize - halfCell, (minY - exitBoundaryMargin) * cellSize - halfCell);
            Vector2 max = new Vector2((maxX + exitBoundaryMargin) * cellSize + halfCell, (maxY + exitBoundaryMargin) * cellSize + halfCell);
            return (min, max);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Vector3 center = new Vector3((minX + maxX) * 0.5f * cellSize, (minY + maxY) * 0.5f * cellSize, 0f);
            Vector3 size = new Vector3((maxX - minX + 1) * cellSize, (maxY - minY + 1) * cellSize, 0.1f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
