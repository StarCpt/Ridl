using Ridl.Png.Filtering;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Ridl.Png.Reconstruction
{
    internal class Reconstruct32() : ReconstructGeneric(BITS_PER_PIXEL), IReconstructor
    {
        private const int BITS_PER_PIXEL = 32;

        public override void FilterSub(Span<byte> scanline)
        {
            if (Vector128.IsHardwareAccelerated)
            {
                FilterSubSimd128(scanline);
            }
            else
            {
                for (int x = 4; x < scanline.Length; x += 4)
                {
                    scanline[x + 0] += scanline[x - 4];
                    scanline[x + 1] += scanline[x - 3];
                    scanline[x + 2] += scanline[x - 2];
                    scanline[x + 3] += scanline[x - 1];
                }
            }
        }

        private static void FilterSubSimd128(Span<byte> scanline)
        {
            Vector128<byte> prev = Vector128<byte>.Zero;
            int x = 0;
            for (; x < scanline.Length - (16 - 1); x += 4)
            {
                ref uint currentPixelRef = ref Unsafe.As<byte, uint>(ref scanline[x]);
                Vector128<byte> current = Vector128.LoadUnsafe(ref scanline[x]);
                current += prev;
                currentPixelRef = current.AsUInt32()[0];
                prev = current;
            }

            if (x == 0)
                x = 4;

            for (; x < scanline.Length; x += 4)
            {
                scanline[x + 0] += scanline[x - 4];
                scanline[x + 1] += scanline[x - 3];
                scanline[x + 2] += scanline[x - 2];
                scanline[x + 3] += scanline[x - 1];
            }
        }

        public override void FilterAvgScan0(Span<byte> scanline)
        {
            for (int x = 4; x < scanline.Length; x += 4)
            {
                scanline[x + 0] += (byte)(scanline[x - 4] / 2);
                scanline[x + 1] += (byte)(scanline[x - 3] / 2);
                scanline[x + 2] += (byte)(scanline[x - 2] / 2);
                scanline[x + 3] += (byte)(scanline[x - 1] / 2);
            }
        }

        public override void FilterAvg(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            if (Sse2.IsSupported)
            {
                FilterAvgSse2(scanline, prevScanline);
            }
            else
            {
                scanline[0] += (byte)(prevScanline[0] / 2);
                scanline[1] += (byte)(prevScanline[1] / 2);
                scanline[2] += (byte)(prevScanline[2] / 2);
                scanline[3] += (byte)(prevScanline[3] / 2);

                for (int x = 4; x < scanline.Length; x += 4)
                {
                    scanline[x + 0] += (byte)((scanline[x - 4] + prevScanline[x + 0]) / 2);
                    scanline[x + 1] += (byte)((scanline[x - 3] + prevScanline[x + 1]) / 2);
                    scanline[x + 2] += (byte)((scanline[x - 2] + prevScanline[x + 2]) / 2);
                    scanline[x + 3] += (byte)((scanline[x - 1] + prevScanline[x + 3]) / 2);
                }
            }
        }

        private static void FilterAvgSse2(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            Vector128<byte> left = Vector128<byte>.Zero;
            int x = 0;
            for (; x < scanline.Length - (16 - 1); x += 4)
            {
                ref uint currentPixelRef = ref Unsafe.As<byte, uint>(ref scanline[x]);
                Vector128<byte> current = Vector128.LoadUnsafe(ref scanline[x]);
                Vector128<byte> above = Vector128.LoadUnsafe(in prevScanline[x]);

                var above_left_avg = Sse2.Subtract(Sse2.Add(above, left), Sse2.Average(above, left));
                current = Sse2.Add(above_left_avg, current);

                currentPixelRef = current.AsUInt32()[0];
                left = current;
            }

            if (x == 0)
                x = 4;

            for (; x < scanline.Length; x += 4)
            {
                scanline[x + 0] += (byte)((scanline[x - 4] + prevScanline[x + 0]) / 2);
                scanline[x + 1] += (byte)((scanline[x - 3] + prevScanline[x + 1]) / 2);
                scanline[x + 2] += (byte)((scanline[x - 2] + prevScanline[x + 2]) / 2);
                scanline[x + 3] += (byte)((scanline[x - 1] + prevScanline[x + 3]) / 2);
            }
        }

        public override void FilterPaeth(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            if (Sse41.IsSupported)
            {
                FilterPaethSse41(scanline, prevScanline);
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                FilterPaethSimd128(scanline, prevScanline);
            }
            else
            {
                scanline[0] += prevScanline[0];
                scanline[1] += prevScanline[1];
                scanline[2] += prevScanline[2];
                scanline[3] += prevScanline[3];

                for (int x = 4; x < scanline.Length; x += 4)
                {
                    scanline[x + 0] += FilteringHelpers.PaethPredictor(scanline[x - 4], prevScanline[x + 0], prevScanline[x - 4]);
                    scanline[x + 1] += FilteringHelpers.PaethPredictor(scanline[x - 3], prevScanline[x + 1], prevScanline[x - 3]);
                    scanline[x + 2] += FilteringHelpers.PaethPredictor(scanline[x - 2], prevScanline[x + 2], prevScanline[x - 2]);
                    scanline[x + 3] += FilteringHelpers.PaethPredictor(scanline[x - 1], prevScanline[x + 3], prevScanline[x - 1]);
                }
            }
        }

        private static void FilterPaethSimd128(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            // a = corresponding byte in left pixel
            // b = byte directly above
            // c = corresponding byte in above-left pixel
            // t = current byte
            Vector128<int> a, b, c, t;
            a = Vector128<int>.Zero;
            c = Vector128<int>.Zero;

            Vector128<int> byteMask = Vector128.Create(0xff);

            for (int x = 0; x < scanline.Length; x += 4)
            {
                b = Vector128.Create(prevScanline[x], prevScanline[x + 1], prevScanline[x + 2], prevScanline[x + 3]);
                t = Vector128.Create(scanline[x], scanline[x + 1], scanline[x + 2], scanline[x + 3]);

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
                scanline[x + 3] = tb[12];

                a = t;
                c = b;
            }
        }

        private static void FilterPaethSse41(Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            // a = corresponding byte in left pixel
            // b = byte directly above
            // c = corresponding byte in above-left pixel
            // t = current byte
            Vector128<byte> a, c;
            a = Vector128<byte>.Zero;
            c = Vector128<byte>.Zero;

            int x = 0;
            for (; x < scanline.Length - (16 - 1); x += 4)
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
                scanline[x + 3] = t[3];

                a = t;
                c = b;
            }

            if (x is 0) // true if image stride < 16
            {
                scanline[x + 0] += prevScanline[x + 0];
                scanline[x + 1] += prevScanline[x + 1];
                scanline[x + 2] += prevScanline[x + 2];
                scanline[x + 3] += prevScanline[x + 3];
                x += 4;
            }

            for (; x < scanline.Length; x += 4)
            {
                scanline[x + 0] += FilteringHelpers.PaethPredictor(scanline[x - 4], prevScanline[x + 0], prevScanline[x - 4]);
                scanline[x + 1] += FilteringHelpers.PaethPredictor(scanline[x - 3], prevScanline[x + 1], prevScanline[x - 3]);
                scanline[x + 2] += FilteringHelpers.PaethPredictor(scanline[x - 2], prevScanline[x + 2], prevScanline[x - 2]);
                scanline[x + 3] += FilteringHelpers.PaethPredictor(scanline[x - 1], prevScanline[x + 3], prevScanline[x - 1]);
            }
        }
    }
}
