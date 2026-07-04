using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Single source of truth for every color used in the prototype. No script should hardcode
    /// a Color literal for a game element — it should reference one of these instead, so the
    /// whole game reads as one consistent palette.
    /// </summary>
    public static class GamePalette
    {
        public static readonly Color Water = FromHex("#3B8BD4");
        public static readonly Color Land = FromHex("#8B8378");
        // Near-black per the maze reference art: line body and arrowhead share one dark ink color.
        public static readonly Color ArrowAccent = FromHex("#1B1B26");
        public static readonly Color ArrowBody = FromHex("#1B1B26");
        public static readonly Color BridgePrimary = FromHex("#1B998B");
        public static readonly Color BridgeAccentDark = FromHex("#0F6E56");
        public static readonly Color Background = FromHex("#EAF4FB");
        public static readonly Color Character = FromHex("#D85A30");
        public static readonly Color UIText = FromHex("#1A1A1A");
        public static readonly Color ProgressTrack = FromHex("#D9D9D9");
        public static readonly Color ArrowActive = FromHex("#3FA7E1");
        public static readonly Color LifeFull = FromHex("#E23B4B");
        public static readonly Color LifeEmpty = FromHex("#D9D9D9");

        // 3D world materials
        public static readonly Color DeckWood = FromHex("#9C6B3F");
        public static readonly Color TrussSteel = FromHex("#54616E");
        public static readonly Color IslandGrass = FromHex("#6DA34D");
        public static readonly Color IslandRock = FromHex("#7A6A58");

        private static Color FromHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var color))
                Debug.LogError($"GamePalette: failed to parse hex color '{hex}'.");
            return color;
        }
    }
}
