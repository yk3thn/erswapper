using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERSwapper.Core;

public enum SwapState
{
    Applied,
    RevertedHere,
    RevertedElsewhere,
    Superseded,
    BundleReplaced,
    BundleMissing,
}

public class SwapRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;

    public string DisplayName { get; set; } = "";

    public string Category { get; set; } = "";

    public string TextureObjectName { get; set; } = "";

    public string BundleRelativePath { get; set; } = "";

    public string BundlePath { get; set; } = "";

    public long BundleLength { get; set; }

    public long BundleLastWriteUtcTicks { get; set; }

    public long AbsoluteOffset { get; set; }

    public uint RegionSize { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int MipCount { get; set; }

    public string DxgiFormat { get; set; } = "";

    public string OriginalSha256 { get; set; } = "";

    public string SwappedSha256 { get; set; } = "";

    public bool RevertedByUser { get; set; }

    public bool OriginalBytesDiscarded { get; set; }

    [JsonIgnore]
    public string OriginalBytesPath => Path.Combine(SwapHistory.Directory, Id + ".orig");

    [JsonIgnore]
    public string BeforeThumbnailPath => Path.Combine(SwapHistory.Directory, Id + ".before.png");

    [JsonIgnore]
    public string AfterThumbnailPath => Path.Combine(SwapHistory.Directory, Id + ".after.png");

    [JsonIgnore]
    public bool HasOriginalBytes => !OriginalBytesDiscarded && File.Exists(OriginalBytesPath);
}

public record SwapStatus(SwapState State, string Summary, bool CanRevert);

public static class SwapHistory
{
    public static string Directory => Path.Combine(AppPaths.UserDataDirectory, "History");

