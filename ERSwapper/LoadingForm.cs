namespace ERSwapper;

public partial class LoadingForm : Form
{
    private readonly StartupLoader _loader = new();
    private CancellationTokenSource? _cts;

    public StartupResult? Result { get; private set; }

    public LoadingForm()
    {
        InitializeComponent();
        Theme.Apply(this);

        lblSubtitle.ForeColor = Theme.TextMuted;
        lblStatus.ForeColor = Theme.TextMuted;
        lblTitle.ForeColor = Theme.Text;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        btnAction.Visible = false;
        lblStatus.ForeColor = Theme.TextMuted;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var progress = new Progress<ScanProgress>(report =>
        {
            progressBar.Value = (int)Math.Round(Math.Clamp(report.Fraction, 0, 1) * 1000);
            lblStatus.Text = report.Message;
        });

        progressBar.Maximum = 1000;

        try
        {
            Result = await _loader.RunAsync(progress, _cts.Token);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        catch (RustNotFoundException ex)
        {
            ShowRecoverable(ex.Message, "Locate Rust folder…");
        }
        catch (Exception ex)
        {
            ShowRecoverable(ex.Message, "Try again");
        }
    }

    private void ShowRecoverable(string message, string actionText)
    {
        lblStatus.ForeColor = Theme.Danger;
        lblStatus.Text = message;

        btnAction.Text = actionText;
        btnAction.Visible = true;

        progressBar.Value = 0;
    }

    private async void btnAction_Click(object sender, EventArgs e)
    {
        if (btnAction.Text.StartsWith("Locate", StringComparison.OrdinalIgnoreCase))
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select your Rust folder (the one containing RustClient.exe)",
                UseDescriptionForTitle = true,
            };

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (!RustInstallLocator.LooksLikeRustInstall(dialog.SelectedPath))
            {
                MessageBox.Show(this,
                    "That folder doesn't contain RustClient.exe.\r\n\r\n" +
                    "Pick the game's install folder — usually:\r\n" +
                    @"…\Steam\steamapps\common\Rust",
                    "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                return;
            }

            _loader.RustPathOverride = dialog.SelectedPath;
        }

        await StartAsync();
    }

    private void btnQuit_Click(object sender, EventArgs e)
    {
        _cts?.Cancel();
        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
