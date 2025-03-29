using Ridl.PixelFormats;

namespace Ridl
{
    public interface IImage
    {
        int Width { get; }
        int Height { get; }
        int Stride { get; }
        PixelFormat Format { get; }
        double DpiX { get; }
        double DpiY { get; }
        Rgba32[]? Palette { get; }
    }
}
