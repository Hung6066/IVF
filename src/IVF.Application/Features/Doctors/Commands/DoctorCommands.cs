using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Doctors.Commands;

public record CreateDoctorCommand(
    Guid UserId,
    string Specialty,
    string? LicenseNumber,
    string? RoomNumber,
    int MaxPatientsPerDay = 20
) : IRequest<Result<Guid>>;

public class CreateDoctorValidator : AbstractValidator<CreateDoctorCommand>
{
    public CreateDoctorValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Specialty).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LicenseNumber).MaximumLength(50);
        RuleFor(x => x.RoomNumber).MaximumLength(20);
        RuleFor(x => x.MaxPatientsPerDay).GreaterThan(0);
    }
}

public class CreateDoctorHandler : IRequestHandler<CreateDoctorCommand, Result<Guid>>
{
    private readonly IDoctorRepository _doctorRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDoctorHandler(IDoctorRepository doctorRepo, IUserRepository userRepo, IUnitOfWork unitOfWork)
    {
        _doctorRepo = doctorRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateDoctorCommand request, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return Result<Guid>.Failure("User not found");

        // Check if already a doctor
        var existing = await _doctorRepo.GetByUserIdAsync(request.UserId, ct);
        if (existing != null)
            return Result<Guid>.Failure("User is already a doctor");

        var doctor = Doctor.Create(
            request.UserId,
            request.Specialty,
            request.LicenseNumber,
            request.RoomNumber,
            request.MaxPatientsPerDay);

        await _doctorRepo.AddAsync(doctor, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<Guid>.Success(doctor.Id);
    }
}
