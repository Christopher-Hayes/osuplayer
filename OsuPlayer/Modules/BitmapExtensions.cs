using Avalonia.Media.Imaging;
using SkiaSharp;

namespace OsuPlayer.Modules;

public static class BitmapExtensions
{
    /// <summary>
    /// Maximum width to decode the source image at before blurring.
    /// The blur destroys high-frequency detail anyway, and the result is rendered at low opacity,
    /// so a small decode size dramatically reduces CPU cost with no visible quality loss.
    /// </summary>
    private const int BlurDecodeWidth = 384;

    public static Bitmap BlurBitmap(string imagePath, float blurRadius = 10f, float opacity = 1f, int quality = 80)
    {
        using var stream = File.OpenRead(imagePath);
        using var original = SKBitmap.Decode(stream);
        if (original == null)
            return new Bitmap(imagePath);

        // Downscale before blurring — the blur destroys detail and the result is
        // rendered at low opacity, so a smaller working size saves significant CPU.
        var scale = Math.Min(1f, (float)BlurDecodeWidth / original.Width);
        SKBitmap skBitmap;
        if (scale < 1f)
        {
            var w = (int)(original.Width * scale);
            var h = (int)(original.Height * scale);
            skBitmap = original.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear));
            if (skBitmap == null)
                skBitmap = original; // fallback to full-size
        }
        else
        {
            skBitmap = original;
        }

        // Scale blur radius to match the downscaled image
        var scaledBlurRadius = blurRadius * scale;
        var blurSigma = scaledBlurRadius / 2;
        var blurFilter = SKImageFilter.CreateBlur(scaledBlurRadius, blurSigma);

        var paint = new SKPaint
        {
            ImageFilter = blurFilter,
            Color = new SKColor(alpha: (byte)(255 * opacity), red: 0, green: 0, blue: 0)
        };

        using var surface = SKSurface.Create(new SKImageInfo(skBitmap.Width, skBitmap.Height));
        var canvas = surface.Canvas;

        // Draw the original image with the blur effect
        canvas.DrawBitmap(skBitmap, 0, 0, paint);

        // Dispose the resized copy if we created one
        if (skBitmap != original) skBitmap.Dispose();

        using var image = surface.Snapshot();
        using var outputStream = new MemoryStream();

        image.Encode(SKEncodedImageFormat.Png, quality).SaveTo(outputStream);
        outputStream.Position = 0;

        return new Bitmap(outputStream);
    }
}