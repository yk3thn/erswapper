namespace ERSwapper.Core;

partial class SwapHistoryForm
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
        lblIntro = new Label();
        lstSwaps = new ListView();
        colItem = new ColumnHeader();
        colWhen = new ColumnHeader();
        colBundle = new ColumnHeader();
        colStatus = new ColumnHeader();
        pnlPreview = new Panel();
        lblBeforeCaption = new Label();
        lblAfterCaption = new Label();
        picBefore = new PictureBox();
        picAfter = new PictureBox();
        lblDetail = new Label();
        pnlBottom = new Panel();
        lblDisk = new Label();
        chkKeepFullBackups = new CheckBox();
        btnRevert = new Button();
        btnCleanUp = new Button();
        btnBackups = new Button();
        btnClose = new Button();
        ((System.ComponentModel.ISupportInitialize)picBefore).BeginInit();
        ((System.ComponentModel.ISupportInitialize)picAfter).BeginInit();
        pnlPreview.SuspendLayout();
        pnlBottom.SuspendLayout();
        SuspendLayout();

        lblIntro.Dock = DockStyle.Top;
        lblIntro.Location = new Point(12, 12);
        lblIntro.Name = "lblIntro";
        lblIntro.Size = new Size(1000, 40);
        lblIntro.TabIndex = 0;
        lblIntro.Text =
            "Every swap you have made. Pick one and press Put Original Back to undo just that texture, "
            + "leaving your other swaps alone.";

        lstSwaps.Columns.AddRange(new[] { colItem, colWhen, colBundle, colStatus });
        lstSwaps.Dock = DockStyle.Fill;
        lstSwaps.FullRowSelect = true;
        lstSwaps.HideSelection = false;
        lstSwaps.Location = new Point(12, 52);
        lstSwaps.MultiSelect = false;
        lstSwaps.Name = "lstSwaps";
        lstSwaps.Size = new Size(1000, 320);
        lstSwaps.TabIndex = 1;
        lstSwaps.UseCompatibleStateImageBehavior = false;
        lstSwaps.View = View.Details;
        lstSwaps.SelectedIndexChanged += lstSwaps_SelectedIndexChanged;

        colItem.Text = "Item";
        colItem.Width = 240;

        colWhen.Text = "When";
        colWhen.Width = 150;

        colBundle.Text = "Bundle";
        colBundle.Width = 190;

        colStatus.Text = "Status";
        colStatus.Width = 416;

        pnlPreview.Controls.Add(lblDetail);
        pnlPreview.Controls.Add(picBefore);
        pnlPreview.Controls.Add(picAfter);
        pnlPreview.Controls.Add(lblBeforeCaption);
        pnlPreview.Controls.Add(lblAfterCaption);
        pnlPreview.Dock = DockStyle.Bottom;
        pnlPreview.Location = new Point(12, 372);
        pnlPreview.Name = "pnlPreview";
        pnlPreview.Size = new Size(1000, 180);
        pnlPreview.TabIndex = 2;

        lblBeforeCaption.Location = new Point(0, 6);
        lblBeforeCaption.Name = "lblBeforeCaption";
        lblBeforeCaption.Size = new Size(140, 20);
        lblBeforeCaption.TabIndex = 0;
        lblBeforeCaption.Text = "Before (original)";

        picBefore.Location = new Point(0, 28);
        picBefore.Name = "picBefore";
        picBefore.Size = new Size(140, 140);
        picBefore.SizeMode = PictureBoxSizeMode.Zoom;
        picBefore.TabIndex = 1;
        picBefore.TabStop = false;

        lblAfterCaption.Location = new Point(156, 6);
        lblAfterCaption.Name = "lblAfterCaption";
        lblAfterCaption.Size = new Size(140, 20);
        lblAfterCaption.TabIndex = 2;
        lblAfterCaption.Text = "After (yours)";

        picAfter.Location = new Point(156, 28);
        picAfter.Name = "picAfter";
        picAfter.Size = new Size(140, 140);
        picAfter.SizeMode = PictureBoxSizeMode.Zoom;
        picAfter.TabIndex = 3;
        picAfter.TabStop = false;

        lblDetail.Location = new Point(316, 28);
        lblDetail.Name = "lblDetail";
        lblDetail.Size = new Size(684, 140);
        lblDetail.TabIndex = 4;
        lblDetail.Text = "Select a swap to see what changed.";

        pnlBottom.Controls.Add(lblDisk);
        pnlBottom.Controls.Add(chkKeepFullBackups);
        pnlBottom.Controls.Add(btnRevert);
        pnlBottom.Controls.Add(btnCleanUp);
        pnlBottom.Controls.Add(btnBackups);
        pnlBottom.Controls.Add(btnClose);
        pnlBottom.Dock = DockStyle.Bottom;
        pnlBottom.Location = new Point(12, 552);
        pnlBottom.Name = "pnlBottom";
        pnlBottom.Size = new Size(1000, 88);
        pnlBottom.TabIndex = 3;

        lblDisk.Location = new Point(0, 4);
        lblDisk.Name = "lblDisk";
        lblDisk.Size = new Size(1000, 20);
        lblDisk.TabIndex = 0;

        chkKeepFullBackups.Location = new Point(0, 28);
        chkKeepFullBackups.Name = "chkKeepFullBackups";
        chkKeepFullBackups.Size = new Size(560, 22);
        chkKeepFullBackups.TabIndex = 1;
        chkKeepFullBackups.Text = "Also keep a full copy of each bundle before its first swap (uses many GB)";
        chkKeepFullBackups.CheckedChanged += chkKeepFullBackups_CheckedChanged;

        btnRevert.Enabled = false;
        btnRevert.Location = new Point(0, 54);
        btnRevert.Name = "btnRevert";
        btnRevert.Size = new Size(190, 32);
        btnRevert.TabIndex = 2;
        btnRevert.Text = "Put Original Back";
        btnRevert.Click += btnRevert_Click;

        btnCleanUp.Location = new Point(200, 54);
        btnCleanUp.Name = "btnCleanUp";
        btnCleanUp.Size = new Size(210, 32);
        btnCleanUp.TabIndex = 3;
        btnCleanUp.Text = "Remove finished entries";
        btnCleanUp.Click += btnCleanUp_Click;

        btnBackups.Location = new Point(420, 54);
        btnBackups.Name = "btnBackups";
        btnBackups.Size = new Size(230, 32);
        btnBackups.TabIndex = 4;
        btnBackups.Text = "Full bundle backups…";
        btnBackups.Click += btnBackups_Click;

        btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClose.DialogResult = DialogResult.OK;
        btnClose.Location = new Point(890, 54);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(110, 32);
        btnClose.TabIndex = 5;
        btnClose.Text = "Close";

        AcceptButton = btnClose;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnClose;
        ClientSize = new Size(1024, 652);
        Controls.Add(lstSwaps);
        Controls.Add(pnlPreview);
        Controls.Add(pnlBottom);
        Controls.Add(lblIntro);
        MinimizeBox = false;
        MinimumSize = new Size(900, 560);
        Name = "SwapHistoryForm";
        Padding = new Padding(12);
        StartPosition = FormStartPosition.CenterParent;
        Text = "ER Swapper — swap history";
        ((System.ComponentModel.ISupportInitialize)picBefore).EndInit();
        ((System.ComponentModel.ISupportInitialize)picAfter).EndInit();
        pnlPreview.ResumeLayout(false);
        pnlBottom.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label lblIntro;
    private System.Windows.Forms.ListView lstSwaps;
    private System.Windows.Forms.ColumnHeader colItem;
    private System.Windows.Forms.ColumnHeader colWhen;
    private System.Windows.Forms.ColumnHeader colBundle;
    private System.Windows.Forms.ColumnHeader colStatus;
    private System.Windows.Forms.Panel pnlPreview;
    private System.Windows.Forms.Label lblBeforeCaption;
    private System.Windows.Forms.Label lblAfterCaption;
    private System.Windows.Forms.PictureBox picBefore;
    private System.Windows.Forms.PictureBox picAfter;
    private System.Windows.Forms.Label lblDetail;
    private System.Windows.Forms.Panel pnlBottom;
    private System.Windows.Forms.Label lblDisk;
    private System.Windows.Forms.CheckBox chkKeepFullBackups;
    private System.Windows.Forms.Button btnRevert;
    private System.Windows.Forms.Button btnCleanUp;
    private System.Windows.Forms.Button btnBackups;
    private System.Windows.Forms.Button btnClose;
}
