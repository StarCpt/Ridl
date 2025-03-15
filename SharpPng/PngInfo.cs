using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace SharpPng
{
    public struct PngInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int BitDepth { get; set; } // 1, 2, 4, 8, 16
        public PngPixelFormat Format { get; set; }
        public PngCompressionMethod Compression { get; set; } // always 0
        public byte Filter { get; set; } // always 0
        public bool Interlace { get; set; }
        public PngColorPalette? Palette { get; set; }

        public int BitsPerPixel
        {
            get
            {
                return Format switch
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
    }
}
