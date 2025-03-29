﻿// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    internal interface IBmpHeaderV3OrAbove : IBmpHeaderV2OrAbove
    {
        uint AlphaMask { get; }
    }
}
