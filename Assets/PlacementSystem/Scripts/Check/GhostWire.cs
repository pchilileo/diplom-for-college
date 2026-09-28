using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Pulsing orange wire that shows a connection from the reference schema
    /// which is missing in the scene. Purely visual: it is not registered on
    /// the connectors and disappears when the check ends.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class GhostWire : MonoBehaviour
    {
        private const int Segments = 20;
        private const float SagFactor = 0.05f;
        private const float Width = 0.12f;

        private static Material sharedMaterial;

        private EnergyConnector connectorA;
        private EnergyConnector connectorB;
        private LineRenderer line;
        private Color color;
        private readonly Vector3[] points = new Vector3[Segments + 1];
        private Vector3 builtStart;
        private Vector3 builtEnd;
        private bool isBuilt;

        public static GhostWire Create(EnergyConnector a, EnergyConnector b, Color color, Transform parent)
        {
            var go = new GameObject($"GhostWire_{a.name}_{b.name}");
            go.transform.SetParent(parent, false);
            var ghost = go.AddComponent<GhostWire>();
            ghost.connectorA = a;
            ghost.connectorB = b;
            ghost.color = color;
            ghost.Rebuild();
            return ghost;
        }

        private void Awake()
        {
            line = GetComponent<LineRenderer>();

            if (sharedMaterial == null)
                sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };

            line.sharedMaterial = sharedMaterial;
            line.useWorldSpace = true;
            line.startWidth = Width;
            line.endWidth = Width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = points.Length;
        }

        private void LateUpdate()
        {
            if (connectorA == null || connectorB == null)
            {
                Destroy(gameObject);
                return;
            }

            // Objects may still be moved during the check — follow them.
            if (!isBuilt || connectorA.transform.position != builtStart || connectorB.transform.position != builtEnd)
                Rebuild();

            // Gentle pulse so missing wires stand out from real ones.
            var c = color;
            c.a = Mathf.Lerp(0.45f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            line.startColor = c;
            line.endColor = c;
        }

        private void Rebuild()
        {
            builtStart = connectorA.transform.position;
            builtEnd = connectorB.transform.position;
            WireConnection.ComputeCurve(builtStart, builtEnd, SagFactor, points);
            line.SetPositions(points);
            isBuilt = true;
        }
    }
}
