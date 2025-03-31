using Ridl.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ridl.Bmp
{
    internal class BitFieldsBitmapDecoder
    {
        internal static byte[] DecodeBitFields16ToRgb24(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, out int stride)
        {
            // >8 bit channels are downsampled to 8

            int resultStride = width * Rgb24.Size;
            byte[] pixelData = new byte[resultStride * height];

            int srcRowLength = width * 2;
            int srcRowPadding = (4 - (srcRowLength % 4)) & 0b11;

            int shiftR = BitOperations.TrailingZeroCount(maskR);
            int shiftG = BitOperations.TrailingZeroCount(maskG);
            int shiftB = BitOperations.TrailingZeroCount(maskB);
            // Assume the masks are contiguous, otherwise they're invalid anyway
            int depthR = BitOperations.TrailingZeroCount((maskR >> shiftR) ^ 0xffff_ffffu);
            int depthG = BitOperations.TrailingZeroCount((maskG >> shiftG) ^ 0xffff_ffffu);
            int depthB = BitOperations.TrailingZeroCount((maskB >> shiftB) ^ 0xffff_ffffu);

            // (2^8-1) / (2^n-1)
            double scaleR = (double)byte.MaxValue / ((1u << depthR) - 1u);
            double scaleG = (double)byte.MaxValue / ((1u << depthG) - 1u);
            double scaleB = (double)byte.MaxValue / ((1u << depthB) - 1u);

            if (isTopDown)
            {
                for (int y = 0; y < height; y++)
                {
                    DecodeScanline(y);
                }
            }
            else
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    DecodeScanline(y);
                }
            }

            stride = resultStride;
            return pixelData;

            void DecodeScanline(int y)
            {
                Span<byte> resultRow = pixelData.AsSpan(resultStride * y, resultStride);
                Span<Rgb24> resultRowRgb = MemoryMarshal.Cast<byte, Rgb24>(resultRow);

                // use part of the current row (the end portion so it doesn't conflict) to store the compressed data
                Span<byte> srcRow = resultRow[^srcRowLength..];
                stream.ReadExactly(srcRow);
                stream.ReadDiscard(srcRowPadding);

                Span<ushort> sourceRow16 = MemoryMarshal.Cast<byte, ushort>(srcRow);

                for (int x = 0; x < width; x++)
                {
                    ushort val = sourceRow16[x];
                    uint r = (val & maskR) >> shiftR;
                    uint g = (val & maskG) >> shiftG;
                    uint b = (val & maskB) >> shiftB;

                    // rescale to 8 bits
                    r = (uint)(r * scaleR);
                    g = (uint)(g * scaleG);
                    b = (uint)(b * scaleB);

                    resultRowRgb[x] = new Rgb24((byte)r, (byte)g, (byte)b);
                }
            }
        }

        internal static byte[] DecodeBitFields16ToRgba32(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, uint maskA, out int stride)
        {
            // >8 bit channels are downsampled to 8

            int resultStride = width * Rgba32.Size;
            byte[] pixelData = new byte[resultStride * height];

            int srcRowLength = width * 2;
            int srcRowPadding = (4 - (srcRowLength % 4)) & 0b11;

            int shiftR = BitOperations.TrailingZeroCount(maskR);
            int shiftG = BitOperations.TrailingZeroCount(maskG);
            int shiftB = BitOperations.TrailingZeroCount(maskB);
            int shiftA = BitOperations.TrailingZeroCount(maskA);
            // Assume the masks are contiguous, otherwise they're invalid anyway
            int depthR = BitOperations.TrailingZeroCount((maskR >> shiftR) ^ 0xffff_ffffu);
            int depthG = BitOperations.TrailingZeroCount((maskG >> shiftG) ^ 0xffff_ffffu);
            int depthB = BitOperations.TrailingZeroCount((maskB >> shiftB) ^ 0xffff_ffffu);
            int depthA = BitOperations.TrailingZeroCount((maskA >> shiftA) ^ 0xffff_ffffu);

            // (2^8-1) / (2^n-1)
            double scaleR = (double)byte.MaxValue / ((1u << depthR) - 1u);
            double scaleG = (double)byte.MaxValue / ((1u << depthG) - 1u);
            double scaleB = (double)byte.MaxValue / ((1u << depthB) - 1u);
            double scaleA = (double)byte.MaxValue / ((1u << depthA) - 1u);

            if (isTopDown)
            {
                for (int y = 0; y < height; y++)
                {
                    DecodeScanline(y);
                }
            }
            else
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    DecodeScanline(y);
                }
            }

            stride = resultStride;
            return pixelData;

            void DecodeScanline(int y)
            {
                Span<byte> resultRow = pixelData.AsSpan(resultStride * y, resultStride);
                Span<Rgba32> resultRowRgba = MemoryMarshal.Cast<byte, Rgba32>(resultRow);

                // use part of the current row (the end portion so it doesn't conflict) to store the compressed data
                Span<byte> srcRow = resultRow[^srcRowLength..];
                stream.ReadExactly(srcRow);
                stream.ReadDiscard(srcRowPadding);

                Span<ushort> sourceRow16 = MemoryMarshal.Cast<byte, ushort>(srcRow);

                for (int x = 0; x < width; x++)
                {
                    ushort val = sourceRow16[x];
                    uint r = (val & maskR) >> shiftR;
                    uint g = (val & maskG) >> shiftG;
                    uint b = (val & maskB) >> shiftB;
                    uint a = (val & maskA) >> shiftA;

                    // rescale to 8 bits
                    r = (uint)(r * scaleR);
                    g = (uint)(g * scaleG);
                    b = (uint)(b * scaleB);
                    a = (uint)(a * scaleA);

                    resultRowRgba[x] = new Rgba32((byte)r, (byte)g, (byte)b, (byte)a);
                }
            }
        }

        internal static byte[] DecodeBitFields32ToRgb48(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, out int stride)
        {
            // >16 bit channels are downsampled to 16
            int resultStride = width * Rgb48.Size;
            byte[] pixelData = new byte[resultStride * height];

            int srcRowSize = width * 4;
            int scrRowPadding = (4 - (srcRowSize % 4)) & 0b11;

            int shiftR = BitOperations.TrailingZeroCount(maskR);
            int shiftG = BitOperations.TrailingZeroCount(maskG);
            int shiftB = BitOperations.TrailingZeroCount(maskB);
            // Assume the masks are contiguous, otherwise they're invalid anyway
            int depthR = BitOperations.TrailingZeroCount(~(maskR >> shiftR));
            int depthG = BitOperations.TrailingZeroCount(~(maskG >> shiftG));
            int depthB = BitOperations.TrailingZeroCount(~(maskB >> shiftB));

            // (2^8-1) / (2^n-1)
            double scaleR = (double)ushort.MaxValue / ((1u << depthR) - 1u);
            double scaleG = (double)ushort.MaxValue / ((1u << depthG) - 1u);
            double scaleB = (double)ushort.MaxValue / ((1u << depthB) - 1u);

            if (isTopDown)
            {
                for (int y = 0; y < height; y++)
                {
                    DecodeScanline(y);
                }
            }
            else
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    DecodeScanline(y);
                }
            }

            stride = resultStride;
            return pixelData;

            void DecodeScanline(int y)
            {
                Span<byte> row = pixelData.AsSpan(resultStride * y, resultStride);
                Span<Rgb48> rowRgb = MemoryMarshal.Cast<byte, Rgb48>(row);

                // use part of the current row (the end portion so it doesn't conflict) to store the compressed data
                Span<byte> srcRow = row[^srcRowSize..];
                stream.ReadExactly(srcRow);
                stream.ReadDiscard(scrRowPadding);

                Span<uint> srcRow32 = MemoryMarshal.Cast<byte, uint>(srcRow);

                for (int x = 0; x < width; x++)
                {
                    uint val = srcRow32[x];
                    uint r = (val & maskR) >> shiftR;
                    uint g = (val & maskG) >> shiftG;
                    uint b = (val & maskB) >> shiftB;

                    // rescale to 16 bits
                    r = (uint)(r * scaleR);
                    g = (uint)(g * scaleG);
                    b = (uint)(b * scaleB);

                    rowRgb[x] = new Rgb48((ushort)r, (ushort)g, (ushort)b);
                }
            }
        }

        internal static byte[] DecodeBitFields32ToRgba64(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, uint maskA, out int stride)
        {
            // >16 bit channels are downsampled to 16
            int resultStride = width * Rgba64.Size;
            byte[] pixelData = new byte[resultStride * height];

            int srcRowSize = width * 4;
            int scrRowPadding = (4 - (srcRowSize % 4)) & 0b11;

            int shiftR = BitOperations.TrailingZeroCount(maskR);
            int shiftG = BitOperations.TrailingZeroCount(maskG);
            int shiftB = BitOperations.TrailingZeroCount(maskB);
            int shiftA = BitOperations.TrailingZeroCount(maskA);
            // Assume the masks are contiguous, otherwise they're invalid anyway
            int depthR = BitOperations.TrailingZeroCount(~(maskR >> shiftR));
            int depthG = BitOperations.TrailingZeroCount(~(maskG >> shiftG));
            int depthB = BitOperations.TrailingZeroCount(~(maskB >> shiftB));
            int depthA = BitOperations.TrailingZeroCount(~(maskA >> shiftA));

            // (2^8-1) / (2^n-1)
            double scaleR = (double)ushort.MaxValue / ((1u << depthR) - 1u);
            double scaleG = (double)ushort.MaxValue / ((1u << depthG) - 1u);
            double scaleB = (double)ushort.MaxValue / ((1u << depthB) - 1u);
            double scaleA = (double)ushort.MaxValue / ((1u << depthA) - 1u);

            if (isTopDown)
            {
                for (int y = 0; y < height; y++)
                {
                    DecodeScanline(y);
                }
            }
            else
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    DecodeScanline(y);
                }
            }

            stride = resultStride;
            return pixelData;

            void DecodeScanline(int y)
            {
                Span<byte> row = pixelData.AsSpan(resultStride * y, resultStride);
                Span<Rgba64> rowRgba = MemoryMarshal.Cast<byte, Rgba64>(row);

                // use part of the current row (the end portion so it doesn't conflict) to store the compressed data
                Span<byte> srcRow = row[^srcRowSize..];
                stream.ReadExactly(srcRow);
                stream.ReadDiscard(scrRowPadding);

                Span<uint> srcRow32 = MemoryMarshal.Cast<byte, uint>(srcRow);

                for (int x = 0; x < width; x++)
                {
                    uint val = srcRow32[x];
                    uint r = (val & maskR) >> shiftR;
                    uint g = (val & maskG) >> shiftG;
                    uint b = (val & maskB) >> shiftB;
                    uint a = (val & maskA) >> shiftA;

                    // rescale to 16 bits
                    r = (uint)(r * scaleR);
                    g = (uint)(g * scaleG);
                    b = (uint)(b * scaleB);
                    a = (uint)(a * scaleA);

                    rowRgba[x] = new Rgba64((ushort)r, (ushort)g, (ushort)b, (ushort)a);
                }
            }
        }
    }
}
