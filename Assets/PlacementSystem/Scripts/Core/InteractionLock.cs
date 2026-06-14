using System;

namespace PlacementSystem
{
    public static class InteractionLock
    {
        public static event Action<bool> LockChanged;

        public static bool IsCameraLocked { get; private set; }
        public static bool IsDraggingAsset { get; private set; }
        public static bool IsEditingInspector { get; private set; }

        public static bool ShouldBlockCamera => IsCameraLocked || IsDraggingAsset || IsEditingInspector;

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

        private static void Notify()
        {
            LockChanged?.Invoke(ShouldBlockCamera);
        }
    }
}
