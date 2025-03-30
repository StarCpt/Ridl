using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    [DebuggerDisplay("R={R}, G={G}, B={B}, A={A}")]
    internal struct Rgba64(ushort r, ushort g, ushort b, ushort a)
    {
        public const int Size = 8;

        public ushort R = r;
        public ushort G = g;
        public ushort B = b;
        public ushort A = a;
    }
}
