using SixLabors.ImageSharp.Formats;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public sealed class MpnFormat : IImageFormat<MpnMetadata>
    {
        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static MpnFormat Instance { get; } = new MpnFormat();

        public string Name => "MPN";
        public string DefaultMimeType => "image/x-mpn";
        public IEnumerable<string> MimeTypes => MpnConstants.MimeTypes;
        public IEnumerable<string> FileExtensions => MpnConstants.FileExtensions;

        public MpnMetadata CreateDefaultFormatMetadata() => new();
    }
}
