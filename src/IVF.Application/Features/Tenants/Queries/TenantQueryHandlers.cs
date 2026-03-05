using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Tenants.Queries;

public class GetAllTenantsQueryHandler : IRequestHandler<GetAllTenantsQuery, PagedResult<TenantListItemDto>>
{
    private readonly ITenantRepository _repo;

    public GetAllTenantsQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<PagedResult<TenantListItemDto>> Handle(GetAllTenantsQuery request, CancellationToken ct)
    {
        var (tenants, totalCount) = await _repo.GetAllAsync(request.Page, request.PageSize, request.Search, request.Status, ct);

        var items = new List<TenantListItemDto>();
        foreach (var t in tenants)
        {
            var plan = await _repo.GetTenantActiveSubscriptionPlanAsync(t.Id, ct);
            var userCount = await _repo.GetTenantUserCountAsync(t.Id, ct);
            var patientCount = await _repo.GetTenantPatientCountAsync(t.Id, ct);
            items.Add(new TenantListItemDto(t.Id, t.Name, t.Slug, t.Status, t.IsolationStrategy, plan, userCount, patientCount, t.CreatedAt));
        }

        return new PagedResult<TenantListItemDto>(items, totalCount, request.Page, request.PageSize);
    }
}

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto?>
{
    private readonly ITenantRepository _repo;

    public GetTenantByIdQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<TenantDto?> Handle(GetTenantByIdQuery request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.Id, ct);
        if (tenant is null) return null;

        var sub = await _repo.GetActiveSubscriptionAsync(tenant.Id, ct);
        var usage = await _repo.GetCurrentUsageAsync(tenant.Id, ct);

        return new TenantDto(
            tenant.Id, tenant.Name, tenant.Slug, tenant.LogoUrl,
            tenant.Address, tenant.Phone, tenant.Email, tenant.Website, tenant.TaxId,
            tenant.Status, tenant.MaxUsers, tenant.MaxPatientsPerMonth, tenant.StorageLimitMb,
            tenant.AiEnabled, tenant.DigitalSigningEnabled, tenant.BiometricsEnabled, tenant.AdvancedReportingEnabled,
            tenant.IsolationStrategy, tenant.IsRootTenant,
            tenant.DatabaseSchema, tenant.ConnectionString,
            tenant.PrimaryColor, tenant.Locale, tenant.TimeZone, tenant.CustomDomain,
            tenant.CreatedAt,
            sub is null ? null : new SubscriptionDto(
                sub.Id, sub.Plan, sub.Status, sub.BillingCycle,
                sub.MonthlyPrice, sub.DiscountPercent, sub.Currency,
                sub.StartDate, sub.EndDate, sub.TrialEndDate, sub.NextBillingDate,
                sub.AutoRenew, sub.GetEffectivePrice()),
            usage is null ? null : new UsageDto(
                usage.Year, usage.Month, usage.ActiveUsers, usage.NewPatients,
                usage.TreatmentCycles, usage.FormResponses, usage.SignedDocuments,
                usage.StorageUsedMb, usage.ApiCalls));
    }
}

public class GetTenantStatsQueryHandler : IRequestHandler<GetTenantStatsQuery, TenantPlatformStats>
{
    private readonly ITenantRepository _repo;

    public GetTenantStatsQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<TenantPlatformStats> Handle(GetTenantStatsQuery request, CancellationToken ct)
    {
        var tenants = await _repo.GetAllTenantsRawAsync(ct);
        var revenue = await _repo.GetTotalMonthlyRevenueAsync(ct);
        var usages = await _repo.GetAllCurrentUsagesAsync(ct);

        return new TenantPlatformStats(
            TotalTenants: tenants.Count,
            ActiveTenants: tenants.Count(t => t.Status == TenantStatus.Active),
            TrialTenants: tenants.Count(t => t.Status == TenantStatus.Trial),
            SuspendedTenants: tenants.Count(t => t.Status == TenantStatus.Suspended),
            MonthlyRevenue: revenue,
            TotalUsers: usages.Sum(u => u.ActiveUsers),
            TotalPatients: usages.Sum(u => u.NewPatients),
            TotalStorageMb: usages.Sum(u => u.StorageUsedMb));
    }
}

public class GetTenantUsageHistoryQueryHandler : IRequestHandler<GetTenantUsageHistoryQuery, TenantUsageAnalytics>
{
    private readonly ITenantRepository _repo;
    private readonly IPricingRepository _pricingRepo;

    public GetTenantUsageHistoryQueryHandler(ITenantRepository repo, IPricingRepository pricingRepo)
    {
        _repo = repo;
        _pricingRepo = pricingRepo;
    }

