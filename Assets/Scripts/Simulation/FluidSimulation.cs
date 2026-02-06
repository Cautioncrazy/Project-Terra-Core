using UnityEngine;
using System.Collections.Generic;
using Voxel;
using Core;

namespace Simulation
{
    [RequireComponent(typeof(VoxelEngine))]
    public class FluidSimulation : MonoBehaviour
    {
        public float tickRate = 0.2f;
        private VoxelEngine engine;
        private float timer;

        void Start()
        {
            engine = GetComponent<VoxelEngine>();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= tickRate)
            {
                timer = 0;
                SimulationTick();
            }
        }

        void SimulationTick()
        {
            List<Chunk> allChunks = new List<Chunk>(engine.GetAllChunks());
            // Process chunks bottom to top (Y) to handle falling properly
            allChunks.Sort((a, b) => a.chunkPosition.y.CompareTo(b.chunkPosition.y));

            HashSet<Chunk> modifiedChunks = new HashSet<Chunk>();

            foreach (Chunk chunk in allChunks)
            {
                // Bottom to Top inside chunk
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    for (int x = 0; x < VoxelData.ChunkWidth; x++)
                    {
                        for (int z = 0; z < VoxelData.ChunkDepth; z++)
                        {
                            byte block = chunk.GetBlock(x, y, z);
                            Vector3Int globalPos = chunk.chunkPosition + new Vector3Int(x, y, z);

                            if (block == VoxelData.Dirt)
                            {
                                ProcessDirt(globalPos, modifiedChunks);
                            }
                            else if (block == VoxelData.Water)
                            {
                                ProcessWater(globalPos, modifiedChunks);
                            }
                        }
                    }
                }
            }

            // Rebuild meshes
            foreach (Chunk c in modifiedChunks)
            {
                c.GenerateMesh();
            }
        }

        void ProcessDirt(Vector3Int pos, HashSet<Chunk> modifiedChunks)
        {
            Vector3Int below = pos + Vector3Int.down;
            byte blockBelow = engine.GetBlock(below);

            if (blockBelow == VoxelData.Air)
            {
                // 50% chance
                if (Random.value > 0.5f)
                {
                    MoveBlock(pos, below, VoxelData.Dirt, modifiedChunks);
                }
            }
        }

        void ProcessWater(Vector3Int pos, HashSet<Chunk> modifiedChunks)
        {
            Vector3Int below = pos + Vector3Int.down;
            byte blockBelow = engine.GetBlock(below);

            if (blockBelow == VoxelData.Air)
            {
                MoveBlock(pos, below, VoxelData.Water, modifiedChunks);
            }
            else if (blockBelow != VoxelData.Water)
            {
                Vector3Int left = pos + Vector3Int.left;
                Vector3Int right = pos + Vector3Int.right;

                bool leftEmpty = engine.GetBlock(left) == VoxelData.Air;
                bool rightEmpty = engine.GetBlock(right) == VoxelData.Air;

                if (leftEmpty && rightEmpty)
                {
                    if (Random.value > 0.5f) MoveBlock(pos, left, VoxelData.Water, modifiedChunks);
                    else MoveBlock(pos, right, VoxelData.Water, modifiedChunks);
                }
                else if (leftEmpty)
                {
                    MoveBlock(pos, left, VoxelData.Water, modifiedChunks);
                }
                else if (rightEmpty)
                {
                    MoveBlock(pos, right, VoxelData.Water, modifiedChunks);
                }
            }
        }

        void MoveBlock(Vector3Int from, Vector3Int to, byte type, HashSet<Chunk> modifiedChunks)
        {
            engine.ModifyBlock(from, VoxelData.Air, false);
            engine.ModifyBlock(to, type, false);

            AddChunkAndNeighborsToSet(from, modifiedChunks);
            AddChunkAndNeighborsToSet(to, modifiedChunks);
        }

        void AddChunkAndNeighborsToSet(Vector3Int pos, HashSet<Chunk> set)
        {
            int cx = Mathf.FloorToInt((float)pos.x / VoxelData.ChunkWidth) * VoxelData.ChunkWidth;
            int cy = Mathf.FloorToInt((float)pos.y / VoxelData.ChunkHeight) * VoxelData.ChunkHeight;
            int cz = Mathf.FloorToInt((float)pos.z / VoxelData.ChunkDepth) * VoxelData.ChunkDepth;
            Vector3Int cPos = new Vector3Int(cx, cy, cz);

            Chunk c = engine.GetChunk(cPos);
            if (c != null)
            {
                set.Add(c);

                Vector3Int[] neighborOffsets = {
                    new Vector3Int(-VoxelData.ChunkWidth, 0, 0),
                    new Vector3Int(VoxelData.ChunkWidth, 0, 0),
                    new Vector3Int(0, -VoxelData.ChunkHeight, 0),
                    new Vector3Int(0, VoxelData.ChunkHeight, 0),
                    new Vector3Int(0, 0, -VoxelData.ChunkDepth),
                    new Vector3Int(0, 0, VoxelData.ChunkDepth)
                };

                foreach(var off in neighborOffsets)
                {
                    Chunk n = engine.GetChunk(cPos + off);
                    if (n != null) set.Add(n);
                }
            }
        }
    }
}
