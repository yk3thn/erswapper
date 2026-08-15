using System.Diagnostics;
using System.Text;

namespace ERSwapper.Core;

public class TexconvException : Exception
{
    public string ToolOutput { get; }

    public TexconvException(string message, string toolOutput) : base(message + "\r\n\r\n" + toolOutput)
        => ToolOutput = toolOutput;
}

public class TexconvWrapper
{
    private readonly string _texconvPath;

    public TexconvWrapper(string texconvPath)
    {
        if (string.IsNullOrWhiteSpace(texconvPath))
            throw new ArgumentException("texconv path is not configured. Set it in Settings.", nameof(texconvPath));

        _texconvPath = texconvPath;
    }

    public string TexconvPath => _texconvPath;

    public static string? TryLocate()
    {
        string[] wellKnown =
        {
            Path.Combine(AppPaths.AppDirectory, "texconv.exe"),
            Path.Combine(AppPaths.DefaultWorkspaceFolder, "texconv.exe"),
        };

        foreach (string candidate in wellKnown)
        {
            try
            {
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar is null) return null;

        foreach (string dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), "texconv.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        return null;
    }

    public void EnsureAvailable()
    {
        if (!File.Exists(_texconvPath))
            throw new FileNotFoundException(
                $"texconv.exe was not found at:\r\n{_texconvPath}\r\n\r\n" +
                "Download it from the Microsoft DirectXTex releases page and point Settings at it.",
                _texconvPath);
    }

    public async Task<string> EncodePngToDdsAsync(
    string pngPath,
    string outputDir,
    string dxgiFormat,
    int mipCount,
    CancellationToken ct = default)
    {
        EnsureAvailable();

        if (!File.Exists(pngPath))
            throw new FileNotFoundException($"Source image not found: {pngPath}", pngPath);

        Directory.CreateDirectory(outputDir);

        string stem = $"erswapper_encode_{Guid.NewGuid():N}";
        string stagedPng = Path.Combine(outputDir, stem + ".png");
        string expectedDds = Path.Combine(outputDir, stem + ".dds");

        ImageOrientation.FlipVerticalTo(pngPath, stagedPng);

        try
        {
            string args = $"-nologo -f {dxgiFormat} -m {mipCount} -dx9 -y -o \"{TrimTrailingSlash(outputDir)}\" \"{stagedPng}\"";
            var result = await RunAsync(args, ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new TexconvException($"texconv failed to encode the PNG (exit code {result.ExitCode}).", result.Output);

            if (!File.Exists(expectedDds))
                throw new TexconvException("texconv reported success but produced no .dds file.", result.Output);

            return expectedDds;
        }
        finally
        {
            try { File.Delete(stagedPng); } catch { }
        }
    }

    public async Task DecodeRawBytesToPngAsync(
    byte[] rawTextureBytes,
    string outputPngPath,
    int width,
    int height,
    int mipCount,
    string dxgiFormat = "BC1_UNORM",
    CancellationToken ct = default)
    {
        EnsureAvailable();

        string workDir = AppPaths.TempDirectory;

        string stem = $"erswapper_decode_{Guid.NewGuid():N}";
        string tempDds = Path.Combine(workDir, stem + ".dds");
        string producedPng = Path.Combine(workDir, stem + ".png");

        byte[] header = DdsHeaderBuilder.BuildHeader(dxgiFormat, width, height, mipCount);

        try
        {
            await using (var fs = new FileStream(tempDds, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(header, ct).ConfigureAwait(false);
                await fs.WriteAsync(rawTextureBytes, ct).ConfigureAwait(false);
            }

            string args = $"-nologo -ft png -y -o \"{TrimTrailingSlash(workDir)}\" \"{tempDds}\"";
            var result = await RunAsync(args, ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new TexconvException($"texconv failed to decode the texture (exit code {result.ExitCode}).", result.Output);

            if (!File.Exists(producedPng))
                throw new TexconvException("texconv reported success but produced no .png file.", result.Output);

            string? outDir = Path.GetDirectoryName(outputPngPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            ImageOrientation.FlipVerticalTo(producedPng, outputPngPath);
        }
        finally
        {
            try { if (File.Exists(producedPng)) File.Delete(producedPng); } catch { }
            try { if (File.Exists(tempDds)) File.Delete(tempDds); } catch { }
        }
    }

    private record ProcessResult(int ExitCode, string Output);

    private async Task<ProcessResult> RunAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _texconvPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new TexconvException(
                $"Could not launch texconv.exe at '{_texconvPath}'.",
                ex.Message);
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> stderr = process.StandardError.ReadToEndAsync(ct);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var combined = new StringBuilder();
        string outText = await stdout.ConfigureAwait(false);
        string errText = await stderr.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(outText)) combined.AppendLine(outText.TrimEnd());
        if (!string.IsNullOrWhiteSpace(errText)) combined.AppendLine(errText.TrimEnd());

        combined.AppendLine();
        combined.AppendLine($"Command: \"{_texconvPath}\" {arguments}");

        return new ProcessResult(process.ExitCode, combined.ToString());
    }

    private static string TrimTrailingSlash(string dir)
        => dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
