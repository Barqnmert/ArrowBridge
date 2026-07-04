using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Grows a 3D wooden-deck truss bridge one segment at a time as arrows are cleared — order
    /// and arrow size don't matter, only the count. Each segment adds a deck plank and side
    /// rails; every Nth segment raises truss posts with diagonal bracing and a top chord on both
    /// faces, sketching a classic Warren-truss silhouette. Segment width is fixed
    /// (total span / TotalSegments) so the bridge always reaches exactly from Kara A to Kara B
    /// once every arrow has been removed.
    /// </summary>
    public class BridgeBuilder : MonoBehaviour
    {
        public static BridgeBuilder Instance { get; private set; }

        [Header("Span")]
        [SerializeField] private int totalSegments = 30;
        [SerializeField] private float bridgeStartX = -6f;
        [SerializeField] private float bridgeEndX = 6f;
        [SerializeField] private float bridgeY;

        [Header("Deck")]
        [SerializeField] private float deckDepth = 1.4f;      // z extent
        [SerializeField] private float deckThickness = 0.16f;
        [SerializeField] private float deckZCenter = 0.7f;    // pushed behind the arrow plane (z=0)
        [SerializeField] private float railHeight = 0.28f;
        [SerializeField] private float railThickness = 0.06f;

        [Header("Truss")]
        [SerializeField] private int trussEveryNSegments = 3;
        [SerializeField] private float trussHeight = 1.1f;
        [SerializeField] private float beamThickness = 0.09f;

        public int SegmentsBuilt { get; private set; }
        public int TotalSegments => totalSegments;

        private float SegmentWidth => (bridgeEndX - bridgeStartX) / totalSegments;
        private float FrontFaceZ => deckZCenter - deckDepth * 0.5f;
        private float BackFaceZ => deckZCenter + deckDepth * 0.5f;

        // X of the previous truss frame, so diagonals/top chords can span between frames.
        private float previousTrussX;
        private bool hasPreviousTruss;

        private void Awake()
        {
            Instance = this;
        }

        public void Configure(float newBridgeStartX, float newBridgeEndX, float newBridgeY, int newTotalSegments)
        {
            bridgeStartX = newBridgeStartX;
            bridgeEndX = newBridgeEndX;
            bridgeY = newBridgeY;
            totalSegments = newTotalSegments;

            // Awake() does not run for components added via editor scripts outside Play mode
            // (the level builder, and edit-mode tests, both call Configure right after
            // AddComponent), so set the singleton here too — Awake still covers Play mode.
            Instance = this;
        }

        /// <summary>World position of the center of the next (not yet built) segment slot. Read-only query, does not mutate state.</summary>
        public Vector3 GetNextSegmentWorldPosition()
        {
            int index = Mathf.Min(SegmentsBuilt, totalSegments - 1);
            return GetSegmentCenter(index);
        }

        private Vector3 GetSegmentCenter(int index)
        {
            float centerX = bridgeStartX + SegmentWidth * (index + 0.5f);
            return new Vector3(centerX, bridgeY, 0f);
        }

        /// <summary>Appends the next bridge segment (and a truss frame every Nth one). Safe to call up to TotalSegments times.</summary>
        public void AddSegment()
        {
            if (SegmentsBuilt >= totalSegments) return;

            int index = SegmentsBuilt;
            Vector3 center = GetSegmentCenter(index);

            var segmentRoot = new GameObject($"BridgeSegment_{index:00}");
            segmentRoot.transform.SetParent(transform);
            segmentRoot.transform.position = center;

            // Deck plank (slight overlap hides seams between neighbors).
            Decor3DFactory.CreateBox("Plank",
                new Vector3(center.x, bridgeY - deckThickness * 0.5f, deckZCenter),
                new Vector3(SegmentWidth * 1.04f, deckThickness, deckDepth),
                GamePalette.DeckWood, segmentRoot.transform);

            // Low side rails along both deck edges.
            foreach (float z in new[] { FrontFaceZ, BackFaceZ })
            {
                Decor3DFactory.CreateBox("Rail",
                    new Vector3(center.x, bridgeY + railHeight, z),
                    new Vector3(SegmentWidth * 1.04f, railThickness, railThickness),
                    GamePalette.TrussSteel, segmentRoot.transform);
            }

            bool isTrussSegment = (index + 1) % trussEveryNSegments == 0;
            if (isTrussSegment)
            {
                BuildTrussFrame(index, center.x);
            }

            SegmentsBuilt++;
        }

        /// <summary>Vertical posts on both faces, plus diagonals and a top chord back to the previous frame (Warren-truss look).</summary>
        private void BuildTrussFrame(int index, float frameX)
        {
            var trussRoot = new GameObject($"Truss_{index:00}");
            trussRoot.transform.SetParent(transform);

            float baseY = bridgeY;
            float topY = bridgeY + trussHeight;

            foreach (float z in new[] { FrontFaceZ, BackFaceZ })
            {
                Decor3DFactory.CreateBeam("Post",
                    new Vector3(frameX, baseY, z), new Vector3(frameX, topY, z),
                    beamThickness, GamePalette.TrussSteel, trussRoot.transform);

                if (hasPreviousTruss)
                {
                    Decor3DFactory.CreateBeam("TopChord",
                        new Vector3(previousTrussX, topY, z), new Vector3(frameX, topY, z),
                        beamThickness, GamePalette.TrussSteel, trussRoot.transform);

                    Decor3DFactory.CreateBeam("Diagonal",
                        new Vector3(previousTrussX, baseY, z), new Vector3(frameX, topY, z),
                        beamThickness * 0.85f, GamePalette.TrussSteel, trussRoot.transform);
                }
            }

            // Cross-beam tying the two faces together at the top.
            Decor3DFactory.CreateBeam("CrossTie",
                new Vector3(frameX, topY, FrontFaceZ), new Vector3(frameX, topY, BackFaceZ),
                beamThickness * 0.85f, GamePalette.TrussSteel, trussRoot.transform);

            previousTrussX = frameX;
            hasPreviousTruss = true;
        }
    }
}
