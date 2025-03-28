using System.Runtime.InteropServices;

namespace Ridl.PixelTypes
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rgba32(byte r, byte g, byte b, byte a)
    {
        public const int Size = 3;

        public byte R = r;
        public byte G = g;
        public byte B = b;
        public byte A = a;
    }
}
