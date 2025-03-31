using System.Runtime.InteropServices;

namespace Ridl.Bmp
{
    /// <summary>
    /// Truncated 16-byte version of <see cref="OS22xBitmapHeader"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly struct OS22xBitmapHeader_16 : IBmpHeader
    {
        public readonly uint SizeImage => 0;
        public readonly int Width => int.Abs(_width);
        public readonly int Height => int.Abs(_height);
        public readonly bool IsTopDown => _height < 0;
        public readonly int BitsPerPixel => _bitCount;
        public readonly BmpCompressionMethod Format => _compression switch
        {
            BmpCompressionMethod.BitFields => BmpCompressionMethod.Huffman1D,
            BmpCompressionMethod.Jpeg => BmpCompressionMethod.Rle24,
            _ => _compression,
        };
        public readonly double DpiX => BmpImage.DEFAULT_DPI;
        public readonly double DpiY => BmpImage.DEFAULT_DPI;
        public readonly int PaletteLength => 0;

        private readonly int _width;
        private readonly int _height;
        private readonly ushort _planes;
        private readonly ushort _bitCount;
        private readonly BmpCompressionMethod _compression;
    }
}
