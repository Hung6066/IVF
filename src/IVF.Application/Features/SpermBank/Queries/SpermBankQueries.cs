using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.SpermBank.Commands;
using MediatR;

namespace IVF.Application.Features.SpermBank.Queries;

// ==================== SEARCH DONORS ====================
public record SearchDonorsQuery(string? Query, int Page = 1, int PageSize = 20) : IRequest<PagedResult<SpermDonorDto>>;

public class SearchDonorsHandler : IRequestHandler<SearchDonorsQuery, PagedResult<SpermDonorDto>>
{
    private readonly ISpermDonorRepository _donorRepo;

    public SearchDonorsHandler(ISpermDonorRepository donorRepo) => _donorRepo = donorRepo;

    public async Task<PagedResult<SpermDonorDto>> Handle(SearchDonorsQuery request, CancellationToken ct)
    {
        var (items, total) = await _donorRepo.SearchAsync(request.Query, request.Page, request.PageSize, ct);
        var dtos = items.Select(d => SpermDonorDto.FromEntity(d, d.Patient?.FullName ?? "")).ToList();
        return new PagedResult<SpermDonorDto>(dtos, total, request.Page, request.PageSize);
    }
}

// ==================== GET DONOR BY ID ====================
public record GetDonorByIdQuery(Guid Id) : IRequest<Result<SpermDonorDto>>;

public class GetDonorByIdHandler : IRequestHandler<GetDonorByIdQuery, Result<SpermDonorDto>>
{
    private readonly ISpermDonorRepository _donorRepo;

    public GetDonorByIdHandler(ISpermDonorRepository donorRepo) => _donorRepo = donorRepo;

    public async Task<Result<SpermDonorDto>> Handle(GetDonorByIdQuery request, CancellationToken ct)
    {
        var donor = await _donorRepo.GetByIdAsync(request.Id, ct);
        if (donor == null) return Result<SpermDonorDto>.Failure("Donor not found");
        return Result<SpermDonorDto>.Success(SpermDonorDto.FromEntity(donor, donor.Patient?.FullName ?? ""));
    }
}

// ==================== GET SAMPLES BY DONOR ====================
public record GetSamplesByDonorQuery(Guid DonorId) : IRequest<IReadOnlyList<SpermSampleDto>>;

public class GetSamplesByDonorHandler : IRequestHandler<GetSamplesByDonorQuery, IReadOnlyList<SpermSampleDto>>
{
    private readonly ISpermSampleRepository _sampleRepo;

    public GetSamplesByDonorHandler(ISpermSampleRepository sampleRepo) => _sampleRepo = sampleRepo;

    public async Task<IReadOnlyList<SpermSampleDto>> Handle(GetSamplesByDonorQuery request, CancellationToken ct)
    {
        var samples = await _sampleRepo.GetByDonorIdAsync(request.DonorId, ct);
        return samples.Select(SpermSampleDto.FromEntity).ToList();
    }
}

// ==================== GET AVAILABLE SAMPLES ====================
public record GetAvailableSamplesQuery() : IRequest<IReadOnlyList<SpermSampleDto>>;

public class GetAvailableSamplesHandler : IRequestHandler<GetAvailableSamplesQuery, IReadOnlyList<SpermSampleDto>>
{
    private readonly ISpermSampleRepository _sampleRepo;

    public GetAvailableSamplesHandler(ISpermSampleRepository sampleRepo) => _sampleRepo = sampleRepo;

    public async Task<IReadOnlyList<SpermSampleDto>> Handle(GetAvailableSamplesQuery request, CancellationToken ct)
    {
        var samples = await _sampleRepo.GetAvailableAsync(ct);
        return samples.Select(SpermSampleDto.FromEntity).ToList();
    }
}
