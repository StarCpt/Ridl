using System.Buffers.Binary;
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
        //AlphaBitFields = 6,
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
    public readonly record struct Rgb32(byte B, byte G, byte R, byte Reserved);

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

        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/b64d0c0b-bb80-4b53-8382-f38f264eb685"/>
        /// </remarks>
        private static byte[] DecompressRle8(Stream stream, int compressedSize, int stride, int height)
        {
            byte[] pixelData = new byte[stride * height];
            Span<byte> buffer = stackalloc byte[2];
            Span<byte> row = pixelData.AsSpan(0, stride);
            int x = 0, y = 0;
            int readBytes = 0;
            while (readBytes < compressedSize)
            {
                stream.ReadExactly(buffer);
                readBytes += 2;
                if (buffer[0] > 0) // Encoded mode
                {
                    byte runLength = buffer[0];
                    byte value = buffer[1];
                    row.Slice(x, runLength).Fill(value);
                }
                else
                {
                    if (buffer[1] == 0) // End of line
                    {
                        x = 0;
                        y++;
                        row = pixelData.AsSpan(stride * y, stride);
                        continue;
                    }
                    else if (buffer[1] == 1) // End of bitmap
                    {
                        break; // Exit while loop
                    }
                    else if (buffer[1] == 2) // Delta (Relative) mode
                    {
                        stream.ReadExactly(buffer);
                        readBytes += 2;

                        byte relX = buffer[0];
                        byte relY = buffer[1];

                        x += relX;
                        y += relY;
                    }
                    else // Absolute mode
                    {
                        byte runLength = buffer[1];
                        stream.ReadExactly(row.Slice(x, runLength));
                        readBytes += runLength;

                        int padding = runLength % 2; // Each run is padded to a 2-byte boundary
                        stream.ReadDiscard(padding);
                        readBytes += padding;
                    }
                }
            }

            return pixelData;
        }

        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/73b57f24-6d78-4eeb-9c06-8f892d88f1ab"/>
        /// </remarks>
        private static byte[] DecompressRle4(Stream stream, int compressedSize, int stride, int width, int height)
        {
            byte[] pixelData = new byte[stride * height];
            Span<byte> buffer = stackalloc byte[2];
            Span<byte> buffer2 = stackalloc byte[256];
            Span<byte> row = pixelData.AsSpan(0, stride);
            int x = 0, y = 0;
            int readBytes = 0;
            while (true)
            {
                stream.ReadExactly(buffer);
                readBytes += 2;

                if (buffer[0] > 0) // Encoded mode
                {
                    byte runLength = buffer[0];
                    byte value = buffer[1];

                    for (int i = 0; i < runLength; i++, x++)
                    {
                        int val = (value << i % 2 * 4) & 0xf0;
                        row[x / 2] |= (byte)(val >> x % 2 * 4);
                    }
                }
                else
                {
                    if (buffer[1] == 0) // End of line
                    {
                        x = 0;
                        y++;
                        row = pixelData.AsSpan(stride * y, stride);
                        continue;
                    }
                    else if (buffer[1] == 1) // End of bitmap
                    {
                        break; // Exit while loop
                    }
                    else if (buffer[1] == 2) // Delta (Relative) mode
                    {
                        stream.ReadExactly(buffer);
                        readBytes += 2;

                        byte relX = buffer[0];
                        byte relY = buffer[1];

                        x += relX;
                        y += relY;
                    }
                    else // Absolute mode
                    {
                        byte runLength = buffer[1];
                        int bytesToRead = MathHelpers.Align((runLength + 1) / 2, 2);
                        stream.ReadExactly(buffer2.Slice(0, bytesToRead));
                        readBytes += bytesToRead;

                        for (int i = 0; i < runLength; i++, x++)
                        {
                            int val = (buffer2[i / 2] << i % 2 * 4) & 0xf0;
                            row[x / 2] |= (byte)(val >> x % 2 * 4);
                        }
                    }
                }
            }

            return pixelData;
        }

        private static byte[] ReadCompressedPixelData(Stream stream, in BmpDibHeader dibHeader)
        {
            int stride = (dibHeader.Header.Width * dibHeader.Header.BitCount + 31) / 32 * 4; // align stride to 4 bytes

            byte[] pixelData;
            if (dibHeader.Header.Compression is BmpCompressionMethod.Rle8 or BmpCompressionMethod.CmykRle8)
            {
                pixelData = DecompressRle8(stream, (int)dibHeader.Header.SizeImage, stride, dibHeader.Header.Height);
            }
            else // if (dibHeader.Header.Compression is BmpCompressionMethod.Rle4 or BmpCompressionMethod.CmykRle4)
            {
                pixelData = DecompressRle4(stream, (int)dibHeader.Header.SizeImage, stride, dibHeader.Header.Width, dibHeader.Header.Height);
            }

            return pixelData;
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
            if (dibHeader.Type is BmpDibHeaderType.BitmapInfoHeader && dibHeader.Header.Compression is BmpCompressionMethod.BitFields)
            {
                // Read extra bit masks
                throw new NotImplementedException();
            }

            Rgb32[] colorTable = new Rgb32[Math.Min(1 << dibHeader.Header.BitCount, dibHeader.Header.ClrUsed)];
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
            else if (dibHeader.Header.Compression is BmpCompressionMethod.Rgb or BmpCompressionMethod.BitFields or BmpCompressionMethod.Cmyk)
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
