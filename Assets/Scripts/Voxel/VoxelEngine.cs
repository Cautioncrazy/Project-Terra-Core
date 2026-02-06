using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Voxel
{
    public class VoxelEngine : MonoBehaviour
    {
        [System.Serializable]
        public class WorldConfig
        {
            public int seed = 12345;
            public int worldSize = 4; // Dimensions in Chunks (4x4x4)
            public int planetRadius = 24; // Radius in blocks
            public float seaLevel = 26f; // Absolute height

            [Range(0.01f, 0.2f)] public float noiseFrequency = 0.03f;
            [Range(0f, 20f)] public float noiseAmplitude = 8f;
            [Range(0f, 1f)] public float caveThreshold = 0.65f;
            [Range(0f, 1f)] public float continentThreshold = 0.4f; // Controls land/water ratio
        }

        public WorldConfig config = new WorldConfig();

        [Header("References")]
        public Material chunkMaterial;

        private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();

        void Start()
        {
            GenerateWorld();
        }

        public void ApplySeedToConfig()
        {
            Random.InitState(config.seed);

            // Generate randomized properties based on seed
            config.planetRadius = Random.Range(15, 55);
            config.seaLevel = config.planetRadius + Random.Range(-5, 10);

            config.noiseFrequency = Random.Range(0.02f, 0.15f);
            config.noiseAmplitude = Random.Range(4f, 12f);

            config.continentThreshold = Random.Range(0.3f, 0.7f);
            config.caveThreshold = Random.Range(0.5f, 0.8f);

            // Ensure sea level is somewhat logical
            if (config.seaLevel < config.planetRadius - 5) config.seaLevel = config.planetRadius - 5;
        }

        public void GenerateWorld()
        {
            // Clean up existing chunks
            foreach (var chunk in chunks.Values)
            {
                if (chunk != null)
                {
                    if (Application.isPlaying)
                        Destroy(chunk.gameObject);
                    else
                        DestroyImmediate(chunk.gameObject);
                }
            }
            chunks.Clear();

            Random.InitState(config.seed);

            // Center of the world in block coordinates
            Vector3 center = GetWorldCenter();

            for (int x = 0; x < config.worldSize; x++)
            {
                for (int y = 0; y < config.worldSize; y++)
                {
                    for (int z = 0; z < config.worldSize; z++)
                    {
                        CreateChunk(x, y, z, center);
                    }
                }
            }
        }

        public Vector3 GetWorldCenter()
        {
            return new Vector3(config.worldSize * VoxelData.ChunkWidth / 2f,
                               config.worldSize * VoxelData.ChunkHeight / 2f,
                               config.worldSize * VoxelData.ChunkDepth / 2f);
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
            float seedOffset = (float)config.seed * 0.1f;

            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelData.ChunkDepth; z++)
                    {
                        Vector3Int globalPos = chunkPos + new Vector3Int(x, y, z);
                        float dist = Vector3.Distance(globalPos, center);

                        // 1. Continent Shape (Low Frequency Noise)
                        // If low -> Deep Ocean, If high -> Land
                        float continentNoise = Mathf.PerlinNoise((globalPos.x + seedOffset) * 0.015f, (globalPos.z + seedOffset) * 0.015f);

                        // 2. Terrain Detail (Medium Frequency)
                        float detailNoise = Mathf.PerlinNoise((globalPos.x + seedOffset) * config.noiseFrequency, (globalPos.z + seedOffset) * config.noiseFrequency);

                        float elevation = 0;

                        if (continentNoise > config.continentThreshold)
                        {
                            // Land Mass
                            elevation = (continentNoise - config.continentThreshold) * config.noiseAmplitude * 2 + (detailNoise * config.noiseAmplitude);
                        }
                        else
                        {
                            // Ocean Bed
                            elevation = -((config.continentThreshold - continentNoise) * config.noiseAmplitude);
                        }

                        float surfaceRadius = config.planetRadius + elevation;

                        // 3. Caves
                        float caveNoise = Mathf.PerlinNoise((globalPos.x + seedOffset) * 0.1f, (globalPos.y + seedOffset) * 0.1f) *
                                          Mathf.PerlinNoise((globalPos.y + seedOffset) * 0.1f, (globalPos.z + seedOffset) * 0.1f);
                        bool isCave = caveNoise > config.caveThreshold;

                        // Determine Block Type
                        if (dist <= surfaceRadius)
                        {
                            if (isCave && dist > 10)
                            {
                                // Cave -> Water if below sea level
                                if (dist <= config.seaLevel)
                                    chunk.SetBlock(x, y, z, VoxelData.Water, false);
                                else
                                    chunk.SetBlock(x, y, z, VoxelData.Air, false);
                            }
                            else
                            {
                                // Solid
                                if (dist < 10)
                                {
                                    chunk.SetBlock(x, y, z, VoxelData.Bedrock, false);
                                }
                                else if (dist > surfaceRadius - 3) // Surface Layer
                                {
                                    if (dist < config.seaLevel + 2)
                                        chunk.SetBlock(x, y, z, VoxelData.Sand, false);
                                    else if (dist < config.seaLevel + 8)
                                        chunk.SetBlock(x, y, z, VoxelData.Grass, false);
                                    else
                                        chunk.SetBlock(x, y, z, VoxelData.Stone, false);
                                }
                                else if (dist > surfaceRadius - 8)
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
                            if (dist <= config.seaLevel)
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
