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
│  Menu consent │     │  17+ Endpoints   │     │  UserGroupPermissions    │
└───────────────┘     └──────────────────┘     │  UserLoginHistories      │
                                               │  UserConsents            │
                                               └──────────────────────────┘
```

### Stack công nghệ

| Layer          | Công nghệ                   | Vai trò                               |
| -------------- | --------------------------- | ------------------------------------- |
| Frontend       | Angular 21, Signals, OnPush | UI quản lý 6 tab, modal, analytics    |
| API            | .NET 10, Minimal API        | 17+ endpoint, AdminOnly authorization |
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

`ConsentEnforcementMiddleware` chặn request đến endpoint nhạy cảm nếu user thiếu consent. Frontend tích hợp proactive consent checking trên menu items (🔒 lock icon).

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

| Command                      | Mô tả                        | Returns                    |
| ---------------------------- | ---------------------------- | -------------------------- |
| `RecordLoginHistoryCommand`  | Ghi nhận đăng nhập           | `Guid`                     |
| `RecordLogoutCommand`        | Ghi nhận đăng xuất           | —                          |
| `GrantConsentCommand`        | Cấp đồng thuận               | `Guid`                     |
| `RevokeConsentCommand`       | Rút lại đồng thuận           | —                          |
| `GrantGroupConsentCommand`   | Cấp đồng thuận cho toàn nhóm | `int`                      |
| `RevokeGroupConsentCommand`  | Thu hồi đồng thuận toàn nhóm | `int`                      |
| `GetGroupConsentStatusQuery` | Trạng thái consent toàn nhóm | `GroupConsentStatusResult` |

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

| Method | Path                                                | Mô tả                     | Auth      |
| ------ | --------------------------------------------------- | ------------------------- | --------- |
| GET    | `/api/user-groups/`                                 | Danh sách nhóm (paged)    | AdminOnly |
| GET    | `/api/user-groups/{groupId}`                        | Chi tiết nhóm             | AdminOnly |
| POST   | `/api/user-groups/`                                 | Tạo nhóm mới              | AdminOnly |
| PUT    | `/api/user-groups/{groupId}`                        | Cập nhật nhóm             | AdminOnly |
| DELETE | `/api/user-groups/{groupId}`                        | Xóa nhóm                  | AdminOnly |
| POST   | `/api/user-groups/{groupId}/members`                | Thêm thành viên           | AdminOnly |
| DELETE | `/api/user-groups/{groupId}/members/{userId}`       | Xóa thành viên            | AdminOnly |
| PUT    | `/api/user-groups/{groupId}/members/{userId}/role`  | Đổi vai trò               | AdminOnly |
| POST   | `/api/user-groups/{groupId}/permissions`            | Gán quyền cho nhóm        | AdminOnly |
| GET    | `/api/user-groups/{groupId}/consents`               | Trạng thái consent nhóm   | AdminOnly |
| POST   | `/api/user-groups/{groupId}/consents`               | Cấp consent toàn nhóm     | AdminOnly |
| DELETE | `/api/user-groups/{groupId}/consents/{consentType}` | Thu hồi consent toàn nhóm | AdminOnly |

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

**Request body cấp consent cho nhóm:**

```json
{
  "consentType": "data_processing",
  "consentVersion": "v2.1",
  "expiresAt": "2027-03-03T00:00:00Z"
}
```

**Response GET `/api/user-groups/{groupId}/consents`:**

```json
{
  "members": [
    {
      "userId": "...",
      "username": "dr.nguyen",
      "fullName": "BS. Nguyễn Văn A",
      "validConsents": ["data_processing", "medical_records"]
    },
    {
      "userId": "...",
      "username": "nurse.tran",
      "fullName": "ĐD. Trần B",
      "validConsents": ["data_processing"]
    }
  ],
  "consentSummary": {
    "data_processing": { "grantedCount": 2, "totalMembers": 2 },
    "medical_records": { "grantedCount": 1, "totalMembers": 2 },
    "marketing": { "grantedCount": 0, "totalMembers": 2 }
  }
}
```

### 6.4 Login History

| Method | Path                  | Mô tả                     | Auth      |
| ------ | --------------------- | ------------------------- | --------- |
| GET    | `/api/login-history/` | Lịch sử đăng nhập (paged) | AdminOnly |

**Query params:** `userId` (Guid?), `page`, `pageSize`, `isSuccess` (bool?), `isSuspicious` (bool?)

### 6.5 Consent Management

| Method | Path                             | Mô tả                                  | Auth          |
| ------ | -------------------------------- | -------------------------------------- | ------------- |
| GET    | `/api/user-consents/my-status`   | Consent status của user đang đăng nhập | Authenticated |
| GET    | `/api/user-consents/{userId}`    | Đồng thuận của user                    | Authenticated |
| POST   | `/api/user-consents/`            | Cấp đồng thuận                         | Authenticated |
| DELETE | `/api/user-consents/{consentId}` | Rút lại đồng thuận                     | Authenticated |

**Response GET `/api/user-consents/my-status`:**

Endpoint mới phục vụ frontend proactive consent checking. Trả về danh sách consent hợp lệ và thiếu cho user hiện tại (lấy userId từ JWT `ClaimTypes.NameIdentifier`).

```json
{
  "validConsents": ["data_processing", "medical_records"],
  "missingConsents": [
    "marketing",
    "analytics",
    "research",
    "third_party",
    "biometric_data",
    "cookies"
  ]
}
```

> **Route ordering:** `/my-status` đặt **trước** `/{userId}` để tránh xung đột parse Guid.

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

### 8.5 Consent Enforcement (Middleware + UI)

Hệ thống enforcement đồng ý dữ liệu gồm **2 lớp bảo vệ**:

#### A. Backend — ConsentEnforcementMiddleware

Middleware chặn request đến endpoint nhạy cảm nếu user chưa cấp đồng ý cần thiết.

```
Request → Auth → Authorization → ConsentEnforcement → TokenBinding → ZeroTrust
                                       ↓
                              Kiểm tra consent theo path mapping
                                       ↓
                         Thiếu consent → 403 + X-Consent-Required header
                         Đủ consent   → cho phép tiếp tục
