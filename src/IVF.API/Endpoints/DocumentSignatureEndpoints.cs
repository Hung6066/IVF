using System.Security.Claims;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Documents.Commands;
using IVF.API.Services;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class DocumentSignatureEndpoints
{
    public static void MapDocumentSignatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/forms/responses")
            .WithTags("Document Signatures")
            .RequireAuthorization();

        // ─── Sign a form response for a specific role ───
        group.MapPost("/{responseId:guid}/sign", async (
            Guid responseId,
            SignFormResponseRequest request,
            ClaimsPrincipal principal,
            IvfDbContext db,
            IMediator m,
            SignedPdfGenerationService pdfGenService) =>
        {
            var userId = Guid.TryParse(
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;

            if (!userId.HasValue)
                return Results.BadRequest(new { error = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

            // Check response exists
            var response = await db.FormResponses
                .FirstOrDefaultAsync(r => r.Id == responseId && !r.IsDeleted);
            if (response == null)
                return Results.NotFound(new { error = "Không tìm thấy phiếu." });

            // Check user has a signature uploaded
            var userSig = await db.UserSignatures
                .Where(s => s.UserId == userId.Value && !s.IsDeleted && s.IsActive)
                .FirstOrDefaultAsync();

            if (userSig == null || string.IsNullOrEmpty(userSig.SignatureImageBase64))
                return Results.BadRequest(new { error = "Bạn chưa tải lên chữ ký. Vui lòng vào Quản trị → Ký số → tab Chữ ký để tải lên." });

            var role = request.SignatureRole?.ToLowerInvariant() ?? "current_user";

            // Check if already signed for this role
            var existingSig = await db.DocumentSignatures
                .Where(d => d.FormResponseId == responseId
                    && d.UserId == userId.Value
                    && d.SignatureRole == role
                    && !d.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingSig != null)
                return Results.Conflict(new { error = $"Bạn đã ký vai trò '{role}' cho phiếu này rồi." });

            // Create document signature record
            var docSig = DocumentSignature.Create(
                responseId, userId.Value, role, request.Notes);

            db.DocumentSignatures.Add(docSig);
            await db.SaveChangesAsync();

            // ─── Check if all required roles are now signed → auto-generate PDF ───
            string? autoGenMessage = null;
            try
            {
                // Invalidate old stored PDF first
                await m.Send(new InvalidateStoredSignedPdfCommand(responseId));

                // Check required roles from report template
                var formResponse = await db.FormResponses
                    .FirstOrDefaultAsync(r => r.Id == responseId && !r.IsDeleted);
                if (formResponse != null)
                {
                    var reportTpl = await db.Set<ReportTemplate>()
                        .Where(r => r.FormTemplateId == formResponse.FormTemplateId && !r.IsDeleted)
                        .FirstOrDefaultAsync();

                    List<string> requiredRoles = [];
                    if (reportTpl?.ConfigurationJson != null)
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(reportTpl.ConfigurationJson);
                            if (doc.RootElement.TryGetProperty("bands", out var bands))
                            {
                                foreach (var band in bands.EnumerateArray())
                                {
                                    if (band.TryGetProperty("controls", out var controls))
                                    {
                                        foreach (var ctrl in controls.EnumerateArray())
                                        {
                                            if (ctrl.TryGetProperty("type", out var type)
                                                && type.GetString() == "signatureZone"
                                                && ctrl.TryGetProperty("signatureRole", out var sr))
                                            {
                                                var r2 = sr.GetString();
                                                if (!string.IsNullOrEmpty(r2) && !requiredRoles.Contains(r2))
                                                    requiredRoles.Add(r2);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { /* ignore parse errors */ }
                    }

                    if (requiredRoles.Count > 0)
                    {
                        var signedRoles = await db.DocumentSignatures
                            .Where(d => d.FormResponseId == responseId && !d.IsDeleted)
                            .Select(d => d.SignatureRole)
                            .ToListAsync();

                        var isFullySigned = requiredRoles.All(r => signedRoles.Contains(r));

                        if (isFullySigned)
                        {
                            // Auto-generate signed PDF with all signatures
                            var genResult = await pdfGenService.GenerateAndStoreAsync(responseId);
                            if (genResult != null && genResult.Success)
                            {
                                autoGenMessage = " PDF ký số đã được tạo và lưu trữ tự động.";
                            }
                            else
                            {
                                autoGenMessage = $" Lưu ý: Không thể tạo PDF tự động ({genResult?.Error ?? "unknown"}).";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DocumentSignature] Auto-generate failed: {ex.Message}");
            }

            return Results.Ok(new
            {
                id = docSig.Id,
                formResponseId = responseId,
                signatureRole = role,
                signedAt = docSig.SignedAt,
                message = "Ký thành công." + (autoGenMessage ?? " PDF ký số sẽ bao gồm chữ ký của bạn khi tải xuống.")
            });
        });

        // ─── Get signing status for a form response ───
        group.MapGet("/{responseId:guid}/signing-status", async (
            Guid responseId,
            IvfDbContext db,
            IMediator m) =>
        {
            var response = await db.FormResponses
                .Include(r => r.FormTemplate)
                .FirstOrDefaultAsync(r => r.Id == responseId && !r.IsDeleted);
            if (response == null)
                return Results.NotFound(new { error = "Không tìm thấy phiếu." });

            var signatures = await db.DocumentSignatures
                .Include(d => d.User)
                .Where(d => d.FormResponseId == responseId && !d.IsDeleted)
                .OrderBy(d => d.SignedAt)
                .Select(d => new
                {
                    id = d.Id,
                    signatureRole = d.SignatureRole,
                    userId = d.UserId,
                    signerName = d.User.FullName,
                    signedAt = d.SignedAt,
                    notes = d.Notes
                })
                .ToListAsync();

            // Get available signature zones from the report template
            var reportTemplate = await db.Set<IVF.Domain.Entities.ReportTemplate>()
                .Where(r => r.FormTemplateId == response.FormTemplateId && !r.IsDeleted)
                .FirstOrDefaultAsync();

            List<string> requiredRoles = [];
            if (reportTemplate?.ConfigurationJson != null)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(reportTemplate.ConfigurationJson);
                    if (doc.RootElement.TryGetProperty("bands", out var bands))
                    {
                        foreach (var band in bands.EnumerateArray())
                        {
                            if (band.TryGetProperty("controls", out var controls))
                            {
                                foreach (var ctrl in controls.EnumerateArray())
                                {
                                    if (ctrl.TryGetProperty("type", out var type)
                                        && type.GetString() == "signatureZone"
                                        && ctrl.TryGetProperty("signatureRole", out var role))
                                    {
                                        var r = role.GetString();
                                        if (!string.IsNullOrEmpty(r) && !requiredRoles.Contains(r))
                                            requiredRoles.Add(r);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { /* ignore parse errors */ }
            }

            var signedRoles = signatures.Select(s => s.signatureRole).ToHashSet();

            // ─── Check if signed PDF exists in MinIO ───
            var storedPdf = await m.Send(new GetStoredSignedPdfQuery(responseId));

            return Results.Ok(new
            {
                formResponseId = responseId,
                signatures,
                requiredRoles,
                pendingRoles = requiredRoles.Where(r => !signedRoles.Contains(r)).ToList(),
                isFullySigned = requiredRoles.Count > 0 && requiredRoles.All(r => signedRoles.Contains(r)),
                storedSignedPdf = storedPdf != null ? new
                {
                    documentId = storedPdf.DocumentId,
                    objectKey = storedPdf.ObjectKey,
                    fileName = storedPdf.OriginalFileName,
                    fileSizeBytes = storedPdf.FileSizeBytes,
                    signedAt = storedPdf.SignedAt,
                    signerNames = storedPdf.SignerNames,
                    downloadUrl = storedPdf.DownloadUrl
                } : null
            });
        });

        // ─── Download stored signed PDF from MinIO ───
        group.MapGet("/{responseId:guid}/signed-pdf", async (
            Guid responseId,
            IMediator m,
            IObjectStorageService objectStorage) =>
        {
            var storedPdf = await m.Send(new GetStoredSignedPdfQuery(responseId));
            if (storedPdf == null)
                return Results.NotFound(new { error = "Chưa có PDF ký số được lưu trữ. Vui lòng xuất PDF với chữ ký trước." });

            var stream = await objectStorage.DownloadAsync(storedPdf.BucketName, storedPdf.ObjectKey);
            if (stream == null)
                return Results.NotFound(new { error = "Không tìm thấy file trong kho lưu trữ." });

            return Results.File(stream, "application/pdf", storedPdf.OriginalFileName);
        });

        // ─── Get presigned URL for stored signed PDF ───
        group.MapGet("/{responseId:guid}/signed-pdf-url", async (
            Guid responseId,
            IMediator m,
            int expiryMinutes = 60) =>
        {
            var storedPdf = await m.Send(new GetStoredSignedPdfQuery(responseId));
            if (storedPdf == null)
                return Results.NotFound(new { error = "Chưa có PDF ký số được lưu trữ." });

            return Results.Ok(new
            {
                documentId = storedPdf.DocumentId,
                downloadUrl = storedPdf.DownloadUrl,
                expiresInMinutes = expiryMinutes,
                fileName = storedPdf.OriginalFileName,
                fileSizeBytes = storedPdf.FileSizeBytes,
                signedAt = storedPdf.SignedAt,
                signerNames = storedPdf.SignerNames
            });
        });

        // ─── Revoke a signature ───
        group.MapDelete("/{responseId:guid}/sign/{signatureId:guid}", async (
            Guid responseId,
            Guid signatureId,
            ClaimsPrincipal principal,
            IvfDbContext db,
            IMediator m) =>
        {
            var userId = Guid.TryParse(
                principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;

            if (!userId.HasValue)
                return Results.BadRequest(new { error = "Không xác định được người dùng." });

            var docSig = await db.DocumentSignatures
                .FirstOrDefaultAsync(d => d.Id == signatureId
                    && d.FormResponseId == responseId
                    && !d.IsDeleted);

            if (docSig == null)
                return Results.NotFound(new { error = "Không tìm thấy chữ ký." });

            // Only the signer can revoke their own signature
            if (docSig.UserId != userId.Value)
                return Results.Forbid();

            docSig.Revoke("Người ký đã thu hồi chữ ký");
            await db.SaveChangesAsync();

            // ─── Invalidate stored signed PDF (chữ ký thu hồi → PDF cũ không còn hợp lệ) ───
            try
            {
                await m.Send(new InvalidateStoredSignedPdfCommand(responseId));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DocumentSignature] Invalidate stored PDF on revoke failed: {ex.Message}");
            }

            return Results.Ok(new { message = "Đã thu hồi chữ ký thành công. PDF ký số đã lưu sẽ được cập nhật lại." });
        });
    }
}

public record SignFormResponseRequest(
    string? SignatureRole,
    string? Notes = null);
