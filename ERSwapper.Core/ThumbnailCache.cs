using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;

namespace ERSwapper.Core;

public static class ThumbnailCache
{
    public const int ThumbnailSize = 128;

    public static string Directory => Path.Combine(AppPaths.UserDataDirectory, "Thumbnails");

    private const int CacheVersion = 2;

    public static string KeyFor(ItemPreset preset)
    {
        string identity = string.Join('|',
            CacheVersion,
            preset.BundleRelativePath.ToLowerInvariant(),
            preset.TextureObjectName.ToLowerInvariant(),
            preset.StreamDataOffset,
            preset.StreamDataSize,
            preset.Width,
            preset.Height,
            preset.MipCount,
            preset.DxgiFormat.ToUpperInvariant(),
            ThumbnailSize);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    public static string ShippedDirectory => Path.Combine(AppPaths.SeedConfigDirectory, "Thumbnails");

    public static string PathFor(ItemPreset preset)
        => Path.Combine(Directory, KeyFor(preset) + ".png");

    public static string? ResolvePath(ItemPreset preset)
    {
        string fileName = KeyFor(preset) + ".png";

        string generated = Path.Combine(Directory, fileName);
        if (File.Exists(generated)) return generated;

        string shipped = Path.Combine(ShippedDirectory, fileName);
        return File.Exists(shipped) ? shipped : null;
    }

    public static bool Exists(ItemPreset preset) => ResolvePath(preset) is not null;

    public static Image? Load(ItemPreset preset)
    {
        string? path = ResolvePath(preset);
        if (path is null) return null;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            using Image decoded = Image.FromStream(ms);
            return new Bitmap(decoded);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveFrom(string sourcePngPath, ItemPreset preset)
    {
        byte[] bytes = File.ReadAllBytes(sourcePngPath);
        using var ms = new MemoryStream(bytes);
        using Image source = Image.FromStream(ms);

        using Bitmap thumbnail = Downscale(source, ThumbnailSize);

        AppPaths.EnsureDirectory(Directory);
        thumbnail.Save(PathFor(preset), ImageFormat.Png);
    }

    private static Bitmap Downscale(Image source, int size)
    {
        var target = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        double scale = Math.Min((double)size / source.Width, (double)size / source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        int x = (size - width) / 2;
        int y = (size - height) / 2;

        using (var g = Graphics.FromImage(target))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(source, x, y, width, height);
        }

        return target;
    }

    public static void Remove(ItemPreset preset)
    {
        try
        {
            string path = PathFor(preset);
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    public static int PruneExcept(IEnumerable<ItemPreset> presets)
    {
        var live = presets.Select(KeyFor).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int deleted = 0;

        try
        {
            foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*.png"))
            {
                if (live.Contains(Path.GetFileNameWithoutExtension(file))) continue;

                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch { }
            }
        }
        catch { }

        return deleted;
    }

    public static int Clear()
    {
        return DeletePngsIn(Directory) + DeletePngsIn(ShippedDirectory);
    }

    private static int DeletePngsIn(string directory)
    {
        int deleted = 0;

        try
        {
            foreach (string file in System.IO.Directory.EnumerateFiles(directory, "*.png"))
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch { }
            }
        }
        catch { }

        return deleted;
    }
}
