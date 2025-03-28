using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl.PixelTypes
{
    public enum PixelFormat
    {
        Unknown = 0,

        Rgb24, Rgb48,
        Bgr24, Bgr48,

        Rgba32, Rgba64,
        Bgra32, Bgra64,
        Argb32, Argb64,

        Indexed1, Indexed2, Indexed4, Indexed8,

        Cmyk32, Cmyk64,

        Gray1, Gray2, Gray4, Gray8, Gray16, Gray32,
        GrayWithAlpha8, GrayWithAlpha16, GrayWithAlpha32,
    }
}
