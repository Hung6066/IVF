using DPFP;
using DPFP.Capture;
using IVF.FingerprintClient.Services;

namespace IVF.FingerprintClient;

/// <summary>
/// Form for fingerprint capture using DigitalPersona SDK.
/// Implements DPFP.Capture.EventHandler for SDK event callbacks.
/// </summary>
public partial class CaptureForm : Form, DPFP.Capture.EventHandler
{
    private DPFP.Capture.Capture? _capturer;
    private DPFP.Processing.Enrollment? _enroller;
    private readonly FingerprintHubService _hubService;
    private readonly string _patientId;
    private readonly string _fingerType;
    private int _samplesCollected;

    public event EventHandler<CaptureCompletedEventArgs>? CaptureCompleted;

    public CaptureForm(FingerprintHubService hubService, string patientId, string fingerType)
    {
        InitializeComponent();
        _hubService = hubService;
        _patientId = patientId;
        _fingerType = fingerType;
        _samplesCollected = 0;
        
        // DEBUG: Show patient ID in title
        Text = $"Chụp vân tay - Patient: {patientId}";
    }

    private void CaptureForm_Load(object sender, EventArgs e)
    {
        InitializeCapture();
        StartCapture();
        UpdatePrompt($"Chụp vân tay: {_fingerType}");
        UpdateStatus("Đặt ngón tay lên thiết bị");
    }

    private void CaptureForm_FormClosed(object sender, FormClosedEventArgs e)
    {
        StopCapture();
    }

