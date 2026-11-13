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
using System.IO;

namespace ShareX.HelpersLib;

public static partial class SkiaImageHelpers
{
    public static SKBitmap CreateBitmap(int width, int height)
    {
        SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        return bitmap;
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
