namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Service for managing vault secrets with encryption at rest.
/// KEK is wrapped/unwrapped via IKeyVaultService (Azure KV or local fallback).
/// </summary>
public interface IVaultSecretService
{
    /// <summary>Get decrypted secret value by path</summary>
    Task<VaultSecretResult?> GetSecretAsync(string path, int? version = null, CancellationToken ct = default);

    /// <summary>Create or update a secret (auto-increments version)</summary>
    Task<VaultSecretResult> PutSecretAsync(string path, string plaintext, Guid? userId = null, string? metadata = null, CancellationToken ct = default);

    /// <summary>Soft-delete a secret</summary>
    Task DeleteSecretAsync(string path, CancellationToken ct = default);

    /// <summary>List secret paths (virtual folder structure)</summary>
    Task<IEnumerable<VaultSecretEntry>> ListSecretsAsync(string? prefix = null, CancellationToken ct = default);

    /// <summary>Get all versions of a secret</summary>
    Task<IEnumerable<VaultSecretVersionInfo>> GetVersionsAsync(string path, CancellationToken ct = default);

    /// <summary>Bulk import secrets from a dictionary</summary>
    Task<VaultImportResult> ImportSecretsAsync(Dictionary<string, string> secrets, string? prefix = null, Guid? userId = null, CancellationToken ct = default);
}

// ─── Result Types ─────────────────────────────────

public record VaultSecretResult(
    Guid Id,
    string Path,
    int Version,
    string Value,
    string? Metadata,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record VaultSecretEntry(string Name, string Type); // type: "folder" | "secret"

public record VaultSecretVersionInfo(
    int Version,
    DateTime CreatedAt,
    DateTime? DeletedAt);

public record VaultImportResult(int Imported, int Failed, List<string> Errors);
