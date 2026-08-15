using System.Text.RegularExpressions;

namespace ERSwapper.Core;

public static class CabIdentity
{
    private static readonly Regex CabPattern =
        new(@"CAB-[0-9A-Fa-f]{8,}", RegexOptions.Compiled);

    public static string? TryExtract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        Match match = CabPattern.Match(text);
        return match.Success ? match.Value : null;
    }

    public static string ResSNameFor(string cabId) => cabId + ".resS";

    public static string SignatureNameFor(string cabId) => cabId + ".resS.sig";

    public static string? TrySignatureNameFrom(string? text)
    {
        string? cab = TryExtract(text);
        return cab is null ? null : SignatureNameFor(cab);
    }
}
