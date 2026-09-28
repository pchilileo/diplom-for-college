using System;

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
