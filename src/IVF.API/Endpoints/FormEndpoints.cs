using System.Security.Claims;
using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Documents.Commands;
using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.Queries;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/responses/{id:guid}/export-pdf", async (Guid id, IMediator m, IDigitalSigningService signingService, ClaimsPrincipal principal, IVF.Infrastructure.Persistence.IvfDbContext db, IObjectStorageService objectStorage, Guid? reportTemplateId = null, bool sign = false) =>
        {
            try
            {
                var response = await m.Send(new GetFormResponseByIdQuery(id));
                if (response == null) return Results.NotFound();

                var template = await m.Send(new GetFormTemplateByIdQuery(response.FormTemplateId));
                if (template == null) return Results.NotFound();

                // Check for band-based report designer template
                ReportTemplateDto? reportTemplate = null;
                if (reportTemplateId.HasValue)
                {
                    reportTemplate = await m.Send(new GetReportTemplateByIdQuery(reportTemplateId.Value));
                }
                else
                {
                    // Auto-discover: find the first band-based report template for this form
                    var reportTemplates = await m.Send(new GetReportTemplatesQuery(response.FormTemplateId));
                    reportTemplate = reportTemplates.FirstOrDefault(rt =>
                        !string.IsNullOrWhiteSpace(rt.ConfigurationJson)
                        && rt.ConfigurationJson.Contains("\"bands\""));
                }

                // If a band-based report template exists, use ReportPdfService
                if (reportTemplate != null
                    && !string.IsNullOrWhiteSpace(reportTemplate.ConfigurationJson)
                    && reportTemplate.ConfigurationJson.Contains("\"bands\""))
                {
                    // Convert single response to the data format ReportPdfService expects
                    var dataRow = new Dictionary<string, object?>
                    {
                        ["patientName"] = response.PatientName ?? "",
                        ["submittedAt"] = response.SubmittedAt?.ToString("o") ?? response.CreatedAt.ToString("o"),
                        ["status"] = response.Status.ToString()
                    };

                    // Add all field values as flat key-value pairs
                    if (response.FieldValues != null)
                    {
                        foreach (var fv in response.FieldValues)
                        {
                            var key = fv.FieldKey ?? fv.FormFieldId.ToString();
                            object? value = fv.NumericValue.HasValue ? fv.NumericValue.Value
                                : fv.DateValue.HasValue ? fv.DateValue.Value
                                : fv.BooleanValue.HasValue ? fv.BooleanValue.Value
                                : !string.IsNullOrEmpty(fv.JsonValue) ? fv.JsonValue
                                : fv.TextValue;
                            dataRow[key] = value;
                        }
                    }

                    var reportData = new ReportDataDto(
                        reportTemplate,
                        [dataRow],
                        null
                    );

                    // Load user signature for embedding in signatureZone controls
                    IVF.Domain.Entities.UserSignature? userSig = null;
                    var userId = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;

                    // ── Multi-signature: load all DocumentSignature records for this response ──
                    var docSignatures = sign
                        ? await db.DocumentSignatures
                            .Include(d => d.User)
                            .Where(d => d.FormResponseId == id && !d.IsDeleted)
                            .OrderBy(d => d.SignedAt)
                            .ToListAsync()
                        : new List<IVF.Domain.Entities.DocumentSignature>();

                    // Build per-role signature contexts from all signers
                    Dictionary<string, ReportPdfService.SignatureContext>? roleSignatures = null;
                    var signerWorkers = new List<(string WorkerName, string? SignerName)>();

                    if (sign && docSignatures.Count > 0)
                    {
                        roleSignatures = new();
                        var signerUserIds = docSignatures.Select(d => d.UserId).Distinct().ToList();
                        var signerSigs = await db.UserSignatures
                            .Include(s => s.User)
                            .Where(s => signerUserIds.Contains(s.UserId) && !s.IsDeleted && s.IsActive)
                            .ToListAsync();
                        var sigLookup = signerSigs.ToDictionary(s => s.UserId);

                        foreach (var ds in docSignatures)
                        {
                            if (sigLookup.TryGetValue(ds.UserId, out var sig)
                                && !string.IsNullOrEmpty(sig.SignatureImageBase64))
                            {
                                var base64 = sig.SignatureImageBase64;
                                if (base64.Contains(","))
                                    base64 = base64[(base64.IndexOf(',') + 1)..];

                                roleSignatures[ds.SignatureRole] = new ReportPdfService.SignatureContext(
                                    SignerName: ds.User?.FullName ?? sig.User?.FullName,
                                    SignatureImageBytes: Convert.FromBase64String(base64),
                                    SignedDate: ds.SignedAt.ToString("dd/MM/yyyy"));

                                // Collect worker info for multi crypto signing
                                var workerName = sig.CertStatus == IVF.Domain.Entities.CertificateStatus.Active
                                    && !string.IsNullOrEmpty(sig.WorkerName)
                                    ? sig.WorkerName
                                    : null;
                                signerWorkers.Add((workerName ?? "__default__", ds.User?.FullName));
                            }
                        }
                    }

                    // Fallback: if no DocumentSignatures exist but sign=true, use current user (backward compat)
                    ReportPdfService.SignatureContext? sigContext = null;
                    if (sign && (roleSignatures == null || roleSignatures.Count == 0))
                    {
                        if (!userId.HasValue)
                            return Results.BadRequest(new { error = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

                        userSig = await db.UserSignatures
                            .Include(s => s.User)
                            .Where(s => s.UserId == userId.Value && !s.IsDeleted && s.IsActive)
                            .FirstOrDefaultAsync();

                        if (userSig == null || string.IsNullOrEmpty(userSig.SignatureImageBase64))
                            return Results.BadRequest(new { error = "Bạn chưa tải lên chữ ký số. Vui lòng vào Quản trị → Ký số → tab Chữ ký để tải lên chữ ký của bạn." });

                        var base64 = userSig.SignatureImageBase64;
                        if (base64.Contains(","))
                            base64 = base64[(base64.IndexOf(',') + 1)..];

                        sigContext = new ReportPdfService.SignatureContext(
                            SignerName: userSig.User?.FullName,
                            SignatureImageBytes: Convert.FromBase64String(base64),
                            SignedDate: DateTime.Now.ToString("dd/MM/yyyy"));

                        var workerName = userSig.CertStatus == IVF.Domain.Entities.CertificateStatus.Active
                            && !string.IsNullOrEmpty(userSig.WorkerName)
                            ? userSig.WorkerName
                            : null;
                        signerWorkers.Add((workerName ?? "__default__", userSig.User?.FullName));
                    }

                    var pdfBytes = ReportPdfService.GenerateReportPdf(reportData,
                        signatureContext: sigContext,
                        roleSignatures: roleSignatures);

                    // Cryptographic digital signing via SignServer — multiple incremental signatures
                    if (sign && signingService.IsEnabled && signerWorkers.Count > 0)
                    {
                        // Deduplicate workers (same certificate doesn't need to sign twice)
                        var uniqueWorkers = signerWorkers
                            .GroupBy(w => w.WorkerName)
                            .Select(g => g.First())
                            .ToList();

                        foreach (var (workerName, signerName) in uniqueWorkers)
                        {
                            var metadata = new SigningMetadata(
                                Reason: $"Xác nhận phiếu: {template.Name}",
                                Location: "IVF Clinic",
                                SignerName: signerName ?? response.PatientName);

                            if (workerName != "__default__")
                            {
                                pdfBytes = await signingService.SignPdfWithUserAsync(
                                    pdfBytes, workerName, metadata);
                            }
                            else
                            {
                                pdfBytes = await signingService.SignPdfAsync(pdfBytes, metadata);
                            }
                        }
                    }

                    // ─── Lưu PDF ký số vào MinIO (bệnh nhân) ───
                    if (sign && response.PatientId.HasValue && pdfBytes.Length > 0)
                    {
                        var patientCode = response.PatientName ?? response.PatientId.Value.ToString("N")[..8];
                        // Try to get actual patient code from DB
                        var patient = await db.Patients
                            .AsNoTracking()
                            .Where(p => p.Id == response.PatientId.Value && !p.IsDeleted)
                            .Select(p => new { p.PatientCode, p.FullName })
                            .FirstOrDefaultAsync();

                        if (patient != null)
                            patientCode = patient.PatientCode;

                        var signerNamesList = string.Join(", ",
                            signerWorkers.Select(w => w.SignerName).Where(n => !string.IsNullOrEmpty(n)));

                        var userId3 = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid3) ? uid3 : (Guid?)null;

                        try
                        {
                            var storeResult = await m.Send(new StoreSignedFormPdfCommand(
                                FormResponseId: id,
                                PatientId: response.PatientId!.Value,
                                PatientCode: patientCode,
                                TemplateName: template.Name,
                                SignedPdfBytes: pdfBytes,
                                SignerNames: signerNamesList,
                                FormTemplateCode: template.Code,
                                UploadedByUserId: userId3,
                                CycleId: response.CycleId));

                            if (!storeResult.IsSuccess)
                            {
                                // Log nhưng không fail response – PDF vẫn trả về cho user
                                Console.Error.WriteLine($"[MinIO] Lưu PDF ký số thất bại: {storeResult.Error}");
                            }
                        }
                        catch (Exception storeEx)
                        {
                            Console.Error.WriteLine($"[MinIO] Exception khi lưu PDF ký số: {storeEx.Message}");
                        }
                    }

                    var fileName = $"{template.Name.Replace(" ", "_")}_{response.CreatedAt:yyyyMMdd}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileName);
                }
                else
                {
                    // Fallback to hardcoded FormPdfService
                    var pdfBytes = FormPdfService.GeneratePdf(response, template);

                    // Digitally sign if requested and signing is enabled
                    if (sign && signingService.IsEnabled)
                    {
                        var metadata = new SigningMetadata(
                            Reason: $"Xác nhận phiếu: {template.Name}",
                            Location: "IVF Clinic",
                            SignerName: response.PatientName);

                        var userId2 = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid2) ? uid2 : (Guid?)null;
                        var userSig2 = userId2.HasValue
                            ? await db.UserSignatures
                                .Where(s => s.UserId == userId2.Value && !s.IsDeleted && s.IsActive && s.WorkerName != null)
                                .FirstOrDefaultAsync()
                            : null;

                        if (userSig2 != null && userSig2.CertStatus == IVF.Domain.Entities.CertificateStatus.Active)
                        {
                            byte[]? sigImage2 = !string.IsNullOrEmpty(userSig2.SignatureImageBase64)
                                ? Convert.FromBase64String(userSig2.SignatureImageBase64) : null;
                            pdfBytes = await signingService.SignPdfWithUserAsync(
                                pdfBytes, userSig2.WorkerName!, metadata, sigImage2);
                        }
                        else
                        {
                            pdfBytes = await signingService.SignPdfAsync(pdfBytes, metadata);
                        }

                        // ─── Lưu PDF ký số vào MinIO (bệnh nhân) ───
                        if (response.PatientId.HasValue && pdfBytes.Length > 0)
                        {
                            var patientForFallback = await db.Patients
                                .AsNoTracking()
                                .Where(p => p.Id == response.PatientId.Value && !p.IsDeleted)
                                .Select(p => new { p.PatientCode })
                                .FirstOrDefaultAsync();

                            if (patientForFallback != null)
                            {
                                var signerNameFallback = userSig2?.User?.FullName ?? response.PatientName;
                                try
                                {
                                    var storeResult2 = await m.Send(new StoreSignedFormPdfCommand(
                                        FormResponseId: id,
                                        PatientId: response.PatientId!.Value,
                                        PatientCode: patientForFallback.PatientCode,
                                        TemplateName: template.Name,
                                        SignedPdfBytes: pdfBytes,
                                        SignerNames: signerNameFallback,
                                        FormTemplateCode: template.Code,
                                        UploadedByUserId: userId2,
                                        CycleId: response.CycleId));

                                    if (!storeResult2.IsSuccess)
                                        Console.Error.WriteLine($"[MinIO] Lưu PDF ký số thất bại (fallback): {storeResult2.Error}");
                                }
                                catch (Exception storeEx2)
                                {
                                    Console.Error.WriteLine($"[MinIO] Exception khi lưu PDF ký số (fallback): {storeEx2.Message}");
                                }
                            }
                        }
                    }

                    var fileName = $"{template.Name.Replace(" ", "_")}_{response.CreatedAt:yyyyMMdd}.pdf";
                    return Results.File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.ToString(),
                    statusCode: 500,
                    title: "PDF generation failed");
            }
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

        group.MapPost("/reports", async (CreateReportTemplateRequest req, ClaimsPrincipal principal, IMediator m) =>
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var r = await m.Send(new CreateReportTemplateCommand(
                req.FormTemplateId, req.Name, req.Description, req.ReportType, req.ConfigurationJson, userId));
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

        group.MapGet("/reports/{id:guid}/export-pdf", async (Guid id, IMediator m, IDigitalSigningService signingService, ClaimsPrincipal principal, IVF.Infrastructure.Persistence.IvfDbContext db, DateTime? from = null, DateTime? to = null, Guid? patientId = null, bool sign = false) =>
        {
            try
            {
                var result = await m.Send(new GenerateReportQuery(id, from, to, patientId));

                // Load user signature for embedding in signatureZone controls
                IVF.Domain.Entities.UserSignature? userSig = null;
                var userId = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;
                if (sign)
                {
                    if (!userId.HasValue)
                        return Results.BadRequest(new { error = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

                    userSig = await db.UserSignatures
                        .Include(s => s.User)
                        .Where(s => s.UserId == userId.Value && !s.IsDeleted && s.IsActive)
                        .FirstOrDefaultAsync();

                    if (userSig == null || string.IsNullOrEmpty(userSig.SignatureImageBase64))
                        return Results.BadRequest(new { error = "Bạn chưa tải lên chữ ký số. Vui lòng vào Quản trị → Ký số → tab Chữ ký để tải lên chữ ký của bạn." });
                }

                ReportPdfService.SignatureContext? sigContext = null;
                if (sign && userSig != null)
                {
                    // Strip data URI prefix if present
                    var base64 = userSig.SignatureImageBase64;
                    if (base64.Contains(","))
                        base64 = base64[(base64.IndexOf(',') + 1)..];

                    sigContext = new ReportPdfService.SignatureContext(
                        SignerName: userSig.User?.FullName,
                        SignatureImageBytes: Convert.FromBase64String(base64),
                        SignedDate: DateTime.Now.ToString("dd/MM/yyyy"));
                }

                var pdfBytes = ReportPdfService.GenerateReportPdf(result, from, to, sigContext, roleSignatures: null);

                // Cryptographic digital signing via SignServer
                if (sign && signingService.IsEnabled)
                {
                    var metadata = new SigningMetadata(
                        Reason: $"Xác nhận báo cáo: {result.Template.Name}",
                        Location: "IVF Clinic",
                        SignerName: userSig?.User?.FullName);

                    if (userSig != null && userSig.CertStatus == IVF.Domain.Entities.CertificateStatus.Active
                        && !string.IsNullOrEmpty(userSig.WorkerName))
                    {
                        pdfBytes = await signingService.SignPdfWithUserAsync(
                            pdfBytes, userSig.WorkerName, metadata);
                    }
                    else
                    {
                        pdfBytes = await signingService.SignPdfAsync(pdfBytes, metadata);
                    }
                }

                var fileName = $"Report_{result.Template.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                return Results.File(pdfBytes, "application/pdf", fileName);
            }
            catch (ArgumentException ex)
            {
                return Results.NotFound(ex.Message);
            }
        });

        #endregion

        #region Linked Field Sources

        group.MapGet("/templates/{templateId:guid}/linked-sources", async (Guid templateId, IMediator m) =>
            Results.Ok(await m.Send(new GetLinkedFieldSourcesQuery(templateId))));

        group.MapPost("/linked-sources", async (CreateLinkedFieldSourceRequest req, IMediator m) =>
        {
            var r = await m.Send(new CreateLinkedFieldSourceCommand(
                req.TargetFieldId, req.SourceTemplateId, req.SourceFieldId,
                req.FlowType, req.Priority, req.Description));
            return r.IsSuccess ? Results.Created($"/api/forms/linked-sources/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/linked-sources/{id:guid}", async (Guid id, UpdateLinkedFieldSourceRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateLinkedFieldSourceCommand(
                id, req.SourceTemplateId, req.SourceFieldId,
                req.FlowType, req.Priority, req.Description));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/linked-sources/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeleteLinkedFieldSourceCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound();
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
    ReportType ReportType, string ConfigurationJson);

public record UpdateReportTemplateRequest(
    string Name, string? Description, ReportType ReportType, string ConfigurationJson);

public record CreateLinkedFieldSourceRequest(
    Guid TargetFieldId, Guid SourceTemplateId, Guid SourceFieldId,
    DataFlowType FlowType = DataFlowType.Suggest, int Priority = 0, string? Description = null);

public record UpdateLinkedFieldSourceRequest(
    Guid? SourceTemplateId = null, Guid? SourceFieldId = null,
    DataFlowType? FlowType = null, int? Priority = null, string? Description = null);

#endregion
