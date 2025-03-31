using System.Runtime.InteropServices;

namespace Ridl.Bmp
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct BmpFileHeader
    {
        public readonly uint FileSize;
        public readonly uint Reserved;
        public readonly uint DataOffset;
    }
}
