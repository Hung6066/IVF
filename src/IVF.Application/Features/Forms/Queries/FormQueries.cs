using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Forms.Commands;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Forms.Queries;

#region Categories

public record GetFormCategoriesQuery(bool ActiveOnly = true) : IRequest<List<FormCategoryDto>>;

public record GetFormCategoryByIdQuery(Guid Id) : IRequest<FormCategoryDto?>;

#endregion

#region Templates

public record GetFormTemplatesQuery(
    Guid? CategoryId = null,
    bool PublishedOnly = false,
    bool IncludeFields = false
) : IRequest<List<FormTemplateDto>>;

public record GetFormTemplateByIdQuery(Guid Id) : IRequest<FormTemplateDto?>;

#endregion

#region Fields

public record GetFormFieldsByTemplateQuery(Guid FormTemplateId) : IRequest<List<FormFieldDto>>;

#endregion

#region Responses

public record GetFormResponsesQuery(
    Guid? TemplateId = null,
    Guid? PatientId = null,
    DateTime? From = null,
    DateTime? To = null,
    ResponseStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<(List<FormResponseDto> Items, int Total)>;

public record GetFormResponseByIdQuery(Guid Id) : IRequest<FormResponseDto?>;

#endregion

#region Reports

public record GetReportTemplatesQuery(Guid FormTemplateId) : IRequest<List<ReportTemplateDto>>;

public record GetReportTemplateByIdQuery(Guid Id) : IRequest<ReportTemplateDto?>;

public record GenerateReportQuery(
    Guid ReportTemplateId,
    DateTime? From = null,
    DateTime? To = null,
    Guid? PatientId = null
) : IRequest<ReportDataDto>;

public record ReportDataDto(
    ReportTemplateDto Template,
    List<Dictionary<string, object?>> Data,
    ReportSummaryDto? Summary = null
);

public record ReportSummaryDto(
    int TotalResponses,
    Dictionary<string, int> FieldValueCounts,
    Dictionary<string, decimal?> FieldValueAverages
);

#endregion

#region Linked Data

/// <summary>
/// Pre-fill query: given a template, patient, and optional cycle,
/// return previously captured concept values that map to fields in this template.
/// </summary>
public record GetLinkedDataQuery(
    Guid TemplateId,
    Guid PatientId,
    Guid? CycleId = null
) : IRequest<List<LinkedDataValueDto>>;

/// <summary>
/// A pre-fill value from a previously captured concept snapshot.
/// </summary>
public record LinkedDataValueDto(
    Guid FieldId,
    string FieldLabel,
    Guid ConceptId,
    string ConceptDisplay,
    string? TextValue,
    decimal? NumericValue,
    DateTime? DateValue,
    bool? BooleanValue,
    string? JsonValue,
    string DisplayValue,
    string SourceFormName,
    DateTime CapturedAt,
    DataFlowType FlowType
);

/// <summary>
/// Get all configured linked field sources for a template.
/// </summary>
public record GetLinkedFieldSourcesQuery(Guid TemplateId) : IRequest<List<LinkedFieldSourceDto>>;

#endregion

#region Handlers

public class FormCategoriesQueryHandler :
    IRequestHandler<GetFormCategoriesQuery, List<FormCategoryDto>>,
    IRequestHandler<GetFormCategoryByIdQuery, FormCategoryDto?>
{
    private readonly IFormRepository _repo;

    public FormCategoriesQueryHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<FormCategoryDto>> Handle(GetFormCategoriesQuery request, CancellationToken ct)
    {
        var categories = await _repo.GetAllCategoriesAsync(ct);

        if (request.ActiveOnly)
            categories = categories.Where(c => c.IsActive).ToList();

        return categories.Select(c => new FormCategoryDto(
            c.Id, c.Name, c.Description, c.IconName, c.DisplayOrder, c.IsActive,
            c.FormTemplates?.Count ?? 0)).ToList();
    }

    public async Task<FormCategoryDto?> Handle(GetFormCategoryByIdQuery request, CancellationToken ct)
    {
        var category = await _repo.GetCategoryByIdAsync(request.Id, ct);
        if (category == null) return null;

        return new FormCategoryDto(
            category.Id, category.Name, category.Description, category.IconName,
            category.DisplayOrder, category.IsActive);
    }
}

public class FormTemplatesQueryHandler :
    IRequestHandler<GetFormTemplatesQuery, List<FormTemplateDto>>,
    IRequestHandler<GetFormTemplateByIdQuery, FormTemplateDto?>
{
    private readonly IFormRepository _repo;

    public FormTemplatesQueryHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<FormTemplateDto>> Handle(GetFormTemplatesQuery request, CancellationToken ct)
    {
        var templates = request.CategoryId.HasValue
            ? await _repo.GetTemplatesByCategoryAsync(request.CategoryId.Value, request.IncludeFields, ct)
            : await _repo.GetAllTemplatesAsync(request.PublishedOnly, request.IncludeFields, ct);

        return templates.Select(t => new FormTemplateDto(
            t.Id,
            t.CategoryId,
            t.Category?.Name ?? "",
            t.Name,
            t.Description,
            t.Version,
            t.IsPublished,
            t.CreatedAt,
            request.IncludeFields ? t.Fields?.Select(f => new FormFieldDto(
                f.Id, f.FieldKey, f.Label, f.Placeholder, f.FieldType, f.DisplayOrder,
                f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.LayoutJson, f.DefaultValue,
                f.HelpText, f.ConditionalLogicJson, f.ConceptId)).ToList() : null)).ToList();
    }

    public async Task<FormTemplateDto?> Handle(GetFormTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(request.Id, true, ct);
        if (template == null) return null;

        return new FormTemplateDto(
            template.Id,
            template.CategoryId,
            template.Category?.Name ?? "",
            template.Name,
            template.Description,
            template.Version,
            template.IsPublished,
            template.CreatedAt,
            template.Fields?.Select(f => new FormFieldDto(
                f.Id, f.FieldKey, f.Label, f.Placeholder, f.FieldType, f.DisplayOrder,
                f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.LayoutJson, f.DefaultValue,
                f.HelpText, f.ConditionalLogicJson, f.ConceptId)).ToList());
    }
}

public class FormFieldsQueryHandler : IRequestHandler<GetFormFieldsByTemplateQuery, List<FormFieldDto>>
{
    private readonly IFormRepository _repo;

    public FormFieldsQueryHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<FormFieldDto>> Handle(GetFormFieldsByTemplateQuery request, CancellationToken ct)
    {
        var fields = await _repo.GetFieldsByTemplateIdAsync(request.FormTemplateId, ct);

        return fields.Select(f => new FormFieldDto(
            f.Id, f.FieldKey, f.Label, f.Placeholder, f.FieldType, f.DisplayOrder,
            f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.LayoutJson, f.DefaultValue,
            f.HelpText, f.ConditionalLogicJson, f.ConceptId)).ToList();
    }
}

public class FormResponsesQueryHandler :
    IRequestHandler<GetFormResponsesQuery, (List<FormResponseDto> Items, int Total)>,
    IRequestHandler<GetFormResponseByIdQuery, FormResponseDto?>
{
    private readonly IFormRepository _repo;

    public FormResponsesQueryHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<(List<FormResponseDto> Items, int Total)> Handle(GetFormResponsesQuery request, CancellationToken ct)
    {
        var (items, total) = await _repo.GetResponsesAsync(
            request.TemplateId,
            request.PatientId,
            request.From,
            request.To,
            request.Status,
            request.Page,
            request.PageSize,
            ct);

        var dtos = items.Select(r => new FormResponseDto(
            r.Id,
            r.FormTemplateId,
            r.FormTemplate?.Name ?? "",
            r.PatientId,
            r.Patient?.FullName,
            r.CycleId,
            r.Status,
            r.Notes,
            r.CreatedAt,
            r.SubmittedAt)).ToList();

        return (dtos, total);
    }

    public async Task<FormResponseDto?> Handle(GetFormResponseByIdQuery request, CancellationToken ct)
    {
        var response = await _repo.GetResponseByIdAsync(request.Id, true, ct);
        if (response == null) return null;

        return new FormResponseDto(
            response.Id,
            response.FormTemplateId,
            response.FormTemplate?.Name ?? "",
            response.PatientId,
            response.Patient?.FullName,
            response.CycleId,
            response.Status,
            response.Notes,
            response.CreatedAt,
            response.SubmittedAt,
            response.FieldValues?.Select(v => new FormFieldValueDto(
                v.Id, v.FormFieldId, v.FormField?.FieldKey, v.FormField?.Label,
                v.TextValue, v.NumericValue, v.DateValue, v.BooleanValue, v.JsonValue,
                v.Details?.Select(d => new FormFieldValueDetailDto(d.Value, d.Label, d.ConceptId)).ToList())).ToList());
    }
}

public class ReportQueriesHandler :
    IRequestHandler<GetReportTemplatesQuery, List<ReportTemplateDto>>,
    IRequestHandler<GetReportTemplateByIdQuery, ReportTemplateDto?>,
    IRequestHandler<GenerateReportQuery, ReportDataDto>
{
    private readonly IFormRepository _repo;

    public ReportQueriesHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<ReportTemplateDto>> Handle(GetReportTemplatesQuery request, CancellationToken ct)
    {
        var templates = await _repo.GetReportTemplatesByFormAsync(request.FormTemplateId, ct);

        return templates.Select(r => new ReportTemplateDto(
            r.Id,
            r.FormTemplateId,
            r.FormTemplate?.Name ?? "",
            r.Name,
            r.Description,
            r.ReportType,
            r.ConfigurationJson,
            r.IsPublished,
            r.CreatedAt)).ToList();
    }

    public async Task<ReportTemplateDto?> Handle(GetReportTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await _repo.GetReportTemplateByIdAsync(request.Id, ct);
        if (template == null) return null;

        return new ReportTemplateDto(
            template.Id,
            template.FormTemplateId,
            template.FormTemplate?.Name ?? "",
            template.Name,
            template.Description,
            template.ReportType,
            template.ConfigurationJson,
            template.IsPublished,
            template.CreatedAt);
    }

    public async Task<ReportDataDto> Handle(GenerateReportQuery request, CancellationToken ct)
    {
        var reportTemplate = await _repo.GetReportTemplateByIdAsync(request.ReportTemplateId, ct);
        if (reportTemplate == null)
            throw new ArgumentException("Report template not found");

        // Single query with full includes — eliminates N+1 (was N+1 per response)
        var (responses, total) = await _repo.GetResponsesWithFieldValuesAsync(
            reportTemplate.FormTemplateId,
            request.PatientId,
            request.From,
            request.To,
            ct);

        // Build report data from responses
        var data = new List<Dictionary<string, object?>>();
        var fieldValueCounts = new Dictionary<string, int>();
        var fieldValueSums = new Dictionary<string, (decimal sum, int count)>();

        foreach (var response in responses)
        {
            if (response.FieldValues == null) continue;

            var row = new Dictionary<string, object?>
            {
                ["responseId"] = response.Id,
                ["patientName"] = response.Patient?.FullName,
                ["submittedAt"] = response.SubmittedAt,
                ["status"] = response.Status.ToString()
            };

            foreach (var fv in response.FieldValues)
            {
                var key = fv.FormField?.FieldKey ?? fv.FormFieldId.ToString();
                var displayValue = fv.GetDisplayValue();
                row[key] = displayValue;

                // Count values
                var countKey = $"{key}:{displayValue}";
                fieldValueCounts[countKey] = fieldValueCounts.GetValueOrDefault(countKey) + 1;

                // Sum numeric values
                if (fv.NumericValue.HasValue)
                {
                    if (!fieldValueSums.ContainsKey(key))
                        fieldValueSums[key] = (0, 0);
                    var (sum, count) = fieldValueSums[key];
                    fieldValueSums[key] = (sum + fv.NumericValue.Value, count + 1);
                }
            }

            data.Add(row);
        }

        // Calculate averages
        var averages = fieldValueSums.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.count > 0 ? (decimal?)Math.Round(kvp.Value.sum / kvp.Value.count, 2) : null);

        var templateDto = new ReportTemplateDto(
            reportTemplate.Id,
            reportTemplate.FormTemplateId,
            reportTemplate.FormTemplate?.Name ?? "",
            reportTemplate.Name,
            reportTemplate.Description,
            reportTemplate.ReportType,
            reportTemplate.ConfigurationJson,
            reportTemplate.IsPublished,
            reportTemplate.CreatedAt);

        var summary = new ReportSummaryDto(total, fieldValueCounts, averages);

        return new ReportDataDto(templateDto, data, summary);
    }
}

