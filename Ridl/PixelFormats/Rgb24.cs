﻿using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ridl.PixelFormats
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("R={R}, G={G}, B={B}")]
    internal struct Rgb24(byte r, byte g, byte b)
    {
        public const int Size = 3;

        public byte R = r;
        public byte G = g;
        public byte B = b;
    }
}
