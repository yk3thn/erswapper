using System.Text.Json;

namespace ERSwapper.Core;

public class SignatureSearchException : Exception
{
    public SignatureSearchException(string message) : base(message) { }
}

public class ResSOffsetLocator
{
    public const int MinimumSignatureLength = 512;

    public const int SignatureLength = 4096;

    public const string BundleDirectorySource = "(bundle directory)";

    private const int ChunkSize = 16 * 1024 * 1024;

    private readonly string _cacheFilePath;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SignatureResolution> _resolved = new();

    public ResSOffsetLocator(string? cacheFilePath = null)
        => _cacheFilePath = cacheFilePath ?? AppPaths.OffsetCacheFile;

    public void ForgetResolved() => _resolved.Clear();

    private SignatureResolution Remember(string key, SignatureResolution resolution)
    {
        _resolved[key] = resolution;
        return resolution;
    }

    private static string ResolutionKey(FileInfo bundle, IReadOnlyList<string> signaturePaths)
        => string.Join('|',
            bundle.FullName.ToLowerInvariant(),
            bundle.Length,
            bundle.LastWriteTimeUtc.Ticks,
            string.Join('|', signaturePaths.Select(p => p.ToLowerInvariant())));

    public async Task<long> FindEntryStartAsync(
    string bundlePath,
    string signaturePath,
    IProgress<ScanProgress>? progress = null,
    CancellationToken ct = default)
    {
        if (!File.Exists(signaturePath))
            throw new FileNotFoundException(
                $"Signature file not found:\r\n{signaturePath}\r\n\r\n" +
                "Use Settings → Add bundle signature to import one exported from UABEA.",
                signaturePath);

        SignatureResolution result = await FindEntryStartAsync(
            bundlePath, new[] { signaturePath }, progress, ct).ConfigureAwait(false);

        return result.EntryStart;
    }

    public async Task<SignatureResolution> FindEntryStartAsync(
    string bundlePath,
    IReadOnlyList<string> candidateSignaturePaths,
    IProgress<ScanProgress>? progress = null,
    CancellationToken ct = default)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException($"Bundle not found:\r\n{bundlePath}", bundlePath);

        var bundleFile = new FileInfo(bundlePath);
        string resolutionKey = ResolutionKey(bundleFile, candidateSignaturePaths);

        if (_resolved.TryGetValue(resolutionKey, out SignatureResolution memo))
        {
            progress?.Report(new ScanProgress(1.0, "Using the .resS offset found earlier this session."));
            return memo with { FromCache = true };
        }

        long? fromDirectory = UnityBundleReader.TryFindResSOffset(bundlePath);

        if (fromDirectory is long structuralOffset)
        {
            progress?.Report(new ScanProgress(
                1.0, $"Found .resS start at offset {structuralOffset:N0} (from the bundle directory)."));

            return Remember(resolutionKey,
                new SignatureResolution(structuralOffset, BundleDirectorySource, FromCache: false));
        }

        if (candidateSignaturePaths.Count == 0)
        {
            throw new SignatureSearchException(
                "No bundle signatures have been imported yet.\r\n\r\n" +
                "Export the .resS for this bundle from UABEA, then use " +
                "Settings → Add bundle signature to import it.");
        }

        var usablePaths = new List<string>();
        var usableBytes = new List<byte[]>();
        var skipped = new List<string>();

