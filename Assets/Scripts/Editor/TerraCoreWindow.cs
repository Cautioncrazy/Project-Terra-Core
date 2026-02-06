#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Voxel;

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
            engine = FindObjectOfType<VoxelEngine>();
        }

        if (engine == null)
        {
            EditorGUILayout.HelpBox("No VoxelEngine found in scene. Please run 'TerraCore -> Setup Test Scene' first.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        // Config Sliders
        engine.config.seed = EditorGUILayout.IntField("Seed", engine.config.seed);
        if (GUILayout.Button("Random Seed"))
        {
            engine.config.seed = Random.Range(0, 999999);
        }

        EditorGUILayout.Space();
        GUILayout.Label("Dimensions", EditorStyles.label);
        engine.config.worldSize = EditorGUILayout.IntSlider("World Size (Chunks)", engine.config.worldSize, 1, 8);
        engine.config.planetRadius = EditorGUILayout.IntSlider("Planet Radius", engine.config.planetRadius, 10, 60);
        engine.config.seaLevel = EditorGUILayout.Slider("Sea Level (Height)", engine.config.seaLevel, 10, 70);

        EditorGUILayout.Space();
        GUILayout.Label("Noise / Terrain", EditorStyles.label);
        engine.config.noiseFrequency = EditorGUILayout.Slider("Terrain Frequency", engine.config.noiseFrequency, 0.01f, 0.2f);
        engine.config.noiseAmplitude = EditorGUILayout.Slider("Terrain Amplitude", engine.config.noiseAmplitude, 0f, 20f);
        engine.config.continentThreshold = EditorGUILayout.Slider("Continent Threshold", engine.config.continentThreshold, 0f, 1f);
        engine.config.caveThreshold = EditorGUILayout.Slider("Cave Threshold", engine.config.caveThreshold, 0f, 1f);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate World", GUILayout.Height(40)))
        {
            engine.GenerateWorld();
        }

        EditorGUILayout.EndScrollView();
    }
}
#endif
