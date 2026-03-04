using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BTTYEngine
{
    public class Chunk
    {
        public const int X_SIZE = 16, Y_SIZE = 16, Z_SIZE = 32;

        public Voxel[, ,] Voxels = new Voxel[X_SIZE,Y_SIZE,Z_SIZE];

        public VertexPositionNormalColor[] VertexArray;
        public short[] IndexArray;

        VoxelWorld parentWorld;
        public int worldX, worldY, worldZ;

        public BoundingSphere boundingSphere;

        public bool Visible = false;
        public bool Updated = false;

        // Static scratch buffers shared across all chunks (mesh building is single-threaded).
        // Short indices cap unique vertices at 32 767; 8 191 quads × 4 verts = 32 764 ≤ short.MaxValue.
        private const int MAX_QUADS = 8191;
        private static readonly VertexPositionNormalColor[] _scratchVerts   = new VertexPositionNormalColor[MAX_QUADS * 4];
        private static readonly short[]                     _scratchIndexes = new short[MAX_QUADS * 6];
        private int _quadCount;
        public  int QuadCount => _quadCount;

        // Face normals, one per axis direction.
        private static readonly Vector3 _normNZ = new Vector3( 0f,  0f, -1f);
        private static readonly Vector3 _normPZ = new Vector3( 0f,  0f,  1f);
        private static readonly Vector3 _normNX = new Vector3(-1f,  0f,  0f);
        private static readonly Vector3 _normPX = new Vector3( 1f,  0f,  0f);
        private static readonly Vector3 _normPY = new Vector3( 0f,  1f,  0f);
        private static readonly Vector3 _normNY = new Vector3( 0f, -1f,  0f);

        // Corner offsets, name encodes sign per axis: n=−HALF_SIZE, p=+HALF_SIZE (x,y,z).
        private static readonly Vector3 _nnn = new Vector3(-Voxel.HALF_SIZE, -Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 _pnn = new Vector3( Voxel.HALF_SIZE, -Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 _ppn = new Vector3( Voxel.HALF_SIZE,  Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 _npn = new Vector3(-Voxel.HALF_SIZE,  Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 _nnp = new Vector3(-Voxel.HALF_SIZE, -Voxel.HALF_SIZE,  Voxel.HALF_SIZE);
        private static readonly Vector3 _pnp = new Vector3( Voxel.HALF_SIZE, -Voxel.HALF_SIZE,  Voxel.HALF_SIZE);
        private static readonly Vector3 _ppp = new Vector3( Voxel.HALF_SIZE,  Voxel.HALF_SIZE,  Voxel.HALF_SIZE);
        private static readonly Vector3 _npp = new Vector3(-Voxel.HALF_SIZE,  Voxel.HALF_SIZE,  Voxel.HALF_SIZE);

        // Neighbour chunk references cached at the start of each UpdateMesh call.
        // Eliminates repeated parentWorld.Chunks[worldX±1,...] array lookups during mesh building.
        private Chunk _nX, _pX, _nY, _pY, _nZ, _pZ;

        private void CacheNeighbours()
        {
            _nX = worldX > 0                        ? parentWorld.Chunks[worldX - 1, worldY, worldZ] : null;
            _pX = worldX < parentWorld.X_CHUNKS - 1 ? parentWorld.Chunks[worldX + 1, worldY, worldZ] : null;
            _nY = worldY > 0                        ? parentWorld.Chunks[worldX, worldY - 1, worldZ] : null;
            _pY = worldY < parentWorld.Y_CHUNKS - 1 ? parentWorld.Chunks[worldX, worldY + 1, worldZ] : null;
            _nZ = worldZ > 0                        ? parentWorld.Chunks[worldX, worldY, worldZ - 1] : null;
            _pZ = worldZ < parentWorld.Z_CHUNKS - 1 ? parentWorld.Chunks[worldX, worldY, worldZ + 1] : null;
        }
        
        public Chunk(VoxelWorld world, int wx, int wy, int wz, bool createGround)
        {
            parentWorld = world;
            worldX = wx;
            worldY = wy;
            worldZ = wz;

            boundingSphere = new BoundingSphere(new Vector3(worldX * (X_SIZE * Voxel.SIZE), -(worldY * (Y_SIZE * Voxel.SIZE)), worldZ * (Z_SIZE * Voxel.SIZE)) + (new Vector3(X_SIZE * Voxel.SIZE, -(Y_SIZE * Voxel.SIZE), Z_SIZE * Voxel.SIZE) / 2f), (X_SIZE * Voxel.SIZE));

            if (createGround)
            {
                for (int y = 0; y < Y_SIZE; y++)
                    for (int x = 0; x < X_SIZE; x++)
                    {
                        for (int z = Chunk.Z_SIZE - 1; z >= Chunk.Z_SIZE - 5; z--)
                        {
                            SetVoxel(x, y, z, true, 0, VoxelType.Ground, new Color(0f, 0.5f + ((float)Helper.Random.NextDouble() * 0.1f), 0f), new Color(0f, 0.3f, 0f));
                        }
                    }
            }

            
        }

        public void SetVoxel(int x, int y, int z, bool active, byte destruct, VoxelType type, Color top, Color side)
        {
            if (x < 0 || y < 0 || z < 0 || x >= X_SIZE || y >= Y_SIZE || z >= Z_SIZE) return;

            ref Voxel v = ref Voxels[x, y, z];
            v.Active       = active;
            v.Type         = type;
            v.Destructable = destruct;
            v.TR           = top.R;
            v.TG           = top.G;
            v.TB           = top.B;
            v.SR           = side.R;
            v.SG           = side.G;
            v.SB           = side.B;

            Updated = true;
        }

        public void UpdateMesh()
        {
            _quadCount = 0;
            CacheNeighbours();

            // Pre-compute the per-chunk world-space origin once outside all loops.
            float baseX =  worldX * (X_SIZE * Voxel.SIZE);
            float baseY = -(worldY * (Y_SIZE * Voxel.SIZE));
            float baseZ =  worldZ * (Z_SIZE * Voxel.SIZE);

            for (int z = Z_SIZE - 1; z >= 0; z--)
                for (int y = 0; y < Y_SIZE; y++)
                    for (int x = 0; x < X_SIZE; x++)
                    {
                        ref Voxel v = ref Voxels[x, y, z];
                        if (!v.Active) continue;

                        Vector3 worldOffset = new Vector3(baseX + x * Voxel.SIZE, baseY - y * Voxel.SIZE, baseZ + z * Voxel.SIZE);

                        if (!IsVoxelAt(x, y, z - 1)) MakeQuad(worldOffset, _nnn, _pnn, _ppn, _npn, _normNZ, CalcLighting(x, y, z,     v.TR, v.TG, v.TB));
                        if (!IsVoxelAt(x, y, z + 1)) MakeQuad(worldOffset, _ppp, _pnp, _nnp, _npp, _normPZ, CalcLighting(x, y, z,     v.TR, v.TG, v.TB));
                        if (!IsVoxelAt(x - 1, y, z)) MakeQuad(worldOffset, _nnn, _npn, _npp, _nnp, _normNX, CalcLighting(x - 1, y, z, v.SR, v.SG, v.SB));
                        if (!IsVoxelAt(x + 1, y, z)) MakeQuad(worldOffset, _ppp, _ppn, _pnn, _pnp, _normPX, CalcLighting(x + 1, y, z, v.SR, v.SG, v.SB));
                        if (!IsVoxelAt(x, y - 1, z)) MakeQuad(worldOffset, _npn, _ppn, _ppp, _npp, _normPY, CalcLighting(x, y - 1, z, v.TR, v.TG, v.TB));
                        if (!IsVoxelAt(x, y + 1, z)) MakeQuad(worldOffset, _pnp, _pnn, _nnn, _nnp, _normNY, CalcLighting(x, y + 1, z, v.SR, v.SG, v.SB));
                    }

            // Copy scratch buffers into instance arrays, reallocating only when capacity is exceeded.
            int vertCount = _quadCount * 4;
            int idxCount  = _quadCount * 6;
            if (VertexArray == null || VertexArray.Length < vertCount)
                VertexArray = new VertexPositionNormalColor[vertCount];
            if (IndexArray == null || IndexArray.Length < idxCount)
                IndexArray = new short[idxCount];
            Array.Copy(_scratchVerts,   VertexArray, vertCount);
            Array.Copy(_scratchIndexes, IndexArray,  idxCount);

            Updated = false;
        }

        public void CopySprite(int x, int y, int z, AnimChunk c)
        {
            for (int xx = 0; xx < c.X_SIZE; xx++)
            {
                for (int yy = 0; yy < c.Y_SIZE; yy++)
                {
                    for (int zz = 0; zz < c.Z_SIZE; zz++)
                    {
                        ref readonly SpriteVoxel src = ref c.Voxels[xx, yy, zz];
                        if (!src.Active) continue;

                        int dx = x + xx;
                        int dy = y + ((c.Z_SIZE - 1) - zz);
                        int dz = z + yy;
                        if (dx < 0 || dy < 0 || dz < 0 || dx >= X_SIZE || dy >= Y_SIZE || dz >= Z_SIZE) continue;

                        Color side = new Color(src.Color.ToVector3() * 0.5f);
                        ref Voxel dst = ref Voxels[dx, dy, dz];
                        dst.Active       = true;
                        dst.Type         = VoxelType.Prefab;
                        dst.Destructable = 0;
                        dst.TR           = src.Color.R;
                        dst.TG           = src.Color.G;
                        dst.TB           = src.Color.B;
                        dst.SR           = side.R;
                        dst.SG           = side.G;
                        dst.SB           = side.B;
                    }
                }
            }
            Updated = true;
        }

        // Accepts raw colour bytes to avoid constructing a Color struct at each of the 6 callsites.
        // Uses a uint bitmask (one bit per shadow direction) instead of a bool[] field,
        // eliminating the reset loop and allowing an early exit once all directions are shadowed.
        Color CalcLighting(int x, int y, int z, byte r, byte g, byte b)
        {
            z++;  // Y-up: probe away from voxel in +Z (toward camera), matching original Y-down behaviour in reverse

            Vector3 colVect = new Color(r, g, b).ToVector3();
            const float intensityFactor = 0.12f;
            float light = 1f;
            uint hit = 0; // bits 0-10 correspond to the 11 shadow directions

            for (int zz = 0; zz < 4; zz++)
            {
                float intensity = (intensityFactor / 4f) * (4f - zz);
                // Three straight-back probes at close/mid/far depth, weights 3+0.5+0.5=4 match original total.
                if ((hit & 0x001u) == 0 && IsVoxelAt(x, y, z + zz))           { light -= intensity * 3f;   hit |= 0x001u; }
                if ((hit & 0x002u) == 0 && IsVoxelAt(x, y, z + (zz + 5)))     { light -= intensity * 0.5f; hit |= 0x002u; }
                if ((hit & 0x004u) == 0 && IsVoxelAt(x, y, z + (zz + 10)))    { light -= intensity * 0.5f; hit |= 0x004u; }
                if ((hit & 0x008u) == 0 && IsVoxelAt(x - zz, y - zz, z + zz)) { light -= intensity;        hit |= 0x008u; }
                if ((hit & 0x010u) == 0 && IsVoxelAt(x, y - zz, z + zz))      { light -= intensity;        hit |= 0x010u; }
                if ((hit & 0x020u) == 0 && IsVoxelAt(x + zz, y - zz, z + zz)) { light -= intensity;        hit |= 0x020u; }
                if ((hit & 0x040u) == 0 && IsVoxelAt(x - zz, y, z + zz))      { light -= intensity;        hit |= 0x040u; }
                if ((hit & 0x080u) == 0 && IsVoxelAt(x + zz, y, z + zz))      { light -= intensity;        hit |= 0x080u; }
                if ((hit & 0x100u) == 0 && IsVoxelAt(x - zz, y + zz, z + zz)) { light -= intensity;        hit |= 0x100u; }
                if ((hit & 0x200u) == 0 && IsVoxelAt(x, y + zz, z + zz))      { light -= intensity;        hit |= 0x200u; }
                if ((hit & 0x400u) == 0 && IsVoxelAt(x + zz, y + zz, z + zz)) { light -= intensity;        hit |= 0x400u; }
                if (hit == 0x7FFu) break; // all 11 directions shadowed, early exit now reachable
            }

            light = MathHelper.Clamp(light, 0f, 1f);
            return new Color(colVect * light);
        }

        void MakeQuad(Vector3 offset, Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl, Vector3 norm, Color col)
        {
            if (_quadCount >= MAX_QUADS) return;
            int vBase = _quadCount * 4;
            int iBase = _quadCount * 6;
            _scratchVerts[vBase]     = new VertexPositionNormalColor(offset + tl, norm, col);
            _scratchVerts[vBase + 1] = new VertexPositionNormalColor(offset + tr, norm, col);
            _scratchVerts[vBase + 2] = new VertexPositionNormalColor(offset + br, norm, col);
            _scratchVerts[vBase + 3] = new VertexPositionNormalColor(offset + bl, norm, col);
            // Indices are purely a function of quad position, no intermediate list needed.
            _scratchIndexes[iBase]     = (short)(vBase);
            _scratchIndexes[iBase + 1] = (short)(vBase + 1);
            _scratchIndexes[iBase + 2] = (short)(vBase + 2);
            _scratchIndexes[iBase + 3] = (short)(vBase + 2);
            _scratchIndexes[iBase + 4] = (short)(vBase + 3);
            _scratchIndexes[iBase + 5] = (short)(vBase);
            _quadCount++;
        }

        public bool IsVoxelAt(int x, int y, int z)
        {
            // Fast path: within this chunk's bounds.
            if (x >= 0 && x < X_SIZE && y >= 0 && y < Y_SIZE && z >= 0 && z < Z_SIZE)
                return Voxels[x, y, z].Active;

            bool xOk = x >= 0 && x < X_SIZE;
            bool yOk = y >= 0 && y < Y_SIZE;
            bool zOk = z >= 0 && z < Z_SIZE;

            // Single-axis out-of-bounds: use cached neighbour with direct array access (no recursion).
            // Multi-axis out-of-bounds (diagonal corner in CalcLighting): return false (treat as unoccluded).
            Chunk n;
            if (!xOk && yOk && zOk) { n = x < 0 ? _nX : _pX; return n != null && n.Voxels[x < 0 ? X_SIZE + x : x - X_SIZE, y, z].Active; }
            if (xOk && !yOk && zOk) { n = y < 0 ? _nY : _pY; return n != null && n.Voxels[x, y < 0 ? Y_SIZE + y : y - Y_SIZE, z].Active; }
            if (xOk && yOk && !zOk) { n = z < 0 ? _nZ : _pZ; return n != null && n.Voxels[x, y, z < 0 ? Z_SIZE + z : z - Z_SIZE].Active; }
            return false;
        }

        public void ClearMem()
        {
            VertexArray = null;
            IndexArray = null;
            
        }
    }
}
