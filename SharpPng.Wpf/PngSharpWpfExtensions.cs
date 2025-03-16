using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SharpPng.Wpf
{
    public static class PngSharpWpfExtensions
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
                if (info.Transparency?.TransparentColorRgb is (ushort R, ushort G, ushort B) alphaColor)
                {
                    throw new NotImplementedException();
                    byte[] newImageBytes;
                    switch (info.BitDepth)
                    {
                        case 8:
                            format = PixelFormats.Bgra32;
                            newImageBytes = new byte[info.Width * info.Height * PixelFormats.Bgra32.BitsPerPixel / 8];
                            for (int i = 0; i < imageBytes.Length; i += 3)
                            {

                            }
                            break;
                        case 16:
                            format = PixelFormats.Rgba64;
                            newImageBytes = new byte[info.Width * info.Height * PixelFormats.Rgba64.BitsPerPixel / 8];
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
                        format = PixelFormats.Bgra32;
                        for (int i = 0; i < imageBytes.Length; i += 4)
                        {
                            (imageBytes[i], imageBytes[i + 2]) = (imageBytes[i + 2], imageBytes[i]);
                        }
                        break;
                    case 16:
                        format = PixelFormats.Rgba64;
                        break;
                    default: throw new Exception($"Invalid bit depth for {info.Format} format. BitDepth={info.BitDepth}");
                }
            }
            else
            {
                throw new Exception($"Invalid pixel format: Format={info.Format}");
            }
            
            double dpiX = 96, dpiY = 96; // TODO: detect suggested dpi in image metadata
            int stride = info.Width * format.BitsPerPixel / 8;
            BitmapSource bitmap = BitmapSource.Create(info.Width, info.Height, dpiX, dpiY, format, palette, imageBytes, stride);
            return bitmap;
        }
    }
}
