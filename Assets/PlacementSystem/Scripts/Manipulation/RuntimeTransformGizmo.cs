using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.UI;
#endif

namespace PlacementSystem
{
    public enum GizmoMode
    {
        Translate,
        Rotate
    }

    public class RuntimeTransformGizmo : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;

        [Tooltip("Base visual size of handles (world units at reference distance).")]
        [SerializeField] private float handleSize = 0.35f;

        [Tooltip("Minimum world-space handle length so arrows stay visible when very close.")]
        [SerializeField] private float minHandleSize = 0.4f;

        [Tooltip("Maximum world-space handle length so arrows don't become enormous when far away.")]
        [SerializeField] private float maxHandleSize = 8f;

        [SerializeField] private float pickRadius = 24f;

        private PlacedObject target;
        private GizmoMode mode = GizmoMode.Translate;
        private bool isDragging;
        private int activeAxis = -1;
        private Plane dragPlane;
        private Vector3 dragStartWorld;
        private Vector3 targetStartPosition;
        private Quaternion targetStartRotation;

        public bool IsDragging => isDragging;
        public GizmoMode Mode => mode;

        private void Awake()
        {
            if (sceneCamera == null)
                sceneCamera = Camera.main;
        }

        public void Attach(PlacedObject placedObject)
        {
            target = placedObject;
        }

        public void Detach()
        {
            target = null;
            isDragging = false;
            activeAxis = -1;
        }

        public void SetMode(GizmoMode newMode)
        {
            mode = newMode;
        }

        public bool TryHandleClick(Vector3 screenPosition)
        {
            if (target == null)
                return false;

            activeAxis = PickAxis(screenPosition);
            if (activeAxis < 0)
                return false;

            BeginDrag(screenPosition);
            return true;
        }

        private void Update()
        {
            if (target == null)
                return;

            HandleModeSwitch();
            
            if (isDragging)
            {
                if (IsPrimaryHeld())
                    ContinueDrag();
                else
                    EndDrag();
            }
        }
        
        private void HandleModeSwitch()
        {
            #if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                if (keyboard == null)
                    return;

                if (keyboard.rKey.wasPressedThisFrame)
                    SetMode(GizmoMode.Rotate);
                else if (keyboard.tKey.wasPressedThisFrame)
                    SetMode(GizmoMode.Translate);
                    
            #else
                if (Input.GetKeyDown(KeyCode.R))
                {
                    SetMode(GizmoMode.Rotate);
                }
                else if (Input.GetKeyDown(KeyCode.T))
                {
                    SetMode(GizmoMode.Translate);
                }
            #endif
        }

        private void OnDrawGizmos()
        {
            if (target == null)
                return;

            var origin = target.transform.position;
            var size = HandleWorldSize(origin);

            switch (mode)
            {
                case GizmoMode.Translate:
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(origin, origin + Vector3.right * size);
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(origin, origin + Vector3.up * size);
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(origin, origin + Vector3.forward * size);
                    break;
                case GizmoMode.Rotate:
                    Gizmos.color = Color.yellow;
                    DrawCircle(origin, size * 1.2f, Vector3.up, 32);
                    break;
            }
        }

        private void BeginDrag(Vector3 screenPosition)
        {
            isDragging = true;
            targetStartPosition = target.transform.position;
            targetStartRotation = target.transform.rotation;

            if (mode == GizmoMode.Translate)
            {
                var normal = sceneCamera.transform.forward * -1f;
                if (activeAxis == 1)
                    normal = Vector3.forward;
                else if (activeAxis == 0)
                    normal = Vector3.up;

                dragPlane = new Plane(normal, targetStartPosition);
                var ray = sceneCamera.ScreenPointToRay(screenPosition);
                if (dragPlane.Raycast(ray, out var enter))
                    dragStartWorld = ray.GetPoint(enter);
            }
            else
            {
                dragStartWorld = screenPosition;
            }
        }

