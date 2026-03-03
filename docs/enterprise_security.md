# Enterprise Security Module — Tài liệu kỹ thuật

> **Module Bảo mật Doanh nghiệp** — Hệ thống bảo mật toàn diện cấp enterprise, lấy cảm hứng từ Google BeyondCorp, Microsoft Entra ID, và AWS IAM.  
> Bao gồm: Conditional Access, Incident Response, Data Retention, Impersonation, Permission Delegation, Behavioral Analytics, Security Notifications, Bot Detection.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Domain Entities](#2-domain-entities)
3. [Service Layer](#3-service-layer)
4. [API Endpoints](#4-api-endpoints)
5. [Middleware Integration](#5-middleware-integration)
6. [Frontend](#6-frontend)
7. [Luồng nghiệp vụ](#7-luồng-nghiệp-vụ)
8. [Seed Data](#8-seed-data)
9. [Cấu hình & DI](#9-cấu-hình--di)
10. [Hướng dẫn mở rộng](#10-hướng-dẫn-mở-rộng)

---

## 1. Tổng quan kiến trúc

### Sơ đồ tổng thể

```
┌────────────────────┐     ┌──────────────────────────┐     ┌──────────────────────────┐
│  Angular 21        │────▶│   .NET 10 API            │────▶│      PostgreSQL           │
│  (Frontend)        │     │   Minimal API            │     │                           │
│                    │     │   35 Endpoints            │     │  ConditionalAccessPolicies│
│  8 Tab UI          │     │   8 Sub-groups           │     │  SecurityIncidents        │
│  Signal-based      │     │   AdminOnly auth         │     │  IncidentResponseRules    │
│  Vietnamese UI     │     │                          │     │  DataRetentionPolicies    │
│  User autocomplete │     │  Services:               │     │  ImpersonationRequests    │
│  Permission select │     │   SecurityEvent           │     │  PermissionDelegations    │
│                    │     │   ConditionalAccess       │     │  UserBehaviorProfiles     │
│                    │     │   BehavioralAnalytics     │     │  NotificationPreferences  │
│                    │     │   IncidentResponse        │     │  SecurityEvents           │
│                    │     │   DataRetention (hosted)  │     │                           │
│                    │     │   SecurityNotification    │     │                           │
│                    │     │   BotDetection            │     │                           │
└────────────────────┘     └──────────────────────────┘     └───────────────────────────┘
                                      │
                              ┌───────┴───────┐
                              │ ZeroTrust MW  │
                              │ Threat Assess │
                              │ Cond. Access  │
                              │ Session Valid │
                              └───────────────┘
```

### Stack công nghệ

| Layer          | Công nghệ                   | Vai trò                                       |
| -------------- | --------------------------- | --------------------------------------------- |
| Frontend       | Angular 21, Signals, OnPush | UI quản lý 8 tab, Vietnamese, autocomplete    |
| API            | .NET 10, Minimal API        | 35 endpoint, AdminOnly authorization          |
| Infrastructure | EF Core, PostgreSQL         | Repository pattern, hosted background service |
| Middleware     | ZeroTrustMiddleware         | Continuous verification, conditional access   |
| Domain         | DDD Entities                | 8 entity classes                              |

### Cấu trúc file

```
src/IVF.Domain/Entities/
├── ConditionalAccessPolicy.cs       # Chính sách truy cập có điều kiện
├── SecurityIncident.cs              # Sự cố bảo mật + IncidentResponseRule
├── DataRetentionPolicy.cs           # Chính sách lưu trữ dữ liệu
├── ImpersonationRequest.cs          # Yêu cầu mạo danh (dual-approval)
├── PermissionDelegation.cs          # Ủy quyền quyền hạn
├── UserBehaviorProfile.cs           # Hồ sơ hành vi người dùng
├── NotificationPreference.cs        # Tùy chọn thông báo bảo mật
└── SecurityEvent.cs                 # Sự kiện bảo mật (immutable log)

src/IVF.Application/Interfaces/
├── IConditionalAccessService.cs     # Đánh giá chính sách truy cập
├── IBehavioralAnalyticsService.cs   # Phân tích hành vi
├── IIncidentResponseService.cs      # Xử lý sự cố tự động
├── ISecurityNotificationService.cs  # Thông báo bảo mật đa kênh
├── IDataRetentionService.cs         # Background purge service
├── IBotDetectionService.cs          # Phát hiện bot
├── IGeoLocationService.cs           # Xác định vị trí địa lý
├── ISecretsScanner.cs               # Quét secrets bị lộ
└── IBreachedPasswordService.cs      # Kiểm tra mật khẩu bị lộ

src/IVF.Infrastructure/Services/
├── ConditionalAccessService.cs      # Implement: đánh giá policy
├── BehavioralAnalyticsService.cs    # Implement: z-score anomaly
├── IncidentResponseService.cs       # Implement: match rules → actions
├── SecurityNotificationService.cs   # Implement: dispatch notifications
├── DataRetentionService.cs          # Implement: IHostedService daily purge
├── SecurityEventService.cs          # Implement: centralized event routing
└── BotDetectionService.cs           # Implement: bot detection logic

src/IVF.API/
├── Endpoints/EnterpriseSecurityEndpoints.cs  # 35 routes, 8 groups
└── Middleware/ZeroTrustMiddleware.cs          # Continuous verification

ivf-client/src/app/
├── features/admin/enterprise-security/
│   └── enterprise-security.component.ts      # 8-tab UI, Vietnamese
├── core/services/enterprise-security.service.ts  # HTTP client
└── core/models/enterprise-security.model.ts      # TypeScript interfaces
```

---

## 2. Domain Entities

### 2.1 ConditionalAccessPolicy

> `src/IVF.Domain/Entities/ConditionalAccessPolicy.cs`

Chính sách truy cập có điều kiện — đánh giá mỗi request dựa trên context (role, IP, quốc gia, thiết bị, thời gian, mức rủi ro).

| Property                 | Type        | Mô tả                                                                                 |
| ------------------------ | ----------- | ------------------------------------------------------------------------------------- |
| `Id`                     | `Guid`      | Primary key                                                                           |
| `Name`                   | `string`    | Tên policy                                                                            |
| `Description`            | `string?`   | Mô tả                                                                                 |
| `IsEnabled`              | `bool`      | Bật/tắt policy                                                                        |
| `Priority`               | `int`       | Ưu tiên (thấp hơn = ưu tiên cao hơn)                                                  |
| `TargetRoles`            | `string?`   | JSON array roles áp dụng, vd: `["Admin","Doctor"]`                                    |
| `TargetUsers`            | `string?`   | JSON array user IDs cụ thể                                                            |
| `AllowedCountries`       | `string?`   | JSON array mã quốc gia cho phép, vd: `["VN","US"]`                                    |
| `BlockedCountries`       | `string?`   | JSON array mã quốc gia chặn                                                           |
| `AllowedIpRanges`        | `string?`   | JSON array CIDR ranges, vd: `["10.0.0.0/8"]`                                          |
| `AllowedTimeWindows`     | `string?`   | JSON array `{dayOfWeek, startHour, endHour}`                                          |
| `MaxRiskLevel`           | `int?`      | Mức rủi ro tối đa cho phép (0-100)                                                    |
| `RequireMfa`             | `bool`      | Yêu cầu MFA                                                                           |
| `RequireCompliantDevice` | `bool`      | Yêu cầu thiết bị tuân thủ                                                             |
| `BlockVpnTor`            | `bool`      | Chặn VPN/Tor                                                                          |
| `RequiredDeviceTrust`    | `string?`   | Mức tin cậy thiết bị yêu cầu                                                          |
| `Action`                 | `string`    | Hành động: `Allow`, `Block`, `RequireMfa`, `RequireStepUp`, `RequireDeviceCompliance` |
| `CustomMessage`          | `string?`   | Thông báo tùy chỉnh khi bị chặn                                                       |
| `CreatedAt`              | `DateTime`  | Ngày tạo (UTC)                                                                        |
| `UpdatedAt`              | `DateTime?` | Ngày cập nhật                                                                         |

**Actions enum:**

- `Allow` — Cho phép truy cập
- `Block` — Chặn hoàn toàn, trả 403
- `RequireMfa` — Yêu cầu xác thực MFA (nếu chưa có)
- `RequireStepUp` — Yêu cầu xác thực bổ sung
- `RequireDeviceCompliance` — Yêu cầu thiết bị tuân thủ

### 2.2 SecurityIncident

> `src/IVF.Domain/Entities/SecurityIncident.cs`

Sự cố bảo mật — được tạo tự động bởi `IncidentResponseService` khi event match rule.

| Property          | Type        | Mô tả                                                                        |
| ----------------- | ----------- | ---------------------------------------------------------------------------- |
| `Id`              | `Guid`      | Primary key                                                                  |
| `IncidentType`    | `string`    | Loại sự cố (từ event type)                                                   |
| `Severity`        | `string`    | Mức độ: `Low`, `Medium`, `High`, `Critical`                                  |
| `Status`          | `string`    | Trạng thái: `Open` → `Investigating` → `Resolved` → `Closed`/`FalsePositive` |
| `UserId`          | `Guid?`     | User liên quan                                                               |
| `Username`        | `string?`   | Username                                                                     |
| `IpAddress`       | `string?`   | IP address                                                                   |
| `Description`     | `string?`   | Mô tả chi tiết                                                               |
| `Details`         | `string?`   | JSON chi tiết bổ sung                                                        |
| `ActionsTaken`    | `string?`   | JSON array các hành động đã thực hiện                                        |
| `AssignedTo`      | `string?`   | Người được gán xử lý                                                         |
| `Resolution`      | `string?`   | Kết quả xử lý                                                                |
| `ResolvedAt`      | `DateTime?` | Thời gian giải quyết                                                         |
| `ResolvedBy`      | `string?`   | Người giải quyết                                                             |
| `RelatedEventIds` | `string?`   | JSON array event IDs liên quan                                               |
| `CreatedAt`       | `DateTime`  | Thời gian tạo                                                                |

**State machine:**

```
Open ──▶ Investigating ──▶ Resolved ──▶ Closed
                  │                        ▲
                  └──────▶ FalsePositive ───┘
```

### 2.3 IncidentResponseRule

> Embedded trong `SecurityIncident.cs`

Quy tắc phản hồi tự động — match event types + severities → trigger automated actions.

| Property               | Type       | Mô tả                                     |
| ---------------------- | ---------- | ----------------------------------------- |
| `Id`                   | `Guid`     | Primary key                               |
| `Name`                 | `string`   | Tên quy tắc                               |
| `Description`          | `string?`  | Mô tả                                     |
| `IsEnabled`            | `bool`     | Bật/tắt                                   |
| `Priority`             | `int`      | Ưu tiên                                   |
| `TriggerEventTypes`    | `string`   | JSON array event types kích hoạt          |
| `TriggerSeverities`    | `string`   | JSON array mức độ severity kích hoạt      |
| `TriggerThreshold`     | `int?`     | Số sự kiện trước khi kích hoạt (optional) |
| `TriggerWindowMinutes` | `int?`     | Cửa sổ thời gian (phút)                   |
| `Actions`              | `string`   | JSON array actions tự động                |
| `IncidentSeverity`     | `string`   | Severity của incident được tạo            |
| `CreatedAt`            | `DateTime` | Thời gian tạo                             |

**Automated Actions:**

- `lock_account` — Khóa tài khoản 60 phút (AccountLockout)
- `revoke_sessions` — Thu hồi tất cả phiên hoạt động
- `block_ip` — Chặn IP (log event)
- `notify_admin` — Gửi thông báo cho admin qua `ISecurityNotificationService`
- `require_password_change` — Yêu cầu đổi mật khẩu

### 2.4 DataRetentionPolicy

> `src/IVF.Domain/Entities/DataRetentionPolicy.cs`

Chính sách lưu trữ dữ liệu — tuân thủ HIPAA (7 năm) và GDPR (data minimization).

| Property          | Type        | Mô tả                                                                          |
| ----------------- | ----------- | ------------------------------------------------------------------------------ |
| `Id`              | `Guid`      | Primary key                                                                    |
| `EntityType`      | `string`    | Loại dữ liệu: `SecurityEvent`, `UserLoginHistory`, `UserSession`, `AuditLog`   |
| `RetentionDays`   | `int`       | Số ngày lưu trữ                                                                |
| `Action`          | `string`    | Hành động: `Delete` (xóa cứng), `Anonymize` (ẩn danh hóa), `Archive` (lưu trữ) |
| `IsEnabled`       | `bool`      | Bật/tắt policy                                                                 |
| `Description`     | `string?`   | Mô tả                                                                          |
| `LastExecutedAt`  | `DateTime?` | Lần thực thi gần nhất                                                          |
| `LastPurgedCount` | `int`       | Số bản ghi đã xử lý lần cuối                                                   |
| `CreatedAt`       | `DateTime`  | Thời gian tạo                                                                  |

### 2.5 ImpersonationRequest

> `src/IVF.Domain/Entities/ImpersonationRequest.cs`

Yêu cầu mạo danh — dual-approval workflow, tạo JWT impersonation theo RFC 8693.

| Property        | Type        | Mô tả                                                 |
| --------------- | ----------- | ----------------------------------------------------- |
| `Id`            | `Guid`      | Primary key                                           |
| `RequestedBy`   | `Guid`      | Admin yêu cầu                                         |
| `TargetUserId`  | `Guid`      | User bị mạo danh                                      |
| `Reason`        | `string`    | Lý do                                                 |
| `Status`        | `string`    | `Pending` → `Approved` → `Active` → `Ended`/`Expired` |
| `ApprovedBy`    | `Guid?`     | Admin duyệt                                           |
| `DeniedBy`      | `Guid?`     | Admin từ chối                                         |
| `DenialReason`  | `string?`   | Lý do từ chối                                         |
| `SessionToken`  | `string?`   | JWT token impersonation                               |
| `ExpiresAt`     | `DateTime?` | Thời gian hết hạn                                     |
| `StartedAt`     | `DateTime?` | Thời gian bắt đầu mạo danh                            |
| `EndedAt`       | `DateTime?` | Thời gian kết thúc                                    |
| `EndReason`     | `string?`   | Lý do kết thúc                                        |
| `DurationHours` | `int`       | Thời lượng yêu cầu (giờ)                              |
| `CreatedAt`     | `DateTime`  | Thời gian tạo                                         |

**State machine:**

```
Pending ──▶ Approved ──▶ Active ──▶ Ended
   │                                  ▲
   ├──▶ Denied                        │
   └──▶ Expired ──────────────────────┘
```

**Domain methods:**

- `Approve(approvedBy, expiresAt)` — Duyệt yêu cầu
- `Deny(deniedBy, reason)` — Từ chối
- `Activate(sessionToken)` — Kích hoạt phiên mạo danh
- `End(reason)` — Kết thúc phiên

**JWT Claims khi mạo danh (RFC 8693):**

| Claim             | Value                             |
| ----------------- | --------------------------------- |
| `sub`             | ID user bị mạo danh               |
| `name`            | Username user bị mạo danh         |
| `role`            | Role user bị mạo danh             |
| `act_sub`         | ID admin thực hiện mạo danh       |
| `act_approved_by` | ID admin duyệt                    |
| `impersonation`   | `"true"`                          |
| Standard claims   | `jti`, `iat`, `exp`, `iss`, `aud` |

### 2.6 PermissionDelegation

> `src/IVF.Domain/Entities/PermissionDelegation.cs`

Ủy quyền quyền hạn — cho phép Doctor ủy quyền tạm thời cho Nurse mà không thay đổi role vĩnh viễn.

| Property       | Type        | Mô tả                                                               |
| -------------- | ----------- | ------------------------------------------------------------------- |
| `Id`           | `Guid`      | Primary key                                                         |
| `FromUserId`   | `Guid`      | Người ủy quyền                                                      |
| `ToUserId`     | `Guid`      | Người được ủy quyền                                                 |
| `Permissions`  | `string`    | JSON array permission codes, vd: `["patients:read","cycles:write"]` |
| `ValidFrom`    | `DateTime`  | Thời gian bắt đầu có hiệu lực                                       |
| `ValidUntil`   | `DateTime`  | Thời gian hết hiệu lực                                              |
| `IsRevoked`    | `bool`      | Đã thu hồi?                                                         |
| `RevokedAt`    | `DateTime?` | Thời gian thu hồi                                                   |
| `RevokeReason` | `string?`   | Lý do thu hồi                                                       |
| `Reason`       | `string?`   | Lý do ủy quyền                                                      |
| `CreatedAt`    | `DateTime`  | Thời gian tạo                                                       |

**Logic kiểm tra quyền:**

- `HasPermissionAsync(userId, permissionCode)` trong `UserPermissionRepository` kiểm tra:
  1. Direct permissions (UserPermission table)
  2. Delegated permissions (PermissionDelegation — active, not revoked, within time window)

### 2.7 UserBehaviorProfile

> `src/IVF.Domain/Entities/UserBehaviorProfile.cs`

Hồ sơ hành vi người dùng — xây dựng từ lịch sử đăng nhập, phục vụ anomaly detection.

| Property                        | Type        | Mô tả                                     |
| ------------------------------- | ----------- | ----------------------------------------- |
| `Id`                            | `Guid`      | Primary key                               |
| `UserId`                        | `Guid`      | User reference                            |
| `TypicalLoginHours`             | `string?`   | JSON array giờ đăng nhập thường dùng      |
| `CommonIpAddresses`             | `string?`   | JSON array IP thường dùng                 |
| `CommonCountries`               | `string?`   | JSON array quốc gia thường dùng           |
| `CommonDeviceFingerprints`      | `string?`   | JSON array device fingerprint thường dùng |
| `CommonUserAgents`              | `string?`   | JSON array user agent thường dùng         |
| `AverageSessionDurationMinutes` | `int`       | Thời lượng phiên trung bình (phút)        |
| `TotalLogins`                   | `int`       | Tổng số lần đăng nhập                     |
| `FailedLogins`                  | `int`       | Tổng số lần đăng nhập thất bại            |
| `RiskFactors`                   | `string?`   | JSON array yếu tố rủi ro                  |
| `LastLoginAt`                   | `DateTime?` | Lần đăng nhập cuối                        |
| `LastFailedLoginAt`             | `DateTime?` | Lần thất bại cuối                         |
| `CreatedAt`                     | `DateTime`  | Thời gian tạo                             |
| `UpdatedAt`                     | `DateTime?` | Thời gian cập nhật                        |

### 2.8 NotificationPreference

> `src/IVF.Domain/Entities/NotificationPreference.cs`

Tùy chọn thông báo bảo mật — mỗi user có thể cấu hình kênh + loại sự kiện muốn nhận.

| Property     | Type       | Mô tả                                                                   |
| ------------ | ---------- | ----------------------------------------------------------------------- |
| `Id`         | `Guid`     | Primary key                                                             |
| `UserId`     | `Guid`     | User reference                                                          |
| `Channel`    | `string`   | Kênh: `in_app`, `email`, `sms`                                          |
| `EventTypes` | `string`   | JSON array event types muốn nhận, vd: `["LoginFailed","AccountLocked"]` |
| `IsEnabled`  | `bool`     | Bật/tắt                                                                 |
| `CreatedAt`  | `DateTime` | Thời gian tạo                                                           |

### 2.9 SecurityEvent

> `src/IVF.Domain/Entities/SecurityEvent.cs`

Sự kiện bảo mật (immutable log) — log tất cả hoạt động bảo mật trong hệ thống.

**50+ Event Types** phân loại theo nhóm:

| Nhóm               | Event Types                                                                                         |
| ------------------ | --------------------------------------------------------------------------------------------------- |
| Authentication     | `LoginSuccess`, `LoginFailed`, `LogoutSuccess`, `TokenRefreshed`, `MfaVerified`, `MfaFailed`        |
| Authorization      | `AccessDenied`, `PermissionDenied`, `UnauthorizedAccess`                                            |
| Zero Trust         | `ZtAccessDenied`, `ZtElevatedVerification`, `ZtSessionBlocked`                                      |
| Threats            | `BruteForceDetected`, `CredentialStuffingDetected`, `AccountTakeoverDetected`, `SuspiciousActivity` |
| Device/Session     | `NewDeviceDetected`, `DeviceBlocked`, `SessionHijackingDetected`, `SessionRevoked`                  |
| Data               | `SensitiveDataAccess`, `DataExport`, `DataDeletion`                                                 |
| API                | `ApiKeyCreated`, `ApiKeyRevoked`, `RateLimitExceeded`                                               |
| Conditional Access | `ConditionalAccessBlocked`, `ConditionalAccessMfaRequired`                                          |
| Incidents          | `IncidentCreated`, `IncidentResolved`                                                               |
| Behavior           | `BehaviorAnomalyDetected`, `BehaviorProfileUpdated`                                                 |
| Impersonation      | `ImpersonationStarted`, `ImpersonationEnded`                                                        |
| Delegation         | `PermissionDelegated`, `DelegationRevoked`                                                          |
| Data Retention     | `DataRetentionExecuted`                                                                             |
| Bot Detection      | `BotDetected`, `CaptchaRequired`                                                                    |

---

## 3. Service Layer

### 3.1 SecurityEventService

> `src/IVF.Infrastructure/Services/SecurityEventService.cs`

**Vai trò:** Hub trung tâm ghi log sự kiện bảo mật + routing tới các service khác.

**Luồng xử lý khi `LogEventAsync()` được gọi:**

```
SecurityEventService.LogEventAsync(event)
    │
    ├── 1. Lưu event vào DB (SecurityEvents table)
    │
    ├── 2. Route tới IncidentResponseService
    │      └── ProcessEventAsync(event)
    │          (có AsyncLocal<bool> chống đệ quy)
    │
    └── 3. Nếu severity = High/Critical → route tới SecurityNotificationService
           └── NotifySecurityEventAsync(event)
               └── Gửi thông báo theo NotificationPreference
```

**Chống đệ quy:** Sử dụng `AsyncLocal<bool> _processingIncident` để tránh vòng lặp khi IncidentResponseService tạo event mới (event → process → log event → process...).

### 3.2 ConditionalAccessService

> `src/IVF.Infrastructure/Services/ConditionalAccessService.cs`

**Vai trò:** Đánh giá request context so với tập chính sách conditional access.

**Method chính:**

```csharp
Task<ConditionalAccessResult> EvaluateAsync(
    string? userRole,
    string? ipAddress,
    string? country,
    int riskLevel,
    AuthenticationLevel currentAuthLevel,
    string? deviceTrustLevel)
```

**Logic đánh giá:**

1. Load tất cả policies enabled, sắp xếp theo priority (ascending)
2. Với mỗi policy, kiểm tra theo thứ tự:
   - **Target Roles** — User role có nằm trong `TargetRoles`?
   - **Allowed Countries** — Country có trong danh sách được phép?
   - **Blocked Countries** — Country có bị chặn?
   - **Allowed IP Ranges** — IP có trong CIDR ranges?
   - **Time Windows** — Hiện tại có trong khung giờ?
   - **VPN/Tor** — Có sử dụng VPN/Tor? (từ threat assessment)
   - **Risk Level** — Risk level có vượt `MaxRiskLevel`?
   - **Device Trust** — Device trust level đạt yêu cầu?
3. Nếu match → trả về action
4. **Optimization:** Nếu action = `RequireMfa`/`RequireStepUp` và user đã có `currentAuthLevel >= MFA` → skip (không yêu cầu lại)

**Return:**

```csharp
record ConditionalAccessResult(bool IsAllowed, string? DeniedReason, string? RequiredAction);
```

### 3.3 BehavioralAnalyticsService

> `src/IVF.Infrastructure/Services/BehavioralAnalyticsService.cs`

**Vai trò:** Xây dựng profile hành vi từ lịch sử đăng nhập, phát hiện anomaly bằng z-score.

**Methods:**

| Method                                | Input                  | Output                | Mô tả                                                   |
| ------------------------------------- | ---------------------- | --------------------- | ------------------------------------------------------- |
| `BuildProfileAsync(userId)`           | `Guid`                 | `UserBehaviorProfile` | Phân tích 100 login gần nhất → tạo/cập nhật profile     |
| `DetectAnomalyAsync(userId, context)` | `Guid`, `LoginContext` | `AnomalyResult`       | So sánh login hiện tại với profile → tính anomaly score |

**Anomaly Detection (z-score):**

| Kiểm tra                 | Phương pháp                               | Điểm |
| ------------------------ | ----------------------------------------- | ---- |
| Giờ đăng nhập bất thường | z-score so với `TypicalLoginHours`        | 0-25 |
| IP mới lạ                | Không có trong `CommonIpAddresses`        | 0-25 |
| Quốc gia mới             | Không có trong `CommonCountries`          | 0-20 |
| Thiết bị mới             | Không có trong `CommonDeviceFingerprints` | 0-15 |
| Đăng nhập liên tục nhanh | Multiple logins trong thời gian ngắn      | 0-15 |

**Ngưỡng:** Score ≥ 25 → `IsAnomalous = true` → tạo `BehaviorAnomalyDetected` event.

### 3.4 IncidentResponseService

> `src/IVF.Infrastructure/Services/IncidentResponseService.cs`

**Vai trò:** Match events với rules → tạo incidents + thực thi automated actions.

**Luồng `ProcessEventAsync(event)`:**

```
1. Load tất cả rules enabled
2. Với mỗi rule:
   a. Kiểm tra event type có trong TriggerEventTypes?
   b. Kiểm tra severity có trong TriggerSeverities?
   c. Nếu match → tạo SecurityIncident
   d. Parse Actions JSON → thực thi tuần tự:
      ┌─ "lock_account"            → User.LockoutEnd = 60 phút
      ├─ "revoke_sessions"         → Xóa tất cả UserSession
      ├─ "block_ip"                → Log SecurityEvent(IpBlocked)
      ├─ "notify_admin"            → SecurityNotificationService.SendAdminAlertAsync()
      └─ "require_password_change" → Log SecurityEvent(PasswordChangeRequired)
```

### 3.5 SecurityNotificationService

> `src/IVF.Infrastructure/Services/SecurityNotificationService.cs`

**Vai trò:** Dispatch thông báo bảo mật qua nhiều kênh (in_app, email, sms).

**Methods:**

| Method                            | Mô tả                                                            |
| --------------------------------- | ---------------------------------------------------------------- |
| `NotifySecurityEventAsync(event)` | Gửi thông báo cho user sở hữu event, theo NotificationPreference |
| `SendAdminAlertAsync(incident)`   | Gửi in-app notification cho tất cả Admin users                   |

**Logic routing:**

1. Tìm `NotificationPreference` của user theo event type
2. Với mỗi preference enabled:
   - `in_app` → `INotificationService.SendNotificationAsync()` (SignalR push)
   - `email` → Log (placeholder cho email service)
   - `sms` → Log (placeholder cho SMS service)
3. Nếu không có preference → gửi mặc định in-app cho High/Critical events

**NotificationType:** `SecurityAlert` (enum value trong `Notification.cs`)

### 3.6 DataRetentionService

> `src/IVF.Infrastructure/Services/DataRetentionService.cs`

**Vai trò:** `IHostedService` chạy nền, tự động dọn dẹp dữ liệu cũ theo policy.

**Schedule:** Chạy mỗi ngày lúc 2:00 AM UTC.

**Logic purge cho mỗi policy enabled:**

```
1. Tính cutoff = DateTime.UtcNow - RetentionDays
2. Thực thi theo EntityType:
   ┌─ SecurityEvent      → DELETE FROM SecurityEvents WHERE CreatedAt < cutoff
   ├─ UserLoginHistory   → action-dependent (Delete/Anonymize)
   ├─ UserSession        → DELETE FROM UserSessions WHERE CreatedAt < cutoff
   └─ AuditLog           → DELETE FROM AuditLogs WHERE CreatedAt < cutoff
3. Cập nhật policy.LastExecutedAt + LastPurgedCount
4. Log SecurityEvent(DataRetentionExecuted)
```

### 3.7 Các service phụ trợ

| Service                   | Vai trò                                                                      |
| ------------------------- | ---------------------------------------------------------------------------- |
| `BotDetectionService`     | Phát hiện bot dựa trên user agent, tần suất request, behavior patterns       |
| `GeoLocationService`      | Xác định quốc gia từ IP address (sử dụng cho conditional access geo-fencing) |
| `SecretsScanner`          | Quét request body/headers tìm secrets bị lộ (API keys, tokens, passwords)    |
| `BreachedPasswordService` | Kiểm tra mật khẩu có trong danh sách breached (Have I Been Pwned style)      |

---

## 4. API Endpoints

> `src/IVF.API/Endpoints/EnterpriseSecurityEndpoints.cs`  
> **Base route:** `/api/security/enterprise`  
> **Authorization:** `RequireAuthorization("AdminOnly")` — chỉ Admin truy cập

### 4.1 Conditional Access (7 routes)

| Method   | Route                              | Mô tả                                           |
| -------- | ---------------------------------- | ----------------------------------------------- |
| `GET`    | `/conditional-access`              | Liệt kê tất cả policies (sắp xếp theo priority) |
| `GET`    | `/conditional-access/{id}`         | Chi tiết 1 policy                               |
| `POST`   | `/conditional-access`              | Tạo policy mới                                  |
| `PUT`    | `/conditional-access/{id}`         | Cập nhật policy                                 |
| `POST`   | `/conditional-access/{id}/enable`  | Bật policy                                      |
| `POST`   | `/conditional-access/{id}/disable` | Tắt policy                                      |
| `DELETE` | `/conditional-access/{id}`         | Xóa policy                                      |

### 4.2 Incident Rules (4 routes)

| Method   | Route                  | Mô tả                |
| -------- | ---------------------- | -------------------- |
| `GET`    | `/incident-rules`      | Liệt kê tất cả rules |
| `POST`   | `/incident-rules`      | Tạo rule mới         |
| `PUT`    | `/incident-rules/{id}` | Cập nhật rule        |
| `DELETE` | `/incident-rules/{id}` | Xóa rule             |

### 4.3 Security Incidents (6 routes)

| Method | Route                                          | Mô tả                               |
| ------ | ---------------------------------------------- | ----------------------------------- |
| `GET`  | `/incidents?page=&pageSize=&status=&severity=` | Liệt kê incidents (phân trang, lọc) |
| `GET`  | `/incidents/{id}`                              | Chi tiết incident                   |
| `POST` | `/incidents/{id}/investigate`                  | Chuyển sang `Investigating`         |
| `POST` | `/incidents/{id}/resolve`                      | Giải quyết (body: `{ resolution }`) |
| `POST` | `/incidents/{id}/close`                        | Đóng incident                       |
| `POST` | `/incidents/{id}/false-positive`               | Đánh dấu false positive             |

### 4.4 Data Retention (4 routes)

| Method   | Route                  | Mô tả                   |
| -------- | ---------------------- | ----------------------- |
| `GET`    | `/data-retention`      | Liệt kê tất cả policies |
| `POST`   | `/data-retention`      | Tạo policy mới          |
| `PUT`    | `/data-retention/{id}` | Cập nhật policy         |
| `DELETE` | `/data-retention/{id}` | Xóa policy              |

### 4.5 Impersonation (5 routes)

| Method | Route                                    | Mô tả                                         |
| ------ | ---------------------------------------- | --------------------------------------------- |
| `GET`  | `/impersonation?page=&pageSize=&status=` | Liệt kê requests (phân trang, lọc)            |
| `POST` | `/impersonation`                         | Tạo yêu cầu mạo danh                          |
| `POST` | `/impersonation/{id}/approve`            | Duyệt + tạo JWT (body: `{ durationMinutes }`) |
| `POST` | `/impersonation/{id}/deny`               | Từ chối (body: `{ reason }`)                  |
| `POST` | `/impersonation/{id}/end`                | Kết thúc phiên mạo danh                       |

**Response khi approve:**

```json
{
  "message": "Đã duyệt yêu cầu mạo danh",
  "token": "<JWT with impersonation claims>",
  "expiresAt": "2026-03-01T10:30:00Z"
}
```

### 4.6 Permission Delegation (3 routes)

| Method | Route                      | Mô tả                              |
| ------ | -------------------------- | ---------------------------------- |
| `GET`  | `/delegations`             | Liệt kê delegations đang hoạt động |
| `POST` | `/delegations`             | Tạo delegation mới                 |
| `POST` | `/delegations/{id}/revoke` | Thu hồi delegation                 |

### 4.7 Behavioral Analytics (3 routes)

| Method | Route                                 | Mô tả                            |
| ------ | ------------------------------------- | -------------------------------- |
| `GET`  | `/behavior-profiles`                  | Top 100 profiles theo tổng login |
| `GET`  | `/behavior-profiles/{userId}`         | Profile của 1 user               |
| `POST` | `/behavior-profiles/{userId}/refresh` | Rebuild profile từ login history |

### 4.8 Notification Preferences (3 routes)

| Method   | Route                                | Mô tả                                         |
| -------- | ------------------------------------ | --------------------------------------------- |
| `GET`    | `/notification-preferences/{userId}` | Preferences của user                          |
| `POST`   | `/notification-preferences`          | Tạo preference (check duplicate user+channel) |
| `DELETE` | `/notification-preferences/{id}`     | Xóa preference                                |

### Request DTOs

```csharp
// Conditional Access
record CreateConditionalAccessRequest(
    string Name, string? Description, int Priority, int MaxRiskLevel,
    bool RequireMfa, bool RequireCompliantDevice, bool BlockVpnTor,
    string Action, List<string>? TargetRoles, List<string>? AllowedCountries,
    List<string>? BlockedCountries, List<string>? AllowedIpRanges,
    List<object>? AllowedTimeWindows);

// Incident Rules
record CreateIncidentRuleRequest(
    string Name, string? Description, int Priority,
    List<string> TriggerEventTypes, List<string> TriggerSeverities,
    int? TriggerThreshold, int? TriggerWindowMinutes,
    List<string> Actions, string IncidentSeverity);

// Data Retention
record CreateDataRetentionRequest(
    string EntityType, int RetentionDays, string Action, string? Description);

// Impersonation
record CreateImpersonationRequest(Guid TargetUserId, string Reason, int DurationHours = 1);
record ApproveImpersonationRequest(int DurationMinutes = 30);
record DenyImpersonationRequest(string? Reason);

// Permission Delegation
record CreateDelegationRequest(
    Guid ToUserId, List<string> Permissions,
    DateTime? ValidFrom, DateTime ValidUntil, string? Reason);

// Notification Preferences
record CreateNotificationPrefRequest(Guid UserId, string Channel, List<string> EventTypes);
```

---

## 5. Middleware Integration

### 5.1 ZeroTrustMiddleware

> `src/IVF.API/Middleware/ZeroTrustMiddleware.cs`

Middleware continuous verification — chạy trên MỌI request authenticated.

**Pipeline:**

```
Request vào
    │
    ├── 1. Skip exempt paths (login, health, swagger)
    ├── 2. Skip unauthenticated requests
    │
    ├── 3. Build RequestSecurityContext
    │      └── IP, User Agent, Device Fingerprint, Auth Level
    │
    ├── 4. Threat Assessment
    │      ├── Store: HttpContext.Items["ZT_Assessment"]
    │      └── Store: HttpContext.Items["ZT_SecurityContext"]
    │
    ├── 5. Add Security Headers
    │      └── X-Risk-Level, X-Correlation-ID, CSP, HSTS
    │
    ├── 6. Block if Critical Risk → 403 + ZtAccessDenied event
    │
    ├── 7. Conditional Access Evaluation
    │      ├── Extract user role from claims
    │      ├── Get device trust from X-Device-Fingerprint
    │      ├── Call ConditionalAccessService.EvaluateAsync()
    │      ├── Block → 403 + ConditionalAccessBlocked event
    │      ├── RequireMfa → 401 + CA_REQUIRE_MFA code
    │      └── RequireStepUp → 401 + CA_REQUIRE_STEP_UP code
    │
    ├── 8. Impersonation Context
    │      ├── Check "impersonation" claim = "true"
    │      ├── Store: HttpContext.Items["IsImpersonation"] = true
    │      └── Store: HttpContext.Items["ImpersonationActorId"] = act_sub
    │
    ├── 9. Session Validation
    │      └── IAdaptiveSessionService: detect hijacking
    │
    └── 10. Continue to next middleware ✓
```

### 5.2 Security Middleware Pipeline Order

```
RateLimiter
    → SecurityEnforcement
        → VaultToken
            → ApiKey
                → Authentication (JWT)
                    → Authorization
                        → ConsentEnforcement
                            → TokenBinding
                                → ZeroTrust ← Enterprise Security tích hợp ở đây
                                    → Application endpoints
```

### 5.3 Permission Delegation trong Authorization

> `src/IVF.Infrastructure/Repositories/UserPermissionRepository.cs`

```
HasPermissionAsync(userId, permissionCode)
    │
    ├── 1. Check direct permissions (UserPermission table)
    │      └── Nếu tìm thấy → return true
    │
    └── 2. Check delegated permissions
           ├── Load PermissionDelegation cho userId
           ├── Filter: IsRevoked = false
           ├── Filter: ValidFrom ≤ now ≤ ValidUntil
           ├── Deserialize JSON permissions
           └── Nếu permissionCode tồn tại → return true
```

### 5.4 Auth Endpoints Integration

> `src/IVF.API/Endpoints/AuthEndpoints.cs`

**`GET /api/auth/me`** — Bổ sung thông tin mạo danh:

```json
{
  "id": "...",
  "username": "...",
  "role": "...",
  "isImpersonation": true,
  "actorUserId": "admin-uuid-here"
}
```

**`GET /api/auth/me/permissions`** — Bao gồm delegated permissions:

```json
{
  "permissions": ["patients:read", "cycles:write", "delegated:billing:read"]
}
```

---

## 6. Frontend

### 6.1 Component

> `ivf-client/src/app/features/admin/enterprise-security/enterprise-security.component.ts`

**Kiến trúc:** Standalone component, signal-based state, 8 tab UI, Vietnamese hoàn toàn.

**8 Tabs:**

| Tab | Tên tiếng Việt        | Chức năng                                                          |
| --- | --------------------- | ------------------------------------------------------------------ |
| 1   | Truy cập có điều kiện | CRUD conditional access policies, bật/tắt, ưu tiên                 |
| 2   | Quy tắc phản hồi      | CRUD incident rules, event types, severity, actions                |
| 3   | Sự cố bảo mật         | Danh sách incidents, investigate, resolve, close, false positive   |
| 4   | Lưu trữ dữ liệu       | CRUD data retention policies, entity types, actions                |
| 5   | Mạo danh              | Tạo/duyệt/từ chối/kết thúc impersonation, autocomplete user search |
| 6   | Ủy quyền              | Tạo/thu hồi delegations, multi-select permissions, user search     |
| 7   | Phân tích hành vi     | Xem behavior profiles, refresh profile                             |
| 8   | Thông báo             | CRUD notification preferences, channel + event types               |

**Tính năng nổi bật:**

- **User autocomplete:** Tìm kiếm user theo username với debounce 300ms
- **Permission multi-select:** Dropdown chọn nhiều permission codes
- **Real-time update:** Sau mỗi CRUD operation → reload danh sách
- **Form validation:** Required fields, date range validation
- **Error handling:** Try/catch + alert() cho mỗi API call

### 6.2 Service

> `ivf-client/src/app/core/services/enterprise-security.service.ts`

**Base URL:** `${environment.apiUrl}/security/enterprise`

**Injectable service** với Http client methods cho tất cả 35 API routes:

| Nhóm               | Methods                                                                                                                                                                                                                                            |
| ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Conditional Access | `getConditionalAccessPolicies()`, `getConditionalAccessPolicy(id)`, `createConditionalAccessPolicy()`, `updateConditionalAccessPolicy()`, `enableConditionalAccessPolicy()`, `disableConditionalAccessPolicy()`, `deleteConditionalAccessPolicy()` |
| Incident Rules     | `getIncidentRules()`, `createIncidentRule()`, `updateIncidentRule()`, `deleteIncidentRule()`                                                                                                                                                       |
| Incidents          | `getIncidents(page?, pageSize?, status?, severity?)`, `getIncident(id)`, `investigateIncident()`, `resolveIncident()`, `closeIncident()`, `markIncidentFalsePositive()`                                                                            |
| Data Retention     | `getDataRetentionPolicies()`, `createDataRetentionPolicy()`, `updateDataRetentionPolicy()`, `deleteDataRetentionPolicy()`                                                                                                                          |
| Impersonation      | `getImpersonationRequests()`, `createImpersonationRequest()`, `approveImpersonation()`, `denyImpersonation()`, `endImpersonation()`                                                                                                                |
| Delegation         | `getActiveDelegations()`, `createDelegation()`, `revokeDelegation()`                                                                                                                                                                               |
| Behavioral         | `getBehaviorProfiles()`, `getBehaviorProfile(userId)`, `refreshBehaviorProfile(userId)`                                                                                                                                                            |
| Notifications      | `getNotificationPreferences(userId)`, `createNotificationPreference()`, `deleteNotificationPreference()`                                                                                                                                           |

### 6.3 Models

> `ivf-client/src/app/core/models/enterprise-security.model.ts`

TypeScript interfaces matching backend DTOs:

```typescript
interface ConditionalAccessPolicy { id, name, description?, isEnabled, priority, targetRoles?, ... }
interface CreateConditionalAccessRequest { name, description?, priority, maxRiskLevel, ... }
interface IncidentResponseRule { id, name, description?, isEnabled, priority, triggerEventTypes?, ... }
interface SecurityIncident { id, incidentType, severity, status, userId?, ... }
interface DataRetentionPolicy { id, entityType, retentionDays, action, isEnabled, ... }
interface ImpersonationRequest { id, requestedBy, targetUserId, reason, status, ... }
interface PermissionDelegation { id, fromUserId, toUserId, permissions, validFrom, validUntil, ... }
interface UserBehaviorProfile { id, userId, typicalLoginHours?, commonIpAddresses?, ... }
interface NotificationPreference { id, userId, channel, eventTypes, isEnabled, ... }
interface PagedResult<T> { items: T[], totalCount, page, pageSize }
```

### 6.4 Routing & Menu

**Route:** `/admin/enterprise-security` (lazy-loaded trong `app.routes.ts`)

**Menu:** Seeded vào database qua `MenuSeeder.cs`:

- Tên: `🏢 Bảo mật doanh nghiệp`
- Icon: `shield`
- Sort order: 110
- Section: Admin
- Route: `/admin/enterprise-security`

**Fallback sidebar:** `main-layout.component.ts` — `🏢 Bảo mật DN`

---

## 7. Luồng nghiệp vụ

### 7.1 Luồng Incident Response tự động

```
User đăng nhập thất bại 5 lần
    │
    ▼
AuthEndpoints: Log SecurityEvent(LoginFailed, severity=High)
    │
    ▼
SecurityEventService.LogEventAsync()
    ├── Lưu event vào DB
    │
    ├── Route → IncidentResponseService.ProcessEventAsync()
    │     │
    │     ├── Match rule "Brute Force Auto-Lock"
    │     │     triggerEventTypes: [LoginFailed, BruteForceDetected]
    │     │     triggerSeverities: [High, Critical]
    │     │
    │     ├── Tạo SecurityIncident(severity=High, status=Open)
    │     │
    │     └── Execute actions:
    │           ├── lock_account → User.LockoutEnd = +60 min
    │           └── notify_admin → SecurityNotificationService
    │                                 └── SendAdminAlertAsync()
    │                                       └── In-app notification to all Admins
    │
    └── Route → SecurityNotificationService.NotifySecurityEventAsync()
          └── Gửi thông báo cho user (nếu có preference)
```

### 7.2 Luồng Conditional Access

```
Request HTTP vào hệ thống
    │
    ▼
ZeroTrustMiddleware
    │
    ├── Build SecurityContext (IP, role, device, risk)
    │
    ├── Threat Assessment → risk score
    │
    ├── ConditionalAccessService.EvaluateAsync()
    │     │
    │     ├── Policy "Block Critical Risk" (priority 10)
    │     │     maxRiskLevel: 70
    │     │     → Nếu risk ≥ 70 → Block → 403
    │     │
    │     ├── Policy "Block VPN/Tor" (priority 30)
    │     │     blockVpnTor: true
    │     │     → Nếu detected VPN/Tor → Block → 403
    │     │
    │     └── Policy "Geography Restriction" (priority 50)
    │           allowedCountries: ["VN"]
    │           → Nếu country ≠ VN → RequireMfa → 401
    │
    └── Nếu tất cả pass → Continue to endpoint ✓
```

### 7.3 Luồng Impersonation

```
Admin A muốn troubleshoot vấn đề cho User B
    │
    ▼
1. POST /api/security/enterprise/impersonation
   { targetUserId: "user-b-id", reason: "Debug billing issue" }
   → Tạo ImpersonationRequest(status=Pending)
    │
    ▼
2. Admin C (khác Admin A) duyệt:
   POST /impersonation/{id}/approve { durationMinutes: 30 }
   → Approve + Generate JWT:
     {
       sub: "user-b-id",
       act_sub: "admin-a-id",
       act_approved_by: "admin-c-id",
       impersonation: "true",
       exp: +30 min
     }
   → Return token + expiresAt
    │
    ▼
3. Admin A sử dụng JWT impersonation
   → ZeroTrustMiddleware detect impersonation claim
   → Set HttpContext.Items["IsImpersonation"] = true
   → Set HttpContext.Items["ImpersonationActorId"] = admin-a-id
   → /api/auth/me trả về isImpersonation: true
    │
    ▼
4. Kết thúc:
   POST /impersonation/{id}/end
   → Log ImpersonationEnded event
```

### 7.4 Luồng Permission Delegation

```
Doctor ủy quyền cho Nurse
    │
    ▼
1. POST /api/security/enterprise/delegations
   {
     toUserId: "nurse-id",
     permissions: ["patients:read", "cycles:read"],
     validFrom: "2026-03-01",
     validUntil: "2026-03-15",
     reason: "Covering during conference"
   }
    │
    ▼
2. Nurse đăng nhập → gọi API cần permission "patients:read"
    │
    ▼
3. UserPermissionRepository.HasPermissionAsync("nurse-id", "patients:read")
   ├── Check direct permissions → không có
   └── Check delegations → tìm thấy delegation active
       ├── IsRevoked = false ✓
       ├── ValidFrom ≤ now ≤ ValidUntil ✓
       └── permissions.Contains("patients:read") ✓
       → return true
    │
    ▼
4. Nurse truy cập được resource ✓
```

### 7.5 Luồng Behavioral Anomaly

```
User đăng nhập
    │
    ▼
Login flow → BehavioralAnalyticsService.DetectAnomalyAsync()
    │
    ├── Load UserBehaviorProfile
    │
    ├── So sánh với profile:
    │     ├── Giờ 3:00 AM → z-score cao → +20 điểm
    │     ├── IP mới (chưa từng thấy) → +15 điểm
    │     └── Device fingerprint mới → +10 điểm
    │
    ├── Total score: 45 ≥ 25 (threshold)
    │     → IsAnomalous = true
    │
    └── Log SecurityEvent(BehaviorAnomalyDetected, severity=Medium)
          │
          └── IncidentResponseService
                ├── Match rule "Behavior Anomaly Alert"
                └── Execute: notify_admin
```

---

## 8. Seed Data

> `src/IVF.Infrastructure/Persistence/EnterpriseSecuritySeeder.cs`  
> Chạy bởi `DatabaseSeeder.SeedAsync()` khi khởi động ở môi trường Development.

### 8.1 Conditional Access Policies (4)

| #   | Tên                        | Priority | Action       | Điều kiện                | Trạng thái                                        |
| --- | -------------------------- | -------- | ------------ | ------------------------ | ------------------------------------------------- |
| 1   | Block Critical Risk Logins | 10       | `Block`      | maxRiskLevel ≥ 70        | **Enabled**                                       |
| 2   | MFA for Admin Roles        | 20       | `RequireMfa` | targetRoles: ["Admin"]   | **Disabled** (bật sau khi cấu hình MFA cho Admin) |
| 3   | Block VPN/Tor Access       | 30       | `Block`      | blockVpnTor: true        | **Enabled**                                       |
| 4   | Geography Restriction      | 50       | `RequireMfa` | allowedCountries: ["VN"] | **Disabled** (bật sau khi cấu hình GeoIP)         |

### 8.2 Incident Response Rules (4)

| #   | Tên                          | Priority | Triggers                                         | Actions                                                | Severity |
| --- | ---------------------------- | -------- | ------------------------------------------------ | ------------------------------------------------------ | -------- |
| 1   | Brute Force Auto-Lock        | 10       | LoginFailed, BruteForceDetected / High, Critical | lock_account, notify_admin                             | High     |
| 2   | Credential Stuffing Response | 20       | CredentialStuffingDetected / Critical            | block_ip, lock_account, notify_admin                   | Critical |
| 3   | Account Takeover Prevention  | 15       | AccountTakeoverDetected / Critical               | revoke_sessions, require_password_change, notify_admin | Critical |
| 4   | Behavior Anomaly Alert       | 50       | BehaviorAnomalyDetected / Medium, High           | notify_admin                                           | Medium   |

### 8.3 Data Retention Policies (4)

| Entity           | Số ngày     | Action      | Compliance               |
| ---------------- | ----------- | ----------- | ------------------------ |
| SecurityEvent    | 365         | `Delete`    | GDPR minimization        |
| UserLoginHistory | 180         | `Anonymize` | GDPR right to erasure    |
| UserSession      | 90          | `Delete`    | Session hygiene          |
| AuditLog         | 730 (2 năm) | `Archive`   | HIPAA 7-year requirement |

---

## 9. Cấu hình & DI

### 9.1 Dependency Injection

> `src/IVF.API/Program.cs` (lines 314-330)

```csharp
// ─── Enterprise Security Services ───
builder.Services.AddScoped<ISecurityEventService, SecurityEventService>();
builder.Services.AddScoped<IThreatDetectionService, ThreatDetectionService>();
builder.Services.AddScoped<IDeviceFingerprintService, DeviceFingerprintService>();
builder.Services.AddScoped<IAdaptiveSessionService, AdaptiveSessionService>();
builder.Services.AddScoped<IStepUpAuthService, StepUpAuthService>();
builder.Services.AddScoped<IContextualAuthService, ContextualAuthService>();
builder.Services.AddScoped<IConditionalAccessService, ConditionalAccessService>();
builder.Services.AddScoped<IBehavioralAnalyticsService, BehavioralAnalyticsService>();
builder.Services.AddScoped<IIncidentResponseService, IncidentResponseService>();
builder.Services.AddScoped<IBotDetectionService, BotDetectionService>();
builder.Services.AddSingleton<ISecretsScanner, SecretsScanner>();
builder.Services.AddScoped<IBreachedPasswordService, BreachedPasswordService>();
builder.Services.AddScoped<IGeoLocationService, GeoLocationService>();
builder.Services.AddScoped<ISecurityNotificationService, SecurityNotificationService>();
builder.Services.AddHostedService<DataRetentionService>(); // Background service
```

**Lifetime:**
| Lifetime | Services | Lý do |
|----------|----------|-------|
| Scoped | Hầu hết services | Cần DbContext (scoped) |
| Singleton | `SecretsScanner` | Stateless, không cần DB |
| Hosted | `DataRetentionService` | Timer-based background task |

### 9.2 Middleware Registration

```csharp
// Program.cs middleware pipeline
app.UseRateLimiter();
app.UseMiddleware<SecurityEnforcementMiddleware>();
app.UseMiddleware<VaultTokenMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ConsentEnforcementMiddleware>();
app.UseMiddleware<TokenBindingMiddleware>();
app.UseMiddleware<ZeroTrustMiddleware>();  // ← Enterprise Security ở đây
```

### 9.3 Endpoint Registration

```csharp
// Program.cs
app.MapEnterpriseSecurityEndpoints();  // 35 routes under /api/security/enterprise
```

---

## 10. Hướng dẫn mở rộng

### 10.1 Thêm Conditional Access Action mới

1. Thêm action string vào `ConditionalAccessPolicy.Action` documentation
2. Xử lý trong `ConditionalAccessService.EvaluateAsync()`:

```csharp
// Thêm case xử lý action mới
if (policy.Action == "RequireDeviceCompliance")
{
    // Logic kiểm tra device compliance
}
```

3. Xử lý response trong `ZeroTrustMiddleware`:

```csharp
if (result.RequiredAction == "RequireDeviceCompliance")
{
    context.Response.StatusCode = 401;
    // Return error code cho frontend
}
```

### 10.2 Thêm Incident Response Action mới

1. Thêm action string vào seed data hoặc UI
2. Xử lý trong `IncidentResponseService.ProcessEventAsync()`:

```csharp
case "force_mfa_enrollment":
    // Logic bắt buộc user đăng ký MFA
    await _dbContext.SaveChangesAsync(ct);
    break;
```

### 10.3 Thêm Entity Type cho Data Retention

1. Thêm entity type string mới
2. Xử lý trong `DataRetentionService` purge logic:

```csharp
case "FormResponse":
    var responses = await _dbContext.FormResponses
        .Where(x => x.CreatedAt < cutoff)
        .ToListAsync(ct);
    _dbContext.FormResponses.RemoveRange(responses);
    break;
```

### 10.4 Tích hợp Email/SMS thực tế

Hiện tại `SecurityNotificationService` chỉ log cho channel `email` và `sms`. Để tích hợp thực:

```csharp
// Inject email/SMS services
case "email":
    await _emailService.SendAsync(user.Email, subject, message);
    break;
case "sms":
    await _smsService.SendAsync(user.PhoneNumber, message);
    break;
```

### 10.5 Thêm tab mới trong Frontend

1. Thêm interface trong `enterprise-security.model.ts`
2. Thêm HTTP methods trong `enterprise-security.service.ts`
3. Thêm tab trong `enterprise-security.component.ts`:
   - Thêm tab button trong template
   - Thêm content section với `@if (activeTab === 'new-tab')`
   - Thêm CRUD methods (load, create, update, delete)

---

## Tài liệu liên quan

- [Bảo mật nâng cao](advanced_security.md) — Passkeys, TOTP, SMS OTP, Rate Limiting, Geo-fencing, Threat Detection
- [Enterprise User Management](enterprise_user_management.md) — Sessions, Groups, IAM, Login Analytics, Consent
- [Digital Signing](digital_signing.md) — SignServer + EJBCA PKI infrastructure
- [Developer Guide](../developer_guide.md) — DB schema, API specs, RBAC matrix
