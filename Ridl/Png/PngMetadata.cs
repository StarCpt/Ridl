namespace Ridl.Png
{
    public readonly struct PngMetadata
    {
        public int Width { get; init; }
        public int Height { get; init; }
        /// <remarks>1, 2, 4, 8, or 16.</remarks>
        public int BitDepth { get; init; }
        public PngPixelFormat Format { get; init; }
        /// <remarks>Always 0.</remarks>
        internal PngCompressionMethod Compression { get; init; }
        /// <remarks>Always 0.</remarks>
        internal byte Filter { get; init; }
        internal PngInterlaceMethod Interlace { get; init; }
        public PngPaletteColor[]? Palette { get; init; }
        public PngTransparency? Transparency { get; init; }
        public PngPixelDimensions? PixelDimensions { get; init; }

        public int BitsPerPixel => Format switch
        {
            // Possible bit depths for each format
            // 1, 2, 4, 8, 16
            PngPixelFormat.Grayscale => BitDepth,
            // 8, 16
            PngPixelFormat.Rgb => BitDepth * 3,
            // 1, 2, 4, 8
            PngPixelFormat.Indexed => BitDepth,
            // 8, 16
            PngPixelFormat.GrayscaleWithAlpha => BitDepth * 2,
            // 8, 16
            PngPixelFormat.Rgba => BitDepth * 4,
            _ => throw new Exception("Unknown pixel format."),
        };

        public int Channels => Format.GetChannels();
    }
}
