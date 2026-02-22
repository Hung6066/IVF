using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Features.Documents.Commands;

/// <summary>
/// Lưu PDF ký số vào MinIO + tạo PatientDocument record
/// Nếu đã có bản cũ cho cùng FormResponseId → tạo version mới (supersede bản cũ)
/// </summary>
public sealed class StoreSignedFormPdfHandler(
    IPatientDocumentRepository documentRepo,
    IObjectStorageService objectStorage,
    IUnitOfWork unitOfWork,
    ILogger<StoreSignedFormPdfHandler> logger)
    : IRequestHandler<StoreSignedFormPdfCommand, Result<PatientDocumentDto>>
{
    public async Task<Result<PatientDocumentDto>> Handle(
        StoreSignedFormPdfCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            const string bucketName = "ivf-signed-pdfs";

            // ─── Build object key có ngữ nghĩa: phân biệt được form nào, BN nào ───
            // Cấu trúc: {patientCode}/Forms/{formTemplateId[..8]}/{year}/{formResponseId}.pdf
            // Nếu FormResponseId == Guid.Empty → signing trực tiếp, dùng path cũ (random)
            string objectKey;
            string fileName;
            if (request.FormResponseId != Guid.Empty)
            {
                objectKey = PatientDocument.BuildSignedFormObjectKey(
                    request.PatientCode, request.FormResponseId, request.FormTemplateCode);
                // Filename hiển thị vẫn dùng tên template để user dễ đọc
                fileName = $"{SanitizeFileName(request.TemplateName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            }
            else
            {
                // Direct signing (không có FormResponse) → dùng path legacy
                objectKey = PatientDocument.BuildObjectKey(
                    request.PatientCode, DocumentType.SignedPdf,
                    $"{SanitizeFileName(request.TemplateName)}.pdf");
                fileName = $"{SanitizeFileName(request.TemplateName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            }

            // SHA-256 checksum
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(request.SignedPdfBytes);
            var checksum = Convert.ToHexStringLower(hash);

            // Upload to MinIO
            logger.LogInformation(
                "Uploading signed PDF to MinIO: {Bucket}/{Key} ({Size} bytes)",
                bucketName, objectKey, request.SignedPdfBytes.Length);

            using var stream = new MemoryStream(request.SignedPdfBytes);
            var uploadResult = await objectStorage.UploadAsync(
                bucketName, objectKey, stream,
                "application/pdf", request.SignedPdfBytes.Length,
                ct: cancellationToken);

            logger.LogInformation(
                "MinIO upload completed: ETag={ETag}, Bucket={Bucket}, Key={Key}",
                uploadResult.ETag, uploadResult.BucketName, uploadResult.ObjectKey);

            // Verify the upload
            var uploadVerified = await objectStorage.ExistsAsync(bucketName, objectKey, cancellationToken);
            if (!uploadVerified)
            {
                logger.LogError("MinIO upload verification FAILED - file not found after upload: {Bucket}/{Key}", bucketName, objectKey);
                return Result<PatientDocumentDto>.Fail("Upload to MinIO succeeded but verification failed");
            }

            // Check if there's an existing signed PDF for this form response
            var existing = await documentRepo.GetSignedPdfByFormResponseAsync(
                request.FormResponseId, cancellationToken);

            PatientDocument document;

            if (existing != null)
            {
                // Active record exists → supersede it and create new version
                document = existing.CreateNewVersion(
                    bucketName, objectKey, fileName,
                    "application/pdf", request.SignedPdfBytes.Length, checksum);

                document.MarkAsSigned(
                    request.SignerNames ?? "System",
                    objectKey);

                documentRepo.Update(existing);
                await documentRepo.AddAsync(document, cancellationToken);
            }
            else
            {
                // No Active record — but check if a Superseded/deleted record occupies the same ObjectKey
                var conflicting = await documentRepo.GetByObjectKeyAsync(bucketName, objectKey, cancellationToken);

                if (conflicting != null)
                {
                    // Reactivate the existing record with updated data
                    conflicting.UpdateStatus(DocumentStatus.Active);
                    conflicting.MarkAsSigned(
                        request.SignerNames ?? "System",
                        objectKey);
                    documentRepo.Update(conflicting);
                    document = conflicting;
                }
                else
                {
                    // Create brand new PatientDocument
                    document = PatientDocument.Create(
                        patientId: request.PatientId,
                        title: $"PDF ký số - {request.TemplateName}",
                        documentType: DocumentType.SignedPdf,
                        bucketName: bucketName,
                        objectKey: objectKey,
                        originalFileName: fileName,
                        contentType: "application/pdf",
                        fileSizeBytes: request.SignedPdfBytes.Length,
                        checksum: checksum,
                        description: $"PDF ký số tự động từ phiếu {request.TemplateName}",
                        cycleId: request.CycleId,
                        formResponseId: request.FormResponseId,
                        uploadedByUserId: request.UploadedByUserId,
                        tags: "[\"signed-pdf\",\"auto-generated\"]",
                        metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                        {
                            signerNames = request.SignerNames,
                            templateName = request.TemplateName,
                            generatedAt = DateTime.UtcNow
                        }));

                    document.MarkAsSigned(
                        request.SignerNames ?? "System",
                        objectKey);

                    await documentRepo.AddAsync(document, cancellationToken);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Stored signed PDF for FormResponse {FormResponseId} → MinIO {Bucket}/{Key} ({Size} bytes)",
                request.FormResponseId, bucketName, objectKey, request.SignedPdfBytes.Length);

            return Result<PatientDocumentDto>.Ok(new PatientDocumentDto(
                document.Id, document.PatientId,
                request.PatientCode, null,
                document.Title, document.Description,
                document.DocumentType.ToString(), document.Status.ToString(),
                document.Confidentiality.ToString(),
                document.BucketName, document.ObjectKey,
                document.OriginalFileName, document.ContentType, document.FileSizeBytes,
                document.Checksum, document.IsSigned, document.SignedByName, document.SignedAt,
                document.SignedObjectKey, document.Version, document.PreviousVersionId,
                document.Tags, document.MetadataJson,
                document.CycleId, document.FormResponseId, document.UploadedByUserId,
                document.CreatedAt, document.UpdatedAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store signed PDF for FormResponse {FormResponseId}", request.FormResponseId);
            return Result<PatientDocumentDto>.Fail($"Lỗi lưu trữ PDF ký số: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "signed" : name.Replace(" ", "_").Replace("/", "-").Replace("\\", "-");
}

/// <summary>
/// Lấy PDF ký số đã lưu từ MinIO, kèm presigned URL để download
/// </summary>
public sealed class GetStoredSignedPdfHandler(
    IPatientDocumentRepository documentRepo,
    IObjectStorageService objectStorage,
    ILogger<GetStoredSignedPdfHandler> logger)
    : IRequestHandler<GetStoredSignedPdfQuery, StoredSignedPdfResult?>
{
    public async Task<StoredSignedPdfResult?> Handle(
        GetStoredSignedPdfQuery request,
        CancellationToken cancellationToken)
    {
        var doc = await documentRepo.GetSignedPdfByFormResponseAsync(
            request.FormResponseId, cancellationToken);

        if (doc == null)
            return null;

        // Generate presigned URL (valid 1 hour)
        string? downloadUrl = null;
        try
        {
            downloadUrl = await objectStorage.GetPresignedUrlAsync(
                doc.BucketName, doc.ObjectKey, 3600, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not generate presigned URL for {Key}", doc.ObjectKey);
        }

        return new StoredSignedPdfResult(
            doc.Id,
            doc.BucketName,
            doc.ObjectKey,
            doc.OriginalFileName,
            doc.FileSizeBytes,
            doc.SignedAt,
            doc.SignedByName,
            downloadUrl);
    }
}

/// <summary>
/// Vô hiệu hóa PDF ký số cũ khi chữ ký thay đổi
/// </summary>
public sealed class InvalidateStoredSignedPdfHandler(
    IPatientDocumentRepository documentRepo,
    IUnitOfWork unitOfWork,
    ILogger<InvalidateStoredSignedPdfHandler> logger)
    : IRequestHandler<InvalidateStoredSignedPdfCommand, bool>
{
    public async Task<bool> Handle(
        InvalidateStoredSignedPdfCommand request,
        CancellationToken cancellationToken)
    {
        var doc = await documentRepo.GetSignedPdfByFormResponseAsync(
            request.FormResponseId, cancellationToken);

        if (doc == null)
            return false;

        doc.UpdateStatus(DocumentStatus.Superseded);
        documentRepo.Update(doc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Invalidated stored signed PDF {DocumentId} for FormResponse {FormResponseId}",
            doc.Id, request.FormResponseId);

        return true;
    }
}
