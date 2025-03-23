namespace Ridl.Png
{
    public readonly struct PngTransparency
    {
        /// <summary>
        /// May only exist for <see cref="PngPixelFormat.Grayscale"/>.
        /// </summary>
        public ushort? TransparentColorGrayscale { get; init; }

        /// <summary>
        /// May only exist for <see cref="PngPixelFormat.Rgb"/>.
        /// </summary>
        public (ushort R, ushort G, ushort B)? TransparentColorRgb { get; init; }

        /// <summary>
        /// May only exist for <see cref="PngPixelFormat.Indexed"/>.
        /// </summary>
        public byte[]? PaletteTransparencyMap { get; init; }
    }
}