    public static string HistoryFile => Path.Combine(Directory, "history.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<SwapRecord> Load()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return new List<SwapRecord>();

            return JsonSerializer.Deserialize<List<SwapRecord>>(File.ReadAllText(HistoryFile))
                   ?? new List<SwapRecord>();
        }
        catch
        {
            return new List<SwapRecord>();
        }
    }

    public static void Save(List<SwapRecord> records)
    {
        AppPaths.EnsureDirectory(Directory);
        File.WriteAllText(HistoryFile, JsonSerializer.Serialize(records, Options));
    }

    public static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public static async Task<SwapRecord> RecordAsync(
        ItemPreset preset,
        string bundlePath,
        long absoluteOffset,
        byte[] originalBytes,
        byte[] swappedBytes,
        string? appliedPngPath,
        CancellationToken ct = default)
    {
        var bundle = new FileInfo(bundlePath);

        var record = new SwapRecord
        {
            DisplayName = preset.DisplayName,
            Category = preset.Category,
            TextureObjectName = preset.TextureObjectName,
            BundleRelativePath = preset.BundleRelativePath,
            BundlePath = bundle.FullName,
            BundleLength = bundle.Length,
            BundleLastWriteUtcTicks = bundle.LastWriteTimeUtc.Ticks,
            AbsoluteOffset = absoluteOffset,
            RegionSize = preset.StreamDataSize,
            Width = preset.Width,
            Height = preset.Height,
            MipCount = preset.MipCount,
            DxgiFormat = preset.DxgiFormat,
            OriginalSha256 = Hash(originalBytes),
            SwappedSha256 = Hash(swappedBytes),
        };

        AppPaths.EnsureDirectory(Directory);

        await File.WriteAllBytesAsync(record.OriginalBytesPath, originalBytes, ct).ConfigureAwait(false);

        TryCopyThumbnail(ThumbnailCache.ResolvePath(preset), record.BeforeThumbnailPath);
        TryCopyThumbnail(appliedPngPath, record.AfterThumbnailPath);

        List<SwapRecord> all = Load();
        all.Add(record);
        Save(all);

        return record;
    }

    private static void TryCopyThumbnail(string? sourcePng, string destination)
    {
        if (string.IsNullOrWhiteSpace(sourcePng) || !File.Exists(sourcePng)) return;

        try
        {
            using var source = new MemoryStream(File.ReadAllBytes(sourcePng));
            using Image image = Image.FromStream(source);
            using Bitmap thumbnail = Fit(image, ThumbnailCache.ThumbnailSize);

            thumbnail.Save(destination, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch
        {
        }
    }

    private static Bitmap Fit(Image source, int size)
    {
        var target = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        double scale = Math.Min((double)size / source.Width, (double)size / source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var g = Graphics.FromImage(target);
        g.Clear(Color.Transparent);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, (size - width) / 2, (size - height) / 2, width, height);

        return target;
    }

    public static async Task<SwapStatus> DescribeAsync(SwapRecord record, CancellationToken ct = default)
    {
        if (record.RevertedByUser)
            return new SwapStatus(SwapState.RevertedHere, "Reverted", false);

        if (!File.Exists(record.BundlePath))
            return new SwapStatus(SwapState.BundleMissing, "Bundle is missing", false);

        var bundle = new FileInfo(record.BundlePath);
        bool lengthChanged = bundle.Length != record.BundleLength;

        try
        {
            byte[] live = await BundlePatcher
                .ReadBytesAtAsync(record.BundlePath, record.AbsoluteOffset, (int)record.RegionSize, ct)
                .ConfigureAwait(false);

            string hash = Hash(live);

            if (hash == record.SwappedSha256)
            {
                return new SwapStatus(SwapState.Applied, "Applied", record.HasOriginalBytes);
            }

            if (hash == record.OriginalSha256)
            {
                return new SwapStatus(
                    SwapState.RevertedElsewhere,
                    "No longer applied — the original texture is back",
                    false);
            }

            if (lengthChanged)
            {
                return new SwapStatus(
                    SwapState.BundleReplaced,
                    "No longer applied — the game file was replaced by an update",
                    false);
            }

            return new SwapStatus(
                SwapState.Superseded,
                "Replaced by a newer swap of the same texture",
                false);
        }
        catch (Exception ex)
        {
            return lengthChanged
                ? new SwapStatus(
                    SwapState.BundleReplaced,
                    "No longer applied — the game file was replaced by an update",
                    false)
                : new SwapStatus(SwapState.BundleMissing, "Could not read: " + ex.Message, false);
        }
    }

    public static bool FreesDiskWhenStale(SwapState state) =>
        state is SwapState.RevertedHere
            or SwapState.RevertedElsewhere
            or SwapState.BundleReplaced
            or SwapState.BundleMissing;

    public static async Task RevertAsync(
        SwapRecord record, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        SwapStatus status = await DescribeAsync(record, ct).ConfigureAwait(false);

        if (status.State != SwapState.Applied)
        {
            throw new PatchSafetyException(
                $"This swap cannot be reverted because it is not the texture currently in the game.\r\n\r\n" +
                $"{status.Summary}.\r\n\r\n" +
                "Nothing was written.");
        }

        if (!record.HasOriginalBytes)
        {
            throw new PatchSafetyException(
                "The original texture for this swap is no longer stored, so it cannot be put back.\r\n\r\n" +
                "Use Steam → Verify Integrity of Game Files to restore the original.");
        }

        byte[] original = await File.ReadAllBytesAsync(record.OriginalBytesPath, ct).ConfigureAwait(false);

        if (Hash(original) != record.OriginalSha256)
        {
            throw new PatchSafetyException(
                "The stored original texture is damaged — its checksum does not match what was recorded.\r\n\r\n" +
                "Nothing was written.");
        }

        progress?.Report(new ScanProgress(0.5, $"Putting {record.DisplayName} back…"));

        await BundlePatcher
            .WriteBytesAtAsync(record.BundlePath, record.AbsoluteOffset, original, (int)record.RegionSize, ct)
            .ConfigureAwait(false);

        var bundle = new FileInfo(record.BundlePath);

        List<SwapRecord> all = Load();
        SwapRecord? stored = all.FirstOrDefault(r => r.Id == record.Id);

        if (stored is not null)
        {
            stored.RevertedByUser = true;
            stored.BundleLength = bundle.Length;
            stored.BundleLastWriteUtcTicks = bundle.LastWriteTimeUtc.Ticks;
        }

        record.RevertedByUser = true;

        Save(all);
        DiscardOriginalBytes(record, all);

        progress?.Report(new ScanProgress(1.0, $"{record.DisplayName} restored."));
    }

    public static void DiscardOriginalBytes(SwapRecord record, List<SwapRecord>? all = null)
    {
        try
        {
            if (File.Exists(record.OriginalBytesPath)) File.Delete(record.OriginalBytesPath);
        }
        catch
        {
            return;
        }

        record.OriginalBytesDiscarded = true;

        List<SwapRecord> records = all ?? Load();
        SwapRecord? stored = records.FirstOrDefault(r => r.Id == record.Id);

        if (stored is not null) stored.OriginalBytesDiscarded = true;

        Save(records);
    }

    public static long StoredBytes()
    {
        try
        {
            return new DirectoryInfo(Directory)
                .EnumerateFiles("*.orig")
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    public static void Forget(SwapRecord record)
    {
        foreach (string path in new[]
        {
            record.OriginalBytesPath, record.BeforeThumbnailPath, record.AfterThumbnailPath,
        })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        List<SwapRecord> all = Load();
        all.RemoveAll(r => r.Id == record.Id);
        Save(all);
    }
}
