using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Couples.Commands;

// ==================== CREATE COUPLE ====================
public record CreateCoupleCommand(
    Guid WifeId,
    Guid HusbandId,
    DateTime? MarriageDate,
    int? InfertilityYears
) : IRequest<Result<CoupleDto>>;

public class CreateCoupleValidator : AbstractValidator<CreateCoupleCommand>
{
    public CreateCoupleValidator()
    {
        RuleFor(x => x.WifeId).NotEmpty();
        RuleFor(x => x.HusbandId).NotEmpty();
        RuleFor(x => x.InfertilityYears).GreaterThanOrEqualTo(0).When(x => x.InfertilityYears.HasValue);
    }
}

public class CreateCoupleHandler : IRequestHandler<CreateCoupleCommand, Result<CoupleDto>>
{
    private readonly ICoupleRepository _coupleRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCoupleHandler(ICoupleRepository coupleRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _coupleRepo = coupleRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CoupleDto>> Handle(CreateCoupleCommand request, CancellationToken ct)
    {
        // Verify wife exists
        var wife = await _patientRepo.GetByIdAsync(request.WifeId, ct);
        if (wife == null)
            return Result<CoupleDto>.Failure("Wife patient not found");

        // Verify husband exists
        var husband = await _patientRepo.GetByIdAsync(request.HusbandId, ct);
        if (husband == null)
            return Result<CoupleDto>.Failure("Husband patient not found");

        // Check if couple already exists
        var existing = await _coupleRepo.GetByWifeAndHusbandAsync(request.WifeId, request.HusbandId, ct);
        if (existing != null)
            return Result<CoupleDto>.Failure("Couple already exists");

        var couple = Couple.Create(request.WifeId, request.HusbandId, request.MarriageDate, request.InfertilityYears);
        await _coupleRepo.AddAsync(couple, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CoupleDto>.Success(CoupleDto.FromEntity(couple, wife, husband));
    }
}

// ==================== UPDATE COUPLE ====================
public record UpdateCoupleCommand(
    Guid Id,
    DateTime? MarriageDate,
    int? InfertilityYears
) : IRequest<Result<CoupleDto>>;

public class UpdateCoupleHandler : IRequestHandler<UpdateCoupleCommand, Result<CoupleDto>>
{
    private readonly ICoupleRepository _coupleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCoupleHandler(ICoupleRepository coupleRepo, IUnitOfWork unitOfWork)
    {
        _coupleRepo = coupleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CoupleDto>> Handle(UpdateCoupleCommand request, CancellationToken ct)
    {
        var couple = await _coupleRepo.GetByIdAsync(request.Id, ct);
        if (couple == null)
            return Result<CoupleDto>.Failure("Couple not found");

        couple.Update(request.MarriageDate, request.InfertilityYears);
        await _coupleRepo.UpdateAsync(couple, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CoupleDto>.Success(CoupleDto.FromEntity(couple, couple.Wife, couple.Husband));
    }
}

// ==================== SET SPERM DONOR ====================
public record SetSpermDonorCommand(Guid CoupleId, Guid DonorId) : IRequest<Result>;

public class SetSpermDonorHandler : IRequestHandler<SetSpermDonorCommand, Result>
{
    private readonly ICoupleRepository _coupleRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SetSpermDonorHandler(ICoupleRepository coupleRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _coupleRepo = coupleRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetSpermDonorCommand request, CancellationToken ct)
    {
        var couple = await _coupleRepo.GetByIdAsync(request.CoupleId, ct);
        if (couple == null)
            return Result.Failure("Couple not found");

        var donor = await _patientRepo.GetByIdAsync(request.DonorId, ct);
        if (donor == null)
            return Result.Failure("Donor patient not found");

        couple.SetSpermDonor(request.DonorId);
        await _coupleRepo.UpdateAsync(couple, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== DTO ====================
public record CoupleDto(
    Guid Id,
    Guid WifeId,
    string WifeName,
    Guid HusbandId,
    string HusbandName,
    DateTime? MarriageDate,
    int? InfertilityYears,
    DateTime CreatedAt
)
{
    public static CoupleDto FromEntity(Couple c, Patient wife, Patient husband) => new(
        c.Id, c.WifeId, wife.FullName, c.HusbandId, husband.FullName,
        c.MarriageDate, c.InfertilityYears, c.CreatedAt
    );
}