```

**Path → Consent Type Mapping:**

| API Path                | Consent yêu cầu                      |
| ----------------------- | ------------------------------------ |
| `/api/patients`         | `data_processing`, `medical_records` |
| `/api/couples`          | `data_processing`, `medical_records` |
| `/api/treatment-cycles` | `data_processing`, `medical_records` |
| `/api/ultrasounds`      | `data_processing`, `medical_records` |
| `/api/lab`              | `data_processing`, `medical_records` |
| `/api/andrology`        | `data_processing`, `medical_records` |
| `/api/semen-analysis`   | `data_processing`, `medical_records` |
| `/api/embryos`          | `data_processing`, `medical_records` |
| `/api/sperm-bank`       | `data_processing`, `biometric_data`  |
| `/api/fingerprint`      | `biometric_data`                     |
| `/api/reports`          | `data_processing`, `analytics`       |
| `/api/forms`            | `data_processing`                    |

**Exempt Paths** (không kiểm tra consent):

| Prefix                  | Lý do                              |
| ----------------------- | ---------------------------------- |
| `/api/auth`             | Authentication flow                |
| `/api/menu`             | Menu loading                       |
| `/api/enterprise-users` | Enterprise user management         |
| `/api/user-consents`    | Consent management (CRUD + status) |
| `/api/notifications`    | Real-time notifications            |
| `/api/queue`            | Queue management                   |
| `/api/dashboard`        | Dashboard KPI                      |
| `/health`, `/healthz`   | Health checks                      |
| `/swagger`              | API documentation                  |
| `/hubs`                 | SignalR hubs                       |

**Response khi thiếu consent:**

```json
{
  "error": "Required data consent not granted",
  "code": "CONSENT_REQUIRED",
  "missingConsents": ["data_processing", "medical_records"],
  "message": "Vui lòng cấp đồng ý xử lý dữ liệu trước khi truy cập tài nguyên này."
}
```

**Files liên quan:**

- `IVF.Application/Common/Interfaces/IConsentValidationService.cs` — interface
- `IVF.Infrastructure/Services/ConsentValidationService.cs` — implementation
- `IVF.API/Middleware/ConsentEnforcementMiddleware.cs` — middleware

#### B. Frontend — Consent Warning Banner

Khi backend trả về 403 `CONSENT_REQUIRED`, frontend hiển thị banner cảnh báo:

```
HTTP 403 + code=CONSENT_REQUIRED
  → consentInterceptor bắt response
  → ConsentBannerService.showMissingConsents([...types])
  → Amber banner hiển thị giữa header và main content
  → Link "Quản lý đồng ý →" chuyển đến trang consent management
  → User có thể dismiss tạm thời (banner tái hiện khi 403 tiếp theo)
