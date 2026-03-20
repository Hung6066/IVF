using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Entities;
using FluentValidation;
using MediatR;

namespace IVF.Application.Features.EggDonorRecipients.Commands;

// ==================== DTO ====================
public class EggDonorRecipientDto
{
    public Guid Id { get; set; }
    public Guid EggDonorId { get; set; }
    public Guid RecipientCoupleId { get; set; }
    public Guid? CycleId { get; set; }
    public Guid MatchedByUserId { get; set; }
    public DateTime MatchedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public static EggDonorRecipientDto FromEntity(EggDonorRecipient m) => new()
    {
        Id = m.Id,
        EggDonorId = m.EggDonorId,
        RecipientCoupleId = m.RecipientCoupleId,
        CycleId = m.CycleId,
        MatchedByUserId = m.MatchedByUserId,
        MatchedAt = m.MatchedAt,
        Status = m.Status.ToString(),
        Notes = m.Notes,
        CreatedAt = m.CreatedAt
    };
}

// ==================== MATCH DONOR WITH RECIPIENT ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record MatchDonorWithRecipientCommand(
    Guid EggDonorId,
    Guid RecipientCoupleId,
    Guid MatchedByUserId,
    string? Notes) : IRequest<Result<EggDonorRecipientDto>>;

public class MatchDonorWithRecipientValidator : AbstractValidator<MatchDonorWithRecipientCommand>
{
    public MatchDonorWithRecipientValidator()
    {
        RuleFor(x => x.EggDonorId).NotEmpty();
        RuleFor(x => x.RecipientCoupleId).NotEmpty();
        RuleFor(x => x.MatchedByUserId).NotEmpty();
    }
}

public class MatchDonorWithRecipientHandler : IRequestHandler<MatchDonorWithRecipientCommand, Result<EggDonorRecipientDto>>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public MatchDonorWithRecipientHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EggDonorRecipientDto>> Handle(MatchDonorWithRecipientCommand req, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        if (await _repo.MatchExistsAsync(req.EggDonorId, req.RecipientCoupleId, tenantId, ct))
            return Result<EggDonorRecipientDto>.Failure("Cặp cho-nhận này đã được ghép nối.");

        var match = EggDonorRecipient.Create(tenantId, req.EggDonorId, req.RecipientCoupleId, req.MatchedByUserId, req.Notes);
        await _repo.AddAsync(match, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<EggDonorRecipientDto>.Success(EggDonorRecipientDto.FromEntity(match));
    }
}

// ==================== LINK TO CYCLE ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record LinkMatchToCycleCommand(Guid Id, Guid CycleId) : IRequest<Result<EggDonorRecipientDto>>;

public class LinkMatchToCycleHandler : IRequestHandler<LinkMatchToCycleCommand, Result<EggDonorRecipientDto>>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public LinkMatchToCycleHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EggDonorRecipientDto>> Handle(LinkMatchToCycleCommand req, CancellationToken ct)
    {
        var match = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (match is null) return Result<EggDonorRecipientDto>.Failure("Không tìm thấy thông tin ghép nối.");

        match.LinkToCycle(req.CycleId);
        await _repo.UpdateAsync(match, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<EggDonorRecipientDto>.Success(EggDonorRecipientDto.FromEntity(match));
    }
}

// ==================== COMPLETE / CANCEL ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record CompleteMatchCommand(Guid Id) : IRequest<Result<EggDonorRecipientDto>>;

public class CompleteMatchHandler : IRequestHandler<CompleteMatchCommand, Result<EggDonorRecipientDto>>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteMatchHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EggDonorRecipientDto>> Handle(CompleteMatchCommand req, CancellationToken ct)
    {
        var match = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (match is null) return Result<EggDonorRecipientDto>.Failure("Không tìm thấy thông tin ghép nối.");

        match.Complete();
        await _repo.UpdateAsync(match, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<EggDonorRecipientDto>.Success(EggDonorRecipientDto.FromEntity(match));
    }
}

[RequiresFeature(FeatureCodes.EggBank)]
public record CancelMatchCommand(Guid Id) : IRequest<Result<EggDonorRecipientDto>>;

public class CancelMatchHandler : IRequestHandler<CancelMatchCommand, Result<EggDonorRecipientDto>>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CancelMatchHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EggDonorRecipientDto>> Handle(CancelMatchCommand req, CancellationToken ct)
    {
        var match = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (match is null) return Result<EggDonorRecipientDto>.Failure("Không tìm thấy thông tin ghép nối.");

        match.Cancel();
        await _repo.UpdateAsync(match, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<EggDonorRecipientDto>.Success(EggDonorRecipientDto.FromEntity(match));
    }
}
