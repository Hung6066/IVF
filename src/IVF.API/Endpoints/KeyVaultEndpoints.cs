using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.KeyVault.Commands;
using IVF.Application.Features.KeyVault.Queries;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;

namespace IVF.API.Endpoints;

public static class KeyVaultEndpoints
{
    public static void MapKeyVaultEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/keyvault").WithTags("KeyVault").RequireAuthorization("AdminOnly");

        // ─── Vault Status & Init (existing MediatR) ─────────
        group.MapGet("/status", async (IMediator m) =>
            Results.Ok(await m.Send(new GetVaultStatusQuery())));

        group.MapPost("/initialize", async (InitializeVaultCommand cmd, IMediator m) =>
            Results.Ok(await m.Send(cmd)));

        // ─── API Keys (existing MediatR) ─────────────────────
        group.MapPost("/keys", async (CreateApiKeyCommand cmd, IMediator m) =>
            Results.Ok(await m.Send(cmd)));

        group.MapGet("/keys/{serviceName}/{keyName}", async (string serviceName, string keyName, IMediator m) =>
        {
            var result = await m.Send(new GetApiKeyQuery(serviceName, keyName));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/keys/rotate", async (RotateKeyCommand cmd, IMediator m) =>
            Results.Ok(await m.Send(cmd)));

        group.MapDelete("/keys/{id:guid}", async (Guid id, IMediator m) =>
            Results.Ok(await m.Send(new DeactivateApiKeyCommand(id))));

        group.MapGet("/keys/expiring", async (IMediator m, int withinDays = 30) =>
            Results.Ok(await m.Send(new GetExpiringKeysQuery(withinDays))));

        group.MapGet("/health", async (IKeyVaultService svc) =>
            Results.Ok(new { healthy = await svc.IsHealthyAsync() }));

        // ═══════════════════════════════════════════════════════
        // ─── Secrets (DB-backed with AES-256-GCM encryption) ─
        // ═══════════════════════════════════════════════════════

        group.MapGet("/secrets", async (IVaultSecretService svc, string? prefix) =>
        {
            var entries = await svc.ListSecretsAsync(prefix);
            return Results.Ok(entries);
        });

        group.MapGet("/secrets/{*path}", async (string path, IVaultSecretService svc, int? version) =>
        {
            var result = await svc.GetSecretAsync(path, version);
            return result is null
                ? Results.NotFound(new { error = $"Secret '{path}' not found" })
                : Results.Ok(new { result.Id, name = result.Path, value = result.Value, result.Version, retrievedAt = DateTime.UtcNow });
        });

        group.MapPost("/secrets", async (SecretCreateRequest req, IVaultSecretService svc, IVaultRepository repo, HttpContext ctx) =>
        {
            var result = await svc.PutSecretAsync(req.Name, req.Value);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "secret.create", "Secret", req.Name,
                details: $"{{\"version\":{result.Version}}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, name = req.Name, result.Version });
        });

        group.MapDelete("/secrets/{*path}", async (string path, IVaultSecretService svc, IVaultRepository repo, HttpContext ctx) =>
        {
            await svc.DeleteSecretAsync(path);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "secret.delete", "Secret", path,
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, name = path });
        });

        group.MapGet("/secrets-versions/{*path}", async (string path, IVaultSecretService svc) =>
        {
            var versions = await svc.GetVersionsAsync(path);
            return Results.Ok(versions);
        });

        group.MapPost("/secrets/import", async (SecretImportRequest req, IVaultSecretService svc, IVaultRepository repo, HttpContext ctx) =>
        {
            var result = await svc.ImportSecretsAsync(req.Secrets, req.Prefix);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "secret.import", "Secret", req.Prefix,
                details: $"{{\"imported\":{result.Imported},\"failed\":{result.Failed}}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(result);
        });

        // ═══════════════════════════════════════════════════════
        // ─── Key Wrap / Unwrap — Azure KV only feature ───────
        // ═══════════════════════════════════════════════════════

        group.MapPost("/wrap", async (WrapKeyCommand cmd, IMediator m) =>
        {
            try { return Results.Ok(await m.Send(cmd)); }
            catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        });

        group.MapPost("/unwrap", async (UnwrapKeyCommand cmd, IMediator m) =>
        {
            try { return Results.Ok(await m.Send(cmd)); }
            catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        });

        group.MapPost("/encrypt", async (EncryptDataCommand cmd, IMediator m) =>
        {
            try { return Results.Ok(await m.Send(cmd)); }
            catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        });

        group.MapPost("/decrypt", async (DecryptDataCommand cmd, IMediator m) =>
        {
            try { return Results.Ok(await m.Send(cmd)); }
            catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        });

        // ─── Auto-Unseal (Azure KV wrap/unwrap) ─────────────
        group.MapGet("/auto-unseal/status", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAutoUnsealStatusQuery())));

        group.MapPost("/auto-unseal/configure", async (ConfigureAutoUnsealCommand cmd, IMediator m) =>
        {
            try { return Results.Ok(new { success = await m.Send(cmd) }); }
            catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        });

        group.MapPost("/auto-unseal/unseal", async (IMediator m) =>
        {
            try { return Results.Ok(new { success = await m.Send(new AutoUnsealCommand()) }); }
            catch (Exception ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        });

        // ═══════════════════════════════════════════════════════
        // ─── Policies (DB-backed) ────────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/policies", async (IVaultRepository repo) =>
        {
            var policies = await repo.GetPoliciesAsync();
            return Results.Ok(policies.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.PathPattern,
                p.Capabilities,
                p.CreatedAt
            }));
        });

        group.MapPost("/policies", async (PolicyCreateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var existing = await repo.GetPolicyByNameAsync(req.Name);
            if (existing is not null)
                return Results.Conflict(new { error = $"Policy '{req.Name}' already exists" });

            var policy = VaultPolicy.Create(req.Name, req.PathPattern, req.Capabilities, req.Description);
            await repo.AddPolicyAsync(policy);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "policy.create", "Policy", policy.Id.ToString(),
                details: $"{{\"name\":\"{req.Name}\"}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, policy.Id, policy.Name });
        });

        group.MapPut("/policies/{id:guid}", async (Guid id, PolicyUpdateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var policy = await repo.GetPolicyByIdAsync(id);
            if (policy is null) return Results.NotFound();
            policy.Update(req.PathPattern, req.Capabilities, req.Description);
            await repo.UpdatePolicyAsync(policy);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "policy.update", "Policy", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        group.MapDelete("/policies/{id:guid}", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            await repo.DeletePolicyAsync(id);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "policy.delete", "Policy", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        // ═══════════════════════════════════════════════════════
        // ─── User Policies (DB-backed) ───────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/user-policies", async (IVaultRepository repo) =>
        {
            var ups = await repo.GetUserPoliciesAsync();
            return Results.Ok(ups.Select(up => new
            {
                up.Id,
                up.UserId,
                up.PolicyId,
                userName = up.User?.FullName,
                userEmail = up.User?.Username,
                policyName = up.Policy?.Name,
                up.GrantedAt
            }));
        });

        group.MapPost("/user-policies", async (UserPolicyAssignRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var up = VaultUserPolicy.Create(req.UserId, req.PolicyId, req.GrantedBy);
            await repo.AddUserPolicyAsync(up);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "user-policy.assign", "UserPolicy", up.Id.ToString(),
                details: $"{{\"userId\":\"{req.UserId}\",\"policyId\":\"{req.PolicyId}\"}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, up.Id });
        });

        group.MapDelete("/user-policies/{id:guid}", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            await repo.RemoveUserPolicyAsync(id);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "user-policy.remove", "UserPolicy", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        // ═══════════════════════════════════════════════════════
        // ─── Leases (DB-backed) ──────────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/leases", async (IVaultRepository repo, bool? includeExpired) =>
        {
            var leases = await repo.GetLeasesAsync(includeExpired ?? false);
            return Results.Ok(leases.Select(l => new
            {
                l.Id,
                l.LeaseId,
                l.SecretId,
                secretPath = l.Secret?.Path,
                l.Ttl,
                l.Renewable,
                l.ExpiresAt,
                l.Revoked,
                l.CreatedAt
            }));
        });

        group.MapPost("/leases", async (LeaseCreateRequest req, IVaultRepository repo, IVaultSecretService svc, HttpContext ctx) =>
        {
            var secret = await svc.GetSecretAsync(req.SecretPath);
            if (secret is null)
                return Results.NotFound(new { error = $"Secret '{req.SecretPath}' not found" });
            var lease = VaultLease.Create(secret.Id, req.TtlSeconds, req.Renewable);
            await repo.AddLeaseAsync(lease);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "lease.create", "Lease", lease.LeaseId,
                details: $"{{\"secretPath\":\"{req.SecretPath}\",\"ttl\":{req.TtlSeconds}}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, lease.Id, lease.LeaseId, lease.ExpiresAt });
        });

        group.MapPost("/leases/{leaseId}/revoke", async (string leaseId, IVaultRepository repo, HttpContext ctx) =>
        {
            await repo.RevokeLeaseAsync(leaseId);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "lease.revoke", "Lease", leaseId,
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        group.MapPost("/leases/{leaseId}/renew", async (string leaseId, LeaseRenewRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var lease = await repo.GetLeaseByIdAsync(leaseId);
            if (lease is null) return Results.NotFound();
            if (!lease.Renewable) return Results.BadRequest(new { error = "Lease is not renewable" });
            lease.Renew(req.IncrementSeconds);
            await repo.UpdateLeaseAsync(lease);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "lease.renew", "Lease", leaseId,
                details: $"{{\"newExpiry\":\"{lease.ExpiresAt:O}\"}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, lease.ExpiresAt });
        });

        // ═══════════════════════════════════════════════════════
        // ─── Dynamic Credentials (DB-backed) ─────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/dynamic", async (IVaultRepository repo) =>
        {
            var creds = await repo.GetDynamicCredentialsAsync();
            return Results.Ok(creds.Select(c => new
            {
                c.Id,
                c.LeaseId,
                c.Backend,
                c.Username,
                c.DbHost,
                c.DbPort,
                c.DbName,
                c.ExpiresAt,
                c.Revoked,
                c.CreatedAt,
                isExpired = c.IsExpired,
                hasAdminPassword = !string.IsNullOrEmpty(c.AdminPasswordEncrypted)
            }));
        });

        group.MapPost("/dynamic", async (DynamicCredentialCreateRequest req, IVaultRepository repo, IKeyVaultService kvSvc, HttpContext ctx) =>
        {
            // Encrypt admin password via Key Vault before storing
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(req.AdminPassword);
            var encrypted = await kvSvc.EncryptAsync(passwordBytes, IVF.Domain.Enums.KeyPurpose.Data);
            var encryptedPassword = $"{encrypted.CiphertextBase64}|{encrypted.IvBase64}";

            var cred = VaultDynamicCredential.Create(
                req.Backend, req.Username, req.DbHost, req.DbPort,
                req.DbName, req.AdminUsername, encryptedPassword,
                req.TtlSeconds);
            await repo.AddDynamicCredentialAsync(cred);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "dynamic.create", "DynamicCredential", cred.Id.ToString(),
                details: $"{{\"backend\":\"{req.Backend}\",\"dbName\":\"{req.DbName}\"}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, cred.Id, cred.LeaseId });
        });

        group.MapDelete("/dynamic/{id:guid}", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            await repo.RevokeDynamicCredentialAsync(id);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "dynamic.revoke", "DynamicCredential", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        // ═══════════════════════════════════════════════════════
        // ─── Tokens (DB-backed) ──────────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/tokens", async (IVaultRepository repo) =>
        {
            var tokens = await repo.GetTokensAsync();
            return Results.Ok(tokens.Select(t => new
            {
                t.Id,
                t.Accessor,
                t.DisplayName,
                t.Policies,
                t.TokenType,
                t.Ttl,
                t.NumUses,
                t.UsesCount,
                t.ExpiresAt,
                t.Revoked,
                t.CreatedAt,
                t.LastUsedAt,
                isValid = t.IsValid
            }));
        });

        group.MapPost("/tokens", async (TokenCreateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            // Generate a random token, store its SHA-256 hash
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            var token = VaultToken.Create(
                tokenHash, req.DisplayName, req.Policies,
                req.TokenType ?? "service", req.Ttl, req.NumUses);
            await repo.AddTokenAsync(token);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "token.create", "Token", token.Id.ToString(),
                details: $"{{\"displayName\":\"{req.DisplayName}\"}}",
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));

            return Results.Ok(new
            {
                success = true,
                token.Id,
                token.Accessor,
                token = rawToken, // Only returned once!
                token.ExpiresAt
            });
        });

        group.MapDelete("/tokens/{id:guid}", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            await repo.RevokeTokenAsync(id);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "token.revoke", "Token", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        // ═══════════════════════════════════════════════════════
        // ─── Settings (DB-backed) ────────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/settings", async (IVaultRepository repo, IConfiguration config) =>
        {
            // Merge config-file Azure KV settings with DB overrides
            var dbSettings = await repo.GetAllSettingsAsync();
            var dbDict = dbSettings.ToDictionary(s => s.Key, s => s.ValueJson);

            // Helper to parse JSON-encoded string value from DB
            string dbValue(string key, string fallback)
            {
                if (!dbDict.TryGetValue(key, out var json)) return fallback;
                try { return JsonSerializer.Deserialize<string>(json) ?? fallback; }
                catch { return json; }
            }

            // DB overrides take priority over appsettings.json
            var azureConfig = new
            {
                vaultUrl = dbValue("vaultUrl", config["AzureKeyVault:VaultUrl"] ?? ""),
                keyName = dbValue("keyName", config["AzureKeyVault:KeyName"] ?? ""),
                tenantId = dbValue("tenantId", config["AzureKeyVault:TenantId"] ?? ""),
                clientId = dbValue("clientId", config["AzureKeyVault:ClientId"] ?? ""),
                hasClientSecret = dbDict.ContainsKey("clientSecret") || !string.IsNullOrEmpty(config["AzureKeyVault:ClientSecret"]),
                enabled = config.GetValue<bool>("AzureKeyVault:Enabled"),
                fallbackToLocal = config.GetValue<bool>("AzureKeyVault:FallbackToLocal"),
                useManagedIdentity = config.GetValue<bool>("AzureKeyVault:UseManagedIdentity")
            };

            // Return vault settings excluding Azure keys that are already merged above
            var azureKeys = new HashSet<string> { "vaultUrl", "keyName", "tenantId", "clientId", "clientSecret" };
            var vaultSettings = dbDict
                .Where(kv => !azureKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return Results.Ok(new
            {
                azure = azureConfig,
                vault = vaultSettings
            });
        });

        group.MapPost("/settings", async (JsonElement body, IVaultRepository repo, IKeyVaultService kvSvc, HttpContext ctx) =>
        {
            try
            {
                foreach (var prop in body.EnumerateObject())
                {
                    string value;
                    if (prop.Name == "clientSecret")
                    {
                        // Encrypt clientSecret via Key Vault before storing
                        var secretValue = prop.Value.GetString() ?? "";
                        var secretBytes = System.Text.Encoding.UTF8.GetBytes(secretValue);
                        var encrypted = await kvSvc.EncryptAsync(secretBytes, IVF.Domain.Enums.KeyPurpose.Data);
                        value = JsonSerializer.Serialize($"ENC:{encrypted.CiphertextBase64}|{encrypted.IvBase64}");
                    }
                    else
                    {
                        // Store as valid JSON (jsonb column)
                        value = prop.Value.GetRawText();
                    }
                    await repo.SaveSettingAsync(prop.Name, value);
                }
                await repo.AddAuditLogAsync(VaultAuditLog.Create(
                    "settings.update", "Settings", null,
                    ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
                return Results.Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 400);
            }
        });

        group.MapPost("/test-connection", async (IKeyVaultService svc) =>
        {
            try
            {
                var healthy = await svc.IsHealthyAsync();
                return Results.Ok(new
                {
                    connected = healthy,
                    message = healthy
                        ? "Kết nối Azure Key Vault thành công!"
                        : "Không thể kết nối đến Azure Key Vault"
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { connected = false, message = $"Lỗi: {ex.Message}" });
            }
        });

        // ═══════════════════════════════════════════════════════
        // ─── Audit Logs (DB-backed) ──────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/audit-logs", async (IVaultRepository repo, int? page, int? pageSize, string? action) =>
        {
            var p = page ?? 1;
            var ps = Math.Min(pageSize ?? 50, 200);
            var logs = await repo.GetAuditLogsAsync(p, ps, action);
            var total = await repo.GetAuditLogCountAsync(action);
            return Results.Ok(new
            {
                items = logs.Select(l => new
                {
                    l.Id,
                    l.Action,
                    l.ResourceType,
                    l.ResourceId,
                    l.UserId,
                    l.Details,
                    l.IpAddress,
                    l.CreatedAt
                }),
                totalCount = total,
                page = p,
                pageSize = ps
            });
        });

        // ═══════════════════════════════════════════════════════
        // ─── DB Schema Introspection ─────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/db-schema", async (IvfDbContext db) =>
        {
            var tables = await db.Database.SqlQueryRaw<DbTableColumn>(
                """
                SELECT t.table_name AS "TableName", c.column_name AS "ColumnName", c.data_type AS "DataType"
                FROM information_schema.tables t
                JOIN information_schema.columns c ON c.table_schema = t.table_schema AND c.table_name = t.table_name
                WHERE t.table_schema = 'public' AND t.table_type = 'BASE TABLE'
                ORDER BY t.table_name, c.ordinal_position
                """).ToListAsync();

            var grouped = tables
                .GroupBy(t => t.TableName)
                .Select(g => new
                {
                    tableName = g.Key,
                    columns = g.Select(c => new { name = c.ColumnName, dataType = c.DataType }).ToList()
                })
                .OrderBy(g => g.tableName)
                .ToList();

            return Results.Ok(grouped);
        });

        // ═══════════════════════════════════════════════════════
        // ─── Encryption Configs ──────────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/encryption-configs", async (IVaultRepository repo) =>
        {
            var configs = await repo.GetAllEncryptionConfigsAsync();
            return Results.Ok(configs.Select(c => new
            {
                c.Id,
                c.TableName,
                c.EncryptedFields,
                c.DekPurpose,
                c.IsEnabled,
                c.IsDefault,
                c.Description,
                c.CreatedAt,
                c.UpdatedAt
            }));
        });

        group.MapPost("/encryption-configs", async (EncryptionConfigCreateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var config = EncryptionConfig.Create(
                req.TableName, req.EncryptedFields, req.DekPurpose ?? "data",
                req.Description, req.IsDefault);
            await repo.AddEncryptionConfigAsync(config);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "encryption_config.create", "EncryptionConfig", config.Id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, id = config.Id });
        });

        group.MapPut("/encryption-configs/{id:guid}", async (Guid id, EncryptionConfigUpdateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var config = await repo.GetEncryptionConfigByIdAsync(id);
            if (config is null) return Results.NotFound();
            config.Update(req.EncryptedFields, req.DekPurpose ?? config.DekPurpose, req.Description);
            await repo.UpdateEncryptionConfigAsync(config);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "encryption_config.update", "EncryptionConfig", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        group.MapPut("/encryption-configs/{id:guid}/toggle", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            var config = await repo.GetEncryptionConfigByIdAsync(id);
            if (config is null) return Results.NotFound();
            config.SetEnabled(!config.IsEnabled);
            await repo.UpdateEncryptionConfigAsync(config);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                config.IsEnabled ? "encryption_config.enable" : "encryption_config.disable",
                "EncryptionConfig", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, isEnabled = config.IsEnabled });
        });

        group.MapDelete("/encryption-configs/{id:guid}", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            var config = await repo.GetEncryptionConfigByIdAsync(id);
            if (config is null) return Results.NotFound();
            if (config.IsDefault) return Results.Json(new { error = "Không thể xóa cấu hình mặc định" }, statusCode: 400);
            await repo.DeleteEncryptionConfigAsync(id);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "encryption_config.delete", "EncryptionConfig", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        // ═══════════════════════════════════════════════════════
        // ─── Field Access Policies ───────────────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/field-access-policies", async (IVaultRepository repo) =>
        {
            var policies = await repo.GetAllFieldAccessPoliciesAsync();
            return Results.Ok(policies.Select(p => new
            {
                p.Id,
                p.TableName,
                p.FieldName,
                p.Role,
                p.AccessLevel,
                p.MaskPattern,
                p.PartialLength,
                p.Description,
                p.CreatedAt,
                p.UpdatedAt
            }));
        });

        group.MapPost("/field-access-policies", async (FieldAccessPolicyCreateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var policy = FieldAccessPolicy.Create(
                req.TableName, req.FieldName, req.Role, req.AccessLevel,
                req.MaskPattern, req.PartialLength, req.Description);
            await repo.AddFieldAccessPolicyAsync(policy);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "field_access.create", "FieldAccessPolicy", policy.Id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true, id = policy.Id });
        });

        group.MapPut("/field-access-policies/{id:guid}", async (Guid id, FieldAccessPolicyUpdateRequest req, IVaultRepository repo, HttpContext ctx) =>
        {
            var policy = await repo.GetFieldAccessPolicyByIdAsync(id);
            if (policy is null) return Results.NotFound();
            policy.Update(req.AccessLevel, req.MaskPattern, req.PartialLength, req.Description);
            await repo.UpdateFieldAccessPolicyAsync(policy);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "field_access.update", "FieldAccessPolicy", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        group.MapDelete("/field-access-policies/{id:guid}", async (Guid id, IVaultRepository repo, HttpContext ctx) =>
        {
            await repo.DeleteFieldAccessPolicyAsync(id);
            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "field_access.delete", "FieldAccessPolicy", id.ToString(),
                ipAddress: ctx.Connection.RemoteIpAddress?.ToString()));
            return Results.Ok(new { success = true });
        });

        // ═══════════════════════════════════════════════════════
        // ─── Security Dashboard (Zero Trust) ─────────────────
        // ═══════════════════════════════════════════════════════

        group.MapGet("/security-dashboard", async (IVaultRepository repo) =>
        {
            var auditLogs = await repo.GetAuditLogsAsync(1, 200);
            var encConfigs = await repo.GetAllEncryptionConfigsAsync();

            var recentEvents = auditLogs
                .Where(l => l.Action.StartsWith("zt_") || l.Action.StartsWith("encryption_") || l.Action.StartsWith("field_access"))
                .Take(10)
                .Select(l => new { l.Id, l.Action, l.ResourceType, l.IpAddress, l.CreatedAt })
                .ToList();

            var blockedAttempts = auditLogs.Count(l => l.Action == "zt_decision" && (l.Details?.Contains("DENIED") == true));

            return Results.Ok(new
            {
                securityScore = Math.Min(100, 60 + encConfigs.Count(c => c.IsEnabled) * 5),
                vaultStatus = "Sealed",
                trustedDevices = 3,
                recentAlerts = 0,
                blockedAttempts,
                encryptionConfigCount = encConfigs.Count,
                recentEvents
            });
        });
    }
}

