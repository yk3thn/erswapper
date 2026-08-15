using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ERSwapper.Core;

public static class RustInstallLocator
{
    private const string GameFolderName = "Rust";

    public static string? TryLocate()
    {
        foreach (string library in GetSteamLibraryFolders())
        {
            try
            {
                string candidate = Path.Combine(library, "steamapps", "common", GameFolderName);
                if (LooksLikeRustInstall(candidate)) return candidate;
            }
            catch { }
        }

        return null;
    }

    public static bool LooksLikeRustInstall(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;

        return File.Exists(Path.Combine(path, "RustClient.exe"))
               || Directory.Exists(Path.Combine(path, "RustClient_Data"))
               || Directory.Exists(Path.Combine(path, "Rust_Data"));
    }

    private static IEnumerable<string> GetSteamLibraryFolders()
    {
        string? steamPath = GetSteamPath();
        if (steamPath is null) yield break;

        yield return steamPath;

        string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;

        string content;
        try { content = File.ReadAllText(vdf); }
        catch { yield break; }

        foreach (Match match in Regex.Matches(content, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            string raw = match.Groups[1].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(raw)) yield return raw;
        }
    }

    private static string? GetSteamPath()
    {
        string?[] candidates =
        {
            ReadRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            ReadRegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
        };

        foreach (string? candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                return candidate.Replace('/', '\\');
        }

        return null;
    }

    private static string? ReadRegistryString(RegistryKey hive, string subKey, string valueName)
    {
        try
        {
            using RegistryKey? key = hive.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }
}
