﻿using Ridl.Png;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ridl.Wpf
{
    public static class RidlWpfExtensions
    {
        private static BitmapSource CreateBitmapSource(byte[] pixelData, int width, int height, int bitDepth, PngPixelFormat format, PngPaletteColor[]? palette, PngTransparency? transparency, PngPixelDimensions? pixelDimensions)
        {
            PixelFormat wpfFormat;
            BitmapPalette? wpfPalette = null;
            if (format is PngPixelFormat.Grayscale)
            {
                if (transparency?.TransparentColorGrayscale is ushort alphaColor)
                {
                    switch (bitDepth)
                    {
                        case 1:
                            wpfFormat = PixelFormats.Indexed1;
                            wpfPalette = new BitmapPalette(
                            [
                                Color.FromArgb(alphaColor == 0 ? byte.MinValue : byte.MaxValue, 0, 0, 0),
                                Color.FromArgb(alphaColor == 1 ? byte.MinValue : byte.MaxValue, 255, 255, 255),
                            ]);
                            break;
                        case 2:
                            wpfFormat = PixelFormats.Indexed2;
                            Color[] paletteColors = new Color[1 << 2];
                            for (int i = 0; i < paletteColors.Length; i++)
                            {
                                byte value = (byte)(i << 6);
                                paletteColors[i] = Color.FromArgb(alphaColor == i ? byte.MinValue : byte.MaxValue, value, value, value);
                            }
                            wpfPalette = new BitmapPalette(paletteColors);
                            break;
                        case 4:
                            wpfFormat = PixelFormats.Indexed4;
                            paletteColors = new Color[1 << 4];
                            for (int i = 0; i < paletteColors.Length; i++)
                            {
                                byte value = (byte)(i << 4);
                                paletteColors[i] = Color.FromArgb(alphaColor == i ? byte.MinValue : byte.MaxValue, value, value, value);
                            }
                            wpfPalette = new BitmapPalette(paletteColors);
                            break;
                        case 8:
                            wpfFormat = PixelFormats.Indexed8;
                            paletteColors = new Color[1 << 8];
                            for (int i = 0; i < paletteColors.Length; i++)
                            {
                                byte value = (byte)i;
                                paletteColors[i] = Color.FromArgb(alphaColor == i ? byte.MinValue : byte.MaxValue, value, value, value);
                            }
                            wpfPalette = new BitmapPalette(paletteColors);
                            break;
                        case 16:
                            // No indexed16 format, so use rgba64 for 16-bit depth w/alpha
                            wpfFormat = PixelFormats.Rgba64;
                            byte[] newImageBytes = new byte[pixelData.Length * 4];

                            // Work with 16-bit elements since it's a bit more readable
                            Span<ushort> imageSpan16 = MemoryMarshal.Cast<byte, ushort>(pixelData);
                            Span<ushort> newImageSpan16 = MemoryMarshal.Cast<byte, ushort>(newImageBytes);

                            for (int i = 0; i < imageSpan16.Length; i++)
                            {
                                ushort colorGray = imageSpan16[i];

                                newImageSpan16[i * 4 + 0] = colorGray;
                                newImageSpan16[i * 4 + 1] = colorGray;
                                newImageSpan16[i * 4 + 2] = colorGray;
                                newImageSpan16[i * 4 + 3] = colorGray == alphaColor ? ushort.MinValue : ushort.MaxValue;
                            }
                            pixelData = newImageBytes;
                            break;
                        default: throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}");
                    }
                }
                else
                {
                    wpfFormat = bitDepth switch
                    {
                        1 => PixelFormats.BlackWhite,
                        2 => PixelFormats.Gray2,
                        4 => PixelFormats.Gray4,
                        8 => PixelFormats.Gray8,
                        16 => PixelFormats.Gray16,
                        _ => throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}"),
                    };
                }
            }
            else if (format is PngPixelFormat.Rgb)
            {
                if (transparency?.TransparentColorRgb is (ushort alphaR, ushort alphaG, ushort alphaB) alphaColor)
                {
                    switch (bitDepth)
                    {
                        case 8:
                            wpfFormat = PixelFormats.Bgra32;
                            byte[] newImageBytes = new byte[width * height * wpfFormat.BitsPerPixel / 8];
                            byte alphaR8 = (byte)(alphaR & 0xff);
                            byte alphaG8 = (byte)(alphaG & 0xff);
                            byte alphaB8 = (byte)(alphaB & 0xff);
                            for (int i = 0, j = 0; i < pixelData.Length; i += 3, j += 4)
                            {
                                byte r = pixelData[i + 0];
                                byte g = pixelData[i + 1];
                                byte b = pixelData[i + 2];
                                byte alpha = (r == alphaR8 && g == alphaG8 && b == alphaB8) ? byte.MinValue : byte.MaxValue;

                                newImageBytes[j + 0] = b;
                                newImageBytes[j + 1] = g;
                                newImageBytes[j + 2] = r;
                                newImageBytes[j + 3] = alpha;
                            }
                            pixelData = newImageBytes;
                            break;
                        case 16:
                            wpfFormat = PixelFormats.Rgba64;
                            newImageBytes = new byte[width * height * wpfFormat.BitsPerPixel / 8];

                            // Work with 16-bit elements since it's a bit more readable
                            Span<ushort> imageSpan16 = MemoryMarshal.Cast<byte, ushort>(pixelData);
                            Span<ushort> newImageSpan16 = MemoryMarshal.Cast<byte, ushort>(newImageBytes);

                            for (int i = 0, j = 0; i < imageSpan16.Length; i += 3, j += 4)
                            {
                                ushort r = imageSpan16[i + 0];
                                ushort g = imageSpan16[i + 1];
                                ushort b = imageSpan16[i + 2];
                                ushort alpha = (r == alphaR && g == alphaG && b == alphaB) ? ushort.MinValue : ushort.MaxValue;

                                newImageSpan16[j + 0] = r;
                                newImageSpan16[j + 1] = g;
                                newImageSpan16[j + 2] = b;
                                newImageSpan16[j + 3] = alpha;
                            }
                            pixelData = newImageBytes;
                            break;
                        default: throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}");
                    };
                }
                else
                {
                    wpfFormat = bitDepth switch
                    {
                        8 => PixelFormats.Rgb24,
                        16 => PixelFormats.Rgb48,
                        _ => throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}"),
                    };
                    ;
                }
            }
            else if (format is PngPixelFormat.Indexed)
            {
                wpfFormat = bitDepth switch
                {
                    1 => PixelFormats.Indexed1,
                    2 => PixelFormats.Indexed2,
                    4 => PixelFormats.Indexed4,
                    8 => PixelFormats.Indexed8,
                    _ => throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}"),
                };

                if (transparency?.PaletteTransparencyMap is byte[] paletteAlphaMap)
                {
                    wpfPalette = new BitmapPalette(palette!.Select((i, index) => Color.FromArgb(paletteAlphaMap[index], i.R, i.G, i.B)).ToArray());
                }
                else
                {
                    wpfPalette = new BitmapPalette(palette!.Select(static i => Color.FromRgb(i.R, i.G, i.B)).ToArray());
                }
            }
            else if (format is PngPixelFormat.GrayscaleWithAlpha)
            {
                // wpf doesn't have grayscale formats with alpha
                // convert the image to BGRA32 or RGBA64. this kinda sucks because the size increases significantly.
                // may need to find a different solution
                switch (bitDepth)
                {
                    case 8:
                        wpfFormat = PixelFormats.Bgra32;
                        byte[] newImageBytes = new byte[width * height * wpfFormat.BitsPerPixel / 8];
                        for (int i = 0; i < pixelData.Length; i += 2)
                        {
                            byte rgb = pixelData[i + 0];
                            byte a   = pixelData[i + 1];

                            newImageBytes[i * 2 + 0] = rgb;
                            newImageBytes[i * 2 + 1] = rgb;
                            newImageBytes[i * 2 + 2] = rgb;
                            newImageBytes[i * 2 + 3] = a;
                        }
                        pixelData = newImageBytes;
                        break;
                    case 16:
                        wpfFormat = PixelFormats.Rgba64;
                        newImageBytes = new byte[width * height * wpfFormat.BitsPerPixel / 8];

                        // Work with 16-bit elements since it's a bit more readable
                        Span<ushort> imageSpan16 = MemoryMarshal.Cast<byte, ushort>(pixelData);
                        Span<ushort> newImageSpan16 = MemoryMarshal.Cast<byte, ushort>(newImageBytes);

                        for (int i = 0; i < imageSpan16.Length; i += 2)
                        {
                            ushort rgb = imageSpan16[i];
                            ushort a = imageSpan16[i + 1];

                            newImageSpan16[i * 2 + 0] = rgb;
                            newImageSpan16[i * 2 + 1] = rgb;
                            newImageSpan16[i * 2 + 2] = rgb;
                            newImageSpan16[i * 2 + 3] = a;
                        }
                        pixelData = newImageBytes;
                        break;
                    default: throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}");
                }
            }
            else if (format is PngPixelFormat.Rgba)
            {
                switch (bitDepth)
                {
                    case 8:
                        // There is no Rgba32 PixelFormat in wpf so the pixels need to be converted to Bgra32
                        wpfFormat = PixelFormats.Bgra32;
                        for (int i = 0; i < pixelData.Length; i += 4)
                        {
                            (pixelData[i], pixelData[i + 2]) = (pixelData[i + 2], pixelData[i]);
                        }
                        break;
                    case 16: wpfFormat = PixelFormats.Rgba64; break;
                    default: throw new Exception($"Invalid bit depth for {format} format. BitDepth={bitDepth}");
                }
            }
            else
            {
                throw new Exception($"Invalid pixel format: Format={format}");
            }

            double dpiX = 96, dpiY = 96;
            if (pixelDimensions is PngPixelDimensions dims)
            {
                if (dims.Units is PngPixelUnit.Unknown)
                {
                    double aspectRatio = (double)dims.PixelsPerUnitX / dims.PixelsPerUnitY;
                    dpiX *= aspectRatio;
                }
                else if (dims.Units is PngPixelUnit.Meter)
                {
                    dpiX = double.Round(dims.PixelsPerUnitX / 39.3700787402, 1);
                    dpiY = double.Round(dims.PixelsPerUnitY / 39.3700787402, 1);
                }
            }

            int stride = MathHelpers.DivRoundUp(width * wpfFormat.BitsPerPixel, 8);
            BitmapSource bitmap = BitmapSource.Create(width, height, dpiX, dpiY, wpfFormat, wpfPalette, pixelData, stride);
            return bitmap;
        }

        public static BitmapSource DecodeToBitmapSource(this PngDecoder decoder, Stream pngStream)
        {
            byte[] pixelData = decoder.Decode(pngStream, out PngMetadata metadata);
            return CreateBitmapSource(pixelData, metadata.Width, metadata.Height, metadata.BitDepth, metadata.Format, metadata.Palette, metadata.Transparency, metadata.PixelDimensions);
        }

        public static BitmapSource ToBitmapSource(this PngImage image)
        {
            return CreateBitmapSource(image.PixelData, image.Width, image.Height, image.BitDepth, image.Format, image.Palette, image.Transparency, image.PixelDimensions);
        }
    }
}
