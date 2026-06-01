using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public class MpnMetadata : IDeepCloneable
    {
        public MpnMetadata()
        {
        }

        public MpnMetadata(MpnMetadata other)
        {
            CopyFrom(other);
        }

        public IDeepCloneable DeepClone() => new MpnMetadata(this);

        public void CopyFrom(MpnMetadata other)
        {
            TileCount = other.TileCount;
            Palette = new List<Rgb24>(other.Palette);
        }

        public ushort TileCount { get; set; }
        public List<Rgb24> Palette { get; set; } = new();
    }
}
