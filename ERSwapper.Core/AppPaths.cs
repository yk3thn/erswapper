namespace ERSwapper.Core;

public static class AppPaths
{
    public static string AppDirectory => AppContext.BaseDirectory;

    public static string DefaultConfigDirectory => Path.Combine(AppDirectory, "Config");

    public static string? ConfigDirectoryOverride { get; set; }

    public static string SeedConfigDirectory =>
        !string.IsNullOrWhiteSpace(ConfigDirectoryOverride) && Directory.Exists(ConfigDirectoryOverride)
            ? ConfigDirectoryOverride
            : DefaultConfigDirectory;

    public static string UserDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationName);

    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ApplicationName
    {
        get
        {
            string? name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
            return string.IsNullOrWhiteSpace(name) ? "ERSwapper" : name;
        }
    }

    public static string SettingsFile => Path.Combine(UserDataDirectory, "settings.json");

    public static string OffsetCacheFile => Path.Combine(UserDataDirectory, "offset_cache.json");

    public static string ShippedSignaturesDirectory => Path.Combine(SeedConfigDirectory, "Signatures");

    public static string SignaturesDirectory => Path.Combine(UserDataDirectory, "Signatures");

    public static string BackupDirectory => Path.Combine(UserDataDirectory, "Backups");

    public static string TempDirectory
    {
        get
        {
            string dir = Path.Combine(Path.GetTempPath(), "ERSwapper");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DesktopDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string DefaultWorkspaceFolder => Path.Combine(DesktopDirectory, "rust_mod");

    public static string DefaultDumpFolder => Path.Combine(DefaultWorkspaceFolder, "dumps");

    public static string ResolveSignaturePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new ArgumentException("Signature path is empty.", nameof(configuredPath));

        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        string fileName = Path.GetFileName(configuredPath);

        string[] candidates =
        {
            Path.Combine(SignaturesDirectory, fileName),
            Path.Combine(AppDirectory, configuredPath),
            Path.Combine(UserDataDirectory, configuredPath),
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(SignaturesDirectory, fileName);
    }

    public static IReadOnlyList<string> GetSignatureCandidates(ItemPreset preset)
    {
        string? mapped = BundleRegistry.SignatureFor(preset.BundleRelativePath);

        return GetSignatureCandidates(mapped, preset.ResSSignatureSourcePath);
    }

    public static IReadOnlyList<string> GetSignatureCandidates(params string?[] preferredPaths)
    {
        var candidates = new List<string>();

        foreach (string? preferred in preferredPaths)
        {
            if (string.IsNullOrWhiteSpace(preferred)) continue;

            string exact = ResolveSignaturePath(preferred);

            bool alreadyAdded = candidates.Any(existing =>
                string.Equals(Path.GetFullPath(existing), Path.GetFullPath(exact),
                    StringComparison.OrdinalIgnoreCase));

            if (File.Exists(exact) && !alreadyAdded) candidates.Add(exact);
        }

        foreach (string file in SignatureStore.ListInstalled())
        {
            bool alreadyListed = candidates.Any(existing =>
                string.Equals(Path.GetFullPath(existing), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase));

            if (!alreadyListed) candidates.Add(file);
        }

        return candidates;
    }

    public static void ClearTemp()
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(TempDirectory))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }
}
