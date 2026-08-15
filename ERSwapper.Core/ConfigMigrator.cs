using System.Text.Json;

namespace ERSwapper.Core;

public class LayoutState
{
    public int SchemaVersion { get; set; }

    public string LastAppVersion { get; set; } = "";
}

public record MigrationResult(int From, int To, List<string> Notes)
{
    public bool Changed => From != To || Notes.Count > 0;
}

public static class ConfigMigrator
{
    public const int CurrentSchemaVersion = 1;

    public static string StateFile => Path.Combine(AppPaths.UserDataDirectory, "layout.json");

    public static LayoutState LoadState()
    {
        try
        {
            if (!File.Exists(StateFile)) return new LayoutState();

            return JsonSerializer.Deserialize<LayoutState>(File.ReadAllText(StateFile)) ?? new LayoutState();
        }
        catch
        {
            return new LayoutState();
        }
    }

    public static void SaveState(LayoutState state)
    {
        try
        {
            File.WriteAllText(
                StateFile,
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public static MigrationResult Run()
    {
        LayoutState state = LoadState();

        int from = state.SchemaVersion;
        var notes = new List<string>();

        if (from < 1) notes.AddRange(MigrateToOne());

        state.SchemaVersion = CurrentSchemaVersion;
        state.LastAppVersion = AppVersion.Display;

        SaveState(state);

        return new MigrationResult(from, CurrentSchemaVersion, notes);
    }

    private static List<string> MigrateToOne()
    {
        var notes = new List<string>();

        int shadowed = RemoveShadowCopies();
        if (shadowed > 0) notes.Add($"Removed {shadowed} stale copy(s) of shipped files from your data folder.");

        return notes;
    }

    private static int RemoveShadowCopies()
    {
        int removed = 0;

        removed += RemoveIfShippedEquivalentExists(
            PresetStore.UserPresetsPath, PresetStore.SeedPresetsPath);

        removed += RemoveDuplicateFiles(
            Path.Combine(AppPaths.SeedConfigDirectory, "Thumbnails"),
            Path.Combine(AppPaths.UserDataDirectory, "Thumbnails"),
            "*.png");

        removed += RemoveDuplicateFiles(
            AppPaths.ShippedSignaturesDirectory,
            Path.Combine(AppPaths.UserDataDirectory, "Signatures"),
            "*.sig");

        return removed;
    }

    private static int RemoveIfShippedEquivalentExists(string userPath, string shippedPath)
    {
        try
        {
            if (!File.Exists(userPath) || !File.Exists(shippedPath)) return 0;

            File.Delete(userPath);
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    private static int RemoveDuplicateFiles(string shippedDirectory, string userDirectory, string pattern)
    {
        int removed = 0;

        try
        {
            if (!Directory.Exists(shippedDirectory) || !Directory.Exists(userDirectory)) return 0;

            foreach (string shipped in Directory.EnumerateFiles(shippedDirectory, pattern))
            {
                string duplicate = Path.Combine(userDirectory, Path.GetFileName(shipped));
                if (!File.Exists(duplicate)) continue;

                try
                {
                    File.Delete(duplicate);
                    removed++;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return removed;
    }
}

public record ConfigIntegrity(List<string> Missing, bool InstallFolderWritable)
{
    public bool IsHealthy => Missing.Count == 0;
}

public static class ConfigCheck
{
    public static ConfigIntegrity Inspect()
    {
        var missing = new List<string>();

        ReleaseManifest manifest = ReleaseManifest.TryLoad(AppPaths.SeedConfigDirectory)
                                   ?? ReleaseManifest.ForCurrentBuild();

        foreach (string required in manifest.RequiredFiles)
        {
            if (!File.Exists(Path.Combine(AppPaths.SeedConfigDirectory, required))) missing.Add(required);
        }

        return new ConfigIntegrity(missing, IsWritable(AppPaths.AppDirectory));
    }

    public static bool IsWritable(string directory)
    {
        try
        {
            string probe = Path.Combine(directory, ".erswapper_write_probe");

            File.WriteAllText(probe, "");
            File.Delete(probe);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string DescribeProblem(ConfigIntegrity integrity)
    {
        var lines = new List<string>();

        if (!integrity.IsHealthy)
        {
            lines.Add("These files are missing from the Config folder next to ER Swapper:");
            lines.AddRange(integrity.Missing.Select(m => "  • " + m));
            lines.Add("");
            lines.Add("Re-extract the release zip over this folder to put them back.");
        }

        if (!integrity.InstallFolderWritable)
        {
            if (lines.Count > 0) lines.Add("");

            lines.Add(
                "ER Swapper cannot write to its own folder, so it will not be able to update itself. " +
                "Move it somewhere in your user folder — the Desktop or Documents is fine — rather " +
                "than Program Files.");
        }

        return string.Join("\r\n", lines);
    }
}
