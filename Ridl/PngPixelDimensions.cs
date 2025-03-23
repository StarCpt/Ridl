namespace Ridl
{
    public readonly struct PngPixelDimensions
    {
        public int PixelsPerUnitX { get; init; }
        public int PixelsPerUnitY { get; init; }
        public PngPixelUnit Units { get; init; }
    }
}
