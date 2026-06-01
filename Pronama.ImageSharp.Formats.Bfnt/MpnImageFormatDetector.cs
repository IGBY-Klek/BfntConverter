using SixLabors.ImageSharp.Formats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public sealed class MpnImageFormatDetector : IImageFormatDetector
    {
        public int HeaderSize => 4;

        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return IsSupportedFileFormat(header) ? MpnFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= HeaderSize &&
                   header[..4].SequenceEqual(MpnConstants.HeaderBytes);
        }
    }
}
