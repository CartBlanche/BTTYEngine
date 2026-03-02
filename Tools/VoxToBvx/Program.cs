// VoxToBvx — converts MagicaVoxel .vox to BTTYEngine .bvx (v1)
// Usage: VoxToBvx <input.vox> [output.bvx]  (output defaults to input stem + .bvx)
//
// Axis remap (inverse of VxsToVox):
//   MagicaVoxel (X-right, Y-forward, Z-up) → BTTYEngine (X-right, Y-up, Z-depth)
//       vox.X  →  btty.X   (unchanged)
//       vox.Z  →  btty.Y   (Z-up becomes Y-up)
//       vox.Y  →  btty.Z   (Y-forward becomes depth)
//
// .bvx v1 layout  (all little-endian, uncompressed):
//   Offset  Size  Field
//   0       4     Magic "BVXF"
//   4       1     Version = 1
//   5       3     Reserved (0x00)
//   8       2     SizeX
//   10      2     SizeY
//   12      2     SizeZ
//   14      2     FrameCount
//   16      2     PivotX
//   18      2     PivotY
//   20      2     PivotZ
//   22      2     FrameRate
//   24      3     Reserved (0x00)
//   27      1     PaletteSize = 256
//   28      1024  Palette: 256 × RGBA (each 4 bytes, R G B A)
//   1052    N     Voxel data: [frame][z][y][x], 1 byte per voxel (palette index)

using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: VoxToBvx <input.vox> [output.bvx]");
    return 1;
}

string inputPath  = args[0];
string outputPath = args.Length >= 2
    ? args[1]
    : Path.ChangeExtension(inputPath, ".bvx");

// ── 1. Read .vox ──────────────────────────────────────────────────────────────
byte[] voxBytes = File.ReadAllBytes(inputPath);

// Validate magic & version
if (Encoding.ASCII.GetString(voxBytes, 0, 4) != "VOX ")
    throw new InvalidDataException($"\"{inputPath}\" is not a MagicaVoxel .vox file (bad magic).");

int version = BitConverter.ToInt32(voxBytes, 4);
if (version != 150)
    Console.Error.WriteLine($"Warning: unexpected .vox version {version} (expected 150) — continuing anyway.");

// ── 2. Parse chunks ───────────────────────────────────────────────────────────
static ChunkInfo ReadChunkHeader(byte[] data, int offset)
{
    string id          = Encoding.ASCII.GetString(data, offset, 4);
    int    contentSize = BitConverter.ToInt32(data, offset + 4);
    int    childSize   = BitConverter.ToInt32(data, offset + 8);
    return new ChunkInfo(id, offset + 12, contentSize, offset + 12 + contentSize, childSize);
}

// Collect all SIZE and XYZI chunks (in order) and one RGBA chunk.
// The MAIN chunk content is empty; its children follow immediately.
var mainChunk = ReadChunkHeader(voxBytes, 8);
// mainChunk.ContentOffset is where MAIN's children start (MAIN has no content).

var sizeChunks = new List<(int voxX, int voxY, int voxZ)>();
var xyziChunks = new List<(byte x, byte y, byte z, byte i)[]>();
byte[]? paletteRgba = null; // 256 × 4 bytes (RGBA)

int cursor  = mainChunk.ContentOffset + mainChunk.ContentSize; // start of MAIN children
int childEnd = cursor + mainChunk.ChildrenSize;

while (cursor < childEnd)
{
    var chunk = ReadChunkHeader(voxBytes, cursor);

    switch (chunk.Id)
    {
        case "SIZE":
        {
            int sx = BitConverter.ToInt32(voxBytes, chunk.ContentOffset);
            int sy = BitConverter.ToInt32(voxBytes, chunk.ContentOffset + 4);
            int sz = BitConverter.ToInt32(voxBytes, chunk.ContentOffset + 8);
            sizeChunks.Add((sx, sy, sz));
            break;
        }
        case "XYZI":
        {
            int count   = BitConverter.ToInt32(voxBytes, chunk.ContentOffset);
            var voxels  = new (byte x, byte y, byte z, byte i)[count];
            int dataPos = chunk.ContentOffset + 4;
            for (int v = 0; v < count; v++, dataPos += 4)
                voxels[v] = (voxBytes[dataPos], voxBytes[dataPos + 1], voxBytes[dataPos + 2], voxBytes[dataPos + 3]);
            xyziChunks.Add(voxels);
            break;
        }
        case "RGBA":
        {
            paletteRgba = new byte[1024];
            Array.Copy(voxBytes, chunk.ContentOffset, paletteRgba, 0, 1024);
            break;
        }
    }

    // Advance past this chunk (content + children)
    cursor += 12 + chunk.ContentSize + chunk.ChildrenSize;
}

if (sizeChunks.Count == 0)
    throw new InvalidDataException("No SIZE chunk found in .vox file.");
if (xyziChunks.Count != sizeChunks.Count)
    throw new InvalidDataException($"Mismatch: {sizeChunks.Count} SIZE chunk(s) but {xyziChunks.Count} XYZI chunk(s).");

// ── 3. Determine output dimensions ───────────────────────────────────────────
// All frames must share the same canvas in MagicaVoxel multi-model export;
// use the max extents seen across all SIZE chunks.
int voxSizeX = 0, voxSizeY = 0, voxSizeZ = 0;
foreach (var (sx, sy, sz) in sizeChunks)
{
    voxSizeX = Math.Max(voxSizeX, sx);
    voxSizeY = Math.Max(voxSizeY, sy);
    voxSizeZ = Math.Max(voxSizeZ, sz);
}