    public async Task<TenantUsageAnalytics> Handle(GetTenantUsageHistoryQuery request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.TenantId, ct);
        if (tenant is null) throw new KeyNotFoundException($"Tenant {request.TenantId} not found");

        var now = DateTime.UtcNow;

        // Get real-time counts
        var activeUsers = await _repo.GetTenantUserCountAsync(request.TenantId, ct);
        var newPatients = await _repo.GetTenantNewPatientCountAsync(request.TenantId, now.Year, now.Month, ct);
        var treatmentCycles = await _repo.GetTenantTreatmentCycleCountAsync(request.TenantId, now.Year, now.Month, ct);
        var formResponses = await _repo.GetTenantFormResponseCountAsync(request.TenantId, now.Year, now.Month, ct);
        var signedDocs = await _repo.GetTenantSignedDocumentCountAsync(request.TenantId, now.Year, now.Month, ct);
        var storageBytes = await _repo.GetTenantStorageUsedBytesAsync(request.TenantId, ct);
        var storageMb = storageBytes / (1024 * 1024);

        // Get API calls from usage record (can't compute from logs)
        var currentUsage = await _repo.GetCurrentUsageAsync(request.TenantId, ct);
        var apiCalls = currentUsage?.ApiCalls ?? 0;

        var snapshot = new UsageSnapshotDto(
            now.Year, now.Month, activeUsers, newPatients,
            treatmentCycles, formResponses, signedDocs, storageMb, apiCalls);

        // Get effective limits
        var (maxUsers, maxPatients, storageLimitMb) = await GetEffectiveLimitsAsync(request.TenantId, ct);

        var usersPercent = maxUsers > 0 ? Math.Round((double)activeUsers / maxUsers * 100, 1) : 0;
        var patientsPercent = maxPatients > 0 ? Math.Round((double)newPatients / maxPatients * 100, 1) : 0;
        var storagePercent = storageLimitMb > 0 ? Math.Round((double)storageMb / storageLimitMb * 100, 1) : 0;

        var limitComparison = new LimitComparisonDto(
            maxUsers, activeUsers, usersPercent,
            maxPatients, newPatients, patientsPercent,
            storageLimitMb, storageMb, storagePercent);

        // Usage history
        var historyRecords = await _repo.GetUsageHistoryAsync(request.TenantId, request.Months, ct);
        var history = historyRecords.Select(h => new UsageHistoryItemDto(
            h.Year, h.Month, h.ActiveUsers, h.NewPatients,
            h.TreatmentCycles, h.FormResponses, h.SignedDocuments,
            h.StorageUsedMb, h.ApiCalls)).ToList();

        // Generate alerts
        var alerts = new List<UsageAlertDto>();
        AddAlerts(alerts, "users", "người dùng", usersPercent);
        AddAlerts(alerts, "patients", "bệnh nhân/tháng", patientsPercent);
        AddAlerts(alerts, "storage", "lưu trữ", storagePercent);

        return new TenantUsageAnalytics(snapshot, limitComparison, history, alerts);
    }

    private static void AddAlerts(List<UsageAlertDto> alerts, string metric, string label, double percent)
    {
        if (percent >= 100)
            alerts.Add(new UsageAlertDto("critical", metric,
                $"Đã đạt giới hạn {label} ({percent}%). Cần nâng cấp gói.", percent));
        else if (percent >= 90)
            alerts.Add(new UsageAlertDto("warning", metric,
                $"Sắp đạt giới hạn {label} ({percent}%). Nên nâng cấp gói.", percent));
        else if (percent >= 80)
            alerts.Add(new UsageAlertDto("info", metric,
                $"Đã sử dụng {percent}% giới hạn {label}.", percent));
    }

    private async Task<(int MaxUsers, int MaxPatients, long StorageLimitMb)> GetEffectiveLimitsAsync(
        Guid tenantId, CancellationToken ct)
    {
        var sub = await _repo.GetActiveSubscriptionAsync(tenantId, ct);
        if (sub != null)
        {
            var plan = await _pricingRepo.GetPlanByTypeAsync(sub.Plan, ct);
            if (plan != null)
                return (plan.MaxUsers, plan.MaxPatientsPerMonth, plan.StorageLimitMb);
        }
        var tenant = await _repo.GetByIdAsync(tenantId, ct);
        if (tenant != null)
            return (tenant.MaxUsers, tenant.MaxPatientsPerMonth, tenant.StorageLimitMb);
        return (5, 50, 1024);
    }
}

public class RefreshTenantUsageCommandHandler : IRequestHandler<RefreshTenantUsageCommand, UsageSnapshotDto>
{
    private readonly ITenantRepository _repo;
    private readonly IUnitOfWork _uow;