        private void ContinueDrag()
        {
            if (PlacementManager.Instance == null)
                return;

            var snap = PlacementManager.Instance.SnapSettings;
            var ray = sceneCamera.ScreenPointToRay(GetMousePosition());

            if (mode == GizmoMode.Translate)
            {
                if (!dragPlane.Raycast(ray, out var enter))
                    return;

                var currentWorld = ray.GetPoint(enter);
                var delta = currentWorld - dragStartWorld;
                var next = targetStartPosition + FilterDelta(delta);

                target.transform.position = snap.SnapPosition(next);
            }
            else
            {
                var current = GetMousePosition();
                var deltaX = current.x - dragStartWorld.x;
                var euler = targetStartRotation.eulerAngles;
                euler.y += deltaX * 0.5f;
                euler = snap.SnapRotation(euler);
                target.transform.rotation = Quaternion.Euler(euler);
            }

            target.NotifyTransformChanged();
        }

        private void EndDrag()
        {
            isDragging = false;
            activeAxis = -1;
        }

        private Vector3 FilterDelta(Vector3 delta)
        {
            return activeAxis switch
            {
                0 => new Vector3(delta.x, 0f, 0f),
                1 => new Vector3(0f, delta.y, 0f),
                2 => new Vector3(0f, 0f, delta.z),
                _ => new Vector3(delta.x, 0f, delta.z)
            };
        }

        private int PickAxis(Vector3 screenPosition)
        {
            var origin = target.transform.position;
            var size = HandleWorldSize(origin);

            if (mode == GizmoMode.Rotate)
            {
                var centerScreen = sceneCamera.WorldToScreenPoint(origin);
                var dist = Vector2.Distance(new Vector2(screenPosition.x, screenPosition.y),
                    new Vector2(centerScreen.x, centerScreen.y));
                return dist <= pickRadius * 1.5f ? 3 : -1;
            }

            var bestAxis = -1;
            var bestDistance = float.MaxValue;

            var axes = new[]
            {
                (axis: 0, world: origin + Vector3.right * size),
                (axis: 1, world: origin + Vector3.up * size),
                (axis: 2, world: origin + Vector3.forward * size)
            };

            foreach (var (axis, world) in axes)
            {
                var screen = sceneCamera.WorldToScreenPoint(world);
                var dist = Vector2.Distance(new Vector2(screenPosition.x, screenPosition.y),
                    new Vector2(screen.x, screen.y));
                if (dist < bestDistance && dist <= pickRadius)
                {
                    bestDistance = dist;
                    bestAxis = axis;
                }
            }

            if (bestAxis < 0)
            {
                var centerScreen = sceneCamera.WorldToScreenPoint(origin);
                var centerDist = Vector2.Distance(new Vector2(screenPosition.x, screenPosition.y),
                    new Vector2(centerScreen.x, centerScreen.y));
                if (centerDist <= pickRadius)
                    bestAxis = 3;
            }

            return bestAxis;
        }

        /// <summary>
        /// Computes a screen-stable handle size that is clamped so arrows are
        /// always visible regardless of camera distance.
        /// </summary>
        private float HandleWorldSize(Vector3 worldPosition)
        {
            if (sceneCamera == null)
                return Mathf.Clamp(handleSize, minHandleSize, maxHandleSize);

            var distance = Vector3.Distance(sceneCamera.transform.position, worldPosition);

            // Scale proportionally with distance but clamp to avoid extremes
            var size = handleSize * distance * 0.15f;
            return Mathf.Clamp(size, minHandleSize, maxHandleSize);
        }

        private static void DrawCircle(Vector3 center, float radius, Vector3 normal, int segments)
        {
            var from = Vector3.Cross(normal, Vector3.up);
            if (from.sqrMagnitude < 0.001f)
                from = Vector3.Cross(normal, Vector3.right);

            from.Normalize();
            var step = 360f / segments;
            var prev = center + from * radius;

            for (var i = 1; i <= segments; i++)
            {
                var next = center + (Quaternion.AngleAxis(step * i, normal) * from) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static Vector3 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
#else
            return Input.mousePosition;
#endif
        }

        private static bool IsPrimaryHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
            return Input.GetMouseButton(0);
#endif
        }
    }
}