// Invert axis remap: vox→btty
//   vox.X  →  btty.X
//   vox.Z  →  btty.Y  (btty up-axis)
//   vox.Y  →  btty.Z  (btty depth-axis)
int bttyX = voxSizeX;
int bttyY = voxSizeZ; // vox Z was btty Y
int bttyZ = voxSizeY; // vox Y was btty Z

if (bttyX > 255 || bttyY > 255 || bttyZ > 255)
    Console.Error.WriteLine(
        $"Warning: dimension(s) exceed 255 — storing as uint16 in .bvx header but " +
        $"AnimChunk may need to be widened if it currently caps at byte dimensions.");

int frameCount = sizeChunks.Count;

// ── 4. Build the .bvx palette ─────────────────────────────────────────────────
// MagicaVoxel RGBA chunk: entry 0 = colour for voxel index 1, entry 1 = colour for 2, etc.
// (palette is rotated by one vs. voxel indices.)
// Voxel index 0 = air.  For .bvx we keep the same convention: index 0 = air.
// We write 256 × RGBA.

byte[] bvxPalette; // 256 × 4 = 1024 bytes
if (paletteRgba is not null)
{
    // Entry at slot k (0-based) in RGBA chunk is the colour for voxel index (k+1).
    // bvxPalette[0] = air (all zeros), bvxPalette[i] = RGBA chunk entry[i-1].
    bvxPalette = new byte[1024];
    // index 0 stays all zeros (air)
    for (int i = 1; i <= 255; i++)
    {
        int srcSlot = (i - 1) * 4; // RGBA chunk slot for voxel index i
        int dstSlot = i * 4;
        bvxPalette[dstSlot]     = paletteRgba[srcSlot];
        bvxPalette[dstSlot + 1] = paletteRgba[srcSlot + 1];
        bvxPalette[dstSlot + 2] = paletteRgba[srcSlot + 2];
        bvxPalette[dstSlot + 3] = paletteRgba[srcSlot + 3];
    }
}
else
{
    // No RGBA chunk — use a greyscale fallback palette so the file is still valid.
    Console.Error.WriteLine("Warning: no RGBA chunk in .vox file — using greyscale fallback palette.");
    bvxPalette = new byte[1024];
    for (int i = 1; i <= 255; i++)
    {
        byte grey = (byte)((i * 255) / 255);
        int slot  = i * 4;
        bvxPalette[slot]     = grey;
        bvxPalette[slot + 1] = grey;
        bvxPalette[slot + 2] = grey;
        bvxPalette[slot + 3] = 255;
    }
}

// ── 5. Build the voxel grid ───────────────────────────────────────────────────
// Layout: [frame][z][y][x] (all BTTYEngine coords after axis remap)
int frameStride = bttyZ * bttyY * bttyX;
var grid = new byte[frameCount * frameStride]; // all zeros = air

for (int f = 0; f < frameCount; f++)
{
    foreach (var (vx, vy, vz, ci) in xyziChunks[f])
    {
        if (ci == 0) continue; // air

        // Invert axis remap:
        //   vox.X  →  btty.X  (unchanged)
        //   vox.Z  →  btty.Y
        //   vox.Y  →  btty.Z
        int bx = vx;
        int by = vz; // vox Z → btty Y
        int bz = vy; // vox Y → btty Z

        if (bx >= bttyX || by >= bttyY || bz >= bttyZ) continue; // safety clamp

        int offset = (f * frameStride)
                   + (bz * bttyY * bttyX)
                   + (by * bttyX)
                   + bx;
        grid[offset] = ci;
    }
}

// ── 6. Write .bvx file ────────────────────────────────────────────────────────
using var outFs = File.Create(outputPath);
using var w     = new BinaryWriter(outFs, Encoding.ASCII);

// --- Header (1052 bytes) ---
// [0-3]  Magic
w.Write(Encoding.ASCII.GetBytes("BVXF"));
// [4]    Version
w.Write((byte)1);
// [5-7]  Reserved
w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
// [8-9]  SizeX
w.Write((ushort)bttyX);
// [10-11] SizeY
w.Write((ushort)bttyY);
// [12-13] SizeZ
w.Write((ushort)bttyZ);
// [14-15] FrameCount
w.Write((ushort)frameCount);
// [16-21] Pivot (default 0,0,0)
w.Write((ushort)0); // PivotX
w.Write((ushort)0); // PivotY
w.Write((ushort)0); // PivotZ
// [22-23] FrameRate (0 = no animation for single-frame; use 12 fps for multi-frame)
ushort frameRate = (ushort)(frameCount > 1 ? 12 : 0);
w.Write(frameRate);
// [24-26] Reserved
w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
// [27]   PaletteSize — 256 entries; stored as 0 (byte wraps: 256 % 256 == 0).
//         The loader checks Version == 1 and treats 0 as 256.
w.Write((byte)0);
// [28-1051] Palette (256 × RGBA = 1024 bytes)
w.Write(bvxPalette);

// --- Voxel data ---
w.Write(grid);

Console.WriteLine(
    $"Converted \"{inputPath}\" → \"{outputPath}\" " +
    $"({bttyX}×{bttyY}×{bttyZ}, {frameCount} frame(s), frameRate={frameRate} fps)");
return 0;

record struct ChunkInfo(string Id, int ContentOffset, int ContentSize, int ChildrenOffset, int ChildrenSize);
