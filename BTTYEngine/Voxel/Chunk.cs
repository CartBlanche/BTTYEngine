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
        private static readonly VertexPositionNormalColor[] scratchVerts   = new VertexPositionNormalColor[MAX_QUADS * 4];
        private static readonly short[]                     scratchIndexes = new short[MAX_QUADS * 6];
        private int quadCount;
        public  int QuadCount => quadCount;

        // Face normals, one per axis direction.
        private static readonly Vector3 normNZ = new Vector3( 0f,  0f, -1f);
        private static readonly Vector3 normPZ = new Vector3( 0f,  0f,  1f);
        private static readonly Vector3 normNX = new Vector3(-1f,  0f,  0f);
        private static readonly Vector3 normPX = new Vector3( 1f,  0f,  0f);
        private static readonly Vector3 normPY = new Vector3( 0f,  1f,  0f);
        private static readonly Vector3 normNY = new Vector3( 0f, -1f,  0f);

        // Corner offsets, name encodes sign per axis: n=−HALF_SIZE, p=+HALF_SIZE (x,y,z).
        private static readonly Vector3 nnn = new Vector3(-Voxel.HALF_SIZE, -Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 pnn = new Vector3( Voxel.HALF_SIZE, -Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 ppn = new Vector3( Voxel.HALF_SIZE,  Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 npn = new Vector3(-Voxel.HALF_SIZE,  Voxel.HALF_SIZE, -Voxel.HALF_SIZE);
        private static readonly Vector3 nnp = new Vector3(-Voxel.HALF_SIZE, -Voxel.HALF_SIZE,  Voxel.HALF_SIZE);
        private static readonly Vector3 pnp = new Vector3( Voxel.HALF_SIZE, -Voxel.HALF_SIZE,  Voxel.HALF_SIZE);
        private static readonly Vector3 ppp = new Vector3( Voxel.HALF_SIZE,  Voxel.HALF_SIZE,  Voxel.HALF_SIZE);
        private static readonly Vector3 npp = new Vector3(-Voxel.HALF_SIZE,  Voxel.HALF_SIZE,  Voxel.HALF_SIZE);

        // Neighbour chunk references cached at the start of each UpdateMesh call.
        // Eliminates repeated parentWorld.Chunks[worldX±1,...] array lookups during mesh building.
        private Chunk nX, pX, nY, pY, nZ, pZ;

        private void CacheNeighbours()
        {
            nX = worldX > 0                        ? parentWorld.Chunks[worldX - 1, worldY, worldZ] : null;
            pX = worldX < parentWorld.X_CHUNKS - 1 ? parentWorld.Chunks[worldX + 1, worldY, worldZ] : null;
            nY = worldY > 0                        ? parentWorld.Chunks[worldX, worldY - 1, worldZ] : null;
            pY = worldY < parentWorld.Y_CHUNKS - 1 ? parentWorld.Chunks[worldX, worldY + 1, worldZ] : null;
            nZ = worldZ > 0                        ? parentWorld.Chunks[worldX, worldY, worldZ - 1] : null;
            pZ = worldZ < parentWorld.Z_CHUNKS - 1 ? parentWorld.Chunks[worldX, worldY, worldZ + 1] : null;
        }
        
        public Chunk(VoxelWorld world, int wx, int wy, int wz, bool createGround)
        {
            parentWorld = world;
            worldX = wx;
            worldY = wy;
            worldZ = wz;

            boundingSphere = new BoundingSphere(new Vector3(worldX * (X_SIZE * Voxel.SIZE), worldY * (Y_SIZE * Voxel.SIZE), worldZ * (Z_SIZE * Voxel.SIZE)) + (new Vector3(X_SIZE * Voxel.SIZE, Y_SIZE * Voxel.SIZE, Z_SIZE * Voxel.SIZE) / 2f), (X_SIZE * Voxel.SIZE));

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
            quadCount = 0;
            CacheNeighbours();

            // Pre-compute the per-chunk world-space origin once outside all loops.
            float baseX =  worldX * (X_SIZE * Voxel.SIZE);
            float baseY = worldY * (Y_SIZE * Voxel.SIZE);
            float baseZ =  worldZ * (Z_SIZE * Voxel.SIZE);

            for (int z = Z_SIZE - 1; z >= 0; z--)
                for (int y = 0; y < Y_SIZE; y++)
                    for (int x = 0; x < X_SIZE; x++)
                    {
                        ref Voxel v = ref Voxels[x, y, z];
                        if (!v.Active) continue;

                        Vector3 worldOffset = new Vector3(baseX + x * Voxel.SIZE, baseY + y * Voxel.SIZE, baseZ + z * Voxel.SIZE);

                        if (!IsVoxelAt(x, y, z - 1)) MakeQuad(worldOffset, nnn, pnn, ppn, npn, normNZ, CalcAO(x, y, z, v.TR, v.TG, v.TB, normNZ));
                        if (!IsVoxelAt(x, y, z + 1)) MakeQuad(worldOffset, ppp, pnp, nnp, npp, normPZ, CalcAO(x, y, z, v.TR, v.TG, v.TB, normPZ));
                        if (!IsVoxelAt(x - 1, y, z)) MakeQuad(worldOffset, nnn, npn, npp, nnp, normNX, CalcAO(x, y, z, v.SR, v.SG, v.SB, normNX));
                        if (!IsVoxelAt(x + 1, y, z)) MakeQuad(worldOffset, ppp, ppn, pnn, pnp, normPX, CalcAO(x, y, z, v.SR, v.SG, v.SB, normPX));
                        if (!IsVoxelAt(x, y + 1, z)) MakeQuad(worldOffset, npn, ppn, ppp, npp, normPY, CalcAO(x, y, z, v.TR, v.TG, v.TB, normPY));
                        if (!IsVoxelAt(x, y - 1, z)) MakeQuad(worldOffset, pnp, pnn, nnn, nnp, normNY, CalcAO(x, y, z, v.SR, v.SG, v.SB, normNY));
                    }

            // Copy scratch buffers into instance arrays, reallocating only when capacity is exceeded.
            int vertCount = quadCount * 4;
            int idxCount  = quadCount * 6;
            if (VertexArray == null || VertexArray.Length < vertCount)
                VertexArray = new VertexPositionNormalColor[vertCount];
            if (IndexArray == null || IndexArray.Length < idxCount)
                IndexArray = new short[idxCount];
            Array.Copy(scratchVerts,   VertexArray, vertCount);
            Array.Copy(scratchIndexes, IndexArray,  idxCount);

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
                        int dy = y + zz;
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

        // Bakes per-face ambient occlusion into vertex colour.
        // faceNormal selects which axis hemisphere to probe, making the result camera-agnostic:
        // the +Y face is always occluded by geometry above it, the -X face by geometry to its
        // left, etc., regardless of which way the camera is pointing.
        // intensityFactor is halved vs. the old CalcLighting (+Z-only); BasicEffect's directional
        // light now supplies primary contrast so AO is a subtle crevice accent only.
        Color CalcAO(int x, int y, int z, byte r, byte g, byte b, Vector3 faceNormal)
        {
            // Probe origin: one step outward along the face normal from the voxel centre.
            int ox = x + (int)faceNormal.X;
            int oy = y + (int)faceNormal.Y;
            int oz = z + (int)faceNormal.Z;

            Vector3 colVect = new Color(r, g, b).ToVector3();
            const float intensityFactor = 0.06f;
            float light = 1f;
            uint hit = 0;

            // Primary step direction and two perpendicular axes, derived from faceNormal.
            // faceNormal is always one of ±(1,0,0), ±(0,1,0), ±(0,0,1).
            int sx, sy, sz;  // one unit along faceNormal per depth level
            int px, py, pz;  // first perpendicular axis
            int qx, qy, qz;  // second perpendicular axis
            if      (faceNormal.Z != 0f) { sx = 0; sy = 0; sz = (int)faceNormal.Z; px = 1; py = 0; pz = 0; qx = 0; qy = 1; qz = 0; }
            else if (faceNormal.Y != 0f) { sx = 0; sy = (int)faceNormal.Y; sz = 0; px = 1; py = 0; pz = 0; qx = 0; qy = 0; qz = 1; }
            else                         { sx = (int)faceNormal.X; sy = 0; sz = 0; px = 0; py = 1; pz = 0; qx = 0; qy = 0; qz = 1; }

            for (int d = 0; d < 4; d++)
            {
                float intensity = (intensityFactor / 4f) * (4f - d);
                // Three straight probes along the face normal: close, mid-range, far.
                if ((hit & 0x001u) == 0 && IsVoxelAt(ox + sx * d,        oy + sy * d,        oz + sz * d))        { light -= intensity * 3f;   hit |= 0x001u; }
                if ((hit & 0x002u) == 0 && IsVoxelAt(ox + sx * (d + 5),  oy + sy * (d + 5),  oz + sz * (d + 5)))  { light -= intensity * 0.5f; hit |= 0x002u; }
                if ((hit & 0x004u) == 0 && IsVoxelAt(ox + sx * (d + 10), oy + sy * (d + 10), oz + sz * (d + 10))) { light -= intensity * 0.5f; hit |= 0x004u; }
                // Eight fan probes spread across the hemisphere (±p, ±q, and their four diagonal combinations).
                if ((hit & 0x008u) == 0 && IsVoxelAt(ox + sx*d - px*d,        oy + sy*d - py*d,        oz + sz*d - pz*d))        { light -= intensity; hit |= 0x008u; }
                if ((hit & 0x010u) == 0 && IsVoxelAt(ox + sx*d - qx*d,        oy + sy*d - qy*d,        oz + sz*d - qz*d))        { light -= intensity; hit |= 0x010u; }
                if ((hit & 0x020u) == 0 && IsVoxelAt(ox + sx*d + px*d,        oy + sy*d + py*d,        oz + sz*d + pz*d))        { light -= intensity; hit |= 0x020u; }
                if ((hit & 0x040u) == 0 && IsVoxelAt(ox + sx*d + qx*d,        oy + sy*d + qy*d,        oz + sz*d + qz*d))        { light -= intensity; hit |= 0x040u; }
                if ((hit & 0x080u) == 0 && IsVoxelAt(ox + sx*d - px*d - qx*d, oy + sy*d - py*d - qy*d, oz + sz*d - pz*d - qz*d)) { light -= intensity; hit |= 0x080u; }
                if ((hit & 0x100u) == 0 && IsVoxelAt(ox + sx*d + px*d - qx*d, oy + sy*d + py*d - qy*d, oz + sz*d + pz*d - qz*d)) { light -= intensity; hit |= 0x100u; }
                if ((hit & 0x200u) == 0 && IsVoxelAt(ox + sx*d - px*d + qx*d, oy + sy*d - py*d + qy*d, oz + sz*d - pz*d + qz*d)) { light -= intensity; hit |= 0x200u; }
                if ((hit & 0x400u) == 0 && IsVoxelAt(ox + sx*d + px*d + qx*d, oy + sy*d + py*d + qy*d, oz + sz*d + pz*d + qz*d)) { light -= intensity; hit |= 0x400u; }
                if (hit == 0x7FFu) break;
            }

            light = MathHelper.Clamp(light, 0f, 1f);
            return new Color(colVect * light);
        }

        void MakeQuad(Vector3 offset, Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl, Vector3 norm, Color col)
        {
            if (quadCount >= MAX_QUADS) return;
            int vBase = quadCount * 4;
            int iBase = quadCount * 6;
            scratchVerts[vBase]     = new VertexPositionNormalColor(offset + tl, norm, col);
            scratchVerts[vBase + 1] = new VertexPositionNormalColor(offset + tr, norm, col);
            scratchVerts[vBase + 2] = new VertexPositionNormalColor(offset + br, norm, col);
            scratchVerts[vBase + 3] = new VertexPositionNormalColor(offset + bl, norm, col);
            // Indices are purely a function of quad position, no intermediate list needed.
            scratchIndexes[iBase]     = (short)(vBase);
            scratchIndexes[iBase + 1] = (short)(vBase + 1);
            scratchIndexes[iBase + 2] = (short)(vBase + 2);
            scratchIndexes[iBase + 3] = (short)(vBase + 2);
            scratchIndexes[iBase + 4] = (short)(vBase + 3);
            scratchIndexes[iBase + 5] = (short)(vBase);
            quadCount++;
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
            if (!xOk && yOk && zOk) { n = x < 0 ? nX : pX; return n != null && n.Voxels[x < 0 ? X_SIZE + x : x - X_SIZE, y, z].Active; }
            if (xOk && !yOk && zOk) { n = y < 0 ? nY : pY; return n != null && n.Voxels[x, y < 0 ? Y_SIZE + y : y - Y_SIZE, z].Active; }
            if (xOk && yOk && !zOk) { n = z < 0 ? nZ : pZ; return n != null && n.Voxels[x, y, z < 0 ? Z_SIZE + z : z - Z_SIZE].Active; }
            return false;
        }

        public void ClearMem()
        {
            VertexArray = null;
            IndexArray = null;
            
        }
    }
}
