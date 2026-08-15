namespace ERSwapper.Core;

public static class BundleLocator
{
    private static readonly string[] SearchRoots =
{
        "",
        "Bundles",
        Path.Combine("Bundles", "Bundles"),
        Path.Combine("RustClient_Data", "Bundles"),
        Path.Combine("RustClient_Data", "StreamingAssets"),
        Path.Combine("RustClient_Data", "StreamingAssets", "Bundles"),
        Path.Combine("Rust_Data", "Bundles"),
        Path.Combine("Rust_Data", "StreamingAssets"),
        Path.Combine("Rust_Data", "StreamingAssets", "Bundles"),
    };

    public static string Resolve(string rustInstallPath, string bundleRelativePath)
    {
        if (string.IsNullOrWhiteSpace(rustInstallPath))
            throw new DirectoryNotFoundException("The Rust install path is not set. Open Settings to configure it.");

        if (!Directory.Exists(rustInstallPath))
            throw new DirectoryNotFoundException($"The configured Rust install folder does not exist:\r\n{rustInstallPath}");

        string relative = bundleRelativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var tried = new List<string>();

        foreach (string root in SearchRoots)
        {
            string candidate = Path.Combine(rustInstallPath, root, relative);
            tried.Add(candidate);
            if (File.Exists(candidate)) return candidate;
        }

        string fileName = Path.GetFileName(relative);
        try
        {
            List<string> found = Directory
                .EnumerateFiles(rustInstallPath, fileName, SearchOption.AllDirectories)
                .Take(2)
                .ToList();

            if (found.Count == 1) return found[0];

            if (found.Count > 1)
            {
                throw new FileNotFoundException(
                    $"'{relative}' was not found in any known bundle folder, and more than one file " +
                    $"named '{fileName}' exists elsewhere under the install.\r\n\r\n" +
                    "The correct one cannot be identified automatically. Set the preset's " +
                    "BundleRelativePath to the exact path under the install root.");
            }
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            tried.Add($"(recursive search failed: {ex.Message})");
        }

        throw new FileNotFoundException(
            $"Could not find '{relative}' under the Rust install.\r\n\r\nLocations checked:\r\n  "
            + string.Join("\r\n  ", tried));
    }
}
