# Hướng dẫn tích hợp Vault cho các dịch vụ

> Tài liệu mô tả kiến trúc, cách sử dụng và tích hợp tự động hệ thống Vault vào bất kỳ dịch vụ nào trong hệ thống IVF — bao gồm mã hóa dữ liệu, quản lý secret, kiểm soát truy cập theo trường, và Zero Trust.

> **Xem thêm**: [vault_usage_guide.md](vault_usage_guide.md) — hướng dẫn sử dụng giao diện Vault Manager (15 tabs)

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Cấu hình ban đầu](#2-cấu-hình-ban-đầu)
3. [Tích hợp mã hóa dữ liệu tự động](#3-tích-hợp-mã-hóa-dữ-liệu-tự-động)
4. [Quản lý Secret (Secret Engine)](#4-quản-lý-secret-secret-engine)
5. [Kiểm soát truy cập theo trường (Field-Level Access)](#5-kiểm-soát-truy-cập-theo-trường-field-level-access)
6. [Zero Trust — bảo vệ tự động qua MediatR](#6-zero-trust--bảo-vệ-tự-động-qua-mediatr)
7. [Vault Token Authentication](#7-vault-token-authentication)
8. [API Key Authentication](#8-api-key-authentication)
9. [Vault Policy Authorization](#9-vault-policy-authorization)
10. [Dynamic Credentials (PostgreSQL)](#10-dynamic-credentials-postgresql)
11. [Lease Management](#11-lease-management)
12. [Vault Configuration Provider](#12-vault-configuration-provider)
13. [Background Maintenance Service](#13-background-maintenance-service)
14. [MediatR Pipeline tổng hợp](#14-mediatr-pipeline-tổng-hợp)
15. [Auto-Unseal với Azure Key Vault](#15-auto-unseal-với-azure-key-vault)
16. [API Reference](#16-api-reference)
17. [Tích hợp cho dịch vụ mới — từng bước](#17-tích-hợp-cho-dịch-vụ-mới--từng-bước)
18. [Ví dụ thực tế](#18-ví-dụ-thực-tế)
19. [Tích hợp Vault với CA/mTLS](#19-tích-hợp-vault-với-camtls-certificate-authority)
20. [Secret Rotation Engine](#20-secret-rotation-engine)
21. [DEK Rotation](#21-dek-rotation)
22. [DB Credential Rotation](#22-db-credential-rotation)
23. [Observability & Metrics](#23-observability--metrics)
24. [SIEM Integration](#24-siem-integration)
25. [KMS Provider Abstraction](#25-kms-provider-abstraction)
26. [Continuous Access Evaluation (CAE)](#26-continuous-access-evaluation-cae)
27. [Compliance Scoring Engine](#27-compliance-scoring-engine)
28. [Vault Disaster Recovery](#28-vault-disaster-recovery)
29. [Multi-Provider Unseal](#29-multi-provider-unseal)
30. [Test Coverage](#30-test-coverage)

---

## 1. Tổng quan kiến trúc

### Sơ đồ hệ thống

```
┌──────────────────────────────────────────────────────────┐
│                      Angular Frontend                     │
│                Vault Manager (15 tabs)                     │
└─────────────┬────────────────────────────────────────────┘
              │ HTTP (JWT Bearer | X-Vault-Token | X-API-Key)
              ▼
┌──────────────────────────────────────────────────────────┐
│                   ASP.NET Minimal API                      │
│                                                            │
│  ┌─────────────────────────────────────────────────┐      │
│  │ Middleware Pipeline                              │      │
│  │  VaultTokenMiddleware → ApiKeyMiddleware →        │      │
│  │  Authentication → Routing                        │      │
│  └─────────────────────────────────────────────────┘      │
│                                                            │
│  /api/keyvault/* (AdminOnly + VaultPolicy endpoints)      │
│  /api/keyvault/me/* (Authenticated users)                 │
│                                                            │
│  ┌─────────────────────────────────────────────────┐      │
│  │           MediatR Pipeline (4 behaviors)         │      │
│  │  Validation → VaultPolicy → ZeroTrust →          │      │
│  │  FieldAccess → Handler                           │      │
│  └─────────────────────────────────────────────────┘      │
│                                                            │
│  ┌─────────────────────────────────────────────────┐      │
│  │         EF Core Interceptors                     │      │
│  │  AuditInterceptor + VaultEncryptionInterceptor   │      │
│  └─────────────────────────────────────────────────┘      │
│                                                            │
│  ┌─────────────────────────────────────────────────┐      │
│  │    Enterprise Services (Phase 2–7)               │      │
│  │  SecretRotation │ DEK Rotation │ DB Rotation     │      │
│  │  CAE │ Compliance │ VaultDR │ MultiUnseal        │      │
│  │  KmsProvider │ VaultMetrics │ SIEM Events        │      │
│  └─────────────────────────────────────────────────┘      │
└─────────────┬────────────────────────────────────────────┘
              │
    ┌─────────┼─────────┐
    ▼         ▼         ▼
┌────────┐ ┌──────────┐ ┌──────────────────┐
│ Vault  │ │ KMS      │ │ PostgreSQL       │
│ Secret │ │ Provider │ │ (Dynamic Roles)  │
│ Service│ │ Azure or │ │ CREATE ROLE ...  │
│ (AES-  │ │ Local    │ │ VALID UNTIL ...  │
│ 256-   │ │ (config- │ │ A/B Dual-Slot    │
│ GCM)   │ │ driven)  │ │ Rotation         │
└───┬────┘ └──────────┘ └──────────────────┘
    │
    ▼
┌──────────────────────────────────────────────────────────┐
│                    PostgreSQL (ivf_db)                     │
│  vault_secrets | vault_policies | vault_user_policies     │
│  vault_tokens | vault_leases | vault_dynamic_credentials  │
│  encryption_configs | field_access_policies               │
│  zt_policies | vault_audit_logs | vault_settings          │
│  secret_rotation_schedules                                │
└──────────────────────────────────────────────────────────┘
    ▲
    │ Auto-reload (5 min)
┌───┴──────────────────────────────────────────────────────┐
│  Background Services                                      │
│  • VaultLeaseMaintenanceService (5 min cycle)             │
│    - Revoke expired leases, credentials, tokens           │
│  • CertAutoRenewalService                                 │
│    - Escalating warnings (30/14/7/1 day)                  │
│    - Auto-renewal via EJBCA                                │
│  • SIEM SecurityEventPublisher                            │
│    - Syslog/CEF format events                              │
└──────────────────────────────────────────────────────────┘
```

### Các thành phần chính

#### Core Services

| Thành phần               | Interface             | Lifetime  | Mô tả                                                               |
| ------------------------ | --------------------- | --------- | ------------------------------------------------------------------- |
| **AzureKeyVaultService** | `IKeyVaultService`    | Singleton | Wrap/unwrap KEK qua Azure RSA-OAEP-256, fallback local AES-256-GCM  |
| **VaultSecretService**   | `IVaultSecretService` | Scoped    | CRUD secret với mã hóa AES-256-GCM, envelope encryption             |
| **ZeroTrustService**     | `IZeroTrustService`   | Scoped    | 6-point access evaluation, cache 15 phút                            |
| **VaultRepository**      | `IVaultRepository`    | Scoped    | Data access cho toàn bộ vault entities (50+ methods)                |
| **CurrentUserService**   | `ICurrentUserService` | Scoped    | Trích xuất user context từ HttpContext (UserId, Username, Role, IP) |

#### Authentication & Authorization

| Thành phần                          | Interface               | Lifetime | Mô tả                                                                    |
| ----------------------------------- | ----------------------- | -------- | ------------------------------------------------------------------------ |
| **VaultTokenValidator**             | `IVaultTokenValidator`  | Scoped   | SHA-256 hash lookup, validate token + kiểm tra capabilities              |
| **VaultTokenMiddleware**            | ASP.NET Middleware      | —        | Xác thực X-Vault-Token header → ClaimsPrincipal                          |
| **ApiKeyValidator**                 | `IApiKeyValidator`      | Scoped   | BCrypt hash verify (DB) + fallback config keys, audit logging            |
| **ApiKeyMiddleware**                | ASP.NET Middleware      | —        | Xác thực X-API-Key header / apiKey query → ClaimsPrincipal               |
| **VaultPolicyEvaluator**            | `IVaultPolicyEvaluator` | Scoped   | Đánh giá policy cho cả JWT users và vault tokens, path matching `*`/`**` |
| **VaultPolicyAuthorizationHandler** | `IAuthorizationHandler` | Scoped   | ASP.NET Core authorization handler cho vault policies trên endpoints     |

#### MediatR Pipeline Behaviors

| Thành phần              | Interface           | Lifetime  | Mô tả                                                         |
| ----------------------- | ------------------- | --------- | ------------------------------------------------------------- |
| **ValidationBehavior**  | `IPipelineBehavior` | Transient | FluentValidation — chạy đầu tiên                              |
| **VaultPolicyBehavior** | `IPipelineBehavior` | Transient | Kiểm tra vault policy qua `IVaultPolicyProtected` marker      |
| **ZeroTrustBehavior**   | `IPipelineBehavior` | Transient | 6-point access evaluation qua `IZeroTrustProtected` marker    |
| **FieldAccessBehavior** | `IPipelineBehavior` | Transient | Auto-mask fields theo role qua `IFieldAccessProtected` marker |

#### EF Core Interceptors

| Thành phần                     | Lifetime | Mô tả                                                 |
| ------------------------------ | -------- | ----------------------------------------------------- |
| **AuditInterceptor**           | Scoped   | Tự động ghi audit trail khi save (pre-existing)       |
| **VaultEncryptionInterceptor** | Scoped   | Tự động mã hóa trường nhạy cảm khi `SaveChangesAsync` |

#### Service Layer

| Thành phần                     | Interface                    | Lifetime | Mô tả                                                                  |
| ------------------------------ | ---------------------------- | -------- | ---------------------------------------------------------------------- |
| **FieldAccessService**         | `IFieldAccessService`        | Scoped   | Áp dụng masking theo FieldAccessPolicy + role, request-scoped cache    |
| **VaultDecryptionService**     | `IVaultDecryptionService`    | Scoped   | Giải mã field values đã được encrypt (`{"c":"...","iv":"..."}`)        |
| **DynamicCredentialProvider**  | `IDynamicCredentialProvider` | Scoped   | Tạo PostgreSQL roles tạm thời với `VALID UNTIL`, auto-revoke           |
| **LeaseManager**               | `ILeaseManager`              | Scoped   | Quản lý lifecycle lease cho secrets (tạo/renew/revoke)                 |
| **VaultConfigurationProvider** | `ConfigurationProvider`      | —        | Inject vault secrets vào `IConfiguration` pipeline, auto-reload 5 phút |

#### Enterprise Vault Services (Phase 2–7)

| Thành phần                      | Interface                      | Lifetime  | Mô tả                                                                    |
| ------------------------------- | ------------------------------ | --------- | ------------------------------------------------------------------------ |
| **SecretRotationService**       | `ISecretRotationService`       | Scoped    | Tự động xoay secret theo schedule (30/60/90 ngày), callback hooks        |
| **DekRotationService**          | `IDekRotationService`          | Scoped    | Xoay DEK theo phiên bản, lưu trữ dek-{purpose}-v{N}, re-encrypt batch    |
| **DbCredentialRotationService** | `IDbCredentialRotationService` | Scoped    | Google-style dual A/B slot rotation cho database credentials             |
| **VaultMetrics**                | —                              | Singleton | System.Diagnostics.Metrics cho vault operations (counters, histograms)   |
| **SecurityEventPublisher**      | `ISecurityEventPublisher`      | Singleton | SIEM integration — Syslog/CEF format, webhook cho security events        |
| **LocalKmsProvider**            | `IKmsProvider`                 | Scoped    | Provider-agnostic KMS — local AES-256-GCM, keys lưu trong vault settings |
| **AzureKmsProvider**            | `IKmsProvider`                 | Scoped    | Adapter bridge IKmsProvider → IKeyVaultService cho Azure KV              |
| **ContinuousAccessEvaluator**   | `IContinuousAccessEvaluator`   | Scoped    | Google CAE pattern — session re-evaluation, step-up auth, token binding  |
| **ComplianceScoringEngine**     | `IComplianceScoringEngine`     | Scoped    | Real-time HIPAA/SOC 2/GDPR compliance scoring từ vault state             |
| **VaultDrService**              | `IVaultDrService`              | Scoped    | Encrypted backup/restore vault state (AES-256-GCM + PBKDF2)              |
| **MultiProviderUnsealService**  | `IMultiProviderUnsealService`  | Scoped    | Priority-based failover unseal qua nhiều KMS provider                    |

#### Background Services

| Thành phần                       | Lifetime | Mô tả                                                                           |
| -------------------------------- | -------- | ------------------------------------------------------------------------------- |
| **VaultLeaseMaintenanceService** | Hosted   | Auto-revoke expired leases, credentials, tokens mỗi 5 phút                      |
| **CertAutoRenewalService**       | Hosted   | Kiểm tra certificate expiry, escalating warnings (30/14/7/1 ngày), auto-renewal |

### Mô hình mã hóa (Envelope Encryption)

```
DEK (Data Encryption Key)     ← random 256-bit, dùng cho AES-256-GCM
  └── được wrap bởi KEK
KEK (Key Encryption Key)      ← random 256-bit, cache trong memory
  └── được wrap bởi Azure KV RSA key (hoặc local AES-256-GCM fallback)
Master Key                     ← Azure RSA-OAEP-256 key (hoặc PBKDF2 từ JwtSettings:Secret)
```

---

## 2. Cấu hình ban đầu

### 2.1 appsettings.json

```json
{
  "AzureKeyVault": {
    "Enabled": true,
    "VaultUrl": "https://<your-vault>.vault.azure.net/",
    "KeyName": "unseal-key",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id>",
    "ClientSecret": "",
    "UseManagedIdentity": true,
    "FallbackToLocal": true
  }
}
```

| Thuộc tính           | Mô tả                                                                    |
| -------------------- | ------------------------------------------------------------------------ |
| `Enabled`            | Bật/tắt tích hợp Azure KV                                                |
| `VaultUrl`           | URL của Azure Key Vault                                                  |
| `KeyName`            | Tên RSA key trong Azure KV dùng để wrap/unwrap KEK                       |
| `UseManagedIdentity` | `true` = dùng Managed Identity (production), `false` = dùng ClientSecret |
| `FallbackToLocal`    | `true` = nếu Azure KV không khả dụng, dùng AES-256-GCM local (dev)       |

### 2.2 Đăng ký DI (Infrastructure/DependencyInjection.cs)

```csharp
// ─── Key Vault & Zero Trust (core) ───
builder.Services.AddSingleton<IKeyVaultService, AzureKeyVaultService>();
builder.Services.AddScoped<IVaultSecretService, VaultSecretService>();
builder.Services.AddScoped<IZeroTrustService, ZeroTrustService>();

// ─── EF Core Interceptors ───
services.AddScoped<AuditInterceptor>();
services.AddScoped<VaultEncryptionInterceptor>();
services.AddDbContext<IvfDbContext>((sp, options) =>
{
    options.UseNpgsql(...)
        .AddInterceptors(
            sp.GetRequiredService<AuditInterceptor>(),
            sp.GetRequiredService<VaultEncryptionInterceptor>());
});

// ─── Vault Integration Services ───
services.AddScoped<IVaultTokenValidator, VaultTokenValidator>();
services.AddScoped<IDynamicCredentialProvider, DynamicCredentialProvider>();
services.AddScoped<ILeaseManager, LeaseManager>();
services.AddScoped<IVaultPolicyEvaluator, VaultPolicyEvaluator>();
services.AddScoped<IVaultDecryptionService, VaultDecryptionService>();

// ─── Background Services ───
services.AddHostedService<VaultLeaseMaintenanceService>();
```

### 2.2.1 Đăng ký Application Layer (Application/DependencyInjection.cs)

```csharp
// MediatR Pipeline Behaviors (thứ tự thực thi)
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(VaultPolicyBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ZeroTrustBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FieldAccessBehavior<,>));
```

### 2.2.2 Đăng ký API Layer (Program.cs)

```csharp
// ─── Authorization ───
builder.Services.AddVaultPolicyAuthorization(); // VaultPolicyAuthorizationHandler
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ─── Middleware Pipeline (thứ tự quan trọng!) ───
app.UseVaultTokenAuth();    // X-Vault-Token → ClaimsPrincipal (TRƯỚC UseAuthentication)
app.UseAuthentication();
app.UseAuthorization();

// ─── Vault Configuration Provider (sau khi seed xong) ───
try
{
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
    var config = app.Services.GetRequiredService<IConfigurationRoot>();
    config.AddVaultSecrets(scopeFactory);
    config.Reload();
}
catch (Exception ex)
{
    Log.Warning(ex, "Vault Configuration Provider skipped");
}
```

### 2.3 Đăng ký Repository & Services (DependencyInjection.cs)

```csharp
// Repositories
services.AddScoped<IVaultRepository, VaultRepository>();
services.AddScoped<IApiKeyManagementRepository, ApiKeyManagementRepository>();

// API Key Validator (centralized — replaces inline config checks)
services.AddScoped<IApiKeyValidator, ApiKeyValidator>();

// Vault Token Validator
services.AddScoped<IVaultTokenValidator, VaultTokenValidator>();
```

### 2.4 Đăng ký Middleware (Program.cs)

```csharp
app.UseVaultTokenAuth();  // X-Vault-Token → ClaimsPrincipal
app.UseApiKeyAuth();      // X-API-Key / apiKey query → ClaimsPrincipal
app.UseAuthentication();  // JWT Bearer
app.UseAuthorization();
```

### 2.5 Đăng ký Endpoints

```csharp
app.MapKeyVaultEndpoints();   // /api/keyvault/* (AdminOnly) + /api/keyvault/me/* (Authenticated)
app.MapZeroTrustEndpoints();  // /api/zero-trust/*
```

---

## 3. Tích hợp mã hóa dữ liệu tự động

Hệ thống cho phép cấu hình mã hóa **theo bảng + theo trường** mà không cần sửa code — chỉ cần thêm bản ghi vào bảng `encryption_configs`.

### 3.1 Entity EncryptionConfig

```csharp
public class EncryptionConfig : BaseEntity
{
    public string TableName { get; private set; }        // "patients"
    public string[] EncryptedFields { get; private set; } // ["medical_history", "allergies"]
    public string DekPurpose { get; private set; }        // "data" | "session" | "api" | "backup"
    public bool IsEnabled { get; private set; }
    public bool IsDefault { get; private set; }
    public string? Description { get; private set; }
}
```

### 3.2 Seeder mặc định

Hệ thống tự seed 5 config khi khởi tạo:

| Bảng              | Trường mã hóa                                                      | DekPurpose |
| ----------------- | ------------------------------------------------------------------ | ---------- |
| `medical_records` | diagnosis, symptoms, treatment_plan, notes, medications, allergies | data       |
| `patients`        | medical_history, allergies, emergency_contact, insurance_info      | data       |
| `prescriptions`   | medications, dosage_instructions, notes                            | data       |
| `lab_results`     | results, notes, interpretation                                     | data       |
| `user_sessions`   | session_token                                                      | session    |

### 3.3 Thêm mã hóa cho bảng mới (qua API)

**Không cần code** — chỉ gọi API hoặc dùng UI Vault Manager tab "Mã hóa":

```http
POST /api/keyvault/encryption-configs
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "tableName": "billing_records",
  "encryptedFields": ["total_amount", "payment_method", "card_last4"],
  "dekPurpose": "data",
  "description": "Mã hóa thông tin thanh toán"
}
```

### 3.4 Tích hợp mã hóa vào Service Layer

Để **tự động** mã hóa/giải mã trong service layer, inject `IKeyVaultService` rồi gọi `EncryptAsync`/`DecryptAsync`:

```csharp
public class BillingService
{
    private readonly IKeyVaultService _kvService;
    private readonly IVaultRepository _vaultRepo;

    public BillingService(IKeyVaultService kvService, IVaultRepository vaultRepo)
    {
        _kvService = kvService;
        _vaultRepo = vaultRepo;
    }

    public async Task<BillingRecord> SaveBillingAsync(BillingRecord record)
    {
        // 1. Lấy config mã hóa cho bảng billing_records
        var configs = await _vaultRepo.GetEncryptionConfigsAsync();
        var config = configs.FirstOrDefault(c =>
            c.TableName == "billing_records" && c.IsEnabled);

        if (config is not null)
        {
            // 2. Mã hóa từng trường được cấu hình
            foreach (var field in config.EncryptedFields)
            {
                var value = GetFieldValue(record, field);
                if (!string.IsNullOrEmpty(value))
                {
                    var encrypted = await _kvService.EncryptAsync(
                        Encoding.UTF8.GetBytes(value),
                        Enum.Parse<KeyPurpose>(config.DekPurpose, true));
                    SetFieldValue(record, field, encrypted.CiphertextBase64);
                }
            }
        }

        await _repository.AddAsync(record);
        return record;
    }

    public async Task<string> DecryptFieldAsync(string ciphertext, string iv, string dekPurpose = "data")
    {
        var plainBytes = await _kvService.DecryptAsync(
            ciphertext, iv, Enum.Parse<KeyPurpose>(dekPurpose, true));
        return Encoding.UTF8.GetString(plainBytes);
    }
}
```

### 3.5 Tích hợp qua EF Core Interceptor (tự động hoàn toàn — ĐÃ TÍCH HỢP)

`VaultEncryptionInterceptor` đã được tích hợp sẵn trong hệ thống. Mọi entity thuộc bảng có `EncryptionConfig` sẽ **tự động** được mã hóa khi ghi.

**File**: `IVF.Infrastructure/Persistence/VaultEncryptionInterceptor.cs`

```csharp
public class VaultEncryptionInterceptor : SaveChangesInterceptor
{
    private readonly IKeyVaultService _kvService;
    private readonly IVaultRepository _vaultRepo;
    private List<EncryptionConfig>? _configsCache;  // Request-scoped cache

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _configsCache ??= (await _vaultRepo.GetEncryptionConfigsAsync(cancellationToken)).ToList();

        foreach (var entry in context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var tableName = entry.Metadata.GetTableName();
            var config = _configsCache.FirstOrDefault(c =>
                c.TableName == tableName && c.IsEnabled);

            if (config is null) continue;

            foreach (var field in config.EncryptedFields)
            {
                var prop = entry.Properties.FirstOrDefault(p =>
                    p.Metadata.GetColumnName() == field);
                if (prop?.CurrentValue is string value && !IsAlreadyEncrypted(value))
                {
                    var encrypted = await _kvService.EncryptAsync(
                        Encoding.UTF8.GetBytes(value),
                        Enum.Parse<KeyPurpose>(config.DekPurpose, true), cancellationToken);
                    prop.CurrentValue = JsonSerializer.Serialize(new {
                        c = encrypted.CiphertextBase64,
                        iv = encrypted.IvBase64
                    });
                }
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

**Đặc điểm**:

- **Skip already encrypted**: Kiểm tra JSON có key `"c"` + `"iv"` → bỏ qua
- **Graceful degradation**: Nếu mã hóa lỗi → log warning, lưu dữ liệu không mã hóa
- **Request-scoped cache**: `_configsCache` chỉ load từ DB một lần mỗi request

**Giải mã khi đọc** — dùng `IVaultDecryptionService`:

```csharp
public interface IVaultDecryptionService
{
    Task<string> DecryptFieldAsync(string encryptedValue,
        string dekPurpose = "data", CancellationToken ct = default);
    bool IsEncrypted(string? value);
}

// Sử dụng
var decrypted = await _decryptionService.DecryptFieldAsync(patient.MedicalHistory);
```

Đăng ký interceptor **đã được thực hiện tự động** trong `Infrastructure/DependencyInjection.cs`:

```csharp
services.AddScoped<VaultEncryptionInterceptor>();
services.AddDbContext<IvfDbContext>((sp, options) =>
{
    options.UseNpgsql(...)
        .AddInterceptors(
            sp.GetRequiredService<AuditInterceptor>(),
            sp.GetRequiredService<VaultEncryptionInterceptor>());
});
```

> **Lưu ý**: Interceptor hoạt động **tự động** — không cần code thêm trong service layer trừ khi cần giải mã khi đọc.

---

## 4. Quản lý Secret (Secret Engine)

### 4.1 Inject vào service

```csharp
public class ExternalApiService
{
    private readonly IVaultSecretService _vault;

    public ExternalApiService(IVaultSecretService vault)
    {
        _vault = vault;
    }

    public async Task<string> GetApiKeyAsync()
    {
        // Secret path theo convention: {service}/{key}
        var result = await _vault.GetSecretAsync("external-api/api-key");
        return result?.Value ?? throw new InvalidOperationException("API key not found");
    }
}
```

### 4.2 Lưu secret

```csharp
// Tự động tạo version mới nếu path đã tồn tại
await _vault.PutSecretAsync(
    path: "database/readonly-password",
    plaintext: "super-secure-pw",
    userId: currentUserId,
    metadata: "{\"environment\":\"production\"}");
```

### 4.3 Import hàng loạt

```csharp
var secrets = new Dictionary<string, string>
{
    ["smtp/host"] = "smtp.gmail.com",
    ["smtp/port"] = "587",
    ["smtp/username"] = "noreply@clinic.vn",
    ["smtp/password"] = "app-specific-password",
};

var result = await _vault.ImportSecretsAsync(secrets, prefix: "mail-service");
// result.Imported = 4, result.Failed = 0
```

### 4.4 Convention đặt tên Secret Path

```
{service-name}/{secret-type}
```

Ví dụ:

- `database/connection-string` — chuỗi kết nối DB
- `minio/access-key` — MinIO access key
- `smtp/password` — mật khẩu SMTP
- `sms-gateway/api-key` — API key SMS
- `digital-signing/pfx-password` — password file chứng thư số
- `external-api/lab-system/token` — token tích hợp hệ thống lab

### 4.5 Phiên bản Secret

Mỗi lần gọi `PutSecretAsync` với cùng path sẽ tạo **version mới** (không ghi đè):

```csharp
// Đọc version cụ thể
var v2 = await _vault.GetSecretAsync("database/password", version: 2);

// Liệt kê toàn bộ version
var versions = await _vault.GetVersionsAsync("database/password");
```

---

## 5. Kiểm soát truy cập theo trường (Field-Level Access)

### 5.1 Entity FieldAccessPolicy

```csharp
public class FieldAccessPolicy : BaseEntity
{
    public string TableName { get; private set; }   // "patients"
    public string FieldName { get; private set; }   // "medical_history"
    public string Role { get; private set; }        // "Doctor"
    public string AccessLevel { get; private set; } // "full" | "partial" | "masked" | "none"
    public string MaskPattern { get; private set; } // "********"
    public int PartialLength { get; private set; }  // 5
}
```

### 5.2 Các mức truy cập

| AccessLevel | Hiển thị           | Ví dụ (gốc: "Nguyễn Văn An") |
| ----------- | ------------------ | ---------------------------- |
| `full`      | Toàn bộ            | "Nguyễn Văn An"              |
| `partial`   | N ký tự đầu + mask | "Nguyễ**\*\*\*\***"          |
| `masked`    | Mask hoàn toàn     | "**\*\*\*\***"               |
| `none`      | Ẩn hoàn toàn       | _(không trả về)_             |

### 5.3 Tạo policy qua API

```http
POST /api/keyvault/field-access-policies
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "tableName": "patients",
  "fieldName": "medical_history",
  "role": "Nurse",
  "accessLevel": "partial",
  "maskPattern": "***HIDDEN***",
  "partialLength": 10,
  "description": "Y tá chỉ xem 10 ký tự đầu"
}
```

### 5.4 FieldAccessService (tích hợp sẵn)

**File**: `IVF.Application/Common/Services/FieldAccessService.cs`

```csharp
public interface IFieldAccessService
{
    Task ApplyFieldAccessAsync<T>(T dto, string tableName, string userRole,
        CancellationToken ct = default) where T : class;
    Task ApplyFieldAccessAsync<T>(IEnumerable<T> dtos, string tableName,
        string userRole, CancellationToken ct = default) where T : class;
}
```

**Đặc điểm**:

- **Admin bypass**: Role `"Admin"` → trả về dữ liệu gốc
- **Request-scoped cache**: Load policies từ DB một lần mỗi request
- **Reflection-based**: Tìm `PropertyInfo` theo `FieldName` (case-insensitive)
- **Hỗ trợ cả đơn lẻ và collection**: Apply cho một DTO hoặc `IEnumerable<T>`

### 5.5 FieldAccessBehavior — MediatR Pipeline (TỰ ĐỘNG)

**File**: `IVF.Application/Common/Behaviors/FieldAccessBehavior.cs`

Thay vì gọi thủ công `FieldAccessHelper` trong mỗi handler, hệ thống đã tích hợp `FieldAccessBehavior` như MediatR pipeline behavior:

```csharp
public interface IFieldAccessProtected
{
    string TableName { get; }
}

public class FieldAccessBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IFieldAccessService _fieldAccessService;
    private readonly ICurrentUserService _currentUser;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is not IFieldAccessProtected fap) return response;
        if (_currentUser.Role == "Admin") return response;

        // Tự động unwrap Result<T>, PagedResult<T>, hoặc DTO trực tiếp
        // Áp dụng field masking theo role
        await ApplyFieldAccess(response, fap.TableName, cancellationToken);

        return response;
    }
}
```

**Response Types hỗ trợ**:

- `Result<T>` → unwrap `.Value` property
- `PagedResult<T>` → iterate `.Items` collection
- Direct DTO objects

**Sử dụng trong Query** — chỉ cần implement `IFieldAccessProtected`:

```csharp
public record GetPatientByIdQuery(Guid Id)
    : IRequest<Result<PatientDto>>, IFieldAccessProtected
{
    public string TableName => "patients";
}

public record SearchPatientsQuery(string? Search, int Page, int PageSize)
    : IRequest<PagedResult<PatientDto>>, IFieldAccessProtected
{
    public string TableName => "patients";
}
```

> **Không cần code gì thêm** — `FieldAccessBehavior` tự động mask fields sau khi handler trả về kết quả.

---

## 6. Zero Trust — bảo vệ tự động qua MediatR

### 6.1 Cách hoạt động

Zero Trust Behavior là `IPipelineBehavior<TRequest, TResponse>` chạy **trước** mọi MediatR handler. Nó kiểm tra 6 điểm:

| #   | Kiểm tra           | Mô tả                                                     |
| --- | ------------------ | --------------------------------------------------------- |
| 1   | **Auth Level**     | Yêu cầu mức xác thực tối thiểu (Session / MFA / Hardware) |
| 2   | **Device Risk**    | Đánh giá rủi ro thiết bị (Low / Medium / High / Critical) |
| 3   | **Trusted Device** | Thiết bị đã đăng ký và tin cậy                            |
| 4   | **Fresh Session**  | Phiên làm việc chưa quá thời hạn                          |
| 5   | **Geo-fence**      | Vị trí địa lý nằm trong phạm vi cho phép                  |
| 6   | **VPN/Tor**        | Không truy cập qua VPN/Tor ẩn danh                        |

### 6.2 Tích hợp vào Command

Chỉ cần implement interface `IZeroTrustProtected`:

```csharp
public record TransferPatientDataCommand(
    Guid PatientId,
    string TargetSystem) : IRequest<bool>, IZeroTrustProtected
{
    // Tự động enforce Zero Trust cho action này
    public ZTVaultAction RequiredAction => ZTVaultAction.ExportData;
}
```

**Không cần code gì thêm** — `ZeroTrustBehavior` tự động validate trước khi handler chạy.

`ZeroTrustBehavior` sử dụng `ICurrentUserService` để tự động lấy user context:

```csharp
var context = new ZTAccessContext(
    UserId: _currentUser.UserId?.ToString() ?? "anonymous",
    DeviceId: "server",
    IpAddress: _currentUser.IpAddress ?? "127.0.0.1",
    Country: "VN",
    CurrentAuthLevel: AuthLevel.Session,
    ...);
```

### 6.3 Gọi trực tiếp (cho trường hợp không dùng MediatR)

```csharp
public class CustomService
{
    private readonly IZeroTrustService _ztService;

    public async Task ExportDataAsync(ZTAccessContext context)
    {
        var decision = await _ztService.CheckVaultAccessAsync(
            new CheckZTAccessRequest(ZTVaultAction.ExportData, context));

        if (!decision.Allowed)
            throw new UnauthorizedAccessException(
                $"Zero Trust denied: {decision.Reason}. " +
                $"Failed checks: {string.Join(", ", decision.FailedChecks)}");

        // Tiếp tục xử lý...
    }
}
```

### 6.4 Break-Glass Override (trường hợp khẩn cấp)

```csharp
var decision = await _ztService.CheckVaultAccessAsync(
    new CheckZTAccessRequest(
        Action: ZTVaultAction.ReadSecret,
        Context: context,
        UseBreakGlassOverride: true,
        BreakGlassRequestId: "EMRG-2026-001"));
```

> Break-glass override sẽ được ghi nhận vào audit log để review sau.

---

## 7. Vault Token Authentication

### 7.1 Tổng quan

Ngoài JWT Bearer, hệ thống hỗ trợ xác thực qua **Vault Token** — cho phép programmatic access từ service-to-service hoặc CI/CD pipelines.

```
┌──────────────────────┐      X-Vault-Token: hvs.xxx
│   External Service   │ ─────────────────────────────────► API
│   (Python, Node, CI) │                                    │
└──────────────────────┘                                    ▼
                                                ┌───────────────────┐
                                                │ VaultTokenMiddle- │
                                                │ ware              │
                                                │  SHA-256 lookup   │
                                                │  → ClaimsPrincipal│
                                                └───────────────────┘
```

### 7.2 VaultTokenMiddleware

**File**: `IVF.API/Middleware/VaultTokenMiddleware.cs`

Middleware chạy **TRƯỚC** `UseAuthentication()` — nếu request có header `X-Vault-Token`, tự động tạo `ClaimsPrincipal` từ token:

```csharp
public class VaultTokenMiddleware
{
    private const string VaultTokenHeader = "X-Vault-Token";

    public async Task InvokeAsync(HttpContext context, IVaultTokenValidator tokenValidator)
    {
        if (context.Request.Headers.TryGetValue(VaultTokenHeader, out var tokenValue))
        {
            var result = await tokenValidator.ValidateTokenAsync(tokenValue.ToString());

            if (result is not null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, result.TokenId.ToString()),
                    new(ClaimTypes.Name, result.DisplayName ?? result.Accessor),
                    new("vault_accessor", result.Accessor),
                    new("vault_token_type", result.TokenType),
                    new("auth_method", "vault_token")
                };

                // Policies trở thành Role claims
                foreach (var policy in result.Policies)
                    claims.Add(new Claim(ClaimTypes.Role, policy));

                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "VaultToken"));
            }
        }

        await _next(context);
    }
}
```

**Claims được tạo**:

| Claim              | Giá trị                   | Mục đích                    |
| ------------------ | ------------------------- | --------------------------- |
| `NameIdentifier`   | Token ID (Guid)           | Xác định token              |
| `Name`             | DisplayName hoặc Accessor | Tên hiển thị                |
| `vault_accessor`   | Accessor string           | Theo dõi audit              |
| `vault_token_type` | `"service"` / `"batch"`   | Loại token                  |
| `auth_method`      | `"vault_token"`           | Phân biệt JWT vs Token auth |
| `Role` (multiple)  | Tên các policy            | Authorization               |

### 7.3 IVaultTokenValidator

**File**: `IVF.Application/Common/Interfaces/IVaultTokenValidator.cs`

```csharp
public interface IVaultTokenValidator
{
    Task<VaultTokenValidationResult?> ValidateTokenAsync(
        string rawToken, CancellationToken ct = default);
    Task<bool> HasCapabilityAsync(
        string rawToken, string path, string capability, CancellationToken ct = default);
}

public record VaultTokenValidationResult(
    Guid TokenId, string Accessor, string? DisplayName,
    string[] Policies, string TokenType,
    DateTime? ExpiresAt, int UsesCount);
```

**Validation flow**:

1. Compute SHA-256 hash of raw token
2. Lookup `VaultToken` by `TokenHash` in DB
3. Check `IsValid` (not revoked, not expired, not exhausted)
4. Increment `UsesCount`
5. Return validation result with policies

**Capability checking** (path-based):

- Load `VaultPolicy` definitions matching token's policy names
- Match `PathPattern` against requested path (regex: `*` → `[^/]+`, `**` → `.*`)
- `"sudo"` capability implies all other capabilities

### 7.4 Sử dụng Token từ service bên ngoài

```python
import requests

headers = {"X-Vault-Token": "hvs.CAESIGxxxxxxxxxx"}

# Đọc secret qua vault token
resp = requests.get(
    "https://api.clinic.vn/api/keyvault/secrets/database/password",
    headers=headers)
```

```bash
# cURL
curl -H "X-Vault-Token: hvs.CAESIGxxxxxxxxxx" \
  https://api.clinic.vn/api/keyvault/secrets/database/password
```

---

## 8. API Key Authentication

### 8.1 Tổng quan

Hệ thống hỗ trợ xác thực qua **API Key** — chủ yếu cho desktop client (WinForms fingerprint app) và các dịch vụ backend cần truy cập API mà không qua JWT.

```
┌──────────────────────┐      X-API-Key: IVF-xxx...
│   Desktop Client     │ ─────────────────────────────────► API
│   (WinForms/Console) │                                    │
└──────────────────────┘                                    ▼
                                                ┌───────────────────┐
                                                │ ApiKeyMiddleware  │
                                                │  BCrypt verify    │
                                                │  (DB + config     │
                                                │   fallback)       │
                                                │  → ClaimsPrincipal│
                                                └───────────────────┘
```

**3 phương thức xác thực trong pipeline**:

```
Request → VaultTokenMiddleware (X-Vault-Token)
        → ApiKeyMiddleware (X-API-Key / apiKey query)
        → JWT Authentication (Authorization: Bearer)
        → Authorization
```

### 8.2 IApiKeyValidator

**File**: `IVF.Application/Common/Interfaces/IApiKeyValidator.cs`

```csharp
public interface IApiKeyValidator
{
    Task<ApiKeyValidationResult?> ValidateAsync(
        string rawKey, CancellationToken ct = default);
}

public record ApiKeyValidationResult(
    Guid? KeyId,         // null nếu từ config
    string KeyName,
    string ServiceName,
    string? KeyPrefix,
    string? Environment,
    int Version,
    string Source         // "database" hoặc "config"
);
```

### 8.3 ApiKeyValidator — Validation Flow

**File**: `IVF.Infrastructure/Services/ApiKeyValidator.cs`

Quy trình xác thực 2 lớp:

1. **Database lookup** (ưu tiên):
   - Trích prefix từ key (e.g. `IVF` từ `IVF-Desktop-...`)
   - Tìm active keys trong bảng `api_key_managements` (theo service name)
   - BCrypt.Verify raw key vs stored `KeyHash`
   - Kiểm tra `IsActive` + `ExpiresAt`
   - Ghi audit log khi xác thực thành công

2. **Config fallback** (backward compatibility):
   - Đọc `DesktopClients:ApiKeys` từ `appsettings.json`
   - So sánh constant-time (chống timing attack)
   - Trả về `Source = "config"`

```csharp
// ApiKeyValidator.cs — core flow
public async Task<ApiKeyValidationResult?> ValidateAsync(string rawKey, CancellationToken ct)
{
    // 1. Try DB-backed validation (BCrypt hash)
    var result = await ValidateFromDatabaseAsync(rawKey, ct);
    if (result is not null) return result;

    // 2. Fallback to config-based keys
    return ValidateFromConfig(rawKey);
}
```

### 8.4 ApiKeyMiddleware

**File**: `IVF.API/Middleware/ApiKeyMiddleware.cs`

Middleware chạy **SAU** `VaultTokenMiddleware`, **TRƯỚC** `UseAuthentication()`:

```csharp
public class ApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-API-Key";
    private const string ApiKeyQuery = "apiKey";

    public async Task InvokeAsync(HttpContext context, IApiKeyValidator apiKeyValidator)
    {
        // Skip nếu đã authenticated (e.g., bởi VaultTokenMiddleware)
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        var rawKey = context.Request.Headers[ApiKeyHeader].FirstOrDefault()
                  ?? context.Request.Query[ApiKeyQuery].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(rawKey))
        {
            var result = await apiKeyValidator.ValidateAsync(rawKey);
            if (result is not null)
            {
                // Tạo ClaimsPrincipal với API key metadata
                var claims = new List<Claim> { ... };
                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, "ApiKey"));
            }
        }

        await _next(context);
    }
}
```

**Claims được tạo khi API key hợp lệ**:

| Claim                 | Giá trị                   | Mục đích                                |
| --------------------- | ------------------------- | --------------------------------------- |
| `Name`                | KeyName                   | Tên key                                 |
| `NameIdentifier`      | KeyId (Guid)              | Xác định key (chỉ khi từ DB)            |
| `auth_method`         | `"api_key"`               | Phân biệt JWT vs API Key vs Vault Token |
| `api_key_source`      | `"database"` / `"config"` | Nguồn xác thực                          |
| `service_name`        | ServiceName               | Dịch vụ sở hữu key                      |
| `api_key_environment` | Environment               | Môi trường (dev/staging/prod)           |
| `api_key_prefix`      | KeyPrefix                 | Prefix của key                          |
| `api_key_version`     | Version number            | Phiên bản key                           |

### 8.5 Tích hợp với SignalR Hubs

Desktop client kết nối SignalR qua `apiKey` query param:

```
wss://api.clinic.vn/hubs/fingerprint?apiKey=IVF-Desktop-xxx
```

- **ApiKeyMiddleware** xử lý trước khi WebSocket upgrade
- **FingerprintHub.OnConnectedAsync** kiểm tra `auth_method` claim hoặc fallback validate qua `IApiKeyValidator`
- **ApiKeyAuthorizationFilter** (IHubFilter) bảo vệ hub method invocations

### 8.6 ApiKeyManagement Entity

**File**: `IVF.Domain/Entities/ApiKeyManagement.cs`

Quản lý vòng đời API key trong database:

```csharp
public class ApiKeyManagement : BaseEntity
{
    public string KeyName { get; private set; }
    public string ServiceName { get; private set; }
    public string? KeyPrefix { get; private set; }
    public string KeyHash { get; private set; }      // BCrypt hash
    public bool IsActive { get; private set; }
    public string? Environment { get; private set; }
    public Guid CreatedBy { get; private set; }
    public int? RotationIntervalDays { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastRotatedAt { get; private set; }
    public int Version { get; private set; } = 1;

    // Methods: Create(), Rotate(), Deactivate(), Activate()
}
```

### 8.7 API Key Management Endpoints

Quản lý key qua `/api/keyvault/keys/*`:

| Method | Path                                  | Mô tả                         |
| ------ | ------------------------------------- | ----------------------------- |
| POST   | `/api/keyvault/keys`                  | Tạo key mới (lưu BCrypt hash) |
| GET    | `/api/keyvault/keys/{service}/{name}` | Lấy thông tin key             |
| POST   | `/api/keyvault/keys/rotate`           | Xoay key (version++)          |
| DELETE | `/api/keyvault/keys/{id}`             | Deactivate key                |
| GET    | `/api/keyvault/keys/expiring?days=30` | Danh sách key sắp hết hạn     |

### 8.8 Migration từ Config sang DB

Để migrate desktop client keys từ `appsettings.json` sang DB:

```bash
# 1. Hash key hiện tại
# BCrypt.Net.BCrypt.HashPassword("IVF-Desktop-FingerprintClient-2024-SecureKey-001")

# 2. Tạo key trong DB qua API
POST /api/keyvault/keys
{
  "keyName": "fingerprint-client-001",
  "serviceName": "DesktopClient",
  "keyPrefix": "IVF",
  "keyHash": "$2a$11$...",
  "environment": "production",
  "rotationIntervalDays": 90
}

# 3. Sau khi verify, xóa key khỏi appsettings.json
# ApiKeyValidator sẽ validate từ DB thay vì config
```

### 8.9 Sử dụng từ Desktop Client

```csharp
// WinForms / Console App
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-API-Key", "IVF-Desktop-xxx");

var response = await client.GetAsync(
    "https://api.clinic.vn/api/patients/fingerprints/all");
```

```csharp
// SignalR connection từ desktop
var connection = new HubConnectionBuilder()
    .WithUrl("https://api.clinic.vn/hubs/fingerprint?apiKey=IVF-Desktop-xxx")
    .Build();
await connection.StartAsync();
```

---

## 9. Vault Policy Authorization

### 8.1 Tổng quan

Vault Policy Authorization cho phép kiểm soát truy cập **dựa trên path và capability** — tương tự HashiCorp Vault ACL. Hoạt động cho cả JWT users (qua `VaultUserPolicy` assignments) và Vault tokens (qua `VaultToken.Policies`).

```
User/Token ──► VaultPolicyEvaluator ──► Allowed/Denied
                    │
    ┌───────────────┼────────────────┐
    │               │                │
    ▼               ▼                ▼
JWT User        Vault Token      Admin Role
VaultUserPolicy Role claims      Auto-bypass
assignments     → VaultPolicy    (implicit)
```

### 8.2 VaultPolicy entity

```csharp
public class VaultPolicy : BaseEntity
{
    public string Name { get; private set; }         // "secret-reader"
    public string PathPattern { get; private set; }   // "secrets/**"
    public string[] Capabilities { get; private set; } // ["read", "list"]
}
```

**Capabilities**: `read`, `create`, `update`, `delete`, `list`, `sudo`

**Path Patterns**:

- `secrets/*` — match `secrets/foo` nhưng **không** match `secrets/foo/bar`
- `secrets/**` — match `secrets/foo` **và** `secrets/foo/bar/baz`
- `patients/medical-history` — match chính xác

### 8.3 IVaultPolicyEvaluator

**File**: `IVF.Application/Common/Interfaces/IVaultPolicyEvaluator.cs`

```csharp
public interface IVaultPolicyEvaluator
{
    Task<PolicyEvaluation> EvaluateAsync(string resourcePath,
        string capability, CancellationToken ct = default);
    Task<PolicyEvaluation> EvaluateForUserAsync(Guid userId,
        string resourcePath, string capability, CancellationToken ct = default);
    Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesAsync(
        CancellationToken ct = default);
    Task<IReadOnlyList<EffectivePolicy>> GetEffectivePoliciesForUserAsync(
        Guid userId, CancellationToken ct = default);
}

public record PolicyEvaluation(bool Allowed, string? MatchedPolicy, string? Reason);

public record EffectivePolicy(
    string PolicyName, string PathPattern,
    string[] Capabilities, string Source);
// Source: "user-assignment" | "vault-token" | "admin-bypass"
```

**Luồng đánh giá** (VaultPolicyEvaluator):

1. **Admin bypass**: Role = `"Admin"` → cho phép ngay
2. **Vault Token auth** (`auth_method` claim = `"vault_token"`):
   - Đọc policy names từ `ClaimTypes.Role` claims
   - Load `VaultPolicy` theo tên → kiểm tra path + capability
3. **JWT user auth**:
   - Load `VaultUserPolicy` assignments theo `UserId`
   - Get corresponding `VaultPolicy` definitions
   - Kiểm tra path + capability
4. **Request-scoped cache**: `_allPoliciesCache` để tránh load lại trong cùng request

### 8.4 VaultPolicyBehavior (MediatR Pipeline)

**File**: `IVF.Application/Common/Behaviors/VaultPolicyBehavior.cs`

```csharp
public interface IVaultPolicyProtected
{
    string ResourcePath { get; }       // "patients/**"
    string RequiredCapability { get; } // "read"
}

public class VaultPolicyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IVaultPolicyProtected policyRequest)
            return await next();

        var evaluation = await _evaluator.EvaluateAsync(
            policyRequest.ResourcePath, policyRequest.RequiredCapability);

        if (!evaluation.Allowed)
            throw new UnauthorizedAccessException(
                $"Vault policy denied: {evaluation.Reason}");

        return await next();
    }
}
```

**Sử dụng**:

```csharp
public record ExportPatientDataQuery(Guid PatientId)
    : IRequest<byte[]>, IVaultPolicyProtected
{
    public string ResourcePath => "patients/export";
    public string RequiredCapability => "read";
}
```

### 8.5 VaultPolicyAuthorizationHandler (Endpoint-level)

**File**: `IVF.API/Authorization/VaultPolicyAuthorizationHandler.cs`

Cho phép bảo vệ endpoint bằng vault policy mà không cần MediatR:

```csharp
// Đăng ký trong DI
builder.Services.AddVaultPolicyAuthorization();

// Sử dụng trên endpoint
app.MapGet("/api/patients/export", handler)
    .RequireVaultPolicy("patients/export", "read");
```

### 8.6 User Policy Endpoints (non-AdminOnly)

Hai endpoint cho phép **mọi user đã xác thực** kiểm tra policies của mình:

```http
# Xem danh sách policies hiện tại
GET /api/keyvault/me/policies
Authorization: Bearer <jwt>

# Response:
[
  {
    "policyName": "patient-reader",
    "pathPattern": "patients/**",
    "capabilities": ["read", "list"],
    "source": "user-assignment"
  }
]
```

```http
# Kiểm tra quyền truy cập cụ thể
GET /api/keyvault/me/check?path=patients/export&capability=read
Authorization: Bearer <jwt>

# Response:
{
  "allowed": true,
  "matchedPolicy": "patient-reader",
  "reason": "Policy 'patient-reader' grants 'read' on 'patients/**'"
}
```

---

## 10. Dynamic Credentials (PostgreSQL)

### 9.1 Tổng quan

Dynamic Credentials cho phép tạo **PostgreSQL roles tạm thời** với thời hạn sử dụng — tự động hết hạn khi TTL hết. Phù hợp cho microservices, batch jobs, hoặc third-party integrations cần truy cập DB giới hạn.

```
Request → DynamicCredentialProvider → PostgreSQL
                │                        │
                │  CREATE ROLE "ivf_dyn_a1b2c3"
                │  LOGIN PASSWORD 'xxx'
                │  VALID UNTIL '2026-03-01 12:30:00+00'
                │                        │
                │  GRANT SELECT, INSERT ON TABLE patients
                │                        │
                ▼                        ▼
        VaultDynamicCredential      PostgreSQL Role
        (stored in DB, encrypted)   (auto-expires)
```

### 9.2 IDynamicCredentialProvider

**File**: `IVF.Application/Common/Interfaces/IDynamicCredentialProvider.cs`

```csharp
public interface IDynamicCredentialProvider
{
    Task<DynamicCredentialResult> GenerateCredentialAsync(
        DynamicCredentialRequest request, CancellationToken ct = default);
    Task RevokeCredentialAsync(Guid credentialId, CancellationToken ct = default);
    Task<int> RevokeExpiredCredentialsAsync(CancellationToken ct = default);
    Task<string?> GetConnectionStringAsync(Guid credentialId, CancellationToken ct = default);
}

public record DynamicCredentialRequest(
    string DbHost, int DbPort, string DbName,
    string AdminUsername, string AdminPassword,
    int TtlSeconds = 3600,          // Default 1 giờ
    string[]? GrantedTables = null,  // null = tất cả tables
    bool ReadOnly = false);          // true = chỉ SELECT

public record DynamicCredentialResult(
    Guid Id, string LeaseId, string Username, string Password,
    string ConnectionString, DateTime ExpiresAt);
```

### 9.3 Flow tạo credential

1. **Tạo username**: `"ivf_dyn_<6-byte-hex>"` (ví dụ: `ivf_dyn_a1b2c3`)
2. **Tạo password**: `Base64(24 random bytes)`
3. **PostgreSQL commands**:
   ```sql
   CREATE ROLE "ivf_dyn_a1b2c3"
     LOGIN PASSWORD 'dGhlX3NlY3VyZV9wYXNzd29yZA=='
     VALID UNTIL '2026-03-01 12:30:00+00';
   GRANT CONNECT ON DATABASE "ivf_db" TO "ivf_dyn_a1b2c3";
   GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE patients TO "ivf_dyn_a1b2c3";
   ```
4. **Lưu DB**: `VaultDynamicCredential` entity, admin password encrypted via `IKeyVaultService`
5. **Audit log**: Ghi nhận tạo credential

### 9.4 Revocation

```csharp
// Revoke thủ công
await _dynamicCredentialProvider.RevokeCredentialAsync(credentialId);

// PostgreSQL:
// REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public FROM "ivf_dyn_a1b2c3";
// DROP ROLE IF EXISTS "ivf_dyn_a1b2c3";
```

### 9.5 API Endpoints

```http
# Tạo dynamic credential
POST /api/keyvault/dynamic
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "dbHost": "localhost",
  "dbPort": 5433,
  "dbName": "ivf_db",
  "adminUsername": "postgres",
  "adminPassword": "postgres",
  "ttlSeconds": 3600,
  "grantedTables": ["patients", "treatment_cycles"],
  "readOnly": true
}

# Response:
{
  "id": "...",
  "leaseId": "lease-xxx",
  "username": "ivf_dyn_a1b2c3",
  "password": "dGhlX3NlY3VyZV9wYXNzd29yZA==",
  "connectionString": "Host=localhost;Port=5433;Database=ivf_db;Username=ivf_dyn_a1b2c3;Password=...",
  "expiresAt": "2026-03-01T12:30:00Z"
}

# Revoke
DELETE /api/keyvault/dynamic/{id}
Authorization: Bearer <admin-jwt>
```

---

## 11. Lease Management

### 10.1 Tổng quan

Lease Management cho phép **giới hạn thời gian truy cập** vào secrets. Sau khi lease hết hạn, secret không thể đọc qua lease đó nữa (secret vẫn tồn tại trong vault).

### 10.2 ILeaseManager

**File**: `IVF.Application/Common/Interfaces/ILeaseManager.cs`

```csharp
public interface ILeaseManager
{
    Task<LeaseInfo> CreateLeaseAsync(string secretPath, int ttlSeconds,
        bool renewable = true, CancellationToken ct = default);
    Task<LeaseInfo> RenewLeaseAsync(string leaseId, int incrementSeconds,
        CancellationToken ct = default);
    Task RevokeLeaseAsync(string leaseId, CancellationToken ct = default);
    Task<LeasedSecretResult?> GetLeasedSecretAsync(string leaseId,
        CancellationToken ct = default);
    Task<IReadOnlyList<LeaseInfo>> GetActiveLeasesAsync(CancellationToken ct = default);
}

public record LeaseInfo(
    string LeaseId, Guid SecretId, string? SecretPath,
    int Ttl, bool Renewable, DateTime ExpiresAt);

public record LeasedSecretResult(
    string LeaseId, string SecretPath, string Value,
    DateTime ExpiresAt, bool Renewable);
```

### 10.3 Sử dụng

```csharp
// Tạo lease cho secret (1 giờ, có thể renew)
var lease = await _leaseManager.CreateLeaseAsync(
    "database/connection-string", ttlSeconds: 3600, renewable: true);

// Đọc secret qua lease
var secret = await _leaseManager.GetLeasedSecretAsync(lease.LeaseId);
Console.WriteLine(secret?.Value); // "Host=localhost;Port=5433;..."

// Gia hạn lease thêm 30 phút
var renewed = await _leaseManager.RenewLeaseAsync(lease.LeaseId, incrementSeconds: 1800);

// Thu hồi lease (secret vẫn tồn tại, chỉ không đọc được qua lease này)
await _leaseManager.RevokeLeaseAsync(lease.LeaseId);
```

### 10.4 API Endpoints

```http
# Tạo lease
POST /api/keyvault/leases
{ "secretPath": "database/password", "ttlSeconds": 3600, "renewable": true }

# Renew lease
POST /api/keyvault/leases/{leaseId}/renew
{ "incrementSeconds": 1800 }

# Revoke lease
POST /api/keyvault/leases/{leaseId}/revoke
```

---

## 12. Vault Configuration Provider

### 11.1 Tổng quan

`VaultConfigurationProvider` tích hợp vault secrets vào **ASP.NET IConfiguration pipeline** — cho phép đọc secrets như configuration values thông thường (`IOptions<T>`, `IConfiguration["key"]`).

```
Vault Secret Path              → IConfiguration Key
config/ConnectionStrings/Redis  → ConnectionStrings:Redis
config/Smtp/Host                → Smtp:Host
config/Smtp/Password            → Smtp:Password
```

### 11.2 Cấu hình

**File**: `IVF.Infrastructure/Services/VaultConfigurationProvider.cs`

```csharp
public class VaultConfigurationProvider : ConfigurationProvider
{
    private Timer? _reloadTimer;
    private readonly TimeSpan _reloadInterval;  // Default: 5 phút

    public override void Load()
    {
        // Load secrets có prefix "config/" từ vault
        // Map path → config key (thay "/" bằng ":")
    }
}
```

**Đăng ký trong Program.cs** (đã tích hợp sẵn sau seeders):

```csharp
var config = app.Services.GetRequiredService<IConfigurationRoot>();
config.AddVaultSecrets(scopeFactory, reloadInterval: TimeSpan.FromMinutes(5));
config.Reload();
```

### 11.3 Lưu config vào Vault

```http
# Lưu connection string Redis vào vault
POST /api/keyvault/secrets
{
  "path": "config/ConnectionStrings/Redis",
  "value": "localhost:6379,abortConnect=false"
}

# Lưu SMTP config
POST /api/keyvault/secrets
{ "path": "config/Smtp/Host", "value": "smtp.gmail.com" }
POST /api/keyvault/secrets
{ "path": "config/Smtp/Password", "value": "app-specific-password" }
```

### 11.4 Đọc config từ IConfiguration

```csharp
public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    public void Send()
    {
        // Đọc từ vault thay vì appsettings.json
        var host = _config["Smtp:Host"];       // → "smtp.gmail.com"
        var password = _config["Smtp:Password"]; // → "app-specific-password"
    }
}
```

### 11.5 Auto-Reload

Provider tự động reload secrets từ vault mỗi 5 phút (configurable). Khi vault secrets thay đổi, `IOptionsMonitor<T>` sẽ tự động nhận giá trị mới mà không cần restart ứng dụng.

---

## 13. Background Maintenance Service

### 12.1 VaultLeaseMaintenanceService

**File**: `IVF.Infrastructure/Services/VaultLeaseMaintenanceService.cs`

Background service chạy **mỗi 5 phút** (sau 45s startup delay), tự động dọn dẹp tài nguyên hết hạn:

```csharp
public class VaultLeaseMaintenanceService : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunMaintenanceAsync(stoppingToken);
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
```

### 12.2 Các tác vụ cleanup

| Tác vụ                  | Action                | Chi tiết                                                           |
| ----------------------- | --------------------- | ------------------------------------------------------------------ |
| **Expired Leases**      | `lease.auto-revoke`   | Revoke lease đã hết hạn, secret vẫn tồn tại                        |
| **Expired Credentials** | `dynamic.auto-revoke` | Gọi `RevokeExpiredCredentialsAsync()` → DROP ROLE trong PostgreSQL |
| **Expired Tokens**      | `token.auto-revoke`   | Mark token là revoked                                              |

### 12.3 Audit Logging

Mỗi auto-revocation đều được ghi vào `vault_audit_logs`:

```
Action: "lease.auto-revoke"
ResourceType: "VaultLease"
ResourceId: "<lease-id>"
PerformedBy: "system"
IpAddress: "background-service"
```

---

## 14. MediatR Pipeline tổng hợp

### 13.1 Thứ tự thực thi

```
Request
  │
  ▼
┌──────────────────────────────────────────────┐
│ 1. ValidationBehavior                        │
│    FluentValidation → ValidationException    │
│    (nếu lỗi, dừng pipeline)                  │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│ 2. VaultPolicyBehavior                       │
│    IVaultPolicyProtected marker?             │
│    → IVaultPolicyEvaluator.EvaluateAsync()   │
│    → UnauthorizedAccessException nếu denied  │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│ 3. ZeroTrustBehavior                         │
│    IZeroTrustProtected marker?               │
│    → IZeroTrustService.CheckVaultAccessAsync │
│    → UnauthorizedAccessException nếu denied  │
│    → Sử dụng ICurrentUserService cho context │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│ 4. Handler (business logic)                  │
│    Thực thi command/query                    │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│ 5. FieldAccessBehavior (post-processing)     │
│    IFieldAccessProtected marker?             │
│    → IFieldAccessService.ApplyFieldAccessAsync│
│    → Mask fields theo role                   │
│    → Unwrap Result<T>, PagedResult<T>        │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
               Response
```

### 13.2 Marker Interfaces

| Interface               | Mục đích                      | Properties                                         |
| ----------------------- | ----------------------------- | -------------------------------------------------- |
| `IZeroTrustProtected`   | Yêu cầu Zero Trust check      | `ZTVaultAction RequiredAction`                     |
| `IVaultPolicyProtected` | Yêu cầu vault policy check    | `string ResourcePath`, `string RequiredCapability` |
| `IFieldAccessProtected` | Tự động mask fields theo role | `string TableName`                                 |

### 13.3 Kết hợp nhiều marker

Một request có thể implement **nhiều marker cùng lúc**:

```csharp
public record SensitivePatientQuery(Guid PatientId)
    : IRequest<Result<PatientDto>>,
      IVaultPolicyProtected,      // Kiểm tra policy trước
      IZeroTrustProtected,        // Kiểm tra zero trust
      IFieldAccessProtected       // Mask fields sau
{
    public string ResourcePath => "patients/sensitive";
    public string RequiredCapability => "read";
    public ZTVaultAction RequiredAction => ZTVaultAction.ReadSecret;
    public string TableName => "patients";
}
```

### 13.4 Đăng ký (Application/DependencyInjection.cs)

```csharp
// Thứ tự đăng ký = thứ tự thực thi
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(VaultPolicyBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ZeroTrustBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(FieldAccessBehavior<,>));
```

---

## 15. Auto-Unseal với Azure Key Vault

### 14.1 Cấu hình Auto-Unseal

```http
POST /api/keyvault/auto-unseal/configure
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "masterPassword": "<master-password>",
  "azureKeyName": "unseal-key"
}
```

Khi cấu hình:

1. Master password được **wrap** bằng RSA key trong Azure KV
2. Wrapped password lưu vào DB (bảng `vault_settings`)
3. Khi hệ thống khởi động lại, tự động **unwrap** master password → mở vault

### 14.2 Kiểm tra trạng thái

```http
GET /api/keyvault/auto-unseal/status
```

Response:

```json
{
  "isConfigured": true,
  "keyVaultUrl": "https://emr-viet-care.vault.azure.net/",
  "keyName": "unseal-key",
  "algorithm": "RSA-OAEP-256",
  "configuredAt": "2026-01-15T10:30:00Z"
}
```

### 14.3 Thực hiện Unseal thủ công

```http
POST /api/keyvault/auto-unseal/unseal
Authorization: Bearer <admin-jwt>
```

---

## 16. API Reference

Tất cả endpoint thuộc nhóm `/api/keyvault/` yêu cầu **AdminOnly** authorization, trừ `/api/keyvault/me/*` chỉ cần **Authenticated**.

**3 phương thức xác thực được hỗ trợ**:

| Header / Param                              | Middleware             | Auth Type   | Ưu tiên      |
| ------------------------------------------- | ---------------------- | ----------- | ------------ |
| `X-Vault-Token: hvs.xxx`                    | `VaultTokenMiddleware` | Vault Token | 1 (cao nhất) |
| `X-API-Key: IVF-xxx` hoặc `?apiKey=IVF-xxx` | `ApiKeyMiddleware`     | API Key     | 2            |
| `Authorization: Bearer <jwt>`               | JWT Bearer middleware  | JWT         | 3            |

### Secret Management

| Method   | Path                       | Mô tả                               |
| -------- | -------------------------- | ----------------------------------- |
| `GET`    | `/secrets`                 | Liệt kê secrets (hỗ trợ `?prefix=`) |
| `GET`    | `/secrets/{path}`          | Đọc secret (hỗ trợ `?version=`)     |
| `POST`   | `/secrets`                 | Tạo/cập nhật secret                 |
| `DELETE` | `/secrets/{path}`          | Xóa secret (soft-delete)            |
| `GET`    | `/secrets-versions/{path}` | Liệt kê versions                    |
| `POST`   | `/secrets/import`          | Import hàng loạt                    |

### Key Operations

| Method   | Path                     | Mô tả                               |
| -------- | ------------------------ | ----------------------------------- |
| `POST`   | `/keys`                  | Tạo API key                         |
| `GET`    | `/keys/{service}/{name}` | Đọc API key                         |
| `POST`   | `/keys/rotate`           | Rotate key                          |
| `DELETE` | `/keys/{id}`             | Xóa key                             |
| `GET`    | `/keys/expiring`         | Keys sắp hết hạn (`?withinDays=30`) |

### Encryption / Decryption

| Method | Path       | Mô tả           |
| ------ | ---------- | --------------- |
| `POST` | `/encrypt` | Mã hóa dữ liệu  |
| `POST` | `/decrypt` | Giải mã dữ liệu |
| `POST` | `/wrap`    | Wrap key (KEK)  |
| `POST` | `/unwrap`  | Unwrap key      |

### Encryption Configs

| Method   | Path                              | Mô tả           |
| -------- | --------------------------------- | --------------- |
| `GET`    | `/encryption-configs`             | Liệt kê configs |
| `POST`   | `/encryption-configs`             | Tạo config mới  |
| `PUT`    | `/encryption-configs/{id}`        | Cập nhật config |
| `PUT`    | `/encryption-configs/{id}/toggle` | Bật/tắt config  |
| `DELETE` | `/encryption-configs/{id}`        | Xóa config      |

### Field Access Policies

| Method   | Path                          | Mô tả            |
| -------- | ----------------------------- | ---------------- |
| `GET`    | `/field-access-policies`      | Liệt kê policies |
| `POST`   | `/field-access-policies`      | Tạo policy       |
| `PUT`    | `/field-access-policies/{id}` | Cập nhật policy  |
| `DELETE` | `/field-access-policies/{id}` | Xóa policy       |

### Policies & User Policies (AdminOnly)

| Method   | Path                  | Mô tả                           |
| -------- | --------------------- | ------------------------------- |
| `GET`    | `/policies`           | Liệt kê vault policies          |
| `POST`   | `/policies`           | Tạo policy                      |
| `PUT`    | `/policies/{id}`      | Cập nhật policy                 |
| `DELETE` | `/policies/{id}`      | Xóa policy                      |
| `GET`    | `/user-policies`      | Liệt kê user→policy assignments |
| `POST`   | `/user-policies`      | Gán policy cho user             |
| `DELETE` | `/user-policies/{id}` | Xóa assignment                  |

### Tokens (AdminOnly)

| Method   | Path           | Mô tả                                    |
| -------- | -------------- | ---------------------------------------- |
| `GET`    | `/tokens`      | Liệt kê tokens                           |
| `POST`   | `/tokens`      | Tạo token (trả plaintext 1 lần duy nhất) |
| `DELETE` | `/tokens/{id}` | Revoke token                             |

### Leases (AdminOnly)

| Method | Path                       | Mô tả          |
| ------ | -------------------------- | -------------- |
| `GET`  | `/leases`                  | Liệt kê leases |
| `POST` | `/leases`                  | Tạo lease      |
| `POST` | `/leases/{leaseId}/renew`  | Gia hạn lease  |
| `POST` | `/leases/{leaseId}/revoke` | Thu hồi lease  |

### Dynamic Credentials (AdminOnly)

| Method   | Path            | Mô tả                         |
| -------- | --------------- | ----------------------------- |
| `GET`    | `/dynamic`      | Liệt kê dynamic credentials   |
| `POST`   | `/dynamic`      | Tạo PostgreSQL role tạm thời  |
| `DELETE` | `/dynamic/{id}` | Revoke credential (DROP ROLE) |

### User Self-Service (Authenticated — non-AdminOnly)

| Method | Path                          | Mô tả                           |
| ------ | ----------------------------- | ------------------------------- |
| `GET`  | `/me/policies`                | Xem danh sách policies hiệu lực |
| `GET`  | `/me/check?path=&capability=` | Kiểm tra quyền truy cập cụ thể  |

### Settings (AdminOnly)

| Method | Path        | Mô tả                                      |
| ------ | ----------- | ------------------------------------------ |
| `GET`  | `/settings` | Đọc cấu hình vault + Azure KV              |
| `POST` | `/settings` | Cập nhật settings (clientSecret encrypted) |

### Auto-Unseal (AdminOnly)

| Method | Path                     | Mô tả                  |
| ------ | ------------------------ | ---------------------- |
| `GET`  | `/auto-unseal/status`    | Trạng thái auto-unseal |
| `POST` | `/auto-unseal/configure` | Cấu hình auto-unseal   |
| `POST` | `/auto-unseal/unseal`    | Thực hiện unseal       |

### Monitoring (AdminOnly)

| Method | Path               | Mô tả                                   |
| ------ | ------------------ | --------------------------------------- |
| `GET`  | `/health`          | Health check Azure KV                   |
| `GET`  | `/status`          | Vault status                            |
| `GET`  | `/audit-logs`      | Audit logs (`?page=&pageSize=&action=`) |
| `POST` | `/test-connection` | Test kết nối Azure KV                   |
| `GET`  | `/db-schema`       | Introspect DB schema (cho UI)           |

---

## 17. Tích hợp cho dịch vụ mới — từng bước

### Bước 1: Xác định dữ liệu nhạy cảm

Liệt kê các bảng và trường cần mã hóa:

```
Dịch vụ: Billing Service
├── billing_records
│   ├── total_amount       → mã hóa
│   ├── payment_method     → mã hóa
│   └── card_last4         → mã hóa
└── invoices
    └── notes              → mã hóa
```

### Bước 2: Tạo Encryption Configs (qua API hoặc Seeder)

**Cách 1: API (runtime, không cần deploy)**

```http
POST /api/keyvault/encryption-configs
{
  "tableName": "billing_records",
  "encryptedFields": ["total_amount", "payment_method", "card_last4"],
  "dekPurpose": "data",
  "description": "Billing sensitive fields"
}
```

**Cách 2: Seeder (code, chạy khi khởi tạo)**

```csharp
public static class BillingEncryptionSeeder
{
    public static async Task SeedAsync(IvfDbContext context)
    {
        if (await context.EncryptionConfigs
            .AnyAsync(c => c.TableName == "billing_records"))
            return;

        var config = EncryptionConfig.Create(
            "billing_records",
            ["total_amount", "payment_method", "card_last4"],
            "data", "Billing sensitive fields", isDefault: true);

        await context.EncryptionConfigs.AddAsync(config);
        await context.SaveChangesAsync();
    }
}
```

### Bước 3: Tạo Field Access Policies

```http
POST /api/keyvault/field-access-policies
{
  "tableName": "billing_records",
  "fieldName": "card_last4",
  "role": "Nurse",
  "accessLevel": "masked",
  "maskPattern": "****"
}

POST /api/keyvault/field-access-policies
{
  "tableName": "billing_records",
  "fieldName": "total_amount",
  "role": "Cashier",
  "accessLevel": "full"
}
```

### Bước 4: Bảo vệ bằng Vault Policy (nếu cần)

```csharp
public record ProcessPaymentCommand(Guid BillingId, decimal Amount)
    : IRequest<PaymentResult>, IVaultPolicyProtected
{
    public string ResourcePath => "billing/payments";
    public string RequiredCapability => "create";
}
```

### Bước 7: Tạo Dynamic Credentials cho service (nếu cần)

```http
POST /api/keyvault/dynamic
{
  "dbHost": "localhost",
  "dbPort": 5433,
  "dbName": "ivf_db",
  "adminUsername": "postgres",
  "adminPassword": "postgres",
  "ttlSeconds": 3600,
  "grantedTables": ["billing_records", "invoices"],
  "readOnly": false
}
```

### Bước 8: Inject Vault vào Service

```csharp
public class BillingService
{
    private readonly IVaultSecretService _vault;
    private readonly IKeyVaultService _kvService;

    public BillingService(IVaultSecretService vault, IKeyVaultService kvService)
    {
        _vault = vault;
        _kvService = kvService;
    }

    // Đọc cấu hình API key từ vault
    public async Task<string> GetPaymentGatewayKeyAsync()
    {
        var secret = await _vault.GetSecretAsync("billing/payment-gateway-key");
        return secret?.Value ?? throw new InvalidOperationException("Payment key not configured");
    }

    // Mã hóa trước khi lưu DB
    public async Task<EncryptedPayload> EncryptSensitiveAsync(string plaintext)
    {
        return await _kvService.EncryptAsync(
            Encoding.UTF8.GetBytes(plaintext), KeyPurpose.Data);
    }
}
```

### Bước 5: Bảo vệ bằng Zero Trust (nếu cần)

```csharp
public record ProcessPaymentCommand(Guid BillingId, decimal Amount)
    : IRequest<PaymentResult>, IZeroTrustProtected
{
    public ZTVaultAction RequiredAction => ZTVaultAction.WriteSecret;
}
```

### Bước 6: Lưu secret vào Vault (một lần)

```csharp
// Trong migration script hoặc admin tool
await _vault.PutSecretAsync("billing/payment-gateway-key", "pk_live_xxx");
await _vault.PutSecretAsync("billing/stripe-webhook-secret", "whsec_xxx");
```

---

## 18. Ví dụ thực tế

### 18.1 Dịch vụ SMS Gateway

```csharp
public class SmsService
{
    private readonly IVaultSecretService _vault;
    private string? _cachedApiKey;

    public SmsService(IVaultSecretService vault) => _vault = vault;

    public async Task SendSmsAsync(string phone, string message)
    {
        // Lấy API key từ vault (cache trong scope)
        _cachedApiKey ??= (await _vault.GetSecretAsync("sms/api-key"))?.Value
            ?? throw new InvalidOperationException("SMS API key not in vault");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_cachedApiKey}");
        await client.PostAsJsonAsync("https://sms-provider.vn/api/send", new { phone, message });
    }
}
```

### 18.2 Dịch vụ Email với Vault + Zero Trust

```csharp
// Command với Zero Trust protection
public record SendBulkEmailCommand(List<string> Recipients, string Subject, string Body)
    : IRequest<int>, IZeroTrustProtected
{
    public ZTVaultAction RequiredAction => ZTVaultAction.ReadSecret;
}

// Handler
public class SendBulkEmailHandler : IRequestHandler<SendBulkEmailCommand, int>
{
    private readonly IVaultSecretService _vault;

    public SendBulkEmailHandler(IVaultSecretService vault) => _vault = vault;

    public async Task<int> Handle(SendBulkEmailCommand request, CancellationToken ct)
    {
        var host = (await _vault.GetSecretAsync("smtp/host", ct: ct))!.Value;
        var port = int.Parse((await _vault.GetSecretAsync("smtp/port", ct: ct))!.Value);
        var user = (await _vault.GetSecretAsync("smtp/username", ct: ct))!.Value;
        var pass = (await _vault.GetSecretAsync("smtp/password", ct: ct))!.Value;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };

        var sent = 0;
        foreach (var recipient in request.Recipients)
        {
            await client.SendMailAsync(user, recipient, request.Subject, request.Body);
            sent++;
        }
        return sent;
    }
}
```

### 18.3 Dịch vụ tích hợp Lab (LIS) với Field Access

```csharp
// Query với field access
public record GetLabResultQuery(Guid ResultId)
    : IRequest<LabResultDto>, IFieldAccessProtected
{
    public string TableName => "lab_results";
}

// Handler
public class GetLabResultHandler : IRequestHandler<GetLabResultQuery, LabResultDto>
{
    private readonly ILabRepository _repo;
    private readonly IVaultRepository _vaultRepo;
    private readonly ICurrentUserService _currentUser;

    public async Task<LabResultDto> Handle(GetLabResultQuery request, CancellationToken ct)
    {
        var result = await _repo.GetByIdAsync(request.ResultId, ct);
        var dto = _mapper.Map<LabResultDto>(result);

        // ÁpVaultPolicyProtected` vào các Command/Query cần kiểm soát policy
- [ ] Thêm `IFieldAccessProtected` vào các Query cần mask
- [ ] Tạo `VaultPolicy` + `VaultUserPolicy` assignments cho từng role
- [ ] Cấp Vault Token cho service-to-service nếu không dùng JWT
- [ ] Tạo Dynamic Credentials cho service cần truy cập DB trực tiếp
- [ ] Lưu connection strings, API keys, credentials vào Vault
- [ ] Lưu config values vào vault (prefix `config/`) nếu cần inject vào `IConfiguration`
- [ ] Test với `GET /api/keyvault/health` và `POST /api/keyvault/test-connection`
- [ ] Kiểm tra policies hiệu lực: `GET /api/keyvault/me/policies
    }
}
```

### 18.4 Desktop Client với API Key Authentication

```csharp
// WinForms desktop app — kết nối API bằng API key
public class FingerprintApiClient
{
    private readonly HttpClient _client;

    public FingerprintApiClient(string apiKey)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    public async Task<List<FingerprintDto>> GetAllFingerprintsAsync()
    {
        var resp = await _client.GetAsync(
            "https://api.clinic.vn/api/patients/fingerprints/all");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<FingerprintDto>>();
    }
}
```

```csharp
// SignalR connection từ desktop client
var connection = new HubConnectionBuilder()
    .WithUrl("https://api.clinic.vn/hubs/fingerprint?apiKey=IVF-Desktop-xxx")
    .WithAutomaticReconnect()
    .Build();

connection.On<CaptureRequest>("RequestCapture", async req =>
{
    // Capture fingerprint from device
    var template = await CaptureFromDevice(req);
    await connection.InvokeAsync("SubmitCaptureResult", template);
});

await connection.StartAsync();
```

### 18.5 Microservice bên ngoài gọi Vault qua API

Nếu dịch vụ bên ngoài (Python, Node.js, ...) cần sử dụng vault:

> **Lưu ý quan trọng**:
>
> - **Vault Token** chỉ hiển thị plaintext **1 lần** khi tạo — lưu trữ an toàn
> - **API Key** lưu BCrypt hash trong DB — plaintext chỉ trả về khi tạo/rotate
> - **Dynamic Credentials** tự động hết hạn qua `VALID UNTIL` — `VaultLeaseMaintenanceService` DROP ROLE mỗi 5 phút
> - **Vault Policy** nên dùng path cụ thể (`patients/medical-history`) hơn là wildcard (`**`)
> - **Triple auth**: Hệ thống hỗ trợ JWT Bearer, X-Vault-Token, và X-API-Key — middleware pipeline: VaultToken → ApiKey → JWT

```python
# Python — đọc secret từ Vault API
import requests

VAULT_URL = "https://api.clinic.vn/api/keyvault"
HEADERS = {"Authorization": "Bearer <admin-jwt>"}

# Đọc secret
resp = requests.get(f"{VAULT_URL}/secrets/database/connection-string", headers=HEADERS)
secret = resp.json()["value"]

# Mã hóa dữ liệu
resp = requests.post(f"{VAULT_URL}/encrypt", headers=HEADERS, json={
    "plaintext": "sensitive-data",
    "purpose": "Data"
})
encrypted = resp.json()
# {"ciphertextBase64": "...", "ivBase64": "...", "purpose": "Data"}

# Giải mã
resp = requests.post(f"{VAULT_URL}/decrypt", headers=HEADERS, json={
    "ciphertextBase64": encrypted["ciphertextBase64"],
    "ivBase64": encrypted["ivBase64"],
    "purpose": "Data"
})
plaintext = resp.json()["plaintext"]
```

```javascript
// Node.js — đọc secret từ Vault API
const axios = require("axios");

const VAULT_URL = "https://api.clinic.vn/api/keyvault";
const headers = { Authorization: "Bearer <admin-jwt>" };

// Lưu secret
await axios.post(
  `${VAULT_URL}/secrets`,
  {
    path: "node-service/db-password",
    value: "secure-pass-123",
  },
  { headers },
);

// Đọc secret
const { data } = await axios.get(
  `${VAULT_URL}/secrets/node-service/db-password`,
  { headers },
);
console.log(data.value); // "secure-pass-123"
```

---

## Checklist tích hợp nhanh

- [ ] Xác định bảng/trường nhạy cảm
- [ ] Tạo `EncryptionConfig` qua API hoặc Seeder
- [ ] Tạo `FieldAccessPolicy` cho từng role cần giới hạn
- [ ] Inject `IVaultSecretService` vào service cần đọc/ghi secret
- [ ] Inject `IKeyVaultService` nếu cần mã hóa/giải mã trực tiếp
- [ ] Thêm `IZeroTrustProtected` vào các Command nhạy cảm
- [ ] Thêm `IFieldAccessProtected` vào các Query cần mask
- [ ] Lưu connection strings, API keys, credentials vào Vault
- [ ] Tạo API key cho desktop client/service qua `POST /api/keyvault/keys`
- [ ] Inject `IApiKeyValidator` vào endpoint/hub cần xác thực API key
- [ ] Test middleware pipeline: VaultToken → ApiKey → JWT
- [ ] Test với `GET /api/keyvault/health` và `POST /api/keyvault/test-connection`
- [ ] Kiểm tra audit logs tại `GET /api/keyvault/audit-logs`

---

## Lưu ý bảo mật

1. **Không hardcode secret** trong code hoặc appsettings — luôn đọc từ Vault
2. **Production**: Dùng `UseManagedIdentity: true`, không dùng ClientSecret
3. **ClientSecret** trong appsettings phải để rỗng (`""`) — chỉ dùng cho dev
4. **Rotate key** định kỳ qua `POST /api/keyvault/keys/rotate`
5. **Audit log** tự động ghi mọi thao tác — review định kỳ
6. **Field Access Policy** phải theo nguyên tắc **least privilege** — mặc định `none`, chỉ mở `full` cho role cần thiết
7. **Auto-unseal** chỉ nên cấu hình ở production với Azure Key Vault thật
8. **API Key** nên lưu hash (BCrypt) trong DB (`ApiKeyManagement`) thay vì plaintext trong config — migrate bằng `POST /api/keyvault/keys`
9. **Desktop API key** trong `appsettings.Development.json` chỉ dùng cho dev — production phải dùng DB-backed keys
10. **Constant-time comparison** cho config-based keys chống **timing attack** — đã tích hợp sẵn trong `ApiKeyValidator`
11. **Triple auth**: Hệ thống hỗ trợ cả **JWT Bearer**, **X-Vault-Token**, và **X-API-Key** — middleware pipeline: VaultToken → ApiKey → JWT

---

_Tài liệu cập nhật: 03/2026 — thêm API Key Authentication (Section 8)_
_Áp dụng cho: IVF Information System — .NET 10 + Angular 21_

---

## 19. Tích hợp Vault với CA/mTLS (Certificate Authority)

### 19.1 Tổng quan

Hệ thống Certificate Authority (`CertificateAuthorityService`) đã được tích hợp với Vault để mã hóa private key bằng **envelope encryption** (AES-256-GCM + KEK) thay vì `IDataProtectionProvider` (DPAPI) đơn giản.

```
Private Key PEM
  └── encrypted by DEK (random 256-bit, purpose: Certificate)
      └── DEK encrypted by KEK (random 256-bit)
          └── KEK wrapped by Azure RSA-OAEP-256 (or local AES-256-GCM fallback)
```

### 19.2 Kiến trúc mã hóa

```
┌─────────────────────────────────────────────────────────┐
│              CertificateAuthorityService                 │
│                                                          │
│  ProtectKeyAsync(keyPem)                                │
│    ├── IKeyVaultService.EncryptAsync(keyBytes, Certificate) │
│    │     └── AES-256-GCM + KEK (envelope encryption)    │
│    │     └── Stored as "VAULT:{ciphertext}:{iv}"        │
│    └── Fallback: IDataProtectionProvider (DPAPI)        │
│                                                          │
│  UnprotectKeyAsync(protectedKey)                        │
│    ├── "-----BEGIN" → Legacy unencrypted (return as-is) │
│    ├── "VAULT:" → IKeyVaultService.DecryptAsync()       │
│    └── Else → IDataProtectionProvider.Unprotect()       │
└─────────────────────────────────────────────────────────┘
```

### 19.3 Ba format mã hóa được hỗ trợ

| Format               | Prefix/Pattern        | Encryption                | Migration                      |
| -------------------- | --------------------- | ------------------------- | ------------------------------ |
| Legacy (unencrypted) | `-----BEGIN`          | None                      | Auto-detected, returned as-is  |
| DPAPI                | Base64 (no prefix)    | `IDataProtectionProvider` | Fallback khi Vault unavailable |
| Vault Envelope       | `VAULT:{base64}:{iv}` | AES-256-GCM + KEK         | Mặc định cho key mới           |

### 19.4 Vault Audit cho CA Operations

Mọi thao tác mã hóa/giải mã private key đều được ghi vào `vault_audit_logs` bảng:

| Action           | ResourceType         | Khi nào                                  |
| ---------------- | -------------------- | ---------------------------------------- |
| `ca.key.encrypt` | CertificateAuthority | Tạo Root CA, Intermediate CA, Issue cert |
| `ca.key.decrypt` | CertificateAuthority | Deploy cert, Sign CRL, Renew cert        |

### 19.5 KeyPurpose: Certificate

Enum `KeyPurpose` đã được mở rộng thêm giá trị `Certificate` để Vault quản lý DEK riêng cho CA:

```csharp
public enum KeyPurpose
{
    Data,         // Encrypt PHI/patient data at rest
    Session,      // Encrypt session tokens
    Api,          // Encrypt API keys for desktop clients
    Backup,       // Encrypt database backups
    MasterSalt,   // Salt for password hashing
    Certificate   // Encrypt CA/mTLS private keys at rest ← NEW
}
```

### 19.6 Backward Compatibility

- **Legacy keys**: `UnprotectKeyAsync()` tự động phát hiện format PEM không mã hóa
- **DPAPI keys**: Các key đã được mã hóa bằng DPAPI vẫn được giải mã bình thường
- **Vault keys**: Key mới sẽ tự động dùng Vault envelope encryption
- **Fallback**: Nếu Vault unavailable, tự động fallback về DPAPI

### 19.7 API thay đổi

Không có thay đổi API endpoints. Integration hoàn toàn transparent cho client.

### 19.8 Cấu hình

Không cần cấu hình thêm. Vault integration tự động hoạt động nếu `AzureKeyVault:Enabled` đã được bật trong appsettings. Nếu Vault chưa cấu hình, hệ thống sử dụng local AES-256-GCM fallback (cùng cấp độ bảo mật, chỉ không có Azure HSM wrap).

---

## 20. Secret Rotation Engine

### 20.1 Tổng quan

Tự động xoay secrets theo lịch cấu hình, theo pattern Amazon Secrets Manager (30/60/90-day schedules). Hỗ trợ callback hooks để thông báo các dịch vụ phụ thuộc sau khi xoay.

### 20.2 Interface

```csharp
public interface ISecretRotationService
{
    Task<bool> RotateSecretAsync(string secretPath, CancellationToken ct = default);
    Task<List<SecretRotationSchedule>> GetDueRotationsAsync(CancellationToken ct = default);
    Task ExecuteDueRotationsAsync(CancellationToken ct = default);
}
```

### 20.3 Rotation Schedule Entity

```csharp
SecretRotationSchedule.Create(
    secretPath: "secret/db-password",
    rotationIntervalDays: 30,
    gracePeriodHours: 24,
    automaticallyRotate: true,
    rotationStrategy: "generate", // "generate" | "callback"
    callbackUrl: null);
```

| Thuộc tính             | Mô tả                                                    |
| ---------------------- | -------------------------------------------------------- |
| `SecretPath`           | Đường dẫn secret cần xoay                                |
| `RotationIntervalDays` | Chu kỳ xoay (ngày)                                       |
| `GracePeriodHours`     | Thời gian chờ trước khi đánh dấu `overdue`               |
| `AutomaticallyRotate`  | `true` = xoay tự động khi đến hạn                        |
| `RotationStrategy`     | `"generate"` = tạo value mới, `"callback"` = gọi webhook |
| `CallbackUrl`          | URL webhook nếu strategy = `"callback"`                  |
| `NextRotationAt`       | Thời điểm xoay tiếp theo (tự động tính)                  |
| `IsDueForRotation`     | Computed property: `NextRotationAt <= DateTime.UtcNow`   |

### 20.4 Sử dụng

```csharp
// Tạo rotation schedule
await repo.AddRotationScheduleAsync(SecretRotationSchedule.Create(
    "secret/api-key", 90));

// Xoay thủ công
await rotationService.RotateSecretAsync("secret/api-key");

// Chạy tất cả rotations đến hạn (gọi từ BackgroundService)
await rotationService.ExecuteDueRotationsAsync();
```

### 20.5 Dashboard hiển thị

`GET /api/keyvault/dashboard` trả về:

```json
{
  "rotation": {
    "activeSchedules": 5,
    "overdueCount": 0,
    "lastRotationAt": "2026-02-28T12:00:00Z"
  }
}
```

---

## 21. DEK Rotation

### 21.1 Tổng quan

Xoay Data Encryption Key (DEK) theo phiên bản, lưu trữ key cũ để giải mã dữ liệu đã mã hóa trước đó, và hỗ trợ re-encrypt batch toàn bộ dữ liệu sang key mới. Theo pattern Google sharded encryption.

### 21.2 Interface

```csharp
public interface IDekRotationService
{
    /// Xoay DEK: lưu key cũ tại dek-{purpose}-v{N}, tạo key mới tại dek-{purpose}
    Task<DekRotationResult> RotateDekAsync(string purpose, CancellationToken ct = default);

    /// Re-encrypt tất cả encrypted fields trong 1 table sang DEK mới
    Task<ReEncryptionResult> ReEncryptTableAsync(string tableName, CancellationToken ct = default);

    /// Lấy thông tin phiên bản DEK hiện tại
    Task<DekVersionInfo> GetDekVersionInfoAsync(string purpose, CancellationToken ct = default);

    /// Theo dõi tiến trình re-encrypt
    Task<ReEncryptionProgress> GetReEncryptionProgressAsync(string tableName, CancellationToken ct = default);
}
```

### 21.3 Kiến trúc phiên bản

```
Vault Settings:
  dek-{purpose}         → Current DEK (256-bit, base64)
  dek-{purpose}-v1      → Archived DEK version 1
  dek-{purpose}-v2      → Archived DEK version 2
  dek-version-{purpose} → Version metadata JSON

Version Metadata:
{
  "CurrentVersion": 3,
  "RotatedAt": "2026-03-01T00:00:00Z",
  "PreviousVersions": [1, 2]
}
```

### 21.4 Quy trình xoay DEK

```
1. Đọc DEK hiện tại tại dek-{purpose}
2. Lưu DEK hiện tại tại dek-{purpose}-v{N} (archive)
3. Tạo DEK mới (256-bit random)
4. Lưu DEK mới tại dek-{purpose}
5. Cập nhật version metadata
6. Ghi audit log: "dek.rotated"
```

### 21.5 Re-encryption

```csharp
// Re-encrypt toàn bộ patients table sang DEK mới
var result = await dekRotation.ReEncryptTableAsync("patients");
// result.RowsProcessed, result.RowsFailed, result.Duration
```

Quá trình re-encrypt:

- Đọc EncryptionConfig → xác định encrypted fields
- Decrypt mỗi field bằng DEK cũ
- Encrypt lại bằng DEK mới
- Batch update (không lock table)

---

## 22. DB Credential Rotation

### 22.1 Tổng quan

Google-style dual-credential (A/B slot) rotation cho database connections. Luôn có 2 credential active: slot A và slot B. Khi xoay, credential cũ bị revoke, credential mới được tạo ở slot standby, sau đó swap active slot.

### 22.2 Interface

```csharp
public interface IDbCredentialRotationService
{
    Task<DbCredentialRotationResult> RotateAsync(CancellationToken ct = default);
    Task<DualCredentialStatus> GetStatusAsync(CancellationToken ct = default);
    Task<string?> GetActiveConnectionStringAsync(CancellationToken ct = default);
}
```

### 22.3 Dual-Credential Architecture

```
┌──────────────────────────────────────────┐
│          DB Rotation State               │
│  (vault setting: "db-rotation-state")    │
│                                          │
│  ActiveSlot: "A" | "B"                   │
│  SlotA:                                  │
│    CredentialId: "uuid"                  │
│    Username: "ivf_dyn_abc123"            │
│    ExpiresAt: "2026-03-02T00:00:00Z"     │
│    ConnectionString: "Host=..."          │
│  SlotB:                                  │
│    CredentialId: "uuid"                  │
│    Username: "ivf_dyn_xyz789"            │
│    ExpiresAt: "2026-03-02T12:00:00Z"     │
│    ConnectionString: "Host=..."          │
└──────────────────────────────────────────┘
```

### 22.4 Quy trình xoay

```
1. Đọc state hiện tại → xác định standby slot
2. Revoke credential cũ ở standby slot (DROP ROLE)
3. Tạo credential mới qua IDynamicCredentialProvider
4. Cập nhật standby slot với credential mới
5. Swap active ↔ standby
6. Lưu connection string mới vào config/ConnectionStrings/DefaultConnection
7. Ghi audit log: "db.credential.rotated"
```

Default TTL: 24 giờ (86400 giây).

---

## 23. Observability & Metrics

### 23.1 Tổng quan

Sử dụng `System.Diagnostics.Metrics` (built-in .NET) để expose real-time vault metrics. Theo Google's 4 Golden Signals: latency, traffic, errors, saturation.

### 23.2 VaultMetrics class

```csharp
// Singleton, inject vào vault services
public class VaultMetrics
{
    // Counters
    vault.secret.operations     (tags: operation=create|read|update|delete)
    vault.encryption.operations (tags: operation=encrypt|decrypt)
    vault.token.validations     (tags: result=success|failure|expired)
    vault.policy.evaluations    (tags: result=allow|deny)
    vault.zerotrust.evaluations (tags: result=allow|deny, reason=...)

    // Gauges
    vault.lease.active          (current active lease count)
    vault.dynamic_credential.active

    // Histograms
    vault.encryption.duration   (milliseconds)
    vault.key_rotation.age_seconds
}
```

### 23.3 Health Dashboard

`GET /api/keyvault/dashboard` — aggregated vault health:

```json
{
  "timestamp": "2026-03-01T12:00:00Z",
  "secrets": { "total": 42 },
  "leases": { "active": 5, "expiringSoon": 1 },
  "rotation": {
    "activeSchedules": 3,
    "overdueCount": 0,
    "lastRotationAt": "2026-02-28T12:00:00Z"
  },
  "keyVault": { "healthy": true },
  "compliance": {
    "score": "5/5",
    "percentage": 100,
    "checks": {
      "encryptionAtRest": true,
      "keyVaultHealthy": true,
      "noOverdueRotations": true,
      "activeRotationSchedules": true,
      "noExpiringLeases": true
    }
  }
}
```

---

## 24. SIEM Integration

### 24.1 Tổng quan

Phát hành security events theo format Syslog/CEF (Common Event Format) cho tích hợp Microsoft Sentinel, Splunk, AWS CloudTrail.

### 24.2 Interface

```csharp
public interface ISecurityEventPublisher
{
    Task PublishAsync(VaultSecurityEvent securityEvent, CancellationToken ct = default);
}
```

### 24.3 VaultSecurityEvent

```csharp
public class VaultSecurityEvent
{
    public required string EventType { get; init; }     // e.g., "secret.accessed", "policy.denied"
    public required SecuritySeverity Severity { get; init; }  // Info, Low, Medium, High, Critical
    public required string Source { get; init; }         // Service name
    public required string Action { get; init; }         // Action performed
    public string? UserId { get; init; }
    public string? IpAddress { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? Outcome { get; init; }               // "success" | "failure" | "deny"
    public string? Reason { get; init; }
}
```

### 24.4 Các event types

| EventType                    | Severity | Mô tả                                             |
| ---------------------------- | -------- | ------------------------------------------------- |
| `secret.accessed`            | Info     | Secret được đọc                                   |
| `secret.rotated`             | Info     | Secret đã xoay thành công                         |
| `policy.denied`              | High     | Truy cập bị từ chối bởi policy                    |
| `session.ip_changed`         | High     | IP thay đổi trong session (potential hijack)      |
| `session.country_changed`    | High     | Country thay đổi (impossible travel)              |
| `session.expired`            | Medium   | Session hết hạn                                   |
| `session.zt_denied`          | High     | Zero Trust re-evaluation thất bại                 |
| `vault.backup.created`       | Info     | Backup vault state thành công                     |
| `vault.unseal.all_failed`    | Critical | Tất cả unseal providers đều thất bại              |
| `certificate.renewal.failed` | High     | Auto-renewal certificate thất bại                 |
| `certificate.expiry.warning` | Varies   | Cảnh báo certificate sắp hết hạn (30/14/7/1 ngày) |
| `dek.rotated`                | Info     | DEK đã xoay thành công                            |
| `db.credential.rotated`      | Info     | DB credential đã xoay (A/B slot)                  |

### 24.5 Certificate Expiry Warnings

`CertAutoRenewalService` phát cảnh báo theo bậc:

| Thời gian còn lại | Severity |
| ----------------- | -------- |
| ≤ 1 ngày          | Critical |
| ≤ 7 ngày          | High     |
| ≤ 14 ngày         | Medium   |
| ≤ 30 ngày         | Low      |

---

## 25. KMS Provider Abstraction

### 25.1 Tổng quan

Provider-agnostic Key Management Service abstraction. Cho phép chuyển đổi giữa Azure Key Vault, Local AES, hoặc custom KMS mà không thay đổi application code. Theo pattern như AWS KMS client / Azure KeyClient.

### 25.2 Interface

```csharp
public interface IKmsProvider
{
    string ProviderName { get; }

    Task<KmsKeyInfo> CreateKeyAsync(KmsCreateKeyRequest request, CancellationToken ct = default);
    Task<KmsKeyInfo?> GetKeyInfoAsync(string keyName, CancellationToken ct = default);
    Task<List<KmsKeyInfo>> ListKeysAsync(CancellationToken ct = default);
    Task<KmsKeyInfo> RotateKeyAsync(string keyName, CancellationToken ct = default);

    Task<KmsEncryptResult> EncryptAsync(string keyName, byte[] plaintext, CancellationToken ct = default);
    Task<byte[]> DecryptAsync(string keyName, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken ct = default);

    Task<KmsWrapResult> WrapKeyAsync(string wrappingKeyName, byte[] keyToWrap, CancellationToken ct = default);
    Task<byte[]> UnwrapKeyAsync(string wrappingKeyName, byte[] wrappedKey, byte[] iv, byte[] tag, CancellationToken ct = default);
}
```

### 25.3 Providers

| Provider             | File                               | Mô tả                                      |
| -------------------- | ---------------------------------- | ------------------------------------------ |
| **LocalKmsProvider** | `Services/Kms/LocalKmsProvider.cs` | AES-256-GCM, keys lưu trong vault settings |
| **AzureKmsProvider** | `Services/Kms/AzureKmsProvider.cs` | Bridge adapter tới `IKeyVaultService`      |

### 25.4 Cấu hình

```json
{
  "KmsProvider": "Azure" // "Azure" | "Local"
}
```

Đăng ký DI tự động qua `services.AddKmsProvider(configuration)`:

```csharp
// KmsProviderRegistration.cs
public static IServiceCollection AddKmsProvider(
    this IServiceCollection services, IConfiguration configuration)
{
    var provider = configuration.GetValue<string>("KmsProvider") ?? "Local";
    return provider switch
    {
        "Azure" => services.AddScoped<IKmsProvider, AzureKmsProvider>(),
        _ => services.AddScoped<IKmsProvider, LocalKmsProvider>(),
    };
}
```

### 25.5 Key Versioning (Local)

```
Vault Settings:
  kms-key-{name}       → Current key metadata + encrypted key material
  kms-key-{name}-v1    → Archived version 1
  kms-key-{name}-v2    → Archived version 2
```

---

## 26. Continuous Access Evaluation (CAE)

### 26.1 Tổng quan

Google CAE-style continuous session evaluation. Thay vì kiểm tra 1 lần khi request bắt đầu, hệ thống liên tục đánh giá session để phát hiện anomaly và yêu cầu re-auth hoặc step-up authentication.

### 26.2 Interface

```csharp
public interface IContinuousAccessEvaluator
{
    /// Đánh giá session: kiểm tra tuổi, IP, country, ZT re-eval
    Task<CaeDecision> EvaluateSessionAsync(CaeSessionContext session, CancellationToken ct = default);

    /// Kiểm tra yêu cầu step-up auth cho critical actions
    Task<StepUpRequirement> CheckStepUpRequirementAsync(
        ZTVaultAction action, string userId, AuthLevel currentLevel, CancellationToken ct = default);

    /// Bind vault token với session (correlated revocation)
    Task BindSessionTokenAsync(string sessionId, Guid vaultTokenId, string userId, CancellationToken ct = default);

    /// Revoke tất cả tokens liên kết với session
    Task<int> RevokeSessionTokensAsync(string sessionId, string reason, CancellationToken ct = default);
}
```

### 26.3 Session Evaluations

| Check                | Điều kiện                 | Kết quả                                |
| -------------------- | ------------------------- | -------------------------------------- |
| **Session Age**      | Tuổi > 8 giờ              | Deny → yêu cầu Password re-auth        |
| **IP Changed**       | `IpChanged = true`        | Deny → yêu cầu Password re-auth        |
| **Country Changed**  | `CountryChanged = true`   | Deny → yêu cầu MFA (impossible travel) |
| **ZT Re-evaluation** | `IZeroTrustService` fails | Deny → follow ZT decision              |

### 26.4 Step-Up Authentication

Critical vault actions yêu cầu MFA nếu `currentLevel < AuthLevel.MFA`:

| ZTVaultAction      | Yêu cầu MFA | Timeout |
| ------------------ | ----------- | ------- |
| `SecretDelete`     | ✅          | 5 phút  |
| `SecretExport`     | ✅          | 5 phút  |
| `KeyRotate`        | ✅          | 5 phút  |
| `BreakGlassAccess` | ✅          | 5 phút  |
| `SecretRead`       | ❌          | —       |
| `SecretWrite`      | ❌          | —       |
| `VaultUnseal`      | ❌          | —       |

### 26.5 Session-Token Binding

```
Vault Settings:
  session-binding-{sessionId} → {
    "SessionId": "...",
    "VaultTokenId": "uuid",
    "UserId": "...",
    "BoundAt": "2026-03-01T..."
  }
```

Khi session bị revoke (IP change, country change), tất cả vault tokens liên kết cũng bị revoke đồng thời.

---

## 27. Compliance Scoring Engine

### 27.1 Tổng quan

Real-time compliance scoring engine đánh giá vault state theo 3 frameworks: HIPAA Security Rule, SOC 2 Trust Service Criteria, và GDPR. Tự động tính điểm từ live vault data, không cần manual audit.

### 27.2 Interface

```csharp
public interface IComplianceScoringEngine
{
    /// Đánh giá tất cả frameworks, trả về overall score + grade
    Task<ComplianceReport> EvaluateAsync(CancellationToken ct = default);

    /// Đánh giá 1 framework cụ thể
    Task<FrameworkScore> EvaluateFrameworkAsync(ComplianceFramework framework, CancellationToken ct = default);
}
```

### 27.3 Frameworks & Controls

#### HIPAA Security Rule (10 controls, 100 điểm)

| Control ID | Tên                   | §HIPAA                | Kiểm tra                          |
| ---------- | --------------------- | --------------------- | --------------------------------- |
| HIPAA-1    | Encryption at Rest    | §164.312(a)(2)(iv)    | EncryptionConfig count > 0        |
| HIPAA-2    | Audit Controls        | §164.312(b)           | Audit log count > 0               |
| HIPAA-3    | Data Integrity        | §164.312(c)(1)        | Secrets managed + versioned       |
| HIPAA-4    | Authentication        | §164.312(d)           | Policies + user assignments exist |
| HIPAA-5    | Transmission Security | §164.312(e)(1)        | Key vault TLS healthy             |
| HIPAA-6    | Token Management      | §164.308(a)(5)(ii)(D) | No expired active tokens          |
| HIPAA-7    | Secret Rotation       | §164.308(a)(4)        | Active rotation schedules exist   |
| HIPAA-8    | Field-Level Access    | §164.312(a)(1)        | Field access policies defined     |
| HIPAA-9    | Key Protection        | §164.310(d)(1)        | Auto-unseal configured            |
| HIPAA-10   | Lease Management      | §164.308(a)(1)(ii)(D) | Lease management active           |

#### SOC 2 Trust Service Criteria (7 controls, 70 điểm)

| Control ID | Tên                 | Kiểm tra                           |
| ---------- | ------------------- | ---------------------------------- |
| SOC2-CC6.1 | Logical Access      | Vault policies + user bindings     |
| SOC2-CC6.3 | Encryption Controls | Tables with field-level encryption |
| SOC2-CC6.7 | Data Transmission   | Key vault TLS verified             |
| SOC2-CC7.2 | Monitoring          | Audit entries (100+ = Pass)        |
| SOC2-CC8.1 | Change Management   | Secrets with version history       |
| SOC2-A1.2  | Recovery Mechanisms | DEK versioning active              |
| SOC2-C1.1  | Confidentiality     | Field access policies defined      |

#### GDPR (7 controls, 70 điểm)

| Control ID | Tên                         | §GDPR       | Kiểm tra                       |
| ---------- | --------------------------- | ----------- | ------------------------------ |
| GDPR-32a   | Pseudonymisation            | Art. 32.1.a | Field-level encryption configs |
| GDPR-32b   | Confidentiality             | Art. 32.1.b | Access policies enforced       |
| GDPR-32c   | Resilience                  | Art. 32.1.c | Auto-unseal for availability   |
| GDPR-32d   | Regular Testing             | Art. 32.1.d | Audit log evidence             |
| GDPR-5f    | Integrity & Confidentiality | Art. 5.1.f  | Encryption + access policies   |
| GDPR-25    | Privacy by Design           | Art. 25     | Automated rotation schedules   |
| GDPR-30    | Processing Records          | Art. 30     | Vault audit log maintained     |

### 27.4 Grade Calculation

| Percentage | Grade |
| ---------- | ----- |
| ≥ 95%      | A+    |
| ≥ 90%      | A     |
| ≥ 85%      | A-    |
| ≥ 80%      | B+    |
| ≥ 75%      | B     |
| ≥ 70%      | B-    |
| ≥ 65%      | C+    |
| ≥ 60%      | C     |
| ≥ 50%      | D     |
| < 50%      | F     |

### 27.5 Response Sample

```json
{
  "evaluatedAt": "2026-03-01T12:00:00Z",
  "overallScore": 230,
  "maxScore": 240,
  "percentage": 95.8,
  "grade": "A+",
  "frameworks": [
    {
      "framework": "Hipaa",
      "name": "HIPAA",
      "score": 100,
      "maxScore": 100,
      "percentage": 100.0,
      "controls": [
        {
          "controlId": "HIPAA-1",
          "name": "Encryption at Rest",
          "status": "Pass",
          "score": 10,
          "maxScore": 10
        }
      ]
    }
  ]
}
```

---

## 28. Vault Disaster Recovery

### 28.1 Tổng quan

Encrypted backup/restore toàn bộ vault state (secrets, policies, settings, encryption configs). Backup được mã hóa bằng AES-256-GCM với key derive từ PBKDF2 (100K iterations). Theo HashiCorp Vault snapshot pattern.

### 28.2 Interface

```csharp
public interface IVaultDrService
{
    /// Tạo encrypted backup
    Task<VaultBackupResult> BackupAsync(string backupKey, CancellationToken ct = default);

    /// Restore từ encrypted backup
    Task<VaultRestoreResult> RestoreAsync(byte[] backupData, string backupKey, CancellationToken ct = default);

    /// Verify backup integrity (không restore)
    Task<VaultBackupValidation> ValidateBackupAsync(byte[] backupData, string backupKey, CancellationToken ct = default);

    /// DR readiness status
    Task<DrReadinessStatus> GetReadinessAsync(CancellationToken ct = default);
}
```

### 28.3 Backup Format

```
Byte layout:
  [salt: 16 bytes]          ← PBKDF2 salt (random)
  [iv: 12 bytes]            ← AES-GCM nonce (random)
  [tag: 16 bytes]           ← AES-GCM authentication tag
  [ciphertext: variable]    ← Encrypted JSON snapshot

Key derivation:
  PBKDF2-SHA256(password, salt, 100000 iterations) → 256-bit key
```

### 28.4 Snapshot Contents

```json
{
  "backupId": "vault-backup-20260301-120000",
  "createdAt": "2026-03-01T12:00:00Z",
  "secrets": [
    { "path": "secret/db", "encryptedData": "...", "iv": "...", "version": 3 }
  ],
  "policies": [
    {
      "name": "admin",
      "pathPattern": "secret/*",
      "capabilities": ["read", "write"]
    }
  ],
  "settings": [{ "key": "dek-version-data", "valueJson": "{...}" }],
  "encryptionConfigs": [
    {
      "tableName": "patients",
      "encryptedFields": ["ssn", "phone"],
      "dekPurpose": "data"
    }
  ]
}
```

### 28.5 Restore Behavior

- Chỉ restore entities chưa tồn tại (skip existing)
- Secrets: kiểm tra theo `path`
- Policies: kiểm tra theo `name`
- Settings: kiểm tra theo `key`
- EncryptionConfigs: kiểm tra theo `tableName`
- Ghi audit log: `"vault.backup.restored"`

### 28.6 DR Readiness

```csharp
var status = await drService.GetReadinessAsync();
// status.ReadinessGrade: "A" (5/5 checks) → "F" (0-1/5 checks)
```

| Check                  | Mô tả                               |
| ---------------------- | ----------------------------------- |
| Auto-unseal configured | VaultAutoUnseal entity exists       |
| Encryption active      | EncryptionConfig count > 0          |
| Active secrets         | Secrets count > 0                   |
| Active policies        | Policies count > 0                  |
| Last backup recent     | vault-last-backup-at setting exists |

---

## 29. Multi-Provider Unseal

### 29.1 Tổng quan

Multi-KMS auto-unseal với priority-based failover. Nếu primary provider (ví dụ Azure KV) fail, tự động thử secondary provider. Publish SIEM event nếu tất cả providers đều fail.

### 29.2 Interface

```csharp
public interface IMultiProviderUnsealService
{
    /// Auto-unseal theo priority order, fallback nếu primary fail
    Task<UnsealResult> AutoUnsealAsync(CancellationToken ct = default);

    /// Cấu hình provider mới
    Task<bool> ConfigureProviderAsync(UnsealProviderConfig config, string masterPassword, CancellationToken ct = default);

    /// Lấy trạng thái tất cả providers
    Task<List<UnsealProviderStatus>> GetProviderStatusAsync(CancellationToken ct = default);
}
```

### 29.3 Provider Architecture

```
┌──────────────────────────────────────────────────┐
│        MultiProviderUnsealService                │
│                                                  │
│   Priority 1: AzureKmsProvider (primary)         │
│   Priority 2: LocalKmsProvider (fallback)        │
│                                                  │
│   Vault Setting: "unseal-providers" → JSON[]     │
│                                                  │
│   On all fail → SIEM event:                      │
│     "vault.unseal.all_failed" (Critical)         │
└──────────────────────────────────────────────────┘
```

### 29.4 Cấu hình provider

```csharp
await unsealService.ConfigureProviderAsync(
    new UnsealProviderConfig(
        ProviderId: "azure-primary",
        ProviderType: "Azure",        // "Azure" | "Local"
        Priority: 1,                  // Lower = tried first
        KeyIdentifier: "unseal-key"),
    masterPassword: "vault-master-password");
```

### 29.5 Provider Status

```json
[
  {
    "providerId": "azure-primary",
    "providerType": "Azure",
    "priority": 1,
    "available": true,
    "lastUsedAt": "2026-03-01T00:00:00Z",
    "error": null
  },
  {
    "providerId": "local-fallback",
    "providerType": "Local",
    "priority": 2,
    "available": true,
    "lastUsedAt": null,
    "error": null
  }
]
```

---

## 30. Test Coverage

### 30.1 Tổng quan

227 unit tests covering tất cả vault services. Framework: xUnit 2.9.3 + FluentAssertions 8.8.0 + Moq 4.20.72.

### 30.2 Test Files

| File                                  | Tests | Mô tả                                           |
| ------------------------------------- | ----- | ----------------------------------------------- |
| `VaultEntityTests.cs`                 | 26    | Domain entity factory methods, validation       |
| `VaultSecretServiceTests.cs`          | 9     | Secret CRUD, encryption round-trip              |
| `VaultTokenValidatorTests.cs`         | 10    | Token expiry, exhaustion, capability matching   |
| `VaultPolicyEvaluatorTests.cs`        | 11    | Admin bypass, policy merging, path patterns     |
| `LeaseManagerTests.cs`                | 10    | Lease TTL, renewal, expiry                      |
| `VaultBehaviorTests.cs`               | 6     | MediatR pipeline behaviors                      |
| `FieldAccessTests.cs`                 | 13    | Field masking, role-based access                |
| `DynamicCredentialProviderTests.cs`   | 9     | PostgreSQL dynamic role creation/revocation     |
| `SecretRotationServiceTests.cs`       | 20    | Rotation schedules, auto-rotate, callbacks      |
| `VaultMetricsTests.cs`                | 17    | Counter/histogram recording, tags               |
| `SecurityEventPublisherTests.cs`      | 6     | SIEM event formatting, publishing               |
| `DekRotationServiceTests.cs`          | 15    | DEK versioning, archive, re-encrypt             |
| `DbCredentialRotationServiceTests.cs` | 10    | Dual A/B slot rotation, state persistence       |
| `KmsProviderTests.cs`                 | 13    | Local + Azure KMS provider operations           |
| `ContinuousAccessEvaluatorTests.cs`   | 11    | Session eval, step-up, token binding/revocation |
| `ComplianceScoringEngineTests.cs`     | 12    | HIPAA/SOC 2/GDPR scoring, grade calculation     |
| `VaultDrServiceTests.cs`              | 11    | Encrypted backup/restore round-trip, validation |
| `MultiProviderUnsealServiceTests.cs`  | 8     | Priority failover, provider config, status      |

### 30.3 Chạy tests

```bash
# Chạy tất cả vault tests
dotnet test tests/IVF.Tests/IVF.Tests.csproj --filter "FullyQualifiedName~Vault"

# Chạy test cho 1 service cụ thể
dotnet test --filter "FullyQualifiedName~ComplianceScoringEngine"

# Chạy test với coverage
dotnet test --collect:"XPlat Code Coverage"
```
