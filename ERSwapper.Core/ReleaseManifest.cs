using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERSwapper.Core;

public class ReleaseManifest
{
    public const string FileName = "release.json";

    public int FormatVersion { get; set; } = 1;

    public int MinimumInstallerVersion { get; set; } = 1;

    public string AppVersion { get; set; } = "";

    public string ExecutableName { get; set; } = "ERSwapper.exe";

    public List<string> RequiredFiles { get; set; } = new();

    [JsonIgnore]
    public bool IsUsableBy => MinimumInstallerVersion <= UpdateInstaller.InstallerVersion;

    public static string PathIn(string configDirectory) => Path.Combine(configDirectory, FileName);

    public static ReleaseManifest? TryLoad(string configDirectory)
    {
        try
        {
            string path = PathIn(configDirectory);
            if (!File.Exists(path)) return null;

            return JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public void Save(string configDirectory)
    {
        Directory.CreateDirectory(configDirectory);

        File.WriteAllText(
            PathIn(configDirectory),
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static ReleaseManifest ForCurrentBuild() => new()
    {
        FormatVersion = 1,
        MinimumInstallerVersion = 1,
        AppVersion = ERSwapper.Core.AppVersion.Display.TrimStart('v'),
        ExecutableName = "ERSwapper.exe",
        RequiredFiles = new List<string> { "presets.json", "bundles.json" },
    };
}
