using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BfntConverterApp
{
    public static class CdgDecoder
    {
        public const int PaletteColorCount = 16;
        private const int HeaderLength = 16;
        private const int PlaneAlpha = 0x1;
        private const int PlaneColor = 0x4;

        public static Image<Bgra32> Decode(string imagePath, string palettePath, out CdgInfo info)
        {
            var palette = LoadPalette(palettePath);
            var data = File.ReadAllBytes(imagePath);
            if (data.Length < HeaderLength)
                throw new InvalidDataException("CDG/CD2 文件头长度不足。 ");

            var planeSize = ReadUInt16LittleEndian(data, 0);
            var width = ReadUInt16LittleEndian(data, 2);
            var height = ReadUInt16LittleEndian(data, 4);
            var unknown1 = ReadUInt16LittleEndian(data, 6);
            var unknown2 = ReadUInt16LittleEndian(data, 8);
            var images = data[10] == 0 ? 1 : data[10];
            var headerAlpha = data[11];

            if (planeSize <= 0)
                throw new InvalidDataException("CDG/CD2 平面大小无效。 ");
            if (width <= 0 || height <= 0)
                throw new InvalidDataException("CDG/CD2 图像尺寸无效。 ");
            if (width % 8 != 0)
                throw new InvalidDataException("CDG/CD2 宽度必须是 8 的倍数。 ");

            var expectedPlaneSize = (width / 8) * height;
            if (planeSize < expectedPlaneSize)
                throw new InvalidDataException($"CDG/CD2 平面大小不足。需要至少 {expectedPlaneSize:#,0} 字节，但头部记录为 {planeSize:#,0} 字节。 ");

            var dataOffset = HeaderLength;
            var dataSize = data.Length - dataOffset;
            var planes = DetectPlaneFlags(dataSize, planeSize, images);
            var bytesPerImage = planeSize * GetPlaneCount(planes);
            var usedDataSize = bytesPerImage * images;
            var trailingByteCount = Math.Max(0, dataSize - usedDataSize);

            if (dataSize < usedDataSize)
                throw new InvalidDataException($"CDG/CD2 数据区长度不足。需要至少 {usedDataSize:#,0} 字节，实际为 {dataSize:#,0} 字节。 ");

            var rowBytes = width / 8;
            var outputHeight = height * images;
            var image = new Image<Bgra32>(width, outputHeight);

            image.ProcessPixelRows(accessor =>
            {
                for (var imageIndex = 0; imageIndex < images; imageIndex++)
                {
                    var imageOffset = dataOffset + (imageIndex * bytesPerImage);
                    var alphaPlaneOffset = (planes & PlaneAlpha) != 0 ? imageOffset : -1;
                    var colorPlaneOffset = imageOffset + ((planes & PlaneAlpha) != 0 ? planeSize : 0);

                    for (var y = 0; y < height; y++)
                    {
                        var outputY = (imageIndex * height) + (height - 1 - y);
                        var row = accessor.GetRowSpan(outputY);
                        for (var x = 0; x < width; x++)
                        {
                            var byteIndex = y * rowBytes + (x / 8);
                            var bitIndex = 7 - (x % 8);
                            var colorIndex = (planes & PlaneColor) != 0 ? 0 : 0x0f;
                            var alpha = (planes & PlaneAlpha) != 0 ? (byte)0 : byte.MaxValue;

                            if ((planes & PlaneAlpha) != 0)
                            {
                                var alphaBit = (data[alphaPlaneOffset + byteIndex] >> bitIndex) & 1;
                                alpha = alphaBit == 0 ? (byte)0 : byte.MaxValue;
                            }

                            if ((planes & PlaneColor) != 0)
                            {
                                for (var plane = 0; plane < PlaneColor; plane++)
                                {
                                    var planeOffset = colorPlaneOffset + (plane * planeSize);
                                    var bit = (data[planeOffset + byteIndex] >> bitIndex) & 1;
                                    colorIndex |= bit << plane;
                                }
                            }

                            var color = palette[colorIndex];
                            row[x] = new Bgra32(color.R, color.G, color.B, alpha);
                        }
                    }
                }
            });

            info = new CdgInfo(
                planeSize,
                width,
                height,
                unknown1,
                unknown2,
                images,
                headerAlpha,
                planes,
                trailingByteCount,
                palette);
            return image;
        }

        private static int DetectPlaneFlags(int dataSize, int planeSize, int images)
        {
            var minimumSize = planeSize * images;
            var planes = 0;
            var planeCounts = new[] { PlaneAlpha, PlaneColor, PlaneAlpha | PlaneColor };
            foreach (var planeCount in planeCounts)
            {
                if (dataSize >= minimumSize * planeCount)
                    planes = planeCount;
            }

            if (planes == 0)
                throw new InvalidDataException($"CDG/CD2 数据区长度不足。至少需要 {minimumSize:#,0} 字节，实际为 {dataSize:#,0} 字节。 ");

            return planes;
        }

        private static int GetPlaneCount(int planes)
        {
            var count = 0;
            if ((planes & PlaneAlpha) != 0)
                count++;
            if ((planes & PlaneColor) != 0)
                count += 4;

            return count;
        }

        private static IReadOnlyList<Rgb24> LoadPalette(string palettePath)
        {
            var paletteBytes = File.ReadAllBytes(palettePath);
            if (TryLoadJascPalette(paletteBytes, out var jascPalette))
                return jascPalette;

            if (paletteBytes.Length < PaletteColorCount * 3)
                throw new InvalidDataException("调色板必须至少包含 16*3 = 48 字节，或使用 JASC-PAL 文本格式。 ");

            var usesPc98FourBitChannels = paletteBytes.Take(PaletteColorCount * 3).All(value => value <= 0x0f);
            var palette = new List<Rgb24>(PaletteColorCount);
            for (var i = 0; i < PaletteColorCount; i++)
            {
                var r = paletteBytes[i * 3];
                var g = paletteBytes[i * 3 + 1];
                var b = paletteBytes[i * 3 + 2];

                if (usesPc98FourBitChannels)
                    palette.Add(new Rgb24((byte)(r * 17), (byte)(g * 17), (byte)(b * 17)));
                else
                    palette.Add(new Rgb24(r, g, b));
            }

            return palette;
        }

        private static bool TryLoadJascPalette(byte[] paletteBytes, out IReadOnlyList<Rgb24> palette)
        {
            palette = Array.Empty<Rgb24>();
            var text = System.Text.Encoding.ASCII.GetString(paletteBytes);

            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
            if (lines.Length < 4 || !string.Equals(lines[0], "JASC-PAL", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!int.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var colorCount) || colorCount < PaletteColorCount)
                throw new InvalidDataException("JASC-PAL 调色板至少需要 16 色。 ");
            if (lines.Length < PaletteColorCount + 3)
                throw new InvalidDataException("JASC-PAL 调色板颜色行数量不足。 ");

            var colors = new List<Rgb24>(PaletteColorCount);
            for (var i = 0; i < PaletteColorCount; i++)
            {
                var parts = lines[i + 3].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3 ||
                    !byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ||
                    !byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ||
                    !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                    throw new InvalidDataException("JASC-PAL 调色板颜色行格式无效。 ");

                colors.Add(new Rgb24(r, g, b));
            }

            palette = colors;
            return true;
        }

        private static int ReadUInt16LittleEndian(byte[] data, int offset) =>
            data[offset] | (data[offset + 1] << 8);
    }

    public sealed class CdgInfo
    {
        public CdgInfo(
            int planeSize,
            int width,
            int height,
            int unknown1,
            int unknown2,
            int images,
            int headerAlpha,
            int planes,
            int trailingByteCount,
            IReadOnlyList<Rgb24> palette)
        {
            PlaneSize = planeSize;
            Width = width;
            Height = height;
            Unknown1 = unknown1;
            Unknown2 = unknown2;
            Images = images;
            HeaderAlpha = headerAlpha;
            Planes = planes;
            TrailingByteCount = trailingByteCount;
            Palette = palette;
        }

        public int PlaneSize { get; }
        public int Width { get; }
        public int Height { get; }
        public int Unknown1 { get; }
        public int Unknown2 { get; }
        public int Images { get; }
        public int HeaderAlpha { get; }
        public int Planes { get; }
        public bool HasColorPlanes => (Planes & 0x4) != 0;
        public bool HasAlphaPlane => (Planes & 0x1) != 0;
        public int ColorPlaneCount => HasColorPlanes ? 4 : 0;
        public int PlaneCount => (HasAlphaPlane ? 1 : 0) + (HasColorPlanes ? 4 : 0);
        public int TrailingByteCount { get; }
        public IReadOnlyList<Rgb24> Palette { get; }
    }
}
