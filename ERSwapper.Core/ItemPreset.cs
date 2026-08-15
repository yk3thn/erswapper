using System.Text.Json.Serialization;

namespace ERSwapper.Core;

public class ItemPreset
{
    public string DisplayName { get; set; } = "";

    public string Category { get; set; } = DefaultCategory;

    public const string DefaultCategory = "Uncategorized";

    public static readonly string[] PreferredCategoryOrder =
{
        "Weapons", "Tools", "Medical", "Clothing", "Deployables", "Resources", "Other",
    };

    public const string FallbackCategory = "Other";

    public static readonly IReadOnlyDictionary<string, string> RenamedCategories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Custom"] = "Other",
        };

    public static string NormaliseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return DefaultCategory;

        string trimmed = category.Trim();

        return RenamedCategories.TryGetValue(trimmed, out string? renamed) ? renamed : trimmed;
    }

    public string TextureObjectName { get; set; } = "";

    public string BundleRelativePath { get; set; } = "";

    public string ResSSignatureSourcePath { get; set; } = "";

    public ulong StreamDataOffset { get; set; }

    public uint StreamDataSize { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public int MipCount { get; set; }

    public string DxgiFormat { get; set; } = "BC1_UNORM";

    public override string ToString() => DisplayName;

    [JsonIgnore]
    public long ExpectedMipChainSize =>
    DdsHeaderBuilder.ComputeMipChainSize(DxgiFormat, Width, Height, MipCount);

    public string? GetSizeAdvisory()
    {
        long expected = ExpectedMipChainSize;
        if (expected == 0 || expected == StreamDataSize) return null;

        return $"Preset '{DisplayName}' looks inconsistent.\r\n\r\n" +
               $"{Width}x{Height} {DxgiFormat} with {MipCount} mips comes to {expected:N0} bytes, " +
               $"but StreamDataSize is {StreamDataSize:N0}.\r\n\r\n" +
               "Encoding will almost certainly produce a payload the patcher refuses to write. " +
               "Re-check the width, height, mip count and format in UABEA.";
    }

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
            return "DisplayName is empty.";
        if (string.IsNullOrWhiteSpace(BundleRelativePath))
            return $"'{DisplayName}': BundleRelativePath is empty.";
        if (Width <= 0 || Height <= 0)
            return $"'{DisplayName}': Width/Height must be positive.";
        if (MipCount <= 0)
            return $"'{DisplayName}': MipCount must be positive.";
        if (StreamDataSize == 0)
            return $"'{DisplayName}': StreamDataSize is zero.";
        if (StreamDataSize > int.MaxValue)
            return $"'{DisplayName}': StreamDataSize {StreamDataSize} exceeds the 2GB single-read limit.";

        long minTopMip = (long)DdsHeaderBuilder.BlockCountForFormat(DxgiFormat, Width, Height);
        if (minTopMip > 0 && StreamDataSize < minTopMip)
            return $"'{DisplayName}': StreamDataSize {StreamDataSize} is smaller than a single " +
                   $"{Width}x{Height} {DxgiFormat} mip ({minTopMip} bytes). Check the values from UABEA.";

        return null;
    }
}
