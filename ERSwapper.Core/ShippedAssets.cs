namespace ERSwapper.Core;

public static class ShippedAssets
{
    public static string ShippedThumbnailsDirectory => Path.Combine(AppPaths.SeedConfigDirectory, "Thumbnails");

    public static string ShippedOffsetCacheFile => Path.Combine(AppPaths.SeedConfigDirectory, "offset_cache.json");

    public static async Task EnsureTexconvAsync(
        IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        if (TexconvProvider.Locate() is not null) return;

        await TexconvProvider.EnsureAsync(progress, ct).ConfigureAwait(false);
    }

    public static int SeedUserData()
    {
        SeedOffsetCache();
        return 0;
    }

    private static void SeedOffsetCache()
    {
        try
        {
            if (!File.Exists(ShippedOffsetCacheFile)) return;

            if (!File.Exists(AppPaths.OffsetCacheFile))
            {
                File.Copy(ShippedOffsetCacheFile, AppPaths.OffsetCacheFile);
                return;
            }

            var shipped = ReadCache(ShippedOffsetCacheFile);
            if (shipped.Count == 0) return;

            var user = ReadCache(AppPaths.OffsetCacheFile);
            bool changed = false;

            foreach (KeyValuePair<string, OffsetCacheEntry> entry in shipped)
            {
                if (user.ContainsKey(entry.Key)) continue;

                user[entry.Key] = entry.Value;
                changed = true;
            }

            if (!changed) return;

            string json = System.Text.Json.JsonSerializer.Serialize(
                user, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(AppPaths.OffsetCacheFile, json);
        }
        catch
        {
        }
    }

    private static Dictionary<string, OffsetCacheEntry> ReadCache(string path)
    {
        try
        {
            string json = File.ReadAllText(path);

            return System.Text.Json.JsonSerializer
                       .Deserialize<Dictionary<string, OffsetCacheEntry>>(json)
                   ?? new Dictionary<string, OffsetCacheEntry>();
        }
        catch
        {
            return new Dictionary<string, OffsetCacheEntry>();
        }
    }
}
