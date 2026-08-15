using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace ERSwapper;

public partial class MainForm : Form
{
    private const string AllCategories = "All items";

    private readonly AppSettings _settings;
    private readonly List<ItemPreset> _presets;
    private readonly ResSOffsetLocator _locator = new();

    private readonly ImageList _thumbnails = new()
    {
        ImageSize = new Size(ThumbnailCache.ThumbnailSize, ThumbnailCache.ThumbnailSize),
        ColorDepth = ColorDepth.Depth32Bit,
    };

    private string? _extractedPngPath;
    private ItemPreset? _extractedPreset;
    private CancellationTokenSource? _cts;

    public MainForm(StartupResult startup)
    {
        InitializeComponent();

        _settings = startup.Settings;
        _presets = startup.Presets;

        Theme.Apply(this);

        lblStatus.ForeColor = Theme.TextMuted;
        picPreview.BackColor = Theme.WindowBackground;
        lstCategories.BackColor = Theme.Surface;

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        components ??= new System.ComponentModel.Container();
        components.Add(_thumbnails);

        lstItems.LargeImageList = _thumbnails;
        lstItems.OwnerDraw = true;
        lstItems.DrawItem += lstItems_DrawItem;

        LoadThumbnails();
        PopulateBundleFilter();
        PopulateCategories();
        RebuildGallery(null);

        SetStatus($"{_presets.Count} item(s) ready. Pick one to swap its texture.");
    }

    private void PopulateCategories()
    {
        var categories = new List<string> { AllCategories };

        categories.AddRange(PresetStore.GroupForDisplay(_presets).Select(g => g.Key));

        lstCategories.BeginUpdate();
        lstCategories.Items.Clear();
        foreach (string category in categories) lstCategories.Items.Add(category);
        lstCategories.EndUpdate();

        lstCategories.SelectedIndex = 0;
    }

    private string SelectedCategory =>
        lstCategories.SelectedItem as string ?? AllCategories;

    private void lstCategories_SelectedIndexChanged(object sender, EventArgs e)
        => RebuildGallery(null);

    private void lstCategories_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        using (var background = new SolidBrush(selected ? Theme.SurfaceHover : Theme.Surface))
        {
            e.Graphics.FillRectangle(background, e.Bounds);
        }

        if (selected)
        {
            using var marker = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(marker, new Rectangle(e.Bounds.X, e.Bounds.Y + 4, 3, e.Bounds.Height - 8));
        }