```

**Files liên quan:**

- `ivf-client/src/app/core/interceptors/consent.interceptor.ts` — HTTP interceptor
- `ivf-client/src/app/core/services/consent-banner.service.ts` — signal-based state
- `ivf-client/src/app/layout/main-layout/` — banner HTML + SCSS

#### C. Menu Consent Lock — Proactive Checking

Ngoài reactive enforcement (403), hệ thống còn **proactive consent checking** trên sidebar menu:

```
User đăng nhập
  → MainLayoutComponent.ngOnInit()
  → ConsentBannerService.loadConsentStatus()
  → GET /api/user-consents/my-status
  → Response: { validConsents: [...], missingConsents: [...] }
  → Mỗi menu item kiểm tra route → required consents → filter missing
  → Menu items thiếu consent:
      • Opacity giảm 50% (.consent-blocked)
      • Icon 🔒 hiện bên phải
      • Tooltip: "Thiếu đồng ý: Xử lý dữ liệu, Hồ sơ y tế"
```

**Route → Consent Mapping (Frontend):**

```typescript
// consent-banner.service.ts
const ROUTE_CONSENT_MAP: Record<string, string[]> = {
  "/patients": ["data_processing", "medical_records"],
  "/reception": ["data_processing", "medical_records"],
  "/couples": ["data_processing", "medical_records"],
  "/consultation": ["data_processing", "medical_records"],
  "/ultrasound": ["data_processing", "medical_records"],
  "/lab": ["data_processing", "medical_records"],
  "/andrology": ["data_processing", "medical_records"],
  "/injection": ["data_processing", "medical_records"],
  "/sperm-bank": ["data_processing", "biometric_data"],
  "/reports": ["data_processing", "analytics"],
  "/forms": ["data_processing"],
};
```

**Methods:**

| Method                      | Mô tả                                    |
| --------------------------- | ---------------------------------------- |
| `loadConsentStatus()`       | Gọi GET `/my-status`, cập nhật signals   |
| `isRouteBlocked(route)`     | Route có bị chặn bởi thiếu consent?      |
| `getMissingForRoute(route)` | Trả về danh sách consent thiếu cho route |
| `getLabel(type)`            | Chuyển key → label tiếng Việt            |

**MainLayoutComponent integration:**

```html
<!-- Menu item với consent lock -->
<a
  [routerLink]="item.route"
  [class.consent-blocked]="isMenuConsentBlocked(item)"
  [title]="getConsentTooltip(item)"
>
  <span class="icon">{{ item.icon }}</span> {{ item.label }} @if
  (isMenuConsentBlocked(item)) {
  <span class="consent-lock">🔒</span>
  }
</a>
```

**SCSS:**

```scss
.consent-blocked {
  opacity: 0.5;
  position: relative;
}
.consent-lock {
  margin-left: auto;
  font-size: 0.75rem;
  opacity: 0.8;
}
```

**Logout cleanup:**

```typescript
// auth.service.ts → logout()
this.consentBanner.clear(); // Reset consent state khi đăng xuất
```

> **Lưu ý:** `ROUTE_CONSENT_MAP` phải đồng bộ với `PathConsentMap` trong backend. Sử dụng frontend route (không có `/api/` prefix).

**Files liên quan:**

- `ivf-client/src/app/core/services/consent-banner.service.ts` — route mapping + signals
- `ivf-client/src/app/layout/main-layout/main-layout.component.ts` — `isMenuConsentBlocked()`, `getConsentTooltip()`
- `ivf-client/src/app/layout/main-layout/main-layout.component.html` — consent-blocked class + 🔒 icon
- `ivf-client/src/app/layout/main-layout/main-layout.component.scss` — styling
- `ivf-client/src/app/core/services/auth.service.ts` — `clear()` on logout

#### D. Thêm Path Mapping mới

Khi thêm endpoint mới cần consent, cần cập nhật **3 nơi**:

1. **Backend** — `PathConsentMap` trong `ConsentEnforcementMiddleware.cs`:

   ```csharp
   ["/api/new-feature"] = [ConsentTypes.DataProcessing, ConsentTypes.Research],
   ```

2. **Frontend** — `ROUTE_CONSENT_MAP` trong `consent-banner.service.ts`:

   ```typescript
   '/new-feature': ['data_processing', 'research'],
   ```

3. **Frontend** — `CONSENT_LABELS` trong `consent-banner.service.ts` (nếu consent type mới):

   ```typescript
   new_type: 'Label tiếng Việt',
   ```

### 8.6 Consent Flow Examples

#### Group Consent — Cấp/Thu hồi đồng ý theo nhóm

Admin có thể cấp hoặc thu hồi consent cho **toàn bộ thành viên** của một nhóm cùng lúc:

```
Admin mở Group Detail Modal → nhóm "team-bac-si" (5 thành viên)
  → Click "Quản lý đồng ý"
  → Modal hiện consent overview grid:
      📊 Xử lý dữ liệu:     3/5 thành viên (60%)
      🏥 Hồ sơ y tế:       2/5 thành viên (40%)
      📧 Tiếp thị:          0/5 thành viên (0%)
      ...
  → Admin chọn "Xử lý dữ liệu" + version "v2.1"
  → Click "Cấp cho toàn nhóm"
  → POST /api/user-groups/{groupId}/consents
     { consentType: "data_processing", consentVersion: "v2.1" }
  → Backend: GrantGroupConsentHandler
      → Lấy memberIds từ UserGroupMembers
      → Với mỗi member: SupersedeConsents + Grant mới
      → Response: { count: 5 }
  → Frontend alert: "Đã cấp đồng ý cho 5 thành viên"
  → Grid cập nhật: Xử lý dữ liệu: 5/5 (100%)
