using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// A "snake" arrow: occupies a fixed bent path of cells (straight runs joined by 90° turns).
    /// Interaction is click-only. All movement — sliding out of the puzzle, routing around the
    /// outside of the board, and reeling into the bridge — happens along a single world-space
    /// polyline that the whole body flows through, so the head turns at every corner and the tail
    /// follows the exact same route behind it. The body is rendered as a dense chain of small
    /// capsule segments (several per cell) so bends stay smooth instead of kinking at corners.
    /// A blocked click slides forward to the obstacle and flows back, costing a life (ArrowBlocked).
    /// While moving, the piece tints to the "active" blue.
    /// </summary>
    public class ArrowController : MonoBehaviour
    {
        /// <summary>Raised once an arrow has fully reeled into the bridge, right before it is destroyed.</summary>
        public static event Action<ArrowController> ArrowExited;

        /// <summary>Raised when a clicked arrow turned out to be blocked and returned to rest — a wrong move that costs a life.</summary>
        public static event Action<ArrowController> ArrowBlocked;

        [Header("Visual Tuning")]
        [Tooltip("Thickness of the line body. Deliberately much thinner than a cell for the elegant maze look.")]
        [SerializeField] private float lineThickness = 0.18f;
        [SerializeField] private float headLength = 0.34f;
        [SerializeField] private float headWidthFactor = 2.6f;
        [Tooltip("Body samples per grid cell. Higher = smoother flow around corners (at the cost of more sprites).")]
        [SerializeField] private int bodySamplesPerCell = 4;
        [Tooltip("Per-cell clickable area. Kept near full cell size so thin lines stay easy to tap.")]
        [SerializeField] private float clickableSize = 0.95f;

        [Header("Movement")]
        [SerializeField] private int extensionSearchLimit = 60;
        [SerializeField] private float slideSpeedUnitsPerSecond = 14f;
        [Tooltip("How far outside the expanded board the exit corridors run. Wide enough to also clear the 3D islands flanking the bridge.")]
        [SerializeField] private float routeCornerClearance = 3.6f;
        [Tooltip("Exited pieces approach the bridge along a lane this far above deck height, then drop straight in — so no route ever passes under the bridge.")]
        [SerializeField] private float bridgeApproachHeight = 1f;
        [Tooltip("How far a fully wedged arrow (zero free cells ahead) still nudges forward to signal the failed attempt.")]
        [SerializeField] private float wedgedNudgeDistance = 0.35f;
        [Tooltip("Press counts as a tap only if the pointer moved less than this many screen pixels — larger movements are camera pans, even when they start on an arrow.")]
        [SerializeField] private float tapPixelThreshold = 24f;

        // [SerializeField] here isn't for Inspector editing — it's what makes this data survive
        // Unity's edit->play domain reload (plain private fields reset to null/default the moment
        // Play mode starts, since Configure() only ever runs once at edit time).
        [SerializeField, HideInInspector] private List<Vector2Int> pathCells = new List<Vector2Int>();
        [SerializeField, HideInInspector] private ArrowDirection exitDirection;

        public ArrowDirection ExitDirection => exitDirection;
        public IReadOnlyList<Vector2Int> PathCells => pathCells;

        private List<Vector2Int> combinedTrack;
        private bool hasExited;
        private bool isAnimating;

        // The polyline currently being flowed along, with cumulative arc lengths per point.
        private readonly List<Vector3> polylinePoints = new List<Vector3>();
        private readonly List<float> polylineArcLengths = new List<float>();
        private float PolylineTotalLength => polylineArcLengths.Count > 0 ? polylineArcLengths[polylineArcLengths.Count - 1] : 0f;

        // Dense body chain: nodes sampled every (cellSize / bodySamplesPerCell) along the body,
        // with a connector capsule between each consecutive pair.
        private readonly List<Transform> bodyNodes = new List<Transform>();
        private readonly List<Transform> connectors = new List<Transform>();
        private Transform head;
        private readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
        private SpriteRenderer headRenderer;

        private float BodyLength => (pathCells.Count - 1) * ArrowGridManager.Instance.CellSize;
        private float SampleStep => ArrowGridManager.Instance.CellSize / Mathf.Max(1, bodySamplesPerCell);

        private void Awake()
        {
            RebuildCombinedTrack();
        }

        private void Start()
        {
            // The grid's occupancy dictionary is a runtime cache, not serialized — it's empty
            // again after the domain reload, so every arrow re-registers itself once all Awakes
            // (including ArrowGridManager's own) have definitely run. Our C# piece lists don't
            // survive the reload either, so the visuals get rebuilt here too.
            if (pathCells != null && pathCells.Count > 0)
            {
                BuildVisualAndColliders();
                ArrowGridManager.Instance.RegisterArrow(this, pathCells);
            }
        }

        private void RebuildCombinedTrack()
        {
            if (pathCells == null || pathCells.Count == 0) return;
            combinedTrack = ArrowPathUtils.BuildCombinedTrack(pathCells, exitDirection, extensionSearchLimit + pathCells.Count + 2);
        }

        /// <summary>Sets up this arrow instance, builds its visuals/colliders and registers it on the grid. Called once by the level builder.</summary>
        public void Configure(IReadOnlyList<Vector2Int> newPathCells, ArrowDirection newExitDirection)
        {
            pathCells = new List<Vector2Int>(newPathCells);
            exitDirection = newExitDirection;
            hasExited = false;
            isAnimating = false;

            RebuildCombinedTrack();
            BuildVisualAndColliders();
            ArrowGridManager.Instance.RegisterArrow(this, pathCells);
        }

        // ----- Polyline plumbing -----

        private void SetPolyline(List<Vector3> points)
        {
            polylinePoints.Clear();
            polylinePoints.AddRange(points);
            polylineArcLengths.Clear();
            float accumulated = 0f;
            polylineArcLengths.Add(0f);
            for (int i = 1; i < polylinePoints.Count; i++)
            {
                accumulated += Vector3.Distance(polylinePoints[i - 1], polylinePoints[i]);
                polylineArcLengths.Add(accumulated);
            }
        }

        private Vector3 SamplePolyline(float arc)
        {
            if (polylinePoints.Count == 0) return transform.position;
            if (arc <= 0f) return polylinePoints[0];
            float total = PolylineTotalLength;
            if (arc >= total) return polylinePoints[polylinePoints.Count - 1];

            for (int i = 1; i < polylineArcLengths.Count; i++)
            {
                if (arc <= polylineArcLengths[i])
                {
                    float segmentStart = polylineArcLengths[i - 1];
                    float segmentLength = polylineArcLengths[i] - segmentStart;
                    float t = segmentLength > 0.0001f ? (arc - segmentStart) / segmentLength : 0f;
                    return Vector3.Lerp(polylinePoints[i - 1], polylinePoints[i], t);
                }
            }
            return polylinePoints[polylinePoints.Count - 1];
        }

        /// <summary>The default polyline for a resting arrow: its own path cells plus one cell of exit runway (so the head has a direction to point along).</summary>
        private List<Vector3> BuildRestPolyline(int extensionCells)
        {
            var points = new List<Vector3>(pathCells.Count + extensionCells);
            for (int i = 0; i < pathCells.Count; i++)
                points.Add(ArrowGridManager.Instance.CellToWorld(pathCells[i]));
            for (int i = 0; i < extensionCells; i++)
                points.Add(ArrowGridManager.Instance.CellToWorld(combinedTrack[pathCells.Count + i]));
            return points;
        }

        // ----- Visual construction -----

        /// <summary>Creates the dense node/connector chain and the head, then snaps them to the resting pose.</summary>
        private void BuildVisualAndColliders()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            foreach (var existingCollider in GetComponents<BoxCollider2D>())
            {
                if (Application.isPlaying) Destroy(existingCollider); else DestroyImmediate(existingCollider);
            }

            bodyNodes.Clear();
            connectors.Clear();
            bodyRenderers.Clear();

            int len = pathCells.Count;
            float step = SampleStep;
            int nodeCount = (len - 1) * Mathf.Max(1, bodySamplesPerCell) + 1;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = new GameObject($"Node_{i}");
                node.transform.SetParent(transform, false);
                var nodeRenderer = node.AddComponent<SpriteRenderer>();
                nodeRenderer.sprite = ShapeSpriteFactory.CreateCapsuleWorld(lineThickness, lineThickness, Color.white);
                nodeRenderer.color = GamePalette.ArrowBody;
                nodeRenderer.sortingOrder = 1;
                bodyNodes.Add(node.transform);
                bodyRenderers.Add(nodeRenderer);

                if (i < nodeCount - 1)
                {
                    var connector = new GameObject($"Connector_{i}");
                    connector.transform.SetParent(transform, false);
                    var connectorRenderer = connector.AddComponent<SpriteRenderer>();
                    connectorRenderer.sprite = ShapeSpriteFactory.CreateRoundedRectWorld(step, lineThickness, Color.white);
                    connectorRenderer.color = GamePalette.ArrowBody;
                    connectorRenderer.sortingOrder = 1;
                    connectors.Add(connector.transform);
                    bodyRenderers.Add(connectorRenderer);
                }
            }

            // Clickable area stays near full cell size even though the line is thin.
            for (int i = 0; i < len; i++)
            {
                Vector3 restingWorldPos = ArrowGridManager.Instance.CellToWorld(pathCells[i]);
                var pieceCollider = gameObject.AddComponent<BoxCollider2D>();
                pieceCollider.offset = new Vector2(restingWorldPos.x, restingWorldPos.y);
                pieceCollider.size = new Vector2(clickableSize, clickableSize);
            }

            var headObject = new GameObject("Head");
            headObject.transform.SetParent(transform, false);
            headRenderer = headObject.AddComponent<SpriteRenderer>();
            headRenderer.sprite = ShapeSpriteFactory.CreateTriangleWorld(headLength, lineThickness * headWidthFactor, Color.white);
            headRenderer.color = GamePalette.ArrowAccent;
            headRenderer.sortingOrder = 2;
            head = headObject.transform;

            SetPolyline(BuildRestPolyline(1));
            UpdateSlidingVisual(0f);
        }

        /// <summary>
        /// Positions the whole chain for a continuous slide distance along the active polyline.
        /// Nodes sample the polyline a fraction of a cell apart, so bends and route corners render
        /// as smooth curves rather than kinked elbows; connectors stretch/rotate to bridge nodes.
        /// </summary>
        private void UpdateSlidingVisual(float distance)
        {
            float step = SampleStep;

            for (int i = 0; i < bodyNodes.Count; i++)
            {
                bodyNodes[i].position = SamplePolyline(distance + i * step);
            }

            for (int i = 0; i < connectors.Count; i++)
            {
                Vector3 a = bodyNodes[i].position;
                Vector3 b = bodyNodes[i + 1].position;
                Vector3 span = b - a;
                float spanLength = span.magnitude;
                connectors[i].position = (a + b) * 0.5f;
                connectors[i].rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
                // Authored one sample-step long; squash when nodes bunch up at the route's end.
                connectors[i].localScale = new Vector3(spanLength > 0.0001f ? spanLength / step : 0f, 1f, 1f);
            }

            // The head's base sits exactly on the leading node (slight overlap, same ink color)
            // so there is never a visible seam between the tip and the body.
            float headArc = distance + BodyLength;
            Vector3 headBase = bodyNodes[bodyNodes.Count - 1].position;
            Vector3 ahead = SamplePolyline(headArc + 0.4f);
            Vector3 travel = ahead - headBase;
            if (travel.sqrMagnitude < 0.00001f)
            {
                Vector3 behind = SamplePolyline(Mathf.Max(0f, headArc - 0.4f));
                travel = headBase - behind;
            }
            if (travel.sqrMagnitude > 0.00001f)
            {
                Vector3 dir = travel.normalized;
                head.position = headBase;
                head.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
        }

        private void SetTint(Color bodyColor, Color accentColor)
        {
            foreach (var r in bodyRenderers) if (r != null) r.color = bodyColor;
            if (headRenderer != null) headRenderer.color = accentColor;
        }

        private void SetAlpha(float alpha)
        {
            foreach (var r in bodyRenderers)
            {
                if (r == null) continue;
                var c = r.color; c.a = alpha; r.color = c;
            }
            if (headRenderer != null)
            {
                var c = headRenderer.color; c.a = alpha; headRenderer.color = c;
            }
        }

        // ----- Interaction -----

        private Vector3 pressScreenPosition;
        private bool isPressed;

        private void OnMouseDown()
        {
            if (hasExited || isAnimating) return;
            isPressed = true;
            pressScreenPosition = Input.mousePosition;
        }

        private void OnMouseUp()
        {
            if (!isPressed) return;
            isPressed = false;
            if (hasExited || isAnimating) return;

            // Only a near-stationary press is a tap; a real drag is the camera panning and must
            // not fire the arrow it happened to start on.
            if (((Vector3)Input.mousePosition - pressScreenPosition).magnitude > tapPixelThreshold) return;

            var (canExit, cells) = EvaluateClick();
            if (canExit)
            {
                StartCoroutine(ExitRoutine(cells));
            }
            else
            {
                StartCoroutine(BlockedRoutine(cells));
            }
        }

        /// <summary>
        /// Scans forward along the combined track (own path first, then the straight extension
        /// past the head). Returns (true, cellsToFullyExit) the moment the whole piece would sit
        /// outside the margin-expanded board with nothing in the way, or (false,
        /// cellsReachableBeforeBlocked) if another arrow's cell stops it first.
        /// </summary>
        private (bool canExit, int cells) EvaluateClick()
        {
            int len = pathCells.Count;
            int reachable = 0;

            for (int k = 1; k <= extensionSearchLimit; k++)
            {
                if (k + len > combinedTrack.Count) break;

                Vector2Int enteringCell = combinedTrack[k + len - 1];
                if (ArrowGridManager.Instance.IsCellOccupied(enteringCell, this))
                {
                    return (false, reachable);
                }
                reachable = k;

                var window = ArrowPathUtils.GetWindow(combinedTrack, len, k);
                if (ArrowGridManager.Instance.IsFullyOutsideBounds(window, exitDirection))
                {
                    return (true, k);
                }
            }
            return (false, reachable);
        }

        /// <summary>
        /// Full exit: one continuous polyline from the resting shape, out past the board edge,
        /// around the outside of the puzzle, and into the bridge slot. The body flows through the
        /// whole thing head-first; the tail reels in last while the piece fades out.
        /// </summary>
        private IEnumerator ExitRoutine(int cellsToExit)
        {
            isAnimating = true;
            hasExited = true;
            SetTint(GamePalette.ArrowActive, GamePalette.ArrowActive);
            foreach (var collider in GetComponents<BoxCollider2D>()) collider.enabled = false;
            ArrowGridManager.Instance.UnregisterCells(pathCells);

            // cellsToExit is the GAME-LOGIC runway: it's how far the piece must be able to slide
            // before the tail also clears the board, and EvaluateClick verified all of it is free.
            // The VISUAL route, however, can turn off toward the bridge as soon as the HEAD leaves
            // the expanded bounds — the body just flows through the same hole behind it. Extending
            // straight for the full runway is what made long down-exiting pieces dive several
            // units below the bridge (even to the water floor) before routing back up.
            int headOutCells = cellsToExit;
            for (int j = 1; j <= cellsToExit; j++)
            {
                if (ArrowGridManager.Instance.IsCellOutsideExpandedBounds(combinedTrack[pathCells.Count - 1 + j]))
                {
                    headOutCells = j;
                    break;
                }
            }

            var points = BuildRestPolyline(headOutCells);
            AppendRouteToBridge(points);
            SetPolyline(points);

            float total = PolylineTotalLength;
            float fadeStart = Mathf.Max(0f, total - BodyLength); // head arrival — fade while the tail reels in

            float s = 0f;
            while (s < total)
            {
                s = Mathf.Min(total, s + slideSpeedUnitsPerSecond * Time.deltaTime);
                UpdateSlidingVisual(s);
                if (s > fadeStart && total > fadeStart)
                {
                    SetAlpha(1f - Mathf.Clamp01((s - fadeStart) / (total - fadeStart)));
                }
                yield return null;
            }

            ArrowExited?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Appends waypoints that carry the head from wherever it left the board to the bridge —
        /// always approaching along a lane ABOVE deck height and dropping straight down into the
        /// segment, so no route ever dips under the bridge. Pieces leaving through the top hook
        /// over to the nearest side corridor first; side and bottom exits are already clear of
        /// the box and descend at their own column before turning onto the approach lane.
        /// </summary>
        private void AppendRouteToBridge(List<Vector3> points)
        {
            Vector3 exitPoint = points[points.Count - 1];
            Vector3 bridgeTarget = BridgeBuilder.Instance != null
                ? BridgeBuilder.Instance.GetNextSegmentWorldPosition()
                : exitPoint;

            var (boundsMin, boundsMax) = ArrowGridManager.Instance.GetExpandedWorldBounds();
            float corridorLeft = boundsMin.x - routeCornerClearance;
            float corridorRight = boundsMax.x + routeCornerClearance;
            float approachY = bridgeTarget.y + bridgeApproachHeight;

            bool clearOfSides = exitPoint.x <= boundsMin.x || exitPoint.x >= boundsMax.x;
            bool clearOfBottom = exitPoint.y <= boundsMin.y;

            if (!clearOfSides && !clearOfBottom)
            {
                // Left through the top: run along the outside to the nearest corridor, then down
                // that corridor to the approach lane.
                float corridorX = (exitPoint.x - corridorLeft) < (corridorRight - exitPoint.x) ? corridorLeft : corridorRight;
                points.Add(new Vector3(corridorX, exitPoint.y, 0f));
                points.Add(new Vector3(corridorX, approachY, 0f));
            }
            else
            {
                // Side or bottom exit: drop (or rise) to the approach lane at this column first.
                if (Mathf.Abs(exitPoint.y - approachY) > 0.01f)
                {
                    points.Add(new Vector3(exitPoint.x, approachY, 0f));
                }
            }

            // Run the approach lane horizontally to above the target, then straight down into it.
            points.Add(new Vector3(bridgeTarget.x, approachY, 0f));
            points.Add(bridgeTarget);
        }

        /// <summary>Blocked attempt: flow forward to the obstacle, flow back to rest, restore color, report the failed move.</summary>
        private IEnumerator BlockedRoutine(int reachableCells)
        {
            isAnimating = true;
            SetTint(GamePalette.ArrowActive, GamePalette.ArrowActive);

            float cellSize = ArrowGridManager.Instance.CellSize;
            // One extra runway cell so even a fully wedged piece can visually nudge into the gap.
            SetPolyline(BuildRestPolyline(reachableCells + 1));
            float target = reachableCells > 0 ? reachableCells * cellSize : wedgedNudgeDistance;

            yield return SlideBetween(0f, target);
            yield return SlideBetween(target, 0f);

            SetPolyline(BuildRestPolyline(1));
            UpdateSlidingVisual(0f);
            SetTint(GamePalette.ArrowBody, GamePalette.ArrowAccent);
            isAnimating = false;
            ArrowBlocked?.Invoke(this);
        }

        private IEnumerator SlideBetween(float fromArc, float toArc)
        {
            float span = Mathf.Abs(toArc - fromArc);
            if (span < 0.0001f) yield break;

            float duration = span / slideSpeedUnitsPerSecond;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                UpdateSlidingVisual(Mathf.Lerp(fromArc, toArc, t));
                yield return null;
            }
            UpdateSlidingVisual(toArc);
        }
    }
}
