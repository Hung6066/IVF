using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Patients.Commands;

// ==================== CREATE PATIENT ====================
[RequiresFeature(FeatureCodes.PatientManagement)]
public record CreatePatientCommand(
    string FullName,
    DateTime DateOfBirth,
    Gender Gender,
    PatientType PatientType,
    string? IdentityNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Ethnicity,
    string? Nationality,
    string? Occupation,
    string? InsuranceNumber,
    string? InsuranceProvider,
    BloodType? BloodType,
    string? Allergies,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    string? ReferralSource,
    Guid? ReferringDoctorId
) : IRequest<Result<PatientDto>>;

public class CreatePatientValidator : AbstractValidator<CreatePatientCommand>
{
    public CreatePatientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).LessThan(DateTime.Today).WithMessage("Date of birth must be in the past");
        RuleFor(x => x.IdentityNumber).MaximumLength(20);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.EmergencyContactPhone).MaximumLength(20);
        RuleFor(x => x.InsuranceNumber).MaximumLength(50);
    }
}

public class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantLimitService _limitService;

    public CreatePatientHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork,
        IAuditLogRepository auditLogRepo, ICurrentUserService currentUser, ITenantLimitService limitService)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
        _auditLogRepo = auditLogRepo;
        _currentUser = currentUser;
        _limitService = limitService;
    }

    public async Task<Result<PatientDto>> Handle(CreatePatientCommand request, CancellationToken ct)
    {
        await _limitService.EnsurePatientLimitAsync(ct);

        var code = await _patientRepo.GenerateCodeAsync(ct);

        var patient = Patient.Create(
            code,
            request.FullName,
            request.DateOfBirth,
            request.Gender,
            request.PatientType,
            request.IdentityNumber,
            request.Phone,
            request.Email,
            request.Address,
            request.Ethnicity,
            request.Nationality,
            request.Occupation,
            request.InsuranceNumber,
            request.InsuranceProvider,
            request.BloodType,
            request.Allergies,
            request.EmergencyContactName,
            request.EmergencyContactPhone,
            request.EmergencyContactRelation,
            request.ReferralSource,
            request.ReferringDoctorId
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

// ==================== UPDATE PATIENT DEMOGRAPHICS ====================
public record UpdatePatientDemographicsCommand(
    Guid Id,
    string? Email,
    string? Ethnicity,
    string? Nationality,
    string? Occupation,
    string? InsuranceNumber,
    string? InsuranceProvider,
    BloodType? BloodType,
    string? Allergies
) : IRequest<Result<PatientDto>>;

public class UpdatePatientDemographicsValidator : AbstractValidator<UpdatePatientDemographicsCommand>
{
    public UpdatePatientDemographicsValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.InsuranceNumber).MaximumLength(50);
    }
}

