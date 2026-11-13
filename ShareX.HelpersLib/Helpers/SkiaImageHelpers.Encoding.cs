#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using SkiaSharp;
using System;
using System.Drawing;
using System.IO;

namespace ShareX.HelpersLib;

public static partial class SkiaImageHelpers
{
    public static bool IsImageTransparent(SKBitmap bitmap)
    {
        using SkiaPixelBuffer pixels = new(bitmap, true, PixelAccess.ReadOnly);
        return pixels.IsTransparent();
    }

    public static MemoryStream SaveJPEG(SKBitmap bitmap, int quality,
        SKJpegEncoderDownsample subsampling = SKJpegEncoderDownsample.Downsample420)
    {
        MemoryStream stream = new();
        SaveJPEG(bitmap, stream, quality, subsampling);
        return stream;
    }

    public static IndexedImage Quantize(SKBitmap bitmap, GIFQuality quality = GIFQuality.Adaptive)
    {
        if (quality == GIFQuality.Adaptive)
        {
            return new AdaptiveGifQuantizer().Quantize(bitmap);
        }

        SkiaQuantizer quantizer = quality switch
        {
            GIFQuality.Grayscale => new SkiaGrayscaleQuantizer(),
            GIFQuality.Bit4 => new SkiaOctreeQuantizer(15, 4),
            _ => new SkiaOctreeQuantizer(255, 4)
        };
        return quantizer.Quantize(bitmap);
    }

    public static MemoryStream SaveGIF(SKBitmap bitmap, GIFQuality quality)
    {
        MemoryStream stream = new();
        SaveGIF(bitmap, stream, quality);
        return stream;
    }

    public static void SaveGIF(SKBitmap bitmap, Stream stream, GIFQuality quality)
        => Quantize(bitmap, quality).SaveGif(stream);

    public static SKBitmap FillBackground(SKBitmap source, Color color)
    {
        SKBitmap output = CreateBitmap(source.Width, source.Height);
        using SKCanvas canvas = new(output);
        canvas.Clear(new SKColor(color.R, color.G, color.B, color.A));
        canvas.DrawBitmap(source, 0, 0);
        return output;
    }

