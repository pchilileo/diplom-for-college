using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    public static class UiPointerUtility
    {
        public static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
                return false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
                return EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId);

            return EventSystem.current.IsPointerOverGameObject();
#else
            return EventSystem.current.IsPointerOverGameObject();
#endif
        }
    }
}
