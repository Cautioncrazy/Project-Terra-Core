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

                        // Simple noise to vary surface
                        // Using x and z for noise coords to create height variations
                        float noiseVal = Mathf.PerlinNoise(globalPos.x * 0.1f, globalPos.z * 0.1f);
                        float elevation = noiseVal * 6; // Variance 0 to 6

                        float surfaceRadius = planetRadius + elevation;

                        if (dist <= surfaceRadius)
                        {
                            if (dist < 8)
                            {
                                chunk.SetBlock(x,y,z, VoxelData.Bedrock, false);
                            }
                            else if (dist > surfaceRadius - 3)
                            {
                                chunk.SetBlock(x,y,z, VoxelData.Dirt, false);
                            }
                            else
                            {
                                chunk.SetBlock(x,y,z, VoxelData.Stone, false);
                            }
                        }
                        else
                        {
                            chunk.SetBlock(x,y,z, VoxelData.Air, false);
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
