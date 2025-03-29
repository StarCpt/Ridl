namespace Ridl.Bmp
{
    internal enum BmpHeaderType : uint
    {
        BitmapCoreHeader = 12,
        OS21xBitmapHeader = BitmapCoreHeader, // Same size, only difference is that BitmapCoreHeader stores W/H as Int16 and OS/2 1.x stores it as UInt16
        OS22xBitmapHeader = 64,
        OS22xBitmapHeader_Short = 16,
        BitmapInfoHeader = 40,
        BitmapV2InfoHeader = 52,
        BitmapV3InfoHeader = 56,
        BitmapV4Header = 108,
        BitmapV5Header = 124,
    }
}
