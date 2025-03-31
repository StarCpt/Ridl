using System.Runtime.InteropServices;

namespace Ridl.Bmp
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BitmapCoreHeader : IBmpHeader
    {
        public readonly int Width => _width;
        public readonly int Height => _height;
        public readonly bool IsTopDown => false;
        public readonly int BitsPerPixel => _bitCount;
        public readonly BmpCompressionMethod Format => BmpCompressionMethod.Rgb;
        public readonly double DpiX => BmpImage.DEFAULT_DPI;
        public readonly double DpiY => BmpImage.DEFAULT_DPI;
        public readonly int PaletteLength => _bitCount is 4 or 8 ? (int)(1u << _bitCount) : 0;

        private readonly short _width; // Int16 for Windows 2.x, UInt16 on OS/2 1.x
        private readonly short _height;
        private readonly ushort _planes;
        private readonly ushort _bitCount;

        public BitmapCoreHeader(short width, short height, ushort planes, ushort bitCount)
        {
            _width = width;
            _height = height;
            _planes = planes;
            _bitCount = bitCount;
        }
    }
}
