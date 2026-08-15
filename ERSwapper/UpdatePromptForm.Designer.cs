namespace ERSwapper;

partial class UpdatePromptForm
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
        lblHeadline = new Label();
        lblVersions = new Label();
        txtNotes = new TextBox();
        progressBar = new ProgressBar();
        lblStatus = new Label();
        btnUpdate = new Button();
        btnLater = new Button();
        btnSkip = new Button();
        SuspendLayout();

        lblHeadline.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        lblHeadline.Location = new Point(20, 18);
        lblHeadline.Name = "lblHeadline";
        lblHeadline.Size = new Size(500, 28);
        lblHeadline.TabIndex = 0;
        lblHeadline.Text = "A new version is available";

        lblVersions.Location = new Point(22, 48);
        lblVersions.Name = "lblVersions";
        lblVersions.Size = new Size(500, 22);
        lblVersions.TabIndex = 1;
        lblVersions.Text = "You have v1.0.0 - v1.1.0 is available";

        txtNotes.Location = new Point(22, 80);
        txtNotes.Multiline = true;
        txtNotes.Name = "txtNotes";
        txtNotes.ReadOnly = true;
        txtNotes.ScrollBars = ScrollBars.Vertical;
        txtNotes.Size = new Size(496, 160);
        txtNotes.TabIndex = 2;

        progressBar.Location = new Point(22, 252);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(496, 10);
        progressBar.TabIndex = 3;
        progressBar.Visible = false;

        lblStatus.Location = new Point(22, 266);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(496, 22);
        lblStatus.TabIndex = 4;
        lblStatus.Text = "";

        btnUpdate.Location = new Point(22, 296);
        btnUpdate.Name = "btnUpdate";
        btnUpdate.Size = new Size(150, 34);
        btnUpdate.TabIndex = 5;
        btnUpdate.Text = "Update now";
        btnUpdate.Click += btnUpdate_Click;

        btnLater.Location = new Point(368, 296);
        btnLater.Name = "btnLater";
        btnLater.Size = new Size(150, 34);
        btnLater.TabIndex = 7;
        btnLater.Text = "Not now";
        btnLater.Click += btnLater_Click;

        btnSkip.Location = new Point(212, 296);
        btnSkip.Name = "btnSkip";
        btnSkip.Size = new Size(150, 34);
        btnSkip.TabIndex = 6;
        btnSkip.Text = "Skip this version";
        btnSkip.Click += btnSkip_Click;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(540, 348);
        Controls.Add(btnSkip);
        Controls.Add(btnLater);
        Controls.Add(btnUpdate);
        Controls.Add(lblStatus);
        Controls.Add(progressBar);
        Controls.Add(txtNotes);
        Controls.Add(lblVersions);
        Controls.Add(lblHeadline);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "UpdatePromptForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ER Swapper - update available";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label lblHeadline;
    private System.Windows.Forms.Label lblVersions;
    private System.Windows.Forms.TextBox txtNotes;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Button btnUpdate;
    private System.Windows.Forms.Button btnLater;
    private System.Windows.Forms.Button btnSkip;
}
