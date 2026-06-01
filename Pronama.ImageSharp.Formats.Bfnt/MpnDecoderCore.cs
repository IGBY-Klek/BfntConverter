using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal sealed class MpnDecoderCore
    {
        private BinaryReader _currentBinaryReader;
        private MpnMetadata _mpnMetadata;
        private List<Rgb24> _palette;

        public MpnDecoderCore(Configuration configuration)
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

            return new MpnImageInfo(new PixelTypeInfo(4), MpnConstants.CanvasWidth, MpnConstants.CanvasHeight, Metadata);
        }

        private void ReadHeader()
        {
            Metadata = new ImageMetadata();
            _mpnMetadata = Metadata.GetFormatMetadata(MpnFormat.Instance);

            var br = _currentBinaryReader;
            var header = br.ReadBytes(MpnConstants.HeaderSize);
            if (header.Length < MpnConstants.HeaderSize)
            {
                throw new InvalidImageContentException("MPN header is incomplete.");
            }

            if (!header.AsSpan(0, 4).SequenceEqual(MpnConstants.HeaderBytes))
            {
                throw new InvalidImageContentException("MPN header was not found.");
            }

            var tileCount = ReadUInt16LittleEndian(header, 4) + 1;
            if (tileCount > MpnConstants.MaxTileCount)
            {
                throw new InvalidImageContentException($"MPN tile count exceeds {MpnConstants.MaxTileCount}.");
            }

            _mpnMetadata.TileCount = (ushort)tileCount;
            _palette = new List<Rgb24>(MpnConstants.PaletteColorCount);

            for (var i = 0; i < MpnConstants.PaletteColorCount; i++)
            {
                var offset = 6 + (i * 3);
                var color = new Rgb24(header[offset], header[offset + 1], header[offset + 2]);
                _palette.Add(color);
                _mpnMetadata.Palette.Add(color);
            }
        }

        private Image<TPixel> ReadData<TPixel>() where TPixel : unmanaged, IPixel<TPixel>
        {
            var pixels = DecodePaletteIndexes();
            var image = new Image<TPixel>(Configuration, MpnConstants.CanvasWidth, MpnConstants.CanvasHeight);
            var imageMpnMetadata = image.Metadata.GetMpnMetadata();
            imageMpnMetadata.CopyFrom(_mpnMetadata);
            _mpnMetadata = imageMpnMetadata;
            Metadata = image.Metadata;

            image.ProcessPixelRows(accessor =>
            {
                var index = 0;
                for (var y = 0; y < MpnConstants.CanvasHeight; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < MpnConstants.CanvasWidth; x++)
                    {
                        row[x].FromRgb24(_palette[pixels[index++]]);
                    }
                }
            });

            return image;
        }

        private byte[] DecodePaletteIndexes()
        {
            var output = new byte[MpnConstants.CanvasWidth * MpnConstants.CanvasHeight];
            var br = _currentBinaryReader;

            for (var tileIndex = 0; tileIndex < _mpnMetadata.TileCount; tileIndex++)
            {
                var tileData = br.ReadBytes(MpnConstants.TileSize);
                if (tileData.Length < MpnConstants.TileSize)
                {
                    break;
                }

                DecodeTile(output, tileData, tileIndex);
            }

            if (_mpnMetadata.TileCount < MpnConstants.MaxTileCount)
            {
                DrawPaletteMarkerTile(output, _mpnMetadata.TileCount);
            }

            return output;
        }

        private static void DecodeTile(byte[] output, byte[] tileData, int tileIndex)
        {
            var tileX = (tileIndex % MpnConstants.TileColumns) * MpnConstants.TileWidth;
            var tileY = (tileIndex / MpnConstants.TileColumns) * MpnConstants.TileHeight;

            for (var y = 0; y < MpnConstants.TileHeight; y++)
            {
                for (var xByte = 0; xByte < 2; xByte++)
                {
                    var planeOffset = (y * 2) + xByte;
                    var bByte = tileData[planeOffset];
                    var rByte = tileData[MpnConstants.BytesPerPlane + planeOffset];
                    var gByte = tileData[(MpnConstants.BytesPerPlane * 2) + planeOffset];
                    var eByte = tileData[(MpnConstants.BytesPerPlane * 3) + planeOffset];

                    for (var bit = 0; bit < 8; bit++)
                    {
                        var shift = 7 - bit;
                        var paletteIndex = (((eByte >> shift) & 1) << 3) |
                                           (((gByte >> shift) & 1) << 2) |
                                           (((rByte >> shift) & 1) << 1) |
                                           ((bByte >> shift) & 1);
                        var x = tileX + (xByte * 8) + bit;
                        output[((tileY + y) * MpnConstants.CanvasWidth) + x] = (byte)paletteIndex;
                    }
                }
            }
        }

        private static void DrawPaletteMarkerTile(byte[] output, int tileIndex)
        {
            var tileX = (tileIndex % MpnConstants.TileColumns) * MpnConstants.TileWidth;
            var tileY = (tileIndex / MpnConstants.TileColumns) * MpnConstants.TileHeight;

            for (var y = 0; y < MpnConstants.TileHeight; y++)
            {
                for (var x = 0; x < MpnConstants.TileWidth; x++)
                {
                    output[((tileY + y) * MpnConstants.CanvasWidth) + tileX + x] = (byte)y;
                }
            }
        }

        private static int ReadUInt16LittleEndian(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }
    }
}
