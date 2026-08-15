using System.Globalization;
using System.Text.Json;

namespace ERSwapper.Core;

public class ParsedTexture
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int MipCount { get; set; }
    public int TextureFormat { get; set; }
    public ulong StreamOffset { get; set; }
    public uint StreamSize { get; set; }
    public uint CompleteImageSize { get; set; }

    public string StreamPath { get; set; } = "";

    public string SourceFile { get; set; } = "";

    public string DroppedFile { get; set; } = "";

    public string? ResolvedBundlePath { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? CabId =>
    CabIdentity.TryExtract(StreamPath)
    ?? CabIdentity.TryExtract(SourceFile)
    ?? CabIdentity.TryExtract(DroppedFile);

    [System.Text.Json.Serialization.JsonIgnore]
    public string? SignatureName =>
    CabId is null ? null : CabIdentity.SignatureNameFor(CabId);

    [System.Text.Json.Serialization.JsonIgnore]
    public string? DxgiFormat => UabeaDumpParser.MapTextureFormat(TextureFormat);

    [System.Text.Json.Serialization.JsonIgnore]
    public uint EffectiveSize => StreamSize > 0 ? StreamSize : CompleteImageSize;

    public string? GetBlocker()
    {
        if (Width <= 0 || Height <= 0)
            return "missing width/height";
        if (EffectiveSize == 0)
            return "no stream size — the texture may not be streamed from a .resS";
        if (StreamOffset == 0 && StreamSize == 0)
            return "no m_StreamData — texture data is embedded, not streamed";
        if (DxgiFormat is null)
            return $"unsupported m_TextureFormat {TextureFormat} (only DXT1/DXT3/DXT5 can be written)";

        return null;
    }

    public ItemPreset ToPreset(string category, string bundleRelativePath, string signaturePath)
    {
        string format = DxgiFormat ?? "BC1_UNORM";
        int mips = DdsHeaderBuilder.InferMipCount(format, Width, Height, EffectiveSize);
        if (mips == 0) mips = MipCount > 0 ? MipCount : 1;

        return new ItemPreset
        {
            DisplayName = FriendlyName(Name),
            Category = category,
            TextureObjectName = Name,

            BundleRelativePath = ResolvedBundlePath ?? bundleRelativePath,
            ResSSignatureSourcePath = SignatureName ?? signaturePath,
            StreamDataOffset = StreamOffset,
            StreamDataSize = EffectiveSize,
            Width = Width,
            Height = Height,
            MipCount = mips,
            DxgiFormat = format,
        };
    }

    public static string FriendlyName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return "Unnamed";

        string name = objectName;

        if (name.StartsWith("v_", StringComparison.OrdinalIgnoreCase)) name = name[2..];

        string[] suffixes = { "_combined_bc", "_bc", "_basecolor", "_albedo", "_diff", "_d" };
        foreach (string suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        name = name.Replace('_', ' ').Trim();
        if (name.Length == 0) return objectName;

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}

public static class UabeaDumpParser
{
    public static string? MapTextureFormat(int unityTextureFormat) => unityTextureFormat switch
    {
        10 => "BC1_UNORM",
        11 => "BC2_UNORM",
        12 => "BC3_UNORM",
        _ => null,
    };

    public static ParsedTexture Parse(string filePath)
    {
        string content = File.ReadAllText(filePath);

        ParsedTexture texture = LooksLikeJson(content)
            ? ParseJson(content)
            : ParseTextDump(content);

        texture.SourceFile = filePath;

        if (string.IsNullOrWhiteSpace(texture.Name))
            texture.Name = Path.GetFileNameWithoutExtension(filePath);

        return texture;
    }

    public const long WholeFileLimit = 64 * 1024;

    private const int HeadWindow = 8 * 1024;
    private const int TailWindow = 16 * 1024;
    private const int ExtendedTailWindow = 1024 * 1024;

    public static ParsedTexture ParseMetadata(string filePath)
    {
        long length = new FileInfo(filePath).Length;
        if (length <= WholeFileLimit) return Parse(filePath);

        using FileStream stream = File.OpenRead(filePath);

        string head = ReadAt(stream, 0, HeadWindow);
        string tail = ReadTail(stream, TailWindow);

        if (FindField(tail, "m_StreamData") < 0)
            tail = ReadTail(stream, ExtendedTailWindow);

        ParsedTexture texture = ParseWindows(head, tail);
        texture.SourceFile = filePath;

        if (string.IsNullOrWhiteSpace(texture.Name))
            texture.Name = Path.GetFileNameWithoutExtension(filePath);

        return texture;
    }

    private static string ReadAt(FileStream stream, long position, int count)
    {
        int size = (int)Math.Min(count, stream.Length - position);
        if (size <= 0) return "";

        byte[] buffer = new byte[size];
        stream.Position = position;

        int read = 0;
        while (read < size)
        {
            int step = stream.Read(buffer, read, size - read);
            if (step <= 0) break;
            read += step;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static string ReadTail(FileStream stream, int count) =>
        ReadAt(stream, Math.Max(0, stream.Length - count), count);

    private static ParsedTexture ParseWindows(string head, string tail)
    {
        var texture = new ParsedTexture
        {
            Name = FindString(head, "m_Name") ?? "",
            Width = (int)FindNumber(head, "m_Width"),
            Height = (int)FindNumber(head, "m_Height"),
            MipCount = (int)FindNumber(head, "m_MipCount"),
            TextureFormat = (int)FindNumber(head, "m_TextureFormat"),
            CompleteImageSize = (uint)FindNumber(head, "m_CompleteImageSize"),
        };

        int marker = LastFieldIndex(tail, "m_StreamData");
        if (marker >= 0)
        {
            string block = tail[marker..];
            texture.StreamOffset = FindNumber(block, "offset");
            texture.StreamSize = (uint)FindNumber(block, "size");
            texture.StreamPath = FindString(block, "path") ?? "";
        }

        return texture;
    }

    private static bool IsFieldChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int FindField(string text, string field, int from = 0)
    {
        int index = from;

        while (index <= text.Length - field.Length)
        {
            index = text.IndexOf(field, index, StringComparison.Ordinal);
            if (index < 0) return -1;

            int after = index + field.Length;

            bool startOk = index == 0 || !IsFieldChar(text[index - 1]);
            bool endOk = after >= text.Length || !IsFieldChar(text[after]);

            if (startOk && endOk) return after;

            index = after;
        }

        return -1;
    }

    private static int LastFieldIndex(string text, string field)
    {
        int found = -1;
        int at = 0;

        while (true)
        {
            int next = FindField(text, field, at);
            if (next < 0) return found;

            found = next;
            at = next;
        }
    }

    private static int SkipToValue(string text, int at)
    {
        if (at < text.Length && text[at] == '"') at++;
        while (at < text.Length && (text[at] == ' ' || text[at] == '\t')) at++;
        if (at < text.Length && (text[at] == ':' || text[at] == '=')) at++;
        while (at < text.Length && (text[at] == ' ' || text[at] == '\t')) at++;

        return at;
    }

    private static string? FindString(string text, string field)
    {
        int at = FindField(text, field);
        if (at < 0) return null;

        at = SkipToValue(text, at);
        if (at >= text.Length) return null;

        if (text[at] == '"')
        {
            int end = text.IndexOf('"', at + 1);
            return end < 0 ? null : text[(at + 1)..end];
        }

        int stop = at;
        while (stop < text.Length && text[stop] != ',' && text[stop] != '\n' && text[stop] != '}') stop++;

        return text[at..stop].Trim();
    }

    private static ulong FindNumber(string text, string field)
    {
        string? value = FindString(text, field);
        return value is null ? 0 : ParseULong(value);
    }

    private static bool LooksLikeJson(string content)
    {
        foreach (char c in content)
        {
            if (char.IsWhiteSpace(c)) continue;
            return c == '{' || c == '[';
        }

        return false;
    }

    private static ParsedTexture ParseTextDump(string content)
    {
        var texture = new ParsedTexture();

        bool inStreamData = false;
        int streamDataIndent = -1;

        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0) continue;

            if (!char.IsAsciiDigit(trimmed[0])) continue;

            int indent = line.Length - trimmed.Length;
            int equals = trimmed.IndexOf('=');

            if (equals < 0)
            {
                string? nodeField = LastToken(trimmed);
                if (nodeField is null) continue;

                if (nodeField == "m_StreamData")
                {
                    inStreamData = true;
                    streamDataIndent = indent;
                }
                else if (inStreamData && indent <= streamDataIndent)
                {
                    inStreamData = false;
                }

                continue;
            }

            string? name = LastToken(trimmed[..equals]);
            if (name is null) continue;

            string value = Unquote(trimmed[(equals + 1)..]);

            if (inStreamData && indent <= streamDataIndent) inStreamData = false;

            if (inStreamData)
            {
                switch (name)
                {
                    case "offset": texture.StreamOffset = ParseULong(value); break;
                    case "size": texture.StreamSize = (uint)ParseULong(value); break;
                    case "path": texture.StreamPath = value; break;
                }

                continue;
            }

            switch (name)
            {
                case "m_Name": texture.Name = value; break;
                case "m_Width": texture.Width = (int)ParseULong(value); break;
                case "m_Height": texture.Height = (int)ParseULong(value); break;
                case "m_MipCount": texture.MipCount = (int)ParseULong(value); break;
                case "m_TextureFormat": texture.TextureFormat = (int)ParseULong(value); break;
                case "m_CompleteImageSize": texture.CompleteImageSize = (uint)ParseULong(value); break;
            }
        }

        return texture;
    }

    private static string? LastToken(string text)
    {
        string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= 2 ? tokens[^1] : null;
    }

    private static ParsedTexture ParseJson(string content)
    {
        var texture = new ParsedTexture();

        using JsonDocument document = JsonDocument.Parse(content, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        JsonElement root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            root = root[0];

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The JSON dump does not contain a texture object.");

        texture.Name = GetString(root, "m_Name") ?? "";
        texture.Width = (int)GetNumber(root, "m_Width");
        texture.Height = (int)GetNumber(root, "m_Height");
        texture.MipCount = (int)GetNumber(root, "m_MipCount");
        texture.TextureFormat = (int)GetNumber(root, "m_TextureFormat");
        texture.CompleteImageSize = (uint)GetNumber(root, "m_CompleteImageSize");

        if (root.TryGetProperty("m_StreamData", out JsonElement stream)
            && stream.ValueKind == JsonValueKind.Object)
        {
            texture.StreamOffset = GetNumber(stream, "offset");
            texture.StreamSize = (uint)GetNumber(stream, "size");
            texture.StreamPath = GetString(stream, "path") ?? "";
        }

        return texture;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static ulong GetNumber(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value)) return 0;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetUInt64(out ulong n) ? n : 0,
            JsonValueKind.String => ParseULong(value.GetString() ?? ""),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0,
        };
    }

    private static string Unquote(string value)
    {
        value = value.Trim();

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            value = value[1..^1];

        return value.Trim();
    }

    private static ulong ParseULong(string value)
    {
        value = value.Trim();

        int space = value.IndexOf(' ');
        if (space > 0) value = value[..space];

        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed))
            return parsed;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double asDouble)
            && asDouble >= 0 && asDouble <= ulong.MaxValue)
        {
            return (ulong)asDouble;
        }

        return 0;
    }
}
