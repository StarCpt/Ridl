using Ridl.PixelFormats;

namespace Ridl.Bmp
{
    public class BmpImage : IImage
    {
        public const double DEFAULT_DPI = 96;

        public Span<byte> PixelData => _pixelData;
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public PixelFormat Format { get; }
        public double DpiX { get; }
        public double DpiY { get; }
        public Rgba32[]? Palette { get; }

        private readonly byte[] _pixelData;

        internal BmpImage(byte[] pixelData, int width, int height, int stride, PixelFormat format, double dpiX, double dpiY, Bgrx32[]? palette)
        {
            _pixelData = pixelData;
            Width = width;
            Height = height;
            Stride = stride;
            Format = format;
            DpiX = dpiX;
            DpiY = dpiY;
            Palette = palette?.Select(i => new Rgba32(i.R, i.G, i.B, byte.MaxValue)).ToArray();

            if (format.IsIndexed() && palette is null)
                throw new Exception($"Indexed formats must contain a palette. Format: {format}");
        }

        public Span<byte> GetRow(int row)
        {
            if (row < 0 || row >= Height)
                throw new ArgumentOutOfRangeException(nameof(row));

            return _pixelData.AsSpan(row * Stride);
        }
    }
}
