namespace ERSwapper;

public partial class SettingsForm : Form
{
    public AppSettings Settings { get; }

    public bool NeedsRestart { get; private set; }

    public SettingsForm(AppSettings current)
    {
        InitializeComponent();
        Theme.Apply(this);

        lblRustHint.ForeColor = Theme.TextMuted;
        lblConfigHint.ForeColor = Theme.TextMuted;
        lblVersion.ForeColor = Theme.TextMuted;
        lblStatus.ForeColor = Theme.TextMuted;

        Settings = current;

        txtRust.Text = current.RustInstallPath;
        txtConfig.Text = current.EffectiveConfigFolder;

        lblVersion.Text = $"ER Swapper {AppVersion.Display}";

        if (!string.IsNullOrWhiteSpace(current.SkippedUpdateVersion))
            lblStatus.Text = $"You skipped {current.SkippedUpdateVersion}. Checking now offers it again.";
    }

    private void btnBrowseRust_Click(object sender, EventArgs e)
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Select your Rust folder (the one containing RustClient.exe)",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(txtRust.Text) ? txtRust.Text : "",
        };

        if (picker.ShowDialog(this) != DialogResult.OK) return;

        txtRust.Text = picker.SelectedPath;

        if (!RustInstallLocator.LooksLikeRustInstall(picker.SelectedPath))
        {
            Warn("That folder does not contain RustClient.exe. Swapping will not work until it does.");
        }
    }

    private void btnBrowseConfig_Click(object sender, EventArgs e)
    {
        using var picker = new FolderBrowserDialog
        {
            Description = "Select the Config folder holding the item catalogue",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(txtConfig.Text) ? txtConfig.Text : AppPaths.DefaultConfigDirectory,
        };

        if (picker.ShowDialog(this) != DialogResult.OK) return;

        txtConfig.Text = picker.SelectedPath;
    }

    private void btnResetConfig_Click(object sender, EventArgs e)
    {
        txtConfig.Text = AppPaths.DefaultConfigDirectory;
        lblStatus.ForeColor = Theme.TextMuted;
        lblStatus.Text = "Config folder set back to the one next to ER Swapper.";
    }

    private void btnUpdate_Click(object sender, EventArgs e)
    {
        UpdateFlow.ForgetSkippedVersion();

        btnUpdate.Enabled = false;
        UseWaitCursor = true;

        lblStatus.ForeColor = Theme.TextMuted;
        lblStatus.Text = "Checking for updates…";
        Application.DoEvents();

        try
        {
            UpdateOutcome outcome = UpdateFlow.Run(this, honourSkip: false, out string? version);

            lblStatus.ForeColor = outcome == UpdateOutcome.CheckFailed ? Theme.Danger : Theme.TextMuted;

            lblStatus.Text = outcome switch
            {
                UpdateOutcome.NotConfigured => "Updates are not set up in this build.",
                UpdateOutcome.CheckFailed => "Could not reach GitHub. Check your internet connection.",
                UpdateOutcome.UpToDate => $"You are on the latest version ({AppVersion.Display}).",
                UpdateOutcome.Skipped => $"{version} is available whenever you want it.",
                UpdateOutcome.Declined => $"{version} is available whenever you want it.",
                UpdateOutcome.Restarting => "Updating — ER Swapper will restart.",
                _ => "",
            };

            if (outcome == UpdateOutcome.Restarting) Close();
        }
        finally
        {
            UseWaitCursor = false;
            btnUpdate.Enabled = true;
        }
    }

    private void btnClearHistory_Click(object sender, EventArgs e)
    {
        List<SwapRecord> records = SwapHistory.Load();

        var applied = new List<SwapRecord>();

        foreach (SwapRecord record in records)
        {
            if (SwapHistory.DescribeAsync(record).GetAwaiter().GetResult().State == SwapState.Applied)
                applied.Add(record);
        }

        string warning = applied.Count > 0
            ? $"\r\n\r\n{applied.Count} swap(s) are still applied to your game. Clearing the history " +
              "throws away the only record of how to undo them — you would need Steam → Verify " +
              "Integrity of Game Files to get the originals back."
            : "";

        var confirm = MessageBox.Show(this,
            $"Delete the swap history? ({records.Count} entry(s))" + warning +
            "\r\n\r\nYour game files are not changed by this.",
            "ER Swapper — clear history",
            MessageBoxButtons.YesNo,
            applied.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes) return;

        int removed = 0;

        foreach (SwapRecord record in records)
        {
            SwapHistory.Forget(record);
            removed++;
        }

        try
        {
            if (Directory.Exists(SwapHistory.Directory)
                && !Directory.EnumerateFileSystemEntries(SwapHistory.Directory).Any())
            {
                Directory.Delete(SwapHistory.Directory);
            }
        }
        catch
        {
        }

        lblStatus.ForeColor = Theme.TextMuted;
        lblStatus.Text = $"Cleared {removed} history entry(s).";
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        string rust = txtRust.Text.Trim();
        string config = txtConfig.Text.Trim();

        if (!string.IsNullOrWhiteSpace(rust) && !Directory.Exists(rust))
        {
            Warn("That Rust folder does not exist.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(config) && !Directory.Exists(config))
        {
            Warn("That Config folder does not exist.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(config)
            && !File.Exists(Path.Combine(config, "presets.json")))
        {
            Warn("That folder has no presets.json in it, so there would be no items to show.");
            return;
        }

        bool configChanged = !string.Equals(
            config, Settings.EffectiveConfigFolder, StringComparison.OrdinalIgnoreCase);

        Settings.RustInstallPath = rust;

        Settings.ConfigFolder = string.Equals(
            config, AppPaths.DefaultConfigDirectory, StringComparison.OrdinalIgnoreCase)
            ? ""
            : config;

        try
        {
            Settings.Save();
        }
        catch (Exception ex)
        {
            Warn("Could not save your settings:\r\n\r\n" + ex.Message);
            return;
        }

        Settings.ApplyConfigFolder();
        NeedsRestart = configChanged;

        DialogResult = DialogResult.OK;
        Close();
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
