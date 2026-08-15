namespace ERSwapper;

partial class UnsupportedListForm
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
        lstUnsupported = new ListView();
        colName = new ColumnHeader();
        colSize = new ColumnHeader();
        colReason = new ColumnHeader();
        lblCount = new Label();
        btnClose = new Button();
        SuspendLayout();
        lblIntro.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblIntro.Location = new Point(14, 12);
        lblIntro.Name = "lblIntro";
        lblIntro.Size = new Size(836, 56);
        lblIntro.TabIndex = 0;
        lblIntro.Text = "These textures cannot be swapped. Rust stores them in a compressed form this tool cannot rewrite safely.";
        lstUnsupported.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lstUnsupported.Columns.AddRange(new ColumnHeader[] { colName, colSize, colReason });
        lstUnsupported.FullRowSelect = true;
        lstUnsupported.Location = new Point(14, 74);
        lstUnsupported.MultiSelect = false;
        lstUnsupported.Name = "lstUnsupported";
        lstUnsupported.Size = new Size(836, 340);
        lstUnsupported.TabIndex = 1;
        lstUnsupported.UseCompatibleStateImageBehavior = false;
        lstUnsupported.View = View.Details;
        lstUnsupported.Resize += lstUnsupported_Resize;
        colName.Text = "Texture";
        colName.Width = 240;
        colSize.Text = "Size";
        colSize.Width = 110;
        colReason.Text = "Why it cannot be swapped";
        colReason.Width = 470;
        lblCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblCount.Location = new Point(14, 428);
        lblCount.Name = "lblCount";
        lblCount.Size = new Size(400, 24);
        lblCount.TabIndex = 2;
        btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClose.DialogResult = DialogResult.OK;
        btnClose.Location = new Point(740, 424);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(110, 32);
        btnClose.TabIndex = 3;
        btnClose.Text = "Close";
        AcceptButton = btnClose;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        CancelButton = btnClose;
        ClientSize = new Size(864, 470);
        Controls.Add(btnClose);
        Controls.Add(lblCount);
        Controls.Add(lstUnsupported);
        Controls.Add(lblIntro);
        MinimizeBox = false;
        MinimumSize = new Size(700, 420);
        Name = "UnsupportedListForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "ER Swapper — textures that cannot be swapped";
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label lblIntro;
    private System.Windows.Forms.ListView lstUnsupported;
    private System.Windows.Forms.ColumnHeader colName;
    private System.Windows.Forms.ColumnHeader colSize;
    private System.Windows.Forms.ColumnHeader colReason;
    private System.Windows.Forms.Label lblCount;
    private System.Windows.Forms.Button btnClose;
}
