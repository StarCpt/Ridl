using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ridl.Wpf
{
    public static class RidlWpfExtensions
    {
        public static BitmapSource DecodeToBitmapSource(this PngDecoder decoder, Stream pngStream)
        {
            byte[] imageBytes = decoder.Decode(pngStream, out PngMetadata info);

            PixelFormat format;
            BitmapPalette? palette = null;
            if (info.Format is PngPixelFormat.Grayscale)
            {
                if (info.Transparency?.TransparentColorGrayscale is ushort alphaColor)
                {
                    switch (info.BitDepth)
                    {
                        case 1:
                            format = PixelFormats.Indexed1;
                            palette = new BitmapPalette(
                            [
                                Color.FromArgb(alphaColor == 0 ? byte.MinValue : byte.MaxValue, 0, 0, 0),
                                Color.FromArgb(alphaColor == 1 ? byte.MinValue : byte.MaxValue, 255, 255, 255),
                            ]);
                            break;
                        case 2:
                            format = PixelFormats.Indexed2;
                            Color[] paletteColors = new Color[1 << 2];
                            for (int i = 0; i < paletteColors.Length; i++)
                            {
                                byte value = (byte)(i << 6);
                                paletteColors[i] = Color.FromArgb(alphaColor == i ? byte.MinValue : byte.MaxValue, value, value, value);
                            }
                            palette = new BitmapPalette(paletteColors);
                            break;
                        case 4:
                            format = PixelFormats.Indexed4;
                            paletteColors = new Color[1 << 4];
                            for (int i = 0; i < paletteColors.Length; i++)
                            {
                                byte value = (byte)(i << 4);
                                paletteColors[i] = Color.FromArgb(alphaColor == i ? byte.MinValue : byte.MaxValue, value, value, value);
                            }
                            palette = new BitmapPalette(paletteColors);
                            break;
                        case 8:
                            format = PixelFormats.Indexed8;
                            paletteColors = new Color[1 << 8];
                            for (int i = 0; i < paletteColors.Length; i++)
                            {
                                byte value = (byte)i;
                                paletteColors[i] = Color.FromArgb(alphaColor == i ? byte.MinValue : byte.MaxValue, value, value, value);
                            }
                            palette = new BitmapPalette(paletteColors);
                            break;
                        case 16:
                            // No indexed16 format, so use rgba64 for 16-bit depth w/alpha
                            format = PixelFormats.Rgba64;
                            byte[] newImageBytes = new byte[imageBytes.Length * 4];

                            // Work with 16-bit elements since it's a bit more readable
                            Span<ushort> imageSpan16 = MemoryMarshal.Cast<byte, ushort>(imageBytes);
                            Span<ushort> newImageSpan16 = MemoryMarshal.Cast<byte, ushort>(newImageBytes);

                            for (int i = 0; i < imageSpan16.Length; i++)
                            {
                                ushort colorGray = imageSpan16[i];

                                newImageSpan16[i * 4 + 0] = colorGray;
                                newImageSpan16[i * 4 + 1] = colorGray;
                                newImageSpan16[i * 4 + 2] = colorGray;
                                newImageSpan16[i * 4 + 3] = colorGray == alphaColor ? ushort.MinValue : ushort.MaxValue;
                            }
                            imageBytes = newImageBytes;
                            break;
                        default: throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}");
                    }
                }
                else
                {
                    format = info.BitDepth switch
                    {
                        1 => PixelFormats.BlackWhite,
                        2 => PixelFormats.Gray2,
                        4 => PixelFormats.Gray4,
                        8 => PixelFormats.Gray8,
                        16 => PixelFormats.Gray16,
                        _ => throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}"),
                    };
                }
            }
            else if (info.Format is PngPixelFormat.Rgb)
            {
                if (info.Transparency?.TransparentColorRgb is (ushort alphaR, ushort alphaG, ushort alphaB) alphaColor)
                {
                    switch (info.BitDepth)
                    {
                        case 8:
                            format = PixelFormats.Bgra32;
                            byte[] newImageBytes = new byte[info.Width * info.Height * format.BitsPerPixel / 8];
                            byte alphaR8 = (byte)(alphaR & 0xff);
                            byte alphaG8 = (byte)(alphaG & 0xff);
                            byte alphaB8 = (byte)(alphaB & 0xff);
                            for (int i = 0, j = 0; i < imageBytes.Length; i += 3, j += 4)
                            {
                                byte r = imageBytes[i + 0];
                                byte g = imageBytes[i + 1];
                                byte b = imageBytes[i + 2];
                                byte alpha = (r == alphaR8 && g == alphaG8 && b == alphaB8) ? byte.MinValue : byte.MaxValue;

                                newImageBytes[j + 0] = b;
                                newImageBytes[j + 1] = g;
                                newImageBytes[j + 2] = r;
                                newImageBytes[j + 3] = alpha;
                            }
                            imageBytes = newImageBytes;
                            break;
                        case 16:
                            format = PixelFormats.Rgba64;
                            newImageBytes = new byte[info.Width * info.Height * format.BitsPerPixel / 8];

                            // Work with 16-bit elements since it's a bit more readable
                            Span<ushort> imageSpan16 = MemoryMarshal.Cast<byte, ushort>(imageBytes);
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
                            imageBytes = newImageBytes;
                            break;
                        default: throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}");
                    };
                }
                else
                {
                    format = info.BitDepth switch
                    {
                        8 => PixelFormats.Rgb24,
                        16 => PixelFormats.Rgb48,
                        _ => throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}"),
                    };
                    ;
                }
            }
            else if (info.Format is PngPixelFormat.Indexed)
            {
                format = info.BitDepth switch
                {
                    1 => PixelFormats.Indexed1,
                    2 => PixelFormats.Indexed2,
                    4 => PixelFormats.Indexed4,
                    8 => PixelFormats.Indexed8,
                    _ => throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}"),
                };

                if (info.Transparency?.PaletteTransparencyMap is byte[] paletteAlphaMap)
                {
                    palette = new BitmapPalette(info.Palette!.Select((i, index) => Color.FromArgb(paletteAlphaMap[index], i.R, i.G, i.B)).ToArray());
                }
                else
                {
                    palette = new BitmapPalette(info.Palette!.Select(static i => Color.FromRgb(i.R, i.G, i.B)).ToArray());
                }
            }
            else if (info.Format is PngPixelFormat.GrayscaleWithAlpha)
            {
                // wpf doesn't have grayscale formats with alpha
                // convert the image to BGRA32 or RGBA64. this kinda sucks because the size increases significantly.
                // may need to find a different solution
                switch (info.BitDepth)
                {
                    case 8:
                        format = PixelFormats.Bgra32;
                        byte[] newImageBytes = new byte[info.Width * info.Height * format.BitsPerPixel / 8];
                        for (int i = 0; i < imageBytes.Length; i += 2)
                        {
                            byte rgb = imageBytes[i + 0];
                            byte a   = imageBytes[i + 1];

                            newImageBytes[i * 2 + 0] = rgb;
                            newImageBytes[i * 2 + 1] = rgb;
                            newImageBytes[i * 2 + 2] = rgb;
                            newImageBytes[i * 2 + 3] = a;
                        }
                        imageBytes = newImageBytes;
                        break;
                    case 16:
                        format = PixelFormats.Rgba64;
                        newImageBytes = new byte[info.Width * info.Height * format.BitsPerPixel / 8];

                        // Work with 16-bit elements since it's a bit more readable
                        Span<ushort> imageSpan16 = MemoryMarshal.Cast<byte, ushort>(imageBytes);
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
                        imageBytes = newImageBytes;
                        break;
                    default: throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}");
                }
            }
            else if (info.Format is PngPixelFormat.Rgba)
            {
                switch (info.BitDepth)
                {
                    case 8:
                        // There is no Rgba32 PixelFormat in wpf so the pixels need to be converted to Bgra32
                        format = PixelFormats.Bgra32;
                        for (int i = 0; i < imageBytes.Length; i += 4)
                        {
                            (imageBytes[i], imageBytes[i + 2]) = (imageBytes[i + 2], imageBytes[i]);
                        }
                        break;
                    case 16: format = PixelFormats.Rgba64; break;
                    default: throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}");
                }
            }
            else
            {
                throw new Exception($"Invalid pixel format: Format={info.Format}");
            }

            double dpiX = 96, dpiY = 96;
            if (info.PixelDimensions is PngPixelDimensions dims)
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

            int stride = MathHelpers.DivRoundUp(info.Width * format.BitsPerPixel, 8);
            BitmapSource bitmap = BitmapSource.Create(info.Width, info.Height, dpiX, dpiY, format, palette, imageBytes, stride);
            return bitmap;
        }
    }
}
