namespace ERSwapper.Core;

public static class ItemSearch
{
    public static List<ItemPreset> Filter(IEnumerable<ItemPreset> presets, string? query)
    {
        List<ItemPreset> all = presets.ToList();

        string[] terms = (query ?? "").Split(
            ' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (terms.Length == 0) return all;

        return all.Where(preset => terms.All(term => Matches(preset, term))).ToList();
    }

    public static List<ItemPreset> FilterByBundle(IEnumerable<ItemPreset> presets, string? bundleRelativePath)
    {
        List<ItemPreset> all = presets.ToList();

        if (string.IsNullOrWhiteSpace(bundleRelativePath)) return all;

        return all
            .Where(p => string.Equals(p.BundleRelativePath, bundleRelativePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static List<(string BundleRelativePath, int Count)> BundleBreakdown(IEnumerable<ItemPreset> presets)
    {
        return presets
            .GroupBy(p => p.BundleRelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => (BundleRelativePath: g.Key, Count: g.Count()))
            .OrderBy(x => x.BundleRelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool Matches(ItemPreset preset, string term)
        => Contains(preset.DisplayName, term)
           || Contains(preset.TextureObjectName, term)
           || Contains(preset.Category, term);

    private static bool Contains(string? haystack, string needle)
        => haystack is not null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
