using UnityEngine;

namespace ArrowBridge
{
    /// <summary>
    /// Mobile-style camera navigation: one-finger (or left-mouse) drag pans across the level,
    /// two-finger pinch (or mouse scroll) zooms. Pan and zoom are clamped to a configured world
    /// window so the player can never lose the level off-screen. Arrow taps still work because
    /// ArrowController only treats a press as a "click" when the pointer barely moved — a real
    /// drag becomes a pan even when it starts on top of an arrow.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Pan")]
        [Tooltip("World-space rectangle the camera center is confined to.")]
        [SerializeField] private Vector2 panMin = new Vector2(-8f, -2f);
        [SerializeField] private Vector2 panMax = new Vector2(8f, 16f);

        [Header("Zoom")]
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float scrollZoomSpeed = 2f;
        [SerializeField] private float pinchZoomSpeed = 0.02f;

        private Camera cam;
        private bool isPanning;
        private Vector3 panStartPointerWorld;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void Configure(Vector2 newPanMin, Vector2 newPanMax, float newMinDistance, float newMaxDistance)
        {
            panMin = newPanMin;
            panMax = newPanMax;
            minDistance = newMinDistance;
            maxDistance = newMaxDistance;
        }

        private void Update()
        {
            if (Input.touchCount >= 2)
            {
                isPanning = false;
                HandlePinch();
                return;
            }

            HandlePan();
            HandleScrollZoom();
        }

        // ----- Pan -----

        private void HandlePan()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isPanning = true;
                panStartPointerWorld = PointerOnWorldPlane();
            }
            else if (Input.GetMouseButton(0) && isPanning)
            {
                // Keep the world point that was grabbed under the pointer: move the camera by
                // however far the pointer's world position has drifted from it.
                Vector3 drift = PointerOnWorldPlane() - panStartPointerWorld;
                MoveCameraBy(-drift);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isPanning = false;
            }
        }

        /// <summary>Pointer position projected onto the z=0 gameplay plane.</summary>
        private Vector3 PointerOnWorldPlane()
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.back, Vector3.zero); // z = 0
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : transform.position;
        }

        private void MoveCameraBy(Vector3 delta)
        {
            Vector3 target = transform.position + new Vector3(delta.x, delta.y, 0f);
            target.x = Mathf.Clamp(target.x, panMin.x, panMax.x);
            target.y = Mathf.Clamp(target.y, panMin.y, panMax.y);
            transform.position = target;
        }

        // ----- Zoom -----

        private void HandleScrollZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ZoomBy(scroll * scrollZoomSpeed);
            }
        }

        private void HandlePinch()
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);

            float currentGap = (a.position - b.position).magnitude;
            float previousGap = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
            ZoomBy((currentGap - previousGap) * pinchZoomSpeed);
        }

        /// <summary>Positive amount zooms in (camera moves toward the plane along its forward axis), clamped by distance to the z=0 plane.</summary>
        private void ZoomBy(float amount)
        {
            float distance = Mathf.Abs(transform.position.z);
            float newDistance = Mathf.Clamp(distance - amount, minDistance, maxDistance);
            Vector3 position = transform.position;
            position.z = -newDistance;
            transform.position = position;
        }
    }
}
