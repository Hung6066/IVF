using System.Text.Json;
using System.Text.RegularExpressions;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Forms.Commands;

#region Form Categories

public record CreateFormCategoryCommand(
    string Name,
    string? Description,
    string? IconName,
    int DisplayOrder = 0
) : IRequest<Result<FormCategoryDto>>;

public record UpdateFormCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    string? IconName,
    int DisplayOrder
) : IRequest<Result<FormCategoryDto>>;

public record DeleteFormCategoryCommand(Guid Id) : IRequest<Result<bool>>;

#endregion

#region Form Templates

public record CreateFormTemplateCommand(
    Guid? CategoryId,
    string Name,
    string? Description,
    Guid? CreatedByUserId,
    List<CreateFormFieldDto>? Fields
) : IRequest<Result<FormTemplateDto>>;

public record UpdateFormTemplateCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid? CategoryId
) : IRequest<Result<FormTemplateDto>>;

public record PublishFormTemplateCommand(Guid Id) : IRequest<Result<FormTemplateDto>>;

public record UnpublishFormTemplateCommand(Guid Id) : IRequest<Result<FormTemplateDto>>;

public record DeleteFormTemplateCommand(Guid Id) : IRequest<Result<bool>>;

public record DuplicateFormTemplateCommand(Guid SourceTemplateId, string? NewName = null) : IRequest<Result<FormTemplateDto>>;

#endregion

#region Form Fields

public record AddFormFieldCommand(
    Guid FormTemplateId,
    string FieldKey,
    string Label,
    FieldType FieldType,
    int DisplayOrder,
    bool IsRequired = false,
    string? Placeholder = null,
    string? OptionsJson = null,
    string? ValidationRulesJson = null,
    string? DefaultValue = null,
    string? HelpText = null,
    string? ConditionalLogicJson = null,
    string? LayoutJson = null
) : IRequest<Result<FormFieldDto>>;

public record UpdateFormFieldCommand(
    Guid Id,
    string Label,
    FieldType FieldType,
    int DisplayOrder,
    bool IsRequired,
    string? Placeholder,
    string? OptionsJson,
    string? ValidationRulesJson,
    string? DefaultValue,
    string? HelpText,
    string? ConditionalLogicJson,
    string? LayoutJson = null
) : IRequest<Result<FormFieldDto>>;

public record DeleteFormFieldCommand(Guid Id) : IRequest<Result<bool>>;

public record ReorderFormFieldsCommand(
    Guid FormTemplateId,
    List<Guid> FieldIds
) : IRequest<Result<bool>>;

#endregion

#region Form Responses

public record SubmitFormResponseCommand(
    Guid FormTemplateId,
    Guid? SubmittedByUserId,  // Made nullable
    Guid? PatientId,
    Guid? CycleId,
    List<FormFieldValueDto> FieldValues,
    bool IsDraft = false
) : IRequest<Result<FormResponseDto>>;

public record UpdateFormResponseStatusCommand(
    Guid Id,
    ResponseStatus NewStatus,
    string? Notes = null
) : IRequest<Result<FormResponseDto>>;

public record UpdateFormResponseCommand(
    Guid Id,
    List<FormFieldValueDto> FieldValues
) : IRequest<Result<FormResponseDto>>;

public record DeleteFormResponseCommand(Guid Id) : IRequest<Result<bool>>;

#endregion

#region Report Templates

public record CreateReportTemplateCommand(
    Guid FormTemplateId,
    string Name,
    string? Description,
    ReportType ReportType,
    string ConfigurationJson,
    Guid CreatedByUserId
) : IRequest<Result<ReportTemplateDto>>;

public record UpdateReportTemplateCommand(
    Guid Id,
    string Name,
    string? Description,
    ReportType ReportType,
    string ConfigurationJson
) : IRequest<Result<ReportTemplateDto>>;

public record PublishReportTemplateCommand(Guid Id) : IRequest<Result<ReportTemplateDto>>;

public record DeleteReportTemplateCommand(Guid Id) : IRequest<Result<bool>>;

#endregion

#region Linked Field Sources

