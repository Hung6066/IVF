using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Entities;
using FluentValidation;
using MediatR;
using Entities = IVF.Domain.Entities;

namespace IVF.Application.Features.DrugCatalog.Commands;

// ==================== DTO ====================
public class DrugCatalogDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? DefaultDosage { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public static DrugCatalogDto FromEntity(Entities.DrugCatalog d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        GenericName = d.GenericName,
        Category = d.Category.ToString(),
        Unit = d.Unit,
        ActiveIngredient = d.ActiveIngredient,
        DefaultDosage = d.DefaultDosage,
        Notes = d.Notes,
        IsActive = d.IsActive,
        CreatedAt = d.CreatedAt
    };
}

// ==================== CREATE ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record CreateDrugCatalogCommand(
    string Code,
    string Name,
    string GenericName,
    DrugCategory Category,
    string Unit,
    string? ActiveIngredient,
    string? DefaultDosage,
    string? Notes) : IRequest<Result<DrugCatalogDto>>;

public class CreateDrugCatalogValidator : AbstractValidator<CreateDrugCatalogCommand>
{
    public CreateDrugCatalogValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GenericName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
    }
}

public class CreateDrugCatalogHandler : IRequestHandler<CreateDrugCatalogCommand, Result<DrugCatalogDto>>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public CreateDrugCatalogHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<DrugCatalogDto>> Handle(CreateDrugCatalogCommand req, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        if (await _repo.CodeExistsAsync(req.Code, tenantId, ct))
            return Result<DrugCatalogDto>.Failure($"Mã thuốc '{req.Code}' đã tồn tại.");

        var drug = Entities.DrugCatalog.Create(tenantId, req.Code, req.Name, req.GenericName,
            req.Category, req.Unit, req.ActiveIngredient, req.DefaultDosage, req.Notes);

        await _repo.AddAsync(drug, ct);
        return Result<DrugCatalogDto>.Success(DrugCatalogDto.FromEntity(drug));
    }
}

// ==================== UPDATE ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record UpdateDrugCatalogCommand(
    Guid Id,
    string Name,
    string GenericName,
    DrugCategory Category,
    string Unit,
    string? ActiveIngredient,
    string? DefaultDosage,
    string? Notes) : IRequest<Result<DrugCatalogDto>>;

public class UpdateDrugCatalogValidator : AbstractValidator<UpdateDrugCatalogCommand>
{
    public UpdateDrugCatalogValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GenericName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
    }
}

public class UpdateDrugCatalogHandler : IRequestHandler<UpdateDrugCatalogCommand, Result<DrugCatalogDto>>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public UpdateDrugCatalogHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<DrugCatalogDto>> Handle(UpdateDrugCatalogCommand req, CancellationToken ct)
    {
        var drug = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (drug is null) return Result<DrugCatalogDto>.Failure("Không tìm thấy thuốc.");

        drug.Update(req.Name, req.GenericName, req.Category, req.Unit,
            req.ActiveIngredient, req.DefaultDosage, req.Notes);

        await _repo.UpdateAsync(drug, ct);
        return Result<DrugCatalogDto>.Success(DrugCatalogDto.FromEntity(drug));
    }
}

// ==================== TOGGLE ACTIVE ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record ToggleDrugActiveCommand(Guid Id, bool Activate) : IRequest<Result<DrugCatalogDto>>;

public class ToggleDrugActiveHandler : IRequestHandler<ToggleDrugActiveCommand, Result<DrugCatalogDto>>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public ToggleDrugActiveHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<Result<DrugCatalogDto>> Handle(ToggleDrugActiveCommand req, CancellationToken ct)
    {
        var drug = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (drug is null) return Result<DrugCatalogDto>.Failure("Không tìm thấy thuốc.");

        if (req.Activate) drug.Activate(); else drug.Deactivate();
        await _repo.UpdateAsync(drug, ct);
        return Result<DrugCatalogDto>.Success(DrugCatalogDto.FromEntity(drug));
    }
}
