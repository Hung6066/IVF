using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PatientDocumentRepository : IPatientDocumentRepository
{
    private readonly IvfDbContext _context;

    public PatientDocumentRepository(IvfDbContext context) => _context = context;

    public async Task<PatientDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.PatientDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<PatientDocument?> GetByIdWithPatientAsync(Guid id, CancellationToken ct = default)
        => await _context.PatientDocuments
            .Include(d => d.Patient)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<(List<PatientDocument> Items, int Total)> GetByPatientAsync(
        Guid patientId, DocumentType? type = null, DocumentStatus? status = null,
        string? searchTerm = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.PatientDocuments
            .AsNoTracking()
            .Where(d => d.PatientId == patientId && !d.IsDeleted);

        if (type.HasValue)
            query = query.Where(d => d.DocumentType == type.Value);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);
        else
            query = query.Where(d => d.Status != DocumentStatus.Superseded);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(d =>
                d.Title.ToLower().Contains(term) ||
                (d.Description != null && d.Description.ToLower().Contains(term)) ||
                d.OriginalFileName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<List<PatientDocument>> GetByPatientAndTypeAsync(
        Guid patientId, DocumentType type, CancellationToken ct = default)
        => await _context.PatientDocuments
            .AsNoTracking()
            .Where(d => d.PatientId == patientId && d.DocumentType == type
                        && !d.IsDeleted && d.Status == DocumentStatus.Active)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<PatientDocument>> GetVersionHistoryAsync(
        Guid documentId, CancellationToken ct = default)
    {
        var doc = await GetByIdAsync(documentId, ct);
        if (doc == null) return [];

        // Walk backwards through versions
        var result = new List<PatientDocument> { doc };
        var currentId = doc.PreviousVersionId;

        while (currentId.HasValue)
        {
            var prev = await _context.PatientDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == currentId.Value, ct);

            if (prev == null) break;
            result.Add(prev);
            currentId = prev.PreviousVersionId;
        }

        return result.OrderByDescending(d => d.Version).ToList();
    }

    public async Task<PatientDocument?> GetSignedPdfByFormResponseAsync(
        Guid formResponseId, CancellationToken ct = default)
        => await _context.PatientDocuments
            .Include(d => d.Patient)
            .Where(d => d.FormResponseId == formResponseId
                        && d.DocumentType == DocumentType.SignedPdf
                        && !d.IsDeleted
                        && d.Status == DocumentStatus.Active)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Find ANY document (including Superseded) by BucketName + ObjectKey.
    /// Used to detect unique constraint conflicts before INSERT.
    /// </summary>
    public async Task<PatientDocument?> GetByObjectKeyAsync(
        string bucketName, string objectKey, CancellationToken ct = default)
        => await _context.PatientDocuments
            .IgnoreQueryFilters() // include soft-deleted/superseded
            .Where(d => d.BucketName == bucketName && d.ObjectKey == objectKey)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(PatientDocument document, CancellationToken ct = default)
        => await _context.PatientDocuments.AddAsync(document, ct);

    public void Update(PatientDocument document)
        => _context.PatientDocuments.Update(document);

    public async Task<long> GetTotalStorageByPatientAsync(Guid patientId, CancellationToken ct = default)
        => await _context.PatientDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted)
            .SumAsync(d => d.FileSizeBytes, ct);

    public async Task<Dictionary<DocumentType, int>> GetDocumentCountsByTypeAsync(
        Guid patientId, CancellationToken ct = default)
        => await _context.PatientDocuments
            .Where(d => d.PatientId == patientId && !d.IsDeleted && d.Status == DocumentStatus.Active)
            .GroupBy(d => d.DocumentType)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
}
