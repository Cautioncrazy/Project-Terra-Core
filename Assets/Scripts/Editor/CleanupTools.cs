#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CleanupTools : EditorWindow
{
    [MenuItem("TerraCore/Cleanup Missing Scripts")]
    public static void CleanUp()
    {
        // 1. Find all GameObjects in scene
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        int count = 0;

        foreach (GameObject go in allObjects)
        {
            if (go.hideFlags == HideFlags.None || go.hideFlags == HideFlags.HideInHierarchy) // Only scene objects
            {
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    Debug.Log($"Removed {removed} missing scripts from {go.name}");
                    count += removed;
                }
            }
        }

        Debug.Log($"Cleanup Complete. Removed {count} missing script components.");
    }
}
#endif
