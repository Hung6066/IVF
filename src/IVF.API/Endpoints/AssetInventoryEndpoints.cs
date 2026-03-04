using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class AssetInventoryEndpoints
{
    public static void MapAssetInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance/assets")
            .WithTags("AssetInventory")
            .RequireAuthorization("AdminOnly");

        // List all assets
        group.MapGet("/", async (IvfDbContext db, string? type, string? classification, string? status) =>
        {
            var query = db.AssetInventories.Where(a => !a.IsDeleted).AsQueryable();

            if (!string.IsNullOrEmpty(type))
                query = query.Where(a => a.AssetType == type);
            if (!string.IsNullOrEmpty(classification))
                query = query.Where(a => a.Classification == classification);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(a => a.Status == status);

            var assets = await query.OrderBy(a => a.AssetName).ToListAsync();
            return Results.Ok(assets);
        }).WithName("ListAssets");

        // Get single asset
        group.MapGet("/{id:guid}", async (Guid id, IvfDbContext db) =>
        {
            var asset = await db.AssetInventories.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            return asset is null ? Results.NotFound() : Results.Ok(asset);
        }).WithName("GetAsset");

        // Create asset
        group.MapPost("/", async (CreateAssetRequest req, IvfDbContext db) =>
        {
            var asset = AssetInventory.Create(
                req.AssetName, req.AssetType, req.Classification,
                req.Owner, req.CriticalityLevel,
                req.ContainsPhi, req.ContainsPii,
                req.Department, req.Location, req.Environment, req.Version);

            if (req.HasEncryption || req.HasBackup || req.HasAccessControl || req.HasMonitoring)
                asset.UpdateSecurityPosture(req.HasEncryption, req.HasBackup, req.HasAccessControl, req.HasMonitoring);

            db.AssetInventories.Add(asset);
            await db.SaveChangesAsync();
            return Results.Created($"/api/compliance/assets/{asset.Id}", asset);
        }).WithName("CreateAsset");

        // Update security posture
        group.MapPut("/{id:guid}/security", async (Guid id, UpdateAssetSecurityRequest req, IvfDbContext db) =>
        {
            var asset = await db.AssetInventories.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (asset is null) return Results.NotFound();
            asset.UpdateSecurityPosture(req.HasEncryption, req.HasBackup, req.HasAccessControl, req.HasMonitoring);
            await db.SaveChangesAsync();
            return Results.Ok(asset);
        }).WithName("UpdateAssetSecurity");

        // Mark audited
        group.MapPost("/{id:guid}/audit", async (Guid id, IvfDbContext db, DateTime? nextAuditDue) =>
        {
            var asset = await db.AssetInventories.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (asset is null) return Results.NotFound();
            asset.MarkAudited(nextAuditDue);
            await db.SaveChangesAsync();
            return Results.Ok(asset);
        }).WithName("MarkAssetAudited");

        // Decommission asset
        group.MapPost("/{id:guid}/decommission", async (Guid id, IvfDbContext db) =>
        {
            var asset = await db.AssetInventories.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
            if (asset is null) return Results.NotFound();
            asset.Decommission();
            await db.SaveChangesAsync();
            return Results.Ok(asset);
        }).WithName("DecommissionAsset");

        // Dashboard — asset risk summary
        group.MapGet("/dashboard", async (IvfDbContext db) =>
        {
            var assets = await db.AssetInventories
                .Where(a => !a.IsDeleted && a.Status == AssetStatus.Active)
                .ToListAsync();

            var dashboard = new
            {
                totalAssets = assets.Count,
                byType = assets.GroupBy(a => a.AssetType)
                    .Select(g => new { type = g.Key, count = g.Count() }),
                byClassification = assets.GroupBy(a => a.Classification)
                    .Select(g => new { classification = g.Key, count = g.Count() }),
                byCriticality = assets.GroupBy(a => a.CriticalityLevel)
                    .Select(g => new { criticality = g.Key, count = g.Count() }),
                containingPhi = assets.Count(a => a.ContainsPhi),
                containingPii = assets.Count(a => a.ContainsPii),
                withEncryption = assets.Count(a => a.HasEncryption),
                withBackup = assets.Count(a => a.HasBackup),
                withAccessControl = assets.Count(a => a.HasAccessControl),
                withMonitoring = assets.Count(a => a.HasMonitoring),
                overdueForAudit = assets.Count(a => a.IsOverdueForAudit()),
                averageRiskScore = assets.Count > 0
                    ? Math.Round(assets.Average(a => a.CalculateRiskScore()), 1) : 0,
                highRiskAssets = assets
                    .Where(a => a.CalculateRiskScore() >= 50)
                    .Select(a => new { a.AssetName, a.AssetType, riskScore = a.CalculateRiskScore() })
                    .OrderByDescending(a => a.riskScore)
            };

            return Results.Ok(dashboard);
        }).WithName("AssetDashboard");

        // Overdue for audit
        group.MapGet("/overdue-audit", async (IvfDbContext db) =>
        {
            var assets = await db.AssetInventories
                .Where(a => !a.IsDeleted && a.Status == AssetStatus.Active)
                .ToListAsync();

            var overdue = assets.Where(a => a.IsOverdueForAudit())
                .OrderBy(a => a.NextAuditDueAt)
                .ToList();

            return Results.Ok(overdue);
        }).WithName("AssetsOverdueForAudit");
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreateAssetRequest(
    string AssetName,
    string AssetType,
    string Classification,
    string Owner,
    string CriticalityLevel,
    bool ContainsPhi,
    bool ContainsPii,
    string? Department = null,
    string? Location = null,
    string? Environment = null,
    string? Version = null,
    bool HasEncryption = false,
    bool HasBackup = false,
    bool HasAccessControl = false,
    bool HasMonitoring = false);

public record UpdateAssetSecurityRequest(
    bool HasEncryption,
    bool HasBackup,
    bool HasAccessControl,
    bool HasMonitoring);
