using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds default Zero Trust policies for vault access control.
/// Idempotent — only runs if no policies exist yet.
/// </summary>
public static class ZTPolicySeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.ZTPolicies.AnyAsync())
        {
            Console.WriteLine("[ZTPolicySeeder] Policies already exist. Skipping.");
            return;
        }

        Console.WriteLine("[ZTPolicySeeder] Seeding default Zero Trust policies...");

        var policies = new List<ZTPolicy>
        {
            ZTPolicy.Create(
                action: "VaultUnseal",
                requiredAuthLevel: "Password",
                maxAllowedRisk: "Medium",
                requireTrustedDevice: false,
                requireFreshSession: false,
                blockAnomaly: true,
                requireGeoFence: true,
                allowedCountries: "VN",
                blockVpnTor: false,
                allowBreakGlassOverride: true),

            ZTPolicy.Create(
                action: "SecretRead",
                requiredAuthLevel: "Session",
                maxAllowedRisk: "Medium",
                requireTrustedDevice: false,
                requireFreshSession: false,
                blockAnomaly: false,
                requireGeoFence: false,
                allowedCountries: null,
                blockVpnTor: false,
                allowBreakGlassOverride: false),

            ZTPolicy.Create(
                action: "SecretWrite",
                requiredAuthLevel: "FreshSession",
                maxAllowedRisk: "Medium",
                requireTrustedDevice: false,
                requireFreshSession: true,
                blockAnomaly: true,
                requireGeoFence: false,
                allowedCountries: null,
                blockVpnTor: false,
                allowBreakGlassOverride: true),

            ZTPolicy.Create(
                action: "SecretDelete",
                requiredAuthLevel: "Password",
                maxAllowedRisk: "Low",
                requireTrustedDevice: true,
                requireFreshSession: true,
                blockAnomaly: true,
                requireGeoFence: true,
                allowedCountries: "VN",
                blockVpnTor: true,
                allowBreakGlassOverride: true),

            ZTPolicy.Create(
                action: "SecretExport",
                requiredAuthLevel: "MFA",
                maxAllowedRisk: "Low",
                requireTrustedDevice: true,
                requireFreshSession: true,
                blockAnomaly: true,
                requireGeoFence: true,
                allowedCountries: "VN",
                blockVpnTor: true,
                allowBreakGlassOverride: true),

            ZTPolicy.Create(
                action: "KeyRotate",
                requiredAuthLevel: "MFA",
                maxAllowedRisk: "Low",
                requireTrustedDevice: true,
                requireFreshSession: true,
                blockAnomaly: true,
                requireGeoFence: true,
                allowedCountries: "VN",
                blockVpnTor: true,
                allowBreakGlassOverride: true),

            ZTPolicy.Create(
                action: "BreakGlassAccess",
                requiredAuthLevel: "Biometric",
                maxAllowedRisk: "Critical",
                requireTrustedDevice: false,
                requireFreshSession: false,
                blockAnomaly: false,
                requireGeoFence: false,
                allowedCountries: null,
                blockVpnTor: false,
                allowBreakGlassOverride: false),
        };

        context.ZTPolicies.AddRange(policies);
        await context.SaveChangesAsync();

        Console.WriteLine($"[ZTPolicySeeder] Seeded {policies.Count} Zero Trust policies.");
    }
}
