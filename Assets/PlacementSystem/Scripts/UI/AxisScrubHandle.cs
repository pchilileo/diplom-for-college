using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlacementSystem
{
    /// <summary>
    /// Axis label ("X", "Y", "Z") that changes its value when dragged
    /// left / right, like the field labels in the Unity inspector.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class AxisScrubHandle : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Horizontal mouse movement in pixels since the last event.</summary>
        public event Action<float> Scrubbed;

        private TMP_Text label;
        private Color baseColor;
        private bool isHovered;
        private bool isDragging;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
            baseColor = label.color;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            // Freezes the camera and stops the inspector from overwriting the value mid-drag.
            InteractionLock.SetEditingInspector(true);
            UpdateColor();
        }

        public void OnDrag(PointerEventData eventData)
        {
            Scrubbed?.Invoke(eventData.delta.x);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            InteractionLock.SetEditingInspector(false);
            UpdateColor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateColor();
        }

        private void UpdateColor()
        {
            label.color = isHovered || isDragging ? Color.Lerp(baseColor, Color.white, 0.5f) : baseColor;
        }
    }
}
