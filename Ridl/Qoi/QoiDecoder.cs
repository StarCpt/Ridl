using Ridl.PixelFormats;
using System.Runtime.InteropServices;

namespace Ridl.Qoi
{
    /// <summary>
    /// <see href="https://qoiformat.org/qoi-specification.pdf"/>
    /// </summary>
    public class QoiDecoder
    {
        public static QoiDecoder Default { get; } = new QoiDecoder();

        private static bool CheckSignature(Stream stream)
        {
            Span<byte> sig = [113, 111, 105, 102]; // ASCII "qoif"

            Span<byte> buffer = stackalloc byte[4];
            stream.ReadExactly(buffer);
            return buffer.SequenceEqual(sig);
        }

        private static QoiHeader ReadHeader(Stream stream)
        {
            Span<byte> buffer = stackalloc byte[10];
            stream.ReadExactly(buffer);
            return new QoiHeader(buffer);
        }

        // 8-bit tags
        private const byte QOI_OP_RGB = 0b11111110;
        private const byte QOI_OP_RGBA = 0b11111111;
        // 2-bit tags (highest 2 bits used)
        private const byte QOI_OP_INDEX = 0b00_000000;
        private const byte QOI_OP_DIFF = 0b01_000000;
        private const byte QOI_OP_LUMA = 0b10_000000;
        private const byte QOI_OP_RUN = 0b11_000000;

        private static byte[] DecodeRgba32(Stream stream, in QoiHeader header)
        {
            Span<Rgba32> seenPixels = stackalloc Rgba32[64];
            Span<byte> buffer = stackalloc byte[5];

            byte[] pixelBytes = new byte[header.Width * header.Height * Rgba32.Size];
            Span<Rgba32> pixels = MemoryMarshal.Cast<byte, Rgba32>(pixelBytes.AsSpan());

            //int x = 0, y = 0;
            int pos = 0;
            Rgba32 pixel = new(0, 0, 0, byte.MaxValue);
            while (pos < pixels.Length)
            {
                stream.ReadExactly(buffer[..1]);

                // Check 8-bit tags
                if (buffer[0] is QOI_OP_RGB)
                {
                    stream.ReadExactly(buffer[1..4]);
                    pixel = pixel with
                    {
                        R = buffer[1],
                        G = buffer[2],
                        B = buffer[3],
                    };
                }
                else if (buffer[0] is QOI_OP_RGBA)
                {
                    stream.ReadExactly(buffer[1..5]);
                    pixel = new(buffer[1], buffer[2], buffer[3], buffer[4]);
                }
                else
                {
                    // Check 2-bit tags
                    byte tag2b = (byte)(buffer[0] & 0b11000000);
                    if (tag2b is QOI_OP_INDEX)
                    {
                        int index = buffer[0] & 0b00111111;
                        pixel = seenPixels[index];
                    }
                    else if (tag2b is QOI_OP_DIFF)
                    {
                        byte dr = (byte)(((buffer[0] & 0b00110000) >> 4) - 2);
                        byte dg = (byte)(((buffer[0] & 0b00001100) >> 2) - 2);
                        byte db = (byte)((buffer[0] & 0b00000011) - 2);

                        pixel.R += dr;
                        pixel.G += dg;
                        pixel.B += db;
                    }
                    else if (tag2b is QOI_OP_LUMA)
                    {
                        stream.ReadExactly(buffer[1..2]);
                        int dg = (buffer[0] & 0b00111111) - 32;
                        int dr_dg = ((buffer[1] & 0b11110000) >> 4) - 8;
                        int db_dg = (buffer[1] & 0b00001111) - 8;

                        int dr = dr_dg + dg;
                        int db = db_dg + dg;

                        pixel.R += (byte)dr;
                        pixel.G += (byte)dg;
                        pixel.B += (byte)db;
                    }
                    else // if (tag2b is QOI_OP_RUN)
                    {
                        int runLength = (buffer[0] & 0b00111111) + 1;
                        pixels.Slice(pos, runLength).Fill(pixel);
                        pos += runLength;

                        seenPixels[(pixel.R * 3 + pixel.G * 5 + pixel.B * 7 + pixel.A * 11) % 64] = pixel;
                        continue;
                    }
                }

                pixels[pos++] = pixel;
                seenPixels[(pixel.R * 3 + pixel.G * 5 + pixel.B * 7 + pixel.A * 11) % 64] = pixel;
            }

            return pixelBytes;
        }

