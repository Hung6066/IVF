using System.Text.Json;
using IVF.Domain.Common;
using IVF.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Interceptor that automatically creates audit logs for entity changes
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    
    public AuditInterceptor(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditLogs = CreateAuditLogs(eventData.Context);
        
        if (auditLogs.Any())
        {
            await eventData.Context.Set<AuditLog>().AddRangeAsync(auditLogs, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditLog> CreateAuditLogs(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var auditLogs = new List<AuditLog>();

        var userId = GetCurrentUserId();
        var username = GetCurrentUsername();
        var ipAddress = GetIpAddress();
        var userAgent = GetUserAgent();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Skip audit log entries themselves and non-entity types
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            // Only audit entities with Id property (BaseEntity)
            if (entry.Entity is not BaseEntity baseEntity)
                continue;

            var entityType = entry.Entity.GetType().Name;
            var entityId = baseEntity.Id;

            string action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => null!
            };

            if (action == null) continue;

            string? oldValues = null;
            string? newValues = null;
            string? changedColumns = null;

            if (entry.State == EntityState.Modified)
            {
                var changes = new Dictionary<string, object?>();
                var originals = new Dictionary<string, object?>();
                var columns = new List<string>();

                foreach (var prop in entry.Properties.Where(p => p.IsModified))
                {
                    var propName = prop.Metadata.Name;
                    if (propName is "UpdatedAt" or "CreatedAt") continue;
                    
                    columns.Add(propName);
                    originals[propName] = prop.OriginalValue;
                    changes[propName] = prop.CurrentValue;
                }

                if (columns.Any())
                {
                    oldValues = JsonSerializer.Serialize(originals);
                    newValues = JsonSerializer.Serialize(changes);
                    changedColumns = string.Join(",", columns);
                }
            }
            else if (entry.State == EntityState.Added)
            {
                var values = entry.Properties
                    .Where(p => p.Metadata.Name is not ("CreatedAt" or "UpdatedAt"))
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                newValues = JsonSerializer.Serialize(values);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var values = entry.Properties
                    .Where(p => p.Metadata.Name is not ("CreatedAt" or "UpdatedAt"))
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                oldValues = JsonSerializer.Serialize(values);
            }

            var auditLog = AuditLog.Create(
                entityType,
                entityId,
                action,
                userId,
                username,
                oldValues,
                newValues,
                changedColumns,
                ipAddress,
                userAgent);

            auditLogs.Add(auditLog);
        }

        return auditLogs;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = _httpContextAccessor?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    private string? GetCurrentUsername()
        => _httpContextAccessor?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

    private string? GetIpAddress()
        => _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    private string? GetUserAgent()
        => _httpContextAccessor?.HttpContext?.Request?.Headers["User-Agent"].ToString();
}
