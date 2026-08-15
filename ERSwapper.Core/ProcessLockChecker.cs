using System.Diagnostics;

namespace ERSwapper.Core;

public static class ProcessLockChecker
{
    public static readonly string[] GameProcessNames =
{
        "RustClient",
        "Rust",
        "RustSteamLauncher",
        "EasyAntiCheat",
    };

    public static bool IsRustRunning() => GetRunningGameProcesses().Count > 0;

    public static IReadOnlyList<string> GetRunningGameProcesses()
    {
        var found = new List<string>();

        foreach (string name in GameProcessNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            try
            {
                if (processes.Length > 0 && !found.Contains(name))
                    found.Add(name);
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }

        return found;
    }

    public static bool IsFileLocked(string path)
    {
        if (!File.Exists(path)) return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
