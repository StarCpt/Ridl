using SharpPng.Filtering;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpPng.Reconstruction
{
    internal class Reconstruct24 : IReconstructor
    {
        private readonly int _imageStride;
        private readonly ReconstructGeneric _generic;

        public Reconstruct24(int imageWidth)
        {
            _imageStride = imageWidth * 3;
            _generic = new ReconstructGeneric(imageWidth, 24);
        }

        public void FilterSub(Span<byte> scanline)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                FilterSubSimd128(scanline);
            }
            else
            {
                for (int x = 3; x < _imageStride; x += 3)
                {
                    scanline[x + 0] += scanline[x - 3];
                    scanline[x + 1] += scanline[x - 2];
                    scanline[x + 2] += scanline[x - 1];
                }
            }
        }

        private void FilterSubSimd128(Span<byte> scanline)
        {
            Vector128<byte> prev = Vector128.CreateScalarUnsafe<uint>(Unsafe.As<byte, uint>(ref scanline[0])).AsByte();
            int x = 3;
            for (; x <= _imageStride - 4; x += 3)
            {
                ref uint currentPixelRef = ref Unsafe.As<byte, uint>(ref scanline[x]);
                Vector128<byte> current = Vector128.CreateScalarUnsafe<uint>(currentPixelRef).AsByte();
                current += prev;
                scanline[x + 0] = current[0];
                scanline[x + 1] = current[1];
                scanline[x + 2] = current[2];
                prev = current;
            }

            for (; x < _imageStride; x++)
            {
                scanline[x] += scanline[x - 3];
            }
        }

        public void FilterUp(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            _generic.FilterUp(scanline, prevScanline);
        }

        public void FilterAvg_Scan0(Span<byte> scanline)
        {
            for (int x = 3; x < _imageStride; x += 3)
            {
                scanline[x + 0] += (byte)(scanline[x - 3] / 2);
                scanline[x + 1] += (byte)(scanline[x - 2] / 2);
                scanline[x + 2] += (byte)(scanline[x - 1] / 2);
            }
        }

        public void FilterAvg(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            scanline[0] += (byte)(prevScanline[0] / 2);
            scanline[1] += (byte)(prevScanline[1] / 2);
            scanline[2] += (byte)(prevScanline[2] / 2);

            for (int x = 3; x < _imageStride; x += 3)
            {
                scanline[x + 0] += (byte)((scanline[x - 3] + prevScanline[x + 0]) / 2);
                scanline[x + 1] += (byte)((scanline[x - 2] + prevScanline[x + 1]) / 2);
                scanline[x + 2] += (byte)((scanline[x - 1] + prevScanline[x + 2]) / 2);
            }
        }

        public void FilterPaeth_Scan0(Span<byte> scanline) => FilterSub(scanline);

        public void FilterPaeth(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                if (Sse41.IsSupported)
                    FilterPaethSimd128_Sse41(scanline, prevScanline);
                else
                    FilterPaethSimd128(scanline, prevScanline);
            }
            else
            {
                scanline[0] += prevScanline[0];
                scanline[1] += prevScanline[1];
                scanline[2] += prevScanline[2];

                for (int x = 3; x < _imageStride; x += 3)
                {
                    scanline[x + 0] += FilteringHelpers.PaethPredictor(scanline[x - 3], prevScanline[x + 0], prevScanline[x - 3]);
                    scanline[x + 1] += FilteringHelpers.PaethPredictor(scanline[x - 2], prevScanline[x + 1], prevScanline[x - 2]);
                    scanline[x + 2] += FilteringHelpers.PaethPredictor(scanline[x - 1], prevScanline[x + 2], prevScanline[x - 1]);
                }
            }
        }

        private void FilterPaethSimd128(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            // a = corresponding byte in left pixel
            // b = byte directly above
            // c = corresponding byte in above-left pixel
            // t = current byte
            Vector128<int> a, b, c, t;
            a = Vector128<int>.Zero;
            c = Vector128<int>.Zero;

            Vector128<int> byteMask = Vector128.Create(0xff);

            for (int x = 0; x < _imageStride; x += 3)
            {
                b = Vector128.Create(prevScanline[x], prevScanline[x + 1], prevScanline[x + 2], 0);
                t = Vector128.Create(scanline[x], scanline[x + 1], scanline[x + 2], 0);

                Vector128<int> pa4 = b - c;
                Vector128<int> pb4 = a - c;
                Vector128<int> pc4 = pa4 + pb4;

                pa4 = Vector128.Abs(pa4);
                pb4 = Vector128.Abs(pb4);
                pc4 = Vector128.Abs(pc4);

                // based on https://stackoverflow.com/a/67569032
                Vector128<int> not_a_le_b = Vector128.GreaterThan(pa4, pb4);
                Vector128<int> not_a_le_c = Vector128.GreaterThan(pa4, pc4);
                Vector128<int> not_b_le_c = Vector128.GreaterThan(pb4, pc4);

                Vector128<int> not_take_a = Vector128.BitwiseOr(not_a_le_b, not_a_le_c);
                Vector128<int> pr_bc = Vector128.ConditionalSelect(not_b_le_c, c, b);
                Vector128<int> pr = Vector128.ConditionalSelect(not_take_a, pr_bc, a);

                t = Vector128.BitwiseAnd(Vector128.Add(t, pr), byteMask);

                var tb = t.AsByte();
                scanline[x + 0] = tb[0];
                scanline[x + 1] = tb[4];
                scanline[x + 2] = tb[8];

                a = t;
                c = b;
            }
        }

        private void FilterPaethSimd128_Sse41(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            // a = corresponding byte in left pixel
            // b = byte directly above
            // c = corresponding byte in above-left pixel
            // t = current byte
            Vector128<byte> a, c;
            a = Vector128<byte>.Zero;
            c = Vector128<byte>.Zero;

            int x = 0;
            for (; x < _imageStride - (16 - 1); x += 3)
            {
                var t = Vector128.LoadUnsafe(ref scanline[x]);
                var b = Vector128.LoadUnsafe(in prevScanline[x]);

                var min_ab = Sse2.Min(a, b);
                var max_ab = Sse2.Max(a, b);
                
                var pa = Sse2.SubtractSaturate(max_ab, c);
                var pb = Sse2.SubtractSaturate(c, min_ab);
                
                var min_pab = Sse2.Min(pa, pb);
                var pc = Sse2.Subtract(Sse2.Max(pa, pb), min_pab);
                
                var min_pabc = Sse2.Min(min_pab, pc);
                var use_a = Sse2.CompareEqual(min_pabc, pa);
                var use_b = Sse2.CompareEqual(min_pabc, pb);

                var pr = Sse41.BlendVariable(Sse41.BlendVariable(c, max_ab, use_b), min_ab, use_a);

                t = Sse2.Add(t, pr);

                scanline[x + 0] = t[0];
                scanline[x + 1] = t[1];
                scanline[x + 2] = t[2];

                a = t;
                c = b;
            }

            if (x is 0) // true if image stride < 16
            {
                scanline[x + 0] += prevScanline[x + 0];
                scanline[x + 1] += prevScanline[x + 1];
                scanline[x + 2] += prevScanline[x + 2];
                x += 3;
            }

            for (; x < _imageStride; x += 3)
            {
                scanline[x + 0] += FilteringHelpers.PaethPredictor(scanline[x - 3], prevScanline[x + 0], prevScanline[x - 3]);
                scanline[x + 1] += FilteringHelpers.PaethPredictor(scanline[x - 2], prevScanline[x + 1], prevScanline[x - 2]);
                scanline[x + 2] += FilteringHelpers.PaethPredictor(scanline[x - 1], prevScanline[x + 2], prevScanline[x - 1]);
            }
        }
    }
}
