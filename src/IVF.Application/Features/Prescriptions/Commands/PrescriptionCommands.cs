using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Prescriptions.Commands;

// ==================== DTOs ====================
public record PrescriptionDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientCode,
    Guid? CycleId,
    Guid DoctorId,
    string DoctorName,
    DateTime PrescriptionDate,
    string Status,
    DateTime? EnteredAt,
    DateTime? PrintedAt,
    DateTime? DispensedAt,
    string? Notes,
    bool WaiveConsultationFee,
    DateTime CreatedAt,
    List<PrescriptionItemDto> Items)
{
    public static PrescriptionDto FromEntity(Prescription p) => new(
        p.Id,
        p.PatientId,
        p.Patient?.FullName ?? "",
        p.Patient?.PatientCode ?? "",
        p.CycleId,
        p.DoctorId,
        p.Doctor?.FullName ?? "",
        p.PrescriptionDate,
        p.Status,
        p.EnteredAt,
        p.PrintedAt,
        p.DispensedAt,
        p.Notes,
        p.WaiveConsultationFee,
        p.CreatedAt,
        p.Items?.Select(PrescriptionItemDto.FromEntity).ToList() ?? []);
}

public record PrescriptionItemDto(
    Guid Id,
    string? DrugCode,
    string DrugName,
    string? Dosage,
    string? Frequency,
    string? Duration,
    int Quantity)
{
    public static PrescriptionItemDto FromEntity(PrescriptionItem item) => new(
        item.Id,
        item.DrugCode,
        item.DrugName,
        item.Dosage,
        item.Frequency,
        item.Duration,
        item.Quantity);
}

// ==================== CREATE PRESCRIPTION ====================
public record PrescriptionItemInput(
    string DrugName,
    int Quantity,
    string? DrugCode = null,
    string? Dosage = null,
    string? Frequency = null,
    string? Duration = null);

[RequiresFeature(FeatureCodes.Pharmacy)]
public record CreatePrescriptionCommand(
    Guid PatientId,
    Guid DoctorId,
    DateTime PrescriptionDate,
    Guid? CycleId,
    string? Notes,
    Guid? TemplateId,
    bool WaiveConsultationFee,
    List<PrescriptionItemInput> Items
) : IRequest<Result<PrescriptionDto>>;

public class CreatePrescriptionValidator : AbstractValidator<CreatePrescriptionCommand>
{
    public CreatePrescriptionValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient ID is required");
        RuleFor(x => x.DoctorId).NotEmpty().WithMessage("Doctor ID is required");
        RuleFor(x => x.PrescriptionDate).NotEmpty().WithMessage("Prescription date is required");
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DrugName).NotEmpty().WithMessage("Drug name is required");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
        });
    }
}

public class CreatePrescriptionHandler : IRequestHandler<CreatePrescriptionCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IPatientRepository _patientRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrescriptionHandler(IPrescriptionRepository prescriptionRepo, IPatientRepository patientRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _patientRepo = patientRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(CreatePrescriptionCommand request, CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(request.PatientId, ct);
        if (patient == null)
            return Result<PrescriptionDto>.Failure($"Patient with ID {request.PatientId} not found");

        var prescription = Prescription.Create(
            request.PatientId,
            request.DoctorId,
            request.PrescriptionDate,
            request.CycleId,
            request.Notes,
            request.TemplateId,
            request.WaiveConsultationFee);

        try
        {
            await _prescriptionRepo.AddAsync(prescription, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Add items after prescription is saved (need the Id)
            foreach (var item in request.Items)
            {
                var prescriptionItem = PrescriptionItem.Create(
                    prescription.Id,
                    item.DrugName,
                    item.Quantity,
                    item.DrugCode,
                    item.Dosage,
                    item.Frequency,
                    item.Duration);
                prescription.AddItem(prescriptionItem);
            }

            await _prescriptionRepo.UpdateAsync(prescription, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<PrescriptionDto>.Failure($"Failed to create prescription: {ex.Message}");
        }

        // Reload with navigation properties
        var saved = await _prescriptionRepo.GetByIdWithItemsAsync(prescription.Id, ct);
        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(saved!));
    }
}

// ==================== ADD ITEM ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record AddPrescriptionItemCommand(
    Guid PrescriptionId,
    string DrugName,
    int Quantity,
    string? DrugCode,
    string? Dosage,
    string? Frequency,
    string? Duration
) : IRequest<Result<PrescriptionDto>>;

