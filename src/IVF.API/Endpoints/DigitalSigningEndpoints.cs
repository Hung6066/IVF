using IVF.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

/// <summary>
/// API endpoints for digital signing operations.
/// Manages PDF signing via SignServer/EJBCA infrastructure.
/// </summary>
public static class DigitalSigningEndpoints
{
    public static void MapDigitalSigningEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/signing")
            .WithTags("Digital Signing")
            .RequireAuthorization();

        // ─── Health Check ───────────────────────────────────────────
        group.MapGet("/health", async (IDigitalSigningService signingService) =>
        {
            var status = await signingService.CheckHealthAsync();
            return Results.Ok(status);
        })
        .WithName("GetSigningHealth")
        .WithDescription("Check SignServer and EJBCA health status");

        // ─── Sign an existing PDF ──────────────────────────────────
        group.MapPost("/sign-pdf", async (
            HttpRequest request,
            IDigitalSigningService signingService) =>
        {
            if (!signingService.IsEnabled)
                return Results.BadRequest(new { error = "Digital signing is not enabled" });

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "Expected multipart/form-data" });

            var form = await request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();

            if (file == null || file.Length == 0)
                return Results.BadRequest(new { error = "No PDF file provided" });

            if (file.ContentType != "application/pdf" &&
                !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Only PDF files are accepted" });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var pdfBytes = ms.ToArray();

            var reason = form["reason"].FirstOrDefault();
            var location = form["location"].FirstOrDefault();
            var contactInfo = form["contactInfo"].FirstOrDefault();
            var signerName = form["signerName"].FirstOrDefault();

            var metadata = new SigningMetadata(reason, location, contactInfo, signerName);

            try
            {
                var signedPdf = await signingService.SignPdfAsync(pdfBytes, metadata);
                var fileName = $"signed_{file.FileName}";
                return Results.File(signedPdf, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    title: "Signing failed",
                    statusCode: 502);
            }
        })
        .WithName("SignPdf")
        .WithDescription("Upload and digitally sign a PDF document")
        .DisableAntiforgery();

        // ─── Get signing configuration info ─────────────────────────
        group.MapGet("/config", (IDigitalSigningService signingService) =>
        {
            return Results.Ok(new
            {
                enabled = signingService.IsEnabled,
                description = "Digital signing via SignServer + EJBCA",
                endpoints = new
                {
                    health = "/api/signing/health",
                    signPdf = "POST /api/signing/sign-pdf (multipart/form-data)",
                    signFormResponse = "GET /api/forms/responses/{id}/export-signed-pdf"
                },
                infrastructure = new
                {
                    ejbca = "Certificate Authority - manages certificates",
                    signServer = "Document signer - signs PDFs using EJBCA certificates"
                }
            });
        })
        .WithName("GetSigningConfig")
        .AllowAnonymous()
        .WithDescription("Get digital signing configuration and endpoint information");
    }
}
