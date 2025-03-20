namespace SharpPng
{
    public readonly struct PngMetadata
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int BitDepth { get; init; } // 1, 2, 4, 8, 16
        public PngPixelFormat Format { get; init; }
        public PngCompressionMethod Compression { get; init; } // always 0
        public byte Filter { get; init; } // always 0
        public bool Interlaced { get; init; }
        public PngColor[]? Palette { get; init; }
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

        public int Channels => Format switch
        {
            PngPixelFormat.Grayscale => 1,
            PngPixelFormat.Rgb => 3,
            PngPixelFormat.Indexed => 1,
            PngPixelFormat.GrayscaleWithAlpha => 2,
            PngPixelFormat.Rgba => 4,
            _ => throw new Exception("Unknown pixel format."),
        };
    }
}
