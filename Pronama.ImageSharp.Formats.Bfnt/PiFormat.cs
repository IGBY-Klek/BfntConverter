using SixLabors.ImageSharp.Formats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public sealed class PiFormat : IImageFormat<PiMetadata>
    {
        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static PiFormat Instance { get; } = new PiFormat();

        public string Name => "PI";
        public string DefaultMimeType => "image/x-pi";
        public IEnumerable<string> MimeTypes => PiConstants.MimeTypes;
        public IEnumerable<string> FileExtensions => PiConstants.FileExtensions;

        public PiMetadata CreateDefaultFormatMetadata() => new();
    }
}
