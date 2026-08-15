namespace ERSwapper.Core;

public class BundleIndex
{
    private readonly Dictionary<string, string> _cabToBundle = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Unreadable { get; } = new();

    public IReadOnlyDictionary<string, string> Map => _cabToBundle;

    public int Count => _cabToBundle.Count;

    public static BundleIndex Build(string rustInstallPath)
    {
        var index = new BundleIndex();

        if (string.IsNullOrWhiteSpace(rustInstallPath) || !Directory.Exists(rustInstallPath))
            return index;

        IEnumerable<string> bundles;
        try
        {
            bundles = Directory.EnumerateFiles(rustInstallPath, "*.bundle", SearchOption.AllDirectories);
        }
        catch
        {
            return index;
        }

        foreach (string bundlePath in bundles)
        {
            try
            {
                IReadOnlyList<BundleEntry> entries = UnityBundleReader.ReadEntries(bundlePath);

                foreach (BundleEntry entry in entries)
                {
                    string? cab = CabIdentity.TryExtract(entry.Path);
                    if (cab is null) continue;

                    index._cabToBundle.TryAdd(cab, ToRelativePath(rustInstallPath, bundlePath));
                }
            }
            catch
            {
                index.Unreadable.Add(bundlePath);
            }
        }

        return index;
    }

    public string? FindBundleForCab(string? cabId)
    {
        if (string.IsNullOrWhiteSpace(cabId)) return null;
        return _cabToBundle.TryGetValue(cabId, out string? bundle) ? bundle : null;
    }

    public string? FindBundleForText(string? text) => FindBundleForCab(CabIdentity.TryExtract(text));

    private static string ToRelativePath(string rustInstallPath, string bundlePath)
    {
        string relative = Path.GetRelativePath(rustInstallPath, bundlePath);

        const string bundlesPrefix = "Bundles" + "\\";
        if (relative.StartsWith(bundlesPrefix, StringComparison.OrdinalIgnoreCase))
            relative = relative[bundlesPrefix.Length..];

        return relative;
    }
}
