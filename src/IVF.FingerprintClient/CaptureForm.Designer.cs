namespace IVF.FingerprintClient;

partial class CaptureForm
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
        panelMain = new Panel();
        panelTop = new Panel();
        panelCenter = new Panel();
        panelBottom = new Panel();

        lblPrompt = new Label();
        lblStatus = new Label();
        pictureBoxFingerprint = new PictureBox();
        txtLog = new TextBox();
        btnCancel = new Button();
        progressBar = new ProgressBar();

        // Panel Main
        panelMain.Dock = DockStyle.Fill;
        panelMain.Padding = new Padding(15);

        // Panel Top
        panelTop.Dock = DockStyle.Top;
        panelTop.Height = 80;
        panelTop.Padding = new Padding(10);

        lblPrompt.Text = "Đặt ngón tay lên thiết bị quét";
        lblPrompt.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        lblPrompt.ForeColor = Color.FromArgb(0, 102, 204);
        lblPrompt.Dock = DockStyle.Top;
        lblPrompt.TextAlign = ContentAlignment.MiddleCenter;
        lblPrompt.Height = 35;

        lblStatus.Text = "Đang chờ...";
        lblStatus.Font = new Font("Segoe UI", 10F);
        lblStatus.ForeColor = Color.DimGray;
        lblStatus.Dock = DockStyle.Top;
        lblStatus.TextAlign = ContentAlignment.MiddleCenter;
        lblStatus.Height = 25;

        panelTop.Controls.Add(lblStatus);
        panelTop.Controls.Add(lblPrompt);

        // Panel Center
        panelCenter.Dock = DockStyle.Fill;
        panelCenter.Padding = new Padding(20);

        ((System.ComponentModel.ISupportInitialize)pictureBoxFingerprint).BeginInit();
        pictureBoxFingerprint.Size = new Size(180, 220);
        pictureBoxFingerprint.Location = new Point(60, 10);
        pictureBoxFingerprint.BorderStyle = BorderStyle.FixedSingle;
        pictureBoxFingerprint.BackColor = Color.FromArgb(245, 245, 245);
        pictureBoxFingerprint.SizeMode = PictureBoxSizeMode.CenterImage;
        ((System.ComponentModel.ISupportInitialize)pictureBoxFingerprint).EndInit();

        progressBar.Size = new Size(180, 8);
        progressBar.Location = new Point(60, 240);
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.MarqueeAnimationSpeed = 30;

        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Size = new Size(260, 100);
        txtLog.Location = new Point(20, 260);
        txtLog.Font = new Font("Consolas", 8F);
        txtLog.BackColor = Color.FromArgb(250, 250, 250);

        panelCenter.Controls.Add(pictureBoxFingerprint);
        panelCenter.Controls.Add(progressBar);
        panelCenter.Controls.Add(txtLog);

        // Panel Bottom
        panelBottom.Dock = DockStyle.Bottom;
        panelBottom.Height = 60;
        panelBottom.Padding = new Padding(10);

        btnCancel.Text = "❌ Hủy";
        btnCancel.Size = new Size(120, 40);
        btnCancel.Location = new Point(90, 10);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.BackColor = Color.FromArgb(220, 53, 69);
        btnCancel.ForeColor = Color.White;
        btnCancel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        btnCancel.Click += btnCancel_Click;

        panelBottom.Controls.Add(btnCancel);

        // Assemble
        panelMain.Controls.Add(panelCenter);
        panelMain.Controls.Add(panelBottom);
        panelMain.Controls.Add(panelTop);

        Controls.Add(panelMain);

        // Form settings
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(300, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "CaptureForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Chụp vân tay";
        TopMost = true;
        Load += CaptureForm_Load;
        FormClosed += CaptureForm_FormClosed;
    }

    #endregion

    private Panel panelMain;
    private Panel panelTop;
    private Panel panelCenter;
    private Panel panelBottom;
    private Label lblPrompt;
    private Label lblStatus;
    private PictureBox pictureBoxFingerprint;
    private TextBox txtLog;
    private Button btnCancel;
    private ProgressBar progressBar;
}