    private void InitializeCapture()
    {
        try
        {
            AddLog("Đang khởi tạo thiết bị quét vân tay...");
            _capturer = new DPFP.Capture.Capture();
            _enroller = new DPFP.Processing.Enrollment();

            if (_capturer != null)
            {
                _capturer.EventHandler = this;
                AddLog("Thiết bị đã được khởi tạo thành công");
            }
            else
            {
                UpdateStatus("Không thể khởi tạo thiết bị!");
                AddLog("ERROR: Capturer is null");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Lỗi khởi tạo: {ex.Message}\n\nChi tiết: {ex.ToString()}";
            AddLog($"ERROR: {errorMsg}");
            MessageBox.Show(errorMsg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartCapture()
    {
        if (_capturer != null)
        {
            try
            {
                AddLog("Đang bắt đầu quét...");
                _capturer.StartCapture();
                UpdatePrompt("Đặt ngón tay lên thiết bị quét vân tay");
                AddLog("Quét đã bắt đầu - chờ ngón tay");
                _ = SendStatusAsync("WaitingForFinger");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Không thể bắt đầu quét!\n\nLỗi: {ex.Message}\n\nKiểm tra:\n1. Thiết bị đọc vân tay đã được kết nối?\n2. Driver DigitalPersona đã được cài đặt?\n3. Kiểm tra Device Manager → Biometric Devices\n\nChi tiết: {ex.ToString()}";
                UpdateStatus("Không thể bắt đầu quét!");
                AddLog($"ERROR: {errorMsg}");
                MessageBox.Show(errorMsg, "Lỗi khởi động quét", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                // Send failure result with detailed error
                _ = Task.Run(async () =>
                {
                    var result = new CaptureResultDto
                    {
                        PatientId = _patientId,
                        Success = false,
                        ErrorMessage = $"Không thể khởi động quét: {ex.Message}",
                        CapturedAt = DateTime.UtcNow
                    };
                    await _hubService.SendCaptureResultAsync(result);
                });
                
                Close();
            }
        }
        else
        {
            AddLog("ERROR: Capturer is null, cannot start capture");
            MessageBox.Show("Thiết bị chưa được khởi tạo!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopCapture()
    {
        if (_capturer != null)
        {
            try
            {
                _capturer.StopCapture();
            }
            catch { }
        }
    }

    #region DPFP.Capture.EventHandler Implementation

    public async void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
    {
        AddLog("Đã nhận mẫu vân tay");
        DrawFingerprint(Sample);
        ProcessSample(Sample);
    }

    public void OnFingerGone(object Capture, string ReaderSerialNumber)
    {
        AddLog("Ngón tay đã rời khỏi thiết bị");
    }

    public void OnFingerTouch(object Capture, string ReaderSerialNumber)
    {
        AddLog("Đã phát hiện ngón tay");
        _ = SendStatusAsync("Capturing");
    }

    public async void OnReaderConnect(object Capture, string ReaderSerialNumber)
    {
        AddLog($"Thiết bị đã kết nối: {ReaderSerialNumber}");
        UpdateStatus("Thiết bị sẵn sàng");
    }

    public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
    {
        AddLog("Thiết bị đã ngắt kết nối");
        UpdateStatus("Thiết bị đã ngắt!");
    }

    public async void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback)
    {
        string quality = CaptureFeedback == DPFP.Capture.CaptureFeedback.Good ? "Good" : "Poor";
        AddLog($"Chất lượng mẫu: {quality}");

        try
        {
            await _hubService.SendSampleQualityAsync(_patientId, quality);
        }
        catch { }
    }

    #endregion

    private async void ProcessSample(DPFP.Sample Sample)
    {
        var features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Enrollment);

        if (features != null && _enroller != null)
        {
            try
            {
                _enroller.AddFeatures(features);
                _samplesCollected++;

                // Send progress update
                await SendEnrollmentProgressAsync(_samplesCollected, (int)_enroller.FeaturesNeeded + _samplesCollected);

                UpdateStatus($"Mẫu {_samplesCollected} đã thu thập");

                if (_enroller.TemplateStatus == DPFP.Processing.Enrollment.Status.Ready)
                {
                    // Enrollment complete!
                    await OnEnrollmentComplete();
                }
                else if (_enroller.TemplateStatus == DPFP.Processing.Enrollment.Status.Failed)
                {
                    UpdateStatus("Đăng ký thất bại - thử lại");
                    _enroller.Clear();
                    _samplesCollected = 0;
                }
                else
                {
                    UpdatePrompt($"Quét tiếp ({(int)_enroller.FeaturesNeeded} lần nữa)");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Lỗi: {ex.Message}");
            }
        }
        else
        {
            UpdateStatus("Mẫu không đủ chất lượng, thử lại");
        }
    }

    private async Task OnEnrollmentComplete()
    {
        StopCapture();
        UpdateStatus("✅ Hoàn thành!");
        UpdatePrompt("Đã đăng ký vân tay thành công!");
        AddLog("=== BẮT ĐẦU GỬI KẾT QUẢ ===");

        try
        {
            // Get the template data
            AddLog("Đang lấy template data...");
            var template = _enroller!.Template;
            var templateData = Convert.ToBase64String(template.Bytes);
            AddLog($"Template data length: {templateData.Length} characters");

            // Get fingerprint image
            string? imageData = null;
            // Note: Image data would need to be captured separately if needed

            // Send result via SignalR
            AddLog("Đang tạo CaptureResultDto...");
            var result = new CaptureResultDto
            {
                PatientId = _patientId,
                Success = true,
                TemplateData = templateData,
                FingerType = _fingerType,
                Quality = "95", // Enrollment requires multiple good samples
                ImageData = imageData,
                CapturedAt = DateTime.UtcNow
            };

            AddLog("Đang gửi kết quả qua SignalR...");
            await _hubService.SendCaptureResultAsync(result);
            AddLog("✅ Đã gửi CaptureResult thành công!");
            
            await SendStatusAsync("Completed");
            AddLog("✅ Đã gửi status Completed!");

            CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(true, templateData));
            AddLog("✅ Đã invoke CaptureCompleted event!");

            // Close form after short delay
            // AddLog("Đang đợi 0.5 giây trước khi đóng form...");
            // await Task.Delay(500);
            // AddLog("Đang đóng form...");
            Invoke(() => Close());
            AddLog("✅ Đã gọi Close()!");
        }
        catch (Exception ex)
        {
            var errorMsg = $"Lỗi gửi kết quả: {ex.Message}\n\nStack trace: {ex.StackTrace}";
            AddLog($"❌ ERROR: {errorMsg}");
            MessageBox.Show(errorMsg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private DPFP.FeatureSet? ExtractFeatures(DPFP.Sample Sample, DPFP.Processing.DataPurpose Purpose)
    {
        var extractor = new DPFP.Processing.FeatureExtraction();
        var feedback = DPFP.Capture.CaptureFeedback.None;
        var features = new DPFP.FeatureSet();

        extractor.CreateFeatureSet(Sample, Purpose, ref feedback, ref features);

        return feedback == DPFP.Capture.CaptureFeedback.Good ? features : null;
    }

    private Bitmap? ConvertSampleToBitmap(DPFP.Sample Sample)
    {
        try
        {
            var convertor = new DPFP.Capture.SampleConversion();
            Bitmap? bitmap = null;
            convertor.ConvertToPicture(Sample, ref bitmap);
            if (bitmap != null)
            {
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void DrawFingerprint(DPFP.Sample Sample)
    {
        var bitmap = ConvertSampleToBitmap(Sample);
        if (bitmap != null)
        {
            Invoke(() =>
            {
                pictureBoxFingerprint.Image = new Bitmap(bitmap, pictureBoxFingerprint.Size);
            });
        }
    }

    private async Task SendStatusAsync(string status)
    {
        try
        {
            await _hubService.SendCaptureStatusAsync(_patientId, status);
        }
        catch { }
    }

    private async Task SendEnrollmentProgressAsync(int collected, int needed)
    {
        try
        {
            await _hubService.SendEnrollmentProgressAsync(_patientId, collected, needed);
        }
        catch { }
    }

    #region UI Updates

    private void UpdateStatus(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateStatus(text));
            return;
        }
        lblStatus.Text = text;
    }

    private void UpdatePrompt(string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdatePrompt(text));
            return;
        }
        lblPrompt.Text = text;
    }

    private void AddLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AddLog(message));
            return;
        }
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
    }

    #endregion

    private void btnCancel_Click(object sender, EventArgs e)
    {
        StopCapture();

        // Send failure result
        _ = Task.Run(async () =>
        {
            var result = new CaptureResultDto
            {
                PatientId = _patientId,
                Success = false,
                ErrorMessage = "Người dùng đã hủy",
                CapturedAt = DateTime.UtcNow
            };
            await _hubService.SendCaptureResultAsync(result);
        });

        CaptureCompleted?.Invoke(this, new CaptureCompletedEventArgs(false, null));
        Close();
    }
}

public class CaptureCompletedEventArgs : EventArgs
{
    public bool Success { get; }
    public string? TemplateData { get; }

    public CaptureCompletedEventArgs(bool success, string? templateData)
    {
        Success = success;
        TemplateData = templateData;
    }
}
