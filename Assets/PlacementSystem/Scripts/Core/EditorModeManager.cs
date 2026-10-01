using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlacementSystem
{
    /// <summary>
    /// Central keyboard-mode switcher for the placement editor.
    ///
    ///   1       →  Normal mode  (object selection, translate / rotate gizmo)
    ///   2       →  Wire Connect mode  (draw wires between connectors)
    ///   3       →  Wire Delete  mode  (click a wire to remove it)
    ///   Escape  →  cancel the half-built wire, otherwise back to Normal
    ///
    /// Add this component to the same Manager GameObject as
    /// <see cref="WireConnectionMode"/> and <see cref="WireDeleteMode"/>.
    /// The sibling scripts keep their own per-mode logic; this script is the
    /// only place that reads the mode keys, so <see cref="CurrentMode"/>
    /// always matches the mode that is actually running.
    /// </summary>
    public class EditorModeManager : MonoBehaviour
    {
        public enum EditorMode { Normal = 1, WireConnect = 2, WireDelete = 3 }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Auto-found on the same GameObject if not assigned.")]
        [SerializeField] private WireConnectionMode wireConnectMode;

        [Tooltip("Auto-found on the same GameObject if not assigned.")]
        [SerializeField] private WireDeleteMode wireDeleteMode;

        // ── State ─────────────────────────────────────────────────────────────

        private EditorMode currentMode = EditorMode.Normal;

        public EditorMode CurrentMode => currentMode;

        /// <summary>Fired after the active mode has changed.</summary>
        public event Action<EditorMode> ModeChanged;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (wireConnectMode == null)
                wireConnectMode = GetComponentInChildren<WireConnectionMode>()
                               ?? FindAnyObjectByType<WireConnectionMode>();

            if (wireDeleteMode == null)
                wireDeleteMode = GetComponentInChildren<WireDeleteMode>()
                              ?? FindAnyObjectByType<WireDeleteMode>();
        }

        private void Update()
        {
            // Digits and Escape typed into a text field (or an open window) belong there.
            if (InteractionLock.IsKeyboardCaptured)
                return;

            if (InputUtility.WasEscapePressed)
            {
                // Escape that already closed a window or cancelled a drag this
                // frame, or that is about to cancel a drag (object from the list,
                // gizmo handle — they lock the camera), is not a mode command.
                if (!InteractionLock.IsEscapeConsumed && !InteractionLock.IsDraggingAsset && !InteractionLock.IsCameraLocked)
                    HandleEscape();
                return;
            }

            var key = ReadModeKey();
            if (key == 0)
                return;

            // Pressing the current mode key again → return to Normal
            if ((int)currentMode == key)
            {
                SwitchTo(EditorMode.Normal);
                return;
            }

            SwitchTo((EditorMode)key);
        }

        // ── Switching ─────────────────────────────────────────────────────────

        public void SwitchTo(EditorMode mode)
        {
            if (currentMode == mode)
                return;

            if (InteractionLock.IsCheckMode && mode != EditorMode.Normal)
            {
                EditorNotifications.Post(Loc.Get("MSG_CHECK_NO_WIRE_EDIT"));
                return;
            }

            // ── Leave current mode ─────────────────────────────────────────
            switch (currentMode)
            {
                case EditorMode.WireConnect:
                    wireConnectMode?.ForceDeactivate();
                    break;
                case EditorMode.WireDelete:
                    wireDeleteMode?.ForceDeactivate();
                    break;
            }

            currentMode = mode;

            // ── Enter new mode ─────────────────────────────────────────────
            switch (currentMode)
            {
                case EditorMode.Normal:
                    // Nothing extra — InteractionLock is already cleared by ForceDeactivate above.
                    break;

                case EditorMode.WireConnect:
                    wireConnectMode?.ForceActivate();
                    break;

                case EditorMode.WireDelete:
                    wireDeleteMode?.ForceActivate();
                    break;
            }

            ModeChanged?.Invoke(currentMode);
        }

        private void HandleEscape()
        {
            if (currentMode == EditorMode.WireConnect && wireConnectMode != null &&
                wireConnectMode.TryCancelPendingWire())
                return;

            SwitchTo(EditorMode.Normal);
        }

        // ── Key reading ───────────────────────────────────────────────────────

        /// <summary>Returns 1, 2, or 3 if the corresponding key was pressed this frame; otherwise 0.</summary>
        private static int ReadModeKey()
        {
            if (InputUtility.WasKeyPressed(Key.Digit1)) return 1;
            if (InputUtility.WasKeyPressed(Key.Digit2)) return 2;
            if (InputUtility.WasKeyPressed(Key.Digit3)) return 3;
            return 0;
        }
    }
}