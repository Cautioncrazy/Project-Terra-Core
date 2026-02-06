using UnityEngine;
using UnityEditor;
using Voxel;
using Simulation;
using Player;
using Security;

public class SceneSetup : MonoBehaviour
{
    [MenuItem("TerraCore/Setup Test Scene")]
    static void SetupScene()
    {
        // 1. Setup Voxel Engine / Managers
        GameObject managers = GameObject.Find("Managers");
        if (managers == null)
        {
            managers = new GameObject("Managers");
            Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
        }

        VoxelEngine engine = managers.GetComponent<VoxelEngine>();
        if (engine == null) engine = Undo.AddComponent<VoxelEngine>(managers);

        FluidSimulation fluid = managers.GetComponent<FluidSimulation>();
        if (fluid == null) fluid = Undo.AddComponent<FluidSimulation>(managers);

        SecurityManager sec = managers.GetComponent<SecurityManager>();
        if (sec == null) sec = Undo.AddComponent<SecurityManager>(managers);

        // Create a default material if needed
        if (engine.chunkMaterial == null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.name = "VoxelMaterial";
            // Assign a basic color or texture setup if we had assets
            engine.chunkMaterial = mat;
            // Ideally we'd save this material asset, but for a runtime setup script we can keep it in memory or user assigns it.
            Debug.Log("Created temporary material for VoxelEngine. Please assign a saved material in Inspector.");
        }

        // 2. Setup Player
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            // Set pivot for camera orbit
            player.transform.position = new Vector3(32, 32, 32); // Center of 4x4 chunks (approx)
        }

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc == null) pc = Undo.AddComponent<PlayerController>(player);

        pc.engine = engine;

        // 3. Setup Camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            Undo.RegisterCreatedObjectUndo(camObj, "Create Camera");
        }

        // Parent camera to player for orbit
        cam.transform.SetParent(player.transform);
        cam.transform.localPosition = new Vector3(0, 20, -20); // Isometric offset
        cam.transform.LookAt(player.transform);

        pc.cam = cam;

        // 4. Lights
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
            Undo.RegisterCreatedObjectUndo(lightObj, "Create Light");
        }

        Debug.Log("Terra Core Scene Setup Complete!");
    }
}
