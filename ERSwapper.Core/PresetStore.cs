using System.Text.Json;

namespace ERSwapper.Core;

public static class PresetStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string UserPresetsPath => Path.Combine(AppPaths.UserDataDirectory, "presets.json");

    public static string SeedPresetsPath => Path.Combine(AppPaths.SeedConfigDirectory, "presets.json");

    public static List<ItemPreset> Load(out List<string> warnings, string? explicitPath = null)
    {
        warnings = new List<string>();

        string path;

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            path = explicitPath;
        }
        else
        {
            path = File.Exists(UserPresetsPath) ? UserPresetsPath : SeedPresetsPath;
        }

        if (!File.Exists(path))
        {
            warnings.Add($"No presets.json found at {UserPresetsPath} or {SeedPresetsPath}.");
            return new List<ItemPreset>();
        }

        List<ItemPreset>? parsed;
        try
        {
            string json = File.ReadAllText(path);
            parsed = JsonSerializer.Deserialize<List<ItemPreset>>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            warnings.Add($"presets.json is not valid JSON ({ex.Message}). No items were loaded.");
            return new List<ItemPreset>();
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not read presets.json: {ex.Message}");
            return new List<ItemPreset>();
        }

        if (parsed is null) return new List<ItemPreset>();

        var valid = new List<ItemPreset>();
        foreach (ItemPreset preset in parsed)
        {
            preset.Category = ItemPreset.NormaliseCategory(preset.Category);

            string? problem = preset.Validate();
            if (problem is null) valid.Add(preset);
            else warnings.Add($"Skipped preset: {problem}");
        }

        return valid;
    }

    public static void Save(IEnumerable<ItemPreset> presets)
    {
        string json = JsonSerializer.Serialize(
            presets.ToList(), new JsonSerializerOptions { WriteIndented = true });

        string target = UserPresetsPath;
        string temp = target + ".tmp";

        File.WriteAllText(temp, json);

        if (File.Exists(target)) File.Replace(temp, target, destinationBackupFileName: null);
        else File.Move(temp, target);
    }

    public static IEnumerable<IGrouping<string, ItemPreset>> GroupForDisplay(IEnumerable<ItemPreset> presets)
    {
        return presets
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Category) ? ItemPreset.DefaultCategory : p.Category)
            .OrderBy(g => CategoryRank(g.Key))
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static int CategoryRank(string category)
    {
        int index = Array.FindIndex(
            ItemPreset.PreferredCategoryOrder,
            c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : ItemPreset.PreferredCategoryOrder.Length;
    }
}
