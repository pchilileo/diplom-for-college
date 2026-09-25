using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    public enum GizmoMode
    {
        Translate,
        Rotate,
        Scale
    }

    public enum GizmoHandle
    {
        None,
        MoveX, MoveY, MoveZ,
        /// <summary>Move in the plane whose normal is the given axis (MovePlaneY = ground plane XZ).</summary>
        MovePlaneX, MovePlaneY, MovePlaneZ,
        RotateX, RotateY, RotateZ,
        /// <summary>Outer ring: rotate around the camera view direction.</summary>
        RotateView,
        ScaleX, ScaleY, ScaleZ,
        ScaleUniform
    }

    /// <summary>
    /// Everything needed to draw and pick the gizmo in the current frame.
    /// Computed by <see cref="RuntimeTransformGizmo.RefreshFrame"/>; shared by
    /// picking and <see cref="RuntimeGizmoVisualizer"/> so that what is drawn is
    /// exactly what can be clicked.
    /// </summary>
    public sealed class GizmoFrame
    {
        public bool Visible;
        public Vector3 Origin;
        /// <summary>World length of the handles (constant size on screen).</summary>
        public float Size;
        /// <summary>World units per screen pixel at the origin.</summary>
        public float PixelSize;
        public Vector3 CameraPosition;
        public Vector3 CameraForward;
        public bool Orthographic;
        /// <summary>Unit vector from the origin towards the viewer.</summary>
        public Vector3 ToCamera;

        public readonly Vector3[] Axes = new Vector3[3];
        /// <summary>Axis handles fade out when looked at head-on (1 = fully visible).</summary>
        public readonly float[] AxisAlpha = new float[3];
        /// <summary>Plane handles fade out when seen edge-on.</summary>
        public readonly float[] PlaneAlpha = new float[3];
        /// <summary>Plane handles sit in the quadrant that faces the camera.</summary>
        public readonly float[] PlaneSign = new float[3];
        /// <summary>Scale handle length multiplier while scaling (visual feedback).</summary>
        public readonly float[] ScaleDisplay = { 1f, 1f, 1f };

        // Rotation feedback (pie slice while dragging a ring)
        public bool ShowArc;
        public Vector3 ArcAxis;
        public Vector3 ArcFrom;
        public float ArcAngle;
        public float ArcRadius;
    }

    /// <summary>Handle proportions (in units of <see cref="GizmoFrame.Size"/>), shared by picking and drawing.</summary>
    public static class GizmoGeometry
    {
        public const float ConeLength   = 0.22f;
        public const float ConeRadius   = 0.07f;
        public const float PlaneMin     = 0.14f;
        public const float PlaneMax     = 0.38f;
        public const float RingRadius   = 1.0f;
        public const float ViewRing     = 1.13f;
        public const float ScaleCube    = 0.065f;   // half size
        public const float CenterCube   = 0.09f;    // half size
        public const int   RingSegments = 64;

        /// <summary>Two unit vectors spanning the plane perpendicular to <paramref name="normal"/>.</summary>
        public static void Basis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            var reference = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(normal, reference).normalized;
            v = Vector3.Cross(normal, u);
        }

        public static Vector3 RingPoint(Vector3 center, Vector3 u, Vector3 v, float radius, int index)
        {
            var angle = index * Mathf.PI * 2f / RingSegments;
            return center + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
        }

        /// <summary>
        /// True if a point of the rotation sphere faces the camera. Only this half
        /// of each axis ring is drawn and clickable, like in the Unity editor.
        /// </summary>
        public static bool IsFrontFacing(GizmoFrame f, Vector3 point)
        {
            var toViewer = f.Orthographic ? -f.CameraForward : f.CameraPosition - point;
            return Vector3.Dot(point - f.Origin, toViewer) >= -0.001f * f.Size;
        }

        public static Vector3 PlaneCorner(GizmoFrame f, int normalAxis, float a, float b)
        {
            var i = (normalAxis + 1) % 3;
            var j = (normalAxis + 2) % 3;
            return f.Origin
                + f.Axes[i] * (f.PlaneSign[i] * a * f.Size)
                + f.Axes[j] * (f.PlaneSign[j] * b * f.Size);
        }
    }

    /// <summary>
    /// Unity-style transform gizmo for the selected object:
    ///
    ///   T — move    (axis arrows + plane squares)
    ///   R — rotate  (axis rings, outer ring = around the view direction)
    ///   Y — scale   (axis cubes, centre cube = uniform)
    ///
    /// • Handles light up under the cursor; a press only starts changing the
    ///   object after the mouse has moved a few pixels (no accidental nudges).
    /// • Hold Ctrl to snap: 0.5 m / 15° / 0.1.
    /// • Escape or right mouse button during a drag cancels it.
    /// • The camera is locked while a handle is being dragged.
    ///
    /// The gizmo sits at <see cref="PlacedObject.PivotPoint"/> (bottom centre),
    /// and rotation / scaling happen around that point.
    /// </summary>
    public class RuntimeTransformGizmo : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;

        [Tooltip("Handle length in pixels on a 1080p screen (scaled with the screen height).")]
        [SerializeField] private float handlePixels = 100f;

        [Tooltip("How close (pixels) the cursor must be to a handle to grab it.")]
        [SerializeField] private float pickTolerance = 8f;

        [Tooltip("The mouse must move this far (pixels) before a pressed handle starts dragging.")]
        [SerializeField] private float dragThreshold = 3f;

        [Header("Ctrl snapping")]
        [SerializeField] private float moveSnap = 0.5f;
        [SerializeField] private float rotateSnap = 15f;
        [SerializeField] private float scaleSnap = 0.1f;

        private PlacedObject target;
        private GizmoMode mode = GizmoMode.Translate;
        private readonly GizmoFrame frame = new();

        private GizmoHandle hovered;
        private GizmoHandle active;
        private bool pressed;
        private bool dragging;

        // Transform at the moment of the press (used for dragging and cancelling)
        private Vector2 pressMouse;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 startScale;
        private Vector3 startPivot;

        // Move
        private Vector3 moveAxis;
        private float startAxisParam;
        private Plane movePlane;
        private Vector3 startPlaneHit;

        // Rotate
        private Vector3 rotateAxis;
        private Vector2 rotateTangent;
        private float rotatePixelsPerRadian;
        private Vector2 rotateCenterScreen;
        private Vector2 rotateStartDir;
        private float lastAngle;

        // Scale
        private Vector2 scaleScreenDir;
        private float scaleScreenLength;
        private int scaleLocalAxis;
        private int scaleHandleAxis;

        public PlacedObject Target => target;
        public GizmoMode Mode => mode;
        public GizmoFrame Frame => frame;
        public GizmoHandle HoveredHandle => pressed ? GizmoHandle.None : hovered;
        public GizmoHandle ActiveHandle => pressed ? active : GizmoHandle.None;

        /// <summary>True from the press on a handle until the mouse is released.</summary>
        public bool IsDragging => pressed;

        /// <summary>Fired when the tool switches between move / rotate / scale.</summary>
        public event Action<GizmoMode> ModeChanged;

        /// <summary>
        /// World position of the gizmo: the bottom centre of the selected object
        /// (see <see cref="PlacedObject.PivotPoint"/>).
        /// </summary>
        public static Vector3 GetOrigin(PlacedObject placed)
        {
            return placed != null ? placed.PivotPoint : Vector3.zero;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }

        private void OnDisable()
        {
            EndPress();
        }

        public void Attach(PlacedObject placedObject)
        {
            EndPress();
            target = placedObject;
            hovered = GizmoHandle.None;
        }

        public void Detach()
        {
            EndPress();
            target = null;
            hovered = GizmoHandle.None;
            frame.Visible = false;
        }

        public void SetMode(GizmoMode newMode)
        {
            if (mode == newMode || pressed)
                return;

            mode = newMode;
            hovered = GizmoHandle.None;
            ModeChanged?.Invoke(mode);
        }

        private void Update()
        {
            if (target == null)
                return;

            if (pressed)
            {
                UpdateDrag();
                return;
            }

            HandleModeKeys();
            UpdateHover();
        }

        // ── Input entry point (called by SelectionManager on mouse down) ───────

        /// <summary>Starts a drag if the press hit a handle. Returns true if the click was consumed.</summary>
        public bool TryHandleClick(Vector3 screenPosition)
        {
            if (target == null || pressed || !RefreshFrame())
                return false;

            var handle = Pick(screenPosition);
            if (handle == GizmoHandle.None)
                return false;

            BeginPress(handle, screenPosition);
            return true;
        }

        // ── Frame ─────────────────────────────────────────────────────────────

        /// <summary>Recomputes <see cref="Frame"/> for the current camera and object. False if not visible.</summary>
        public bool RefreshFrame()
        {
            frame.Visible = false;
            if (target == null || sceneCamera == null)
                return false;

            var camTransform = sceneCamera.transform;
            frame.Origin = GetOrigin(target);
            frame.CameraPosition = camTransform.position;
            frame.CameraForward = camTransform.forward;
            frame.Orthographic = sceneCamera.orthographic;

            var depth = Vector3.Dot(frame.Origin - frame.CameraPosition, frame.CameraForward);
            if (!frame.Orthographic && depth <= sceneCamera.nearClipPlane)
                return false;

            var screenHeight = Mathf.Max(1, Screen.height);
            frame.PixelSize = frame.Orthographic
                ? 2f * sceneCamera.orthographicSize / screenHeight
                : 2f * depth * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / screenHeight;
            frame.Size = handlePixels * (screenHeight / 1080f) * frame.PixelSize;
            frame.ToCamera = frame.Orthographic
                ? -frame.CameraForward
                : (frame.CameraPosition - frame.Origin).normalized;

            if (mode == GizmoMode.Scale)
            {
                // Scale follows the object's heading so the handles line up with the model.
                var yaw = Quaternion.Euler(0f, target.transform.eulerAngles.y, 0f);
                frame.Axes[0] = yaw * Vector3.right;
                frame.Axes[1] = Vector3.up;
                frame.Axes[2] = yaw * Vector3.forward;
            }
            else
            {
                frame.Axes[0] = Vector3.right;
                frame.Axes[1] = Vector3.up;
                frame.Axes[2] = Vector3.forward;
            }

            for (var i = 0; i < 3; i++)
            {
                var facing = Mathf.Abs(Vector3.Dot(frame.Axes[i], frame.ToCamera));
                frame.AxisAlpha[i] = 1f - Mathf.InverseLerp(0.93f, 0.99f, facing);
                frame.PlaneAlpha[i] = Mathf.InverseLerp(0.06f, 0.2f, facing);
                frame.PlaneSign[i] = Vector3.Dot(frame.Axes[i], frame.ToCamera) >= 0f ? 1f : -1f;
            }

            frame.Visible = true;
            return true;
        }

        // ── Hover & keys ──────────────────────────────────────────────────────

        private void UpdateHover()
        {
            hovered = GizmoHandle.None;

            // No highlight while looking around with the camera or pointing at the UI.
            if (IsSecondaryHeld() || UiPointerUtility.IsPointerOverUi() || InteractionLock.ShouldBlockSelection)
                return;

            if (RefreshFrame())
                hovered = Pick(GetMousePosition());
        }

        private void HandleModeKeys()
        {
            // Letters typed into an inspector field must not switch the tool.
            if (InteractionLock.IsEditingInspector)
                return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.tKey.wasPressedThisFrame) SetMode(GizmoMode.Translate);
            else if (keyboard.rKey.wasPressedThisFrame) SetMode(GizmoMode.Rotate);
            else if (keyboard.yKey.wasPressedThisFrame) SetMode(GizmoMode.Scale);