public class AddPrescriptionItemValidator : AbstractValidator<AddPrescriptionItemCommand>
{
    public AddPrescriptionItemValidator()
    {
        RuleFor(x => x.PrescriptionId).NotEmpty();
        RuleFor(x => x.DrugName).NotEmpty().WithMessage("Drug name is required");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than 0");
    }
}

public class AddPrescriptionItemHandler : IRequestHandler<AddPrescriptionItemCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AddPrescriptionItemHandler(IPrescriptionRepository prescriptionRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(AddPrescriptionItemCommand request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return Result<PrescriptionDto>.Failure("Prescription not found");

        if (prescription.Status is "Dispensed" or "Cancelled")
            return Result<PrescriptionDto>.Failure("Cannot modify a dispensed or cancelled prescription");

        var item = PrescriptionItem.Create(
            prescription.Id,
            request.DrugName,
            request.Quantity,
            request.DrugCode,
            request.Dosage,
            request.Frequency,
            request.Duration);

        prescription.AddItem(item);
        await _prescriptionRepo.UpdateAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(prescription));
    }
}

// ==================== ENTER PRESCRIPTION (pharmacist receives) ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record EnterPrescriptionCommand(Guid PrescriptionId, Guid EnteredByUserId) : IRequest<Result<PrescriptionDto>>;

public class EnterPrescriptionHandler : IRequestHandler<EnterPrescriptionCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public EnterPrescriptionHandler(IPrescriptionRepository prescriptionRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(EnterPrescriptionCommand request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return Result<PrescriptionDto>.Failure("Prescription not found");

        if (prescription.Status != "Pending")
            return Result<PrescriptionDto>.Failure($"Cannot enter prescription with status '{prescription.Status}'");

        prescription.Enter(request.EnteredByUserId);
        await _prescriptionRepo.UpdateAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(prescription));
    }
}

// ==================== PRINT PRESCRIPTION ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record PrintPrescriptionCommand(Guid PrescriptionId) : IRequest<Result<PrescriptionDto>>;

public class PrintPrescriptionHandler : IRequestHandler<PrintPrescriptionCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public PrintPrescriptionHandler(IPrescriptionRepository prescriptionRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(PrintPrescriptionCommand request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return Result<PrescriptionDto>.Failure("Prescription not found");

        prescription.MarkPrinted();
        await _prescriptionRepo.UpdateAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(prescription));
    }
}

// ==================== DISPENSE PRESCRIPTION ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record DispensePrescriptionCommand(Guid PrescriptionId, Guid DispensedByUserId) : IRequest<Result<PrescriptionDto>>;

public class DispensePrescriptionHandler : IRequestHandler<DispensePrescriptionCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DispensePrescriptionHandler(IPrescriptionRepository prescriptionRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(DispensePrescriptionCommand request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return Result<PrescriptionDto>.Failure("Prescription not found");

        if (prescription.Status is "Dispensed" or "Cancelled")
            return Result<PrescriptionDto>.Failure($"Cannot dispense prescription with status '{prescription.Status}'");

        prescription.Dispense(request.DispensedByUserId);
        await _prescriptionRepo.UpdateAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(prescription));
    }
}

// ==================== CANCEL PRESCRIPTION ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record CancelPrescriptionCommand(Guid PrescriptionId) : IRequest<Result<PrescriptionDto>>;

public class CancelPrescriptionHandler : IRequestHandler<CancelPrescriptionCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPrescriptionHandler(IPrescriptionRepository prescriptionRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(CancelPrescriptionCommand request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return Result<PrescriptionDto>.Failure("Prescription not found");

        if (prescription.Status == "Dispensed")
            return Result<PrescriptionDto>.Failure("Cannot cancel a dispensed prescription");

        prescription.Cancel();
        await _prescriptionRepo.UpdateAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(prescription));
    }
}

// ==================== UPDATE NOTES ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record UpdatePrescriptionNotesCommand(Guid PrescriptionId, string? Notes) : IRequest<Result<PrescriptionDto>>;

public class UpdatePrescriptionNotesHandler : IRequestHandler<UpdatePrescriptionNotesCommand, Result<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePrescriptionNotesHandler(IPrescriptionRepository prescriptionRepo, IUnitOfWork unitOfWork)
    {
        _prescriptionRepo = prescriptionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionDto>> Handle(UpdatePrescriptionNotesCommand request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.PrescriptionId, ct);
        if (prescription == null)
            return Result<PrescriptionDto>.Failure("Prescription not found");

        prescription.UpdateNotes(request.Notes);
        await _prescriptionRepo.UpdateAsync(prescription, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<PrescriptionDto>.Success(PrescriptionDto.FromEntity(prescription));
    }
}
