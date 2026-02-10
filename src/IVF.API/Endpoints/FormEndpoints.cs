using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.API.Endpoints;

public static class FormEndpoints
{
    public static void MapFormEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/forms").WithTags("Forms").RequireAuthorization();

        #region Categories

        group.MapGet("/categories", async (IMediator m, bool activeOnly = true) =>
            Results.Ok(await m.Send(new GetFormCategoriesQuery(activeOnly))));

        group.MapGet("/categories/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetFormCategoryByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/categories", async (CreateFormCategoryRequest req, IMediator m) =>
        {
            var r = await m.Send(new CreateFormCategoryCommand(req.Name, req.Description, req.IconName, req.DisplayOrder));
            return r.IsSuccess ? Results.Created($"/api/forms/categories/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/categories/{id:guid}", async (Guid id, UpdateFormCategoryRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateFormCategoryCommand(id, req.Name, req.Description, req.IconName, req.DisplayOrder));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/categories/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeleteFormCategoryCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        #endregion

        #region Templates

        group.MapGet("/templates", async (IMediator m, Guid? categoryId = null, bool publishedOnly = false, bool includeFields = false) =>
            Results.Ok(await m.Send(new GetFormTemplatesQuery(categoryId, publishedOnly, includeFields))));

        group.MapGet("/templates/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetFormTemplateByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/templates", async (CreateFormTemplateRequest req, IMediator m) =>
        {
            var fields = req.Fields?.Select(f => new CreateFormFieldDto(
                f.FieldKey, f.Label, f.FieldType, f.DisplayOrder, f.IsRequired,
                f.Placeholder, f.OptionsJson, f.ValidationRulesJson, f.DefaultValue, f.HelpText, f.ConditionalLogicJson)).ToList();

            // Parse string GUIDs, treating empty strings as null
            Guid? categoryId = string.IsNullOrEmpty(req.CategoryId) ? null : Guid.TryParse(req.CategoryId, out var cid) ? cid : null;
            Guid? createdByUserId = string.IsNullOrEmpty(req.CreatedByUserId) ? null : Guid.TryParse(req.CreatedByUserId, out var uid) ? uid : null;

            var r = await m.Send(new CreateFormTemplateCommand(categoryId, req.Name, req.Description, createdByUserId, fields));
            return r.IsSuccess ? Results.Created($"/api/forms/templates/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/templates/{id:guid}", async (Guid id, UpdateFormTemplateRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateFormTemplateCommand(id, req.Name, req.Description, req.CategoryId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/templates/{id:guid}/publish", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new PublishFormTemplateCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/templates/{id:guid}/unpublish", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new UnpublishFormTemplateCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/templates/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeleteFormTemplateCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/templates/{id:guid}/duplicate", async (Guid id, DuplicateFormTemplateRequest? req, IMediator m) =>
        {
            var r = await m.Send(new DuplicateFormTemplateCommand(id, req?.NewName));
            return r.IsSuccess ? Results.Created($"/api/forms/templates/{r.Value!.Id}", r.Value) : Results.NotFound(r.Error);
        });

        #endregion

        #region Fields

        group.MapGet("/templates/{templateId:guid}/fields", async (Guid templateId, IMediator m) =>
            Results.Ok(await m.Send(new GetFormFieldsByTemplateQuery(templateId))));

        group.MapPost("/templates/{templateId:guid}/fields", async (Guid templateId, AddFormFieldRequest req, IMediator m) =>
        {
            var r = await m.Send(new AddFormFieldCommand(
                templateId, req.FieldKey, req.Label, req.FieldType, req.DisplayOrder, req.IsRequired,
                req.Placeholder, req.OptionsJson, req.ValidationRulesJson, req.DefaultValue, req.HelpText, req.ConditionalLogicJson, req.LayoutJson));
            return r.IsSuccess ? Results.Created($"/api/forms/fields/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/fields/{id:guid}", async (Guid id, UpdateFormFieldRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateFormFieldCommand(
                id, req.Label, req.FieldType, req.DisplayOrder, req.IsRequired,
                req.Placeholder, req.OptionsJson, req.ValidationRulesJson, req.DefaultValue, req.HelpText, req.ConditionalLogicJson, req.LayoutJson));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/fields/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeleteFormFieldCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/templates/{templateId:guid}/fields/reorder", async (Guid templateId, ReorderFieldsRequest req, IMediator m) =>
        {
            var r = await m.Send(new ReorderFormFieldsCommand(templateId, req.FieldIds));
            return r.IsSuccess ? Results.Ok() : Results.BadRequest(r.Error);
        });

        #endregion

        #region Responses

        group.MapGet("/responses", async (IMediator m, Guid? templateId = null, Guid? patientId = null, DateTime? from = null, DateTime? to = null, int? status = null, int page = 1, int pageSize = 20) =>
        {
            var statusFilter = status.HasValue ? (ResponseStatus)status.Value : (ResponseStatus?)null;
            var (items, total) = await m.Send(new GetFormResponsesQuery(templateId, patientId, from, to, statusFilter, page, pageSize));
            return Results.Ok(new { Items = items, Total = total });
        });

        group.MapGet("/responses/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetFormResponseByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/responses", async (SubmitFormResponseRequest req, IMediator m) =>
        {
            var fieldValues = req.FieldValues.Select(v => new FormFieldValueDto(
                null, v.FormFieldId, null, null, v.TextValue, v.NumericValue, v.DateValue, v.BooleanValue, v.JsonValue,
                v.Details?.Select(d => new FormFieldValueDetailDto(d.Value, d.Label, d.ConceptId)).ToList())).ToList();

            var r = await m.Send(new SubmitFormResponseCommand(req.FormTemplateId, req.SubmittedByUserId, req.PatientId, req.CycleId, fieldValues, req.IsDraft));
            return r.IsSuccess ? Results.Created($"/api/forms/responses/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        // Update existing response
        group.MapPut("/responses/{id:guid}", async (Guid id, SubmitFormResponseRequest req, IMediator m) =>
        {
            var fieldValues = req.FieldValues.Select(v => new FormFieldValueDto(
                null, v.FormFieldId, null, null, v.TextValue, v.NumericValue, v.DateValue, v.BooleanValue, v.JsonValue,
                v.Details?.Select(d => new FormFieldValueDetailDto(d.Value, d.Label, d.ConceptId)).ToList())).ToList();

            var r = await m.Send(new UpdateFormResponseCommand(id, fieldValues));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPut("/responses/{id:guid}/status", async (Guid id, UpdateResponseStatusRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateFormResponseStatusCommand(id, req.NewStatus, req.Notes));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/responses/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeleteFormResponseCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/responses/{id:guid}/export-pdf", async (Guid id, IMediator m) =>
        {
            var response = await m.Send(new GetFormResponseByIdQuery(id));
            if (response == null) return Results.NotFound();

            var template = await m.Send(new GetFormTemplateByIdQuery(response.FormTemplateId));
            if (template == null) return Results.NotFound();

            var pdfBytes = FormPdfService.GeneratePdf(response, template);
            var fileName = $"{template.Name.Replace(" ", "_")}_{response.CreatedAt:yyyyMMdd}.pdf";
            return Results.File(pdfBytes, "application/pdf", fileName);
        });

        #endregion

        #region Reports

        group.MapGet("/templates/{templateId:guid}/reports", async (Guid templateId, IMediator m) =>
            Results.Ok(await m.Send(new GetReportTemplatesQuery(templateId))));

        group.MapGet("/linked-data/{templateId:guid}", async (Guid templateId, Guid patientId, IMediator m, Guid? cycleId = null) =>
        {
            var result = await m.Send(new GetLinkedDataQuery(templateId, patientId, cycleId));
            return Results.Ok(result);
        });

        group.MapGet("/reports/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetReportTemplateByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapPost("/reports", async (CreateReportTemplateRequest req, IMediator m) =>
        {
            var r = await m.Send(new CreateReportTemplateCommand(
                req.FormTemplateId, req.Name, req.Description, req.ReportType, req.ConfigurationJson, req.CreatedByUserId));
            return r.IsSuccess ? Results.Created($"/api/forms/reports/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/reports/{id:guid}", async (Guid id, UpdateReportTemplateRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateReportTemplateCommand(id, req.Name, req.Description, req.ReportType, req.ConfigurationJson));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/reports/{id:guid}/publish", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new PublishReportTemplateCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/reports/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeleteReportTemplateCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/reports/{id:guid}/generate", async (Guid id, IMediator m, DateTime? from = null, DateTime? to = null, Guid? patientId = null) =>
        {
            try
            {
                var result = await m.Send(new GenerateReportQuery(id, from, to, patientId));
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(ex.Message);
            }
        });

        #endregion

        #region Files

        group.MapPost("/files/upload", async (HttpRequest httpReq, IFileStorageService fileStorage) =>
        {
            if (!httpReq.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data");

            var form = await httpReq.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded");

            // Validate file size (max 10MB)
            if (file.Length > 10 * 1024 * 1024)
                return Results.BadRequest("File size exceeds 10MB limit");

            using var stream = file.OpenReadStream();
            var result = await fileStorage.UploadAsync(stream, file.FileName, file.ContentType, "forms");
            return Results.Ok(result);
        }).DisableAntiforgery();

        group.MapGet("/files/{**filePath}", async (string filePath, IFileStorageService fileStorage) =>
        {
            var result = await fileStorage.GetAsync(filePath);
            if (result == null) return Results.NotFound();

            var (stream, contentType, fileName) = result.Value;
            return Results.File(stream, contentType, fileName);
        });

        group.MapDelete("/files/{**filePath}", async (string filePath, IFileStorageService fileStorage) =>
        {
            var deleted = await fileStorage.DeleteAsync(filePath);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        #endregion
    }
}

#region Request DTOs

public record CreateFormCategoryRequest(string Name, string? Description, string? IconName, int DisplayOrder = 0);
public record UpdateFormCategoryRequest(string Name, string? Description, string? IconName, int DisplayOrder);

public record CreateFormTemplateRequest(string? CategoryId, string Name, string? Description, string? CreatedByUserId, List<CreateFieldRequest>? Fields);
public record UpdateFormTemplateRequest(string Name, string? Description, Guid? CategoryId);
public record DuplicateFormTemplateRequest(string? NewName);

public record CreateFieldRequest(
    string FieldKey, string Label, FieldType FieldType, int DisplayOrder, bool IsRequired = false,
    string? Placeholder = null, string? OptionsJson = null, string? ValidationRulesJson = null,
    string? LayoutJson = null, string? DefaultValue = null, string? HelpText = null, string? ConditionalLogicJson = null);

public record AddFormFieldRequest(
    string FieldKey, string Label, FieldType FieldType, int DisplayOrder, bool IsRequired = false,
    string? Placeholder = null, string? OptionsJson = null, string? ValidationRulesJson = null,
    string? LayoutJson = null, string? DefaultValue = null, string? HelpText = null, string? ConditionalLogicJson = null);

public record UpdateFormFieldRequest(
    string Label, FieldType FieldType, int DisplayOrder, bool IsRequired,
    string? Placeholder, string? OptionsJson, string? ValidationRulesJson, string? LayoutJson,
    string? DefaultValue, string? HelpText, string? ConditionalLogicJson);

public record ReorderFieldsRequest(List<Guid> FieldIds);

public record SubmitFormResponseRequest(
    Guid FormTemplateId, Guid? SubmittedByUserId, Guid? PatientId, Guid? CycleId,
    List<FieldValueRequest> FieldValues, bool IsDraft = false);

public record FieldValueRequest(
    Guid FormFieldId, string? TextValue, decimal? NumericValue,
    DateTime? DateValue, bool? BooleanValue, string? JsonValue,
    List<FieldValueDetailRequest>? Details = null);

public record FieldValueDetailRequest(
    string Value, string? Label = null, Guid? ConceptId = null);

public record UpdateResponseStatusRequest(ResponseStatus NewStatus, string? Notes);

public record CreateReportTemplateRequest(
    Guid FormTemplateId, string Name, string? Description,
    ReportType ReportType, string ConfigurationJson, Guid CreatedByUserId);

public record UpdateReportTemplateRequest(
    string Name, string? Description, ReportType ReportType, string ConfigurationJson);

#endregion
