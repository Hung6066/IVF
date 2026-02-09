using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
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
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _context.FormResponses
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
        _context.FormResponses.Update(response);

        // Handle details: soft-deleted ones stay Modified, new ones must be Added
        foreach (var fieldValue in response.FieldValues)
        {
            if (fieldValue.Details != null)
            {
                foreach (var detail in fieldValue.Details)
                {
                    var entry = _context.Entry(detail);
                    if (detail.IsDeleted)
                    {
                        // Existing detail that was soft-deleted — mark as Modified
                        entry.State = EntityState.Modified;
                    }
                    else if (entry.State == EntityState.Detached || entry.State == EntityState.Modified)
                    {
                        // New detail added after ClearDetails — mark as Added
                        // Check if it already exists in the database
                        var existsInDb = await _context.FormFieldValueDetails
                            .IgnoreQueryFilters()
                            .AnyAsync(d => d.Id == detail.Id, ct);
                        entry.State = existsInDb ? EntityState.Modified : EntityState.Added;
                    }
                }
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
}
