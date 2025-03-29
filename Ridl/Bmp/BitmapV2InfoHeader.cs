using System.Runtime.InteropServices;

namespace Ridl.Bmp
{
    /// <summary>
    /// https://web.archive.org/web/20150127132443/https://forums.adobe.com/message/3272950
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BitmapV2InfoHeader : IBmpHeader, IBmpHeaderV2OrAbove
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

        public readonly uint RedMask => _redMask;
        public readonly uint GreenMask => _greenMask;
        public readonly uint BlueMask => _blueMask;

        private readonly BitmapInfoHeader _base;
        private readonly uint _redMask;
        private readonly uint _greenMask;
        private readonly uint _blueMask;
    }
}
