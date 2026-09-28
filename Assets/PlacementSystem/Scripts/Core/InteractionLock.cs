using System;
using UnityEngine;

namespace PlacementSystem
{
    public static class InteractionLock
    {
        public static event Action<bool> LockChanged;

        /// <summary>Fired when the wiring check mode is entered (true) or left (false).</summary>
        public static event Action<bool> CheckModeChanged;

        public static bool IsCameraLocked { get; private set; }
        public static bool IsDraggingAsset { get; private set; }
        public static bool IsEditingInspector { get; private set; }

        /// <summary>True while Wire Connection Mode is active. Blocks object selection and gizmo interaction.</summary>
        public static bool IsWiringMode { get; private set; }

        /// <summary>True while a modal window (e.g. the save / load menu) covers the screen.</summary>
        public static bool IsModalOpen { get; private set; }

        /// <summary>
        /// True while a reference file is loaded and the wiring is being checked:
        /// wires can't be added or removed, objects can't be placed or deleted —
        /// only moved, rotated and scaled.
        /// </summary>
        public static bool IsCheckMode { get; private set; }

        /// <summary>Keyboard shortcuts must be ignored: the user is typing or a window is open.</summary>
        public static bool IsKeyboardCaptured => IsEditingInspector || IsModalOpen;

        public static bool ShouldBlockCamera    => IsCameraLocked || IsDraggingAsset || IsEditingInspector || IsModalOpen;
        public static bool ShouldBlockSelection => IsDraggingAsset || IsWiringMode || IsModalOpen;

        // ── Clicks after a system dialog ──────────────────────────────────────

        private static float clicksSuppressedUntil;

        /// <summary>
        /// Ignore mouse clicks for a moment. Used after a Windows file dialog
        /// closes: the last click in the dialog (e.g. the second half of a
        /// double-click on a file) arrives in the game window afterwards.
        /// </summary>
        public static void SuppressClicks(float seconds)
        {
            clicksSuppressedUntil = Time.unscaledTime + seconds;
        }

        public static bool AreClicksSuppressed => Time.unscaledTime < clicksSuppressedUntil;

        // ── Escape ────────────────────────────────────────────────────────────

        private static int escapeConsumedFrame = -1;

        /// <summary>
        /// Call after reacting to Escape (closing a window, cancelling a drag…),
        /// so nothing else reacts to the same key press in this frame.
        /// </summary>
        public static void ConsumeEscape()
        {
            escapeConsumedFrame = Time.frameCount;
        }

        /// <summary>True if Escape was already handled in this frame.</summary>
        public static bool IsEscapeConsumed => escapeConsumedFrame == Time.frameCount;

        public static void SetDraggingAsset(bool value)
        {
            if (IsDraggingAsset == value)
                return;

            IsDraggingAsset = value;
            Notify();
        }

        public static void SetEditingInspector(bool value)
        {
            if (IsEditingInspector == value)
                return;

            IsEditingInspector = value;
            Notify();
        }

        public static void SetCameraLocked(bool value)
        {
            if (IsCameraLocked == value)
                return;

            IsCameraLocked = value;
            Notify();
        }

        public static void SetWiringMode(bool value)
        {
            if (IsWiringMode == value)
                return;

            IsWiringMode = value;
            Notify();
        }

        public static void SetModalOpen(bool value)
        {
            if (IsModalOpen == value)
                return;

            IsModalOpen = value;
            Notify();
        }

        public static void SetCheckMode(bool value)
        {
            if (IsCheckMode == value)
                return;

            IsCheckMode = value;
            CheckModeChanged?.Invoke(value);
        }

        private static void Notify()
        {
            LockChanged?.Invoke(ShouldBlockCamera);
        }
    }
}
