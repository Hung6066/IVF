using System.Text.Json;
using System.Text.Json.Serialization;
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

#region Report Configuration DTOs

public class ReportConfigDto
{
    [JsonPropertyName("columns")]
    public List<ReportColumnConfigDto> Columns { get; set; } = [];

    [JsonPropertyName("page")]
    public ReportPageConfigDto? Page { get; set; }

    [JsonPropertyName("header")]
    public ReportHeaderConfigDto? Header { get; set; }

    [JsonPropertyName("footer")]
    public ReportFooterConfigDto? Footer { get; set; }

    [JsonPropertyName("filters")]
    public List<ReportFilterConfigDto> Filters { get; set; } = [];

    [JsonPropertyName("groupBy")]
    public string? GroupBy { get; set; }

    [JsonPropertyName("sortBy")]
    public string? SortBy { get; set; }

    [JsonPropertyName("sortDirection")]
    public string? SortDirection { get; set; }

    [JsonPropertyName("chart")]
    public ReportChartConfigDto? Chart { get; set; }

    [JsonPropertyName("conditionalFormats")]
    public List<ConditionalFormatConfigDto> ConditionalFormats { get; set; } = [];

    [JsonPropertyName("calculatedFields")]
    public List<CalculatedFieldConfigDto> CalculatedFields { get; set; } = [];

    [JsonPropertyName("showFooterAggregations")]
    public bool ShowFooterAggregations { get; set; }

    [JsonPropertyName("groupSummary")]
    public GroupSummaryConfigDto? GroupSummary { get; set; }
}

public class ReportColumnConfigDto
{
    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("aggregation")]
    public string? Aggregation { get; set; }
}

public class ReportPageConfigDto
{
    [JsonPropertyName("size")]
    public string Size { get; set; } = "A4";

    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "landscape";

    [JsonPropertyName("margins")]
    public ReportMarginsDto? Margins { get; set; }
}

public class ReportMarginsDto
{
    [JsonPropertyName("top")]
    public int Top { get; set; } = 30;

    [JsonPropertyName("right")]
    public int Right { get; set; } = 30;

    [JsonPropertyName("bottom")]
    public int Bottom { get; set; } = 30;

    [JsonPropertyName("left")]
    public int Left { get; set; } = 30;
}

public class ReportHeaderConfigDto
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("showLogo")]
    public bool ShowLogo { get; set; } = true;

    [JsonPropertyName("showDate")]
    public bool ShowDate { get; set; } = true;
}

public class ReportFooterConfigDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("showPageNumber")]
    public bool ShowPageNumber { get; set; } = true;
}

public class ReportFilterConfigDto
{
    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = "";

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "eq";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public class ReportChartConfigDto
{
    [JsonPropertyName("categoryField")]
    public string? CategoryField { get; set; }

    [JsonPropertyName("valueField")]
    public string? ValueField { get; set; }

    [JsonPropertyName("aggregation")]
    public string Aggregation { get; set; } = "count";

    [JsonPropertyName("showLegend")]
    public bool ShowLegend { get; set; } = true;

    [JsonPropertyName("showValues")]
    public bool ShowValues { get; set; } = true;

    [JsonPropertyName("maxItems")]
    public int MaxItems { get; set; } = 12;
}

public class ConditionalFormatConfigDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = "";

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "eq";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("applyTo")]
    public string ApplyTo { get; set; } = "row";

    [JsonPropertyName("style")]
    public ConditionalFormatStyleDto Style { get; set; } = new();
}

public class ConditionalFormatStyleDto
{
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("textColor")]
    public string? TextColor { get; set; }

    [JsonPropertyName("fontWeight")]
    public string? FontWeight { get; set; }

    [JsonPropertyName("fontStyle")]
    public string? FontStyle { get; set; }
}

public class CalculatedFieldConfigDto
{
    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";

    [JsonPropertyName("format")]
    public string? Format { get; set; }
}

