using System.Security.Claims;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Documents.Commands;
using IVF.API.Services;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class AmendmentEndpoints
{
    public static void MapAmendmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/forms/responses")
            .WithTags("Document Amendments")
            .RequireAuthorization();

        var amendGroup = app.MapGroup("/api/amendments")
            .WithTags("Document Amendments")
            .RequireAuthorization();

        // ─── Create amendment request for a signed form response ───
        group.MapPost("/{responseId:guid}/amendments", CreateAmendmentRequest);

        // ─── Get amendment history for a form response ───
        group.MapGet("/{responseId:guid}/amendments", GetAmendmentHistory);

        // ─── Get single amendment detail with diff ───
        amendGroup.MapGet("/{amendmentId:guid}", GetAmendmentDetail);

        // ─── Get all pending amendments (approval dashboard) ───
        amendGroup.MapGet("/pending", GetPendingAmendments);

        // ─── Approve amendment ───
        amendGroup.MapPost("/{amendmentId:guid}/approve", ApproveAmendment);

        // ─── Reject amendment ───
        amendGroup.MapPost("/{amendmentId:guid}/reject", RejectAmendment);
    }

    // ═══════════════════════════════════════════════════════════
    // CREATE AMENDMENT REQUEST
    // ═══════════════════════════════════════════════════════════
    private static async Task<IResult> CreateAmendmentRequest(
        Guid responseId,
        CreateAmendmentApiRequest request,
        ClaimsPrincipal principal,
        IvfDbContext db,
        IUserPermissionRepository permRepo)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
            return Results.BadRequest(new { error = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

        // ─── Permission check: must have RequestAmendment permission ───
        var userRole = principal.FindFirst(ClaimTypes.Role)?.Value;
        var isPlatformAdmin = principal.FindFirst("platform_admin")?.Value == "true";
        if (!isPlatformAdmin && !string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var hasPerm = await permRepo.HasPermissionAsync(userId.Value, "RequestAmendment");
            if (!hasPerm)
                return Results.Json(new { error = "Bạn không có quyền yêu cầu chỉnh sửa." }, statusCode: 403);
        }

        // Validate form response exists and has signatures
        var formResponse = await db.FormResponses
            .Include(r => r.FieldValues)
                .ThenInclude(fv => fv.FormField)
            .FirstOrDefaultAsync(r => r.Id == responseId && !r.IsDeleted);

        if (formResponse == null)
            return Results.NotFound(new { error = "Không tìm thấy phiếu." });

        var hasSignatures = await db.DocumentSignatures
            .AnyAsync(d => d.FormResponseId == responseId && !d.IsDeleted);

        if (!hasSignatures)
            return Results.BadRequest(new { error = "Phiếu này chưa được ký số. Chỉ có thể tạo yêu cầu chỉnh sửa cho phiếu đã ký." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Results.BadRequest(new { error = "Vui lòng nhập lý do chỉnh sửa." });

        if (request.FieldChanges == null || request.FieldChanges.Count == 0)
            return Results.BadRequest(new { error = "Chưa có thay đổi nào." });

        // Check for existing pending amendment
        var hasPending = await db.SignedDocumentAmendments
            .AnyAsync(a => a.FormResponseId == responseId
                && a.Status == AmendmentStatus.Pending
                && !a.IsDeleted);

        if (hasPending)
            return Results.Conflict(new { error = "Đã có yêu cầu chỉnh sửa đang chờ duyệt cho phiếu này." });

        // Calculate next version
        var maxVersion = await db.SignedDocumentAmendments
            .Where(a => a.FormResponseId == responseId && !a.IsDeleted)
            .MaxAsync(a => (int?)a.Version) ?? 0;

        // Build old values snapshot
        var oldValuesDict = formResponse.FieldValues
            .Where(fv => !fv.IsDeleted)
            .ToDictionary(
                fv => fv.FormFieldId.ToString(),
                fv => new
                {
                    fieldKey = fv.FormField?.FieldKey,
                    fieldLabel = fv.FormField?.Label,
                    textValue = fv.TextValue,
                    numericValue = fv.NumericValue,
                    dateValue = fv.DateValue,
                    booleanValue = fv.BooleanValue,
                    jsonValue = fv.JsonValue
                });
        var oldSnapshot = JsonSerializer.Serialize(oldValuesDict);

        // Create amendment entity
        var amendment = SignedDocumentAmendment.Create(
            responseId,
            userId.Value,
            maxVersion + 1,
            request.Reason,
            oldSnapshot);

        // Set tenant from form response
        amendment.SetTenantId(formResponse.TenantId);

        // Add field changes with old/new comparison
        foreach (var change in request.FieldChanges)
        {
            var existingValue = formResponse.FieldValues
                .FirstOrDefault(fv => fv.FormFieldId == change.FormFieldId && !fv.IsDeleted);

            var field = existingValue?.FormField
                ?? await db.FormFields.FirstOrDefaultAsync(f => f.Id == change.FormFieldId);

            if (field == null) continue;

            var changeType = existingValue == null ? FieldChangeType.Added : FieldChangeType.Modified;

            amendment.AddFieldChange(
                change.FormFieldId,
                field.FieldKey,
                field.Label,
                changeType,
                oldTextValue: existingValue?.TextValue,
                newTextValue: change.NewTextValue,
                oldNumericValue: existingValue?.NumericValue,
                newNumericValue: change.NewNumericValue,
                oldDateValue: existingValue?.DateValue,
                newDateValue: change.NewDateValue,
                oldBooleanValue: existingValue?.BooleanValue,
                newBooleanValue: change.NewBooleanValue,
                oldJsonValue: existingValue?.JsonValue,
                newJsonValue: change.NewJsonValue);
        }

        // Build new values snapshot
        var newValuesDict = new Dictionary<string, object?>();
        foreach (var fc in amendment.FieldChanges)
        {
            newValuesDict[fc.FormFieldId.ToString()] = new
            {
                fieldKey = fc.FieldKey,
                fieldLabel = fc.FieldLabel,
                textValue = fc.NewTextValue,
                numericValue = fc.NewNumericValue,
                dateValue = fc.NewDateValue,
                booleanValue = fc.NewBooleanValue,
                jsonValue = fc.NewJsonValue
            };
        }

        // We need to use reflection or a method to set NewValuesSnapshot since it has private setter
        // Instead, create with snapshot
        var amendmentWithSnapshot = SignedDocumentAmendment.Create(
            responseId,
            userId.Value,
            maxVersion + 1,
            request.Reason,
            oldSnapshot,
            JsonSerializer.Serialize(newValuesDict));

        amendmentWithSnapshot.SetTenantId(formResponse.TenantId);

        // Re-add field changes to the new entity
        foreach (var change in request.FieldChanges)
        {
            var existingValue = formResponse.FieldValues
                .FirstOrDefault(fv => fv.FormFieldId == change.FormFieldId && !fv.IsDeleted);

            var field = existingValue?.FormField
                ?? await db.FormFields.FirstOrDefaultAsync(f => f.Id == change.FormFieldId);

            if (field == null) continue;

            var changeType = existingValue == null ? FieldChangeType.Added : FieldChangeType.Modified;

            amendmentWithSnapshot.AddFieldChange(
                change.FormFieldId,
                field.FieldKey,
                field.Label,
                changeType,
                oldTextValue: existingValue?.TextValue,
                newTextValue: change.NewTextValue,
                oldNumericValue: existingValue?.NumericValue,
                newNumericValue: change.NewNumericValue,
                oldDateValue: existingValue?.DateValue,
                newDateValue: change.NewDateValue,
                oldBooleanValue: existingValue?.BooleanValue,
                newBooleanValue: change.NewBooleanValue,
                oldJsonValue: existingValue?.JsonValue,
                newJsonValue: change.NewJsonValue);
        }

        db.SignedDocumentAmendments.Add(amendmentWithSnapshot);
        await db.SaveChangesAsync();

        return Results.Created($"/api/amendments/{amendmentWithSnapshot.Id}",
            MapToDto(amendmentWithSnapshot));
    }

    // ═══════════════════════════════════════════════════════════
    // GET AMENDMENT HISTORY
    // ═══════════════════════════════════════════════════════════
    private static async Task<IResult> GetAmendmentHistory(
        Guid responseId,
        IvfDbContext db)
    {
        var amendments = await db.SignedDocumentAmendments
            .Include(a => a.RequestedByUser)
            .Include(a => a.ReviewedByUser)
            .Include(a => a.FieldChanges)
            .Where(a => a.FormResponseId == responseId && !a.IsDeleted)
            .OrderByDescending(a => a.Version)
            .ToListAsync();

        return Results.Ok(amendments.Select(MapToDto).ToList());
    }

    // ═══════════════════════════════════════════════════════════
    // GET AMENDMENT DETAIL
    // ═══════════════════════════════════════════════════════════
    private static async Task<IResult> GetAmendmentDetail(
        Guid amendmentId,
        IvfDbContext db)
    {
        var amendment = await db.SignedDocumentAmendments
            .Include(a => a.RequestedByUser)
            .Include(a => a.ReviewedByUser)
            .Include(a => a.FieldChanges)
            .FirstOrDefaultAsync(a => a.Id == amendmentId && !a.IsDeleted);

        if (amendment == null)
            return Results.NotFound(new { error = "Không tìm thấy yêu cầu chỉnh sửa." });

        return Results.Ok(MapToDto(amendment));
    }

    // ═══════════════════════════════════════════════════════════
    // GET PENDING AMENDMENTS
    // ═══════════════════════════════════════════════════════════
    private static async Task<IResult> GetPendingAmendments(
        int page,
        int pageSize,
        IvfDbContext db)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var query = db.SignedDocumentAmendments
            .Include(a => a.RequestedByUser)
            .Include(a => a.FieldChanges)
            .Where(a => a.Status == AmendmentStatus.Pending && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Results.Ok(new
        {
            items = items.Select(MapToDto).ToList(),
            totalCount,
            page,
            pageSize
        });
    }

    // ═══════════════════════════════════════════════════════════
    // APPROVE AMENDMENT
    // ═══════════════════════════════════════════════════════════
    private static async Task<IResult> ApproveAmendment(
        Guid amendmentId,
        ApproveRejectRequest request,
        ClaimsPrincipal principal,
        IvfDbContext db,
        IMediator mediator,
        IUserPermissionRepository permRepo,
        SignedPdfGenerationService pdfGenService)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
            return Results.BadRequest(new { error = "Không xác định được người dùng." });

        // ─── Permission check: must have ApproveAmendment permission ───
        var userRole = principal.FindFirst(ClaimTypes.Role)?.Value;
        var isPlatformAdmin = principal.FindFirst("platform_admin")?.Value == "true";
        if (!isPlatformAdmin && !string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var hasPerm = await permRepo.HasPermissionAsync(userId.Value, "ApproveAmendment");
            if (!hasPerm)
                return Results.Json(new { error = "Bạn không có quyền phê duyệt chỉnh sửa." }, statusCode: 403);
        }

        var amendment = await db.SignedDocumentAmendments
            .Include(a => a.FieldChanges)
            .FirstOrDefaultAsync(a => a.Id == amendmentId && !a.IsDeleted);

        if (amendment == null)
            return Results.NotFound(new { error = "Không tìm thấy yêu cầu chỉnh sửa." });

        if (amendment.Status != AmendmentStatus.Pending)
            return Results.BadRequest(new { error = "Yêu cầu này đã được xử lý." });

        if (amendment.RequestedByUserId == userId.Value)
            return Results.BadRequest(new { error = "Không thể tự phê duyệt yêu cầu chỉnh sửa của mình." });

        // ─── Apply field changes to FormResponse ───
        var formResponse = await db.FormResponses
            .Include(r => r.FieldValues)
            .FirstOrDefaultAsync(r => r.Id == amendment.FormResponseId && !r.IsDeleted);

        if (formResponse == null)
            return Results.NotFound(new { error = "Không tìm thấy phiếu gốc." });

        foreach (var change in amendment.FieldChanges.Where(fc => !fc.IsDeleted))
        {
            var existingValue = formResponse.FieldValues
                .FirstOrDefault(fv => fv.FormFieldId == change.FormFieldId && !fv.IsDeleted);

            if (change.ChangeType == FieldChangeType.Added)
            {
                // Add new field value
                formResponse.AddFieldValue(
                    change.FormFieldId,
                    change.NewTextValue,
                    change.NewNumericValue,
                    change.NewDateValue,
                    change.NewBooleanValue,
                    change.NewJsonValue);
            }
            else if (change.ChangeType == FieldChangeType.Modified && existingValue != null)
            {
                // Update existing field value
                existingValue.Update(
                    change.NewTextValue,
                    change.NewNumericValue,
                    change.NewDateValue,
                    change.NewBooleanValue,
                    change.NewJsonValue);
            }
            else if (change.ChangeType == FieldChangeType.Removed && existingValue != null)
            {
                existingValue.MarkAsDeleted();
            }
        }

        // ─── Revoke all existing signatures (soft delete) ───
        var existingSignatures = await db.DocumentSignatures
            .Where(d => d.FormResponseId == amendment.FormResponseId && !d.IsDeleted)
            .ToListAsync();

        foreach (var sig in existingSignatures)
        {
            sig.Revoke($"Thu hồi do chỉnh sửa phiếu (phiên bản {amendment.Version})");
        }

        // ─── Invalidate stored signed PDF ───
        await mediator.Send(new InvalidateStoredSignedPdfCommand(amendment.FormResponseId));

        // ─── Mark amendment as approved ───
        amendment.Approve(userId.Value, request.Notes);
        await db.SaveChangesAsync();

        // Reload with navigation for DTO mapping
        await db.Entry(amendment).Reference(a => a.RequestedByUser).LoadAsync();
        await db.Entry(amendment).Reference(a => a.ReviewedByUser).LoadAsync();

        return Results.Ok(new
        {
            amendment = MapToDto(amendment),
            message = "Đã phê duyệt chỉnh sửa. Dữ liệu đã được cập nhật, chữ ký cũ đã bị thu hồi. Vui lòng ký số lại."
        });
    }

    // ═══════════════════════════════════════════════════════════
    // REJECT AMENDMENT
    // ═══════════════════════════════════════════════════════════
    private static async Task<IResult> RejectAmendment(
        Guid amendmentId,
        ApproveRejectRequest request,
        ClaimsPrincipal principal,
        IvfDbContext db,
        IUserPermissionRepository permRepo)
    {
        var userId = GetUserId(principal);
        if (!userId.HasValue)
            return Results.BadRequest(new { error = "Không xác định được người dùng." });

        // ─── Permission check: must have ApproveAmendment permission ───
        var userRole = principal.FindFirst(ClaimTypes.Role)?.Value;
        var isPlatformAdmin = principal.FindFirst("platform_admin")?.Value == "true";
        if (!isPlatformAdmin && !string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var hasPerm = await permRepo.HasPermissionAsync(userId.Value, "ApproveAmendment");
            if (!hasPerm)
                return Results.Json(new { error = "Bạn không có quyền phê duyệt/từ chối chỉnh sửa." }, statusCode: 403);
        }

        var amendment = await db.SignedDocumentAmendments
            .FirstOrDefaultAsync(a => a.Id == amendmentId && !a.IsDeleted);

        if (amendment == null)
            return Results.NotFound(new { error = "Không tìm thấy yêu cầu chỉnh sửa." });

        if (amendment.Status != AmendmentStatus.Pending)
            return Results.BadRequest(new { error = "Yêu cầu này đã được xử lý." });

        if (string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { error = "Vui lòng nhập lý do từ chối." });

        amendment.Reject(userId.Value, request.Notes);
        await db.SaveChangesAsync();

        await db.Entry(amendment).Reference(a => a.RequestedByUser).LoadAsync();
        await db.Entry(amendment).Reference(a => a.ReviewedByUser).LoadAsync();

        return Results.Ok(new
        {
            amendment = MapToDto(amendment),
            message = "Đã từ chối yêu cầu chỉnh sửa."
        });
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════
    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        return Guid.TryParse(
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
    }

    private static AmendmentDto MapToDto(SignedDocumentAmendment a) => new()
    {
        Id = a.Id,
        FormResponseId = a.FormResponseId,
        Version = a.Version,
        Status = a.Status.ToString(),
        Reason = a.Reason,
        ReviewNotes = a.ReviewNotes,
        RequestedByUserId = a.RequestedByUserId,
        RequestedByName = a.RequestedByUser?.FullName,
        ReviewedByUserId = a.ReviewedByUserId,
        ReviewedByName = a.ReviewedByUser?.FullName,
        CreatedAt = a.CreatedAt,
        ReviewedAt = a.ReviewedAt,
        FieldChanges = a.FieldChanges
            .Where(fc => !fc.IsDeleted)
            .Select(fc => new FieldChangeDto
            {
                Id = fc.Id,
                FormFieldId = fc.FormFieldId,
                FieldKey = fc.FieldKey,
                FieldLabel = fc.FieldLabel,
                ChangeType = fc.ChangeType.ToString(),
                OldTextValue = fc.OldTextValue,
                NewTextValue = fc.NewTextValue,
                OldNumericValue = fc.OldNumericValue,
                NewNumericValue = fc.NewNumericValue,
                OldDateValue = fc.OldDateValue,
                NewDateValue = fc.NewDateValue,
                OldBooleanValue = fc.OldBooleanValue,
                NewBooleanValue = fc.NewBooleanValue,
                OldJsonValue = fc.OldJsonValue,
                NewJsonValue = fc.NewJsonValue
            }).ToList()
    };
}

// ─── API Request DTOs ───────────────────────────────────────

public record CreateAmendmentApiRequest
{
    public string Reason { get; init; } = string.Empty;
    public List<FieldChangeApiRequest> FieldChanges { get; init; } = [];
}

public record FieldChangeApiRequest
{
    public Guid FormFieldId { get; init; }
    public string? NewTextValue { get; init; }
    public decimal? NewNumericValue { get; init; }
    public DateTime? NewDateValue { get; init; }
    public bool? NewBooleanValue { get; init; }
    public string? NewJsonValue { get; init; }
}

public record ApproveRejectRequest
{
    public string? Notes { get; init; }
}
