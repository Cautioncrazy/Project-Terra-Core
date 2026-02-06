using UnityEngine;
using System.Collections.Generic;
using Core;

namespace Voxel
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Chunk : MonoBehaviour
    {
        public byte[,,] blocks = new byte[VoxelData.ChunkWidth, VoxelData.ChunkHeight, VoxelData.ChunkDepth];
        public Vector3Int chunkPosition;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private VoxelEngine world;

        public void Initialize(VoxelEngine world, Vector3Int position)
        {
            this.world = world;
            this.chunkPosition = position;
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
        }

        public void SetBlock(int x, int y, int z, byte type, bool regenerateMesh = true)
        {
            if (IsBounds(x, y, z))
            {
                blocks[x, y, z] = type;
                if (regenerateMesh)
                    GenerateMesh();
            }
        }

        public byte GetBlock(int x, int y, int z)
        {
             if (IsBounds(x, y, z))
                return blocks[x, y, z];
             return VoxelData.Air;
        }

        private bool IsBounds(int x, int y, int z)
        {
            return x >= 0 && x < VoxelData.ChunkWidth &&
                   y >= 0 && y < VoxelData.ChunkHeight &&
                   z >= 0 && z < VoxelData.ChunkDepth;
        }

        public void GenerateMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();

            int vertexIndex = 0;

            for (int x = 0; x < VoxelData.ChunkWidth; x++)
            {
                for (int y = 0; y < VoxelData.ChunkHeight; y++)
                {
                    for (int z = 0; z < VoxelData.ChunkDepth; z++)
                    {
                        byte blockId = blocks[x, y, z];
                        if (blockId == VoxelData.Air) continue;

                        Vector3 blockPos = new Vector3(x, y, z);

                        for (int i = 0; i < 6; i++)
                        {
                            Vector3Int dir = VoxelData.FaceChecks[i];
                            int nx = x + dir.x;
                            int ny = y + dir.y;
                            int nz = z + dir.z;

                            byte neighborBlock;

                            if (IsBounds(nx, ny, nz))
                            {
                                neighborBlock = blocks[nx, ny, nz];
                            }
                            else
                            {
                                if (world != null)
                                    neighborBlock = world.GetBlock(chunkPosition + new Vector3Int(nx, ny, nz));
                                else
                                    neighborBlock = VoxelData.Air;
                            }

                            // Optimization: Face Culling Logic
                            bool drawFace = false;

                            if (blockId == VoxelData.Water)
                            {
                                // Water only renders against Air (Surface)
                                // If neighbor is Water, don't draw (internal face)
                                // If neighbor is Solid, don't draw (underground)
                                if (neighborBlock == VoxelData.Air) drawFace = true;
                            }
                            else // Solid blocks
                            {
                                // Draw against Air or Water (Transparent)
                                if (VoxelData.IsTransparent(neighborBlock)) drawFace = true;
                            }

                            if (drawFace)
                            {
                                int v0Idx = VoxelData.VoxelTris[i, 0];
                                int v1Idx = VoxelData.VoxelTris[i, 1];
                                int v2Idx = VoxelData.VoxelTris[i, 2];
                                int v3Idx = VoxelData.VoxelTris[i, 3];

                                vertices.Add(blockPos + VoxelData.VoxelVerts[v0Idx]);
                                vertices.Add(blockPos + VoxelData.VoxelVerts[v1Idx]);
                                vertices.Add(blockPos + VoxelData.VoxelVerts[v2Idx]);
                                vertices.Add(blockPos + VoxelData.VoxelVerts[v3Idx]);

                                Color col = VoxelData.GetColor(blockId);
                                colors.Add(col);
                                colors.Add(col);
                                colors.Add(col);
                                colors.Add(col);

                                triangles.Add(vertexIndex);
                                triangles.Add(vertexIndex + 1);
                                triangles.Add(vertexIndex + 2);

                                triangles.Add(vertexIndex + 2);
                                triangles.Add(vertexIndex + 1);
                                triangles.Add(vertexIndex + 3);

                                vertexIndex += 4;
                            }
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.colors = colors.ToArray();
            mesh.RecalculateNormals();

            if (meshFilter != null) meshFilter.mesh = mesh;
            if (meshCollider != null) meshCollider.sharedMesh = mesh;
        }
    }
}
