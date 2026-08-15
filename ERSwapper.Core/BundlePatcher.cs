namespace ERSwapper.Core;

public class PatchSafetyException : Exception
{
    public PatchSafetyException(string message) : base(message) { }
}

public static class BundlePatcher
{
    private const int CopyBufferSize = 4 * 1024 * 1024;

    private const string BackupSuffix = ".BACKUP";

    private const string OriginSuffix = ".origin";

    public static string LegacyBackupPath(string bundlePath) => bundlePath + BackupSuffix;

    public static string ManagedBackupPath(string bundlePath)
        => Path.Combine(
            AppPaths.BackupDirectory,
            Path.GetFileName(bundlePath).ToLowerInvariant()
            + "." + PathFingerprint(bundlePath) + BackupSuffix);

    public static string GetBackupPath(string bundlePath)
    {
        string legacy = LegacyBackupPath(bundlePath);
        return File.Exists(legacy) ? legacy : ManagedBackupPath(bundlePath);
    }

    public static string OriginPath(string backupPath) => backupPath + OriginSuffix;

    private static string PathFingerprint(string bundlePath)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(bundlePath).ToLowerInvariant()));

        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    public static string GetBundlePathFromBackup(string backupPath)
    {
        try
        {
            string origin = OriginPath(backupPath);

            if (File.Exists(origin))
            {
                string recorded = File.ReadAllText(origin).Trim();
                if (!string.IsNullOrWhiteSpace(recorded)) return recorded;
            }
        }
        catch
        {
        }

        return backupPath.EndsWith(BackupSuffix, StringComparison.OrdinalIgnoreCase)
            ? backupPath[..^BackupSuffix.Length]
            : backupPath;
    }

    public static IReadOnlyList<string> FindBackups(string rustInstallPath)
    {
        var found = new List<string>();

        try
        {
            found.AddRange(Directory.EnumerateFiles(AppPaths.BackupDirectory, "*" + BackupSuffix));
        }
        catch
        {
        }

        if (!string.IsNullOrWhiteSpace(rustInstallPath) && Directory.Exists(rustInstallPath))
        {
            try
            {
                found.AddRange(Directory.EnumerateFiles(
                    rustInstallPath, "*" + BackupSuffix, SearchOption.AllDirectories));
            }
            catch
            {
            }
        }

        return found
            .Where(path => File.Exists(GetBundlePathFromBackup(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task<bool> IsBundleModifiedAsync(
    string bundlePath, string backupPath, CancellationToken ct = default)
    {
        if (!File.Exists(bundlePath) || !File.Exists(backupPath)) return false;

        var live = new FileInfo(bundlePath);
        var backup = new FileInfo(backupPath);
        if (live.Length != backup.Length) return true;

        const int compareBuffer = 1024 * 1024;

        await using var a = new FileStream(bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            compareBuffer, FileOptions.SequentialScan | FileOptions.Asynchronous);
        await using var b = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            compareBuffer, FileOptions.SequentialScan | FileOptions.Asynchronous);

        byte[] bufA = new byte[compareBuffer];
        byte[] bufB = new byte[compareBuffer];

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            int readA = await a.ReadAsync(bufA, ct).ConfigureAwait(false);
            int readB = await b.ReadAsync(bufB, ct).ConfigureAwait(false);

            if (readA != readB) return true;
            if (readA == 0) return false;

            if (!bufA.AsSpan(0, readA).SequenceEqual(bufB.AsSpan(0, readB))) return true;
        }
    }

    public static async Task<bool> BackupIfNeededAsync(
    string bundlePath,
    string backupPath,
    IProgress<ScanProgress>? progress = null,
    CancellationToken ct = default)
    {
        if (File.Exists(backupPath))
        {
            progress?.Report(new ScanProgress(1.0, "Backup already exists — keeping the original."));
            return false;
        }

        var source = new FileInfo(bundlePath);
        if (!source.Exists)
            throw new FileNotFoundException($"Bundle not found:\r\n{bundlePath}", bundlePath);

        string? backupDir = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(backupDir)) Directory.CreateDirectory(backupDir);

        EnsureFreeSpace(backupPath, source.Length);

        string tempPath = backupPath + ".partial";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        try
        {
            await using (var src = new FileStream(
                             bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                             CopyBufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous))
            await using (var dst = new FileStream(
                             tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                             CopyBufferSize, FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[CopyBufferSize];
                long copied = 0;
                long total = src.Length;
                int reportCounter = 0;

                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    int read = await src.ReadAsync(buffer, ct).ConfigureAwait(false);
                    if (read == 0) break;

                    await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    copied += read;

                    if (++reportCounter % 4 == 0 && total > 0)
                    {
                        double fraction = (double)copied / total;
                        progress?.Report(new ScanProgress(
                            fraction,
                            $"Backing up bundle… {fraction:P0} ({copied / (1024 * 1024):N0} / {total / (1024 * 1024):N0} MB)"));
                    }
                }

                await dst.FlushAsync(ct).ConfigureAwait(false);
            }

            File.Move(tempPath, backupPath, overwrite: false);

            if (!string.Equals(backupPath, LegacyBackupPath(bundlePath), StringComparison.OrdinalIgnoreCase))
                await File.WriteAllTextAsync(OriginPath(backupPath), Path.GetFullPath(bundlePath), ct)
                    .ConfigureAwait(false);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            throw;
        }

        progress?.Report(new ScanProgress(1.0, "Backup complete."));
        return true;
    }

    public static async Task RestoreBackupAsync(
    string bundlePath,
    string backupPath,
    IProgress<ScanProgress>? progress = null,
    CancellationToken ct = default)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException(
                $"No backup found at:\r\n{backupPath}\r\n\r\n" +
                "You can still restore the original through Steam → Verify Integrity of Game Files.",
                backupPath);

        await using (var probe = new FileStream(bundlePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        await using (var src = new FileStream(
                         backupPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                         CopyBufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous))
        await using (var dst = new FileStream(
                         bundlePath, FileMode.Create, FileAccess.Write, FileShare.None,
                         CopyBufferSize, FileOptions.Asynchronous))
        {
            byte[] buffer = new byte[CopyBufferSize];
            long copied = 0;
            long total = src.Length;
            int reportCounter = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int read = await src.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read == 0) break;

                await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                copied += read;

                if (++reportCounter % 4 == 0 && total > 0)
                {
                    double fraction = (double)copied / total;
                    progress?.Report(new ScanProgress(fraction, $"Restoring backup… {fraction:P0}"));
                }
            }

            await dst.FlushAsync(ct).ConfigureAwait(false);
        }

        progress?.Report(new ScanProgress(1.0, "Restore complete."));
    }

    public static async Task<byte[]> ReadBytesAtAsync(
    string bundlePath, long absoluteOffset, int size, CancellationToken ct = default)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Read size must be positive.");
        if (absoluteOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteOffset), "Offset cannot be negative.");

        await using var fs = new FileStream(
            bundlePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 1024 * 1024, FileOptions.Asynchronous);

        long end = absoluteOffset + size;
        if (end > fs.Length)
        {
            throw new PatchSafetyException(
                $"The requested range runs past the end of the bundle.\r\n\r\n" +
                $"Offset:      {absoluteOffset:N0}\r\n" +
                $"Size:        {size:N0}\r\n" +
                $"Range end:   {end:N0}\r\n" +
                $"Bundle size: {fs.Length:N0}\r\n\r\n" +
                "The preset's offset/size values probably do not match this version of the game files.");
        }

        fs.Seek(absoluteOffset, SeekOrigin.Begin);
        byte[] data = new byte[size];
        await fs.ReadExactlyAsync(data, ct).ConfigureAwait(false);
        return data;
    }

    public static async Task WriteBytesAtAsync(
    string bundlePath,
    long absoluteOffset,
    byte[] data,
    int expectedSize,
    CancellationToken ct = default)
    {
        if (absoluteOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteOffset), "Offset cannot be negative.");

        if (data.Length != expectedSize)
        {
            throw new PatchSafetyException(
                $"Payload size mismatch — nothing was written.\r\n\r\n" +
                $"Expected: {expectedSize:N0} bytes\r\n" +
                $"Produced: {data.Length:N0} bytes\r\n" +
                $"Difference: {data.Length - expectedSize:+#,0;-#,0} bytes\r\n\r\n" +
                "Textures sit back to back inside the .resS blob, so a wrong-size write would " +
                "overwrite the neighbouring texture. Check that the image dimensions, mip count " +
                "and format match the preset exactly.");
        }

        await using var fs = new FileStream(
            bundlePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None,
            bufferSize: 1024 * 1024, FileOptions.Asynchronous);

        long end = absoluteOffset + data.Length;
        if (end > fs.Length)
        {
            throw new PatchSafetyException(
                $"The write range runs past the end of the bundle — nothing was written.\r\n\r\n" +
                $"Offset:      {absoluteOffset:N0}\r\n" +
                $"Size:        {data.Length:N0}\r\n" +
                $"Range end:   {end:N0}\r\n" +
                $"Bundle size: {fs.Length:N0}");
        }

        fs.Seek(absoluteOffset, SeekOrigin.Begin);
        await fs.WriteAsync(data, ct).ConfigureAwait(false);
        await fs.FlushAsync(ct).ConfigureAwait(false);
    }

    private static void EnsureFreeSpace(string targetPath, long requiredBytes)
    {
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(targetPath));
            if (string.IsNullOrEmpty(root)) return;

            var drive = new DriveInfo(root);
            if (!drive.IsReady) return;

            long needed = requiredBytes + (64L * 1024 * 1024);
            if (drive.AvailableFreeSpace < needed)
            {
                throw new PatchSafetyException(
                    $"Not enough free space on {root} to back up the bundle.\r\n\r\n" +
                    $"Required: {needed / (1024 * 1024):N0} MB\r\n" +
                    $"Available: {drive.AvailableFreeSpace / (1024 * 1024):N0} MB");
            }
        }
        catch (PatchSafetyException)
        {
            throw;
        }
        catch
        {
        }
    }
}
