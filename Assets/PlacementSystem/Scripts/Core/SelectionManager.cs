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

            if (InteractionLock.AreClicksSuppressed)
                return;

            if (!InputUtility.WasLeftClickPressed)
                return;

            if (UiPointerUtility.IsPointerOverUi())
                return;

            var mousePosition = InputUtility.MousePosition;
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

            if (InteractionLock.IsCheckMode)
            {
                EditorNotifications.Post(Loc.Get("MSG_CHECK_NO_DELETE"));
                return;
            }

            // Keep the reference: the selection is cleared before the object is destroyed.
            var toRemove = selectedObject;

            // Clear highlight and selection state first
            toRemove.SetSelected(false);
            selectedObject = null;
            transformGizmo?.Detach();
            SelectionChanged?.Invoke(null);

            // Now safe to destroy
            PlacementManager.Instance.Remove(toRemove);
        }
    }
}