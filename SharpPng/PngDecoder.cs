﻿using SharpPng.Reconstruction;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

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

        public static int debug1 = 0;

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

        private static PngInfo ReadHeaderChunk(Stream pngStream)
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

            return new PngInfo
            {
                Width = (int)BinaryPrimitives.ReadUInt32BigEndian(headerData[..4]),
                Height = (int)BinaryPrimitives.ReadUInt32BigEndian(headerData[4..8]),
                BitDepth = headerData![8],
                Format = (PngPixelFormat)headerData[9],
                Compression = (PngCompressionMethod)headerData[10],
                Filter = headerData[11],
                Interlace = headerData[12] != 0,
            };
        }

        private static byte[] DecodeImageData(ZLibStream decompressor, in PngInfo info)
        {
            int none = 0, sub = 0, up = 0, average = 0, paeth = 0;
            int imageStride = info.Width * (info.BitsPerPixel / 8);
            byte[] decodedImageData = new byte[imageStride * info.Height];

            IReconstructor recon = info.BitsPerPixel switch
            {
                24 => new Reconstruct24(info.Width),
                32 => new Reconstruct32(info.Width),
                _ => new ReconstructGeneric(info.Width, info.BitsPerPixel / 8),
            };

            for (int y = 0; y < info.Height; y++)
            {
                FilterType filterType = (FilterType)decompressor.ReadByte();
                ReadOnlySpan<byte> prevScanline = y != 0 ? decodedImageData.AsSpan((y - 1) * imageStride, imageStride) : default;
                Span<byte> scanline = decodedImageData.AsSpan(y * imageStride, imageStride);
                decompressor.ReadExactly(scanline);

                if (filterType is FilterType.None)
                {
                    none++;
                    continue;
                }
                else if (filterType is FilterType.Sub)
                {
                    sub++;
                    recon.FilterSub(scanline);
                }
                else if (filterType is FilterType.Up)
                {
                    up++;
                    if (y is 0)
                        continue;
                    recon.FilterUp(scanline, prevScanline);
                }
                else if (filterType is FilterType.Average)
                {
                    average++;
                    if (y is 0)
                        recon.FilterAvg_Scan0(scanline);
                    else
                        recon.FilterAvg(scanline, prevScanline);
                }
                else if (filterType is FilterType.Paeth)
                {
                    paeth++;
                    if (y is 0)
                        recon.FilterPaeth_Scan0(scanline);
                    else
                        recon.FilterPaeth(scanline, prevScanline);
                }
            }

            return decodedImageData;
        }

        private static byte[] DecodeImageDataChunks(Stream pngStream, in PngInfo info, bool canBaseStreamSeek)
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
            byte[] decodedImageData = DecodeImageData(decompressor, info);
            return decodedImageData;
        }

        private static void DecodeChunks(Stream pngStream, in PngInfo info, [NotNull] out byte[]? imageData)
        {
            imageData = null;
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
                    byte[] data = new byte[length];
                    pngStream.ReadExactly(data);

                    // TODO: support
                }
                else if (type == ChunkType.Trailer)
                {
                    endOfImage = true;

                    if (length != 0)
                        throw new Exception("Trailer chunk length must be 0.");
                }
                else
                {
                    byte[] data = new byte[length];
                    pngStream.ReadExactly(data);

                    if (!type.IsAncillary)
                        throw new NotImplementedException();
                }

                pngStream.ReadExactly(buffer);
                uint crc = BinaryPrimitives.ReadUInt32BigEndian(buffer);
            }

            if (imageData == null)
                throw new Exception($"{nameof(pngStream)} doesn't contain IDAT chunks.");
        }

        public byte[] Decode(Stream pngStream, out PngInfo info)
        {
            Span<byte> header = stackalloc byte[8];
            pngStream.ReadExactly(header);

            if (!CheckSignature(header))
                throw new Exception($"Provided {nameof(pngStream)} does not contain a valid signature.");

            info = ReadHeaderChunk(pngStream);

            if (info.Format == PngPixelFormat.Indexed)
                throw new NotImplementedException(); // TODO

            if (info.Interlace)
                throw new NotImplementedException(); // TODO

            if (info.Filter != 0)
                throw new NotSupportedException("Invalid filter type.");

            DecodeChunks(pngStream, info, out byte[] imgData);

            return imgData;
        }
    }
}
