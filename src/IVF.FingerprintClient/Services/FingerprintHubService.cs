using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace IVF.FingerprintClient.Services;

/// <summary>
/// Service for connecting to the IVF API FingerprintHub via SignalR.
/// </summary>
public class FingerprintHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;
    private string? _currentPatientId;

    public event EventHandler<CaptureRequestEventArgs>? CaptureRequested;
    public event EventHandler<VerificationRequestEventArgs>? VerificationRequested;
    public event EventHandler? CaptureCancelled;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public string? CurrentPatientId => _currentPatientId;

    /// <summary>
    /// Send verification result to the Angular UI.
    /// </summary>
    public async Task SendVerificationResultAsync(VerificationResultDto result)
    {
        if (_hubConnection is null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to hub");
        }

        try
        {
            StatusChanged?.Invoke(this, $"Gửi verification result cho patient: {result.PatientId}");
            await _hubConnection.SendAsync("SendVerificationResult", result);
            StatusChanged?.Invoke(this, result.Success ? "Đã gửi verification thành công" : "Đã gửi verification thất bại");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Lỗi gửi verification result: {ex.Message}");
            throw;
        }
    }

    public FingerprintHubService(string hubUrl)
    {
        _hubUrl = hubUrl;
    }

    /// <summary>
    /// Connect to the FingerprintHub with API key for desktop client authentication.
    /// </summary>
    public async Task<bool> ConnectAsync(string? apiKey = null)
    {
        try
        {
            var builder = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    // Add API key to query string for desktop client authentication
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        options.Headers.Add("X-API-Key", apiKey);
                        // Also add to query string as backup
                        var uriBuilder = new UriBuilder(_hubUrl);
                        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                        query["apiKey"] = apiKey;
                        uriBuilder.Query = query.ToString();
                        options.Url = uriBuilder.Uri;
                    }
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                });

            _hubConnection = builder.Build();

            SetupEventHandlers();

            await _hubConnection.StartAsync();
            StatusChanged?.Invoke(this, "Đã kết nối đến server");
            ConnectionChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Lỗi kết nối: {ex.Message}");
            ConnectionChanged?.Invoke(this, false);
            return false;
        }
    }

    /// <summary>
    /// Disconnect from the hub.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            StatusChanged?.Invoke(this, "Đã ngắt kết nối");
            ConnectionChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Join a patient capture group to listen for capture requests.
    /// </summary>
    public async Task JoinPatientCaptureAsync(string patientId)
    {
        if (_hubConnection is null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to hub");
        }

        await _hubConnection.InvokeAsync("JoinPatientCapture", patientId);
        _currentPatientId = patientId;
        StatusChanged?.Invoke(this, $"Đã tham gia nhóm bệnh nhân: {patientId}");
    }

    /// <summary>
    /// Leave a patient capture group.
    /// </summary>
    public async Task LeavePatientCaptureAsync(string patientId)
    {
        if (_hubConnection is null || !IsConnected) return;

        await _hubConnection.InvokeAsync("LeavePatientCapture", patientId);
        if (_currentPatientId == patientId)
        {
            _currentPatientId = null;
        }
    }

    /// <summary>
    /// Send capture result to the Angular UI.
    /// </summary>
    public async Task SendCaptureResultAsync(CaptureResultDto result)
    {
        if (_hubConnection is null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to hub");
        }

        try
        {
            StatusChanged?.Invoke(this, $"Gửi result cho patient: {result.PatientId}");
            // Use SendAsync (fire-and-forget) instead of InvokeAsync since hub method returns void
            await _hubConnection.SendAsync("SendCaptureResult", result);
            StatusChanged?.Invoke(this, result.Success ? "Đã gửi kết quả thành công" : "Đã gửi kết quả thất bại");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Lỗi gửi kết quả: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Send capture status update.
    /// </summary>
    public async Task SendCaptureStatusAsync(string patientId, string status, string? message = null)
    {
        if (_hubConnection is null || !IsConnected) return;

        await _hubConnection.SendAsync("SendCaptureStatus", patientId, status, message);
    }

    /// <summary>
    /// Send sample quality feedback.
    /// </summary>
    public async Task SendSampleQualityAsync(string patientId, string quality, string? imageData = null)
    {
        if (_hubConnection is null || !IsConnected) return;

        await _hubConnection.SendAsync("SendSampleQuality", patientId, quality, imageData);
    }

    /// <summary>
    /// Send enrollment progress.
    /// </summary>
    public async Task SendEnrollmentProgressAsync(string patientId, int samplesCollected, int samplesNeeded)
    {
        if (_hubConnection is null || !IsConnected) return;

        await _hubConnection.SendAsync("SendEnrollmentProgress", patientId, samplesCollected, samplesNeeded);
    }

    private void SetupEventHandlers()
    {
        if (_hubConnection is null) return;

        _hubConnection.Reconnecting += error =>
        {
            StatusChanged?.Invoke(this, "Đang kết nối lại...");
            ConnectionChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            StatusChanged?.Invoke(this, "Đã kết nối lại");
            ConnectionChanged?.Invoke(this, true);
            
            // Auto-rejoin patient group if we were in one
            if (!string.IsNullOrEmpty(_currentPatientId))
            {
                try
                {
                    StatusChanged?.Invoke(this, $"Đang rejoin group cho patient: {_currentPatientId}");
                    await JoinPatientCaptureAsync(_currentPatientId);
                    StatusChanged?.Invoke(this, "Đã rejoin group thành công!");
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Lỗi rejoin group: {ex.Message}");
                }
            }
        };

        _hubConnection.Closed += error =>
        {
            StatusChanged?.Invoke(this, "Đã mất kết nối");
            ConnectionChanged?.Invoke(this, false);
            return Task.CompletedTask;
        };

        // Listen for capture requests from Angular UI
        _hubConnection.On<CaptureRequestDto>("CaptureRequested", async request =>
        {
            _currentPatientId = request.PatientId;
            StatusChanged?.Invoke(this, $"Nhận request cho patient: {request.PatientId}");
            
            // Join the patient group so we can send results back
            try
            {
                StatusChanged?.Invoke(this, $"Đang join group: fingerprint_{request.PatientId}");
                await JoinPatientCaptureAsync(request.PatientId);
                StatusChanged?.Invoke(this, $"Đã join group thành công!");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Lỗi join group: {ex.Message}");
            }
            
            CaptureRequested?.Invoke(this, new CaptureRequestEventArgs(request));
        });

            // Listen for verification requests
            _hubConnection.On<VerificationRequestDto>("VerificationRequested", async dto =>
            {
                var request = dto.Request;
                _currentPatientId = request.PatientId;
                StatusChanged?.Invoke(this, $"Nhận verification request cho patient: {request.PatientId}");
                
                try
                {
                    StatusChanged?.Invoke(this, $"Đang join group cho verification: fingerprint_{request.PatientId}");
                    await JoinPatientCaptureAsync(request.PatientId);
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Lỗi join group: {ex.Message}");
                }
                
                VerificationRequested?.Invoke(this, new VerificationRequestEventArgs(dto));
            });
// ... (lines 271-351 unchanged)



        // Listen for capture cancellation
        _hubConnection.On<string>("CaptureCancelled", patientId =>
        {
            if (patientId == _currentPatientId)
            {
                CaptureCancelled?.Invoke(this, EventArgs.Empty);
            }
        });

        // Confirmation of joining
        _hubConnection.On<string>("JoinedCapture", patientId =>
        {
            StatusChanged?.Invoke(this, $"Đã xác nhận tham gia: {patientId}");
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

// DTOs matching the server-side definitions
public record CaptureRequestDto
{
    public string PatientId { get; init; } = string.Empty;
    public string FingerType { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}

public record CaptureResultDto
{
    public string PatientId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? TemplateData { get; init; }  // Base64 encoded fingerprint template
    public string? ImageData { get; init; }     // Base64 encoded fingerprint image (JPEG)
    public string? FingerType { get; init; }
    public string? Quality { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime CapturedAt { get; init; }
}

public class CaptureRequestEventArgs : EventArgs
{
    public CaptureRequestDto Request { get; }

    public CaptureRequestEventArgs(CaptureRequestDto request)
    {
        Request = request;
    }
}

// DTOs matching the server-side definitions
public record VerificationRequestDto
{
    public VerificationRequestData Request { get; init; } = new();
}

public record VerificationRequestData
{
    public string PatientId { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public List<FingerprintTemplateDto> Templates { get; init; } = new();
}

public record FingerprintTemplateDto
{
    public string FingerType { get; init; } = string.Empty;
    public string TemplateData { get; init; } = string.Empty;
}

public record VerificationResultDto
{
    public string PatientId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? FingerType { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime VerifiedAt { get; init; }
}

public class VerificationRequestEventArgs : EventArgs
{
    public VerificationRequestData Request { get; }
    public VerificationRequestEventArgs(VerificationRequestDto dto) => Request = dto.Request;
}
