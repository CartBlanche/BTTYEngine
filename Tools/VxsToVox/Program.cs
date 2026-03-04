// VxsToVox, converts .vxs (GarethIW format) to MagicaVoxel .vox
// Usage: VxsToVox <input.vxs> <output.vox>
//
// Axis remap applied during conversion:
//   GarethIW (X-right, Y-up, Z-depth) → MagicaVoxel (X-right, Y-forward, Z-up)
//       btty.X  →  vox.X
//       btty.Y  →  vox.Z   (Y-up becomes Z-up)
//       btty.Z  →  vox.Y   (depth becomes Y-forward)
//
// .vxs is also Y-down, so the Y-flip (ys-1-vy) is applied before the remap.
// VoxToBvx inverts this remap when writing the final .bvx file.

using System.IO.Compression;
using System.Text;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: VxsToVox <input.vxs> <output.vox>");
    return 1;
}

string inputPath  = args[0];
string outputPath = args[1];

// 1. Read & GZip-decompress the .vxs file
byte[] buffer;
{
    using var raw = File.OpenRead(inputPath);
    using var ms  = new MemoryStream();
    raw.CopyTo(ms);

    // Last 4 bytes = little-endian int32 uncompressed length
    var lb = new byte[4];
    ms.Position = ms.Length - 4;
    ms.ReadExactly(lb, 0, 4);
    int msgLength = BitConverter.ToInt32(lb, 0);

    buffer = new byte[msgLength];
    ms.Position = 0;
    using var gz = new GZipStream(ms, CompressionMode.Decompress);
    gz.ReadExactly(buffer, 0, msgLength);
}

// 2. Parse .vxs header
int pos = 0;
int xs         = buffer[pos++]; // width  (btty X)
int ys         = buffer[pos++]; // height (btty Y, stored Y-down)
int zs         = buffer[pos++]; // depth  (btty Z)
int frameCount = buffer[pos++];

// Skip 10 × 3-byte swatches (editor colour swatches, unused at runtime)
pos += 30;

// 3. Parse voxel frames
// Each voxel: vx(1) vy(1) vz(1) R(1) G(1) B(1) = 6 bytes
// A frame ends when the next byte is ASCII 'c' (0x63).
var frames = new List<List<VoxVoxel>>(frameCount);

for (int f = 0; f < frameCount; f++)
{
    var voxels = new List<VoxVoxel>();
    while (pos < buffer.Length)
    {
        if (buffer[pos] == (byte)'c')
        {
            pos++; // consume frame sentinel
            break;
        }

        // Guard: need a full 6-byte record; the final frame may have no sentinel.
        if (pos + 6 > buffer.Length) break;

        byte vx = buffer[pos];
        byte vy = buffer[pos + 1];
        byte vz = buffer[pos + 2];
        byte r  = buffer[pos + 3];
        byte g  = buffer[pos + 4];
        byte b  = buffer[pos + 5];
        pos += 6;

        // .vxs is Y-down → flip to Y-up (GarethIW convention)
        byte vyUp = (byte)(ys - 1 - vy);

        // Remap to MagicaVoxel (X-right, Y-forward, Z-up)
        //   btty.X  → vox.X  (unchanged)
        //   btty.Y  → vox.Z  (up axis)
        //   btty.Z  → vox.Y  (depth axis)
        byte voxX = vx;
        byte voxY = vz;    // btty Z  → vox Y
        byte voxZ = vyUp;  // btty Y  → vox Z

        voxels.Add(new VoxVoxel(voxX, voxY, voxZ, r, g, b));
    }
    frames.Add(voxels);
}

// 4. Build palette (256 entries, index 0 = air)
// MagicaVoxel RGBA chunk: 256 × 4 bytes; voxel byte I references RGBA[I-1].
var colorToIndex  = new Dictionary<(byte R, byte G, byte B), byte>();
var paletteRgba   = new byte[256 * 4]; // written as-is into the RGBA chunk
int nextIndex     = 1;                 // 1-based (int so >255 check works); 0 = air

