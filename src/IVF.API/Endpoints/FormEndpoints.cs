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

        #endregion

        #region Fields

        group.MapGet("/templates/{templateId:guid}/fields", async (Guid templateId, IMediator m) =>
            Results.Ok(await m.Send(new GetFormFieldsByTemplateQuery(templateId))));

        group.MapPost("/templates/{templateId:guid}/fields", async (Guid templateId, AddFormFieldRequest req, IMediator m) =>
        {
            var r = await m.Send(new AddFormFieldCommand(
                templateId, req.FieldKey, req.Label, req.FieldType, req.DisplayOrder, req.IsRequired,
                req.Placeholder, req.OptionsJson, req.ValidationRulesJson, req.DefaultValue, req.HelpText, req.ConditionalLogicJson));
            return r.IsSuccess ? Results.Created($"/api/forms/fields/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/fields/{id:guid}", async (Guid id, UpdateFormFieldRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateFormFieldCommand(
                id, req.Label, req.FieldType, req.DisplayOrder, req.IsRequired,
                req.Placeholder, req.OptionsJson, req.ValidationRulesJson, req.DefaultValue, req.HelpText, req.ConditionalLogicJson));
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

        group.MapGet("/responses", async (IMediator m, Guid? templateId = null, Guid? patientId = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 20) =>
        {
            var (items, total) = await m.Send(new GetFormResponsesQuery(templateId, patientId, from, to, page, pageSize));
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

            var r = await m.Send(new SubmitFormResponseCommand(req.FormTemplateId, req.SubmittedByUserId, req.PatientId, req.CycleId, fieldValues));
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

        #endregion

        #region Reports

        group.MapGet("/templates/{templateId:guid}/reports", async (Guid templateId, IMediator m) =>
            Results.Ok(await m.Send(new GetReportTemplatesQuery(templateId))));

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
    }
}

#region Request DTOs

public record CreateFormCategoryRequest(string Name, string? Description, string? IconName, int DisplayOrder = 0);
public record UpdateFormCategoryRequest(string Name, string? Description, string? IconName, int DisplayOrder);

public record CreateFormTemplateRequest(string? CategoryId, string Name, string? Description, string? CreatedByUserId, List<CreateFieldRequest>? Fields);
public record UpdateFormTemplateRequest(string Name, string? Description, Guid? CategoryId);

public record CreateFieldRequest(
    string FieldKey, string Label, FieldType FieldType, int DisplayOrder, bool IsRequired = false,
    string? Placeholder = null, string? OptionsJson = null, string? ValidationRulesJson = null,
    string? DefaultValue = null, string? HelpText = null, string? ConditionalLogicJson = null);

public record AddFormFieldRequest(
    string FieldKey, string Label, FieldType FieldType, int DisplayOrder, bool IsRequired = false,
    string? Placeholder = null, string? OptionsJson = null, string? ValidationRulesJson = null,
    string? DefaultValue = null, string? HelpText = null, string? ConditionalLogicJson = null);

public record UpdateFormFieldRequest(
    string Label, FieldType FieldType, int DisplayOrder, bool IsRequired,
    string? Placeholder, string? OptionsJson, string? ValidationRulesJson,
    string? DefaultValue, string? HelpText, string? ConditionalLogicJson);

public record ReorderFieldsRequest(List<Guid> FieldIds);

public record SubmitFormResponseRequest(
    Guid FormTemplateId, Guid? SubmittedByUserId, Guid? PatientId, Guid? CycleId,
    List<FieldValueRequest> FieldValues);

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
