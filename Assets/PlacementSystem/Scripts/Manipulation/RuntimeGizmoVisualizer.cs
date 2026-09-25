using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PlacementSystem
{
    /// <summary>
    /// Draws <see cref="RuntimeTransformGizmo"/> in the style of the Unity editor:
    /// arrows with cones and plane squares (move), front halves of the axis rings
    /// plus an outer view ring (rotate), cubes (scale).
    ///
    /// All handles are written into one dynamic mesh every frame. The mesh lives
    /// on the "GizmoOverlay" layer, which is rendered by a URP Overlay camera
    /// stacked on the main camera — so the gizmo is always drawn on top of the
    /// scene. If that layer does not exist the mesh is drawn by the main camera
    /// and can be hidden behind objects.
    /// </summary>
    [RequireComponent(typeof(RuntimeTransformGizmo))]
    public class RuntimeGizmoVisualizer : MonoBehaviour
    {
        [SerializeField] private RuntimeTransformGizmo gizmo;

        [Tooltip("Name of the Layer used exclusively for gizmo objects.")]
        [SerializeField] private string gizmoLayerName = "GizmoOverlay";

        [Header("Line widths (pixels)")]
        [SerializeField] private float axisWidth = 2.5f;
        [SerializeField] private float ringWidth = 2.5f;
        [SerializeField] private float highlightExtraWidth = 1.5f;

        // Unity editor handle colours
        private static readonly Color[] AxisColors =
        {
            new(0.859f, 0.243f, 0.113f),
            new(0.604f, 0.953f, 0.282f),
            new(0.227f, 0.478f, 0.972f),
        };
        private static readonly Color CenterColor    = new(0.8f, 0.8f, 0.8f, 0.93f);
        private static readonly Color HoverColor     = new(1f, 0.92f, 0.4f);
        private static readonly Color ActiveColor    = new(0.965f, 0.949f, 0.196f);
        private static readonly Color SilhouetteColor = new(0.5f, 0.5f, 0.5f, 0.45f);

        private Camera overlayCam;
        private Camera mainCam;
        private int gizmoLayer;

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private readonly GizmoMeshBuilder builder = new();

        // Draw order for the three axes (far → near)
        private readonly int[] order = { 0, 1, 2 };
        private readonly float[] orderDistance = new float[3];

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (gizmo == null)
                gizmo = GetComponent<RuntimeTransformGizmo>();

            gizmoLayer = LayerMask.NameToLayer(gizmoLayerName);
            if (gizmoLayer < 0)
            {
                Debug.LogWarning($"[RuntimeGizmoVisualizer] Layer \"{gizmoLayerName}\" not found. " +
                                 "The gizmo will be drawn by the main camera and can be hidden by objects. " +
                                 "Create the layer in Edit > Project Settings > Tags and Layers.");
                gizmoLayer = 0;
            }
            else
            {
                CreateOverlayCamera();
            }

            CreateMeshObject();
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        private void LateUpdate()
        {
            SyncOverlayCamera();

            var visible = gizmo != null && gizmo.Target != null && gizmo.RefreshFrame();
            meshRenderer.enabled = visible;
            if (!visible)
                return;

            builder.Begin(gizmo.Frame);
            switch (gizmo.Mode)
            {
                case GizmoMode.Translate: DrawTranslate(); break;
                case GizmoMode.Rotate:    DrawRotate();    break;
                case GizmoMode.Scale:     DrawScale();     break;
            }
            builder.WriteTo(mesh);
        }

        // ── Setup ──────────────────────────────────────────────────────────────

        private void CreateOverlayCamera()
        {
            var main = Camera.main;
            if (main == null)
                return;

            mainCam = main;

            // Child of the main camera: always at the same position and angle.
            var camGO = new GameObject("GizmoOverlayCamera");
            camGO.transform.SetParent(main.transform, false);

            overlayCam = camGO.AddComponent<Camera>();
            overlayCam.cullingMask   = 1 << gizmoLayer;   // only gizmo layer
            overlayCam.allowHDR      = false;
            overlayCam.allowMSAA     = false;

            // In URP a plain second camera is another *Base* camera: it renders
            // the frame again from scratch with its own background, wiping out
            // the main camera's skybox. It has to be an Overlay camera stacked
            // on top of the main one instead (clearFlags are ignored by URP).
            overlayCam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(overlayCam);

            // The main camera must not draw the gizmo a second time.
            main.cullingMask &= ~(1 << gizmoLayer);
        }

        /// <summary>
        /// The overlay camera must see the scene exactly like the main camera,
        /// otherwise the gizmo is drawn from the wrong viewpoint.
        /// </summary>
        private void SyncOverlayCamera()
        {
            if (overlayCam == null || mainCam == null)
                return;

            overlayCam.fieldOfView      = mainCam.fieldOfView;
            overlayCam.orthographic     = mainCam.orthographic;
            overlayCam.orthographicSize = mainCam.orthographicSize;
            overlayCam.nearClipPlane    = 0.01f;   // very close — handles near the camera are not clipped
            overlayCam.farClipPlane     = mainCam.farClipPlane;
        }

        private void CreateMeshObject()
        {
            var go = new GameObject("GizmoMesh");
            go.transform.SetParent(transform, false);
            go.layer = gizmoLayer;

            mesh = new Mesh { name = "GizmoMesh" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Own unlit vertex-colour shader (Resources/PlacementGizmo.shader).
            // Sprites/Default is not used: on a MeshRenderer it multiplies by
            // _RendererColor, which only SpriteRenderer sets — the mesh can come out invisible.
            var shader = Resources.Load<Shader>("PlacementGizmo");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[RuntimeGizmoVisualizer] Shader Resources/PlacementGizmo is missing or unsupported.");
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader) { renderQueue = 4000 };
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
        }

        // ── Move ──────────────────────────────────────────────────────────────

        private void DrawTranslate()
        {
            var f = gizmo.Frame;
            var dimOthers = gizmo.ActiveHandle != GizmoHandle.None;

            // Plane squares first: they are translucent and sit behind the arrows.
            for (var k = 0; k < 3; k++)
            {
                var alpha = f.PlaneAlpha[k];
                if (alpha < 0.02f)
                    continue;

                var handle = GizmoHandle.MovePlaneX + k;
                var color = HandleColor(handle, AxisColors[k], dimOthers);

                var c0 = GizmoGeometry.PlaneCorner(f, k, GizmoGeometry.PlaneMin, GizmoGeometry.PlaneMin);
                var c1 = GizmoGeometry.PlaneCorner(f, k, GizmoGeometry.PlaneMax, GizmoGeometry.PlaneMin);
                var c2 = GizmoGeometry.PlaneCorner(f, k, GizmoGeometry.PlaneMax, GizmoGeometry.PlaneMax);
                var c3 = GizmoGeometry.PlaneCorner(f, k, GizmoGeometry.PlaneMin, GizmoGeometry.PlaneMax);

                var highlighted = IsHighlighted(handle);
                builder.Quad(c0, c1, c2, c3, WithAlpha(color, (highlighted ? 0.45f : 0.22f) * alpha));
                var outline = WithAlpha(color, 0.9f * alpha);
                builder.Line(c0, c1, 1.5f, outline);
                builder.Line(c1, c2, 1.5f, outline);
                builder.Line(c2, c3, 1.5f, outline);
                builder.Line(c3, c0, 1.5f, outline);
            }

            foreach (var i in SortedAxes(f, 1f))
            {
                var alpha = f.AxisAlpha[i];
                if (alpha < 0.02f)
                    continue;

                var handle = GizmoHandle.MoveX + i;
                var color = WithAlpha(HandleColor(handle, AxisColors[i], dimOthers), alpha);
                var width = axisWidth + (IsHighlighted(handle) ? highlightExtraWidth : 0f);

                var axis = f.Axes[i];
                var coneBase = f.Origin + axis * f.Size;
                builder.Line(f.Origin, coneBase, width, color);
                builder.Cone(coneBase, axis, GizmoGeometry.ConeLength * f.Size, GizmoGeometry.ConeRadius * f.Size, color);
            }
        }

        // ── Rotate ────────────────────────────────────────────────────────────

        private void DrawRotate()
        {
            var f = gizmo.Frame;
            var active = gizmo.ActiveHandle;
            var dimOthers = active != GizmoHandle.None;

            // Silhouette of the rotation sphere
            builder.Ring(f.Origin, f.ToCamera, GizmoGeometry.RingRadius * f.Size, 1.5f, SilhouetteColor, frontOnly: false);

            // Swept angle while dragging
            if (f.ShowArc && Mathf.Abs(f.ArcAngle) > 0.01f)
            {
                var color = active == GizmoHandle.RotateView ? CenterColor : AxisColors[Mathf.Clamp(active - GizmoHandle.RotateX, 0, 2)];
                var radius = f.ArcRadius * f.Size;
                builder.Sector(f.Origin, f.ArcAxis, f.ArcFrom, f.ArcAngle, radius, WithAlpha(color, 0.25f));
                var end = Quaternion.AngleAxis(f.ArcAngle, f.ArcAxis) * f.ArcFrom;
                builder.Line(f.Origin, f.Origin + f.ArcFrom * radius, 1.5f, WithAlpha(color, 0.8f));
                builder.Line(f.Origin, f.Origin + end * radius, 1.5f, WithAlpha(color, 0.8f));
            }

            for (var i = 0; i < 3; i++)
            {
                var handle = GizmoHandle.RotateX + i;
                var color = HandleColor(handle, AxisColors[i], dimOthers);
                var width = ringWidth + (IsHighlighted(handle) ? highlightExtraWidth : 0f);
                builder.Ring(f.Origin, f.Axes[i], GizmoGeometry.RingRadius * f.Size, width, color, frontOnly: true);
            }

            var viewColor = HandleColor(GizmoHandle.RotateView, CenterColor, dimOthers);
            var viewWidth = ringWidth + (IsHighlighted(GizmoHandle.RotateView) ? highlightExtraWidth : 0f);
            builder.Ring(f.Origin, f.ToCamera, GizmoGeometry.ViewRing * f.Size, viewWidth, viewColor, frontOnly: false);
        }

        // ── Scale ─────────────────────────────────────────────────────────────

        private void DrawScale()
        {
            var f = gizmo.Frame;
            var dimOthers = gizmo.ActiveHandle != GizmoHandle.None;

            foreach (var i in SortedAxes(f, 1f))
            {
                var alpha = f.AxisAlpha[i];
                if (alpha < 0.02f)
                    continue;

                var handle = GizmoHandle.ScaleX + i;
                var color = WithAlpha(HandleColor(handle, AxisColors[i], dimOthers), alpha);
                var width = axisWidth + (IsHighlighted(handle) ? highlightExtraWidth : 0f);

                // The handle stretches while dragging, like in Unity.
                var end = f.Origin + f.Axes[i] * (f.Size * f.ScaleDisplay[i]);
                builder.Line(f.Origin, end, width, color);
                builder.Box(end, f.Axes, GizmoGeometry.ScaleCube * f.Size, color);
            }

            var centerColor = HandleColor(GizmoHandle.ScaleUniform, CenterColor, dimOthers);
            builder.Box(f.Origin, f.Axes, GizmoGeometry.CenterCube * f.Size, centerColor);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool IsHighlighted(GizmoHandle handle)
        {
            return gizmo.ActiveHandle == handle || gizmo.HoveredHandle == handle;
        }

        private Color HandleColor(GizmoHandle handle, Color baseColor, bool dimOthers)
        {
            if (gizmo.ActiveHandle == handle)
                return ActiveColor;
            if (gizmo.HoveredHandle == handle)
                return HoverColor;
            return dimOthers ? WithAlpha(baseColor, baseColor.a * 0.35f) : baseColor;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>Axis indices ordered far → near, so nearer handles are drawn on top.</summary>
        private int[] SortedAxes(GizmoFrame f, float lengthFactor)
        {
            for (var i = 0; i < 3; i++)
            {
                order[i] = i;
                var tip = f.Origin + f.Axes[i] * (f.Size * lengthFactor);
                orderDistance[i] = f.Orthographic
                    ? Vector3.Dot(tip, f.CameraForward)
                    : (tip - f.CameraPosition).sqrMagnitude;
            }

            // Three elements: a tiny insertion sort, no allocations.
            for (var i = 1; i < 3; i++)
            {
                for (var j = i; j > 0 && orderDistance[order[j]] > orderDistance[order[j - 1]]; j--)
                    (order[j], order[j - 1]) = (order[j - 1], order[j]);
            }
            return order;
        }
    }

    /// <summary>
    /// Collects coloured triangles for the gizmo. Lines are camera-facing strips
    /// with a width in pixels; solids only emit faces that look at the camera,
    /// which is enough for convex shapes without a depth buffer.
    /// </summary>
    internal sealed class GizmoMeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Color> colors = new();
        private readonly List<int> triangles = new();
        private GizmoFrame frame;

        public void Begin(GizmoFrame f)
        {
            frame = f;
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
        }

        public void WriteTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, calculateBounds: false);
            // The gizmo must never be frustum-culled.
            mesh.bounds = new Bounds(frame.Origin, Vector3.one * (frame.Size * 10f));
        }

        private Vector3 ToViewer(Vector3 point)
        {
            return frame.Orthographic ? -frame.CameraForward : (frame.CameraPosition - point).normalized;
        }

        /// <summary>Simple shading so solids read as 3D: faces turned away from the viewer are darker.</summary>
        private Color Shade(Color color, Vector3 normal, Vector3 point)
        {
            var light = 0.6f + 0.4f * Mathf.Clamp01(Vector3.Dot(normal, ToViewer(point)));
            return new Color(color.r * light, color.g * light, color.b * light, color.a);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            var start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            // Culling is off in the sprite shader, so winding does not matter.
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Triangle(a, b, c, color);
            Triangle(a, c, d, color);
        }

        public void Line(Vector3 a, Vector3 b, float widthPixels, Color color)
        {
            var direction = b - a;
            if (direction.sqrMagnitude < 1e-10f)
                return;

            var side = Vector3.Cross(direction, ToViewer((a + b) * 0.5f));
            if (side.sqrMagnitude < 1e-10f)
                return;   // seen exactly end-on

            side = side.normalized * (widthPixels * frame.PixelSize * 0.5f);
            Quad(a - side, b - side, b + side, a + side, color);
        }

        public void Ring(Vector3 center, Vector3 normal, float radius, float widthPixels, Color color, bool frontOnly)
        {
            GizmoGeometry.Basis(normal, out var u, out var v);

            var prev = GizmoGeometry.RingPoint(center, u, v, radius, 0);
            var prevVisible = !frontOnly || GizmoGeometry.IsFrontFacing(frame, prev);
            for (var i = 1; i <= GizmoGeometry.RingSegments; i++)
            {
                var point = GizmoGeometry.RingPoint(center, u, v, radius, i);
                var visible = !frontOnly || GizmoGeometry.IsFrontFacing(frame, point);
                if (visible && prevVisible)
                {
                    // Slightly overlap segments so there are no gaps at the joints.
                    var overlap = (point - prev) * 0.08f;
                    Line(prev - overlap, point + overlap, widthPixels, color);
                }

                prev = point;
                prevVisible = visible;
            }
        }

        /// <summary>Filled pie slice from <paramref name="from"/> turning by <paramref name="angle"/> degrees.</summary>
        public void Sector(Vector3 center, Vector3 axis, Vector3 from, float angle, float radius, Color color)
        {
            var steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(angle) / 4f), 1, 180);
            var prev = center + from * radius;
            for (var i = 1; i <= steps; i++)
            {
                var point = center + Quaternion.AngleAxis(angle * i / steps, axis) * from * radius;
                Triangle(center, prev, point, color);
                prev = point;
            }
        }

        public void Cone(Vector3 baseCenter, Vector3 direction, float length, float radius, Color color)
        {
            const int segments = 16;
            var tip = baseCenter + direction * length;
            GizmoGeometry.Basis(direction, out var u, out var v);

            // Base cap (only if it faces the viewer)
            if (Vector3.Dot(-direction, ToViewer(baseCenter)) > 0f)
            {
                var capColor = Shade(color, -direction, baseCenter);
                for (var i = 0; i < segments; i++)
                    Triangle(baseCenter, Rim(i), Rim(i + 1), capColor);
            }

            for (var i = 0; i < segments; i++)
            {
                var a = Rim(i);
                var b = Rim(i + 1);
                var mid = ((a + b) * 0.5f - baseCenter).normalized;
                var normal = (mid * length + direction * radius).normalized;
                var faceCenter = (a + b + tip) / 3f;
                if (Vector3.Dot(normal, ToViewer(faceCenter)) < 0f)
                    continue;   // back face
                Triangle(a, b, tip, Shade(color, normal, faceCenter));
            }

            Vector3 Rim(int i)
            {
                var angle = i * Mathf.PI * 2f / segments;
                return baseCenter + (u * Mathf.Cos(angle) + v * Mathf.Sin(angle)) * radius;
            }
        }

        public void Box(Vector3 center, Vector3[] axes, float half, Color color)
        {
            for (var i = 0; i < 3; i++)
            {
                var n = axes[i];
                var a = axes[(i + 1) % 3] * half;
                var b = axes[(i + 2) % 3] * half;

                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var normal = n * sign;
                    var faceCenter = center + normal * half;
                    if (Vector3.Dot(normal, ToViewer(faceCenter)) <= 0f)
                        continue;   // back face

                    Quad(faceCenter - a - b, faceCenter + a - b, faceCenter + a + b, faceCenter - a + b,
                        Shade(color, normal, faceCenter));
                }
            }
        }
    }
}
