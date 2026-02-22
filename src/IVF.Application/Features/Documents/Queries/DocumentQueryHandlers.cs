using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Documents.Commands;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Documents.Queries;

public sealed class GetDocumentByIdHandler(
    IPatientDocumentRepository documentRepo)
    : IRequestHandler<GetDocumentByIdQuery, PatientDocumentDto?>
{
    public async Task<PatientDocumentDto?> Handle(
        GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var doc = await documentRepo.GetByIdWithPatientAsync(request.DocumentId, cancellationToken);
        return doc == null ? null : MapToDto(doc);
    }

    private static PatientDocumentDto MapToDto(PatientDocument d) => new(
        d.Id, d.PatientId,
        d.Patient?.PatientCode, d.Patient?.FullName,
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

public sealed class GetPatientDocumentsHandler(
    IPatientDocumentRepository documentRepo,
    IPatientRepository patientRepo)
    : IRequestHandler<GetPatientDocumentsQuery, (List<PatientDocumentDto> Items, int Total)>
{
    public async Task<(List<PatientDocumentDto> Items, int Total)> Handle(
        GetPatientDocumentsQuery request, CancellationToken cancellationToken)
    {
        var patient = await patientRepo.GetByIdAsync(request.PatientId, cancellationToken);
        var (items, total) = await documentRepo.GetByPatientAsync(
            request.PatientId, request.Type, request.Status,
            request.SearchTerm, request.Page, request.PageSize,
            cancellationToken);

        var dtos = items.Select(d => new PatientDocumentDto(
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
            d.CreatedAt, d.UpdatedAt)).ToList();

        return (dtos, total);
    }
}

public sealed class GetDocumentVersionsHandler(
    IPatientDocumentRepository documentRepo)
    : IRequestHandler<GetDocumentVersionsQuery, List<PatientDocumentDto>>
{
    public async Task<List<PatientDocumentDto>> Handle(
        GetDocumentVersionsQuery request, CancellationToken cancellationToken)
    {
        var versions = await documentRepo.GetVersionHistoryAsync(request.DocumentId, cancellationToken);

        return versions.Select(d => new PatientDocumentDto(
            d.Id, d.PatientId, null, null,
            d.Title, d.Description,
            d.DocumentType.ToString(), d.Status.ToString(),
            d.Confidentiality.ToString(),
            d.BucketName, d.ObjectKey,
            d.OriginalFileName, d.ContentType, d.FileSizeBytes,
            d.Checksum, d.IsSigned, d.SignedByName, d.SignedAt,
            d.SignedObjectKey, d.Version, d.PreviousVersionId,
            d.Tags, d.MetadataJson,
            d.CycleId, d.FormResponseId, d.UploadedByUserId,
            d.CreatedAt, d.UpdatedAt)).ToList();
    }
}

public sealed class GetPatientDocumentSummaryHandler(
    IPatientDocumentRepository documentRepo,
    IPatientRepository patientRepo)
    : IRequestHandler<GetPatientDocumentSummaryQuery, PatientDocumentSummaryDto?>
{
    public async Task<PatientDocumentSummaryDto?> Handle(
        GetPatientDocumentSummaryQuery request, CancellationToken cancellationToken)
    {
        var patient = await patientRepo.GetByIdAsync(request.PatientId, cancellationToken);
        if (patient == null) return null;

        var totalStorage = await documentRepo.GetTotalStorageByPatientAsync(
            request.PatientId, cancellationToken);
        var countsByType = await documentRepo.GetDocumentCountsByTypeAsync(
            request.PatientId, cancellationToken);

        return new PatientDocumentSummaryDto(
            patient.Id, patient.PatientCode, patient.FullName,
            totalStorage,
            countsByType.Values.Sum(),
            countsByType.ToDictionary(x => x.Key.ToString(), x => x.Value));
    }
}

public sealed class GetDocumentDownloadUrlHandler(
    IPatientDocumentRepository documentRepo,
    IObjectStorageService objectStorage)
    : IRequestHandler<GetDocumentDownloadUrlQuery, DocumentDownloadDto?>
{
    public async Task<DocumentDownloadDto?> Handle(
        GetDocumentDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var doc = await documentRepo.GetByIdAsync(request.DocumentId, cancellationToken);
        if (doc == null) return null;

        string bucket, key;
        if (request.GetSignedVersion && doc.IsSigned && !string.IsNullOrEmpty(doc.SignedObjectKey))
        {
            bucket = "ivf-signed-pdfs";
            key = doc.SignedObjectKey;
        }
        else
        {
            bucket = doc.BucketName;
            key = doc.ObjectKey;
        }

        // Try primary location, fallback if needed
        bool exists = await objectStorage.ExistsAsync(bucket, key, cancellationToken);
        if (!exists && doc.IsSigned && !string.IsNullOrEmpty(doc.SignedObjectKey))
        {
            // Fallback: try the other location
            bucket = request.GetSignedVersion ? doc.BucketName : "ivf-signed-pdfs";
            key = request.GetSignedVersion ? doc.ObjectKey : doc.SignedObjectKey;
        }

        var url = await objectStorage.GetPresignedUrlAsync(
            bucket, key, request.ExpirySeconds, cancellationToken);

        return new DocumentDownloadDto(
            url, doc.OriginalFileName, doc.ContentType,
            DateTime.UtcNow.AddSeconds(request.ExpirySeconds));
    }
}
