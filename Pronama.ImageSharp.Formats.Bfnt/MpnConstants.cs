namespace Pronama.ImageSharp.Formats.Bfnt
{
    internal static class MpnConstants
    {
        /// <summary>
        /// The list of mimetypes that equate to an MPN image.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/x-mpn" };

        /// <summary>
        /// The list of file extensions that equate to an MPN image.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "MPN" };

        /// <summary>
        /// Gets the header bytes identifying an MPN image.
        /// </summary>
        public static ReadOnlySpan<byte> HeaderBytes => new byte[]
        {
            (byte)'M',
            (byte)'P',
            (byte)'T',
            (byte)'N'
        };

        public const int HeaderSize = 54;
        public const int PaletteColorCount = 16;
        public const int PaletteBytes = PaletteColorCount * 3;
        public const int TileWidth = 16;
        public const int TileHeight = 16;
        public const int TileColumns = 16;
        public const int TileRows = 16;
        public const int MaxTileCount = TileColumns * TileRows;
        public const int BytesPerPlane = 32;
        public const int TilePlanes = 4;
        public const int TileSize = BytesPerPlane * TilePlanes;
        public const int CanvasWidth = TileColumns * TileWidth;
        public const int CanvasHeight = TileRows * TileHeight;
    }
}
