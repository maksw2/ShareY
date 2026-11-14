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
using System.Drawing.Drawing2D;
using System.IO;

namespace ShareX.HelpersLib;

public enum ImageFileFormat { Png, Jpeg, Gif, Bmp, Webp }

public static partial class SkiaImageHelpers
{
    public static void Save(this SKBitmap bitmap, Stream stream, SKEncodedImageFormat format, int quality = 100)
    {
        if (format == SKEncodedImageFormat.Gif) { SaveGIF(bitmap, stream, GIFQuality.Adaptive); return; }
        if (format == SKEncodedImageFormat.Bmp) { SaveBMP(bitmap, stream); return; }
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, Math.Clamp(quality, 0, 100));
        if (data == null) throw new InvalidDataException($"Image encoding failed: {format}.");
        data.SaveTo(stream);
    }

    public static void Save(this SKBitmap bitmap, Stream stream, ImageFileFormat format, int quality = 100)
    {
        bitmap.Save(stream, format switch
        {
            ImageFileFormat.Jpeg => SKEncodedImageFormat.Jpeg,
            ImageFileFormat.Gif => SKEncodedImageFormat.Gif,
            ImageFileFormat.Bmp => SKEncodedImageFormat.Bmp,
            ImageFileFormat.Webp => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Png
        }, quality);
    }

    public static void Save(this SKBitmap bitmap, string filePath, ImageFileFormat format, int quality = 100)
    {
        using FileStream stream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        bitmap.Save(stream, format, quality);
    }

    public static ImageFileFormat GetImageFormat(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" => ImageFileFormat.Jpeg,
        ".gif" => ImageFileFormat.Gif,
        ".bmp" => ImageFileFormat.Bmp,
        ".webp" => ImageFileFormat.Webp,
        _ => ImageFileFormat.Png
    };

    public static SKBitmap CreateBitmap(int width, int height)
    {
        SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return bitmap;
    }

    public static SKBitmap CreateThumbnail(SKBitmap source, int width, int height,
        InterpolationMode interpolationMode = InterpolationMode.HighQualityBicubic)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        float scale = Math.Max(width / (float)source.Width, height / (float)source.Height);
        float sourceWidth = width / scale, sourceHeight = height / scale;
        SKRect crop = SKRect.Create((source.Width - sourceWidth) / 2, (source.Height - sourceHeight) / 2, sourceWidth, sourceHeight);
        SKBitmap result = CreateBitmap(width, height);
        SKSamplingOptions sampling = interpolationMode switch
        {
            InterpolationMode.NearestNeighbor => new(SKFilterMode.Nearest),
            InterpolationMode.Bilinear or InterpolationMode.HighQualityBilinear => new(SKFilterMode.Linear),
            _ => new(SKCubicResampler.Mitchell)
        };
        using SKCanvas canvas = new(result);
        using SKImage image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, crop, SKRect.Create(width, height), sampling);
        return result;
    }

    public static SKBitmap Decode(Stream stream, bool applyOrientation = true)
    {
        if (!stream.CanSeek)
        {
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            copy.Position = 0;
            return Decode(copy, applyOrientation);
        }
        using SKManagedStream managedStream = new(stream, false);
        using SKCodec codec = SKCodec.Create(managedStream);
        if (codec == null) throw new InvalidDataException("The image format is not supported.");

        SKBitmap bitmap = CreateBitmap(codec.Info.Width, codec.Info.Height);
        SKCodecResult result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            throw new InvalidDataException($"Image decoding failed: {result}.");
        }

        if (!applyOrientation || codec.EncodedOrigin == SKEncodedOrigin.TopLeft) return bitmap;

        bool swapDimensions = (int)codec.EncodedOrigin >= (int)SKEncodedOrigin.LeftTop;
        SKBitmap oriented = CreateBitmap(swapDimensions ? bitmap.Height : bitmap.Width,
            swapDimensions ? bitmap.Width : bitmap.Height);
        using (bitmap)
        using (SKCanvas canvas = new(oriented))
        {
            SKMatrix transform = codec.EncodedOrigin switch
            {
                SKEncodedOrigin.TopRight => new(-1, 0, bitmap.Width, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new(-1, 0, bitmap.Width, 0, -1, bitmap.Height, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new(1, 0, 0, 0, -1, bitmap.Height, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new(0, -1, bitmap.Height, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new(0, -1, bitmap.Height, -1, 0, bitmap.Width, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new(0, 1, 0, -1, 0, bitmap.Width, 0, 0, 1),
                _ => SKMatrix.Identity
            };
            canvas.SetMatrix(transform);
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        return oriented;
    }

}
