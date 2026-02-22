using IVF.Application.Features.Documents.Commands;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Documents.Queries;

// ─── Get Document by Id ───
public sealed record GetDocumentByIdQuery(Guid DocumentId)
    : IRequest<PatientDocumentDto?>;

// ─── Get Documents by Patient ───
public sealed record GetPatientDocumentsQuery(
    Guid PatientId,
    DocumentType? Type = null,
    DocumentStatus? Status = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<(List<PatientDocumentDto> Items, int Total)>;

// ─── Get Document Version History ───
public sealed record GetDocumentVersionsQuery(Guid DocumentId)
    : IRequest<List<PatientDocumentDto>>;

// ─── Get Patient Document Summary ───
public sealed record GetPatientDocumentSummaryQuery(Guid PatientId)
    : IRequest<PatientDocumentSummaryDto?>;

// ─── Get Download URL ───
public sealed record GetDocumentDownloadUrlQuery(
    Guid DocumentId,
    bool GetSignedVersion = false,
    int ExpirySeconds = 3600
) : IRequest<DocumentDownloadDto?>;

// ─── DTOs from Commands ───
// Reusing PatientDocumentDto and PatientDocumentSummaryDto from Commands namespace

public sealed record DocumentDownloadDto(
    string Url,
    string FileName,
    string ContentType,
    DateTime ExpiresAt);
