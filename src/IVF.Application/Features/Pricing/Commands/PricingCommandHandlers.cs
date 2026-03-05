using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Pricing.Commands;

// ── Feature Definition Handlers ──

public class CreateFeatureDefinitionHandler : IRequestHandler<CreateFeatureDefinitionCommand, Guid>
{
    private readonly IPricingRepository _repo;
    public CreateFeatureDefinitionHandler(IPricingRepository repo) => _repo = repo;

    public async Task<Guid> Handle(CreateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = FeatureDefinition.Create(
            request.Code, request.DisplayName, request.Description,
            request.Icon, request.Category, request.SortOrder);
        await _repo.AddFeatureAsync(feature, ct);
        await _repo.SaveChangesAsync(ct);
        return feature.Id;
    }
}

public class UpdateFeatureDefinitionHandler : IRequestHandler<UpdateFeatureDefinitionCommand>
{
    private readonly IPricingRepository _repo;
    public UpdateFeatureDefinitionHandler(IPricingRepository repo) => _repo = repo;

    public async Task Handle(UpdateFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = await _repo.GetFeatureByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Feature {request.Id} not found");
        feature.Update(request.DisplayName, request.Description, request.Icon,
            request.Category, request.SortOrder, request.IsActive);
        await _repo.SaveChangesAsync(ct);
    }
}

public class DeleteFeatureDefinitionHandler : IRequestHandler<DeleteFeatureDefinitionCommand>
{
    private readonly IPricingRepository _repo;
    public DeleteFeatureDefinitionHandler(IPricingRepository repo) => _repo = repo;

    public async Task Handle(DeleteFeatureDefinitionCommand request, CancellationToken ct)
    {
        var feature = await _repo.GetFeatureByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Feature {request.Id} not found");
        feature.MarkAsDeleted();
        await _repo.SaveChangesAsync(ct);
    }
}

// ── Plan Definition Handlers ──

public class CreatePlanDefinitionHandler : IRequestHandler<CreatePlanDefinitionCommand, Guid>
{
    private readonly IPricingRepository _repo;
    public CreatePlanDefinitionHandler(IPricingRepository repo) => _repo = repo;

    public async Task<Guid> Handle(CreatePlanDefinitionCommand request, CancellationToken ct)
    {
        var plan = PlanDefinition.Create(
            request.Plan, request.DisplayName, request.Description,
            request.MonthlyPrice, request.Currency, request.Duration,
            request.MaxUsers, request.MaxPatientsPerMonth, request.StorageLimitMb,
            request.SortOrder, request.IsFeatured);
        await _repo.AddPlanAsync(plan, ct);
        await _repo.SaveChangesAsync(ct);
        return plan.Id;
    }
}

public class UpdatePlanDefinitionHandler : IRequestHandler<UpdatePlanDefinitionCommand>
{
    private readonly IPricingRepository _repo;
    public UpdatePlanDefinitionHandler(IPricingRepository repo) => _repo = repo;

    public async Task Handle(UpdatePlanDefinitionCommand request, CancellationToken ct)
    {
        var plan = await _repo.GetPlanByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Plan {request.Id} not found");
        plan.Update(request.DisplayName, request.Description, request.MonthlyPrice,
            request.Duration, request.MaxUsers, request.MaxPatientsPerMonth,
            request.StorageLimitMb, request.SortOrder, request.IsFeatured, request.IsActive);
        await _repo.SaveChangesAsync(ct);
    }
}

public class DeletePlanDefinitionHandler : IRequestHandler<DeletePlanDefinitionCommand>
{
    private readonly IPricingRepository _repo;
    public DeletePlanDefinitionHandler(IPricingRepository repo) => _repo = repo;

    public async Task Handle(DeletePlanDefinitionCommand request, CancellationToken ct)
    {
        var plan = await _repo.GetPlanByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Plan {request.Id} not found");
        plan.MarkAsDeleted();
        await _repo.SaveChangesAsync(ct);
    }
}

// ── Plan Feature Mapping Handler ──

public class UpdatePlanFeaturesHandler : IRequestHandler<UpdatePlanFeaturesCommand>
{
    private readonly IPricingRepository _repo;
    public UpdatePlanFeaturesHandler(IPricingRepository repo) => _repo = repo;

    public async Task Handle(UpdatePlanFeaturesCommand request, CancellationToken ct)
    {
        await _repo.RemovePlanFeaturesAsync(request.PlanDefinitionId, ct);

        for (int i = 0; i < request.FeatureDefinitionIds.Count; i++)
        {
            var pf = PlanFeature.Create(request.PlanDefinitionId, request.FeatureDefinitionIds[i], i);
            await _repo.AddPlanFeatureAsync(pf, ct);
        }

        await _repo.SaveChangesAsync(ct);
    }
}

// ── Tenant Feature Override Handler ──

public class UpdateTenantFeaturesHandler : IRequestHandler<UpdateTenantFeaturesCommand>
{
    private readonly IPricingRepository _repo;
    public UpdateTenantFeaturesHandler(IPricingRepository repo) => _repo = repo;

    public async Task Handle(UpdateTenantFeaturesCommand request, CancellationToken ct)
    {
        var existing = await _repo.GetTenantFeaturesAsync(request.TenantId, ct);
        var existingMap = existing.ToDictionary(tf => tf.FeatureDefinitionId);

        foreach (var update in request.Features)
        {
            if (existingMap.TryGetValue(update.FeatureDefinitionId, out var tf))
            {
                tf.SetEnabled(update.IsEnabled);
                existingMap.Remove(update.FeatureDefinitionId);
            }
            else
            {
                await _repo.AddTenantFeatureAsync(
                    TenantFeature.Create(request.TenantId, update.FeatureDefinitionId, update.IsEnabled), ct);
            }
        }

        await _repo.SaveChangesAsync(ct);
    }
}