```

**Thu hồi consent cho nhóm:**

```
Admin click "Thu hồi" tại dòng "Xử lý dữ liệu"
  → Confirm: "Thu hồi đồng ý 'Xử lý dữ liệu' cho toàn bộ nhóm?"
  → DELETE /api/user-groups/{groupId}/consents/data_processing?reason=...
  → Backend: RevokeGroupConsentHandler
      → Với mỗi member: tìm consent data_processing còn valid → revoke
      → Response: { count: 5 }
  → Frontend alert: "Đã thu hồi 5 đồng ý"
  → Grid: Xử lý dữ liệu: 0/5 (0%)
  → Tất cả thành viên bị chặn bởi ConsentEnforcementMiddleware
```

**Chi tiết theo thành viên:**

Bảng chi tiết hiện matrix member × consent type với ✅/❌ cho từng ô.

**Files liên quan:**

- `EnterpriseUserCommands.cs` — `GrantGroupConsentCommand`, `RevokeGroupConsentCommand`, `GetGroupConsentStatusQuery`
- `EnterpriseUserCommandHandlers.cs` — 3 handlers
- `IEnterpriseUserRepository.cs` — `GetGroupMemberIdsAsync()`
- `EnterpriseUserEndpoints.cs` — 3 endpoints dưới `/api/user-groups/{groupId}/consents`
- `enterprise-user.service.ts` — `getGroupConsentStatus()`, `grantGroupConsent()`, `revokeGroupConsent()`
- `enterprise-users.component.ts` — Group consent modal state + methods
- `enterprise-users.component.html` — Group consent modal UI

Các ví dụ end-to-end theo dõi luồng consent từ cấp → enforcement → rút lại.

---

#### Ví dụ 1: Bác sĩ đăng nhập lần đầu — chưa có consent

```
1. Bác sĩ Nguyễn đăng nhập → JWT token cấp thành công

2. Bác sĩ mở trang Bệnh nhân → GET /api/patients
   → ConsentEnforcementMiddleware kiểm tra userId từ JWT
   → GetMissingConsentsAsync(userId, ["data_processing", "medical_records"])
   → Kết quả: ["data_processing", "medical_records"] (thiếu cả 2)
   → Response 403:
```

```json
{
  "error": "Required data consent not granted",
  "code": "CONSENT_REQUIRED",
  "missingConsents": ["data_processing", "medical_records"],
  "message": "Vui lòng cấp đồng ý xử lý dữ liệu trước khi truy cập tài nguyên này."
}
```

```
3. Frontend consentInterceptor bắt 403
   → ConsentBannerService.showMissingConsents(["data_processing", "medical_records"])
   → Banner vàng hiện: "⚠️ Thiếu đồng ý dữ liệu: Xử lý dữ liệu | Hồ sơ y tế — Quản lý đồng ý →"

4. Bác sĩ click "Quản lý đồng ý →" → chuyển đến /admin/security#consent
```

---

#### Ví dụ 2: Cấp đồng ý — từ Admin hoặc tự user

**API Request — Cấp data_processing:**

```bash
POST /api/enterprise-users/user-consents
Authorization: Bearer <jwt_token>
Content-Type: application/json

