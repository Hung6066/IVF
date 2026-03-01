using IVF.Application.Common.Interfaces;
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
/// Desktop clients authenticate via API key (X-API-Key header or apiKey query param).
/// JWT-authenticated users (Angular UI) are allowed through directly.
/// </summary>
public class FingerprintHub : Hub
{
    private readonly ILogger<FingerprintHub> _logger;
    private readonly IApiKeyValidator _apiKeyValidator;
    private readonly MediatR.ISender _sender;
    private readonly IBiometricMatcher _matcher;

    public FingerprintHub(
        ILogger<FingerprintHub> logger,
        IApiKeyValidator apiKeyValidator,
        MediatR.ISender sender,
        IBiometricMatcher matcher)
    {
        _logger = logger;
        _apiKeyValidator = apiKeyValidator;
        _sender = sender;
        _matcher = matcher;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        // Check if user is already authenticated (JWT via middleware or API key via middleware)
        var isAuthenticated = httpContext?.User?.Identity?.IsAuthenticated == true;

        if (isAuthenticated)
        {
            // API key or JWT already validated by middleware
            var authMethod = httpContext?.User?.FindFirst("auth_method")?.Value;
            if (authMethod == "api_key")
            {
                _logger.LogInformation("Desktop client connected via API key middleware: {ConnectionId}", Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, "DesktopClients");
            }
        }
        else
        {
            // Try inline API key validation for SignalR connections 
            // (middleware may not have processed query params for WebSocket upgrade)
            var apiKey = httpContext?.Request.Query["apiKey"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKey))
            {
                var result = await _apiKeyValidator.ValidateAsync(apiKey);
                if (result is null)
                {
                    _logger.LogWarning("Invalid API key attempt from {ConnectionId}", Context.ConnectionId);
                    Context.Abort();
                    return;
                }

                _logger.LogInformation("Desktop client authenticated with API key: {ConnectionId}", Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, "DesktopClients");
            }
            else
            {
                _logger.LogWarning("Unauthenticated connection attempt from {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }
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
    /// Request identification (1:N) (called by Angular UI)
    /// </summary>
    public async Task RequestIdentification()
    {
        _logger.LogInformation("Identification requested by {ConnectionId}", Context.ConnectionId);

        await Clients.Group("DesktopClients").SendAsync("IdentificationRequested", new IdentificationRequestDto
        {
            RequestedBy = Context.ConnectionId,
            RequestedAt = DateTime.UtcNow
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
            .Where(f => !string.IsNullOrEmpty(f.TemplateData)) // Ensure template data exists
            .Select(f => new FingerprintTemplateDto
            {
                FingerType = f.FingerType,
                TemplateData = f.TemplateData! // Validated by Where above
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



    // ... (Existing OnConnected/OnDisconnected)

    /// <summary>
    /// Identify a patient by fingerprint features (Server-Side Matching)
    /// </summary>
    public async Task IdentifyFingerprint(string featureSetBase64, string? originalRequesterId)
    {
        try
        {
            _logger.LogInformation("Received identification request from {ConnectionId} (Server-Side Match)", Context.ConnectionId);

            if (!_matcher.IsLoaded)
            {
                var error = new IdentificationResultDto
                {
                    Success = false,
                    ErrorMessage = "System is initializing. Please try again in a moment."
                };
                await Clients.Caller.SendAsync("IdentificationResult", error);
                if (!string.IsNullOrEmpty(originalRequesterId))
                {
                    await Clients.Client(originalRequesterId).SendAsync("IdentificationResult", error);
                }
                return;
            }

            byte[] features = Convert.FromBase64String(featureSetBase64);

            // Perform match in memory
            var (match, patientId, score) = _matcher.Identify(features);

            var result = new IdentificationResultDto
            {
                Success = match,
                PatientId = match ? patientId.ToString() : null,
                ErrorMessage = match ? null : "No matching fingerprint found."
            };

            // 1. Notify the Desktop Client (Caller) - so it can show green/red
            await Clients.Caller.SendAsync("IdentificationResult", result);

            // 2. Notify the Angular Client (Original Requester) - so it can navigate
            if (!string.IsNullOrEmpty(originalRequesterId))
            {
                _logger.LogInformation("Notifying original requester: {RequesterId}", originalRequesterId);
                await Clients.Client(originalRequesterId).SendAsync("IdentificationResult", result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during server-side identification");
            var error = new IdentificationResultDto
            {
                Success = false,
                ErrorMessage = "Server error during matching."
            };
            await Clients.Caller.SendAsync("IdentificationResult", error);
            if (!string.IsNullOrEmpty(originalRequesterId))
            {
                await Clients.Client(originalRequesterId).SendAsync("IdentificationResult", error);
            }
        }
    }

    /// <summary>
    /// Send identification result back to UI (called by WinForms app)
    /// </summary>
    public async Task SendIdentificationResult(IdentificationResultDto result)
    {
        _logger.LogInformation("Identification result received. Match: {Success}, Patient: {PatientId}",
            result.Success, result.PatientId);

        // Notify the specific requester (Angular UI)
        if (!string.IsNullOrEmpty(result.RequestedBy))
        {
            await Clients.Client(result.RequestedBy).SendAsync("IdentificationResult", result);
        }
    }

    private static string GetPatientGroup(string patientId) => $"fingerprint_{patientId}";
}

// ... existing DTOs ...

public record IdentificationRequestDto
{
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}

public record IdentificationResultDto
{
    public bool Success { get; init; }
    public string? PatientId { get; init; }
    public string? RequestedBy { get; init; }
    public string? ErrorMessage { get; init; }
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
