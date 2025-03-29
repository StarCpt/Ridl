using System.Runtime.InteropServices;

// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    /// <remarks>
    /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapv4header"/>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BitmapV4Header : IBmpHeader, IBmpHeaderV3OrAbove
    {
        public readonly uint SizeImage => _base.SizeImage;
        public readonly int Width => _base.Width;
        public readonly int Height => _base.Height;
        public readonly bool IsTopDown => _base.IsTopDown;
        public readonly int BitsPerPixel => _base.BitsPerPixel;
        public readonly BmpCompressionMethod Format => _base.Format;
        public readonly double DpiX => _base.DpiX;
        public readonly double DpiY => _base.DpiY;
        public readonly int PaletteLength => _base.PaletteLength;
        public readonly uint RedMask => _base.RedMask;
        public readonly uint GreenMask => _base.GreenMask;
        public readonly uint BlueMask => _base.BlueMask;
        public readonly uint AlphaMask => _base.AlphaMask;

        private readonly BitmapV3InfoHeader _base;
        private readonly uint _cSType;
        private readonly uint _redX, _redY, _redZ;
        private readonly uint _greenX, _greenY, _greenZ;
        private readonly uint _blueX, _blueY, _blueZ;
        private readonly uint _gammaRed;
        private readonly uint _gammaGreen;
        private readonly uint _gammaBlue;
    }
}
