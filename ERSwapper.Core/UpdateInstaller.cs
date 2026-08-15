using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text;

namespace ERSwapper.Core;

public class UpdateNotSupportedException : Exception
{
    public UpdateNotSupportedException(string message) : base(message) { }
}

public class UpdateInstaller
{
    public const int InstallerVersion = 1;

    private readonly string _executableName;

    public UpdateInstaller(string executableName = "ERSwapper.exe")
        => _executableName = executableName;

    public static string UpdateRoot => Path.Combine(AppPaths.UserDataDirectory, "update");

    public async Task<string> DownloadAsync(
        UpdateInfo info, IProgress<double>? progress, CancellationToken ct = default)
    {
        Cleanup();

        string zipPath = Path.Combine(AppPaths.EnsureDirectory(UpdateRoot), "update.zip");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ERSwapper");

        using HttpResponseMessage response = await client
            .GetAsync(info.Asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? info.Asset.Size;

        await using (Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var destination = new FileStream(
                         zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true))
        {
            byte[] buffer = new byte[1024 * 128];
            long received = 0;

            while (true)
            {
                int read = await source.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read == 0) break;

                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                received += read;

                if (total > 0) progress?.Report(Math.Clamp((double)received / total, 0, 1));
            }
        }

        return zipPath;
    }

    public string Stage(string zipPath)
    {
        string staged = Path.Combine(AppPaths.EnsureDirectory(UpdateRoot), "staged");

        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
        Directory.CreateDirectory(staged);

        ZipFile.ExtractToDirectory(zipPath, staged, overwriteFiles: true);

        string resolved = ResolveRoot(staged);

        if (!File.Exists(Path.Combine(resolved, _executableName)))
        {
            throw new InvalidOperationException(
                $"The downloaded update does not contain {_executableName}.");
        }

        VerifySupported(resolved);

        return resolved;
    }

    public void VerifySupported(string stagedRoot)
    {
        ReleaseManifest? manifest = ReleaseManifest.TryLoad(Path.Combine(stagedRoot, "Config"));

        if (manifest is null) return;

        if (manifest.MinimumInstallerVersion > InstallerVersion)
        {
            throw new UpdateNotSupportedException(
                $"This update needs a newer installer than this copy of ER Swapper has.\r\n\r\n" +
                $"This version installs updates built for version {InstallerVersion} and below, " +
                $"but the download needs version {manifest.MinimumInstallerVersion}.\r\n\r\n" +
                "Nothing has been changed. Download the latest release from the releases page and " +
                "extract it over this folder by hand — after that, updating will work normally again.");
        }
    }

    private string ResolveRoot(string staged)
    {
        if (File.Exists(Path.Combine(staged, _executableName))) return staged;

        string[] directories = Directory.GetDirectories(staged);
        string[] files = Directory.GetFiles(staged);

        if (directories.Length == 1 && files.Length == 0) return ResolveRoot(directories[0]);

        return staged;
    }

    public void LaunchAndExit(string stagedRoot)
    {
        string installDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string executable = Path.Combine(installDirectory, _executableName);
        string scriptPath = Path.Combine(AppPaths.EnsureDirectory(UpdateRoot), "apply_update.cmd");

        File.WriteAllText(scriptPath, BuildScript(stagedRoot, installDirectory, executable), Encoding.ASCII);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"\"{scriptPath}\" {Environment.ProcessId}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = UpdateRoot,
        };

        Process.Start(startInfo);
    }

    private static string BuildScript(string stagedRoot, string installDirectory, string executable)
    {
        var script = new StringBuilder();

        script.AppendLine("@echo off");
        script.AppendLine("setlocal");
        script.AppendLine("set \"TARGETPID=%~1\"");
        script.AppendLine();
        script.AppendLine(":wait");
        script.AppendLine("tasklist /FI \"PID eq %TARGETPID%\" 2>nul | find \"%TARGETPID%\" >nul");
        script.AppendLine("if not errorlevel 1 (");
        script.AppendLine("    timeout /t 1 /nobreak >nul");
        script.AppendLine("    goto wait");
        script.AppendLine(")");
        script.AppendLine();
        script.AppendLine($"robocopy \"{stagedRoot}\" \"{installDirectory}\" /E /IS /IT /XD \"{Path.Combine(stagedRoot, "Config")}\" /R:3 /W:1 /NFL /NDL /NJH /NJS /NP >nul");
        script.AppendLine("if errorlevel 8 (");
        script.AppendLine("    echo ER Swapper could not be updated. The existing version is unchanged.");
        script.AppendLine("    pause");
        script.AppendLine("    exit /b 1");
        script.AppendLine(")");
        script.AppendLine();
        script.AppendLine($"robocopy \"{Path.Combine(stagedRoot, "Config")}\" \"{Path.Combine(installDirectory, "Config")}\" /MIR /R:3 /W:1 /NFL /NDL /NJH /NJS /NP >nul");
        script.AppendLine("if errorlevel 8 (");
        script.AppendLine("    echo ER Swapper could not replace its Config folder. The existing version is unchanged.");
        script.AppendLine("    pause");
        script.AppendLine("    exit /b 1");
        script.AppendLine(")");
        script.AppendLine();
        script.AppendLine($"start \"\" /D \"{installDirectory}\" \"{executable}\"");
        script.AppendLine();
        script.AppendLine($"rd /s /q \"{stagedRoot}\" >nul 2>&1");
        script.AppendLine($"del /q \"{Path.Combine(UpdateRoot, "update.zip")}\" >nul 2>&1");
        script.AppendLine("(goto) 2>nul & del \"%~f0\"");

        return script.ToString();
    }

    public static void Cleanup()
    {
        try
        {
            string staged = Path.Combine(UpdateRoot, "staged");
            if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);

            string zip = Path.Combine(UpdateRoot, "update.zip");
            if (File.Exists(zip)) File.Delete(zip);
        }
        catch
        {
        }
    }
}
