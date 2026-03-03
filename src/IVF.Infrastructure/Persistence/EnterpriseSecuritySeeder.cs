using System.Text.Json;
using IVF.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// Seeds default enterprise security configuration.
/// Idempotent — only runs if no records exist yet.
/// </summary>
public static class EnterpriseSecuritySeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        await SeedConditionalAccessPoliciesAsync(context);
        await SeedIncidentResponseRulesAsync(context);
        await SeedDataRetentionPoliciesAsync(context);
    }

    private static async Task SeedConditionalAccessPoliciesAsync(IvfDbContext context)
    {
        if (await context.ConditionalAccessPolicies.AnyAsync())
            return;

        Console.WriteLine("[EnterpriseSecuritySeeder] Seeding conditional access policies...");

        var blockHighRisk = ConditionalAccessPolicy.Create(
            "Block Critical Risk Logins", "Block login attempts with critical risk scores",
            10, "Block", null);
        blockHighRisk.SetConditions(maxRiskLevel: "70");

        var mfaForAdmin = ConditionalAccessPolicy.Create(
            "MFA for Admin Roles", "Require MFA for all admin operations",
            20, "RequireMfa", null);
        mfaForAdmin.SetConditions(
            targetRoles: JsonSerializer.Serialize(new[] { "Admin" }));
        mfaForAdmin.Disable(); // Disabled by default — enable after admin MFA is configured

        var blockTor = ConditionalAccessPolicy.Create(
            "Block VPN/Tor Access", "Block connections from VPN and Tor exit nodes",
            30, "Block", null);
        blockTor.SetConditions(blockVpnTor: true);

        var geoRestrict = ConditionalAccessPolicy.Create(
            "Geography Restriction", "Only allow logins from Vietnam",
            50, "RequireMfa", null);
        geoRestrict.SetConditions(
            allowedCountries: JsonSerializer.Serialize(new[] { "VN" }));
        geoRestrict.Disable(); // Disabled by default — enable after GeoIP is configured

        context.ConditionalAccessPolicies.AddRange(blockHighRisk, mfaForAdmin, blockTor, geoRestrict);
        await context.SaveChangesAsync();
        Console.WriteLine("[EnterpriseSecuritySeeder] Seeded 4 conditional access policies.");
    }

    private static async Task SeedIncidentResponseRulesAsync(IvfDbContext context)
    {
        if (await context.IncidentResponseRules.AnyAsync())
            return;

        Console.WriteLine("[EnterpriseSecuritySeeder] Seeding incident response rules...");

        var bruteForce = IncidentResponseRule.Create(
            "Brute Force Auto-Lock",
            "Lock account after repeated failed logins",
            10,
            JsonSerializer.Serialize(new[] { "LoginFailed", "BruteForceDetected" }),
            JsonSerializer.Serialize(new[] { "High", "Critical" }),
            JsonSerializer.Serialize(new[] { "lock_account", "notify_admin" }),
            "High", null);

        var credStuffing = IncidentResponseRule.Create(
            "Credential Stuffing Response",
            "Block IP and notify on credential stuffing detection",
            20,
            JsonSerializer.Serialize(new[] { "CredentialStuffingDetected" }),
            JsonSerializer.Serialize(new[] { "Critical" }),
            JsonSerializer.Serialize(new[] { "block_ip", "lock_account", "notify_admin" }),
            "Critical", null);

        var accountTakeover = IncidentResponseRule.Create(
            "Account Takeover Prevention",
            "Revoke sessions and require password change on takeover detection",
            15,
            JsonSerializer.Serialize(new[] { "AccountTakeoverDetected" }),
            JsonSerializer.Serialize(new[] { "Critical" }),
            JsonSerializer.Serialize(new[] { "revoke_sessions", "require_password_change", "notify_admin" }),
            "Critical", null);

        var anomalyAlert = IncidentResponseRule.Create(
            "Behavior Anomaly Alert",
            "Notify admin when behavioral anomaly detected",
            50,
            JsonSerializer.Serialize(new[] { "BehaviorAnomalyDetected" }),
            JsonSerializer.Serialize(new[] { "Medium", "High" }),
            JsonSerializer.Serialize(new[] { "notify_admin" }),
            "Medium", null);

        context.IncidentResponseRules.AddRange(bruteForce, credStuffing, accountTakeover, anomalyAlert);
        await context.SaveChangesAsync();
        Console.WriteLine("[EnterpriseSecuritySeeder] Seeded 4 incident response rules.");
    }

    private static async Task SeedDataRetentionPoliciesAsync(IvfDbContext context)
    {
        if (await context.DataRetentionPolicies.AnyAsync())
            return;

        Console.WriteLine("[EnterpriseSecuritySeeder] Seeding data retention policies...");

        var policies = new[]
        {
            DataRetentionPolicy.Create("SecurityEvent", 365, "Purge", null),
            DataRetentionPolicy.Create("UserLoginHistory", 180, "Anonymize", null),
            DataRetentionPolicy.Create("UserSession", 90, "Purge", null),
            DataRetentionPolicy.Create("AuditLog", 730, "Archive", null),
        };

        context.DataRetentionPolicies.AddRange(policies);
        await context.SaveChangesAsync();
        Console.WriteLine("[EnterpriseSecuritySeeder] Seeded 4 data retention policies.");
    }
}
