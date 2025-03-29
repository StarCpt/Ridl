using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl.PixelFormats
{
    public enum PixelFormat
    {
        Unknown = 0,

        // Different RGB formats
        Rgb24, Rgb48,
        Bgr24, Bgr48,

        // Different RGBA formats
        Rgba32, Rgba64,
        //Bgra32, Bgra64,
        ///Argb32, Argb64,

        // Paletted
        Indexed1, Indexed2, Indexed4, Indexed8,

        // Grayscale
        Gray1, Gray2, Gray4, Gray8, Gray16,
        // Grayscale + Alpha (2 channels)
        GrayWithAlpha4, GrayWithAlpha8, GrayWithAlpha16, GrayWithAlpha32,
    }

    public static class PixelFormatExtensions
    {
        public static int GetBitsPerPixel(this PixelFormat format) => format switch
        {
            PixelFormat.Rgb24 or PixelFormat.Bgr24 => 24,
            PixelFormat.Rgb48 or PixelFormat.Bgr48 => 48,
            PixelFormat.Rgba32 => 32,
            PixelFormat.Rgba64 => 64,
            PixelFormat.Indexed1 => 1,
            PixelFormat.Indexed2 => 2,
            PixelFormat.Indexed4 => 4,
            PixelFormat.Indexed8 => 8,
            PixelFormat.Gray1 => 1,
            PixelFormat.Gray2 => 2,
            PixelFormat.Gray4 => 4,
            PixelFormat.Gray8 => 8,
            PixelFormat.Gray16 => 16,
            PixelFormat.GrayWithAlpha4 => 4,
            PixelFormat.GrayWithAlpha8 => 8,
            PixelFormat.GrayWithAlpha16 => 16,
            PixelFormat.GrayWithAlpha32 => 32,
            _ => throw new ArgumentException($"Invalid pixel format: {format}"),
        };

        public static bool IsIndexed(this PixelFormat format) => format is PixelFormat.Indexed1 or PixelFormat.Indexed2 or PixelFormat.Indexed4 or PixelFormat.Indexed8;
    }
}
