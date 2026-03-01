using System.Reflection;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Common.Services;

public interface IFieldAccessService
{
    Task ApplyFieldAccessAsync<T>(T dto, string tableName, string userRole, CancellationToken ct = default) where T : class;
    Task ApplyFieldAccessAsync<T>(IEnumerable<T> dtos, string tableName, string userRole, CancellationToken ct = default) where T : class;
}

public class FieldAccessService : IFieldAccessService
{
    private readonly IVaultRepository _vaultRepo;
    private readonly ILogger<FieldAccessService> _logger;

    // Cache policies per request scope (Scoped lifetime)
    private List<FieldAccessPolicy>? _policiesCache;

    public FieldAccessService(IVaultRepository vaultRepo, ILogger<FieldAccessService> logger)
    {
        _vaultRepo = vaultRepo;
        _logger = logger;
    }

    public async Task ApplyFieldAccessAsync<T>(T dto, string tableName, string userRole, CancellationToken ct) where T : class
    {
        if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            return;

        var policies = await GetPoliciesAsync(ct);
        var applicable = policies
            .Where(p => string.Equals(p.TableName, tableName, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(p.Role, userRole, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (applicable.Count == 0) return;

        ApplyPolicies(dto, applicable);
    }

    public async Task ApplyFieldAccessAsync<T>(IEnumerable<T> dtos, string tableName, string userRole, CancellationToken ct) where T : class
    {
        if (string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
            return;

        var policies = await GetPoliciesAsync(ct);
        var applicable = policies
            .Where(p => string.Equals(p.TableName, tableName, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(p.Role, userRole, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (applicable.Count == 0) return;

        foreach (var dto in dtos)
        {
            ApplyPolicies(dto, applicable);
        }
    }

    private async Task<List<FieldAccessPolicy>> GetPoliciesAsync(CancellationToken ct)
    {
        return _policiesCache ??= await _vaultRepo.GetAllFieldAccessPoliciesAsync(ct);
    }

    private static void ApplyPolicies<T>(T dto, List<FieldAccessPolicy> policies) where T : class
    {
        foreach (var policy in policies)
        {
            var prop = typeof(T).GetProperty(policy.FieldName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (prop is null || prop.PropertyType != typeof(string) || !prop.CanWrite)
                continue;

            var value = prop.GetValue(dto) as string;
            if (string.IsNullOrEmpty(value)) continue;

            var masked = policy.AccessLevel switch
            {
                "full" => value,
                "partial" => value.Length > policy.PartialLength
                    ? value[..policy.PartialLength] + policy.MaskPattern
                    : value,
                "masked" => policy.MaskPattern,
                "none" => null,
                _ => value
            };

            prop.SetValue(dto, masked);
        }
    }
}
