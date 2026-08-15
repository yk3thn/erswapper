namespace ERSwapper.Core;

public partial class SwapHistoryForm : Form
{
    private readonly AppSettings _settings;

    private List<SwapRecord> _records = new();
    private readonly Dictionary<string, SwapStatus> _statuses = new();

    private bool _suspendSettingWrite;

    public SwapHistoryForm(AppSettings settings)
    {
        InitializeComponent();
        Theme.Apply(this);

        _settings = settings;

        lblIntro.ForeColor = Theme.TextMuted;
        lblDetail.ForeColor = Theme.TextMuted;
        lblDisk.ForeColor = Theme.TextMuted;
        lblBeforeCaption.ForeColor = Theme.TextMuted;
        lblAfterCaption.ForeColor = Theme.TextMuted;
        picBefore.BackColor = Theme.Surface;
        picAfter.BackColor = Theme.Surface;

        _suspendSettingWrite = true;
        chkKeepFullBackups.Checked = settings.KeepFullBundleBackups;
        _suspendSettingWrite = false;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        UseWaitCursor = true;

        try
        {
            _records = SwapHistory.Load()
                .OrderByDescending(r => r.AppliedUtc)
                .ToList();

            _statuses.Clear();

            foreach (SwapRecord record in _records)
                _statuses[record.Id] = await SwapHistory.DescribeAsync(record);

            FreeStaleBytes();
            Populate();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void FreeStaleBytes()
    {
        List<SwapRecord> all = SwapHistory.Load();
        bool changed = false;

        foreach (SwapRecord record in _records)
        {
            if (!_statuses.TryGetValue(record.Id, out SwapStatus? status)) continue;
            if (!SwapHistory.FreesDiskWhenStale(status.State)) continue;
            if (!record.HasOriginalBytes) continue;

            SwapHistory.DiscardOriginalBytes(record, all);
            changed = true;
        }

        if (changed) _records = SwapHistory.Load().OrderByDescending(r => r.AppliedUtc).ToList();
    }

    private void Populate()
    {
        lstSwaps.BeginUpdate();
        lstSwaps.Items.Clear();

        foreach (SwapRecord record in _records)
        {
            SwapStatus status = _statuses.TryGetValue(record.Id, out SwapStatus? s)
                ? s
                : new SwapStatus(SwapState.BundleMissing, "Unknown", false);

            var item = new ListViewItem(record.DisplayName) { Tag = record };

            item.SubItems.Add(record.AppliedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            item.SubItems.Add(BundleRegistry.LabelFor(record.BundleRelativePath));
            item.SubItems.Add(status.Summary);

            item.ForeColor = status.State == SwapState.Applied ? Theme.Text : Theme.TextMuted;

            lstSwaps.Items.Add(item);
        }

        lstSwaps.EndUpdate();

        UpdateDiskLine();
        UpdateSelection();
    }

    private void UpdateDiskLine()
    {
        int applied = _statuses.Values.Count(s => s.State == SwapState.Applied);
        long stored = SwapHistory.StoredBytes();

        lblDisk.Text =
            $"{_records.Count} swap(s) recorded, {applied} still applied. " +
            $"Original textures kept for undo: {BackupAudit.DescribeSize(stored)}.";
    }

    private SwapRecord? Selected =>
        lstSwaps.SelectedItems.Count == 1 ? lstSwaps.SelectedItems[0].Tag as SwapRecord : null;

    private void lstSwaps_SelectedIndexChanged(object sender, EventArgs e) => UpdateSelection();

    private void UpdateSelection()
    {
        SwapRecord? record = Selected;

        SetPicture(picBefore, record?.BeforeThumbnailPath);
        SetPicture(picAfter, record?.AfterThumbnailPath);

        if (record is null)
        {
            btnRevert.Enabled = false;
            lblDetail.Text = "Select a swap to see what changed.";
            return;
        }

        SwapStatus status = _statuses[record.Id];
        btnRevert.Enabled = status.CanRevert;

        lblDetail.Text =
            $"{record.TextureObjectName}\r\n" +
            $"{record.Width} x {record.Height}   {record.DxgiFormat}   " +
            $"{record.RegionSize:N0} bytes at offset {record.AbsoluteOffset:N0}\r\n" +
            $"{BundleRegistry.LabelFor(record.BundleRelativePath)}\r\n\r\n" +
            $"{status.Summary}." +
            (status.CanRevert
                ? "\r\n\r\nPut Original Back rewrites only this texture's bytes. Other swaps in the " +
                  "same bundle are untouched."
                : record.HasOriginalBytes
                    ? ""
                    : "\r\n\r\nThe stored original has been released to save disk.");
    }

    private static void SetPicture(PictureBox box, string? path)
    {
        Image? previous = box.Image;
        box.Image = null;
        previous?.Dispose();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            using Image decoded = Image.FromStream(stream);
            box.Image = new Bitmap(decoded);
        }
        catch
        {
        }
    }

    private async void btnRevert_Click(object sender, EventArgs e)
    {
        SwapRecord? record = Selected;
        if (record is null) return;

        IReadOnlyList<string> running = ProcessLockChecker.GetRunningGameProcesses();

        if (running.Count > 0)
        {
            MessageBox.Show(this,
                "Please close Rust first.\r\n\r\n" +
                "Windows locks the game's files while it is open, so nothing can be changed.",
                "ER Swapper — Rust is running", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            return;
        }

        var confirm = MessageBox.Show(this,
            $"Put the original {record.DisplayName} texture back?\r\n\r\n" +
            $"Only this texture's {record.RegionSize:N0} bytes are rewritten. " +
            "Any other swaps in the same bundle stay exactly as they are.",
            "ER Swapper — confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        UseWaitCursor = true;
        btnRevert.Enabled = false;

        try
        {
            await SwapHistory.RevertAsync(record);
            await ReloadAsync();

            MessageBox.Show(this,
                $"{record.DisplayName} is back to the original texture.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ER Swapper — nothing was written",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            await ReloadAsync();
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async void btnCleanUp_Click(object sender, EventArgs e)
    {
        List<SwapRecord> finished = _records
            .Where(r => _statuses.TryGetValue(r.Id, out SwapStatus? s) && s.State != SwapState.Applied)
            .ToList();

        if (finished.Count == 0)
        {
            MessageBox.Show(this,
                "Nothing to remove — every recorded swap is still applied.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return;
        }

        var confirm = MessageBox.Show(this,
            $"Remove {finished.Count} finished entry(s) from the list?\r\n\r\n" +
            "These are swaps that are no longer in the game — reverted here, put back by Steam, or " +
            "replaced by a newer swap. Your game files are not touched.",
            "ER Swapper — tidy up",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        foreach (SwapRecord record in finished) SwapHistory.Forget(record);

        await ReloadAsync();
    }

    private void btnBackups_Click(object sender, EventArgs e)
    {
        var states = _statuses.ToDictionary(kv => kv.Key, kv => kv.Value.State);
        List<BackupAuditEntry> entries = BackupAudit.Audit(_settings.RustInstallPath, _records, states);

        if (entries.Count == 0)
        {
            MessageBox.Show(this,
                "There are no full bundle backups taking up space.\r\n\r\n" +
                "Swaps are undone from the small per-texture originals listed here instead.",
                "ER Swapper — full bundle backups", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return;
        }

        List<BackupAuditEntry> removable = entries
            .Where(entry => entry.Verdict == BackupVerdict.NotNeeded)
            .ToList();

        string summary = string.Join("\r\n", entries.Select(entry =>
            $"  • {entry.BundleName}  ({BackupAudit.DescribeSize(entry.Bytes)}) — {entry.Reason}"));

        long reclaim = removable.Sum(entry => entry.Bytes);

        if (removable.Count == 0)
        {
            MessageBox.Show(this,
                $"{entries.Count} full bundle backup(s), " +
                $"{BackupAudit.DescribeSize(entries.Sum(entry => entry.Bytes))} in total:\r\n\r\n" +
                summary + "\r\n\r\nNone can be deleted safely right now.",
                "ER Swapper — full bundle backups", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return;
        }

        var confirm = MessageBox.Show(this,
            summary + "\r\n\r\n" +
            $"Delete the {removable.Count} backup(s) that are no longer needed and free " +
            $"{BackupAudit.DescribeSize(reclaim)}?\r\n\r\n" +
            "Your game files are not touched.",
            "ER Swapper — free up space",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        int deleted = BackupAudit.Delete(removable);

        MessageBox.Show(this,
            $"Deleted {deleted} backup(s), freeing {BackupAudit.DescribeSize(reclaim)}.",
            "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Information);

        UpdateDiskLine();
    }

    private void chkKeepFullBackups_CheckedChanged(object sender, EventArgs e)
    {
        if (_suspendSettingWrite) return;

        _settings.KeepFullBundleBackups = chkKeepFullBackups.Checked;

        try { _settings.Save(); } catch { }
    }
}
