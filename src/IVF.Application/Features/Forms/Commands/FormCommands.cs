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
    string? ConditionalLogicJson = null
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
    string? ConditionalLogicJson
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
    Guid SubmittedByUserId,
    Guid? PatientId,
    Guid? CycleId,
    List<FormFieldValueDto> FieldValues
) : IRequest<Result<FormResponseDto>>;

public record UpdateFormResponseStatusCommand(
    Guid Id,
    ResponseStatus NewStatus,
    string? Notes = null
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
    string? JsonValue
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
    IRequestHandler<DeleteFormTemplateCommand, Result<bool>>
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
                    fieldDto.ConditionalLogicJson);
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
            f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.DefaultValue,
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
            request.ConditionalLogicJson);

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
            request.ConditionalLogicJson);

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
        f.IsRequired, f.OptionsJson, f.ValidationRulesJson, f.DefaultValue,
        f.HelpText, f.ConditionalLogicJson, f.ConceptId);
}

public class FormResponseCommandsHandler :
    IRequestHandler<SubmitFormResponseCommand, Result<FormResponseDto>>,
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
        }

        response.Submit();

        await _repo.AddResponseAsync(response, ct);

        response = await _repo.GetResponseByIdAsync(response.Id, true, ct);

        return Result<FormResponseDto>.Success(MapToDto(response!));
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
        r.CreatedAt,
        r.SubmittedAt,
        r.FieldValues?.Select(v => new FormFieldValueDto(
            v.Id, v.FormFieldId, v.FormField?.FieldKey, v.FormField?.Label,
            v.TextValue, v.NumericValue, v.DateValue, v.BooleanValue, v.JsonValue)).ToList());
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

#endregion
