using Ridl.PixelFormats;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    internal enum BmpHalftoneAlgorithm : ushort
    {
        None = 0,
        ErrorDiffusion = 1,
        PANDA = 2,
        SuperCircle = 3,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly record struct Bgrx32(byte B, byte G, byte R, byte Reserved)
    {
        internal Rgb24 ToRgb24() => new(R, G, B);
    }

    public class BmpDecoder
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct BmpFileHeader
        {
            public uint FileSize;
            private uint Reserved;
            public uint DataOffset;
        }

        private static bool CheckSignature(Stream stream)
        {
            Span<byte> sig = [ 0x42, 0x4d ];

            Span<byte> buffer = stackalloc byte[2];
            stream.ReadExactly(buffer);
            return buffer.SequenceEqual(sig);
        }

        private static BmpFileHeader ReadFileHeader(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[12];
            stream.ReadExactly(buffer);
            return MemoryMarshal.AsRef<BmpFileHeader>(buffer);
        }

        private static IBmpHeader ReadBmpHeader(Stream stream, out uint headerSize)
        {
            Span<byte> buffer = stackalloc byte[124]; // size of the largest header type
            stream.ReadExactly(buffer[..4]);

            headerSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer); // size including the length field
            stream.ReadExactly(buffer[..(int)(headerSize - 4)]);

            // TODO: Create unified header struct - figure out how to handle nonexistent fields in some header types

            switch ((BmpHeaderType)headerSize)
            {
                case BmpHeaderType.BitmapCoreHeader:
                    var coreHeader = MemoryMarshal.AsRef<BitmapCoreHeader>(buffer);
                    if (coreHeader.Width <= 0 || coreHeader.Height <= 0)
                    {
                        // This bitmap may be an OS/2 1.x bitmap, which stores w/h as UInt16 instead of Int16.
                        coreHeader = new BitmapCoreHeader(
                            (short)(coreHeader.Width - short.MinValue),
                            (short)(coreHeader.Height - short.MinValue),
                            1, (ushort)coreHeader.BitsPerPixel);
                    }
                    return coreHeader;
                case BmpHeaderType.OS22xBitmapHeader:
                    var os22xHeader = MemoryMarshal.AsRef<OS22xBitmapHeader>(buffer);
                    if (os22xHeader.HalftoneAlgorithm != BmpHalftoneAlgorithm.None)
                        throw new NotImplementedException();
                    return os22xHeader;
                case BmpHeaderType.OS22xBitmapHeader_Short: return MemoryMarshal.AsRef<OS22xBitmapHeader_16>(buffer);
                case BmpHeaderType.BitmapInfoHeader: return MemoryMarshal.AsRef<BitmapInfoHeader>(buffer);
                case BmpHeaderType.BitmapV2InfoHeader: return MemoryMarshal.AsRef<BitmapV2InfoHeader>(buffer);
                case BmpHeaderType.BitmapV3InfoHeader: return MemoryMarshal.AsRef<BitmapV3InfoHeader>(buffer);
                case BmpHeaderType.BitmapV4Header: return MemoryMarshal.AsRef<BitmapV4Header>(buffer);
                case BmpHeaderType.BitmapV5Header: return MemoryMarshal.AsRef<BitmapV5Header>(buffer);
                default: throw new Exception($"Unknown bitmap header type: {headerSize}.");
            }
        }

        private static byte[] ReadUncompressedPixelData(Stream stream, int width, int height, int bpp, bool isTopDown, out int stride)
        {
            stride = (width * bpp + 31) / 32 * 4; // align stride to 4 bytes
            byte[] pixelData = new byte[stride * height];

            if (isTopDown)
            {
                stream.ReadExactly(pixelData);
            }
            else
            {
                // the stream contains the image bottom to top
                for (int y = height - 1; y >= 0; y--)
                {
                    stream.ReadExactly(pixelData.AsSpan(stride * y, stride));
                }
            }

            return pixelData;
        }

        private static BmpImage DecodeRle(Stream stream, IBmpHeader header, Bgrx32[] colorTable)
        {
            int stride = (header.Width * header.BitsPerPixel + 31) / 32 * 4; // align stride to 4 bytes

            // technically top-down orientation isn't valid for RLE formats,
            // but since it's doable just let it use the orientation instead of erroring.
            bool isTopDown = header.IsTopDown;

            byte[] pixelData;
            PixelFormat format;
            if (header.Format is BmpCompressionMethod.Rle8 or BmpCompressionMethod.CmykRle8)
            {
                if (header.BitsPerPixel != 8)
                    throw new Exception($"Bpp must be 8. Bpp={header.BitsPerPixel}");

                pixelData = RleBitmapDecoder.DecodeRle8(stream, stride, header.Height, isTopDown);
                format = PixelFormat.Indexed8;
            }
            else // if (dibHeader.Header.Compression is BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle4)
            {
                if (header.BitsPerPixel != 4)
                    throw new Exception($"Bpp must be 4. Bpp={header.BitsPerPixel}");

                pixelData = RleBitmapDecoder.DecodeRle4(stream, stride, header.Height, isTopDown);
                format = PixelFormat.Indexed4;
            }

            return new BmpImage(pixelData, header.Width, header.Height, stride, format, header.DpiX, header.DpiY, colorTable);
        }

        private static byte[] DecodeBitFields16ToRgb24(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, out int stride)
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

        private static byte[] DecodeBitFields16ToRgba32(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, uint maskA, out int stride)
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

        private static byte[] DecodeBitFields32ToRgb48(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, out int stride)
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

        private static byte[] DecodeBitFields32ToRgba64(Stream stream, int width, int height, bool isTopDown, uint maskR, uint maskG, uint maskB, uint maskA, out int stride)
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

        private static BmpImage DecodeBitFields(Stream stream, IBmpHeader header, BitFields masks)
        {
            bool containsAlpha = masks.A != 0;

            byte[] pixelData;
            int stride;
            PixelFormat format;
            if (header.BitsPerPixel == 16)
            {
                if (((masks.R | masks.G | masks.B | masks.A) & 0xffff0000) != 0)
                    throw new Exception("Invalid BitField masks. Only the lower 16 bits should be used on 16bpp.");

                if (!containsAlpha)
                {
                    pixelData = DecodeBitFields16ToRgb24(stream, header.Width, header.Height, header.IsTopDown, masks.R, masks.G, masks.B, out stride);
                    format = PixelFormat.Rgb24;
                }
                else
                {
                    pixelData = DecodeBitFields16ToRgba32(stream, header.Width, header.Height, header.IsTopDown, masks.R, masks.G, masks.B, masks.A, out stride);
                    format = PixelFormat.Rgba32;
                }
            }
            else // if (dibHeader.Header.BitCount == 32)
            {
                if (!containsAlpha)
                {
                    pixelData = DecodeBitFields32ToRgb48(stream, header.Width, header.Height, header.IsTopDown, masks.R, masks.G, masks.B, out stride);
                    format = PixelFormat.Rgb48;
                }
                else
                {
                    pixelData = DecodeBitFields32ToRgba64(stream, header.Width, header.Height, header.IsTopDown, masks.R, masks.G, masks.B, masks.A, out stride);
                    format = PixelFormat.Rgba64;
                }
            }

            return new BmpImage(pixelData, header.Width, header.Height, stride, format, header.DpiX, header.DpiY, null);
        }

        /// <summary>
        /// Decode RGB16 (5 bits each, 1 bit reserved) to RGB24
        /// </summary>
        private static byte[] DecodeRgb16ToRgb24(Stream stream, int width, int height, out int stride) =>
            DecodeBitFields16ToRgb24(stream, width, int.Abs(height), height < 0, 0b0_11111_00000_00000, 0b0_00000_11111_00000, 0b_00000_00000_11111, out stride);

        /// <summary>
        /// Decode BGRx32 (BGR24 + 8 reserved bits) to RGB24
        /// </summary>
        private static byte[] DecodeBgrx32ToRgb24(Stream stream, int width, int height, bool isTopDown, out int stride)
        {
            int srcStride = width * 4; // is naturally aligned to 4 bytes
            int destStride = width * Rgb24.Size;

            byte[] srcScan = new byte[srcStride];
            Span<Bgrx32> srcScanAsBgrx32 = MemoryMarshal.Cast<byte, Bgrx32>(srcScan.AsSpan());

            byte[] convertedPixelData = new byte[destStride * height];

            if (isTopDown)
            {
                for (int y = 0; y < height; y++)
                {
                    DecodeScanline(y, srcScanAsBgrx32);
                }
            }
            else
            {
                for (int y = height - 1; y >= 0; y--)
                {
                    DecodeScanline(y, srcScanAsBgrx32);
                }
            }

            stride = destStride;
            return convertedPixelData;

            void DecodeScanline(int y, Span<Bgrx32> srcScanAsBgrx32)
            {
                stream.ReadExactly(srcScan);
                Span<Rgb24> destScanAsRgb24 = MemoryMarshal.Cast<byte, Rgb24>(convertedPixelData.AsSpan(destStride * y, destStride));
                for (int x = 0; x < width; x++)
                {
                    destScanAsRgb24[x] = srcScanAsBgrx32[x].ToRgb24();
                }
            }
        }

        private static BmpImage DecodeRgb(Stream stream, IBmpHeader header, Bgrx32[]? colorTable)
        {
            byte[] pixelData;
            PixelFormat format;
            int stride;
            switch (header.BitsPerPixel)
            {
                case 1:
                case 2:
                case 4:
                case 8:
                    pixelData = ReadUncompressedPixelData(stream, header.Width, header.Height, header.BitsPerPixel, header.IsTopDown, out stride);
                    format = header.BitsPerPixel switch
                    {
                        1 => PixelFormat.Indexed1,
                        2 => PixelFormat.Indexed2,
                        4 => PixelFormat.Indexed4,
                        _ => PixelFormat.Indexed8, // 8bpp
                    };
                    break;
                case 16:
                    pixelData = DecodeRgb16ToRgb24(stream, header.Width, header.Height, out stride);
                    format = PixelFormat.Rgb24;
                    break;
                case 24:
                    pixelData = ReadUncompressedPixelData(stream, header.Width, header.Height, header.BitsPerPixel, header.IsTopDown, out stride);
                    format = PixelFormat.Bgr24;
                    break;
                case 32:
                    pixelData = DecodeBgrx32ToRgb24(stream, header.Width, header.Height, header.IsTopDown, out stride);
                    format = PixelFormat.Rgb24;
                    break;
                case 64:
                    // This may contain 16-bit HDR values, in which case the brightness will be incorrect.
                    pixelData = ReadUncompressedPixelData(stream, header.Width, header.Height, header.BitsPerPixel, header.IsTopDown, out stride);
                    format = PixelFormat.Rgba64;
                    break;
                default: throw new Exception($"Invalid bit depth: {header.BitsPerPixel}");
            }

            var image = new BmpImage(pixelData, header.Width, header.Height, stride, format, header.DpiX, header.DpiY, colorTable);
            return image;
        }

        private struct BitFields
        {
            public uint R;
            public uint G;
            public uint B;
            public uint A;
        }

        public IImage Decode(Stream stream)
        {
            if (!BitConverter.IsLittleEndian)
                throw new Exception("Big endian systems are not supported.");

            if (!stream.CanRead)
                throw new ArgumentException($"Provided stream {stream} does not support reading.");

            if (!CheckSignature(stream))
                throw new InvalidDataException("The stream doesn't contain a valid BMP file signature.");

            BmpFileHeader fileHeader = ReadFileHeader(stream);
            IBmpHeader bmpHeader = ReadBmpHeader(stream, out uint headerSize);

            int extraBitMasksSize = 0;
            BitFields bitFields = default;
            if (bmpHeader.Format is BmpCompressionMethod.BitFields or BmpCompressionMethod.AlphaBitFields)
            {
                if (bmpHeader is BitmapInfoHeader)
                {
                    if (bmpHeader.BitsPerPixel is not 16 and not 32)
                    {
                        throw new Exception($"Format {BmpCompressionMethod.BitFields} is only valid for 16 or 32 bpp. Bpp={bmpHeader.BitsPerPixel}");
                    }

                    if (bmpHeader.Format is BmpCompressionMethod.BitFields)
                    {
                        // Read extra bit masks
                        extraBitMasksSize = sizeof(uint) * 3;
                        stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref bitFields, 1))[..extraBitMasksSize]);
                    }
                    else
                    {
                        extraBitMasksSize = sizeof(uint) * 4;
                        stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref bitFields, 1))[..extraBitMasksSize]);
                    }
                }
                else if (bmpHeader is IBmpHeaderV2OrAbove v2Header)
                {
                    bitFields.R = v2Header.RedMask;
                    bitFields.G = v2Header.GreenMask;
                    bitFields.B = v2Header.BlueMask;

                    if (bmpHeader is IBmpHeaderV3OrAbove v3Header)
                    {
                        bitFields.A = v3Header.AlphaMask;
                    }
                }
            }

            Bgrx32[]? colorTable = null;
            int colorTableSize = 0;
            // Check PNG and JPEG since they may erroneously claim to contain a palette with one or more colors
            if (bmpHeader.BitsPerPixel is 1 or 2 or 4 or 8 && bmpHeader.Format is not BmpCompressionMethod.Png and not BmpCompressionMethod.Jpeg)
            {
                int colorTableLength = 1 << bmpHeader.BitsPerPixel;
                if (bmpHeader.PaletteLength > 0 && bmpHeader.PaletteLength < colorTableLength)
                    colorTableLength = bmpHeader.PaletteLength;
                colorTable = new Bgrx32[colorTableLength];

                // A malformed bmp file may have less than colorTableLength palette entries.
                // Instead of throwing an error, try to fill as many entries as possible then leave the remaining entries black.

                int colorEntrySize = bmpHeader is BitmapCoreHeader ? 3 : 4;
                // Number of bytes between the current file offset and the start of the bitmap (according to the file header)
                int availableColorTableBytes = (int)fileHeader.DataOffset - (14 + (int)headerSize + extraBitMasksSize);
                // Max number of entries that can exist in this file. Is <= colorTableLength
                int numColorEntries = Math.Min(colorTableLength, availableColorTableBytes / colorEntrySize);

                colorTableSize = colorEntrySize * numColorEntries;

                if (bmpHeader is BitmapInfoHeader or OS22xBitmapHeader or OS22xBitmapHeader_16 or IBmpHeaderV2OrAbove)
                {
                    stream.ReadExactly(MemoryMarshal.AsBytes(colorTable.AsSpan())[..colorTableSize]);
                }
                else if (bmpHeader is BitmapCoreHeader && bmpHeader.BitsPerPixel is 4 or 8)
                {
                    // The entries here are BGR so they're converted to BGRx
                    for (int i = 0; i < numColorEntries; i++)
                    {
                        stream.ReadExactly(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref colorTable[i], 1))[..3]);
                    }
                }
                else
                {
                    // Color table wasn't found
                    colorTable = null;
                    colorTableSize = 0;
                }
            }

            int currentFileOffset = 14 + (int)headerSize + extraBitMasksSize + colorTableSize;
            stream.ReadDiscard((int)(fileHeader.DataOffset - currentFileOffset));

            if (currentFileOffset > fileHeader.DataOffset)
                throw new Exception("An error occurred while reading bitmap metadata.");

            IImage image;
            if (bmpHeader.Format is BmpCompressionMethod.Jpeg)
            {
                // TODO: Implement JPEG decoding
                throw new NotImplementedException();
            }
            else if (bmpHeader.Format is BmpCompressionMethod.Png)
            {
                image = Ridl.Png.PngDecoder.Default.Decode(stream);
            }
            else if (bmpHeader.Format is BmpCompressionMethod.BitFields or BmpCompressionMethod.AlphaBitFields)
            {
                image = DecodeBitFields(stream, bmpHeader, bitFields);
            }
            else if (bmpHeader.Format is BmpCompressionMethod.Rgb)
            {
                image = DecodeRgb(stream, bmpHeader, colorTable);
            }
            else if (bmpHeader.Format is BmpCompressionMethod.Cmyk)
            {
                throw new NotImplementedException();
            }
            else if (bmpHeader.Format is BmpCompressionMethod.Rle8 or BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle8 or BmpCompressionMethod.CmykRle4)
            {
                image = DecodeRle(stream, bmpHeader, colorTable ?? throw new Exception("Indexed formats must contain a color table."));
            }
            else if (bmpHeader.Format is BmpCompressionMethod.Huffman1D)
            {
                throw new NotImplementedException();
            }
            else if (bmpHeader.Format is BmpCompressionMethod.Rle24)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new InvalidDataException("Unknown BMP compression type.");
            }

            if (bmpHeader is BitmapV5Header v5Header && v5Header.ProfileSize > 0)
            {
                currentFileOffset = (int)(fileHeader.DataOffset + v5Header.SizeImage);
                stream.ReadDiscard((int)((14 + v5Header.ProfileData) - currentFileOffset));
            
                // Read ICC Color Profile
                byte[] colorProfileData = new byte[v5Header.ProfileSize];
                stream.ReadExactly(colorProfileData);

                //throw new NotImplementedException();
            }

            return image;
        }
    }
}
