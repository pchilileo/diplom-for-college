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
        [SerializeField] private float wireWidth = 0.03f;

        [Tooltip("Color of the wire.")]
        [SerializeField] private Color wireColor = new(0.15f, 0.15f, 0.15f, 1f);

        // ── Runtime state ─────────────────────────────────────────────────────

        private EnergyConnector connectorA;
        private EnergyConnector connectorB;
        private LineRenderer line;

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
            // Rebuild the curve every frame so it tracks moving objects
            if (connectorA == null || connectorB == null)
            {
                DestroyWire();
                return;
            }

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

        /// <summary>
        /// Resets the LineRenderer colors back to the wire's configured <see cref="wireColor"/>.
        /// Called by <see cref="WireDeleteMode"/> when exiting delete mode.
        /// </summary>
        public void RestoreDefaultColor()
        {
            if (line == null)
                return;

            line.startColor = wireColor;
            line.endColor   = wireColor;

            if (line.material != null)
                line.material.color = wireColor;
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

            line.positionCount = segments + 1;

            var maxSag = Vector3.Distance(start, end) * sagFactor;

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;

                // Linear interpolation along the chord
                var point = Vector3.Lerp(start, end, t);

                // Parabolic sag: f(t) = 4 * maxSag * t * (1 - t)
                // This peaks at t=0.5 and is 0 at both endpoints.
                var sag = 4f * maxSag * t * (1f - t);

                // Sag direction: world down, but never along the wire itself
                var wireDir = (end - start).normalized;
                var sagDir  = Vector3.down;

                // Remove any component of sagDir that is parallel to the wire
                // (prevents weird artifacts when the wire is nearly vertical)
                sagDir = (sagDir - Vector3.Dot(sagDir, wireDir) * wireDir).normalized;

                if (sagDir.sqrMagnitude < 0.001f)
                    sagDir = Vector3.forward; // fallback for perfectly vertical wires

                point += sagDir * sag;
                line.SetPosition(i, point);
            }
        }

        // ── LineRenderer setup ────────────────────────────────────────────────

        private void ConfigureLineRenderer()
        {
            if (line == null)
                return;

            // Use a simple unlit material so the wire is always visible
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = wireColor;
            line.material = mat;

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