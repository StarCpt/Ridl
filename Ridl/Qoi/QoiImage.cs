using Ridl.PixelFormats;

namespace Ridl.Qoi
{
    public class QoiImage : IImage
    {
        public const double DEFAULT_DPI = 96;

        public Span<byte> PixelData => _pixelData;
        public int Width => (int)_header.Width;
        public int Height => (int)_header.Height;
        public int Stride => (int)(_header.Width * _header.Channels);
        public PixelFormat Format => _header.Channels is 3 ? PixelFormat.Rgb24 : PixelFormat.Rgba32;
        public double DpiX => DEFAULT_DPI;
        public double DpiY => DEFAULT_DPI;
        public Rgba32[]? Palette => null;

        public QoiColorSpace ColorSpace => _header.ColorSpace;

        private readonly byte[] _pixelData;
        private readonly QoiHeader _header;

        public QoiImage(byte[] pixelData, in QoiHeader header)
        {
            _pixelData = pixelData;
            _header = header;
        }

        public Span<byte> GetRow(int row) => _pixelData.AsSpan(row * Stride, Stride);
    }
}
