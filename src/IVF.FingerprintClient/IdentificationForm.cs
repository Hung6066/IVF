using System.Windows.Forms;
using System.Drawing;
using DPFP;
using DPFP.Capture;
using IVF.FingerprintClient.Services;

namespace IVF.FingerprintClient;

public partial class IdentificationForm : Form, DPFP.Capture.EventHandler
{
    private DPFP.Capture.Capture? _capturer;
    private DPFP.Verification.Verification? _verificator;
    private readonly FingerprintHubService _hubService;
    private readonly TemplateCacheService _cacheService;
    private readonly IdentificationRequestDto _request;
    
    public event EventHandler<bool>? IdentificationCompleted;

    // UI Controls
    private Label lblStatus;
    private Label lblPrompt;
    private PictureBox pictureBoxFingerprint;
    private Button btnCancel;
    private TextBox txtLog;

    public IdentificationForm(FingerprintHubService hubService, TemplateCacheService cacheService, IdentificationRequestDto request)
    {
        _hubService = hubService;
        _cacheService = cacheService;
        _request = request;

        InitializeComponent();
        
        Text = "Định danh bệnh nhân (Identifcation)";
        AddLog($"Đang tìm kiếm trong {_cacheService.TemplateCount} mẫu vân tay...");
    }

    private void InitializeComponent()
    {
        // Simple UI Setup
        this.Size = new Size(500, 400);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.TopMost = true;

        lblPrompt = new Label { Text = "Đặt ngón tay để định danh", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        lblStatus = new Label { Text = "Sẵn sàng", Location = new Point(20, 50), AutoSize = true, ForeColor = Color.DimGray };
        
        pictureBoxFingerprint = new PictureBox { Location = new Point(20, 80), Size = new Size(150, 200), BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
        
        btnCancel = new Button { Text = "Hủy bỏ", Location = new Point(350, 320), Size = new Size(100, 30) };
        btnCancel.Click += (s, e) => { StopCapture(); Close(); };

        txtLog = new TextBox { Location = new Point(190, 80), Size = new Size(280, 200), Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true };

        this.Controls.Add(lblPrompt);
        this.Controls.Add(lblStatus);
        this.Controls.Add(pictureBoxFingerprint);
        this.Controls.Add(btnCancel);
        this.Controls.Add(txtLog);

        this.FormClosed += IdentificationForm_FormClosed;
        this.Load += IdentificationForm_Load;
    }

    private void IdentificationForm_Load(object? sender, EventArgs e)
    {
        _hubService.IdentificationResultReceived += OnIdentificationResult;
        InitializeCapture();
        StartCapture();
    }

    private void IdentificationForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _hubService.IdentificationResultReceived -= OnIdentificationResult;
        StopCapture();
    }

    private void InitializeCapture()
    {
        try
        {
            _capturer = new DPFP.Capture.Capture();
            _verificator = new DPFP.Verification.Verification();

            if (_capturer != null)
            {
                _capturer.EventHandler = this;
                AddLog("Thiết bị sẵn sàng");
            }
            else
            {
                UpdateStatus("Không tìm thấy thiết bị!");
            }
        }
        catch (Exception ex)
        {
            AddLog($"ERROR: {ex.Message}");
        }
    }

    private void StartCapture()
    {
        if (_capturer != null)
        {
            try
            {
                _capturer.StartCapture();
                UpdateStatus("Đang chờ quét...");
            }
            catch (Exception ex)
            {
                AddLog($"ERROR: {ex.Message}");
            }
        }
    }

    private void StopCapture()
    {
        if (_capturer != null)
        {
            try { _capturer.StopCapture(); } catch { }
        }
    }

    #region DPFP.Capture.EventHandler
    public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
    {
        AddLog("Đã nhận mẫu vân tay");
        DrawFingerprint(Sample);
        
        // Server-Side Matching Logic
        Invoke(() =>
        {
            UpdateStatus("Đang nhận diện trên server...");
            
            var features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Verification);

            if (features != null)
            {
                try 
                {
                    using var ms = new MemoryStream();
                    features.Serialize(ms);
                    var featuresBase64 = Convert.ToBase64String(ms.ToArray());
                    
                    var requesterId = _request?.RequestedBy;
                    
                    // Send to Server
                    _ = _hubService.IdentifyFingerprintAsync(featuresBase64, requesterId);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                }
            }
        });
    }

