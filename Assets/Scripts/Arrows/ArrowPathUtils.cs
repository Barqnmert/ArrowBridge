using System.Collections.Generic;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Shared math for path-shaped ("snake") arrows, used by both the runtime ArrowController and
    /// editor-time level generation/verification tools so the two never drift apart.
    /// </summary>
    public static class ArrowPathUtils
    {
        /// <summary>
        /// The combined "track" an arrow can occupy: its own fixed bent path (tail to head),
        /// followed by a straight run in exitDirection extending well past the board. Cells never
        /// change once built — pulling the arrow just slides which contiguous window of this list
        /// is currently "occupied" (see <see cref="GetWindow"/>).
        /// </summary>
        public static List<Vector2Int> BuildCombinedTrack(IReadOnlyList<Vector2Int> pathCells, ArrowDirection exitDirection, int extensionLength)
        {
            var track = new List<Vector2Int>(pathCells.Count + extensionLength);
            track.AddRange(pathCells);

            Vector2Int dir = ArrowDirections.ToVector(exitDirection);
            Vector2Int cursor = pathCells[pathCells.Count - 1];
            for (int i = 0; i < extensionLength; i++)
            {
                cursor += dir;
                track.Add(cursor);
            }
            return track;
        }

        /// <summary>The `windowLength` cells starting at `offset` cells into the combined track.</summary>
        public static List<Vector2Int> GetWindow(List<Vector2Int> combinedTrack, int windowLength, int offset)
        {
            var window = new List<Vector2Int>(windowLength);
            for (int i = 0; i < windowLength; i++) window.Add(combinedTrack[offset + i]);
            return window;
        }
    }
}
