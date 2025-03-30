using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    [DebuggerDisplay("B={B}, G={G}, R={R}")]
    internal struct Bgr48(ushort b, ushort g, ushort r)
    {
        public const int Size = 6;

        public ushort B = b;
        public ushort G = g;
        public ushort R = r;
    }
}
