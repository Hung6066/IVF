using FluentValidation;
using IVF.Domain.Enums;

namespace IVF.Application.Features.Documents.Commands;

// ─── DTOs ───────────────────────────────────────────────────

public record AmendmentDto
{
    public Guid Id { get; init; }
    public Guid FormResponseId { get; init; }
    public int Version { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? ReviewNotes { get; init; }
    public Guid RequestedByUserId { get; init; }
    public string? RequestedByName { get; init; }
    public Guid? ReviewedByUserId { get; init; }
    public string? ReviewedByName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public List<FieldChangeDto> FieldChanges { get; init; } = [];
}

public record FieldChangeDto
{
    public Guid Id { get; init; }
    public Guid FormFieldId { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string FieldLabel { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public string? OldTextValue { get; init; }
    public string? NewTextValue { get; init; }
    public decimal? OldNumericValue { get; init; }
    public decimal? NewNumericValue { get; init; }
    public DateTime? OldDateValue { get; init; }
    public DateTime? NewDateValue { get; init; }
    public bool? OldBooleanValue { get; init; }
    public bool? NewBooleanValue { get; init; }
    public string? OldJsonValue { get; init; }
    public string? NewJsonValue { get; init; }
}

// ─── Create Amendment Request ───────────────────────────────

public record FieldChangeRequest
{
    public Guid FormFieldId { get; init; }
    public string? NewTextValue { get; init; }
    public decimal? NewNumericValue { get; init; }
    public DateTime? NewDateValue { get; init; }
    public bool? NewBooleanValue { get; init; }
    public string? NewJsonValue { get; init; }
}

public sealed record CreateAmendmentRequestCommand(
    Guid FormResponseId,
    Guid RequestedByUserId,
    string Reason,
    List<FieldChangeRequest> FieldChanges
);

public class CreateAmendmentRequestValidator : AbstractValidator<CreateAmendmentRequestCommand>
{
    public CreateAmendmentRequestValidator()
    {
        RuleFor(x => x.FormResponseId).NotEmpty().WithMessage("Chưa chọn phiếu cần chỉnh sửa.");
        RuleFor(x => x.RequestedByUserId).NotEmpty().WithMessage("Không xác định được người yêu cầu.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000).WithMessage("Vui lòng nhập lý do chỉnh sửa (tối đa 1000 ký tự).");
        RuleFor(x => x.FieldChanges).NotEmpty().WithMessage("Chưa có thay đổi nào.");
    }
}

// ─── Approve Amendment ──────────────────────────────────────

public sealed record ApproveAmendmentCommand(
    Guid AmendmentId,
    Guid ReviewedByUserId,
    string? ReviewNotes
);

public class ApproveAmendmentValidator : AbstractValidator<ApproveAmendmentCommand>
{
    public ApproveAmendmentValidator()
    {
        RuleFor(x => x.AmendmentId).NotEmpty();
        RuleFor(x => x.ReviewedByUserId).NotEmpty();
    }
}

// ─── Reject Amendment ───────────────────────────────────────

public sealed record RejectAmendmentCommand(
    Guid AmendmentId,
    Guid ReviewedByUserId,
    string? ReviewNotes
);

public class RejectAmendmentValidator : AbstractValidator<RejectAmendmentCommand>
{
    public RejectAmendmentValidator()
    {
        RuleFor(x => x.AmendmentId).NotEmpty();
        RuleFor(x => x.ReviewedByUserId).NotEmpty();
        RuleFor(x => x.ReviewNotes).NotEmpty().WithMessage("Vui lòng nhập lý do từ chối.");
    }
}
