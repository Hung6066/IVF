# Enterprise User Management — Tài liệu kỹ thuật

> **Module Quản lý Người dùng Nâng cao** — Hệ thống quản lý user cấp enterprise, lấy cảm hứng từ Google Workspace, AWS IAM và Facebook Workplace.  
> Bao gồm: Session Management, Group-based IAM, Login Analytics, Risk Detection, GDPR/HIPAA Consent.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Domain Entities](#2-domain-entities)
3. [CQRS Commands & Handlers](#3-cqrs-commands--handlers)
4. [CQRS Queries & DTOs](#4-cqrs-queries--dtos)
5. [Repository Interface](#5-repository-interface)
6. [API Endpoints](#6-api-endpoints)
7. [Frontend](#7-frontend)
8. [Luồng nghiệp vụ](#8-luồng-nghiệp-vụ)
9. [Bảo mật & Compliance](#9-bảo-mật--compliance)
10. [Hướng dẫn mở rộng](#10-hướng-dẫn-mở-rộng)

---

## 1. Tổng quan kiến trúc

### Sơ đồ tổng thể

```
┌───────────────┐     ┌──────────────────┐     ┌──────────────────────────┐
│  Angular 21   │────▶│   .NET 10 API    │────▶│      PostgreSQL          │
│  (Frontend)   │     │  Minimal API     │     │                          │
│               │     │  MediatR CQRS    │     │  UserSessions            │
│  6 Tab UI     │     │  14 Commands     │     │  UserGroups              │
│  Signal-based │     │  7 Queries       │     │  UserGroupMembers        │
│               │     │  15+ Endpoints   │     │  UserGroupPermissions    │
└───────────────┘     └──────────────────┘     │  UserLoginHistories      │
                                               │  UserConsents            │
                                               └──────────────────────────┘
```

### Stack công nghệ

| Layer          | Công nghệ                   | Vai trò                               |
| -------------- | --------------------------- | ------------------------------------- |
| Frontend       | Angular 21, Signals, OnPush | UI quản lý 6 tab, modal, analytics    |
| API            | .NET 10, Minimal API        | 15+ endpoint, AdminOnly authorization |
| Application    | MediatR, CQRS               | 14 commands, 7 queries                |
| Infrastructure | EF Core, PostgreSQL         | Repository pattern, 65+ methods       |
| Domain         | DDD Entities                | 6 entity classes, value objects       |

### Cấu trúc file

```
src/
├── IVF.Domain/Entities/
│   ├── UserSession.cs              # Session tracking
│   ├── UserGroup.cs                # IAM groups
│   ├── UserGroupMember.cs          # Group membership
│   ├── UserGroupPermission.cs      # Group-level permissions
│   ├── UserLoginHistory.cs         # Login forensics
│   └── UserConsent.cs              # GDPR/HIPAA consent
│
├── IVF.Application/
│   ├── Common/Interfaces/
│   │   └── IEnterpriseUserRepository.cs    # 65+ repository methods
│   └── Features/Users/
│       ├── Commands/
│       │   ├── EnterpriseUserCommands.cs          # 14 command records
│       │   └── EnterpriseUserCommandHandlers.cs   # 14 handlers
│       └── Queries/
│           ├── EnterpriseUserQueries.cs            # 7 queries + 12 DTOs
│           └── EnterpriseUserQueryHandlers.cs      # 7 handlers
│
├── IVF.Infrastructure/Repositories/
│   └── EnterpriseUserRepository.cs  # EF Core implementation
│
└── IVF.API/Endpoints/
    └── EnterpriseUserEndpoints.cs   # 15+ Minimal API endpoints

ivf-client/src/app/
├── core/
│   ├── models/enterprise-user.model.ts     # TypeScript interfaces
│   └── services/enterprise-user.service.ts # 18 API methods
└── features/admin/enterprise-users/
    ├── enterprise-users.component.ts       # Signal-based Component
    ├── enterprise-users.component.html     # 6-tab UI (~470 lines)
    └── enterprise-users.component.scss     # Google-grade styling (~800 lines)
```

### Phân quyền

Tất cả endpoint enterprise user management yêu cầu **AdminOnly** authorization, ngoại trừ consent management chỉ yêu cầu authenticated user.

---

## 2. Domain Entities

### 2.1 UserSession

Quản lý session theo kiểu persistent — thay thế session in-memory bằng bản ghi durable trên DB.

| Property          | Type      | Mô tả                                  |
| ----------------- | --------- | -------------------------------------- |
| UserId            | Guid      | FK đến User                            |
| SessionToken      | string    | Token định danh session                |
| IpAddress         | string?   | Địa chỉ IP khi tạo session             |
| UserAgent         | string?   | User-Agent header                      |
| DeviceFingerprint | string?   | Fingerprint thiết bị                   |
| Country / City    | string?   | Vị trí địa lý (GeoIP)                  |
| DeviceType        | string?   | Desktop, Mobile, Tablet, API           |
| OperatingSystem   | string?   | Windows, macOS, Linux, Android, iOS    |
| Browser           | string?   | Chrome, Firefox, Safari, Edge          |
| StartedAt         | DateTime  | Thời gian bắt đầu session              |
| ExpiresAt         | DateTime  | Thời gian hết hạn                      |
| LastActivityAt    | DateTime  | Hoạt động gần nhất                     |
| IsRevoked         | bool      | Đã thu hồi?                            |
| RevokedReason     | string?   | Lý do thu hồi                          |
| RevokedAt         | DateTime? | Thời gian thu hồi                      |
| RevokedBy         | string?   | "system", "user", "admin", hoặc userId |

**Methods:**

- `Create(...)` — Factory method tạo session mới
- `IsActive()` — Kiểm tra session còn hoạt động: `!IsRevoked && !IsDeleted && ExpiresAt > UtcNow`
- `RecordActivity()` — Cập nhật `LastActivityAt`
- `Revoke(reason, revokedBy)` — Thu hồi session với lý do
- `Extend(newExpiresAt)` — Gia hạn session

### 2.2 UserGroup

Nhóm người dùng cho tổ chức phân cấp — tương tự Google Workspace Groups, AWS IAM Groups.

| Property      | Type    | Mô tả                                        |
| ------------- | ------- | -------------------------------------------- |
| Name          | string  | Tên nhóm (unique)                            |
| DisplayName   | string? | Tên hiển thị                                 |
| Description   | string? | Mô tả                                        |
| GroupType     | string  | `team`, `department`, `role-group`, `custom` |
| ParentGroupId | Guid?   | Nhóm cha (phân cấp)                          |
| IsSystem      | bool    | Nhóm hệ thống không thể xóa                  |
| IsActive      | bool    | Trạng thái hoạt động                         |
| Metadata      | string? | JSON mở rộng                                 |

**Methods:**

- `Create(...)` — Factory method
- `Update(name, displayName, description, groupType)` — Cập nhật thông tin
- `Activate()` / `Deactivate()` — Bật/tắt nhóm

### 2.3 UserGroupMember

Bảng join N-N giữa User và UserGroup — quản lý membership.

| Property   | Type     | Mô tả                      |
| ---------- | -------- | -------------------------- |
| UserId     | Guid     | FK đến User                |
| GroupId    | Guid     | FK đến UserGroup           |
| MemberRole | string   | `owner`, `admin`, `member` |
| AddedBy    | Guid?    | Người thêm vào nhóm        |
| JoinedAt   | DateTime | Thời gian tham gia         |

**Methods:**

- `Create(userId, groupId, memberRole, addedBy)` — Factory method
- `UpdateRole(memberRole)` — Thay đổi vai trò trong nhóm

### 2.4 UserGroupPermission

Permission được gán cho nhóm — tất cả thành viên kế thừa.

| Property       | Type     | Mô tả                              |
| -------------- | -------- | ---------------------------------- |
| GroupId        | Guid     | FK đến UserGroup                   |
| PermissionCode | string   | Code permission (VD: `users.view`) |
| GrantedBy      | Guid?    | Người cấp quyền                    |
| GrantedAt      | DateTime | Thời gian cấp                      |

### 2.5 UserLoginHistory

Theo dõi hoạt động đăng nhập — phục vụ forensics, compliance, phân tích rủi ro.

| Property          | Type      | Mô tả                                          |
| ----------------- | --------- | ---------------------------------------------- |
| UserId            | Guid      | FK đến User                                    |
| LoginMethod       | string    | `password`, `passkey`, `mfa`, `sso`, `api-key` |
| IsSuccess         | bool      | Đăng nhập thành công?                          |
| FailureReason     | string?   | Lý do thất bại                                 |
| IpAddress         | string?   | IP đăng nhập                                   |
| DeviceFingerprint | string?   | Fingerprint thiết bị                           |
| Country / City    | string?   | Vị trí GeoIP                                   |
| DeviceType        | string?   | Desktop, Mobile, Tablet, API                   |
| OperatingSystem   | string?   | OS người dùng                                  |
| Browser           | string?   | Trình duyệt                                    |
| RiskScore         | decimal?  | Điểm rủi ro (0-100)                            |
| IsSuspicious      | bool      | Đánh dấu đáng ngờ                              |
| RiskFactors       | string?   | JSON array các yếu tố rủi ro                   |
| SessionDuration   | TimeSpan? | Thời lượng session (tính khi logout)           |
| LoginAt           | DateTime  | Thời gian đăng nhập                            |
| LogoutAt          | DateTime? | Thời gian đăng xuất                            |

**Methods:**

- `Create(...)` — Factory method với đầy đủ thông tin forensic
- `RecordLogout()` — Ghi nhận logout, tính `SessionDuration`

### 2.6 UserConsent

Quản lý đồng thuận xử lý dữ liệu — tuân thủ GDPR/HIPAA.

| Property       | Type      | Mô tả                           |
| -------------- | --------- | ------------------------------- |
| UserId         | Guid      | FK đến User                     |
| ConsentType    | string    | Loại đồng thuận (xem bảng dưới) |
| IsGranted      | bool      | Đã đồng ý?                      |
| ConsentVersion | string?   | Phiên bản chính sách            |
| IpAddress      | string?   | IP khi đồng ý                   |
| UserAgent      | string?   | Trình duyệt khi đồng ý          |
| ConsentedAt    | DateTime  | Thời gian đồng ý                |
| RevokedAt      | DateTime? | Thời gian rút lại               |
| RevokedReason  | string?   | Lý do rút lại                   |
| ExpiresAt      | DateTime? | Hết hạn đồng thuận              |

**Consent Types (ConsentTypes class):**

| Hằng số           | Giá trị           | Ý nghĩa               |
| ----------------- | ----------------- | --------------------- |
| DataProcessing    | `data_processing` | Xử lý dữ liệu cá nhân |
| MedicalRecords    | `medical_records` | Truy cập hồ sơ y tế   |
| Marketing         | `marketing`       | Tiếp thị, quảng cáo   |
| Analytics         | `analytics`       | Thu thập phân tích    |
| Research          | `research`        | Nghiên cứu khoa học   |
| ThirdPartySharing | `third_party`     | Chia sẻ bên thứ ba    |
| BiometricData     | `biometric_data`  | Dữ liệu sinh trắc học |
| CookieConsent     | `cookies`         | Chấp nhận cookies     |

**Methods:**

- `Grant(...)` — Factory method cấp đồng thuận
- `Revoke(reason)` — Rút lại đồng thuận
- `IsValid()` — Kiểm tra còn hiệu lực: `IsGranted && !IsDeleted && (ExpiresAt == null || ExpiresAt > UtcNow)`

---

## 3. CQRS Commands & Handlers

### 3.1 Session Commands

| Command                        | Mô tả                           | Returns |
| ------------------------------ | ------------------------------- | ------- |
| `CreateUserSessionCommand`     | Tạo session mới                 | `Guid`  |
| `RevokeUserSessionCommand`     | Thu hồi 1 session               | —       |
| `RevokeAllUserSessionsCommand` | Thu hồi tất cả session của user | `int`   |

### 3.2 Group Commands

| Command                         | Mô tả                            | Returns |
| ------------------------------- | -------------------------------- | ------- |
| `CreateUserGroupCommand`        | Tạo nhóm mới                     | `Guid`  |
| `UpdateUserGroupCommand`        | Cập nhật thông tin nhóm          | —       |
| `DeleteUserGroupCommand`        | Soft-delete nhóm                 | —       |
| `AddGroupMemberCommand`         | Thêm thành viên vào nhóm         | `Guid`  |
| `RemoveGroupMemberCommand`      | Xóa thành viên khỏi nhóm         | —       |
| `UpdateGroupMemberRoleCommand`  | Cập nhật vai trò thành viên      | —       |
| `AssignGroupPermissionsCommand` | Gán quyền cho nhóm (replace all) | —       |

### 3.3 Login & Consent Commands

| Command                     | Mô tả              | Returns |
| --------------------------- | ------------------ | ------- |
| `RecordLoginHistoryCommand` | Ghi nhận đăng nhập | `Guid`  |
| `RecordLogoutCommand`       | Ghi nhận đăng xuất | —       |
| `GrantConsentCommand`       | Cấp đồng thuận     | `Guid`  |
| `RevokeConsentCommand`      | Rút lại đồng thuận | —       |

### Kiến trúc Handler

Tất cả handler sử dụng **primary constructor** pattern, inject `IEnterpriseUserRepository`:

```csharp
public class CreateUserSessionHandler(IEnterpriseUserRepository repo)
    : IRequestHandler<CreateUserSessionCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserSessionCommand request, CancellationToken ct)
    {
        var session = UserSession.Create(
            request.UserId, request.SessionToken, request.ExpiresAt,
            request.IpAddress, request.UserAgent, request.DeviceFingerprint,
            request.Country, request.City, request.DeviceType,
            request.OperatingSystem, request.Browser);

        await repo.AddSessionAsync(session);
        await repo.SaveChangesAsync(ct);
        return session.Id;
    }
}
```

---

## 4. CQRS Queries & DTOs

### 4.1 Queries

| Query                      | Mô tả                              | Returns                    |
| -------------------------- | ---------------------------------- | -------------------------- |
| `GetUserAnalyticsQuery`    | Dashboard analytics tổng hợp       | `UserAnalyticsDto`         |
| `GetUserDetailQuery`       | Chi tiết 1 user (sessions, groups) | `UserDetailDto`            |
| `GetUserSessionsQuery`     | Danh sách session của user         | `List<UserSessionDto>`     |
| `GetUserGroupsQuery`       | Danh sách nhóm (paged, search)     | `UserGroupListResponse`    |
| `GetUserGroupDetailQuery`  | Chi tiết nhóm + members + perms    | `UserGroupDetailDto`       |
| `GetUserLoginHistoryQuery` | Lịch sử đăng nhập (paged, filter)  | `LoginHistoryListResponse` |
| `GetUserConsentsQuery`     | Đồng thuận của user                | `List<UserConsentDto>`     |

### 4.2 DTOs chính

#### UserAnalyticsDto — Dashboard KPI

```typescript
{
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  mfaEnabledCount: number;
  passkeyCount: number;
  usersByRole: { [role: string]: number };
  totalLogins24h: number;
  failedLogins24h: number;
  suspiciousLogins24h: number;
  activeSessions: number;
  totalGroups: number;
  loginTrend7Days: LoginTrendDto[];
  mostActiveUsers: TopUserDto[];
  highRiskUsers: RiskUserDto[];
}
```

#### UserDetailDto — Chi tiết user

```typescript
{
  id: string;
  username: string;
  fullName: string;
  role: string;
  department: string;
  isActive: boolean;
  createdAt: Date;
  mfaEnabled: boolean;
  mfaMethod: string;
  passkeyCount: number;
  activeSessionCount: number;
  groups: string[];          // Tên các nhóm
  permissions: string[];     // Quyền trực tiếp
  groupPermissions: string[];// Quyền kế thừa từ nhóm
  loginSummary: {
    totalLogins: number;
    failedLogins: number;
    suspiciousLogins: number;
    lastLoginAt: Date;
    lastLoginIp: string;
    lastLoginCountry: string;
    lastFailedAt: Date;
    avgRiskScore: number;
  };
}
```

---

## 5. Repository Interface

### IEnterpriseUserRepository

Interface nằm ở `IVF.Application/Common/Interfaces/`, implementation ở `IVF.Infrastructure/Repositories/`.

**65+ methods, chia theo nhóm:**

#### Sessions (5 methods)

```csharp
Task AddSessionAsync(UserSession session);
Task<UserSession?> GetSessionByIdAsync(Guid id);
Task<List<UserSession>> GetUserSessionsAsync(Guid userId, bool activeOnly);
Task RevokeSessionAsync(Guid sessionId, string reason, string revokedBy);
Task<int> RevokeAllSessionsAsync(Guid userId, string reason, string revokedBy);
```

#### Groups (5 methods)

```csharp
Task AddGroupAsync(UserGroup group);
Task<UserGroup?> GetGroupByIdAsync(Guid id);
Task<(List<UserGroup>, int)> GetGroupsAsync(string? search, string? groupType, int page, int pageSize);
Task UpdateGroupAsync(UserGroup group);
Task DeleteGroupAsync(Guid id);
```

#### Group Members (5 methods)

```csharp
Task AddGroupMemberAsync(UserGroupMember member);
Task RemoveGroupMemberAsync(Guid groupId, Guid userId);
Task UpdateGroupMemberRoleAsync(Guid groupId, Guid userId, string role);
Task<List<UserGroupMember>> GetGroupMembersAsync(Guid groupId);
Task<UserGroupMember?> GetGroupMemberAsync(Guid groupId, Guid userId);
```

#### Group Permissions (3 methods)

```csharp
Task ReplaceGroupPermissionsAsync(Guid groupId, List<string> permissions, Guid? grantedBy);
Task<List<UserGroupPermission>> GetGroupPermissionsAsync(Guid groupId);
Task<List<string>> GetUserGroupPermissionsAsync(Guid userId);
```

#### Login History (3 methods)

```csharp
Task AddLoginHistoryAsync(UserLoginHistory entry);
Task<UserLoginHistory?> GetLoginHistoryByIdAsync(Guid id);
Task<(List<UserLoginHistory>, int)> GetLoginHistoryAsync(Guid? userId, int page, int pageSize, bool? isSuccess, bool? isSuspicious);
```

#### Consent (4 methods)

```csharp
Task AddConsentAsync(UserConsent consent);
Task<UserConsent?> GetConsentByIdAsync(Guid id);
Task<List<UserConsent>> GetUserConsentsAsync(Guid userId);
Task UpdateConsentAsync(UserConsent consent);
```

#### Analytics (13 methods)

```csharp
Task<int> GetTotalUsersAsync();
Task<int> GetActiveUsersAsync();
Task<int> GetInactiveUsersAsync();
Task<int> GetMfaEnabledCountAsync();
Task<int> GetPasskeyCountAsync();
Task<Dictionary<string, int>> GetUsersByRoleAsync();
Task<int> GetTotalLoginsLast24hAsync();
Task<int> GetFailedLoginsLast24hAsync();
Task<int> GetSuspiciousLoginsLast24hAsync();
Task<int> GetActiveSessionCountAsync();
Task<List<LoginTrendDto>> GetLoginTrend7DaysAsync();
Task<List<TopUserDto>> GetMostActiveUsersAsync(int count);
Task<List<RiskUserDto>> GetHighRiskUsersAsync(int count);
```

#### User Detail (8 methods)

```csharp
Task<dynamic?> GetUserBasicAsync(Guid userId);
Task<bool> IsUserMfaEnabledAsync(Guid userId);
Task<string?> GetUserMfaMethodAsync(Guid userId);
Task<int> GetUserPasskeyCountAsync(Guid userId);
Task<int> GetUserActiveSessionCountAsync(Guid userId);
Task<List<string>> GetUserGroupNamesAsync(Guid userId);
Task<List<string>> GetUserDirectPermissionsAsync(Guid userId);
Task<UserLoginSummaryDto> GetUserLoginSummaryAsync(Guid userId);
```

#### Persistence

```csharp
Task SaveChangesAsync(CancellationToken ct = default);
```

### DI Registration

```csharp
// IVF.Infrastructure/DependencyInjection.cs
services.AddScoped<IEnterpriseUserRepository, EnterpriseUserRepository>();
```

---

## 6. API Endpoints

### 6.1 User Analytics

| Method | Path                             | Mô tả                  | Auth      |
| ------ | -------------------------------- | ---------------------- | --------- |
| GET    | `/api/user-analytics/`           | Dashboard KPI tổng hợp | AdminOnly |
| GET    | `/api/user-analytics/users/{id}` | Chi tiết 1 user        | AdminOnly |

**Response GET `/api/user-analytics/`:**

```json
{
  "totalUsers": 45,
  "activeUsers": 38,
  "inactiveUsers": 7,
  "mfaEnabledCount": 12,
  "passkeyCount": 5,
  "usersByRole": { "Doctor": 8, "Nurse": 12, "Admin": 3 },
  "totalLogins24h": 156,
  "failedLogins24h": 8,
  "suspiciousLogins24h": 1,
  "activeSessions": 28,
  "totalGroups": 6,
  "loginTrend7Days": [
    {
      "date": "2026-03-01",
      "successCount": 45,
      "failedCount": 3,
      "suspiciousCount": 0
    }
  ],
  "mostActiveUsers": [
    {
      "userId": "...",
      "username": "dr.nguyen",
      "fullName": "BS. Nguyễn Văn A",
      "loginCount": 24,
      "lastLogin": "..."
    }
  ],
  "highRiskUsers": [
    {
      "userId": "...",
      "username": "nurse.tran",
      "fullName": "ĐD. Trần B",
      "avgRiskScore": 72.5,
      "suspiciousCount": 3
    }
  ]
}
```

### 6.2 Session Management

| Method | Path                                   | Mô tả                  | Auth      |
| ------ | -------------------------------------- | ---------------------- | --------- |
| GET    | `/api/user-sessions/{userId}`          | Sessions của user      | AdminOnly |
| DELETE | `/api/user-sessions/{sessionId}`       | Thu hồi 1 session      | AdminOnly |
| DELETE | `/api/user-sessions/user/{userId}/all` | Thu hồi tất cả session | AdminOnly |

**Query params:** `activeOnly` (bool, default true), `reason` (string, cho DELETE)

### 6.3 Group Management

| Method | Path                                               | Mô tả                  | Auth      |
| ------ | -------------------------------------------------- | ---------------------- | --------- |
| GET    | `/api/user-groups/`                                | Danh sách nhóm (paged) | AdminOnly |
| GET    | `/api/user-groups/{groupId}`                       | Chi tiết nhóm          | AdminOnly |
| POST   | `/api/user-groups/`                                | Tạo nhóm mới           | AdminOnly |
| PUT    | `/api/user-groups/{groupId}`                       | Cập nhật nhóm          | AdminOnly |
| DELETE | `/api/user-groups/{groupId}`                       | Xóa nhóm               | AdminOnly |
| POST   | `/api/user-groups/{groupId}/members`               | Thêm thành viên        | AdminOnly |
| DELETE | `/api/user-groups/{groupId}/members/{userId}`      | Xóa thành viên         | AdminOnly |
| PUT    | `/api/user-groups/{groupId}/members/{userId}/role` | Đổi vai trò            | AdminOnly |
| POST   | `/api/user-groups/{groupId}/permissions`           | Gán quyền cho nhóm     | AdminOnly |

**Query params cho GET danh sách:** `search` (string), `groupType` (string), `page` (int), `pageSize` (int)

**Request body tạo nhóm:**

```json
{
  "name": "team-embryology",
  "displayName": "Đội Phôi học",
  "description": "Nhân viên phòng phôi học",
  "groupType": "team",
  "parentGroupId": null
}
```

**Request body thêm thành viên:**

```json
{
  "userId": "guid-here",
  "memberRole": "member",
  "addedBy": "admin-guid"
}
```

**Request body gán quyền:**

```json
{
  "permissions": ["patients.view", "cycles.view", "lab.manage"],
  "grantedBy": "admin-guid"
}
```

### 6.4 Login History

| Method | Path                  | Mô tả                     | Auth      |
| ------ | --------------------- | ------------------------- | --------- |
| GET    | `/api/login-history/` | Lịch sử đăng nhập (paged) | AdminOnly |

**Query params:** `userId` (Guid?), `page`, `pageSize`, `isSuccess` (bool?), `isSuspicious` (bool?)

### 6.5 Consent Management

| Method | Path                             | Mô tả               | Auth          |
| ------ | -------------------------------- | ------------------- | ------------- |
| GET    | `/api/user-consents/{userId}`    | Đồng thuận của user | Authenticated |
| POST   | `/api/user-consents/`            | Cấp đồng thuận      | Authenticated |
| DELETE | `/api/user-consents/{consentId}` | Rút lại đồng thuận  | Authenticated |

**Request body cấp đồng thuận:**

```json
{
  "userId": "guid",
  "consentType": "medical_records",
  "consentVersion": "v2.1",
  "ipAddress": "192.168.1.100",
  "userAgent": "Mozilla/5.0...",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

---

## 7. Frontend

### 7.1 Cấu trúc Component

Component chính: `EnterpriseUsersComponent` — standalone, signal-based, OnPush change detection.

**Route:** `/admin/enterprise-users` (lazy-loaded)

```typescript
// app.routes.ts
{
  path: 'admin/enterprise-users',
  loadComponent: () => import('./features/admin/enterprise-users/enterprise-users.component')
    .then(m => m.EnterpriseUsersComponent)
}
```

### 7.2 Giao diện 6 Tab

| Tab | Tên           | Nội dung                                        |
| --- | ------------- | ----------------------------------------------- |
| 1   | Analytics     | KPI cards, biểu đồ trend 7 ngày, phân bổ role   |
| 2   | Users         | Bảng danh sách user, click → modal chi tiết     |
| 3   | Groups        | Quản lý nhóm CRUD, click → modal chi tiết nhóm  |
| 4   | Sessions      | Xem/thu hồi sessions (tìm theo user)            |
| 5   | Login History | Lịch sử đăng nhập, lọc theo status & suspicious |
| 6   | Consent       | Quản lý đồng thuận GDPR/HIPAA (tìm theo user)   |

### 7.3 Signal-based State

```typescript
// State signals
analytics = signal<UserAnalyticsDto | null>(null);
groups = signal<UserGroupDto[]>([]);
sessions = signal<UserSessionDto[]>([]);
loginHistory = signal<UserLoginHistoryDto[]>([]);
consents = signal<UserConsentDto[]>([]);
selectedUser = signal<UserDetailDto | null>(null);
selectedGroup = signal<UserGroupDetailDto | null>(null);
activeTab = signal<string>("analytics");
loading = signal(false);
```

### 7.4 Service (18 API methods)

```typescript
@Injectable({ providedIn: "root" })
export class EnterpriseUserService {
  // Analytics
  getAnalytics(): Observable<UserAnalyticsDto>;
  getUserDetail(userId: string): Observable<UserDetailDto>;

  // Sessions
  getUserSessions(
    userId: string,
    activeOnly?: boolean,
  ): Observable<UserSessionDto[]>;
  revokeSession(sessionId: string, reason?: string): Observable<void>;
  revokeAllSessions(
    userId: string,
    reason?: string,
  ): Observable<{ revokedCount: number }>;

  // Groups
  getGroups(params?): Observable<UserGroupListResponse>;
  getGroupDetail(groupId: string): Observable<UserGroupDetailDto>;
  createGroup(data): Observable<{ id: string }>;
  updateGroup(groupId: string, data): Observable<void>;
  deleteGroup(groupId: string): Observable<void>;
  addGroupMember(groupId: string, data): Observable<{ id: string }>;
  removeGroupMember(groupId: string, userId: string): Observable<void>;
  updateMemberRole(
    groupId: string,
    userId: string,
    role: string,
  ): Observable<void>;
  assignGroupPermissions(groupId: string, data): Observable<any>;

  // Login History
  getLoginHistory(params?): Observable<LoginHistoryListResponse>;

  // Consent
  getUserConsents(userId: string): Observable<UserConsentDto[]>;
  grantConsent(data): Observable<{ id: string }>;
  revokeConsent(consentId: string, reason?: string): Observable<void>;
}
```

### 7.5 Vietnamese Constants

Frontend sử dụng các map dịch sang tiếng Việt:

```typescript
export const GROUP_TYPES: Record<string, string> = {
  team: "Đội / nhóm",
  department: "Phòng ban",
  "role-group": "Nhóm vai trò",
  custom: "Tùy chỉnh",
};

export const CONSENT_TYPES: Record<string, string> = {
  data_processing: "Xử lý dữ liệu",
  medical_records: "Hồ sơ y tế",
  marketing: "Tiếp thị",
  analytics: "Phân tích",
  research: "Nghiên cứu",
  third_party: "Bên thứ ba",
  biometric_data: "Sinh trắc học",
  cookies: "Cookies",
};

export const LOGIN_METHOD_LABELS: Record<string, string> = {
  password: "Mật khẩu",
  passkey: "Passkey",
  mfa: "Xác thực 2 bước",
  sso: "SSO",
  "api-key": "API Key",
};

export const DEVICE_TYPE_ICONS: Record<string, string> = {
  Desktop: "🖥️",
  Mobile: "📱",
  Tablet: "📋",
  API: "🔌",
};

export const MEMBER_ROLES: Record<string, string> = {
  owner: "Chủ sở hữu",
  admin: "Quản trị",
  member: "Thành viên",
};
```

### 7.6 User Detail Modal

Modal chi tiết user có **5 sub-tab**:

| Sub-tab     | Nội dung                                            |
| ----------- | --------------------------------------------------- |
| overview    | Thông tin cơ bản, role, MFA, passkey, session count |
| groups      | Danh sách nhóm user tham gia                        |
| permissions | Quyền trực tiếp + quyền kế thừa từ nhóm             |
| sessions    | Sessions đang hoạt động, nút thu hồi                |
| logins      | Lịch sử đăng nhập gần đây, risk score               |

### 7.7 Group Detail Modal

Modal nhóm gồm:

- Thông tin nhóm (name, type, status)
- Danh sách thành viên + vai trò
- Danh sách permissions được gán
- Nút thêm/xóa thành viên, gán quyền

---

## 8. Luồng nghiệp vụ

### 8.1 Tạo Session khi đăng nhập

```
User đăng nhập thành công
  → AuthService.Login()
  → [Optional] CreateUserSessionCommand
  → UserSession.Create(...) với device info, GeoIP
  → Lưu vào DB
  → RecordLoginHistoryCommand (ghi log, risk score)
```

### 8.2 Group-based Permission (IAM)

```
Admin tạo group "team-embryology"
  → CreateUserGroupCommand

Admin thêm user vào group
  → AddGroupMemberCommand(groupId, userId, "member")

Admin gán permissions cho group
  → AssignGroupPermissionsCommand(groupId, ["lab.manage", "cycles.view"])

Hệ thống kiểm tra quyền user:
  1. Lấy direct permissions (user role → default perms)
  2. Lấy group permissions (user → groups → permissions)
  3. Merge tất cả → effective permissions
```

### 8.3 Risk Detection

```
User đăng nhập
  → Hệ thống tính RiskScore dựa trên:
    - IP mới (chưa thấy trước đây)
    - Quốc gia mới
    - DeviceType khác thường
    - Thời gian đăng nhập bất thường
    - Nhiều lần thất bại liên tiếp
  → RiskScore > threshold → IsSuspicious = true
  → Hiển thị cảnh báo trên Dashboard
  → Admin review trong tab Login History
```

### 8.4 GDPR Consent Flow

```
User truy cập hệ thống lần đầu
  → Hiển thị consent form (data_processing, medical_records)
  → User đồng ý → GrantConsentCommand
  → Lưu consent + IP + UserAgent + timestamp

User rút lại đồng thuận
  → RevokeConsentCommand
  → Đánh dấu RevokedAt, RevokedReason
  → Hệ thống hạn chế tính năng liên quan

Admin kiểm tra compliance
  → Tab Consent → tìm user → xem tất cả consent records
```

---

## 9. Bảo mật & Compliance

### Authorization

- Tất cả endpoint quản trị: **AdminOnly** policy
- Consent endpoint: **Authenticated** (user xem được consent của mình)
- Session token: Không trả về qua API (chỉ metadata)

### Audit Trail

- Mỗi entity kế thừa `BaseEntity` → tự động `CreatedAt`, `UpdatedAt`, `IsDeleted`
- `UserLoginHistory` ghi lại mọi lần đăng nhập/xuất
- `UserGroupPermission.GrantedBy` + `GrantedAt` → ai gán quyền, khi nào
- `UserGroupMember.AddedBy` + `JoinedAt` → ai thêm thành viên, khi nào
- `UserConsent.IpAddress` + `UserAgent` → bằng chứng đồng thuận pháp lý

### HIPAA Compliance

| Yêu cầu HIPAA                       | Giải pháp                            |
| ----------------------------------- | ------------------------------------ |
| Access Control (§164.312(a))        | Group-based IAM, AdminOnly endpoints |
| Audit Controls (§164.312(b))        | UserLoginHistory, full forensic data |
| Person Authentication (§164.312(d)) | Multi-method login tracking          |
| Integrity (§164.312(e))             | Session management, revocation       |
| Privacy Rule                        | UserConsent for medical_records      |

### GDPR Compliance

| Yêu cầu GDPR      | Giải pháp                              |
| ----------------- | -------------------------------------- |
| Consent (Art. 7)  | UserConsent entity, explicit GrantedAt |
| Right to Withdraw | RevokeConsentCommand                   |
| Record of Consent | IP, UserAgent, timestamp, version      |
| Data Minimization | Consent per purpose (8 types)          |
| Lawful Processing | Consent version tracking, expiry       |

---

## 10. Hướng dẫn mở rộng

### Thêm loại Consent mới

1. Thêm constant vào `ConsentTypes` class trong `UserConsent.cs`:

   ```csharp
   public const string NewType = "new_type";
   ```

2. Thêm label tiếng Việt vào `enterprise-user.model.ts`:
   ```typescript
   export const CONSENT_TYPES: Record<string, string> = {
     // ... existing
     new_type: "Loại mới",
   };
   ```

### Thêm GroupType mới

1. Thêm vào `UserGroup.Create()` validation (nếu có)
2. Cập nhật `GROUP_TYPES` trong `enterprise-user.model.ts`
3. Cập nhật filter dropdown trong component HTML

### Thêm LoginMethod mới

1. Sử dụng giá trị mới khi gọi `RecordLoginHistoryCommand`
2. Cập nhật `LOGIN_METHOD_LABELS` trong `enterprise-user.model.ts`

### Tích hợp với luồng Authentication hiện tại

Để tự động ghi session và login history:

```csharp
// Trong AuthEndpoints hoặc AuthService, sau khi login thành công:
await mediator.Send(new CreateUserSessionCommand(
    userId, jwtToken, expiresAt,
    httpContext.Connection.RemoteIpAddress?.ToString(),
    httpContext.Request.Headers.UserAgent,
    // ... device info from UA parsing
));

await mediator.Send(new RecordLoginHistoryCommand(
    userId, "password", true, null,
    httpContext.Connection.RemoteIpAddress?.ToString(),
    // ... full forensic data
));
```

### Migration cho Entity mới

Các entity đã được thêm vào `IvfDbContext` nhưng chưa có migration. Để tạo:

```bash
dotnet ef migrations add AddEnterpriseUserManagement \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API
```

> **Lưu ý:** Trong môi trường Development, auto-migration chạy khi startup nên không cần apply thủ công.

---

## Tài liệu liên quan

- [Advanced Security](advanced_security.md) — MFA, Passkeys, Rate Limiting, Geo-blocking
- [Zero Trust Architecture](zero_trust_architecture.md) — Network security, mTLS
- [Developer Guide](developer_guide.md) — Tổng quan dự án, coding conventions
- [Digital Signing](digital_signing.md) — PKI, ký số tài liệu
