using UnityEngine;
using UnityEngine.InputSystem;

namespace PlacementSystem
{
    [RequireComponent(typeof(Camera))]
    public class FlyingCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float fastMoveMultiplier = 2.5f;
        [SerializeField] private float lookSensitivity = 0.15f;
        [Tooltip("How far (metres) one notch of the mouse wheel moves the camera up / down. Shift multiplies it too.")]
        [SerializeField] private float scrollStep = 1f;

        private float pitch;
        private float yaw;
        private bool isLooking;

        private void Start()
        {
            var euler = transform.eulerAngles;
            pitch = euler.x;
            yaw = euler.y;
        }

        /// <summary>Moves the camera (e.g. when a project is loaded) and keeps mouse-look in sync.</summary>
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);

            var euler = rotation.eulerAngles;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
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

            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleMove()
        {
            var speed = moveSpeed;

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

            // The wheel is a step per notch, not a speed: a notch lasts a single
            // frame, so adding it to the per-second movement moved only millimetres.
            if (mouse != null && !pointerOverUi)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    transform.position += Vector3.up * (Mathf.Sign(scroll) * scrollStep * (speed / moveSpeed));
            }

            if (moveInput.sqrMagnitude < 0.001f)
                return;

            transform.position += moveInput.normalized * (speed * Time.deltaTime);
        }
    }
}
