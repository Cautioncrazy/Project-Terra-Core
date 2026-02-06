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
        GUILayout.Label("Dimensions", EditorStyles.label);
        engine.config.worldSize = EditorGUILayout.IntSlider("World Size (Chunks)", engine.config.worldSize, 1, 8);

        EditorGUI.BeginChangeCheck();
        int rad = EditorGUILayout.IntSlider("Planet Radius", engine.config.planetRadius, 10, 60);
        float sea = EditorGUILayout.Slider("Sea Level (Height)", engine.config.seaLevel, 10, 70);

        EditorGUILayout.Space();
        GUILayout.Label("Noise / Terrain", EditorStyles.label);
        float freq = EditorGUILayout.Slider("Terrain Frequency", engine.config.noiseFrequency, 0.01f, 0.2f);
        float amp = EditorGUILayout.Slider("Terrain Amplitude", engine.config.noiseAmplitude, 0f, 20f);
        float cont = EditorGUILayout.Slider("Continent Threshold", engine.config.continentThreshold, 0f, 1f);
        float cave = EditorGUILayout.Slider("Cave Threshold", engine.config.caveThreshold, 0f, 1f);

        bool paramsChanged = EditorGUI.EndChangeCheck();

        if (paramsChanged)
        {
            engine.config.planetRadius = rad;
            engine.config.seaLevel = sea;
            engine.config.noiseFrequency = freq;
            engine.config.noiseAmplitude = amp;
            engine.config.continentThreshold = cont;
            engine.config.caveThreshold = cave;

            // Reverse Seed: Update seed based on params so "changing sliders changes seed"
            // Simple hash combination
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + rad.GetHashCode();
                hash = hash * 23 + sea.GetHashCode();
                hash = hash * 23 + freq.GetHashCode();
                hash = hash * 23 + amp.GetHashCode();
                hash = hash * 23 + cont.GetHashCode();
                engine.config.seed = System.Math.Abs(hash % 999999);
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate World", GUILayout.Height(40)))
        {
            engine.GenerateWorld();
        }

        EditorGUILayout.EndScrollView();
    }
}
#endif
