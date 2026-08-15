using System.Text.Json;

namespace ERSwapper.Core;

public class UnsupportedEntry
{
    public string DumpFile { get; set; } = "";

    public string TextureName { get; set; } = "";

    public string Status { get; set; } = UnsupportedRegistry.UnsupportedStatus;

    public string Reason { get; set; } = "";

    public int TextureFormat { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    public string RecordedUtc { get; set; } = "";
}

public static class UnsupportedRegistry
{
    public const string UnsupportedStatus = "unsupported";

    public static string FilePath => Path.Combine(AppPaths.UserDataDirectory, "unsupported.json");

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static List<UnsupportedEntry> Load(string? filePath = null)
    {
        string path = filePath ?? FilePath;

        try
        {
            if (!File.Exists(path)) return new List<UnsupportedEntry>();

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<UnsupportedEntry>>(json) ?? new List<UnsupportedEntry>();
        }
        catch
        {
            return new List<UnsupportedEntry>();
        }
    }

    public static bool IsKnown(string dumpFileName, string? filePath = null)
    {
        string name = Path.GetFileName(dumpFileName);

        return Load(filePath).Any(entry =>
            string.Equals(entry.DumpFile, name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Record(ParsedTexture texture, string? reasonOverride = null, string? filePath = null)
    {
        string dumpFile = Path.GetFileName(texture.SourceFile);
        string reason = reasonOverride ?? texture.GetBlocker() ?? "unknown";

        List<UnsupportedEntry> entries = Load(filePath);

        UnsupportedEntry? existing = entries.FirstOrDefault(e =>
            string.Equals(e.DumpFile, dumpFile, StringComparison.OrdinalIgnoreCase));

        bool isNew = existing is null;

        if (existing is null)
        {
            existing = new UnsupportedEntry
            {
                DumpFile = dumpFile,
                RecordedUtc = DateTime.UtcNow.ToString("O"),
            };

            entries.Add(existing);
        }

        existing.TextureName = texture.Name;
        existing.Status = UnsupportedStatus;
        existing.Reason = reason;
        existing.TextureFormat = texture.TextureFormat;
        existing.Width = texture.Width;
        existing.Height = texture.Height;

        Save(entries, filePath);
        return isNew;
    }

    private static void Save(List<UnsupportedEntry> entries, string? filePath)
    {
        string path = filePath ?? FilePath;

        try
        {
            entries.Sort((a, b) => string.Compare(a.DumpFile, b.DumpFile, StringComparison.OrdinalIgnoreCase));

            string json = JsonSerializer.Serialize(entries, WriteOptions);
            string temp = path + ".tmp";

            File.WriteAllText(temp, json);

            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null);
            else File.Move(temp, path);
        }
        catch
        {
        }
    }
}
