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
        OS21xBitmapHeader = 12,
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

        public BitmapInfoHeader Header;
        public BitmapV4Header Header4;
        public BitmapV5Header Header5;

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

            public double GetDpiX(int digitsToRoundTo) => double.Round(XPelsPerMeter / 39.3700787402, digitsToRoundTo);
            public double GetDpiY(int digitsToRoundTo) => double.Round(YPelsPerMeter / 39.3700787402, digitsToRoundTo);
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

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal readonly record struct Rgbx32(byte R, byte G, byte B, byte Reserved)
    {
        public Rgb24 ToRgb24() => new(R, G, B);
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

        private static BmpDibHeader ReadDibHeader(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[124];
            stream.ReadExactly(buffer[..4]);

            uint dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            stream.ReadExactly(buffer[4..(int)dibHeaderSize]);

            BmpDibHeader dibHeader = new() { Type = (BmpDibHeaderType)dibHeaderSize };
            switch (dibHeader.Type)
            {
                case BmpDibHeaderType.BitmapCoreHeader: throw new NotImplementedException();
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

        private static byte[] ReadUncompressedPixelData(Stream stream, in BmpDibHeader dibHeader, bool isTopDown)
        {
            int stride = (dibHeader.Header.Width * dibHeader.Header.BitCount + 31) / 32 * 4; // align stride to 4 bytes
            byte[] pixelData = new byte[stride * dibHeader.Header.Height];
            stream.ReadExactly(pixelData);

            return pixelData;
        }

        private static byte[] ReadCompressedPixelData(Stream stream, in BmpDibHeader dibHeader)
        {
            int stride = (dibHeader.Header.Width * dibHeader.Header.BitCount + 31) / 32 * 4; // align stride to 4 bytes

            byte[] pixelData;
            if (dibHeader.Header.Compression is BmpCompressionMethod.Rle8 or BmpCompressionMethod.CmykRle8)
            {
                if (dibHeader.Header.BitCount != 8)
                    throw new Exception($"Bpp must be 8. Bpp={dibHeader.Header.BitCount}");

                pixelData = RleBitmapDecoder.DecodeRle8(stream, (int)dibHeader.Header.SizeImage, stride, dibHeader.Header.Height);
            }
            else // if (dibHeader.Header.Compression is BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle4)
            {
                if (dibHeader.Header.BitCount != 4)
                    throw new Exception($"Bpp must be 4. Bpp={dibHeader.Header.BitCount}");

                pixelData = RleBitmapDecoder.DecodeRle4(stream, (int)dibHeader.Header.SizeImage, stride, dibHeader.Header.Height);
            }

            return pixelData;
        }

        private static BmpImage DecodeBitFields16ToRgb24(Stream stream, in BmpDibHeader dibHeader, uint maskR, uint maskG, uint maskB)
        {
            // >8 bit channels are downsampled to 8

            int resultStride = dibHeader.Header.Width * Rgb24.Size;
            byte[] pixelData = new byte[resultStride * dibHeader.Header.Height];

            int srcRowSize = dibHeader.Header.Width * 2;
            int srcRowPadding = (4 - (srcRowSize % 4)) & 0b11;

            int shiftR = BitOperations.TrailingZeroCount(maskR);
            int shiftG = BitOperations.TrailingZeroCount(maskG);
            int shiftB = BitOperations.TrailingZeroCount(maskB);
            // Assume the masks are contiguous, otherwise they're invalid anyway
            int depthR = BitOperations.TrailingZeroCount((maskR >> shiftR) ^ 0xffff_ffffu);
            int depthG = BitOperations.TrailingZeroCount((maskG >> shiftG) ^ 0xffff_ffffu);
            int depthB = BitOperations.TrailingZeroCount((maskB >> shiftB) ^ 0xffff_ffffu);

            for (int y = 0; y < dibHeader.Header.Height; y++)
            {
                Span<byte> resultRow = pixelData.AsSpan(resultStride * y, resultStride);
                Span<Rgb24> resultRowRgb = MemoryMarshal.Cast<byte, Rgb24>(resultRow);

                // use part of the current row (the end portion so it doesn't conflict) to store the compressed data
                Span<byte> srcRow = resultRow[^srcRowSize..];
                stream.ReadExactly(srcRow);
                stream.ReadDiscard(srcRowPadding);

                Span<ushort> sourceRow16 = MemoryMarshal.Cast<byte, ushort>(srcRow);

                for (int x = 0; x < dibHeader.Header.Width; x++)
                {
                    ushort val = sourceRow16[x];
                    uint r = (val & maskR) >> shiftR;
                    uint g = (val & maskG) >> shiftG;
                    uint b = (val & maskB) >> shiftB;

                    // downsample to 8 bits (if needed) - not sure if I need to use a more sophisticated method
                    r = depthR > 8 ? (r >> (depthR - 8)) : r;
                    g = depthG > 8 ? (g >> (depthG - 8)) : g;
                    b = depthB > 8 ? (b >> (depthB - 8)) : b;

                    resultRowRgb[x] = new Rgb24((byte)r, (byte)g, (byte)b);
                }
            }

            double dpiX = double.Round(dibHeader.Header.XPelsPerMeter / 39.3700787402, 1);
            double dpiY = double.Round(dibHeader.Header.YPelsPerMeter / 39.3700787402, 1);
            return new BmpImage(pixelData, dibHeader.Header.Width, dibHeader.Header.Height, resultStride, BmpPixelFormat.Rgb24, dpiX, dpiY, null);
        }

        private static BmpImage DecodeBitFields32ToRgb48(Stream stream, in BmpDibHeader dibHeader, uint maskR, uint maskG, uint maskB)
        {
            // >16 bit channels are downsampled to 16
            int resultStride = dibHeader.Header.Width * Rgb48.Size;
            byte[] pixelData = new byte[resultStride * dibHeader.Header.Height];

            int srcRowSize = dibHeader.Header.Width * 4;
            int scrRowPadding = (4 - (srcRowSize % 4)) & 0b11;

            int shiftR = BitOperations.TrailingZeroCount(maskR);
            int shiftG = BitOperations.TrailingZeroCount(maskG);
            int shiftB = BitOperations.TrailingZeroCount(maskB);
            // Assume the masks are contiguous, otherwise they're invalid anyway
            int depthR = BitOperations.TrailingZeroCount((maskR >> shiftR) ^ 0xffff_ffffu);
            int depthG = BitOperations.TrailingZeroCount((maskG >> shiftG) ^ 0xffff_ffffu);
            int depthB = BitOperations.TrailingZeroCount((maskB >> shiftB) ^ 0xffff_ffffu);

            for (int y = 0; y < dibHeader.Header.Height; y++)
            {
                Span<byte> row = pixelData.AsSpan(resultStride * y, resultStride);
                Span<Rgb48> rowRgb = MemoryMarshal.Cast<byte, Rgb48>(row);

                // use part of the current row (the end portion so it doesn't conflict) to store the compressed data
                Span<byte> srcRow = row[^srcRowSize..];
                stream.ReadExactly(srcRow);
                stream.ReadDiscard(scrRowPadding);

                Span<ushort> srcRow16 = MemoryMarshal.Cast<byte, ushort>(srcRow);

                for (int x = 0; x < dibHeader.Header.Width; x++)
                {
                    ushort val = srcRow16[x];
                    uint r = (val & maskR) >> shiftR;
                    uint g = (val & maskG) >> shiftG;
                    uint b = (val & maskB) >> shiftB;

                    // downsample to 8 bits (if needed) - not sure if I need to use a more sophisticated method
                    r = depthR > 16 ? (r >> (depthR - 16)) : r;
                    g = depthG > 16 ? (g >> (depthG - 16)) : g;
                    b = depthB > 16 ? (b >> (depthB - 16)) : b;

                    rowRgb[x] = new Rgb48((ushort)r, (ushort)g, (ushort)b);
                }
            }

            double dpiX = double.Round(dibHeader.Header.XPelsPerMeter / 39.3700787402, 1);
            double dpiY = double.Round(dibHeader.Header.YPelsPerMeter / 39.3700787402, 1);
            return new BmpImage(pixelData, dibHeader.Header.Width, dibHeader.Header.Height, resultStride, BmpPixelFormat.Rgb24, dpiX, dpiY, null);
        }

        private static BmpImage DecodeBitFields(Stream stream, in BmpDibHeader dibHeader, uint maskR, uint maskG, uint maskB)
        {
            if (dibHeader.Header.BitCount == 16)
            {
                // For 16 bpp, only the lower 16 bits should be used
                if (((maskR | maskG | maskB) & 0xffff0000) != 0)
                    throw new Exception("Invalid BitField masks. Only the lower 16 bits should be used on 16bpp.");

                return DecodeBitFields16ToRgb24(stream, dibHeader, maskR, maskG, maskB);
            }
            else // if (dibHeader.Header.BitCount == 32)
            {
                return DecodeBitFields32ToRgb48(stream, dibHeader, maskR, maskG, maskB);
            }
        }

        /// <summary>
        /// Convert RGB16 (5 bits each, 1 bit reserved) to RGB24
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="dibHeader"></param>
        /// <returns></returns>
        private static BmpImage DecodeRgb16ToRgb24(Stream stream, in BmpDibHeader dibHeader) =>
            DecodeBitFields16ToRgb24(stream, dibHeader, 0b0_11111_00000_00000, 0b0_00000_11111_00000, 0b0_00000_00000_11111);

        /// <summary>
        /// Decode RGBx32 (RGB24 + 8 reserved bits) to RGB24
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        private static BmpImage DecodeRgbx32ToRgb24(Stream stream, in BmpDibHeader.BitmapInfoHeader header)
        {
            int srcStride = MathHelpers.Align(header.Width * 4, 4);
            int destStride = header.Width * Rgb24.Size;

            byte[] srcScan = new byte[srcStride];
            Span<Rgbx32> srcScanAsRgbx32 = MemoryMarshal.Cast<byte, Rgbx32>(srcScan.AsSpan());

            byte[] convertedPixelData = new byte[destStride * header.Height];

            for (int y = 0; y < header.Height; y++)
            {
                stream.ReadExactly(srcScan);
                Span<Rgb24> destScanAsRgb24 = MemoryMarshal.Cast<byte, Rgb24>(convertedPixelData.AsSpan(destStride * y, destStride));
                for (int x = 0; x < header.Width; x++)
                {
                    destScanAsRgb24[x] = srcScanAsRgbx32[x].ToRgb24();
                }
            }

            return new BmpImage(convertedPixelData, header.Width, header.Height, destStride, BmpPixelFormat.Rgb24, header.GetDpiX(1), header.GetDpiY(1), null);
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

            int extraBitMasksSize = 0;
            Span<uint> bitFields = stackalloc uint[4];
            if (dibHeader.Type is BmpDibHeaderType.BitmapInfoHeader && dibHeader.Header.Compression is BmpCompressionMethod.BitFields)
            {
                if (dibHeader.Header.BitCount is not 16 and not 32)
                {
                    throw new Exception($"{BmpDibHeaderType.BitmapInfoHeader} is only valid with 16 or 32 bpp. Bpp={dibHeader.Header.BitCount}");
                }

                extraBitMasksSize = sizeof(uint) * 3;

                // Read extra bit masks
                stream.ReadExactly(MemoryMarshal.AsBytes(bitFields[..3]));

                // Note: BitFields formats are converted from 16/32 bits to RGB24/48 respectively as it simplifies things
            }

            Bgrx32[] colorTable = new Bgrx32[Math.Min(1 << dibHeader.Header.BitCount, dibHeader.Header.ClrUsed)];
            int colorTableSize = colorTable.Length * 4;
            if (dibHeader.Type is BmpDibHeaderType.BitmapInfoHeader or BmpDibHeaderType.BitmapV4Header or BmpDibHeaderType.BitmapV5Header && dibHeader.Header.ClrUsed > 0)
            {
                // Read color table
                Span<byte> colorTableAsBytes = MemoryMarshal.AsBytes(colorTable.AsSpan());
                stream.ReadExactly(colorTableAsBytes);
            }

            int currentFileOffset = 14 + (int)dibHeader.Type + extraBitMasksSize + colorTableSize;
            stream.ReadDiscard((int)(fileHeader.DataOffset - currentFileOffset));

            byte[] pixelData;
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
                return DecodeBitFields(stream, dibHeader, bitFields[0], bitFields[1], bitFields[2]);
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.AlphaBitFields)
            {
                throw new NotImplementedException();
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Rgb)
            {
                // For uncompressed formats, the origin is bottom-left if y is positive and top-left if y is negative.
                bool isTopDown = int.IsNegative(dibHeader.Header.Height);
                dibHeader.Header.Height = int.Abs(dibHeader.Header.Height);

                switch (dibHeader.Header.BitCount)
                {
                    case 8:
                        pixelData = ReadUncompressedPixelData(stream, dibHeader, isTopDown);
                        break;
                    case 16:
                        return DecodeRgb16ToRgb24(stream, dibHeader);
                    case 24:
                        pixelData = ReadUncompressedPixelData(stream, dibHeader, isTopDown);
                        break;
                    case 32:
                        return DecodeRgbx32ToRgb24(stream, dibHeader.Header);
                    default: throw new Exception($"Invalid {nameof(BmpCompressionMethod)}.{BmpCompressionMethod.Rgb} bit depth.");
                }
                
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Cmyk)
            {
                // For uncompressed formats, the origin is bottom-left if y is positive and top-left if y is negative.
                bool isTopDown = int.IsNegative(dibHeader.Header.Height);
                dibHeader.Header.Height = int.Abs(dibHeader.Header.Height);

                pixelData = ReadUncompressedPixelData(stream, dibHeader, isTopDown);
            }
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Rle8 or BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle8 or BmpCompressionMethod.CmykRle4)
            {
                pixelData = ReadCompressedPixelData(stream, dibHeader);
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

            var image = new BmpImage(pixelData, dibHeader, colorTable);
            return image;
        }
    }
}
