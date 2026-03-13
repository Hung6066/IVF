using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Features.Waf.Commands;

// ═══════════════════════════════════════════════════════════════════════════════════════
// CREATE WAF RULE COMMAND
// ═══════════════════════════════════════════════════════════════════════════════════════

public record CreateWafRuleCommand(
    string Name,
    string? Description,
    int Priority,
    WafRuleGroup RuleGroup,
    WafAction Action,
    WafMatchType MatchType,
    bool NegateMatch,
    string? Expression,
    List<string>? UriPathPatterns,
    List<string>? QueryStringPatterns,
    List<string>? HeaderPatterns,
    List<string>? BodyPatterns,
    List<string>? Methods,
    List<string>? IpCidrList,
    List<string>? CountryCodes,
    List<string>? UserAgentPatterns,
    int? RateLimitRequests,
    int? RateLimitWindowSeconds,
    string? BlockResponseMessage,
    string? CreatedBy
) : IRequest<Result<WafRuleDto>>;

public class CreateWafRuleValidator : AbstractValidator<CreateWafRuleCommand>
{
    public CreateWafRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9999);
        RuleFor(x => x.RuleGroup).IsInEnum();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.MatchType).IsInEnum();
        RuleFor(x => x.RateLimitRequests)
            .GreaterThan(0).When(x => x.Action == WafAction.RateLimit)
            .WithMessage("RateLimitRequests is required for RateLimit action");
        RuleFor(x => x.RateLimitWindowSeconds)
            .GreaterThan(0).When(x => x.Action == WafAction.RateLimit)
            .WithMessage("RateLimitWindowSeconds is required for RateLimit action");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// UPDATE WAF RULE COMMAND
// ═══════════════════════════════════════════════════════════════════════════════════════

public record UpdateWafRuleCommand(
    Guid Id,
    string Name,
    string? Description,
    int Priority,
    WafAction Action,
    WafMatchType MatchType,
    bool NegateMatch,
    string? Expression,
    List<string>? UriPathPatterns,
    List<string>? QueryStringPatterns,
    List<string>? HeaderPatterns,
    List<string>? BodyPatterns,
    List<string>? Methods,
    List<string>? IpCidrList,
    List<string>? CountryCodes,
    List<string>? UserAgentPatterns,
    int? RateLimitRequests,
    int? RateLimitWindowSeconds,
    string? BlockResponseMessage,
    string? ModifiedBy
) : IRequest<Result<WafRuleDto>>;

public class UpdateWafRuleValidator : AbstractValidator<UpdateWafRuleCommand>
{
    public UpdateWafRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0).LessThanOrEqualTo(9999);
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.MatchType).IsInEnum();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// DELETE WAF RULE COMMAND
// ═══════════════════════════════════════════════════════════════════════════════════════

public record DeleteWafRuleCommand(Guid Id) : IRequest<Result>;

public class DeleteWafRuleValidator : AbstractValidator<DeleteWafRuleCommand>
{
    public DeleteWafRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// TOGGLE WAF RULE COMMAND
// ═══════════════════════════════════════════════════════════════════════════════════════

public record ToggleWafRuleCommand(Guid Id, bool Enable) : IRequest<Result>;

public class ToggleWafRuleValidator : AbstractValidator<ToggleWafRuleCommand>
{
    public ToggleWafRuleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// DTO
// ═══════════════════════════════════════════════════════════════════════════════════════

public record WafRuleDto(
    Guid Id,
    string Name,
    string? Description,
    int Priority,
    bool IsEnabled,
    string RuleGroup,
    bool IsManaged,
    string Action,
    string MatchType,
    bool NegateMatch,
    string? Expression,
    List<string>? UriPathPatterns,
    List<string>? QueryStringPatterns,
    List<string>? HeaderPatterns,
    List<string>? BodyPatterns,
    List<string>? Methods,
    List<string>? IpCidrList,
    List<string>? CountryCodes,
    List<string>? UserAgentPatterns,
    int? RateLimitRequests,
    int? RateLimitWindowSeconds,
    string? BlockResponseMessage,
    long HitCount,
    string? CreatedBy,
    string? LastModifiedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
