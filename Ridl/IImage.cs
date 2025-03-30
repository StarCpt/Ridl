using Ridl.PixelFormats;

namespace Ridl
{
    public interface IImage
    {
        Span<byte> PixelData { get; }
        int Width { get; }
        int Height { get; }
        int Stride { get; }
        PixelFormat Format { get; }
        double DpiX { get; }
        double DpiY { get; }
        Rgba32[]? Palette { get; }

        Span<byte> GetRow(int row);
        // IImage ConvertToFormat(PixelFormat format);
    }
}
