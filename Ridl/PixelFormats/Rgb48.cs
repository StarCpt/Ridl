using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    [DebuggerDisplay("R={R}, G={G}, B={B}")]
    internal struct Rgb48(ushort r, ushort g, ushort b)
    {
        public const int Size = 6;

        public ushort R = r;
        public ushort G = g;
        public ushort B = b;
    }
}
