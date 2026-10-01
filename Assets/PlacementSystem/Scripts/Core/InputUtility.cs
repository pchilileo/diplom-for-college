using UnityEngine;
using UnityEngine.InputSystem;

namespace PlacementSystem
{
    /// <summary>
    /// Mouse and keyboard queries shared by all editor systems.
    /// Every query is safe when the device is missing (returns false / zero).
    /// </summary>
    public static class InputUtility
    {
        public static Vector2 MousePosition =>
            Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public static bool WasLeftClickPressed  => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool WasRightClickPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool IsLeftButtonHeld     => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool IsRightButtonHeld    => Mouse.current != null && Mouse.current.rightButton.isPressed;

        public static bool IsCtrlHeld  => Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
        public static bool IsShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        public static bool WasEscapePressed => WasKeyPressed(Key.Escape);

        public static bool WasKeyPressed(Key key) =>
            Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    }
}
