using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Consent.Commands;

// ==================== DTOs ====================
public record ConsentFormDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid? CycleId,
    Guid? ProcedureId,
    string ConsentType,
    string Title,
    string? Description,
    string Status,
    DateTime? SignedAt,
    Guid? SignedByPatientId,
    Guid? WitnessUserId,
    Guid? DoctorUserId,
    string? ScannedDocumentUrl,
    DateTime? ExpiresAt,
    bool IsExpired,
    bool IsValid,
    string? RevokeReason,
    DateTime? RevokedAt,
    string? Notes,
    DateTime CreatedAt)
{
    public static ConsentFormDto FromEntity(ConsentForm c) => new(
        c.Id, c.PatientId, c.Patient?.FullName ?? "", c.CycleId, c.ProcedureId,
        c.ConsentType, c.Title, c.Description, c.Status, c.SignedAt,
        c.SignedByPatientId, c.WitnessUserId, c.DoctorUserId,
        c.ScannedDocumentUrl, c.ExpiresAt, c.IsExpired, c.IsValid,
        c.RevokeReason, c.RevokedAt, c.Notes, c.CreatedAt);
}

// ==================== CREATE CONSENT FORM ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record CreateConsentFormCommand(
    Guid PatientId,
    string ConsentType,
    string Title,
    string? Description,
    string? TemplateContent,
    Guid? CycleId,
    Guid? ProcedureId,
    DateTime? ExpiresAt,
    string? Notes
) : IRequest<Result<ConsentFormDto>>;

public class CreateConsentFormValidator : AbstractValidator<CreateConsentFormCommand>
{
    public CreateConsentFormValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient ID is required");
        RuleFor(x => x.ConsentType).NotEmpty().Must(t => t is "OPU" or "IUI" or "Anesthesia" or "EggDonation" or "SpermDonation" or "FET" or "General")
            .WithMessage("Invalid consent type");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}

public class CreateConsentFormHandler : IRequestHandler<CreateConsentFormCommand, Result<ConsentFormDto>>
{
    private readonly IConsentFormRepository _consentRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateConsentFormHandler(IConsentFormRepository consentRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _consentRepo = consentRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsentFormDto>> Handle(CreateConsentFormCommand r, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(r.PatientId, ct);
        if (patient == null) return Result<ConsentFormDto>.Failure("Patient not found");

        var consent = ConsentForm.Create(r.PatientId, r.ConsentType, r.Title, r.Description, r.TemplateContent, r.CycleId, r.ProcedureId, r.ExpiresAt, r.Notes);
        await _consentRepo.AddAsync(consent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _consentRepo.GetByIdAsync(consent.Id, ct);
        return Result<ConsentFormDto>.Success(ConsentFormDto.FromEntity(saved!));
    }
}

// ==================== SIGN CONSENT ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record SignConsentCommand(
    Guid ConsentId,
    Guid PatientId,
    string? PatientSignature,
    Guid? WitnessUserId,
    string? WitnessSignature,
    Guid? DoctorUserId,
    string? DoctorSignature
) : IRequest<Result<ConsentFormDto>>;

public class SignConsentHandler : IRequestHandler<SignConsentCommand, Result<ConsentFormDto>>
{
    private readonly IConsentFormRepository _consentRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SignConsentHandler(IConsentFormRepository consentRepo, IUnitOfWork unitOfWork)
    {
        _consentRepo = consentRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsentFormDto>> Handle(SignConsentCommand r, CancellationToken ct)
    {
        var consent = await _consentRepo.GetByIdAsync(r.ConsentId, ct);
        if (consent == null) return Result<ConsentFormDto>.Failure("Consent form not found");

        try
        {
            consent.Sign(r.PatientId, r.PatientSignature, r.WitnessUserId, r.WitnessSignature, r.DoctorUserId, r.DoctorSignature);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ConsentFormDto>.Failure(ex.Message);
        }

        await _consentRepo.UpdateAsync(consent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsentFormDto>.Success(ConsentFormDto.FromEntity(consent));
    }
}

// ==================== REVOKE CONSENT ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record RevokeConsentCommand(Guid ConsentId, string Reason) : IRequest<Result<ConsentFormDto>>;

public class RevokeConsentValidator : AbstractValidator<RevokeConsentCommand>
{
    public RevokeConsentValidator()
    {
        RuleFor(x => x.ConsentId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class RevokeConsentHandler : IRequestHandler<RevokeConsentCommand, Result<ConsentFormDto>>
{
    private readonly IConsentFormRepository _consentRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RevokeConsentHandler(IConsentFormRepository consentRepo, IUnitOfWork unitOfWork)
    {
        _consentRepo = consentRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsentFormDto>> Handle(RevokeConsentCommand r, CancellationToken ct)
    {
        var consent = await _consentRepo.GetByIdAsync(r.ConsentId, ct);
        if (consent == null) return Result<ConsentFormDto>.Failure("Consent form not found");

        try
        {
            consent.Revoke(r.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ConsentFormDto>.Failure(ex.Message);
        }

        await _consentRepo.UpdateAsync(consent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsentFormDto>.Success(ConsentFormDto.FromEntity(consent));
    }
}

// ==================== UPLOAD SCAN ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record UploadConsentScanCommand(Guid ConsentId, string DocumentUrl) : IRequest<Result<ConsentFormDto>>;

public class UploadConsentScanHandler : IRequestHandler<UploadConsentScanCommand, Result<ConsentFormDto>>
{
    private readonly IConsentFormRepository _consentRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UploadConsentScanHandler(IConsentFormRepository consentRepo, IUnitOfWork unitOfWork)
    {
        _consentRepo = consentRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsentFormDto>> Handle(UploadConsentScanCommand r, CancellationToken ct)
    {
        var consent = await _consentRepo.GetByIdAsync(r.ConsentId, ct);
        if (consent == null) return Result<ConsentFormDto>.Failure("Consent form not found");

        consent.UploadScan(r.DocumentUrl);
        await _consentRepo.UpdateAsync(consent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsentFormDto>.Success(ConsentFormDto.FromEntity(consent));
    }
}

// ==================== CHECK VALID CONSENT ====================
public record CheckValidConsentQuery(Guid PatientId, string ConsentType, Guid? CycleId) : IRequest<bool>;

public class CheckValidConsentHandler : IRequestHandler<CheckValidConsentQuery, bool>
{
    private readonly IConsentFormRepository _consentRepo;
    public CheckValidConsentHandler(IConsentFormRepository consentRepo) => _consentRepo = consentRepo;

    public async Task<bool> Handle(CheckValidConsentQuery r, CancellationToken ct)
        => await _consentRepo.HasValidConsentAsync(r.PatientId, r.ConsentType, r.CycleId, ct);
}
