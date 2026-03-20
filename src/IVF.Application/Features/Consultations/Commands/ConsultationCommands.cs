using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Consultations.Commands;

// ==================== DTOs ====================
public record ConsultationDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientCode,
    Guid DoctorId,
    string DoctorName,
    Guid? CycleId,
    string ConsultationType,
    DateTime ConsultationDate,
    string Status,
    string? ChiefComplaint,
    string? MedicalHistory,
    string? PastHistory,
    string? SurgicalHistory,
    string? FamilyHistory,
    string? ObstetricHistory,
    string? MenstrualHistory,
    string? PhysicalExamination,
    string? Diagnosis,
    string? TreatmentPlan,
    string? RecommendedMethod,
    string? Notes,
    bool WaiveConsultationFee,
    DateTime CreatedAt)
{
    public static ConsultationDto FromEntity(Consultation c) => new(
        c.Id,
        c.PatientId,
        c.Patient?.FullName ?? "",
        c.Patient?.PatientCode ?? "",
        c.DoctorId,
        c.Doctor?.FullName ?? "",
        c.CycleId,
        c.ConsultationType,
        c.ConsultationDate,
        c.Status,
        c.ChiefComplaint,
        c.MedicalHistory,
        c.PastHistory,
        c.SurgicalHistory,
        c.FamilyHistory,
        c.ObstetricHistory,
        c.MenstrualHistory,
        c.PhysicalExamination,
        c.Diagnosis,
        c.TreatmentPlan,
        c.RecommendedMethod?.ToString(),
        c.Notes,
        c.WaiveConsultationFee,
        c.CreatedAt);
}

// ==================== CREATE CONSULTATION ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record CreateConsultationCommand(
    Guid PatientId,
    Guid DoctorId,
    DateTime ConsultationDate,
    string ConsultationType,
    Guid? CycleId,
    string? ChiefComplaint,
    string? Notes,
    bool WaiveConsultationFee
) : IRequest<Result<ConsultationDto>>;

public class CreateConsultationValidator : AbstractValidator<CreateConsultationCommand>
{
    public CreateConsultationValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient ID is required");
        RuleFor(x => x.DoctorId).NotEmpty().WithMessage("Doctor ID is required");
        RuleFor(x => x.ConsultationDate).NotEmpty().WithMessage("Consultation date is required");
        RuleFor(x => x.ConsultationType).NotEmpty().Must(t => t is "FirstVisit" or "FollowUp" or "TreatmentDecision")
            .WithMessage("Consultation type must be FirstVisit, FollowUp, or TreatmentDecision");
    }
}

