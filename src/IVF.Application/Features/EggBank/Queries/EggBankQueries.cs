using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.EggBank.Commands;
using MediatR;

namespace IVF.Application.Features.EggBank.Queries;

// ==================== SEARCH DONORS ====================
public record SearchEggDonorsQuery(string? Query, int Page = 1, int PageSize = 20) : IRequest<PagedResult<EggDonorDto>>;

public class SearchEggDonorsHandler(IEggDonorRepository donorRepo)
    : IRequestHandler<SearchEggDonorsQuery, PagedResult<EggDonorDto>>
{
    public async Task<PagedResult<EggDonorDto>> Handle(SearchEggDonorsQuery request, CancellationToken ct)
    {
        var (items, total) = await donorRepo.SearchAsync(request.Query, request.Page, request.PageSize, ct);
        var dtos = items.Select(d => EggDonorDto.FromEntity(d, d.Patient?.FullName ?? "")).ToList();
        return new PagedResult<EggDonorDto>(dtos, total, request.Page, request.PageSize);
    }
}

// ==================== GET DONOR BY ID ====================
public record GetEggDonorByIdQuery(Guid Id) : IRequest<Result<EggDonorDto>>;

public class GetEggDonorByIdHandler(IEggDonorRepository donorRepo)
    : IRequestHandler<GetEggDonorByIdQuery, Result<EggDonorDto>>
{
    public async Task<Result<EggDonorDto>> Handle(GetEggDonorByIdQuery request, CancellationToken ct)
    {
        var donor = await donorRepo.GetByIdAsync(request.Id, ct);
        if (donor == null) return Result<EggDonorDto>.Failure("Không tìm thấy người hiến noãn");
        return Result<EggDonorDto>.Success(EggDonorDto.FromEntity(donor, donor.Patient?.FullName ?? ""));
    }
}

// ==================== GET SAMPLES BY DONOR ====================
public record GetOocyteSamplesByDonorQuery(Guid DonorId) : IRequest<IReadOnlyList<OocyteSampleDto>>;

public class GetOocyteSamplesByDonorHandler(IOocyteSampleRepository sampleRepo)
    : IRequestHandler<GetOocyteSamplesByDonorQuery, IReadOnlyList<OocyteSampleDto>>
{
    public async Task<IReadOnlyList<OocyteSampleDto>> Handle(GetOocyteSamplesByDonorQuery request, CancellationToken ct)
    {
        var samples = await sampleRepo.GetByDonorIdAsync(request.DonorId, ct);
        return samples.Select(OocyteSampleDto.FromEntity).ToList();
    }
}

// ==================== GET AVAILABLE SAMPLES ====================
public record GetAvailableOocyteSamplesQuery() : IRequest<IReadOnlyList<OocyteSampleDto>>;

public class GetAvailableOocyteSamplesHandler(IOocyteSampleRepository sampleRepo)
    : IRequestHandler<GetAvailableOocyteSamplesQuery, IReadOnlyList<OocyteSampleDto>>
{
    public async Task<IReadOnlyList<OocyteSampleDto>> Handle(GetAvailableOocyteSamplesQuery request, CancellationToken ct)
    {
        var samples = await sampleRepo.GetAvailableAsync(ct);
        return samples.Select(OocyteSampleDto.FromEntity).ToList();
    }
}