public class LinkedDataQueryHandler : IRequestHandler<GetLinkedDataQuery, List<LinkedDataValueDto>>
{
    private readonly IFormRepository _repo;

    public LinkedDataQueryHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<LinkedDataValueDto>> Handle(GetLinkedDataQuery request, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(request.TemplateId, true, ct);
        if (template?.Fields == null)
            return [];

        var results = new List<LinkedDataValueDto>();
        var resolvedFieldIds = new HashSet<Guid>();

        // === Priority 1: Same ConceptId auto-link ===
        // Fields that share the same ConceptId across forms are automatically linked
        // via PatientConceptSnapshot (materialized on each form submission).
        var fieldsWithConcept = template.Fields
            .Where(f => f.ConceptId.HasValue)
            .ToList();

        if (fieldsWithConcept.Count > 0)
        {
            var snapshots = await _repo.GetSnapshotsByPatientAsync(
                request.PatientId, request.CycleId, ct);

            // Only keep snapshots from OTHER templates (≠ current)
            var externalSnapshots = snapshots
                .Where(s => s.FormField?.FormTemplateId != request.TemplateId)
                .ToList();

            var snapshotByConceptId = externalSnapshots
                .GroupBy(s => s.ConceptId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CapturedAt).First());

            foreach (var field in fieldsWithConcept)
            {
                if (!snapshotByConceptId.TryGetValue(field.ConceptId!.Value, out var snapshot))
                    continue;

                results.Add(new LinkedDataValueDto(
                    FieldId: field.Id,
                    FieldLabel: field.Label,
                    ConceptId: snapshot.ConceptId,
                    ConceptDisplay: snapshot.Concept?.Display ?? snapshot.ConceptId.ToString(),
                    TextValue: snapshot.TextValue,
                    NumericValue: snapshot.NumericValue,
                    DateValue: snapshot.DateValue,
                    BooleanValue: snapshot.BooleanValue,
                    JsonValue: snapshot.JsonValue,
                    DisplayValue: snapshot.GetDisplayValue(),
                    SourceFormName: snapshot.FormResponse?.FormTemplate?.Name ?? "",
                    CapturedAt: snapshot.CapturedAt,
                    FlowType: DataFlowType.AutoFill
                ));

                resolvedFieldIds.Add(field.Id);
            }
        }

