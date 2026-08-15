namespace ERSwapper.Core;

public enum BackupVerdict
{
    InUse,
    NotNeeded,
    Unknown,
}

public record BackupAuditEntry(
    string BackupPath,
    string BundlePath,
    long Bytes,
    BackupVerdict Verdict,
    string Reason)
{
    public string BundleName => Path.GetFileName(BundlePath);
}

public static class BackupAudit
{
    public static List<BackupAuditEntry> Audit(
        string rustInstallPath,
        IReadOnlyList<SwapRecord> history,
        IReadOnlyDictionary<string, SwapState> states)
    {
        var entries = new List<BackupAuditEntry>();

        foreach (string backup in BundlePatcher.FindBackups(rustInstallPath))
        {
            string bundle = BundlePatcher.GetBundlePathFromBackup(backup);

            long bytes = 0;
            try { bytes = new FileInfo(backup).Length; } catch { }

            List<SwapRecord> forBundle = history
                .Where(r => string.Equals(r.BundlePath, bundle, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (forBundle.Count == 0)
            {
                entries.Add(new BackupAuditEntry(
                    backup, bundle, bytes, BackupVerdict.Unknown,
                    "No swap history for this bundle — kept, because it may be the only way back"));

                continue;
            }

            int applied = forBundle.Count(r =>
                states.TryGetValue(r.Id, out SwapState state) && state == SwapState.Applied);

            entries.Add(applied > 0
                ? new BackupAuditEntry(backup, bundle, bytes, BackupVerdict.InUse,
                    $"{applied} swap(s) still applied in this bundle")
                : new BackupAuditEntry(backup, bundle, bytes, BackupVerdict.NotNeeded,
                    "No swaps are applied in this bundle any more"));
        }

        return entries
            .OrderBy(e => e.Verdict)
            .ThenByDescending(e => e.Bytes)
            .ToList();
    }

    public static int Delete(IEnumerable<BackupAuditEntry> entries)
    {
        int deleted = 0;

        foreach (BackupAuditEntry entry in entries)
        {
            try
            {
                if (File.Exists(entry.BackupPath)) File.Delete(entry.BackupPath);

                string origin = BundlePatcher.OriginPath(entry.BackupPath);
                if (File.Exists(origin)) File.Delete(origin);

                deleted++;
            }
            catch
            {
            }
        }

        return deleted;
    }

    public static string DescribeSize(long bytes)
    {
        if (bytes >= 1L << 30) return $"{bytes / (double)(1L << 30):N1} GB";
        if (bytes >= 1L << 20) return $"{bytes / (double)(1L << 20):N1} MB";
        if (bytes >= 1L << 10) return $"{bytes / (double)(1L << 10):N0} KB";

        return $"{bytes:N0} B";
    }
}
