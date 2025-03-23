using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ridl
{
    [StructLayout(LayoutKind.Explicit)]
    [DebuggerDisplay("{R}, {G}, {B}")]
    public struct PngColor
    {
        [FieldOffset(0)] public byte R;
        [FieldOffset(1)] public byte G;
        [FieldOffset(2)] public byte B;
    }
}