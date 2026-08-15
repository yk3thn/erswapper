namespace ERSwapper.Core;

public record BundleCoverage(
    string BundleRelativePath,
    int ItemCount,
    IReadOnlyList<string> ReferencedSignatureNames,
    bool AnySignatureInstalled,
    bool ReadableFromBundleDirectory = false)
{
    public bool IsCovered => ReadableFromBundleDirectory || AnySignatureInstalled;
}

public static class SignatureCoverage
{
    public static List<BundleCoverage> Analyse(IEnumerable<ItemPreset> presets, string? rustInstallPath = null)
    {
        var installed = SignatureStore.ListInstalled()
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return presets
            .GroupBy(p => p.BundleRelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                string? mapped = BundleRegistry.SignatureFor(group.Key);

                List<string> names = group
                    .Select(p => Path.GetFileName(p.ResSSignatureSourcePath))
                    .Append(mapped)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                bool signatureInstalled = names.Any(installed.Contains);
                bool readable = CanReadDirectory(rustInstallPath, group.Key);

                return new BundleCoverage(group.Key, group.Count(), names, signatureInstalled, readable);
            })
            .OrderByDescending(c => c.ItemCount)
            .ToList();
    }

    private static bool CanReadDirectory(string? rustInstallPath, string bundleRelativePath)
    {
        if (string.IsNullOrWhiteSpace(rustInstallPath)) return false;

        try
        {
            string full = BundleLocator.Resolve(rustInstallPath, bundleRelativePath);
            return UnityBundleReader.TryFindResSOffset(full) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static List<BundleCoverage> FindUncovered(IEnumerable<ItemPreset> presets, string? rustInstallPath = null)
    => Analyse(presets, rustInstallPath).Where(c => !c.IsCovered).ToList();

    public static string DescribeMissing(IReadOnlyList<BundleCoverage> uncovered)
    {
        if (uncovered.Count == 0) return "";

        var lines = new List<string>
        {
            uncovered.Count == 1
                ? "One bundle has no signature installed, so its items cannot be located:"
                : $"{uncovered.Count} bundles have no signature installed, so their items cannot be located:",
            "",
        };

        foreach (BundleCoverage bundle in uncovered)
        {
            lines.Add($"  • {bundle.BundleRelativePath}  ({bundle.ItemCount} item(s))");

            foreach (string name in bundle.ReferencedSignatureNames)
                lines.Add($"      needs: {name}");
        }

        lines.Add("");
        lines.Add("Export that bundle's .resS in UABEA, then add it under");
        lines.Add("Settings → Add bundle signature.");

        return string.Join("\r\n", lines);
    }
}
