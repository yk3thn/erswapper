using System.Reflection;

namespace ERSwapper.Core;

public static class AppVersion
{
    public static Version Current
    {
        get
        {
            Assembly? entry = Assembly.GetEntryAssembly();

            string? informational = entry?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (TryParse(informational, out Version? parsed)) return parsed!;

            return entry?.GetName().Version ?? new Version(0, 0, 0);
        }
    }

    public static string Display
    {
        get
        {
            Version version = Current;
            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public static bool TryParse(string? text, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[1..];

        int cut = trimmed.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut > 0) trimmed = trimmed[..cut];

        var digits = new List<int>();

        foreach (string part in trimmed.Split('.'))
        {
            if (!int.TryParse(part, out int value)) break;
            digits.Add(value);
            if (digits.Count == 4) break;
        }

        if (digits.Count == 0) return false;

        while (digits.Count < 3) digits.Add(0);

        version = new Version(digits[0], digits[1], digits[2]);
        return true;
    }
}
