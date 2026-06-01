namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal static class PiConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to a PI image.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-pi" };

        /// <summary>
        /// The list of file extensions that equate to a PI image.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "PI" };

        /// <summary>
        /// Gets the header bytes identifying a PI image.
        /// </summary>
        public static ReadOnlySpan<byte> HeaderBytes => new byte[]
        {
            (byte)'P',
            (byte)'i'
        };
    }
}