public class UpdatePatientDemographicsHandler : IRequestHandler<UpdatePatientDemographicsCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePatientDemographicsHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(UpdatePatientDemographicsCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.Id, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.UpdateDemographics(
            request.Email, request.Ethnicity, request.Nationality, request.Occupation,
            request.InsuranceNumber, request.InsuranceProvider, request.BloodType, request.Allergies);
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== UPDATE EMERGENCY CONTACT ====================
public record UpdateEmergencyContactCommand(
    Guid PatientId,
    string? Name,
    string? Phone,
    string? Relation
) : IRequest<Result<PatientDto>>;

public class UpdateEmergencyContactHandler : IRequestHandler<UpdateEmergencyContactCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateEmergencyContactHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(UpdateEmergencyContactCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.UpdateEmergencyContact(request.Name, request.Phone, request.Relation);
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== UPDATE PATIENT CONSENT ====================
public record UpdatePatientConsentCommand(
    Guid PatientId,
    bool ConsentDataProcessing,
    bool ConsentResearch,
    bool ConsentMarketing
) : IRequest<Result<PatientDto>>;

public class UpdatePatientConsentHandler : IRequestHandler<UpdatePatientConsentCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePatientConsentHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(UpdatePatientConsentCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.UpdateConsent(request.ConsentDataProcessing, request.ConsentResearch, request.ConsentMarketing);
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== SET PATIENT RISK LEVEL ====================
public record SetPatientRiskCommand(
    Guid PatientId,
    RiskLevel RiskLevel,
    string? RiskNotes
) : IRequest<Result<PatientDto>>;

public class SetPatientRiskHandler : IRequestHandler<SetPatientRiskCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public SetPatientRiskHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(SetPatientRiskCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.SetRiskLevel(request.RiskLevel, request.RiskNotes);
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== CHANGE PATIENT STATUS ====================
public record ChangePatientStatusCommand(
    Guid PatientId,
    PatientStatus Status
) : IRequest<Result<PatientDto>>;

public class ChangePatientStatusHandler : IRequestHandler<ChangePatientStatusCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePatientStatusHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(ChangePatientStatusCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.ChangeStatus(request.Status);
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PatientDto>.Success(PatientDto.FromEntity(patient));
    }
}

// ==================== RECORD PATIENT VISIT ====================
public record RecordPatientVisitCommand(Guid PatientId) : IRequest<Result>;

public class RecordPatientVisitHandler : IRequestHandler<RecordPatientVisitCommand, Result>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordPatientVisitHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RecordPatientVisitCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result.Failure("Patient not found");

        patient.RecordVisit();
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== ANONYMIZE PATIENT (GDPR) ====================
public record AnonymizePatientCommand(Guid PatientId) : IRequest<Result>;

public class AnonymizePatientHandler : IRequestHandler<AnonymizePatientCommand, Result>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AnonymizePatientHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(AnonymizePatientCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result.Failure("Patient not found");

        patient.Anonymize();
        await _patientRepo.UpdateAsync(patient, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== UPDATE MEDICAL NOTES ====================
public record UpdatePatientMedicalNotesCommand(
    Guid PatientId,
    string? MedicalNotes
) : IRequest<Result<PatientDto>>;

public class UpdatePatientMedicalNotesHandler : IRequestHandler<UpdatePatientMedicalNotesCommand, Result<PatientDto>>
{
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePatientMedicalNotesHandler(IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PatientDto>> Handle(UpdatePatientMedicalNotesCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PatientDto>.Failure("Patient not found");

        patient.SetMedicalNotes(request.MedicalNotes);
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
    string? Email,
    string? Address,
    string Status,
    // Demographics
    string? Ethnicity,
    string? Nationality,
    string? Occupation,
    string? InsuranceNumber,
    string? InsuranceProvider,
    string? BloodType,
    string? Allergies,
    // Emergency contact
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    // Referral
    string? ReferralSource,
    Guid? ReferringDoctorId,
    string? MedicalNotes,
    // Consent
    bool ConsentDataProcessing,
    DateTime? ConsentDataProcessingDate,
    bool ConsentResearch,
    DateTime? ConsentResearchDate,
    bool ConsentMarketing,
    DateTime? ConsentMarketingDate,
    // Risk & priority
    string RiskLevel,
    string? RiskNotes,
    string Priority,
    // Activity
    DateTime? LastVisitDate,
    int TotalVisits,
    string? Tags,
    string? Notes,
    bool IsAnonymized,
    DateTime CreatedAt,
    DateTime? UpdatedAt
)
{
    public static PatientDto FromEntity(Patient p) => new(
        p.Id, p.PatientCode, p.FullName, p.DateOfBirth,
        p.Gender.ToString(), p.PatientType.ToString(),
        p.IdentityNumber, p.Phone, p.Email, p.Address,
        p.Status.ToString(),
        p.Ethnicity, p.Nationality, p.Occupation,
        p.InsuranceNumber, p.InsuranceProvider,
        p.BloodType?.ToString(), p.Allergies,
        p.EmergencyContactName, p.EmergencyContactPhone, p.EmergencyContactRelation,
        p.ReferralSource, p.ReferringDoctorId, p.MedicalNotes,
        p.ConsentDataProcessing, p.ConsentDataProcessingDate,
        p.ConsentResearch, p.ConsentResearchDate,
        p.ConsentMarketing, p.ConsentMarketingDate,
        p.RiskLevel.ToString(), p.RiskNotes,
        p.Priority.ToString(),
        p.LastVisitDate, p.TotalVisits,
        p.Tags, p.Notes,
        p.IsAnonymized,
        p.CreatedAt, p.UpdatedAt
    );
}
