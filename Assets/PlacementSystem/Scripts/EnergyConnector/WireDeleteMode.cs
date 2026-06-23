using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    /// <summary>
    /// Wire-deletion mode.
    ///
    /// ── How it works ─────────────────────────────────────────────────────────
    /// • Press <b>3</b> to toggle Wire Delete Mode on/off.
    /// • All existing wires light up (orange/red highlight).
    /// • Hover a wire → it turns bright red.
    /// • Click a wire → it is permanently deleted.
    /// • Press <b>3</b> or <b>Escape</b> to exit without deleting.
    ///
    /// ── Setup ─────────────────────────────────────────────────────────────────
    /// Add this component to the same Manager GameObject as WireConnectionMode.
    /// Assign <c>sceneCamera</c> or leave null for Camera.main.
    /// ─────────────────────────────────────────────────────────────────────────
    /// </summary>
    public class WireDeleteMode : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Reference to WireConnectionMode for mutual exclusion (auto-found if null).")]
        [SerializeField] private WireConnectionMode connectionMode;

        [Tooltip("Scene camera used for picking. Defaults to Camera.main.")]
        [SerializeField] private Camera sceneCamera;

        [Tooltip("Screen-space pixel radius used when picking a wire segment.")]
        [SerializeField] private float pickRadius = 18f;

        [Tooltip("Color applied to all wires while the mode is active (idle).")]
        [SerializeField] private Color idleHighlightColor = new(1f, 0.55f, 0.05f, 1f);   // orange

        [Tooltip("Color applied to the wire under the cursor.")]
        [SerializeField] private Color hoverColor = new(1f, 0.1f, 0.1f, 1f);             // red

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool isActive;

        /// <summary>True while wire-delete mode is active.</summary>
        public bool IsActive => isActive;

        // All wires currently known (refreshed on activate)
        private readonly List<WireConnection> allWires = new();

        // Wire currently under the cursor
        private WireConnection hoveredWire;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (sceneCamera == null)
                sceneCamera = Camera.main;

            if (connectionMode == null)
                connectionMode = FindAnyObjectByType<WireConnectionMode>();
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

        // ── Toggle ────────────────────────────────────────────────────────────

        // Key handling is delegated to EditorModeManager (keys 1/2/3).

        /// <summary>Activate wire-delete mode. Called by <see cref="EditorModeManager"/>.</summary>
        public void ForceActivate()
        {
            if (!isActive)
                Activate();
        }

        private void HandleModeToggle()
        {
            // Intentionally empty — EditorModeManager owns all mode-key logic.
        }

        /// <summary>Called by WireConnectionMode to mutually exclude modes.</summary>
        public void ForceDeactivate()
        {
            if (isActive)
                Deactivate();
        }

        private void Activate()
        {
            // Deactivate connection mode if it's running
            if (connectionMode != null && connectionMode.IsActive)
                connectionMode.ForceDeactivate();

            isActive = true;

            // Block normal object selection while choosing a wire
            InteractionLock.SetWiringMode(true);
            SelectionManager.Instance?.Deselect();

            RefreshWireList();
            SetAllWireColors(idleHighlightColor);

            Debug.Log("[WireDeleteMode] Activated — click a wire to delete it.");
        }

        private void Deactivate()
        {
            isActive = false;

            // Restore wire colors before hiding highlights
            RestoreAllWireColors();

            hoveredWire = null;
            allWires.Clear();

            InteractionLock.SetWiringMode(false);

            Debug.Log("[WireDeleteMode] Deactivated.");
        }

        // ── Hover ─────────────────────────────────────────────────────────────

        private void HandleHover()
        {
            var mousePos = GetMousePosition();
            var hit = PickWire(mousePos);

            if (hit == hoveredWire)
                return;

            // Restore previous hover
            if (hoveredWire != null)
                SetWireColor(hoveredWire, idleHighlightColor);

            hoveredWire = hit;

            if (hoveredWire != null)
                SetWireColor(hoveredWire, hoverColor);
        }

        // ── Click ─────────────────────────────────────────────────────────────

        private void HandleClick()
        {
            if (!WasPrimaryClickThisFrame())
                return;

            if (UiPointerUtility.IsPointerOverUi())
                return;

            var mousePos = GetMousePosition();
            var hit = PickWire(mousePos);

            if (hit == null)
                return;

            DeleteWire(hit);
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
            if (escape) Deactivate();
        }

        // ── Wire management ───────────────────────────────────────────────────

        private void RefreshWireList()
        {
            allWires.Clear();
            var found = FindObjectsByType<WireConnection>(FindObjectsSortMode.None);
            allWires.AddRange(found);
        }

        private void DeleteWire(WireConnection wire)
        {
            allWires.Remove(wire);

            if (wire == hoveredWire)
                hoveredWire = null;

            // Notify owners before destroying
            wire.ConnectorA?.Owner?.NotifyConnectionsChanged();
            wire.ConnectorB?.Owner?.NotifyConnectionsChanged();

            wire.DestroyWire();

            Debug.Log("[WireDeleteMode] Wire deleted.");
        }

        // ── Wire color helpers ────────────────────────────────────────────────

        private static void SetWireColor(WireConnection wire, Color color)
        {
            if (wire == null) return;

            var lr = wire.GetComponent<LineRenderer>();
            if (lr == null) return;

            lr.startColor = color;
            lr.endColor   = color;

            // Also tint the material so it's visible on all pipelines
            if (lr.material != null)
                lr.material.color = color;
        }

        private void SetAllWireColors(Color color)
        {
            allWires.RemoveAll(w => w == null);
            foreach (var wire in allWires)
                SetWireColor(wire, color);
        }

        private void RestoreAllWireColors()
        {
            allWires.RemoveAll(w => w == null);
            foreach (var wire in allWires)
            {
                // Ask the wire to reset itself to its configured color
                var lr = wire.GetComponent<LineRenderer>();
                if (lr == null) continue;

                // WireConnection stores wireColor as a private field.
                // We restore by calling the wire's own restore method.
                wire.RestoreDefaultColor();
            }
        }

        // ── Wire picking ──────────────────────────────────────────────────────

        /// <summary>
        /// Picks the wire whose line passes closest to the mouse in screen space.
        /// Iterates over every segment of each wire's LineRenderer.
        /// </summary>
        private WireConnection PickWire(Vector2 screenPos)
        {
            if (sceneCamera == null)
                return null;

            WireConnection best = null;
            var bestDist = float.MaxValue;

            foreach (var wire in allWires)
            {
                if (wire == null) continue;

                var lr = wire.GetComponent<LineRenderer>();
                if (lr == null || lr.positionCount < 2) continue;

                var dist = MinScreenDistToLineRenderer(lr, screenPos);
                if (dist < pickRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = wire;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns the minimum screen-space distance (pixels) from <paramref name="screenPos"/>
        /// to any segment of the given LineRenderer.
        /// </summary>
        private float MinScreenDistToLineRenderer(LineRenderer lr, Vector2 screenPos)
        {
            var minDist = float.MaxValue;

            // Collect world positions
            var count = lr.positionCount;
            var positions = new Vector3[count];
            lr.GetPositions(positions);

            for (var i = 0; i < count - 1; i++)
            {
                var sA = sceneCamera.WorldToScreenPoint(positions[i]);
                var sB = sceneCamera.WorldToScreenPoint(positions[i + 1]);

                // Skip segments behind camera
                if (sA.z < 0f && sB.z < 0f) continue;

                var dist = PointToSegmentDistance(
                    screenPos,
                    new Vector2(sA.x, sA.y),
                    new Vector2(sB.x, sB.y));

                if (dist < minDist)
                    minDist = dist;
            }

            return minDist;
        }

        /// <summary>Point-to-line-segment distance in 2-D.</summary>
        private static float PointToSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var ap = p - a;
            var lenSq = ab.sqrMagnitude;

            if (lenSq < 0.0001f)
                return Vector2.Distance(p, a);

            var t = Mathf.Clamp01(Vector2.Dot(ap, ab) / lenSq);
            var closest = a + t * ab;
            return Vector2.Distance(p, closest);
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