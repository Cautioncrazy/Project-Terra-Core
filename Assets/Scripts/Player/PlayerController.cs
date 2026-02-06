using UnityEngine;
using UnityEngine.InputSystem;
using Voxel;
using Core;
using Security;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        public VoxelEngine engine;
        public Camera cam;

        [Header("Settings")]
        public float rotateSpeed = 0.5f; // Reduced for Input System delta values
        public float zoomSpeed = 0.01f; // Reduced for Input System scroll values
        [Range(1f, 100f)]
        public float zoomSensitivity = 10f; // Multiplier for zoom

        [Header("Tool Heat")]
        public float heatPerClick = 10f;
        public float heatCoolRate = 20f;
        public float maxHeat = 100f;
        private float currentHeat = 0f;
        private bool isOverheated = false;

        void Update()
        {
            // Cooldown
            if (!isOverheated && currentHeat > 0)
            {
                currentHeat -= heatCoolRate * Time.deltaTime;
                if (currentHeat < 0) currentHeat = 0;
            }

            if (Mouse.current != null)
            {
                HandleCamera();
                HandleInput();
            }
        }

        void HandleCamera()
        {
            // Ensure camera orbits the center of the world
            if (engine != null)
            {
                Vector3 center = engine.GetWorldCenter();
                if (Vector3.Distance(transform.position, center) > 0.01f)
                {
                    transform.position = center;
                }
            }

            // Orbit around local Y axis (assuming script is attached to a pivot object)
            if (Mouse.current.rightButton.isPressed)
            {
                float h = Mouse.current.delta.x.ReadValue();
                float v = Mouse.current.delta.y.ReadValue();

                // Rotate around world up (Yaw)
                transform.Rotate(Vector3.up, h * rotateSpeed, Space.World);

                // Rotate around local right (Pitch) - Inverted Y for natural feel
                transform.Rotate(Vector3.right, -v * rotateSpeed, Space.Self);
            }

            // Zoom
            float scroll = Mouse.current.scroll.y.ReadValue();
            if (scroll != 0 && cam != null)
            {
                float multiplier = 1f;
                if (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
                {
                    multiplier = 10f;
                }

                cam.transform.Translate(Vector3.forward * scroll * zoomSpeed * multiplier * zoomSensitivity, Space.Self);
            }
        }

        void HandleInput()
        {
            if (isOverheated) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (SecurityManager.Instance != null && !SecurityManager.Instance.ValidateInput())
                    return;

                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100f))
                {
                    // Hit mesh
                    // To destroy: Move slightly inside the block along normal (opposite to normal).
                    Vector3 insidePoint = hit.point - hit.normal * 0.1f;

                    Vector3Int blockPos = Vector3Int.FloorToInt(insidePoint);

                    Dig(blockPos);
                }
            }
        }

        void Dig(Vector3Int pos)
        {
            if (engine == null) return;

            byte block = engine.GetBlock(pos);

            if (block == VoxelData.Bedrock)
            {
                // Indestructible
                return;
            }

            if (block != VoxelData.Air)
            {
                engine.ModifyBlock(pos, VoxelData.Air);

                // Add Heat
                currentHeat += heatPerClick;
                if (currentHeat >= maxHeat)
                {
                    currentHeat = maxHeat;
                    isOverheated = true;
                    Debug.Log("Tool Overheated! Disabled for 3 seconds.");
                    Invoke("ResetOverheat", 3.0f);
                }
            }
        }

        void ResetOverheat()
        {
            isOverheated = false;
            currentHeat = 0;
            Debug.Log("Tool Cooled Down.");
        }
    }
}
