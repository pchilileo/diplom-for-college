using System.Collections.Generic;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Manages the wire-connection editing mode.
    ///
    /// ── How it works ─────────────────────────────────────────────────────────
    /// • Press <b>2</b> to toggle Wire Connection Mode on/off.
    /// • All EnergyConnector points in the scene light up (cyan).
    /// • Click a connector → it turns green (first endpoint selected).
    /// • Click another connector → a wire is created between them.
    /// • <b>Escape</b> drops the first endpoint; with nothing picked it leaves
    ///   the mode (handled by <see cref="EditorModeManager"/>).
    ///
    /// ── Setup in the Unity Editor ─────────────────────────────────────────────
    /// 1. Add this component to any persistent Manager GameObject.
    /// 2. Assign <c>wirePrefab</c> — a Prefab with a <see cref="WireConnection"/>
    ///    (and LineRenderer) on its root.  A plain empty GameObject works too;
    ///    WireConnection adds a LineRenderer via RequireComponent.
    /// 3. Assign <c>sceneCamera</c> (or leave null to use Camera.main).
    /// 4. Optionally tune <c>hoverTolerancePx</c> (extra pixels around a marker).
    ///    Turn on <c>debugDrawPickAreas</c> to see the clickable areas while tuning.
    ///
    /// ── Picking ───────────────────────────────────────────────────────────────
    /// A point is under the cursor when the cursor is on its marker (the
    /// marker's real size on screen) or within a few pixels of it. Points hidden
    /// behind equipment can't be picked. When markers overlap on screen, the one
    /// nearer to the camera wins.
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public class WireConnectionMode : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Prefab that has WireConnection (+ LineRenderer) on its root.")]
        [SerializeField] private WireConnection wirePrefab;

        [Tooltip("Scene camera used for screen-space picking. Defaults to Camera.main.")]
        [SerializeField] private Camera sceneCamera;

        [Tooltip("Extra pixels around a marker that still count as pointing at it (at 1080p, scaled with the screen). " +
                 "The marker itself is always hit exactly.")]
        [SerializeField, Range(0f, 20f)] private float hoverTolerancePx = 3f;

        [Tooltip("Smallest clickable radius in pixels, so far-away markers stay reachable (at 1080p).")]
        [SerializeField, Range(1f, 20f)] private float minRadiusPx = 4f;

        [Tooltip("Draws the clickable circle of every point in the Game view (for tuning the values above).")]
        [SerializeField] private bool debugDrawPickAreas;

        [Tooltip("Terminals often sit slightly inside their own model. Geometry of the same object closer " +
                 "than this (metres) in front of the point does not hide it.")]
        [SerializeField] private float ownModelAllowance = 0.25f;

        [Tooltip("Layers that can hide connection points (equipment, ground).")]
        [SerializeField] private LayerMask occluderMask = Physics.DefaultRaycastLayers;

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool isActive;
        private EnergyConnector firstConnector;

        // All connectors found in the scene (refreshed each time mode activates)
        private readonly List<EnergyConnector> allConnectors = new();

        // Currently hovered connector (for hover highlight)
        private EnergyConnector hoveredConnector;

        // Reused buffer for the visibility raycasts
        private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }

        private void Update()
        {
            if (!isActive)
                return;

            HandleHover();
            HandleClick();
        }

        // ── Mode toggle ───────────────────────────────────────────────────────

        // All mode switching (keys 1/2/3, Escape) is owned by EditorModeManager.

        /// <summary>Activate wire-connection mode. Called by <see cref="EditorModeManager"/>.</summary>
        public void ForceActivate()
        {
            if (!isActive)
                Activate();
        }

        /// <summary>Deactivate wire-connection mode. Called by <see cref="EditorModeManager"/>.</summary>
        public void ForceDeactivate()
        {
            if (isActive)
                Deactivate();
        }

        /// <summary>
        /// Drops the half-built wire, if any. Returns false when there was
        /// nothing to cancel, so the caller can leave the mode instead.
        /// </summary>
        public bool TryCancelPendingWire()
        {
            if (!isActive || firstConnector == null)
                return false;

            firstConnector.SetHighlight(EnergyConnector.HighlightState.Available);
            firstConnector = null;
            return true;
        }

        private void Activate()
        {
            isActive = true;
            firstConnector = null;

            // Block selection and gizmo while in wire mode
            InteractionLock.SetWiringMode(true);

            // Deselect any currently selected object so the gizmo disappears
            SelectionManager.Instance?.Deselect();

            // Find all connectors, enable their GameObjects, then highlight them
            RefreshConnectorList();
            SetAllVisible(true);
            SetAllHighlights(EnergyConnector.HighlightState.Available);

            // Equipment may still be placed or deleted while wiring.
            if (PlacementManager.Instance != null)
            {
                PlacementManager.Instance.ObjectSpawned += OnObjectSpawned;
                PlacementManager.Instance.ObjectRemoved += OnObjectRemoved;
            }
        }

        private void Deactivate()
        {
            isActive = false;

            if (PlacementManager.Instance != null)
            {
                PlacementManager.Instance.ObjectSpawned -= OnObjectSpawned;
                PlacementManager.Instance.ObjectRemoved -= OnObjectRemoved;
            }

            // Reset highlights before hiding so renderers end up in idle state
            SetAllHighlights(EnergyConnector.HighlightState.Idle);
            SetAllVisible(false);

            firstConnector   = null;
            hoveredConnector = null;

            InteractionLock.SetWiringMode(false);
        }

        // ── Hover ─────────────────────────────────────────────────────────────

        private void HandleHover()
        {
            var mousePos = InputUtility.MousePosition;
            var hit = PickConnector(mousePos);

            if (hit == hoveredConnector)
                return;

            // Restore previous hover
            if (hoveredConnector != null && hoveredConnector != firstConnector)
                hoveredConnector.SetHighlight(EnergyConnector.HighlightState.Available);

            hoveredConnector = hit;

            // Apply hover highlight (unless it's the already-selected first connector)
            if (hoveredConnector != null && hoveredConnector != firstConnector)
                hoveredConnector.SetHighlight(EnergyConnector.HighlightState.Hover);
        }

        // ── Click ─────────────────────────────────────────────────────────────

        private void HandleClick()
        {
            if (!InputUtility.WasLeftClickPressed || InteractionLock.AreClicksSuppressed)
                return;

            if (UiPointerUtility.IsPointerOverUi())
                return;

            var mousePos = InputUtility.MousePosition;
            var hit = PickConnector(mousePos);

            if (hit == null)
            {
                // Clicked empty space — cancel first selection
                if (firstConnector != null)
                {
                    firstConnector.SetHighlight(EnergyConnector.HighlightState.Available);
                    firstConnector = null;
                }
                return;
            }

            if (firstConnector == null)
            {
                // ── First endpoint ─────────────────────────────────────────
                firstConnector = hit;
                firstConnector.SetHighlight(EnergyConnector.HighlightState.Selected);
            }
            else
            {
                // ── Second endpoint ────────────────────────────────────────
                if (hit == firstConnector)
                {
                    // Same connector clicked — deselect
                    firstConnector.SetHighlight(EnergyConnector.HighlightState.Available);
                    firstConnector = null;
                    return;
                }

                if (firstConnector.IsConnectedTo(hit))
                    EditorNotifications.Post(Loc.Get("MSG_WIRE_ALREADY_CONNECTED"));
                else
                    CreateWire(firstConnector, hit);

                // Reset for next wire
                firstConnector.SetHighlight(EnergyConnector.HighlightState.Available);
                firstConnector = null;
                hoveredConnector = null;

                // Refresh so the new wire's connector is still highlighted
                SetAllHighlights(EnergyConnector.HighlightState.Available);
            }
        }

        // ── Wire creation ─────────────────────────────────────────────────────

        /// <summary>
        /// Connects two points with a wire (also used when a saved project is
        /// loaded). Returns null if the points are the same or already connected.
        /// </summary>
        public WireConnection Connect(EnergyConnector a, EnergyConnector b)
        {
            if (a == null || b == null || a == b || a.IsConnectedTo(b))
                return null;

            return CreateWire(a, b);
        }

        private WireConnection CreateWire(EnergyConnector a, EnergyConnector b)
        {
            // The wire lives as a child of connector A's PlacedObject so it
            // moves with it and is included in its hierarchy (e.g. for saving).
            Transform parent = a.Owner != null ? a.Owner.transform : transform;

            WireConnection wire;
            if (wirePrefab != null)
            {
                wire = Instantiate(wirePrefab, parent);
            }
            else
            {
                // Fallback: create a minimal GameObject with WireConnection
                var go = new GameObject("Wire");
                go.transform.SetParent(parent, false);
                wire = go.AddComponent<WireConnection>();
            }

            wire.name = $"Wire_{a.name}_{b.name}";
            wire.Initialize(a, b);

            // Notify both PlacedObjects that their connector data changed
            a.Owner?.NotifyConnectionsChanged();
            b.Owner?.NotifyConnectionsChanged();

            return wire;
        }

        /// <summary>A new object's connection points join the mode immediately.</summary>
        private void OnObjectSpawned(PlacedObject placed)
        {
            foreach (var connector in placed.Connectors)
            {
                if (connector == null || allConnectors.Contains(connector))
                    continue;

                allConnectors.Add(connector);
                connector.SetVisible(true);
                connector.SetHighlight(EnergyConnector.HighlightState.Available);
            }
        }

        /// <summary>Forget the points of a deleted object (and a wire started from it).</summary>
        private void OnObjectRemoved(PlacedObject placed)
        {
            foreach (var connector in placed.Connectors)
            {
                allConnectors.Remove(connector);
                if (connector == firstConnector)
                    firstConnector = null;
                if (connector == hoveredConnector)
                    hoveredConnector = null;
            }
        }

        // ── Connector management ──────────────────────────────────────────────

        /// <summary>
        /// Finds every EnergyConnector currently in the scene.
        /// Called once when the mode activates.
        /// </summary>
        private void RefreshConnectorList()
        {
            allConnectors.Clear();
            var found = FindObjectsByType<EnergyConnector>();
            allConnectors.AddRange(found);
        }

        private void SetAllHighlights(EnergyConnector.HighlightState state)
        {
            allConnectors.RemoveAll(c => c == null);

            foreach (var connector in allConnectors)
                connector.SetHighlight(state);
        }

        /// <summary>
        /// Shows or hides the marker of every known connector.
        /// Connectors are hidden by default and only shown while the mode is active.
        /// </summary>
        private void SetAllVisible(bool visible)
        {
            allConnectors.RemoveAll(c => c == null);

            foreach (var connector in allConnectors)
                connector.SetVisible(visible);
        }

        // ── Connector picking ─────────────────────────────────────────────────

        /// <summary>
        /// Returns the connection point under the cursor, or null.
        ///
        /// For every point the distance from the cursor to the <i>edge</i> of its
        /// marker on screen is measured (0 = the cursor is on the marker). Points
        /// within the tolerance are candidates; hidden ones are dropped; the
        /// closest wins, and among markers the cursor is on, the nearest to the camera.
        /// </summary>
        private EnergyConnector PickConnector(Vector2 screenPos)
        {
            if (sceneCamera == null || UiPointerUtility.IsPointerOverUi())
                return null;

            var ray = sceneCamera.ScreenPointToRay(screenPos);
            var tolerance = hoverTolerancePx * ScreenScale;

            EnergyConnector best = null;
            var bestEdge = float.MaxValue;
            var bestDepth = float.MaxValue;

            foreach (var connector in allConnectors)
            {
                if (!TryGetScreenCircle(connector, out var center, out var radius, out var depth))
                    continue;

                float edge;
                if (connector.RaycastMarker(ray, sceneCamera.farClipPlane, out _))
                {
                    edge = 0f;   // the cursor is exactly over the marker
                }
                else
                {
                    edge = Mathf.Max(0f, Vector2.Distance(screenPos, center) - radius);
                    if (edge > tolerance)
                        continue;
                }

                // Better = closer to the cursor; when both are under the cursor
                // (or practically tied), the one nearer to the camera.
                var better = edge < bestEdge - 0.5f || (edge <= bestEdge + 0.5f && depth < bestDepth);
                if (!better)
                    continue;

                // Only now the (more expensive) visibility test
                if (!IsVisible(connector))
                    continue;

                best = connector;
                bestEdge = edge;
                bestDepth = depth;
            }

            return best;
        }

        private static float ScreenScale => Screen.height / 1080f;

        /// <summary>Where the marker is on screen and how big (radius in pixels).</summary>
        private bool TryGetScreenCircle(EnergyConnector connector, out Vector2 center, out float radius, out float depth)
        {
            center = default;
            radius = 0f;
            depth = 0f;
            if (connector == null)
                return false;

            var screenPoint = sceneCamera.WorldToScreenPoint(connector.transform.position);
            depth = screenPoint.z;
            if (depth <= sceneCamera.nearClipPlane)
                return false;   // behind the camera

            var pixelSize = sceneCamera.orthographic
                ? 2f * sceneCamera.orthographicSize / Screen.height
                : 2f * depth * Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;

            center = new Vector2(screenPoint.x, screenPoint.y);
            radius = Mathf.Max(minRadiusPx * ScreenScale, connector.WorldRadius / pixelSize);
            return true;
        }

        // ── Debug: clickable areas ────────────────────────────────────────────
        // Only in the editor and development builds: a defined OnGUI costs a
        // little every frame even when it draws nothing.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

        private static Texture2D ringTexture;

        private void OnGUI()
        {
            if (!debugDrawPickAreas || !isActive || sceneCamera == null || Event.current.type != EventType.Repaint)
                return;

            if (ringTexture == null)
                ringTexture = CreateRingTexture(64, 2);

            var tolerance = hoverTolerancePx * ScreenScale;
            foreach (var connector in allConnectors)
            {
                if (!TryGetScreenCircle(connector, out var center, out var radius, out _))
                    continue;

                // Inner circle — the marker size; outer — marker + tolerance.
                // GUI coordinates have Y pointing down.
                var guiCenter = new Vector2(center.x, Screen.height - center.y);
                GUI.color = new Color(0.3f, 1f, 1f, 0.9f);
                DrawRing(guiCenter, radius);
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.6f);
                DrawRing(guiCenter, radius + tolerance);
            }
            GUI.color = Color.white;
        }

        private static void DrawRing(Vector2 center, float radius)
        {
            var size = radius * 2f;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, size, size), ringTexture);
        }

        private static Texture2D CreateRingTexture(int size, int thickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var c = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    var a = Mathf.Clamp01(1f - Mathf.Abs(d - (c - thickness)) / thickness);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            texture.Apply();
            return texture;
        }
#endif

        /// <summary>
        /// False if scene geometry is between the camera and the point. Its own
        /// marker never counts, and its own model counts only if it is noticeably
        /// in front (terminals are often slightly embedded in the model).
        /// </summary>
        private bool IsVisible(EnergyConnector connector)
        {
            var target = connector.transform.position;

            // Ray from the camera's near plane through the point (works for perspective and orthographic)
            var ray = sceneCamera.ScreenPointToRay(sceneCamera.WorldToScreenPoint(target));
            var distance = Vector3.Distance(ray.origin, target);
            if (distance < 1e-3f)
                return true;

            var count = Physics.RaycastNonAlloc(ray.origin, (target - ray.origin) / distance, hitBuffer, distance,
                occluderMask, QueryTriggerInteraction.Ignore);

            var markerSlack = connector.WorldRadius * 1.5f;
            for (var i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (connector.OwnsCollider(hit.collider))
                    continue;

                var slack = markerSlack;
                var owner = hit.collider.GetComponentInParent<PlacedObject>();
                if (owner != null && owner == connector.Owner)
                    slack = Mathf.Max(slack, ownModelAllowance);

                if (hit.distance < distance - slack)
                    return false;
            }

            return true;
        }
    }
}