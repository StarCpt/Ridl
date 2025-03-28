using Ridl.PixelTypes;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;

// References:
// https://en.wikipedia.org/wiki/BMP_file_format
// https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-storage
// https://www.loc.gov/preservation/digital/formats/fdd/fdd000189.shtml

namespace Ridl.Bmp
{
    internal enum BmpDibHeaderType : uint
    {
        BitmapCoreHeader = 12,
        OS21xBitmapHeader = BitmapCoreHeader, // Same size, only difference is that BitmapCoreHeader stores W/H as Int16 and OS/2 1.x stores it as UInt16
        OS22xBitmapHeader = 64,
        OS22xBitmapHeader_Short = 16,
        BitmapInfoHeader = 40,
        BitmapV2InfoHeader = 52,
        BitmapV3InfoHeader = 56,
        BitmapV4Header = 108,
        BitmapV5Header = 124,
    }

    /// <remarks>
    /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/4e588f70-bd92-4a6f-b77f-35d0feaf7a57"/>
    /// </remarks>
    internal enum BmpCompressionMethod : uint
    {
        Rgb = 0,
        Rle8 = 1,
        Rle4 = 2,
        BitFields = 3,
        Jpeg = 4,
        Png = 5,
        AlphaBitFields = 6, // not in MS docs but mentioned in the wikipedia article
        Cmyk = 11,
        CmykRle8 = 12,
        CmykRle4 = 13,
    }

    internal struct BmpDibHeader
    {
        public BmpDibHeaderType Type;

