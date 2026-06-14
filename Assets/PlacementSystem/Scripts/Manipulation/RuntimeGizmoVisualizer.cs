using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Draws the translate/rotate gizmo arrows using a dedicated overlay camera
    /// so the lines are ALWAYS visible regardless of camera distance, and render
    /// on top of the 3-D scene but below the UI canvas.
    ///
    /// ── Setup required in the Unity Editor ────────────────────────────────────
    /// 1. Create a new Layer called "GizmoOverlay" (e.g. Layer 31).
    /// 2. On your main scene Camera:
    ///      • Open the Culling Mask dropdown and UNCHECK "GizmoOverlay"
    ///        so the main camera doesn't draw the gizmo lines a second time.
    /// 3. On your UI Canvas:
    ///      • Make sure it renders on a higher sort order than the overlay camera
    ///        (the default "Screen Space – Overlay" canvas always appears on top anyway).
    /// The overlay camera is created at runtime by this script — you don't need to
    /// add one yourself.
    /// ──────────────────────────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(RuntimeTransformGizmo))]
    public class RuntimeGizmoVisualizer : MonoBehaviour
    {
        [SerializeField] private RuntimeTransformGizmo gizmo;

        [Tooltip("Width of gizmo lines in world units.")]
        [SerializeField] private float lineWidth = 0.04f;

        [Tooltip("Fixed screen-space length of each arrow in pixels. " +
                 "Arrows are always this size regardless of distance.")]
        [SerializeField] private float arrowScreenLength = 80f;

        [Tooltip("Name of the Layer used exclusively for gizmo objects. " +
                 "Create this layer in Edit ▸ Project Settings ▸ Tags and Layers.")]
        [SerializeField] private string gizmoLayerName = "GizmoOverlay";

        // Line renderers
        private LineRenderer axisX;
        private LineRenderer axisY;
        private LineRenderer axisZ;
        private LineRenderer rotateRing;

        // Overlay camera that draws only the GizmoOverlay layer
        private Camera overlayCam;

        // Cached layer index
        private int gizmoLayer = -1;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (gizmo == null)
                gizmo = GetComponent<RuntimeTransformGizmo>();

            gizmoLayer = LayerMask.NameToLayer(gizmoLayerName);
            if (gizmoLayer < 0)
            {
                // Fall back to default layer and warn — the developer needs to create it.
                Debug.LogWarning($"[RuntimeGizmoVisualizer] Layer \"{gizmoLayerName}\" not found. " +
                                 "Gizmo lines will be drawn on the Default layer and may be occluded. " +
                                 "Create the layer in Edit > Project Settings > Tags and Layers.");
                gizmoLayer = 0;
            }

            CreateOverlayCamera();

            axisX      = CreateLine("GizmoAxisX",    Color.red);
            axisY      = CreateLine("GizmoAxisY",    Color.green);
            axisZ      = CreateLine("GizmoAxisZ",    Color.blue);
            rotateRing = CreateLine("GizmoRotateRing", Color.yellow, loop: true);
        }

        private void LateUpdate()
        {
            if (SelectionManager.Instance == null || SelectionManager.Instance.SelectedObject == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            var origin = SelectionManager.Instance.SelectedObject.transform.position;

            // Use a fixed screen-space length so arrows are always the same size
            // regardless of how close or far the camera is.
            var size = ScreenLengthToWorldLength(origin, arrowScreenLength);

            UpdateLine(axisX, origin, origin + Vector3.right   * size);
            UpdateLine(axisY, origin, origin + Vector3.up      * size);
            UpdateLine(axisZ, origin, origin + Vector3.forward * size);
            UpdateRing(rotateRing, origin, size * 1.2f, 48);
        }

        // ── Overlay camera ─────────────────────────────────────────────────────

        private void CreateOverlayCamera()
        {
            var camGO = new GameObject("GizmoOverlayCamera");
            camGO.transform.SetParent(transform, false);

            overlayCam = camGO.AddComponent<Camera>();

            // Mirror main camera settings at runtime
            overlayCam.clearFlags      = CameraClearFlags.Depth; // don't clear colour
            overlayCam.cullingMask     = 1 << gizmoLayer;        // only gizmo layer
            overlayCam.depth           = 1;                       // render after main cam (depth 0), before UI overlay
            overlayCam.nearClipPlane   = 0.01f;                   // very close — prevents near-clip hiding
            overlayCam.farClipPlane    = 10000f;
            overlayCam.allowHDR        = false;
            overlayCam.allowMSAA       = false;
        }

        private void Update()
        {
            // Keep overlay camera in sync with the main camera every frame
            var main = Camera.main;
            if (main == null || overlayCam == null)
                return;

            var t = main.transform;
            overlayCam.transform.SetPositionAndRotation(t.position, t.rotation);
            overlayCam.fieldOfView      = main.fieldOfView;
            overlayCam.orthographic     = main.orthographic;
            overlayCam.orthographicSize = main.orthographicSize;
            overlayCam.aspect           = main.aspect;
        }

        // ── Line helpers ───────────────────────────────────────────────────────

        private LineRenderer CreateLine(string goName, Color color, bool loop = false)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.layer = gizmoLayer;

            var line = go.AddComponent<LineRenderer>();

            // "Sprites/Default" renders without depth testing by default,
            // but with the overlay camera we always win on depth anyway.
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.renderQueue = 4000; // after geometry, before UI
            line.material = mat;

            line.startColor    = color;
            line.endColor      = color;
            line.startWidth    = lineWidth;
            line.endWidth      = lineWidth;
            line.useWorldSpace = true;
            line.loop          = loop;
            line.positionCount = loop ? 49 : 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows    = false;
            line.enabled = false;
            return line;
        }

        private static void UpdateLine(LineRenderer line, Vector3 start, Vector3 end)
        {
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static void UpdateRing(LineRenderer line, Vector3 center, float radius, int segments)
        {
            line.positionCount = segments + 1;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                line.SetPosition(i, point);
            }
        }

        private void SetVisible(bool visible)
        {
            if (axisX == null) return;
            axisX.enabled      = visible;
            axisY.enabled      = visible;
            axisZ.enabled      = visible;
            rotateRing.enabled = visible && gizmo != null && gizmo.Mode == GizmoMode.Rotate;
        }

        // ── Screen-space size helper ───────────────────────────────────────────

        /// <summary>
        /// Converts a pixel length on screen into a world-space length at the
        /// position of <paramref name="worldPoint"/>, so the gizmo arrows are
        /// always the same size on screen regardless of distance.
        /// </summary>
        private float ScreenLengthToWorldLength(Vector3 worldPoint, float screenPixels)
        {
            var cam = Camera.main;
            if (cam == null)
                return 1f;

            // Project the origin and a point 1 unit to the right, measure screen distance
            var screenOrigin = cam.WorldToScreenPoint(worldPoint);
            var screenRight  = cam.WorldToScreenPoint(worldPoint + cam.transform.right);
            var screenDist   = Vector2.Distance(screenOrigin, screenRight);

            if (screenDist < 0.001f)
                return 1f;

            return screenPixels / screenDist;
        }
    }
}