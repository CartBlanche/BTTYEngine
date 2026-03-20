using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BTTYEngine
{
    public static class ParticleCube
    {
        // Pre-computed normalised corner directions — never change, computed once at startup.
        static readonly Vector3 _nTLF = Vector3.Normalize(new Vector3(-1f,  1f,  1f));
        static readonly Vector3 _nBLF = Vector3.Normalize(new Vector3(-1f, -1f,  1f));
        static readonly Vector3 _nTRF = Vector3.Normalize(new Vector3( 1f,  1f,  1f));
        static readonly Vector3 _nBRF = Vector3.Normalize(new Vector3( 1f, -1f,  1f));
        static readonly Vector3 _nTLB = Vector3.Normalize(new Vector3(-1f,  1f, -1f));
        static readonly Vector3 _nTRB = Vector3.Normalize(new Vector3( 1f,  1f, -1f));
        static readonly Vector3 _nBLB = Vector3.Normalize(new Vector3(-1f, -1f, -1f));
        static readonly Vector3 _nBRB = Vector3.Normalize(new Vector3( 1f, -1f, -1f));

        static short[] cubeIndices = new short[] {
                                        0,  1,  2,  // front face
                                        1,  3,  2,
                                        4,  5,  6,  // back face
                                        6,  5,  7,
                                        8,  9, 10,  // top face
                                        8, 11,  9,
                                        12, 13, 14, // bottom face
                                        12, 14, 15,
                                        16, 17, 18, // left face
                                        19, 17, 16,
                                        20, 21, 22, // right face
                                        23, 20, 22  };

        public static void Create(ref VertexPositionNormalColor[] verts, ref short[] indexes, Vector3 pos, int partNum, float scale, Color col)
        {
            int vertOffset = partNum * 24;

            Vector3 topLeftFront = new Vector3(-1.0f, 1.0f, 1.0f) * scale;
            Vector3 bottomLeftFront = new Vector3(-1.0f, -1.0f, 1.0f) * scale;
            Vector3 topRightFront = new Vector3(1.0f, 1.0f, 1.0f) * scale;
            Vector3 bottomRightFront = new Vector3(1.0f, -1.0f, 1.0f) * scale;
            Vector3 topLeftBack = new Vector3(-1.0f, 1.0f, -1.0f) * scale;
            Vector3 topRightBack = new Vector3(1.0f, 1.0f, -1.0f) * scale;
            Vector3 bottomLeftBack = new Vector3(-1.0f, -1.0f, -1.0f) * scale;
            Vector3 bottomRightBack = new Vector3(1.0f, -1.0f, -1.0f) * scale;

            // Front face
            verts[vertOffset+0] = new VertexPositionNormalColor(pos + topLeftFront,     _nTLF, col);
            verts[vertOffset+1] = new VertexPositionNormalColor(pos + bottomLeftFront,   _nBLF, col);
            verts[vertOffset+2] = new VertexPositionNormalColor(pos + topRightFront,     _nTRF, col);
            verts[vertOffset+3] = new VertexPositionNormalColor(pos + bottomRightFront,  _nBRF, col);

            // Back face
            verts[vertOffset+4] = new VertexPositionNormalColor(pos + topLeftBack,       _nTLB, col);
            verts[vertOffset+5] = new VertexPositionNormalColor(pos + topRightBack,       _nTRB, col);
            verts[vertOffset+6] = new VertexPositionNormalColor(pos + bottomLeftBack,     _nBLB, col);
            verts[vertOffset+7] = new VertexPositionNormalColor(pos + bottomRightBack,    _nBRB, col);

            // Top face
            verts[vertOffset+8]  = new VertexPositionNormalColor(pos + topLeftFront,     _nTLF, col);
            verts[vertOffset+9]  = new VertexPositionNormalColor(pos + topRightBack,      _nTRB, col);
            verts[vertOffset+10] = new VertexPositionNormalColor(pos + topLeftBack,       _nTLB, col);
            verts[vertOffset+11] = new VertexPositionNormalColor(pos + topRightFront,     _nTRF, col);

            // Bottom face
            verts[vertOffset+12] = new VertexPositionNormalColor(pos + bottomLeftFront,   _nBLF, col);
            verts[vertOffset+13] = new VertexPositionNormalColor(pos + bottomLeftBack,     _nBLB, col);
            verts[vertOffset+14] = new VertexPositionNormalColor(pos + bottomRightBack,    _nBRB, col);
            verts[vertOffset+15] = new VertexPositionNormalColor(pos + bottomRightFront,   _nBRF, col);

            // Left face
            verts[vertOffset+16] = new VertexPositionNormalColor(pos + topLeftFront,      _nTLF, col);
            verts[vertOffset+17] = new VertexPositionNormalColor(pos + bottomLeftBack,     _nBLB, col);
            verts[vertOffset+18] = new VertexPositionNormalColor(pos + bottomLeftFront,    _nBLF, col);
            verts[vertOffset+19] = new VertexPositionNormalColor(pos + topLeftBack,        _nTLB, col);

            // Right face
            verts[vertOffset+20] = new VertexPositionNormalColor(pos + topRightFront,     _nTRF, col);
            verts[vertOffset+21] = new VertexPositionNormalColor(pos + bottomRightFront,   _nBRF, col);
            verts[vertOffset+22] = new VertexPositionNormalColor(pos + bottomRightBack,    _nBRB, col);
            verts[vertOffset+23] = new VertexPositionNormalColor(pos + topRightBack,       _nTRB, col);

            for (int i = 0; i < 36; i++) indexes[(partNum * 36) + i] = (short)((partNum*24) + cubeIndices[i]);
        }
    }

}
