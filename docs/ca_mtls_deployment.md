# Certificate Authority, mTLS & Deployment — Developer Guide

> Self-contained Private PKI for the IVF system. Built with .NET 10 `System.Security.Cryptography` — no external OpenSSL dependency.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Source Code Map](#2-source-code-map)
3. [Domain Entities](#3-domain-entities)
4. [Service Layer — CertificateAuthorityService](#4-service-layer--certificateauthorityservice)
5. [API Endpoints](#5-api-endpoints)
6. [Certificate Lifecycle & Code Patterns](#6-certificate-lifecycle--code-patterns)
7. [Certificate Revocation & CRL Generation](#7-certificate-revocation--crl-generation)
8. [OCSP Responder](#8-ocsp-responder)
9. [Certificate Deployment Engine](#9-certificate-deployment-engine)
10. [Auto-Renewal Background Service](#10-auto-renewal-background-service)
11. [mTLS / Digital Signing Integration](#11-mtls--digital-signing-integration)
12. [Audit Trail](#12-audit-trail)
13. [Angular Frontend](#13-angular-frontend)
14. [Docker Network & Container Topology](#14-docker-network--container-topology)
15. [Development Workflow](#15-development-workflow)
16. [Extending the PKI System](#16-extending-the-pki-system)
17. [Configuration Reference](#17-configuration-reference)
18. [Troubleshooting](#18-troubleshooting)

---

## 1. Architecture Overview

The IVF system implements a **self-contained Private PKI** (Public Key Infrastructure) that enables:

- **TLS encryption** for PostgreSQL (primary + replica) and MinIO (primary + replica)
- **mTLS** (mutual TLS) for API ↔ SignServer ↔ EJBCA communication
- **Certificate Revocation Lists** (CRL) per RFC 5280
- **OCSP-like status queries** per RFC 6960
- **Automated certificate rotation** with configurable renewal windows
- **Real-time deployment monitoring** via SignalR

### Component Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                    IVF API (.NET 10)                          │
│  ┌────────────────────────────────────────────────────────┐  │
│  │         CertificateAuthorityService (1464 LOC)         │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │  │
│  │  │ CA Mgmt  │ │ Cert     │ │ CRL/OCSP │ │ Deploy   │  │  │
│  │  │ Root/Int │ │ Issue    │ │ Generate │ │ Local    │  │  │
│  │  │ Create   │ │ Renew    │ │ Query    │ │ Remote   │  │  │
│  │  │ Revoke   │ │ Revoke   │ │ Publish  │ │ SSH+SCP  │  │  │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────┐  ┌──────────────────────┐         │
│  │ SignalR (BackupHub)  │  │ EF Core (PostgreSQL) │         │
│  │ Real-time deploy log │  │ 5 domain entities    │         │
│  └──────────────────────┘  └──────────────────────┘         │
└──────────┬───────────────────────────┬───────────────────────┘
           │                           │
    ┌──────▼──────┐            ┌───────▼────────┐
    │ Local Docker│            │ Remote Server  │
    │ ivf-db      │            │ 172.16.102.11  │
    │ ivf-minio   │            │ ivf-db-replica │
    │ ivf-ejbca   │            │ ivf-minio-rep  │
    │ ivf-signsvr │            │ (via SSH/SCP)  │
    └─────────────┘            └────────────────┘
```

### Technology Stack

| Component            | Technology                         | Purpose                          |
| -------------------- | ---------------------------------- | -------------------------------- |
| Key generation       | `RSA.Create(keySize)`              | RSA 2048/4096-bit keys           |
| Certificate creation | `CertificateRequest`               | X.509v3 cert builder             |
| CRL generation       | `CertificateRevocationListBuilder` | RFC 5280 CRL                     |
| ASN.1 encoding       | `System.Formats.Asn1`              | CRL Distribution Point extension |
| Hashing              | `SHA256`                           | Fingerprints, CRL digests        |
| PEM export           | Manual Base64 encoding             | Cert/Key/CRL PEM output          |
| Deployment           | `docker cp` / `scp` + `ssh`        | File transfer to containers      |
| Real-time UI         | SignalR `BackupHub`                | Step-by-step deploy progress     |
| Database             | PostgreSQL via EF Core             | Certificate storage              |
| Frontend             | Angular 21 standalone              | Admin management UI (9 tabs)     |

### Key Design Decisions

1. **No external tools** — All crypto uses `System.Security.Cryptography`. No OpenSSL or Bouncy Castle.
2. **Singleton service** — `CertificateAuthorityService` is registered as Singleton and creates its own `IServiceScope` per operation for DbContext access.
3. **Independent deploy lifetime** — Deployment operations use their own `CancellationTokenSource` (5–10 min) independent of the HTTP request, so deploys aren't canceled if the client disconnects.
4. **SignalR for progress** — Deployment steps stream to clients in real-time via BackupHub, with non-critical error handling (SignalR failures don't break deploys).

---

## 2. Source Code Map

```
src/
├── IVF.Domain/Entities/
│   ├── CertificateAuthority.cs         ← CA entity (Root/Intermediate)
│   ├── ManagedCertificate.cs           ← Issued cert entity (lifecycle tracking)
│   ├── CertDeploymentLog.cs            ← Deploy operation log + JSON log lines
│   ├── CertificateRevocationList.cs    ← RFC 5280 CRL storage
│   └── CertificateAuditEvent.cs        ← Immutable audit trail
│
├── IVF.Infrastructure/Persistence/
│   ├── IvfDbContext.cs                 ← 5 DbSets for PKI entities
│   ├── Configurations/
│   │   ├── CertificateAuthorityConfiguration.cs
│   │   ├── ManagedCertificateConfiguration.cs
│   │   ├── CertDeploymentLogConfiguration.cs
│   │   ├── CertificateRevocationListConfiguration.cs
│   │   └── CertificateAuditEventConfiguration.cs
│   └── Migrations/
│       └── 20260227072232_AddCertificateAuthority.cs
│
├── IVF.API/
│   ├── Services/
│   │   ├── CertificateAuthorityService.cs   ← All PKI logic (1464 LOC)
│   │   └── CertAutoRenewalService.cs        ← BackgroundService (hourly)
│   ├── Endpoints/
│   │   └── CertificateAuthorityEndpoints.cs ← 30+ REST endpoints
│   └── Hubs/
│       └── BackupHub.cs                     ← SignalR for deploy progress
│
ivf-client/src/app/
├── features/admin/certificate-management/
│   └── certificate-management.component.ts  ← 9-tab admin UI
├── core/services/
│   └── backup.service.ts                    ← 25+ HTTP methods for certs
└── core/models/
    └── backup.models.ts                     ← TypeScript interfaces
```

---

## 3. Domain Entities

Five EF Core entities in `IVF.Domain/Entities/` store all PKI state. All extend `BaseEntity` (provides `Id`, `CreatedAt`, `UpdatedAt`).

### 3.1 CertificateAuthority

**File:** `src/IVF.Domain/Entities/CertificateAuthority.cs`

Root or intermediate CA that signs certificates.

| Property                 | Type     | Description                                |
| ------------------------ | -------- | ------------------------------------------ |
| `Name`                   | string   | Human-readable CA name                     |
| `CommonName`             | string   | X.500 CN field                             |
| `Organization`           | string   | O field                                    |
| `OrganizationalUnit`     | string?  | OU field                                   |
| `Country`                | string?  | C field                                    |
| `State`                  | string?  | ST field                                   |
| `Locality`               | string?  | L field                                    |
| `Type`                   | CaType   | `Root` (0) or `Intermediate` (1)           |
| `KeyAlgorithm`           | string   | Always "RSA"                               |
| `KeySize`                | int      | 2048 or 4096                               |
| `CertificatePem`         | string   | PEM-encoded CA certificate                 |
| `PrivateKeyPem`          | string   | PEM-encoded PKCS#8 private key             |
| `ChainPem`               | string?  | Full certificate chain PEM                 |
| `Fingerprint`            | string   | SHA-256 hash (hex)                         |
| `NotBefore` / `NotAfter` | DateTime | Validity period                            |
| `NextSerialNumber`       | long     | Monotonically increasing serial allocator  |
| `NextCrlNumber`          | long     | CRL number allocator                       |
| `ParentCaId`             | Guid?    | Parent CA (for intermediates)              |
| `Status`                 | CaStatus | `Active` (0), `Revoked` (1), `Expired` (2) |

**Key Methods:**

```csharp
// Factory method — creates a new CA entity with all fields
public static CertificateAuthority Create(
    string name, string commonName, string organization, string? orgUnit,
    string country, string? state, string? locality, CaType type,
    string keyAlgorithm, int keySize, string certificatePem, string privateKeyPem,
    string fingerprint, DateTime notBefore, DateTime notAfter,
    Guid? parentCaId = null, string? chainPem = null);

// Thread-safe serial allocation — monotonically increasing
public long AllocateSerialNumber();   // Returns serial, increments NextSerialNumber
public long AllocateCrlNumber();      // Returns CRL number, increments NextCrlNumber

public void Revoke();                 // Sets Status = Revoked
public void UpdateChain(string chainPem); // Updates the certificate chain PEM
```

**Navigation:** `IssuedCertificates` → all `ManagedCertificate` entities signed by this CA.

### 3.2 ManagedCertificate

**File:** `src/IVF.Domain/Entities/ManagedCertificate.cs`

Issued end-entity certificates (server or client) with full lifecycle tracking.

| Property                 | Type              | Description                                         |
| ------------------------ | ----------------- | --------------------------------------------------- |
| `CommonName`             | string            | CN field                                            |
| `SubjectAltNames`        | string?           | Comma-separated SANs (DNS/IP)                       |
| `Type`                   | CertType          | `Server` (0) or `Client` (1)                        |
| `Purpose`                | string            | Purpose label (e.g., "pg-primary", "minio-replica") |
| `CertificatePem`         | string            | PEM certificate                                     |
| `PrivateKeyPem`          | string            | PEM private key                                     |
| `Fingerprint`            | string            | SHA-256 fingerprint                                 |
| `SerialNumber`           | string            | Hex serial number                                   |
| `NotBefore` / `NotAfter` | DateTime          | Validity window                                     |
| `KeyAlgorithm`           | string            | "RSA"                                               |
| `KeySize`                | int               | Key size in bits                                    |
| `IssuingCaId`            | Guid              | FK to signing CA                                    |
| `Status`                 | ManagedCertStatus | `Active`, `Revoked`, `Expired`, `Superseded`        |
| `DeployedTo`             | string?           | Deployment target (e.g., "ivf-db:/var/lib/...")     |
| `DeployedAt`             | DateTime?         | When last deployed                                  |
| `AutoRenewEnabled`       | bool              | Auto-renewal flag (default: true)                   |
| `RenewBeforeDays`        | int               | Days before expiry to renew (default: 30)           |
| `ValidityDays`           | int               | Calculated from `NotAfter - NotBefore`              |
| `ReplacedCertId`         | Guid?             | Previous cert in rotation chain                     |
| `ReplacedByCertId`       | Guid?             | Next cert in rotation chain                         |
| `LastRenewalAttempt`     | DateTime?         | Last auto-renewal attempt                           |
| `LastRenewalResult`      | string?           | Success/failure message                             |
| `RevocationReason`       | RevocationReason? | RFC 5280 §5.3.1 reason code                         |
| `RevokedAt`              | DateTime?         | Revocation timestamp                                |

**Rotation Chain — Linked List Pattern:**

```
Cert A (Superseded) → Cert B (Superseded) → Cert C (Active)
  ReplacedByCertId=B     ReplacedByCertId=C    ReplacedCertId=B
```

**Key Methods:**

```csharp
// Lifecycle transitions
public void Revoke(RevocationReason reason);               // → Revoked + records timestamp + reason
public void MarkDeployed(string target);                   // Records target + timestamp
public void MarkExpired();                                 // → Expired
public void MarkSuperseded(Guid replacedByCertId);         // → Superseded (rotation chain)
public void SetReplacedCert(Guid replacedCertId);          // Links back to old cert

// Auto-renewal helpers
public bool IsExpiringSoon();       // NotAfter <= UtcNow + RenewBeforeDays
public bool NeedsAutoRenewal();     // AutoRenewEnabled && Active && no replacement && expiring
public void SetAutoRenew(bool enabled, int? renewBeforeDays);
public void RecordRenewalAttempt(string result);
```

### 3.3 Enums

```csharp
// Certificate Authority type
public enum CaType { Root = 0, Intermediate = 1 }
public enum CaStatus { Active = 0, Revoked = 1, Expired = 2 }

// Certificate type
public enum CertType { Server = 0, Client = 1 }
public enum ManagedCertStatus { Active = 0, Revoked = 1, Expired = 2, Superseded = 3 }

// Deployment
public enum DeployStatus { Running = 0, Completed = 1, Failed = 2 }

// RFC 5280 §5.3.1 revocation reasons
public enum RevocationReason
{
    Unspecified = 0, KeyCompromise = 1, CaCompromise = 2,
    AffiliationChanged = 3, Superseded = 4, CessationOfOperation = 5,
    CertificateHold = 6, RemoveFromCrl = 8,
    PrivilegeWithdrawn = 9, AaCompromise = 10
}

// Audit event types
public enum CertAuditEventType
{
    CaCreated = 0, CaRevoked = 1,
    CertIssued = 10, CertRenewed = 11, CertRevoked = 12,
    CertExpired = 13, CertSuperseded = 14,
    CertDeployed = 20, CertDeployFailed = 21,
    AutoRenewTriggered = 30, AutoRenewFailed = 31,
    CrlGenerated = 40, OcspQuery = 50,
    IntermediateCaCreated = 60,
    CertRotationStarted = 70, CertRotationCompleted = 71
}
```

### 3.4 CertDeploymentLog

**File:** `src/IVF.Domain/Entities/CertDeploymentLog.cs`

Persistent log of deployment operations — each deploy creates one entity with an array of step-by-step log lines.

```csharp
public class CertDeploymentLog : BaseEntity
{
    public Guid CertificateId { get; set; }
    public string OperationId { get; set; }      // 12-char hex ID for SignalR group
    public string Target { get; set; }            // "pg-primary", "minio-replica", "custom"
    public string Container { get; set; }          // "ivf-db", "ivf-minio", etc.
    public string? RemoteHost { get; set; }        // Remote server (null for local)
    public DeployStatus Status { get; set; }       // Running → Completed/Failed
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DeployLogLine> LogLines { get; set; } = []; // JSON column

    public void AddLine(string level, string message);
    public void Complete(bool success, string? error = null);
}

public class DeployLogLine
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; }     // "info", "warn", "error", "success"
    public string Message { get; set; }
}
```

### 3.5 CertificateRevocationList

**File:** `src/IVF.Domain/Entities/CertificateRevocationList.cs`

RFC 5280 CRL with both PEM and DER encodings.

| Property                    | Type     | Description                                           |
| --------------------------- | -------- | ----------------------------------------------------- |
| `CaId` → FK                 | Guid     | Issuing CA                                            |
| `CrlNumber`                 | long     | Monotonically increasing (RFC 5280 §5.2.3)            |
| `ThisUpdate` / `NextUpdate` | DateTime | Validity: 7 days window                               |
| `CrlPem`                    | string   | PEM-encoded CRL                                       |
| `CrlDer`                    | byte[]   | DER-encoded CRL binary (served at distribution point) |
| `RevokedCount`              | int      | Number of revoked entries                             |
| `Fingerprint`               | string   | SHA-256 of DER bytes                                  |

**DB Index:** Unique composite `(CaId, CrlNumber)`, non-unique on `NextUpdate`.

### 3.6 CertificateAuditEvent

**File:** `src/IVF.Domain/Entities/CertificateAuditEvent.cs`

Immutable audit log for all PKI operations.

```csharp
public class CertificateAuditEvent : BaseEntity
{
    public Guid? CertificateId { get; private set; }  // Related cert
    public Guid? CaId { get; private set; }            // Related CA
    public CertAuditEventType EventType { get; private set; }
    public string Description { get; private set; }    // Human-readable
    public string Actor { get; private set; }          // "system" or user
    public string? SourceIp { get; private set; }
    public string? Metadata { get; private set; }      // JSON (serial, fingerprint, etc.)
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }

    public static CertificateAuditEvent Create(
        CertAuditEventType eventType, string description, string actor = "system",
        Guid? certificateId = null, Guid? caId = null,
        string? sourceIp = null, string? metadata = null,
        bool success = true, string? errorMessage = null);
}
```

---

## 4. Service Layer — CertificateAuthorityService

**File:** `src/IVF.API/Services/CertificateAuthorityService.cs` (1464 LOC)

This singleton service contains **all** PKI logic — CA management, cert issuance, renewal, revocation, CRL generation, OCSP, deployment, and audit. It creates its own `IServiceScope` per operation.

### 4.1 Registration

```csharp
// In Program.cs
builder.Services.AddSingleton<CertificateAuthorityService>();
builder.Services.AddHostedService<CertAutoRenewalService>();
```

### 4.2 Constructor (Primary Constructor)

```csharp
public sealed class CertificateAuthorityService(
    IServiceScopeFactory scopeFactory,       // For creating DbContext scopes
    IHubContext<BackupHub> hubContext,        // SignalR for deploy progress
    IConfiguration configuration,            // For CertificateAuthority:BaseUrl
    ILogger<CertificateAuthorityService> logger)
```

### 4.3 Public Method Reference

**CA Management:**

| Method                           | Returns                 | Description                         |
| -------------------------------- | ----------------------- | ----------------------------------- |
| `ListCAsAsync()`                 | `List<CaListItem>`      | List all CAs with active cert count |
| `GetCaAsync(id)`                 | `CertificateAuthority?` | Get CA with issued certs (Include)  |
| `CreateRootCaAsync(req)`         | `CertificateAuthority`  | Self-signed root CA                 |
| `CreateIntermediateCaAsync(req)` | `CertificateAuthority`  | CA signed by parent (pathLen=0)     |
| `GetDashboardAsync()`            | `CaDashboard`           | Summary: counts, expiring, recent   |

**Certificate Management:**

| Method                                   | Returns                  | Description                    |
| ---------------------------------------- | ------------------------ | ------------------------------ |
| `ListCertificatesAsync(caId?)`           | `List<CertListItem>`     | List certs, optional CA filter |
| `IssueCertificateAsync(req)`             | `ManagedCertificate`     | Issue server/client cert       |
| `RenewCertificateAsync(certId)`          | `ManagedCertificate`     | New key + link rotation chain  |
| `GetExpiringCertificatesAsync()`         | `List<CertListItem>`     | Certs within renewal window    |
| `AutoRenewExpiringAsync()`               | `CertRenewalBatchResult` | Batch auto-renewal             |
| `SetAutoRenewAsync(certId, ...)`         | void                     | Configure auto-renewal         |
| `RevokeCertificateAsync(certId, reason)` | void                     | Revoke + auto-generate CRL     |
| `GetCertBundleAsync(certId)`             | `CertBundle?`            | Download cert + key + chain    |

**CRL / OCSP:**

| Method                               | Returns                      | Description            |
| ------------------------------------ | ---------------------------- | ---------------------- |
| `GenerateCrlAsync(caId)`             | `CertificateRevocationList`  | RFC 5280 CRL           |
| `GetLatestCrlAsync(caId)`            | `CertificateRevocationList?` | Latest CRL by number   |
| `ListCrlsAsync(caId)`                | `List<CrlListItem>`          | All CRLs for a CA      |
| `CheckCertStatusAsync(serial, caId)` | `OcspResponse`               | OCSP-like status query |

**Deployment:**

| Method                                           | Returns               | Description                        |
| ------------------------------------------------ | --------------------- | ---------------------------------- |
| `DeployCertificateAsync(certId, container, ...)` | `CertDeployResult`    | Generic local deploy via docker cp |
| `DeployPgSslAsync(certId)`                       | `CertDeployResult`    | PostgreSQL primary + SSL reload    |
| `DeployMinioSslAsync(certId)`                    | `CertDeployResult`    | MinIO primary + container restart  |
| `DeployToRemoteContainerAsync(certId, ...)`      | `CertDeployResult`    | Remote deploy via SSH/SCP          |
| `DeployReplicaPgSslAsync(certId)`                | `CertDeployResult`    | Replica PG via SSH + SSL reload    |
| `DeployReplicaMinioSslAsync(certId)`             | `CertDeployResult`    | Replica MinIO via SSH + restart    |
| `ListDeployLogsAsync(certId?, limit)`            | `List<DeployLogItem>` | Historical deploy logs             |
| `GetDeployLogAsync(operationId)`                 | `DeployLogItem?`      | Specific deploy log                |

**Audit:**

| Method                                                    | Returns               | Description            |
| --------------------------------------------------------- | --------------------- | ---------------------- |
| `ListAuditEventsAsync(certId?, caId?, eventType?, limit)` | `List<CertAuditItem>` | Filterable audit trail |

### 4.4 DTOs (Record Types)

All DTOs are defined at the bottom of `CertificateAuthorityService.cs`:

```csharp
// Request DTOs
public record CreateCaRequest(string Name, string CommonName, string? Organization,
    string? OrgUnit, string? Country, string? State, string? Locality,
    int KeySize = 4096, int ValidityDays = 3650);

public record IssueCertRequest(Guid CaId, string CommonName, string? SubjectAltNames,
    CertType Type, string Purpose, int ValidityDays = 365,
    int KeySize = 2048, int RenewBeforeDays = 30);

public record CreateIntermediateCaRequest(Guid ParentCaId, string Name, string CommonName,
    string? Organization, string? OrgUnit, string? Country, string? State,
    string? Locality, int KeySize = 4096, int ValidityDays = 1825);

public record RevokeCertRequest(RevocationReason Reason = RevocationReason.Unspecified);

// Response DTOs
public record CaListItem(Guid Id, string Name, string CommonName, CaType Type,
    CaStatus Status, string KeyAlgorithm, int KeySize, string Fingerprint,
    DateTime NotBefore, DateTime NotAfter, Guid? ParentCaId, int ActiveCertCount);

public record CertListItem(Guid Id, string CommonName, string? SubjectAltNames,
    CertType Type, string Purpose, ManagedCertStatus Status, string Fingerprint,
    string SerialNumber, DateTime NotBefore, DateTime NotAfter, Guid IssuingCaId,
    string? DeployedTo, DateTime? DeployedAt, bool AutoRenewEnabled, int RenewBeforeDays,
    Guid? ReplacedCertId, Guid? ReplacedByCertId,
    DateTime? LastRenewalAttempt, string? LastRenewalResult, bool IsExpiringSoon);

public record CertBundle(string CertificatePem, string PrivateKeyPem,
    string CaChainPem, string CommonName, string Purpose);

public record CertDeployResult(bool Success, List<string> Steps, string? OperationId = null);
public record CertRenewalResult(Guid OldCertId, Guid? NewCertId,
    string CommonName, string Purpose, bool Success, string Message);
public record CertRenewalBatchResult(int TotalCandidates, int RenewedCount,
    List<CertRenewalResult> Results);

public record CaDashboard(int TotalCAs, int ActiveCAs, int TotalCerts, int ActiveCerts,
    int ExpiringSoon, int RevokedCerts,
    List<CertListItem> ExpiringSoonList, List<CertListItem> RecentRenewals);

public record CrlListItem(Guid Id, long CrlNumber, DateTime ThisUpdate,
    DateTime NextUpdate, int RevokedCount, string Fingerprint);

public record OcspResponse(OcspCertStatus Status, string SerialNumber,
    DateTime? RevokedAt, RevocationReason? RevocationReason, DateTime ProducedAt);

public record CertAuditItem(Guid Id, Guid? CertificateId, Guid? CaId,
    CertAuditEventType EventType, string Description, string Actor, string? SourceIp,
    string? Metadata, bool Success, string? ErrorMessage, DateTime CreatedAt);

public record DeployLogItem(Guid Id, string OperationId, Guid CertificateId,
    string Target, string Container, string? RemoteHost,
    DeployStatus Status, DateTime StartedAt, DateTime? CompletedAt,
    string? ErrorMessage, List<DeployLogLine> LogLines);
```

---

## 5. API Endpoints

**File:** `src/IVF.API/Endpoints/CertificateAuthorityEndpoints.cs`

### 5.1 Admin Endpoints (`/api/admin/certificates`)

All require `AdminOnly` authorization policy.

```csharp
var group = app.MapGroup("/api/admin/certificates")
    .WithTags("Certificate Authority")
    .RequireAuthorization("AdminOnly");
```

| Method                     | Path                               | Description                                    |
| -------------------------- | ---------------------------------- | ---------------------------------------------- |
| **Dashboard**              |
| GET                        | `/dashboard`                       | CA dashboard summary                           |
| **CA Management**          |
| GET                        | `/ca`                              | List all CAs                                   |
| GET                        | `/ca/{id}`                         | Get CA details (cert PEM, chain, issued count) |
| POST                       | `/ca`                              | Create Root CA                                 |
| POST                       | `/ca/intermediate`                 | Create Intermediate CA                         |
| GET                        | `/ca/{id}/chain`                   | Download CA chain PEM                          |
| **Certificate Management** |
| GET                        | `/certs`                           | List certificates (`?caId=` optional filter)   |
| POST                       | `/certs`                           | Issue new certificate                          |
| GET                        | `/certs/{id}/bundle`               | Download cert bundle (cert + key + chain)      |
| POST                       | `/certs/{id}/renew`                | Renew certificate (new key pair)               |
| PUT                        | `/certs/{id}/auto-renew`           | Configure auto-renewal                         |
| GET                        | `/certs/expiring`                  | List expiring certificates                     |
| POST                       | `/certs/auto-renew-now`            | Trigger batch auto-renewal                     |
| POST                       | `/certs/{id}/revoke`               | Revoke with reason                             |
| **CRL Management**         |
| POST                       | `/ca/{id}/crl/generate`            | Generate CRL                                   |
| GET                        | `/ca/{id}/crl`                     | List CRLs for CA                               |
| GET                        | `/ca/{id}/crl/latest`              | Download latest CRL (PEM)                      |
| **OCSP**                   |
| GET                        | `/ocsp/{caId}/{serial}`            | OCSP status query                              |
| **Audit**                  |
| GET                        | `/audit`                           | Audit trail (filterable)                       |
| **Deployment**             |
| POST                       | `/certs/{id}/deploy`               | Generic deploy to container                    |
| POST                       | `/certs/{id}/deploy-pg`            | Deploy to PostgreSQL primary                   |
| POST                       | `/certs/{id}/deploy-minio`         | Deploy to MinIO primary                        |
| POST                       | `/certs/{id}/deploy-replica-pg`    | Deploy to PostgreSQL replica (SSH)             |
| POST                       | `/certs/{id}/deploy-replica-minio` | Deploy to MinIO replica (SSH)                  |
| GET                        | `/deploy-logs`                     | List deployment logs                           |
| GET                        | `/deploy-logs/{operationId}`       | Get specific deployment log                    |

### 5.2 Public PKI Endpoints (`/api/pki`)

Unauthenticated per RFC 5280 / RFC 6960 requirements.

```csharp
var pki = app.MapGroup("/api/pki")
    .WithTags("PKI Public")
    .AllowAnonymous();
```

| Method | Path                    | Content-Type           | Description                         |
| ------ | ----------------------- | ---------------------- | ----------------------------------- |
| GET    | `/crl/{caId}`           | `application/pkix-crl` | CRL Distribution Point (DER binary) |
| GET    | `/ocsp/{caId}/{serial}` | `application/json`     | Public OCSP query                   |

---

## 6. Certificate Lifecycle & Code Patterns

### 6.1 Root CA Creation

**Endpoint:** `POST /api/admin/certificates/ca`

Creates a self-signed Root CA with RSA 4096-bit key:

```csharp
// From CertificateAuthorityService.CreateRootCaAsync()
using var rsa = RSA.Create(keySize);  // 4096 default
var certReq = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

// CA extensions
certReq.CertificateExtensions.Add(
    new X509BasicConstraintsExtension(true, false, 0, true));  // CA=true (critical)
certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
certReq.CertificateExtensions.Add(
    new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

using var cert = certReq.CreateSelfSigned(notBefore, notAfter);
```

**Request:**

```json
{
  "name": "IVF Root CA",
  "commonName": "IVF Root CA",
  "organization": "IVF System",
  "country": "VN",
  "keySize": 4096,
  "validityDays": 3650
}
```

### 6.2 Intermediate CA Creation

**Endpoint:** `POST /api/admin/certificates/ca/intermediate`

Creates an Intermediate CA signed by an existing Root CA:

```csharp
// Key differences from Root:
certReq.CertificateExtensions.Add(
    new X509BasicConstraintsExtension(true, true, 0, true));  // pathLenConstraint=0

// Signed by parent CA (not self-signed)
using var parentCert = X509Certificate2.CreateFromPem(parentCa.CertificatePem, parentCa.PrivateKeyPem);
using var signedCert = certReq.Create(parentCert, notBefore, notAfter, serialBytes);

// Chain = intermediate cert + parent chain
var chainPem = certPem + (parentCa.ChainPem ?? parentCa.CertificatePem);
```

**CA Hierarchy:**

```
Root CA (self-signed, 10yr)
  └── Intermediate CA (signed by root, 5yr, pathLen=0)
        ├── Server Cert (PostgreSQL primary)
        ├── Server Cert (PostgreSQL replica)
        ├── Server Cert (MinIO primary)
        ├── Server Cert (MinIO replica)
        └── Client Cert (API mTLS)
```

### 6.3 Certificate Issuance

**Endpoint:** `POST /api/admin/certificates/certs`

Issues a server or client certificate signed by a CA:

```csharp
// From CertificateAuthorityService.IssueCertificateAsync()

// Server certificates:
certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
    [new Oid("1.3.6.1.5.5.7.3.1")], false));  // serverAuth

// Client certificates:
certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
    X509KeyUsageFlags.DigitalSignature, true));
certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
    [new Oid("1.3.6.1.5.5.7.3.2")], false));  // clientAuth

// Subject Alternative Names (comma-separated, auto-detects IP vs DNS)
foreach (var san in req.SubjectAltNames.Split(','))
{
    if (IPAddress.TryParse(san, out var ip))
        sanBuilder.AddIpAddress(ip);
    else
        sanBuilder.AddDnsName(san);
}

// CRL Distribution Point extension (if BaseUrl configured)
var baseUrl = configuration.GetValue<string>("CertificateAuthority:BaseUrl");
if (!string.IsNullOrWhiteSpace(baseUrl))
{
    var crlUrl = $"{baseUrl}/api/pki/crl/{ca.Id}";
    certReq.CertificateExtensions.Add(BuildCrlDistributionPointExtension(crlUrl));
}

// Validity clamped to CA expiry
if (notAfter > caCert.NotAfter)
    notAfter = caCert.NotAfter.AddMinutes(-1);
```

**Purpose Presets (UI helpers):**

| Value            | Description                     |
| ---------------- | ------------------------------- |
| `pg-primary`     | PostgreSQL Primary Server       |
| `pg-replica`     | PostgreSQL Replica Server       |
| `pg-client`      | PostgreSQL Client (verify-full) |
| `minio-primary`  | MinIO Primary TLS               |
| `minio-replica`  | MinIO Replica TLS               |
| `api-client`     | API mTLS Client                 |
| `signserver-tls` | SignServer TLS                  |
| `custom`         | Custom purpose                  |

### 6.4 CRL Distribution Point Extension (ASN.1)

Built using `System.Formats.Asn1`:

```csharp
private static X509Extension BuildCrlDistributionPointExtension(string crlUrl)
{
    // ASN.1 Structure: SEQUENCE { SEQUENCE { [0] { [0] { [6] uri } } } }
    var writer = new AsnWriter(AsnEncodingRules.DER);
    using (writer.PushSequence())                                                    // CRLDistributionPoints
    {
        using (writer.PushSequence())                                                // DistributionPoint
        {
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) // distributionPoint [0]
            {
                using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true))) // fullName [0]
                {
                    writer.WriteCharacterString(UniversalTagNumber.IA5String,
                        crlUrl, new Asn1Tag(TagClass.ContextSpecific, 6));           // uniformResourceIdentifier [6]
                }
            }
        }
    }
    return new X509Extension("2.5.29.31", writer.Encode(), false);
}
```

### 6.5 Certificate Renewal (Rotation)

**Endpoint:** `POST /api/admin/certificates/certs/{id}/renew`

Generates a **new key pair** with the same parameters and links a rotation chain:

```csharp
// From RenewCertificateAsync()
// 1. Issue new cert with same CN, SANs, type, purpose, validity
var newCert = await IssueCertificateInternalAsync(db, new IssueCertRequest(
    CaId: oldCert.IssuingCaId,
    CommonName: oldCert.CommonName,
    SubjectAltNames: oldCert.SubjectAltNames,
    Type: oldCert.Type,
    Purpose: oldCert.Purpose,
    ValidityDays: oldCert.ValidityDays,
    KeySize: oldCert.KeySize,
    RenewBeforeDays: oldCert.RenewBeforeDays
), ct);

// 2. Link rotation chain
newCert.SetReplacedCert(oldCert.Id);
oldCert.MarkSuperseded(newCert.Id);
oldCert.RecordRenewalAttempt($"Renewed → {newCert.Id}");
```

### 6.6 PEM Export Helpers

```csharp
private static string ExportCertPem(X509Certificate2 cert)
{
    var sb = new StringBuilder();
    sb.AppendLine("-----BEGIN CERTIFICATE-----");
    sb.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
    sb.AppendLine("-----END CERTIFICATE-----");
    return sb.ToString();
}

private static string ExportKeyPem(RSA rsa)
{
    var keyBytes = rsa.ExportPkcs8PrivateKey();
    var sb = new StringBuilder();
    sb.AppendLine("-----BEGIN PRIVATE KEY-----");
    sb.AppendLine(Convert.ToBase64String(keyBytes, Base64FormattingOptions.InsertLineBreaks));
    sb.AppendLine("-----END PRIVATE KEY-----");
    return sb.ToString();
}

private static string ExportCrlPem(byte[] crlDer)
{
    var sb = new StringBuilder();
    sb.AppendLine("-----BEGIN X509 CRL-----");
    sb.AppendLine(Convert.ToBase64String(crlDer, Base64FormattingOptions.InsertLineBreaks));
    sb.AppendLine("-----END X509 CRL-----");
    return sb.ToString();
}
```

---

## 7. Certificate Revocation & CRL Generation

### 7.1 Revocation

**Endpoint:** `POST /api/admin/certificates/certs/{id}/revoke`

```csharp
// From RevokeCertificateAsync()
cert.Revoke(reason);  // Sets Status=Revoked, RevokedAt=now, RevocationReason=reason

// Auto-generate CRL after revocation
await GenerateCrlAsync(cert.IssuingCaId, ct);
```

**Request:** `{ "reason": "KeyCompromise" }`

### 7.2 CRL Generation

**Endpoint:** `POST /api/admin/certificates/ca/{id}/crl/generate`

Uses .NET's `CertificateRevocationListBuilder` (RFC 5280 §5):

```csharp
// From GenerateCrlAsync()
var crlBuilder = new CertificateRevocationListBuilder();
foreach (var cert in revokedCerts)
{
    // Serial number: strip leading zeros per X.690 DER encoding rules
    var hexSerial = cert.SerialNumber.TrimStart('0');
    if (hexSerial.Length == 0) hexSerial = "0";
    if (hexSerial.Length % 2 != 0) hexSerial = "0" + hexSerial;
    var serialBytes = Convert.FromHexString(hexSerial);
    crlBuilder.AddEntry(serialBytes, cert.RevokedAt ?? cert.UpdatedAt ?? DateTime.UtcNow);
}

// Sign CRL with CA key, 7-day validity
var crlDer = crlBuilder.Build(
    caCert,
    crlNumber,         // Monotonically increasing (CA.AllocateCrlNumber())
    nextUpdate,        // thisUpdate + 7 days
    HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1,
    thisUpdate);
```

### 7.3 CRL Distribution (Public)

```
GET /api/pki/crl/{caId}     ← AllowAnonymous
```

Returns the latest CRL in DER binary format (`application/pkix-crl`). This endpoint is unauthenticated per RFC 5280, and its URL is embedded in issued certificates via the CRL Distribution Point extension.

---

## 8. OCSP Responder

**Authenticated:** `GET /api/admin/certificates/ocsp/{caId}/{serialNumber}`
**Public:** `GET /api/pki/ocsp/{caId}/{serialNumber}` (AllowAnonymous)

Simplified OCSP-like query (HTTP GET, JSON response):

```csharp
// From CheckCertStatusAsync()
return cert.Status switch
{
    ManagedCertStatus.Revoked => new OcspResponse(
        OcspCertStatus.Revoked, serialNumber,
        cert.RevokedAt, cert.RevocationReason, DateTime.UtcNow),
    ManagedCertStatus.Active or ManagedCertStatus.Superseded =>
        new OcspResponse(OcspCertStatus.Good, serialNumber, null, null, DateTime.UtcNow),
    _ => new OcspResponse(OcspCertStatus.Unknown, serialNumber, null, null, DateTime.UtcNow)
};
```

**Response:**

```json
{
  "status": "Good", // Good | Revoked | Unknown
  "serialNumber": "00000000001A",
  "revokedAt": null,
  "revocationReason": null,
  "producedAt": "2026-02-28T12:00:00Z"
}
```

Every OCSP query is logged as an `OcspQuery` audit event.

---

## 9. Certificate Deployment Engine

The deployment system writes certificates, private keys, and CA chains to Docker containers — both local and remote — with real-time progress streaming via SignalR.

### 9.1 Core Deploy Flow

Every deploy method follows this pattern:

```csharp
// From DeployCertificateAsync()
// 1. Independent 5-minute timeout (detached from HTTP request lifetime)
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// 2. Generate 12-char operation ID for SignalR group
var operationId = Guid.NewGuid().ToString("N")[..12];

// 3. Fire-and-forget to background — returns operationId immediately
_ = Task.Run(async () =>
{
    var log = CertDeploymentLog.Create(cert.Id, operationId, target, container, remoteHost);
    db.CertDeploymentLogs.Add(log);

    // Write files, set permissions, reload config...
    await WriteToContainerAsync(container, certPath, certPem, "644", log, cts.Token);
    await WriteToContainerAsync(container, keyPath, keyPem, "600", log, cts.Token);

    cert.MarkDeployed($"{container}:{certPath}");
    log.Complete(true);
    await db.SaveChangesAsync(cts.Token);

    await EmitStatus(operationId, "completed");
}, cts.Token);

return new CertDeployResult(true, ["Deploy started"], operationId);
```

### 9.2 Local Container File Writing

```csharp
// WriteToContainerAsync() — uses docker cp
private async Task WriteToContainerAsync(string container, string path, string content,
    string chmod, CertDeploymentLog log, CancellationToken ct)
{
    var tempFile = Path.GetTempFileName();
    try
    {
        await File.WriteAllTextAsync(tempFile, content, ct);

        // docker cp {tempFile} {container}:{path}
        await RunCommandAsync("docker", $"cp {tempFile} {container}:{path}", ct);
        await EmitLog(log.OperationId, $"✓ Copied to {container}:{path}");

        // Set permissions
        await RunCommandAsync("docker",
            $"exec {container} chmod {chmod} {path}", ct);

        // Set ownership (postgres = 999:999 on Debian-based, 70:70 on Alpine)
        await RunCommandAsync("docker",
            $"exec {container} chown 999:999 {path}", ct);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

### 9.3 Remote Container File Writing (SSH/SCP)

```csharp
// WriteToRemoteContainerAsync() — SCP + SSH
private async Task WriteToRemoteContainerAsync(string remoteHost, string container,
    string path, string content, string chmod, CertDeploymentLog log, CancellationToken ct)
{
    var sshOpts = "-o StrictHostKeyChecking=no -o ConnectTimeout=30 -o BatchMode=yes "
                + "-o ServerAliveInterval=10 -o ServerAliveCountMax=3";

    var tempFile = Path.GetTempFileName();
    var remoteTmp = $"/tmp/cert_{Guid.NewGuid():N}";
    try
    {
        await File.WriteAllTextAsync(tempFile, content, ct);

        // Step 1: SCP temp file to remote (with retry, 90s timeout)
        await RetryRemoteCommandAsync(
            "scp", $"{sshOpts} {tempFile} root@{remoteHost}:{remoteTmp}",
            TimeSpan.FromSeconds(90), ct);

        // Step 2: docker cp on remote
        await RetryRemoteCommandAsync(
            "ssh", $"{sshOpts} root@{remoteHost} docker cp {remoteTmp} {container}:{path}",
            TimeSpan.FromSeconds(30), ct);

        // Step 3: Set permissions on remote container
        await RetryRemoteCommandAsync(
            "ssh", $"{sshOpts} root@{remoteHost} docker exec {container} chmod {chmod} {path}",
            TimeSpan.FromSeconds(30), ct);

        // Step 4: Cleanup remote temp file
        await RetryRemoteCommandAsync(
            "ssh", $"{sshOpts} root@{remoteHost} rm -f {remoteTmp}",
            TimeSpan.FromSeconds(10), ct);
    }
    finally
    {
        File.Delete(tempFile);
    }
}
```

### 9.4 Retry Logic

```csharp
// RetryRemoteCommandAsync() — 3 retries, exponential backoff
private async Task RetryRemoteCommandAsync(string command, string args,
    TimeSpan timeout, CancellationToken ct)
{
    const int maxRetries = 3;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            await RunCommandAsync(command, args, cts.Token);
            return;  // Success
        }
        catch when (attempt < maxRetries)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));  // 2s, 4s, 8s
            await Task.Delay(delay, ct);
        }
    }
}
```

### 9.5 Deploy Targets

| Endpoint               | Container                 | Cert Files                                | Post-Deploy Action                                   |
| ---------------------- | ------------------------- | ----------------------------------------- | ---------------------------------------------------- |
| `deploy-pg`            | `ivf-db`                  | `server.crt`, `server.key`, `root.crt`    | `ALTER SYSTEM SET ssl = on; SELECT pg_reload_conf()` |
| `deploy-minio`         | `ivf-minio`               | `public.crt`, `private.key`, `CAs/ca.crt` | `docker restart ivf-minio`                           |
| `deploy-replica-pg`    | `ivf-db-replica` (SSH)    | Same as pg                                | Same + `chown postgres:postgres` via SSH             |
| `deploy-replica-minio` | `ivf-minio-replica` (SSH) | Same as minio                             | `ssh ... docker restart ivf-minio-replica`           |
| `deploy` (generic)     | Any container             | Custom paths                              | None (caller specifies)                              |

### 9.6 SignalR Real-Time Progress

```csharp
// EmitLog() — sends step-by-step log line to SignalR group
private async Task EmitLog(string operationId, string message, string level = "info")
{
    await _hubContext.Clients.Group($"backup_{operationId}")
        .SendAsync("DeployLog", new {
            operationId, timestamp = DateTime.UtcNow, level, message
        });
}

// EmitStatus() — sends status change to SignalR group
private async Task EmitStatus(string operationId, string status,
    string? error = null)
{
    await _hubContext.Clients.Group($"backup_{operationId}")
        .SendAsync("DeployStatus", new {
            operationId, status, completedAt = DateTime.UtcNow, error
        });
}
```

**Angular client connection:**

```typescript
// Connect to SignalR and join operation group
this.hubConnection = new signalR.HubConnectionBuilder()
  .withUrl(`${apiUrl}/hubs/queue?access_token=${token}`)
  .build();

this.hubConnection.on("DeployLog", (data) => this.deployLogs.push(data));
this.hubConnection.on(
  "DeployStatus",
  (data) => (this.deployStatus = data.status),
);
this.hubConnection.invoke("JoinBackupGroup", operationId);
```

---

## 10. Auto-Renewal Background Service

**File:** `src/IVF.API/Services/CertAutoRenewalService.cs`

```csharp
public class CertAutoRenewalService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);  // Wait for app startup

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await caService.AutoRenewExpiringAsync(stoppingToken);
            logger.LogInformation(
                "Auto-renewal check: {Total} candidates, {Renewed} renewed",
                result.TotalCandidates, result.RenewedCount);

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
```

### 10.1 Renewal Eligibility

A certificate is eligible for auto-renewal when **all** conditions are met:

```csharp
// ManagedCertificate.NeedsAutoRenewal
public bool NeedsAutoRenewal =>
    AutoRenewEnabled &&
    Status == ManagedCertStatus.Active &&
    IsExpiringSoon;

// ManagedCertificate.IsExpiringSoon
public bool IsExpiringSoon =>
    NotAfter <= DateTime.UtcNow.AddDays(RenewBeforeDays);
```

### 10.2 Batch Renewal Logic

```csharp
// From AutoRenewExpiringAsync()
var candidates = await db.ManagedCertificates
    .Where(c => c.AutoRenewEnabled && c.Status == ManagedCertStatus.Active)
    .ToListAsync(ct);

var needsRenewal = candidates.Where(c => c.NeedsAutoRenewal).ToList();

foreach (var cert in needsRenewal)
{
    try
    {
        var newCert = await IssueCertificateInternalAsync(db, new IssueCertRequest(
            cert.IssuingCaId, cert.CommonName, cert.SubjectAltNames,
            cert.Type, cert.Purpose, cert.ValidityDays,
            cert.KeySize, cert.RenewBeforeDays), ct);

        newCert.SetReplacedCert(cert.Id);
        cert.MarkSuperseded(newCert.Id);
        cert.RecordRenewalAttempt($"Auto-renewed → {newCert.Id}");
        results.Add(new CertRenewalResult(cert.Id, newCert.Id,
            cert.CommonName, cert.Purpose, true, "Auto-renewed"));
    }
    catch (Exception ex)
    {
        cert.RecordRenewalAttempt($"Failed: {ex.Message}");
        results.Add(new CertRenewalResult(cert.Id, null,
            cert.CommonName, cert.Purpose, false, ex.Message));
    }
}
```

**Note:** Auto-renewal issues a new certificate but does **not** auto-deploy. Deployment is a separate manual step via the UI or API.

---

## 11. mTLS / Digital Signing Integration

### 11.1 Architecture

```
IVF API  ──mTLS──▶  SignServer  ──────▶  EJBCA
(client cert)      (PDF signing)      (CA management)
     │                                      │
     └──── api-client.p12 ◄── issued by ────┘
```

### 11.2 Docker Configuration

**EJBCA** (`ivf-ejbca`):

- Port 8443 (HTTPS)
- Separate PostgreSQL DB (`ivf-ejbca-db`)
- Networks: `ivf-signing` (internal) + `ivf-data`

**SignServer** (`ivf-signserver`):

- Port 9443 (HTTPS + mTLS)
- Separate PostgreSQL DB (`ivf-signserver-db`)
- Networks: `ivf-signing` + `ivf-data`
- Read-only filesystem with tmpfs mounts

### 11.3 API mTLS Configuration

```json
// appsettings.json → DigitalSigning
{
  "SignServerUrl": "https://signserver:8443/signserver",
  "Enabled": true,
  "ClientCertificatePath": "/app/certs/api-client.p12",
  "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
  "TrustedCaCertPath": "/app/certs/ca-chain.pem",
  "SkipTlsValidation": true
}
```

Docker Compose mounts:

```yaml
volumes:
  - ./certs/api/api-client.p12:/app/certs/api-client.p12:ro
  - ./certs/ca-chain.pem:/app/certs/ca-chain.pem:ro
  - ./secrets/api_cert_password.txt:/run/secrets/api_cert_password:ro
```

### 11.4 Digital Signing Flow

1. API receives PDF signing request
2. Loads client P12 certificate from disk
3. Opens mTLS connection to SignServer at `https://signserver:8443`
4. SignServer validates client certificate against trusted CA
5. SignServer signs PDF using configured worker (PDFSigner)
6. Signed PDF stored in MinIO bucket `ivf-signed-pdfs`

---

## 12. Audit Trail

### 12.1 Audit Helper

```csharp
// Private helper inside CertificateAuthorityService
private async Task AuditAsync(IvfDbContext db, CertAuditEventType eventType,
    string description, bool success = true, Guid? certId = null,
    Guid? caId = null, string? metadata = null, string? error = null,
    CancellationToken ct = default)
{
    var audit = CertificateAuditEvent.Create(
        certId, caId, eventType, description,
        "system",     // Actor
        null,         // Source IP
        metadata, success, error);
    db.CertificateAuditEvents.Add(audit);
    await db.SaveChangesAsync(ct);
}
```

### 12.2 Audited Operations

| Operation              | Event Type              | Metadata                            |
| ---------------------- | ----------------------- | ----------------------------------- |
| Create Root CA         | `CaCreated`             | KeySize, ValidityDays               |
| Create Intermediate CA | `IntermediateCaCreated` | ParentCaId, ParentCaName, KeySize   |
| Issue Certificate      | `CertIssued`            | CaId, Purpose, SerialNumber, SANs   |
| Renew Certificate      | `CertRenewed`           | OldCertId, NewSerialNumber          |
| Revoke Certificate     | `CertRevoked`           | Reason, SerialNumber                |
| Deploy Certificate     | `CertDeployed`          | Target, Container                   |
| Deploy Failed          | `CertDeployFailed`      | Error message                       |
| Generate CRL           | `CrlGenerated`          | CrlNumber, RevokedCount, NextUpdate |
| OCSP Query             | `OcspQuery`             | SerialNumber                        |
| Auto-Renew Triggered   | `AutoRenewTriggered`    | TotalCandidates                     |
| Auto-Renew Success     | `AutoRenewSuccess`      | RenewedCount                        |
| Auto-Renew Failed      | `AutoRenewFailed`       | Error details                       |
| Config Changed         | `ConfigChanged`         | AutoRenewEnabled, RenewBeforeDays   |
| Cert Exported          | `CertExported`          | Bundle type                         |
| Chain Downloaded       | `ChainDownloaded`       | CA ID                               |
| CA Status Changed      | `CaStatusChanged`       | New status                          |

### 12.3 Querying Audit Events

```
GET /api/admin/certificates/audit?certId={guid}&caId={guid}&eventType={type}&limit=100
```

All parameters are optional. Returns events sorted by `CreatedAt` descending.

---

## 13. Angular Frontend

### 13.1 Component Structure

**File:** `ivf-client/src/app/features/admin/certificate-management/certificate-management.component.ts`

Standalone Angular component with 9 tabs:

| Tab          | Key         | Features                                                         |
| ------------ | ----------- | ---------------------------------------------------------------- |
| Dashboard    | `dashboard` | CA/cert counts, expiring soon list, recent renewals              |
| CAs          | `cas`       | List/create Root CAs, create Intermediate CAs, view chain        |
| Certificates | `certs`     | Issue/list certs, filter by CA, view bundles, revoke with reason |
| Expiring     | `expiring`  | List expiring certs, trigger batch auto-renewal                  |
| Deploy       | `deploy`    | Quick-deploy buttons (PG/MinIO primary & replica), custom deploy |
| Logs         | `logs`      | Real-time deploy log viewer via SignalR, historical logs         |
| CRL          | `crl`       | Generate CRL, list CRLs per CA, download PEM                     |
| OCSP         | `ocsp`      | Query certificate status by CA + serial number                   |
| Audit        | `audit`     | Filter audit events by cert, CA, event type                      |

### 13.2 Service Methods

**File:** `ivf-client/src/app/core/services/backup.service.ts`

```typescript
// CA Management
getCaDashboard(): Observable<CaDashboard>
listCAs(): Observable<CaListItem[]>
getCA(id: string): Observable<CaDetail>
createRootCA(req: CreateCaRequest): Observable<{id, name, fingerprint}>
createIntermediateCA(req): Observable<{id, name, fingerprint, parentCaId, type}>
downloadCaChain(id: string): Observable<string>

// Certificate Management
listCertificates(caId?: string): Observable<CertListItem[]>
issueCertificate(req: IssueCertRequest): Observable<any>
getCertBundle(id: string): Observable<CertBundle>

// Renewal & Auto-Renewal
renewCertificate(id: string): Observable<any>
setAutoRenew(id: string, enabled: boolean, renewBeforeDays?: number): Observable<void>
getExpiringCertificates(): Observable<CertListItem[]>
triggerAutoRenewal(): Observable<CertRenewalBatchResult>

// Revocation
revokeCertificate(id: string, reason?: string): Observable<void>

// CRL
generateCrl(caId: string): Observable<{id, crlNumber, thisUpdate, nextUpdate, revokedCount}>
listCrls(caId: string): Observable<CrlListItem[]>
downloadLatestCrl(caId: string): Observable<string>

// OCSP
checkCertStatus(caId: string, serialNumber: string): Observable<OcspResponse>

// Audit
listCertAuditEvents(certId?, caId?, eventType?, limit?): Observable<CertAuditItem[]>

// Deployment
deployCertificate(id: string, req: DeployCertRequest): Observable<CertDeployResult>
deployPgSsl(id: string): Observable<CertDeployResult>
deployMinioSsl(id: string): Observable<CertDeployResult>
deployReplicaPgSsl(id: string): Observable<CertDeployResult>
deployReplicaMinioSsl(id: string): Observable<CertDeployResult>
listDeployLogs(certId?, limit?): Observable<DeployLogItem[]>
getDeployLog(operationId: string): Observable<DeployLogItem>
```

### 13.3 TypeScript Models

**File:** `ivf-client/src/app/core/models/backup.models.ts`

Key interfaces mirroring backend DTOs:

```typescript
interface CaDashboard {
  totalCAs: number;
  activeCAs: number;
  totalCerts: number;
  activeCerts: number;
  expiringSoon: number;
  revokedCerts: number;
  expiringSoonList: CertListItem[];
  recentRenewals: CertListItem[];
}

interface CertListItem {
  id: string;
  commonName: string;
  subjectAltNames?: string;
  type: CertType;
  purpose: string;
  status: ManagedCertStatus;
  fingerprint: string;
  serialNumber: string;
  notBefore: string;
  notAfter: string;
  issuingCaId: string;
  deployedTo?: string;
  deployedAt?: string;
  autoRenewEnabled: boolean;
  renewBeforeDays: number;
  replacedCertId?: string;
  replacedByCertId?: string;
  lastRenewalAttempt?: string;
  lastRenewalResult?: string;
  isExpiringSoon: boolean;
}

interface CertBundle {
  certificatePem: string;
  privateKeyPem: string;
  caChainPem: string;
  commonName: string;
  purpose: string;
}

interface DeployLogItem {
  id: string;
  operationId: string;
  certificateId: string;
  target: string;
  container: string;
  remoteHost?: string;
  status: DeployStatus;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  logLines: DeployLogLine[];
}
```

---

## 14. Docker Network Topology

### 14.1 Network Segmentation

```
┌─────────────────────────────────────────────────────────┐
│                    ivf-public (bridge)                   │
│  ┌─────────┐  ┌─────────┐  ┌──────────┐  ┌──────────┐  │
│  │ ivf-api │  │ivf-redis│  │ivf-minio │  │db-standby│  │
│  │ :5000   │  │ :6379   │  │ :9000    │  │ :5434    │  │
│  └────┬────┘  └────┬────┘  └────┬─────┘  └────┬─────┘  │
└───────┼─────────────┼───────────┼──────────────┼────────┘
        │             │           │              │
┌───────┼─────────────┼───────────┼──────────────┼────────┐
│       │      ivf-data (internal, no internet)  │        │
│  ┌────▼────┐  ┌─────▼───┐  ┌───▼──────┐  ┌────▼─────┐  │
│  │ ivf-db  │  │ivf-redis│  │ivf-minio │  │ ejbca-db │  │
│  │ :5433   │  │         │  │          │  │          │  │
│  └─────────┘  └─────────┘  └──────────┘  └──────────┘  │
│  ┌──────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │ivf-ejbca │  │ivf-signserver│  │signserver-db     │   │
│  │ :8443    │  │ :9443        │  │                  │   │
│  └────┬─────┘  └──────┬───────┘  └──────────────────┘   │
└───────┼───────────────┼─────────────────────────────────┘
        │               │
┌───────┼───────────────┼─────────────────────────────────┐
│       │   ivf-signing (internal, no internet)           │
│  ┌────▼────┐  ┌───────▼──────┐  ┌─────────┐            │
│  │ivf-ejbca│  │ivf-signserver│  │ ivf-api │            │
│  │         │  │  (mTLS)      │  │(client) │            │
│  └─────────┘  └──────────────┘  └─────────┘            │
└─────────────────────────────────────────────────────────┘
```

### 14.2 Remote Replica Topology

```
┌─────── Local Server ────────┐     SSH / SCP      ┌── Remote Server (172.16.102.11) ──┐
│                              │ ──────────────────▶│                                   │
│  ivf-api (:5000)             │                    │  ivf-db-replica (:5201)            │
│    ├── docker exec ivf-db    │                    │    ├── server.crt/key/root.crt     │
│    ├── docker exec ivf-minio │                    │                                   │
│    └── ssh root@172.16.102.11│                    │  ivf-minio-replica (:9000)         │
│                              │                    │    ├── public.crt/private.key      │
│  ivf-db (:5433)              │    WAL Stream      │    └── CAs/ca.crt                  │
│    └── SSL: server.crt/key   │ ──────────────────▶│                                   │
│                              │                    │                                   │
│  ivf-minio (:9000)           │    mc mirror       │                                   │
│    └── TLS: public.crt/key   │ ──────────────────▶│                                   │
└──────────────────────────────┘                    └───────────────────────────────────┘
```

### 14.3 TLS Certificate File Paths

| Service          | Container           | Cert                                  | Key               | CA                        |
| ---------------- | ------------------- | ------------------------------------- | ----------------- | ------------------------- |
| PG Primary       | `ivf-db`            | `/var/lib/postgresql/data/server.crt` | `.../server.key`  | `.../root.crt`            |
| PG Replica       | `ivf-db-replica`    | `/var/lib/postgresql/data/server.crt` | `.../server.key`  | `.../root.crt`            |
| MinIO Primary    | `ivf-minio`         | `/root/.minio/certs/public.crt`       | `.../private.key` | `.../CAs/ca.crt`          |
| MinIO Replica    | `ivf-minio-replica` | `/root/.minio/certs/public.crt`       | `.../private.key` | `.../CAs/ca.crt`          |
| API → SignServer | `ivf-api`           | `/app/certs/api-client.p12`           | (in P12)          | `/app/certs/ca-chain.pem` |

---

## 15. Development Workflow

Step-by-step guide for developers working with the PKI system.

### 15.1 Initial PKI Setup (Dev Environment)

```bash
# 1. Start infrastructure
docker-compose up -d

# 2. Login (default admin)
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}' | jq -r '.token')

# 3. Create Root CA
ROOT_CA=$(curl -s -X POST http://localhost:5000/api/admin/certificates/ca \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "IVF Root CA",
    "commonName": "IVF Root CA",
    "organization": "IVF System",
    "country": "VN",
    "keySize": 4096,
    "validityDays": 3650
  }' | jq -r '.id')

# 4. Issue PostgreSQL server certificate
PG_CERT=$(curl -s -X POST http://localhost:5000/api/admin/certificates/certs \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"caId\": \"$ROOT_CA\",
    \"commonName\": \"ivf-db.ivf.local\",
    \"subjectAltNames\": \"ivf-db,localhost,127.0.0.1\",
    \"type\": 0,
    \"purpose\": \"pg-primary\",
    \"validityDays\": 365
  }" | jq -r '.id')

# 5. Deploy to PostgreSQL
curl -X POST "http://localhost:5000/api/admin/certificates/certs/$PG_CERT/deploy-pg" \
  -H "Authorization: Bearer $TOKEN"

# 6. Verify SSL
docker exec ivf-db psql -U postgres -c "SHOW ssl;"
```

### 15.2 Certificate Rotation Workflow

```bash
# 1. Check expiring certificates
curl -s http://localhost:5000/api/admin/certificates/certs/expiring \
  -H "Authorization: Bearer $TOKEN" | jq '.[] | {commonName, notAfter, purpose}'

# 2. Renew a specific certificate
NEW_CERT=$(curl -s -X POST "http://localhost:5000/api/admin/certificates/certs/$OLD_CERT_ID/renew" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.id')

# 3. Deploy the new certificate
curl -X POST "http://localhost:5000/api/admin/certificates/certs/$NEW_CERT/deploy-pg" \
  -H "Authorization: Bearer $TOKEN"

# 4. Or trigger batch auto-renewal
curl -s -X POST http://localhost:5000/api/admin/certificates/certs/auto-renew-now \
  -H "Authorization: Bearer $TOKEN" | jq '.'
```

### 15.3 Emergency Revocation

```bash
# 1. Revoke compromised certificate
curl -X POST "http://localhost:5000/api/admin/certificates/certs/$CERT_ID/revoke" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"reason": "KeyCompromise"}'
# → Automatically generates new CRL

# 2. Issue replacement and deploy
# (same as Initial Setup steps 4-5)

# 3. Verify CRL contains revoked serial
curl -s "http://localhost:5000/api/pki/crl/$CA_ID" --output latest.crl
openssl crl -inform DER -in latest.crl -text -noout
```

### 15.4 Verifying SSL/TLS Connections

```bash
# PostgreSQL SSL status
docker exec ivf-db psql -U postgres -c "SHOW ssl;"
docker exec ivf-db psql -U postgres -c "SELECT * FROM pg_stat_ssl;"

# MinIO certificate files
docker exec ivf-minio ls -la /root/.minio/certs/

# API connection tests
curl -s http://localhost:5000/api/admin/cloud-replication/test-db-connection \
  -H "Authorization: Bearer $TOKEN" | jq '.'

curl -s http://localhost:5000/api/admin/cloud-replication/test-minio-connection \
  -H "Authorization: Bearer $TOKEN" | jq '.'
```

---

## 16. Extending the PKI System

### 16.1 Adding a New Deploy Target

To deploy certificates to a new service (e.g., Redis, Nginx):

**Step 1:** Add a new deploy method in `CertificateAuthorityService.cs`:

```csharp
public async Task<CertDeployResult> DeployRedisSslAsync(Guid certId, CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

    var cert = await db.ManagedCertificates.FindAsync([certId], ct)
        ?? throw new KeyNotFoundException("Certificate not found");
    var ca = await db.CertificateAuthorities.FindAsync([cert.IssuingCaId], ct)!;

    var operationId = Guid.NewGuid().ToString("N")[..12];
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    var log = CertDeploymentLog.Create(cert.Id, operationId, "redis-primary", "ivf-redis", null);
    db.CertDeploymentLogs.Add(log);

    // Write cert files
    await WriteToContainerAsync("ivf-redis", "/tls/redis.crt", cert.CertificatePem, "644", log, cts.Token);
    await WriteToContainerAsync("ivf-redis", "/tls/redis.key", cert.PrivateKeyPem, "600", log, cts.Token);
    await WriteToContainerAsync("ivf-redis", "/tls/ca.crt", ca.ChainPem ?? ca.CertificatePem, "644", log, cts.Token);

    // Reload Redis TLS (if supported)
    await RunCommandAsync("docker", "exec ivf-redis redis-cli CONFIG SET tls-cert-file /tls/redis.crt", cts.Token);

    cert.MarkDeployed("ivf-redis:/tls/redis.crt");
    log.Complete(true);
    await AuditAsync(db, CertAuditEventType.CertDeployed, $"Deployed to Redis", certId: certId);
    await db.SaveChangesAsync(cts.Token);

    return new CertDeployResult(true, ["Deployed to Redis"], operationId);
}
```

**Step 2:** Add endpoint in `CertificateAuthorityEndpoints.cs`:

```csharp
group.MapPost("/certs/{id:guid}/deploy-redis", async (Guid id, CertificateAuthorityService svc, CancellationToken ct)
    => Results.Ok(await svc.DeployRedisSslAsync(id, ct)));
```

**Step 3:** Add Angular service method in `backup.service.ts`:

```typescript
deployRedisSsl(id: string): Observable<CertDeployResult> {
  return this.api.post<CertDeployResult>(`admin/certificates/certs/${id}/deploy-redis`, {});
}
```

### 16.2 Adding a New Certificate Type

To add a new certificate type (e.g., `CodeSigning`):

**Step 1:** Add enum value in `ManagedCertificate.cs`:

```csharp
public enum CertType { Server, Client, CodeSigning }
```

**Step 2:** Add key usage in `IssueCertificateInternalAsync()`:

```csharp
CertType.CodeSigning => (
    X509KeyUsageFlags.DigitalSignature,
    new Oid("1.3.6.1.5.5.7.3.3")  // codeSigning
),
```

**Step 3:** Create EF migration:

```bash
dotnet ef migrations add AddCodeSigningCertType \
  --project src/IVF.Infrastructure --startup-project src/IVF.API
```

### 16.3 Adding a New Audit Event Type

**Step 1:** Add enum value in `CertificateAuditEvent.cs`:

```csharp
public enum CertAuditEventType
{
    // ... existing values ...
    CertBackedUp = 16,
}
```

**Step 2:** Call `AuditAsync()` at the appropriate place:

```csharp
await AuditAsync(db, CertAuditEventType.CertBackedUp,
    $"Certificate {cert.CommonName} backed up",
    certId: cert.Id, metadata: $"BackupPath={path}");
```

---

## 17. Configuration Reference

### 17.1 appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=ivf_db;Username=postgres;Password=postgres;SSL Mode=Require;Trust Server Certificate=true"
  },
  "CertificateAuthority": {
    "BaseUrl": "http://localhost:5000"
  },
  "DigitalSigning": {
    "SignServerUrl": "https://signserver:8443/signserver",
    "WorkerName": "PDFSigner",
    "Enabled": true,
    "SkipTlsValidation": true,
    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "UseSSL": true,
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin123"
  },
  "CloudReplication": {
    "RemoteDbHost": "172.16.102.11",
    "RemoteDbPort": 5201,
    "RemoteMinioEndpoint": "172.16.102.11:9000",
    "RemoteDbSslMode": "require"
  }
}
```

### 17.2 EF Core Migration

```bash
# Apply migration
dotnet ef database update --project src/IVF.Infrastructure --startup-project src/IVF.API

# Create new migration (if needed)
dotnet ef migrations add <Name> --project src/IVF.Infrastructure --startup-project src/IVF.API
```

### 17.3 Database Tables

| Table                        | EF DbSet                     | Description                           |
| ---------------------------- | ---------------------------- | ------------------------------------- |
| `CertificateAuthorities`     | `CertificateAuthorities`     | Root and Intermediate CAs             |
| `ManagedCertificates`        | `ManagedCertificates`        | Issued certificates with key material |
| `CertDeploymentLogs`         | `CertDeploymentLogs`         | Deployment operation history          |
| `CertificateRevocationLists` | `CertificateRevocationLists` | CRL PEM + DER storage                 |
| `CertificateAuditEvents`     | `CertificateAuditEvents`     | Immutable audit log                   |

### 17.4 Registration

`CertificateAuthorityService` is registered as a **singleton** (it creates its own scopes internally):

```csharp
// In Program.cs or service registration
builder.Services.AddSingleton<CertificateAuthorityService>();
builder.Services.AddHostedService<CertAutoRenewalService>();
```

---

## 18. Troubleshooting

### 18.1 SSH/SCP Remote Deployment Failures

| Symptom                         | Cause                      | Fix                                                          |
| ------------------------------- | -------------------------- | ------------------------------------------------------------ |
| `Connection timed out`          | Remote host unreachable    | `ping 172.16.102.11`, check firewall rules                   |
| `Permission denied (publickey)` | SSH key not authorized     | Add API host's public key to remote `~/.ssh/authorized_keys` |
| `docker: command not found`     | Docker not in SSH path     | Verify `ssh root@172.16.102.11 docker ps` works              |
| `Host key verification failed`  | Known_hosts mismatch       | Service uses `StrictHostKeyChecking=no` by default           |
| SCP timeout (90s)               | Large file or slow network | Check network bandwidth, increase timeout                    |

### 18.2 SSL Certificate Issues

| Symptom                                       | Cause                        | Fix                                                               |
| --------------------------------------------- | ---------------------------- | ----------------------------------------------------------------- |
| `FATAL: could not load server certificate`    | Wrong permissions            | `chmod 600 server.key; chown postgres:postgres server.*`          |
| `SSL connection has been closed unexpectedly` | Key/cert mismatch            | Re-issue certificate from same CA                                 |
| MinIO won't start after cert deploy           | Certificate chain incomplete | Include CA chain in `CAs/ca.crt`                                  |
| `certificate verify failed` from API          | CA not trusted               | Add CA cert to connection string: `Trust Server Certificate=true` |

### 18.3 Common Development Issues

| Issue                                       | Resolution                                                    |
| ------------------------------------------- | ------------------------------------------------------------- |
| `CertificateAuthorityService` not resolving | Verify singleton registration in `Program.cs`                 |
| Audit events not appearing                  | Check `IvfDbContext.CertificateAuditEvents` DbSet mapping     |
| Auto-renewal not triggering                 | Check `AutoRenewEnabled=true` and `RenewBeforeDays` threshold |
| SignalR deploy logs not received            | Ensure client joins group: `JoinBackupGroup(operationId)`     |
| CRL Distribution Point missing              | Set `CertificateAuthority:BaseUrl` in appsettings.json        |

### 18.4 Useful Diagnostic Commands

```bash
# Check all SSL connections
docker exec ivf-db psql -U postgres -c "SELECT pid, ssl, version, cipher FROM pg_stat_ssl;"

# View certificate details
docker exec ivf-db openssl x509 -in /var/lib/postgresql/data/server.crt -text -noout

# Test MinIO TLS
curl -k https://localhost:9000/minio/health/live

# Check auto-renewal service logs
docker logs ivf-api 2>&1 | grep -i "auto-renewal"

# Verify CRL via public endpoint
curl -s http://localhost:5000/api/pki/crl/{ca-id} -o /tmp/test.crl
openssl crl -inform DER -in /tmp/test.crl -text -noout
```

---

## Appendix: File Inventory

| File                                                        | Lines | Purpose                                   |
| ----------------------------------------------------------- | ----- | ----------------------------------------- |
| `src/IVF.Domain/Entities/CertificateAuthority.cs`           | ~150  | CA entity with serial/CRL counters        |
| `src/IVF.Domain/Entities/ManagedCertificate.cs`             | ~230  | Certificate entity with lifecycle methods |
| `src/IVF.Domain/Entities/CertDeploymentLog.cs`              | ~90   | Deploy log with JSON LogLines             |
| `src/IVF.Domain/Entities/CertificateRevocationList.cs`      | ~80   | CRL entity (PEM + DER)                    |
| `src/IVF.Domain/Entities/CertificateAuditEvent.cs`          | ~100  | Immutable audit events                    |
| `src/IVF.API/Services/CertificateAuthorityService.cs`       | ~1750 | All PKI logic + DTOs                      |
| `src/IVF.API/Services/CertAutoRenewalService.cs`            | ~60   | Background auto-renewal                   |
| `src/IVF.API/Endpoints/CertificateAuthorityEndpoints.cs`    | ~280  | 30+ REST endpoints                        |
| `ivf-client/src/app/core/models/backup.models.ts`           | ~720  | TypeScript DTOs                           |
| `ivf-client/src/app/core/services/backup.service.ts`        | ~510  | Angular HTTP service                      |
| `ivf-client/src/app/features/admin/certificate-management/` | —     | 9-tab management UI                       |

## Appendix: Security Considerations

1. **Private keys** are stored in the database (encrypted at rest via PostgreSQL). Production should use HSM integration.
2. **SSH access** uses `StrictHostKeyChecking=no` for dev convenience. Production should use known_hosts verification.
3. **CRL validity** is 7 days. Clients should refresh CRLs periodically.
4. **OCSP endpoint** returns JSON (not ASN.1/DER per strict RFC 6960). Simplified for internal use.
5. **Admin endpoints** require `AdminOnly` authorization policy (JWT). Public PKI endpoints are unauthenticated per RFC.
6. **Self-signed CA trust**: MinIO SDK uses `ServerCertificateCustomValidationCallback` to trust the private CA.
7. **File permissions**: PostgreSQL requires `postgres:postgres` ownership (UID 999 Debian, 70 Alpine). MinIO uses root.
8. **Temp file cleanup**: All deploy operations write to temp files and cleanup in `finally` blocks.
