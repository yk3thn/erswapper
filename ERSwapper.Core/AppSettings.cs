using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERSwapper.Core;

public class AppSettings
{
    public string RustInstallPath { get; set; } = "";

    public string ExportFolder { get; set; } = "";

    public string DumpFolder { get; set; } = "";

    public bool AcknowledgedSafetyNotice { get; set; }

    public string PublishFolder { get; set; } = "";

    public string SkippedUpdateVersion { get; set; } = "";

    public bool KeepFullBundleBackups { get; set; }

    public string ConfigFolder { get; set; } = "";

    [JsonIgnore]
    public string EffectiveConfigFolder =>
        string.IsNullOrWhiteSpace(ConfigFolder) ? AppPaths.DefaultConfigDirectory : ConfigFolder;

    public void ApplyConfigFolder() =>
        AppPaths.ConfigDirectoryOverride =
            string.IsNullOrWhiteSpace(ConfigFolder) ? null : ConfigFolder;

    [JsonIgnore]
    public string EffectiveDumpFolder =>
        string.IsNullOrWhiteSpace(DumpFolder) ? AppPaths.DefaultDumpFolder : DumpFolder;

    [JsonIgnore]
    public string EffectiveExportFolder =>
        string.IsNullOrWhiteSpace(ExportFolder) ? AppPaths.DesktopDirectory : ExportFolder;

    [JsonIgnore]
    public string TexconvPath => TexconvProvider.Locate() ?? TexconvProvider.ExpectedPath;

    [JsonIgnore]
    public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(RustInstallPath) && Directory.Exists(RustInstallPath)
    && TexconvProvider.Locate() is not null;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                string json = File.ReadAllText(AppPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);

                if (loaded is not null)
                {
                    if (loaded.FillMissingPaths())
                    {
                        try { loaded.Save(); } catch { }
                    }

                    return loaded;
                }
            }
        }
        catch
        {
        }

        return new AppSettings
        {
            RustInstallPath = RustInstallLocator.TryLocate() ?? "",
        };
    }

    public bool FillMissingPaths()
    {
        bool changed = false;

        if (string.IsNullOrWhiteSpace(RustInstallPath) || !Directory.Exists(RustInstallPath))
        {
            string? found = RustInstallLocator.TryLocate();
            if (found is not null && found != RustInstallPath)
            {
                RustInstallPath = found;
                changed = true;
            }
        }

        return changed;
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.SettingsFile, json);
    }
}
