namespace ERSwapper.Core;

public class TextureCandidate
{
    public required ParsedTexture Texture { get; init; }

    public required ItemPreset Provisional { get; init; }

    public bool AlreadyInCatalogue { get; set; }

    public bool Queued { get; set; }

    public bool Unsupported => Texture.GetBlocker() is not null;

    public string Blocker => Texture.GetBlocker() ?? "";

    public string Channel
    {
        get
        {
            string name = Texture.Name;

            int underscore = name.LastIndexOf('_');
            if (underscore < 0 || underscore == name.Length - 1) return "";

            return name[(underscore + 1)..].ToLowerInvariant();
        }
    }

    public bool IsBaseColour => Channel is "bc" or "basecolor" or "albedo" or "diff" or "d";

    public string Pixels => $"{Texture.Width}x{Texture.Height}";
}

public static class TextureCatalogueScanner
{
    public static string CachePath => Path.Combine(AppPaths.UserDataDirectory, "texture_index.json");

    public static List<TextureCandidate> Scan(
        string dumpFolder,
        BundleIndex? bundleIndex,
        IEnumerable<ItemPreset> existing,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(dumpFolder)) return new List<TextureCandidate>();

        FileInfo[] files = new DirectoryInfo(dumpFolder)
            .EnumerateFiles()
            .Where(f => DumpLookup.IsDumpFile(f.FullName))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<string, CacheEntry> cache = LoadCache();

        var textures = new List<ParsedTexture>(files.Length);
        var stale = new List<FileInfo>();

        foreach (FileInfo file in files)
        {
            if (cache.TryGetValue(CacheKey(file.FullName), out CacheEntry? entry)
                && entry.Length == file.Length
                && entry.Ticks == file.LastWriteTimeUtc.Ticks)
            {
                textures.Add(entry.Texture);
            }
            else
            {
                stale.Add(file);
            }
        }

        if (stale.Count > 0)
        {
            progress?.Report(new ScanProgress(
                0,
                textures.Count > 0
                    ? $"Reading {stale.Count:N0} new dump{(stale.Count == 1 ? "" : "s")} ({textures.Count:N0} cached)…"
                    : $"Reading {stale.Count:N0} dumps…"));

            List<ParsedTexture> fresh = ParseInParallel(stale, progress, ct);
            textures.AddRange(fresh);

            SaveCache(files, textures);
        }
        else
        {
            progress?.Report(new ScanProgress(0.9, $"Loaded {textures.Count:N0} textures from cache."));
        }

        var known = existing
            .Select(p => p.TextureObjectName + "|" + p.StreamDataOffset)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<TextureCandidate>(textures.Count);

        foreach (ParsedTexture texture in textures)
        {
            if (texture.Width <= 0 || texture.Height <= 0) continue;

            texture.ResolvedBundlePath ??= bundleIndex?.FindBundleForCab(texture.CabId);

            candidates.Add(new TextureCandidate
            {
                Texture = texture,
                Provisional = texture.ToPreset(
                    ItemPreset.FallbackCategory,
                    texture.ResolvedBundlePath ?? "",
                    texture.SignatureName ?? ""),
                AlreadyInCatalogue = known.Contains(texture.Name + "|" + texture.StreamOffset),
            });
        }

        progress?.Report(new ScanProgress(1.0, $"{candidates.Count:N0} textures."));

        return candidates
            .OrderBy(c => c.Texture.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ParsedTexture> ParseInParallel(
        List<FileInfo> files, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var parsed = new System.Collections.Concurrent.ConcurrentBag<ParsedTexture>();
        int done = 0;

        Parallel.ForEach(
            files,
            new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount },
            file =>
            {
                ParsedTexture texture;

                try
                {
                    texture = UabeaDumpParser.ParseMetadata(file.FullName);
                }
                catch
                {
                    texture = new ParsedTexture();
                }

                texture.SourceFile = file.FullName;
                parsed.Add(texture);

                int completed = Interlocked.Increment(ref done);

                if (completed % 250 == 0 && files.Count > 0)
                {
                    progress?.Report(new ScanProgress(
                        (double)completed / files.Count,
                        $"Reading dumps… {completed:N0} of {files.Count:N0}"));
                }
            });

        return parsed.ToList();
    }

    private class CacheEntry
    {
        public long Length { get; set; }

        public long Ticks { get; set; }

        public ParsedTexture Texture { get; set; } = new();
    }

    private static string CacheKey(string path) => path.ToLowerInvariant();

    private static Dictionary<string, CacheEntry> LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return new Dictionary<string, CacheEntry>();

            return System.Text.Json.JsonSerializer
                .Deserialize<Dictionary<string, CacheEntry>>(File.ReadAllText(CachePath))
                ?? new Dictionary<string, CacheEntry>();
        }
        catch
        {
            return new Dictionary<string, CacheEntry>();
        }
    }

    private static void SaveCache(FileInfo[] files, List<ParsedTexture> textures)
    {
        try
        {
            var bySource = new Dictionary<string, ParsedTexture>(StringComparer.OrdinalIgnoreCase);
            foreach (ParsedTexture texture in textures) bySource[texture.SourceFile] = texture;

            var cache = new Dictionary<string, CacheEntry>();

            foreach (FileInfo file in files)
            {
                if (!bySource.TryGetValue(file.FullName, out ParsedTexture? texture)) continue;

                cache[CacheKey(file.FullName)] = new CacheEntry
                {
                    Length = file.Length,
                    Ticks = file.LastWriteTimeUtc.Ticks,
                    Texture = texture,
                };
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, System.Text.Json.JsonSerializer.Serialize(cache));
        }
        catch
        {
        }
    }

    public static void ClearCache()
    {
        try
        {
            if (File.Exists(CachePath)) File.Delete(CachePath);

            string legacy = CachePath + ".fingerprint";
            if (File.Exists(legacy)) File.Delete(legacy);
        }
        catch
        {
        }
    }

    public static List<TextureCandidate> Filter(
        IEnumerable<TextureCandidate> candidates,
        string? nameQuery,
        string? bundlePath,
        bool baseColourOnly,
        bool hideAlreadyAdded,
        bool hideUnsupported)
    {
        IEnumerable<TextureCandidate> result = candidates;

        if (baseColourOnly) result = result.Where(c => c.IsBaseColour);
        if (hideAlreadyAdded) result = result.Where(c => !c.AlreadyInCatalogue && !c.Queued);
        if (hideUnsupported) result = result.Where(c => !c.Unsupported);

        if (!string.IsNullOrWhiteSpace(bundlePath))
        {
            result = result.Where(c =>
                string.Equals(c.Provisional.BundleRelativePath, bundlePath, StringComparison.OrdinalIgnoreCase));
        }

        string[] terms = (nameQuery ?? "").Split(
            ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (terms.Length > 0)
        {
            result = result.Where(c => terms.All(term =>
                c.Texture.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return result.ToList();
    }
}
