using System.Runtime.InteropServices;

// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

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
        public readonly double DpiX => BmpDecoder.DEFAULT_BMP_DPI;
        public readonly double DpiY => BmpDecoder.DEFAULT_BMP_DPI;
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