public record CreateLinkedFieldSourceCommand(
    Guid TargetFieldId,
    Guid SourceTemplateId,
    Guid SourceFieldId,
    DataFlowType FlowType = DataFlowType.Suggest,
    int Priority = 0,
    string? Description = null
) : IRequest<Result<LinkedFieldSourceDto>>;

public record UpdateLinkedFieldSourceCommand(
    Guid Id,
    Guid? SourceTemplateId = null,
    Guid? SourceFieldId = null,
    DataFlowType? FlowType = null,
    int? Priority = null,
    string? Description = null
) : IRequest<Result<LinkedFieldSourceDto>>;

public record DeleteLinkedFieldSourceCommand(Guid Id) : IRequest<Result<bool>>;

#endregion

#region DTOs

public record FormCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    string? IconName,
    int DisplayOrder,
    bool IsActive,
    int TemplateCount = 0
);

public record FormTemplateDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    string Version,
    bool IsPublished,
    DateTime CreatedAt,
    List<FormFieldDto>? Fields = null
);

public record FormFieldDto(
    Guid Id,
    string FieldKey,
    string Label,
    string? Placeholder,
    FieldType FieldType,
    int DisplayOrder,
    bool IsRequired,
    string? OptionsJson,
    string? ValidationRulesJson,
    string? LayoutJson,
    string? DefaultValue,
    string? HelpText,
    string? ConditionalLogicJson,
    Guid? ConceptId
);

public record CreateFormFieldDto(
    string FieldKey,
    string Label,
    FieldType FieldType,
    int DisplayOrder,
    bool IsRequired = false,
    string? Placeholder = null,
    string? OptionsJson = null,
    string? ValidationRulesJson = null,
    string? LayoutJson = null,
    string? DefaultValue = null,
    string? HelpText = null,
    string? ConditionalLogicJson = null
);

public record FormResponseDto(
    Guid Id,
    Guid FormTemplateId,
    string FormTemplateName,
    Guid? PatientId,
    string? PatientName,
    Guid? CycleId,
    ResponseStatus Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    List<FormFieldValueDto>? FieldValues = null
);

public record FormFieldValueDto(
    Guid? Id,
    Guid FormFieldId,
    string? FieldKey,
    string? FieldLabel,
    string? TextValue,
    decimal? NumericValue,
    DateTime? DateValue,
    bool? BooleanValue,
    string? JsonValue,
    List<FormFieldValueDetailDto>? Details = null
);

public record FormFieldValueDetailDto(
    string Value,
    string? Label,
    Guid? ConceptId
);

public record ReportTemplateDto(
    Guid Id,
    Guid FormTemplateId,
    string FormTemplateName,
    string Name,
    string? Description,
    ReportType ReportType,
    string ConfigurationJson,
    bool IsPublished,
    DateTime CreatedAt
);

public record LinkedFieldSourceDto(
    Guid Id,
    Guid TargetFieldId,
    string TargetFieldLabel,
    Guid SourceTemplateId,
    string SourceTemplateName,
    Guid SourceFieldId,
    string SourceFieldLabel,
    DataFlowType FlowType,
    int Priority,
    bool IsActive,
    string? Description,
    DateTime CreatedAt
);

#endregion

#region Handlers

public class FormCategoryCommandsHandler :
    IRequestHandler<CreateFormCategoryCommand, Result<FormCategoryDto>>,
    IRequestHandler<UpdateFormCategoryCommand, Result<FormCategoryDto>>,
    IRequestHandler<DeleteFormCategoryCommand, Result<bool>>
{
    private readonly IFormRepository _repo;

    public FormCategoryCommandsHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FormCategoryDto>> Handle(CreateFormCategoryCommand request, CancellationToken ct)
    {
        var category = FormCategory.Create(
            request.Name,
            request.Description,
            request.IconName,
            request.DisplayOrder);

        await _repo.AddCategoryAsync(category, ct);

        return Result<FormCategoryDto>.Success(MapToDto(category));
    }

    public async Task<Result<FormCategoryDto>> Handle(UpdateFormCategoryCommand request, CancellationToken ct)
    {
        var category = await _repo.GetCategoryByIdAsync(request.Id, ct);
        if (category == null)
            return Result<FormCategoryDto>.Failure("Category not found");

        category.Update(request.Name, request.Description, request.IconName, request.DisplayOrder);
        await _repo.UpdateCategoryAsync(category, ct);

        return Result<FormCategoryDto>.Success(MapToDto(category));
    }

    public async Task<Result<bool>> Handle(DeleteFormCategoryCommand request, CancellationToken ct)
    {
        await _repo.DeleteCategoryAsync(request.Id, ct);
        return Result<bool>.Success(true);
    }

    private static FormCategoryDto MapToDto(FormCategory c) => new(
        c.Id, c.Name, c.Description, c.IconName, c.DisplayOrder, c.IsActive);
}

