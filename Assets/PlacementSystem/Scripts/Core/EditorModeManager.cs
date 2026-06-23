using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    /// <summary>
    /// Central keyboard-mode switcher for the placement editor.
    ///
    ///   1  →  Normal mode  (object selection, translate / rotate gizmo)
    ///   2  →  Wire Connect mode  (draw wires between connectors)
    ///   3  →  Wire Delete  mode  (click a wire to remove it)
    ///
    /// Add this component to the same Manager GameObject as
    /// <see cref="WireConnectionMode"/> and <see cref="WireDeleteMode"/>.
    /// The three sibling scripts keep their own per-mode logic;
    /// this script is the only place that reads keys 1 / 2 / 3
    /// and decides which mode to activate or deactivate.
    ///
    /// ── Migration note ────────────────────────────────────────────────────────
    /// <see cref="WireConnectionMode"/> and <see cref="WireDeleteMode"/> no longer
    /// handle their own toggle keys — remove (or leave harmless) any duplicate
    /// HandleModeToggle calls if you edited those files separately.
    /// This script supersedes them; the individual ForceDeactivate / ForceActivate
    /// public methods on those classes are still used here.
    /// ─────────────────────────────────────────────────────────────────────────
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
                    Debug.Log("[EditorModeManager] → Normal mode (1)");
                    break;

                case EditorMode.WireConnect:
                    wireConnectMode?.ForceActivate();
                    Debug.Log("[EditorModeManager] → Wire Connect mode (2)");
                    break;

                case EditorMode.WireDelete:
                    wireDeleteMode?.ForceActivate();
                    Debug.Log("[EditorModeManager] → Wire Delete mode (3)");
                    break;
            }
        }

        // ── Key reading ───────────────────────────────────────────────────────

        /// <summary>Returns 1, 2, or 3 if the corresponding key was pressed this frame; otherwise 0.</summary>
        private static int ReadModeKey()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return 0;

            if (kb.digit1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame) return 3;
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
#endif
            return 0;
        }
    }
}