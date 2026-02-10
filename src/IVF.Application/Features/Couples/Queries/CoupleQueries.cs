using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Couples.Queries;

// ==================== Query Records ====================
public record GetCoupleByIdQuery(Guid Id) : IRequest<Result<CoupleDto>>;
public record GetCoupleByPatientIdQuery(Guid PatientId) : IRequest<Result<CoupleDto>>;

public record GetAllCouplesQuery() : IRequest<IEnumerable<CoupleDto>>;

// ==================== DTOs ====================
public record CoupleDto(
    Guid Id,
    PatientDto Wife,
    PatientDto Husband,
    DateTime? MarriageDate,
    int? InfertilityYears,
    Guid? SpermDonorId
);

public record PatientDto(
    Guid Id,
    string PatientCode,
    string FullName
);

// ==================== Handlers ====================
public class GetCoupleByIdHandler(ICoupleRepository repo)
    : IRequestHandler<GetCoupleByIdQuery, Result<CoupleDto>>
{
    public async Task<Result<CoupleDto>> Handle(GetCoupleByIdQuery request, CancellationToken ct)
    {
        // GetByIdAsync already includes Wife + Husband — no extra queries needed
        var couple = await repo.GetByIdAsync(request.Id, ct);
        if (couple == null)
            return Result<CoupleDto>.Failure("Couple not found");

        return Result<CoupleDto>.Success(CoupleMapper.MapToDto(couple));
    }
}

public class GetAllCouplesHandler(ICoupleRepository repo)
    : IRequestHandler<GetAllCouplesQuery, IEnumerable<CoupleDto>>
{
    public async Task<IEnumerable<CoupleDto>> Handle(GetAllCouplesQuery request, CancellationToken ct)
    {
        // Single query with Include — eliminates N+1 (was 2N+1 queries)
        var couples = await repo.GetAllAsync(ct);
        return couples.Select(CoupleMapper.MapToDto);
    }
}

public class GetCoupleByPatientIdHandler(ICoupleRepository repo)
    : IRequestHandler<GetCoupleByPatientIdQuery, Result<CoupleDto>>
{
    public async Task<Result<CoupleDto>> Handle(GetCoupleByPatientIdQuery request, CancellationToken ct)
    {
        // GetByPatientIdAsync already includes Wife + Husband — no extra queries needed
        var couple = await repo.GetByPatientIdAsync(request.PatientId, ct);
        if (couple == null)
            return Result<CoupleDto>.Failure("Couple not found for this patient");

        return Result<CoupleDto>.Success(CoupleMapper.MapToDto(couple));
    }
}

// Shared mapping to avoid N+1 re-fetches — uses already-loaded navigations
internal static class CoupleMapper
{
    public static CoupleDto MapToDto(Couple couple) => new(
        couple.Id,
        new PatientDto(couple.Wife!.Id, couple.Wife.PatientCode, couple.Wife.FullName),
        new PatientDto(couple.Husband!.Id, couple.Husband.PatientCode, couple.Husband.FullName),
        couple.MarriageDate,
        couple.InfertilityYears,
        couple.SpermDonorId
    );
}
