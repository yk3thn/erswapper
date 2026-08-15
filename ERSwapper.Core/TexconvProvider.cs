using System.Net.Http;

namespace ERSwapper.Core;

public class TexconvDownloadException : Exception
{
    public TexconvDownloadException(string message, Exception? inner = null) : base(message, inner) { }
}

public static class TexconvProvider
{
    public const string DownloadUrl =
        "https://github.com/yk3thn/erswapper/raw/refs/heads/main/texconv.exe";

    private const long MinimumPlausibleSize = 64 * 1024;

    private const long MaximumPlausibleSize = 32 * 1024 * 1024;

    public static string ExpectedPath => Path.Combine(AppPaths.AppDirectory, "texconv.exe");

    public static bool IsPresent => File.Exists(ExpectedPath);

    public static string? Locate()
    {
        if (IsPresent) return ExpectedPath;

        foreach (string candidate in new[]
        {
            Path.Combine(AppPaths.DefaultWorkspaceFolder, "texconv.exe"),
            Path.Combine(AppPaths.SeedConfigDirectory, "texconv.exe"),
        })
        {
            try { if (File.Exists(candidate)) return candidate; }
            catch { }
        }

        return null;
    }

    public static async Task<string> EnsureAsync(
        IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        string? existing = Locate();
        if (existing is not null) return existing;

        progress?.Report(new ScanProgress(0.05, "Downloading the texture converter…"));

        byte[] payload;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ERSwapper");

            payload = await client.GetByteArrayAsync(DownloadUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new TexconvDownloadException(
                "Could not download texconv.exe, the tool that converts textures.\r\n\r\n" +
                "Check your internet connection, or download it yourself and put it next to " +
                $"ERSwapper.exe:\r\n{DownloadUrl}", ex);
        }

        Validate(payload);

        string target = ExpectedPath;
        string temporary = target + ".part";

        try
        {
            await File.WriteAllBytesAsync(temporary, payload, ct).ConfigureAwait(false);

            if (File.Exists(target)) File.Delete(target);
            File.Move(temporary, target);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }

            throw new TexconvDownloadException(
                $"Downloaded texconv.exe but could not save it to:\r\n{target}\r\n\r\n" +
                "Move ER Swapper somewhere you can write to, such as your Desktop.", ex);
        }

        progress?.Report(new ScanProgress(0.1, "Texture converter ready."));

        return target;
    }

    public static void Validate(byte[] payload)
    {
        if (payload.Length < MinimumPlausibleSize || payload.Length > MaximumPlausibleSize)
        {
            throw new TexconvDownloadException(
                $"The download was {payload.Length:N0} bytes, which is not a usable texconv.exe. " +
                "Nothing was saved.");
        }

        if (payload.Length < 2 || payload[0] != (byte)'M' || payload[1] != (byte)'Z')
        {
            throw new TexconvDownloadException(
                "The download was not a Windows program — it was probably an error page. " +
                "Nothing was saved.");
        }
    }
}
