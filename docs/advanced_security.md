# Bảo mật nâng cao (Advanced Security)

> Module quản trị bảo mật nâng cao cho IVF System — bao gồm Passkeys/WebAuthn, TOTP, SMS OTP, quản lý thiết bị, phiên hoạt động, rate limiting, geo-fencing, threat detection, khóa tài khoản và IP whitelist.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Domain Entities](#2-domain-entities)
3. [API Endpoints](#3-api-endpoints)
4. [Frontend](#4-frontend)
5. [Luồng xác thực MFA](#5-luồng-xác-thực-mfa)
6. [Passkeys / WebAuthn](#6-passkeys--webauthn)
7. [Rate Limiting](#7-rate-limiting)
8. [Geo Security](#8-geo-security)
9. [Threat Detection](#9-threat-detection)
10. [Khóa tài khoản](#10-khóa-tài-khoản)
11. [IP Whitelist](#11-ip-whitelist)
12. [Quản lý thiết bị & phiên](#12-quản-lý-thiết-bị--phiên)
13. [Cấu hình & triển khai](#13-cấu-hình--triển-khai)
14. [Tích hợp Zero Trust](#14-tích-hợp-zero-trust)
15. [Enterprise Security (Google/Microsoft/AWS)](#15-enterprise-security-googlemicrosoftaws)

---

## 1. Tổng quan kiến trúc

### Sơ đồ module

```
┌─────────────────────────────────────────────────────────┐
│                    Angular 21 Frontend                  │
│  AdvancedSecurityComponent (10 tabs, standalone)        │
│  ├── AdvancedSecurityService (HTTP client)              │
│  ├── AuthService (JWT + session binding)                │
│  ├── SecurityInterceptor (ZT headers)                   │
│  └── SecurityService (security events)                  │
└────────────────────────┬────────────────────────────────┘
                         │ HTTP (JWT RS256 Bearer)
                         │ + X-Device-Fingerprint
                         │ + X-Session-Id
                         │ + X-Correlation-Id
                         ▼
┌─────────────────────────────────────────────────────────┐
│              .NET 10 Minimal API                        │
│  /api/security/advanced/* (AdminOnly policy)            │
│  ├── JwtKeyService (RSA 3072-bit, RS256 signing)       │
│  ├── TokenBindingMiddleware (device/session validation) │
│  ├── RefreshTokenFamilyService (reuse detection)       │
│  ├── PasswordPolicyService (NIST SP 800-63B)           │
│  ├── Fido2 (WebAuthn / Passkeys)                       │
│  ├── Otp.NET (TOTP generation & validation)             │
│  ├── SecurityEvent (audit trail, 40+ event types)      │
│  └── EF Core 10 → PostgreSQL                           │
└────────────────────────┬────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          ▼              ▼              ▼
    PostgreSQL 16    Redis Cache    MinIO (S3)
```

### Stack công nghệ

| Thành phần     | Công nghệ                     | Phiên bản     |
| -------------- | ----------------------------- | ------------- |
| Backend        | .NET Minimal API              | 10.0          |
| WebAuthn/FIDO2 | Fido2.AspNet                  | 4.0.0-beta1   |
| TOTP           | Otp.NET                       | 1.4.0         |
| ORM            | EF Core + Npgsql              | 10.0          |
| Frontend       | Angular Standalone Components | 21            |
| UI             | Tailwind CSS                  | 4.2           |
| Auth           | JWT Bearer + Refresh Token    | **RS256** (asymmetric), 60 min |
| Database       | PostgreSQL                    | 16            |

### Phân quyền

Toàn bộ endpoint bảo mật nâng cao yêu cầu policy **`AdminOnly`**. Chỉ user có role `Admin` mới truy cập được.

```csharp
var group = app.MapGroup("/api/security/advanced")
    .WithTags("Advanced Security")
    .RequireAuthorization("AdminOnly");
```

---

## 2. Domain Entities

### 2.1 SecurityEvent

> `src/IVF.Domain/Entities/SecurityEvent.cs`

Ghi nhận tất cả sự kiện bảo mật. Immutable sau tạo. Hỗ trợ 40+ loại event theo chuẩn MITRE ATT&CK.

| Property            | Type    | Mô tả                                 |
| ------------------- | ------- | ------------------------------------- |
| `EventType`         | string  | Loại event (xem danh sách bên dưới)   |
| `Severity`          | string  | Info / Low / Medium / High / Critical |
| `UserId`            | string? | ID người dùng liên quan               |
| `IpAddress`         | string? | Địa chỉ IP                            |
| `UserAgent`         | string? | Trình duyệt / thiết bị                |
| `DeviceFingerprint` | string? | Dấu vân tay thiết bị                  |
| `Country`           | string? | Quốc gia (GeoIP)                      |
| `RiskScore`         | int     | Điểm rủi ro 0–100                     |
| `IsBlocked`         | bool    | Đã bị chặn hay không                  |
| `ThreatIndicators`  | string? | JSON chỉ số mối đe dọa                |

**Phân loại event (40+ types):**

| Nhóm           | Event Types                                                                                  |
| -------------- | -------------------------------------------------------------------------------------------- |
| Authentication | `LoginSuccess`, `LoginFailed`, `LoginBruteForce`, `TokenRefresh`, `MfaRequired`, `MfaFailed` |
| Authorization  | `AccessDenied`, `PrivilegeEscalation`, `PolicyViolation`                                     |
| Zero Trust     | `ZT_ACCESS_DENIED`, `ZT_BREAK_GLASS`, `ZT_DEVICE_UNTRUSTED`, `ZT_GEO_FENCE_VIOLATION`        |
| Threats        | `ImpossibleTravel`, `AnomalousAccess`, `TorExit`, `VpnProxy`, `SqlInjectionAttempt`          |
| Devices        | `DeviceRegistered`, `DeviceTrusted`, `DeviceRevoked`, `NewDeviceLogin`                       |
| Sessions       | `SessionCreated`, `SessionHijackAttempt`, `ConcurrentSession`, `SessionAnomalous`            |
| Data           | `SensitiveDataAccess`, `BulkDataExport`, `DataModification`                                  |
| APIs           | `ApiKeyAbuse`, `ApiRateLimited`, `ApiUnauthorized`                                           |

### 2.2 UserMfaSetting

> `src/IVF.Domain/Entities/UserMfaSetting.cs`

Cấu hình MFA cho từng user. Hỗ trợ TOTP, SMS, Passkey.

| Property            | Type    | Mô tả                               |
| ------------------- | ------- | ----------------------------------- |
| `UserId`            | string  | ID người dùng                       |
| `IsMfaEnabled`      | bool    | MFA đang bật?                       |
| `MfaMethod`         | string  | `none` / `totp` / `sms` / `passkey` |
| `TotpSecretKey`     | string? | Secret Base32 cho Authenticator App |
| `IsTotpVerified`    | bool    | TOTP đã xác minh?                   |
| `PhoneNumber`       | string? | Số điện thoại cho SMS OTP           |
| `IsPhoneVerified`   | bool    | Số điện thoại đã xác minh?          |
| `RecoveryCodes`     | string? | JSON mã khôi phục (hashed)          |
| `FailedMfaAttempts` | int     | Số lần MFA thất bại                 |

**Domain methods:** `EnableTotp()`, `VerifyTotp()`, `SetPhoneNumber()`, `VerifyPhone()`, `DisableMfa()`, `RecordMfaSuccess()`, `RecordMfaFailure()`

### 2.3 PasskeyCredential

> `src/IVF.Domain/Entities/PasskeyCredential.cs`

Lưu trữ FIDO2/WebAuthn credential sau đăng ký thành công.

| Property           | Type    | Mô tả                                  |
| ------------------ | ------- | -------------------------------------- |
| `UserId`           | string  | ID người dùng                          |
| `CredentialId`     | string  | Base64url credential ID                |
| `PublicKey`        | string  | Base64 public key                      |
| `UserHandle`       | string  | Base64url user handle                  |
| `SignatureCounter` | uint    | Bộ đếm chữ ký (chống clone)            |
| `DeviceName`       | string? | "Windows Hello", "Touch ID", "YubiKey" |
| `AaGuid`           | string? | Authenticator Attestation GUID         |
| `IsActive`         | bool    | Đang hoạt động?                        |

**Domain methods:** `Create()`, `UpdateCounter()`, `Revoke()`, `Rename()`

### 2.4 AccountLockout

> `src/IVF.Domain/Entities/AccountLockout.cs`

Theo dõi trạng thái khóa tài khoản (tự động hoặc thủ công).

| Property         | Type      | Mô tả                     |
| ---------------- | --------- | ------------------------- |
| `UserId`         | string    | ID người dùng bị khóa     |
| `Username`       | string    | Tên đăng nhập             |
| `Reason`         | string    | Lý do khóa                |
| `LockedAt`       | DateTime  | Thời điểm khóa            |
| `UnlocksAt`      | DateTime? | Thời điểm tự mở khóa      |
| `FailedAttempts` | int       | Số lần đăng nhập thất bại |
| `LockedBy`       | string?   | Admin thực hiện khóa      |
| `IsManualLock`   | bool      | Khóa thủ công hay tự động |

### 2.5 RateLimitConfig

> `src/IVF.Domain/Entities/RateLimitConfig.cs`

Cấu hình rate limiting tùy chỉnh cho từng endpoint.

| Property        | Type    | Mô tả                                |
| --------------- | ------- | ------------------------------------ |
| `PolicyName`    | string  | Tên policy                           |
| `WindowType`    | string  | `fixed` / `sliding` / `token_bucket` |
| `WindowSeconds` | int     | Kích thước cửa sổ (giây)             |
| `PermitLimit`   | int     | Số request tối đa trong cửa sổ       |
| `AppliesTo`     | string? | Endpoint pattern (null = toàn cục)   |
| `IsEnabled`     | bool    | Đang bật?                            |

### 2.6 GeoBlockRule

> `src/IVF.Domain/Entities/GeoBlockRule.cs`

Luật chặn/cho phép truy cập theo quốc gia.

| Property      | Type    | Mô tả                             |
| ------------- | ------- | --------------------------------- |
| `CountryCode` | string  | Mã ISO 3166-1 alpha-2             |
| `CountryName` | string  | Tên quốc gia                      |
| `IsBlocked`   | bool    | `true` = chặn, `false` = cho phép |
| `Reason`      | string? | Lý do                             |
| `IsEnabled`   | bool    | Đang bật?                         |

### 2.7 IpWhitelistEntry

> `src/IVF.Domain/Entities/IpWhitelistEntry.cs`

Danh sách IP được phép truy cập, hỗ trợ CIDR range.

| Property      | Type      | Mô tả                      |
| ------------- | --------- | -------------------------- |
| `IpAddress`   | string    | Địa chỉ IPv4/IPv6          |
| `CidrRange`   | string?   | CIDR notation (vd: /24)    |
| `Description` | string?   | Ghi chú                    |
| `AddedBy`     | string    | Admin thêm                 |
| `ExpiresAt`   | DateTime? | Hết hạn (null = vĩnh viễn) |
| `IsActive`    | bool      | Đang hoạt động?            |

---

## 3. API Endpoints

**Base route:** `POST/GET/PUT/DELETE /api/security/advanced/*`

**Authorization:** Tất cả yêu cầu `AdminOnly` policy (JWT Bearer + role Admin)

### 3.1 Security Score

| Method | Path     | Mô tả                            | Response        |
| ------ | -------- | -------------------------------- | --------------- |
| GET    | `/score` | Tính điểm bảo mật tổng thể 0–100 | `SecurityScore` |

**Cách tính điểm:**

- Bắt đầu từ 100 điểm
- Trừ điểm: critical events (-10/event), high events (-5), medium (-2), active lockouts (-15/each)
- Cộng điểm: MFA adoption (+10/user), passkey registered (+5/key), trusted devices (+3/device)
- Kết quả: `good` (≥80), `warning` (≥50), `critical` (<50)

### 3.2 Login History

| Method | Path                    | Mô tả                          | Response              |
| ------ | ----------------------- | ------------------------------ | --------------------- |
| GET    | `/login-history?count=` | Lấy lịch sử đăng nhập gần nhất | `LoginHistoryEntry[]` |

**Risk factors phân tích tự động:**

- `multiple_failed_attempts` — ≥3 lần đăng nhập thất bại
- `impossible_travel` — Di chuyển bất thường
- `off_hours_access` — Ngoài giờ làm việc (22h–6h)
- `new_device` — Thiết bị mới
- `new_ip` — IP chưa từng thấy

### 3.3 Rate Limits

| Method | Path                        | Mô tả                                  | Request/Response         |
| ------ | --------------------------- | -------------------------------------- | ------------------------ |
| GET    | `/rate-limits`              | Danh sách policies (built-in + custom) | `RateLimitStatus`        |
| POST   | `/rate-limits`              | Tạo policy tùy chỉnh                   | `CreateRateLimitRequest` |
| PUT    | `/rate-limits/{id}`         | Cập nhật policy                        | `UpdateRateLimitRequest` |
| DELETE | `/rate-limits/{id}`         | Xóa policy tùy chỉnh                   | `{ message }`            |
| GET    | `/rate-limit-events?hours=` | Sự kiện vi phạm rate limit             | `RateLimitEvent[]`       |

**Built-in policies (không xóa/sửa được):**

| Policy Name     | Window | Limit   | Áp dụng                  |
| --------------- | ------ | ------- | ------------------------ |
| Global          | 1 phút | 100 req | Toàn bộ API              |
| Authentication  | 1 phút | 10 req  | `/api/auth/*`            |
| Sensitive Data  | 1 phút | 30 req  | `/api/patients/*`        |
| Digital Signing | 1 phút | 30 req  | `/api/digital-signing/*` |

### 3.4 Geo Security

| Method | Path                 | Mô tả                       | Request/Response            |
| ------ | -------------------- | --------------------------- | --------------------------- |
| GET    | `/geo-events?hours=` | Phân bố địa lý + cảnh báo   | `GeoSecurityData`           |
| POST   | `/geo-rules`         | Tạo/cập nhật luật geo block | `CreateGeoBlockRuleRequest` |
| DELETE | `/geo-rules/{id}`    | Xóa luật geo block          | `{ message }`               |

**Impossible Travel Detection:**
Phát hiện khi cùng user đăng nhập từ nhiều quốc gia khác nhau trong thời gian ngắn. Hiển thị cảnh báo trên giao diện.

### 3.5 Threat Detection

| Method | Path              | Mô tả                              | Response         |
| ------ | ----------------- | ---------------------------------- | ---------------- |
| GET    | `/threats?hours=` | Tổng quan mối đe dọa theo danh mục | `ThreatOverview` |

**Phân loại mối đe dọa:**

- **Authentication Threats:** Brute force, credential stuffing
- **Injection Attacks:** SQL injection, XSS, command injection, path traversal
- **Network Threats:** Tor exit nodes, VPN/proxy, DDoS
- **Data Threats:** Bulk export, unauthorized access
- **Session Threats:** Hijacking, anomalous sessions

### 3.6 Account Lockouts

| Method | Path                    | Mô tả                    | Request/Response     |
| ------ | ----------------------- | ------------------------ | -------------------- |
| GET    | `/lockouts`             | Danh sách khóa tài khoản | `AccountLockout[]`   |
| POST   | `/lockouts`             | Khóa tài khoản           | `LockAccountRequest` |
| POST   | `/lockouts/{id}/unlock` | Mở khóa tài khoản        | `{ message }`        |

**LockAccountRequest:**

```json
{
  "userId": "string",
  "username": "string",
  "reason": "string",
  "durationMinutes": 30,
  "failedAttempts": 0
}
```

### 3.7 IP Whitelist

| Method | Path                 | Mô tả        | Request/Response           |
| ------ | -------------------- | ------------ | -------------------------- |
| GET    | `/ip-whitelist`      | Danh sách IP | `WhitelistedIp[]`          |
| POST   | `/ip-whitelist`      | Thêm IP      | `AddIpWhitelistRequest`    |
| PUT    | `/ip-whitelist/{id}` | Cập nhật IP  | `UpdateIpWhitelistRequest` |
| DELETE | `/ip-whitelist/{id}` | Xóa IP       | `{ message }`              |

**AddIpWhitelistRequest:**

```json
{
  "ipAddress": "192.168.1.100",
  "cidrRange": "/24",
  "description": "Văn phòng chính",
  "expiresInDays": 365
}
```

### 3.8 Device Management

| Method | Path                  | Mô tả                       | Response       |
| ------ | --------------------- | --------------------------- | -------------- |
| GET    | `/devices/{userId}`   | Danh sách thiết bị của user | `UserDevice[]` |
| POST   | `/devices/{id}/trust` | Đánh dấu thiết bị tin cậy   | `{ message }`  |
| DELETE | `/devices/{id}`       | Xóa thiết bị                | `{ message }`  |

### 3.9 Passkeys / WebAuthn

| Method | Path                              | Mô tả                    | Request/Response          |
| ------ | --------------------------------- | ------------------------ | ------------------------- |
| GET    | `/passkeys/{userId}`              | Danh sách passkeys       | `PasskeyCredential[]`     |
| POST   | `/passkeys/register/begin`        | Bắt đầu đăng ký WebAuthn | `CredentialCreateOptions` |
| POST   | `/passkeys/register/complete`     | Hoàn tất đăng ký         | `{ message }`             |
| POST   | `/passkeys/authenticate/begin`    | Bắt đầu xác thực         | `AssertionOptions`        |
| POST   | `/passkeys/authenticate/complete` | Hoàn tất xác thực        | `{ message }`             |
| PUT    | `/passkeys/{id}/rename`           | Đổi tên passkey          | `{ message }`             |
| DELETE | `/passkeys/{id}`                  | Thu hồi passkey          | `{ message }`             |

### 3.10 TOTP (Authenticator App)

| Method | Path             | Mô tả                                      | Request/Response      |
| ------ | ---------------- | ------------------------------------------ | --------------------- |
| POST   | `/totp/setup`    | Tạo TOTP secret + OTPAuth URI              | `TotpSetupResponse`   |
| POST   | `/totp/verify`   | Xác minh TOTP lần đầu + tạo recovery codes | `{ recoveryCodes[] }` |
| POST   | `/totp/validate` | Xác thực TOTP khi đăng nhập                | `{ valid: boolean }`  |

### 3.11 SMS OTP

| Method | Path            | Mô tả                              | Request/Response |
| ------ | --------------- | ---------------------------------- | ---------------- |
| POST   | `/sms/register` | Gửi SMS OTP xác minh số điện thoại | `{ message }`    |
| POST   | `/sms/verify`   | Xác minh mã SMS                    | `{ message }`    |
| POST   | `/sms/send`     | Gửi lại OTP                        | `{ message }`    |

### 3.12 MFA Settings

| Method | Path            | Mô tả                   | Response      |
| ------ | --------------- | ----------------------- | ------------- |
| GET    | `/mfa/{userId}` | Trạng thái MFA của user | `MfaSettings` |
| DELETE | `/mfa/{userId}` | Tắt toàn bộ MFA         | `{ message }` |

---

## 4. Frontend

### 4.1 Cấu trúc file

```
ivf-client/src/app/
├── core/
│   ├── models/
│   │   └── advanced-security.model.ts    # Interfaces & constants
│   └── services/
│       └── advanced-security.service.ts  # HTTP client service
└── features/
    └── admin/
        └── advanced-security/
            ├── advanced-security.component.ts    # Component logic
            ├── advanced-security.component.html  # Template (10 tabs)
            └── advanced-security.component.scss  # Styles
```

### 4.2 Component Architecture

**Standalone component** — không sử dụng NgModule.

```typescript
@Component({
  selector: 'app-advanced-security',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './advanced-security.component.html',
  styleUrls: ['./advanced-security.component.scss'],
})
export class AdvancedSecurityComponent implements OnInit, OnDestroy
```

**State management:** Angular Signals cho reactive state.

```typescript
// Core signals
activeTab = signal<TabKey>("overview");
securityScore = signal<SecurityScore | null>(null);
loading = signal(false);
autoRefresh = signal(true); // 30s auto-refresh

// Data signals (per tab)
loginHistory = signal<LoginHistoryEntry[]>([]);
rateLimitStatus = signal<RateLimitStatus | null>(null);
geoData = signal<GeoSecurityData | null>(null);
threatOverview = signal<ThreatOverview | null>(null);
lockouts = signal<AccountLockout[]>([]);
ipWhitelist = signal<WhitelistedIp[]>([]);
devices = signal<UserDevice[]>([]);
sessions = signal<any[]>([]);
passkeys = signal<PasskeyCredential[]>([]);
mfaSettings = signal<MfaSettings | null>(null);

// Computed signals
currentUser = computed(() => this.authService.user());
currentUserId = computed(() => this.authService.user()?.id ?? "");
currentUsername = computed(
  () =>
    this.authService.user()?.fullName ??
    this.authService.user()?.username ??
    "",
);
```

### 4.3 Auto-load User & Machine

Component tự động populate userId cho tất cả các tab từ user đang đăng nhập:

```typescript
ngOnInit() {
  this.initCurrentUser();    // Auto-populate all userId fields
  this.checkPasskeySupport();
  this.loadOverview();
  this.startAutoRefresh();
}

private initCurrentUser() {
  const userId = this.currentUserId();
  this.passkeyUserId = userId;
  this.deviceUserId = userId;
  this.sessionUserId = userId;
  this.mfaUserId = userId;
  this.totpUserId = userId;
  this.smsUserId = userId;
  this.machineFingerprint = localStorage.getItem('x-device-fingerprint') ?? '';
}
```

**Auto-load khi chuyển tab:**

```typescript
private loadTabData(tab: TabKey) {
  switch (tab) {
    case 'overview':   this.loadOverview(); break;
    case 'passkeys':   this.loadPasskeys(); this.loadMfaSettings(); break;
    case 'devices':    this.loadDevices(); break;
    case 'sessions':   this.loadSessions(); break;
    case 'history':    this.loadLoginHistory(); break;
    case 'rate-limit': this.loadRateLimits(); break;
    case 'geo-security': this.loadGeoSecurity(); break;
    case 'threats':    this.loadThreats(); break;
    case 'lockouts':   this.loadLockouts(); break;
    case 'ip-whitelist': this.loadIpWhitelist(); break;
  }
}
```

### 4.4 User Context Banner

Hiển thị thông tin user đang hoạt động ở đầu trang:

```html
<div class="user-context-banner">
  👤 Đang hoạt động: <strong>Nguyễn Văn A</strong>
  <span class="badge">Admin</span>
  <span>ID: abc-123</span>
  <span>Device: a1b2c3d4e5f6…</span>
</div>
```

### 4.5 Giao diện 10 tab

| #   | Tab            | Icon | Chức năng                                         |
| --- | -------------- | ---- | ------------------------------------------------- |
| 1   | Tổng quan      | 📊   | Security score ring, stat cards, quick actions    |
| 2   | Passkeys       | 🔑   | WebAuthn register/list, MFA status, TOTP, SMS OTP |
| 3   | Thiết bị       | 🖥️   | Device list, trust/remove, risk visualization     |
| 4   | Phiên          | 🔗   | Active sessions, revoke                           |
| 5   | Lịch sử        | 📋   | Login history, risk factors, suspicion flags      |
| 6   | Rate Limit     | ⏱️   | Built-in + custom policies CRUD, violation events |
| 7   | Geo Security   | 🌍   | Geo distribution, impossible travel, block rules  |
| 8   | Threats        | ⚡   | Categories, top IPs, severity breakdown           |
| 9   | Khóa tài khoản | 🔒   | Lockout list, lock/unlock form                    |
| 10  | IP Whitelist   | 🌐   | CRUD với CIDR support, expiry                     |

### 4.6 Formatting Helpers

| Method                     | Mô tả                                   |
| -------------------------- | --------------------------------------- |
| `formatDate(dateStr)`      | Format ngày giờ theo locale Việt Nam    |
| `formatTimeAgo(dateStr)`   | "5 phút trước", "2 giờ trước"           |
| `getDeviceIcon(ua)`        | 📱 (mobile) hoặc 🖥️ (desktop)           |
| `getBrowserName(ua)`       | Chrome / Firefox / Safari / Edge        |
| `getOsName(ua)`            | Windows / macOS / Linux / Android / iOS |
| `formatEventType(type)`    | Tên event tiếng Việt                    |
| `getRiskScoreClass(score)` | CSS class theo mức rủi ro               |

---

## 5. Luồng xác thực MFA

### 5.1 Thiết lập TOTP (Authenticator App)

```
User                Frontend              Backend                 Database
 │                     │                     │                        │
 ├─ Click "Tạo TOTP"──►│                     │                        │
 │                     ├─ POST /totp/setup───►│                        │
 │                     │                     ├─ Generate secret────────►│
 │                     │                     ├─ Create OTPAuth URI     │
 │                     │◄─ { secret, uri } ──┤                        │
 │◄─ Show QR + secret─┤                     │                        │
 │                     │                     │                        │
 ├─ Nhập mã 6 số──────►│                     │                        │
 │                     ├─ POST /totp/verify──►│                        │
 │                     │                     ├─ Validate TOTP code     │
 │                     │                     ├─ Generate recovery codes│
 │                     │                     ├─ EnableTotp()───────────►│
 │                     │◄─ { recoveryCodes }─┤                        │
 │◄─ Show recovery ────┤                     │                        │
```

### 5.2 Xác thực TOTP khi đăng nhập

```
User                Frontend              Backend
 │                     │                     │
 ├─ Nhập mã TOTP──────►│                     │
 │                     ├─ POST /totp/validate►│
 │                     │                     ├─ Validate code (30s window)
 │                     │                     ├─ RecordMfaSuccess()
 │                     │◄─ { valid: true } ──┤
 │◄─ Login thành công──┤                     │
```

### 5.3 SMS OTP Flow

```
1. POST /sms/register { userId, phoneNumber }  → Gửi SMS OTP
2. POST /sms/verify   { userId, code }          → Xác minh & lưu
3. POST /sms/send     { userId }                → Gửi lại OTP (login)
```

---

## 6. Passkeys / WebAuthn

### 6.1 Luồng đăng ký Passkey

```
User                Browser               Frontend              Backend            Database
 │                     │                     │                     │                    │
 ├─ Click "Đăng ký"───►│                     │                     │                    │
 │                     │                     ├─ POST register/begin►│                    │
 │                     │                     │                     ├─ Fido2.RequestNew   │
 │                     │                     │◄─ options ──────────┤                    │
 │                     │◄─ navigator.        │                     │                    │
 │                     │   credentials.      │                     │                    │
 │                     │   create(options)    │                     │                    │
 │◄─── Biometric ──────┤                     │                     │                    │
 │──── Touch/Face ─────►│                     │                     │                    │
 │                     ├─── attestation ─────►│                     │                    │
 │                     │                     ├─ POST register/     │                    │
 │                     │                     │  complete ──────────►│                    │
 │                     │                     │                     ├─ Fido2.MakeNew      │
 │                     │                     │                     ├─ Save credential───►│
 │                     │                     │◄─ success ──────────┤                    │
 │◄─── Đăng ký xong ──┤                     │                     │                    │
```

### 6.2 Luồng xác thực Passkey

```
1. POST authenticate/begin  { userId }        → GetAssertionOptions
2. Browser: navigator.credentials.get()       → User biometric
3. POST authenticate/complete { assertion }   → Fido2.MakeAssertion + UpdateCounter
```

### 6.3 Thiết bị hỗ trợ

| Thiết bị      | Authenticator      | Giao thức                 |
| ------------- | ------------------ | ------------------------- |
| Windows 10/11 | Windows Hello      | Platform (TPM)            |
| macOS / iOS   | Touch ID / Face ID | Platform (Secure Enclave) |
| Android       | Biometric          | Platform                  |
| YubiKey       | FIDO2 Key          | Cross-platform            |
| Chrome        | Passkey Manager    | Hybrid                    |

### 6.4 Yêu cầu trình duyệt

Component kiểm tra `window.PublicKeyCredential` khi khởi tạo. Nếu trình duyệt không hỗ trợ WebAuthn, hiện cảnh báo và vô hiệu nút đăng ký.

---

## 7. Rate Limiting

### 7.1 Built-in Policies

Hệ thống có 4 policy mặc định, **không thể xóa hoặc sửa** qua UI:

| Policy          | Window | Limit   | Scope                    |
| --------------- | ------ | ------- | ------------------------ |
| Global          | 60s    | 100 req | Tất cả endpoint          |
| Authentication  | 60s    | 10 req  | `/api/auth/*`            |
| Sensitive Data  | 60s    | 30 req  | `/api/patients/*`        |
| Digital Signing | 60s    | 30 req  | `/api/digital-signing/*` |

### 7.2 Custom Policies

Admin có thể tạo policy tùy chỉnh với 3 loại window:

| Window Type    | Mô tả                                                |
| -------------- | ---------------------------------------------------- |
| `fixed`        | Reset counter sau mỗi window cố định                 |
| `sliding`      | Cửa sổ trượt, tính request trong N giây gần nhất     |
| `token_bucket` | Token refill liên tục, cho phép burst trong giới hạn |

### 7.3 Violation Events

Endpoint `/rate-limit-events` trả về danh sách sự kiện `ApiRateLimited` trong N giờ gần nhất, bao gồm IP, user agent, endpoint bị ảnh hưởng.

---

## 8. Geo Security

### 8.1 Phân bố địa lý

Endpoint `/geo-events` phân tích `SecurityEvent.Country` để tạo bảng phân bố:

```json
{
  "distribution": [
    { "country": "VN", "countryName": "Vietnam", "count": 1250, "percentage": 85.3 },
    { "country": "US", "countryName": "United States", "count": 120, "percentage": 8.2 }
  ],
  "impossibleTravels": [...],
  "geoBlockRules": [...]
}
```

### 8.2 Impossible Travel Detection

Phát hiện tự động khi:

- Cùng `UserId` xuất hiện từ 2+ quốc gia khác nhau trong cùng khung thời gian
- Ví dụ: Đăng nhập từ VN lúc 10:00, rồi từ US lúc 10:30

### 8.3 Geo Block Rules

Admin tạo luật chặn quốc gia:

```json
{
  "countryCode": "CN",
  "countryName": "China",
  "isBlocked": true,
  "reason": "Chỉ cho phép truy cập từ Việt Nam"
}
```

---

## 9. Threat Detection

### 9.1 Phân loại mối đe dọa

| Category             | Event Types bao gồm                                     | Severity mặc định |
| -------------------- | ------------------------------------------------------- | ----------------- |
| Brute Force          | `LoginBruteForce`, `LoginFailed` (≥3 liên tiếp)         | High              |
| Injection            | `SqlInjectionAttempt`, `XssAttempt`, `CommandInjection` | Critical          |
| Session Hijacking    | `SessionHijackAttempt`, `SessionAnomalous`              | Critical          |
| Data Exfiltration    | `BulkDataExport`, `SensitiveDataAccess`                 | High              |
| Network Anomaly      | `TorExit`, `VpnProxy`, `AnomalousAccess`                | Medium            |
| Privilege Escalation | `PrivilegeEscalation`, `PolicyViolation`                | Critical          |

### 9.2 Top IPs

Endpoint nhóm các mối đe dọa theo IP nguồn, giúp admin nhanh chóng xác định và chặn IP đáng ngờ.

---

## 10. Khóa tài khoản

### 10.1 Khóa tự động

Hệ thống tự phát hiện và khóa khi:

- ≥5 lần đăng nhập thất bại liên tiếp
- Phát hiện brute force attack
- MFA thất bại quá nhiều lần

### 10.2 Khóa thủ công

Admin khóa trực tiếp qua form:

| Field           | Bắt buộc | Mô tả                      |
| --------------- | -------- | -------------------------- |
| User ID         | ✅       | Auto-fill từ user hiện tại |
| Username        | ✅       | Auto-fill từ user hiện tại |
| Reason          | ✅       | Lý do khóa                 |
| Duration (phút) | Có       | Mặc định 30 phút           |
| Failed Attempts | Có       | Mặc định 0                 |

### 10.3 Mở khóa

Admin click "Mở khóa" → `POST /lockouts/{id}/unlock` → Cập nhật `UnlocksAt = now`.

---

## 11. IP Whitelist

### 11.1 Tính năng

- **CIDR support:** Cho phép whitelist dải IP (vd: `192.168.1.0/24`)
- **Expiry:** Đặt thời hạn cho IP (vd: 30 ngày, 365 ngày, hoặc vĩnh viễn)
- **Auto-deactivate:** IP hết hạn tự động bị vô hiệu
- **Audit trail:** Ghi nhận admin nào thêm/sửa/xóa

### 11.2 Ví dụ sử dụng

```
IP: 10.0.1.0      CIDR: /24      Mô tả: "LAN văn phòng HCM"     Hết hạn: Vĩnh viễn
IP: 103.45.67.89   CIDR: null     Mô tả: "VPN đối tác XYZ"       Hết hạn: 30 ngày
```

---

## 12. Quản lý thiết bị & phiên

### 12.1 Thiết bị

Mỗi thiết bị được nhận diện qua `DeviceFingerprint` (kết hợp UserAgent, screen resolution, timezone).

| Thông tin    | Nguồn              |
| ------------ | ------------------ |
| Browser name | Parse từ UserAgent |
| OS           | Parse từ UserAgent |
| IP address   | Request headers    |
| Country/City | GeoIP lookup       |
| Risk score   | Tính tự động 0–100 |
| Trust status | Admin đánh dấu     |

**Risk level:**

- `Low` (0–30): Thiết bị quen thuộc
- `Medium` (31–60): Thiết bị mới hoặc IP mới
- `High` (61–80): Nhiều yếu tố đáng ngờ
- `Critical` (81–100): Tor/VPN, impossible travel

### 12.2 Phiên hoạt động

Quản lý phiên đăng nhập hiện hành. Admin có thể:

- Xem tất cả phiên đang hoạt động theo user
- Thu hồi (revoke) phiên cụ thể
- Xem chi tiết: IP, device, quốc gia, thời gian tạo/hoạt động cuối

---

## 13. Cấu hình & triển khai

### 13.1 NuGet Packages (Backend)

```xml
<!-- src/IVF.Infrastructure/IVF.Infrastructure.csproj -->
<PackageReference Include="Fido2.AspNet" Version="4.0.0-beta1" />

<!-- src/IVF.Application/IVF.Application.csproj -->
<PackageReference Include="Otp.NET" Version="1.4.0" />
```

### 13.2 Fido2 DI Registration

Fido2 được đăng ký trong DI container tại `Program.cs`:

```csharp
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
    options.ServerName = builder.Configuration["Fido2:ServerName"] ?? "IVF System";
    options.Origins = builder.Configuration.GetSection("Fido2:Origins").Get<HashSet<string>>()
        ?? new HashSet<string> { "https://localhost:4200", "http://localhost:4200" };
});
```

Cấu hình trong `appsettings.json`:

```json
{
  "Fido2": {
    "ServerDomain": "localhost",
    "ServerName": "IVF System",
    "Origins": ["https://localhost:4200", "http://localhost:4200"]
  }
}
```

Endpoint passkeys inject `IFido2` từ DI thay vì tạo instance thủ công.

### 13.3 Security Enforcement Middleware

Middleware `SecurityEnforcementMiddleware` thực thi IP whitelist và geo-blocking ở mức network boundary:

```
Request Pipeline (Enterprise-grade ordering):
  CORS → ExceptionHandler → SecurityHeaders → RateLimiter → SecurityEnforcement
       → VaultToken → ApiKey → Authentication → Authorization
       → TokenBinding → ZeroTrust → Endpoints
```

> **Lưu ý quan trọng:** `RateLimiter` đặt **trước** Authentication (chuẩn Google/AWS) để ngăn DDoS trên endpoint xác thực. `TokenBinding` đặt **sau** Authorization để validate JWT claims match request context.

**IP Whitelist Enforcement:**

- Nếu có bất kỳ IP nào trong whitelist (active), chỉ IP trong danh sách mới được truy cập
- Hỗ trợ CIDR range matching (vd: `192.168.1.0/24`)
- Tự động skip IP hết hạn (`ExpiresAt`)
- Response 403 với code `IP_NOT_WHITELISTED`

**Geo-Blocking Enforcement:**

- Kiểm tra header `X-Country-Code` hoặc `GeoCountry` từ context
- Nếu quốc gia nằm trong `GeoBlockRules` với `IsBlocked = true`, trả về 403
- Response code `GEO_BLOCKED`

**Exempt paths:** `/health`, `/healthz`, `/swagger`

### 13.4 Database Migration

Entities được quản lý bởi EF Core migration `AddAdvancedSecurityEntities`:

```bash
# Tạo migration (đã chạy)
dotnet ef migrations add AddAdvancedSecurityEntities \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Áp dụng migration
dotnet ef database update \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API
```

**DbContext DbSets:**

```csharp
public DbSet<AccountLockout> AccountLockouts => Set<AccountLockout>();
public DbSet<IpWhitelistEntry> IpWhitelistEntries => Set<IpWhitelistEntry>();
public DbSet<RateLimitConfig> RateLimitConfigs => Set<RateLimitConfig>();
public DbSet<GeoBlockRule> GeoBlockRules => Set<GeoBlockRule>();
public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();
public DbSet<UserMfaSetting> UserMfaSettings => Set<UserMfaSetting>();
```

### 13.3 Routing (Frontend)

```typescript
// app.routes.ts
{
  path: 'admin/advanced-security',
  loadComponent: () => import('./features/admin/advanced-security/advanced-security.component')
    .then(m => m.AdvancedSecurityComponent),
  canActivate: [authGuard],
}
```

### 13.4 Environment

```typescript
// ivf-client/src/environments/environment.ts
export const environment = {
  apiUrl: "http://localhost:5000/api",
};
```

Service base URL: `${environment.apiUrl}/security/advanced`

---

## 14. Tích hợp Zero Trust

Module bảo mật nâng cao là thành phần chính trong kiến trúc Zero Trust của IVF System.

### 14.1 Mapping CISA Zero Trust Maturity

| Pillar       | Tính năng trong module                                                                         | Level    |
| ------------ | ---------------------------------------------------------------------------------------------- | -------- |
| Identity     | MFA (TOTP + SMS + Passkey), RS256 JWT, token binding, password policy (NIST 800-63B)          | Optimal  |
| Devices      | Device fingerprinting, trust scoring, token-device binding, proof-of-possession               | Optimal  |
| Networks     | Rate limiting (before auth), IP whitelist, geo-fencing, CIDR support                          | Optimal  |
| Applications | AdminOnly policy, per-endpoint rate limits, refresh token reuse detection                     | Optimal  |
| Data         | Audit trail (SecurityEvent), threat indicators, asymmetric signing (forgery-proof)            | Optimal  |

### 14.2 MITRE ATT&CK Coverage

| Tactic            | Techniques covered                                     |
| ----------------- | ------------------------------------------------------ |
| Initial Access    | Brute force detection, geo-blocking, IP whitelist      |
| Credential Access | MFA enforcement, passkeys, TOTP validation             |
| Persistence       | Session management, device trust revocation            |
| Lateral Movement  | Impossible travel detection, concurrent session alerts |
| Exfiltration      | Bulk data export monitoring, sensitive access logging  |
| Impact            | Account lockout (auto/manual), rate limiting           |

### 14.3 Liên kết với các module khác

| Module            | Tương tác                                |
| ----------------- | ---------------------------------------- |
| Vault Integration | Secret rotation, token management, audit |
| Digital Signing   | Rate limited (30 req/min), audit logged  |
| Biometric Matcher | Device fingerprint correlation           |
| SignalR Hubs      | Real-time security event notifications   |

---

## Xem thêm

- [Zero Trust Architecture](zero_trust_architecture.md) — Kiến trúc Zero Trust tổng thể
- [Vault Integration Guide](vault_integration_guide.md) — Quản lý secrets & tokens
- [Digital Signing](digital_signing.md) — Ký số PDF
- [Developer Guide](developer_guide.md) — Hướng dẫn phát triển tổng thể

---

## 15. Enterprise Security (Google/Microsoft/AWS)

> Nâng cấp bảo mật đạt chuẩn enterprise theo Google BeyondCorp, Microsoft Entra ID và AWS IAM.

### 15.1 JWT RS256 Asymmetric Signing

> `src/IVF.API/Services/JwtKeyService.cs`

**Trước:** HS256 (symmetric) — nếu secret key bị lộ, attacker có thể tạo token giả mạo bất kỳ.

**Sau:** RS256 (asymmetric, RSA 3072-bit) — chỉ private key mới ký được token. Kể cả khi validation key bị lộ, token cũng không thể bị giả mạo.

#### Kiến trúc

```
┌─────────────────────────────────────────────────┐
│              JwtKeyService (Singleton)            │
│  ┌─────────────────────────────────────────────┐ │
│  │  RSA 3072-bit Key Pair                      │ │
│  │  ├── Private Key (jwt-private.pem)          │ │
│  │  │   → Ký token (chỉ server sử dụng)       │ │
│  │  └── Public Key (derived)                   │ │
│  │      → Xác thực token (có thể chia sẻ)     │ │
│  └─────────────────────────────────────────────┘ │
│  SigningCredentials: RS256 + KeyId (SHA256)       │
│  ValidationKey: RSA Public Key                    │
└─────────────────────────────────────────────────┘
```

#### Cấu hình

```json
{
  "JwtSettings": {
    "RsaKeysPath": "keys/jwt",
    "Issuer": "IVF-System",
    "Audience": "IVF-Client"
  }
}
```

- Key tự động tạo lần đầu khởi động, lưu tại `keys/jwt/jwt-private.pem`
- File được đặt thuộc tính `ReadOnly | Hidden`
- NIST khuyến nghị RSA 3072-bit cho hệ thống hoạt động sau 2030
- Có `KeyId` (SHA256 của public modulus) hỗ trợ key rotation

#### API Chống Algorithm Confusion

```csharp
TokenValidationParameters = new TokenValidationParameters
{
    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
    // ... chỉ chấp nhận RS256, chặn HS256/none attack
}
```

#### So sánh với Enterprise Standards

| Tiêu chí              | HS256 (cũ)          | RS256 (hiện tại)       | Google/Microsoft   |
| --------------------- | ------------------- | ---------------------- | ------------------ |
| Loại key              | Symmetric (shared)  | Asymmetric (pub/priv)  | Asymmetric         |
| Key size              | 256-bit             | 3072-bit               | 2048-4096 bit      |
| Token forgery risk    | Cao (key lộ = done) | Không (cần private)    | Không              |
| Key rotation          | Khó (toàn hệ thống) | Dễ (KeyId-based)       | Tự động            |
| Algorithm confusion   | Dễ tấn công         | Chặn (ValidAlgorithms) | Chặn               |

---

### 15.2 Refresh Token Family Detection (RFC 6749 §10.4)

> `src/IVF.API/Services/RefreshTokenFamilyService.cs`

Phát hiện tấn công refresh token reuse — khi attacker đánh cắp refresh token và cả attacker lẫn nạn nhân đều sử dụng nó.

#### Cách hoạt động

```
Login → Token Family A created
  │
  ├─ Refresh #1: Token A₁ → A₂ (A₁ marked as used)
  │
  ├─ Refresh #2: Token A₂ → A₃ (A₂ marked as used)
  │
  └─ ⚠️ Attacker replays A₁ (already used!)
     → REUSE DETECTED → Revoke entire Family A
     → Revoke user's refresh token in DB
     → Log Critical security event
     → 401 TOKEN_REUSE_DETECTED
```

#### API

```csharp
// Đăng ký token mới khi login/refresh
tokenFamily.RegisterToken(userId, refreshToken, previousToken: null); // login
tokenFamily.RegisterToken(userId, newToken, previousToken);          // refresh

// Validate trước khi chấp nhận refresh
var validation = tokenFamily.ValidateToken(refreshToken);
if (validation.IsReuse)
{
    tokenFamily.RevokeFamily(validation.FamilyId!);
    // Revoke user session + log critical event
}
```

#### Tích hợp endpoints

| Endpoint                            | Hành động                                           |
| ----------------------------------- | --------------------------------------------------- |
| `POST /api/auth/login`              | `RegisterToken(userId, refreshToken, null)`          |
| `POST /api/auth/refresh`            | `ValidateToken()` → `RegisterToken()` nếu hợp lệ   |
| `POST /api/auth/mfa-verify`         | `RegisterToken(userId, refreshToken, null)`          |
| `POST /api/auth/passkey-login`      | `RegisterToken(userId, refreshToken, null)`          |

#### So sánh

| Tiêu chí            | Trước                   | Sau (RFC 6749)                        | Google/Microsoft        |
| -------------------- | ----------------------- | ------------------------------------- | ----------------------- |
| Token reuse          | Không phát hiện         | Phát hiện + revoke cả family          | Phát hiện + revoke      |
| Token lineage        | Không theo dõi          | Family → Used → Active tracking       | Family tracking         |
| Stolen token impact  | Vĩnh viễn (7 ngày)     | Bị phát hiện ngay khi reuse           | Tương tự                |

---

### 15.3 Token Binding Middleware (Microsoft CAE Pattern)

> `src/IVF.API/Middleware/TokenBindingMiddleware.cs`

Xác thực rằng JWT claims khớp với request context thực tế. Ngăn chặn stolen token được sử dụng trên thiết bị khác.

#### Validation Flow

```
Request → TokenBindingMiddleware
  │
  ├─ Extract JWT claims: device_fingerprint, session_id, jti
  │
  ├─ Compare device_fingerprint claim vs X-Device-Fingerprint header
  │   └─ Mismatch → Log warning (drift detection)
  │
  ├─ Store session_id → context.Items["TokenSessionId"]
  │   └─ ZeroTrust middleware sẽ validate session còn active
  │
  └─ Store jti → context.Items["TokenJti"]
      └─ Hỗ trợ token revocation kiểm tra
```

#### JWT Claims được bind

| Claim              | Mô tả                      | Validation                     |
| ------------------ | --------------------------- | ------------------------------ |
| `device_fingerprint` | Fingerprint thiết bị gốc  | So sánh X-Device-Fingerprint   |
| `session_id`        | Session ID tại thời điểm login | Truyền cho ZeroTrust validate |
| `jti`               | JWT unique ID              | Hỗ trợ token revocation        |
| `iat`               | Issued-at timestamp        | Kiểm tra freshness             |

#### Exempt Paths

```
/api/auth/login, /api/auth/refresh, /api/auth/mfa-verify,
/api/auth/mfa-send-sms, /api/auth/passkey-login,
/health, /healthz, /swagger, /hubs/*
```

---

### 15.4 Password Policy Engine (NIST SP 800-63B)

> `src/IVF.API/Services/PasswordPolicyService.cs`

Tuân thủ đầy đủ NIST Special Publication 800-63B — Digital Identity Guidelines.

#### Quy tắc validation

| Quy tắc                    | Giá trị             | Chuẩn NIST             |
| --------------------------- | ------------------- | ---------------------- |
| Độ dài tối thiểu            | 10 ký tự            | ≥8 (khuyến nghị ≥10)  |
| Độ dài tối đa               | 128 ký tự           | ≥64                   |
| Complexity categories       | 3/4 required        | Khuyến nghị            |
| Banned passwords            | 50+ common passwords | Bắt buộc              |
| Username similarity         | Blocked             | Bắt buộc              |
| Repetitive patterns         | Blocked (aaa, 111)  | Khuyến nghị            |
| Sequential patterns         | Blocked (abc, 123)  | Khuyến nghị            |

#### Complexity Categories

- Chữ hoa (A-Z)
- Chữ thường (a-z)
- Số (0-9)
- Ký tự đặc biệt (!@#$%^&*...)

→ Cần ít nhất **3 trong 4** loại.

#### Entropy Scoring

| Strength    | Entropy (bits) | Mô tả                    |
| ----------- | -------------- | ------------------------ |
| VeryWeak    | < 28           | Không chấp nhận          |
| Weak        | 28–35          | Không chấp nhận          |
| Fair        | 36–59          | Tối thiểu chấp nhận     |
| Strong      | 60–80          | Tốt                     |
| VeryStrong  | > 80           | Xuất sắc                 |

#### API Usage

```csharp
var result = passwordPolicy.Validate(password, username);
// result.IsValid      — true/false
// result.Errors       — string[] chi tiết lỗi
// result.Strength     — VeryWeak → VeryStrong
// result.EntropyBits  — entropy tính bằng bit
```

#### Banned Password List (trích)

```
password, 123456, qwerty, admin, letmein, welcome,
monkey, dragon, master, 1234567890, abc123, ...
```

---

### 15.5 Frontend Security Hardening

#### Security Interceptor

> `ivf-client/src/app/core/interceptors/security.interceptor.ts`

Tự động gắn security headers vào mọi HTTP request:

| Header               | Mô tả                          | Pattern                     |
| -------------------- | ------------------------------ | --------------------------- |
| `X-Device-Fingerprint` | Fingerprint thiết bị (hash)  | Deterministic từ browser signals |
| `X-Session-Id`        | Session ID từ JWT             | Lưu sessionStorage          |
| `X-Correlation-Id`    | Request tracing ID            | Timestamp + random          |
| `X-Requested-With`    | CSRF protection               | `XMLHttpRequest`            |

#### Session Binding

> `ivf-client/src/app/core/services/auth.service.ts`

Sau khi login thành công:
1. Decode JWT payload (Base64)
2. Extract `session_id` claim
3. Lưu vào `sessionStorage['zt_session_id']`
4. Security interceptor tự động gắn vào header `X-Session-Id`

Khi logout:
- Xóa `zt_session_id` và `zt_device_fingerprint` khỏi sessionStorage
- Xóa tokens và user data khỏi localStorage

#### Device Fingerprint Generation

Tính từ các tín hiệu browser (không thu thập PII):

```
navigator.userAgent + navigator.language + navigator.platform
+ Intl.DateTimeFormat().resolvedOptions().timeZone
+ screen.width + "x" + screen.height
→ simpleHash() → 8-char hex string
→ Cache trong sessionStorage['zt_device_fingerprint']
```

---

### 15.6 Middleware Pipeline (Enterprise Order)

```
┌──────────────────────────────────────────────────────────────┐
│  1. CORS                    — Cross-origin handling          │
│  2. ExceptionHandler        — Global error handling          │
│  3. SecurityHeaders         — OWASP A+ headers               │
│     ├── X-Content-Type-Options: nosniff                      │
│     ├── X-Frame-Options: DENY                                │
│     ├── Strict-Transport-Security: max-age=31536000          │
│     ├── Content-Security-Policy                              │
│     ├── Referrer-Policy: strict-origin-when-cross-origin     │
│     └── Permissions-Policy                                   │
│  4. RateLimiter ⚡          — TRƯỚC auth (chặn DDoS)         │
│  5. SecurityEnforcement     — IP whitelist + geo-blocking    │
│  6. VaultToken Auth         — X-Vault-Token middleware       │
│  7. ApiKey Auth             — X-API-Key middleware           │
│  8. Authentication          — JWT Bearer validation (RS256)  │
│  9. Authorization           — Role/policy checks             │
│ 10. TokenBinding 🆕        — Device + session claim binding  │
│ 11. ZeroTrust              — Adaptive risk assessment        │
│ 12. Endpoints              — Minimal API handlers            │
└──────────────────────────────────────────────────────────────┘
```

> **Key insight:** RateLimiter đặt ở vị trí 4 (trước Authentication) theo chuẩn Google Cloud & AWS API Gateway — ngăn chặn DDoS tấn công endpoint xác thực. TokenBinding đặt ở vị trí 10 (sau Authorization) để validate JWT claims match request context.

---

### 15.7 So sánh với Enterprise Standards

#### Google BeyondCorp

| Tiêu chí                           | IVF System                           | Google BeyondCorp        | Đạt? |
| ----------------------------------- | ------------------------------------ | ------------------------ | ---- |
| Device trust required               | ✅ DeviceFingerprint + trust scoring | Device inventory         | ✅   |
| User identity verification          | ✅ MFA (TOTP + SMS + Passkey)        | 2FA mandatory            | ✅   |
| Context-aware access                | ✅ ZeroTrust middleware + risk score | Access Context Manager   | ✅   |
| No VPN dependency                   | ✅ Token-based, no VPN needed        | No VPN                   | ✅   |
| Continuous verification             | ✅ Per-request TokenBinding          | Continuous evaluation    | ✅   |

#### Microsoft Entra ID / Azure AD

| Tiêu chí                           | IVF System                           | Microsoft Entra          | Đạt? |
| ----------------------------------- | ------------------------------------ | ------------------------ | ---- |
| Conditional Access Evaluation (CAE) | ✅ TokenBindingMiddleware            | CAE                      | ✅   |
| Passwordless auth                   | ✅ Passkeys/WebAuthn (FIDO2)         | Windows Hello, FIDO2     | ✅   |
| Asymmetric token signing            | ✅ RS256 (RSA 3072-bit)              | RS256                    | ✅   |
| Token reuse detection               | ✅ RefreshTokenFamilyService         | Token protection         | ✅   |
| Risk-based session management       | ✅ Adaptive sessions + risk scoring  | Identity Protection      | ✅   |
| Session binding                     | ✅ Device + session JWT claims       | Token binding            | ✅   |

#### AWS IAM / Cognito

| Tiêu chí                           | IVF System                           | AWS                      | Đạt? |
| ----------------------------------- | ------------------------------------ | ------------------------ | ---- |
| Rate limiting before auth           | ✅ Pipeline position 4               | API Gateway throttling   | ✅   |
| Password policy (NIST 800-63B)      | ✅ PasswordPolicyService             | Cognito password policy  | ✅   |
| Geo-blocking                        | ✅ GeoBlockRules + enforcement       | WAF geo-blocking         | ✅   |
| IP allowlisting                     | ✅ CIDR support + expiry             | Security groups          | ✅   |
| Threat detection                    | ✅ 40+ event types, MITRE ATT&CK    | GuardDuty                | ✅   |
| Audit logging                       | ✅ SecurityEvent (partitioned)       | CloudTrail               | ✅   |
