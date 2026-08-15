using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ERSwapper.Core;

public record ReleaseAsset(string Name, string DownloadUrl, long Size);

public record UpdateInfo(Version Version, string TagName, string Title, string Notes, ReleaseAsset Asset)
{
    public string SizeText => Asset.Size > 0
        ? $"{Asset.Size / 1024.0 / 1024.0:N1} MB"
        : "unknown size";
}

public class UpdateChecker
{
    private const string AssetExtension = ".zip";

    private readonly string _owner;
    private readonly string _repository;

    public UpdateChecker(string owner, string repository)
    {
        _owner = owner;
        _repository = repository;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_owner)
        && !string.IsNullOrWhiteSpace(_repository)
        && !_owner.Contains("YOUR-", StringComparison.OrdinalIgnoreCase);

    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ERSwapper", AppVersion.Display.TrimStart('v')));

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        string url = $"https://api.github.com/repos/{_owner}/{_repository}/releases/latest";

        using HttpResponseMessage response = await client.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out JsonElement pre) && pre.GetBoolean()) return null;

        string tag = GetString(root, "tag_name") ?? "";
        if (!AppVersion.TryParse(tag, out Version? released) || released is null) return null;
        if (released <= current) return null;

        ReleaseAsset? asset = FindAsset(root);
        if (asset is null) return null;

        string title = GetString(root, "name") ?? tag;
        string notes = GetString(root, "body") ?? "";

        return new UpdateInfo(released, tag, title, notes, asset);
    }

    private static ReleaseAsset? FindAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        ReleaseAsset? fallback = null;

        foreach (JsonElement element in assets.EnumerateArray())
        {
            string name = GetString(element, "name") ?? "";
            string url = GetString(element, "browser_download_url") ?? "";

            if (url.Length == 0) continue;
            if (!name.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase)) continue;

            long size = element.TryGetProperty("size", out JsonElement sizeElement)
                        && sizeElement.TryGetInt64(out long parsed)
                ? parsed
                : 0;

            var candidate = new ReleaseAsset(name, url, size);

            if (name.StartsWith("ERSwapper", StringComparison.OrdinalIgnoreCase)) return candidate;

            fallback ??= candidate;
        }

        return fallback;
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
