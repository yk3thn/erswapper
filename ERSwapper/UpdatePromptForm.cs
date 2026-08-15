namespace ERSwapper;

public enum UpdateChoice
{
    Later,
    Skip,
    Restarting,
}

public partial class UpdatePromptForm : Form
{
    private readonly UpdateInfo _info;
    private readonly UpdateInstaller _installer = new();
    private CancellationTokenSource? _cts;

    public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    public UpdatePromptForm(UpdateInfo info)
    {
        InitializeComponent();
        Theme.Apply(this);

        _info = info;

        lblVersions.ForeColor = Theme.TextMuted;
        lblStatus.ForeColor = Theme.TextMuted;

        lblVersions.Text = $"You have {AppVersion.Display} - {info.TagName} is available ({info.SizeText})";

        txtNotes.Text = string.IsNullOrWhiteSpace(info.Notes)
            ? $"{info.Title}\r\n\r\nNo release notes were provided."
            : $"{info.Title}\r\n\r\n{info.Notes.Replace("\n", "\r\n")}";

        txtNotes.Select(0, 0);
    }

    private async void btnUpdate_Click(object sender, EventArgs e)
    {
        SetBusy(true);
        _cts = new CancellationTokenSource();

        try
        {
            lblStatus.Text = "Downloading...";

            var progress = new Progress<double>(fraction =>
            {
                progressBar.Value = (int)Math.Round(Math.Clamp(fraction, 0, 1) * 1000);
                lblStatus.Text = $"Downloading... {fraction:P0}";
            });

            string zip = await _installer.DownloadAsync(_info, progress, _cts.Token);

            lblStatus.Text = "Unpacking...";
            string staged = _installer.Stage(zip);

            lblStatus.Text = "Restarting to finish the update...";
            _installer.LaunchAndExit(staged);

            Choice = UpdateChoice.Restarting;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException)
        {
            SetBusy(false);
            lblStatus.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            SetBusy(false);
            lblStatus.ForeColor = Theme.Danger;
            lblStatus.Text = "Update failed.";

            MessageBox.Show(this,
                "The update could not be installed:\r\n\r\n" + ex.Message +
                "\r\n\r\nYou can carry on using the current version.",
                "ER Swapper", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void btnSkip_Click(object sender, EventArgs e)
    {
        Choice = UpdateChoice.Skip;
        DialogResult = DialogResult.Ignore;
        Close();
    }

    private void btnLater_Click(object sender, EventArgs e)
    {
        Choice = UpdateChoice.Later;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void SetBusy(bool busy)
    {
        btnUpdate.Enabled = !busy;
        btnSkip.Enabled = !busy;
        btnLater.Enabled = !busy;
        progressBar.Visible = busy;
        progressBar.Maximum = 1000;
        if (busy) progressBar.Value = 0;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnFormClosing(e);
    }
}
