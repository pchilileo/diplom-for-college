using System;

namespace PlacementSystem
{
    public static class InteractionLock
    {
        public static event Action<bool> LockChanged;

        public static bool IsCameraLocked { get; private set; }
        public static bool IsDraggingAsset { get; private set; }
        public static bool IsEditingInspector { get; private set; }

        /// <summary>True while Wire Connection Mode is active. Blocks object selection and gizmo interaction.</summary>
        public static bool IsWiringMode { get; private set; }

        public static bool ShouldBlockCamera    => IsCameraLocked || IsDraggingAsset || IsEditingInspector;
        public static bool ShouldBlockSelection => IsDraggingAsset || IsWiringMode;

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

        private static void Notify()
        {
            LockChanged?.Invoke(ShouldBlockCamera);
        }
    }
}