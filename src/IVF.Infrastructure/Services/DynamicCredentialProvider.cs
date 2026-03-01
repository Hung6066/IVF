using System.Security.Cryptography;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Generates short-lived PostgreSQL credentials (CREATE ROLE with VALID UNTIL).
/// Tracks credentials in VaultDynamicCredential table. Drops roles on revocation.
/// </summary>
public class DynamicCredentialProvider : IDynamicCredentialProvider
{
    private readonly IVaultRepository _repo;
    private readonly IKeyVaultService _kvSvc;
    private readonly ILogger<DynamicCredentialProvider> _logger;

    public DynamicCredentialProvider(
        IVaultRepository repo,
        IKeyVaultService kvSvc,
        ILogger<DynamicCredentialProvider> logger)
    {
        _repo = repo;
        _kvSvc = kvSvc;
        _logger = logger;
    }

    public async Task<DynamicCredentialResult> GenerateCredentialAsync(
        DynamicCredentialRequest request,
        CancellationToken ct = default)
    {
        // Generate secure random username and password
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var username = $"ivf_dyn_{suffix}";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var expiresAt = DateTime.UtcNow.AddSeconds(request.TtlSeconds);

        // Create the PostgreSQL role via admin connection
        var adminConnStr = BuildConnectionString(
            request.DbHost, request.DbPort, request.DbName,
            request.AdminUsername, request.AdminPassword);

        await using var conn = new NpgsqlConnection(adminConnStr);
        await conn.OpenAsync(ct);

        // CREATE ROLE with login, password, and VALID UNTIL for auto-expiry
        // Using parameterized approach for password, but role names must be identifiers
        var validUntil = expiresAt.ToString("yyyy-MM-dd HH:mm:ss+00");

        await using (var cmd = conn.CreateCommand())
        {
            // Role name is generated internally (ivf_dyn_<hex>), safe as identifier
            cmd.CommandText = $"""
                CREATE ROLE "{username}" LOGIN PASSWORD @pwd VALID UNTIL '{validUntil}';
                GRANT CONNECT ON DATABASE "{SanitizeIdentifier(request.DbName)}" TO "{username}";
                """;
            cmd.Parameters.AddWithValue("pwd", password);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Grant table-level permissions
        if (request.GrantedTables is { Length: > 0 })
        {
            var privilege = request.ReadOnly ? "SELECT" : "SELECT, INSERT, UPDATE, DELETE";
            foreach (var table in request.GrantedTables)
            {
                var safeTable = SanitizeIdentifier(table);
                await using var grantCmd = conn.CreateCommand();
                grantCmd.CommandText = $"""GRANT {privilege} ON TABLE "{safeTable}" TO "{username}";""";
                await grantCmd.ExecuteNonQueryAsync(ct);
            }
        }
        else
        {
            // Default: grant on all tables in public schema
            var privilege = request.ReadOnly ? "SELECT" : "SELECT, INSERT, UPDATE, DELETE";
            await using var grantCmd = conn.CreateCommand();
            grantCmd.CommandText = $"""GRANT {privilege} ON ALL TABLES IN SCHEMA public TO "{username}";""";
            await grantCmd.ExecuteNonQueryAsync(ct);
        }

        // Encrypt admin password for storage
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(request.AdminPassword);
        var encrypted = await _kvSvc.EncryptAsync(passwordBytes, KeyPurpose.Data, ct);
        var encryptedPassword = $"{encrypted.CiphertextBase64}|{encrypted.IvBase64}";

        // Track in VaultDynamicCredential
        var cred = VaultDynamicCredential.Create(
            "postgres", username, request.DbHost, request.DbPort,
            request.DbName, request.AdminUsername, encryptedPassword,
            request.TtlSeconds);
        await _repo.AddDynamicCredentialAsync(cred, ct);

        var connStr = BuildConnectionString(
            request.DbHost, request.DbPort, request.DbName, username, password);

        _logger.LogInformation(
            "Dynamic credential created: {Username} for {DbName}, expires {ExpiresAt}",
            username, request.DbName, expiresAt);

        return new DynamicCredentialResult(
            cred.Id, cred.LeaseId, username, password, connStr, expiresAt);
    }

    public async Task RevokeCredentialAsync(Guid credentialId, CancellationToken ct = default)
    {
        var cred = await _repo.GetDynamicCredentialByIdAsync(credentialId, ct);
        if (cred is null || cred.Revoked) return;

        await DropRoleAsync(cred, ct);
        await _repo.RevokeDynamicCredentialAsync(credentialId, ct);

        _logger.LogInformation("Dynamic credential revoked: {Username}", cred.Username);
    }

    public async Task<int> RevokeExpiredCredentialsAsync(CancellationToken ct = default)
    {
        var allCreds = await _repo.GetDynamicCredentialsAsync(includeRevoked: false, ct);
        var expired = allCreds.Where(c => c.IsExpired && !c.Revoked).ToList();
        var revoked = 0;

        foreach (var cred in expired)
        {
            try
            {
                await DropRoleAsync(cred, ct);
                await _repo.RevokeDynamicCredentialAsync(cred.Id, ct);
                revoked++;
                _logger.LogInformation("Expired dynamic credential cleaned up: {Username}", cred.Username);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke expired credential {Username}", cred.Username);
            }
        }

        return revoked;
    }

    public async Task<string?> GetConnectionStringAsync(Guid credentialId, CancellationToken ct = default)
    {
        var cred = await _repo.GetDynamicCredentialByIdAsync(credentialId, ct);
        if (cred is null || cred.Revoked || cred.IsExpired) return null;

        // Decrypt admin password to verify (password for dynamic user is not stored)
        // Dynamic credentials rely on PostgreSQL VALID UNTIL for auth
        // Return connection string without password (client must use the password from creation)
        return null; // Password is only returned at creation time
    }

    private async Task DropRoleAsync(VaultDynamicCredential cred, CancellationToken ct)
    {
        try
        {
            var adminPassword = await DecryptAdminPasswordAsync(cred.AdminPasswordEncrypted, ct);
            var adminConnStr = BuildConnectionString(
                cred.DbHost, cred.DbPort, cred.DbName,
                cred.AdminUsername, adminPassword);

            await using var conn = new NpgsqlConnection(adminConnStr);
            await conn.OpenAsync(ct);

            var username = SanitizeIdentifier(cred.Username);

            // Revoke all privileges and drop role
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"""
                    REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM "{username}";
                    REVOKE CONNECT ON DATABASE "{SanitizeIdentifier(cred.DbName)}" FROM "{username}";
                    DROP ROLE IF EXISTS "{username}";
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to drop PostgreSQL role {Username}", cred.Username);
        }
    }

    private async Task<string> DecryptAdminPasswordAsync(string encryptedPassword, CancellationToken ct)
    {
        var parts = encryptedPassword.Split('|');
        if (parts.Length != 2)
            throw new InvalidOperationException("Invalid encrypted password format");

        var decrypted = await _kvSvc.DecryptAsync(parts[0], parts[1], KeyPurpose.Data, ct);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }

    private static string BuildConnectionString(string host, int port, string dbName, string username, string password)
    {
        return $"Host={host};Port={port};Database={dbName};Username={username};Password={password};";
    }

    private static string SanitizeIdentifier(string identifier)
    {
        // Only allow alphanumeric, underscore, and dot — prevents injection in SQL identifiers
        return new string(identifier.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.').ToArray());
    }
}
