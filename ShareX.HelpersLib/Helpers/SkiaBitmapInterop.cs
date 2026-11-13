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
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace ShareX.HelpersLib;

public static class SkiaBitmapInterop
{
    public static SKBitmap ToSKBitmap(Image source)
    {
        using Bitmap bitmap = new(source.Width, source.Height, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(source, 0, 0);
        }

        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        SKBitmap result = new(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            using SKPixmap pixels = new(result.Info, data.Scan0, data.Stride);
            if (!pixels.ReadPixels(result.Info, result.GetPixels(), result.RowBytes))
                throw new InvalidOperationException("Unable to read image pixels.");
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static Bitmap ToBitmap(SKBitmap source)
    {
        Bitmap bitmap = new(source.Width, source.Height, PixelFormat.Format32bppPArgb);
        try
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                using SKPixmap pixels = source.PeekPixels();
                SKImageInfo info = new(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                if (!pixels.ReadPixels(info, data.Scan0, data.Stride))
                    throw new InvalidOperationException("Unable to read image pixels.");
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }
}
