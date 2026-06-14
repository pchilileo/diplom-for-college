using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlacementSystem
{
    [RequireComponent(typeof(InputField))]
    public class InspectorInputFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public void OnSelect(BaseEventData eventData)
        {
            InteractionLock.SetEditingInspector(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            InteractionLock.SetEditingInspector(false);
        }
    }
}
