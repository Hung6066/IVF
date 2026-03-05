using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using FluentValidation;
using MediatR;

namespace IVF.Application.Features.Tenants.Commands;

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Guid>
{
    private readonly ITenantRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly IPricingRepository _pricingRepo;
    private readonly IUnitOfWork _uow;

    private static readonly Dictionary<SubscriptionPlan, decimal> PlanPricing = new()
    {
        [SubscriptionPlan.Trial] = 0,
        [SubscriptionPlan.Starter] = 5_000_000,
        [SubscriptionPlan.Professional] = 15_000_000,
        [SubscriptionPlan.Enterprise] = 35_000_000,
        [SubscriptionPlan.Custom] = 0
    };

    private static readonly Dictionary<SubscriptionPlan, (int Users, int Patients, long StorageMb, bool Ai, bool Sign, bool Bio, bool Reports)> PlanLimits = new()
    {
        [SubscriptionPlan.Trial] = (3, 20, 512, false, false, false, false),
        [SubscriptionPlan.Starter] = (10, 100, 5_120, false, false, false, true),
        [SubscriptionPlan.Professional] = (30, 500, 20_480, true, true, false, true),
        [SubscriptionPlan.Enterprise] = (100, 2000, 102_400, true, true, true, true),
        [SubscriptionPlan.Custom] = (999, 99999, 1_048_576, true, true, true, true)
    };

    public CreateTenantCommandHandler(ITenantRepository repo, IUserRepository userRepo, IPricingRepository pricingRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _userRepo = userRepo;
        _pricingRepo = pricingRepo;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateTenantCommand request, CancellationToken ct)
    {
        if (await _repo.SlugExistsAsync(request.Slug.ToLowerInvariant(), ct))
            throw new InvalidOperationException($"Tenant slug '{request.Slug}' already exists");

        var tenant = Tenant.Create(request.Name, request.Slug, request.Email, request.Phone, request.Address);

        var limits = PlanLimits[request.Plan];
        tenant.SetResourceLimits(limits.Users, limits.Patients, limits.StorageMb,
            limits.Ai, limits.Sign, limits.Bio, limits.Reports);

        // Set isolation strategy
        if (request.IsolationStrategy != DataIsolationStrategy.SharedDatabase)
            tenant.SetDatabaseIsolation(request.IsolationStrategy, null, request.Slug.ToLowerInvariant());

        if (request.Plan == SubscriptionPlan.Trial)
            tenant.StartTrial();
        else
            tenant.Activate();

        await _repo.AddAsync(tenant, ct);

        var price = PlanPricing[request.Plan];
        var subscription = TenantSubscription.Create(tenant.Id, request.Plan, request.BillingCycle, price);
        await _repo.AddSubscriptionAsync(subscription, ct);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword);
        var adminUser = User.Create(request.AdminUsername, passwordHash, request.AdminFullName, "Admin", null, tenant.Id);
        await _userRepo.AddAsync(adminUser, ct);

        var now = DateTime.UtcNow;
        var usage = TenantUsageRecord.Create(tenant.Id, now.Year, now.Month);
        usage.UpdateUsage(1, 0, 0, 0, 0, 0, 0);
        await _repo.AddUsageRecordAsync(usage, ct);

        // Sync tenant features from plan definition
        await _pricingRepo.SyncTenantFeaturesFromPlanAsync(tenant.Id, request.Plan, ct);

        await _uow.SaveChangesAsync(ct);
        return tenant.Id;
    }
}

public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public UpdateTenantCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(UpdateTenantCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");
        tenant.UpdateInfo(request.Name, request.Address, request.Phone, request.Email, request.Website, request.TaxId);
        await _uow.SaveChangesAsync(ct);
    }
}

public class UpdateTenantBrandingCommandHandler : IRequestHandler<UpdateTenantBrandingCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public UpdateTenantBrandingCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(UpdateTenantBrandingCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");

        // Validate custom domain format and uniqueness
        if (!string.IsNullOrWhiteSpace(request.CustomDomain))
        {
            var domain = request.CustomDomain.Trim().ToLowerInvariant();
            if (!IsValidDomain(domain))
                throw new ValidationException("Tên miền không hợp lệ. Vui lòng nhập dạng: clinic.your-domain.com");

            if (await _repo.CustomDomainExistsAsync(domain, request.Id, ct))
                throw new ValidationException("Tên miền này đã được sử dụng bởi tenant khác.");
        }

        tenant.UpdateBranding(request.LogoUrl, request.PrimaryColor, request.CustomDomain);
        await _uow.SaveChangesAsync(ct);
    }

    private static bool IsValidDomain(string domain)
    {
        if (domain.Length > 200) return false;
        // Must be a valid hostname (no scheme, no path, no port)
        return System.Text.RegularExpressions.Regex.IsMatch(domain,
            @"^(?!-)[a-z0-9-]{1,63}(?<!-)(\.[a-z0-9-]{1,63})*\.[a-z]{2,}$");
    }
}

