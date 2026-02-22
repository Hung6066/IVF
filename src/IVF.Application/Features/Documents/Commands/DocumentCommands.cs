using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Documents.Commands;

// ─── Upload Document ───
public sealed record UploadPatientDocumentCommand(
    Guid PatientId,
    string Title,
    DocumentType DocumentType,
    string OriginalFileName,
    string ContentType,
    Stream FileStream,
    long FileSizeBytes,
    string? Description = null,
    Guid? CycleId = null,
    Guid? FormResponseId = null,
    Guid? UploadedByUserId = null,
    ConfidentialityLevel Confidentiality = ConfidentialityLevel.Normal,
    string? Tags = null
) : IRequest<Result<PatientDocumentDto>>;

// ─── Update Document Metadata ───
public sealed record UpdateDocumentMetadataCommand(
    Guid DocumentId,
    string? Title = null,
    string? Description = null,
    string? Tags = null,
    DocumentStatus? Status = null
) : IRequest<Result<PatientDocumentDto>>;

// ─── Upload New Version ───
public sealed record UploadDocumentVersionCommand(
    Guid DocumentId,
    string OriginalFileName,
    string ContentType,
    Stream FileStream,
    long FileSizeBytes
) : IRequest<Result<PatientDocumentDto>>;

// ─── Delete Document ───
public sealed record DeletePatientDocumentCommand(
    Guid DocumentId
) : IRequest<Result<bool>>;

// ─── Sign Document ───
public sealed record SignDocumentCommand(
    Guid DocumentId,
    string SignedByName,
    Guid? SignedByUserId = null
) : IRequest<Result<PatientDocumentDto>>;

// ─── Result wrapper ───
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static Result<T> Ok(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Fail(string error) => new() { IsSuccess = false, Error = error };
}

// ─── DTOs ───
public sealed record PatientDocumentDto(
    Guid Id,
    Guid PatientId,
    string? PatientCode,
    string? PatientName,
    string Title,
    string? Description,
    string DocumentType,
    string Status,
    string Confidentiality,
    string BucketName,
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    string? Checksum,
    bool IsSigned,
    string? SignedByName,
    DateTime? SignedAt,
    string? SignedObjectKey,
    int Version,
    Guid? PreviousVersionId,
    string? Tags,
    string? MetadataJson,
    Guid? CycleId,
    Guid? FormResponseId,
    Guid? UploadedByUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? DownloadUrl = null
);

public sealed record PatientDocumentSummaryDto(
    Guid PatientId,
    string PatientCode,
    string PatientName,
    long TotalStorageBytes,
    int TotalDocuments,
    Dictionary<string, int> DocumentCountsByType
);
