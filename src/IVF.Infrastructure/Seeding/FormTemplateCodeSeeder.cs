using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Seeding;

/// <summary>
/// Chạy một lần khi startup: regenerate Code cho các form template
/// còn dùng UUID-prefix (từ migration backfill) hoặc chưa có code chuẩn.
/// </summary>
public static class FormTemplateCodeSeeder
{
    public static async Task RegenerateCodesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IvfDbContext>>();

        // Templates có code trông như UUID-prefix (32 hex lowercase, 20 chars) → cần regenerate
        var templates = await db.Set<FormTemplate>()
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .ToListAsync();

        var changed = 0;
        // Track codes already assigned to avoid collision
        var usedCodes = templates
            .Select(t => t.Code)
            .Where(c => !string.IsNullOrEmpty(c) && !IsUuidPrefix(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var t in templates)
        {
            if (!IsUuidPrefix(t.Code))
                continue; // Already has a proper code, skip

            var candidate = FormTemplate.GenerateCode(t.Name);

            // Ensure uniqueness: append number suffix if collision
            var finalCode = candidate;
            var suffix = 2;
            while (usedCodes.Contains(finalCode))
                finalCode = candidate.Length > 46 ? candidate[..46] + suffix++ : candidate + suffix++;

            usedCodes.Add(finalCode);

            // Use EF raw SQL to bypass private setter
            await db.Database.ExecuteSqlRawAsync(
                @"UPDATE form_templates SET ""Code"" = {0} WHERE ""Id"" = {1}",
                finalCode, t.Id);

            logger.LogInformation(
                "[FormTemplateCodeSeeder] {Id} '{Name}' → Code: '{OldCode}' → '{NewCode}'",
                t.Id, t.Name, t.Code, finalCode);
            changed++;
        }

        if (changed > 0)
            logger.LogInformation("[FormTemplateCodeSeeder] Updated {Count} template codes", changed);
    }

    /// <summary>
    /// UUID prefix từ migration backfill có dạng 32 hex lowercase chars (no hyphens), length 20.
    /// Ví dụ: "229c4ca47e434ba2b5a2"
    /// </summary>
    private static bool IsUuidPrefix(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 20) return false;
        return code.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }
}