    public void OnFingerGone(object Capture, string ReaderSerialNumber) { }
    public void OnFingerTouch(object Capture, string ReaderSerialNumber) { AddLog("Đã phát hiện ngón tay"); }
    public void OnReaderConnect(object Capture, string ReaderSerialNumber) { UpdateStatus("Thiết bị sẵn sàng"); }
    public void OnReaderDisconnect(object Capture, string ReaderSerialNumber) { UpdateStatus("Thiết bị ngắt kết nối"); }
    public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback) { }
    #endregion

    private async void ProcessSample(DPFP.Sample sample)
    {
        var features = ExtractFeatures(sample, DPFP.Processing.DataPurpose.Verification);

        if (features != null && _verificator != null)
        {
            AddLog("Đang tìm kiếm trong CSDL...");
            
            // USE CACHE SERVICE TO FIND MATCH
            var result = _cacheService.FindBestMatch(features, _verificator);

            if (result.Match != null)
            {
                var patientId = result.Match.PatientId.ToString();
                AddLog($"✅ TÌM THẤY: {patientId} (Score: {result.Score})");
                UpdateStatus("Đã định danh thành công!");

                await SendResultAsync(true, patientId);
                
                // Auto-close on success
                Invoke(() => {
                    Task.Delay(1000).ContinueWith(_ => Invoke(() => Close()));
                });
            }
            else
            {
                AddLog("❌ Không tìm thấy mẫu khớp");
            }
        }
    }

    private DPFP.FeatureSet? ExtractFeatures(DPFP.Sample Sample, DPFP.Processing.DataPurpose Purpose)
    {
        var extractor = new DPFP.Processing.FeatureExtraction();
        var feedback = DPFP.Capture.CaptureFeedback.None;
        var features = new DPFP.FeatureSet();
        
        extractor.CreateFeatureSet(Sample, Purpose, ref feedback, ref features);
        
        if (feedback == DPFP.Capture.CaptureFeedback.Good)
            return features;
            
        return null;
    }
    
    // Listen for results from server
    private void OnIdentificationResult(object? sender, IdentificationResultDto result)
    {
        Invoke(() => 
        {
            if (result.Success)
            {
                UpdateStatus($"✅ TÌM THẤY! PatientID: {result.PatientId}");
                // Assuming lblMessage is lblStatus based on context
                lblStatus.ForeColor = Color.Green;
                
                // Minimize to allow user to see the Web App result immediately
                this.WindowState = FormWindowState.Minimized;

                // Close form after delay
                Task.Delay(2000).ContinueWith(_ => Invoke(Close));
                
                IdentificationCompleted?.Invoke(this, true);
            }
            else
            {
                UpdateStatus($"❌ {result.ErrorMessage ?? "Không tìm thấy kết quả"}");
                // Assuming lblMessage is lblStatus based on context
                lblStatus.ForeColor = Color.Red;
                
                IdentificationCompleted?.Invoke(this, false);
            }
        });
    }

    private void DrawFingerprint(DPFP.Sample Sample)
    {
        var convertor = new DPFP.Capture.SampleConversion();
        Bitmap? bitmap = null;
        convertor.ConvertToPicture(Sample, ref bitmap);
        if (bitmap != null)
        {
            bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
            Invoke(() => pictureBoxFingerprint.Image = new Bitmap(bitmap, pictureBoxFingerprint.Size));
        }
    }

    private async Task SendResultAsync(bool success, string? patientId, string? error = null)
    {
        try
        {
            var dto = new IdentificationResultDto
            {
                Success = success,
                PatientId = patientId,
                RequestedBy = _request.RequestedBy,
                ErrorMessage = error
            };

            await _hubService.SendIdentificationResultAsync(dto);
        }
        catch (Exception ex)
        {
            AddLog($"Lỗi gửi kết quả: {ex.Message}");
        }
    }

    private void UpdateStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatus(text)); return; }
        lblStatus.Text = text;
        if (text.Contains("thành công") || text.Contains("TÌM THẤY")) lblStatus.ForeColor = Color.Green;
        else if (text.Contains("Không") || text.Contains("Lỗi")) lblStatus.ForeColor = Color.Red;
        else lblStatus.ForeColor = Color.DimGray;
    }

    private void AddLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AddLog(message)); return; }
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
    }
}
