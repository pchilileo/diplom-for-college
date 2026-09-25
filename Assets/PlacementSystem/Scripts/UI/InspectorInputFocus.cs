using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlacementSystem
{
    /// <summary>
    /// While a text field has focus, keyboard shortcuts (camera WASD, mode keys,
    /// Delete, T/R) are suspended so typing does not drive the editor.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
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

        private void OnDisable()
        {
            InteractionLock.SetEditingInspector(false);
        }
    }
}
