using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.EggBank.Commands;

// ==================== CREATE DONOR ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record CreateEggDonorCommand(Guid PatientId) : IRequest<Result<EggDonorDto>>;

public class CreateEggDonorValidator : AbstractValidator<CreateEggDonorCommand>
{
    public CreateEggDonorValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Vui lòng chọn bệnh nhân");
    }
}

public class CreateEggDonorHandler(IEggDonorRepository donorRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateEggDonorCommand, Result<EggDonorDto>>
{
    public async Task<Result<EggDonorDto>> Handle(CreateEggDonorCommand request, CancellationToken ct)
    {
        var patient = await patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null) return Result<EggDonorDto>.Failure("Không tìm thấy bệnh nhân");

        var code = await donorRepo.GenerateCodeAsync(ct);
        var donor = EggDonor.Create(code, request.PatientId);
        await donorRepo.AddAsync(donor, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<EggDonorDto>.Success(EggDonorDto.FromEntity(donor, patient.FullName));
    }
}

// ==================== UPDATE PROFILE ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record UpdateEggDonorProfileCommand(
    Guid DonorId,
    string? BloodType,
    decimal? Height,
    decimal? Weight,
    string? EyeColor,
    string? HairColor,
    string? Ethnicity,
    string? Education,
    string? Occupation,
    int? AmhLevel,
    int? AntralFollicleCount,
    string? MenstrualHistory) : IRequest<Result<EggDonorDto>>;

public class UpdateEggDonorProfileHandler(IEggDonorRepository donorRepo, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateEggDonorProfileCommand, Result<EggDonorDto>>
{
    public async Task<Result<EggDonorDto>> Handle(UpdateEggDonorProfileCommand r, CancellationToken ct)
    {
        var donor = await donorRepo.GetByIdAsync(r.DonorId, ct);
        if (donor == null) return Result<EggDonorDto>.Failure("Không tìm thấy người hiến");

        donor.UpdateProfile(r.BloodType, r.Height, r.Weight, r.EyeColor, r.HairColor,
            r.Ethnicity, r.Education, r.Occupation, r.AmhLevel, r.AntralFollicleCount, r.MenstrualHistory);
        await donorRepo.UpdateAsync(donor, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<EggDonorDto>.Success(EggDonorDto.FromEntity(donor, donor.Patient?.FullName ?? ""));
    }
}

// ==================== CREATE OOCYTE SAMPLE ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record CreateOocyteSampleCommand(
    Guid DonorId,
    DateTime CollectionDate) : IRequest<Result<OocyteSampleDto>>;

public class CreateOocyteSampleHandler(IOocyteSampleRepository sampleRepo, IEggDonorRepository donorRepo, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOocyteSampleCommand, Result<OocyteSampleDto>>
{
    public async Task<Result<OocyteSampleDto>> Handle(CreateOocyteSampleCommand request, CancellationToken ct)
    {
        var donor = await donorRepo.GetByIdAsync(request.DonorId, ct);
        if (donor == null) return Result<OocyteSampleDto>.Failure("Không tìm thấy người hiến");

        var code = await sampleRepo.GenerateCodeAsync(ct);
        var sample = OocyteSample.Create(request.DonorId, code, request.CollectionDate);
        donor.RecordDonation();

        await sampleRepo.AddAsync(sample, ct);
        await donorRepo.UpdateAsync(donor, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<OocyteSampleDto>.Success(OocyteSampleDto.FromEntity(sample));
    }
}

// ==================== RECORD OOCYTE QUALITY ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record RecordOocyteQualityCommand(
    Guid SampleId,
    int? TotalOocytes,
    int? MatureOocytes,
    int? ImmatureOocytes,
    int? DegeneratedOocytes,
    string? Notes) : IRequest<Result<OocyteSampleDto>>;

public class RecordOocyteQualityHandler(IOocyteSampleRepository sampleRepo, IUnitOfWork unitOfWork)
    : IRequestHandler<RecordOocyteQualityCommand, Result<OocyteSampleDto>>
{
    public async Task<Result<OocyteSampleDto>> Handle(RecordOocyteQualityCommand r, CancellationToken ct)
    {
        var sample = await sampleRepo.GetByIdAsync(r.SampleId, ct);
        if (sample == null) return Result<OocyteSampleDto>.Failure("Không tìm thấy mẫu noãn");

        sample.RecordQuality(r.TotalOocytes, r.MatureOocytes, r.ImmatureOocytes, r.DegeneratedOocytes, r.Notes);
        await sampleRepo.UpdateAsync(sample, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<OocyteSampleDto>.Success(OocyteSampleDto.FromEntity(sample));
    }
}

// ==================== VITRIFY OOCYTES ====================
[RequiresFeature(FeatureCodes.EggBank)]
public record VitrifyOocytesCommand(
    Guid SampleId,
    int Count,
    DateTime FreezeDate,
    Guid? CryoLocationId) : IRequest<Result<OocyteSampleDto>>;

public class VitrifyOocytesHandler(IOocyteSampleRepository sampleRepo, IUnitOfWork unitOfWork)
    : IRequestHandler<VitrifyOocytesCommand, Result<OocyteSampleDto>>
{
    public async Task<Result<OocyteSampleDto>> Handle(VitrifyOocytesCommand r, CancellationToken ct)
    {
        var sample = await sampleRepo.GetByIdAsync(r.SampleId, ct);
        if (sample == null) return Result<OocyteSampleDto>.Failure("Không tìm thấy mẫu noãn");

        sample.Vitrify(r.Count, r.FreezeDate, r.CryoLocationId);
        await sampleRepo.UpdateAsync(sample, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<OocyteSampleDto>.Success(OocyteSampleDto.FromEntity(sample));
    }
}

// ==================== DTOs ====================
public record EggDonorDto(
    Guid Id,
    string DonorCode,
    Guid PatientId,
    string PatientName,
    string Status,
    string? BloodType,
    decimal? Height,
    decimal? Weight,
    string? Ethnicity,
    int? AmhLevel,
    int? AntralFollicleCount,
    int TotalDonations,
    int SuccessfulPregnancies,
    DateTime? LastDonationDate,
    DateTime? ScreeningDate,
    DateTime CreatedAt)
{
    public static EggDonorDto FromEntity(EggDonor d, string patientName) => new(
        d.Id, d.DonorCode, d.PatientId, patientName, d.Status.ToString(),
        d.BloodType, d.Height, d.Weight, d.Ethnicity,
        d.AmhLevel, d.AntralFollicleCount,
        d.TotalDonations, d.SuccessfulPregnancies,
        d.LastDonationDate, d.ScreeningDate, d.CreatedAt);
}

public record OocyteSampleDto(
    Guid Id,
    Guid DonorId,
    string SampleCode,
    DateTime CollectionDate,
    int? TotalOocytes,
    int? MatureOocytes,
    int? ImmatureOocytes,
    int? DegeneratedOocytes,
    int? VitrifiedCount,
    bool IsAvailable,
    DateTime? FreezeDate,
    DateTime? ThawDate,
    int? SurvivedAfterThaw,
    string? Notes,
    DateTime CreatedAt)
{
    public static OocyteSampleDto FromEntity(OocyteSample s) => new(
        s.Id, s.DonorId, s.SampleCode, s.CollectionDate,
        s.TotalOocytes, s.MatureOocytes, s.ImmatureOocytes, s.DegeneratedOocytes,
        s.VitrifiedCount, s.IsAvailable, s.FreezeDate, s.ThawDate, s.SurvivedAfterThaw,
        s.Notes, s.CreatedAt);
}
