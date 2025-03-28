using System.Runtime.InteropServices;

namespace Ridl.PixelTypes
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Rgb24(byte r, byte g, byte b)
    {
        public const int Size = 3;

        public byte R = r;
        public byte G = g;
        public byte B = b;
    }
}