    public RefreshTenantUsageCommandHandler(ITenantRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<UsageSnapshotDto> Handle(RefreshTenantUsageCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tenantId = request.TenantId;

        var activeUsers = await _repo.GetTenantUserCountAsync(tenantId, ct);
        var newPatients = await _repo.GetTenantNewPatientCountAsync(tenantId, now.Year, now.Month, ct);
        var treatmentCycles = await _repo.GetTenantTreatmentCycleCountAsync(tenantId, now.Year, now.Month, ct);
        var formResponses = await _repo.GetTenantFormResponseCountAsync(tenantId, now.Year, now.Month, ct);
        var signedDocs = await _repo.GetTenantSignedDocumentCountAsync(tenantId, now.Year, now.Month, ct);
        var storageBytes = await _repo.GetTenantStorageUsedBytesAsync(tenantId, ct);
        var storageMb = storageBytes / (1024 * 1024);

        var usage = await _repo.GetCurrentUsageAsync(tenantId, ct);
        if (usage is null)
        {
            usage = TenantUsageRecord.Create(tenantId, now.Year, now.Month);
            await _repo.AddUsageRecordAsync(usage, ct);
        }

        usage.UpdateUsage(activeUsers, newPatients, treatmentCycles,
            formResponses, signedDocs, storageMb, usage.ApiCalls);

        await _uow.SaveChangesAsync(ct);

        return new UsageSnapshotDto(
            now.Year, now.Month, activeUsers, newPatients,
            treatmentCycles, formResponses, signedDocs, storageMb, usage.ApiCalls);
    }
}

public class GetTenantUsageDetailQueryHandler : IRequestHandler<GetTenantUsageDetailQuery, UsageDetailResult>
{
    private readonly ITenantRepository _repo;

    public GetTenantUsageDetailQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<UsageDetailResult> Handle(GetTenantUsageDetailQuery request, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(request.TenantId, ct);
        if (tenant is null) throw new KeyNotFoundException($"Tenant {request.TenantId} not found");

        return request.Metric switch
        {
            "users" => await GetUsersDetailAsync(request.TenantId, ct),
            "patients" => await GetPatientsDetailAsync(request.TenantId, request.Year, request.Month, ct),
            "cycles" => await GetCyclesDetailAsync(request.TenantId, request.Year, request.Month, ct),
            "forms" => await GetFormsDetailAsync(request.TenantId, request.Year, request.Month, ct),
            "documents" => await GetDocumentsDetailAsync(request.TenantId, request.Year, request.Month, ct),
            "storage" => await GetStorageDetailAsync(request.TenantId, ct),
            _ => new UsageDetailResult(request.Metric, "Không xác định", 0, [])
        };
    }

    private async Task<UsageDetailResult> GetUsersDetailAsync(Guid tenantId, CancellationToken ct)
    {
        var users = await _repo.GetTenantActiveUsersDetailAsync(tenantId, ct);
        var items = users.Select(u => new UsageDetailItemDto(
            u.Id, u.FullName, u.Username, u.IsActive ? "Hoạt động" : "Ngưng", u.CreatedAt,
            new Dictionary<string, string> { ["role"] = u.Role }
        )).ToList();
        return new UsageDetailResult("users", "Người dùng hoạt động", items.Count, items);
    }

    private async Task<UsageDetailResult> GetPatientsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct)
    {
        var patients = await _repo.GetTenantNewPatientsDetailAsync(tenantId, year, month, ct);
        var items = patients.Select(p => new UsageDetailItemDto(
            p.Id, p.FullName, p.PatientCode, null, p.CreatedAt,
            p.Phone != null ? new Dictionary<string, string> { ["phone"] = p.Phone } : null
        )).ToList();
        return new UsageDetailResult("patients", $"Bệnh nhân mới — Tháng {month}/{year}", items.Count, items);
    }

    private async Task<UsageDetailResult> GetCyclesDetailAsync(Guid tenantId, int year, int month, CancellationToken ct)
    {
        var cycles = await _repo.GetTenantTreatmentCyclesDetailAsync(tenantId, year, month, ct);
        var items = cycles.Select(c => new UsageDetailItemDto(
            c.Id, c.CycleCode, c.Method, c.Outcome, c.CreatedAt,
            new Dictionary<string, string> { ["phase"] = c.Phase }
        )).ToList();
        return new UsageDetailResult("cycles", $"Chu kỳ điều trị — Tháng {month}/{year}", items.Count, items);
    }

