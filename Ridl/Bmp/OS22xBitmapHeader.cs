using System.Runtime.InteropServices;

// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct OS22xBitmapHeader : IBmpHeader
    {
        public readonly uint SizeImage => _base.SizeImage;
        public readonly int Width => _base.Width;
        public readonly int Height => _base.Height;
        public readonly bool IsTopDown => _base.IsTopDown;
        public readonly int BitsPerPixel => _base.BitsPerPixel;
        public readonly BmpCompressionMethod Format => _base.Format switch
        {
            BmpCompressionMethod.BitFields => BmpCompressionMethod.Huffman1D,
            BmpCompressionMethod.Jpeg => BmpCompressionMethod.Rle24,
            var format => format,
        };
        public readonly double DpiX => _base.DpiX;
        public readonly double DpiY => _base.DpiY;
        public readonly int PaletteLength => (int)_base.PaletteLength;

        public readonly BmpHalftoneAlgorithm HalftoneAlgorithm => _halftoneAlgorithm;
        public readonly uint HalftoneParam1 => _halftoneParam1;
        public readonly uint HalftoneParam2 => _halftoneParam2;

        private readonly BitmapInfoHeader _base;
        // Only the halftone fields are important, other fields only have 1 valid value or are unused.
        private readonly ushort _resolutionUnits; // X/YPelsPerMeter units, only valid value is 0, for Pixels Per Meter.
        private readonly ushort _padding; // Unused, should be 0 but shouldn't cause an error if not.
        private readonly ushort _direction; // Only valid value is 0, for bottom-left origin.
        private readonly BmpHalftoneAlgorithm _halftoneAlgorithm;
        private readonly uint _halftoneParam1;
        private readonly uint _halftoneParam2;
        private readonly uint _pixelFormat; // Only valid value is 0, for RGB.
        private readonly uint _applicationReserved; // Application-defined value, ignored for decoding and rendering.
    }
}
