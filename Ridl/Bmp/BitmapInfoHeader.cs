using System.Runtime.InteropServices;

namespace Ridl.Bmp
{
    /// <remarks>
    /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader"/>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BitmapInfoHeader : IBmpHeader
    {
        public readonly uint SizeImage => _sizeImage;
        public readonly int Width => int.Abs(_width);
        public readonly int Height => int.Abs(_height);
        // For uncompressed formats, the origin is bottom-left if y is positive and top-left if y is negative.
        public readonly bool IsTopDown => _height < 0;
        public readonly int BitsPerPixel => _bitCount;
        public readonly BmpCompressionMethod Format => _compression;
        public readonly double DpiX => _xPelsPerMeter != 0 ? MathHelpers.DpmToDpi(_xPelsPerMeter, 1) : BmpImage.DEFAULT_DPI;
        public readonly double DpiY => _yPelsPerMeter != 0 ? MathHelpers.DpmToDpi(_yPelsPerMeter, 1) : BmpImage.DEFAULT_DPI;
        public readonly int PaletteLength => (int)_clrUsed;

        private readonly int _width;
        private readonly int _height;
        private readonly ushort _planes;
        private readonly ushort _bitCount;
        private readonly BmpCompressionMethod _compression;
        private readonly uint _sizeImage;
        private readonly int _xPelsPerMeter;
        private readonly int _yPelsPerMeter;
        private readonly uint _clrUsed;
        private readonly uint _clrImportant;
    }
}
