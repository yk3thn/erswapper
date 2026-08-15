using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERSwapper.Core;

public class BundleMapping
{
    public string Bundle { get; set; } = "";

    public string Cab { get; set; } = "";

    public string Signature { get; set; } = "";

    public string Label { get; set; } = "";

    [JsonIgnore]
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Label) ? FolderQualifiedName : Label;

    [JsonIgnore]
    public string FolderQualifiedName
    {
        get
        {
            string file = Path.GetFileNameWithoutExtension(Bundle);
            string? folder = Path.GetDirectoryName(Bundle);

            return string.IsNullOrWhiteSpace(folder) ? file : $"{folder} / {file}";
        }
    }
}

public static class BundleRegistry
{
    public const string FileName = "bundles.json";

    private static List<BundleMapping>? _cached;

    public static string ShippedPath => Path.Combine(AppPaths.SeedConfigDirectory, FileName);

    public static string UserPath => Path.Combine(AppPaths.UserDataDirectory, FileName);

    public static IReadOnlyList<BundleMapping> All => _cached ??= Load();

    public static void Invalidate() => _cached = null;

    public static List<BundleMapping> Load()
    {
        foreach (string path in new[] { UserPath, ShippedPath })
        {
            List<BundleMapping>? loaded = TryRead(path);
            if (loaded is { Count: > 0 }) return loaded;
        }

        return new List<BundleMapping>();
    }

    private static List<BundleMapping>? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            return JsonSerializer.Deserialize<List<BundleMapping>>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
        }
        catch
        {
            return null;
        }
    }

    public static BundleMapping? FindByBundle(string? bundleRelativePath)
    {
        if (string.IsNullOrWhiteSpace(bundleRelativePath)) return null;

        string wanted = Normalise(bundleRelativePath);

        return All.FirstOrDefault(m => Normalise(m.Bundle) == wanted)
               ?? All.FirstOrDefault(m =>
                   string.Equals(Path.GetFileName(m.Bundle), Path.GetFileName(wanted),
                       StringComparison.OrdinalIgnoreCase));
    }

    public static BundleMapping? FindByCab(string? cabId)
    {
        if (string.IsNullOrWhiteSpace(cabId)) return null;

        return All.FirstOrDefault(m =>
            string.Equals(m.Cab, cabId, StringComparison.OrdinalIgnoreCase));
    }

    public static string? SignatureFor(string? bundleRelativePath)
    {
        BundleMapping? mapping = FindByBundle(bundleRelativePath);

        if (mapping is null) return null;

        if (!string.IsNullOrWhiteSpace(mapping.Signature)) return mapping.Signature;

        return string.IsNullOrWhiteSpace(mapping.Cab)
            ? null
            : CabIdentity.SignatureNameFor(mapping.Cab);
    }

    public static string LabelFor(string? bundleRelativePath)
    {
        BundleMapping? mapping = FindByBundle(bundleRelativePath);

        if (mapping is not null) return mapping.DisplayName;

        return string.IsNullOrWhiteSpace(bundleRelativePath)
            ? ""
            : Path.GetFileName(bundleRelativePath);
    }

    private static string Normalise(string path) =>
        path.Replace('/', '\\').Trim().TrimStart('\\').ToLowerInvariant();
}
