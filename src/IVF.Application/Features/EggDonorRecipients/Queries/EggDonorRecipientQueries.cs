using IVF.Application.Common.Interfaces;
using IVF.Application.Features.EggDonorRecipients.Commands;
using MediatR;

namespace IVF.Application.Features.EggDonorRecipients.Queries;

public record GetEggDonorRecipientByIdQuery(Guid Id) : IRequest<EggDonorRecipientDto?>;

public class GetEggDonorRecipientByIdHandler : IRequestHandler<GetEggDonorRecipientByIdQuery, EggDonorRecipientDto?>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetEggDonorRecipientByIdHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<EggDonorRecipientDto?> Handle(GetEggDonorRecipientByIdQuery req, CancellationToken ct)
    {
        var match = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        return match is null ? null : EggDonorRecipientDto.FromEntity(match);
    }
}

public record GetRecipientsByDonorQuery(Guid EggDonorId) : IRequest<IReadOnlyList<EggDonorRecipientDto>>;

public class GetRecipientsByDonorHandler : IRequestHandler<GetRecipientsByDonorQuery, IReadOnlyList<EggDonorRecipientDto>>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetRecipientsByDonorHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EggDonorRecipientDto>> Handle(GetRecipientsByDonorQuery req, CancellationToken ct)
    {
        var matches = await _repo.GetByDonorAsync(req.EggDonorId, _currentUser.TenantId ?? Guid.Empty, ct);
        return matches.Select(EggDonorRecipientDto.FromEntity).ToList().AsReadOnly();
    }
}

public record GetMatchesByRecipientCoupleQuery(Guid CoupleId) : IRequest<IReadOnlyList<EggDonorRecipientDto>>;

public class GetMatchesByRecipientCoupleHandler : IRequestHandler<GetMatchesByRecipientCoupleQuery, IReadOnlyList<EggDonorRecipientDto>>
{
    private readonly IEggDonorRecipientRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetMatchesByRecipientCoupleHandler(IEggDonorRecipientRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EggDonorRecipientDto>> Handle(GetMatchesByRecipientCoupleQuery req, CancellationToken ct)
    {
        var matches = await _repo.GetByRecipientCoupleAsync(req.CoupleId, _currentUser.TenantId ?? Guid.Empty, ct);
        return matches.Select(EggDonorRecipientDto.FromEntity).ToList().AsReadOnly();
    }
}
