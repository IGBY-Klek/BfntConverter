using SixLabors.ImageSharp.Metadata;

namespace Pronama.ImageSharp.Formats.Bfnt
{
    public static class MetadataExtensions
    {
        /// <summary>
        /// Gets the BFNT format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="BfntMetadata"/>.</returns>
        public static BfntMetadata GetBfntMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(BfntFormat.Instance);

        /// <summary>
        /// Gets the PI format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="PiMetadata"/>.</returns>
        public static PiMetadata GetPiMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(PiFormat.Instance);

        /// <summary>
        /// Gets the MPN format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="MpnMetadata"/>.</returns>
        public static MpnMetadata GetMpnMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(MpnFormat.Instance);
    }
}
