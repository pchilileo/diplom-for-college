using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    /// • Press <b>2</b> or <b>Escape</b> to cancel at any time.
    ///
    /// ── Setup in the Unity Editor ─────────────────────────────────────────────
    /// 1. Add this component to any persistent Manager GameObject.
    /// 2. Assign <c>wirePrefab</c> — a Prefab with a <see cref="WireConnection"/>
    ///    (and LineRenderer) on its root.  A plain empty GameObject works too;
    ///    WireConnection adds a LineRenderer via RequireComponent.
    /// 3. Assign <c>sceneCamera</c> (or leave null to use Camera.main).
    /// 4. Optionally set <c>connectorPickRadius</c> for screen-space picking tolerance.
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public class WireConnectionMode : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Prefab that has WireConnection (+ LineRenderer) on its root.")]
        [SerializeField] private WireConnection wirePrefab;

        [Tooltip("Scene camera used for screen-space picking. Defaults to Camera.main.")]
        [SerializeField] private Camera sceneCamera;

        [Tooltip("Screen-space pixel radius for connector picking.")]
        [SerializeField] private float connectorPickRadius = 28f;

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool isActive;
        private EnergyConnector firstConnector;

        // All connectors found in the scene (refreshed each time mode activates)
        private readonly List<EnergyConnector> allConnectors = new();

        // Currently hovered connector (for hover highlight)
        private EnergyConnector hoveredConnector;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }


        private void Start()
        {
            // Hide every connector that already exists in the scene at startup.
            // Connectors spawned later at runtime are hidden by EnergyConnector.Awake.
            var found = FindObjectsByType<EnergyConnector>(FindObjectsSortMode.None);
            foreach (var c in found)
                c.gameObject.SetActive(false);
        }

        private void Update()
        {
            HandleModeToggle();

            if (!isActive)
                return;

            HandleHover();
            HandleClick();
            HandleCancel();
        }

        // ── Mode toggle ───────────────────────────────────────────────────────

        private void HandleModeToggle()
        {
            var pressed = false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame)
                pressed = true;
#else
            if (Input.GetKeyDown(KeyCode.Alpha2))
                pressed = true;
#endif

            if (!pressed)
                return;

            if (isActive)
                Deactivate();
            else
                Activate();
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
            SetAllGameObjectsActive(true);
            SetAllHighlights(EnergyConnector.HighlightState.Available);

            Debug.Log("[WireConnectionMode] Activated — click a connector to start a wire.");
        }

        private void Deactivate()
        {
            isActive = false;

            // Reset highlights before hiding so renderers end up in idle state
            SetAllHighlights(EnergyConnector.HighlightState.Idle);
            SetAllGameObjectsActive(false);

            firstConnector   = null;
            hoveredConnector = null;

            InteractionLock.SetWiringMode(false);

            Debug.Log("[WireConnectionMode] Deactivated.");
        }

        // ── Hover ─────────────────────────────────────────────────────────────

        private void HandleHover()
        {
            var mousePos = GetMousePosition();
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
            if (!WasPrimaryClickThisFrame())
                return;

            if (UiPointerUtility.IsPointerOverUi())
                return;

            var mousePos = GetMousePosition();
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
                Debug.Log($"[WireConnectionMode] First connector selected: {hit.name} on {hit.Owner?.name}");
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

                CreateWire(firstConnector, hit);

                // Reset for next wire
                firstConnector.SetHighlight(EnergyConnector.HighlightState.Available);
                firstConnector = null;
                hoveredConnector = null;

                // Refresh so the new wire's connector is still highlighted
                SetAllHighlights(EnergyConnector.HighlightState.Available);
            }
        }

        // ── Cancel ────────────────────────────────────────────────────────────

        private void HandleCancel()
        {
            var escape = false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                escape = true;
#else
            if (Input.GetKeyDown(KeyCode.Escape))
                escape = true;
#endif

            if (escape)
                Deactivate();
        }

        // ── Wire creation ─────────────────────────────────────────────────────

        private void CreateWire(EnergyConnector a, EnergyConnector b)
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

            Debug.Log($"[WireConnectionMode] Wire created: {a.name} ↔ {b.name}");
        }

        // ── Connector management ──────────────────────────────────────────────

        /// <summary>
        /// Finds every EnergyConnector currently active in the scene.
        /// Called once when the mode activates.
        /// </summary>
        private void RefreshConnectorList()
        {
            allConnectors.Clear();
            var found = FindObjectsByType<EnergyConnector>(FindObjectsSortMode.None);
            allConnectors.AddRange(found);
        }

        private void SetAllHighlights(EnergyConnector.HighlightState state)
        {
            allConnectors.RemoveAll(c => c == null);

            foreach (var connector in allConnectors)
                connector.SetHighlight(state);
        }

        /// <summary>
        /// Shows or hides the GameObject of every known connector.
        /// Connectors are hidden by default and only shown while the mode is active.
        /// </summary>
        private void SetAllGameObjectsActive(bool active = true)
        {
            allConnectors.RemoveAll(c => c == null);

            foreach (var connector in allConnectors)
                connector.gameObject.GetComponent<MeshRenderer>().enabled = active;
        }

        // ── Connector picking ─────────────────────────────────────────────────

        /// <summary>
        /// Finds the nearest EnergyConnector within <see cref="connectorPickRadius"/>
        /// screen pixels of <paramref name="screenPos"/>. Returns null if none.
        /// </summary>
        private EnergyConnector PickConnector(Vector2 screenPos)
        {
            if (sceneCamera == null)
                return null;

            EnergyConnector best = null;
            var bestDist = float.MaxValue;

            foreach (var connector in allConnectors)
            {
                if (connector == null)
                    continue;

                var screenPoint = sceneCamera.WorldToScreenPoint(connector.transform.position);

                // Discard if behind the camera
                if (screenPoint.z < 0f)
                    continue;

                var dist = Vector2.Distance(screenPos, new Vector2(screenPoint.x, screenPoint.y));
                if (dist < connectorPickRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = connector;
                }
            }

            return best;
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

        private static bool WasPrimaryClickThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }
}