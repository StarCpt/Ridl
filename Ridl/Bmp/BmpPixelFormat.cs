namespace Ridl.Bmp
{
    public enum BmpPixelFormat
    {
        Rgb24, Rgb48,
        Rgba32, Rgba64,
        Bgr24,
        Indexed1, Indexed2, Indexed4, Indexed8,
        Cmyk32,
    }

    public static class BmpPixelFormatExtensions
    {
        public static int GetBitsPerPixel(this BmpPixelFormat format) => format switch
        {
            BmpPixelFormat.Rgb24 or BmpPixelFormat.Bgr24 => 24,
            BmpPixelFormat.Rgb48 => 48,
            BmpPixelFormat.Indexed1 => 1,
            BmpPixelFormat.Indexed2 => 2,
            BmpPixelFormat.Indexed4 => 4,
            BmpPixelFormat.Indexed8 => 8,
            BmpPixelFormat.Cmyk32 => 32,
            _ => throw new ArgumentException($"Invalid pixel format: {format}"),
        };

        public static bool IsIndexed(this BmpPixelFormat format) => format is BmpPixelFormat.Indexed1 or BmpPixelFormat.Indexed2 or BmpPixelFormat.Indexed4 or BmpPixelFormat.Indexed8;
    }
}
