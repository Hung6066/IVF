using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Patients.Commands;

// ==================== UPLOAD PATIENT PHOTO ====================
public record UploadPatientPhotoCommand(
    Guid PatientId,
    byte[] PhotoData,
    string ContentType,
    string? FileName
) : IRequest<Result<PatientPhotoDto>>;

public class UploadPatientPhotoValidator : AbstractValidator<UploadPatientPhotoCommand>
{
    public UploadPatientPhotoValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.PhotoData).NotEmpty().WithMessage("Photo data is required");
        RuleFor(x => x.ContentType).NotEmpty().Must(ct => ct.StartsWith("image/"))
            .WithMessage("Must be an image file");
    }
}

public class UploadPatientPhotoHandler : IRequestHandler<UploadPatientPhotoCommand, Result<PatientPhotoDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IPatientBiometricsRepository _biometricsRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UploadPatientPhotoHandler(
        IPatientRepository patientRepo,
        IPatientBiometricsRepository biometricsRepo,
        IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _biometricsRepo = biometricsRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientPhotoDto>> Handle(UploadPatientPhotoCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientPhotoDto>.Failure("Patient not found");

        // Check if photo already exists (replace)
        var existing = await _biometricsRepo.GetPhotoByPatientIdAsync(request.PatientId, ct);
        if (existing != null)
        {
            existing.UpdatePhoto(request.PhotoData, request.ContentType, request.FileName);
            await _biometricsRepo.UpdatePhotoAsync(existing, ct);
        }
        else
        {
            var photo = PatientPhoto.Create(request.PatientId, request.PhotoData, request.ContentType, request.FileName);
            await _biometricsRepo.AddPhotoAsync(photo, ct);
            existing = photo;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PatientPhotoDto>.Success(PatientPhotoDto.FromEntity(existing));
    }
}

// ==================== DELETE PATIENT PHOTO ====================
public record DeletePatientPhotoCommand(Guid PatientId) : IRequest<Result>;

public class DeletePatientPhotoHandler : IRequestHandler<DeletePatientPhotoCommand, Result>
{
    private readonly IPatientBiometricsRepository _biometricsRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePatientPhotoHandler(IPatientBiometricsRepository biometricsRepo, IUnitOfWork unitOfWork)
    {
        _biometricsRepo = biometricsRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeletePatientPhotoCommand request, CancellationToken ct)
    {
        var photo = await _biometricsRepo.GetPhotoByPatientIdAsync(request.PatientId, ct);
        if (photo == null)
            return Result.Failure("Patient photo not found");

        await _biometricsRepo.DeletePhotoAsync(photo, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ==================== REGISTER PATIENT FINGERPRINT ====================
public record RegisterPatientFingerprintCommand(
    Guid PatientId,
    byte[] FingerprintData,
    FingerprintType FingerType,
    FingerprintSdkType SdkType,
    int Quality
) : IRequest<Result<PatientFingerprintDto>>;

public class RegisterPatientFingerprintValidator : AbstractValidator<RegisterPatientFingerprintCommand>
{
    public RegisterPatientFingerprintValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.FingerprintData).NotEmpty().WithMessage("Fingerprint data is required");
        RuleFor(x => x.Quality).InclusiveBetween(0, 100).WithMessage("Quality must be 0-100");
    }
}

public class RegisterPatientFingerprintHandler : IRequestHandler<RegisterPatientFingerprintCommand, Result<PatientFingerprintDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IPatientBiometricsRepository _biometricsRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBiometricMatcher _matcherService;

    public RegisterPatientFingerprintHandler(
        IPatientRepository patientRepo,
        IPatientBiometricsRepository biometricsRepo,
        IUnitOfWork unitOfWork,
        IBiometricMatcher matcherService)
    {
        _patientRepo = patientRepo;
        _biometricsRepo = biometricsRepo;
        _unitOfWork = unitOfWork;
        _matcherService = matcherService;
    }

    public async Task<Result<PatientFingerprintDto>> Handle(RegisterPatientFingerprintCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientFingerprintDto>.Failure("Patient not found");

        // Check if fingerprint for this finger already exists (replace)
        var existing = await _biometricsRepo.GetFingerprintByPatientAndTypeAsync(
            request.PatientId, request.FingerType, ct);

        if (existing != null)
        {
            existing.UpdateFingerprint(request.FingerprintData, request.Quality);
            await _biometricsRepo.UpdateFingerprintAsync(existing, ct);
        }
        else
        {
            var fingerprint = PatientFingerprint.Create(
                request.PatientId, request.FingerprintData,
                request.FingerType, request.SdkType, request.Quality);
            await _biometricsRepo.AddFingerprintAsync(fingerprint, ct);
            existing = fingerprint;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Sync to Redis/Matcher
        await _matcherService.SyncToRedis(patient.Id, existing.FingerType, existing.FingerprintData);

        return Result<PatientFingerprintDto>.Success(PatientFingerprintDto.FromEntity(existing));
    }
}

// ==================== DELETE PATIENT FINGERPRINT ====================
public record DeletePatientFingerprintCommand(Guid FingerprintId) : IRequest<Result>;

public class DeletePatientFingerprintHandler : IRequestHandler<DeletePatientFingerprintCommand, Result>
{
    private readonly IPatientBiometricsRepository _biometricsRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePatientFingerprintHandler(IPatientBiometricsRepository biometricsRepo, IUnitOfWork unitOfWork)
    {
        _biometricsRepo = biometricsRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeletePatientFingerprintCommand request, CancellationToken ct)
    {
        var fingerprint = await _biometricsRepo.GetFingerprintByIdAsync(request.FingerprintId, ct);
        if (fingerprint == null)
            return Result.Failure("Fingerprint not found");

        await _biometricsRepo.DeleteFingerprintAsync(fingerprint, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ==================== DTOs ====================
public record PatientPhotoDto(
    Guid Id,
    Guid PatientId,
    string ContentType,
    string? FileName,
    DateTime UploadedAt,
    int SizeBytes
)
{
    public static PatientPhotoDto FromEntity(PatientPhoto p) => new(
        p.Id, p.PatientId, p.ContentType, p.FileName,
        p.UploadedAt, p.PhotoData.Length
    );
}

public record PatientFingerprintDto(
    Guid Id,
    Guid PatientId,
    string FingerType,
    string SdkType,
    int Quality,
    DateTime CapturedAt,
    string TemplateData
)
{
    public static PatientFingerprintDto FromEntity(PatientFingerprint f) => new(
        f.Id, f.PatientId, f.FingerType.ToString(),
        f.SdkType.ToString(), f.Quality, f.CapturedAt,
        Convert.ToBase64String(f.FingerprintData)
    );
}
