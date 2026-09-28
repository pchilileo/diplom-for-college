using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    public class DragPlacementHandler : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;

        private AssetData draggingAsset;
        private bool isDragging;

        private void Awake()
        {
            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }

        private void Update()
        {
            // Escape cancels the placement; releasing the mouse afterwards does nothing.
            if (isDragging && WasEscapePressed())
            {
                InteractionLock.ConsumeEscape();
                CancelDrag();
            }
        }

        public void BeginDrag(AssetData asset)
        {
            if (asset == null || PlacementManager.Instance == null)
                return;

            if (InteractionLock.IsCheckMode)
            {
                EditorNotifications.Post(Loc.Get("MSG_CHECK_NO_PLACE"));
                return;
            }

            draggingAsset = asset;
            isDragging = true;
            InteractionLock.SetDraggingAsset(true);
            PlacementManager.Instance.CreatePreview(asset);
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (!isDragging || draggingAsset == null || PlacementManager.Instance == null)
                return;

            if (TryGetGroundPoint(eventData.position, out var point))
                PlacementManager.Instance.UpdatePreviewPosition(point);
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (!isDragging || draggingAsset == null || PlacementManager.Instance == null)
            {
                CancelDrag();
                return;
            }

            // Released over a panel: the ground behind the panel is not a target.
            if (!UiPointerUtility.IsPointerOverUi() && TryGetGroundPoint(eventData.position, out var point))
            {
                var spawned = PlacementManager.Instance.CommitPreview(draggingAsset, point);
                if (spawned != null && SelectionManager.Instance != null)
                    SelectionManager.Instance.Select(spawned);
            }
            else
            {
                PlacementManager.Instance.DestroyPreview();
            }

            draggingAsset = null;
            isDragging = false;
            InteractionLock.SetDraggingAsset(false);
        }

        public void CancelDrag()
        {
            if (PlacementManager.Instance != null)
                PlacementManager.Instance.DestroyPreview();

            draggingAsset = null;
            isDragging = false;
            InteractionLock.SetDraggingAsset(false);
        }

        private static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = default;
            if (sceneCamera == null)
                return false;

            var ray = sceneCamera.ScreenPointToRay(screenPosition);
            if (!PlacementLayerUtility.TryRaycastGround(ray, out var hit))
                return false;

            point = hit.point;
            return true;
        }
    }
}
