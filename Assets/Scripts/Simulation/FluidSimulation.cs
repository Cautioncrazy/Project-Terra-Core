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

        public void SimulationTick()
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
            HashSet<Vector3Int> processedBlocks = new HashSet<Vector3Int>();

            foreach (Chunk chunk in allChunks)
            {
                // Iterate blocks. Order matters less now but generally should process ones that can move.
                for (int x = 0; x < VoxelData.ChunkWidth; x++)
                {
                    for (int y = 0; y < VoxelData.ChunkHeight; y++)
                    {
                        for (int z = 0; z < VoxelData.ChunkDepth; z++)
                        {
                            Vector3Int globalPos = chunk.chunkPosition + new Vector3Int(x, y, z);
                            if (processedBlocks.Contains(globalPos)) continue;

                            byte block = chunk.GetBlock(x, y, z);
                            if (block == VoxelData.Air || block == VoxelData.Bedrock || block == VoxelData.Stone) continue;

                            // Include all loose terrain types AND Stone/Magma in gravity
                            if (block == VoxelData.Dirt || block == VoxelData.Sand ||
                                block == VoxelData.Grass || block == VoxelData.Snow ||
                                block == VoxelData.Gravel || block == VoxelData.Clay ||
                                block == VoxelData.Stone || block == VoxelData.Magma)
                            {
                                ProcessSolidGravity(globalPos, block, center, modifiedChunks, processedBlocks);
                            }
                            else if (block == VoxelData.Water)
                            {
                                ProcessFluid(globalPos, block, center, modifiedChunks, processedBlocks);
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

        void ProcessSolidGravity(Vector3Int pos, byte type, Vector3 center, HashSet<Chunk> modifiedChunks, HashSet<Vector3Int> processedBlocks)
        {
            Vector3Int down = GetRadialDown(pos, center);
            Vector3Int targetPos = pos + down;

            if (!engine.IsInsideWorld(targetPos)) return; // Constraint

            byte blockTarget = engine.GetBlock(targetPos);

            // 1. Direct Fall
            if (blockTarget == VoxelData.Air || blockTarget == VoxelData.Water)
            {
                if (blockTarget == VoxelData.Water)
                {
                    // Displacement: Solid sinks, Water moves up (Swap)
                    SwapBlock(pos, targetPos, type, VoxelData.Water, modifiedChunks, processedBlocks);
                }
                else
                {
                    // Fall into Air
                    MoveBlock(pos, targetPos, type, modifiedChunks, processedBlocks);
                }
            }
            else
            {
                // 2. Displacement / Landslide (Slide sideways if blocked)
                // Try to find a diagonal neighbor that is closer to center or same level but empty
                AttemptSlide(pos, type, down, modifiedChunks, processedBlocks);
            }
        }

        void AttemptSlide(Vector3Int pos, byte type, Vector3Int down, HashSet<Chunk> modifiedChunks, HashSet<Vector3Int> processedBlocks)
        {
            // Check 4 diagonals relative to 'down'
            // If down is (0, -1, 0), diagonals are (1, -1, 0), (-1, -1, 0), etc.

            List<Vector3Int> slideCandidates = new List<Vector3Int>();

            foreach (var face in VoxelData.FaceChecks)
            {
                if (face == down || face == -down) continue; // Skip up/down relative to radial

                Vector3Int diagonal = down + face;
                Vector3Int target = pos + diagonal;

                if (!engine.IsInsideWorld(target)) continue;

                byte targetBlock = engine.GetBlock(target);
                if (targetBlock == VoxelData.Air || targetBlock == VoxelData.Water)
                {
                    slideCandidates.Add(target);
                }
            }

            if (slideCandidates.Count > 0)
            {
                // High chance to slide (prevent pillars)
                // Randomly pick one candidate to avoid bias
                Vector3Int target = slideCandidates[Random.Range(0, slideCandidates.Count)];
                byte targetBlock = engine.GetBlock(target);

                if (targetBlock == VoxelData.Water)
                    SwapBlock(pos, target, type, VoxelData.Water, modifiedChunks, processedBlocks);
                else
                    MoveBlock(pos, target, type, modifiedChunks, processedBlocks);
            }
        }

        void SwapBlock(Vector3Int posA, Vector3Int posB, byte typeA, byte typeB, HashSet<Chunk> modifiedChunks, HashSet<Vector3Int> processedBlocks)
        {
            engine.ModifyBlock(posA, typeB, false);
            engine.ModifyBlock(posB, typeA, false);

            processedBlocks.Add(posA);
            processedBlocks.Add(posB);

            AddChunkAndNeighborsToSet(posA, modifiedChunks);
            AddChunkAndNeighborsToSet(posB, modifiedChunks);
        }

        void ProcessFluid(Vector3Int pos, byte type, Vector3 center, HashSet<Chunk> modifiedChunks, HashSet<Vector3Int> processedBlocks)
        {
            Vector3Int down = GetRadialDown(pos, center);
            Vector3Int targetPos = pos + down;

            if (!engine.IsInsideWorld(targetPos)) return; // Constraint

            byte blockTarget = engine.GetBlock(targetPos);

            if (blockTarget == VoxelData.Air)
            {
                MoveBlock(pos, targetPos, type, modifiedChunks, processedBlocks);
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
                     MoveBlock(pos, spreadTo, type, modifiedChunks, processedBlocks);
                }
            }
        }

        void MoveBlock(Vector3Int from, Vector3Int to, byte type, HashSet<Chunk> modifiedChunks, HashSet<Vector3Int> processedBlocks)
        {
            engine.ModifyBlock(from, VoxelData.Air, false);
            engine.ModifyBlock(to, type, false);

            processedBlocks.Add(from);
            processedBlocks.Add(to);

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