public class FormTemplateCommandsHandler :
    IRequestHandler<CreateFormTemplateCommand, Result<FormTemplateDto>>,
    IRequestHandler<UpdateFormTemplateCommand, Result<FormTemplateDto>>,
    IRequestHandler<PublishFormTemplateCommand, Result<FormTemplateDto>>,
    IRequestHandler<UnpublishFormTemplateCommand, Result<FormTemplateDto>>,
    IRequestHandler<DeleteFormTemplateCommand, Result<bool>>,
    IRequestHandler<DuplicateFormTemplateCommand, Result<FormTemplateDto>>
{
    private readonly IFormRepository _repo;

    public FormTemplateCommandsHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FormTemplateDto>> Handle(CreateFormTemplateCommand request, CancellationToken ct)
    {
        // Get or create default category if none specified
        var categoryId = request.CategoryId ?? Guid.Empty;
        if (categoryId == Guid.Empty)
        {
            var allCategories = await _repo.GetAllCategoriesAsync(ct);
            var defaultCategory = allCategories.FirstOrDefault();
            if (defaultCategory == null)
            {
                // Create a default category
                defaultCategory = FormCategory.Create("Mặc định", "Danh mục mặc định", "📁", 0);
                await _repo.AddCategoryAsync(defaultCategory, ct);
            }
            categoryId = defaultCategory.Id;
        }

        // Use null for createdByUserId if not provided (avoids FK constraint)
        var createdByUserId = request.CreatedByUserId;

        var template = FormTemplate.Create(
            categoryId,
            request.Name,
            createdByUserId,
            request.Description);

        // Add fields if provided
        if (request.Fields != null)
        {
            foreach (var fieldDto in request.Fields)
            {
                template.AddField(
                    fieldDto.FieldKey,
                    fieldDto.Label,
                    fieldDto.FieldType,
                    fieldDto.DisplayOrder,
                    fieldDto.IsRequired,
                    fieldDto.Placeholder,
                    fieldDto.OptionsJson,
                    fieldDto.ValidationRulesJson,
                    fieldDto.DefaultValue,
                    fieldDto.HelpText,
                    fieldDto.ConditionalLogicJson,
                    fieldDto.LayoutJson);
            }
        }

        await _repo.AddTemplateAsync(template, ct);

        // Reload with category
        template = await _repo.GetTemplateByIdAsync(template.Id, true, ct);

        return Result<FormTemplateDto>.Success(MapToDto(template!));
    }

    public async Task<Result<FormTemplateDto>> Handle(UpdateFormTemplateCommand request, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(request.Id, false, ct);
        if (template == null)
            return Result<FormTemplateDto>.Failure("Template not found");

        template.Update(request.Name, request.Description);
        if (request.CategoryId.HasValue)
            template.UpdateCategory(request.CategoryId.Value);

        await _repo.UpdateTemplateAsync(template, ct);

        template = await _repo.GetTemplateByIdAsync(template.Id, true, ct);
        return Result<FormTemplateDto>.Success(MapToDto(template!));
    }

    public async Task<Result<FormTemplateDto>> Handle(PublishFormTemplateCommand request, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(request.Id, true, ct);
        if (template == null)
            return Result<FormTemplateDto>.Failure("Template not found");

        template.Publish();
        await _repo.UpdateTemplateAsync(template, ct);

        return Result<FormTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result<FormTemplateDto>> Handle(UnpublishFormTemplateCommand request, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(request.Id, true, ct);
        if (template == null)
            return Result<FormTemplateDto>.Failure("Template not found");

        template.Unpublish();
        await _repo.UpdateTemplateAsync(template, ct);

        return Result<FormTemplateDto>.Success(MapToDto(template));
    }

    public async Task<Result<bool>> Handle(DeleteFormTemplateCommand request, CancellationToken ct)
    {
        await _repo.DeleteTemplateAsync(request.Id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<FormTemplateDto>> Handle(DuplicateFormTemplateCommand request, CancellationToken ct)
    {
        var source = await _repo.GetTemplateByIdAsync(request.SourceTemplateId, true, ct);
        if (source == null)
            return Result<FormTemplateDto>.Failure("Source template not found");

        var newName = request.NewName ?? $"{source.Name} (Bản sao)";

        var duplicate = FormTemplate.Create(
            source.CategoryId,
            newName,
            source.CreatedByUserId,
            source.Description);

        if (source.Fields != null)
        {
            foreach (var field in source.Fields.OrderBy(f => f.DisplayOrder))
            {
                duplicate.AddField(
                    field.FieldKey,
                    field.Label,
                    field.FieldType,
                    field.DisplayOrder,
                    field.IsRequired,
                    field.Placeholder,
                    field.OptionsJson,
                    field.ValidationRulesJson,
                    field.DefaultValue,
                    field.HelpText,
                    field.ConditionalLogicJson,
                    field.LayoutJson);
            }
        }

        await _repo.AddTemplateAsync(duplicate, ct);

        duplicate = await _repo.GetTemplateByIdAsync(duplicate.Id, true, ct);
        return Result<FormTemplateDto>.Success(MapToDto(duplicate!));
    }

    private static FormTemplateDto MapToDto(FormTemplate t) => new(
        t.Id,
        t.CategoryId,
        t.Category?.Name ?? "",
        t.Name,
        t.Description,
        t.Version,
        t.IsPublished,
        t.CreatedAt,
        t.Fields?.Select(f => new FormFieldDto(
            f.Id, f.FieldKey, f.Label, f.Placeholder, f.FieldType, f.DisplayOrder,
            f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.LayoutJson, f.DefaultValue,
            f.HelpText, f.ConditionalLogicJson, f.ConceptId)).ToList());
}

public class FormFieldCommandsHandler :
    IRequestHandler<AddFormFieldCommand, Result<FormFieldDto>>,
    IRequestHandler<UpdateFormFieldCommand, Result<FormFieldDto>>,
    IRequestHandler<DeleteFormFieldCommand, Result<bool>>,
    IRequestHandler<ReorderFormFieldsCommand, Result<bool>>
{
    private readonly IFormRepository _repo;

    public FormFieldCommandsHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FormFieldDto>> Handle(AddFormFieldCommand request, CancellationToken ct)
    {
        var field = FormField.Create(
            request.FormTemplateId,
            request.FieldKey,
            request.Label,
            request.FieldType,
            request.DisplayOrder,
            request.IsRequired,
            request.Placeholder,
            request.OptionsJson,
            request.ValidationRulesJson,
            request.DefaultValue,
            request.HelpText,
            request.ConditionalLogicJson,
            request.LayoutJson);

        await _repo.AddFieldAsync(field, ct);

        return Result<FormFieldDto>.Success(MapToDto(field));
    }

    public async Task<Result<FormFieldDto>> Handle(UpdateFormFieldCommand request, CancellationToken ct)
    {
        var field = await _repo.GetFieldByIdAsync(request.Id, ct);
        if (field == null)
            return Result<FormFieldDto>.Failure("Field not found");

        field.Update(
            request.Label,
            request.Placeholder,
            request.FieldType,
            request.DisplayOrder,
            request.IsRequired,
            request.OptionsJson,
            request.ValidationRulesJson,
            request.DefaultValue,
            request.HelpText,
            request.ConditionalLogicJson,
            request.LayoutJson);

        await _repo.UpdateFieldAsync(field, ct);

        return Result<FormFieldDto>.Success(MapToDto(field));
    }

    public async Task<Result<bool>> Handle(DeleteFormFieldCommand request, CancellationToken ct)
    {
        await _repo.DeleteFieldAsync(request.Id, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> Handle(ReorderFormFieldsCommand request, CancellationToken ct)
    {
        await _repo.ReorderFieldsAsync(request.FormTemplateId, request.FieldIds, ct);
        return Result<bool>.Success(true);
    }

    private static FormFieldDto MapToDto(FormField f) => new(
        f.Id, f.FieldKey, f.Label, f.Placeholder, f.FieldType, f.DisplayOrder,
        f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.LayoutJson, f.DefaultValue,
        f.HelpText, f.ConditionalLogicJson, f.ConceptId);
}

public class FormResponseCommandsHandler :
    IRequestHandler<SubmitFormResponseCommand, Result<FormResponseDto>>,
    IRequestHandler<UpdateFormResponseCommand, Result<FormResponseDto>>,
    IRequestHandler<UpdateFormResponseStatusCommand, Result<FormResponseDto>>,
    IRequestHandler<DeleteFormResponseCommand, Result<bool>>
{
    private readonly IFormRepository _repo;

    public FormResponseCommandsHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<FormResponseDto>> Handle(SubmitFormResponseCommand request, CancellationToken ct)
    {
        // Load template with fields for validation
        var template = await _repo.GetTemplateByIdAsync(request.FormTemplateId, true, ct);
        if (template == null)
            return Result<FormResponseDto>.Failure("Form template not found");

        // Server-side validation (skip for drafts)
        if (!request.IsDraft)
        {
            var validationErrors = ValidateFieldValues(template.Fields, request.FieldValues);
            if (validationErrors.Count > 0)
                return Result<FormResponseDto>.Failure(string.Join("; ", validationErrors));
        }

        var response = FormResponse.Create(
            request.FormTemplateId,
            request.SubmittedByUserId,
            request.PatientId,
            request.CycleId);

        // Add field values
        foreach (var fv in request.FieldValues)
        {
            response.AddFieldValue(
                fv.FormFieldId,
                fv.TextValue,
                fv.NumericValue,
                fv.DateValue,
                fv.BooleanValue,
                fv.JsonValue);

            // Add details if provided
            if (fv.Details != null && fv.Details.Any())
            {
                var fieldValue = response.FieldValues.Last(); // The one we just added
                foreach (var detail in fv.Details)
                {
                    fieldValue.AddDetail(detail.Value, detail.Label, detail.ConceptId);
                }
            }
        }

        if (request.IsDraft)
            response.AddNotes("Draft saved");
        else
            response.Submit();

        await _repo.AddResponseAsync(response, ct);

        // Upsert concept snapshots for cross-form linked data
        if (!request.IsDraft && request.PatientId.HasValue)
        {
            await UpsertConceptSnapshots(
                response, template.Fields, request.PatientId.Value, request.CycleId, ct);
        }

        response = await _repo.GetResponseByIdAsync(response.Id, true, ct);

        return Result<FormResponseDto>.Success(MapToDto(response!));
    }

    public async Task<Result<FormResponseDto>> Handle(UpdateFormResponseCommand request, CancellationToken ct)
    {
        var response = await _repo.GetResponseByIdAsync(request.Id, true, ct);
        if (response == null)
            return Result<FormResponseDto>.Failure("Response not found");

        // Update existing field values or add new ones
        var existingValues = response.FieldValues.ToDictionary(fv => fv.FormFieldId);

        foreach (var fv in request.FieldValues)
        {
            if (existingValues.TryGetValue(fv.FormFieldId, out var existing))
            {
                // Update existing value
                existing.Update(fv.TextValue, fv.NumericValue, fv.DateValue, fv.BooleanValue, fv.JsonValue);

                // Update details: Clear and re-add (simpler than syncing)
                existing.ClearDetails();
                if (fv.Details != null && fv.Details.Any())
                {
                    foreach (var detail in fv.Details)
                    {
                        existing.AddDetail(detail.Value, detail.Label, detail.ConceptId);
                    }
                }
            }
            else
            {
                // Add new value
                response.AddFieldValue(
                    fv.FormFieldId,
                    fv.TextValue,
                    fv.NumericValue,
                    fv.DateValue,
                    fv.BooleanValue,
                    fv.JsonValue);

                // Add details if provided
                if (fv.Details != null && fv.Details.Any())
                {
                    var fieldValue = response.FieldValues.Last();
                    foreach (var detail in fv.Details)
                    {
                        fieldValue.AddDetail(detail.Value, detail.Label, detail.ConceptId);
                    }
                }
            }
        }

        await _repo.UpdateResponseAsync(response, ct);

        // Upsert concept snapshots if response has patient and is submitted
        if (response.PatientId.HasValue && response.Status == ResponseStatus.Submitted)
        {
            var template = await _repo.GetTemplateByIdAsync(response.FormTemplateId, true, ct);
            if (template?.Fields != null)
            {
                await UpsertConceptSnapshots(
                    response, template.Fields, response.PatientId.Value, response.CycleId, ct);
            }
        }

        return Result<FormResponseDto>.Success(MapToDto(response));
    }

    public async Task<Result<FormResponseDto>> Handle(UpdateFormResponseStatusCommand request, CancellationToken ct)
    {
        var response = await _repo.GetResponseByIdAsync(request.Id, true, ct);
        if (response == null)
            return Result<FormResponseDto>.Failure("Response not found");

        switch (request.NewStatus)
        {
            case ResponseStatus.Reviewed:
                response.MarkAsReviewed();
                break;
            case ResponseStatus.Approved:
                response.Approve();
                break;
            case ResponseStatus.Rejected:
                response.Reject(request.Notes);
                break;
        }

        await _repo.UpdateResponseAsync(response, ct);

        return Result<FormResponseDto>.Success(MapToDto(response));
    }

    public async Task<Result<bool>> Handle(DeleteFormResponseCommand request, CancellationToken ct)
    {
        await _repo.DeleteResponseAsync(request.Id, ct);
        return Result<bool>.Success(true);
    }

    private static FormResponseDto MapToDto(FormResponse r) => new(
        r.Id,
        r.FormTemplateId,
        r.FormTemplate?.Name ?? "",
        r.PatientId,
        r.Patient?.FullName,
        r.CycleId,
        r.Status,
        r.Notes,
        r.CreatedAt,
        r.SubmittedAt,
        r.FieldValues?.Select(v => new FormFieldValueDto(
            v.Id, v.FormFieldId, v.FormField?.FieldKey, v.FormField?.Label,
            v.TextValue, v.NumericValue, v.DateValue, v.BooleanValue, v.JsonValue,
            v.Details?.Select(d => new FormFieldValueDetailDto(d.Value, d.Label, d.ConceptId)).ToList())).ToList());

    private static List<string> ValidateFieldValues(ICollection<FormField>? templateFields, List<FormFieldValueDto> fieldValues)
    {
        var errors = new List<string>();
        if (templateFields == null) return errors;

        var valuesByFieldId = fieldValues.ToDictionary(fv => fv.FormFieldId);

        foreach (var field in templateFields)
        {
            // Skip layout-only fields
            if (field.FieldType is FieldType.Section or FieldType.Label or FieldType.PageBreak)
                continue;

            valuesByFieldId.TryGetValue(field.Id, out var fv);
            var hasValue = fv != null && !IsEmpty(fv);

            // Required check
            if (field.IsRequired && !hasValue)
            {
                errors.Add($"Trường '{field.Label}' là bắt buộc");
                continue;
            }

            if (!hasValue || string.IsNullOrEmpty(field.ValidationRulesJson))
                continue;

            // Parse validation rules
            List<ValidationRuleEntry>? rules;
            try
            {
                rules = JsonSerializer.Deserialize<List<ValidationRuleEntry>>(
                    field.ValidationRulesJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { continue; }

            if (rules == null) continue;

            var textVal = fv!.TextValue ?? "";
            var numVal = fv.NumericValue;

            foreach (var rule in rules)
            {
                switch (rule.Type?.ToLowerInvariant())
                {
                    case "minlength":
                        if (int.TryParse(rule.Value?.ToString(), out var minLen) && textVal.Length < minLen)
                            errors.Add(rule.Message ?? $"Trường '{field.Label}' phải có ít nhất {minLen} ký tự");
                        break;
                    case "maxlength":
                        if (int.TryParse(rule.Value?.ToString(), out var maxLen) && textVal.Length > maxLen)
                            errors.Add(rule.Message ?? $"Trường '{field.Label}' không được quá {maxLen} ký tự");
                        break;
                    case "min":
                        if (decimal.TryParse(rule.Value?.ToString(), out var min) && numVal.HasValue && numVal.Value < min)
                            errors.Add(rule.Message ?? $"Trường '{field.Label}' phải lớn hơn hoặc bằng {min}");
                        break;
                    case "max":
                        if (decimal.TryParse(rule.Value?.ToString(), out var max) && numVal.HasValue && numVal.Value > max)
                            errors.Add(rule.Message ?? $"Trường '{field.Label}' phải nhỏ hơn hoặc bằng {max}");
                        break;
                    case "pattern":
                        var pattern = rule.Value?.ToString();
                        if (!string.IsNullOrEmpty(pattern) && !Regex.IsMatch(textVal, pattern))
                            errors.Add(rule.Message ?? $"Trường '{field.Label}' không đúng định dạng");
                        break;
                    case "email":
                        if (!string.IsNullOrEmpty(textVal) && !Regex.IsMatch(textVal, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                            errors.Add(rule.Message ?? $"Trường '{field.Label}' phải là email hợp lệ");
                        break;
                }
            }
        }

        return errors;
    }

    private static bool IsEmpty(FormFieldValueDto fv)
    {
        return string.IsNullOrWhiteSpace(fv.TextValue)
            && !fv.NumericValue.HasValue
            && !fv.DateValue.HasValue
            && !fv.BooleanValue.HasValue
            && string.IsNullOrWhiteSpace(fv.JsonValue);
    }

    /// <summary>
    /// After a form response is saved, upsert PatientConceptSnapshots for all fields that have a ConceptId.
    /// This materializes the latest known value per (Patient, Concept) for O(1) cross-form lookup.
    /// </summary>
    private async Task UpsertConceptSnapshots(
        FormResponse response,
        ICollection<FormField> templateFields,
        Guid patientId,
        Guid? cycleId,
        CancellationToken ct)
    {
        var fieldsWithConcept = templateFields
            .Where(f => f.ConceptId.HasValue)
            .ToDictionary(f => f.Id, f => f);

        if (fieldsWithConcept.Count == 0) return;

        var fieldValueMap = response.FieldValues?.ToDictionary(fv => fv.FormFieldId) ?? new();

        var snapshots = new List<PatientConceptSnapshot>();

        foreach (var (fieldId, field) in fieldsWithConcept)
        {
            if (!fieldValueMap.TryGetValue(fieldId, out var fv)) continue;

            // Skip empty values
            if (string.IsNullOrWhiteSpace(fv.TextValue)
                && !fv.NumericValue.HasValue
                && !fv.DateValue.HasValue
                && !fv.BooleanValue.HasValue
                && string.IsNullOrWhiteSpace(fv.JsonValue))
                continue;

            var snapshot = PatientConceptSnapshot.Create(
                patientId,
                field.ConceptId!.Value,
                response.Id,
                fieldId,
                cycleId,
                response.SubmittedAt ?? DateTime.UtcNow,
                fv.TextValue,
                fv.NumericValue,
                fv.DateValue,
                fv.BooleanValue,
                fv.JsonValue);

            snapshots.Add(snapshot);
        }

        if (snapshots.Count > 0)
        {
            await _repo.UpsertSnapshotsAsync(snapshots, ct);
        }
    }

    private record ValidationRuleEntry
    {
        public string? Type { get; init; }
        public object? Value { get; init; }
        public string? Message { get; init; }
    }
}

public class ReportTemplateCommandsHandler :
    IRequestHandler<CreateReportTemplateCommand, Result<ReportTemplateDto>>,
    IRequestHandler<UpdateReportTemplateCommand, Result<ReportTemplateDto>>,
    IRequestHandler<PublishReportTemplateCommand, Result<ReportTemplateDto>>,
    IRequestHandler<DeleteReportTemplateCommand, Result<bool>>
{
    private readonly IFormRepository _repo;

    public ReportTemplateCommandsHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<ReportTemplateDto>> Handle(CreateReportTemplateCommand request, CancellationToken ct)
    {
        var reportTemplate = ReportTemplate.Create(
            request.FormTemplateId,
            request.Name,
            request.ReportType,
            request.CreatedByUserId,
            request.Description,
            request.ConfigurationJson);

        await _repo.AddReportTemplateAsync(reportTemplate, ct);

        reportTemplate = await _repo.GetReportTemplateByIdAsync(reportTemplate.Id, ct);

        return Result<ReportTemplateDto>.Success(MapToDto(reportTemplate!));
    }

    public async Task<Result<ReportTemplateDto>> Handle(UpdateReportTemplateCommand request, CancellationToken ct)
    {
        var reportTemplate = await _repo.GetReportTemplateByIdAsync(request.Id, ct);
        if (reportTemplate == null)
            return Result<ReportTemplateDto>.Failure("Report template not found");

        reportTemplate.Update(request.Name, request.Description, request.ReportType, request.ConfigurationJson);
        await _repo.UpdateReportTemplateAsync(reportTemplate, ct);

        return Result<ReportTemplateDto>.Success(MapToDto(reportTemplate));
    }

    public async Task<Result<ReportTemplateDto>> Handle(PublishReportTemplateCommand request, CancellationToken ct)
    {
        var reportTemplate = await _repo.GetReportTemplateByIdAsync(request.Id, ct);
        if (reportTemplate == null)
            return Result<ReportTemplateDto>.Failure("Report template not found");

        reportTemplate.Publish();
        await _repo.UpdateReportTemplateAsync(reportTemplate, ct);

        return Result<ReportTemplateDto>.Success(MapToDto(reportTemplate));
    }

    public async Task<Result<bool>> Handle(DeleteReportTemplateCommand request, CancellationToken ct)
    {
        await _repo.DeleteReportTemplateAsync(request.Id, ct);
        return Result<bool>.Success(true);
    }

    private static ReportTemplateDto MapToDto(ReportTemplate r) => new(
        r.Id,
        r.FormTemplateId,
        r.FormTemplate?.Name ?? "",
        r.Name,
        r.Description,
        r.ReportType,
        r.ConfigurationJson,
        r.IsPublished,
        r.CreatedAt);
}

public class LinkedFieldSourceCommandsHandler :
    IRequestHandler<CreateLinkedFieldSourceCommand, Result<LinkedFieldSourceDto>>,
    IRequestHandler<UpdateLinkedFieldSourceCommand, Result<LinkedFieldSourceDto>>,
    IRequestHandler<DeleteLinkedFieldSourceCommand, Result<bool>>
{
    private readonly IFormRepository _repo;

    public LinkedFieldSourceCommandsHandler(IFormRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<LinkedFieldSourceDto>> Handle(CreateLinkedFieldSourceCommand request, CancellationToken ct)
    {
        var source = LinkedFieldSource.Create(
            request.TargetFieldId,
            request.SourceTemplateId,
            request.SourceFieldId,
            request.FlowType,
            request.Priority,
            request.Description);

        await _repo.AddLinkedFieldSourceAsync(source, ct);

        // Reload with navigation properties
        source = await _repo.GetLinkedFieldSourceByIdAsync(source.Id, ct);

        return Result<LinkedFieldSourceDto>.Success(MapToDto(source!));
    }

    public async Task<Result<LinkedFieldSourceDto>> Handle(UpdateLinkedFieldSourceCommand request, CancellationToken ct)
    {
        var source = await _repo.GetLinkedFieldSourceByIdAsync(request.Id, ct);
        if (source == null)
            return Result<LinkedFieldSourceDto>.Failure("Linked field source not found");

        source.Update(
            request.SourceTemplateId,
            request.SourceFieldId,
            request.FlowType,
            request.Priority,
            request.Description);

        await _repo.UpdateLinkedFieldSourceAsync(source, ct);

        return Result<LinkedFieldSourceDto>.Success(MapToDto(source));
    }

    public async Task<Result<bool>> Handle(DeleteLinkedFieldSourceCommand request, CancellationToken ct)
    {
        await _repo.DeleteLinkedFieldSourceAsync(request.Id, ct);
        return Result<bool>.Success(true);
    }

    private static LinkedFieldSourceDto MapToDto(LinkedFieldSource s) => new(
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
        s.CreatedAt);
}

#endregion
