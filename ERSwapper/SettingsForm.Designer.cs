namespace ERSwapper;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        lblRust = new Label();
        txtRust = new TextBox();
        btnBrowseRust = new Button();
        lblRustHint = new Label();
        lblConfig = new Label();
        txtConfig = new TextBox();
        btnBrowseConfig = new Button();
        btnResetConfig = new Button();
        lblConfigHint = new Label();
        lblVersion = new Label();
        btnUpdate = new Button();
        btnClearHistory = new Button();
        lblStatus = new Label();
        btnSave = new Button();
        btnCancel = new Button();
        SuspendLayout();

        lblRust.Location = new Point(16, 16);
        lblRust.Name = "lblRust";
        lblRust.Size = new Size(200, 20);
        lblRust.TabIndex = 0;
        lblRust.Text = "Rust folder";

        txtRust.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtRust.Location = new Point(16, 38);
        txtRust.Name = "txtRust";
        txtRust.Size = new Size(500, 23);
        txtRust.TabIndex = 1;

        btnBrowseRust.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowseRust.Location = new Point(524, 37);
        btnBrowseRust.Name = "btnBrowseRust";
        btnBrowseRust.Size = new Size(90, 26);
        btnBrowseRust.TabIndex = 2;
        btnBrowseRust.Text = "Browse…";
        btnBrowseRust.Click += btnBrowseRust_Click;

        lblRustHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblRustHint.Location = new Point(16, 64);
        lblRustHint.Name = "lblRustHint";
        lblRustHint.Size = new Size(598, 20);
        lblRustHint.TabIndex = 3;
        lblRustHint.Text = "The folder containing RustClient.exe. Found automatically unless Steam moved it.";

        lblConfig.Location = new Point(16, 96);
        lblConfig.Name = "lblConfig";
        lblConfig.Size = new Size(200, 20);
        lblConfig.TabIndex = 4;
        lblConfig.Text = "Config folder";

        txtConfig.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtConfig.Location = new Point(16, 118);
        txtConfig.Name = "txtConfig";
        txtConfig.Size = new Size(404, 23);
        txtConfig.TabIndex = 5;

        btnBrowseConfig.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowseConfig.Location = new Point(428, 117);
        btnBrowseConfig.Name = "btnBrowseConfig";
        btnBrowseConfig.Size = new Size(90, 26);
        btnBrowseConfig.TabIndex = 6;
        btnBrowseConfig.Text = "Browse…";
        btnBrowseConfig.Click += btnBrowseConfig_Click;

        btnResetConfig.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnResetConfig.Location = new Point(524, 117);
        btnResetConfig.Name = "btnResetConfig";
        btnResetConfig.Size = new Size(90, 26);
        btnResetConfig.TabIndex = 7;
        btnResetConfig.Text = "Default";
        btnResetConfig.Click += btnResetConfig_Click;

        lblConfigHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblConfigHint.Location = new Point(16, 144);
        lblConfigHint.Name = "lblConfigHint";
        lblConfigHint.Size = new Size(598, 20);
        lblConfigHint.TabIndex = 8;
        lblConfigHint.Text = "Where the item catalogue lives. Normally the Config folder next to ER Swapper.";

        lblVersion.Location = new Point(16, 186);
        lblVersion.Name = "lblVersion";
        lblVersion.Size = new Size(300, 22);
        lblVersion.TabIndex = 9;

        btnUpdate.Location = new Point(16, 212);
        btnUpdate.Name = "btnUpdate";
        btnUpdate.Size = new Size(190, 32);
        btnUpdate.TabIndex = 10;
        btnUpdate.Text = "Check for updates now";
        btnUpdate.Click += btnUpdate_Click;

        btnClearHistory.Location = new Point(214, 212);
        btnClearHistory.Name = "btnClearHistory";
        btnClearHistory.Size = new Size(210, 32);
        btnClearHistory.TabIndex = 11;
        btnClearHistory.Text = "Clear swap history";
        btnClearHistory.Click += btnClearHistory_Click;

        lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblStatus.Location = new Point(16, 252);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(598, 40);
        lblStatus.TabIndex = 12;

        btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnSave.Location = new Point(414, 300);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(96, 32);
        btnSave.TabIndex = 13;
        btnSave.Text = "Save";
        btnSave.Click += btnSave_Click;

        btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(518, 300);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(96, 32);
        btnCancel.TabIndex = 14;
        btnCancel.Text = "Cancel";

        AcceptButton = btnSave;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnCancel;
        ClientSize = new Size(630, 346);
        Controls.Add(lblRust);
        Controls.Add(txtRust);
        Controls.Add(btnBrowseRust);
        Controls.Add(lblRustHint);
        Controls.Add(lblConfig);
        Controls.Add(txtConfig);
        Controls.Add(btnBrowseConfig);
        Controls.Add(btnResetConfig);
        Controls.Add(lblConfigHint);
        Controls.Add(lblVersion);
        Controls.Add(btnUpdate);
        Controls.Add(btnClearHistory);
        Controls.Add(lblStatus);
        Controls.Add(btnSave);
        Controls.Add(btnCancel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "ER Swapper — settings";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label lblRust;
    private System.Windows.Forms.TextBox txtRust;
    private System.Windows.Forms.Button btnBrowseRust;
    private System.Windows.Forms.Label lblRustHint;
    private System.Windows.Forms.Label lblConfig;
    private System.Windows.Forms.TextBox txtConfig;
    private System.Windows.Forms.Button btnBrowseConfig;
    private System.Windows.Forms.Button btnResetConfig;
    private System.Windows.Forms.Label lblConfigHint;
    private System.Windows.Forms.Label lblVersion;
    private System.Windows.Forms.Button btnUpdate;
    private System.Windows.Forms.Button btnClearHistory;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Button btnSave;
    private System.Windows.Forms.Button btnCancel;
}
