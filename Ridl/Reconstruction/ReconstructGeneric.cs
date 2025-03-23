using Ridl.Filtering;
using System.Runtime.Intrinsics;

namespace Ridl.Reconstruction
{
    internal class ReconstructGeneric(int bitsPerPixel) : IReconstructor
    {
        private readonly int _bytesPerPixel = bitsPerPixel < 8 ? 1 : (bitsPerPixel / 8); // 1 when bpp < 8

        public virtual void FilterSub(Span<byte> scanline)
        {
            for (int x = _bytesPerPixel; x < scanline.Length; x++)
            {
                scanline[x] += scanline[x - _bytesPerPixel];
            }
        }

        public virtual void FilterUp(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            if (Vector256.IsHardwareAccelerated)
            {
                FilterUpSimd256(scanline, prevScanline);
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                FilterUpSimd128(scanline, prevScanline);
            }
            else
            {
                for (int x = 0; x < scanline.Length; x++)
                {
                    scanline[x] += prevScanline[x];
                }
            }
        }

        private static void FilterUpSimd256(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            int x = 0;
            for (; x <= scanline.Length - 32; x += 32)
            {
                Vector256<byte> current = Vector256.LoadUnsafe(ref scanline[x]);
                Vector256<byte> above = Vector256.LoadUnsafe(in prevScanline[x]);

                current += above;
                current.StoreUnsafe(ref scanline[x]);
            }

            for (; x < scanline.Length; x++)
            {
                scanline[x] += prevScanline[x];
            }
        }

        private static void FilterUpSimd128(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            int x = 0;
            for (; x <= scanline.Length - 16; x += 16)
            {
                Vector128<byte> current = Vector128.LoadUnsafe(ref scanline[x]);
                Vector128<byte> above = Vector128.LoadUnsafe(in prevScanline[x]);

                current += above;
                current.StoreUnsafe(ref scanline[x]);
            }

            for (; x < scanline.Length; x++)
            {
                scanline[x] += prevScanline[x];
            }
        }

        public virtual void FilterAvgScan0(Span<byte> scanline)
        {
            for (int x = _bytesPerPixel; x < scanline.Length; x++)
            {
                scanline[x] += (byte)(scanline[x - _bytesPerPixel] / 2);
            }
        }

        public virtual void FilterAvg(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            int x = 0;
            for (; x < _bytesPerPixel; x++)
            {
                scanline[x] += (byte)(prevScanline[x] / 2);
            }

            for (; x < scanline.Length; x++)
            {
                scanline[x] += (byte)((scanline[x - _bytesPerPixel] + prevScanline[x]) / 2);
            }
        }

        // if y is 0, Sub filter outputs the same values as Paeth filter with prev scanline filled with 0s
        public virtual void FilterPaethScan0(Span<byte> scanline) => FilterSub(scanline);

        public virtual void FilterPaeth(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            for (int x = 0; x < _bytesPerPixel; x++)
            {
                scanline[x] += prevScanline[x];
            }

            for (int x = _bytesPerPixel; x < scanline.Length; x++)
            {
                scanline[x] += FilteringHelpers.PaethPredictor(scanline[x - _bytesPerPixel], prevScanline[x], prevScanline[x - _bytesPerPixel]);
            }
        }
    }
}
