using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.SpermBank.Commands;

// ==================== CREATE DONOR ====================
public record CreateDonorCommand(Guid PatientId) : IRequest<Result<SpermDonorDto>>;

public class CreateDonorHandler : IRequestHandler<CreateDonorCommand, Result<SpermDonorDto>>
{
    private readonly ISpermDonorRepository _donorRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDonorHandler(ISpermDonorRepository donorRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _donorRepo = donorRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SpermDonorDto>> Handle(CreateDonorCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null) return Result<SpermDonorDto>.Failure("Patient not found");

        var code = await _donorRepo.GenerateCodeAsync(ct);
        var donor = SpermDonor.Create(code, request.PatientId);
        await _donorRepo.AddAsync(donor, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SpermDonorDto>.Success(SpermDonorDto.FromEntity(donor, patient.FullName));
    }
}

// ==================== UPDATE PROFILE ====================
public record UpdateDonorProfileCommand(
    Guid DonorId,
    string? BloodType,
    decimal? Height,
    decimal? Weight,
    string? EyeColor,
    string? HairColor,
    string? Ethnicity,
    string? Education,
    string? Occupation
) : IRequest<Result<SpermDonorDto>>;

public class UpdateDonorProfileHandler : IRequestHandler<UpdateDonorProfileCommand, Result<SpermDonorDto>>
{
    private readonly ISpermDonorRepository _donorRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDonorProfileHandler(ISpermDonorRepository donorRepo, IUnitOfWork unitOfWork)
    {
        _donorRepo = donorRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SpermDonorDto>> Handle(UpdateDonorProfileCommand r, CancellationToken ct)
    {
        var donor = await _donorRepo.GetByIdAsync(r.DonorId, ct);
        if (donor == null) return Result<SpermDonorDto>.Failure("Donor not found");

        donor.UpdateProfile(r.BloodType, r.Height, r.Weight, r.EyeColor, r.HairColor, r.Ethnicity, r.Education, r.Occupation);
        await _donorRepo.UpdateAsync(donor, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SpermDonorDto>.Success(SpermDonorDto.FromEntity(donor, donor.Patient?.FullName ?? ""));
    }
}

// ==================== CREATE SAMPLE ====================
public record CreateSampleCommand(
    Guid DonorId,
    DateTime CollectionDate,
    SpecimenType SpecimenType
) : IRequest<Result<SpermSampleDto>>;

public class CreateSampleHandler : IRequestHandler<CreateSampleCommand, Result<SpermSampleDto>>
{
    private readonly ISpermSampleRepository _sampleRepo;
    private readonly ISpermDonorRepository _donorRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSampleHandler(ISpermSampleRepository sampleRepo, ISpermDonorRepository donorRepo, IUnitOfWork unitOfWork)
    {
        _sampleRepo = sampleRepo;
        _donorRepo = donorRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SpermSampleDto>> Handle(CreateSampleCommand request, CancellationToken ct)
    {
        var donor = await _donorRepo.GetByIdAsync(request.DonorId, ct);
        if (donor == null) return Result<SpermSampleDto>.Failure("Donor not found");

        var code = await _sampleRepo.GenerateCodeAsync(ct);
        var sample = SpermSample.Create(request.DonorId, code, request.CollectionDate, request.SpecimenType);
        
        donor.RecordDonation();
        
        await _sampleRepo.AddAsync(sample, ct);
        await _donorRepo.UpdateAsync(donor, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SpermSampleDto>.Success(SpermSampleDto.FromEntity(sample));
    }
}

// ==================== RECORD QUALITY ====================
public record RecordSampleQualityCommand(
    Guid SampleId,
    decimal? Volume,
    decimal? Concentration,
    decimal? Motility,
    int? VialCount
) : IRequest<Result<SpermSampleDto>>;

public class RecordSampleQualityHandler : IRequestHandler<RecordSampleQualityCommand, Result<SpermSampleDto>>
{
    private readonly ISpermSampleRepository _sampleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordSampleQualityHandler(ISpermSampleRepository sampleRepo, IUnitOfWork unitOfWork)
    {
        _sampleRepo = sampleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SpermSampleDto>> Handle(RecordSampleQualityCommand r, CancellationToken ct)
    {
        var sample = await _sampleRepo.GetByIdAsync(r.SampleId, ct);
        if (sample == null) return Result<SpermSampleDto>.Failure("Sample not found");

        sample.RecordQuality(r.Volume, r.Concentration, r.Motility, r.VialCount);
        await _sampleRepo.UpdateAsync(sample, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SpermSampleDto>.Success(SpermSampleDto.FromEntity(sample));
    }
}

// ==================== DTOs ====================
public record SpermDonorDto(
    Guid Id,
    string DonorCode,
    Guid PatientId,
    string PatientName,
    string Status,
    string? BloodType,
    int TotalDonations,
    int SuccessfulPregnancies,
    DateTime CreatedAt
)
{
    public static SpermDonorDto FromEntity(SpermDonor d, string patientName) => new(
        d.Id, d.DonorCode, d.PatientId, patientName, d.Status.ToString(),
        d.BloodType, d.TotalDonations, d.SuccessfulPregnancies, d.CreatedAt
    );
}

public record SpermSampleDto(
    Guid Id,
    Guid DonorId,
    string SampleCode,
    DateTime CollectionDate,
    string SpecimenType,
    decimal? Volume,
    decimal? Concentration,
    decimal? Motility,
    int? VialCount,
    bool IsAvailable,
    DateTime CreatedAt
)
{
    public static SpermSampleDto FromEntity(SpermSample s) => new(
        s.Id, s.DonorId, s.SampleCode, s.CollectionDate, s.SpecimenType.ToString(),
        s.Volume, s.Concentration, s.Motility, s.VialCount, s.IsAvailable, s.CreatedAt
    );
}
