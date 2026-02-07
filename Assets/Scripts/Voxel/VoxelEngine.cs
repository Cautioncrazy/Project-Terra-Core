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
        public Material chunkMaterial; // Opaque
        public Material waterMaterial; // Transparent

        private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();

        public bool splitView = false;

        void Start()
        {
            GenerateWorld();
        }

        public void ToggleSplitView()
        {
            splitView = !splitView;
            foreach (var chunk in chunks.Values)
            {
                chunk.GenerateMesh();
            }
        }

        public bool IsClipped(Vector3Int globalPos)
        {
            if (!splitView) return false;
            Vector3 center = GetWorldCenter();
            return globalPos.x > center.x;
        }

        public void EmergencyReset()
        {
            config.worldSize = 1;
            config.planetRadius = 8;
            config.seaLevel = 10;
            config.noiseAmplitude = 0;
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

        // Enum to control generation phase
        public enum GenMode { Full, ShapeOnly, TerrainOnly }

        public void GenerateWorld()
        {
            GenerateWorld(GenMode.Full);
        }

        public void GenerateBaseShape()
        {
            GenerateWorld(GenMode.ShapeOnly);
        }

        public void GenerateTerrain()
        {
             // For terrain only, we might want to keep existing chunks but re-populate?
             // For now, simpler to just regenerate everything with full logic.
             // But to simulate "Applying Terrain", we could just ensure we run the full logic.
             GenerateWorld(GenMode.Full);
        }

        public void GenerateWorld(GenMode mode)
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
                        CreateChunk(x, y, z, center, mode);
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

        void CreateChunk(int x, int y, int z, Vector3 center, GenMode mode)
        {
            Vector3Int chunkCoord = new Vector3Int(x * VoxelData.ChunkWidth, y * VoxelData.ChunkHeight, z * VoxelData.ChunkDepth);

            GameObject go = new GameObject($"Chunk_{x}_{y}_{z}");
            go.transform.parent = this.transform;
            go.transform.position = chunkCoord;

            Chunk chunk = go.AddComponent<Chunk>();

            if (chunkMaterial != null && waterMaterial != null)
            {
                chunk.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { chunkMaterial, waterMaterial };
            }
            else if (chunkMaterial != null)
            {
                 chunk.GetComponent<MeshRenderer>().material = chunkMaterial;
            }

            chunk.Initialize(this, chunkCoord);

            PopulateChunk(chunk, chunkCoord, center, mode);

            chunks.Add(chunkCoord, chunk);

            // Generate mesh for this chunk
            chunk.GenerateMesh();

            // Re-generate meshes for existing neighbors to hide seams
            UpdateNeighborChunks(chunkCoord);
        }

        void UpdateNeighborChunks(Vector3Int chunkPos)
        {
            Vector3Int[] offsets = {
                new Vector3Int(VoxelData.ChunkWidth, 0, 0),
                new Vector3Int(-VoxelData.ChunkWidth, 0, 0),
                new Vector3Int(0, VoxelData.ChunkHeight, 0),
                new Vector3Int(0, -VoxelData.ChunkHeight, 0),
                new Vector3Int(0, 0, VoxelData.ChunkDepth),
                new Vector3Int(0, 0, -VoxelData.ChunkDepth)
            };

            foreach (var offset in offsets)
            {
                Vector3Int neighborPos = chunkPos + offset;
                if (chunks.ContainsKey(neighborPos))
                {
                    chunks[neighborPos].GenerateMesh();
                }
            }
        }

        // Voronoi Cell Result
        struct VoronoiResult
        {
            public float distance; // Distance to closest point
            public float id; // Hashed ID of the cell
        }

        VoronoiResult GetVoronoi(float x, float z, float scale, float seed)
        {
            x *= scale;
            z *= scale;

            int ix = Mathf.FloorToInt(x);
            int iz = Mathf.FloorToInt(z);
            float fx = x - ix;
            float fz = z - iz;

            float minDist = 100f;
            float minID = 0f;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    // Random point in neighboring cell
                    float rx = dx + Mathf.Sin(seed + (ix + dx) * 12.9898f + (iz + dz) * 78.233f) * 0.5f + 0.5f;
                    float rz = dz + Mathf.Cos(seed + (ix + dx) * 43.123f + (iz + dz) * 12.345f) * 0.5f + 0.5f;

                    // Distance from pixel to point
                    float dist = Vector2.Distance(new Vector2(fx, fz), new Vector2(rx, rz));

                    if (dist < minDist)
                    {
                        minDist = dist;
                        minID = Mathf.Sin((ix + dx) * 12.9898f + (iz + dz) * 78.233f);
                    }
                }
            }

            return new VoronoiResult { distance = minDist, id = minID };
        }

        void PopulateChunk(Chunk chunk, Vector3Int chunkPos, Vector3 center, GenMode mode)
        {
            float seedOffset = (float)(config.seed % 10000) * 100f;
            // Override settings if in ShapeOnly mode
            bool useNoise = (mode != GenMode.ShapeOnly);

            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelData.ChunkDepth; z++)
                    {
                        Vector3Int globalPos = chunkPos + new Vector3Int(x, y, z);
                        float dist = Vector3.Distance(globalPos, center);

                        // --- Smart Noise Generation (Voronoi Plates) ---
                        float wx = globalPos.x + seedOffset;
                        float wz = globalPos.z + seedOffset;

                        float elevation = 0;
                        bool isMountain = false;

                        if (useNoise)
                        {
                            // 1. Tectonic Plates (Voronoi)
                            // Scale 0.01 makes plates roughly 100 blocks wide
                            VoronoiResult tectonic = GetVoronoi(wx, wz, 0.01f, config.seed);

                            // Determine Plate Type based on ID (-1 to 1)
                            bool isLandPlate = tectonic.id > (config.continentThreshold * 2f - 1f);

                            // Edge Smoothing: minDist is 0 at center, ~0.7 at edge.
                            // We want edges to taper.
                            float plateEdgeFactor = Mathf.Clamp01(1f - tectonic.distance); // 1 at center, 0 at edge
                            plateEdgeFactor = Mathf.Pow(plateEdgeFactor, 0.5f); // Curve it

                            if (isLandPlate)
                            {
                                // LAND
                                float mntNoise = Mathf.PerlinNoise(wx * config.noiseFrequency, wz * config.noiseFrequency);
                                float ridge = 1f - Mathf.Abs(mntNoise * 2f - 1f);
                                ridge = Mathf.Pow(ridge, 2f);

                                // Base Continent Height
                                float baseHeight = 5f * plateEdgeFactor;

                                // Mountain Height
                                elevation = baseHeight + (ridge * config.noiseAmplitude * plateEdgeFactor);

                                if (ridge > 0.6f && plateEdgeFactor > 0.5f) isMountain = true;
                            }
                            else
                            {
                                // OCEAN
                                // Deep ocean at center, shallow at edge
                                elevation = -10f * plateEdgeFactor;
                                // Add some rolling hills on sea floor
                                elevation += Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f) * 4f;
                            }
                        }

                        float surfaceRadius = config.planetRadius + elevation;

                        // 3. Caves (3D Noise)
                        bool isCave = false;
                        if (useNoise)
                        {
                            float caveNoise = Mathf.PerlinNoise((globalPos.x + seedOffset) * 0.1f, (globalPos.y + seedOffset) * 0.1f) *
                                              Mathf.PerlinNoise((globalPos.y + seedOffset) * 0.1f, (globalPos.z + seedOffset) * 0.1f);
                            isCave = caveNoise > config.caveThreshold;
                        }

                        // --- Stratigraphy Logic ---
                        if (dist <= surfaceRadius)
                        {
                            if (!IsInsideWorld(globalPos)) continue;

                            // Cave Check (only in Crust/Mantle, not Core)
                            if (isCave && dist > config.planetRadius * 0.3f)
                            {
                                if (dist <= config.seaLevel)
                                    chunk.SetBlock(x, y, z, VoxelData.Water, false);
                                else
                                    chunk.SetBlock(x, y, z, VoxelData.Air, false);
                            }
                            else
                            {
                                // Layers: Core -> Mantle -> Crust
                                if (dist < config.planetRadius * 0.15f)
                                {
                                    chunk.SetBlock(x, y, z, VoxelData.Magma, false); // Core
                                }
                                else if (dist < config.planetRadius * 0.5f)
                                {
                                    // Mantle (Bedrock/Magma mix?)
                                    if (Random.value > 0.9f) chunk.SetBlock(x, y, z, VoxelData.Magma, false);
                                    else chunk.SetBlock(x, y, z, VoxelData.Bedrock, false);
                                }
                                else if (dist < surfaceRadius - 4)
                                {
                                    // Deep Crust
                                    chunk.SetBlock(x, y, z, VoxelData.Stone, false);
                                }
                                else
                                {
                                    // Surface Crust (Biomes)
                                    // Temperature Noise
                                    float tempNoise = Mathf.PerlinNoise(wx * 0.01f + 500, wz * 0.01f + 500);

                                    if (dist <= config.seaLevel + 1) // Beach/Water Level
                                    {
                                        chunk.SetBlock(x, y, z, VoxelData.Sand, false);
                                    }
                                    else if (isMountain && dist > config.seaLevel + 15) // High Peaks
                                    {
                                        chunk.SetBlock(x, y, z, VoxelData.Snow, false);
                                    }
                                    else
                                    {
                                        // Biome Selection with Color Variance
                                        float patchNoise = Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f); // Detail patches

                                        if (tempNoise < 0.3f) // Cold -> Snow/Stone
                                        {
                                             chunk.SetBlock(x, y, z, VoxelData.Snow, false);
                                        }
                                        else if (tempNoise > 0.7f) // Hot -> Sand
                                        {
                                             if (patchNoise > 0.6f) chunk.SetBlock(x, y, z, VoxelData.Clay, false); // Clay patch
                                             else chunk.SetBlock(x, y, z, VoxelData.Sand, false);
                                        }
                                        else // Temperate -> Grass
                                        {
                                             if (patchNoise > 0.7f) chunk.SetBlock(x, y, z, VoxelData.Dirt, false); // Dirt patch
                                             else chunk.SetBlock(x, y, z, VoxelData.Grass, false);
                                        }

                                        // Gravel beaches?
                                        if (dist <= config.seaLevel + 2 && patchNoise < 0.3f)
                                        {
                                            chunk.SetBlock(x, y, z, VoxelData.Gravel, false);
                                        }
                                    }
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

        // Hard Constraints
        public bool IsInsideWorld(Vector3Int pos)
        {
            int max = config.worldSize * VoxelData.ChunkWidth;
            return pos.x >= 0 && pos.x < max &&
                   pos.y >= 0 && pos.y < max &&
                   pos.z >= 0 && pos.z < max;
        }

        public void ModifyBlock(Vector3Int globalPos, byte type, bool regenerateMesh)
        {
            if (!IsInsideWorld(globalPos)) return;

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
