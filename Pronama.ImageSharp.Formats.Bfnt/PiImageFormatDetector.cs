using SixLabors.ImageSharp.Formats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public sealed class PiImageFormatDetector : IImageFormatDetector
    {
        public int HeaderSize => 2;

        public IImageFormat DetectFormat(ReadOnlySpan<byte> header)
        {
            return IsSupportedFileFormat(header) ? PiFormat.Instance : null;
        }

        private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        {
            return header.Length >= HeaderSize &&
                   header[..2].SequenceEqual(PiConstants.HeaderBytes);
        }
    }
}
