using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IPatientDocumentRepository
{
    Task<PatientDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PatientDocument?> GetByIdWithPatientAsync(Guid id, CancellationToken ct = default);
    Task<(List<PatientDocument> Items, int Total)> GetByPatientAsync(
        Guid patientId, DocumentType? type = null, DocumentStatus? status = null,
        string? searchTerm = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default);
    Task<List<PatientDocument>> GetByPatientAndTypeAsync(
        Guid patientId, DocumentType type, CancellationToken ct = default);
    Task<List<PatientDocument>> GetVersionHistoryAsync(Guid documentId, CancellationToken ct = default);
    Task<PatientDocument?> GetSignedPdfByFormResponseAsync(Guid formResponseId, CancellationToken ct = default);
    Task<PatientDocument?> GetByObjectKeyAsync(string bucketName, string objectKey, CancellationToken ct = default);
    Task AddAsync(PatientDocument document, CancellationToken ct = default);
    void Update(PatientDocument document);
    Task<long> GetTotalStorageByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<Dictionary<DocumentType, int>> GetDocumentCountsByTypeAsync(
        Guid patientId, CancellationToken ct = default);
}
