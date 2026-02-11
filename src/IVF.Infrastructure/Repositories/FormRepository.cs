using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class FormRepository : IFormRepository
{
    private readonly IvfDbContext _context;

    public FormRepository(IvfDbContext context)
    {
        _context = context;
    }

    #region Form Categories

    public async Task<List<FormCategory>> GetAllCategoriesAsync(CancellationToken ct = default)
    {
        return await _context.FormCategories
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<FormCategory?> GetCategoryByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.FormCategories.FindAsync(new object[] { id }, ct);
    }

    public async Task<FormCategory> AddCategoryAsync(FormCategory category, CancellationToken ct = default)
    {
        _context.FormCategories.Add(category);
        await _context.SaveChangesAsync(ct);
        return category;
    }

    public async Task UpdateCategoryAsync(FormCategory category, CancellationToken ct = default)
    {
        _context.FormCategories.Update(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _context.FormCategories.FindAsync(new object[] { id }, ct);
        if (category != null)
        {
            category.MarkAsDeleted();
            await _context.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Form Templates

    public async Task<List<FormTemplate>> GetTemplatesByCategoryAsync(Guid categoryId, bool includeFields = false, CancellationToken ct = default)
    {
        var query = _context.FormTemplates.Where(t => t.CategoryId == categoryId);

        if (includeFields)
        {
            query = query.Include(t => t.Fields.OrderBy(f => f.DisplayOrder));
        }

        return await query.OrderBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<List<FormTemplate>> GetAllTemplatesAsync(bool publishedOnly = false, bool includeFields = false, CancellationToken ct = default)
    {
        var query = _context.FormTemplates
            .Include(t => t.Category)
            .AsQueryable();

        if (publishedOnly)
        {
            query = query.Where(t => t.IsPublished);
        }

        if (includeFields)
        {
            query = query.Include(t => t.Fields.OrderBy(f => f.DisplayOrder));
        }

        return await query.OrderBy(t => t.Category.Name).ThenBy(t => t.Name).ToListAsync(ct);
    }

    public async Task<FormTemplate?> GetTemplateByIdAsync(Guid id, bool includeFields = true, CancellationToken ct = default)
    {
        var query = _context.FormTemplates
            .Include(t => t.Category)
            .AsQueryable();

        if (includeFields)
        {
            query = query.Include(t => t.Fields.OrderBy(f => f.DisplayOrder));
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<FormTemplate> AddTemplateAsync(FormTemplate template, CancellationToken ct = default)
    {
        _context.FormTemplates.Add(template);
        await _context.SaveChangesAsync(ct);
        return template;
    }

    public async Task UpdateTemplateAsync(FormTemplate template, CancellationToken ct = default)
    {
        _context.FormTemplates.Update(template);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _context.FormTemplates.FindAsync(new object[] { id }, ct);
        if (template != null)
        {
            template.MarkAsDeleted();
            await _context.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Form Fields

    public async Task<FormField?> GetFieldByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.FormFields.FindAsync(new object[] { id }, ct);
    }

    public async Task<List<FormField>> GetFieldsByTemplateIdAsync(Guid templateId, CancellationToken ct = default)
    {
        return await _context.FormFields
            .Where(f => f.FormTemplateId == templateId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<FormField> AddFieldAsync(FormField field, CancellationToken ct = default)
    {
        _context.FormFields.Add(field);
        await _context.SaveChangesAsync(ct);
        return field;
    }

    public async Task UpdateFieldAsync(FormField field, CancellationToken ct = default)
    {
        _context.FormFields.Update(field);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteFieldAsync(Guid id, CancellationToken ct = default)
    {
        var field = await _context.FormFields.FindAsync(new object[] { id }, ct);
        if (field != null)
        {
            field.MarkAsDeleted();
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task ReorderFieldsAsync(Guid templateId, List<Guid> fieldIds, CancellationToken ct = default)
    {
        var fields = await _context.FormFields
            .Where(f => f.FormTemplateId == templateId && fieldIds.Contains(f.Id))
            .ToListAsync(ct);

        for (int i = 0; i < fieldIds.Count; i++)
        {
            var field = fields.FirstOrDefault(f => f.Id == fieldIds[i]);
            if (field != null)
            {
                field.UpdateOrder(i);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    #endregion

    #region Form Responses

    public async Task<(List<FormResponse> Items, int Total)> GetResponsesAsync(
        Guid? templateId = null,
        Guid? patientId = null,
        DateTime? from = null,
        DateTime? to = null,
        ResponseStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.FormResponses
            .AsNoTracking()
            .Include(r => r.FormTemplate)
            .Include(r => r.Patient)
            .Include(r => r.SubmittedByUser)
            .AsQueryable();

        if (templateId.HasValue)
            query = query.Where(r => r.FormTemplateId == templateId.Value);

        if (patientId.HasValue)
            query = query.Where(r => r.PatientId == patientId.Value);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<FormResponse?> GetResponseByIdAsync(Guid id, bool includeFieldValues = true, CancellationToken ct = default)
    {
        var query = _context.FormResponses
            .AsNoTracking()
            .Include(r => r.FormTemplate)
                .ThenInclude(t => t.Fields.OrderBy(f => f.DisplayOrder))
            .Include(r => r.Patient)
            .AsQueryable();

        if (includeFieldValues)
        {
            query = query.Include(r => r.FieldValues)
                .ThenInclude(v => v.FormField)
                .Include(r => r.FieldValues)
                .ThenInclude(v => v.Details);
        }

        return await query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    /// <summary>
    /// Loads a FormResponse WITH change tracking for mutation scenarios.
    /// Only includes FieldValues + Details (no FormTemplate/FormField navigations)
    /// to avoid duplicate entity tracking conflicts.
    /// </summary>
    public async Task<FormResponse?> GetResponseForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.FormResponses
            .Include(r => r.FieldValues)
                .ThenInclude(v => v.Details)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<(List<FormResponse> Items, int Total)> GetResponsesWithFieldValuesAsync(
        Guid templateId, Guid? patientId = null, DateTime? from = null, DateTime? to = null,
        CancellationToken ct = default)
    {
        var query = _context.FormResponses
            .AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.FieldValues).ThenInclude(v => v.FormField)
            .Include(r => r.FieldValues).ThenInclude(v => v.Details)
            .Where(r => r.FormTemplateId == templateId);

        if (patientId.HasValue)
            query = query.Where(r => r.PatientId == patientId.Value);
        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<FormResponse> AddResponseAsync(FormResponse response, CancellationToken ct = default)
    {
        _context.FormResponses.Add(response);

        // Explicitly ensure Details are tracked by EF Core
        foreach (var fieldValue in response.FieldValues)
        {
            foreach (var detail in fieldValue.Details)
            {
                _context.Entry(detail).State = EntityState.Added;
            }
        }

        await _context.SaveChangesAsync(ct);
        return response;
    }

    public async Task UpdateResponseAsync(FormResponse response, CancellationToken ct = default)
    {
        // Fix entity states for entities with client-generated GUIDs.
        //
        // When new entities (FormFieldValue, FormFieldValueDetail) are added to tracked
        // navigation collections, EF may track them as Modified instead of Added because
        // they have non-default GUID keys. EF sets original values to defaults, making
        // ALL properties appear modified — including CreatedAt.
        //
        // Distinction: existing entities loaded from DB never have CreatedAt modified
        // (we never change CreatedAt). New entities always have CreatedAt "modified"
        // (original = default vs current = DateTime set in Create()).
        //
        // Also: ClearDetails() soft-deletes old details → convert to hard deletes.

        foreach (var entry in _context.ChangeTracker.Entries<FormFieldValueDetail>().ToList())
        {
            if (entry.Entity.IsDeleted)
            {
                entry.State = EntityState.Deleted;
            }
            else if (entry.State == EntityState.Modified && entry.Property("CreatedAt").IsModified)
            {
                entry.State = EntityState.Added;
            }
        }

        foreach (var entry in _context.ChangeTracker.Entries<FormFieldValue>().ToList())
        {
            if (entry.State == EntityState.Modified && entry.Property("CreatedAt").IsModified)
            {
                entry.State = EntityState.Added;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteResponseAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _context.FormResponses
            .Include(r => r.FieldValues)
                .ThenInclude(v => v.Details)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (response != null)
        {
            // Cascade soft delete to field values and their details
            foreach (var fieldValue in response.FieldValues)
            {
                foreach (var detail in fieldValue.Details)
                {
                    detail.MarkAsDeleted();
                }
                fieldValue.MarkAsDeleted();
            }
            response.MarkAsDeleted();
            await _context.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Report Templates

    public async Task<List<ReportTemplate>> GetReportTemplatesByFormAsync(Guid formTemplateId, CancellationToken ct = default)
    {
        return await _context.ReportTemplates
            .Where(r => r.FormTemplateId == formTemplateId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<ReportTemplate?> GetReportTemplateByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ReportTemplates
            .Include(r => r.FormTemplate)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<ReportTemplate> AddReportTemplateAsync(ReportTemplate reportTemplate, CancellationToken ct = default)
    {
        _context.ReportTemplates.Add(reportTemplate);
        await _context.SaveChangesAsync(ct);
        return reportTemplate;
    }

    public async Task UpdateReportTemplateAsync(ReportTemplate reportTemplate, CancellationToken ct = default)
    {
        _context.ReportTemplates.Update(reportTemplate);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteReportTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var reportTemplate = await _context.ReportTemplates.FindAsync(new object[] { id }, ct);
        if (reportTemplate != null)
        {
            reportTemplate.MarkAsDeleted();
            await _context.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Linked Field Sources

    public async Task<List<LinkedFieldSource>> GetLinkedFieldSourcesByTargetTemplateAsync(
        Guid targetTemplateId, CancellationToken ct = default)
    {
        // Single query with subquery — eliminates double roundtrip
        return await _context.LinkedFieldSources
            .AsNoTracking()
            .Include(s => s.TargetField)
            .Include(s => s.SourceTemplate)
            .Include(s => s.SourceField)
            .Where(s => _context.FormFields
                .Where(f => f.FormTemplateId == targetTemplateId)
                .Select(f => f.Id)
                .Contains(s.TargetFieldId))
            .OrderBy(s => s.TargetFieldId)
            .ThenByDescending(s => s.Priority)
            .ToListAsync(ct);
    }

    public async Task<List<LinkedFieldSource>> GetLinkedFieldSourcesByTargetFieldAsync(
        Guid targetFieldId, CancellationToken ct = default)
    {
        return await _context.LinkedFieldSources
            .Include(s => s.TargetField)
            .Include(s => s.SourceTemplate)
            .Include(s => s.SourceField)
            .Where(s => s.TargetFieldId == targetFieldId)
            .OrderByDescending(s => s.Priority)
            .ToListAsync(ct);
    }

    public async Task<LinkedFieldSource?> GetLinkedFieldSourceByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.LinkedFieldSources
            .Include(s => s.TargetField)
            .Include(s => s.SourceTemplate)
            .Include(s => s.SourceField)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<LinkedFieldSource> AddLinkedFieldSourceAsync(LinkedFieldSource source, CancellationToken ct = default)
    {
        _context.LinkedFieldSources.Add(source);
        await _context.SaveChangesAsync(ct);
        return source;
    }

    public async Task UpdateLinkedFieldSourceAsync(LinkedFieldSource source, CancellationToken ct = default)
    {
        _context.LinkedFieldSources.Update(source);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteLinkedFieldSourceAsync(Guid id, CancellationToken ct = default)
    {
        var source = await _context.LinkedFieldSources.FindAsync(new object[] { id }, ct);
        if (source != null)
        {
            source.MarkAsDeleted();
            await _context.SaveChangesAsync(ct);
        }
    }

    #endregion

    #region Patient Concept Snapshots

    public async Task<List<PatientConceptSnapshot>> GetSnapshotsByPatientAsync(
        Guid patientId, Guid? cycleId = null, CancellationToken ct = default)
    {
        var query = _context.PatientConceptSnapshots
            .Include(s => s.Concept)
            .Include(s => s.FormField)
            .Include(s => s.FormResponse)
                .ThenInclude(r => r.FormTemplate)
            .Where(s => s.PatientId == patientId);

        if (cycleId.HasValue)
            query = query.Where(s => s.CycleId == cycleId.Value);

        return await query.OrderByDescending(s => s.CapturedAt).ToListAsync(ct);
    }

    public async Task<PatientConceptSnapshot?> GetSnapshotAsync(
        Guid patientId, Guid conceptId, Guid? cycleId, CancellationToken ct = default)
    {
        return await _context.PatientConceptSnapshots
            .FirstOrDefaultAsync(s =>
                s.PatientId == patientId &&
                s.ConceptId == conceptId &&
                s.CycleId == cycleId, ct);
    }

    public async Task UpsertSnapshotAsync(PatientConceptSnapshot snapshot, CancellationToken ct = default)
    {
        var existing = await GetSnapshotAsync(
            snapshot.PatientId, snapshot.ConceptId, snapshot.CycleId, ct);

        if (existing != null)
        {
            existing.UpdateValue(
                snapshot.FormResponseId,
                snapshot.FormFieldId,
                snapshot.CapturedAt,
                snapshot.TextValue,
                snapshot.NumericValue,
                snapshot.DateValue,
                snapshot.BooleanValue,
                snapshot.JsonValue);
        }
        else
        {
            _context.PatientConceptSnapshots.Add(snapshot);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task UpsertSnapshotsAsync(IEnumerable<PatientConceptSnapshot> snapshots, CancellationToken ct = default)
    {
        var snapshotList = snapshots.ToList();
        if (snapshotList.Count == 0) return;

        // Batch-load all existing snapshots in one query — eliminates N+1
        var patientId = snapshotList[0].PatientId;
        var cycleId = snapshotList[0].CycleId;
        var conceptIds = snapshotList.Select(s => s.ConceptId).ToList();

        var existingSnapshots = await _context.PatientConceptSnapshots
            .Where(s => s.PatientId == patientId
                     && conceptIds.Contains(s.ConceptId)
                     && s.CycleId == cycleId)
            .ToDictionaryAsync(s => s.ConceptId, ct);

        foreach (var snapshot in snapshotList)
        {
            if (existingSnapshots.TryGetValue(snapshot.ConceptId, out var existing))
            {
                existing.UpdateValue(
                    snapshot.FormResponseId,
                    snapshot.FormFieldId,
                    snapshot.CapturedAt,
                    snapshot.TextValue,
                    snapshot.NumericValue,
                    snapshot.DateValue,
                    snapshot.BooleanValue,
                    snapshot.JsonValue);
            }
            else
            {
                _context.PatientConceptSnapshots.Add(snapshot);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    #endregion
}