    private async Task<UsageDetailResult> GetFormsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct)
    {
        var forms = await _repo.GetTenantFormResponsesDetailAsync(tenantId, year, month, ct);
        var items = forms.Select(f => new UsageDetailItemDto(
            f.Id, f.TemplateName, f.PatientName, f.Status, f.CreatedAt, null
        )).ToList();
        return new UsageDetailResult("forms", $"Biểu mẫu — Tháng {month}/{year}", items.Count, items);
    }

    private async Task<UsageDetailResult> GetDocumentsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct)
    {
        var docs = await _repo.GetTenantSignedDocumentsDetailAsync(tenantId, year, month, ct);
        var items = docs.Select(d => new UsageDetailItemDto(
            d.Id, d.Title, d.FileName, d.DocumentType, d.CreatedAt,
            new Dictionary<string, string>
            {
                ["fileSize"] = FormatBytes(d.FileSizeBytes),
                ["signedBy"] = d.SignedByName ?? "N/A",
                ["signedAt"] = d.SignedAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"
            }
        )).ToList();
        return new UsageDetailResult("documents", $"Tài liệu đã ký — Tháng {month}/{year}", items.Count, items);
    }

    private async Task<UsageDetailResult> GetStorageDetailAsync(Guid tenantId, CancellationToken ct)
    {
        var docs = await _repo.GetTenantStorageDetailAsync(tenantId, ct);
        var items = docs.Select(d => new UsageDetailItemDto(
            d.Id, d.Title, d.FileName, d.DocumentType, d.CreatedAt,
            new Dictionary<string, string>
            {
                ["fileSize"] = FormatBytes(d.FileSizeBytes),
                ["patient"] = d.PatientName ?? "N/A"
            }
        )).ToList();
        var totalBytes = docs.Sum(d => d.FileSizeBytes);
        return new UsageDetailResult("storage", $"Chi tiết lưu trữ — {FormatBytes(totalBytes)}", items.Count, items);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}

// ═══════════════ Tenant User Management ═══════════════

public class GetTenantUsersQueryHandler : IRequestHandler<GetTenantUsersQuery, TenantUsersResult>
{
    private readonly ITenantRepository _repo;

    public GetTenantUsersQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<TenantUsersResult> Handle(GetTenantUsersQuery request, CancellationToken ct)
    {
        var (users, total) = await _repo.GetTenantUsersAsync(
            request.TenantId, request.Search, request.Role, request.IsActive,
            request.Page, request.PageSize, ct);

        var items = new List<TenantUserDto>();
        foreach (var u in users)
        {
            var lastLogin = await _repo.GetUserLastLoginAsync(u.Id, ct);
            items.Add(new TenantUserDto(
                u.Id, u.Username, u.FullName, u.Role, u.Department,
                u.IsActive, u.CreatedAt, lastLogin));
        }

        return new TenantUsersResult(items, total, request.Page, request.PageSize);
    }
}

public class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _uow;

    public AdminResetPasswordCommandHandler(ITenantRepository tenantRepo, IUserRepository userRepo, IUnitOfWork uow)
    {
        _tenantRepo = tenantRepo;
        _userRepo = userRepo;
        _uow = uow;
    }

    public async Task Handle(AdminResetPasswordCommand request, CancellationToken ct)
    {
        var tenant = await _tenantRepo.GetByIdAsync(request.TenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {request.TenantId} not found");

        var user = await _userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User {request.UserId} not found");

        if (user.TenantId != request.TenantId)
            throw new UnauthorizedAccessException("User does not belong to this tenant");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatePassword(passwordHash);

        await _userRepo.UpdateAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
    }
}

// ═══════════════ API Call Logs ═══════════════

public class GetTenantApiCallsQueryHandler : IRequestHandler<GetTenantApiCallsQuery, TenantApiCallsResult>
{
    private readonly ITenantRepository _repo;

    public GetTenantApiCallsQueryHandler(ITenantRepository repo) => _repo = repo;

    public async Task<TenantApiCallsResult> Handle(GetTenantApiCallsQuery request, CancellationToken ct)
    {
        var (items, total) = await _repo.GetTenantApiCallLogsAsync(
            request.TenantId, request.Page, request.PageSize, request.Method, request.StatusCode, ct);

        var (statsTotal, success, error, avgMs, maxMs) = await _repo.GetTenantApiCallStatsAsync(request.TenantId, ct);
        var byMethod = await _repo.GetTenantApiCallsByMethodAsync(request.TenantId, ct);
        var topPaths = await _repo.GetTenantApiCallsTopPathsAsync(request.TenantId, 10, ct);

        var dtos = items.Select(a => new ApiCallLogDto(
            a.Id, a.Username, a.Method, a.Path, a.StatusCode,
            a.DurationMs, a.IpAddress, a.RequestedAt)).ToList();

        var stats = new ApiCallStatsDto(statsTotal, success, error, avgMs, maxMs, byMethod, topPaths);

        return new TenantApiCallsResult(dtos, total, request.Page, request.PageSize, stats);
    }
}
