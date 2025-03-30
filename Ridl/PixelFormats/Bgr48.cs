using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct Bgr48(ushort b, ushort g, ushort r)
    {
        public const int Size = 6;

        public ushort B = b;
        public ushort G = g;
        public ushort R = r;
    }
}
