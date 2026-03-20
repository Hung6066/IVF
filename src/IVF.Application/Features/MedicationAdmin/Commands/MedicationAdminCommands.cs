using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.MedicationAdmin.Commands;

// ==================== DTOs ====================
public record MedicationAdminDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid CycleId,
    Guid? PrescriptionId,
    Guid AdministeredByUserId,
    string AdministeredByName,
    string MedicationName,
    string? MedicationCode,
    string Dosage,
    string Route,
    string? Site,
    DateTime AdministeredAt,
    DateTime? ScheduledAt,
    bool IsTriggerShot,
    string? BatchNumber,
    string? Notes,
    string Status,
    DateTime CreatedAt)
{
    public static MedicationAdminDto FromEntity(MedicationAdministration m) => new(
        m.Id, m.PatientId, m.Patient?.FullName ?? "", m.CycleId, m.PrescriptionId,
        m.AdministeredByUserId, m.AdministeredBy?.FullName ?? "",
        m.MedicationName, m.MedicationCode, m.Dosage, m.Route, m.Site,
        m.AdministeredAt, m.ScheduledAt, m.IsTriggerShot, m.BatchNumber,
        m.Notes, m.Status, m.CreatedAt);
}

// ==================== RECORD ADMINISTRATION ====================
[RequiresFeature(FeatureCodes.Injection)]
public record RecordMedicationAdminCommand(
    Guid PatientId,
    Guid CycleId,
    Guid AdministeredByUserId,
    string MedicationName,
    string Dosage,
    string Route,
    DateTime AdministeredAt,
    Guid? PrescriptionId,
    string? MedicationCode,
    string? Site,
    DateTime? ScheduledAt,
    bool IsTriggerShot,
    string? BatchNumber,
    string? Notes
) : IRequest<Result<MedicationAdminDto>>;

public class RecordMedicationAdminValidator : AbstractValidator<RecordMedicationAdminCommand>
{
    public RecordMedicationAdminValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.AdministeredByUserId).NotEmpty();
        RuleFor(x => x.MedicationName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dosage).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Route).NotEmpty().Must(r => r is "SC" or "IM" or "PO" or "IV" or "Topical")
            .WithMessage("Route must be SC, IM, PO, IV, or Topical");
    }
}

public class RecordMedicationAdminHandler : IRequestHandler<RecordMedicationAdminCommand, Result<MedicationAdminDto>>
{
    private readonly IMedicationAdministrationRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMedicationAdminHandler(IMedicationAdministrationRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MedicationAdminDto>> Handle(RecordMedicationAdminCommand r, CancellationToken ct)
    {
        var med = MedicationAdministration.Create(
            r.PatientId, r.CycleId, r.AdministeredByUserId, r.MedicationName,
            r.Dosage, r.Route, r.AdministeredAt, r.PrescriptionId, r.MedicationCode,
            r.Site, r.ScheduledAt, r.IsTriggerShot, r.BatchNumber, r.Notes);

        await _repo.AddAsync(med, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _repo.GetByIdAsync(med.Id, ct);
        return Result<MedicationAdminDto>.Success(MedicationAdminDto.FromEntity(saved!));
    }
}

// ==================== MARK SKIPPED ====================
[RequiresFeature(FeatureCodes.Injection)]
public record SkipMedicationCommand(Guid MedicationId, string? Reason) : IRequest<Result<MedicationAdminDto>>;

public class SkipMedicationHandler : IRequestHandler<SkipMedicationCommand, Result<MedicationAdminDto>>
{
    private readonly IMedicationAdministrationRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public SkipMedicationHandler(IMedicationAdministrationRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MedicationAdminDto>> Handle(SkipMedicationCommand r, CancellationToken ct)
    {
        var med = await _repo.GetByIdAsync(r.MedicationId, ct);
        if (med == null) return Result<MedicationAdminDto>.Failure("Medication record not found");

        med.MarkSkipped(r.Reason);
        await _repo.UpdateAsync(med, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<MedicationAdminDto>.Success(MedicationAdminDto.FromEntity(med));
    }
}
