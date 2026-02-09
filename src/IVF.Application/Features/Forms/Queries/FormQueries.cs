using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Forms.Commands;
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

        var (responses, total) = await _repo.GetResponsesAsync(
            reportTemplate.FormTemplateId,
            request.PatientId,
            request.From,
            request.To,
            null, // no status filter for reports
            1,
            1000, // Get more for report
            ct);

        // Build report data from responses
        var data = new List<Dictionary<string, object?>>();
        var fieldValueCounts = new Dictionary<string, int>();
        var fieldValueSums = new Dictionary<string, (decimal sum, int count)>();

        foreach (var response in responses)
        {
            var fullResponse = await _repo.GetResponseByIdAsync(response.Id, true, ct);
            if (fullResponse?.FieldValues == null) continue;

            var row = new Dictionary<string, object?>
            {
                ["responseId"] = fullResponse.Id,
                ["patientName"] = fullResponse.Patient?.FullName,
                ["submittedAt"] = fullResponse.SubmittedAt,
                ["status"] = fullResponse.Status.ToString()
            };

            foreach (var fv in fullResponse.FieldValues)
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

#endregion