public class CreateConsultationHandler : IRequestHandler<CreateConsultationCommand, Result<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateConsultationHandler(IConsultationRepository consultationRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _consultationRepo = consultationRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsultationDto>> Handle(CreateConsultationCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<ConsultationDto>.Failure($"Patient with ID {request.PatientId} not found");

        var consultation = Consultation.Create(
            request.PatientId,
            request.DoctorId,
            request.ConsultationDate,
            request.ConsultationType,
            request.CycleId,
            request.ChiefComplaint,
            request.Notes,
            request.WaiveConsultationFee);

        try
        {
            await _consultationRepo.AddAsync(consultation, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<ConsultationDto>.Failure($"Failed to create consultation: {ex.Message}");
        }

        var saved = await _consultationRepo.GetByIdAsync(consultation.Id, ct);
        return Result<ConsultationDto>.Success(ConsultationDto.FromEntity(saved!));
    }
}

// ==================== START CONSULTATION ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record StartConsultationCommand(Guid ConsultationId) : IRequest<Result<ConsultationDto>>;

public class StartConsultationHandler : IRequestHandler<StartConsultationCommand, Result<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public StartConsultationHandler(IConsultationRepository consultationRepo, IUnitOfWork unitOfWork)
    {
        _consultationRepo = consultationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsultationDto>> Handle(StartConsultationCommand request, CancellationToken ct)
    {
        var consultation = await _consultationRepo.GetByIdAsync(request.ConsultationId, ct);
        if (consultation == null)
            return Result<ConsultationDto>.Failure("Consultation not found");

        if (consultation.Status != "Scheduled")
            return Result<ConsultationDto>.Failure($"Cannot start consultation with status '{consultation.Status}'");

        consultation.Start();
        await _consultationRepo.UpdateAsync(consultation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsultationDto>.Success(ConsultationDto.FromEntity(consultation));
    }
}

// ==================== RECORD CLINICAL DATA ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record RecordClinicalDataCommand(
    Guid ConsultationId,
    string? ChiefComplaint,
    string? MedicalHistory,
    string? PastHistory,
    string? SurgicalHistory,
    string? FamilyHistory,
    string? ObstetricHistory,
    string? MenstrualHistory,
    string? PhysicalExamination
) : IRequest<Result<ConsultationDto>>;

public class RecordClinicalDataHandler : IRequestHandler<RecordClinicalDataCommand, Result<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordClinicalDataHandler(IConsultationRepository consultationRepo, IUnitOfWork unitOfWork)
    {
        _consultationRepo = consultationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsultationDto>> Handle(RecordClinicalDataCommand request, CancellationToken ct)
    {
        var consultation = await _consultationRepo.GetByIdAsync(request.ConsultationId, ct);
        if (consultation == null)
            return Result<ConsultationDto>.Failure("Consultation not found");

        if (consultation.Status == "Cancelled")
            return Result<ConsultationDto>.Failure("Cannot update a cancelled consultation");

        consultation.RecordClinicalData(
            request.ChiefComplaint,
            request.MedicalHistory,
            request.PastHistory,
            request.SurgicalHistory,
            request.FamilyHistory,
            request.ObstetricHistory,
            request.MenstrualHistory,
            request.PhysicalExamination);

        await _consultationRepo.UpdateAsync(consultation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsultationDto>.Success(ConsultationDto.FromEntity(consultation));
    }
}

// ==================== RECORD DIAGNOSIS & TREATMENT DECISION ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record RecordDiagnosisCommand(
    Guid ConsultationId,
    string? Diagnosis,
    string? TreatmentPlan,
    TreatmentMethod? RecommendedMethod
) : IRequest<Result<ConsultationDto>>;

public class RecordDiagnosisHandler : IRequestHandler<RecordDiagnosisCommand, Result<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordDiagnosisHandler(IConsultationRepository consultationRepo, IUnitOfWork unitOfWork)
    {
        _consultationRepo = consultationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsultationDto>> Handle(RecordDiagnosisCommand request, CancellationToken ct)
    {
        var consultation = await _consultationRepo.GetByIdAsync(request.ConsultationId, ct);
        if (consultation == null)
            return Result<ConsultationDto>.Failure("Consultation not found");

        consultation.RecordDiagnosis(request.Diagnosis, request.TreatmentPlan, request.RecommendedMethod);
        await _consultationRepo.UpdateAsync(consultation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsultationDto>.Success(ConsultationDto.FromEntity(consultation));
    }
}

// ==================== COMPLETE CONSULTATION ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record CompleteConsultationCommand(Guid ConsultationId) : IRequest<Result<ConsultationDto>>;

public class CompleteConsultationHandler : IRequestHandler<CompleteConsultationCommand, Result<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteConsultationHandler(IConsultationRepository consultationRepo, IUnitOfWork unitOfWork)
    {
        _consultationRepo = consultationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsultationDto>> Handle(CompleteConsultationCommand request, CancellationToken ct)
    {
        var consultation = await _consultationRepo.GetByIdAsync(request.ConsultationId, ct);
        if (consultation == null)
            return Result<ConsultationDto>.Failure("Consultation not found");

        consultation.Complete();
        await _consultationRepo.UpdateAsync(consultation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsultationDto>.Success(ConsultationDto.FromEntity(consultation));
    }
}

// ==================== CANCEL CONSULTATION ====================
[RequiresFeature(FeatureCodes.Consultation)]
public record CancelConsultationCommand(Guid ConsultationId) : IRequest<Result<ConsultationDto>>;

public class CancelConsultationHandler : IRequestHandler<CancelConsultationCommand, Result<ConsultationDto>>
{
    private readonly IConsultationRepository _consultationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CancelConsultationHandler(IConsultationRepository consultationRepo, IUnitOfWork unitOfWork)
    {
        _consultationRepo = consultationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ConsultationDto>> Handle(CancelConsultationCommand request, CancellationToken ct)
    {
        var consultation = await _consultationRepo.GetByIdAsync(request.ConsultationId, ct);
        if (consultation == null)
            return Result<ConsultationDto>.Failure("Consultation not found");

        consultation.Cancel();
        await _consultationRepo.UpdateAsync(consultation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<ConsultationDto>.Success(ConsultationDto.FromEntity(consultation));
    }
}
