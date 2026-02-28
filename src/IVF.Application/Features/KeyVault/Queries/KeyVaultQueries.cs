using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Features.KeyVault.Queries;

// Response DTOs
public record KeyInfo(string KeyName, string ServiceName, bool IsActive, int Version, DateTime? ExpiresAt, DateTime? LastRotatedAt);

public record ApiKeyResponse(
    Guid Id,
    string KeyName,
    string ServiceName,
    string? KeyPrefix,
    bool IsActive,
    string? Environment,
    int Version,
    DateTime? ExpiresAt,
    DateTime? LastRotatedAt,
    DateTime CreatedAt);

public record VaultStatusResponse(bool IsInitialized, int ActiveKeyCount, List<KeyInfo> Keys);

// Get Vault Status
public record GetVaultStatusQuery() : IRequest<VaultStatusResponse>;

public class GetVaultStatusQueryHandler : IRequestHandler<GetVaultStatusQuery, VaultStatusResponse>
{
    private readonly IApiKeyManagementRepository _repo;
    private readonly IKeyVaultService _vault;

    public GetVaultStatusQueryHandler(IApiKeyManagementRepository repo, IKeyVaultService vault)
    {
        _repo = repo;
        _vault = vault;
    }

    public async Task<VaultStatusResponse> Handle(GetVaultStatusQuery request, CancellationToken cancellationToken)
    {
        var isHealthy = await _vault.IsHealthyAsync(cancellationToken);
        var keys = await _repo.GetByServiceAsync("vault", cancellationToken);

        var keyInfos = keys.Select(k => new KeyInfo(
            k.KeyName, k.ServiceName, k.IsActive, k.Version, k.ExpiresAt, k.LastRotatedAt)).ToList();

        var activeCount = keys.Count(k => k.IsActive);

        return new VaultStatusResponse(isHealthy && keys.Count > 0, activeCount, keyInfos);
    }
}

// Get API Key
public record GetApiKeyQuery(string ServiceName, string KeyName) : IRequest<ApiKeyResponse?>;

public class GetApiKeyQueryHandler : IRequestHandler<GetApiKeyQuery, ApiKeyResponse?>
{
    private readonly IApiKeyManagementRepository _repo;

    public GetApiKeyQueryHandler(IApiKeyManagementRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApiKeyResponse?> Handle(GetApiKeyQuery request, CancellationToken cancellationToken)
    {
        var key = await _repo.GetByKeyNameAsync(request.ServiceName, request.KeyName, cancellationToken);
        if (key == null) return null;

        return new ApiKeyResponse(
            key.Id, key.KeyName, key.ServiceName, key.KeyPrefix, key.IsActive,
            key.Environment, key.Version, key.ExpiresAt, key.LastRotatedAt, key.CreatedAt);
    }
}

// Get Expiring Keys
public record GetExpiringKeysQuery(int WithinDays = 30) : IRequest<List<ApiKeyResponse>>;

public class GetExpiringKeysQueryHandler : IRequestHandler<GetExpiringKeysQuery, List<ApiKeyResponse>>
{
    private readonly IApiKeyManagementRepository _repo;

    public GetExpiringKeysQueryHandler(IApiKeyManagementRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<ApiKeyResponse>> Handle(GetExpiringKeysQuery request, CancellationToken cancellationToken)
    {
        var keys = await _repo.GetExpiringKeysAsync(request.WithinDays, cancellationToken);

        return keys.Select(k => new ApiKeyResponse(
            k.Id, k.KeyName, k.ServiceName, k.KeyPrefix, k.IsActive,
            k.Environment, k.Version, k.ExpiresAt, k.LastRotatedAt, k.CreatedAt)).ToList();
    }
}

// Get Auto-Unseal Status
public record GetAutoUnsealStatusQuery : IRequest<AutoUnsealStatus>;

public class GetAutoUnsealStatusQueryHandler(IKeyVaultService vault)
    : IRequestHandler<GetAutoUnsealStatusQuery, AutoUnsealStatus>
{
    public Task<AutoUnsealStatus> Handle(GetAutoUnsealStatusQuery request, CancellationToken ct)
        => vault.GetAutoUnsealStatusAsync(ct);
}
