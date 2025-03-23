﻿using SharpPng.Reconstruction;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace SharpPng
{
    public class PngDecoder
    {
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
        private readonly Crc32 _crc = new();

        public PngDecoder(bool checkCrc)
        {
            _checkCrc = checkCrc;
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

        private bool CheckCrc(ChunkType chunkType, ReadOnlySpan<byte> chunkData, uint checkCrcValue)
        {
            _crc.Reset();

            Span<byte> chunkTypeBytes = [ chunkType.b0, chunkType.b1, chunkType.b2, chunkType.b3, ];
            _crc.Append(chunkTypeBytes);
            _crc.Append(chunkData);

            uint currentHash = _crc.GetCurrentHashAsUInt32();
            return currentHash == checkCrcValue;
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
                BitDepth = headerData[8],
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
            int imageStride = MathHelpers.DivRoundUp(info.Width * info.BitsPerPixel, 8);
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

            int width = MathHelpers.DivRoundUp(imageWidth - pixelOffsets[pass, 0], pixelIntervals[pass, 0]);
            int height = MathHelpers.DivRoundUp(imageHeight - pixelOffsets[pass, 1], pixelIntervals[pass, 1]);
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

            int imageStride = MathHelpers.DivRoundUp(info.Width * info.BitsPerPixel, 8);
            byte[] decodedImageData = new byte[imageStride * info.Height];

            for (int pass = 0; pass < 7; pass++)
            {
                (int passWidth, int passHeight) = GetReducedImageSize(pass, info.Width, info.Height);

                if (passWidth == 0 || passHeight == 0)
                    continue;

                int passStride = MathHelpers.DivRoundUp(passWidth * info.BitsPerPixel, 8);

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
                            // (2bpp)   0b_00000000 <- least significant bit (0x1)
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

        private static void SwapEndiannessFor16BitDepth(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i += 2)
            {
                (bytes[i], bytes[i + 1]) = (bytes[i + 1], bytes[i]);
            }
        }

        private byte[] DecodeImageDataChunks(Stream pngStream, in PngMetadata info)
        {
            // pngStream position should be at the start of the first IDAT chunk

            using ImageDataReaderStream compressedImageDataStream = new(pngStream, _checkCrc);
            using ZLibStream decompressor = new(compressedImageDataStream, CompressionMode.Decompress);
            byte[] decodedImageData = info.Interlaced switch
            {
                false => DecodeImageData(decompressor, info),
                true => DecodeInterlacedImageData(decompressor, info),
            };

            // when ImageDataReaderStream finishes reading, the position is at the end of the chunkType value of the chunk after the last IDAT chunk
            pngStream.Position -= 8;

            if (info.BitDepth == 16)
            {
                SwapEndiannessFor16BitDepth(decodedImageData);
            }

            return decodedImageData;
        }

        private void DecodeChunks(Stream pngStream, in PngMetadata info, [NotNull] out byte[]? imageData, out PngColor[]? palette, out PngTransparency? transparency, out PngPixelDimensions? pixelDimensions)
        {
            imageData = null;
            palette = null;
            transparency = null;
            pixelDimensions = null;

            Span<byte> buffer = stackalloc byte[4];

            bool canBaseStreamSeek = pngStream.CanSeek;
            if (!canBaseStreamSeek)
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
                    imageData = DecodeImageDataChunks(pngStream, info);

                    continue;
                }

                byte[] dataArray = ArrayPool<byte>.Shared.Rent(length);
                Span<byte> data = new(dataArray, 0, length);
                pngStream.ReadExactly(data);

                if (type == ChunkType.Palette)
                {
                    if (length % 3 != 0)
                        throw new Exception("Invalid PLTE chunk length; Length must be divisible by 3.");

                    palette = new PngColor[length / 3];
                    Span<byte> flattenedPalette = MemoryMarshal.AsBytes(palette.AsSpan());
                    data.CopyTo(flattenedPalette);
                }
                else if (type == ChunkType.Trailer)
                {
                    endOfImage = true;

                    if (length != 0)
                        throw new Exception("Trailer chunk length must be 0.");
                }
                else if (type == ChunkType.Transparency && info.Format is not PngPixelFormat.GrayscaleWithAlpha and not PngPixelFormat.Rgba)
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
                else if (type == ChunkType.PixelDimensions)
                {
                    pixelDimensions = new PngPixelDimensions
                    {
                        PixelsPerUnitX = (int)BinaryPrimitives.ReadUInt32BigEndian(data),
                        PixelsPerUnitY = (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]),
                        Units = (PngPixelUnit)data[8],
                    };
                }
                else if (type == ChunkType.SuggestedPalette)
                {

                }
                else if (!type.IsAncillary && !type.IsPrivate)
                {
                    throw new Exception($"Unknown critical chunk: {type.Name}");
                }

                pngStream.ReadExactly(buffer);
                if (_checkCrc)
                {
                    uint crc = BinaryPrimitives.ReadUInt32BigEndian(buffer);
                    if (!CheckCrc(type, data, crc))
                        throw new Exception("CRC does not match.");
                }

                ArrayPool<byte>.Shared.Return(dataArray);
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

            DecodeChunks(pngStream, info, out byte[] imgData, out var palette, out var transparency, out var pixelDimensions);
            info = info with { Palette = palette, Transparency = transparency, PixelDimensions = pixelDimensions };

            return imgData;
        }
    }
}
