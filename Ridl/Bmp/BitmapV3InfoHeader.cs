using System.Runtime.InteropServices;

// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    /// <summary>
    /// https://web.archive.org/web/20150127132443/https://forums.adobe.com/message/3272950
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BitmapV3InfoHeader : IBmpHeader, IBmpHeaderV3OrAbove
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

        public readonly uint AlphaMask => _alphaMask;

        private readonly BitmapV2InfoHeader _base;
        private readonly uint _alphaMask;
    }
}