        private static byte[] DecodeRgb24(Stream stream, in QoiHeader header)
        {
            Span<Rgb24> seenPixels = stackalloc Rgb24[64];
            Span<byte> buffer = stackalloc byte[5];

            byte[] pixelBytes = new byte[header.Width * header.Height * Rgb24.Size];
            Span<Rgb24> pixels = MemoryMarshal.Cast<byte, Rgb24>(pixelBytes.AsSpan());

            //int x = 0, y = 0;
            int pos = 0;
            Rgb24 pixel = new(0, 0, 0);
            while (pos < pixels.Length)
            {
                stream.ReadExactly(buffer[..1]);

                // Check 8-bit tags
                if (buffer[0] is QOI_OP_RGB)
                {
                    stream.ReadExactly(buffer[1..4]);
                    pixel = new(buffer[1], buffer[2], buffer[3]);
                }
                else if (buffer[0] is QOI_OP_RGBA) // Shouldn't occur in RGB-only images...
                {
                    stream.ReadExactly(buffer[1..5]);
                    pixel = new(buffer[1], buffer[2], buffer[3]);
                }
                else
                {
                    // Check 2-bit tags
                    byte tag2b = (byte)(buffer[0] & 0b11000000);
                    if (tag2b is QOI_OP_INDEX)
                    {
                        int index = buffer[0] & 0b00111111;
                        pixel = seenPixels[index];
                    }
                    else if (tag2b is QOI_OP_DIFF)
                    {
                        byte dr = (byte)(((buffer[0] & 0b00110000) >> 4) - 2);
                        byte dg = (byte)(((buffer[0] & 0b00001100) >> 2) - 2);
                        byte db = (byte)((buffer[0] & 0b00000011) - 2);

                        pixel.R += dr;
                        pixel.G += dg;
                        pixel.B += db;
                    }
                    else if (tag2b is QOI_OP_LUMA)
                    {
                        stream.ReadExactly(buffer[1..2]);
                        int dg = (buffer[0] & 0b00111111) - 32;
                        int dr_dg = ((buffer[1] & 0b11110000) >> 4) - 8;
                        int db_dg = (buffer[1] & 0b00001111) - 8;

                        int dr = dr_dg + dg;
                        int db = db_dg + dg;

                        pixel.R += (byte)dr;
                        pixel.G += (byte)dg;
                        pixel.B += (byte)db;
                    }
                    else // if (tag2b is QOI_OP_RUN)
                    {
                        int runLength = (buffer[0] & 0b00111111) + 1;
                        pixels.Slice(pos, runLength).Fill(pixel);
                        pos += runLength;

                        seenPixels[(pixel.R * 3 + pixel.G * 5 + pixel.B * 7 + (byte.MaxValue * 11)) % 64] = pixel;
                        continue;
                    }
                }

                pixels[pos++] = pixel;
                seenPixels[(pixel.R * 3 + pixel.G * 5 + pixel.B * 7 + (byte.MaxValue * 11)) % 64] = pixel;
            }

            return pixelBytes;
        }

        public QoiImage Decode(Stream stream)
        {
            if (!CheckSignature(stream))
                throw new InvalidDataException("The stream doesn't contain a QOI file signature.");

            QoiHeader header = ReadHeader(stream);

            byte[] pixelData = header.Channels is 3 ? DecodeRgb24(stream, header) : DecodeRgba32(stream, header);

            return new QoiImage(pixelData, header);
        }
    }
}
