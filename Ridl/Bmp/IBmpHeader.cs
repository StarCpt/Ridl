namespace Ridl.Bmp
{
    internal interface IBmpHeader
    {
        int Width { get; }
        int Height { get; }
        bool IsTopDown { get; }
        int BitsPerPixel { get; }
        BmpCompressionMethod Format { get; }
        double DpiX { get; }
        double DpiY { get; }
        int PaletteLength { get; }
    }
}
