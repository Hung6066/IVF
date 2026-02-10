using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface IFormRepository
{
    // Form Categories
    Task<List<FormCategory>> GetAllCategoriesAsync(CancellationToken ct = default);
    Task<FormCategory?> GetCategoryByIdAsync(Guid id, CancellationToken ct = default);
    Task<FormCategory> AddCategoryAsync(FormCategory category, CancellationToken ct = default);
    Task UpdateCategoryAsync(FormCategory category, CancellationToken ct = default);
    Task DeleteCategoryAsync(Guid id, CancellationToken ct = default);

    // Form Templates
    Task<List<FormTemplate>> GetTemplatesByCategoryAsync(Guid categoryId, bool includeFields = false, CancellationToken ct = default);
    Task<List<FormTemplate>> GetAllTemplatesAsync(bool publishedOnly = false, bool includeFields = false, CancellationToken ct = default);
    Task<FormTemplate?> GetTemplateByIdAsync(Guid id, bool includeFields = true, CancellationToken ct = default);
    Task<FormTemplate> AddTemplateAsync(FormTemplate template, CancellationToken ct = default);
    Task UpdateTemplateAsync(FormTemplate template, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);

    // Form Fields
    Task<FormField?> GetFieldByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<FormField>> GetFieldsByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task<FormField> AddFieldAsync(FormField field, CancellationToken ct = default);
    Task UpdateFieldAsync(FormField field, CancellationToken ct = default);
    Task DeleteFieldAsync(Guid id, CancellationToken ct = default);
    Task ReorderFieldsAsync(Guid templateId, List<Guid> fieldIds, CancellationToken ct = default);

    // Form Responses
    Task<(List<FormResponse> Items, int Total)> GetResponsesAsync(
        Guid? templateId = null,
        Guid? patientId = null,
        DateTime? from = null,
        DateTime? to = null,
        ResponseStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<FormResponse?> GetResponseByIdAsync(Guid id, bool includeFieldValues = true, CancellationToken ct = default);
    Task<(List<FormResponse> Items, int Total)> GetResponsesWithFieldValuesAsync(
        Guid templateId, Guid? patientId = null, DateTime? from = null, DateTime? to = null,
        CancellationToken ct = default);
    Task<FormResponse> AddResponseAsync(FormResponse response, CancellationToken ct = default);
    Task UpdateResponseAsync(FormResponse response, CancellationToken ct = default);
    Task DeleteResponseAsync(Guid id, CancellationToken ct = default);

    // Report Templates
    Task<List<ReportTemplate>> GetReportTemplatesByFormAsync(Guid formTemplateId, CancellationToken ct = default);
    Task<ReportTemplate?> GetReportTemplateByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReportTemplate> AddReportTemplateAsync(ReportTemplate reportTemplate, CancellationToken ct = default);
    Task UpdateReportTemplateAsync(ReportTemplate reportTemplate, CancellationToken ct = default);
    Task DeleteReportTemplateAsync(Guid id, CancellationToken ct = default);

    // Linked Field Sources (Explicit field-to-field links)
    Task<List<LinkedFieldSource>> GetLinkedFieldSourcesByTargetTemplateAsync(Guid targetTemplateId, CancellationToken ct = default);
    Task<List<LinkedFieldSource>> GetLinkedFieldSourcesByTargetFieldAsync(Guid targetFieldId, CancellationToken ct = default);
    Task<LinkedFieldSource?> GetLinkedFieldSourceByIdAsync(Guid id, CancellationToken ct = default);
    Task<LinkedFieldSource> AddLinkedFieldSourceAsync(LinkedFieldSource source, CancellationToken ct = default);
    Task UpdateLinkedFieldSourceAsync(LinkedFieldSource source, CancellationToken ct = default);
    Task DeleteLinkedFieldSourceAsync(Guid id, CancellationToken ct = default);

    // Patient Concept Snapshots (Cross-Form Linked Data)
    Task<List<PatientConceptSnapshot>> GetSnapshotsByPatientAsync(Guid patientId, Guid? cycleId = null, CancellationToken ct = default);
    Task<PatientConceptSnapshot?> GetSnapshotAsync(Guid patientId, Guid conceptId, Guid? cycleId, CancellationToken ct = default);
    Task UpsertSnapshotAsync(PatientConceptSnapshot snapshot, CancellationToken ct = default);
    Task UpsertSnapshotsAsync(IEnumerable<PatientConceptSnapshot> snapshots, CancellationToken ct = default);
}