        // === Priority 2: Explicit LinkedFieldSource configuration ===
        // For fields NOT resolved by concept matching, use manual field-to-field links.
        var explicitLinks = await _repo.GetLinkedFieldSourcesByTargetTemplateAsync(request.TemplateId, ct);
        foreach (var link in explicitLinks.Where(l => l.IsActive))
        {
            if (resolvedFieldIds.Contains(link.TargetFieldId))
                continue;

            // Get latest submitted response for this source template + patient
            var (sourceResponses, _) = await _repo.GetResponsesAsync(
                templateId: link.SourceTemplateId,
                patientId: request.PatientId,
                status: ResponseStatus.Submitted,
                page: 1, pageSize: 1, ct: ct);

            if (sourceResponses.Count == 0) continue;

            var sourceResponse = await _repo.GetResponseByIdAsync(sourceResponses[0].Id, true, ct);
            if (sourceResponse?.FieldValues == null) continue;

            var sourceFieldValue = sourceResponse.FieldValues
                .FirstOrDefault(fv => fv.FormFieldId == link.SourceFieldId);

            if (sourceFieldValue == null) continue;

            var displayValue = sourceFieldValue.GetDisplayValue();
            if (string.IsNullOrEmpty(displayValue)) continue;

            results.Add(new LinkedDataValueDto(
                FieldId: link.TargetFieldId,
                FieldLabel: link.TargetField?.Label ?? "",
                ConceptId: Guid.Empty,
                ConceptDisplay: link.Description ?? "Liên kết cấu hình",
                TextValue: sourceFieldValue.TextValue,
                NumericValue: sourceFieldValue.NumericValue,
                DateValue: sourceFieldValue.DateValue,
                BooleanValue: sourceFieldValue.BooleanValue,
                JsonValue: sourceFieldValue.JsonValue,
                DisplayValue: displayValue,
                SourceFormName: link.SourceTemplate?.Name ?? "",
                CapturedAt: sourceResponse.SubmittedAt ?? sourceResponse.CreatedAt,
                FlowType: link.FlowType
            ));

            resolvedFieldIds.Add(link.TargetFieldId);
        }

        return results;
    }
}

public class LinkedFieldSourcesQueryHandler :
    IRequestHandler<GetLinkedFieldSourcesQuery, List<LinkedFieldSourceDto>>
{
    private readonly IFormRepository _repo;

    public LinkedFieldSourcesQueryHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<LinkedFieldSourceDto>> Handle(GetLinkedFieldSourcesQuery request, CancellationToken ct)
    {
        var sources = await _repo.GetLinkedFieldSourcesByTargetTemplateAsync(request.TemplateId, ct);

        return sources.Select(s => new LinkedFieldSourceDto(
            s.Id,
            s.TargetFieldId,
            s.TargetField?.Label ?? "",
            s.SourceTemplateId,
            s.SourceTemplate?.Name ?? "",
            s.SourceFieldId,
            s.SourceField?.Label ?? "",
            s.FlowType,
            s.Priority,
            s.IsActive,
            s.Description,
            s.CreatedAt)).ToList();
    }
}

#endregion
