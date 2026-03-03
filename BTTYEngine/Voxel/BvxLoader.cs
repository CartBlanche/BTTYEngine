using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace VoxelShooter
{
    /// <summary>
    /// Loads VoxelSprites from the .bvx v1 file format.
    ///
    /// .bvx v1 header layout (all little-endian, 1052 bytes total before voxel data):
    ///   [0-3]   Magic      "BVXF"
    ///   [4]     Version    1
    ///   [5-7]   Reserved   (ignored on read)
    ///   [8-9]   SizeX      uint16
    ///   [10-11] SizeY      uint16
    ///   [12-13] SizeZ      uint16
    ///   [14-15] FrameCount uint16
    ///   [16-17] PivotX     uint16
    ///   [18-19] PivotY     uint16
    ///   [20-21] PivotZ     uint16
    ///   [22-23] FrameRate  uint16  (fps; 0 = no animation)
    ///   [24-26] Reserved   (ignored on read)
    ///   [27]    PaletteSize byte   (0 means 256 for v1. See note below)
    ///   [28-1051] Palette  256 × RGBA (4 bytes each)
    ///   [1052+] Voxel data [frame][z][y][x], 1 byte per voxel (palette index)
    ///
    /// PaletteSize note: the value 256 overflows a byte, so v1 stores 0.
    /// The loader treats PaletteSize == 0 as 256 when Version == 1.
    /// Index 0 is always air (empty); indices 1-255 are solid voxels.
    ///
    /// No Y-flip is applied: VxsToVox + VoxToBvx already remapped axes into
    /// BTTYEngine convention (X-right, Y-up, Z-depth).
    /// </summary>
    public static class BvxLoader
    {
        private const int HeaderSize    = 1052;
        private const int PaletteOffset = 28;
        private const int PaletteBytes  = 1024; // 256 × 4

        public static void LoadSprite(string fn, ref VoxelSprite sprite)
        {
            byte[] data;
            using (Stream raw = TitleContainer.OpenStream(fn))
            {
                using var ms = new MemoryStream();
                raw.CopyTo(ms);
                data = ms.ToArray();
            }

            // Validate magic & version
            if (data.Length < HeaderSize)
                throw new InvalidDataException($"\"{fn}\" is too small to be a valid .bvx file.");

            if (data[0] != 'B' || data[1] != 'V' || data[2] != 'X' || data[3] != 'F')
                throw new InvalidDataException($"\"{fn}\" does not have the BVXF magic bytes.");

            byte version = data[4];
            if (version != 1)
                throw new InvalidDataException($"\"{fn}\" has unsupported .bvx version {version} (only v1 is supported).");

            // Read header fields
            int sizeX      = BitConverter.ToUInt16(data, 8);
            int sizeY      = BitConverter.ToUInt16(data, 10);
            int sizeZ      = BitConverter.ToUInt16(data, 12);
            int frameCount = BitConverter.ToUInt16(data, 14);
            int frameRate  = BitConverter.ToUInt16(data, 22);

            // PaletteSize: 0 stored in file == 256 for v1
            int paletteSize = data[27] == 0 ? 256 : data[27];

            int expectedDataSize = HeaderSize + (frameCount * sizeZ * sizeY * sizeX);
            if (data.Length < expectedDataSize)
                throw new InvalidDataException(
                    $"\"{fn}\" is truncated: expected {expectedDataSize} bytes, got {data.Length}.");

            // Build Color palette
            var palette = new Color[paletteSize];
            for (int i = 0; i < paletteSize; i++)
            {
                int slot  = PaletteOffset + i * 4;
                palette[i] = new Color(data[slot], data[slot + 1], data[slot + 2], data[slot + 3]);
            }
            // palette[0] is air. Its colour doesn't matter, but it's available.

            // Initialise VoxelSprite and AnimChunks
            sprite = new VoxelSprite(sizeX, sizeY, sizeZ);
            sprite.AnimChunks.Clear();
            sprite.ChunkRTs.Clear();
            sprite.FrameRate = frameRate;

            // Read voxel data — layout [frame][z][y][x]
            int frameStride = sizeZ * sizeY * sizeX;

            for (int f = 0; f < frameCount; f++)
            {
                sprite.AddFrame(false);
                AnimChunk chunk = sprite.AnimChunks[f];

                int frameBase = HeaderSize + f * frameStride;

                for (int z = 0; z < sizeZ; z++)
                {
                    for (int y = 0; y < sizeY; y++)
                    {
                        for (int x = 0; x < sizeX; x++)
                        {
                            int   offset    = frameBase + z * sizeY * sizeX + y * sizeX + x;
                            byte  index     = data[offset];
                            if (index == 0) continue; // air — leave voxel inactive

                            chunk.SetVoxel(x, y, z, true, palette[index]);
                        }
                    }
                }

                chunk.UpdateMesh();
            }

            GC.Collect();
        }
    }
}
