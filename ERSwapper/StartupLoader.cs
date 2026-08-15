namespace ERSwapper;

public class RustNotFoundException : Exception
{
    public RustNotFoundException(string message) : base(message) { }
}

public record StartupResult(AppSettings Settings, List<ItemPreset> Presets);

public class StartupLoader
{
    private readonly ResSOffsetLocator _locator = new();

    public string? RustPathOverride { get; set; }

    public async Task<StartupResult> RunAsync(IProgress<ScanProgress> progress, CancellationToken ct)
    {
        progress.Report(new ScanProgress(0.01, "Checking your installation…"));

        ConfigIntegrity integrity = ConfigCheck.Inspect();

        if (!integrity.IsHealthy)
            throw new InvalidOperationException(ConfigCheck.DescribeProblem(integrity));

        ConfigMigrator.Run();
        ShippedAssets.SeedUserData();

        await ShippedAssets.EnsureTexconvAsync(progress, ct).ConfigureAwait(false);

        progress.Report(new ScanProgress(0.02, "Looking for your Rust installation…"));

        AppSettings settings = AppSettings.Load();
        settings.ApplyConfigFolder();

        if (!string.IsNullOrWhiteSpace(RustPathOverride))
        {
            settings.RustInstallPath = RustPathOverride;
            TrySave(settings);
        }

        if (string.IsNullOrWhiteSpace(settings.RustInstallPath) || !Directory.Exists(settings.RustInstallPath))
        {
            throw new RustNotFoundException(
                "Rust could not be found automatically.\r\n" +
                "Choose the folder containing RustClient.exe.");
        }

        progress.Report(new ScanProgress(0.05, "Loading items…"));

        string? catalogue = File.Exists(PresetStore.SeedPresetsPath) ? PresetStore.SeedPresetsPath : null;
        List<ItemPreset> presets = PresetStore.Load(out List<string> warnings, catalogue);

        if (presets.Count == 0)
        {
            throw new InvalidOperationException(
                "No items are available.\r\n\r\n" +
                (warnings.Count > 0 ? string.Join("\r\n", warnings) : "The item catalogue is empty."));
        }

        ThumbnailCache.PruneExcept(presets);

        List<BundleCoverage> uncovered = SignatureCoverage.FindUncovered(presets, settings.RustInstallPath);

        if (uncovered.Count > 0)
        {
            int usable = presets.Count - uncovered.Sum(b => b.ItemCount);

            if (usable == 0)
                throw new InvalidOperationException(SignatureCoverage.DescribeMissing(uncovered));

            var blocked = uncovered
                .Select(b => b.BundleRelativePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            presets = presets.Where(p => !blocked.Contains(p.BundleRelativePath)).ToList();

            progress.Report(new ScanProgress(0.06,
                $"Skipping {uncovered.Sum(b => b.ItemCount)} item(s) with no bundle signature."));
        }

        var bundles = presets
            .Select(p => p.BundleRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < bundles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            ItemPreset sample = presets.First(p =>
                string.Equals(p.BundleRelativePath, bundles[i], StringComparison.OrdinalIgnoreCase));

            string bundlePath = BundleLocator.Resolve(settings.RustInstallPath, bundles[i]);
            IReadOnlyList<string> signatures = AppPaths.GetSignatureCandidates(sample);

            double from = 0.08 + 0.32 * i / bundles.Count;
            double span = 0.32 / bundles.Count;

            var scoped = new Progress<ScanProgress>(p => progress.Report(
                new ScanProgress(from + span * p.Fraction, p.Message)));

            try
            {
                await _locator.FindEntryStartAsync(bundlePath, signatures, scoped, ct).ConfigureAwait(false);
            }
            catch (SignatureSearchException ex)
            {
                throw new InvalidOperationException(
                    $"Could not locate texture data in {bundles[i]}.\r\n\r\n{ex.Message}", ex);
            }
        }

        await GenerateThumbnailsAsync(settings, presets, progress, ct).ConfigureAwait(false);

        progress.Report(new ScanProgress(1.0, "Ready."));
        return new StartupResult(settings, presets);
    }

    private async Task GenerateThumbnailsAsync(
        AppSettings settings, List<ItemPreset> presets, IProgress<ScanProgress> progress, CancellationToken ct)
    {
        List<ItemPreset> missing = presets.Where(p => !ThumbnailCache.Exists(p)).ToList();

        if (missing.Count == 0)
        {
            progress.Report(new ScanProgress(0.95, "Item previews ready."));
            return;
        }

        var generator = new ThumbnailGenerator(settings, _locator);

        if (!generator.CanGenerate)
        {
            progress.Report(new ScanProgress(0.95, "Skipping previews — texture converter unavailable."));
            return;
        }

        for (int i = 0; i < missing.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            double fraction = 0.45 + 0.5 * i / missing.Count;
            progress.Report(new ScanProgress(
                fraction, $"Preparing previews… {i + 1} of {missing.Count}"));

            try
            {
                await generator.EnsureAsync(missing[i], ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }
    }

    private static void TrySave(AppSettings settings)
    {
        try { settings.Save(); } catch { }
    }
}
