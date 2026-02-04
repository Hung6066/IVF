using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Couples.Queries;

// ==================== Query Records ====================
public record GetCoupleByIdQuery(Guid Id) : IRequest<Result<CoupleDto>>;

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
public class GetCoupleByIdHandler(ICoupleRepository repo, IPatientRepository patientRepo) 
    : IRequestHandler<GetCoupleByIdQuery, Result<CoupleDto>>
{
    public async Task<Result<CoupleDto>> Handle(GetCoupleByIdQuery request, CancellationToken ct)
    {
        var couple = await repo.GetByIdAsync(request.Id);
        if (couple == null)
            return Result<CoupleDto>.Failure("Couple not found");

        var wife = await patientRepo.GetByIdAsync(couple.WifeId);
        var husband = await patientRepo.GetByIdAsync(couple.HusbandId);

        return Result<CoupleDto>.Success(new CoupleDto(
            couple.Id,
            new PatientDto(wife!.Id, wife.PatientCode, wife.FullName),
            new PatientDto(husband!.Id, husband.PatientCode, husband.FullName),
            couple.MarriageDate,
            couple.InfertilityYears,
            couple.SpermDonorId
        ));
    }
}

public class GetAllCouplesHandler(ICoupleRepository repo, IPatientRepository patientRepo) 
    : IRequestHandler<GetAllCouplesQuery, IEnumerable<CoupleDto>>
{
    public async Task<IEnumerable<CoupleDto>> Handle(GetAllCouplesQuery request, CancellationToken ct)
    {
        var couples = await repo.GetAllAsync();
        var result = new List<CoupleDto>();

        foreach (var couple in couples)
        {
            var wife = await patientRepo.GetByIdAsync(couple.WifeId);
            var husband = await patientRepo.GetByIdAsync(couple.HusbandId);

            result.Add(new CoupleDto(
                couple.Id,
                new PatientDto(wife!.Id, wife.PatientCode, wife.FullName),
                new PatientDto(husband!.Id, husband.PatientCode, husband.FullName),
                couple.MarriageDate,
                couple.InfertilityYears,
                couple.SpermDonorId
            ));
        }

        return result;
    }
}
