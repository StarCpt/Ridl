using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    [DebuggerDisplay("R={R}, G={G}, B={B}, A={A}")]
    public struct Rgba32(byte r, byte g, byte b, byte a)
    {
        public const int Size = 4;

        public byte R = r;
        public byte G = g;
        public byte B = b;
        public byte A = a;
    }
}
