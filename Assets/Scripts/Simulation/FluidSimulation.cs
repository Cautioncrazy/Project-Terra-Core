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
            // No simple Y-sort for radial gravity. Ideally sort by distance from center descending.
            Vector3 center = engine.GetWorldCenter();
            allChunks.Sort((a, b) => {
                float distA = Vector3.Distance(a.transform.position, center);
                float distB = Vector3.Distance(b.transform.position, center);
                return distA.CompareTo(distB); // Process Inner chunks first? No, Outer first so they fall in.
            });
            allChunks.Reverse(); // Outer chunks first

            HashSet<Chunk> modifiedChunks = new HashSet<Chunk>();

            foreach (Chunk chunk in allChunks)
            {
                // Iterate blocks. Order matters less now but generally should process ones that can move.
                for (int x = 0; x < VoxelData.ChunkWidth; x++)
                {
                    for (int y = 0; y < VoxelData.ChunkHeight; y++)
                    {
                        for (int z = 0; z < VoxelData.ChunkDepth; z++)
                        {
                            byte block = chunk.GetBlock(x, y, z);
                            if (block == VoxelData.Air || block == VoxelData.Bedrock || block == VoxelData.Stone) continue;

                            Vector3Int globalPos = chunk.chunkPosition + new Vector3Int(x, y, z);

                            // Include all loose terrain types in gravity
                            if (block == VoxelData.Dirt || block == VoxelData.Sand ||
                                block == VoxelData.Grass || block == VoxelData.Snow ||
                                block == VoxelData.Gravel || block == VoxelData.Clay)
                            {
                                ProcessSolidGravity(globalPos, block, center, modifiedChunks);
                            }
                            else if (block == VoxelData.Water)
                            {
                                ProcessFluid(globalPos, block, center, modifiedChunks);
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

        Vector3Int GetRadialDown(Vector3Int pos, Vector3 center)
        {
            // Vector pointing to center
            Vector3 dir = center - pos;

            // Find the cardinal direction that best aligns with 'dir'
            Vector3Int bestDir = Vector3Int.zero;
            float maxDot = -Mathf.Infinity;

            foreach (var face in VoxelData.FaceChecks)
            {
                float dot = Vector3.Dot(face, dir);
                if (dot > maxDot)
                {
                    maxDot = dot;
                    bestDir = face;
                }
            }
            return bestDir;
        }

        void ProcessSolidGravity(Vector3Int pos, byte type, Vector3 center, HashSet<Chunk> modifiedChunks)
        {
            Vector3Int down = GetRadialDown(pos, center);
            Vector3Int targetPos = pos + down;

            if (!engine.IsInsideWorld(targetPos)) return; // Constraint

            byte blockTarget = engine.GetBlock(targetPos);

            if (blockTarget == VoxelData.Air || blockTarget == VoxelData.Water) // Displace water?
            {
                 // 50% chance for dirt/sand to fall
                if (Random.value > 0.5f)
                {
                    MoveBlock(pos, targetPos, type, modifiedChunks);
                }
            }
        }

        void ProcessFluid(Vector3Int pos, byte type, Vector3 center, HashSet<Chunk> modifiedChunks)
        {
            Vector3Int down = GetRadialDown(pos, center);
            Vector3Int targetPos = pos + down;

            if (!engine.IsInsideWorld(targetPos)) return; // Constraint

            byte blockTarget = engine.GetBlock(targetPos);

            if (blockTarget == VoxelData.Air)
            {
                MoveBlock(pos, targetPos, type, modifiedChunks);
            }
            else if (blockTarget != VoxelData.Air && blockTarget != VoxelData.Water)
            {
                // Hit something solid. Spread sideways relative to "Down".
                List<Vector3Int> spreadDirs = new List<Vector3Int>();
                Vector3Int up = -down;

                foreach(var face in VoxelData.FaceChecks)
                {
                    if (face == down || face == up) continue;

                    Vector3Int neighborPos = pos + face;
                    if (!engine.IsInsideWorld(neighborPos)) continue; // Constraint check for spread

                    if (engine.GetBlock(neighborPos) == VoxelData.Air)
                    {
                        spreadDirs.Add(neighborPos);
                    }
                }

                if (spreadDirs.Count > 0)
                {
                    // Pick random spread direction
                    Vector3Int spreadTo = spreadDirs[Random.Range(0, spreadDirs.Count)];
                     MoveBlock(pos, spreadTo, type, modifiedChunks);
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