// ─── Request Records ──────────────────────────────────

public record SecretCreateRequest(string Name, string Value);
public record SecretImportRequest(Dictionary<string, string> Secrets, string? Prefix);
public record PolicyCreateRequest(string Name, string PathPattern, string[] Capabilities, string? Description);
public record PolicyUpdateRequest(string PathPattern, string[] Capabilities, string? Description);
public record UserPolicyAssignRequest(Guid UserId, Guid PolicyId, Guid? GrantedBy);
public record LeaseCreateRequest(string SecretPath, int TtlSeconds, bool Renewable = true);
public record LeaseRenewRequest(int IncrementSeconds);
public record DynamicCredentialCreateRequest(string Backend, string Username, string DbHost, int DbPort, string DbName, string AdminUsername, string AdminPassword, int TtlSeconds);
public record TokenCreateRequest(string? DisplayName, string[]? Policies, string? TokenType, int? Ttl, int? NumUses);
public record EncryptionConfigCreateRequest(string TableName, string[] EncryptedFields, string? DekPurpose, string? Description, bool IsDefault = false);
public record EncryptionConfigUpdateRequest(string[] EncryptedFields, string? DekPurpose, string? Description);
public record FieldAccessPolicyCreateRequest(string TableName, string FieldName, string Role, string AccessLevel, string? MaskPattern, int PartialLength = 5, string? Description = null);

public class DbTableColumn
{
    public string TableName { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
}
public record FieldAccessPolicyUpdateRequest(string AccessLevel, string? MaskPattern, int PartialLength = 5, string? Description = null);