public class GroupSummaryConfigDto
{
    [JsonPropertyName("showGroupHeaders")]
    public bool ShowGroupHeaders { get; set; } = true;

    [JsonPropertyName("showGroupFooters")]
    public bool ShowGroupFooters { get; set; } = true;

    [JsonPropertyName("aggregations")]
    public List<GroupAggregationConfigDto> Aggregations { get; set; } = [];
}

public class GroupAggregationConfigDto
{
    [JsonPropertyName("fieldKey")]
    public string FieldKey { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "count";

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

#endregion

#region Band Designer DTOs (Phase 2+3)

/// <summary>
/// Root DTO for the visual band designer configuration.
/// Stored in configurationJson alongside (or replacing) ReportConfigDto.
/// Discriminator: presence of "bands" property indicates band-based design.
/// </summary>
public class ReportDesignDto
{
    [JsonPropertyName("bands")]
    public List<ReportBandDto> Bands { get; set; } = [];

    [JsonPropertyName("pageWidth")]
    public int PageWidth { get; set; } = 800;

    [JsonPropertyName("parameters")]
    public List<ReportParameterDto> Parameters { get; set; } = [];

    [JsonPropertyName("dataSources")]
    public List<ReportDataSourceDto> DataSources { get; set; } = [];

    [JsonPropertyName("subReports")]
    public List<SubReportConfigDto> SubReports { get; set; } = [];

    [JsonPropertyName("tabs")]
    public List<ReportTabDto> Tabs { get; set; } = [];

    [JsonPropertyName("crossTab")]
    public CrossTabConfigDto? CrossTab { get; set; }

    [JsonPropertyName("styles")]
    public List<ReportStyleDefDto> Styles { get; set; } = [];
}

public class ReportBandDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "detail";

    [JsonPropertyName("height")]
    public int Height { get; set; } = 60;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("groupField")]
    public string? GroupField { get; set; }

    [JsonPropertyName("controls")]
    public List<ReportControlDto> Controls { get; set; } = [];
}

public class ReportControlDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "label";

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = 100;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 20;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("fieldKey")]
    public string? FieldKey { get; set; }

    [JsonPropertyName("dataField")]
    public string? DataField { get; set; }

    [JsonPropertyName("expression")]
    public string? Expression { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("style")]
    public ReportControlStyleDto? Style { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("shape")]
    public string? Shape { get; set; }

    [JsonPropertyName("barcodeValue")]
    public string? BarcodeValue { get; set; }

    [JsonPropertyName("signatureRole")]
    public string? SignatureRole { get; set; }
}

public class ReportControlStyleDto
{
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    [JsonPropertyName("fontSize")]
    public int? FontSize { get; set; }

    [JsonPropertyName("fontWeight")]
    public string? FontWeight { get; set; }

    [JsonPropertyName("fontStyle")]
    public string? FontStyle { get; set; }

    [JsonPropertyName("textAlign")]
    public string? TextAlign { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("borderColor")]
    public string? BorderColor { get; set; }

    [JsonPropertyName("borderWidth")]
    public int? BorderWidth { get; set; }

    [JsonPropertyName("padding")]
    public int? Padding { get; set; }
}

public class ReportParameterDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public class ReportDataSourceDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("formTemplateId")]
    public string? FormTemplateId { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public class SubReportConfigDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("reportTemplateId")]
    public string ReportTemplateId { get; set; } = "";

    [JsonPropertyName("bandId")]
    public string? BandId { get; set; }

    [JsonPropertyName("parameterMapping")]
    public Dictionary<string, string> ParameterMapping { get; set; } = [];
}

public class ReportTabDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("bands")]
    public List<ReportBandDto> Bands { get; set; } = [];
}

public class CrossTabConfigDto
{
    [JsonPropertyName("rowFields")]
    public List<string> RowFields { get; set; } = [];

