using SharpPng.Reconstruction;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace SharpPng
{
    public class PngDecoder
    {
        private readonly struct ChunkType
        {
            // Critical Types
            public static readonly ChunkType Header = new("IHDR");
            public static readonly ChunkType Trailer = new("IEND");
            public static readonly ChunkType Palette = new("PLTE");
            public static readonly ChunkType ImageData = new("IDAT");

            // Optional Types
            public static readonly ChunkType Transparency = new("tRNS");
            public static readonly ChunkType PixelDimensions = new("pHYs");
            public static readonly ChunkType SuggestedPalette = new("sPLT");
            public static readonly ChunkType Timestamp = new("tIME");

            public readonly string Name => new([ (char)b0, (char)b1, (char)b2, (char)b3 ]);
            public readonly bool IsAncillary => (b0 & 0x20) != 0;
            public readonly bool IsPrivate => (b1 & 0x20) != 0;
            public readonly bool IsReserved => (b2 & 0x20) != 0;
            public readonly bool IsSafeToCopy => (b3 & 0x20) != 0;

            public readonly byte b0, b1, b2, b3;

            private ChunkType(string name)
            {
                b0 = Convert.ToByte(name[0]);
                b1 = Convert.ToByte(name[1]);
                b2 = Convert.ToByte(name[2]);
                b3 = Convert.ToByte(name[3]);
            }

            public ChunkType(ReadOnlySpan<byte> data)
            {
                b0 = data[0];
                b1 = data[1];
                b2 = data[2];
                b3 = data[3];
            }

            public override string ToString()
            {
                return Name;
            }

            public static bool operator ==(ChunkType x, ChunkType y) => x.b0 == y.b0 && x.b1 == y.b1 && x.b2 == y.b2 && x.b3 == y.b3;
            public static bool operator !=(ChunkType x, ChunkType y) => x.b0 != y.b0 || x.b1 != y.b1 || x.b2 != y.b2 || x.b3 != y.b3;
            public override bool Equals([NotNullWhen(true)] object? obj) => obj is ChunkType chunkType && this == chunkType;
            public override int GetHashCode() => (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
        }

        private enum FilterType : byte
        {
            None = 0,
            Sub = 1,
            Up = 2,
            Average = 3,
            Paeth = 4,
        }

        public static PngDecoder Default { get; } = new PngDecoder(false);

        private readonly bool _checkCrc;

        public PngDecoder(bool checkCrc)
        {
            _checkCrc = checkCrc;

            if (checkCrc)
                throw new NotImplementedException();
        }

        private static bool CheckSignature(ReadOnlySpan<byte> bytes)
        {
            // 137 80 78 71 13 10 26 10 in hex:
            ReadOnlySpan<byte> validSignature =
            [
                0x89,
                0x50, 0x4e, 0x47,
                0x0d, 0x0a,
                0x1a,
                0x0a,
            ];

            return bytes.SequenceEqual(validSignature);
        }

        private static PngMetadata ReadHeaderChunk(Stream pngStream)
        {
            Span<byte> buffer = stackalloc byte[4];

            pngStream.ReadExactly(buffer);
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);

            pngStream.ReadExactly(buffer);
            ChunkType type = new(buffer);

            Span<byte> headerData = stackalloc byte[length];
            pngStream.ReadExactly(headerData);

            pngStream.ReadExactly(buffer);
            uint crc = BinaryPrimitives.ReadUInt32BigEndian(buffer);

            return new PngMetadata
            {
                Width = (int)BinaryPrimitives.ReadUInt32BigEndian(headerData[..4]),
                Height = (int)BinaryPrimitives.ReadUInt32BigEndian(headerData[4..8]),
                BitDepth = headerData![8],
                Format = (PngPixelFormat)headerData[9],
                Compression = (PngCompressionMethod)headerData[10],
                Filter = headerData[11],
                Interlaced = headerData[12] != 0,
            };
        }

#if DEBUG
        private static int none = 0, sub = 0, up = 0, average = 0, paeth = 0;
#endif

        private static void DecodeScanline(IReconstructor recon, FilterType filterType, int y, Span<byte> scanline, ReadOnlySpan<byte> prevScanline)
        {
            if (filterType is FilterType.None)
            {
#if DEBUG
                none++;
#endif
                return;
            }
            else if (filterType is FilterType.Sub)
            {
#if DEBUG
                sub++;
#endif
                recon.FilterSub(scanline);
            }
            else if (filterType is FilterType.Up)
            {
#if DEBUG
                up++;
#endif
                if (y is 0)
                    return;
                recon.FilterUp(scanline, prevScanline);
            }
            else if (filterType is FilterType.Average)
            {
#if DEBUG
                average++;
#endif
                if (y is 0)
                    recon.FilterAvg_Scan0(scanline);
                else
                    recon.FilterAvg(scanline, prevScanline);
            }
            else if (filterType is FilterType.Paeth)
            {
#if DEBUG
                paeth++;
#endif
                if (y is 0)
                    recon.FilterPaeth_Scan0(scanline);
                else
                    recon.FilterPaeth(scanline, prevScanline);
            }
        }

        private static byte[] DecodeImageData(ZLibStream decompressor, in PngMetadata info)
        {
            int imageStride = info.Width * info.BitsPerPixel / 8;
            byte[] decodedImageData = new byte[imageStride * info.Height];

            IReconstructor recon = info.BitsPerPixel switch
            {
                24 => new Reconstruct24(info.Width),
                32 => new Reconstruct32(info.Width),
                _ => new ReconstructGeneric(info.Width, info.BitsPerPixel),
            };

            for (int y = 0; y < info.Height; y++)
            {
                FilterType filterType = (FilterType)decompressor.ReadByte();
                ReadOnlySpan<byte> prevScanline = y != 0 ? decodedImageData.AsSpan((y - 1) * imageStride, imageStride) : default;
                Span<byte> scanline = decodedImageData.AsSpan(y * imageStride, imageStride);
                decompressor.ReadExactly(scanline);

                DecodeScanline(recon, filterType, y, scanline, prevScanline);
            }

            return decodedImageData;
        }

        private static (int width, int height) GetReducedImageSize(int pass, int imageWidth, int imageHeight)
        {
            byte[,] pixelOffsets =
            {
                { 0, 0 },
                { 4, 0 },
                { 0, 4 },
                { 2, 0 },
                { 0, 2 },
                { 1, 0 },
                { 0, 1 },
            };
            byte[,] pixelIntervals =
            {
                { 8, 8 },
                { 8, 8 },
                { 4, 8 },
                { 4, 4 },
                { 2, 4 },
                { 2, 2 },
                { 1, 2 },
            };

            int width = (imageWidth + pixelIntervals[pass, 0] - pixelOffsets[pass, 0] - 1) / pixelIntervals[pass, 0];
            int height = (imageHeight + pixelIntervals[pass, 1] - pixelOffsets[pass, 1] - 1) / pixelIntervals[pass, 1];
            return (width, height);
        }

        private static byte[] DecodeInterlacedImageData(ZLibStream decompressor, in PngMetadata info)
        {
            // TODO?: Support loading reduced images before the entire image is decoded

            byte[,] pixelOffsets =
            {
                { 0, 0 },
                { 4, 0 },
                { 0, 4 },
                { 2, 0 },
                { 0, 2 },
                { 1, 0 },
                { 0, 1 },
            };
            byte[,] pixelIntervals =
            {
                { 8, 8 },
                { 8, 8 },
                { 4, 8 },
                { 4, 4 },
                { 2, 4 },
                { 2, 2 },
                { 1, 2 },
            };

            int imageStride = (int)float.Ceiling(info.Width * info.BitsPerPixel / 8f);
            byte[] decodedImageData = new byte[imageStride * info.Height];

            for (int pass = 0; pass < 7; pass++)
            {
                (int passWidth, int passHeight) = GetReducedImageSize(pass, info.Width, info.Height);

                if (passWidth == 0 || passHeight == 0)
                    continue;

                int passStride = (int)float.Ceiling(passWidth * info.BitsPerPixel / 8f);

                IReconstructor recon = info.BitsPerPixel switch
                {
                    24 => new Reconstruct24(passWidth),
                    32 => new Reconstruct32(passWidth),
                    _ => new ReconstructGeneric(passWidth, info.BitsPerPixel),
                };

                byte[] prevScanline = new byte[passStride];
                byte[] scanline = new byte[passStride];
                for (int y = 0; y < passHeight; y++)
                {
                    FilterType filterType = (FilterType)decompressor.ReadByte();
                    decompressor.ReadExactly(scanline);

                    DecodeScanline(recon, filterType, y, scanline, prevScanline);

                    Span<byte> targetScanline = decodedImageData.AsSpan((y * pixelIntervals[pass, 1] + pixelOffsets[pass, 1]) * imageStride, imageStride);
                    if (info.BitsPerPixel >= 8)
                    {
                        int bytesPerPixel = info.BitsPerPixel / 8;
                        for (int x = 0; x < passWidth; x++)
                        {
                            Span<byte> sourcePixel = scanline.AsSpan(x * bytesPerPixel, bytesPerPixel);
                            Span<byte> targetPixel = targetScanline.Slice((x * pixelIntervals[pass, 0] + pixelOffsets[pass, 0]) * bytesPerPixel, bytesPerPixel);

                            sourcePixel.CopyTo(targetPixel);
                        }
                    }
                    else
                    {
                        // Stride might not be aligned to byte
                        // in that case the last few bits of the scanline aren't used
                        if (info.BitsPerPixel == 1)
                        {
                            for (int x = 0, targetPixelBitPos = pixelOffsets[pass, 0]; x < passWidth; x++, targetPixelBitPos += pixelIntervals[pass, 0])
                            {
                                int val = (scanline[x / 8] << (x % 8)) & 0b_10000000;
                                targetScanline[targetPixelBitPos / 8] |= (byte)(val >> (targetPixelBitPos % 8));
                            }
                        }
                        else if (info.BitsPerPixel == 2)
                        {
                            // (2bpp)   0b_00000000
                            // Pixel 1:    ..
                            // Pixel 2:      ..
                            // Pixel 3:        ..
                            // Pixel 4:          ..
                            for (int x = 0, targetPixelBitPos = pixelOffsets[pass, 0]; x < passWidth; x++, targetPixelBitPos += pixelIntervals[pass, 0])
                            {
                                int val = (scanline[x / 4] << (x % 4 * 2)) & 0b_11000000;
                                targetScanline[targetPixelBitPos / 4] |= (byte)(val >> (targetPixelBitPos % 4 * 2));
                            }
                        }
                        else if (info.BitsPerPixel == 4)
                        {
                            for (int x = 0, targetPixelBitPos = pixelOffsets[pass, 0]; x < passWidth; x++, targetPixelBitPos += pixelIntervals[pass, 0])
                            {
                                int val = (scanline[x / 2] << (x % 2 * 4)) & 0b_11110000;
                                targetScanline[targetPixelBitPos / 2] |= (byte)(val >> (targetPixelBitPos % 2 * 4));
                            }
                        }
                        else
                        {
                            throw new Exception("Invalid bit depth.");
                        }
                    }

                    scanline.CopyTo(prevScanline.AsSpan());
                }
            }

            return decodedImageData;
        }

        private static byte[] DecodeImageDataChunks(Stream pngStream, in PngMetadata info, bool canBaseStreamSeek)
        {
            // pngStream position should be at the start of the first IDAT chunk

            Span<byte> buffer = stackalloc byte[4]; // buffer for reading 32-bit values (length, chunkType, crc)

            // TODO: create wrapper stream around pngstream that only reads the IDAT chunk data to avoid unnecessary memory allocations
            using MemoryStream compressedImageDataStream = new(canBaseStreamSeek ? (int)(pngStream.Length - pngStream.Position) : 0);

            while (true)
            {
                pngStream.ReadExactly(buffer);
                int chunkDataLength = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);

                pngStream.ReadExactly(buffer);
                ChunkType chunkType = new(buffer);

                if (chunkType != ChunkType.ImageData)
                {
                    pngStream.Position -= 8; // rewind the stream to the beginning of the chunk
                    break;
                }

                compressedImageDataStream.SetLength(compressedImageDataStream.Length + chunkDataLength);
                byte[] backingBuffer = compressedImageDataStream.GetBuffer();
                pngStream.ReadExactly(backingBuffer, (int)compressedImageDataStream.Position, chunkDataLength);
                compressedImageDataStream.Position += chunkDataLength;

                pngStream.ReadExactly(buffer);
                uint crc = BinaryPrimitives.ReadUInt32BigEndian(buffer);
            }

            compressedImageDataStream.Position = 0;
            using ZLibStream decompressor = new(compressedImageDataStream, CompressionMode.Decompress);
            byte[] decodedImageData = info.Interlaced switch
            {
                false => DecodeImageData(decompressor, info),
                true => DecodeInterlacedImageData(decompressor, info),
            };
            return decodedImageData;
        }

        private static void DecodeChunks(Stream pngStream, in PngMetadata info, [NotNull] out byte[]? imageData, out PngColor[]? palette, out PngTransparency? transparency)
        {
            imageData = null;
            palette = null;
            transparency = null;
            Span<byte> buffer = stackalloc byte[4];

            bool canBaseStreamSeek = pngStream.CanSeek;

            if (!pngStream.CanSeek)
                pngStream = new ReadSeekableStream(pngStream, 8);

            bool endOfImage = false;
            while (!endOfImage)
            {
                pngStream.ReadExactly(buffer);
                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);

                pngStream.ReadExactly(buffer);
                ChunkType type = new(buffer);

                if (type == ChunkType.ImageData)
                {
                    if (imageData != null)
                        throw new Exception("IDAT chunks must be contiguous.");

                    pngStream.Position -= 8; // rewind to the start of the chunk
                    imageData = DecodeImageDataChunks(pngStream, info, canBaseStreamSeek);

                    continue;
                }

                if (type == ChunkType.Palette)
                {
                    if (length % 3 != 0)
                        throw new Exception("Invalid PLTE chunk length; Length must be divisible by 3.");

                    palette = new PngColor[length / 3];
                    Span<byte> flattenedPalette = MemoryMarshal.AsBytes(palette.AsSpan());
                    pngStream.ReadExactly(flattenedPalette);
                }
                else if (type == ChunkType.Trailer)
                {
                    endOfImage = true;

                    if (length != 0)
                        throw new Exception("Trailer chunk length must be 0.");
                }
                else
                {
                    byte[] dataArray = ArrayPool<byte>.Shared.Rent(length);
                    Span<byte> data = new(dataArray, 0, length);
                    pngStream.ReadExactly(data);

                    if (type == ChunkType.Transparency && info.Format is not PngPixelFormat.GrayscaleWithAlpha and not PngPixelFormat.Rgba)
                    {
                        // https://www.w3.org/TR/2003/REC-PNG-20031110/#11tRNS
                        switch (info.Format)
                        {
                            case PngPixelFormat.Grayscale:
                                ushort alphaColor = BinaryPrimitives.ReadUInt16BigEndian(data);
                                transparency = new PngTransparency
                                {
                                    TransparentColorGrayscale = alphaColor,
                                };
                                break;
                            case PngPixelFormat.Rgb:
                                ushort alphaRed   = BinaryPrimitives.ReadUInt16BigEndian(data);
                                ushort alphaGreen = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
                                ushort alphaBlue  = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
                                transparency = new PngTransparency
                                {
                                    TransparentColorRgb = (alphaRed, alphaGreen, alphaBlue),
                                };
                                break;
                            case PngPixelFormat.Indexed:
                                transparency = new PngTransparency
                                {
                                    PaletteTransparencyMap = data.ToArray(),
                                };
                                break;
                        }
                    }

                    if (!type.IsAncillary)
                        throw new NotImplementedException();

                    ArrayPool<byte>.Shared.Return(dataArray);
                }

                pngStream.ReadExactly(buffer);
                uint crc = BinaryPrimitives.ReadUInt32BigEndian(buffer);
            }

            if (info.Format == PngPixelFormat.Indexed && palette == null)
                throw new Exception($"Missing PLTE chunk; Palette is required for {PngPixelFormat.Indexed} pixel format.");

            if (imageData == null)
                throw new Exception($"{nameof(pngStream)} doesn't contain IDAT chunks.");
        }

        public byte[] Decode(Stream pngStream, out PngMetadata info)
        {
            Span<byte> header = stackalloc byte[8];
            pngStream.ReadExactly(header);

            if (!CheckSignature(header))
                throw new Exception($"Provided {nameof(pngStream)} does not contain a valid signature.");

            info = ReadHeaderChunk(pngStream);

            if (info.Filter != 0)
                throw new InvalidDataException("Invalid filter type.");

            if (!BitConverter.IsLittleEndian)
                throw new NotImplementedException("Big endian systems are not currently supported.");

            // Non byte-aligned stride is unsupported (for now)
            if ((info.Width * info.BitsPerPixel) % 8 != 0)
            {
                // PNG spec 7.2 (Scanlines) line 5:
                // When there are multiple pixels per byte, some low-order bits of the last byte of a scanline may go unused. The contents of these unused bits are not specified.
                throw new NotImplementedException(); // TODO
            }

            DecodeChunks(pngStream, info, out byte[] imgData, out var palette, out var transparency);
            info = info with { Palette = palette, Transparency = transparency };

            return imgData;
        }
    }
}
