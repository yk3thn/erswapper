namespace ERSwapper.Core;

public class ThumbnailGenerator
{
    private readonly AppSettings _settings;
    private readonly ResSOffsetLocator _locator;

    public ThumbnailGenerator(AppSettings settings, ResSOffsetLocator locator)
    {
        _settings = settings;
        _locator = locator;
    }

    public bool CanGenerate =>
    !string.IsNullOrWhiteSpace(_settings.RustInstallPath)
    && Directory.Exists(_settings.RustInstallPath)
    && TexconvProvider.Locate() is not null;

    public readonly record struct PreviewSlice(int MipIndex, long ByteOffset, int ByteLength, int Width, int Height);

    public static int PreviewMipIndex(string dxgiFormat, int width, int height, int mipCount)
    {
        if (DdsHeaderBuilder.BlockBytesForFormat(dxgiFormat) == 0) return 0;
        if (width <= 0 || height <= 0 || mipCount <= 0) return 0;

        int chosen = 0;

        for (int i = 0; i < mipCount; i++)
        {
            int w = Math.Max(1, width >> i);
            int h = Math.Max(1, height >> i);

            if (Math.Max(w, h) < ThumbnailCache.ThumbnailSize) break;

            chosen = i;
        }

        return chosen;
    }

    public static PreviewSlice ChoosePreviewSlice(ItemPreset preset)
    {
        var whole = new PreviewSlice(0, 0, (int)preset.StreamDataSize, preset.Width, preset.Height);

        int mip = PreviewMipIndex(preset.DxgiFormat, preset.Width, preset.Height, preset.MipCount);
        if (mip == 0) return whole;

        int mipWidth = Math.Max(1, preset.Width >> mip);
        int mipHeight = Math.Max(1, preset.Height >> mip);

        long offset = DdsHeaderBuilder.ComputeMipChainSize(preset.DxgiFormat, preset.Width, preset.Height, mip);
        long length = DdsHeaderBuilder.BlockCountForFormat(preset.DxgiFormat, mipWidth, mipHeight);

        if (offset <= 0 || length <= 0 || offset + length > preset.StreamDataSize) return whole;

        return new PreviewSlice(mip, offset, (int)length, mipWidth, mipHeight);
    }

    public async Task<bool> EnsureAsync(ItemPreset preset, CancellationToken ct = default)
    {
        if (ThumbnailCache.Exists(preset)) return true;
        if (!CanGenerate) return false;

        string bundlePath = BundleLocator.Resolve(_settings.RustInstallPath, preset.BundleRelativePath);
        IReadOnlyList<string> signatures = AppPaths.GetSignatureCandidates(preset);

        SignatureResolution resolution = await _locator
            .FindEntryStartAsync(bundlePath, signatures, progress: null, ct)
            .ConfigureAwait(false);

        PreviewSlice slice = ChoosePreviewSlice(preset);

        long absoluteOffset = resolution.EntryStart + (long)preset.StreamDataOffset + slice.ByteOffset;

        byte[] raw = await BundlePatcher
            .ReadBytesAtAsync(bundlePath, absoluteOffset, slice.ByteLength, ct)
            .ConfigureAwait(false);

        string tempPng = Path.Combine(
            AppPaths.TempDirectory, $"erswapper_thumb_{Guid.NewGuid():N}.png");

        try
        {
            var texconv = new TexconvWrapper(_settings.TexconvPath);

            await texconv.DecodeRawBytesToPngAsync(
                raw, tempPng, slice.Width, slice.Height, 1, preset.DxgiFormat, ct)
                .ConfigureAwait(false);

            ThumbnailCache.SaveFrom(tempPng, preset);
            return true;
        }
        finally
        {
            try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch { }
        }
    }
}
