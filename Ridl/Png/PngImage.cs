namespace Ridl.Png
{
    public class PngImage
    {
        public const double DEFAULT_DPI = 96;

        public byte[] PixelData => _pixelData;

        public int Width => _metadata.Width;
        public int Height => _metadata.Height;
        /// <summary>
        /// The length of each row (scanline) in bytes. Some bits of the last byte of each row may be unused by formats using less than 8 bits per pixel. The contents of the unused bits are undefined.
        /// </summary>
        public int Stride { get; }
        /// <summary>Bits per channel.</summary>
        /// <remarks>1, 2, 4, 8, or 16.</remarks>
        public int BitDepth => _metadata.BitDepth;
        public PngPixelFormat Format => _metadata.Format;
        /// <remarks>Always 0.</remarks>
        internal PngCompressionMethod Compression => _metadata.Compression;
        /// <remarks>Always 0.</remarks>
        internal byte Filter => _metadata.Filter;
        internal PngInterlaceMethod Interlace => _metadata.Interlace;

        public double DpiX
        {
            get
            {
                double dpiX = DEFAULT_DPI;
                if (PixelDimensions is PngPixelDimensions dims)
                {
                    if (dims.Units is PngPixelUnit.Unknown)
                    {
                        double aspectRatio = (double)dims.PixelsPerUnitX / dims.PixelsPerUnitY;
                        dpiX *= aspectRatio;
                    }
                    else if (dims.Units is PngPixelUnit.Meter)
                    {
                        dpiX = MathHelpers.DpmToDpi(dims.PixelsPerUnitX, 1);
                    }
                }
                return dpiX;
            }
        }
        public double DpiY
        {
            get
            {
                double dpiY = DEFAULT_DPI;
                if (PixelDimensions is PngPixelDimensions dims)
                {
                    if (dims.Units is PngPixelUnit.Meter)
                    {
                        dpiY = MathHelpers.DpmToDpi(dims.PixelsPerUnitY, 1);
                    }
                }
                return dpiY;
            }
        }

        // Ancilary chunks
        public PngPaletteColor[]? Palette => _metadata.Palette;
        public PngTransparency? Transparency => _metadata.Transparency;
        public PngPixelDimensions? PixelDimensions => _metadata.PixelDimensions;

        public int BitsPerPixel => _metadata.BitsPerPixel;
        public int Channels => _metadata.Channels;

        private readonly PngMetadata _metadata;
        private readonly byte[] _pixelData;

        public PngImage(PngMetadata metadata, byte[] pixelData)
        {
            _metadata = metadata;
            _pixelData = pixelData;
            Stride = MathHelpers.DivRoundUp(Width * BitsPerPixel, 8);
        }

        public Span<byte> GetRow(int row)
        {
            if (row < 0 || row >= Height)
                throw new ArgumentOutOfRangeException(nameof(row));

            return _pixelData.AsSpan(row * Stride);
        }
    }
}
