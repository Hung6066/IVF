using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IVF.API.Hubs;

/// <summary>
/// SignalR Hub for fingerprint capture communication between Angular UI and WinForms desktop app.
/// 
/// Flow:
/// 1. Angular UI connects and joins a patient-specific group
/// 2. UI sends CaptureRequest to signal WinForms to start capture
/// 3. WinForms app captures fingerprint and sends result back
/// 4. UI receives CaptureResult with template data
/// 
/// Note: No authentication required - desktop clients on trusted network connect directly.
/// For production, consider adding API key or certificate-based authentication.
/// </summary>
public class FingerprintHub : Hub
{
    private readonly ILogger<FingerprintHub> _logger;
    private readonly IConfiguration _configuration;
    private readonly MediatR.ISender _sender;

    public FingerprintHub(ILogger<FingerprintHub> logger, IConfiguration configuration, MediatR.ISender sender)
    {
        _logger = logger;
        _configuration = configuration;
        _sender = sender;
    }

    public override async Task OnConnectedAsync()
    {
        // Validate API key for desktop clients
        var httpContext = Context.GetHttpContext();
        var apiKey = httpContext?.Request.Query["apiKey"].FirstOrDefault();
        
        // Check if user is authenticated (Angular UI with JWT)
        var isAuthenticated = httpContext?.User?.Identity?.IsAuthenticated == true;
        
        if (!isAuthenticated && !string.IsNullOrEmpty(apiKey))
        {
            // Validate API key for desktop clients
            var validApiKeys = _configuration.GetSection("DesktopClients:ApiKeys").Get<string[]>() ?? Array.Empty<string>();
            
            if (!validApiKeys.Contains(apiKey))
            {
                _logger.LogWarning("Invalid API key attempt from {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }
            
            _logger.LogInformation("Desktop client authenticated with API key: {ConnectionId}", Context.ConnectionId);
            
            // Add to DesktopClients group to receive capture requests
            await Groups.AddToGroupAsync(Context.ConnectionId, "DesktopClients");
        }
        else if (!isAuthenticated)
        {
            _logger.LogWarning("Unauthenticated connection attempt from {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }
        
        _logger.LogInformation("Client connected to FingerprintHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from FingerprintHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a patient-specific capture group (called by Angular UI)
    /// </summary>
    public async Task JoinPatientCapture(string patientId)
    {
        var groupName = GetPatientGroup(patientId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined patient capture group: {Group}", Context.ConnectionId, groupName);
        
        await Clients.Caller.SendAsync("JoinedCapture", patientId);
    }

    /// <summary>
    /// Leave a patient-specific capture group
    /// </summary>
    public async Task LeavePatientCapture(string patientId)
    {
        var groupName = GetPatientGroup(patientId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} left patient capture group: {Group}", Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Request fingerprint capture for a patient (called by Angular UI)
    /// WinForms app listens for this and opens capture form
    /// </summary>
    public async Task RequestCapture(string patientId, string fingerType)
    {
        _logger.LogInformation("Capture requested for patient {PatientId}, finger: {FingerType}", patientId, fingerType);
        
        // Notify all desktop clients
        // Desktop clients will filter requests or handle queueing if needed, 
        // then join the specific patient group to send results
        await Clients.Group("DesktopClients").SendAsync("CaptureRequested", new CaptureRequestDto
        {
            PatientId = patientId,
            FingerType = fingerType,
            RequestedBy = Context.ConnectionId,
            RequestedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Cancel an ongoing capture request
    /// </summary>
    public async Task CancelCapture(string patientId)
    {
        var groupName = GetPatientGroup(patientId);
        _logger.LogInformation("Capture cancelled for patient {PatientId}", patientId);
        
        await Clients.Group(groupName).SendAsync("CaptureCancelled", patientId);
    }

    /// <summary>
    /// Send capture result to UI (called by WinForms app)
    /// </summary>
    public async Task SendCaptureResult(CaptureResultDto result)
    {
        var groupName = GetPatientGroup(result.PatientId);
        _logger.LogInformation("Capture result received for patient {PatientId}, success: {Success}", 
            result.PatientId, result.Success);
        
        await Clients.Group(groupName).SendAsync("CaptureResult", result);
    }

    /// <summary>
    /// Send capture status update (called by WinForms app)
    /// </summary>
    public async Task SendCaptureStatus(string patientId, string status, string? message = null)
    {
        var groupName = GetPatientGroup(patientId);
        
        await Clients.Group(groupName).SendAsync("CaptureStatus", new CaptureStatusDto
        {
            PatientId = patientId,
            Status = status,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send sample quality feedback during capture
    /// </summary>
    public async Task SendSampleQuality(string patientId, string quality, string? imageData = null)
    {
        var groupName = GetPatientGroup(patientId);
        
        await Clients.Group(groupName).SendAsync("SampleQuality", new SampleQualityDto
        {
            PatientId = patientId,
            Quality = quality,
            ImagePreview = imageData,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send enrollment progress (e.g., "2 of 4 samples collected")
    /// </summary>
    public async Task SendEnrollmentProgress(string patientId, int samplesCollected, int samplesNeeded)
    {
        var groupName = GetPatientGroup(patientId);
        
        await Clients.Group(groupName).SendAsync("EnrollmentProgress", new EnrollmentProgressDto
        {
            PatientId = patientId,
            SamplesCollected = samplesCollected,
            SamplesNeeded = samplesNeeded,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Request verification for a patient (called by Angular UI)
    /// Fetches all enrolled fingerprints and sends them to desktop client for local matching
    /// </summary>
    public async Task RequestVerification(string patientId)
    {
        if (!Guid.TryParse(patientId, out var pid))
        {
            _logger.LogWarning("Invalid patient ID for verification: {PatientId}", patientId);
            return;
        }

        _logger.LogInformation("Verification requested for patient {PatientId}", patientId);

        // Fetch enrolled fingerprints from database
        var query = new IVF.Application.Features.Patients.Queries.GetPatientFingerprintsQuery(pid);
        var result = await _sender.Send(query);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to fetch fingerprints for verification: {Error}", result.Error);
            return;
        }

        var templates = result.Value
            .Where(f => !string.IsNullOrEmpty(f.FingerType) && f.FingerType != "Unknown") // Ensure valid type
            .Select(f => new FingerprintTemplateDto
            {
                FingerType = f.FingerType,
                TemplateData = f.TemplateData // Now available in DTO
            })
            .ToList();
            
        // Send verification request to desktop clients
        await Clients.Group("DesktopClients").SendAsync("VerificationRequested", new VerificationRequestDto
        {
            Request = new VerificationRequestData
            {
                PatientId = patientId,
                Templates = templates,
                RequestedBy = Context.ConnectionId,
                RequestedAt = DateTime.UtcNow
            }
        });
    }

    /// <summary>
    /// Send verification result back to UI (called by WinForms app)
    /// </summary>
    public async Task SendVerificationResult(VerificationResultDto result)
    {
        _logger.LogInformation("Verification result received for patient {PatientId}, success: {Success}", 
            result.PatientId, result.Success);

        // Notify the specific patient group (Angular UI)
        await Clients.Group(GetPatientGroup(result.PatientId)).SendAsync("VerificationResult", result);
    }

    private static string GetPatientGroup(string patientId) => $"fingerprint_{patientId}";
}

// ==================== DTOs for SignalR messages ====================

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

public record CaptureStatusDto
{
    public string PatientId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;  // "Starting", "WaitingForFinger", "Capturing", "Processing", "Completed", "Failed"
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; }
}

public record SampleQualityDto
{
    public string PatientId { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;  // "Good", "Poor", "TooWet", "TooDry"
    public string? ImagePreview { get; init; }  // Base64 JPEG preview
    public DateTime Timestamp { get; init; }
}

public record EnrollmentProgressDto
{
    public string PatientId { get; init; } = string.Empty;
    public int SamplesCollected { get; init; }
    public int SamplesNeeded { get; init; }
    public DateTime Timestamp { get; init; }
}

public record VerificationRequestDto
{
    public VerificationRequestData Request { get; init; } = new();
}

public record VerificationRequestData
{
    public string PatientId { get; init; } = string.Empty;
    public List<FingerprintTemplateDto> Templates { get; init; } = new();
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
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
