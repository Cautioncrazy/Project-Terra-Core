using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Voxel
{
    public class VoxelEngine : MonoBehaviour
    {
        [Header("World Settings")]
        public int worldSize = 4; // Dimensions in Chunks (4x4x4)
        public int planetRadius = 24; // Radius in blocks

        [Header("References")]
        public Material chunkMaterial;

        private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();

        void Start()
        {
            GenerateWorld();
        }

        void GenerateWorld()
        {
            // Center of the world in block coordinates
            Vector3 center = new Vector3(worldSize * VoxelData.ChunkWidth / 2f,
                                         worldSize * VoxelData.ChunkHeight / 2f,
                                         worldSize * VoxelData.ChunkDepth / 2f);

            for (int x = 0; x < worldSize; x++)
            {
                for (int y = 0; y < worldSize; y++)
                {
                    for (int z = 0; z < worldSize; z++)
                    {
                        CreateChunk(x, y, z, center);
                    }
                }
            }
        }

        void CreateChunk(int x, int y, int z, Vector3 center)
        {
            Vector3Int chunkCoord = new Vector3Int(x * VoxelData.ChunkWidth, y * VoxelData.ChunkHeight, z * VoxelData.ChunkDepth);

            GameObject go = new GameObject($"Chunk_{x}_{y}_{z}");
            go.transform.parent = this.transform;
            go.transform.position = chunkCoord;

            Chunk chunk = go.AddComponent<Chunk>();

            if (chunkMaterial != null)
            {
                chunk.GetComponent<MeshRenderer>().material = chunkMaterial;
            }

            chunk.Initialize(this, chunkCoord);

            PopulateChunk(chunk, chunkCoord, center);

            chunk.GenerateMesh();

            chunks.Add(chunkCoord, chunk);
        }

        void PopulateChunk(Chunk chunk, Vector3Int chunkPos, Vector3 center)
        {
            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelData.ChunkDepth; z++)
                    {
                        Vector3Int globalPos = chunkPos + new Vector3Int(x, y, z);
                        float dist = Vector3.Distance(globalPos, center);

                        // 1. Terrain Height Noise (Continents)
                        float terrainNoise = Mathf.PerlinNoise(globalPos.x * 0.05f, globalPos.z * 0.05f);
                        float elevation = terrainNoise * 8f; // Height variance

                        float surfaceRadius = planetRadius + elevation;

                        // 2. Canyon/Cave Noise (3D Perlin approximation)
                        float caveNoise = Mathf.PerlinNoise(globalPos.x * 0.1f, globalPos.y * 0.1f) * Mathf.PerlinNoise(globalPos.y * 0.1f, globalPos.z * 0.1f);
                        bool isCave = caveNoise > 0.65f;

                        if (dist <= surfaceRadius)
                        {
                            if (isCave && dist > 10) // Don't cave into core
                            {
                                // Cave or Canyon -> Water if below sea level, Air if above
                                if (dist <= planetRadius + 2) // Water level slightly above base radius
                                    chunk.SetBlock(x, y, z, VoxelData.Water, false);
                                else
                                    chunk.SetBlock(x, y, z, VoxelData.Air, false);
                            }
                            else
                            {
                                // Solid Terrain
                                if (dist < 10)
                                {
                                    chunk.SetBlock(x, y, z, VoxelData.Bedrock, false);
                                }
                                else if (dist > surfaceRadius - 2) // Top 2 layers
                                {
                                    // Biome logic based on height
                                    if (dist < planetRadius + 3) // Near water level -> Sand
                                        chunk.SetBlock(x, y, z, VoxelData.Sand, false);
                                    else if (dist < planetRadius + 6) // Mid elevation -> Grass
                                        chunk.SetBlock(x, y, z, VoxelData.Grass, false);
                                    else // High elevation -> Stone/Mountain
                                        chunk.SetBlock(x, y, z, VoxelData.Stone, false);
                                }
                                else if (dist > surfaceRadius - 5) // Sub-surface
                                {
                                    chunk.SetBlock(x, y, z, VoxelData.Dirt, false);
                                }
                                else
                                {
                                    chunk.SetBlock(x, y, z, VoxelData.Stone, false);
                                }
                            }
                        }
                        else
                        {
                            // Water check outside terrain surface but within "Sea Level"
                            if (dist <= planetRadius + 2) // Sea Level
                            {
                                chunk.SetBlock(x, y, z, VoxelData.Water, false);
                            }
                            else
                            {
                                chunk.SetBlock(x, y, z, VoxelData.Air, false);
                            }
                        }
                    }
                }
            }
        }

        public byte GetBlock(Vector3Int globalPos)
        {
            int cx = Mathf.FloorToInt((float)globalPos.x / VoxelData.ChunkWidth) * VoxelData.ChunkWidth;
            int cy = Mathf.FloorToInt((float)globalPos.y / VoxelData.ChunkHeight) * VoxelData.ChunkHeight;
            int cz = Mathf.FloorToInt((float)globalPos.z / VoxelData.ChunkDepth) * VoxelData.ChunkDepth;

            Vector3Int chunkCoord = new Vector3Int(cx, cy, cz);

            if (chunks.ContainsKey(chunkCoord))
            {
                Chunk chunk = chunks[chunkCoord];
                int lx = globalPos.x - cx;
                int ly = globalPos.y - cy;
                int lz = globalPos.z - cz;
                return chunk.GetBlock(lx, ly, lz);
            }
            return VoxelData.Air;
        }

        public void ModifyBlock(Vector3Int globalPos, byte type)
        {
            ModifyBlock(globalPos, type, true);
        }

        public void ModifyBlock(Vector3Int globalPos, byte type, bool regenerateMesh)
        {
            int cx = Mathf.FloorToInt((float)globalPos.x / VoxelData.ChunkWidth) * VoxelData.ChunkWidth;
            int cy = Mathf.FloorToInt((float)globalPos.y / VoxelData.ChunkHeight) * VoxelData.ChunkHeight;
            int cz = Mathf.FloorToInt((float)globalPos.z / VoxelData.ChunkDepth) * VoxelData.ChunkDepth;

            Vector3Int chunkCoord = new Vector3Int(cx, cy, cz);

            if (chunks.ContainsKey(chunkCoord))
            {
                Chunk chunk = chunks[chunkCoord];
                int lx = globalPos.x - cx;
                int ly = globalPos.y - cy;
                int lz = globalPos.z - cz;

                chunk.SetBlock(lx, ly, lz, type, regenerateMesh);

                if (regenerateMesh)
                    UpdateNeighborChunks(chunkCoord, lx, ly, lz);
            }
        }

        public Chunk GetChunk(Vector3Int chunkCoord)
        {
            if (chunks.ContainsKey(chunkCoord)) return chunks[chunkCoord];
            return null;
        }

        public void UpdateNeighborChunks(Vector3Int chunkCoord, int lx, int ly, int lz)
        {
            // Simple neighbor check
            if (lx == 0) UpdateChunkMesh(chunkCoord + new Vector3Int(-VoxelData.ChunkWidth, 0, 0));
            if (lx == VoxelData.ChunkWidth - 1) UpdateChunkMesh(chunkCoord + new Vector3Int(VoxelData.ChunkWidth, 0, 0));

            if (ly == 0) UpdateChunkMesh(chunkCoord + new Vector3Int(0, -VoxelData.ChunkHeight, 0));
            if (ly == VoxelData.ChunkHeight - 1) UpdateChunkMesh(chunkCoord + new Vector3Int(0, VoxelData.ChunkHeight, 0));

            if (lz == 0) UpdateChunkMesh(chunkCoord + new Vector3Int(0, 0, -VoxelData.ChunkDepth));
            if (lz == VoxelData.ChunkDepth - 1) UpdateChunkMesh(chunkCoord + new Vector3Int(0, 0, VoxelData.ChunkDepth));
        }

        private void UpdateChunkMesh(Vector3Int chunkCoord)
        {
            if (chunks.ContainsKey(chunkCoord))
            {
                chunks[chunkCoord].GenerateMesh();
            }
        }

        public IEnumerable<Chunk> GetAllChunks()
        {
            return chunks.Values;
        }
    }
}
