using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BfntConverterApp
{
    internal static class LegacyImageExporters
    {
        private const int PaletteColorCount = 16;
        private static readonly byte[] MptnHeader = { (byte)'M', (byte)'P', (byte)'T', (byte)'N' };

        public static void SaveMptn(Image<Bgra32> image, string file)
        {
            if (image.Width != 256 || image.Height != 256)
                throw new InvalidOperationException("MPTN/MPN 输出需要 256x256 的 16x16 图块表。 ");

            var (palette, indexes) = QuantizeTo16Colors(image);
            using var stream = File.Create(file);
            using var writer = new BinaryWriter(stream);
            writer.Write(MptnHeader);
            WriteUInt16LittleEndian(writer, 255);
            foreach (var color in palette)
            {
                writer.Write(color.R);
                writer.Write(color.G);
                writer.Write(color.B);
            }

            for (var tile = 0; tile < 256; tile++)
            {
                var tileX = (tile % 16) * 16;
                var tileY = (tile / 16) * 16;
                for (var plane = 0; plane < 4; plane++)
                {
                    for (var y = 0; y < 16; y++)
                    {
                        for (var xByte = 0; xByte < 2; xByte++)
                        {
                            byte value = 0;
                            for (var bit = 0; bit < 8; bit++)
                            {
                                var x = tileX + xByte * 8 + bit;
                                var index = indexes[(tileY + y) * image.Width + x];
                                value |= (byte)(((index >> plane) & 1) << (7 - bit));
                            }
                            writer.Write(value);
                        }
                    }
                }
            }
        }

        public static void SaveCdg(Image<Bgra32> image, string file)
        {
            if (image.Width <= 0 || image.Height <= 0 || image.Width % 8 != 0 || image.Height > ushort.MaxValue)
                throw new InvalidOperationException("CDG/CD2 输出需要宽度为 8 的倍数且高度不超过 65535。 ");

            var (palette, indexes) = QuantizeTo16Colors(image);
            var planeSize = (image.Width / 8) * image.Height;
            using var stream = File.Create(file);
            using var writer = new BinaryWriter(stream);
            WriteUInt16LittleEndian(writer, planeSize);
            WriteUInt16LittleEndian(writer, image.Width);
            WriteUInt16LittleEndian(writer, image.Height);
            WriteUInt16LittleEndian(writer, 0);
            WriteUInt16LittleEndian(writer, 0);
            writer.Write((byte)1);
            writer.Write((byte)0);
            writer.Write(new byte[4]);

            for (var plane = 0; plane < 4; plane++)
            {
                WriteCdgPlane(writer, indexes, image.Width, image.Height, plane);
            }

            SaveRgbPalette(Path.ChangeExtension(file, ".rgb"), palette);
        }

        public static void SavePi(Image<Bgra32> image, string file)
        {
            var (palette, indexes) = QuantizeTo16Colors(image);
            using var stream = File.Create(file);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'P'); writer.Write((byte)'i');
            writer.Write(System.Text.Encoding.ASCII.GetBytes("BFNT Converter"));
            writer.Write((byte)0x1a); writer.Write((byte)0);
            writer.Write((byte)0); writer.Write((byte)1); writer.Write((byte)1); writer.Write((byte)4);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("none"));
            writer.Write((byte)0); writer.Write((byte)0);
            WriteUInt16BigEndian(writer, image.Width);
            WriteUInt16BigEndian(writer, image.Height);
            foreach (var color in palette)
            {
                writer.Write((byte)(color.R & 0xf0));
                writer.Write((byte)(color.G & 0xf0));
                writer.Write((byte)(color.B & 0xf0));
            }

            var bitWriter = new PiBitWriter(writer);
            var deltaTable = CreateDeltaTable(16);
            var last = 0;
            for (var i = 0; i < indexes.Length; i++)
            {
                WriteDelta(bitWriter, deltaTable, ref last, indexes[i]);
                if (i == 1)
                {
                    bitWriter.WriteBits(0, 2); WriteRepeatLength(bitWriter, 1);
                    bitWriter.WriteBits(0, 2);
                }
                else if (i > 1 && (i & 1) == 1 && i < indexes.Length - 1)
                {
                    bitWriter.WriteBit(1);
                }
            }
            bitWriter.Flush();
        }

        private static void WriteCdgPlane(BinaryWriter writer, byte[] indexes, int width, int height, int plane)
        {
            for (var y = height - 1; y >= 0; y--)
                for (var xByte = 0; xByte < width / 8; xByte++)
                {
                    byte value = 0;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var index = indexes[y * width + xByte * 8 + bit];
                        value |= (byte)(((index >> plane) & 1) << (7 - bit));
                    }
                    writer.Write(value);
                }
        }

        private static (List<Rgb24> Palette, byte[] Indexes) QuantizeTo16Colors(Image<Bgra32> image)
        {
            var colors = new Dictionary<uint, Rgb24>();
            var pixels = new Bgra32[image.Width * image.Height];
            image.CopyPixelDataTo(pixels);
            foreach (var pixel in pixels)
            {
                var rgb = new Rgb24(pixel.R, pixel.G, pixel.B);
                colors.TryAdd(((uint)rgb.R << 16) | ((uint)rgb.G << 8) | rgb.B, rgb);
            }
            var palette = colors.Values.Take(PaletteColorCount).ToList();
            while (palette.Count < PaletteColorCount) palette.Add(new Rgb24(0, 0, 0));
            var indexes = pixels.Select(p => (byte)FindNearestPaletteIndex(palette, p)).ToArray();
            return (palette, indexes);
        }

        private static int FindNearestPaletteIndex(IReadOnlyList<Rgb24> palette, Bgra32 pixel)
        {
            var best = 0; var bestDistance = int.MaxValue;
            for (var i = 0; i < palette.Count; i++)
            {
                var dr = palette[i].R - pixel.R; var dg = palette[i].G - pixel.G; var db = palette[i].B - pixel.B;
                var distance = dr * dr + dg * dg + db * db;
                if (distance < bestDistance) { best = i; bestDistance = distance; }
            }
            return best;
        }

        private static byte[] CreateDeltaTable(int colors)
        {
            var table = new byte[colors * colors];
            for (var a = 0; a < colors; a++) for (var b = 0; b < colors; b++) table[a * colors + b] = (byte)((colors + a - b) % colors);
            return table;
        }

        private static void WriteDelta(PiBitWriter writer, byte[] table, ref int last, byte color)
        {
            var row = last * 16;
            var delta = Array.IndexOf(table, color, row, 16) - row;
            if (delta < 2) { writer.WriteBit(1); writer.WriteBits(delta, 1); }
            else if (delta < 4) { writer.WriteBits(0, 2); writer.WriteBits(delta - 2, 1); }
            else if (delta < 8) { writer.WriteBits(0b010, 3); writer.WriteBits(delta - 4, 2); }
            else { writer.WriteBits(0b011, 3); writer.WriteBits(delta - 8, 3); }
            for (var i = delta; i > 0; i--) table[row + i] = table[row + i - 1];
            table[row] = color;
            last = color;
        }

        private static void WriteRepeatLength(PiBitWriter writer, int length)
        {
            writer.WriteBit(0);
        }

        private static void SaveRgbPalette(string file, IReadOnlyList<Rgb24> palette)
        {
            using var stream = File.Create(file);
            foreach (var color in palette) { stream.WriteByte(color.R); stream.WriteByte(color.G); stream.WriteByte(color.B); }
        }

        private static void WriteUInt16LittleEndian(BinaryWriter writer, int value) { writer.Write((byte)(value & 0xff)); writer.Write((byte)(value >> 8)); }
        private static void WriteUInt16BigEndian(BinaryWriter writer, int value) { writer.Write((byte)(value >> 8)); writer.Write((byte)(value & 0xff)); }

        private sealed class PiBitWriter
        {
            private readonly BinaryWriter _writer; private int _current; private int _bits;
            public PiBitWriter(BinaryWriter writer) => _writer = writer;
            public void WriteBit(int bit) { _current = (_current << 1) | (bit & 1); if (++_bits == 8) FlushByte(); }
            public void WriteBits(int value, int count) { for (var i = count - 1; i >= 0; i--) WriteBit((value >> i) & 1); }
            public void Flush() { if (_bits > 0) { _current <<= 8 - _bits; FlushByte(); } }
            private void FlushByte() { _writer.Write((byte)_current); _current = 0; _bits = 0; }
        }
    }
}