{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "consentType": "data_processing",
  "consentVersion": "v2.1",
  "expiresAt": "2027-03-03T00:00:00Z"
}
```

**Response:**

```json
{
  "id": "f8e7d6c5-b4a3-2190-fedc-ba0987654321"
}
```

**Backend flow:**

```
GrantConsentCommand
  → Validator: userId required, consentType phải nằm trong ConsentTypes
  → Handler:
      1. SupersedeConsentsAsync(userId, "data_processing") — revoke consent cũ cùng loại
      2. UserConsent.Grant(userId, "data_processing", "v2.1", ip, ua, expiresAt)
      3. AddConsentAsync(newConsent)
      4. UnitOfWork.SaveChanges()
  → Return consent ID
```

**Cấp thêm medical_records:**

```bash
POST /api/enterprise-users/user-consents
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "consentType": "medical_records",
  "consentVersion": "v1.0"
}
```

**Sau khi cấp đủ consent — truy cập lại:**

```
GET /api/patients
  → ConsentEnforcementMiddleware kiểm tra
  → GetMissingConsentsAsync → kết quả: [] (đã đủ)
  → Request được chuyển tiếp bình thường
  → Response 200 với danh sách bệnh nhân
```

---

#### Ví dụ 3: User truy cập nhiều tài nguyên khác nhau

```
Bác sĩ có consent: data_processing ✅, medical_records ✅

Truy cập GET /api/patients       → ✅ 200 OK (cần: data_processing + medical_records)
Truy cập GET /api/couples        → ✅ 200 OK (cần: data_processing + medical_records)
Truy cập GET /api/reports        → ❌ 403    (cần: data_processing + analytics)
                                         Missing: ["analytics"]
Truy cập GET /api/fingerprint    → ❌ 403    (cần: biometric_data)
                                         Missing: ["biometric_data"]
Truy cập GET /api/billing        → ✅ 200 OK (không yêu cầu consent)
Truy cập GET /api/queue          → ✅ 200 OK (không yêu cầu consent)
```

---

#### Ví dụ 4: Rút lại đồng ý — Revoke consent

**API Request:**

```bash
DELETE /api/enterprise-users/user-consents/f8e7d6c5-b4a3-2190-fedc-ba0987654321?reason=Yêu cầu xóa dữ liệu
Authorization: Bearer <jwt_token>
```

**Backend flow:**

```
RevokeConsentCommand
  → Tìm consent by ID
  → consent.Revoke("Yêu cầu xóa dữ liệu")
      → IsGranted = false
      → RevokedAt = DateTime.UtcNow
      → RevokedReason = "Yêu cầu xóa dữ liệu"
  → UnitOfWork.SaveChanges()
```

**Sau khi revoke — truy cập lại:**

```
GET /api/patients
  → Middleware kiểm tra → data_processing consent đã bị revoke
  → IsValid() = false (IsGranted = false)
  → Response 403 + missingConsents: ["data_processing"]
  → Frontend banner hiện lại
```

---

#### Ví dụ 5: Consent hết hạn tự động

```
Consent data_processing cấp ngày 01/03/2026, expiresAt = 01/06/2026

Ngày 31/05/2026:
  → GET /api/patients → ✅ 200 OK
  → IsValid() = true (ExpiresAt > UtcNow)

Ngày 02/06/2026:
  → GET /api/patients → ❌ 403
  → IsValid() = false (ExpiresAt < UtcNow)
  → missingConsents: ["data_processing"]
  → User cần cấp lại consent với version mới