        public BitmapCoreHeader Core;
        public BitmapInfoHeader Header;
        public BitmapV4Header Header4;
        public BitmapV5Header Header5;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BitmapCoreHeader
        {
            public short Width; // Int16 for Windows 2.x, UInt16 on OS/2 1.x
            public short Height;
            public ushort Planes;
            public ushort BitCount;
        }

        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader"/>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BitmapInfoHeader
        {
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public BmpCompressionMethod Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;

            public readonly double GetDpiX(int digitsToRoundTo) => MathHelpers.DpmToDpi(XPelsPerMeter, digitsToRoundTo);
            public readonly double GetDpiY(int digitsToRoundTo) => MathHelpers.DpmToDpi(YPelsPerMeter, digitsToRoundTo);
        }

        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapv4header"/>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BitmapV4Header
        {
            public uint RedMask;
            public uint GreenMask;
            public uint BlueMask;
            public uint AlphaMask;
            public uint CSType;
            public uint RedX,   RedY,   RedZ;
            public uint GreenX, GreenY, GreenZ;
            public uint BlueX,  BlueY,  BlueZ;
            public uint GammaRed;
            public uint GammaGreen;
            public uint GammaBlue;
        }

        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapv5header"/>
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BitmapV5Header
        {
            public uint Intent;
            public uint ProfileData;
            public uint ProfileSize;
            private uint Reserved;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly record struct Bgrx32(byte B, byte G, byte R, byte Reserved)
    {
        internal Rgb24 ToRgb24() => new(R, G, B);
    }

    public class BmpDecoder
    {
        public const double DEFAULT_BMP_DPI = 96;

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

        private static BmpDibHeader ReadDibHeader(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[124];
            stream.ReadExactly(buffer[..4]);

            uint dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            stream.ReadExactly(buffer[4..(int)dibHeaderSize]);

            BmpDibHeader dibHeader = new() { Type = (BmpDibHeaderType)dibHeaderSize };
            switch (dibHeader.Type)
            {
                case BmpDibHeaderType.BitmapCoreHeader:
                    dibHeader.Core = MemoryMarshal.AsRef<BmpDibHeader.BitmapCoreHeader>(buffer[4..]);
                    if (dibHeader.Core.Width <= 0 || dibHeader.Core.Height <= 0)
                        throw new Exception("This bitmap may be an OS/2 1.x bitmap, which is not supported.");
                    break;
                case BmpDibHeaderType.OS22xBitmapHeader: throw new NotImplementedException();
                case BmpDibHeaderType.OS22xBitmapHeader_Short: throw new NotImplementedException();
                case BmpDibHeaderType.BitmapInfoHeader:
                    dibHeader.Header = MemoryMarshal.AsRef<BmpDibHeader.BitmapInfoHeader>(buffer[4..]);
                    break;
                case BmpDibHeaderType.BitmapV2InfoHeader: throw new NotImplementedException();
                case BmpDibHeaderType.BitmapV3InfoHeader: throw new NotImplementedException();
                case BmpDibHeaderType.BitmapV4Header:
                    dibHeader.Header = MemoryMarshal.AsRef<BmpDibHeader.BitmapInfoHeader>(buffer[4..]);
                    dibHeader.Header4 = MemoryMarshal.AsRef<BmpDibHeader.BitmapV4Header>(buffer[(4 + 36)..]);
                    break;
                case BmpDibHeaderType.BitmapV5Header:
                    dibHeader.Header = MemoryMarshal.AsRef<BmpDibHeader.BitmapInfoHeader>(buffer[4..]);
                    dibHeader.Header4 = MemoryMarshal.AsRef<BmpDibHeader.BitmapV4Header>(buffer[(4 + 36)..]);
                    dibHeader.Header5 = MemoryMarshal.AsRef<BmpDibHeader.BitmapV5Header>(buffer[(4 + 36 + 68)..]);
                    break;
                default: throw new Exception($"Unknown bitmap header type: {dibHeaderSize}.");
            }
            return dibHeader;
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

        private static BmpImage DecodeRle(Stream stream, in BmpDibHeader.BitmapInfoHeader header, Bgrx32[] colorTable)
        {
            int stride = (header.Width * header.BitCount + 31) / 32 * 4; // align stride to 4 bytes

            bool isTopDown = header.Height < 0;
            int height = int.Abs(header.Height);

            byte[] pixelData;
            BmpPixelFormat format;
            if (header.Compression is BmpCompressionMethod.Rle8 or BmpCompressionMethod.CmykRle8)
            {
                if (header.BitCount != 8)
                    throw new Exception($"Bpp must be 8. Bpp={header.BitCount}");

                pixelData = RleBitmapDecoder.DecodeRle8(stream, stride, height, isTopDown);
                format = BmpPixelFormat.Indexed8;
            }
            else // if (dibHeader.Header.Compression is BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle4)
            {
                if (header.BitCount != 4)
                    throw new Exception($"Bpp must be 4. Bpp={header.BitCount}");

                pixelData = RleBitmapDecoder.DecodeRle4(stream, stride, height, isTopDown);
                format = BmpPixelFormat.Indexed4;
            }

            return new BmpImage(pixelData, header.Width, header.Height, stride, format, header.GetDpiX(1), header.GetDpiY(1), colorTable);
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

        private static BmpImage DecodeBitFields(Stream stream, in BmpDibHeader.BitmapInfoHeader header, uint maskR, uint maskG, uint maskB)
        {
            int width = header.Width;
            int height = header.Height;
            double dpiX = header.GetDpiX(1);
            double dpiY = header.GetDpiY(1);

            bool isTopDown = height < 0;
            height = int.Abs(height);

            byte[] pixelData;
            int stride;
            BmpPixelFormat format;
            if (header.BitCount == 16)
            {
                if (((maskR | maskG | maskB) & 0xffff0000) != 0)
                    throw new Exception("Invalid BitField masks. Only the lower 16 bits should be used on 16bpp.");

                pixelData = DecodeBitFields16ToRgb24(stream, width, height, isTopDown, maskR, maskG, maskB, out stride);
                format = BmpPixelFormat.Rgb24;
            }
            else // if (dibHeader.Header.BitCount == 32)
            {
                pixelData = DecodeBitFields32ToRgb48(stream, width, height, isTopDown, maskR, maskG, maskB, out stride);
                format = BmpPixelFormat.Rgb48;
            }

            return new BmpImage(pixelData, width, height, stride, format, dpiX, dpiY, null);
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

        private static BmpImage DecodeRgb(Stream stream, BmpDibHeader dibHeader, Bgrx32[]? colorTable)
        {
            // For uncompressed formats, the origin is bottom-left if y is positive and top-left if y is negative.
            bool isTopDown = dibHeader.Header.Height < 0;
            dibHeader.Header.Height = int.Abs(dibHeader.Header.Height);

            int bpp = dibHeader.Type is BmpDibHeaderType.BitmapCoreHeader ? dibHeader.Core.BitCount : dibHeader.Header.BitCount;
            int width = dibHeader.Type is BmpDibHeaderType.BitmapCoreHeader ? dibHeader.Core.Width : dibHeader.Header.Width;
            int height = dibHeader.Type is BmpDibHeaderType.BitmapCoreHeader ? dibHeader.Core.Height : dibHeader.Header.Height;
            double dpiX = dibHeader.Type is BmpDibHeaderType.BitmapCoreHeader ? DEFAULT_BMP_DPI : dibHeader.Header.GetDpiX(1);
            double dpiY = dibHeader.Type is BmpDibHeaderType.BitmapCoreHeader ? DEFAULT_BMP_DPI : dibHeader.Header.GetDpiY(1);

            byte[] pixelData;
            BmpPixelFormat format;
            int stride;
            switch (bpp)
            {
                case 1:
                case 2:
                case 4:
                case 8:
                    pixelData = ReadUncompressedPixelData(stream, width, height, bpp, isTopDown, out stride);
                    format = bpp switch
                    {
                        1 => BmpPixelFormat.Indexed1,
                        2 => BmpPixelFormat.Indexed2,
                        4 => BmpPixelFormat.Indexed4,
                        _ => BmpPixelFormat.Indexed8, // 8bpp
                    };
                    break;
                case 16:
                    pixelData = DecodeRgb16ToRgb24(stream, width, height, out stride);
                    format = BmpPixelFormat.Rgb24;
                    break;
                case 24:
                    pixelData = ReadUncompressedPixelData(stream, width, height, bpp, isTopDown, out stride);
                    format = BmpPixelFormat.Bgr24;
                    break;
                case 32:
                    pixelData = DecodeBgrx32ToRgb24(stream, width, height, isTopDown, out stride);
                    format = BmpPixelFormat.Rgb24;
                    break;
                default: throw new Exception($"Invalid bit depth: {bpp}");
            }

            var image = new BmpImage(pixelData, width, height, stride, format, dpiX, dpiY, colorTable);
            return image;
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
            BmpDibHeader dibHeader = ReadDibHeader(stream);

            if (dibHeader.Type is BmpDibHeaderType.BitmapInfoHeader or BmpDibHeaderType.BitmapV4Header or BmpDibHeaderType.BitmapV5Header)
            {
                // When certain fields are 0, set them to their default values
                if (dibHeader.Header.BitCount <= 8 && dibHeader.Header.ClrUsed == 0)
                    dibHeader.Header.ClrUsed = 1u << dibHeader.Header.BitCount;

                if (dibHeader.Header.XPelsPerMeter == 0)
                    dibHeader.Header.XPelsPerMeter = MathHelpers.DpiToDpm(DEFAULT_BMP_DPI);

                if (dibHeader.Header.YPelsPerMeter == 0)
                    dibHeader.Header.YPelsPerMeter = MathHelpers.DpiToDpm(DEFAULT_BMP_DPI);

                if (dibHeader.Header.SizeImage == 0)
                {
                    // The decoder should auto determine the image size depending on bit depth (aligned stride * height). Only valid for RGB format.
                    if (dibHeader.Header.Compression is not BmpCompressionMethod.Rgb)
                        throw new Exception("Image size cannot be 0 for non-RGB formats.");

                    int stride = (dibHeader.Header.Width * dibHeader.Header.BitCount + 31) / 32 * 4;
                    dibHeader.Header.SizeImage = (uint)(stride * dibHeader.Header.Height);
                }
            }

            int extraBitMasksSize = 0;
            Span<uint> bitFields = stackalloc uint[4];
            if (dibHeader.Type is BmpDibHeaderType.BitmapInfoHeader && dibHeader.Header.Compression is BmpCompressionMethod.BitFields)
            {
                if (dibHeader.Header.BitCount is not 16 and not 32)
                {
                    throw new Exception($"{nameof(BmpCompressionMethod)}.{BmpCompressionMethod.BitFields} is only valid with 16 or 32 bpp. Bpp={dibHeader.Header.BitCount}");
                }

                extraBitMasksSize = sizeof(uint) * 3;

                // Read extra bit masks
                stream.ReadExactly(MemoryMarshal.AsBytes(bitFields[..3]));
            }

            Bgrx32[]? colorTable = null;
            int colorTableSize = 0;
            if (dibHeader.Type is BmpDibHeaderType.BitmapInfoHeader or BmpDibHeaderType.BitmapV4Header or BmpDibHeaderType.BitmapV5Header && dibHeader.Header.ClrUsed > 0)
            {
                // Read color table
                colorTable = new Bgrx32[Math.Min(1 << dibHeader.Header.BitCount, dibHeader.Header.ClrUsed)];
                colorTableSize = colorTable.Length * 4;
                Span<byte> colorTableBytes = MemoryMarshal.AsBytes(colorTable.AsSpan());
                stream.ReadExactly(colorTableBytes);
            }
            else if (dibHeader.Type is BmpDibHeaderType.BitmapCoreHeader && dibHeader.Core.BitCount is 4 or 8)
            {
                colorTable = new Bgrx32[1 << dibHeader.Core.BitCount];
                colorTableSize = colorTable.Length * 3;
                byte[] temp = new byte[3 * colorTable.Length];
                stream.ReadExactly(temp);

                for (int i = 0; i < colorTable.Length; i++)
                {
                    colorTable[i] = new Bgrx32(temp[i * 3], temp[i * 3 + 1], temp[i * 3 + 2], 0);
                }
            }

            int currentFileOffset = 14 + (int)dibHeader.Type + extraBitMasksSize + colorTableSize;
            stream.ReadDiscard((int)(fileHeader.DataOffset - currentFileOffset));

            if (dibHeader.Type is BmpDibHeaderType.BitmapV5Header && dibHeader.Header5.ProfileSize > 0)
            {
                throw new NotImplementedException();
            }

            if (dibHeader.Header.Compression is BmpCompressionMethod.Jpeg)
            {
                // TODO: Implement JPEG decoding
                throw new NotImplementedException();
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Png)
            {
                return Ridl.Png.PngDecoder.Default.Decode(stream);
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.BitFields)
            {
                return DecodeBitFields(stream, dibHeader.Header, bitFields[0], bitFields[1], bitFields[2]);
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.AlphaBitFields)
            {
                throw new NotImplementedException();
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Rgb)
            {
                return DecodeRgb(stream, dibHeader, colorTable);
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Cmyk)
            {
                // For uncompressed formats, the origin is bottom-left if y is positive and top-left if y is negative.
                bool isTopDown = dibHeader.Header.Height < 0;
                dibHeader.Header.Height = int.Abs(dibHeader.Header.Height);

                throw new NotImplementedException();

                //pixelData = ReadUncompressedPixelData(stream, dibHeader.Header, isTopDown, out stride);
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Rle8 or BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle8 or BmpCompressionMethod.CmykRle4)
            {
                return DecodeRle(stream, dibHeader.Header, colorTable ?? throw new Exception("Indexed formats must contain a color table."));
            }
            else
            {
                throw new InvalidDataException("Unknown BMP compression type.");
            }

            if (dibHeader.Type is BmpDibHeaderType.BitmapV5Header && dibHeader.Header5.ProfileSize > 0)
            {
                currentFileOffset = (int)(fileHeader.DataOffset + dibHeader.Header.SizeImage);
                stream.ReadDiscard((int)((14 + dibHeader.Header5.ProfileData) - currentFileOffset));

                // Read ICC Color Profile
                byte[] colorProfileData = new byte[dibHeader.Header5.ProfileSize];
                stream.ReadExactly(colorProfileData);
            }

            throw new NotImplementedException();
            //var image = new BmpImage(pixelData,);
            //return image;
        }
    }
}
