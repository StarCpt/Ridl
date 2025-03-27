using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl.Bmp
{
    public enum BmpPixelFormat
    {
        Rgb = 0,
        RgbIndexed8 = 1,
        RgbIndexed4 = 2,
        // BitFields = 3,
        // AlphaBitFields = 6,
        Cmyk = 11,
        CmykIndexed8 = 12,
        CmykIndexed4 = 13,
    }

    public class BmpImage : IImage
    {
        public byte[] PixelData => _pixelData;

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public int BitsPerPixel { get; }
        public BmpPixelFormat Format { get; }
        public double DpiX { get; }
        public double DpiY { get; }
        public Rgb32[]? Palette { get; }

        private readonly byte[] _pixelData;

        internal BmpImage(byte[] pixelData, BmpDibHeader header, Rgb32[]? palette)
        {
            _pixelData = pixelData;

            Width = header.Header.Width;
            Height = header.Header.Height;
            Stride = (header.Header.Width * header.Header.BitCount + 31) / 32 * 4;
            BitsPerPixel = header.Header.BitCount;
            Format = header.Header.Compression switch
            {
                BmpCompressionMethod.Rgb => BitsPerPixel == 8 ? BmpPixelFormat.RgbIndexed8 : (BitsPerPixel == 4 ? BmpPixelFormat.RgbIndexed4 : BmpPixelFormat.Rgb),
                BmpCompressionMethod.Rle8 => BmpPixelFormat.RgbIndexed8,
                BmpCompressionMethod.Rle4 => BmpPixelFormat.RgbIndexed4,
                BmpCompressionMethod.BitFields => throw new NotImplementedException(),
                BmpCompressionMethod.Jpeg => throw new ArgumentException(header.Header.Compression.ToString()),
                BmpCompressionMethod.Png => throw new ArgumentException(header.Header.Compression.ToString()),
                BmpCompressionMethod.Cmyk => BmpPixelFormat.Cmyk,
                BmpCompressionMethod.CmykRle8 => BmpPixelFormat.CmykIndexed8,
                BmpCompressionMethod.CmykRle4 => BmpPixelFormat.CmykIndexed4,
                _ => throw new ArgumentException(header.Header.Compression.ToString()),
            };
            DpiX = header.Header.XPelsPerMeter / 39.3700787402;
            DpiY = header.Header.YPelsPerMeter / 39.3700787402;
            Palette = palette;
        }
    }
}
