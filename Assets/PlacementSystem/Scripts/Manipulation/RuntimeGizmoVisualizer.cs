using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PlacementSystem
{
    /// <summary>
    /// Draws the translate/rotate gizmo arrows using a dedicated overlay camera
    /// so the lines are ALWAYS visible regardless of camera distance, and render
    /// on top of the 3-D scene but below the UI canvas.
    ///
    /// The overlay camera is created at runtime as a URP <i>Overlay</i> camera and
    /// pushed onto the main camera's stack; the "GizmoOverlay" layer is removed
    /// from the main camera's culling mask automatically.
    ///
    /// If the "GizmoOverlay" layer does not exist, no overlay camera is created:
    /// the lines stay on the Default layer and their ZTest-Always material still
    /// keeps them on top of the scene.
    /// ──────────────────────────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(RuntimeTransformGizmo))]
    public class RuntimeGizmoVisualizer : MonoBehaviour
    {
        [SerializeField] private RuntimeTransformGizmo gizmo;

        [Tooltip("Width of gizmo lines in world units.")]
        [SerializeField] private float lineWidth = 0.07f;

        [Tooltip("Fixed screen-space length of each arrow in pixels. " +
                 "Arrows are always this size regardless of distance.")]
        [SerializeField] private float arrowScreenLength = 110f;

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
                                 "Gizmo lines will be drawn by the main camera on the Default layer. " +
                                 "Create the layer in Edit > Project Settings > Tags and Layers.");
                gizmoLayer = 0;
            }
            else
            {
                CreateOverlayCamera();
            }

            axisX      = CreateLine("GizmoAxisX",    Color.red);
            axisY      = CreateLine("GizmoAxisY",    Color.green);
            axisZ      = CreateLine("GizmoAxisZ",    Color.blue);
            rotateRing = CreateLine("GizmoRotateRing", Color.yellow, loop: true);

        }

        private void LateUpdate()
        {
            if (SelectionManager.Instance is null || SelectionManager.Instance.SelectedObject is null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            var origin = RuntimeTransformGizmo.GetOrigin(SelectionManager.Instance.SelectedObject);

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
            var main = Camera.main;
            if (main == null)
                return;

            var camGO = new GameObject("GizmoOverlayCamera");
            camGO.transform.SetParent(transform, false);

            overlayCam = camGO.AddComponent<Camera>();
            overlayCam.cullingMask   = 1 << gizmoLayer;   // only gizmo layer
            overlayCam.nearClipPlane = 0.01f;             // very close — prevents near-clip hiding
            overlayCam.farClipPlane  = 10000f;
            overlayCam.allowHDR      = false;
            overlayCam.allowMSAA     = false;

            // In URP a plain second camera is another *Base* camera: it renders
            // the frame again from scratch with its own background, wiping out
            // the main camera's skybox. It has to be an Overlay camera stacked
            // on top of the main one instead (clearFlags are ignored by URP).
            overlayCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(overlayCam);

            // The main camera must not draw the gizmo lines a second time.
            main.cullingMask &= ~(1 << gizmoLayer);
        }

        private void Update()
        {
            // Keep overlay camera in sync with the main camera every frame
            var main = Camera.main;
            if (main is null || overlayCam is null)
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

            // Build a material that always passes the depth test (ZTest Always)
            // so the gizmo is visible through any geometry.
            // We try the GUI/Text Shader first (guaranteed ZTest Always in Unity built-ins),
            // falling back to Sprites/Default which also disables depth write by default.
            var shader = Shader.Find("GUI/Text Shader") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);

            // Force ZTest Always regardless of shader — works on Standard, URP and HDRP
            mat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4500; // well above geometry (3000) and particles, below UI (5000+)
            mat.color = color;
            line.material = mat;

            // Brighter colors — multiply original by 1.4 clamped to HDR range
            var bright = new Color(
                Mathf.Min(color.r * 1.4f, 1f),
                Mathf.Min(color.g * 1.4f, 1f),
                Mathf.Min(color.b * 1.4f, 1f),
                1f);

            line.startColor    = bright;
            line.endColor      = bright;
            line.startWidth    = lineWidth;
            line.endWidth      = lineWidth * 0.35f; // tapered toward tip — looks like an arrow shaft
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
            if (axisX is null) return;
            axisX.enabled      = visible && gizmo is not null && gizmo.Mode == GizmoMode.Translate;
            axisY.enabled      = visible && gizmo is not null && gizmo.Mode == GizmoMode.Translate;
            axisZ.enabled      = visible && gizmo is not null && gizmo.Mode == GizmoMode.Translate;
            rotateRing.enabled = visible && gizmo is not null && gizmo.Mode == GizmoMode.Rotate;
        }
        private float ScreenLengthToWorldLength(Vector3 worldPoint, float screenPixels)
        {
            var cam = Camera.main;
            if (cam is null)
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