using DPFP;
using DPFP.Capture;
using IVF.FingerprintClient.Services;

namespace IVF.FingerprintClient;

public partial class VerificationForm : Form, DPFP.Capture.EventHandler
{
    private DPFP.Capture.Capture? _capturer;
    private DPFP.Verification.Verification? _verificator;
    private readonly FingerprintHubService _hubService;
    private readonly string _patientId;
    private readonly List<FingerprintTemplateDto> _templates;

    public event EventHandler<VerificationResultDto>? VerificationCompleted;

    public VerificationForm(FingerprintHubService hubService, string patientId, List<FingerprintTemplateDto> templates)
    {
        InitializeComponent();
        _hubService = hubService;
        _patientId = patientId;
        _templates = templates;

        Text = $"Xác thực vân tay - Patient: {patientId}";
        AddLog($"Đã tải {templates.Count} mẫu vân tay cho xác thực");
    }

    private void VerificationForm_Load(object sender, EventArgs e)
    {
        InitializeCapture();
        StartCapture();
    }

    private void VerificationForm_FormClosed(object sender, FormClosedEventArgs e)
    {
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
            MessageBox.Show($"Lỗi khởi tạo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StartCapture()
    {
        if (_capturer != null)
        {
            try
            {
                _capturer.StartCapture();
                UpdatePrompt("Đặt ngón tay lên thiết bị để xác thực");
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
            try
            {
                _capturer.StopCapture();
            }
            catch { }
        }
    }

    #region DPFP.Capture.EventHandler

    public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
    {
        AddLog("Đã nhận mẫu vân tay");
        DrawFingerprint(Sample);
        ProcessSample(Sample);
    }

    public void OnFingerGone(object Capture, string ReaderSerialNumber)
    {
        // No op
    }

    public void OnFingerTouch(object Capture, string ReaderSerialNumber)
    {
        AddLog("Đã phát hiện ngón tay");
    }

    public void OnReaderConnect(object Capture, string ReaderSerialNumber)
    {
        UpdateStatus("Thiết bị sẵn sàng");
    }

    public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
    {
        UpdateStatus("Thiết bị ngắt kết nối");
    }

    public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback)
    {
        // No op
    }

    #endregion

    private async void ProcessSample(DPFP.Sample sample)
    {
        var features = ExtractFeatures(sample, DPFP.Processing.DataPurpose.Verification);

        if (features != null && _verificator != null)
        {
            var result = new DPFP.Verification.Verification.Result();
            FingerprintTemplateDto? matchedTemplate = null;

            AddLog("Đang so sánh vân tay...");

            var matches = new List<(FingerprintTemplateDto Template, int Score)>();

            foreach (var t in _templates)
            {
                try 
                {
                    AddLog($"So khớp mẫu type: {t.FingerType}...");
                    var bytes = Convert.FromBase64String(t.TemplateData);
                    var template = new DPFP.Template();
                    var templateCreated = false;
                    
                    try 
                    {
                        template.DeSerialize(bytes);
                        templateCreated = true;
                    }
                    catch
                    {
                        template = null;
                    }

                    if (template == null)
                    {
                        try 
                        {
                            var fs = new DPFP.FeatureSet();
                            fs.DeSerialize(bytes);
                            
                            var enroller = new DPFP.Processing.Enrollment();
                            for (int i = 0; i < 4; i++) enroller.AddFeatures(fs);
                            
                            if (enroller.TemplateStatus == DPFP.Processing.Enrollment.Status.Ready)
                            {
                                template = enroller.Template;
                                templateCreated = true;
                            }
                        }
                        catch { }
                    }

                    if (templateCreated && template != null)
                    {
                        _verificator.Verify(features, template, ref result);
                        
                        if (result.Verified)
                        {
                            AddLog($"-> MATCH CANDIDATE: {t.FingerType} (Score: {result.FARAchieved})");
                            matches.Add((t, result.FARAchieved));
                        }
                        else
                        {
                            AddLog($"-> No match: {t.FingerType} (Score: {result.FARAchieved})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"Lỗi xử lý mẫu {t.FingerType}: {ex.Message}");
                }
            }

            // Find best match (Lowest FARAchieved is better)
            if (matches.Any())
            {
                var bestMatch = matches.OrderBy(m => m.Score).First();
                matchedTemplate = bestMatch.Template;
                AddLog($"✅ KHỚP TỐT NHẤT: {matchedTemplate.FingerType} (Score: {bestMatch.Score})");
            }
            else
            {
                AddLog("❌ Không tìm thấy mẫu vân tay khớp");
            }

            if (matchedTemplate != null)
            {
                UpdateStatus($"✅ Xác thực: {matchedTemplate.FingerType}");
                await SendResultAsync(true, matchedTemplate.FingerType);
                Invoke(() => Close());
            }
            else
            {
                UpdateStatus("XXX Không khớp! Vui lòng thử lại");
            }
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

    private void DrawFingerprint(DPFP.Sample Sample)
    {
        var convertor = new DPFP.Capture.SampleConversion();
        Bitmap? bitmap = null;
        convertor.ConvertToPicture(Sample, ref bitmap);
        if (bitmap != null)
        {
            bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone); // Adjust as needed
            Invoke(() => pictureBoxFingerprint.Image = new Bitmap(bitmap, pictureBoxFingerprint.Size));
        }
    }

    private async Task SendResultAsync(bool success, string? fingerType = null, string? error = null)
    {
        try
        {
            var dto = new VerificationResultDto
            {
                PatientId = _patientId,
                Success = success,
                FingerType = fingerType,
                ErrorMessage = error,
                VerifiedAt = DateTime.UtcNow
            };

            await _hubService.SendVerificationResultAsync(dto);
            VerificationCompleted?.Invoke(this, dto);
        }
        catch (Exception ex)
        {
            AddLog($"Lỗi gửi kết quả: {ex.Message}");
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        StopCapture();
        _ = SendResultAsync(false, null, "Người dùng đã hủy");
        Close();
    }

    #region UI Updates
    private void UpdateStatus(string text)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatus(text)); return; }
        lblStatus.Text = text;
        
        // Color feedback
        if (text.Contains("thành công") || text.Contains("KHỚP"))
            lblStatus.ForeColor = Color.Green;
        else if (text.Contains("Không") || text.Contains("Lỗi"))
            lblStatus.ForeColor = Color.Red;
        else
            lblStatus.ForeColor = Color.DimGray;
    }

    private void UpdatePrompt(string text)
    {
        if (InvokeRequired) { Invoke(() => UpdatePrompt(text)); return; }
        lblPrompt.Text = text;
    }

    private void AddLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AddLog(message)); return; }
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
    }
    #endregion
}
