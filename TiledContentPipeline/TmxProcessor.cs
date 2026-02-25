using System;
using System.ComponentModel;
using System.IO;
using System.Xml;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Graphics;

namespace TiledContentPipeline
{
	[ContentProcessor(DisplayName = "TMX Processor")]
	public class TmxProcessor : ContentProcessor<XmlDocument, MapContent>
	{
		[DisplayName("TileSet Directory")]
		[Description("The directory (relative to the content root) in which the processor will find the tile sheet images.")]
		public string TileSetDirectory { get; set; }

		public override MapContent Process(XmlDocument input, ContentProcessorContext context)
		{
			// get our MapContent which does all the parsing of the XmlDocument we need
			MapContent content = new MapContent(input);

			// now we do some processing on tile sets to load external textures and figure out tile regions
			foreach (var tileSet in content.TileSets)
			{
				// get the full path to the file
				string path = string.IsNullOrEmpty(TileSetDirectory) ? tileSet.Image : Path.Combine(TileSetDirectory, tileSet.Image);
				string asset = path.Remove(path.LastIndexOf('.'));
				path = Path.Combine(Directory.GetCurrentDirectory(), path);

                //if (path.StartsWith("\\")) path = path.Substring(1);
                //if (asset.StartsWith("\\")) asset = asset.Substring(1);


				// build the asset as an external reference
				OpaqueDataDictionary data = new OpaqueDataDictionary();
				data.Add("GenerateMipmaps", false);
				data.Add("ResizeToPowerOfTwo", false);
				data.Add("TextureFormat", TextureProcessorOutputFormat.Color);
				data.Add("ColorKeyEnabled", tileSet.ColorKey.HasValue);
				data.Add("ColorKeyColor", tileSet.ColorKey.HasValue ? tileSet.ColorKey.Value : Microsoft.Xna.Framework.Color.Magenta);
				tileSet.Texture = context.BuildAsset<TextureContent, TextureContent>(
					new ExternalReference<TextureContent>(path),
					"TextureProcessor",
					data,
					"TextureImporter",
					asset);

                string whitefn = Path.GetFileNameWithoutExtension(tileSet.Image);
                string ext = Path.GetExtension(tileSet.Image);

                //tileSet.Image = whitefn + "-white" + ext;

                //path = string.IsNullOrEmpty(TileSetDirectory) ? tileSet.Image : Path.Combine(TileSetDirectory, tileSet.Image);
                //asset = path.Remove(path.LastIndexOf('.'));
                //path = Path.Combine(Directory.GetCurrentDirectory(), path);

                //tileSet.WhiteTexture = context.BuildAsset<TextureContent, TextureContent>(
                //    new ExternalReference<TextureContent>(path),
                //    "TextureProcessor",
                //    data,
                //    "TextureImporter",
                //    asset);

				// load the image so we can compute the individual tile source rectangles
				GetImageSize(path, out int imageWidth, out int imageHeight);

				// remove the margins from our calculations
				imageWidth -= tileSet.Margin;
				imageHeight -= tileSet.Margin;

				// figure out how many frames fit on the X axis
				int frameCountX = 1;
				while (frameCountX * tileSet.TileWidth < imageWidth)
				{
					frameCountX++;
					imageWidth -= tileSet.Spacing;
				}

				// figure out how many frames fit on the Y axis
				int frameCountY = 1;
				while (frameCountY * tileSet.TileHeight < imageHeight)
				{
					frameCountY++;
					imageHeight -= tileSet.Spacing;
				}

				// make our tiles. tiles are numbered by row, left to right.
				for (int y = 0; y < frameCountY; y++)
				{
					for (int x = 0; x < frameCountX; x++)
					{
						Tile tile = new Tile();

						// calculate the source rectangle
						int rx = tileSet.Margin + x * (tileSet.TileWidth + tileSet.Spacing);
						int ry = tileSet.Margin + y * (tileSet.TileHeight + tileSet.Spacing);
						tile.Source = new Microsoft.Xna.Framework.Rectangle(rx, ry, tileSet.TileWidth, tileSet.TileHeight);

						// get any properties from the tile set
						if (tileSet.TileProperties.ContainsKey((y * frameCountX) +  x))
						{
							tile.Properties = tileSet.TileProperties[(y * frameCountX) + x];
						}

						// save the tile
						tileSet.Tiles.Add(tile);
					}
				}
			}

			return content;
		}

		/// <summary>
		/// Reads image dimensions from PNG or BMP file headers without requiring System.Drawing.
		/// </summary>
		private static void GetImageSize(string path, out int width, out int height)
		{
			width = 0;
			height = 0;
			using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
			using var br = new BinaryReader(fs);

			// PNG: signature is 8 bytes, then IHDR chunk (4-byte length + 4-byte 'IHDR' + 4-byte w + 4-byte h)
			if (fs.Length >= 24)
			{
				byte[] sig = br.ReadBytes(8);
				if (sig[0] == 0x89 && sig[1] == 0x50 && sig[2] == 0x4E && sig[3] == 0x47)
				{
					// Skip 4-byte chunk length + 4-byte 'IHDR'
					br.ReadBytes(8);
					// Width and height are big-endian
					byte[] wb = br.ReadBytes(4);
					byte[] hb = br.ReadBytes(4);
					if (BitConverter.IsLittleEndian) { Array.Reverse(wb); Array.Reverse(hb); }
					width = BitConverter.ToInt32(wb, 0);
					height = BitConverter.ToInt32(hb, 0);
					return;
				}

				// BMP: signature 'BM', width at offset 18, height at offset 22 (little-endian)
				fs.Seek(0, SeekOrigin.Begin);
				br.BaseStream.Position = 0;
				if (sig[0] == 0x42 && sig[1] == 0x4D && fs.Length >= 26)
				{
					fs.Seek(18, SeekOrigin.Begin);
					width = br.ReadInt32();
					height = Math.Abs(br.ReadInt32());
					return;
				}
			}

			// Fallback: try JPEG (SOF0 marker) – or throw for unsupported formats
			throw new System.NotSupportedException(
				$"Cannot read dimensions from image '{path}'. Only PNG and BMP tile sheet files are supported.");
		}
	}
}