        foreach (string path in candidateSignaturePaths)
        {
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                ValidateSignature(bytes, path);

                usablePaths.Add(path);
                usableBytes.Add(bytes);
            }
            catch (Exception ex) when (ex is SignatureSearchException or IOException or UnauthorizedAccessException)
            {
                skipped.Add($"  • {Path.GetFileName(path)} — {FirstLine(ex.Message)}");
            }
        }

        if (usablePaths.Count == 0)
        {
            throw new SignatureSearchException(
                "None of the imported signatures are usable:\r\n\r\n" + string.Join("\r\n", skipped));
        }

        var bundleInfo = new FileInfo(bundlePath);
        var cache = await LoadCacheAsync(ct).ConfigureAwait(false);

        for (int i = 0; i < usablePaths.Count; i++)
        {
            string key = MakeKey(bundlePath, usablePaths[i]);

            if (!cache.TryGetValue(key, out OffsetCacheEntry? entry)) continue;

            if (await VerifyAtOffsetAsync(bundlePath, entry.EntryStart, usableBytes[i], ct).ConfigureAwait(false))
            {
                progress?.Report(new ScanProgress(1.0, "Using cached .resS offset."));
                return Remember(resolutionKey,
                    new SignatureResolution(entry.EntryStart, usablePaths[i], FromCache: true));
            }
        }

        SignatureMatch match = await ScanAsync(bundlePath, usableBytes, progress, ct).ConfigureAwait(false);

        string matchedPath = usablePaths[match.SignatureIndex];

        cache[MakeKey(bundlePath, matchedPath)] = new OffsetCacheEntry
        {
            BundlePath = bundlePath,
            SignaturePath = matchedPath,
            BundleLength = bundleInfo.Length,
            BundleLastWriteUtcTicks = bundleInfo.LastWriteTimeUtc.Ticks,
            EntryStart = match.Offset,
        };

        await SaveCacheAsync(cache, ct).ConfigureAwait(false);
        return Remember(resolutionKey, new SignatureResolution(match.Offset, matchedPath, FromCache: false));
    }

    private static string FirstLine(string text)
    {
        int newline = text.IndexOf('\r');
        if (newline < 0) newline = text.IndexOf('\n');
        return newline < 0 ? text : text[..newline];
    }

    public static async Task<long> ScanAsync(
    string bundlePath,
    byte[] signature,
    IProgress<ScanProgress>? progress = null,
    CancellationToken ct = default,
    int chunkSize = ChunkSize)
    {
        SignatureMatch match = await ScanAsync(
            bundlePath, new[] { signature }, progress, ct, chunkSize).ConfigureAwait(false);

        return match.Offset;
    }

    public static async Task<SignatureMatch> ScanAsync(
    string bundlePath,
    IReadOnlyList<byte[]> signatures,
    IProgress<ScanProgress>? progress = null,
    CancellationToken ct = default,
    int chunkSize = ChunkSize)
    {
        if (signatures.Count == 0)
            throw new ArgumentException("No signatures supplied.", nameof(signatures));
        if (signatures.Any(s => s.Length == 0))
            throw new ArgumentException("A signature is empty.", nameof(signatures));
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        int maxSigLen = signatures.Max(s => s.Length);
        int overlap = maxSigLen - 1;

        var matches = new Dictionary<int, HashSet<long>>();

        await using var fs = new FileStream(
            bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);

        long fileLength = fs.Length;
        byte[] buffer = new byte[overlap + chunkSize];

        long bufferStartAbs = 0;
        int carried = 0;
        long totalRead = 0;
        int reportCounter = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            int read = await fs.ReadAsync(buffer.AsMemory(carried, chunkSize), ct).ConfigureAwait(false);
            if (read == 0) break;

            totalRead += read;
            int valid = carried + read;

            for (int si = 0; si < signatures.Count; si++)
            {
                byte[] signature = signatures[si];
                int searchFrom = 0;

                while (searchFrom + signature.Length <= valid)
                {
                    int idx = buffer.AsSpan(searchFrom, valid - searchFrom).IndexOf(signature);
                    if (idx < 0) break;

                    if (!matches.TryGetValue(si, out HashSet<long>? offsets))
                    {
                        offsets = new HashSet<long>();
                        matches[si] = offsets;
                    }

                    offsets.Add(bufferStartAbs + searchFrom + idx);
                    searchFrom += idx + 1;
                }
            }

            if (signatures.Count == 1
                && matches.TryGetValue(0, out HashSet<long>? only)
                && only.Count >= 2)
            {
                break;
            }

            carried = Math.Min(overlap, valid);
            Array.Copy(buffer, valid - carried, buffer, 0, carried);
            bufferStartAbs += valid - carried;

            if (++reportCounter % 4 == 0 && fileLength > 0)
            {
                double fraction = Math.Min(1.0, (double)totalRead / fileLength);
                progress?.Report(new ScanProgress(
                    fraction,
                    $"Scanning bundle for .resS start… {fraction:P0} ({totalRead / (1024 * 1024):N0} MB)"));
            }
        }

        var unique = matches.Where(kv => kv.Value.Count == 1).ToList();
        var ambiguous = matches.Where(kv => kv.Value.Count > 1).ToList();

        if (unique.Count == 1)
        {
            long offset = unique[0].Value.Single();
            progress?.Report(new ScanProgress(1.0, $"Found .resS start at offset {offset:N0}."));
            return new SignatureMatch(unique[0].Key, offset);
        }

        if (unique.Count > 1)
        {
            throw new SignatureSearchException(
                $"{unique.Count} different signatures each match this bundle at a single offset, " +
                "so the correct .resS start cannot be identified.\r\n\r\n" +
                "Remove the signatures that belong to other bundles from the Signatures folder " +
                "(Settings → Open data folder) and try again.");
        }

        if (ambiguous.Count > 0)
        {
            List<long> offsets = ambiguous[0].Value.OrderBy(o => o).Take(2).ToList();
            throw new SignatureSearchException(
                $"A signature was found at more than one offset ({offsets[0]:N0} and {offsets[1]:N0}).\r\n\r\n" +
                "It is not unique enough to identify the .resS start, so the operation was stopped " +
                "rather than risk writing to the wrong location. Re-export the signature using a " +
                "larger slice of the .resS blob.");
        }

        throw new SignatureSearchException(
            (signatures.Count == 1
                ? "The signature was not found anywhere in this bundle.\r\n\r\n"
                : $"None of the {signatures.Count} imported signatures were found in this bundle.\r\n\r\n") +
            "Likely causes:\r\n" +
            "  • The signature was exported from a different bundle.\r\n" +
            "  • The bundle has already been modified over the signature region.\r\n" +
            "  • A game update changed the bundle — re-export the signature from UABEA.");
    }

    private static void ValidateSignature(byte[] signature, string signaturePath)
    {
        if (signature.Length < MinimumSignatureLength)
        {
            throw new SignatureSearchException(
                $"Signature file is only {signature.Length} bytes — at least {MinimumSignatureLength} " +
                $"are required for a trustworthy match.\r\n\r\n{signaturePath}");
        }

        bool allSame = true;
        for (int i = 1; i < signature.Length; i++)
        {
            if (signature[i] != signature[0]) { allSame = false; break; }
        }

        if (allSame)
        {
            throw new SignatureSearchException(
                $"Signature file contains only the repeated byte 0x{signature[0]:X2}, which would match " +
                $"in many places.\r\n\r\n{signaturePath}\r\n\r\n" +
                "Re-export it from a region of the .resS blob that contains actual texture data.");
        }
    }

    private static async Task<bool> VerifyAtOffsetAsync(
        string bundlePath, long offset, byte[] signature, CancellationToken ct)
    {
        try
        {
            await using var fs = new FileStream(
                bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                bufferSize: 4096, FileOptions.Asynchronous);

            if (offset < 0 || offset + signature.Length > fs.Length) return false;

            fs.Seek(offset, SeekOrigin.Begin);
            byte[] actual = new byte[signature.Length];
            await fs.ReadExactlyAsync(actual, ct).ConfigureAwait(false);

            return actual.AsSpan().SequenceEqual(signature);
        }
        catch
        {
            return false;
        }
    }

    private static string MakeKey(string bundlePath, string signaturePath)
    {
        long length;
        try { length = new FileInfo(bundlePath).Length; }
        catch { length = 0; }

        return Path.GetFileName(bundlePath).ToLowerInvariant()
               + "|" + length
               + "|" + Path.GetFileName(signaturePath).ToLowerInvariant();
    }

    private async Task<Dictionary<string, OffsetCacheEntry>> LoadCacheAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_cacheFilePath)) return new Dictionary<string, OffsetCacheEntry>();

            await using var fs = File.OpenRead(_cacheFilePath);
            var loaded = await JsonSerializer
                .DeserializeAsync<Dictionary<string, OffsetCacheEntry>>(fs, cancellationToken: ct)
                .ConfigureAwait(false);

            return loaded ?? new Dictionary<string, OffsetCacheEntry>();
        }
        catch
        {
            return new Dictionary<string, OffsetCacheEntry>();
        }
    }

    private async Task SaveCacheAsync(Dictionary<string, OffsetCacheEntry> cache, CancellationToken ct)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await using var fs = File.Create(_cacheFilePath);
            await JsonSerializer
                .SerializeAsync(fs, cache, new JsonSerializerOptions { WriteIndented = true }, ct)
                .ConfigureAwait(false);
        }
        catch
        {
        }
    }
}

public readonly record struct ScanProgress(double Fraction, string Message);

public readonly record struct SignatureMatch(int SignatureIndex, long Offset);

public readonly record struct SignatureResolution(long EntryStart, string SignaturePath, bool FromCache)
{
    public bool FromBundleDirectory =>
    SignaturePath == ResSOffsetLocator.BundleDirectorySource;
}

public class OffsetCacheEntry
{
    public string BundlePath { get; set; } = "";
    public string SignaturePath { get; set; } = "";
    public long BundleLength { get; set; }
    public long BundleLastWriteUtcTicks { get; set; }
    public long EntryStart { get; set; }
}
