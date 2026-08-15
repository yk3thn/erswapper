namespace ERSwapper.Core;

public record DumpMatch(string DroppedFile, IReadOnlyList<string> Dumps, string? Problem)
{
    public bool Found => Dumps.Count > 0;
}

public static class DumpLookup
{
    public static readonly string[] DumpExtensions = { ".json", ".txt", ".dump" };

    public static bool IsDumpFile(string path)
    {
        string extension = Path.GetExtension(path);
        return DumpExtensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static DumpMatch FindFor(string droppedPath, string dumpFolder)
    {
        if (string.IsNullOrWhiteSpace(dumpFolder) || !Directory.Exists(dumpFolder))
        {
            return new DumpMatch(droppedPath, Array.Empty<string>(),
                $"Dump folder not found:\r\n{dumpFolder}\r\n\r\nSet it in Settings.");
        }

        foreach (string stem in CandidateStems(Path.GetFileNameWithoutExtension(droppedPath)))
        {
            if (stem.Length == 0) continue;

            foreach (string extension in DumpExtensions)
            {
                string exact = Path.Combine(dumpFolder, stem + extension);
                if (File.Exists(exact)) return new DumpMatch(droppedPath, new[] { exact }, null);
            }

            var prefixed = new List<string>();
            foreach (string extension in DumpExtensions)
            {
                try
                {
                    prefixed.AddRange(Directory.EnumerateFiles(
                        dumpFolder, EscapePattern(stem) + "-CAB-*" + extension, SearchOption.TopDirectoryOnly));
                }
                catch { }
            }

            if (prefixed.Count > 0)
            {
                prefixed.Sort(StringComparer.OrdinalIgnoreCase);
                return new DumpMatch(droppedPath, prefixed, null);
            }
        }

        return new DumpMatch(droppedPath, Array.Empty<string>(),
            $"No dump matching '{Path.GetFileNameWithoutExtension(droppedPath)}' was found in:\r\n{dumpFolder}");
    }

    private static IEnumerable<string> CandidateStems(string fileStem)
    {
        yield return fileStem;

        const string exportPrefix = "ERSwapper_";
        if (fileStem.StartsWith(exportPrefix, StringComparison.OrdinalIgnoreCase))
            yield return fileStem[exportPrefix.Length..];
    }

    private static string EscapePattern(string stem)
    => stem.Contains('*') || stem.Contains('?') ? Guid.NewGuid().ToString("N") : stem;
}
