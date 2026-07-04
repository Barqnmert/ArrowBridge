using UnityEngine;

namespace ArrowBridge
{
    public enum ArrowDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>Single source of truth for direction &lt;-&gt; grid vector, shared by runtime and generation code.</summary>
    public static class ArrowDirections
    {
        public static Vector2Int ToVector(ArrowDirection direction)
        {
            switch (direction)
            {
                case ArrowDirection.Right: return new Vector2Int(1, 0);
                case ArrowDirection.Left: return new Vector2Int(-1, 0);
                case ArrowDirection.Up: return new Vector2Int(0, 1);
                case ArrowDirection.Down: return new Vector2Int(0, -1);
                default: return Vector2Int.zero;
            }
        }

        public static float ToZAngleDegrees(ArrowDirection direction)
        {
            switch (direction)
            {
                case ArrowDirection.Right: return 0f;
                case ArrowDirection.Up: return 90f;
                case ArrowDirection.Left: return 180f;
                case ArrowDirection.Down: return -90f;
                default: return 0f;
            }
        }

        public static bool IsHorizontal(ArrowDirection direction)
        {
            return direction == ArrowDirection.Left || direction == ArrowDirection.Right;
        }
    }
}
