# SignServer Security Hardening — Developer Implementation Guide

> **Phiên bản:** 2.1  
> **Ngày cập nhật:** 2026-02-23  
> **Stack:** .NET 10 · SignServer CE 7.3.2 · WildFly 35.0.1 · Docker Compose · SoftHSM2  
> **Mục tiêu:** Bảo mật toàn bộ kênh giao tiếp giữa IVF API và SignServer bằng mTLS + certificate authorization + PKCS#11 + compliance audit

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Phase 1 — Key Management & Network Hardening](#2-phase-1--key-management--network-hardening)
3. [Phase 2 — mTLS & Certificate Authorization](#3-phase-2--mtls--certificate-authorization)
4. [Thay đổi code chi tiết](#4-thay-đổi-code-chi-tiết)
5. [Cấu hình (appsettings)](#5-cấu-hình-appsettings)
6. [Cấu trúc chứng chỉ & bí mật](#6-cấu-trúc-chứng-chỉ--bí-mật)
7. [Quy trình sau khi clone / reset](#7-quy-trình-sau-khi-clone--reset)
8. [Troubleshooting](#8-troubleshooting)
9. [Security Checklist](#9-security-checklist)
10. [Phase 3 — Hardening & Monitoring](#10-phase-3--hardening--monitoring)
11. [Phase 4 — Compliance & PKCS#11](#11-phase-4--compliance--pkcs11)
12. [Phase 5 — TSA, OCSP & Certificate Lifecycle](#12-phase-5--tsa-ocsp--certificate-lifecycle)

---

## 1. Tổng quan kiến trúc

```
┌─────────────┐  mTLS (P12 client cert)  ┌──────────────────┐
│   IVF.API   │ ───────────────────────► │   SignServer CE  │
│  (.NET 10)  │   HTTPS :9443            │  (WildFly 35)    │
└─────────────┘                          └────────┬─────────┘
      │                                           │
      │  REST /process                            │ P12CryptoToken (Phase 1-3)
      │  multipart/form-data                      │ PKCS11CryptoToken (Phase 4)
      │                                           │  └── SoftHSM2 FIPS 140-2 L1
      ▼                                           ▼
┌─────────────┐                          ┌──────────────────┐
│  MinIO S3   │                          │    EJBCA CE      │
│ (signed PDF)│                          │  (Internal CA)   │
└─────────────┘                          └──────────────────┘
```

**Luồng ký số:**

1. API gửi PDF → SignServer qua HTTPS (port 9443) với client certificate (mTLS)
2. SignServer xác thực client cert bằng `ClientCertAuthorizer` (kiểm tra serial + issuer DN)
3. Worker ký PDF bằng private key trong P12 keystore (persistent volume)
4. PDF đã ký được trả về API → lưu vào MinIO S3

---

## 2. Phase 1 — Key Management & Network Hardening

### 2.1. Vấn đề trước Phase 1

| Vấn đề                  | Mức độ   | Mô tả                                                        |
| ----------------------- | -------- | ------------------------------------------------------------ |
| V1 — Key lưu `/tmp/`    | Critical | PKCS12 keystore bị xóa khi container restart                 |
| V2 — Key permission 644 | High     | Bất kỳ process nào trong container cũng đọc được private key |
| V4 — Port 9080 public   | Medium   | SignServer HTTP endpoint truy cập được từ mọi host           |

### 2.2. Giải pháp đã triển khai

#### 2.2.1. Di chuyển keystore sang persistent volume

**Trước:**

```
KEYSTOREPATH = /tmp/pdfsigner_user.p12
```

**Sau:**

```
KEYSTOREPATH = /opt/keyfactor/persistent/keys/pdfsigner_user.p12
```

**Code thay đổi** — `UserSignatureEndpoints.cs`:

```csharp
// Đường dẫn keystore mới (persistent volume)
const string keyDir = "/opt/keyfactor/persistent/keys";
var keystorePath = $"{keyDir}/{workerName.ToLowerInvariant()}.p12";
```

#### 2.2.2. Phân quyền file (chmod 400)

Sau khi tạo keystore bằng keytool, set quyền owner read-only:

```csharp
// Step 0: Đảm bảo thư mục tồn tại với quyền đúng
await RunDockerExecAsRootAsync("ivf-signserver",
    $"mkdir -p {keyDir} && chown 10001:root {keyDir} && chmod 700 {keyDir}", logger);

// Sau keytool -genkeypair:
await RunDockerExecAsRootAsync("ivf-signserver",
    $"chmod 400 {keystorePath} && chown 10001:root {keystorePath}", logger);
```

**Tại sao dùng `RunDockerExecAsRootAsync`?**

- Container SignServer chạy với uid `10001` (non-root)
- Thư mục persistent volume thuộc sở hữu root
- Cần exec `-u root` để `mkdir`, `chown`, `chmod`

```csharp
private static async Task RunDockerExecAsRootAsync(
    string container, string command, ILogger logger)
{
    await RunProcessAsync("docker",
        $"exec -u root {container} bash -c \"{command}\"", logger);
}
```

#### 2.2.3. Giới hạn port 9080 chỉ localhost

**docker-compose.yml trước:**

```yaml
ports:
  - "9080:8080" # Ai cũng truy cập được
```

**docker-compose.yml sau:**

```yaml
ports:
  - "127.0.0.1:9080:8080" # Chỉ localhost
```

> **Production:** Xóa hoàn toàn dòng port 9080. API truy cập SignServer qua Docker network nội bộ.

#### 2.2.4. Security Status Endpoint

Thêm endpoint `GET /api/admin/signing/security-status` để admin kiểm tra tình trạng bảo mật:

```csharp
group.MapGet("/security-status", (IOptions<DigitalSigningOptions> options, IHostEnvironment env) =>
{
    var opts = options.Value;
    var warnings = new List<string>();
    var issues = new List<string>();

    if (opts.SkipTlsValidation)
        issues.Add("SkipTlsValidation=true — disabled in production.");
    if (string.IsNullOrEmpty(opts.ClientCertificatePath))
        warnings.Add("No client certificate configured.");
    // ... more checks ...

    var securityScore = 100
        - (issues.Count * 25)
        - (warnings.Count * 10);

    return Results.Ok(new { securityScore, level, mtls, tls, audit, issues, warnings });
});
```

**Response mẫu (development):**

```json
{
  "securityScore": 45,
  "level": "moderate",
  "mtls": {
    "enabled": true,
    "required": false,
    "clientCertConfigured": true,
    "trustedCaConfigured": true
  },
  "tls": { "usesHttps": true, "skipValidation": true },
  "audit": { "enabled": false }
}
```

---

## 3. Phase 2 — mTLS & Certificate Authorization

### 3.1. Vấn đề trước Phase 2

| Vấn đề                | Mức độ   | Mô tả                                             |
| --------------------- | -------- | ------------------------------------------------- |
| V3 — AUTHTYPE=NOAUTH  | Critical | Bất kỳ ai gửi POST đến `/process` đều ký được PDF |
| V5 — Không mTLS       | High     | Giao tiếp API↔SignServer không xác thực client    |
| V6 — HTTP (không TLS) | High     | Traffic giữa API và SignServer gửi plaintext      |

### 3.2. Tổng quan giải pháp

```
                     Bước 1: Tạo Internal CA
                     Bước 2: Generate client cert (API) + admin cert
                     Bước 3: Cấu hình WildFly truststore + SSL context
                     Bước 4: Set AUTHTYPE=ClientCertAuthorizer trên workers
                     Bước 5: Cập nhật mã nguồn API dùng mTLS
```

### 3.3. Bước 1 — Tạo Internal Root CA

```powershell
# Windows: Dùng openssl từ Git for Windows
$env:MSYS_NO_PATHCONV = "1"  # Tránh MSYS path conversion trên Windows
$openssl = "C:\Program Files\Git\usr\bin\openssl.exe"

# Tạo CA private key (RSA 4096, mã hóa AES-256)
& $openssl genrsa -aes256 -passout pass:changeit -out certs/ca/ca.key 4096

# Tạo CA certificate (self-signed, 10 năm)
& $openssl req -new -x509 -sha256 -days 3650 `
    -key certs/ca/ca.key -passin pass:changeit `
    -out certs/ca/ca.pem `
    -subj "/C=VN/ST=Ho Chi Minh/O=IVF Clinic/OU=IT Department/CN=IVF Internal Root CA"
```

**Kết quả:**

- `certs/ca/ca.key` — CA private key (encrypted, **KHÔNG commit vào git**)
- `certs/ca/ca.pem` — CA public certificate
- `certs/ca-chain.pem` — CA chain (copy của ca.pem, dùng cho TLS validation)

### 3.4. Bước 2 — Generate Client Certificates

#### API Client Certificate (dùng cho mTLS)

```powershell
# 1. Generate key
& $openssl genrsa -out certs/api/api-client.key 2048

# 2. Create CSR
& $openssl req -new -key certs/api/api-client.key `
    -out certs/api/api-client.csr `
    -subj "/C=VN/ST=Ho Chi Minh/O=IVF Clinic/OU=API Service/CN=ivf-api-client"

# 3. Create extensions file (clientAuth EKU)
@"
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=clientAuth
subjectAltName=DNS:ivf-api,DNS:localhost
"@ | Set-Content -Path certs/api/api-client.ext

# 4. Sign with CA
& $openssl x509 -req -sha256 -days 365 `
    -in certs/api/api-client.csr `
    -CA certs/ca/ca.pem -CAkey certs/ca/ca.key -passin pass:changeit `
    -set_serial ("0x" + (& $openssl rand -hex 20)) `
    -extfile certs/api/api-client.ext `
    -out certs/api/api-client.pem

# 5. Export to P12 (SignServer/WildFly cần P12)
$certPassword = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
& $openssl pkcs12 -export `
    -in certs/api/api-client.pem `
    -inkey certs/api/api-client.key `
    -certfile certs/ca/ca.pem `
    -out certs/api/api-client.p12 `
    -passout "pass:$certPassword"

# 6. Lưu password vào Docker Secret file
$certPassword | Set-Content -NoNewline -Path secrets/api_cert_password.txt

# 7. Lưu serial number (cần cho addauthorizedclient)
& $openssl x509 -in certs/api/api-client.pem -serial -noout |
    ForEach-Object { $_ -replace 'serial=', '' } |
    Set-Content -NoNewline -Path certs/api/api-client-serial.txt
```

> **Serial number ví dụ:** `2EB6EB968DE282D3D8E731F79081CA1405836E08`  
> **Issuer DN:** `CN=IVF Internal Root CA, OU=IT Department, O=IVF Clinic, ST=Ho Chi Minh, C=VN`

#### Admin Certificate (tương tự, thay OU + CN)

```powershell
# Tương tự API client cert, chỉ thay:
#   OU=Administration, CN=ivf-admin
#   Serial tăng thêm 1 byte cuối
```

### 3.5. Bước 3 — Cấu hình WildFly mTLS

#### 3.5.1. Copy CA cert vào container

```bash
# CA cert được mount tự động qua docker-compose.yml:
#   ./certs/ca/ca.pem:/opt/keyfactor/persistent/keys/ivf-ca.pem:ro
```

#### 3.5.2. Tạo truststore (JKS)

```bash
docker exec ivf-signserver keytool -import -trustcacerts \
    -alias ivf-internal-ca \
    -file /opt/keyfactor/persistent/keys/ivf-ca.pem \
    -keystore /opt/keyfactor/persistent/truststore.jks \
    -storepass changeit -noprompt
```

> **Lưu ý:** Truststore ở `/opt/keyfactor/persistent/truststore.jks` (không phải `/keys/`). Thư mục `persistent` được mount là Docker volume nên truststore tồn tại qua container restart.

#### 3.5.3. Cấu hình WildFly Elytron SSL

```bash
docker exec ivf-signserver /opt/keyfactor/wildfly-35.0.1.Final/bin/jboss-cli.sh \
    --connect --commands="
        /subsystem=elytron/key-store=trustKS:add(\
            path=/opt/keyfactor/persistent/truststore.jks,\
            type=JKS,\
            credential-reference={clear-text=changeit}),
        /subsystem=elytron/trust-manager=httpsTM:add(\
            key-store=trustKS),
        /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(\
            name=trust-manager,value=httpsTM),
        /subsystem=elytron/server-ssl-context=httpsSSC:write-attribute(\
            name=want-client-auth,value=true)"
```

> **Elytron resource names:** `trustKS` (key-store), `httpsTM` (trust-manager), `httpsSSC` (server-ssl-context). Nhất quán với `scripts/init-mtls.sh`.

**`want-client-auth` vs `need-client-auth`:**

| Setting                 | Hành vi                                                                                      |
| ----------------------- | -------------------------------------------------------------------------------------------- |
| `want-client-auth=true` | Server _yêu cầu_ client cert, nhưng vẫn cho phép kết nối không cert (health check hoạt động) |
| `need-client-auth=true` | Server _bắt buộc_ client cert, mọi kết nối không cert đều bị reject (health check fail)      |

> Chọn `want-client-auth` vì health check (`/healthcheck/signserverhealth`) không gửi client cert. Authorization thực tế do `ClientCertAuthorizer` tại worker level đảm bảo.

#### 3.5.4. Reload WildFly

```bash
docker exec ivf-signserver /opt/keyfactor/wildfly-35.0.1.Final/bin/jboss-cli.sh \
    --connect --command=":reload"
```

#### 3.5.5. Script tự động: `scripts/init-mtls.sh`

Script idempotent cấu hình mọi bước mTLS (truststore + Elytron + reload). Hỗ trợ chạy từ **host** hoặc **trong container**:

```bash
# Từ host (không cần docker exec):
bash scripts/init-mtls.sh

# Hoặc trong container:
docker exec ivf-signserver bash /opt/keyfactor/persistent/init-mtls.sh
```

Script kiểm tra từng resource (`trustKS`, `httpsTM`, `httpsSSC`) trước khi tạo — an toàn chạy lại nhiều lần. Chỉ reload WildFly khi có thay đổi.

**Production** dùng `scripts/init-mtls-production.sh` với `need-client-auth=true` và thêm HTTP health listener trên port 8081 (để health check không cần client cert).

Mount trong `docker-compose.yml`:

```yaml
volumes:
  - ./scripts/init-mtls.sh:/opt/keyfactor/persistent/init-mtls.sh:ro
```

> **Quan trọng:** WildFly configuration nằm trên tmpfs (`standalone/configuration`). Mỗi lần container được tạo mới (`docker compose up` sau `docker compose down`), cần chạy lại script.

### 3.6. Bước 4 — Cấu hình Workers với ClientCertAuthorizer

#### Danh sách workers hiện tại

| Worker ID | Worker Name               | Mô tả           |
| --------- | ------------------------- | --------------- |
| 1         | PDFSigner                 | Worker mặc định |
| 272       | PDFSigner_techinical      | Ký thuật viên   |
| 444       | PDFSigner_head_department | Trưởng khoa     |
| 597       | PDFSigner_doctor1         | Bác sĩ          |
| 907       | PDFSigner_admin           | Quản trị viên   |

#### Set AUTHTYPE trên tất cả workers

```bash
CLI="/opt/signserver/bin/signserver"
SERIAL="2EB6EB968DE282D3D8E731F79081CA1405836E08"
ISSUER="CN=IVF Internal Root CA,OU=IT Department,O=IVF Clinic,ST=Ho Chi Minh,C=VN"

for WID in 1 272 444 597 907; do
    docker exec ivf-signserver $CLI setproperty $WID AUTHTYPE \
        org.signserver.server.ClientCertAuthorizer
    docker exec ivf-signserver $CLI addauthorizedclient $WID $SERIAL "$ISSUER"
    docker exec ivf-signserver $CLI reload $WID
done
```

> **QUAN TRỌNG:** Sử dụng lệnh `addauthorizedclient`, KHÔNG dùng `setproperty AUTHORIZED_CLIENTS`. Lệnh `setproperty` đặt giá trị String nhưng SignServer cần HashSet, gây `ClassCastException`.

#### Xác minh

```bash
# Kiểm tra AUTHTYPE
docker exec ivf-signserver /opt/signserver/bin/signserver getconfig 1 | grep AUTHTYPE
# Kết quả: AUTHTYPE=org.signserver.server.ClientCertAuthorizer

# Kiểm tra authorized clients
docker exec ivf-signserver /opt/signserver/bin/signserver listauthorizedclients 1
# Kết quả: SN: 2EB6EB968DE282D3D8E731F79081CA1405836E08, Issuer DN: CN=IVF...

# Kiểm tra worker status
docker exec ivf-signserver /opt/signserver/bin/signserver getstatus brief all
# Tất cả 5 workers: Status: Active
```

### 3.7. Bước 5 — Cập nhật mã nguồn API

Xem phần [4. Thay đổi code chi tiết](#4-thay-đổi-code-chi-tiết).

---

## 4. Thay đổi code chi tiết

### 4.1. File: `src/IVF.API/Services/DigitalSigningOptions.cs`

**Thêm mới hoàn toàn các property sau:**

```csharp
// mTLS client certificate
public string? ClientCertificatePath { get; set; }
public string? ClientCertificatePassword { get; set; }
public string? ClientCertificatePasswordFile { get; set; }
public string? TrustedCaCertPath { get; set; }
public bool RequireMtls { get; set; } = false;
public bool EnableAuditLogging { get; set; } = false;

// SignServer admin (CLI-based via docker exec)
public string SignServerContainerName { get; set; } = "ivf-signserver";
```

**Method `ResolveClientCertificatePassword()`:**

```csharp
/// Docker Secret file ưu tiên hơn giá trị cứng
public string? ResolveClientCertificatePassword()
{
    if (!string.IsNullOrEmpty(ClientCertificatePasswordFile) &&
        File.Exists(ClientCertificatePasswordFile))
    {
        return File.ReadAllText(ClientCertificatePasswordFile).Trim();
    }
    return ClientCertificatePassword;
}
```

> **Lý do:** Production dùng Docker Secret (`/run/secrets/api_cert_password`), dev dùng file trực tiếp.

**Method `ValidateProduction()`:**

```csharp
public void ValidateProduction()
{
    if (!Enabled) return;

    if (RequireMtls)
    {
        if (string.IsNullOrEmpty(ClientCertificatePath))
            throw new InvalidOperationException(
                "ClientCertificatePath is required when RequireMtls=true");
        if (!File.Exists(ClientCertificatePath))
            throw new InvalidOperationException(
                $"Client certificate not found: {ClientCertificatePath}");
        if (string.IsNullOrEmpty(ResolveClientCertificatePassword()))
            throw new InvalidOperationException(
                "Client certificate password is required.");
    }

    // SkipTlsValidation + RequireMtls = cấm
    if (SkipTlsValidation && RequireMtls)
        throw new InvalidOperationException(
            "SkipTlsValidation cannot be true when RequireMtls is enabled.");

    // HTTP + RequireMtls = cấm
    if (SignServerUrl.StartsWith("http://") && RequireMtls)
        throw new InvalidOperationException(
            "SignServerUrl must use HTTPS when RequireMtls is enabled.");
}
```

### 4.2. File: `src/IVF.API/Program.cs`

**HttpClient handler setup — trước Phase 2:**

```csharp
// ĐƠN GIẢN: chỉ accept mọi cert
handler.ServerCertificateCustomValidationCallback =
    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
```

**Sau Phase 2:**

```csharp
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // 1. TLS Validation
    if (signingOptions.SkipTlsValidation)
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    else if (!string.IsNullOrEmpty(signingOptions.TrustedCaCertPath)
             && File.Exists(signingOptions.TrustedCaCertPath))
    {
        // Custom CA validation — trust Internal CA chain
        var trustedCa = X509CertificateLoader.LoadCertificateFromFile(
            signingOptions.TrustedCaCertPath);
        handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) =>
        {
            if (errors == SslPolicyErrors.None) return true;
            if (cert != null)
            {
                chain ??= new X509Chain();
                chain.ChainPolicy.ExtraStore.Add(trustedCa);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags =
                    X509VerificationFlags.AllowUnknownCertificateAuthority;
                return chain.Build(X509CertificateLoader.LoadCertificate(
                    cert.GetRawCertData()));
            }
            return false;
        };
    }

    // 2. Client Certificate (mTLS)
    if (!string.IsNullOrEmpty(signingOptions.ClientCertificatePath))
    {
        var certPassword = signingOptions.ResolveClientCertificatePassword();
        handler.ClientCertificates.Add(
            X509CertificateLoader.LoadPkcs12FromFile(
                signingOptions.ClientCertificatePath, certPassword));
    }

    return handler;
});
```

**Lưu ý `.NET 10 SYSLIB0057`:**

| API cũ (obsolete)                                     | API mới                                                    |
| ----------------------------------------------------- | ---------------------------------------------------------- |
| `new X509Certificate2(string path)`                   | `X509CertificateLoader.LoadCertificateFromFile(path)`      |
| `new X509Certificate2(string path, string? password)` | `X509CertificateLoader.LoadPkcs12FromFile(path, password)` |
| `new X509Certificate2(byte[] data)`                   | `X509CertificateLoader.LoadCertificate(data)`              |

### 4.3. File: `src/IVF.API/Endpoints/UserSignatureEndpoints.cs`

#### Provisioning: `ProvisionUserCertificateAsync()`

**Thay đổi chính:**

1. **Keystore path** → persistent volume:

   ```csharp
   const string keyDir = "/opt/keyfactor/persistent/keys";
   ```

2. **AUTHTYPE** → `ClientCertAuthorizer`:

   ```csharp
   $"WORKER{workerId}.AUTHTYPE = org.signserver.server.ClientCertAuthorizer\n"
   ```

3. **Authorized client** (Step 4 mới):

   ```csharp
   // Đọc serial + issuer từ API client cert
   using var apiCert = X509CertificateLoader.LoadPkcs12FromFile(
       opts.ClientCertificatePath, opts.ResolveClientCertificatePassword());
   var serial = apiCert.SerialNumber;
   var issuerDN = apiCert.Issuer;

   await RunDockerExecAsync("ivf-signserver",
       $"bin/signserver addauthorizedclient {workerId} {serial} \"{issuerDN}\"",
       logger);
   ```

4. **Temp file cleanup** (bảo mật):
   ```csharp
   // Xóa file properties tạm (chứa keystore password)
   await RunDockerExecAsync("ivf-signserver",
       $"rm -f {containerPropsPath}", logger);
   ```

#### Signing: `SignPdfWithWorkerAsync()`

**Thêm mTLS client cert:**

```csharp
if (!string.IsNullOrEmpty(opts.ClientCertificatePath)
    && File.Exists(opts.ClientCertificatePath))
{
    var certPassword = opts.ResolveClientCertificatePassword();
    handler.ClientCertificates.Add(
        X509CertificateLoader.LoadPkcs12FromFile(
            opts.ClientCertificatePath, certPassword));
}
```

### 4.4. File: `src/IVF.API/Endpoints/SigningAdminEndpoints.cs`

**`CreateHandler()` helper — thêm mTLS support:**

```csharp
private static HttpClientHandler CreateHandler(DigitalSigningOptions opts)
{
    var handler = new HttpClientHandler();
    if (opts.SkipTlsValidation)
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    if (!string.IsNullOrEmpty(opts.ClientCertificatePath)
        && File.Exists(opts.ClientCertificatePath))
    {
        var certPassword = opts.ResolveClientCertificatePassword();
        handler.ClientCertificates.Add(
            X509CertificateLoader.LoadPkcs12FromFile(
                opts.ClientCertificatePath, certPassword));
    }
    return handler;
}
```

**Config endpoint — thêm security fields:**

```csharp
hasClientCertificate = !string.IsNullOrEmpty(opts.ClientCertificatePath),
requireMtls = opts.RequireMtls,
enableAuditLogging = opts.EnableAuditLogging,
hasTrustedCa = !string.IsNullOrEmpty(opts.TrustedCaCertPath)
```

### 4.5. File: `src/IVF.API/Services/SignServerDigitalSigningService.cs`

**Thêm audit logging (SHA256 hash tracking):**

```csharp
if (_options.EnableAuditLogging)
{
    var inputHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(pdfBytes));
    _logger.LogInformation(
        "Signing PDF: worker={Worker}, inputHash={Hash}, inputSize={Size}",
        workerName, inputHash, pdfBytes.Length);
}
```

### 4.6. File: `docker-compose.yml`

**SignServer service — volumes và tmpfs:**

```yaml
signserver:
  environment:
    - SIGNSERVER_NODEID=node1 # Ổn định worker ID
  ports:
    - "9443:8443" # HTTPS Admin (không expose HTTP 8080)
  tmpfs:
    - standalone/configuration:size=50M # WildFly config (cho jboss-cli ghi trong read_only)
  volumes:
    - signserver_persistent:/opt/keyfactor/persistent
    - ./certs/ca/ca.pem:/opt/keyfactor/persistent/keys/ivf-ca.pem:ro
    - ./scripts/init-mtls.sh:/opt/keyfactor/persistent/init-mtls.sh:ro
```

> **`standalone/configuration` tmpfs:** Cần thiết để `jboss-cli.sh` ghi được `standalone.xml` khi service dùng `read_only: true`. Mất khi container recreate nên cần `init-mtls.sh` sau mỗi `docker compose up`.

**API service — mount certs:**

```yaml
api:
  volumes:
    - ./certs/api/api-client.p12:/app/certs/api-client.p12:ro
    - ./certs/ca-chain.pem:/app/certs/ca-chain.pem:ro
    - ./secrets/api_cert_password.txt:/run/secrets/api_cert_password:ro
```

---

## 5. Cấu hình (appsettings)

### 5.1. Development — `appsettings.json`

```jsonc
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://localhost:9443/signserver", // HTTPS qua host
    "SkipTlsValidation": true, // Dev: accept self-signed cert
    "ClientCertificatePath": "../../certs/api/api-client.p12", // Đường dẫn tương đối
    "ClientCertificatePassword": null,
    "ClientCertificatePasswordFile": "../../secrets/api_cert_password.txt",
    "TrustedCaCertPath": "../../certs/ca-chain.pem",
    "RequireMtls": false, // Dev: không enforce validation
    "EnableAuditLogging": false,
  },
}
```

> **Đường dẫn tương đối `../../certs/`:** Vì `dotnet run` chạy từ `src/IVF.API/`, nên `../../` trỏ về project root `D:\Pr.Net\IVF\`.

### 5.2. Production — `appsettings.Production.json`

```jsonc
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver", // Docker network hostname
    "SkipTlsValidation": false, // PHẢI false
    "ClientCertificatePath": "/app/certs/api-client.p12", // Mount path trong container
    "ClientCertificatePassword": null,
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password", // Docker Secret
    "TrustedCaCertPath": "/app/certs/ca-chain.pem",
    "RequireMtls": true, // PHẢI true
    "EnableAuditLogging": true, // Audit log bật
  },
}
```

### 5.3. So sánh Dev vs Production

| Property                        | Development                           | Production                       | Lý do                             |
| ------------------------------- | ------------------------------------- | -------------------------------- | --------------------------------- |
| `SignServerUrl`                 | `https://localhost:9443`              | `https://signserver:8443`        | Dev: host port; Prod: Docker DNS  |
| `SkipTlsValidation`             | `true`                                | `false`                          | Dev: self-signed OK; Prod: cấm    |
| `ClientCertificatePath`         | `../../certs/api/api-client.p12`      | `/app/certs/api-client.p12`      | Relative vs Docker mount          |
| `ClientCertificatePasswordFile` | `../../secrets/api_cert_password.txt` | `/run/secrets/api_cert_password` | Local file vs Docker Secret       |
| `RequireMtls`                   | `false`                               | `true`                           | Dev: không crash nếu cert missing |
| `EnableAuditLogging`            | `false`                               | `true`                           | Dev: giảm noise log               |

---

## 6. Cấu trúc chứng chỉ & bí mật

### 6.1. Cấu trúc thư mục

```
IVF/
├── certs/                          # .gitignore
│   ├── ca/
│   │   ├── ca.key                  # CA private key (AES-256 encrypted)
│   │   └── ca.pem                  # CA public certificate (10 năm)
│   ├── ca-chain.pem                # CA chain (= copy of ca.pem)
│   ├── signserver/
│   │   ├── signserver-tls.key      # TLS server key
│   │   ├── signserver-tls.pem      # TLS server cert
│   │   └── signserver-tls.p12      # TLS server P12
│   ├── api/
│   │   ├── api-client.key          # API client key
│   │   ├── api-client.pem          # API client cert
│   │   ├── api-client.p12          # ← Dùng cho mTLS
│   │   ├── api-client.csr          # CSR (giữ lại để reference)
│   │   ├── api-client.ext          # Extensions file
│   │   └── api-client-serial.txt   # Serial hex (cần cho addauthorizedclient)
│   └── admin/
│       ├── admin.key, .pem, .p12
│       └── admin-serial.txt
├── secrets/                        # .gitignore
│   ├── api_cert_password.txt       # 32 chars random
│   ├── signserver_tls_password.txt # 32 chars random
│   └── admin_cert_password.txt     # 16 chars random
└── scripts/
    ├── generate-certs.sh           # Tạo lại toàn bộ certs
    ├── init-mtls.sh                # Cấu hình WildFly mTLS (idempotent)
    ├── signserver-init.sh          # Init worker ban đầu
    └── rotate-keys.sh              # Rotate certs
```

### 6.2. Thông tin chứng chỉ

| Cert           | Subject                                                                         | Validity | Purpose                            |
| -------------- | ------------------------------------------------------------------------------- | -------- | ---------------------------------- |
| CA             | `CN=IVF Internal Root CA, OU=IT Department, O=IVF Clinic, ST=Ho Chi Minh, C=VN` | 10 năm   | Root CA, sign client/server certs  |
| API Client     | `CN=ivf-api-client, OU=API Service, O=IVF Clinic, ST=Ho Chi Minh, C=VN`         | 1 năm    | mTLS authentication API→SignServer |
| Admin          | `CN=ivf-admin, OU=Administration, O=IVF Clinic, ST=Ho Chi Minh, C=VN`           | 1 năm    | Admin access                       |
| SignServer TLS | `CN=signserver, DNS:signserver,localhost,127.0.0.1`                             | 825 ngày | TLS server cert                    |

---

## 7. Quy trình sau khi clone / reset

### 7.1. Fresh Clone (chưa có certs)

```powershell
# 1. Tạo thư mục
mkdir certs/ca, certs/api, certs/admin, certs/signserver, secrets -Force

# 2. Generate certificates (xem Bước 1-2 ở Phase 2)
# Hoặc chạy script:
bash scripts/generate-certs.sh

# 3. Start containers
docker compose up -d

# 4. Chờ SignServer healthy (~2 phút)
docker compose logs -f signserver  # chờ "ALLOK"

# 5. Cấu hình mTLS trên WildFly (chạy từ host)
bash scripts/init-mtls.sh

# 6. Cấu hình workers (nếu chưa có worker nào)
# Chạy signserver-init.sh hoặc tạo worker qua Admin UI
# https://localhost:9443/signserver/adminweb/

# 7. Set AUTHTYPE cho tất cả workers
SERIAL=$(cat certs/api/api-client-serial.txt)
for WID in 1 272 444 597 907; do
    docker exec ivf-signserver /opt/signserver/bin/signserver setproperty $WID AUTHTYPE \
        org.signserver.server.ClientCertAuthorizer
    docker exec ivf-signserver /opt/signserver/bin/signserver addauthorizedclient $WID \
        $SERIAL "CN=IVF Internal Root CA,OU=IT Department,O=IVF Clinic,ST=Ho Chi Minh,C=VN"
    docker exec ivf-signserver /opt/signserver/bin/signserver reload $WID
done

# 8. Start API
cd src/IVF.API
dotnet run

# 9. Verify
# GET http://localhost:5079/api/admin/signing/security-status
# POST http://localhost:5079/api/admin/signing/test-sign
```

### 7.2. Container Restart

Sau khi `docker compose restart signserver`:

```bash
# WildFly mất Elytron config khi restart → chạy lại init-mtls.sh từ host
bash scripts/init-mtls.sh

# Worker config + authorized clients persist trong DB → không cần cấu hình lại
```

### 7.3. Rotate API Client Certificate

```powershell
# 1. Tạo cert mới
# ... (openssl commands giống Bước 2)

# 2. Lấy serial mới
$newSerial = Get-Content certs/api/api-client-serial.txt

# 3. Thêm authorized client mới vào tất cả workers
foreach ($wid in @(1, 272, 444, 597, 907)) {
    docker exec ivf-signserver bin/signserver addauthorizedclient $wid `
        $newSerial "CN=IVF Internal Root CA,OU=IT Department,O=IVF Clinic,ST=Ho Chi Minh,C=VN"
}

# 4. Restart API để load cert mới
# (hoặc hot-reload nếu dùng IOptionsMonitor)

# 5. Xóa cert cũ khỏi authorized list (grace period)
$oldSerial = "2EB6EB968DE282D3D8E731F79081CA1405836E08"
foreach ($wid in @(1, 272, 444, 597, 907)) {
    docker exec ivf-signserver bin/signserver removeauthorizedclient $wid `
        $oldSerial "CN=IVF Internal Root CA,OU=IT Department,O=IVF Clinic,ST=Ho Chi Minh,C=VN"
}
```

---

## 8. Troubleshooting

### 8.1. MSYS Path Conversion (Windows)

**Triệu chứng:** OpenSSL trên Git Bash chuyển `/C=VN/...` thành `C:\=VN\...`

**Fix:**

```powershell
$env:MSYS_NO_PATHCONV = "1"
# hoặc trong bash:
MSYS_NO_PATHCONV=1 openssl req ...
```

### 8.2. AUTHORIZED_CLIENTS ClassCastException

**Triệu chứng:** Worker báo lỗi `String cannot be cast to HashSet` sau khi setproperty

**Fix:**

```bash
# Xóa property sai
docker exec ivf-signserver bin/signserver removeproperty <workerId> AUTHORIZED_CLIENTS

# Dùng đúng lệnh
docker exec ivf-signserver bin/signserver addauthorizedclient <workerId> <serial> "<issuerDN>"
```

### 8.3. HTTP 400 "Client authentication is required"

**Triệu chứng:** `curl POST http://localhost:9080/signserver/process` → 400

**Giải thích:** Đúng hành vi! Worker yêu cầu client certificate authentication. Requests không có cert sẽ bị reject bởi `ClientCertAuthorizer`.

### 8.4. Windows curl "SEC_E_INTERNAL_ERROR" với P12

**Triệu chứng:** `curl --cert api-client.p12:password` → Schannel error

**Fix:** Dùng API endpoint test thay cho curl trực tiếp:

```
POST /api/admin/signing/test-sign
```

### 8.5. File Lock khi Build

**Triệu chứng:** `MSB3021: Unable to copy file ... IVF.API.dll`

**Fix:**

```powershell
# Tìm và kill process đang giữ file
Get-Process -Name "IVF.API" -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build
```

### 8.6. SYSLIB0057 Warning (.NET 10)

**Triệu chứng:** `warning SYSLIB0057: 'X509Certificate2(string)' is obsolete`

**Fix:** Thay tất cả constructor calls:

```csharp
// ❌ Cũ
new X509Certificate2(path);
new X509Certificate2(path, password);
new X509Certificate2(bytes);

// ✅ Mới
X509CertificateLoader.LoadCertificateFromFile(path);
X509CertificateLoader.LoadPkcs12FromFile(path, password);
X509CertificateLoader.LoadCertificate(bytes);
```

### 8.7. Container uid 10001 — Permission Denied

**Triệu chứng:** `java.io.FileNotFoundException: /opt/keyfactor/persistent/keys/worker.p12`

**Fix:**

```bash
# Dùng docker exec -u root để tạo thư mục
docker exec -u root ivf-signserver bash -c \
    "mkdir -p /opt/keyfactor/persistent/keys && \
     chown 10001:root /opt/keyfactor/persistent/keys && \
     chmod 700 /opt/keyfactor/persistent/keys"
```

### 8.8. WildFly mTLS config mất sau restart

**Triệu chứng:** `httpsTrustStore` key-store không tồn tại sau container restart

**Giải thích:** Elytron config lưu trong standalone.xml, file này nằm trong container layer (không persistent). Mỗi lần restart cần chạy lại.

**Fix:**

```bash
docker exec ivf-signserver bash /opt/keyfactor/persistent/init-mtls.sh
```

---

## 9. Security Checklist

### Phase 1 ✅

- [x] PKCS12 keystores lưu tại persistent volume (`/opt/keyfactor/persistent/keys/`)
- [x] Keystore permissions: owner read-only (`chmod 400`)
- [x] Key directory: `chmod 700`, owned by uid 10001
- [x] Port 9080: bound to `127.0.0.1` only
- [x] `SIGNSERVER_NODEID=node1` — ổn định worker ID
- [x] Security status endpoint (`/api/admin/signing/security-status`)
- [x] `RunDockerExecAsRootAsync()` helper cho privileged operations

### Phase 2 ✅

- [x] Internal Root CA (RSA 4096, 10 năm, AES-256 encrypted key)
- [x] API client certificate (RSA 2048, 1 năm, clientAuth EKU)
- [x] WildFly truststore (JKS) with Internal CA
- [x] WildFly SSL context: `want-client-auth=true`
- [x] `init-mtls.sh` script (idempotent)
- [x] All workers: `AUTHTYPE=org.signserver.server.ClientCertAuthorizer`
- [x] All workers: `addauthorizedclient` with API cert serial
- [x] `Program.cs`: Custom CA validation + mTLS client cert
- [x] `CreateHandler()`: mTLS support for admin endpoints
- [x] `SignPdfWithWorkerAsync()`: mTLS support for per-user signing
- [x] `ProvisionUserCertificateAsync()`: Auto-add authorized client on new workers
- [x] `X509CertificateLoader` — no SYSLIB0057 warnings
- [x] `ValidateProduction()` — startup validation
- [x] `ResolveClientCertificatePassword()` — Docker Secret support
- [x] Production config (`appsettings.Production.json`): `RequireMtls=true`, `SkipTlsValidation=false`
- [x] Temp properties file cleanup after provisioning

### Production TODO (Phase 3) ✅ COMPLETED

- [x] Network isolation: 3 Docker networks (`ivf-public`, `ivf-signing` internal, `ivf-data` internal)
- [x] Container hardening: `read_only: true` + tmpfs for SignServer, `no-new-privileges:true` all services
- [x] Port 9080 removed entirely from docker-compose
- [x] Rate limiting: `signing` (30/min configurable), `signing-provision` (3/min strict)
- [x] Certificate expiry monitoring: `CertificateExpiryMonitorService` (hourly check, 3 cert types)
- [x] Audit logging: correlation IDs (12-char hex), `Stopwatch` duration tracking, signer identity
- [x] Production mTLS: `need-client-auth=true` + health listener on port 8081
- [x] `init-mtls-production.sh` — strict mTLS init script
- [x] Config: `CertExpiryWarningDays`, `CertExpiryCheckIntervalMinutes`, `SigningRateLimitPerMinute`
- [x] Enhanced security-status endpoint: cert expiry data, container security flags, rate limit info

---

## 10. Phase 3 — Hardening & Monitoring

### 10.1. Network Isolation

3 isolated Docker networks replace the single `ivf-network`:

| Network       | Type                 | Services                     | Purpose               |
| ------------- | -------------------- | ---------------------------- | --------------------- |
| `ivf-public`  | bridge               | API                          | External access       |
| `ivf-signing` | bridge, **internal** | API, SignServer, EJBCA       | Signing (no internet) |
| `ivf-data`    | bridge, **internal** | API, databases, MinIO, Redis | Data (no internet)    |

```yaml
# docker-compose.yml
networks:
  ivf-public:
    driver: bridge
  ivf-signing:
    driver: bridge
    internal: true # No external access
  ivf-data:
    driver: bridge
    internal: true # No external access
```

**API** connects to all 3 networks (needs public for external, signing for SignServer, data for DB).
**SignServer** connects to `ivf-signing` + `ivf-data` only.
**Databases** connect to `ivf-data` only.

### 10.2. Container Security

All services get `security_opt: [no-new-privileges:true]`. SignServer additionally gets:

```yaml
signserver:
  read_only: true
  tmpfs:
    - /tmp:size=100M
    - /opt/keyfactor/wildfly-35.0.1.Final/standalone/tmp:size=50M
    - /opt/keyfactor/wildfly-35.0.1.Final/standalone/data:size=200M
    - /opt/keyfactor/wildfly-35.0.1.Final/standalone/log:size=50M
```

### 10.3. Rate Limiting

Two policies registered in `Program.cs`:

```csharp
// Configurable signing rate limit (default 30/min)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("signing", opt => {
        opt.PermitLimit = signingOptions.SigningRateLimitPerMinute;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });
    options.AddFixedWindowLimiter("signing-provision", opt => {
        opt.PermitLimit = 3;        // Strict: provisioning is expensive
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});
```

Applied at endpoint level:

- `SigningAdminEndpoints`: Group-level `.RequireRateLimiting("signing")`
- `security-status`: `.DisableRateLimiting()` (monitoring must not be rate limited)
- `ProvisionUserCertificate`: `.RequireRateLimiting("signing-provision")`
- `TestUserSigning`: `.RequireRateLimiting("signing")`

### 10.4. Certificate Expiry Monitoring

New `CertificateExpiryMonitorService` (BackgroundService):

```csharp
// Registered as singleton + hosted service
builder.Services.AddSingleton<CertificateExpiryMonitorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CertificateExpiryMonitorService>());
```

**Monitors 3 certificate types:**

| Certificate      | Source                  | Check Method                                      |
| ---------------- | ----------------------- | ------------------------------------------------- |
| API Client (P12) | `ClientCertificatePath` | `X509CertificateLoader.LoadPkcs12FromFile()`      |
| Trusted CA (PEM) | `TrustedCaCertPath`     | `X509CertificateLoader.LoadCertificateFromFile()` |
| SignServer TLS   | Remote HTTPS connection | TLS handshake callback                            |

**Alert thresholds:**

- **Critical** (log Critical): Certificate expired
- **Error** (log Error): < 7 days remaining
- **Warning** (log Warning): < 30 days remaining (configurable)
- **Debug** (log Debug): Certificate OK

**Exposed via security-status endpoint:**

```json
{
  "certificateExpiry": {
    "overallStatus": "OK",
    "lastChecked": "2025-07-15T10:30:00Z",
    "certificates": [
      {
        "name": "API Client Certificate",
        "status": "OK",
        "expiresAt": "2026-07-15",
        "daysRemaining": 365
      }
    ]
  }
}
```

### 10.5. Audit Logging Enhancements

`SignServerDigitalSigningService.SendToSignServerAsync()` now includes:

```csharp
var correlationId = Guid.NewGuid().ToString("N")[..12];
var sw = Stopwatch.StartNew();

// On success:
_logger.LogInformation(
    "AUDIT[{CorrelationId}]: Signing SUCCESS — Worker={Worker}, Reason={Reason}, " +
    "Signer={Signer}, DocumentHash={Hash}, Size={Size}, DurationMs={Duration}",
    correlationId, workerName, reason, signerIdentity, inputHash, pdfBytes.Length, sw.ElapsedMilliseconds);

// On failure:
_logger.LogError(
    "AUDIT[{CorrelationId}]: Signing FAILED — Worker={Worker}, Reason={Reason}, " +
    "Signer={Signer}, DurationMs={Duration}, Error={Error}",
    correlationId, workerName, reason, signerIdentity, sw.ElapsedMilliseconds, ex.Message);
```

### 10.6. Production mTLS (need-client-auth)

Script `scripts/init-mtls-production.sh`:

- Sets `need-client-auth=true` (strict — rejects all connections without client cert)
- Adds HTTP health listener on port 8081 (localhost only) to bypass mTLS for health checks
- Upgrades existing `want-client-auth` to `need-client-auth` if already configured

```bash
# Production: strict mTLS
docker exec ivf-signserver bash /opt/keyfactor/persistent/init-mtls-production.sh

# Development: permissive mTLS
docker exec ivf-signserver bash /opt/keyfactor/persistent/init-mtls.sh
```

### 10.7. Configuration

New properties in `DigitalSigningOptions`:

| Property                         | Default | Description                           |
| -------------------------------- | ------- | ------------------------------------- |
| `CertExpiryWarningDays`          | 30      | Days before expiry to trigger warning |
| `CertExpiryCheckIntervalMinutes` | 60      | Certificate check interval            |
| `SigningRateLimitPerMinute`      | 30      | Max signing requests per minute       |

### 10.8. Enhanced Security-Status Endpoint

`GET /api/admin/signing/security-status` now returns:

```json
{
  "securityScore": 85,
  "level": "good",
  "mtls": { ... },
  "tls": { ... },
  "audit": { ... },
  "certificateExpiry": {
    "overallStatus": "OK",
    "certificates": [ ... ]
  },
  "containerSecurity": {
    "noNewPrivileges": true,
    "readOnlyFilesystem": true,
    "networkIsolation": true,
    "description": "Container runs with no-new-privileges, read-only filesystem, and isolated networks"
  },
  "rateLimiting": {
    "enabled": true,
    "signingLimitPerMinute": 30,
    "provisionLimitPerMinute": 3
  }
}
```

---

## Phụ lục: API Endpoints tham khảo

| Method | Path                                                    | Mô tả                                            |
| ------ | ------------------------------------------------------- | ------------------------------------------------ |
| GET    | `/api/admin/signing/dashboard`                          | Dashboard tổng quan                              |
| GET    | `/api/admin/signing/config`                             | Cấu hình hiện tại                                |
| GET    | `/api/admin/signing/security-status`                    | Security posture + score                         |
| GET    | `/api/admin/signing/signserver/health`                  | Health check SignServer                          |
| GET    | `/api/admin/signing/signserver/workers`                 | Danh sách workers                                |
| GET    | `/api/admin/signing/signserver/workers/{id}`            | Chi tiết worker                                  |
| POST   | `/api/admin/signing/test-sign`                          | Test ký PDF mẫu                                  |
| GET    | `/api/admin/signing/ejbca/health`                       | Health check EJBCA                               |
| GET    | `/api/admin/signing/ejbca/cas`                          | Danh sách CA trên EJBCA                          |
| GET    | `/api/admin/signing/ejbca/certificates`                 | Tìm kiếm chứng chỉ EJBCA                         |
| GET    | `/api/admin/signing/compliance-audit`                   | Compliance audit 21 checks (Phase 4)             |
| GET    | `/api/admin/signing/security-audit-evidence`            | Evidence package cho third-party audit (Phase 4) |
| POST   | `/api/admin/signing/pentest`                            | Inline penetration test (Phase 4)                |
| POST   | `/api/user-signatures/users/{id}/provision-certificate` | Cấp cert cho user                                |
| POST   | `/api/user-signatures/users/{id}/test-sign`             | Test ký với cert user                            |

---

## 11. Phase 4 — Compliance & PKCS#11

### 11.1. Tổng quan

Phase 4 nâng cấp hệ thống ký số lên chuẩn compliance:

| Hạng mục                 | Mô tả                                                   | Files liên quan                                                                                       |
| ------------------------ | ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| **SoftHSM2 / PKCS#11**   | FIPS 140-2 Level 1 key storage, thay thế P12 file-based | `docker/signserver-softhsm/Dockerfile`, `scripts/init-softhsm.sh`, `scripts/migrate-p12-to-pkcs11.sh` |
| **Compliance Audit**     | 21 checks tự động, scoring A–F                          | `SecurityComplianceService.cs`                                                                        |
| **Third-Party Audit**    | Evidence package cho external auditor                   | `SecurityAuditService.cs`                                                                             |
| **Certificate Rotation** | Tự động rotate cert theo policy                         | `scripts/rotate-certs.sh`                                                                             |
| **Security Headers**     | OWASP headers (HSTS, COEP, COOP, CORP)                  | `Program.cs` middleware                                                                               |
| **Penetration Testing**  | OWASP Top 10 automated + inline API test                | `scripts/pentest.sh`, `SigningAdminEndpoints.cs`                                                      |
| **Health Check Fix**     | Không gửi client cert cho public health endpoints       | `SigningAdminEndpoints.cs` `CreateHandler()`                                                          |

### 11.2. SoftHSM2 / PKCS#11 Integration

#### 11.2.1. CryptoTokenType Enum

**File:** `src/IVF.API/Services/DigitalSigningOptions.cs`

```csharp
/// <summary>
/// Loại crypto token cho SignServer workers.
/// Phase 1-3 mặc định dùng P12.
/// Phase 4 thêm PKCS11 (SoftHSM2) cho FIPS 140-2 compliance.
/// </summary>
public enum CryptoTokenType
{
    /// PKCS#12 file-based keystore (default)
    P12,
    /// SoftHSM2 or hardware HSM — FIPS 140-2 Level 1
    PKCS11
}
```

#### 11.2.2. Cấu hình PKCS#11

Thêm vào `DigitalSigningOptions`:

| Property                  | Type              | Default             | Mô tả                                |
| ------------------------- | ----------------- | ------------------- | ------------------------------------ |
| `CryptoTokenType`         | `CryptoTokenType` | `P12`               | Chọn P12 hoặc PKCS11                 |
| `Pkcs11SharedLibraryName` | `string`          | `"SOFTHSM"`         | Tên library đăng ký trong SignServer |
| `Pkcs11SlotLabel`         | `string`          | `"SignServerToken"` | Token/slot label                     |
| `Pkcs11Pin`               | `string?`         | `null`              | PIN trực tiếp (chỉ dùng dev)         |
| `Pkcs11PinFile`           | `string?`         | `null`              | Docker Secret path chứa PIN          |

**Method `ResolvePkcs11Pin()`:**

```csharp
public string? ResolvePkcs11Pin()
{
    if (!string.IsNullOrEmpty(Pkcs11PinFile) && File.Exists(Pkcs11PinFile))
        return File.ReadAllText(Pkcs11PinFile).Trim();
    return Pkcs11Pin;
}
```

**appsettings.json (Development):**

```json
{
  "DigitalSigning": {
    "CryptoTokenType": "P12",
    "Pkcs11SharedLibraryName": "SOFTHSM",
    "Pkcs11SlotLabel": "SignServerToken",
    "Pkcs11Pin": null,
    "Pkcs11PinFile": null
  }
}
```

**Production override:**

```yaml
# docker-compose.production.yml
environment:
  - DigitalSigning__CryptoTokenType=PKCS11
  - DigitalSigning__Pkcs11SharedLibraryName=SOFTHSM
  - DigitalSigning__Pkcs11SlotLabel=SignServerToken
  - DigitalSigning__Pkcs11PinFile=/run/secrets/softhsm_pin
```

#### 11.2.3. SoftHSM2 Docker Image

**File:** `docker/signserver-softhsm/Dockerfile`

```dockerfile
FROM keyfactor/signserver-ce:latest

USER root

# Install SoftHSM2 + PKCS#11 tools
RUN apt-get update && apt-get install -y --no-install-recommends \
    softhsm2 opensc libengine-pkcs11-openssl \
    && rm -rf /var/lib/apt/lists/*

# SoftHSM2 directories
RUN mkdir -p /opt/keyfactor/persistent/softhsm/tokens \
    && chown -R 10001:root /opt/keyfactor/persistent/softhsm \
    && chmod 700 /opt/keyfactor/persistent/softhsm/tokens

# SoftHSM2 config — file-based object store
RUN echo "directories.tokendir = /opt/keyfactor/persistent/softhsm/tokens\n\
objectstore.backend = file\nlog.level = INFO" > /etc/softhsm2.conf

ENV SOFTHSM2_CONF=/etc/softhsm2.conf
ENV PKCS11_LIBRARY_PATH=/usr/lib/softhsm/libsofthsm2.so

USER 10001
```

**Docker Compose service (profile `softhsm`):**

```yaml
signserver-softhsm:
  build:
    context: ./docker/signserver-softhsm
    dockerfile: Dockerfile
  container_name: ivf-signserver-softhsm
  profiles: ["softhsm"]
  environment:
    - SOFTHSM_TOKEN_LABEL=SignServerToken
    - SOFTHSM_USER_PIN=changeit
    - SOFTHSM_SO_PIN=changeit
  volumes:
    - signserver_persistent:/opt/keyfactor/persistent
    - softhsm_tokens:/opt/keyfactor/persistent/softhsm/tokens
    - ./scripts/init-softhsm.sh:/opt/keyfactor/persistent/init-softhsm.sh:ro
    - ./scripts/migrate-p12-to-pkcs11.sh:/opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh:ro
```

> **Kích hoạt:** `docker compose --profile softhsm up -d signserver-softhsm`

#### 11.2.4. Init SoftHSM2 Token

**File:** `scripts/init-softhsm.sh`

**Usage:**

```bash
docker exec ivf-signserver bash /opt/keyfactor/persistent/init-softhsm.sh
```

**Quy trình:**

1. `check_deps()` — Verify `softhsm2-util` và PKCS#11 library
2. `init_token()` — Tạo token (idempotent, skip nếu đã tồn tại):
   ```bash
   softhsm2-util --init-token --free --label "$TOKEN_LABEL" \
       --pin "$USER_PIN" --so-pin "$SO_PIN"
   ```
3. `register_pkcs11_library()` — Đăng ký library name `SOFTHSM` vào SignServer:
   ```bash
   bin/signserver setproperty global GLOB.WORKER_PKCS11_LIBRARY.SOFTHSM \
       /usr/lib/softhsm/libsofthsm2.so
   ```
4. `generate_signing_key()` — Tạo RSA-2048 key pair:
   ```bash
   pkcs11-tool --module /usr/lib/softhsm/libsofthsm2.so \
       --login --pin "$USER_PIN" \
       --keypairgen --key-type rsa:2048 \
       --id "$KEY_ID" --label "$KEY_LABEL"
   ```

#### 11.2.5. Migration P12 → PKCS#11

**File:** `scripts/migrate-p12-to-pkcs11.sh`

**Usage:**

```bash
# Dry run — kiểm tra trước khi migrate
docker exec ivf-signserver bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh --dry-run

# Migrate tất cả workers
docker exec ivf-signserver bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh

# Migrate worker cụ thể
docker exec ivf-signserver bash /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh --worker-id 272
```

**Quy trình cho mỗi P12 worker:**

```
1. Backup P12 → backup_p12/
2. openssl pkcs12 → extract key.pem + cert.pem
3. Convert key.pem → key.der (DER format)
4. pkcs11-tool --write-object key.der → import vào SoftHSM2
5. pkcs11-tool --write-object cert.der → import cert
6. Reconfigure worker:
   - SIGNERTOKEN.CLASSPATH → PKCS11CryptoToken
   - SHAREDLIBRARYNAME → SOFTHSM
   - CKA_EXTRACTABLE=FALSE
   - CKA_SENSITIVE=TRUE
   - Remove KEYSTOREPATH, KEYSTOREPASSWORD
7. signserver reload + activatecryptotoken
8. Verify worker status = Active
```

**Key security properties (PKCS#11 mode):**

| Property            | Value     | Ý nghĩa                             |
| ------------------- | --------- | ----------------------------------- |
| `CKA_EXTRACTABLE`   | `FALSE`   | Key không thể export ra ngoài HSM   |
| `CKA_SENSITIVE`     | `TRUE`    | Key không thể đọc plaintext         |
| `SHAREDLIBRARYNAME` | `SOFTHSM` | Library đã đăng ký trong SignServer |

#### 11.2.6. Dual-Path Provisioning

**File:** `src/IVF.API/Endpoints/UserSignatureEndpoints.cs` — `ProvisionUserCertificateAsync()`

Khi provision certificate cho user mới, code tự chọn P12 hoặc PKCS#11 dựa trên `CryptoTokenType`:

```csharp
if (opts.CryptoTokenType == CryptoTokenType.PKCS11)
{
    // 1. Generate key inside SoftHSM2 token
    var pin = opts.ResolvePkcs11Pin() ?? "changeit";
    var genKeyCmd = $"pkcs11-tool --module /usr/lib/softhsm/libsofthsm2.so " +
        $"--login --pin {pin} " +
        $"--keypairgen --key-type rsa:2048 " +
        $"--id {keyId} --label {workerName}";
    await RunDockerExecAsync("ivf-signserver", genKeyCmd, logger);

    // 2. Create Java PKCS#11 provider config
    var providerConfig = $"name=SoftHSM2\nlibrary=/usr/lib/softhsm/libsofthsm2.so\n" +
        $"slot={slotIndex}\n";

    // 3. Worker properties → PKCS11CryptoToken
    var properties =
        $"GLOB.WORKER{workerId}.SIGNERTOKEN.CLASSPATH = org.signserver.server.cryptotokens.PKCS11CryptoToken\n" +
        $"WORKER{workerId}.SHAREDLIBRARYNAME = {opts.Pkcs11SharedLibraryName}\n" +
        $"WORKER{workerId}.DEFAULTKEY = {workerName}\n" +
        $"WORKER{workerId}.CKA_EXTRACTABLE = FALSE\n" +
        $"WORKER{workerId}.CKA_SENSITIVE = TRUE\n";
}
else
{
    // P12 path: keytool -genkeypair + chmod 400 (Phase 1-3 behavior)
}
```

### 11.3. Security Compliance Audit Service

**File:** `src/IVF.API/Services/SecurityComplianceService.cs`

**Class:** `SecurityComplianceService` — Singleton service, chạy 21 compliance checks.

**Registration (Program.cs):**

```csharp
builder.Services.AddSingleton<SecurityComplianceService>();
```

**Endpoint:** `GET /api/admin/signing/compliance-audit`

```csharp
group.MapGet("/compliance-audit", async (SecurityComplianceService complianceService) =>
{
    var result = await complianceService.RunAuditAsync();
    return Results.Ok(result);
})
.WithName("SecurityComplianceAudit")
.DisableRateLimiting();
```

#### 21 Compliance Checks

| Phase | Check ID     | Category        | Tên                              | Pass condition                     |
| ----- | ------------ | --------------- | -------------------------------- | ---------------------------------- |
| 1     | KEY-001      | Key Management  | Key Storage Location             | Keystore ở persistent volume       |
| 1     | KEY-002      | Key Management  | Key File Permissions             | `chmod 400`                        |
| 1     | NET-001      | Network         | HTTP Port 9080 Removed           | Port 9080 không expose             |
| 1     | KEY-003      | Key Management  | Secret Management                | Docker Secrets (không env vars)    |
| 2     | MTLS-001     | Authentication  | Mutual TLS (mTLS)                | `ClientCertificatePath` configured |
| 2     | TLS-001      | Encryption      | TLS Certificate Validation       | `SkipTlsValidation=false`          |
| 2     | AUTH-001     | Authentication  | ClientCertAuthorizer             | All workers có AUTHTYPE            |
| 2     | TLS-002      | Encryption      | TLS Version                      | Uses HTTPS URL                     |
| 3     | NET-002      | Network         | Network Isolation                | Internal Docker networks           |
| 3     | CTR-001      | Container       | Container Hardening              | `read_only`, `no-new-privileges`   |
| 3     | RL-001       | Rate Limiting   | Rate Limiting                    | Rate limiter configured            |
| 3     | AUD-001      | Audit           | Audit Logging                    | `EnableAuditLogging=true`          |
| 3     | CERT-001     | Certificate     | Certificate Expiry Monitoring    | Monitor service running            |
| **4** | **HSM-001**  | **Compliance**  | **Crypto Token Type**            | `CryptoTokenType=PKCS11`           |
| **4** | **FIPS-001** | **Compliance**  | **FIPS 140-2 Readiness**         | PKCS#11 + non-extractable keys     |
| **4** | **CERT-002** | **Certificate** | **Certificate Chain Validation** | CA cert path configured            |
| **4** | **HDR-001**  | **Security**    | **Security Headers**             | OWASP headers middleware           |
| **4** | **ENV-001**  | **Environment** | **Environment Config**           | Production environment             |
| **4** | **PEN-001**  | **Testing**     | **Penetration Testing**          | Pentest script exists              |
| **4** | **AUD-002**  | **Audit**       | **Third-Party Audit**            | Audit evidence endpoint available  |

#### Scoring & Grading

```
Score = Average(check_scores)
  Pass = 100%, Warning = 50%, Fail = 0%, Info = excluded

Grade:
  A ≥ 90%   "Excellent — production ready"
  B ≥ 80%   "Good — minor improvements recommended"
  C ≥ 70%   "Acceptable — several improvements needed"
  D ≥ 60%   "Poor — significant security gaps"
  F < 60%   "Failing — critical issues must be resolved"
```

**Response mẫu:**

```json
{
  "auditDate": "2026-02-22T10:00:00Z",
  "summary": {
    "totalChecks": 21,
    "passed": 17,
    "warnings": 3,
    "failed": 1,
    "informational": 0,
    "score": 85.5,
    "grade": "B"
  },
  "checks": [
    {
      "id": "HSM-001",
      "name": "Crypto Token Type (FIPS 140-2)",
      "category": "Compliance",
      "phase": 4,
      "status": "Pass",
      "detail": "CryptoTokenType=PKCS11 (FIPS 140-2 Level 1)"
    }
  ],
  "recommendations": [
    "Enable TLS certificate validation (SkipTlsValidation=false) for production",
    "Enable audit logging for signing operations"
  ]
}
```

### 11.4. Third-Party Security Audit Evidence

**File:** `src/IVF.API/Services/SecurityAuditService.cs`

**Class:** `SecurityAuditService` — Singleton, generates comprehensive audit evidence package.

**Registration (Program.cs):**

```csharp
builder.Services.AddSingleton<SecurityAuditService>();
```

**Endpoint:** `GET /api/admin/signing/security-audit-evidence`

```csharp
group.MapGet("/security-audit-evidence", async (SecurityAuditService auditService) =>
{
    var package = await auditService.GenerateAuditPackageAsync();
    return Results.Ok(package);
})
.WithName("SecurityAuditEvidence")
.DisableRateLimiting();
```

#### Evidence Package Contents

| Section                   | Model                         | Nội dung                                                                             |
| ------------------------- | ----------------------------- | ------------------------------------------------------------------------------------ |
| **System Info**           | `SystemInfoSection`           | App name, .NET runtime, OS, assembly version, container status                       |
| **Security Config**       | `SecurityConfigSection`       | Signing config (sanitized), FIPS status, mTLS mode                                   |
| **Compliance Audit**      | `ComplianceAuditResult?`      | Embedded 21-check compliance run                                                     |
| **Certificate Inventory** | `CertificateInventorySection` | Client cert details (subject, issuer, expiry, serial) + 5 worker certs               |
| **Security Controls**     | `SecurityControlsSection`     | 17 controls (SC-001→SC-017) across Phase 1-4                                         |
| **Access Control Matrix** | `AccessControlMatrixSection`  | 9 API endpoints + 7 service paths with auth method                                   |
| **Network Topology**      | `NetworkTopologySection`      | 3 networks, 5 exposed ports, 3 removed ports                                         |
| **Data Protection**       | `DataProtectionSection`       | Encryption at-rest/in-transit, 6 data classifications                                |
| **Incident Response**     | `IncidentResponseSection`     | 4 procedures: key compromise, cert expiry, unauthorized access, container compromise |
| **Pentest Capabilities**  | `PentestCapabilitiesSection`  | 4 tools, OWASP coverage, manual testing notes                                        |
| **Audit Trail Config**    | `AuditTrailConfigSection`     | 6 event types, correlation tracking, log destinations                                |
| **Recommendations**       | `List<string>`                | 10 auto-generated recommendations (AUDIT-REC-001→010)                                |

> **Bảo mật:** Tất cả secrets đều bị redacted. Package an toàn để chia sẻ với external auditor.

**17 Security Controls:**

| Control ID | Phase | Tên                               | Trạng thái  |
| ---------- | ----- | --------------------------------- | ----------- |
| SC-001     | 1     | Key Storage Protection            | Implemented |
| SC-002     | 1     | Key File Permissions (chmod 400)  | Implemented |
| SC-003     | 1     | Docker Secret Management          | Implemented |
| SC-004     | 1     | Port Restriction (9080 localhost) | Implemented |
| SC-005     | 2     | Internal Root CA                  | Implemented |
| SC-006     | 2     | mTLS Client Authentication        | Implemented |
| SC-007     | 2     | ClientCertAuthorizer              | Implemented |
| SC-008     | 2     | TLS v1.2+ Enforcement             | Implemented |
| SC-009     | 3     | Network Isolation (3 zones)       | Implemented |
| SC-010     | 3     | Container Hardening               | Implemented |
| SC-011     | 3     | Rate Limiting                     | Implemented |
| SC-012     | 3     | Certificate Expiry Monitoring     | Implemented |
| SC-013     | 3     | Audit Logging with Correlation    | Implemented |
| SC-014     | 4     | PKCS#11 Key Protection (FIPS)     | Conditional |
| SC-015     | 4     | Security Headers (OWASP)          | Implemented |
| SC-016     | 4     | Compliance Audit Service          | Implemented |
| SC-017     | 4     | Penetration Testing               | Implemented |

**Usage cho third-party audit:**

```bash
# Generate evidence package
curl -H "Authorization: Bearer <admin-jwt>" \
  http://localhost:5000/api/admin/signing/security-audit-evidence \
  | jq . > audit_evidence_$(date +%Y%m%d).json

# Run pentest and attach results
./scripts/pentest.sh --target all --output ./audit-attachments

# Run Trivy scan
docker compose --profile security-scan up trivy-scan 2>&1 > ./audit-attachments/trivy_scan.txt
```

### 11.5. Certificate Rotation Automation

**File:** `scripts/rotate-certs.sh`

**Usage:**

```bash
# Kiểm tra trạng thái expiry tất cả cert
./scripts/rotate-certs.sh --check

# Rotate API client cert (dry run)
./scripts/rotate-certs.sh --type api-client --dry-run

# Force rotate worker cert
./scripts/rotate-certs.sh --type worker --worker-id 444 --force

# Rotate admin cert với grace period 60 ngày
./scripts/rotate-certs.sh --type admin --grace-days 60
```

**Các function chính:**

| Function                   | Mô tả                                                                       |
| -------------------------- | --------------------------------------------------------------------------- |
| `check_cert_expiry()`      | Trả về VALID/EXPIRING/EXPIRED/MISSING + days remaining                      |
| `rotate_api_client_cert()` | EC P-256 key → CSR → EJBCA sign → update authorized clients                 |
| `rotate_admin_cert()`      | Tương tự cho admin cert                                                     |
| `rotate_worker_cert()`     | Phát hiện P12 vs PKCS#11 → `signserver generatekey` → CSR → EJBCA → install |
| `check_all_certs()`        | `--check` flag: bảng expiry cho tất cả PEM cert                             |

**Quy trình rotate API client cert:**

```
1. Generate EC P-256 key (openssl ecparam -genkey)
2. Create CSR (openssl req -new)
3. Sign qua EJBCA REST API (/ejbca-rest-api/v1/certificate/enrollkeystore)
4. Export to P12 (openssl pkcs12 -export)
5. Update SignServer: addauthorizedclient (new serial)
6. Grace period: giữ cert cũ trong authorized list N ngày
7. Sau grace period: removeauthorizedclient (old serial)
8. Backup cert cũ → backup/
```

### 11.6. Security Headers Hardening

**File:** `src/IVF.API/Program.cs` — Middleware

Phase 4 thêm các OWASP security headers:

```csharp
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["X-XSS-Protection"] = "0";  // Disabled — CSP replaces
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; " +
        "base-uri 'self'; form-action 'self';";

    // Phase 4: Enhanced headers
    headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), " +
        "accelerometer=(), gyroscope=(), magnetometer=(), autoplay=()";
    headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Cross-Origin-Resource-Policy"] = "same-origin";

    // Remove server identity
    headers.Remove("Server");
    headers.Remove("X-Powered-By");

    // HSTS (HTTPS/Production only)
    if (context.Request.IsHttps)
        headers["Strict-Transport-Security"] =
            "max-age=63072000; includeSubDomains; preload";

    await next();
});
```

**Bảng headers:**

| Header                         | Value                                          | Purpose                    |
| ------------------------------ | ---------------------------------------------- | -------------------------- |
| `X-Content-Type-Options`       | `nosniff`                                      | Chống MIME sniffing        |
| `X-Frame-Options`              | `DENY`                                         | Chống clickjacking         |
| `X-XSS-Protection`             | `0`                                            | Disabled (CSP thay thế)    |
| `Referrer-Policy`              | `strict-origin-when-cross-origin`              | Giới hạn referrer          |
| `Content-Security-Policy`      | `default-src 'self'; ...`                      | Chống XSS/injection        |
| `Permissions-Policy`           | `camera=(), ...`                               | Chặn browser API           |
| `Cross-Origin-Embedder-Policy` | `require-corp`                                 | Chống cross-origin embed   |
| `Cross-Origin-Opener-Policy`   | `same-origin`                                  | Isolate browsing context   |
| `Cross-Origin-Resource-Policy` | `same-origin`                                  | Chặn cross-origin resource |
| `Strict-Transport-Security`    | `max-age=63072000; includeSubDomains; preload` | Force HTTPS                |
| `Server`                       | **Removed**                                    | Ẩn server identity         |
| `X-Powered-By`                 | **Removed**                                    | Ẩn tech stack              |

### 11.7. Penetration Testing

#### 11.7.1. Automated Script

**File:** `scripts/pentest.sh`

**Usage:**

```bash
# Full test (API + SignServer + EJBCA + headers)
./scripts/pentest.sh --target all

# Chỉ API
./scripts/pentest.sh --target api

# Chỉ SignServer
./scripts/pentest.sh --target signserver

# Custom output
./scripts/pentest.sh --target all --output /tmp/pentest-results
```

**Test Coverage (OWASP Top 10 2021):**

| Category                       | Tests                                    | Mô tả                                  |
| ------------------------------ | ---------------------------------------- | -------------------------------------- |
| A01: Broken Access Control     | Auth bypass, IDOR, privilege escalation  | Thử truy cập admin endpoint không auth |
| A02: Cryptographic Failures    | HSTS, TLS 1.1 rejection, cert validation | Kiểm tra TLS config                    |
| A03: Injection                 | SQL injection, XSS, command injection    | Gửi payload injection                  |
| A04: Insecure Design           | Rate limiting verification               | Kiểm tra rate limiter                  |
| A05: Security Misconfiguration | Headers, CORS, swagger, server identity  | 9 header checks                        |
| A06: Vulnerable Components     | Trivy container scanning                 | CVE scan                               |
| A07: Auth Failures             | JWT invalid, JWT `none` algorithm        | Test JWT bypass                        |
| A08: Data Integrity            | CSP header validation                    | Kiểm tra CSP                           |
| A09: Logging & Monitoring      | Compliance audit endpoint                | Kiểm tra audit endpoint                |
| A10: SSRF                      | Metadata service prevention              | Test `169.254.169.254`                 |
| SS-\*: SignServer              | mTLS, port 9080, admin access, health    | 5 SignServer-specific tests            |
| EJBCA-\*: EJBCA                | Admin access, REST API, enrollment       | 4 EJBCA-specific tests                 |
| HDR-\*: Headers                | 9 OWASP headers + server identity        | 10 header checks                       |

**Output:**

- Markdown report: `pentest-results/pentest_report_YYYYMMDD_HHMMSS.md`
- JSON results: `pentest-results/pentest_results_YYYYMMDD_HHMMSS.json`

#### 11.7.2. Inline API Endpoint

**Endpoint:** `POST /api/admin/signing/pentest`

Chạy non-destructive API-level security checks:

```csharp
group.MapPost("/pentest", (IOptions<DigitalSigningOptions> options, IHostEnvironment env) =>
{
    var opts = options.Value;
    var checks = new List<object>();

    // A01-001: Admin endpoints require auth
    checks.Add(new { id = "A01-001", name = "Admin endpoints require auth",
        severity = "Critical", status = "pass" });

    // A02-001: HSTS configured
    // A02-002: TLS validation
    // A04-001: Rate limiting
    // A05-001: mTLS configured
    // FIPS-001: PKCS#11 key protection
    // ... 11 checks total

    var passed = checks.Count(c => ((dynamic)c).status == "pass");
    var score = (double)passed / checks.Count * 100;
    var grade = score >= 90 ? "A" : score >= 80 ? "B" : score >= 70 ? "C" : "D";

    return Results.Ok(new
    {
        testDate = DateTime.UtcNow,
        environment = env.EnvironmentName,
        totalChecks = checks.Count,
        passed,
        score = Math.Round(score, 1),
        grade,
        checks,
        recommendation = "Run scripts/pentest.sh --target all for full external penetration test"
    });
});
```

### 11.8. Health Check Fix — `CreateHandler(attachClientCert)`

**File:** `src/IVF.API/Endpoints/SigningAdminEndpoints.cs`

**Vấn đề:** `CreateHandler()` luôn gắn client cert (`api-client.p12`) vào mọi HTTPS request. Khi EJBCA/SignServer nhận client cert signed bởi CA mà chúng không trust → TLS handshake bị reject → health check fail (HTTP Status 0).

**Giải pháp:** Thêm parameter `attachClientCert` (default `true`) — health check endpoints pass `false`:

```csharp
private static HttpClientHandler CreateHandler(
    DigitalSigningOptions opts, bool attachClientCert = true)
{
    var handler = new HttpClientHandler();
    if (opts.SkipTlsValidation)
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    // Chỉ gắn client cert khi được yêu cầu
    // Health check endpoints KHÔNG gửi client cert
    if (attachClientCert &&
        !string.IsNullOrEmpty(opts.ClientCertificatePath) &&
        File.Exists(opts.ClientCertificatePath))
    {
        try
        {
            var certPassword = opts.ResolveClientCertificatePassword();
            handler.ClientCertificates.Add(
                X509CertificateLoader.LoadPkcs12FromFile(
                    opts.ClientCertificatePath, certPassword));
        }
        catch (Exception)
        {
            // Certificate loading failed — continue without client cert
        }
    }

    return handler;
}
```

**Endpoints sử dụng `attachClientCert: false`:**

| Endpoint                     | Tại sao                                |
| ---------------------------- | -------------------------------------- |
| `GET /signserver/health`     | Public health endpoint, không cần mTLS |
| `GET /ejbca/health`          | Public health endpoint, không cần mTLS |
| `GetSignServerStatusAsync()` | Dashboard, gọi health check            |
| `GetEjbcaStatusAsync()`      | Dashboard, gọi health check            |

**Endpoints vẫn dùng `attachClientCert: true` (mặc định):**

| Endpoint                       | Tại sao           |
| ------------------------------ | ----------------- |
| `GET /signserver/workers`      | REST API cần auth |
| `GET /signserver/workers/{id}` | REST API cần auth |
| `GET /ejbca/cas`               | REST API cần mTLS |
| `GET /ejbca/certificates`      | REST API cần mTLS |

### 11.9. Container Vulnerability Scanning — Trivy

**Docker Compose service (profile `security-scan`):**

```yaml
trivy-scan:
  image: aquasec/trivy:latest
  profiles: ["security-scan"]
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock:ro
  command: >
    image --severity HIGH,CRITICAL --format table
    keyfactor/signserver-ce:latest
    keyfactor/ejbca-ce:latest
    postgres:16-alpine
    minio/minio:latest
    redis:7-alpine
```

**Usage:**

```bash
docker compose --profile security-scan up trivy-scan
```

Scan tất cả container images cho HIGH và CRITICAL CVEs.

### 11.10. Phase 4 Security Checklist

### Phase 4 ✅ COMPLETED

- [x] **SoftHSM2 Docker image**: `docker/signserver-softhsm/Dockerfile`
- [x] **SoftHSM2 init script**: `scripts/init-softhsm.sh` (idempotent token init + PKCS#11 library registration)
- [x] **P12→PKCS#11 migration**: `scripts/migrate-p12-to-pkcs11.sh` (dry-run, per-worker, backup)
- [x] **CryptoTokenType enum**: `P12` (default) hoặc `PKCS11`
- [x] **PKCS#11 config**: `Pkcs11SharedLibraryName`, `Pkcs11SlotLabel`, `Pkcs11Pin`, `Pkcs11PinFile`
- [x] **Dual-path provisioning**: `ProvisionUserCertificateAsync()` chọn P12/PKCS#11 tự động
- [x] **FIPS 140-2 Level 1**: `CKA_EXTRACTABLE=FALSE`, `CKA_SENSITIVE=TRUE` trên PKCS#11 keys
- [x] **SecurityComplianceService**: 21 checks, scoring A–F, `GET /compliance-audit`
- [x] **SecurityAuditService**: Evidence package (17 controls, cert inventory, access matrix, incident response)
- [x] **`GET /security-audit-evidence`**: Secrets redacted, safe for external auditor
- [x] **Certificate rotation**: `scripts/rotate-certs.sh` (api-client, admin, worker types)
- [x] **Security headers**: HSTS, Permissions-Policy, COEP, COOP, CORP, CSP
- [x] **Server identity removed**: `Server` + `X-Powered-By` headers stripped
- [x] **Penetration testing script**: `scripts/pentest.sh` (OWASP A01–A10, SignServer, EJBCA, headers)
- [x] **Inline pentest endpoint**: `POST /pentest` (non-destructive API checks)
- [x] **Trivy scanning**: `docker compose --profile security-scan up trivy-scan`
- [x] **Health check fix**: `CreateHandler(opts, attachClientCert: false)` cho health endpoints
- [x] **Cert loading try-catch**: Graceful fallback khi client cert load fails
- [x] **ILogger**: Warning logs cho health check failures
- [x] **Error detail**: `ex.GetBaseException().Message` thay vì `ex.Message`
- [x] **Docker Secrets**: `softhsm_pin`, `softhsm_so_pin` trong production compose

### 11.11. File Structure (Phase 4)

```
IVF/
├── docker/
│   └── signserver-softhsm/
│       └── Dockerfile                  # SignServer CE + SoftHSM2 + OpenSC
├── scripts/
│   ├── init-softhsm.sh                # PKCS#11 token init (idempotent)
│   ├── migrate-p12-to-pkcs11.sh       # P12 → PKCS#11 migration
│   ├── rotate-certs.sh                # Certificate rotation automation
│   └── pentest.sh                     # OWASP Top 10 penetration testing
├── src/IVF.API/
│   ├── Services/
│   │   ├── DigitalSigningOptions.cs   # + CryptoTokenType, PKCS#11 props
│   │   ├── SecurityComplianceService.cs  # 21-check compliance audit (NEW)
│   │   └── SecurityAuditService.cs       # Third-party audit evidence (NEW)
│   ├── Endpoints/
│   │   ├── SigningAdminEndpoints.cs    # + compliance-audit, pentest, audit-evidence
│   │   │                              # + CreateHandler(attachClientCert)
│   │   └── UserSignatureEndpoints.cs  # + PKCS#11 provisioning path
│   └── Program.cs                     # + Service registration, security headers
├── docker-compose.yml                 # + signserver-softhsm, trivy-scan profiles
└── docker-compose.production.yml      # + PKCS#11 env, SoftHSM2 secrets
```

---

## 12. Phase 5 — TSA, OCSP & Certificate Lifecycle

### 12.1. Timestamp Authority (TSA)

RFC 3161 timestamp cho PAdES-LTV (Long-Term Validation). PDF signatures include a trusted timestamp proving the document was signed at a specific time, even after the signer's certificate expires.

**Setup:**

```bash
# Tạo TimeStampSigner worker (ID=100) + cấu hình PDFSigner TSA_WORKER
bash scripts/init-tsa.sh
```

Script `init-tsa.sh`:

1. Tạo TSA keystore (`tsa-signer.p12`) với EKU=timeStamping
2. Tạo worker `TimeStampSigner` (ID=100) với `TimeStampSigner` implementation
3. Set `TSA_WORKER=TimeStampSigner` trên tất cả 5 PDFSigner workers
4. Copy ClientCertAuthorizer config từ PDFSigner (nếu mTLS đã bật)

**Config (`appsettings.Production.json`):**

```jsonc
{
  "DigitalSigning": {
    "TsaWorkerName": "TimeStampSigner", // Tên worker TSA
  },
}
```

### 12.2. OCSP Responder

EJBCA CE có built-in OCSP responder tại `/ejbca/publicweb/status/ocsp`.

**Setup:**

```bash
# Cấu hình OCSP responder settings
bash scripts/init-ocsp.sh
```

Script `init-ocsp.sh`:

1. Verify EJBCA health
2. Configure signature algorithm (SHA256WithRSA)
3. Set default responder CA
4. Test OCSP endpoint

**Config (`appsettings.Production.json`):**

```jsonc
{
  "DigitalSigning": {
    "OcspResponderUrl": "https://ejbca:8443/ejbca/publicweb/status/ocsp",
  },
}
```

**Manual AIA extension** (EJBCA Admin UI):

1. Certificate Profiles → Edit → X.509v3 extensions → Authority Information Access
2. Add OCSP URI: `https://ejbca:8443/ejbca/publicweb/status/ocsp`

### 12.3. EJBCA Certificate Enrollment

Thay self-signed keystore bằng EJBCA-issued certificate cho PAdES chain-of-trust.

```bash
# Enroll tất cả workers + TSA
bash scripts/enroll-ejbca-certs.sh

# Chỉ 1 worker
bash scripts/enroll-ejbca-certs.sh --worker 1

# Chỉ TSA
bash scripts/enroll-ejbca-certs.sh --tsa

# Preview (không thay đổi)
bash scripts/enroll-ejbca-certs.sh --dry-run
```

**Prerequisites:**

- EJBCA Certificate Profile: `IVF-PDFSigner-Profile` (Key Usage: digitalSignature, nonRepudiation)
- EJBCA End Entity Profile: `IVF-Signer-EEProfile`
- CA: `ManagementCA` (hoặc custom CA name via `--ca`)

### 12.4. CA Keys Backup

```bash
# Full backup (certs + EJBCA data + SignServer data + EJBCA DB)
bash scripts/backup-ca-keys.sh

# Certificate files only
bash scripts/backup-ca-keys.sh --keys-only

# Custom output directory
bash scripts/backup-ca-keys.sh --output /mnt/backup/
```

Output: `backups/ivf-ca-backup_YYYYMMDD_HHMMSS.tar.gz`

**Encrypt for offsite storage:**

```bash
openssl enc -aes-256-cbc -salt -pbkdf2 \
  -in backups/ivf-ca-backup_*.tar.gz \
  -out backups/ivf-ca-backup_*.tar.gz.enc
```

### 12.5. Phase 5 Security Checklist

### Phase 5 ✅ COMPLETED

- [x] **TSA worker**: `scripts/init-tsa.sh` (TimeStampSigner ID=100, RFC 3161)
- [x] **PDFSigner TSA integration**: `TSA_WORKER=TimeStampSigner` on all 5 workers
- [x] **OCSP responder**: `scripts/init-ocsp.sh` (EJBCA built-in, SHA256WithRSA)
- [x] **EJBCA cert enrollment**: `scripts/enroll-ejbca-certs.sh` (replace self-signed, batch or per-worker)
- [x] **CA keys backup**: `scripts/backup-ca-keys.sh` (full + keys-only modes, encrypted archive)
- [x] **TSA config**: `TsaWorkerName` in `DigitalSigningOptions`
- [x] **OCSP config**: `OcspResponderUrl` in `DigitalSigningOptions`
- [x] **Production config**: `appsettings.Production.json` updated with TSA + OCSP settings
- [x] **TLS validation**: `SkipTlsValidation: false` in production (already Phase 2)
- [x] **Docker mount**: `init-tsa.sh` mounted read-only in both signserver services

### 12.6. Post-Deploy Sequence (Phase 5)

```bash
# After docker compose up -d:
bash scripts/init-mtls.sh          # 1. mTLS (WildFly Elytron)
bash scripts/init-tsa.sh           # 2. TSA worker + PDFSigner integration
bash scripts/init-ocsp.sh          # 3. OCSP responder config
bash scripts/enroll-ejbca-certs.sh # 4. EJBCA-issued certs (optional, replaces self-signed)
bash scripts/backup-ca-keys.sh     # 5. Backup before going live
```
