using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Bgr24(byte b, byte g, byte r)
    {
        public const int Size = 3;

        public byte B = b;
        public byte G = g;
        public byte R = r;
    }
}