    public static void SaveBMP(SKBitmap bitmap, Stream stream, BMPBitDepth depth = BMPBitDepth.Bit24)
    {
        bool alpha = depth == BMPBitDepth.Bit32 || depth == BMPBitDepth.Automatic &&
            bitmap.AlphaType != SKAlphaType.Opaque && IsImageTransparent(bitmap);
        int bytesPerPixel = alpha ? 4 : 3;
        int stride = checked((checked(bitmap.Width * bytesPerPixel) + 3) / 4 * 4);
        int headerSize = alpha ? 108 : 40;
        int pixelOffset = 14 + headerSize;
        int pixelSize = checked(stride * bitmap.Height);
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
        writer.Write((ushort)0x4D42); writer.Write(checked(pixelOffset + pixelSize));
        writer.Write(0); writer.Write(pixelOffset); writer.Write(headerSize); writer.Write(bitmap.Width); writer.Write(bitmap.Height);
        writer.Write((ushort)1); writer.Write((ushort)(bytesPerPixel * 8)); writer.Write(alpha ? 3 : 0); writer.Write(pixelSize);
        writer.Write(3780); writer.Write(3780); writer.Write(0); writer.Write(0);
        if (alpha)
        {
            writer.Write(0x00FF0000u); writer.Write(0x0000FF00u); writer.Write(0x000000FFu); writer.Write(0xFF000000u);
            writer.Write(0x73524742u);
            writer.Write(new byte[48]);
        }
        using SkiaPixelBuffer pixels = new(bitmap, true, PixelAccess.ReadOnly);
        byte[] row = new byte[stride];
        for (int y = bitmap.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                ColorBgra color = pixels.GetPixel(x, y);
                int offset = x * bytesPerPixel;
                row[offset] = color.Blue; row[offset + 1] = color.Green; row[offset + 2] = color.Red;
                if (alpha) row[offset + 3] = color.Alpha;
            }
            writer.Write(row);
        }
    }

    public static SKJpegEncoderOptions GetJPEGEncoderOptions(int quality, SKJpegEncoderDownsample subsampling)
        => new(Math.Clamp(quality, 0, 100), Enum.IsDefined(subsampling) ? subsampling : SKJpegEncoderDownsample.Downsample420,
            SKJpegEncoderAlphaOption.Ignore);

    public static void SaveJPEG(SKBitmap bitmap, Stream stream, int quality,
        SKJpegEncoderDownsample subsampling = SKJpegEncoderDownsample.Downsample420)
    {
        using SKPixmap pixels = bitmap.PeekPixels();
        if (!pixels.Encode(stream, GetJPEGEncoderOptions(quality, subsampling)))
            throw new InvalidDataException("Image encoding failed: Jpeg.");
    }

    public static void SaveJPEG(SKBitmap bitmap, string path, int quality,
        SKJpegEncoderDownsample subsampling = SKJpegEncoderDownsample.Downsample420)
    {
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        SaveJPEG(bitmap, stream, quality, subsampling);
    }

    public static SKWebpEncoderOptions GetWebPEncoderOptions(int quality, bool lossless, int compressionEffort)
        => lossless
            ? new(SKWebpEncoderCompression.Lossless, Math.Clamp(compressionEffort, 0, 100))
            : new(SKWebpEncoderCompression.Lossy, Math.Clamp(quality, 0, 100));

    public static void SaveWebP(SKBitmap bitmap, Stream stream, SKWebpEncoderOptions options)
    {
        using SKPixmap pixels = bitmap.PeekPixels();
        if (!pixels.Encode(stream, options))
            throw new InvalidDataException("Image encoding failed: Webp.");
    }
    public static MemoryStream SavePNG(SKBitmap bitmap, PNGBitDepth depth)
    {
        MemoryStream stream = new(); SavePNG(bitmap, stream, depth); return stream;
    }
    public static void SavePNG(SKBitmap bitmap, Stream stream, PNGBitDepth depth)
    {
        SavePNG(bitmap, stream, depth, long.MaxValue);
    }

    public static bool SavePNG(SKBitmap bitmap, Stream stream, PNGBitDepth depth, long sizeLimit)
    {
        return SavePNG(bitmap, stream, depth, sizeLimit, SKPngEncoderOptions.Default);
    }

    public static bool SavePNG(SKBitmap bitmap, Stream stream, PNGBitDepth depth, long sizeLimit, SKPngEncoderOptions options)
    {
        using SizeLimitedWStream output = new(stream, sizeLimit);
        bool encoded;

        bool opaqueOutput = depth == PNGBitDepth.Bit24 || depth == PNGBitDepth.Automatic &&
            (bitmap.AlphaType == SKAlphaType.Opaque || !IsImageTransparent(bitmap));
        if (opaqueOutput && (bitmap.AlphaType != SKAlphaType.Opaque ||
            bitmap.ColorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888)))
        {
            using SKBitmap opaque = new(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
            using (SkiaPixelBuffer source = new(bitmap, true, PixelAccess.ReadOnly))
            using (SkiaPixelBuffer target = new(opaque, true, PixelAccess.WriteOnly))
            {
                for (int index = 0; index < source.PixelCount; index++)
                {
                    ColorBgra pixel = source.GetPixel(index); pixel.Alpha = 255; target.SetPixel(index, pixel);
                }
            }
            using SKPixmap pixels = opaque.PeekPixels();
            encoded = pixels.Encode(output, options);
        }
        else if (depth == PNGBitDepth.Bit32)
        {
            using SKBitmap rgba = new(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888,
                SKAlphaType.Unpremul, bitmap.ColorSpace));
            using SKPixmap source = bitmap.PeekPixels();
            if (!source.ReadPixels(rgba.Info, rgba.GetPixels(), rgba.RowBytes))
                throw new InvalidDataException("Unable to read image pixels.");
            using SKPixmap pixels = rgba.PeekPixels();
            encoded = pixels.Encode(output, options);
        }
        else
        {
            using SKPixmap pixels = bitmap.PeekPixels();
            encoded = pixels.Encode(output, options);
        }

        if (!encoded && !output.SizeLimitExceeded) throw new InvalidDataException("Image encoding failed: Png.");
        return encoded;
    }

    private sealed class SizeLimitedWStream : SKManagedWStream
    {
        private readonly long sizeLimit;
        private long bytesWritten;
        public bool SizeLimitExceeded { get; private set; }

        public SizeLimitedWStream(Stream stream, long sizeLimit) : base(stream, false)
        {
            this.sizeLimit = Math.Max(0, sizeLimit);
        }

        protected override bool OnWrite(IntPtr buffer, IntPtr size)
        {
            long count = size.ToInt64();
            if (SizeLimitExceeded || count > sizeLimit - bytesWritten)
            {
                SizeLimitExceeded = true;
                return false;
            }

            if (!base.OnWrite(buffer, size)) return false;
            bytesWritten += count;
            return true;
        }
    }

    public static MemoryStream SaveJPEGAutoQuality(SKBitmap img, int sizeLimit, int qualityDecrement = 5,
        int minQuality = 0, int maxQuality = 100, SKJpegEncoderDownsample subsampling = SKJpegEncoderDownsample.Downsample420)
    {
        qualityDecrement = qualityDecrement.Clamp(1, 100);
        minQuality = minQuality.Clamp(0, 100);
        maxQuality = maxQuality.Clamp(0, 100);

        if (minQuality >= maxQuality)
        {
            return SaveJPEG(img, minQuality, subsampling);
        }

        MemoryStream ms = null;

        for (int quality = maxQuality; quality >= minQuality; quality -= qualityDecrement)
        {
            if (ms != null)
            {
                ms.Dispose();
            }

            ms = SaveJPEG(img, quality, subsampling);

            if (ms.Length <= sizeLimit)
            {
                break;
            }
        }

        return ms;
    }
}
