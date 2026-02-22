using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Documents.Commands;

public sealed class UploadPatientDocumentHandler(
    IPatientDocumentRepository documentRepo,
    IPatientRepository patientRepo,
    IObjectStorageService objectStorage,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UploadPatientDocumentCommand, Result<PatientDocumentDto>>
{
    public async Task<Result<PatientDocumentDto>> Handle(
        UploadPatientDocumentCommand request,
        CancellationToken cancellationToken)
    {
        // Validate patient exists
        var patient = await patientRepo.GetByIdAsync(request.PatientId, cancellationToken);
        if (patient == null)
            return Result<PatientDocumentDto>.Fail("Không tìm thấy bệnh nhân");

        // Build S3 object key: {patientCode}/{documentType}/{year}/{uniqueId}{ext}
        var objectKey = PatientDocument.BuildObjectKey(
            patient.PatientCode, request.DocumentType, request.OriginalFileName);

        var bucketName = GetBucketForType(request.DocumentType);

        // Calculate checksum
        string? checksum = null;
        if (request.FileStream.CanSeek)
        {
            request.FileStream.Position = 0;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = await sha256.ComputeHashAsync(request.FileStream, cancellationToken);
            checksum = Convert.ToHexStringLower(hash);
            request.FileStream.Position = 0;
        }

        // Upload to MinIO
        await objectStorage.UploadAsync(
            bucketName, objectKey, request.FileStream,
            request.ContentType, request.FileSizeBytes,
            ct: cancellationToken);

        // Create domain entity
        var document = PatientDocument.Create(
            patientId: request.PatientId,
            title: request.Title,
            documentType: request.DocumentType,
            bucketName: bucketName,
            objectKey: objectKey,
            originalFileName: request.OriginalFileName,
            contentType: request.ContentType,
            fileSizeBytes: request.FileSizeBytes,
            checksum: checksum,
            description: request.Description,
            cycleId: request.CycleId,
            formResponseId: request.FormResponseId,
            uploadedByUserId: request.UploadedByUserId,
            confidentiality: request.Confidentiality,
            tags: request.Tags);

        await documentRepo.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PatientDocumentDto>.Ok(MapToDto(document, patient));
    }

    private string GetBucketForType(IVF.Domain.Enums.DocumentType type)
    {
        return type switch
        {
            IVF.Domain.Enums.DocumentType.SignedPdf => "ivf-signed-pdfs",
            IVF.Domain.Enums.DocumentType.ImagingReport or
            IVF.Domain.Enums.DocumentType.PathologyReport => "ivf-medical-images",
            _ => "ivf-documents"
        };
    }

    private static PatientDocumentDto MapToDto(PatientDocument d, Patient? patient = null)
    {
        return new PatientDocumentDto(
            d.Id, d.PatientId,
            patient?.PatientCode, patient?.FullName,
            d.Title, d.Description,
            d.DocumentType.ToString(), d.Status.ToString(),
            d.Confidentiality.ToString(),
            d.BucketName, d.ObjectKey,
            d.OriginalFileName, d.ContentType, d.FileSizeBytes,
            d.Checksum, d.IsSigned, d.SignedByName, d.SignedAt,
            d.SignedObjectKey, d.Version, d.PreviousVersionId,
            d.Tags, d.MetadataJson,
            d.CycleId, d.FormResponseId, d.UploadedByUserId,
            d.CreatedAt, d.UpdatedAt);
    }
}

public sealed class UpdateDocumentMetadataHandler(
    IPatientDocumentRepository documentRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateDocumentMetadataCommand, Result<PatientDocumentDto>>
{
    public async Task<Result<PatientDocumentDto>> Handle(
        UpdateDocumentMetadataCommand request,
        CancellationToken cancellationToken)
    {
        var doc = await documentRepo.GetByIdWithPatientAsync(request.DocumentId, cancellationToken);
        if (doc == null)
            return Result<PatientDocumentDto>.Fail("Không tìm thấy tài liệu");

        doc.UpdateMetadata(request.Title, request.Description, request.Tags);

        if (request.Status.HasValue)
            doc.UpdateStatus(request.Status.Value);

        documentRepo.Update(doc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PatientDocumentDto>.Ok(new PatientDocumentDto(
            doc.Id, doc.PatientId,
            doc.Patient?.PatientCode, doc.Patient?.FullName,
            doc.Title, doc.Description,
            doc.DocumentType.ToString(), doc.Status.ToString(),
            doc.Confidentiality.ToString(),
            doc.BucketName, doc.ObjectKey,
            doc.OriginalFileName, doc.ContentType, doc.FileSizeBytes,
            doc.Checksum, doc.IsSigned, doc.SignedByName, doc.SignedAt,
            doc.SignedObjectKey, doc.Version, doc.PreviousVersionId,
            doc.Tags, doc.MetadataJson,
            doc.CycleId, doc.FormResponseId, doc.UploadedByUserId,
            doc.CreatedAt, doc.UpdatedAt));
    }
}

public sealed class UploadDocumentVersionHandler(
    IPatientDocumentRepository documentRepo,
    IPatientRepository patientRepo,
    IObjectStorageService objectStorage,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UploadDocumentVersionCommand, Result<PatientDocumentDto>>
{
    public async Task<Result<PatientDocumentDto>> Handle(
        UploadDocumentVersionCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await documentRepo.GetByIdWithPatientAsync(request.DocumentId, cancellationToken);
        if (existing == null)
            return Result<PatientDocumentDto>.Fail("Không tìm thấy tài liệu");

        var patient = await patientRepo.GetByIdAsync(existing.PatientId, cancellationToken);
        if (patient == null)
            return Result<PatientDocumentDto>.Fail("Không tìm thấy bệnh nhân");

        var objectKey = PatientDocument.BuildObjectKey(
            patient.PatientCode, existing.DocumentType, request.OriginalFileName);

        // Calculate checksum
        string? checksum = null;
        if (request.FileStream.CanSeek)
        {
            request.FileStream.Position = 0;
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = await sha256.ComputeHashAsync(request.FileStream, cancellationToken);
            checksum = Convert.ToHexStringLower(hash);
            request.FileStream.Position = 0;
        }

        // Upload to MinIO
        await objectStorage.UploadAsync(
            existing.BucketName, objectKey, request.FileStream,
            request.ContentType, request.FileSizeBytes,
            ct: cancellationToken);

        // Create new version (marks old one as Superseded)
        var newVersion = existing.CreateNewVersion(
            existing.BucketName, objectKey,
            request.OriginalFileName, request.ContentType,
            request.FileSizeBytes, checksum);

        documentRepo.Update(existing);
        await documentRepo.AddAsync(newVersion, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PatientDocumentDto>.Ok(new PatientDocumentDto(
            newVersion.Id, newVersion.PatientId,
            patient.PatientCode, patient.FullName,
            newVersion.Title, newVersion.Description,
            newVersion.DocumentType.ToString(), newVersion.Status.ToString(),
            newVersion.Confidentiality.ToString(),
            newVersion.BucketName, newVersion.ObjectKey,
            newVersion.OriginalFileName, newVersion.ContentType, newVersion.FileSizeBytes,
            newVersion.Checksum, newVersion.IsSigned, newVersion.SignedByName, newVersion.SignedAt,
            newVersion.SignedObjectKey, newVersion.Version, newVersion.PreviousVersionId,
            newVersion.Tags, newVersion.MetadataJson,
            newVersion.CycleId, newVersion.FormResponseId, newVersion.UploadedByUserId,
            newVersion.CreatedAt, newVersion.UpdatedAt));
    }
}

public sealed class DeletePatientDocumentHandler(
    IPatientDocumentRepository documentRepo,
    IObjectStorageService objectStorage,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePatientDocumentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(
        DeletePatientDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var doc = await documentRepo.GetByIdAsync(request.DocumentId, cancellationToken);
        if (doc == null)
            return Result<bool>.Fail("Không tìm thấy tài liệu");

        // Soft delete in DB
        doc.MarkAsDeleted();
        documentRepo.Update(doc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Note: We don't immediately delete from MinIO to allow recovery
        // A background job can purge deleted files after retention period

        return Result<bool>.Ok(true);
    }
}

public sealed class SignDocumentHandler(
    IPatientDocumentRepository documentRepo,
    IObjectStorageService objectStorage,
    IDigitalSigningService signingService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SignDocumentCommand, Result<PatientDocumentDto>>
{
    public async Task<Result<PatientDocumentDto>> Handle(
        SignDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (!signingService.IsEnabled)
            return Result<PatientDocumentDto>.Fail("Dịch vụ ký số chưa được bật");

        var doc = await documentRepo.GetByIdWithPatientAsync(request.DocumentId, cancellationToken);
        if (doc == null)
            return Result<PatientDocumentDto>.Fail("Không tìm thấy tài liệu");

        if (doc.IsSigned)
            return Result<PatientDocumentDto>.Fail("Tài liệu đã được ký số");

        if (doc.ContentType != "application/pdf")
            return Result<PatientDocumentDto>.Fail("Chỉ hỗ trợ ký số file PDF");

        // Download from MinIO
        var stream = await objectStorage.DownloadAsync(doc.BucketName, doc.ObjectKey, cancellationToken);
        if (stream == null)
            return Result<PatientDocumentDto>.Fail("Không thể tải file từ lưu trữ");

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var pdfBytes = ms.ToArray();

        // Sign with SignServer
        var metadata = new SigningMetadata(
            Reason: $"Ký xác nhận: {doc.Title}",
            Location: "IVF Clinic",
            SignerName: request.SignedByName);

        var signedPdf = await signingService.SignPdfAsync(pdfBytes, metadata);

        // Upload signed PDF to signed-pdfs bucket
        var signedKey = $"signed/{doc.ObjectKey}";
        using var signedStream = new MemoryStream(signedPdf);
        await objectStorage.UploadAsync(
            "ivf-signed-pdfs", signedKey, signedStream,
            "application/pdf", signedPdf.Length,
            ct: cancellationToken);

        // Update document record
        doc.MarkAsSigned(request.SignedByName, signedKey);
        documentRepo.Update(doc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PatientDocumentDto>.Ok(new PatientDocumentDto(
            doc.Id, doc.PatientId,
            doc.Patient?.PatientCode, doc.Patient?.FullName,
            doc.Title, doc.Description,
            doc.DocumentType.ToString(), doc.Status.ToString(),
            doc.Confidentiality.ToString(),
            doc.BucketName, doc.ObjectKey,
            doc.OriginalFileName, doc.ContentType, doc.FileSizeBytes,
            doc.Checksum, doc.IsSigned, doc.SignedByName, doc.SignedAt,
            doc.SignedObjectKey, doc.Version, doc.PreviousVersionId,
            doc.Tags, doc.MetadataJson,
            doc.CycleId, doc.FormResponseId, doc.UploadedByUserId,
            doc.CreatedAt, doc.UpdatedAt));
    }
}
