// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    /// <remarks>
    /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/4e588f70-bd92-4a6f-b77f-35d0feaf7a57"/>
    /// </remarks>
    internal enum BmpCompressionMethod : uint
    {
        Rgb = 0,
        Rle8 = 1,
        Rle4 = 2,
        /// <summary>
        /// 1D Huffman encoding in OS22xBitmapHeader
        /// </summary>
        BitFields = 3,
        /// <summary>
        /// RLE24 in OS22xBitmapHeader
        /// </summary>
        Jpeg = 4,
        Png = 5,
        AlphaBitFields = 6, // not in MS docs but mentioned in the wikipedia article
        Cmyk = 11,
        CmykRle8 = 12,
        CmykRle4 = 13,

        // not part of the enum:
        Huffman1D = 20, // BitFields in OS22xBitmapHeader is converted to this value
        Rle24 = 21, // Jpeg in OS22xBitmapHeader is converted to this value
    }
}
