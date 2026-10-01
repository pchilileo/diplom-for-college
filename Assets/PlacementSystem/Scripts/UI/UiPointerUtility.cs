using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PlacementSystem
{
    public static class UiPointerUtility
    {
        public static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
                return false;

            if (Mouse.current != null)
                return EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId);

            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
