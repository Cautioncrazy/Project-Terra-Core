#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxel;
using Core;

public class TerraCoreWindow : EditorWindow
{
    private VoxelEngine engine;
    private Vector2 scrollPos;

    [MenuItem("TerraCore/World Generator Settings")]
    public static void ShowWindow()
    {
        GetWindow<TerraCoreWindow>("TerraCore Gen");
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("World Generation Settings", EditorStyles.boldLabel);

        if (engine == null)
        {
#if UNITY_2023_1_OR_NEWER
            engine = Object.FindFirstObjectByType<VoxelEngine>();
#else
            engine = Object.FindObjectOfType<VoxelEngine>();
#endif
        }

        if (engine == null)
        {
            EditorGUILayout.HelpBox("No VoxelEngine found in scene. Please run 'TerraCore -> Setup Test Scene' first.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        // Config Sliders
        EditorGUI.BeginChangeCheck();
        int newSeed = EditorGUILayout.IntField("Seed", engine.config.seed);
        bool seedChanged = EditorGUI.EndChangeCheck();

        if (seedChanged)
        {
            engine.config.seed = newSeed;
            engine.ApplySeedToConfig();
        }

        if (GUILayout.Button("Randomize Planet"))
        {
            engine.config.seed = Random.Range(0, 999999);
            engine.ApplySeedToConfig();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Camera Settings", EditorStyles.label);

        // Find player to update zoom sensitivity
#if UNITY_2023_1_OR_NEWER
        var player = Object.FindFirstObjectByType<Player.PlayerController>();
#else
        var player = Object.FindObjectOfType<Player.PlayerController>();
#endif
        if (player != null)
        {
            player.zoomSensitivity = EditorGUILayout.Slider("Zoom Sensitivity", player.zoomSensitivity, 1f, 100f);

            EditorGUILayout.Space();
            GUILayout.Label("Painting", EditorStyles.label);

            bool newPaintState = EditorGUILayout.Toggle("Paint Mode", player.isPaintMode);
            if (newPaintState != player.isPaintMode) player.isPaintMode = newPaintState;

            if (player.isPaintMode)
            {
                player.isBucketMode = EditorGUILayout.Toggle("Bucket / Pour Mode", player.isBucketMode);

                GUILayout.BeginHorizontal();
                DrawBlockButton(player, "Stone", VoxelData.Stone, Color.gray);
                DrawBlockButton(player, "Dirt", VoxelData.Dirt, new Color(0.6f, 0.4f, 0.2f));
                DrawBlockButton(player, "Sand", VoxelData.Sand, new Color(0.95f, 0.9f, 0.6f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                DrawBlockButton(player, "Grass", VoxelData.Grass, new Color(0.3f, 0.7f, 0.2f));
                DrawBlockButton(player, "Snow", VoxelData.Snow, Color.white);
                DrawBlockButton(player, "Magma", VoxelData.Magma, new Color(1.0f, 0.4f, 0.0f));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                DrawBlockButton(player, "Water", VoxelData.Water, Color.blue);
                DrawBlockButton(player, "Bedrock", VoxelData.Bedrock, Color.black);
                DrawBlockButton(player, "Air", VoxelData.Air, Color.cyan);
                GUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space();
        GUILayout.Label("Dimensions", EditorStyles.label);
        engine.config.worldSize = EditorGUILayout.IntSlider("World Size (Chunks)", engine.config.worldSize, 1, 8);

        EditorGUI.BeginChangeCheck();
        int rad = EditorGUILayout.IntSlider("Planet Radius", engine.config.planetRadius, 10, 60);
        float sea = EditorGUILayout.Slider("Sea Level (Height)", engine.config.seaLevel, 10, 70);

        EditorGUILayout.Space();
        GUILayout.Label("Noise / Terrain", EditorStyles.label);
        float freq = EditorGUILayout.Slider("Mountain Scale (Freq)", engine.config.noiseFrequency, 0.01f, 0.2f);
        float amp = EditorGUILayout.Slider("Mountain Height (Amp)", engine.config.noiseAmplitude, 0f, 30f);
        float ocean = EditorGUILayout.Slider("Ocean Depth", engine.config.oceanDepth, 0f, 30f);
        float cont = EditorGUILayout.Slider("Land Size %", engine.config.continentThreshold, 0f, 1f);
        float cave = EditorGUILayout.Slider("Cave Density", engine.config.caveThreshold, 0f, 1f);
        float caveScale = EditorGUILayout.Slider("Cave Scale", engine.config.caveFrequency, 0.01f, 0.2f);

        bool paramsChanged = EditorGUI.EndChangeCheck();

        if (paramsChanged)
        {
            engine.config.planetRadius = rad;
            engine.config.seaLevel = sea;
            engine.config.noiseFrequency = freq;
            engine.config.noiseAmplitude = amp;
            engine.config.oceanDepth = ocean;
            engine.config.continentThreshold = cont;
            engine.config.caveThreshold = cave;
            engine.config.caveFrequency = caveScale;

            // Note: We intentionally DO NOT update the seed when parameters change.
            // This allows the user to tweak values (e.g. Sea Level) without re-randomizing the entire noise field.
            // The Seed is now the "Master" preset key, but parameters are independent tweaks.
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate World", GUILayout.Height(40)))
        {
            engine.GenerateWorld();
        }

        EditorGUILayout.Space();
        GUILayout.Label("Generation Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Base (Shape)", GUILayout.Height(30)))
        {
            engine.GenerateBaseShape();
        }
        if (GUILayout.Button("Generate Terrain", GUILayout.Height(30)))
        {
            engine.GenerateTerrain();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        if (GUILayout.Button(engine.splitView ? "Disable Split View" : "Enable Split View", GUILayout.Height(25)))
        {
            engine.ToggleSplitView();
        }

        EditorGUILayout.Space();

        // Physics Simulation
        var sim = engine.GetComponent<Simulation.FluidSimulation>();
        if (sim != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Physics", EditorStyles.boldLabel, GUILayout.Width(60));
            if (GUILayout.Button("Simulate 1 Tick", GUILayout.Height(20)))
            {
                sim.SimulationTick();
            }
            GUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("EMERGENCY RESET", GUILayout.Height(25)))
        {
            engine.EmergencyReset();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    void DrawBlockButton(Player.PlayerController player, string name, byte blockId, Color col)
    {
        GUI.backgroundColor = col;
        if (blockId == player.selectedBlock)
        {
            // Highlight selected
             if (GUILayout.Button($"[{name}]", GUILayout.Width(60)))
             {
                 player.selectedBlock = blockId;
             }
        }
        else
        {
             if (GUILayout.Button(name, GUILayout.Width(60)))
             {
                 player.selectedBlock = blockId;
             }
        }
        GUI.backgroundColor = Color.white;
    }
}
#endif
