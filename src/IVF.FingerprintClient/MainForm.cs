using System.Text.Json;
using IVF.FingerprintClient.Services;

namespace IVF.FingerprintClient;

/// <summary>
/// Main form for the IVF Fingerprint Client.
/// Runs in background (system tray) and opens CaptureForm when capture request is received.
/// </summary>
public partial class MainForm : Form
{
    private FingerprintHubService? _hubService;
    private readonly AppSettings _settings;
#pragma warning disable CS0414 // Field is assigned but never read - used for state tracking
    private bool _isCapturing;
#pragma warning restore CS0414
    private CaptureRequestDto? _currentRequest;
    private CaptureForm? _captureForm;
    private VerificationForm? _verificationForm;
    private TemplateCacheService? _cacheService;
    private IdentificationForm? _identificationForm;

    public MainForm()
    {
        InitializeComponent();
        _settings = LoadSettings();
    }

    private async void MainForm_Load(object sender, EventArgs e)
    {
        _hubService = new FingerprintHubService(_settings.HubUrl);
        // Extract base URL from Hub URL (approximate)
        var apiBaseUrl = _settings.HubUrl.Replace("/hubs/fingerprint", "");
        _cacheService = new TemplateCacheService(apiBaseUrl, _settings.ApiKey);
        
        // Subscribe to hub events
        _hubService.StatusChanged += HubService_StatusChanged;
        _hubService.ConnectionChanged += HubService_ConnectionChanged;
        _hubService.CaptureRequested += HubService_CaptureRequested;
        _hubService.CaptureRequested += HubService_CaptureRequested;
        _hubService.VerificationRequested += HubService_VerificationRequested;
        _hubService.IdentificationRequested += HubService_IdentificationRequested;
        _hubService.CaptureCancelled += HubService_CaptureCancelled;

        // Auto-connect if configured
        if (_settings.AutoConnect)
        {
            await ConnectToHub();
            // Start cache refresh in background
            _ = Task.Run(async () => {
                try 
                {
                    Invoke(() => UpdateStatus("Đang tải CSDL vân tay...", Color.Orange));
                    await _cacheService.RefreshCacheAsync();
                    Invoke(() => UpdateStatus($"Đã tải {_cacheService.TemplateCount} mẫu vân tay", Color.Green));
                }
                catch 
                {
                   // Invoke(() => UpdateStatus("Lỗi tải CSDL vân tay", Color.Red));
                }
            });
        }

        // Minimize to tray on startup if configured
        if (_settings.MinimizeToTray && _settings.AutoConnect)
        {
            Hide();
            notifyIcon.ShowBalloonTip(2000, "IVF Fingerprint Client", 
                "Ứng dụng đang chạy nền, chờ yêu cầu từ hệ thống", ToolTipIcon.Info);
        }
    }
// ...
    private void HubService_IdentificationRequested(object? sender, IdentificationRequestEventArgs e)
    {
        Invoke(() =>
        {
            notifyIcon.ShowBalloonTip(3000, "Yêu cầu định danh (1:N)",
                "Đang mở form định danh...",
                ToolTipIcon.Info);

            OpenIdentificationForm(e.Request);

            if (!Visible)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }
            BringToFront();
            FlashWindow();
        });
    }

    private void OpenIdentificationForm(IdentificationRequestDto request)
    {
        if (_identificationForm != null && !_identificationForm.IsDisposed)
        {
            _identificationForm.BringToFront();
            return;
        }
        
        if (_cacheService == null || !_cacheService.IsLoaded)
        {
            MessageBox.Show("Chưa tải cơ sở dữ liệu vân tay. Vui lòng chờ.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        lblPatientInfo.Text = "Đang định danh bệnh nhân...";
        Text = "IVF Fingerprint Client - Identification";

        _identificationForm = new IdentificationForm(_hubService!, _cacheService, request);
        _identificationForm.FormClosed += (s, args) => _identificationForm = null;
        
        // Handle auto-minimize on success
        _identificationForm.IdentificationCompleted += (s, success) =>
        {
            if (success && _settings.MinimizeToTray)
            {
                Invoke(() => 
                {
                    Task.Delay(1000).ContinueWith(_ => Invoke(() => 
                    {
                        Hide();
                        notifyIcon.ShowBalloonTip(2000, "IVF Fingerprint Client", 
                            "Định danh thành công. Ứng dụng đã ẩn xuống khay hệ thống.", ToolTipIcon.Info);
                    }));
                });
            }
        };

        _identificationForm.Show();
        _identificationForm.BringToFront();
    }
// ...

    private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            notifyIcon.ShowBalloonTip(2000, "IVF Fingerprint Client", 
                "Ứng dụng đang chạy trong khay hệ thống", ToolTipIcon.Info);
            return;
        }

        if (_hubService != null)
        {
            await _hubService.DisposeAsync();
        }
    }

    private async Task ConnectToHub()
    {
        UpdateStatus("Đang kết nối...", Color.Orange);
        btnConnect.Enabled = false;

        // Pass API key from settings for authentication
        var success = await _hubService!.ConnectAsync(_settings.ApiKey);

        if (success)
        {
            btnConnect.Enabled = false;
            btnDisconnect.Enabled = true;
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "IVF Fingerprint - Đã kết nối";
        }
        else
        {
            btnConnect.Enabled = true;
            notifyIcon.Text = "IVF Fingerprint - Chưa kết nối";
        }
    }

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        await ConnectToHub();
    }

    private async void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        await _hubService!.DisconnectAsync();
        btnConnect.Enabled = true;
        btnDisconnect.Enabled = false;
    }

    private async void BtnSimulateCapture_Click(object? sender, EventArgs e)
    {
        if (_currentRequest == null)
        {
            // Create test request for simulation
            _currentRequest = new CaptureRequestDto
            {
                PatientId = "SIMULATION-USER",
                FingerType = "RightIndex",
                RequestedAt = DateTime.UtcNow
            };
        }

        OpenCaptureForm(_currentRequest);
    }

    /// <summary>
    /// Opens the CaptureForm to start fingerprint capture.
    /// Called automatically when capture request is received from Angular UI.
    /// </summary>
    private void OpenCaptureForm(CaptureRequestDto request)
    {
        if (_captureForm != null && !_captureForm.IsDisposed)
        {
            _captureForm.BringToFront();
            return;
        }

        _isCapturing = true;
        lblPatientInfo.Text = $"Đang chụp vân tay cho: {request.PatientId}";
        
        // DEBUG: Show patient ID in title
        Text = $"IVF Fingerprint Client - Patient: {request.PatientId}";

        _captureForm = new CaptureForm(_hubService!, request.PatientId, request.FingerType);
        _captureForm.CaptureCompleted += (s, args) =>
        {
            Invoke(() =>
            {
                _isCapturing = false;
                _currentRequest = null;
                
                System.Diagnostics.Debug.WriteLine($"CaptureCompleted event received: Success={args.Success}");
                
                if (args.Success)
                {
                    lblPatientInfo.Text = $"✅ Đã hoàn thành cho: {request.PatientId}";
                    UpdateStatus("Đã gửi kết quả thành công", Color.Green);

                    // Minimize to tray if configured
                    if (_settings.MinimizeToTray)
                    {
                        Task.Delay(1000).ContinueWith(_ => Invoke(() => 
                        {
                            Hide();
                            notifyIcon.ShowBalloonTip(2000, "IVF Fingerprint Client", 
                                "Đã ẩn xuống khay hệ thống", ToolTipIcon.Info);
                        }));
                    }
                }
                else
                {
                    lblPatientInfo.Text = "Đang chờ yêu cầu từ hệ thống...";
                    UpdateStatus("Đã gửi kết quả thất bại", Color.Red);
                }
            });
        };

        _captureForm.FormClosed += (s, args) =>
        {
            _captureForm = null;
            _isCapturing = false;
        };

        _captureForm.Show();
        _captureForm.BringToFront();
    }

    // Event handlers
    private void HubService_StatusChanged(object? sender, string status)
    {
        Invoke(() => lblPatientInfo.Text = status);
    }

    private void HubService_ConnectionChanged(object? sender, bool connected)
    {
        Invoke(() =>
        {
            if (connected)
            {
                UpdateStatus("● Đã kết nối", Color.Green);
                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                notifyIcon.Text = "IVF Fingerprint - Đã kết nối";
            }
            else
            {
                UpdateStatus("● Chưa kết nối", Color.Gray);
                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;
                notifyIcon.Text = "IVF Fingerprint - Chưa kết nối";
            }
        });
    }

    private void HubService_CaptureRequested(object? sender, CaptureRequestEventArgs e)
    {
        Invoke(() =>
        {
            _currentRequest = e.Request;

            // Show notification
            notifyIcon.ShowBalloonTip(3000, "Yêu cầu chụp vân tay",
                $"Bệnh nhân: {e.Request.PatientId}\nNgón: {e.Request.FingerType}",
                ToolTipIcon.Info);

            // Auto-open capture form
            OpenCaptureForm(e.Request);

            // Show main form if hidden
            if (!Visible)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }
            BringToFront();
            FlashWindow();
        });
    }

    private void HubService_CaptureCancelled(object? sender, EventArgs e)
    {
        Invoke(() =>
        {
            _isCapturing = false;
            _currentRequest = null;
            
            if (_captureForm != null && !_captureForm.IsDisposed)
            {
                _captureForm.Close();
            }

            lblPatientInfo.Text = "Yêu cầu đã bị hủy";
        });
    }

    private void HubService_VerificationRequested(object? sender, VerificationRequestEventArgs e)
    {
        Invoke(() =>
        {
            notifyIcon.ShowBalloonTip(3000, "Yêu cầu xác thực vân tay",
                $"Bệnh nhân: {e.Request.PatientId}",
                ToolTipIcon.Info);

            OpenVerificationForm(e.Request);

            if (!Visible)
            {
                Show();
                WindowState = FormWindowState.Normal;
            }
            BringToFront();
            FlashWindow();
        });
    }

    private void OpenVerificationForm(VerificationRequestData request)
    {
        if (_verificationForm != null && !_verificationForm.IsDisposed)
        {
            _verificationForm.BringToFront();
            return;
        }

        lblPatientInfo.Text = $"Đang xác thực vân tay cho: {request.PatientId}";
        Text = $"IVF Fingerprint Client - Verify: {request.PatientId}";

        _verificationForm = new VerificationForm(_hubService!, request.PatientId, request.Templates);
        _verificationForm.VerificationCompleted += (s, result) =>
        {
            Invoke(() =>
            {
                if (result.Success)
                {
                    lblPatientInfo.Text = $"✅ Đã xác thực thành công cho: {result.PatientId}";
                    UpdateStatus("Xác thực thành công", Color.Green);

                    // Minimize to tray if configured
                    if (_settings.MinimizeToTray)
                    {
                        Task.Delay(1500).ContinueWith(_ => Invoke(() => 
                        {
                            Hide();
                            notifyIcon.ShowBalloonTip(2000, "IVF Fingerprint Client", 
                                "Đã ẩn xuống khay hệ thống", ToolTipIcon.Info);
                        }));
                    }
                }

                else
                {
                    lblPatientInfo.Text = "Xác thực thất bại/Hủy";
                    UpdateStatus("Xác thực thất bại", Color.Red);
                }
            });
        };

        _verificationForm.FormClosed += (s, args) =>
        {
            _verificationForm = null;
        };

        _verificationForm.Show();
        _verificationForm.BringToFront();
    }

    // System tray handlers
    private void ShowMenuItem_Click(object? sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
    }

    private async void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        if (_hubService != null)
        {
            await _hubService.DisposeAsync();
        }
        notifyIcon.Visible = false;
        Application.Exit();
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
    }

    // Helper methods
    private void UpdateStatus(string text, Color color)
    {
        lblConnectionStatus.Text = text;
        lblConnectionStatus.ForeColor = color;
    }

    private void FlashWindow()
    {
        var originalColor = BackColor;
        BackColor = Color.FromArgb(255, 235, 200);
        Task.Delay(200).ContinueWith(_ => 
        {
            if (!IsDisposed)
            {
                Invoke(() => BackColor = originalColor);
            }
        });
    }

    private static AppSettings LoadSettings()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }
}

public class AppSettings
{
    public string HubUrl { get; set; } = "http://localhost:5000/hubs/fingerprint";
    public string ApiKey { get; set; } = string.Empty;
    public bool AutoConnect { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
}
