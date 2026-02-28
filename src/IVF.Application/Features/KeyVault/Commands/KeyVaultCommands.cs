using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;
using FluentValidation;

namespace IVF.Application.Features.KeyVault.Commands;

// Initialize Vault
public record InitializeVaultCommand(string MasterPassword, Guid UserId) : IRequest<bool>;

public class InitializeVaultCommandHandler : IRequestHandler<InitializeVaultCommand, bool>
{
    private readonly IKeyVaultService _vault;
    private readonly IApiKeyManagementRepository _repo;
    private readonly IUnitOfWork _uow;

    public InitializeVaultCommandHandler(IKeyVaultService vault, IApiKeyManagementRepository repo, IUnitOfWork uow)
    {
        _vault = vault;
        _repo = repo;
        _uow = uow;
    }

    public async Task<bool> Handle(InitializeVaultCommand request, CancellationToken cancellationToken)
    {
        foreach (var purpose in Enum.GetValues<KeyPurpose>())
        {
            var keyName = $"dek-{purpose.ToString().ToLowerInvariant()}";
            var serviceName = "vault";

            if (await _repo.ExistsAsync(serviceName, keyName, cancellationToken))
                continue;

            var dek = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            await _vault.SetSecretAsync(keyName, dek, cancellationToken);

            var keyHash = BCrypt.Net.BCrypt.HashPassword(dek);
            var key = ApiKeyManagement.Create(keyName, serviceName, null, keyHash, "production", request.UserId);
            await _repo.AddAsync(key, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public class InitializeVaultCommandValidator : AbstractValidator<InitializeVaultCommand>
{
    public InitializeVaultCommandValidator()
    {
        RuleFor(x => x.MasterPassword).NotEmpty().MinimumLength(12);
        RuleFor(x => x.UserId).NotEmpty();
    }
}

// Rotate Key
public record RotateKeyCommand(string ServiceName, string KeyName, string NewKeyHash, Guid RotatedBy) : IRequest<bool>;

public class RotateKeyCommandHandler : IRequestHandler<RotateKeyCommand, bool>
{
    private readonly IApiKeyManagementRepository _repo;
    private readonly IUnitOfWork _uow;

    public RotateKeyCommandHandler(IApiKeyManagementRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<bool> Handle(RotateKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await _repo.GetByKeyNameAsync(request.ServiceName, request.KeyName, cancellationToken);
        if (key == null) throw new KeyNotFoundException($"Key '{request.KeyName}' not found for service '{request.ServiceName}'");

        key.Rotate(request.NewKeyHash);
        await _repo.UpdateAsync(key, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public class RotateKeyCommandValidator : AbstractValidator<RotateKeyCommand>
{
    public RotateKeyCommandValidator()
    {
        RuleFor(x => x.ServiceName).NotEmpty();
        RuleFor(x => x.KeyName).NotEmpty();
        RuleFor(x => x.NewKeyHash).NotEmpty();
        RuleFor(x => x.RotatedBy).NotEmpty();
    }
}

// Create API Key
public record CreateApiKeyCommand(
    string KeyName,
    string ServiceName,
    string KeyPrefix,
    string KeyHash,
    string Environment,
    Guid CreatedBy,
    int? RotationIntervalDays) : IRequest<Guid>;

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, Guid>
{
    private readonly IApiKeyManagementRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateApiKeyCommandHandler(IApiKeyManagementRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Guid> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        if (await _repo.ExistsAsync(request.ServiceName, request.KeyName, cancellationToken))
            throw new ValidationException($"API key '{request.KeyName}' already exists for service '{request.ServiceName}'");

        var key = ApiKeyManagement.Create(
            request.KeyName,
            request.ServiceName,
            request.KeyPrefix,
            request.KeyHash,
            request.Environment,
            request.CreatedBy,
            request.RotationIntervalDays);

        await _repo.AddAsync(key, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return key.Id;
    }
}

public class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.KeyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ServiceName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.KeyPrefix).NotEmpty().MaximumLength(50);
        RuleFor(x => x.KeyHash).NotEmpty();
        RuleFor(x => x.Environment).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CreatedBy).NotEmpty();
    }
}

// Deactivate API Key
public record DeactivateApiKeyCommand(Guid KeyId) : IRequest<bool>;

public class DeactivateApiKeyCommandHandler : IRequestHandler<DeactivateApiKeyCommand, bool>
{
    private readonly IApiKeyManagementRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeactivateApiKeyCommandHandler(IApiKeyManagementRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<bool> Handle(DeactivateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await _repo.GetByIdAsync(request.KeyId, cancellationToken);
        if (key == null) throw new KeyNotFoundException($"API key {request.KeyId} not found");

        key.Deactivate();
        await _repo.UpdateAsync(key, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return true;
    }
}

// ─── Key Wrap / Unwrap Commands ─────────────────────────────

// Wrap key (envelope encryption)
public record WrapKeyCommand(string PlaintextBase64, string KeyName) : IRequest<WrappedKeyResult>;

public class WrapKeyCommandHandler(IKeyVaultService vault) : IRequestHandler<WrapKeyCommand, WrappedKeyResult>
{
    public async Task<WrappedKeyResult> Handle(WrapKeyCommand request, CancellationToken ct)
    {
        var plaintext = Convert.FromBase64String(request.PlaintextBase64);
        return await vault.WrapKeyAsync(plaintext, request.KeyName, ct);
    }
}

public class WrapKeyCommandValidator : AbstractValidator<WrapKeyCommand>
{
    public WrapKeyCommandValidator()
    {
        RuleFor(x => x.PlaintextBase64).NotEmpty().Must(IsValidBase64).WithMessage("Must be valid Base64");
        RuleFor(x => x.KeyName).NotEmpty().MaximumLength(200);
    }

    private static bool IsValidBase64(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        try { Convert.FromBase64String(value); return true; } catch { return false; }
    }
}

// Unwrap key
public record UnwrapKeyCommand(string WrappedKeyBase64, string IvBase64, string KeyName) : IRequest<UnwrapKeyResult>;
public record UnwrapKeyResult(string PlaintextBase64);

public class UnwrapKeyCommandHandler(IKeyVaultService vault) : IRequestHandler<UnwrapKeyCommand, UnwrapKeyResult>
{
    public async Task<UnwrapKeyResult> Handle(UnwrapKeyCommand request, CancellationToken ct)
    {
        var plaintext = await vault.UnwrapKeyAsync(request.WrappedKeyBase64, request.IvBase64, request.KeyName, ct);
        return new UnwrapKeyResult(Convert.ToBase64String(plaintext));
    }
}

public class UnwrapKeyCommandValidator : AbstractValidator<UnwrapKeyCommand>
{
    public UnwrapKeyCommandValidator()
    {
        RuleFor(x => x.WrappedKeyBase64).NotEmpty();
        RuleFor(x => x.KeyName).NotEmpty().MaximumLength(200);
    }
}

// Encrypt data
public record EncryptDataCommand(string PlaintextBase64, KeyPurpose Purpose) : IRequest<EncryptedPayload>;

public class EncryptDataCommandHandler(IKeyVaultService vault) : IRequestHandler<EncryptDataCommand, EncryptedPayload>
{
    public async Task<EncryptedPayload> Handle(EncryptDataCommand request, CancellationToken ct)
    {
        var plaintext = Convert.FromBase64String(request.PlaintextBase64);
        return await vault.EncryptAsync(plaintext, request.Purpose, ct);
    }
}

public class EncryptDataCommandValidator : AbstractValidator<EncryptDataCommand>
{
    public EncryptDataCommandValidator()
    {
        RuleFor(x => x.PlaintextBase64).NotEmpty().Must(IsValidBase64).WithMessage("Must be valid Base64");
        RuleFor(x => x.Purpose).IsInEnum();
    }

    private static bool IsValidBase64(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        try { Convert.FromBase64String(value); return true; } catch { return false; }
    }
}

// Decrypt data
public record DecryptDataCommand(string CiphertextBase64, string IvBase64, KeyPurpose Purpose) : IRequest<DecryptDataResult>;
public record DecryptDataResult(string PlaintextBase64);

public class DecryptDataCommandHandler(IKeyVaultService vault) : IRequestHandler<DecryptDataCommand, DecryptDataResult>
{
    public async Task<DecryptDataResult> Handle(DecryptDataCommand request, CancellationToken ct)
    {
        var plaintext = await vault.DecryptAsync(request.CiphertextBase64, request.IvBase64, request.Purpose, ct);
        return new DecryptDataResult(Convert.ToBase64String(plaintext));
    }
}

public class DecryptDataCommandValidator : AbstractValidator<DecryptDataCommand>
{
    public DecryptDataCommandValidator()
    {
        RuleFor(x => x.CiphertextBase64).NotEmpty();
        RuleFor(x => x.IvBase64).NotEmpty();
        RuleFor(x => x.Purpose).IsInEnum();
    }
}

// Configure auto-unseal
public record ConfigureAutoUnsealCommand(string MasterPassword, string AzureKeyName) : IRequest<bool>;

public class ConfigureAutoUnsealCommandHandler(IKeyVaultService vault)
    : IRequestHandler<ConfigureAutoUnsealCommand, bool>
{
    public Task<bool> Handle(ConfigureAutoUnsealCommand request, CancellationToken ct)
        => vault.ConfigureAutoUnsealAsync(request.MasterPassword, request.AzureKeyName, ct);
}

public class ConfigureAutoUnsealCommandValidator : AbstractValidator<ConfigureAutoUnsealCommand>
{
    public ConfigureAutoUnsealCommandValidator()
    {
        RuleFor(x => x.MasterPassword).NotEmpty().MinimumLength(12);
        RuleFor(x => x.AzureKeyName).NotEmpty().MaximumLength(200);
    }
}

// Auto-unseal vault
public record AutoUnsealCommand : IRequest<bool>;

public class AutoUnsealCommandHandler(IKeyVaultService vault) : IRequestHandler<AutoUnsealCommand, bool>
{
    public Task<bool> Handle(AutoUnsealCommand request, CancellationToken ct)
        => vault.AutoUnsealAsync(ct);
}
