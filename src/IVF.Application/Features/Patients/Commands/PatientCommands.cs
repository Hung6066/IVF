using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Patients.Commands;

// ==================== CREATE PATIENT ====================
public record CreatePatientCommand(
    string FullName,
    DateTime DateOfBirth,
    Gender Gender,
    PatientType PatientType,
    string? IdentityNumber,
    string? Phone,
    string? Address
) : IRequest<Result<PatientDto>>;

public class CreatePatientValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.Today).WithMessage("Date of birth must be in the past");
        RuleFor(x => x.IdentityNumber).MaximumLength(20);
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}

public class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePatientHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(CreatePatientCommand request, CancellationToken ct)
    {
        var code = await _patientRepo.GenerateCodeAsync(ct);
        
        var patient = Patient.Create(
            code,
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.PatientType,
            request.IdentityNumber,
            request.Phone,
            request.Address
        );

        await _patientRepo.AddAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== UPDATE PATIENT ====================
public record UpdatePatientCommand(
    Guid Id,
    string FullName,
    string? Phone,
    string? Address
) : IRequest<Result<PatientDto>>;

public class UpdatePatientHandler : IRequestHandler<UpdatePatientCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePatientHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(UpdatePatientCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.Id, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.Update(request.FullName, request.Phone, request.Address);
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== DELETE PATIENT ====================
public record DeletePatientCommand(Guid Id) : IRequest<Result>;

public class DeletePatientHandler : IRequestHandler<DeletePatientCommand, Result>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePatientHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeletePatientCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.Id, ct);
        if (patient == null)
            return Result.Failure("Patient not found");

        patient.MarkAsDeleted();
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== DTO ====================
public record PatientDto(
    Guid Id,
    string PatientCode,
    string FullName,
    DateTime DateOfBirth,
    string Gender,
    string PatientType,
    string? IdentityNumber,
    string? Phone,
    string? Address,
    DateTime CreatedAt
)
{
    public static PatientDto FromEntity(Patient p) => new(
        p.Id, p.PatientCode, p.FullName, p.DateOfBirth,
        p.Gender.ToString(), p.PatientType.ToString(),
        p.IdentityNumber, p.Phone, p.Address, p.CreatedAt
    );
}
