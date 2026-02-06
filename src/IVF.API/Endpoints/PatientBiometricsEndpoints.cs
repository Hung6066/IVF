using IVF.Application.Features.Patients.Commands;
using IVF.Application.Features.Patients.Queries;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.API.Endpoints;

public static class PatientBiometricsEndpoints
{
    public static void MapPatientBiometricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patient Biometrics").RequireAuthorization();

        // ==================== PHOTO ====================
        
        // Upload photo (multipart/form-data)
        group.MapPost("/{patientId:guid}/photo", async (Guid patientId, IFormFile file, IMediator m) =>
        {
            if (file.Length == 0 || !file.ContentType.StartsWith("image/"))
                return Results.BadRequest("Invalid image file");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var cmd = new UploadPatientPhotoCommand(patientId, ms.ToArray(), file.ContentType, file.FileName);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        }).DisableAntiforgery();

        // Get photo
        group.MapGet("/{patientId:guid}/photo", async (Guid patientId, IMediator m) =>
        {
            var r = await m.Send(new GetPatientPhotoQuery(patientId));
            if (!r.IsSuccess)
                return Results.NotFound(r.Error);

            return Results.File(r.Value!.PhotoData, r.Value.ContentType, r.Value.FileName);
        });

        // Delete photo
        group.MapDelete("/{patientId:guid}/photo", async (Guid patientId, IMediator m) =>
        {
            var r = await m.Send(new DeletePatientPhotoCommand(patientId));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });

        // ==================== FINGERPRINTS ====================

        // Register fingerprint
        group.MapPost("/{patientId:guid}/fingerprints", async (Guid patientId, RegisterFingerprintRequest req, IMediator m) =>
        {
            var cmd = new RegisterPatientFingerprintCommand(
                patientId, 
                Convert.FromBase64String(req.FingerprintDataBase64),
                req.FingerType,
                req.SdkType,
                req.Quality);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/patients/{patientId}/fingerprints/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        // Get fingerprints list
        group.MapGet("/{patientId:guid}/fingerprints", async (Guid patientId, IMediator m) =>
        {
            var r = await m.Send(new GetPatientFingerprintsQuery(patientId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Get all fingerprints (for 1:N Identification) - Desktop Client Auth via API Key
        group.MapGet("/fingerprints/all", async (IMediator m, HttpRequest request, IConfiguration config) =>
        {
            // Manual API Key Validation for Desktop Client
            var apiKey = request.Headers["X-API-Key"].FirstOrDefault() ?? request.Query["apiKey"].FirstOrDefault();
            var validApiKeys = config.GetSection("DesktopClients:ApiKeys").Get<string[]>() ?? Array.Empty<string>();
            
            if (string.IsNullOrEmpty(apiKey) || !validApiKeys.Contains(apiKey))
            {
                return Results.Unauthorized();
            }

            var r = await m.Send(new GetAllPatientFingerprintsQuery());
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        }).AllowAnonymous();

        // Delete fingerprint
        group.MapDelete("/fingerprints/{fingerprintId:guid}", async (Guid fingerprintId, IMediator m) =>
        {
            var r = await m.Send(new DeletePatientFingerprintCommand(fingerprintId));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });
    }
}

// Request DTOs
public record RegisterFingerprintRequest(
    string FingerprintDataBase64,
    FingerprintType FingerType,
    FingerprintSdkType SdkType,
    int Quality);
