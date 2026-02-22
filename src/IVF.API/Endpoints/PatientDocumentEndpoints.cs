using System.Security.Claims;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Documents.Commands;
using IVF.Application.Features.Documents.Queries;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.API.Endpoints;

public static class PatientDocumentEndpoints
{
    public static void MapPatientDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents")
            .WithTags("Patient Documents")
            .RequireAuthorization();

        // ─── Upload document for a patient ───
        group.MapPost("/upload", async (
            HttpRequest httpReq,
            IMediator mediator,
            ClaimsPrincipal principal) =>
        {
            if (!httpReq.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");

            var form = await httpReq.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return Results.BadRequest("Chưa chọn file");

            // Max 50MB
            if (file.Length > 50 * 1024 * 1024)
                return Results.BadRequest("File vượt quá 50MB");

            if (!Guid.TryParse(form["patientId"], out var patientId))
                return Results.BadRequest("patientId là bắt buộc");

            var title = form["title"].ToString();
            if (string.IsNullOrWhiteSpace(title))
                title = file.FileName;

            _ = Enum.TryParse<DocumentType>(form["documentType"], true, out var docType);
            _ = Enum.TryParse<ConfidentialityLevel>(form["confidentiality"], true, out var confidentiality);

            Guid? cycleId = Guid.TryParse(form["cycleId"], out var cid) ? cid : null;
            Guid? formResponseId = Guid.TryParse(form["formResponseId"], out var frid) ? frid : null;
            var userId = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;

            using var stream = file.OpenReadStream();
            var result = await mediator.Send(new UploadPatientDocumentCommand(
                PatientId: patientId,
                Title: title,
                DocumentType: docType,
                OriginalFileName: file.FileName,
                ContentType: file.ContentType,
                FileStream: stream,
                FileSizeBytes: file.Length,
                Description: form["description"].ToString(),
                CycleId: cycleId,
                FormResponseId: formResponseId,
                UploadedByUserId: userId,
                Confidentiality: confidentiality,
                Tags: form["tags"].ToString()));

            return result.IsSuccess
                ? Results.Created($"/api/documents/{result.Value!.Id}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).DisableAntiforgery();

        // ─── Get document by ID ───
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDocumentByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // ─── Get documents for a patient ───
        group.MapGet("/patient/{patientId:guid}", async (
            Guid patientId, IMediator mediator,
            string? type = null, string? status = null,
            string? search = null, int page = 1, int pageSize = 20) =>
        {
            DocumentType? docType = Enum.TryParse<DocumentType>(type, true, out var dt) ? dt : null;
            DocumentStatus? docStatus = Enum.TryParse<DocumentStatus>(status, true, out var ds) ? ds : null;

            var (items, total) = await mediator.Send(new GetPatientDocumentsQuery(
                patientId, docType, docStatus, search, page, pageSize));

            return Results.Ok(new { items, total, page, pageSize });
        });

        // ─── Get patient document summary (storage stats) ───
        group.MapGet("/patient/{patientId:guid}/summary", async (
            Guid patientId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPatientDocumentSummaryQuery(patientId));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // ─── Get document download URL (presigned) ───
        group.MapGet("/{id:guid}/download-url", async (
            Guid id, IMediator mediator, bool signed = false, int expiry = 3600) =>
        {
            var result = await mediator.Send(new GetDocumentDownloadUrlQuery(id, signed, expiry));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // ─── Download document file directly ───
        group.MapGet("/{id:guid}/download", async (
            Guid id, IMediator mediator,
            IObjectStorageService objectStorage,
            bool signed = false) =>
        {
            var doc = await mediator.Send(new GetDocumentByIdQuery(id));
            if (doc == null) return Results.NotFound(new { error = "Không tìm thấy tài liệu" });

            // Determine primary and fallback download locations
            string primaryBucket, primaryKey;
            string? fallbackBucket = null, fallbackKey = null;

            if (signed && doc.IsSigned && !string.IsNullOrEmpty(doc.SignedObjectKey))
            {
                // User wants signed version
                primaryBucket = "ivf-signed-pdfs";
                primaryKey = doc.SignedObjectKey;
                // Fallback to original if signed file missing
                fallbackBucket = doc.BucketName;
                fallbackKey = doc.ObjectKey;
            }
            else if (doc.IsSigned && !string.IsNullOrEmpty(doc.SignedObjectKey))
            {
                // User wants original, but doc is signed → try original first, fallback to signed
                primaryBucket = doc.BucketName;
                primaryKey = doc.ObjectKey;
                fallbackBucket = "ivf-signed-pdfs";
                fallbackKey = doc.SignedObjectKey;
            }
            else
            {
                // Not signed → just use original
                primaryBucket = doc.BucketName;
                primaryKey = doc.ObjectKey;
            }

            // Try primary location
            var stream = await objectStorage.DownloadAsync(primaryBucket, primaryKey);

            // Fallback if primary not found
            if (stream == null && fallbackBucket != null && fallbackKey != null)
            {
                stream = await objectStorage.DownloadAsync(fallbackBucket, fallbackKey);
            }

            if (stream == null)
            {
                // Check if file exists in MinIO at all
                var primaryExists = await objectStorage.ExistsAsync(primaryBucket, primaryKey);
                var fallbackExists = fallbackBucket != null && fallbackKey != null
                    ? await objectStorage.ExistsAsync(fallbackBucket, fallbackKey) : false;

                return Results.NotFound(new
                {
                    error = "Không tìm thấy file trên kho lưu trữ MinIO. File có thể đã bị xóa hoặc chưa được upload.",
                    documentId = id,
                    isSigned = doc.IsSigned,
                    primaryLocation = new { bucket = primaryBucket, key = primaryKey, exists = primaryExists },
                    fallbackLocation = fallbackBucket != null ? new { bucket = fallbackBucket, key = fallbackKey, exists = fallbackExists } : null,
                    hint = "Vui lòng xuất lại PDF với ký số (export-pdf?sign=true) để tạo lại file trong MinIO."
                });
            }

            return Results.File(stream, doc.ContentType, doc.OriginalFileName);
        });

        // ─── Update document metadata ───
        group.MapPut("/{id:guid}", async (
            Guid id, UpdateDocumentRequest req, IMediator mediator) =>
        {
            DocumentStatus? status = Enum.TryParse<DocumentStatus>(req.Status, true, out var ds) ? ds : null;

            var result = await mediator.Send(new UpdateDocumentMetadataCommand(
                id, req.Title, req.Description, req.Tags, status));

            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // ─── Upload new version ───
        group.MapPost("/{id:guid}/versions", async (
            Guid id, HttpRequest httpReq, IMediator mediator) =>
        {
            if (!httpReq.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");

            var form = await httpReq.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return Results.BadRequest("Chưa chọn file");

            using var stream = file.OpenReadStream();
            var result = await mediator.Send(new UploadDocumentVersionCommand(
                id, file.FileName, file.ContentType, stream, file.Length));

            return result.IsSuccess
                ? Results.Created($"/api/documents/{result.Value!.Id}", result.Value)
                : Results.BadRequest(new { error = result.Error });
        }).DisableAntiforgery();

        // ─── Get version history ───
        group.MapGet("/{id:guid}/versions", async (
            Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDocumentVersionsQuery(id));
            return Results.Ok(result);
        });

        // ─── Sign document ───
        group.MapPost("/{id:guid}/sign", async (
            Guid id, SignDocumentRequest req, IMediator mediator, ClaimsPrincipal principal) =>
        {
            var userId = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;

            var result = await mediator.Send(new SignDocumentCommand(
                id, req.SignerName, userId));

            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(new { error = result.Error });
        });

        // ─── Delete document (soft) ───
        group.MapDelete("/{id:guid}", async (
            Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeletePatientDocumentCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        // ─── Storage health check ───
        group.MapGet("/storage/health", async (IObjectStorageService objectStorage) =>
        {
            try
            {
                await objectStorage.EnsureBucketExistsAsync("ivf-documents");
                await objectStorage.EnsureBucketExistsAsync("ivf-signed-pdfs");

                // Test upload to verify write access
                var testKey = $"_health_check/{Guid.NewGuid():N}.txt";
                var testData = System.Text.Encoding.UTF8.GetBytes("health check " + DateTime.UtcNow);
                using var testStream = new MemoryStream(testData);
                await objectStorage.UploadAsync("ivf-signed-pdfs", testKey, testStream, "text/plain", testData.Length);

                // Verify it exists
                var exists = await objectStorage.ExistsAsync("ivf-signed-pdfs", testKey);

                // Clean up test file
                await objectStorage.DeleteAsync("ivf-signed-pdfs", testKey);

                var docStats = await objectStorage.GetBucketStatsAsync("ivf-documents");
                var signedStats = await objectStorage.GetBucketStatsAsync("ivf-signed-pdfs");

                return Results.Ok(new
                {
                    healthy = true,
                    writeTestPassed = exists,
                    buckets = new
                    {
                        documents = new { docStats.TotalObjects, totalSizeMB = Math.Round(docStats.TotalSizeBytes / 1024.0 / 1024.0, 2) },
                        signedPdfs = new { signedStats.TotalObjects, totalSizeMB = Math.Round(signedStats.TotalSizeBytes / 1024.0 / 1024.0, 2) }
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { healthy = false, error = ex.Message, details = ex.ToString() });
            }
        }).RequireAuthorization("AdminOnly");

        // ─── List document types (for UI dropdown) ───
        group.MapGet("/types", () =>
        {
            var types = Enum.GetValues<DocumentType>()
                .Select(t => new { value = t.ToString(), label = GetDocumentTypeLabel(t) });
            return Results.Ok(types);
        }).AllowAnonymous();
    }

    private static string GetDocumentTypeLabel(DocumentType type) => type switch
    {
        DocumentType.MedicalRecord => "Bệnh án điện tử",
        DocumentType.AdmissionNote => "Phiếu nhập viện",
        DocumentType.DischargeNote => "Phiếu xuất viện",
        DocumentType.ProgressNote => "Ghi nhận tiến triển",
        DocumentType.LabResult => "Kết quả xét nghiệm",
        DocumentType.ImagingReport => "Báo cáo hình ảnh",
        DocumentType.PathologyReport => "Báo cáo giải phẫu bệnh",
        DocumentType.SemenAnalysisReport => "Kết quả phân tích tinh dịch",
        DocumentType.EmbryologyReport => "Báo cáo phôi học",
        DocumentType.OocyteReport => "Báo cáo noãn",
        DocumentType.TransferReport => "Báo cáo chuyển phôi",
        DocumentType.CryopreservationReport => "Báo cáo đông lạnh",
        DocumentType.ConsentForm => "Phiếu cam kết",
        DocumentType.IdentityDocument => "Giấy tờ tùy thân",
        DocumentType.InsuranceDocument => "Giấy tờ bảo hiểm",
        DocumentType.Prescription => "Đơn thuốc",
        DocumentType.TreatmentPlan => "Kế hoạch điều trị",
        DocumentType.SignedPdf => "PDF đã ký số",
        DocumentType.Other => "Tài liệu khác",
        _ => type.ToString()
    };
}

// ─── Request DTOs ───
public record UpdateDocumentRequest(
    string? Title = null,
    string? Description = null,
    string? Tags = null,
    string? Status = null);

public record SignDocumentRequest(string SignerName);
