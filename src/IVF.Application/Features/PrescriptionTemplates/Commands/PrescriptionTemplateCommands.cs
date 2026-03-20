using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Entities;
using FluentValidation;
using MediatR;

namespace IVF.Application.Features.PrescriptionTemplates.Commands;

// ==================== DTOs ====================
public class PrescriptionTemplateItemDto
{
    public Guid Id { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string? Instructions { get; set; }

    public static PrescriptionTemplateItemDto FromEntity(PrescriptionTemplateItem i) => new()
    {
        Id = i.Id,
        MedicationName = i.MedicationName,
        Dosage = i.Dosage,
        Unit = i.Unit,
        Route = i.Route,
        Frequency = i.Frequency,
        DurationDays = i.DurationDays,
        Instructions = i.Instructions
    };
}

public class PrescriptionTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CycleType { get; set; } = string.Empty;
    public Guid CreatedByDoctorId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PrescriptionTemplateItemDto> Items { get; set; } = new();

    public static PrescriptionTemplateDto FromEntity(PrescriptionTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        CycleType = t.CycleType.ToString(),
        CreatedByDoctorId = t.CreatedByDoctorId,
        Description = t.Description,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        Items = t.Items.Select(PrescriptionTemplateItemDto.FromEntity).ToList()
    };
}

// ==================== CREATE ====================
public record TemplateItemInput(string MedicationName, string Dosage, string Unit, string Route, string Frequency, int DurationDays, string? Instructions);

[RequiresFeature(FeatureCodes.Pharmacy)]
public record CreatePrescriptionTemplateCommand(
    string Name,
    PrescriptionCycleType CycleType,
    Guid CreatedByDoctorId,
    string? Description,
    List<TemplateItemInput> Items) : IRequest<Result<PrescriptionTemplateDto>>;

public class CreatePrescriptionTemplateValidator : AbstractValidator<CreatePrescriptionTemplateCommand>
{
    public CreatePrescriptionTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CreatedByDoctorId).NotEmpty();
    }
}

public class CreatePrescriptionTemplateHandler : IRequestHandler<CreatePrescriptionTemplateCommand, Result<PrescriptionTemplateDto>>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrescriptionTemplateHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionTemplateDto>> Handle(CreatePrescriptionTemplateCommand req, CancellationToken ct)
    {
        var template = PrescriptionTemplate.Create(_currentUser.TenantId ?? Guid.Empty, req.Name, req.CycleType,
            req.CreatedByDoctorId, req.Description);

        var items = req.Items.Select(i => PrescriptionTemplateItem.Create(
            template.Id, i.MedicationName, i.Dosage, i.Unit, i.Route, i.Frequency, i.DurationDays, i.Instructions)).ToList();
        template.SetItems(items);

        await _repo.AddAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PrescriptionTemplateDto>.Success(PrescriptionTemplateDto.FromEntity(template));
    }
}

// ==================== UPDATE ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record UpdatePrescriptionTemplateCommand(
    Guid Id,
    string Name,
    PrescriptionCycleType CycleType,
    string? Description,
    List<TemplateItemInput> Items) : IRequest<Result<PrescriptionTemplateDto>>;

public class UpdatePrescriptionTemplateValidator : AbstractValidator<UpdatePrescriptionTemplateCommand>
{
    public UpdatePrescriptionTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdatePrescriptionTemplateHandler : IRequestHandler<UpdatePrescriptionTemplateCommand, Result<PrescriptionTemplateDto>>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePrescriptionTemplateHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionTemplateDto>> Handle(UpdatePrescriptionTemplateCommand req, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (template is null) return Result<PrescriptionTemplateDto>.Failure("Không tìm thấy mẫu đơn thuốc.");

        template.Update(req.Name, req.CycleType, req.Description);
        var items = req.Items.Select(i => PrescriptionTemplateItem.Create(
            template.Id, i.MedicationName, i.Dosage, i.Unit, i.Route, i.Frequency, i.DurationDays, i.Instructions)).ToList();
        template.SetItems(items);

        await _repo.UpdateAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PrescriptionTemplateDto>.Success(PrescriptionTemplateDto.FromEntity(template));
    }
}

// ==================== TOGGLE ACTIVE ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record ToggleTemplateActiveCommand(Guid Id, bool Activate) : IRequest<Result<PrescriptionTemplateDto>>;

public class ToggleTemplateActiveHandler : IRequestHandler<ToggleTemplateActiveCommand, Result<PrescriptionTemplateDto>>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleTemplateActiveHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PrescriptionTemplateDto>> Handle(ToggleTemplateActiveCommand req, CancellationToken ct)
    {
        var template = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (template is null) return Result<PrescriptionTemplateDto>.Failure("Không tìm thấy mẫu đơn thuốc.");

        if (req.Activate) template.Activate(); else template.Deactivate();
        await _repo.UpdateAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<PrescriptionTemplateDto>.Success(PrescriptionTemplateDto.FromEntity(template));
    }
}
