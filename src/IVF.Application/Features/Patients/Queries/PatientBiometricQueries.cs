using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Patients.Commands;
using MediatR;

namespace IVF.Application.Features.Patients.Queries;

// ==================== GET PATIENT PHOTO ====================
public record GetPatientPhotoQuery(Guid PatientId) : IRequest<Result<PatientPhotoDataDto>>;

public class GetPatientPhotoHandler : IRequestHandler<GetPatientPhotoQuery, Result<PatientPhotoDataDto>>
{
    private readonly IPatientBiometricsRepository _biometricsRepo;

    public GetPatientPhotoHandler(IPatientBiometricsRepository biometricsRepo)
    {
        _biometricsRepo = biometricsRepo;
    }

    public async Task<Result<PatientPhotoDataDto>> Handle(GetPatientPhotoQuery request, CancellationToken ct)
    {
        var photo = await _biometricsRepo.GetPhotoByPatientIdAsync(request.PatientId, ct);
        if (photo == null)
            return Result<PatientPhotoDataDto>.Failure("Photo not found");

        return Result<PatientPhotoDataDto>.Success(new PatientPhotoDataDto(
            photo.Id, photo.PatientId, photo.PhotoData,
            photo.ContentType, photo.FileName, photo.UploadedAt
        ));
    }
}

// ==================== GET PATIENT FINGERPRINTS ====================
public record GetPatientFingerprintsQuery(Guid PatientId) : IRequest<Result<IReadOnlyList<PatientFingerprintDto>>>;

public class GetPatientFingerprintsHandler : IRequestHandler<GetPatientFingerprintsQuery, Result<IReadOnlyList<PatientFingerprintDto>>>
{
    private readonly IPatientBiometricsRepository _biometricsRepo;

    public GetPatientFingerprintsHandler(IPatientBiometricsRepository biometricsRepo)
    {
        _biometricsRepo = biometricsRepo;
    }

    public async Task<Result<IReadOnlyList<PatientFingerprintDto>>> Handle(GetPatientFingerprintsQuery request, CancellationToken ct)
    {
        var fingerprints = await _biometricsRepo.GetFingerprintsByPatientIdAsync(request.PatientId, ct);
        var dtos = fingerprints.Select(PatientFingerprintDto.FromEntity).ToList();
        return Result<IReadOnlyList<PatientFingerprintDto>>.Success(dtos);
    }
}

// ==================== DTO ====================
public record PatientPhotoDataDto(
    Guid Id,
    Guid PatientId,
    byte[] PhotoData,
    string ContentType,
    string? FileName,
    DateTime UploadedAt
);