    [JsonPropertyName("columnFields")]
    public List<string> ColumnFields { get; set; } = [];

    [JsonPropertyName("valueField")]
    public string? ValueField { get; set; }

    [JsonPropertyName("aggregation")]
    public string Aggregation { get; set; } = "count";

    [JsonPropertyName("showRowTotals")]
    public bool ShowRowTotals { get; set; } = true;

    [JsonPropertyName("showColumnTotals")]
    public bool ShowColumnTotals { get; set; } = true;
}

public class ReportStyleDefDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("style")]
    public ReportControlStyleDto Style { get; set; } = new();
}

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
            t.Code,
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
            template.Code,
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

        // Parse configuration JSON
        ReportConfigDto? config = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(reportTemplate.ConfigurationJson)
                && reportTemplate.ConfigurationJson != "{}")
            {
                config = JsonSerializer.Deserialize<ReportConfigDto>(reportTemplate.ConfigurationJson);
            }
        }
        catch { /* keep config null — use defaults */ }

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

        // Determine visible columns from config
        var visibleColumns = config?.Columns?
            .Where(c => c.Visible)
            .Select(c => c.FieldKey)
            .ToHashSet() ?? null;

        // Build column label map from config (use GroupBy to handle duplicate keys safely)
        var columnLabels = config?.Columns?
            .Where(c => !string.IsNullOrEmpty(c.Label))
            .GroupBy(c => c.FieldKey)
            .ToDictionary(g => g.Key, g => g.First().Label) ?? [];

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

        // ===== Apply config-based data filters =====
        if (config?.Filters is { Count: > 0 })
        {
            data = data.Where(row => ApplyFilters(row, config.Filters)).ToList();
        }

        // ===== Apply sorting =====
        if (!string.IsNullOrEmpty(config?.SortBy))
        {
            var sortKey = config.SortBy;
            var desc = string.Equals(config.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

            data = desc
                ? data.OrderByDescending(r => r.GetValueOrDefault(sortKey)?.ToString() ?? "").ToList()
                : data.OrderBy(r => r.GetValueOrDefault(sortKey)?.ToString() ?? "").ToList();
        }

        // ===== Filter columns to only visible =====
        if (visibleColumns is { Count: > 0 })
        {
            var keysToKeep = new HashSet<string>(visibleColumns) { "responseId" };
            data = data.Select(row =>
                row.Where(kvp => keysToKeep.Contains(kvp.Key))
                   .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            ).ToList();
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

    /// <summary>
    /// Evaluates all configured filters against a single data row.
    /// All filters must pass (AND logic).
    /// </summary>
    private static bool ApplyFilters(Dictionary<string, object?> row, List<ReportFilterConfigDto> filters)
    {
        foreach (var filter in filters)
        {
            if (string.IsNullOrEmpty(filter.FieldKey) || string.IsNullOrEmpty(filter.Value))
                continue;

            var cellValue = row.GetValueOrDefault(filter.FieldKey)?.ToString() ?? "";
            var filterValue = filter.Value;

            var pass = filter.Operator switch
            {
                "eq" => string.Equals(cellValue, filterValue, StringComparison.OrdinalIgnoreCase),
                "neq" => !string.Equals(cellValue, filterValue, StringComparison.OrdinalIgnoreCase),
                "contains" => cellValue.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
                "gt" => decimal.TryParse(cellValue, out var gv) && decimal.TryParse(filterValue, out var gf) && gv > gf,
                "lt" => decimal.TryParse(cellValue, out var lv) && decimal.TryParse(filterValue, out var lf) && lv < lf,
                "gte" => decimal.TryParse(cellValue, out var gev) && decimal.TryParse(filterValue, out var gef) && gev >= gef,
                "lte" => decimal.TryParse(cellValue, out var lev) && decimal.TryParse(filterValue, out var lef) && lev <= lef,
                _ => true
            };

            if (!pass) return false;
        }

        return true;
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