foreach (var frame in frames)
{
    foreach (var v in frame)
    {
        var key = (v.R, v.G, v.B);
        if (!colorToIndex.ContainsKey(key))
        {
            if (nextIndex > 255)
            {
                // More than 255 unique colours, find nearest already-registered colour
                // (simple nearest-neighbour in RGB space).
                int bestIdx  = 1;
                int bestDist = int.MaxValue;
                foreach (var (existKey, existIdx) in colorToIndex)
                {
                    int dr = v.R - existKey.R;
                    int dg = v.G - existKey.G;
                    int db = v.B - existKey.B;
                    int dist = dr * dr + dg * dg + db * db;
                    if (dist < bestDist) { bestDist = dist; bestIdx = existIdx; }
                }
                colorToIndex[key] = (byte)bestIdx;
                continue;
            }

            colorToIndex[key] = (byte)nextIndex;

            // RGBA chunk is 0-based; voxel reference nextIndex maps to slot (nextIndex-1)
            int slot = (nextIndex - 1) * 4;
            paletteRgba[slot]     = v.R;
            paletteRgba[slot + 1] = v.G;
            paletteRgba[slot + 2] = v.B;
            paletteRgba[slot + 3] = 255; // fully opaque
            nextIndex++;
        }
    }
}

// 5. Write .vox file
// After axis remap the MagicaVoxel grid dimensions are:
//   vox.X extent = btty.X = xs
//   vox.Y extent = btty.Z = zs
//   vox.Z extent = btty.Y = ys
int voxSizeX = xs;
int voxSizeY = zs; // btty Z became vox Y
int voxSizeZ = ys; // btty Y became vox Z

// local helpers -----------------------------------------------------------
static byte[] MakeSizeContent(int sx, int sy, int sz)
{
    using var ms2 = new MemoryStream(12);
    using var bw2 = new BinaryWriter(ms2);
    bw2.Write(sx); bw2.Write(sy); bw2.Write(sz);
    return ms2.ToArray();
}

static byte[] MakeXyziContent(List<VoxVoxel> voxels, Dictionary<(byte, byte, byte), byte> lut)
{
    using var ms2 = new MemoryStream();
    using var bw2 = new BinaryWriter(ms2);
    bw2.Write(voxels.Count);
    foreach (var v in voxels)
    {
        bw2.Write(v.X);
        bw2.Write(v.Y);
        bw2.Write(v.Z);
        bw2.Write(lut[(v.R, v.G, v.B)]);
    }
    return ms2.ToArray();
}

static void WriteChunk(BinaryWriter bw, string id, byte[] content, byte[] children)
{
    bw.Write(Encoding.ASCII.GetBytes(id));
    bw.Write(content.Length);
    bw.Write(children.Length);
    bw.Write(content);
    bw.Write(children);
}
// -------------------------------------------------------------------------

// Accumulate all child chunks for MAIN
using var childMs = new MemoryStream();
using (var cw = new BinaryWriter(childMs, Encoding.ASCII, leaveOpen: true))
{
    foreach (var frame in frames)
    {
        WriteChunk(cw, "SIZE", MakeSizeContent(voxSizeX, voxSizeY, voxSizeZ), []);
        WriteChunk(cw, "XYZI", MakeXyziContent(frame, colorToIndex), []);
    }
    WriteChunk(cw, "RGBA", paletteRgba, []);
}

byte[] childBytes = childMs.ToArray();

using var outFs = File.Create(outputPath);
using var w     = new BinaryWriter(outFs, Encoding.ASCII);

w.Write(Encoding.ASCII.GetBytes("VOX ")); // magic
w.Write(150);                             // version
WriteChunk(w, "MAIN", [], childBytes);

Console.WriteLine(
    $"Converted \"{inputPath}\" → \"{outputPath}\" " +
    $"({frames.Count} frame(s), {nextIndex - 1} colour(s))");
return 0;

record struct VoxVoxel(byte X, byte Y, byte Z, byte R, byte G, byte B);