```

---

#### Ví dụ 6: Frontend consent banner lifecycle

```
                    ┌──────────────────────────────────┐
                    │   User mở trang Bệnh nhân        │
                    └────────────┬─────────────────────┘
                                 │
                    ┌────────────▼─────────────────────┐
                    │ GET /api/patients → 403           │
                    │ code: CONSENT_REQUIRED            │
                    │ missingConsents: ["data_processing"│
                    │                 , "medical_records"]│
                    └────────────┬─────────────────────┘
                                 │
                    ┌────────────▼─────────────────────┐
                    │ consentInterceptor                │
                    │ → catchError(403 + CONSENT_REQUIRED)
                    │ → consentBanner.showMissingConsents│
                    └────────────┬─────────────────────┘
                                 │
              ┌──────────────────┴────────────────────┐
              │                                        │
   ┌──────────▼──────────┐              ┌──────────────▼──────────┐
   │ Banner hiện         │              │ Error vẫn propagate     │
   │ "⚠️ Thiếu đồng ý:   │              │ → Component xử lý 403  │
   │  Xử lý dữ liệu     │              │   (hiển thị lỗi hoặc   │
   │  Hồ sơ y tế"        │              │    trang trống)         │
   │ [Quản lý đồng ý →]  │              └─────────────────────────┘
   │ [✕ Đóng]            │
   └──────────┬──────────┘
              │
   ┌──────────┴──────────────────────────────────────┐
   │ User click "✕ Đóng"                              │
   │ → consentBanner.dismiss()                        │
   │ → Banner ẩn tạm thời                             │
   │ → Khi có 403 tiếp theo → banner hiện lại        │
   └──────────┬──────────────────────────────────────┘
              │
   ┌──────────▼──────────────────────────────────────┐
   │ User click "Quản lý đồng ý →"                   │
   │ → Router navigate: /admin/security#consent      │
   │ → Admin cấp consent cho user                     │
   │ → consentBanner.clear()                          │
   │ → User truy cập lại → 200 OK                    │
   └─────────────────────────────────────────────────┘
```

---

#### Ví dụ 7: Kiểm tra consent trong code (Backend)

```csharp
// Sử dụng IConsentValidationService trong handler/service khác
public class ExportPatientDataHandler
{
    private readonly IConsentValidationService _consentService;

    public async Task Handle(ExportPatientDataCommand cmd, CancellationToken ct)
    {
        // Kiểm tra consent cụ thể
        var hasConsent = await _consentService.HasValidConsentAsync(
            cmd.UserId, ConsentTypes.ThirdPartySharing, ct);

        if (!hasConsent)
            throw new ConsentRequiredException("third_party");

        // Kiểm tra nhiều consent cùng lúc
        var missing = await _consentService.GetMissingConsentsAsync(
            cmd.UserId,
            [ConsentTypes.DataProcessing, ConsentTypes.ThirdPartySharing, ConsentTypes.Research],
            ct);

        if (missing.Count > 0)
            throw new ConsentRequiredException(missing);

        // Tiến hành export...
    }
}
```

---

#### Ví dụ 8: Trạng thái consent trong database

```sql
-- Xem tất cả consent của 1 user
SELECT consent_type, is_granted, consent_version, consented_at,
       revoked_at, revoked_reason, expires_at
FROM user_consents
WHERE user_id = 'a1b2c3d4-...' AND is_deleted = false
ORDER BY consented_at DESC;

-- Kết quả:
-- consent_type     | is_granted | version | consented_at        | revoked_at          | expires_at
-- data_processing  | true       | v2.1    | 2026-03-01 08:00:00 | NULL                | 2027-03-01
-- medical_records  | true       | v1.0    | 2026-03-01 08:01:00 | NULL                | NULL
-- data_processing  | false      | v1.0    | 2025-12-01 10:00:00 | 2026-03-01 08:00:00 | NULL        ← superseded
-- marketing        | false      | v1.0    | 2026-01-15 09:00:00 | 2026-02-20 14:30:00 | NULL        ← revoked
-- analytics        | true       | v1.0    | 2026-02-01 11:00:00 | NULL                | 2026-06-01  ← sẽ hết hạn

-- Kiểm tra consent hợp lệ (IsValid logic)
SELECT consent_type
FROM user_consents
WHERE user_id = 'a1b2c3d4-...'
  AND is_deleted = false
  AND is_granted = true
  AND (expires_at IS NULL OR expires_at > NOW());
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

| Yêu cầu GDPR      | Giải pháp                                           |
| ----------------- | --------------------------------------------------- |
| Consent (Art. 7)  | UserConsent entity, explicit GrantedAt              |
| Right to Withdraw | RevokeConsentCommand                                |
| Record of Consent | IP, UserAgent, timestamp, version                   |
| Data Minimization | Consent per purpose (8 types)                       |
| Lawful Processing | Consent version tracking, expiry                    |
| Enforcement       | ConsentEnforcementMiddleware blocks without consent |
| User Notification | Consent warning banner in frontend UI               |

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

3. Thêm label vào `consent-banner.service.ts` → `CONSENT_LABELS`:

   ```typescript
   const CONSENT_LABELS: Record<string, string> = {
     // ... existing
     new_type: "Loại mới",
   };
   ```

4. (Nếu cần enforcement) Thêm path mapping vào **3 nơi** (xem mục 8.5.D):
   - `ConsentEnforcementMiddleware.cs` → `PathConsentMap`
   - `consent-banner.service.ts` → `ROUTE_CONSENT_MAP`
   - `consent-banner.service.ts` → `CONSENT_LABELS` (nếu type mới)

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
