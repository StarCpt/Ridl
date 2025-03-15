using SharpPng.Filtering;

namespace SharpPng.Reconstruction
{
    internal class ReconstructGeneric : IReconstructor
    {
        private readonly int _bytesPerPixel;
        private readonly int _imageStride;

        public ReconstructGeneric(int imageWidth, int bitsPerPixel)
        {
            _bytesPerPixel = bitsPerPixel < 8 ? 1 : (bitsPerPixel / 8);
            _imageStride = imageWidth * bitsPerPixel / 8;
        }

        public void FilterSub(Span<byte> scanline)
        {
            for (int x = _bytesPerPixel; x < _imageStride; x++)
            {
                scanline[x] += scanline[x - _bytesPerPixel];
            }
        }

        public void FilterUp(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            for (int x = 0; x < _imageStride; x++)
            {
                scanline[x] += prevScanline[x];
            }
        }

        public void FilterAvg_Scan0(Span<byte> scanline)
        {
            for (int x = _bytesPerPixel; x < _imageStride; x++)
            {
                scanline[x] += (byte)(scanline[x - _bytesPerPixel] / 2);
            }
        }

        public void FilterAvg(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            for (int x = 0; x < _bytesPerPixel; x++)
            {
                scanline[x] += (byte)(prevScanline[x] / 2);
            }

            for (int x = _bytesPerPixel; x < _imageStride; x++)
            {
                scanline[x] += (byte)((scanline[x - _bytesPerPixel] + prevScanline[x]) / 2);
            }
        }

        // if y is 0, Sub filter outputs the same values as Paeth filter with prev scanline filled with 0s
        public void FilterPaeth_Scan0(Span<byte> scanline) => FilterSub(scanline);

        public void FilterPaeth(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            for (int x = 0; x < _bytesPerPixel; x++)
            {
                scanline[x] += prevScanline[x];
            }

            for (int x = _bytesPerPixel; x < _imageStride; x++)
            {
                scanline[x] += FilteringHelpers.PaethPredictor(scanline[x - _bytesPerPixel], prevScanline[x], prevScanline[x - _bytesPerPixel]);
            }
        }
    }
}
