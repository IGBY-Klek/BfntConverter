using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public class PiMetadata : IDeepCloneable
    {
        public PiMetadata()
        {
        }

        public PiMetadata(PiMetadata other)
        {
            Mode = other.Mode;
            AspectRatioX = other.AspectRatioX;
            AspectRatioY = other.AspectRatioY;
            BitDepth = other.BitDepth;
            CompressorModel = other.CompressorModel;
            CompressorDataSize = other.CompressorDataSize;
            Width = other.Width;
            Height = other.Height;
            Palette = new List<Rgb24>(other.Palette);
        }

        public IDeepCloneable DeepClone() => new PiMetadata(this);

        public byte Mode { get; set; }
        public byte AspectRatioX { get; set; }
        public byte AspectRatioY { get; set; }
        public byte BitDepth { get; set; }
        public string? CompressorModel { get; set; }
        public ushort CompressorDataSize { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public List<Rgb24> Palette { get; set; } = new();

        public int ColorCount => NormalizedBitDepth == 8 ? 256 : 16;
        public int NormalizedBitDepth => BitDepth == 8 ? 8 : 4;
    }
}
