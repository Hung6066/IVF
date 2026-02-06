namespace IVF.FingerprintClient;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        
        // Main Panel
        panelMain = new Panel();
        panelStatus = new Panel();
        panelCapture = new Panel();
        
        // Status controls
        lblConnectionStatus = new Label();
        lblPatientInfo = new Label();
        btnConnect = new Button();
        btnDisconnect = new Button();
        
        // Capture controls
        lblCaptureStatus = new Label();
        lblQuality = new Label();
        pictureBoxFingerprint = new PictureBox();
        btnSimulateCapture = new Button();
        progressBarCapture = new ProgressBar();
        
        // System tray
        notifyIcon = new NotifyIcon(components);
        contextMenuStrip = new ContextMenuStrip(components);
        showMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        
        // Panel Main
        panelMain.Dock = DockStyle.Fill;
        panelMain.Padding = new Padding(20);
        
        // Panel Status
        panelStatus.Dock = DockStyle.Top;
        panelStatus.Height = 120;
        panelStatus.Padding = new Padding(10);
        
        lblConnectionStatus.Text = "● Chưa kết nối";
        lblConnectionStatus.ForeColor = Color.Gray;
        lblConnectionStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblConnectionStatus.Location = new Point(10, 10);
        lblConnectionStatus.AutoSize = true;
        
        lblPatientInfo.Text = "Đang chờ yêu cầu từ hệ thống...";
        lblPatientInfo.Font = new Font("Segoe UI", 10F);
        lblPatientInfo.ForeColor = Color.DimGray;
        lblPatientInfo.Location = new Point(10, 40);
        lblPatientInfo.AutoSize = true;
        
        btnConnect.Text = "Kết nối";
        btnConnect.Size = new Size(100, 35);
        btnConnect.Location = new Point(10, 70);
        btnConnect.FlatStyle = FlatStyle.Flat;
        btnConnect.BackColor = Color.FromArgb(0, 102, 204);
        btnConnect.ForeColor = Color.White;
        btnConnect.Click += BtnConnect_Click;
        
        btnDisconnect.Text = "Ngắt kết nối";
        btnDisconnect.Size = new Size(100, 35);
        btnDisconnect.Location = new Point(120, 70);
        btnDisconnect.FlatStyle = FlatStyle.Flat;
        btnDisconnect.BackColor = Color.Gray;
        btnDisconnect.ForeColor = Color.White;
        btnDisconnect.Enabled = false;
        btnDisconnect.Click += BtnDisconnect_Click;
        
        panelStatus.Controls.AddRange(new Control[] { 
            lblConnectionStatus, lblPatientInfo, btnConnect, btnDisconnect 
        });
        
        // Panel Capture
        panelCapture.Dock = DockStyle.Fill;
        panelCapture.Padding = new Padding(10);
        
        ((System.ComponentModel.ISupportInitialize)pictureBoxFingerprint).BeginInit();
        pictureBoxFingerprint.Size = new Size(200, 250);
        pictureBoxFingerprint.Location = new Point(50, 20);
        pictureBoxFingerprint.BorderStyle = BorderStyle.FixedSingle;
        pictureBoxFingerprint.BackColor = Color.FromArgb(240, 240, 240);
        pictureBoxFingerprint.SizeMode = PictureBoxSizeMode.CenterImage;
        ((System.ComponentModel.ISupportInitialize)pictureBoxFingerprint).EndInit();
        
        lblCaptureStatus.Text = "Đặt ngón tay lên thiết bị để quét";
        lblCaptureStatus.Font = new Font("Segoe UI", 11F);
        lblCaptureStatus.ForeColor = Color.DimGray;
        lblCaptureStatus.Location = new Point(50, 280);
        lblCaptureStatus.AutoSize = true;
        
        progressBarCapture.Size = new Size(200, 10);
        progressBarCapture.Location = new Point(50, 310);
        progressBarCapture.Style = ProgressBarStyle.Marquee;
        progressBarCapture.Visible = false;
        
        lblQuality.Text = "Chất lượng: --";
        lblQuality.Font = new Font("Segoe UI", 10F);
        lblQuality.Location = new Point(50, 330);
        lblQuality.AutoSize = true;
        
        btnSimulateCapture.Text = "📌 Mô phỏng chụp";
        btnSimulateCapture.Size = new Size(150, 40);
        btnSimulateCapture.Location = new Point(75, 360);
        btnSimulateCapture.FlatStyle = FlatStyle.Flat;
        btnSimulateCapture.BackColor = Color.FromArgb(40, 167, 69);
        btnSimulateCapture.ForeColor = Color.White;
        btnSimulateCapture.Click += BtnSimulateCapture_Click;
        
        panelCapture.Controls.AddRange(new Control[] { 
            pictureBoxFingerprint, lblCaptureStatus, progressBarCapture, lblQuality, btnSimulateCapture 
        });
        
        // Context Menu
        showMenuItem.Text = "Hiển thị";
        showMenuItem.Click += ShowMenuItem_Click;
        exitMenuItem.Text = "Thoát";
        exitMenuItem.Click += ExitMenuItem_Click;
        contextMenuStrip.Items.AddRange(new ToolStripItem[] { showMenuItem, exitMenuItem });
        
        // Notify Icon
        notifyIcon.Icon = SystemIcons.Application;
        notifyIcon.Text = "IVF Fingerprint Client";
        notifyIcon.ContextMenuStrip = contextMenuStrip;
        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        notifyIcon.Visible = true;
        
        // Main Form
        panelMain.Controls.Add(panelCapture);
        panelMain.Controls.Add(panelStatus);
        Controls.Add(panelMain);
        
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(320, 450);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "IVF Fingerprint Client";
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
    }

    #endregion

    private Panel panelMain;
    private Panel panelStatus;
    private Panel panelCapture;
    private Label lblConnectionStatus;
    private Label lblPatientInfo;
    private Button btnConnect;
    private Button btnDisconnect;
    private Label lblCaptureStatus;
    private Label lblQuality;
    private PictureBox pictureBoxFingerprint;
    private Button btnSimulateCapture;
    private ProgressBar progressBarCapture;
    private NotifyIcon notifyIcon;
    private ContextMenuStrip contextMenuStrip;
    private ToolStripMenuItem showMenuItem;
    private ToolStripMenuItem exitMenuItem;
}