        var textBounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);

        TextRenderer.DrawText(e.Graphics, lstCategories.Items[e.Index].ToString(), Font, textBounds,
            selected ? Theme.Text : Theme.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void LoadThumbnails()
    {
        foreach (ItemPreset preset in _presets)
        {
            string key = ThumbnailCache.KeyFor(preset);
            if (_thumbnails.Images.ContainsKey(key)) continue;

            Image? image = ThumbnailCache.Load(preset);
            if (image is null) continue;

            using (image) _thumbnails.Images.Add(key, image);
        }
    }

    private sealed class BundleOption
    {
        public string? Path { get; init; }
        public string Label { get; init; } = "";

        public override string ToString() => Label;
    }

    private bool _rebuildingBundleFilter;

    private string? SelectedBundleFilter => (cboBundle.SelectedItem as BundleOption)?.Path;

    private void PopulateBundleFilter()
    {
        var options = new List<BundleOption>
        {
            new() { Path = null, Label = $"All bundles ({_presets.Count})" },
        };

        foreach ((string bundle, int count) in ItemSearch.BundleBreakdown(_presets))
        {
            options.Add(new BundleOption
            {
                Path = bundle,
                Label = $"{BundleRegistry.LabelFor(bundle)}  ({count})",
            });
        }

        string? previous = SelectedBundleFilter;

        _rebuildingBundleFilter = true;
        try
        {
            cboBundle.BeginUpdate();
            cboBundle.Items.Clear();
            foreach (BundleOption option in options) cboBundle.Items.Add(option);
            cboBundle.EndUpdate();

            int index = options.FindIndex(o =>
                string.Equals(o.Path, previous, StringComparison.OrdinalIgnoreCase));

            cboBundle.SelectedIndex = index >= 0 ? index : 0;
        }
        finally
        {
            _rebuildingBundleFilter = false;
        }
    }

    private void cboBundle_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_rebuildingBundleFilter) return;

        RebuildGallery(SelectedPreset);
    }

    private void RebuildGallery(ItemPreset? toSelect)
    {
        string category = SelectedCategory;

        List<ItemPreset> visible = ItemSearch.FilterByBundle(_presets, SelectedBundleFilter);
        visible = ItemSearch.Filter(visible, txtSearch.Text);

        if (!string.Equals(category, AllCategories, StringComparison.Ordinal))
        {
            visible = visible
                .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        lstItems.BeginUpdate();
        lstItems.Items.Clear();

        foreach (ItemPreset preset in visible.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var item = new ListViewItem(preset.DisplayName) { Tag = preset };

            string key = ThumbnailCache.KeyFor(preset);
            if (_thumbnails.Images.ContainsKey(key)) item.ImageKey = key;

            lstItems.Items.Add(item);
        }

        lstItems.EndUpdate();

        cardGallery.Title = string.Equals(category, AllCategories, StringComparison.Ordinal)
            ? $"Items — {visible.Count}"
            : $"{category} — {visible.Count}";

        ListViewItem? target = lstItems.Items
            .Cast<ListViewItem>()
            .FirstOrDefault(i => ReferenceEquals(i.Tag, toSelect));

        if (target is not null)
        {
            target.Selected = true;
            target.EnsureVisible();
        }
        else
        {
            UpdateDetails(null);
        }
    }

    private ItemPreset? SelectedPreset =>
        lstItems.SelectedItems.Count > 0 ? lstItems.SelectedItems[0].Tag as ItemPreset : null;

    private void lstItems_SelectedIndexChanged(object sender, EventArgs e)
    {
        ItemPreset? preset = SelectedPreset;
        UpdateDetails(preset);

        if (!ReferenceEquals(preset, _extractedPreset)) ClearExtraction();
    }

    private void lstItems_DoubleClick(object sender, EventArgs e)
    {
        if (SelectedPreset is not null) btnExtract.PerformClick();
    }

    private void txtSearch_TextChanged(object sender, EventArgs e) => RebuildGallery(SelectedPreset);

    private void lstItems_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        if (e.Item is null) return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool selected = e.Item.Selected;
        Rectangle card = Rectangle.Inflate(e.Bounds, -4, -4);
        if (card.Width <= 0 || card.Height <= 0) return;

        using (var background = new SolidBrush(selected ? Theme.SurfaceHover : Theme.SurfaceAlt))
        {
            g.FillRectangle(background, card);
        }

        using (var border = new Pen(selected ? Theme.Accent : Theme.Border, selected ? 2f : 1f))
        {
            g.DrawRectangle(border, card);
        }

        const int labelHeight = 24;
        int imageSize = Math.Min(card.Width - 14, card.Height - labelHeight - 12);

        if (imageSize > 8)
        {
            Image? thumbnail = e.Item.ImageKey.Length > 0 ? _thumbnails.Images[e.Item.ImageKey] : null;

            var destination = new Rectangle(
                card.X + (card.Width - imageSize) / 2, card.Y + 7, imageSize, imageSize);

            if (thumbnail is not null)
            {
                g.DrawImage(thumbnail, destination);
            }
            else
            {
                using var brush = new SolidBrush(Theme.Surface);
                g.FillRectangle(brush, destination);
            }
        }

        var labelBounds = new Rectangle(card.X + 4, card.Bottom - labelHeight - 2, card.Width - 8, labelHeight);

        TextRenderer.DrawText(g, e.Item.Text, Font, labelBounds,
            selected ? Theme.Text : Theme.TextMuted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void UpdateDetails(ItemPreset? preset)
    {
        if (preset is null)
        {
            lblDetails.Text = "Pick an item from the gallery.";
            SetPreviewImage(null);
            return;
        }

        lblDetails.Text =
            $"{preset.DisplayName}\r\n" +
            $"{preset.Category}\r\n\r\n" +
            $"Texture size: {preset.Width} × {preset.Height}\r\n\r\n" +
            "1. Extract & Open in Editor\r\n" +
            "2. Edit the PNG and save it — keep the same size\r\n" +
            "3. Apply Edited Texture";

        string cached = ThumbnailCache.PathFor(preset);
        SetPreviewImage(File.Exists(cached) ? cached : null);
    }

    private async void btnExtract_Click(object sender, EventArgs e)
    {
        ItemPreset? preset = SelectedPreset;

        if (preset is null)
        {
            MessageBox.Show(this, "Pick an item first.", "ER Swapper",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true);

        try
        {
            var progress = new Progress<ScanProgress>(ReportProgress);

            string bundlePath = BundleLocator.Resolve(_settings.RustInstallPath, preset.BundleRelativePath);
            IReadOnlyList<string> signatures = AppPaths.GetSignatureCandidates(preset);

            SetStatus("Reading the current texture…");

            SignatureResolution resolution = await _locator
                .FindEntryStartAsync(bundlePath, signatures, progress, _cts.Token);

            long absoluteOffset = resolution.EntryStart + (long)preset.StreamDataOffset;

            byte[] raw = await BundlePatcher.ReadBytesAtAsync(
                bundlePath, absoluteOffset, (int)preset.StreamDataSize, _cts.Token);

            SetStatus("Converting to PNG…");

            string outputPng = Path.Combine(
                _settings.EffectiveExportFolder, $"ERSwapper_{SanitizeFileName(preset.DisplayName)}.png");

            var texconv = new TexconvWrapper(_settings.TexconvPath);
            await texconv.DecodeRawBytesToPngAsync(
                raw, outputPng, preset.Width, preset.Height, preset.MipCount, preset.DxgiFormat, _cts.Token);

            _extractedPngPath = outputPng;
            _extractedPreset = preset;

            SetPreviewImage(outputPng);
            btnApply.Enabled = true;
            SetProgress(1.0);
            SetStatus($"Saved to {outputPng} — edit it, save, then Apply Edited Texture.");

            OpenInDefaultEditor(outputPng);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            ShowError("Could not extract that texture", ex);
            SetStatus("Extract failed.");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async void btnApply_Click(object sender, EventArgs e)
    {
        ItemPreset? preset = _extractedPreset;
        string? pngPath = _extractedPngPath;

        if (preset is null || pngPath is null)
        {
            MessageBox.Show(this, "Extract a texture first.", "ER Swapper",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!File.Exists(pngPath))
        {
            MessageBox.Show(this,
                $"The extracted PNG is gone:\r\n{pngPath}\r\n\r\nExtract it again.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!RequireGameClosed()) return;

        string sourcePng;
        try
        {
            sourcePng = await PrepareSourcePngAsync(pngPath, preset);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
            return;
        }
        catch (Exception ex)
        {
            ShowError("Could not read that image", ex);
            return;
        }

        _cts = new CancellationTokenSource();
        SetBusy(true);

        try
        {
            var progress = new Progress<ScanProgress>(ReportProgress);

            string bundlePath = BundleLocator.Resolve(_settings.RustInstallPath, preset.BundleRelativePath);
            IReadOnlyList<string> signatures = AppPaths.GetSignatureCandidates(preset);

            SetStatus("Converting your image…");

            var texconv = new TexconvWrapper(_settings.TexconvPath);
            string ddsPath = await texconv.EncodePngToDdsAsync(
                sourcePng, AppPaths.TempDirectory, preset.DxgiFormat, preset.MipCount, _cts.Token);

            byte[] dds = await File.ReadAllBytesAsync(ddsPath, _cts.Token);
            byte[] payload = DdsHeaderBuilder.StripHeader(dds, preset.DxgiFormat);

            if (payload.Length != preset.StreamDataSize)
            {
                throw new PatchSafetyException(
                    "The converted image is the wrong size, so nothing was written.\r\n\r\n" +
                    $"Expected {preset.StreamDataSize:N0} bytes but got {payload.Length:N0}.\r\n\r\n" +
                    $"Make sure the image is exactly {preset.Width} × {preset.Height}.");
            }

            SignatureResolution resolution = await _locator
                .FindEntryStartAsync(bundlePath, signatures, progress, _cts.Token);

            long absoluteOffset = resolution.EntryStart + (long)preset.StreamDataOffset;

            if (_settings.KeepFullBundleBackups)
            {
                SetStatus("Backing up your game files…");

                await BundlePatcher.BackupIfNeededAsync(
                    bundlePath, BundlePatcher.GetBackupPath(bundlePath), progress, _cts.Token);
            }

            byte[] originalBytes = await BundlePatcher.ReadBytesAtAsync(
                bundlePath, absoluteOffset, (int)preset.StreamDataSize, _cts.Token);

            var confirm = MessageBox.Show(this,
                $"Apply your edited texture to {preset.DisplayName}?\r\n\r\n" +
                "The original texture is saved first, so you can put this one swap back at any time " +
                "from Swap History.",
                "ER Swapper — confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                SetStatus("Cancelled — nothing was written.");
                return;
            }

            if (!RequireGameClosed())
            {
                SetStatus("Cancelled — Rust is running.");
                return;
            }

            SetStatus("Applying…");
            await BundlePatcher.WriteBytesAtAsync(
                bundlePath, absoluteOffset, payload, (int)preset.StreamDataSize, _cts.Token);

            await SwapHistory.RecordAsync(
                preset, bundlePath, absoluteOffset, originalBytes, payload, sourcePng, _cts.Token);

            RefreshThumbnail(preset, sourcePng);

            SetProgress(1.0);
            SetStatus($"{preset.DisplayName} swapped.");

            MessageBox.Show(this,
                $"{preset.DisplayName} now uses your texture.\r\n\r\n" +
                "Swap History can put this one back on its own, or use Reset All Swaps for everything.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            ShowError("Could not apply that texture", ex);
            SetStatus("Apply failed.");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task<string> PrepareSourcePngAsync(string pngPath, ItemPreset preset)
    {
        (int width, int height) = await Task.Run(() =>
        {
            using Image image = LoadImageUnlocked(pngPath);
            return (image.Width, image.Height);
        });

        if (width == preset.Width && height == preset.Height) return pngPath;

        var choice = MessageBox.Show(this,
            $"Your image is {width} × {height}, but {preset.DisplayName} needs " +
            $"{preset.Width} × {preset.Height}.\r\n\r\n" +
            "Resize a copy and continue? Your file is left untouched.",
            "ER Swapper — wrong size",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (choice != DialogResult.Yes) throw new OperationCanceledException();

        string resizedPath = Path.Combine(AppPaths.TempDirectory, "erswapper_resized.png");

        await Task.Run(() =>
        {
            using Image source = LoadImageUnlocked(pngPath);
            using var resized = new Bitmap(preset.Width, preset.Height);

            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, 0, 0, preset.Width, preset.Height);
            }

            resized.Save(resizedPath, System.Drawing.Imaging.ImageFormat.Png);
        });

        return resizedPath;
    }

    private async void btnResetAll_Click(object sender, EventArgs e)
    {
        List<SwapRecord> history = SwapHistory.Load();
        var applied = new List<SwapRecord>();

        foreach (SwapRecord record in history.OrderByDescending(r => r.AppliedUtc))
        {
            SwapStatus status = await SwapHistory.DescribeAsync(record);
            if (status.CanRevert) applied.Add(record);
        }

        IReadOnlyList<string> backups = BundlePatcher.FindBackups(_settings.RustInstallPath);

        if (applied.Count == 0 && backups.Count == 0)
        {
            MessageBox.Show(this,
                "Nothing to reset — no textures are swapped right now.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!RequireGameClosed()) return;

        var confirm = MessageBox.Show(this,
            $"Put every swapped texture back to the original?\r\n\r\n" +
            $"{applied.Count} swap(s) will be undone." +
            (backups.Count > 0 ? $"\r\n{backups.Count} full bundle backup(s) will also be restored." : ""),
            "ER Swapper — reset everything",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes) return;

        _cts = new CancellationTokenSource();
        SetBusy(true);

        int restored = 0;
        var failures = new List<string>();

        try
        {
            for (int i = 0; i < applied.Count; i++)
            {
                ReportProgress(new ScanProgress(
                    (double)i / Math.Max(1, applied.Count),
                    $"Undoing swap {i + 1} of {applied.Count}…"));

                try
                {
                    await SwapHistory.RevertAsync(applied[i], progress: null, _cts.Token);
                    restored++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add($"  • {applied[i].DisplayName}: {FirstLineOf(ex.Message)}");
                }
            }

            foreach (string backup in backups)
            {
                string bundlePath = BundlePatcher.GetBundlePathFromBackup(backup);

                try
                {
                    if (!await BundlePatcher.IsBundleModifiedAsync(bundlePath, backup, _cts.Token)) continue;

                    ReportProgress(new ScanProgress(0.9, $"Restoring {Path.GetFileName(bundlePath)}…"));

                    await BundlePatcher.RestoreBackupAsync(bundlePath, backup, progress: null, _cts.Token);
                    restored++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add($"  • {Path.GetFileName(bundlePath)}: {FirstLineOf(ex.Message)}");
                }
            }

            SetProgress(1.0);
            SetStatus($"Reset complete — {restored} restored.");

            MessageBox.Show(this,
                failures.Count == 0
                    ? "Everything is back to the original."
                    : $"Restored {restored}.\r\n\r\nCould not restore:\r\n" + string.Join("\r\n", failures),
                "ER Swapper",
                MessageBoxButtons.OK,
                failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            SetStatus($"Cancelled after {restored} file(s).");
        }
        catch (Exception ex)
        {
            ShowError("Reset failed", ex);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void btnUnsupported_Click(object sender, EventArgs e)
    {
        using var dialog = new UnsupportedListForm();
        dialog.ShowDialog(this);
    }

    private void btnSettings_Click(object sender, EventArgs e)
    {
        using var dialog = new SettingsForm(_settings);

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        if (dialog.NeedsRestart)
        {
            MessageBox.Show(this,
                "The Config folder changed, so ER Swapper needs to restart to load the new catalogue.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        SetStatus("Settings saved.");
    }

    private void btnHistory_Click(object sender, EventArgs e)
    {
        using var dialog = new SwapHistoryForm(_settings);
        dialog.ShowDialog(this);
    }

    private async void btnResetPreviews_Click(object sender, EventArgs e)
    {
        var confirm = MessageBox.Show(this,
            "Rebuild every item preview?\r\n\r\n" +
            "The cached preview images are deleted and read from the game again. " +
            "Your game files and your swaps are not touched.",
            "ER Swapper — rebuild previews",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        ThumbnailCache.Clear();
        _thumbnails.Images.Clear();

        foreach (ListViewItem item in lstItems.Items) item.ImageKey = "";
        lstItems.Invalidate();

        var generator = new ThumbnailGenerator(_settings, _locator);

        if (!generator.CanGenerate)
        {
            SetStatus("Previews cleared, but the texture converter is unavailable so they cannot be rebuilt.");
            return;
        }

        ItemPreset? selected = SelectedPreset;

        _cts = new CancellationTokenSource();
        SetBusy(true);

        int built = 0;

        try
        {
            for (int i = 0; i < _presets.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                ReportProgress(new ScanProgress(
                    (double)i / _presets.Count,
                    $"Rebuilding previews… {i + 1} of {_presets.Count}"));

                try
                {
                    if (await generator.EnsureAsync(_presets[i], _cts.Token)) built++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }
            }

            LoadThumbnails();
            RebuildGallery(selected);

            SetProgress(1.0);
            SetStatus($"Rebuilt {built} of {_presets.Count} preview(s).");
        }
        catch (OperationCanceledException)
        {
            LoadThumbnails();
            RebuildGallery(selected);
            SetStatus("Preview rebuild stopped.");
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool RequireGameClosed()
    {
        IReadOnlyList<string> running = ProcessLockChecker.GetRunningGameProcesses();
        if (running.Count == 0) return true;

        MessageBox.Show(this,
            "Please close Rust first.\r\n\r\n" +
            "Windows locks the game's files while it is open, so nothing can be changed.",
            "ER Swapper — Rust is running",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);

        return false;
    }

    private void ClearExtraction()
    {
        _extractedPngPath = null;
        _extractedPreset = null;
        btnApply.Enabled = false;
    }

    private void RefreshThumbnail(ItemPreset preset, string pngPath)
    {
        try
        {
            ThumbnailCache.SaveFrom(pngPath, preset);

            string key = ThumbnailCache.KeyFor(preset);
            if (_thumbnails.Images.ContainsKey(key)) _thumbnails.Images.RemoveByKey(key);

            Image? refreshed = ThumbnailCache.Load(preset);
            if (refreshed is not null)
            {
                using (refreshed) _thumbnails.Images.Add(key, refreshed);
            }

            foreach (ListViewItem item in lstItems.Items)
            {
                if (ReferenceEquals(item.Tag, preset)) item.ImageKey = key;
            }

            lstItems.Invalidate();
            SetPreviewImage(ThumbnailCache.PathFor(preset));
        }
        catch
        {
        }
    }

    private void SetBusy(bool busy)
    {
        lstItems.Enabled = !busy;
        lstCategories.Enabled = !busy;
        txtSearch.Enabled = !busy;
        cboBundle.Enabled = !busy;
        btnExtract.Enabled = !busy;
        btnResetAll.Enabled = !busy;
        btnUnsupported.Enabled = !busy;
        btnApply.Enabled = !busy && _extractedPngPath is not null;

        if (busy) SetProgress(0);
        UseWaitCursor = busy;
    }

    private void ReportProgress(ScanProgress progress)
    {
        SetProgress(progress.Fraction);
        SetStatus(progress.Message);
    }

    private void SetProgress(double fraction)
    {
        progressBar.Maximum = 1000;
        progressBar.Value = (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * 1000);
    }

    private void SetStatus(string message) => lblStatus.Text = message;

    private void SetPreviewImage(string? pngPath)
    {
        Image? old = picPreview.Image;
        picPreview.Image = null;
        old?.Dispose();

        if (pngPath is null || !File.Exists(pngPath)) return;

        try { picPreview.Image = LoadImageUnlocked(pngPath); }
        catch { }
    }

    private static Image LoadImageUnlocked(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using var ms = new MemoryStream(bytes);
        using Image decoded = Image.FromStream(ms);
        return new Bitmap(decoded);
    }

    private void OpenInDefaultEditor(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus($"Saved to {path} (could not open it automatically: {ex.Message})");
        }
    }

    private void ShowError(string title, Exception ex)
    {
        string message = ex switch
        {
            UnauthorizedAccessException =>
                "Access to a game file was denied.\r\n\r\nClose Rust and Steam, then try again.",

            IOException io when (io.HResult & 0xFFFF) is 32 or 33 =>
                "A game file is in use.\r\n\r\nClose Rust and Steam, then try again.",

            _ => ex.Message,
        };

        MessageBox.Show(this, message, $"ER Swapper — {title}",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string FirstLineOf(string text)
    {
        int newline = text.IndexOfAny(new[] { '\r', '\n' });
        return newline < 0 ? text : text[..newline];
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
