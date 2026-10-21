using UnityEngine;
using UnityEngine.InputSystem;
using NotMyBlood.Core;

namespace NotMyBlood.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Crouch / Photo Settings")]
        [SerializeField] private float crouchSpeed = 2.0f;
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private float crouchTransitionSpeed = 10.0f;

        [Header("Mouse Look Settings")]
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private Transform cameraHolder;

        // Private State Variables
        private CharacterController characterController;
        private Vector3 velocity;
        private float xRotation = 0f;
        private bool isCrouching = false;
        private float targetHeight;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            targetHeight = standingHeight;
        }

        private void Start()
        {
            // Lock Mouse Cursor to Screen
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleMouseLook();
            HandleMovement();
            HandleCrouch();
        }

        private void HandleMouseLook()
        {
            if (Mouse.current == null) return;
            if (UIState.BlocksPlayerInput) return; // z. B. Notizbuch offen

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -85f, 85f);

            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        private void HandleMovement()
        {
            // Reset gravity if grounded
            if (characterController.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            float x = 0f;
            float z = 0f;

            if (Keyboard.current != null && !UIState.BlocksPlayerInput)
            {
                if (Keyboard.current.wKey.isPressed) z += 1f;
                if (Keyboard.current.sKey.isPressed) z -= 1f;
                if (Keyboard.current.aKey.isPressed) x -= 1f;
                if (Keyboard.current.dKey.isPressed) x += 1f;
            }

            Vector3 move = transform.right * x + transform.forward * z;
            if (move.magnitude > 1f) move.Normalize();

            float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;
            characterController.Move(move * currentSpeed * Time.deltaTime);

            // Apply Gravity (No Jumping!)
            velocity.y += gravity * Time.deltaTime;
            characterController.Move(velocity * Time.deltaTime);
        }

        private void HandleCrouch()
        {
            if (Keyboard.current == null) return;

            // Toggle Crouch with Left Shift
            if (!UIState.BlocksPlayerInput && Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                isCrouching = !isCrouching;
                targetHeight = isCrouching ? crouchHeight : standingHeight;
            }

            // Smoothly lerp height transition
            float currentHeight = characterController.height;
            characterController.height = Mathf.Lerp(currentHeight, targetHeight, Time.deltaTime * crouchTransitionSpeed);

            // Adjust Camera position relative to height
            Vector3 cameraPos = cameraHolder.localPosition;
            cameraPos.y = characterController.height * 0.45f;
            cameraHolder.localPosition = cameraPos;
        }
    }
}