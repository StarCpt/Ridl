using System.Runtime.InteropServices;

namespace Ridl.Bmp
{
    /// <remarks>
    /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapv5header"/>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BitmapV5Header : IBmpHeader, IBmpHeaderV4OrAbove
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
        public readonly BmpColorSpace ColorSpace => _base.ColorSpace;

        public uint Intent => _intent;
        public uint ProfileData => _profileData;
        public uint ProfileSize => _profileSize;

        private readonly BitmapV4Header _base;
        private readonly uint _intent;
        private readonly uint _profileData;
        private readonly uint _profileSize;
        private readonly uint _reserved;
    }
}