public class VerifyCustomDomainCommandHandler : IRequestHandler<VerifyCustomDomainCommand, CustomDomainVerificationResult>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IDomainVerificationService _domainVerification;

    public VerifyCustomDomainCommandHandler(ITenantRepository repo, IUnitOfWork uow, IDomainVerificationService domainVerification)
    {
        _repo = repo;
        _uow = uow;
        _domainVerification = domainVerification;
    }

    public async Task<CustomDomainVerificationResult> Handle(VerifyCustomDomainCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.TenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.TenantId} not found");

        if (string.IsNullOrWhiteSpace(tenant.CustomDomain))
            return new CustomDomainVerificationResult(false, "Tenant chưa cấu hình custom domain.");

        var result = await _domainVerification.VerifyDomainAsync(
            tenant.CustomDomain, tenant.CustomDomainVerificationToken!, ct);

        if (result.TxtVerified)
        {
            tenant.VerifyCustomDomain();
            await _uow.SaveChangesAsync(ct);
            return new CustomDomainVerificationResult(true, "Xác minh tên miền thành công!");
        }
        else
        {
            tenant.FailCustomDomainVerification();
            await _uow.SaveChangesAsync(ct);

            var details = new List<string>();
            if (!result.CnameVerified)
                details.Add($"CNAME: Trỏ {tenant.CustomDomain} → app.ivf.clinic");
            if (!result.TxtVerified)
                details.Add($"TXT: Tạo _ivf-verify.{tenant.CustomDomain} với giá trị: {tenant.CustomDomainVerificationToken}");

            return new CustomDomainVerificationResult(false,
                $"Xác minh thất bại. Vui lòng cấu hình DNS:\n{string.Join("\n", details)}");
        }
    }
}

public class RemoveCustomDomainCommandHandler : IRequestHandler<RemoveCustomDomainCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public RemoveCustomDomainCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(RemoveCustomDomainCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.TenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.TenantId} not found");
        tenant.RemoveCustomDomain();
        await _uow.SaveChangesAsync(ct);
    }
}

public class UpdateTenantLimitsCommandHandler : IRequestHandler<UpdateTenantLimitsCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public UpdateTenantLimitsCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(UpdateTenantLimitsCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");
        tenant.SetResourceLimits(request.MaxUsers, request.MaxPatientsPerMonth, request.StorageLimitMb,
            request.AiEnabled, request.DigitalSigningEnabled, request.BiometricsEnabled, request.AdvancedReportingEnabled);
        await _uow.SaveChangesAsync(ct);
    }
}

public class ActivateTenantCommandHandler : IRequestHandler<ActivateTenantCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public ActivateTenantCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(ActivateTenantCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");
        tenant.Activate();
        await _uow.SaveChangesAsync(ct);
    }
}

public class SuspendTenantCommandHandler : IRequestHandler<SuspendTenantCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public SuspendTenantCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(SuspendTenantCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");
        tenant.Suspend(request.Reason);
        await _uow.SaveChangesAsync(ct);
    }
}

public class CancelTenantCommandHandler : IRequestHandler<CancelTenantCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    public CancelTenantCommandHandler(ITenantRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task Handle(CancelTenantCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");
        tenant.Cancel();

        var sub = await _repo.GetActiveSubscriptionAsync(request.Id, ct);
        sub?.Cancel();

        await _uow.SaveChangesAsync(ct);
    }
}

public class UpdateSubscriptionCommandHandler : IRequestHandler<UpdateSubscriptionCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IPricingRepository _pricingRepo;
    private readonly IUnitOfWork _uow;
    public UpdateSubscriptionCommandHandler(ITenantRepository repo, IPricingRepository pricingRepo, IUnitOfWork uow)
    {
        _repo = repo;
        _pricingRepo = pricingRepo;
        _uow = uow;
    }

    public async Task Handle(UpdateSubscriptionCommand request, CancellationToken ct)
    {
        var sub = await _repo.GetActiveSubscriptionAsync(request.TenantId, ct);

        if (sub is not null)
        {
            sub.UpgradePlan(request.Plan, request.MonthlyPrice);
            sub.SetDiscount(request.DiscountPercent);
        }
        else
        {
            sub = TenantSubscription.Create(request.TenantId, request.Plan, request.BillingCycle, request.MonthlyPrice);
            sub.SetDiscount(request.DiscountPercent);
            await _repo.AddSubscriptionAsync(sub, ct);
        }

        // Sync tenant features when plan changes
        await _pricingRepo.SyncTenantFeaturesFromPlanAsync(request.TenantId, request.Plan, ct);

        await _uow.SaveChangesAsync(ct);
    }
}

public class UpdateTenantIsolationCommandHandler : IRequestHandler<UpdateTenantIsolationCommand>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ITenantProvisioningService _provisioning;

    public UpdateTenantIsolationCommandHandler(
        ITenantRepository repo,
        IUnitOfWork uow,
        ITenantProvisioningService provisioning)
    {
        _repo = repo;
        _uow = uow;
        _provisioning = provisioning;
    }

    public async Task Handle(UpdateTenantIsolationCommand request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.Id} not found");

        if (tenant.IsRootTenant)
            throw new InvalidOperationException("Cannot change isolation strategy for the root tenant");

        var previousStrategy = tenant.IsolationStrategy;
        var previousSchema = tenant.DatabaseSchema;
        var previousConnStr = tenant.ConnectionString;

        // Deprovision previous isolation if changing strategy
        if (previousStrategy != request.IsolationStrategy
            && previousStrategy != DataIsolationStrategy.SharedDatabase)
        {
            await _provisioning.DeprovisionAsync(
                tenant.Id, previousStrategy, previousSchema, previousConnStr, ct);
        }

        // Provision new isolation infrastructure
        if (request.IsolationStrategy != DataIsolationStrategy.SharedDatabase)
        {
            var result = await _provisioning.ProvisionAsync(
                tenant.Id, tenant.Slug, request.IsolationStrategy, ct);

            if (!result.Success)
                throw new InvalidOperationException(
                    $"Provisioning failed: {result.ErrorMessage}");

            tenant.SetDatabaseIsolation(
                request.IsolationStrategy,
                result.ConnectionString,
                result.SchemaName);
        }
        else
        {
            tenant.SetDatabaseIsolation(DataIsolationStrategy.SharedDatabase, null, null);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
