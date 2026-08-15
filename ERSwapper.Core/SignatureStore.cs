namespace ERSwapper.Core;

public static class SignatureStore
{
    public static IReadOnlyList<string> ListInstalled()
    {
        var found = new List<string>();

        foreach (string folder in new[] { AppPaths.SignaturesDirectory, AppPaths.ShippedSignaturesDirectory })
        {
            try
            {
                if (!Directory.Exists(folder)) continue;

                foreach (string file in Directory.EnumerateFiles(folder, "*.sig")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    bool duplicate = found.Any(existing =>
                        string.Equals(Path.GetFileName(existing), Path.GetFileName(file),
                            StringComparison.OrdinalIgnoreCase));

                    if (!duplicate) found.Add(file);
                }
            }
            catch { }
        }

        return found;
    }

    public static string GetDefaultName(string? preferred = null)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            try
            {
                string resolved = AppPaths.ResolveSignaturePath(preferred);
                if (File.Exists(resolved)) return Path.GetFileName(resolved);
            }
            catch { }
        }

        IReadOnlyList<string> installed = ListInstalled();
        if (installed.Count > 0) return Path.GetFileName(installed[0]);

        return string.IsNullOrWhiteSpace(preferred) ? "" : Path.GetFileName(preferred);
    }

    public static bool ReferenceResolves(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return false;

        try
        {
            return File.Exists(AppPaths.ResolveSignaturePath(configuredPath));
        }
        catch
        {
            return false;
        }
    }

    public static string Import(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"File not found:\r\n{sourcePath}", sourcePath);

        bool alreadyASignature = Path.GetExtension(sourcePath)
            .Equals(".sig", StringComparison.OrdinalIgnoreCase);

        string targetName = alreadyASignature
            ? Path.GetFileName(sourcePath)
            : Path.GetFileName(sourcePath) + ".sig";

        string targetPath = Path.Combine(AppPaths.EnsureDirectory(AppPaths.SignaturesDirectory), targetName);

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return targetPath;
        }

        byte[] signature = ReadLeadingBytes(sourcePath, ResSOffsetLocator.SignatureLength);

        if (signature.Length < ResSOffsetLocator.MinimumSignatureLength)
        {
            throw new SignatureSearchException(
                $"That file is only {signature.Length:N0} bytes — at least " +
                $"{ResSOffsetLocator.MinimumSignatureLength:N0} are required for a reliable match.");
        }

        File.WriteAllBytes(targetPath, signature);
        return targetPath;
    }

    private static byte[] ReadLeadingBytes(string path, int count)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        int toRead = (int)Math.Min(count, fs.Length);
        byte[] buffer = new byte[toRead];
        fs.ReadExactly(buffer, 0, toRead);
        return buffer;
    }
}
