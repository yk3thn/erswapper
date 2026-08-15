namespace ERSwapper;

partial class LoadingForm
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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoadingForm));
        lblTitle = new Label();
        lblSubtitle = new Label();
        progressBar = new ProgressBar();
        lblStatus = new Label();
        btnAction = new Button();
        btnQuit = new Button();
        pictureBox1 = new PictureBox();
        ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
        SuspendLayout();

        lblTitle.Font = new Font("Segoe UI", 22F, FontStyle.Bold);
        lblTitle.Location = new Point(40, 44);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(440, 44);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "ER Swapper";

        lblSubtitle.Location = new Point(42, 90);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(440, 22);
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Rust texture swapper";

        progressBar.Location = new Point(42, 156);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(436, 12);
        progressBar.TabIndex = 2;

        lblStatus.Location = new Point(42, 178);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(436, 76);
        lblStatus.TabIndex = 3;
        lblStatus.Text = "Starting…";

        btnAction.Location = new Point(42, 262);
        btnAction.Name = "btnAction";
        btnAction.Size = new Size(200, 34);
        btnAction.TabIndex = 4;
        btnAction.Text = "Locate Rust folder…";
        btnAction.Visible = false;
        btnAction.Click += btnAction_Click;

        btnQuit.Location = new Point(388, 262);
        btnQuit.Name = "btnQuit";
        btnQuit.Size = new Size(90, 34);
        btnQuit.TabIndex = 5;
        btnQuit.Text = "Quit";
        btnQuit.Click += btnQuit_Click;

        pictureBox1.Image = Properties.Resources.ERSwapper;
        pictureBox1.Location = new Point(408, 15);
        pictureBox1.Name = "pictureBox1";
        pictureBox1.Size = new Size(100, 100);
        pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
        pictureBox1.TabIndex = 6;
        pictureBox1.TabStop = false;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 320);
        ControlBox = false;
        Controls.Add(pictureBox1);
        Controls.Add(btnQuit);
        Controls.Add(btnAction);
        Controls.Add(lblStatus);
        Controls.Add(progressBar);
        Controls.Add(lblSubtitle);
        Controls.Add(lblTitle);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Icon = (Icon)resources.GetObject("$this.Icon");
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoadingForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "ER Swapper";
        ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblSubtitle;
    private System.Windows.Forms.ProgressBar progressBar;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Button btnAction;
    private System.Windows.Forms.Button btnQuit;
    private PictureBox pictureBox1;
}
