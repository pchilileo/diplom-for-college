using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlacementSystem
{
    public class SelectionManager : MonoBehaviour
    {
        public static SelectionManager Instance { get; private set; }

        [SerializeField] private Camera sceneCamera;
        [SerializeField] private RuntimeTransformGizmo transformGizmo;

        private PlacedObject selectedObject;

        public PlacedObject SelectedObject => selectedObject;

        public event Action<PlacedObject> SelectionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }

        private void Update()
        {
            if (InteractionLock.ShouldBlockSelection || InteractionLock.IsEditingInspector)
                return;

            if (transformGizmo != null && transformGizmo.IsDragging)
                return;

            if (!TryGetPrimaryClick(out var mousePosition))
                return;

            if (UiPointerUtility.IsPointerOverUi())
                return;

            if (transformGizmo != null && transformGizmo.TryHandleClick(mousePosition))
                return;

            var ray = sceneCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                var placed = hit.collider.GetComponentInParent<PlacedObject>();
                if (placed != null)
                {
                    Select(placed);
                    return;
                }
            }

            if (PlacementLayerUtility.TryRaycastGround(ray, out _))
                Deselect();
        }

        public void Select(PlacedObject placedObject)
        {
            if (selectedObject == placedObject)
                return;

            // Remove highlight from previously selected object
            if (selectedObject != null)
                selectedObject.SetSelected(false);

            selectedObject = placedObject;
            transformGizmo?.Attach(selectedObject);

            // Apply highlight to new selection
            if (selectedObject != null)
                selectedObject.SetSelected(true);

            SelectionChanged?.Invoke(selectedObject);
        }

        public void Deselect()
        {
            if (selectedObject == null)
                return;

            selectedObject.SetSelected(false);
            selectedObject = null;
            transformGizmo?.Detach();
            SelectionChanged?.Invoke(null);
        }

        public void DeleteSelected()
        {
            if (selectedObject == null || PlacementManager.Instance == null)
                return;

            // FIX: save reference BEFORE Deselect() nulls selectedObject
            var toRemove = selectedObject;

            // Clear highlight and selection state first
            toRemove.SetSelected(false);
            selectedObject = null;
            transformGizmo?.Detach();
            SelectionChanged?.Invoke(null);

            // Now safe to destroy
            PlacementManager.Instance.Remove(toRemove);
        }

        private static bool TryGetPrimaryClick(out Vector3 mousePosition)
        {
            mousePosition = default;

#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return false;

            mousePosition = mouse.position.ReadValue();
            return true;
#else
            if (!Input.GetMouseButtonDown(0))
                return false;

            mousePosition = Input.mousePosition;
            return true;
#endif
        }
    }
}