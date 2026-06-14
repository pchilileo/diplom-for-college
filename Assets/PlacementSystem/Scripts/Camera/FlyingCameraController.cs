using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlacementSystem
{
    [RequireComponent(typeof(Camera))]
    public class FlyingCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float fastMoveMultiplier = 2.5f;
        [SerializeField] private float lookSensitivity = 0.15f;
        [SerializeField] private float scrollVerticalSpeed = 6f;

        private float pitch;
        private float yaw;
        private bool isLooking;

        private void Start()
        {
            var euler = transform.eulerAngles;
            pitch = euler.x;
            yaw = euler.y;
        }

        private void Update()
        {
            if (InteractionLock.ShouldBlockCamera)
            {
                isLooking = false;
                return;
            }

            HandleLook();
            HandleMove();
        }

        private void HandleLook()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.rightButton.wasPressedThisFrame)
                isLooking = true;

            if (mouse.rightButton.wasReleasedThisFrame)
                isLooking = false;

            if (!isLooking)
                return;

            var delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch -= delta.y * lookSensitivity;
#else
            if (Input.GetMouseButtonDown(1))
                isLooking = true;

            if (Input.GetMouseButtonUp(1))
                isLooking = false;

            if (!isLooking)
                return;

            yaw += Input.GetAxis("Mouse X") * lookSensitivity * 10f;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity * 10f;
#endif

            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleMove()
        {
            var speed = moveSpeed;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null)
                return;

            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                speed *= fastMoveMultiplier;

            var moveInput = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                moveInput += transform.forward;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                moveInput -= transform.forward;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                moveInput -= transform.right;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                moveInput += transform.right;

            if (keyboard.qKey.isPressed)
                moveInput += Vector3.down;
            if (keyboard.eKey.isPressed)
                moveInput += Vector3.up;

            var pointerOverUi = UiPointerUtility.IsPointerOverUi();

            if (mouse != null && !pointerOverUi)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    moveInput += Vector3.up * Mathf.Sign(scroll);
            }
#else
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                speed *= fastMoveMultiplier;

            var moveInput = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                moveInput += transform.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                moveInput -= transform.forward;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveInput -= transform.right;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveInput += transform.right;
            if (Input.GetKey(KeyCode.Q))
                moveInput += Vector3.down;
            if (Input.GetKey(KeyCode.E))
                moveInput += Vector3.up;

            if (!UiPointerUtility.IsPointerOverUi())
            {
                moveInput += Vector3.up * Input.mouseScrollDelta.y;
            }
#endif

            if (moveInput.sqrMagnitude < 0.001f)
                return;

            transform.position += moveInput.normalized * (speed * Time.deltaTime);

            if (Mathf.Abs(scrollVerticalSpeed) > 0f && moveInput.y != 0f && moveInput.x == 0f && moveInput.z == 0f)
            {
                // Pure vertical movement already handled above.
            }
        }
    }
}
