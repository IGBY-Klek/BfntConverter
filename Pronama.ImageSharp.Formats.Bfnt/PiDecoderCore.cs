using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal sealed class PiDecoderCore
    {
        private BinaryReader _currentBinaryReader;
        private PiMetadata _piMetadata;
        private List<Rgb24> _palette;

        public PiDecoderCore(Configuration configuration)
        {
            Configuration = configuration;
        }

        public Configuration Configuration { get; }

        public ImageMetadata Metadata { get; private set; }

        public Image<TPixel> Decode<TPixel>(Stream stream, CancellationToken cancellationToken) where TPixel : unmanaged, IPixel<TPixel>
        {
            _currentBinaryReader = new BinaryReader(stream);

            ReadHeader();
            var image = ReadData<TPixel>();

            _currentBinaryReader.Close();

            return image;
        }

        public IImageInfo Identify(Stream stream, CancellationToken cancellationToken)
        {
            _currentBinaryReader = new BinaryReader(stream);
            ReadHeader();
            _currentBinaryReader.Close();

            return new PiImageInfo(new PixelTypeInfo(_piMetadata.NormalizedBitDepth), _piMetadata.Width, _piMetadata.Height, Metadata);
        }

        private void ReadHeader()
        {
            Metadata = new ImageMetadata();
            _piMetadata = Metadata.GetFormatMetadata(PiFormat.Instance);

            var br = _currentBinaryReader;
            var prefix = br.ReadBytes(2);
            if (!prefix.AsSpan().SequenceEqual(PiConstants.HeaderBytes))
            {
                throw new InvalidImageContentException("PI header was not found.");
            }

            SeekImageHeader(br);

            _piMetadata.Mode = br.ReadByte();
            _piMetadata.AspectRatioX = br.ReadByte();
            _piMetadata.AspectRatioY = br.ReadByte();
            _piMetadata.BitDepth = br.ReadByte();
            _piMetadata.CompressorModel = Encoding.ASCII.GetString(br.ReadBytes(4));
            _piMetadata.CompressorDataSize = ReadUInt16BigEndian(br);

            if (_piMetadata.CompressorDataSize > 0)
            {
                br.ReadBytes(_piMetadata.CompressorDataSize);
            }

            _piMetadata.Width = ReadUInt16BigEndian(br);
            _piMetadata.Height = ReadUInt16BigEndian(br);

            if (_piMetadata.NormalizedBitDepth is not (4 or 8))
            {
                throw new NotSupportedException($"PI形式の{_piMetadata.BitDepth}bit画像は、サポートしていません。");
            }

            _palette = new List<Rgb24>(_piMetadata.ColorCount);
            for (var i = 0; i < _piMetadata.ColorCount; i++)
            {
                var r = ScalePaletteComponent(br.ReadByte());
                var g = ScalePaletteComponent(br.ReadByte());
                var b = ScalePaletteComponent(br.ReadByte());
                var color = new Rgb24(r, g, b);
                _palette.Add(color);
                _piMetadata.Palette.Add(color);
            }
        }

        private static void SeekImageHeader(BinaryReader br)
        {
            var foundEndOfComment = false;
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                var value = br.ReadByte();
                if (!foundEndOfComment)
                {
                    foundEndOfComment = value == 0x1a;
                }
                else if (value == 0)
                {
                    return;
                }
            }

            throw new InvalidImageContentException("PI image header was not found.");
        }

        private Image<TPixel> ReadData<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
        {
            var pixels = DecompressPixels();
            var image = new Image<TPixel>(Configuration, _piMetadata.Width, _piMetadata.Height);

            image.ProcessPixelRows(accessor =>
            {
                var index = 0;
                for (var y = 0; y < _piMetadata.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < _piMetadata.Width; x++)
                    {
                        var paletteIndex = pixels[index++];
                        var color = _palette[paletteIndex];
                        row[x].FromRgb24(color);
                    }
                }
            });

            return image;
        }

        private byte[] DecompressPixels()
        {
            var width = _piMetadata.Width;
            var pixelCount = _piMetadata.Width * _piMetadata.Height;
            var colors = _piMetadata.ColorCount;
            var output = new byte[pixelCount];
            var deltaTable = CreateDeltaTable(colors);
            var bitReader = new PiBitReader(_currentBinaryReader);
            var outputOffset = 0;
            var lastByteOut = 0;
            var lastRepeatLocation = -1;
            var doingRepetition = true;

            void ProcessDeltaCode()
            {
                if (outputOffset >= output.Length)
                {
                    return;
                }

                var delta = ReadDeltaCode(bitReader, _piMetadata.NormalizedBitDepth);
                var rowOffset = lastByteOut * colors;
                var color = deltaTable[rowOffset + delta];

                for (var i = delta; i > 0; i--)
                {
                    deltaTable[rowOffset + i] = deltaTable[rowOffset + i - 1];
                }
                deltaTable[rowOffset] = color;

                output[outputOffset++] = color;
                lastByteOut = color;
            }

            void ProcessRepeatCode(int lengthPairsToSubtract)
            {
                var location = ReadLocationCode(bitReader);
                if (location == lastRepeatLocation)
                {
                    doingRepetition = false;
                    lastByteOut = outputOffset == 0 ? 0 : output[outputOffset - 1];
                    return;
                }

                lastRepeatLocation = location;
                var lengthPairs = ReadRepeatLength(bitReader) - lengthPairsToSubtract;
                var length = Math.Max(0, lengthPairs) * 2;
                if (outputOffset + length > output.Length)
                {
                    length = output.Length - outputOffset;
                }

                if (length == 0)
                {
                    return;
                }

                switch (location)
                {
                    case 0:
                        CopyPreviousBytes(output, ref outputOffset, length);
                        break;
                    case 1:
                        CopyBytes(output, ref outputOffset, width, length, reverseOutOfBoundsFiller: false);
                        break;
                    case 2:
                        CopyBytes(output, ref outputOffset, width * 2, length, reverseOutOfBoundsFiller: false);
                        break;
                    case 3:
                        CopyBytes(output, ref outputOffset, width - 1, length, reverseOutOfBoundsFiller: true);
                        break;
                    case 4:
                        CopyBytes(output, ref outputOffset, width + 1, length, reverseOutOfBoundsFiller: true);
                        break;
                }
            }

            ProcessDeltaCode();
            ProcessDeltaCode();
            ProcessRepeatCode(1);

            while (outputOffset < output.Length)
            {
                if (doingRepetition)
                {
                    ProcessRepeatCode(0);
                }
                else
                {
                    ProcessDeltaCode();
                    ProcessDeltaCode();

                    if (outputOffset >= output.Length)
                    {
                        break;
                    }

                    if (bitReader.ReadBit() == 0)
                    {
                        doingRepetition = true;
                        lastRepeatLocation = -1;
                    }
                }
            }

            return output;
        }

        private static byte[] CreateDeltaTable(int colors)
        {
            var table = new byte[colors * colors];
            for (var a = 0; a < colors; a++)
            {
                for (var b = 0; b < colors; b++)
                {
                    table[a * colors + b] = (byte)((colors + a - b) % colors);
                }
            }

            return table;
        }

        private static int ReadDeltaCode(PiBitReader bitReader, int bitDepth)
        {
            if (bitReader.ReadBit() == 1)
            {
                return bitReader.ReadBits(1);
            }

            if (bitReader.ReadBit() == 0)
            {
                return 2 + bitReader.ReadBits(1);
            }

            if (bitReader.ReadBit() == 0)
            {
                return 4 + bitReader.ReadBits(2);
            }

            if (bitDepth == 4)
            {
                return 8 + bitReader.ReadBits(3);
            }

            if (bitReader.ReadBit() == 0)
            {
                return 8 + bitReader.ReadBits(3);
            }

            if (bitReader.ReadBit() == 0)
            {
                return 16 + bitReader.ReadBits(4);
            }

            if (bitReader.ReadBit() == 0)
            {
                return 32 + bitReader.ReadBits(5);
            }

            if (bitReader.ReadBit() == 0)
            {
                return 64 + bitReader.ReadBits(6);
            }

            return 128 + bitReader.ReadBits(7);
        }

        private static int ReadLocationCode(PiBitReader bitReader)
        {
            var first = bitReader.ReadBit();
            var second = bitReader.ReadBit();

            if (first == 0 && second == 0)
            {
                return 0;
            }

            if (first == 0 && second == 1)
            {
                return 1;
            }

            if (first == 1 && second == 0)
            {
                return 2;
            }

            return bitReader.ReadBit() == 0 ? 3 : 4;
        }

        private static int ReadRepeatLength(PiBitReader bitReader)
        {
            var ones = 0;
            while (bitReader.ReadBit() == 1)
            {
                ones++;
            }

            return (1 << ones) + bitReader.ReadBits(ones);
        }

        private static void CopyPreviousBytes(byte[] output, ref int outputOffset, int length)
        {
            if (outputOffset < 2)
            {
                return;
            }

            var previousPatternLength = outputOffset < 4 || output[outputOffset - 1] == output[outputOffset - 2] ? 2 : 4;
            CopyPattern(output, ref outputOffset, previousPatternLength, length);
        }

        private static void CopyPattern(byte[] output, ref int outputOffset, int patternLength, int length)
        {
            var source = outputOffset - patternLength;
            for (var i = 0; i < length && outputOffset < output.Length; i++)
            {
                output[outputOffset++] = output[source + i % patternLength];
            }
        }

        private static void CopyBytes(byte[] output, ref int outputOffset, int repeatOffset, int length, bool reverseOutOfBoundsFiller)
        {
            var outOfBoundsCount = repeatOffset - outputOffset;
            if (outOfBoundsCount > 0)
            {
                var fillerLength = Math.Min(length, outOfBoundsCount);
                WriteFirstWord(output, ref outputOffset, fillerLength, reverseOutOfBoundsFiller);
                length -= fillerLength;
            }

            var source = outputOffset - repeatOffset;
            for (var i = 0; i < length && outputOffset < output.Length; i++)
            {
                output[outputOffset++] = output[source + i];
            }
        }

        private static void WriteFirstWord(byte[] output, ref int outputOffset, int length, bool reverse)
        {
            var first = output.Length > 0 ? output[0] : (byte)0;
            var second = output.Length > 1 ? output[1] : first;

            for (var i = 0; i < length && outputOffset < output.Length; i++)
            {
                output[outputOffset++] = (i & 1) == 0
                    ? (reverse ? second : first)
                    : (reverse ? first : second);
            }
        }

        private static ushort ReadUInt16BigEndian(BinaryReader br)
        {
            var high = br.ReadByte();
            var low = br.ReadByte();
            return (ushort)((high << 8) | low);
        }

        private static byte ScalePaletteComponent(byte value)
        {
            return (byte)((value & 0xf0) | (value >> 4));
        }

        private sealed class PiBitReader
        {
            private readonly BinaryReader _reader;
            private int _currentByte;
            private int _mask;

            public PiBitReader(BinaryReader reader)
            {
                _reader = reader;
            }

            public int ReadBit()
            {
                if (_mask == 0)
                {
                    _currentByte = _reader.ReadByte();
                    _mask = 0x80;
                }

                var bit = (_currentByte & _mask) == 0 ? 0 : 1;
                _mask >>= 1;
                return bit;
            }

            public int ReadBits(int count)
            {
                var value = 0;
                for (var i = 0; i < count; i++)
                {
                    value = (value << 1) | ReadBit();
                }

                return value;
            }
        }
    }
}
