using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace SharpPng
{
    public readonly struct PngInfo
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public int BitDepth { get; init; } // 1, 2, 4, 8, 16
        public PngPixelFormat Format { get; init; }
        public PngCompressionMethod Compression { get; init; } // always 0
        public byte Filter { get; init; } // always 0
        public bool Interlace { get; init; }
        public PngColor[]? Palette { get; init; }

        public int BitsPerPixel => Format switch
        {
            PngPixelFormat.Grayscale => BitDepth,
            PngPixelFormat.Rgb => BitDepth * 3,
            PngPixelFormat.Indexed => 8,
            PngPixelFormat.GrayscaleWithAlpha => BitDepth * 2,
            PngPixelFormat.Rgba => BitDepth * 4,
            _ => throw new Exception("Unknown pixel format."),
        };
    }
}
