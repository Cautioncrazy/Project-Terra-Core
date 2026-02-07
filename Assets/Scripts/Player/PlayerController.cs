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

        [Header("Paint Mode")]
        public bool isPaintMode = false;
        public bool isBucketMode = false; // Continuous Pour
        public byte selectedBlock = VoxelData.Stone;
        private GameObject cursor;
        private float paintTimer = 0f;
        private float paintInterval = 0.05f; // 20 blocks/sec

        void Start()
        {
             CreateCursor();
        }

        void CreateCursor()
        {
            if (cursor == null)
            {
                cursor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cursor.name = "Cursor";
                Destroy(cursor.GetComponent<BoxCollider>());
                cursor.transform.localScale = Vector3.one * 1.1f;
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(1, 1, 0, 0.5f);
                // Make transparent
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                cursor.GetComponent<MeshRenderer>().material = mat;
                cursor.SetActive(false);
            }
        }

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
                HandleCursor();
            }
        }

        void HandleCursor()
        {
            if (!isPaintMode || cam == null || Mouse.current == null)
            {
                if (cursor != null) cursor.SetActive(false);
                return;
            }

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                Vector3 insidePoint = hit.point - hit.normal * 0.1f;
                Vector3Int pos = Vector3Int.FloorToInt(insidePoint);

                if (cursor != null)
                {
                    cursor.SetActive(true);
                    cursor.transform.position = pos + new Vector3(0.5f, 0.5f, 0.5f);
                }
            }
            else
            {
                if (cursor != null) cursor.SetActive(false);
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

            bool isClick = Mouse.current.leftButton.wasPressedThisFrame;
            bool isHold = Mouse.current.leftButton.isPressed;

            // Digging (Click only)
            if (!isPaintMode && isClick)
            {
                if (SecurityManager.Instance != null && !SecurityManager.Instance.ValidateInput()) return;
                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 100f))
                {
                    Vector3 insidePoint = hit.point - hit.normal * 0.1f;
                    Vector3Int blockPos = Vector3Int.FloorToInt(insidePoint);
                    Dig(blockPos);
                }
            }
            // Paint Mode
            else if (isPaintMode)
            {
                if (cam == null) return;

                // Bucket Mode (Continuous Pour)
                if (isBucketMode && isHold)
                {
                    paintTimer += Time.deltaTime;
                    if (paintTimer >= paintInterval)
                    {
                        paintTimer = 0;
                        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                        RaycastHit hit;
                        if (Physics.Raycast(ray, out hit, 100f))
                        {
                            // Spawn OUTSIDE the mesh (build up)
                            Vector3 outsidePoint = hit.point + hit.normal * 0.5f;
                            Vector3Int blockPos = Vector3Int.FloorToInt(outsidePoint);
                            Paint(blockPos);
                        }
                    }
                }
                // Standard Paint (Single Click Replace)
                else if (!isBucketMode && isClick)
                {
                    Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 100f))
                    {
                        // Replace INSIDE the mesh
                        Vector3 insidePoint = hit.point - hit.normal * 0.1f;
                        Vector3Int blockPos = Vector3Int.FloorToInt(insidePoint);
                        Paint(blockPos);
                    }
                }
            }
        }

        void Paint(Vector3Int pos)
        {
            if (engine == null) return;
            // Modify/Place block
            // Note: If Bucket Mode places sand in air, FluidSimulation will move it next tick.
            engine.ModifyBlock(pos, selectedBlock);
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

                // Add Heat (Only digging generates heat?)
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
