using UnityEngine;

namespace Core
{
    public static class VoxelData
    {
        // Block IDs
        public const byte Air = 0;
        public const byte Dirt = 1;
        public const byte Stone = 2;
        public const byte Water = 3;
        public const byte Bedrock = 4;

        // Chunk Dimensions
        public const int ChunkWidth = 16;
        public const int ChunkHeight = 16;
        public const int ChunkDepth = 16;

        // Neighbor Directions (for face checking)
        public static readonly Vector3Int[] FaceChecks = new Vector3Int[]
        {
            new Vector3Int(0, 0, -1), // Back
            new Vector3Int(0, 0, 1),  // Front
            new Vector3Int(0, 1, 0),  // Top
            new Vector3Int(0, -1, 0), // Bottom
            new Vector3Int(-1, 0, 0), // Left
            new Vector3Int(1, 0, 0)   // Right
        };

        // Voxel Vertices (Relative to local origin 0,0,0)
        public static readonly Vector3[] VoxelVerts = new Vector3[]
        {
            new Vector3(0.0f, 0.0f, 0.0f), // 0
            new Vector3(1.0f, 0.0f, 0.0f), // 1
            new Vector3(1.0f, 1.0f, 0.0f), // 2
            new Vector3(0.0f, 1.0f, 0.0f), // 3
            new Vector3(0.0f, 0.0f, 1.0f), // 4
            new Vector3(1.0f, 0.0f, 1.0f), // 5
            new Vector3(1.0f, 1.0f, 1.0f), // 6
            new Vector3(0.0f, 1.0f, 1.0f)  // 7
        };

        // Voxel Face Triangles (Indices into VoxelVerts)
        // Order: Back, Front, Top, Bottom, Left, Right
        public static readonly int[,] VoxelTris = new int[,]
        {
            {0, 3, 1, 2}, // Back Face
            {5, 6, 4, 7}, // Front Face
            {3, 7, 2, 6}, // Top Face
            {1, 5, 0, 4}, // Bottom Face
            {4, 7, 0, 3}, // Left Face
            {1, 2, 5, 6}  // Right Face
        };

        // Corresponding UV lookups (Placeholder for Texture Atlas logic)
        // In a real implementation, this would return UV coordinates for the mesh
        public static Vector2 GetUV(byte blockId)
        {
            // Simple placeholder logic
            switch (blockId)
            {
                case Dirt: return new Vector2(0.1f, 0.1f); // Brown
                case Stone: return new Vector2(0.3f, 0.1f); // Grey
                case Water: return new Vector2(0.5f, 0.1f); // Blue
                case Bedrock: return new Vector2(0.7f, 0.1f); // Black/Dark
                default: return Vector2.zero;
            }
        }

        public static Color GetColor(byte blockId)
        {
             switch (blockId)
            {
                case Dirt: return new Color(0.6f, 0.4f, 0.2f);
                case Stone: return Color.gray;
                case Water: return Color.blue;
                case Bedrock: return Color.black;
                default: return Color.clear;
            }
        }

        public static bool IsTransparent(byte blockId)
        {
            return blockId == Air || blockId == Water;
        }

        public static bool IsDestructible(byte blockId)
        {
            return blockId == Dirt || blockId == Stone;
        }
    }
}
