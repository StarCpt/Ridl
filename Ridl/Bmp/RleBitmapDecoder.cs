using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl.Bmp
{
    /// <summary>
    /// A class for decoding RLE (Run-Length Encoded) compressed bmp data.
    /// </summary>
    internal static class RleBitmapDecoder
    {
        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/b64d0c0b-bb80-4b53-8382-f38f264eb685"/>
        /// </remarks>
        public static byte[] DecodeRle8(Stream stream, int stride, int height, bool isTopDown)
        {
            byte[] pixelData = new byte[stride * height];
            Span<byte> buffer = stackalloc byte[2];
            int x = 0, y = isTopDown ? 0 : (height - 1);
            Span<byte> scanline = pixelData.AsSpan(stride * y, stride);
            while (true)
            {
                stream.ReadExactly(buffer);

                if (buffer[0] > 0) // Encoded mode
                {
                    byte runLength = buffer[0];
                    byte value = buffer[1];
                    scanline.Slice(x, runLength).Fill(value);
                    x += runLength;
                }
                else
                {
                    if (buffer[1] == 0) // End of line
                    {
                        x = 0;
                        y = isTopDown ? (y + 1) : (y - 1);
                        scanline = pixelData.AsSpan(stride * y, stride);
                        continue;
                    }
                    else if (buffer[1] == 1) // End of bitmap
                    {
                        break; // Exit while loop
                    }
                    else if (buffer[1] == 2) // Delta (Relative) mode
                    {
                        stream.ReadExactly(buffer);

                        byte relX = buffer[0];
                        byte relY = buffer[1];

                        x += relX;
                        y = isTopDown ? (y + relY) : (y - relY);
                    }
                    else // Absolute mode
                    {
                        byte runLength = buffer[1];
                        stream.ReadExactly(scanline.Slice(x, runLength));
                        x += runLength;

                        int padding = runLength % 2; // Each run is padded to a 2-byte boundary
                        stream.ReadDiscard(padding);
                    }
                }
            }

            return pixelData;
        }

        /// <remarks>
        /// <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wmf/73b57f24-6d78-4eeb-9c06-8f892d88f1ab"/>
        /// </remarks>
        public static byte[] DecodeRle4(Stream stream, int stride, int height, bool isTopDown)
        {
            byte[] pixelData = new byte[stride * height];
            Span<byte> buffer = stackalloc byte[2];
            Span<byte> buffer2 = stackalloc byte[256];
            int x = 0, y = isTopDown ? 0 : (height - 1);
            Span<byte> scanline = pixelData.AsSpan(stride * y, stride);
            while (true)
            {
                stream.ReadExactly(buffer);

                if (buffer[0] > 0) // Encoded mode
                {
                    byte runLength = buffer[0];
                    byte value = buffer[1];

                    for (int i = 0; i < runLength; i++, x++)
                    {
                        int val = (value << i % 2 * 4) & 0xf0;
                        scanline[x / 2] |= (byte)(val >> x % 2 * 4);
                    }
                }
                else
                {
                    if (buffer[1] == 0) // End of line
                    {
                        x = 0;
                        y = isTopDown ? (y + 1) : (y - 1);
                        scanline = pixelData.AsSpan(stride * y, stride);
                        continue;
                    }
                    else if (buffer[1] == 1) // End of bitmap
                    {
                        break; // Exit loop
                    }
                    else if (buffer[1] == 2) // Delta (Relative) mode
                    {
                        stream.ReadExactly(buffer);

                        byte relX = buffer[0];
                        byte relY = buffer[1];

                        x += relX;
                        y = isTopDown ? (y + relY) : (y - relY);
                    }
                    else // Absolute mode
                    {
                        byte runLength = buffer[1];
                        int bytesToRead = MathHelpers.Align((runLength + 1) / 2, 2);
                        stream.ReadExactly(buffer2.Slice(0, bytesToRead));

                        for (int i = 0; i < runLength; i++, x++)
                        {
                            int val = (buffer2[i / 2] << i % 2 * 4) & 0xf0;
                            scanline[x / 2] |= (byte)(val >> x % 2 * 4);
                        }
                    }
                }
            }

            return pixelData;
        }

        // RLE24 compression details: https://www.fileformat.info/format/os2bmp/egff.htm
        public static byte[] DecodeRle24(Stream stream, int stride, int height, bool isTopDown)
        {
            byte[] pixelData = new byte[stride * height];
            Span<byte> buffer = stackalloc byte[4];
            int x = 0, y = isTopDown ? 0 : (height - 1);
            Span<byte> scanline = pixelData.AsSpan(stride * y, stride);
            while (true)
            {
                stream.ReadExactly(buffer[..2]);

                if (buffer[0] > 0) // Encoded mode
                {
                    byte runLength = buffer[0];
                    stream.ReadExactly(buffer[2..4]);

                    for (int i = 0; i < runLength; i++, x++)
                    {
                        scanline[x * 3] = buffer[1];
                        scanline[x * 3 + 1] = buffer[2];
                        scanline[x * 3 + 2] = buffer[3];
                    }
                }
                else
                {
                    if (buffer[1] == 0) // End of line
                    {
                        x = 0;
                        y = isTopDown ? (y + 1) : (y - 1);
                        scanline = pixelData.AsSpan(stride * y, stride);
                        continue;
                    }
                    else if (buffer[1] == 1) // End of bitmap
                    {
                        break; // Exit while loop
                    }
                    else if (buffer[1] == 2) // Delta (Relative) mode
                    {
                        stream.ReadExactly(buffer);

                        byte relX = buffer[0];
                        byte relY = buffer[1];

                        x += relX;
                        y = isTopDown ? (y + relY) : (y - relY);
                    }
                    else // Absolute mode
                    {
                        byte runLength = buffer[1];
                        stream.ReadExactly(scanline.Slice(x * 3, runLength * 3));
                        x += runLength;

                        int padding = (runLength * 3) % 2; // Each run is padded to a 2-byte boundary
                        stream.ReadDiscard(padding);
                    }
                }
            }

            return pixelData;
        }
    }
}