#else
            if (Input.GetKeyDown(KeyCode.T)) SetMode(GizmoMode.Translate);
            else if (Input.GetKeyDown(KeyCode.R)) SetMode(GizmoMode.Rotate);
            else if (Input.GetKeyDown(KeyCode.Y)) SetMode(GizmoMode.Scale);
#endif
        }

        // ── Picking ───────────────────────────────────────────────────────────

        private GizmoHandle Pick(Vector2 mouse)
        {
            var best = GizmoHandle.None;
            var bestDistance = pickTolerance;

            void Consider(GizmoHandle handle, float distance)
            {
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = handle;
                }
            }

            var f = frame;
            switch (mode)
            {
                case GizmoMode.Translate:
                    for (var k = 0; k < 3; k++)
                    {
                        if (f.PlaneAlpha[k] < 0.3f)
                            continue;
                        if (InsideQuad(mouse, k))
                            Consider(GizmoHandle.MovePlaneX + k, pickTolerance * 0.5f);   // lines right next to it still win
                    }

                    for (var i = 0; i < 3; i++)
                    {
                        if (f.AxisAlpha[i] < 0.3f)
                            continue;

                        var tip = f.Origin + f.Axes[i] * (f.Size * (1f + GizmoGeometry.ConeLength));
                        Consider(GizmoHandle.MoveX + i, ScreenSegmentDistance(mouse, f.Origin, tip));
                        // The cone is fatter than the shaft.
                        var coneBase = f.Origin + f.Axes[i] * f.Size;
                        var coneRadiusPx = GizmoGeometry.ConeRadius * f.Size / f.PixelSize;
                        Consider(GizmoHandle.MoveX + i, ScreenSegmentDistance(mouse, coneBase, tip) - coneRadiusPx);
                    }
                    break;

                case GizmoMode.Rotate:
                    for (var i = 0; i < 3; i++)
                        Consider(GizmoHandle.RotateX + i, RingDistance(mouse, f.Axes[i], GizmoGeometry.RingRadius, frontOnly: true, out _));
                    Consider(GizmoHandle.RotateView, RingDistance(mouse, f.ToCamera, GizmoGeometry.ViewRing, frontOnly: false, out _));
                    break;

                case GizmoMode.Scale:
                    for (var i = 0; i < 3; i++)
                    {
                        if (f.AxisAlpha[i] < 0.3f)
                            continue;

                        var end = f.Origin + f.Axes[i] * f.Size;
                        Consider(GizmoHandle.ScaleX + i, ScreenSegmentDistance(mouse, f.Origin, end));
                        var cubePx = GizmoGeometry.ScaleCube * f.Size / f.PixelSize;
                        Consider(GizmoHandle.ScaleX + i, ScreenPointDistance(mouse, end) - cubePx);
                    }

                    // Centre cube has priority over the axis lines that start inside it.
                    var centerPx = GizmoGeometry.CenterCube * f.Size / f.PixelSize;
                    if (ScreenPointDistance(mouse, f.Origin) <= centerPx + 2f)
                    {
                        best = GizmoHandle.ScaleUniform;
                        bestDistance = -1f;
                    }
                    break;
            }

            return best;
        }

        private bool InsideQuad(Vector2 mouse, int normalAxis)
        {
            var q0 = ToScreen(GizmoGeometry.PlaneCorner(frame, normalAxis, GizmoGeometry.PlaneMin, GizmoGeometry.PlaneMin), out var ok0);
            var q1 = ToScreen(GizmoGeometry.PlaneCorner(frame, normalAxis, GizmoGeometry.PlaneMax, GizmoGeometry.PlaneMin), out var ok1);
            var q2 = ToScreen(GizmoGeometry.PlaneCorner(frame, normalAxis, GizmoGeometry.PlaneMax, GizmoGeometry.PlaneMax), out var ok2);
            var q3 = ToScreen(GizmoGeometry.PlaneCorner(frame, normalAxis, GizmoGeometry.PlaneMin, GizmoGeometry.PlaneMax), out var ok3);
            if (!(ok0 && ok1 && ok2 && ok3))
                return false;

            // Convex quad: the point is inside if it is on the same side of all edges.
            float Side(Vector2 a, Vector2 b) => (b.x - a.x) * (mouse.y - a.y) - (b.y - a.y) * (mouse.x - a.x);
            var s0 = Side(q0, q1);
            var s1 = Side(q1, q2);
            var s2 = Side(q2, q3);
            var s3 = Side(q3, q0);
            return (s0 >= 0 && s1 >= 0 && s2 >= 0 && s3 >= 0) || (s0 <= 0 && s1 <= 0 && s2 <= 0 && s3 <= 0);
        }

        /// <summary>Screen distance to a ring; <paramref name="closest"/> is the nearest ring point.</summary>
        private float RingDistance(Vector2 mouse, Vector3 normal, float radiusFactor, bool frontOnly, out Vector3 closest)
        {
            GizmoGeometry.Basis(normal, out var u, out var v);
            var radius = radiusFactor * frame.Size;

            var best = float.MaxValue;
            closest = frame.Origin;

            var prev = GizmoGeometry.RingPoint(frame.Origin, u, v, radius, 0);
            var prevVisible = !frontOnly || GizmoGeometry.IsFrontFacing(frame, prev);
            for (var i = 1; i <= GizmoGeometry.RingSegments; i++)
            {
                var point = GizmoGeometry.RingPoint(frame.Origin, u, v, radius, i);
                var visible = !frontOnly || GizmoGeometry.IsFrontFacing(frame, point);

                if (visible && prevVisible)
                {
                    var d = ScreenSegmentDistance(mouse, prev, point);
                    if (d < best)
                    {
                        best = d;
                        closest = point;
                    }
                }

                prev = point;
                prevVisible = visible;
            }

            return best;
        }

        // ── Dragging ──────────────────────────────────────────────────────────

        private void BeginPress(GizmoHandle handle, Vector2 mouse)
        {
            active = handle;
            pressed = true;
            dragging = false;
            pressMouse = mouse;

            var t = target.transform;
            startPosition = t.position;
            startRotation = t.rotation;
            startScale = t.localScale;
            startPivot = frame.Origin;

            // The camera must not fly around while a handle is held.
            InteractionLock.SetCameraLocked(true);

            switch (handle)
            {
                case GizmoHandle.MoveX:
                case GizmoHandle.MoveY:
                case GizmoHandle.MoveZ:
                    moveAxis = frame.Axes[handle - GizmoHandle.MoveX];
                    ClosestAxisParam(sceneCamera.ScreenPointToRay(mouse), startPivot, moveAxis, out startAxisParam);
                    break;

                case GizmoHandle.MovePlaneX:
                case GizmoHandle.MovePlaneY:
                case GizmoHandle.MovePlaneZ:
                    movePlane = new Plane(frame.Axes[handle - GizmoHandle.MovePlaneX], startPivot);
                    var ray = sceneCamera.ScreenPointToRay(mouse);
                    startPlaneHit = movePlane.Raycast(ray, out var enter) ? ray.GetPoint(enter) : startPivot;
                    break;

                case GizmoHandle.RotateX:
                case GizmoHandle.RotateY:
                case GizmoHandle.RotateZ:
                    BeginAxisRotation(frame.Axes[handle - GizmoHandle.RotateX], mouse);
                    break;

                case GizmoHandle.RotateView:
                    rotateAxis = frame.CameraForward;
                    rotateCenterScreen = ToScreen(startPivot, out _);
                    rotateStartDir = mouse - rotateCenterScreen;
                    RingDistance(mouse, frame.ToCamera, GizmoGeometry.ViewRing, false, out var grab);
                    StartArc((grab - startPivot).normalized, GizmoGeometry.ViewRing);
                    break;

                case GizmoHandle.ScaleX:
                case GizmoHandle.ScaleY:
                case GizmoHandle.ScaleZ:
                    scaleHandleAxis = handle - GizmoHandle.ScaleX;
                    var axis = frame.Axes[scaleHandleAxis];
                    var a = ToScreen(startPivot, out _);
                    var b = ToScreen(startPivot + axis * frame.Size, out _);
                    scaleScreenLength = Mathf.Max(20f, (b - a).magnitude);
                    scaleScreenDir = (b - a).sqrMagnitude > 1f ? (b - a).normalized : Vector2.right;
                    scaleLocalAxis = LocalAxisAlong(axis);
                    break;
            }
        }

        private void BeginAxisRotation(Vector3 axis, Vector2 mouse)
        {
            rotateAxis = axis;
            RingDistance(mouse, axis, GizmoGeometry.RingRadius, true, out var grab);

            var from = (grab - startPivot).normalized;
            var tangent = Vector3.Cross(axis, from);   // direction a positive angle moves the grabbed point

            // How far (pixels) the cursor has to travel along the ring for one radian.
            const float step = 0.05f;
            var s0 = ToScreen(grab, out _);
            var s1 = ToScreen(grab + tangent * (frame.Size * step), out _);
            var delta = s1 - s0;

            if (delta.magnitude > 1f)
            {
                rotateTangent = delta.normalized;
                rotatePixelsPerRadian = Mathf.Max(40f, delta.magnitude / step);
            }
            else
            {
                // Ring seen edge-on at the grab point: fall back to a sideways drag.
                rotateTangent = Vector2.right;
                rotatePixelsPerRadian = handlePixels * (Screen.height / 1080f);
            }

            StartArc(from, GizmoGeometry.RingRadius);
        }

        private void StartArc(Vector3 from, float radiusFactor)
        {
            frame.ShowArc = true;
            frame.ArcAxis = rotateAxis;
            frame.ArcFrom = from;
            frame.ArcAngle = 0f;
            frame.ArcRadius = radiusFactor;
            lastAngle = 0f;
        }

        private void UpdateDrag()
        {
            if (WasCancelPressed())
            {
                CancelDrag();
                return;
            }

            if (!IsPrimaryHeld())
            {
                EndPress();
                return;
            }

            var mouse = GetMousePosition();
            if (!dragging)
            {
                if ((mouse - pressMouse).magnitude < dragThreshold)
                    return;
                dragging = true;
            }

            ApplyDrag(mouse);
            target.NotifyTransformChanged();
        }

        private void ApplyDrag(Vector2 mouse)
        {
            var snapHeld = IsSnapHeld();
            var ray = sceneCamera.ScreenPointToRay(mouse);

            switch (active)
            {
                case GizmoHandle.MoveX:
                case GizmoHandle.MoveY:
                case GizmoHandle.MoveZ:
                {
                    if (!ClosestAxisParam(ray, startPivot, moveAxis, out var param))
                        return;

                    var offset = param - startAxisParam;
                    if (snapHeld)
                        offset = Snap(offset, moveSnap);
                    MoveTo(startPivot + moveAxis * offset);
                    break;
                }

                case GizmoHandle.MovePlaneX:
                case GizmoHandle.MovePlaneY:
                case GizmoHandle.MovePlaneZ:
                {
                    if (!movePlane.Raycast(ray, out var enter))
                        return;

                    var offset = ray.GetPoint(enter) - startPlaneHit;
                    // A ray almost parallel to the plane would throw the object to the horizon.
                    if (offset.magnitude > frame.Size * 200f)
                        return;

                    if (snapHeld)
                    {
                        for (var i = 0; i < 3; i++)
                            offset += frame.Axes[i] * (Snap(Vector3.Dot(offset, frame.Axes[i]), moveSnap) - Vector3.Dot(offset, frame.Axes[i]));
                    }
                    MoveTo(startPivot + offset);
                    break;
                }

                case GizmoHandle.RotateX:
                case GizmoHandle.RotateY:
                case GizmoHandle.RotateZ:
                {
                    var along = Vector2.Dot(mouse - pressMouse, rotateTangent);
                    RotateBy(along / rotatePixelsPerRadian * Mathf.Rad2Deg, snapHeld);
                    break;
                }

                case GizmoHandle.RotateView:
                {
                    var dir = mouse - rotateCenterScreen;
                    if (dir.magnitude < 4f || rotateStartDir.magnitude < 4f)
                        return;

                    // Accumulate so that going past 180° keeps turning instead of flipping.
                    var raw = Vector2.SignedAngle(rotateStartDir, dir);
                    var angle = lastAngle + Mathf.DeltaAngle(lastAngle, raw);
                    RotateBy(angle, snapHeld);
                    break;
                }

                case GizmoHandle.ScaleX:
                case GizmoHandle.ScaleY:
                case GizmoHandle.ScaleZ:
                {
                    var factor = 1f + Vector2.Dot(mouse - pressMouse, scaleScreenDir) / scaleScreenLength;
                    if (snapHeld)
                        factor = Snap(factor, scaleSnap);

                    var scale = startScale;
                    scale[scaleLocalAxis] = Mathf.Max(0.01f, startScale[scaleLocalAxis] * factor);
                    ScaleTo(scale);

                    frame.ScaleDisplay[scaleHandleAxis] = Mathf.Max(0.05f, factor);
                    break;
                }

                case GizmoHandle.ScaleUniform:
                {
                    var delta = mouse - pressMouse;
                    var factor = 1f + (delta.x + delta.y) / (handlePixels * (Screen.height / 1080f));
                    if (snapHeld)
                        factor = Snap(factor, scaleSnap);
                    factor = Mathf.Max(0.01f, factor);

                    var scale = startScale * factor;
                    for (var i = 0; i < 3; i++)
                        scale[i] = Mathf.Max(0.01f, scale[i]);
                    ScaleTo(scale);

                    for (var i = 0; i < 3; i++)
                        frame.ScaleDisplay[i] = Mathf.Max(0.05f, factor);
                    break;
                }
            }
        }

        private void MoveTo(Vector3 pivot)
        {
            if (PlacementManager.Instance != null)
                pivot = PlacementManager.Instance.SnapSettings.SnapPosition(pivot);

            target.SetPivotPosition(pivot);
        }

        private void RotateBy(float angle, bool snapHeld)
        {
            lastAngle = angle;

            if (snapHeld)
                angle = Snap(angle, rotateSnap);
            else if (PlacementManager.Instance != null)
            {
                var snap = PlacementManager.Instance.SnapSettings;
                if (snap.enableRotationSnap && snap.rotationStep > 0f)
                    angle = Snap(angle, snap.rotationStep);
            }

            // Turn around the gizmo point (bottom centre), not the model file's origin.
            var q = Quaternion.AngleAxis(angle, rotateAxis);
            target.transform.SetPositionAndRotation(startPivot + q * (startPosition - startPivot), q * startRotation);

            frame.ArcAngle = angle;
        }

        private void ScaleTo(Vector3 scale)
        {
            // Grow / shrink around the bottom centre so the object stays on the ground.
            target.transform.localScale = scale;
            target.SetPivotPosition(startPivot);
        }

        private void CancelDrag()
        {
            if (target != null)
            {
                target.transform.SetPositionAndRotation(startPosition, startRotation);
                target.transform.localScale = startScale;
                target.NotifyTransformChanged();
            }

            EndPress();
        }

        private void EndPress()
        {
            if (!pressed)
                return;

            pressed = false;
            dragging = false;
            active = GizmoHandle.None;
            frame.ShowArc = false;
            for (var i = 0; i < 3; i++)
                frame.ScaleDisplay[i] = 1f;

            InteractionLock.SetCameraLocked(false);
        }

        // ── Math helpers ──────────────────────────────────────────────────────

        /// <summary>Parameter along the axis line of the point closest to the mouse ray.</summary>
        private static bool ClosestAxisParam(Ray ray, Vector3 linePoint, Vector3 lineDir, out float param)
        {
            var w0 = linePoint - ray.origin;
            var b = Vector3.Dot(lineDir, ray.direction);
            var d = Vector3.Dot(lineDir, w0);
            var e = Vector3.Dot(ray.direction, w0);
            var denominator = 1f - b * b;   // both directions are unit length

            param = 0f;
            if (denominator < 1e-4f)
                return false;   // looking straight along the axis

            param = (b * e - d) / denominator;
            return true;
        }

        /// <summary>Index of the object's local axis that is most parallel to <paramref name="worldAxis"/>.</summary>
        private int LocalAxisAlong(Vector3 worldAxis)
        {
            var rotation = target.transform.rotation;
            var best = 0;
            var bestDot = -1f;
            for (var i = 0; i < 3; i++)
            {
                var local = Vector3.zero;
                local[i] = 1f;
                var dot = Mathf.Abs(Vector3.Dot(rotation * local, worldAxis));
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }

        private static float Snap(float value, float step)
        {
            return step > 0f ? Mathf.Round(value / step) * step : value;
        }

        private Vector2 ToScreen(Vector3 world, out bool inFront)
        {
            var p = sceneCamera.WorldToScreenPoint(world);
            inFront = p.z > 0f;
            return new Vector2(p.x, p.y);
        }

        private float ScreenPointDistance(Vector2 mouse, Vector3 world)
        {
            var p = ToScreen(world, out var ok);
            return ok ? Vector2.Distance(mouse, p) : float.MaxValue;
        }

        private float ScreenSegmentDistance(Vector2 mouse, Vector3 worldA, Vector3 worldB)
        {
            var a = ToScreen(worldA, out var okA);
            var b = ToScreen(worldB, out var okB);
            if (!okA || !okB)
                return float.MaxValue;

            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-4f)
                return Vector2.Distance(mouse, a);

            var t = Mathf.Clamp01(Vector2.Dot(mouse - a, ab) / lengthSq);
            return Vector2.Distance(mouse, a + ab * t);
        }

        // ── Input helpers ─────────────────────────────────────────────────────

        private static Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        private static bool IsPrimaryHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }

        private static bool IsSecondaryHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
#endif
        }

        private static bool IsSnapHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
        }
    }
}
