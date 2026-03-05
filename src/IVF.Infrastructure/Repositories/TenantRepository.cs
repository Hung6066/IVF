using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IvfDbContext _context;

    public TenantRepository(IvfDbContext context) => _context = context;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<Tenant?> GetByCustomDomainAsync(string domain, CancellationToken ct = default)
        => await _context.Tenants.FirstOrDefaultAsync(t => t.CustomDomain == domain.ToLowerInvariant(), ct);

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => await _context.Tenants.AnyAsync(t => t.Slug == slug, ct);

    public async Task<bool> CustomDomainExistsAsync(string domain, Guid? excludeTenantId = null, CancellationToken ct = default)
    {
        var normalizedDomain = domain.ToLowerInvariant();
        var query = _context.Tenants.Where(t => t.CustomDomain == normalizedDomain);
        if (excludeTenantId.HasValue)
            query = query.Where(t => t.Id != excludeTenantId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetAllAsync(
        int page, int pageSize, string? search, TenantStatus? status, CancellationToken ct = default)
    {
        var query = _context.Tenants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || t.Slug.Contains(search));

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
        => await _context.Tenants.AddAsync(tenant, ct);

    public async Task<TenantSubscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.TenantSubscriptions
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, ct);

    public async Task AddSubscriptionAsync(TenantSubscription subscription, CancellationToken ct = default)
        => await _context.TenantSubscriptions.AddAsync(subscription, ct);

    public async Task<TenantUsageRecord?> GetCurrentUsageAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.TenantUsageRecords
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Year == now.Year && u.Month == now.Month, ct);
    }

    public async Task AddUsageRecordAsync(TenantUsageRecord usage, CancellationToken ct = default)
        => await _context.TenantUsageRecords.AddAsync(usage, ct);

    public async Task<int> GetTenantUserCountAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Users.IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive, ct);

    public async Task<int> GetTenantPatientCountAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Patients.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted, ct);

    public async Task<decimal> GetTotalMonthlyRevenueAsync(CancellationToken ct = default)
        => await _context.TenantSubscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active)
            .SumAsync(s => s.MonthlyPrice, ct);

    public async Task<List<TenantUsageRecord>> GetAllCurrentUsagesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.TenantUsageRecords.AsNoTracking()
            .Where(u => u.Year == now.Year && u.Month == now.Month)
            .ToListAsync(ct);
    }

    public async Task<SubscriptionPlan?> GetTenantActiveSubscriptionPlanAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.TenantSubscriptions
            .Where(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active)
            .Select(s => (SubscriptionPlan?)s.Plan)
            .FirstOrDefaultAsync(ct);

    public async Task<List<Tenant>> GetAllTenantsRawAsync(CancellationToken ct = default)
        => await _context.Tenants.AsNoTracking().ToListAsync(ct);

    public async Task<int> GetTenantTreatmentCycleCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        return await _context.TreatmentCycles.IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted && c.CreatedAt >= startDate && c.CreatedAt < endDate, ct);
    }

    public async Task<int> GetTenantFormResponseCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        return await _context.FormResponses.IgnoreQueryFilters()
            .CountAsync(f => f.TenantId == tenantId && !f.IsDeleted && f.CreatedAt >= startDate && f.CreatedAt < endDate, ct);
    }

    public async Task<int> GetTenantSignedDocumentCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        return await _context.PatientDocuments
            .Where(d => !d.IsDeleted && d.IsSigned && d.SignedAt >= startDate && d.SignedAt < endDate)
            .Join(_context.Patients.IgnoreQueryFilters().Where(p => p.TenantId == tenantId),
                d => d.PatientId, p => p.Id, (d, p) => d)
            .CountAsync(ct);
    }

    public async Task<long> GetTenantStorageUsedBytesAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.PatientDocuments
            .Where(d => !d.IsDeleted)
            .Join(_context.Patients.IgnoreQueryFilters().Where(p => p.TenantId == tenantId),
                d => d.PatientId, p => p.Id, (d, p) => d)
            .SumAsync(d => d.FileSizeBytes, ct);

    public async Task<int> GetTenantNewPatientCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        return await _context.Patients.IgnoreQueryFilters()
            .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted && p.CreatedAt >= startDate && p.CreatedAt < endDate, ct);
    }

    public async Task<List<TenantUsageRecord>> GetUsageHistoryAsync(Guid tenantId, int months = 12, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMonths(-months);
        return await _context.TenantUsageRecords.AsNoTracking()
            .Where(u => u.TenantId == tenantId &&
                        (u.Year > cutoff.Year || (u.Year == cutoff.Year && u.Month >= cutoff.Month)))
            .OrderByDescending(u => u.Year).ThenByDescending(u => u.Month)
            .ToListAsync(ct);
    }

    // ═══════════════ Usage Detail Drill-Down ═══════════════

    public async Task<List<(Guid Id, string FullName, string Username, string Role, bool IsActive, DateTime CreatedAt)>>
        GetTenantActiveUsersDetailAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rows = await _context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Username, u.Role, u.IsActive, u.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(u => (u.Id, u.FullName, u.Username, u.Role, u.IsActive, u.CreatedAt)).ToList();
    }

    public async Task<List<(Guid Id, string PatientCode, string FullName, string? Phone, DateTime CreatedAt)>>
        GetTenantNewPatientsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        var rows = await _context.Patients.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.CreatedAt >= startDate && p.CreatedAt < endDate)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.PatientCode, p.FullName, p.Phone, p.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(p => (p.Id, p.PatientCode, p.FullName, p.Phone, p.CreatedAt)).ToList();
    }

    public async Task<List<(Guid Id, string CycleCode, string Method, string Phase, string Outcome, DateTime CreatedAt)>>
        GetTenantTreatmentCyclesDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        var rows = await _context.TreatmentCycles.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.CreatedAt >= startDate && c.CreatedAt < endDate)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.CycleCode, Method = c.Method.ToString(), Phase = c.CurrentPhase.ToString(), Outcome = c.Outcome.ToString(), c.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(c => (c.Id, c.CycleCode, c.Method, c.Phase, c.Outcome, c.CreatedAt)).ToList();
    }

    public async Task<List<(Guid Id, string TemplateName, string? PatientName, string Status, DateTime CreatedAt)>>
        GetTenantFormResponsesDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        var rows = await _context.FormResponses.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.TenantId == tenantId && !f.IsDeleted && f.CreatedAt >= startDate && f.CreatedAt < endDate)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                TemplateName = f.FormTemplate != null ? f.FormTemplate.Name : "N/A",
                PatientName = f.Patient != null ? f.Patient.FullName : (string?)null,
                Status = f.Status.ToString(),
                f.CreatedAt
            })
            .ToListAsync(ct);
        return rows.Select(f => (f.Id, f.TemplateName, f.PatientName, f.Status, f.CreatedAt)).ToList();
    }

    public async Task<List<(Guid Id, string Title, string FileName, string DocumentType, long FileSizeBytes, string? SignedByName, DateTime? SignedAt, DateTime CreatedAt)>>
        GetTenantSignedDocumentsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        var rows = await _context.PatientDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && d.IsSigned && d.SignedAt >= startDate && d.SignedAt < endDate)
            .Join(_context.Patients.IgnoreQueryFilters().Where(p => p.TenantId == tenantId),
                d => d.PatientId, p => p.Id, (d, p) => d)
            .OrderByDescending(d => d.SignedAt)
            .Select(d => new { d.Id, d.Title, d.OriginalFileName, DocumentType = d.DocumentType.ToString(), d.FileSizeBytes, d.SignedByName, d.SignedAt, d.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(d => (d.Id, d.Title, d.OriginalFileName, d.DocumentType, d.FileSizeBytes, d.SignedByName, d.SignedAt, d.CreatedAt)).ToList();
    }

    public async Task<List<(Guid Id, string Title, string FileName, string DocumentType, long FileSizeBytes, string? PatientName, DateTime CreatedAt)>>
        GetTenantStorageDetailAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rows = await _context.PatientDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Join(_context.Patients.IgnoreQueryFilters().Where(p => p.TenantId == tenantId),
                d => d.PatientId, p => p.Id, (d, p) => new { d, p })
            .OrderByDescending(x => x.d.FileSizeBytes)
            .Select(x => new { x.d.Id, x.d.Title, x.d.OriginalFileName, DocumentType = x.d.DocumentType.ToString(), x.d.FileSizeBytes, PatientName = (string?)x.p.FullName, x.d.CreatedAt })
            .ToListAsync(ct);
        return rows.Select(x => (x.Id, x.Title, x.OriginalFileName, x.DocumentType, x.FileSizeBytes, x.PatientName, x.CreatedAt)).ToList();
    }

    // ═══════════════ Tenant User Management ═══════════════

    public async Task<(List<User> Users, int TotalCount)> GetTenantUsersAsync(
        Guid tenantId, string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Username.Contains(search));
        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (users, total);
    }

    public async Task<DateTime?> GetUserLastLoginAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserLoginHistories.AsNoTracking()
            .Where(h => h.UserId == userId && h.IsSuccess)
            .OrderByDescending(h => h.LoginAt)
            .Select(h => (DateTime?)h.LoginAt)
            .FirstOrDefaultAsync(ct);

    // ═══════════════ API Call Logs ═══════════════

    public async Task<(List<ApiCallLog> Items, int TotalCount)> GetTenantApiCallLogsAsync(
        Guid tenantId, int page, int pageSize, string? method, int? statusCode, CancellationToken ct = default)
    {
        var query = _context.ApiCallLogs.AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(a => a.Method == method);
        if (statusCode.HasValue)
            query = query.Where(a => a.StatusCode == statusCode.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(int Total, int Success, int Error, double AvgMs, long MaxMs)> GetTenantApiCallStatsAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var query = _context.ApiCallLogs.AsNoTracking().Where(a => a.TenantId == tenantId);
        var total = await query.CountAsync(ct);
        if (total == 0) return (0, 0, 0, 0, 0);

        var success = await query.CountAsync(a => a.StatusCode >= 200 && a.StatusCode < 400, ct);
        var error = await query.CountAsync(a => a.StatusCode >= 400, ct);
        var avgMs = await query.AverageAsync(a => (double)a.DurationMs, ct);
        var maxMs = await query.MaxAsync(a => a.DurationMs, ct);
        return (total, success, error, Math.Round(avgMs, 1), maxMs);
    }

    public async Task<Dictionary<string, int>> GetTenantApiCallsByMethodAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.ApiCallLogs.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .GroupBy(a => a.Method)
            .Select(g => new { Method = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Method, g => g.Count, ct);

    public async Task<Dictionary<string, int>> GetTenantApiCallsTopPathsAsync(Guid tenantId, int top = 10, CancellationToken ct = default)
        => await _context.ApiCallLogs.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .GroupBy(a => a.Path)
            .Select(g => new { Path = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(top)
            .ToDictionaryAsync(g => g.Path, g => g.Count, ct);
}
