using System.Drawing.Imaging;

namespace ERSwapper.Core;

public static class ImageOrientation
{
    public static void FlipVerticalInPlace(string pngPath)
    {
        byte[] bytes = File.ReadAllBytes(pngPath);

        using var input = new MemoryStream(bytes);
        using Image decoded = Image.FromStream(input);
        using var flipped = new Bitmap(decoded);

        flipped.RotateFlip(RotateFlipType.RotateNoneFlipY);
        flipped.Save(pngPath, ImageFormat.Png);
    }

    public static void FlipVerticalTo(string sourcePngPath, string targetPngPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePngPath);

        using var input = new MemoryStream(bytes);
        using Image decoded = Image.FromStream(input);
        using var flipped = new Bitmap(decoded);

        flipped.RotateFlip(RotateFlipType.RotateNoneFlipY);
        flipped.Save(targetPngPath, ImageFormat.Png);
    }
}
