using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Represents a single wire between two <see cref="EnergyConnector"/> points.
    /// The wire shape is a catenary curve rendered with a <see cref="LineRenderer"/>.
    ///
    /// Created at runtime by <see cref="WireConnectionMode"/>.
    /// Lives as a child of the first connector's PlacedObject.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class WireConnection : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("How many line segments the catenary curve is divided into. Higher = smoother.")]
        [SerializeField] private int segments = 20;

        [Tooltip("How much the wire droops in the middle. Larger = more sag.")]
        [SerializeField] private float sagFactor = 0.05f;

        [Tooltip("Width of the wire in world units.")]
        [SerializeField] private float wireWidth = 0.15f;

        [Tooltip("Color of the wire.")]
        [SerializeField] private Color wireColor = new(0.15f, 0.15f, 0.15f, 1f);

        // ── Runtime state ─────────────────────────────────────────────────────

        private EnergyConnector connectorA;
        private EnergyConnector connectorB;
        private LineRenderer line;

        // Curve points and the endpoint positions they were built for:
        // the curve is only rebuilt when one of the endpoints has moved.
        private Vector3[] points;
        private Vector3 builtStart;
        private Vector3 builtEnd;
        private bool isBuilt;

        private static Material sharedMaterial;

        public EnergyConnector ConnectorA => connectorA;
        public EnergyConnector ConnectorB => connectorB;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        private void LateUpdate()
        {
            if (connectorA == null || connectorB == null)
            {
                DestroyWire();
                return;
            }

            // Most wires never move: skip them so the LineRenderer mesh is not
            // regenerated for every wire in every frame.
            var start = connectorA.transform.position;
            var end   = connectorB.transform.position;
            if (isBuilt && start == builtStart && end == builtEnd)
                return;

            RebuildCurve();
        }

        private void OnDestroy()
        {
            // Unregister from both endpoints so they don't hold dead references
            connectorA?.UnregisterConnection(this);
            connectorB?.UnregisterConnection(this);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Initialise the wire after it has been instantiated.
        /// Must be called immediately after Instantiate.
        /// </summary>
        public void Initialize(EnergyConnector a, EnergyConnector b)
        {
            connectorA = a;
            connectorB = b;

            connectorA.RegisterConnection(this);
            connectorB.RegisterConnection(this);

            RebuildCurve();
        }

        /// <summary>Remove this wire cleanly from the scene.</summary>
        public void DestroyWire()
        {
            // OnDestroy handles unregistering; just destroy the GameObject
            if (gameObject != null)
                Destroy(gameObject);
        }

        /// <summary>Tints the whole wire (used by <see cref="WireDeleteMode"/> for highlighting).</summary>
        public void SetColor(Color color)
        {
            if (line == null)
                return;

            line.startColor = color;
            line.endColor   = color;
        }

        /// <summary>
        /// Resets the LineRenderer colors back to the wire's configured <see cref="wireColor"/>.
        /// Called by <see cref="WireDeleteMode"/> when exiting delete mode.
        /// </summary>
        public void RestoreDefaultColor()
        {
            SetColor(wireColor);
        }

        // ── Catenary curve ────────────────────────────────────────────────────

        /// <summary>
        /// Computes a simple parabolic approximation of a catenary between two 3-D points.
        /// A true catenary requires solving a transcendental equation; the parabolic
        /// approximation looks identical for normal wire sag amounts and is much cheaper.
        ///
        /// The sag is applied along the world-space "down" direction projected onto
        /// the plane perpendicular to the wire, so it always hangs naturally.
        /// </summary>
        private void RebuildCurve()
        {
            var start = connectorA.transform.position;
            var end   = connectorB.transform.position;

            var count = segments + 1;
            if (points == null || points.Length != count)
                points = new Vector3[count];

            var maxSag = Vector3.Distance(start, end) * sagFactor;

            // Sag direction: world down, but never along the wire itself
            // (prevents weird artifacts when the wire is nearly vertical)
            var wireDir = (end - start).normalized;
            var sagDir  = (Vector3.down - Vector3.Dot(Vector3.down, wireDir) * wireDir).normalized;
            if (sagDir.sqrMagnitude < 0.001f)
                sagDir = Vector3.forward; // fallback for perfectly vertical wires

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)segments;

                // Parabolic sag: f(t) = 4 * maxSag * t * (1 - t)
                // This peaks at t=0.5 and is 0 at both endpoints.
                var sag = 4f * maxSag * t * (1f - t);

                points[i] = Vector3.Lerp(start, end, t) + sagDir * sag;
            }

            line.positionCount = count;
            line.SetPositions(points);

            builtStart = start;
            builtEnd = end;
            isBuilt = true;
        }

        // ── LineRenderer setup ────────────────────────────────────────────────

        private void ConfigureLineRenderer()
        {
            if (line == null)
                return;

            // One shared white unlit material for every wire; the actual colour
            // comes from the LineRenderer vertex colours, so re-tinting a wire
            // never clones the material.
            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            line.sharedMaterial = sharedMaterial;

            line.startColor      = wireColor;
            line.endColor        = wireColor;
            line.startWidth      = wireWidth;
            line.endWidth        = wireWidth;
            line.useWorldSpace   = true;
            line.loop            = false;
            line.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows     = false;
            line.positionCount   = segments + 1;
        }
    }
}