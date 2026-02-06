using UnityEngine;
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
        public float rotateSpeed = 5.0f;
        public float zoomSpeed = 5.0f;

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

            HandleCamera();
            HandleInput();
        }

        void HandleCamera()
        {
            // Orbit around local Y axis (assuming script is attached to a pivot object)
            if (Input.GetMouseButton(1)) // Right click rotate
            {
                float h = Input.GetAxis("Mouse X");

                // Rotate around world up or local up? World up for consistent horizon.
                transform.Rotate(Vector3.up, h * rotateSpeed, Space.World);
            }

            // Zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0 && cam != null)
            {
                cam.transform.Translate(Vector3.forward * scroll * zoomSpeed, Space.Self);
            }
        }

        void HandleInput()
        {
            if (isOverheated) return;

            if (Input.GetMouseButtonDown(0)) // Left click dig
            {
                if (SecurityManager.Instance != null && !SecurityManager.Instance.ValidateInput())
                    return;

                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
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
