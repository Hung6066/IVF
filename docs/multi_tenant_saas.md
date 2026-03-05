# IVF Platform — Kiến trúc Multi-Tenant & Bảng Giá SaaS

## Tổng quan Giải pháp

IVF Platform là hệ thống quản lý trung tâm IVF đầu tiên tại Việt Nam hỗ trợ đa trung tâm (Multi-Tenant SaaS). Mỗi trung tâm có dữ liệu cách ly hoàn toàn theo chiến lược cấu hình (Shared DB / Schema riêng / Database riêng). Platform Super Admin (Root Tenant) quản lý toàn bộ hệ thống, còn Tenant Admin chỉ thấy các tính năng vận hành chính.

**Tính năng nổi bật:**

- ✅ **Dynamic Feature-Plan Mapping** — Tính năng, bảng giá, giới hạn hoàn toàn cấu hình từ database (không hardcode)
- ✅ **Feature-Gated Menu** — Menu hiển thị dựa trên tính năng đã mua, tự động ẩn/hiện
- ✅ **Real Provisioning** — Tự động tạo Schema/Database riêng + migrate dữ liệu
- ✅ **Per-Tenant Feature Override** — Override tính năng cho từng trung tâm (custom plan)
- ✅ **Automatic Feature Sync** — Khi nâng/hạ gói, tính năng tự động đồng bộ
- ✅ **System-wide Limit Enforcement** — Giới hạn users, bệnh nhân/tháng, lưu trữ tự động kiểm tra ở backend
- ✅ **Feature Gate Pipeline** — MediatR pipeline tự động chặn command nếu feature chưa kích hoạt (`[RequiresFeature]`)
- ✅ **Route-level Feature Guard** — Angular route guard chặn truy cập trang nếu feature chưa mua
- ✅ **Tenant Limit Interceptor** — HTTP interceptor hiển thị thông báo khi vượt giới hạn (403)
- ✅ **Custom Domain** — Tenant Enterprise/Custom có thể gán tên miền riêng + xác minh DNS (CNAME + TXT) tự động
- ✅ **DnsClient.NET** — Xác minh DNS chính xác qua CNAME & TXT record (thay thế System.Net.Dns)
- ✅ **Caddy Reverse Proxy** — Wildcard SSL cho `*.ivf.clinic` + On-Demand TLS auto-cert cho custom domain

---

## 1. Kiến trúc Multi-Tenant

### 1.1 Mô hình tổng quan

```
┌────────────────────────────────────────────────────────────────────────┐
│                           IVF Platform                                  │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │              Root Tenant (Super Admin)                            │  │
│  │  • Quản lý toàn bộ trung tâm (CRUD, Activate, Suspend)          │  │
│  │  • Cấu hình chiến lược cô lập (Isolation Strategy)               │  │
│  │  • Quản lý FeatureDefinition, PlanDefinition (dynamic pricing)   │  │
│  │  • Thống kê doanh thu, tuân thủ, bảo mật toàn platform          │  │
│  │  • Quản lý backup, security events, compliance                   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  ┌─ SharedDatabase ──┐  ┌─ SeparateSchema ──┐  ┌─ SeparateDB ─────┐  │
│  │  Tenant A (Row)   │  │  Tenant B         │  │  Tenant C         │  │
│  │  IVF Hà Nội       │  │  IVF HCM          │  │  IVF Đà Nẵng     │  │
│  │  ───────────────  │  │  Schema: hcm      │  │  DB: ivf_dn       │  │
│  │  TenantId filter  │  │  Isolated tables  │  │  Full isolation   │  │
│  │  • Users          │  │  • Users          │  │  • Users          │  │
│  │  • Patients       │  │  • Patients       │  │  • Patients       │  │
│  │  • Cycles         │  │  • Cycles         │  │  • Cycles         │  │
│  │  • Invoices       │  │  • Invoices       │  │  • Invoices       │  │
│  │  • Forms          │  │  • Forms          │  │  • Forms          │  │
│  └───────────────────┘  └───────────────────┘  └───────────────────┘  │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │       Dynamic Feature-Plan System (Database-Driven)              │  │
│  │                                                                   │  │
│  │  FeatureDefinition ←→ PlanFeature ←→ PlanDefinition              │  │
│  │       (24 features)     (84 mappings)    (5 plans)               │  │
│  │                                                                   │  │
│  │  ┌─── Enforcement Layer ───────────────────────────────────────┐ │  │
│  │  │ Backend:                                                     │ │  │
│  │  │  • [RequiresFeature] + FeatureGateBehavior (MediatR)        │ │  │
│  │  │  • ITenantLimitService (Users, Patients, Storage)           │ │  │
│  │  │  • 403 TENANT_LIMIT_EXCEEDED / FEATURE_NOT_ENABLED          │ │  │
│  │  │ Frontend:                                                    │ │  │
│  │  │  • featureGuard() route guards (11 routes)                  │ │  │
│  │  │  • TenantFeatureService (signals)                           │ │  │
│  │  │  • tenantLimitInterceptor (HTTP 403 handler)                │ │  │
│  │  └─────────────────────────────────────────────────────────────┘ │  │
│  │            ↓                                                      │  │
│  │     TenantFeature (per-tenant activation, auto-synced)           │  │
│  │            ↓                                                      │  │
│  │     MenuItem.RequiredFeatureCode → Menu Visibility               │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Chiến lược cô lập dữ liệu (`DataIsolationStrategy`)

| Chiến lược              | Enum Value         | Mô tả                                                               | Use case                                                        |
| ----------------------- | ------------------ | ------------------------------------------------------------------- | --------------------------------------------------------------- |
| **Dùng chung Database** | `SharedDatabase`   | Row-level isolation via EF Core global query filter trên `TenantId` | Mặc định — chi phí thấp, phù hợp Trial/Starter                  |
| **Schema riêng**        | `SeparateSchema`   | Mỗi tenant có PostgreSQL schema riêng trong cùng database           | Cân bằng cô lập & hiệu suất — phù hợp Professional              |
| **Database riêng**      | `SeparateDatabase` | Mỗi tenant có database PostgreSQL riêng biệt                        | Cô lập cao nhất — phù hợp Enterprise, yêu cầu HIPAA nghiêm ngặt |

### 1.3 Cơ chế cách ly kỹ thuật

| Cơ chế                      | Mô tả                                                                                | Đảm bảo                                            |
| --------------------------- | ------------------------------------------------------------------------------------ | -------------------------------------------------- |
| **Row-Level Isolation**     | Mỗi bản ghi có `TenantId`, EF Core global query filter tự động lọc                   | Không bao giờ truy cập dữ liệu trung tâm khác      |
| **JWT Tenant Claim**        | Token JWT chứa `tenant_id` và `platform_admin` claims, middleware đọc và set context | Xác thực đúng trung tâm + phân quyền super admin   |
| **Automatic Assignment**    | `SaveChangesAsync()` tự động gán `TenantId` cho mọi entity mới                       | Không thể tạo dữ liệu mà không thuộc trung tâm nào |
| **Index Optimization**      | Tất cả bảng có index trên `TenantId`                                                 | Hiệu suất truy vấn O(log n)                        |
| **Platform Admin Override** | Super Admin có thể xem tất cả hoặc chọn trung tâm qua `X-Tenant-Id` header           | Quản lý linh hoạt                                  |
| **Schema Isolation**        | Tenant với `SeparateSchema` có tables trong schema riêng                             | Tách biệt vật lý ở mức schema                      |
| **Database Isolation**      | Tenant với `SeparateDatabase` có connection string riêng                             | Tách biệt hoàn toàn ở mức database                 |

### 1.4 Entities được cách ly (12 aggregate roots)

| Entity         | Vai trò                                   |
| -------------- | ----------------------------------------- |
| User           | Người dùng (Bác sĩ, Y tá, Lab Tech, v.v.) |
| Patient        | Bệnh nhân                                 |
| Doctor         | Bác sĩ điều trị                           |
| Couple         | Cặp đôi điều trị                          |
| TreatmentCycle | Chu kỳ điều trị IVF/IUI/ICSI/IVM          |
| QueueTicket    | Phiếu hàng đợi                            |
| Invoice        | Hoá đơn                                   |
| Appointment    | Lịch hẹn                                  |
| Notification   | Thông báo                                 |
| FormTemplate   | Mẫu biểu mẫu động                         |
| FormResponse   | Bản ghi biểu mẫu                          |
| ServiceCatalog | Danh mục dịch vụ                          |

### 1.5 Tenant Entity

```csharp
public class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public TenantStatus Status { get; private set; }

    // Database isolation
    public DataIsolationStrategy IsolationStrategy { get; private set; } = SharedDatabase;
    public string? ConnectionString { get; private set; }   // null = shared DB
    public string? DatabaseSchema { get; private set; }     // null = default schema
    public bool IsRootTenant { get; private set; }          // true = platform root

    // Resource limits
    public int MaxUsers { get; private set; }
    public int MaxPatientsPerMonth { get; private set; }
    public long StorageLimitMb { get; private set; }

    // Feature flags
    public bool AiEnabled { get; private set; }
    public bool DigitalSigningEnabled { get; private set; }
    public bool BiometricsEnabled { get; private set; }
    public bool AdvancedReportingEnabled { get; private set; }

    // Custom Domain (Enterprise/Custom plans)
    public string? CustomDomain { get; private set; }
    public CustomDomainStatus CustomDomainStatus { get; private set; } = CustomDomainStatus.None;
    public DateTime? CustomDomainVerifiedAt { get; private set; }
    public string? CustomDomainVerificationToken { get; private set; }

    // Methods: UpdateBranding(), VerifyCustomDomain(), FailCustomDomainVerification(), RemoveCustomDomain()
    // ... branding, customization
}
```

---

## 2. Super Admin & Phân quyền

### 2.1 Phân cấp vai trò

```
┌─────────────────────────────────────────────────────┐
│              Super Admin (Platform Admin)             │
│  IsPlatformAdmin = true  |  Root Tenant              │
│                                                       │
│  Quyền: TẤT CẢ                                      │
│  • CRUD Tenants (tạo, sửa, xoá, kích hoạt, ngưng)   │
│  • Cấu hình Isolation Strategy                       │
│  • Xem thống kê toàn platform                        │
│  • Quản lý Compliance, Security, Backup              │
│  • Quản lý UI Library, Pricing                       │
│  • Override xem dữ liệu bất kỳ tenant               │
│                                                       │
├───────────────────────────────────────────────────────┤
│              Tenant Admin                             │
│  IsPlatformAdmin = false  |  role = "Admin"           │
│                                                       │
│  Quyền: VẬN HÀNH TRUNG TÂM                           │
│  • Quản lý Users, Permissions trong tenant mình      │
│  • Quản lý Forms, Categories, Services               │
│  • Xem Audit logs, Reports                           │
│  • Cấu hình Digital Signing, Notifications           │
│  • KHÔNG thấy: Tenant Management, Pricing, Backup,   │
│    Security Events, Compliance, UI Library            │
│                                                       │
├───────────────────────────────────────────────────────┤
│              Operational Roles                        │
│  Doctor, Nurse, LabTech, Embryologist, Receptionist,  │
│  Cashier, Pharmacist                                  │
│                                                       │
│  Quyền: Theo permission-based, chỉ trong tenant mình │
└───────────────────────────────────────────────────────┘
```

### 2.2 Menu Visibility

| Menu Section                                     | Super Admin | Tenant Admin | Operational Roles    |
| ------------------------------------------------ | ----------- | ------------ | -------------------- |
| Dashboard, Patients, Cycles, Queue, Appointments | ✅          | ✅           | ✅ (theo permission) |
| Forms, Reports                                   | ✅          | ✅           | ✅ (theo permission) |
| Users, Permissions, Services, Categories         | ✅          | ✅           | ❌                   |
| Audit logs, Notifications, Digital Signing       | ✅          | ✅           | ❌                   |
| **Tenant Management**                            | ✅          | ❌           | ❌                   |
| **Pricing**                                      | ✅          | ❌           | ❌                   |
| **Backup, Security, Security Events**            | ✅          | ❌           | ❌                   |
| **Enterprise Security**                          | ✅          | ❌           | ❌                   |
| **Compliance (Vanta, Trust Page, Audit, ...)**   | ✅          | ❌           | ❌                   |
| **UI Library**                                   | ✅          | ❌           | ❌                   |

### 2.3 Dynamic Feature System (Database-Driven)

Hệ thống tính năng hoàn toàn cấu hình từ database thay vì hardcode. Mỗi tính năng được định nghĩa trong `feature_definitions`, mapping tới plan qua `plan_features`, và kích hoạt per-tenant qua `tenant_features`.

#### 2.3.1 Feature Architecture

```
┌─────────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│  FeatureDefinition  │      │  PlanDefinition   │      │     Tenant       │
│  (24 features)      │      │  (5 plans)        │      │                  │
│  ─────────────────  │      │  ────────────────  │      │                  │
│  code               │◄────►│  plan             │      │                  │
│  displayName        │      │  monthlyPrice     │      │                  │
│  description        │  N:M │  maxUsers         │      │                  │
│  icon               │──────│  isFeatured       │      │                  │
│  category           │      │                    │      │                  │
│  sortOrder          │      └──────────────────┘      │                  │
│  isActive           │                                  │                  │
└─────────┬───────────┘                                  └────────┬─────────┘
          │                                                       │
          │  1:N                                            1:N   │
          ▼                                                       ▼
┌─────────────────────┐                               ┌──────────────────┐
│    PlanFeature      │                               │  TenantFeature   │
│  join table         │                               │  per-tenant      │
│  ─────────────────  │    Auto-sync on               │  ────────────── │
│  planDefinitionId   │    plan change                │  tenantId        │
│  featureDefinitionId│──────────────────────────────►│  featureDefId    │
│  sortOrder          │    SyncTenantFeatures         │  isEnabled       │
└─────────────────────┘    FromPlanAsync()            └──────────────────┘
```

#### 2.3.2 Feature Definitions (24 tính năng)

| Code                 | DisplayName          | Category   | Icon | Gói tối thiểu |
| -------------------- | -------------------- | ---------- | ---- | ------------- |
| `patient_management` | Quản lý bệnh nhân    | core       | 👥   | Trial         |
| `appointments`       | Lịch hẹn             | core       | 📅   | Trial         |
| `queue`              | Hàng đợi             | core       | 🔢   | Trial         |
| `basic_forms`        | Biểu mẫu cơ bản      | core       | 📋   | Trial         |
| `billing`            | Thanh toán           | core       | 💰   | Starter       |
| `consultation`       | Tư vấn khám          | core       | 🩺   | Starter       |
| `ultrasound`         | Siêu âm              | core       | 📡   | Starter       |
| `lab`                | Xét nghiệm           | core       | 🔬   | Starter       |
| `pharmacy`           | Nhà thuốc            | core       | 💊   | Starter       |
| `injection`          | Tiêm thuốc           | core       | 💉   | Starter       |
| `andrology`          | Nam học              | advanced   | 🔬   | Professional  |
| `sperm_bank`         | Ngân hàng tinh trùng | advanced   | 🏦   | Professional  |
| `advanced_reporting` | Báo cáo nâng cao     | advanced   | 📊   | Starter       |
| `export_pdf`         | Xuất PDF             | advanced   | 📄   | Starter       |
| `email_support`      | Hỗ trợ email         | advanced   | 📧   | Starter       |
| `ai`                 | AI hỗ trợ            | advanced   | 🤖   | Professional  |
| `digital_signing`    | Ký số                | advanced   | ✍️   | Professional  |
| `hipaa_gdpr`         | HIPAA/GDPR           | advanced   | 🛡️   | Professional  |
| `priority_support`   | Hỗ trợ ưu tiên       | advanced   | ⭐   | Professional  |
| `biometrics`         | Sinh trắc học        | enterprise | 🔐   | Enterprise    |
| `sso_saml`           | SSO/SAML             | enterprise | 🔑   | Enterprise    |
| `sla_999`            | SLA 99.9%            | enterprise | 📈   | Enterprise    |
| `support_247`        | Hỗ trợ 24/7          | enterprise | 🕐   | Enterprise    |
| `custom_domain`      | Custom domain        | enterprise | 🌐   | Enterprise    |

#### 2.3.3 Tenant Features API

Endpoint `GET /api/tenants/my-features` trả về danh sách tính năng đã kích hoạt (dynamic, từ database):

```typescript
interface TenantFeatures {
  isPlatformAdmin: boolean;       // true = super admin, tự động có tất cả features
  enabledFeatures: string[];      // Mảng feature codes đã kích hoạt
  isolationStrategy: DataIsolationStrategy;
  maxUsers: number;
  maxPatients: number;
}

// Ví dụ response cho gói Professional:
{
  "isPlatformAdmin": false,
  "enabledFeatures": [
    "patient_management", "appointments", "queue", "basic_forms",
    "billing", "consultation", "ultrasound", "lab", "pharmacy", "injection",
    "andrology", "sperm_bank", "advanced_reporting", "export_pdf",
    "email_support", "ai", "digital_signing", "hipaa_gdpr", "priority_support"
  ],
  "isolationStrategy": "SeparateSchema",
  "maxUsers": 30,
  "maxPatients": 500
}
```

#### 2.3.4 Feature-Gated Menu Navigation

Menu items có thể gán `RequiredFeatureCode`. Frontend tự động ẩn menu nếu tenant chưa mua feature:

```typescript
// MenuItem entity (Domain)
public string? RequiredFeatureCode { get; private set; }

// Frontend visibility check (main-layout.component.ts)
isMenuItemVisible(item: MenuItem): boolean {
  if (item.requiredFeatureCode && this.tenantFeatures) {
    if (!this.tenantFeatures.isPlatformAdmin &&
        !this.tenantFeatures.enabledFeatures.includes(item.requiredFeatureCode)) {
      return false;  // Ẩn menu nếu feature chưa kích hoạt
    }
  }
  return true;
}
```

#### 2.3.5 Automatic Feature Sync

Khi tạo tenant mới hoặc nâng/hạ gói, hệ thống tự động đồng bộ tính năng:

```
Tạo Tenant mới (POST /api/tenants)
  └─ CreateTenantCommandHandler
       └─ SyncTenantFeaturesFromPlanAsync(tenantId, plan)
            ├─ Tìm PlanDefinition theo plan enum
            ├─ Xóa tất cả TenantFeature cũ (nếu có)
            ├─ Tạo TenantFeature mới cho mỗi PlanFeature
            └─ SaveChanges (atomic)

Nâng/hạ gói (PUT /api/tenants/{id}/subscription)
  └─ UpdateSubscriptionCommandHandler
       └─ SyncTenantFeaturesFromPlanAsync(tenantId, newPlan)
            ├─ Xóa tất cả TenantFeature cũ (Professional features)
            ├─ Tạo TenantFeature mới (Enterprise features)
            └─ Tenant giờ có feature set của Enterprise
```

#### 2.3.6 System-wide Limit & Feature Enforcement

Hệ thống áp dụng giới hạn và feature gating ở **cả backend lẫn frontend**, đảm bảo tenant chỉ sử dụng được tính năng và tài nguyên trong phạm vi gói đã mua.

##### Backend Enforcement Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                    MediatR Pipeline (theo thứ tự)                    │
│                                                                      │
│  Request → ValidationBehavior                                        │
│          → FeatureGateBehavior  ◄── [RequiresFeature] attribute      │
│          → VaultPolicyBehavior                                       │
│          → ZeroTrustBehavior                                         │
│          → FieldAccessBehavior                                       │
│          → Handler                                                   │
│               └─ ITenantLimitService.EnsureXxxLimitAsync()           │
│                                                                      │
│  Nếu vi phạm:                                                       │
│  • Feature chưa kích hoạt → FeatureNotEnabledException → 403        │
│  • Vượt giới hạn tài nguyên → TenantLimitExceededException → 403   │
│  • Platform Admin bypass TẤT CẢ kiểm tra                            │
└──────────────────────────────────────────────────────────────────────┘
```

**1. Feature Gate Pipeline (`FeatureGateBehavior`)**

`IPipelineBehavior<TRequest, TResponse>` tự động kiểm tra `[RequiresFeature]` attribute trên mỗi MediatR command/query trước khi handler thực thi:

```csharp
// Đánh dấu command yêu cầu feature
[RequiresFeature(FeatureCodes.Billing)]
public record CreateInvoiceCommand(...) : IRequest<Guid>;

[RequiresFeature(FeatureCodes.PatientManagement)]
[RequiresFeature(FeatureCodes.DigitalSigning)]  // Có thể yêu cầu nhiều features
public record SignPatientDocumentCommand(...) : IRequest<string>;
```

Danh sách commands đã gắn `[RequiresFeature]`:

| Feature Code         | Commands                                             |
| -------------------- | ---------------------------------------------------- |
| `patient_management` | CreatePatient, UploadDocument                        |
| `billing`            | CreateInvoice, AddPayment, AssignService             |
| `queue`              | CreateQueueTicket, CallNext, CheckIn, TransferTicket |
| `ultrasound`         | CreateUltrasound                                     |
| `consultation`       | CreateConsultation                                   |
| `injection`          | CreateInjection                                      |
| `andrology`          | CreateAndrology                                      |
| `sperm_bank`         | CreateSpermWashing                                   |
| `lab`                | CreateLabResult, CreateEmbryoRecord                  |
| `basic_forms`        | SubmitFormResponse                                   |
| `digital_signing`    | SignDocument                                         |
| `biometrics`         | EnrollBiometric, VerifyBiometric                     |
| `appointments`       | CreateAppointment                                    |

**2. Resource Limit Enforcement (`ITenantLimitService`)**

Handler-level checks cho 3 loại giới hạn tài nguyên:

```csharp
public interface ITenantLimitService
{
    // Kiểm tra số lượng users hiện tại vs MaxUsers
    Task EnsureUserLimitAsync(CancellationToken ct = default);

    // Kiểm tra bệnh nhân mới tháng này vs MaxPatientsPerMonth
    Task EnsurePatientLimitAsync(CancellationToken ct = default);

    // Kiểm tra dung lượng lưu trữ + file mới vs StorageLimitMb
    Task EnsureStorageLimitAsync(long additionalBytes, CancellationToken ct = default);

    // Kiểm tra feature có enabled trong tenant_features
    Task EnsureFeatureEnabledAsync(string featureCode, CancellationToken ct = default);
}
```

**Quy tắc xác định giới hạn (Effective Limits):**

```
1. PlanDefinition limits     ← Ưu tiên cao nhất (từ gói đã đăng ký)
2. Tenant entity defaults    ← Fallback (cấu hình riêng per-tenant)
3. Hard defaults (5/50/1024) ← Fallback cuối cùng
```

**Handlers đã tích hợp limit check:**

| Handler                        | Limit Check                              |
| ------------------------------ | ---------------------------------------- |
| `CreateUserCommandHandler`     | `EnsureUserLimitAsync()`                 |
| `CreatePatientHandler`         | `EnsurePatientLimitAsync()`              |
| `UploadPatientDocumentHandler` | `EnsureStorageLimitAsync(fileSizeBytes)` |

**3. Custom Exceptions & API Response**

```csharp
// TenantLimitExceededException
{
    LimitType: "MaxUsers" | "MaxPatientsPerMonth" | "StorageLimitMb",
    CurrentCount: int,
    MaxAllowed: int,
    Message: "Đã vượt giới hạn MaxUsers: hiện tại 30/30"
}

// FeatureNotEnabledException
{
    FeatureCode: "billing",
    Message: "Tính năng 'billing' chưa được kích hoạt cho tổ chức của bạn"
}
```

API response format (HTTP 403):

```json
// TENANT_LIMIT_EXCEEDED
{
  "code": "TENANT_LIMIT_EXCEEDED",
  "message": "Đã vượt giới hạn MaxUsers: hiện tại 30/30",
  "limitType": "MaxUsers",
  "currentCount": 30,
  "maxAllowed": 30
}

// FEATURE_NOT_ENABLED
{
  "code": "FEATURE_NOT_ENABLED",
  "message": "Tính năng 'billing' chưa được kích hoạt cho tổ chức của bạn",
  "featureCode": "billing"
}
```

##### Frontend Enforcement Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                    Frontend Enforcement Layers                       │
│                                                                      │
│  Layer 1: Route Guards (featureGuard)                                │
│  ├─ Chặn TRƯỚC khi navigate vào route                              │
│  ├─ Check TenantFeatureService.isFeatureEnabled(code)               │
│  └─ Redirect → /dashboard?featureBlocked={code}                    │
│                                                                      │
│  Layer 2: Menu Visibility (isMenuItemVisible)                       │
│  ├─ Ẩn menu items có requiredFeatureCode chưa kích hoạt            │
│  └─ Platform Admin bypass tất cả                                    │
│                                                                      │
│  Layer 3: HTTP Interceptor (tenantLimitInterceptor)                 │
│  ├─ Bắt 403 TENANT_LIMIT_EXCEEDED → alert giới hạn                │
│  └─ Bắt 403 FEATURE_NOT_ENABLED → alert tính năng                 │
└──────────────────────────────────────────────────────────────────────┘
```

**Route Guards (11 routes bảo vệ):**

```typescript
// app.routes.ts
{ path: 'billing',     canActivate: [featureGuard('billing')],     ... },
{ path: 'queue',        canActivate: [featureGuard('queue')],        ... },
{ path: 'ultrasound',   canActivate: [featureGuard('ultrasound')],   ... },
{ path: 'consultation', canActivate: [featureGuard('consultation')], ... },
{ path: 'injection',    canActivate: [featureGuard('injection')],    ... },
{ path: 'andrology',    canActivate: [featureGuard('andrology')],    ... },
{ path: 'sperm-bank',   canActivate: [featureGuard('sperm_bank')],   ... },
{ path: 'lab',          canActivate: [featureGuard('lab')],          ... },
{ path: 'pharmacy',     canActivate: [featureGuard('pharmacy')],     ... },
{ path: 'reports',      canActivate: [featureGuard('advanced_reporting')], ... },
{ path: 'appointments', canActivate: [featureGuard('appointments')], ... },
```

**TenantFeatureService (Signal-based State):**

```typescript
@Injectable({ providedIn: "root" })
export class TenantFeatureService {
  // Signals
  readonly features: Signal<TenantFeatures | null>;
  readonly enabledFeatures: Signal<string[]>;
  readonly isPlatformAdmin: Signal<boolean>;
  readonly maxUsers: Signal<number>;
  readonly maxPatients: Signal<number>;

  load(): Promise<void>; // Lazy load from GET /api/tenants/my-features
  reload(): Promise<void>; // Force reload (after plan upgrade)
  isFeatureEnabled(code: string): boolean; // Check + platform admin bypass
  clear(): void; // Clear on logout
}
```

### 2.4 JWT Claims

| Claim            | Mô tả            | Ví dụ                                  |
| ---------------- | ---------------- | -------------------------------------- |
| `sub`            | User ID          | `cab11b45-5853-41d0-9ea6-0ae3d357360c` |
| `role`           | Vai trò          | `Admin`, `Doctor`, `Nurse`             |
| `tenant_id`      | Tenant ID        | `00000000-0000-0000-0000-000000000001` |
| `platform_admin` | Super Admin flag | `true` / `false`                       |

---

## 3. API Endpoints — Tenant Management

### 3.1 Public Endpoints

| Method | Path                                | Mô tả                                  | Auth         |
| ------ | ----------------------------------- | -------------------------------------- | ------------ |
| `GET`  | `/api/tenants/pricing`              | Bảng giá SaaS dynamic (từ database)    | ❌ Anonymous |
| `GET`  | `/api/tenants/domain-check?domain=` | Caddy On-Demand TLS callback (200/404) | ❌ Anonymous |

### 3.2 Authenticated Endpoints

| Method | Path                       | Mô tả                                           | Auth   |
| ------ | -------------------------- | ----------------------------------------------- | ------ |
| `GET`  | `/api/tenants/my-features` | Feature codes đã kích hoạt + limits + isolation | ✅ JWT |

### 3.3 Platform Admin Only Endpoints

| Method   | Path                               | Mô tả                                  | Auth             |
| -------- | ---------------------------------- | -------------------------------------- | ---------------- |
| `GET`    | `/api/tenants`                     | Danh sách tenants                      | ✅ PlatformAdmin |
| `GET`    | `/api/tenants/{id}`                | Chi tiết tenant                        | ✅ PlatformAdmin |
| `POST`   | `/api/tenants`                     | Tạo tenant mới + auto-sync features    | ✅ PlatformAdmin |
| `PUT`    | `/api/tenants/{id}`                | Cập nhật thông tin                     | ✅ PlatformAdmin |
| `PUT`    | `/api/tenants/{id}/branding`       | Cập nhật branding                      | ✅ PlatformAdmin |
| `PUT`    | `/api/tenants/{id}/limits`         | Cập nhật giới hạn & features           | ✅ PlatformAdmin |
| `PUT`    | `/api/tenants/{id}/isolation`      | Thay đổi chiến lược cô lập             | ✅ PlatformAdmin |
| `PUT`    | `/api/tenants/{id}/subscription`   | Cập nhật subscription + sync features  | ✅ PlatformAdmin |
| `POST`   | `/api/tenants/{id}/activate`       | Kích hoạt tenant                       | ✅ PlatformAdmin |
| `POST`   | `/api/tenants/{id}/suspend`        | Tạm ngưng tenant                       | ✅ PlatformAdmin |
| `POST`   | `/api/tenants/{id}/cancel`         | Hủy tenant                             | ✅ PlatformAdmin |
| `GET`    | `/api/tenants/stats`               | Thống kê toàn platform                 | ✅ PlatformAdmin |
| `POST`   | `/api/tenants/{id}/domain/verify`  | Xác minh DNS custom domain (CNAME+TXT) | ✅ PlatformAdmin |
| `DELETE` | `/api/tenants/{id}/domain`         | Xóa custom domain                      | ✅ PlatformAdmin |
| `GET`    | `/api/tenants/feature-definitions` | Tất cả feature definitions (CRUD)      | ✅ PlatformAdmin |

### 3.4 Pricing API Response Example

```json
// GET /api/tenants/pricing
[
  {
    "plan": "Trial",
    "displayName": "Dùng thử",
    "description": "Trải nghiệm miễn phí 30 ngày",
    "price": 0,
    "currency": "VND",
    "duration": "30 ngày",
    "maxUsers": 3,
    "maxPatients": 20,
    "storageGb": 0.5,
    "isFeatured": false,
    "features": [
      {
        "code": "patient_management",
        "displayName": "Quản lý bệnh nhân",
        "icon": "👥",
        "category": "core"
      },
      {
        "code": "appointments",
        "displayName": "Lịch hẹn",
        "icon": "📅",
        "category": "core"
      },
      {
        "code": "queue",
        "displayName": "Hàng đợi",
        "icon": "🔢",
        "category": "core"
      },
      {
        "code": "basic_forms",
        "displayName": "Biểu mẫu cơ bản",
        "icon": "📋",
        "category": "core"
      }
    ]
  },
  {
    "plan": "Professional",
    "displayName": "Chuyên nghiệp",
    "description": "Đầy đủ tính năng cho trung tâm quy mô trung-lớn",
    "price": 15000000,
    "currency": "VND",
    "duration": "Tháng",
    "maxUsers": 30,
    "maxPatients": 500,
    "storageGb": 20,
    "isFeatured": true,
    "features": [
      // 19 features including AI, Digital Signing, HIPAA/GDPR
    ]
  }
  // ... Enterprise (24 features)
]
```

---

## 4. Bảng Giá SaaS (Database-Driven)

> **Lưu ý**: Bảng giá dưới đây được cấu hình hoàn toàn từ database (`plan_definitions` + `plan_features`). Super Admin có thể thêm/sửa/xóa plan và feature mapping mà không cần deploy lại code.

### 4.1 So sánh Gói dịch vụ

| Tính năng                | Trial        | Starter        | Professional    | Enterprise      | Custom            |
| ------------------------ | ------------ | -------------- | --------------- | --------------- | ----------------- |
| **Giá/tháng**            | **Miễn phí** | **5,000,000₫** | **15,000,000₫** | **35,000,000₫** | **Thỏa thuận**    |
| **Thời gian**            | 30 ngày      | Không giới hạn | Không giới hạn  | Không giới hạn  | Không giới hạn    |
| **Số tính năng**         | **4**        | **13**         | **19**          | **24**          | **24** (tuỳ chọn) |
| Người dùng               | 3            | 10             | 30              | 100             | 999               |
| Bệnh nhân/tháng          | 20           | 100            | 500             | 2,000           | 99,999            |
| Lưu trữ                  | 512 MB       | 5 GB           | 20 GB           | 100 GB          | 1 TB              |
| **──── Core ────**       |              |                |                 |                 |                   |
| 👥 Quản lý BN            | ✅           | ✅             | ✅              | ✅              | ✅                |
| 📅 Lịch hẹn              | ✅           | ✅             | ✅              | ✅              | ✅                |
| 🔢 Hàng đợi              | ✅           | ✅             | ✅              | ✅              | ✅                |
| 📋 Biểu mẫu cơ bản       | ✅           | ✅             | ✅              | ✅              | ✅                |
| 💰 Thanh toán            | ❌           | ✅             | ✅              | ✅              | ✅                |
| 🩺 Tư vấn khám           | ❌           | ✅             | ✅              | ✅              | ✅                |
| 📡 Siêu âm               | ❌           | ✅             | ✅              | ✅              | ✅                |
| 🔬 Xét nghiệm            | ❌           | ✅             | ✅              | ✅              | ✅                |
| 💊 Nhà thuốc             | ❌           | ✅             | ✅              | ✅              | ✅                |
| 💉 Tiêm thuốc            | ❌           | ✅             | ✅              | ✅              | ✅                |
| **──── Advanced ────**   |              |                |                 |                 |                   |
| 📊 Báo cáo nâng cao      | ❌           | ✅             | ✅              | ✅              | ✅                |
| 📄 Export PDF            | ❌           | ✅             | ✅              | ✅              | ✅                |
| 📧 Hỗ trợ email          | ❌           | ✅             | ✅ (ưu tiên)    | ✅ (24/7)       | ✅                |
| 🤖 AI hỗ trợ             | ❌           | ❌             | ✅              | ✅              | ✅                |
| ✍️ Ký số (PKI)           | ❌           | ❌             | ✅              | ✅              | ✅                |
| 🛡️ HIPAA/GDPR            | ❌           | ❌             | ✅              | ✅              | ✅                |
| ⭐ Hỗ trợ ưu tiên        | ❌           | ❌             | ✅              | ✅              | ✅                |
| 🔬 Nam học               | ❌           | ❌             | ✅              | ✅              | ✅                |
| 🏦 Ngân hàng tinh trùng  | ❌           | ❌             | ✅              | ✅              | ✅                |
| **──── Enterprise ────** |              |                |                 |                 |                   |
| 🔐 Sinh trắc học         | ❌           | ❌             | ❌              | ✅              | ✅                |
| 🔑 SSO/SAML              | ❌           | ❌             | ❌              | ✅              | ✅                |
| 📈 SLA 99.9%             | ❌           | ❌             | ❌              | ✅              | ✅                |
| 🕐 Hỗ trợ 24/7           | ❌           | ❌             | ❌              | ✅              | ✅                |
| 🌐 Custom domain         | ❌           | ❌             | ❌              | ✅              | ✅                |
| **Isolation mặc định**   | SharedDB     | SharedDB       | SeparateSchema  | SeparateDB      | SeparateDB        |

### 4.2 Chiết khấu theo chu kỳ

| Chu kỳ     | Mô tả                   | Chiết khấu |
| ---------- | ----------------------- | ---------- |
| Hàng tháng | Thanh toán mỗi tháng    | 0%         |
| Hàng quý   | Thanh toán 3 tháng/lần  | **5%**     |
| Hàng năm   | Thanh toán 12 tháng/lần | **15%**    |

### 4.3 Ví dụ tính giá

**Gói Professional, thanh toán hàng năm:**

- Giá gốc: 15,000,000₫ × 12 = 180,000,000₫/năm
- Chiết khấu 15%: -27,000,000₫
- **Tổng: 153,000,000₫/năm** (= 12,750,000₫/tháng)

---

## 5. Tenant Provisioning (Real Infrastructure)

### 5.1 Provisioning Service

`ITenantProvisioningService` tự động tạo hạ tầng cô lập khi tạo tenant mới hoặc thay đổi chiến lược:

```
┌────────────────────────────────────────────────────────────────────┐
│                    TenantProvisioningService                       │
│                                                                    │
│  ProvisionAsync(tenantId, slug, strategy)                         │
│  │                                                                 │
│  ├─ SharedDatabase:                                                │
│  │   └─ No-op (row-level isolation via TenantId)                  │
│  │                                                                 │
│  ├─ SeparateSchema:                                                │
│  │   ├─ CREATE SCHEMA "tenant_{slug}"                             │
│  │   ├─ CREATE TABLE "schema"."table" (LIKE "public"."table")     │
│  │   ├─ Migrate tenant data: public → schema (INSERT + DELETE)    │
│  │   └─ Return SchemaName = "tenant_{slug}"                       │
│  │                                                                 │
│  └─ SeparateDatabase:                                              │
│      ├─ CREATE DATABASE "ivf_{slug}"                              │
│      ├─ CREATE TABLE structures (from information_schema DDL)     │
│      ├─ Migrate data via PostgreSQL COPY protocol                 │
│      │   ├─ BeginTextExportAsync (source DB)                      │
│      │   ├─ BeginTextImportAsync (target DB)                      │
│      │   └─ Stream data row-by-row (efficient bulk transfer)      │
│      ├─ DELETE migrated data from source                          │
│      └─ Return ConnectionString for new database                  │
│                                                                    │
│  DeprovisionAsync(tenantId, previousStrategy, schema, connStr)    │
│  │                                                                 │
│  ├─ SeparateSchema:                                                │
│  │   ├─ Migrate data: schema → public (INSERT)                   │
│  │   └─ DROP SCHEMA "tenant_{slug}" CASCADE                      │
│  │                                                                 │
│  └─ SeparateDatabase:                                              │
│      ├─ Migrate data via COPY: tenant DB → main DB               │
│      └─ DROP DATABASE "ivf_{slug}"                                │
└────────────────────────────────────────────────────────────────────┘
```

### 5.2 Tables Provisioned (12 aggregate roots)

| Table              | Entity         | Dữ liệu          |
| ------------------ | -------------- | ---------------- |
| `users`            | User           | Người dùng       |
| `patients`         | Patient        | Bệnh nhân        |
| `doctors`          | Doctor         | Bác sĩ           |
| `couples`          | Couple         | Cặp đôi          |
| `treatment_cycles` | TreatmentCycle | Chu kỳ điều trị  |
| `queue_tickets`    | QueueTicket    | Phiếu hàng đợi   |
| `invoices`         | Invoice        | Hoá đơn          |
| `appointments`     | Appointment    | Lịch hẹn         |
| `notifications`    | Notification   | Thông báo        |
| `form_templates`   | FormTemplate   | Mẫu biểu mẫu     |
| `form_responses`   | FormResponse   | Bản ghi biểu mẫu |
| `service_catalogs` | ServiceCatalog | Danh mục dịch vụ |

### 5.3 Isolation Strategy Change Flow

```
PUT /api/tenants/{id}/isolation { "isolationStrategy": "SeparateDatabase" }
  │
  ├─ UpdateTenantIsolationCommandHandler
  │   ├─ Load current tenant (isolation = SeparateSchema)
  │   ├─ DeprovisionAsync(tenant, SeparateSchema, schemaName, null)
  │   │   ├─ Migrate data: tenant schema → public schema
  │   │   └─ DROP SCHEMA "tenant_slug" CASCADE
  │   ├─ ProvisionAsync(tenant, slug, SeparateDatabase)
  │   │   ├─ CREATE DATABASE "ivf_slug"
  │   │   ├─ Create table structures
  │   │   ├─ Migrate data via COPY protocol
  │   │   └─ Return new ConnectionString
  │   ├─ Update tenant: IsolationStrategy, ConnectionString, DatabaseSchema
  │   └─ SaveChanges
  │
  └─ Tenant data now in dedicated database ✅
```

---

## 6. Key Operational Flows

### 6.1 Tạo Tenant mới (Full Flow)

```
POST /api/tenants
{
  "name": "Trung tâm IVF Đông Đô",
  "slug": "trung-tam-ivf-dong-do",
  "email": "admin@dongdo.vn",
  "plan": "Professional",
  "billingCycle": "Monthly",
  "isolationStrategy": "SeparateSchema",
  "adminUsername": "admin_dd",
  "adminPassword": "SecurePass123!",
  "adminFullName": "Nguyễn Văn Admin"
}

CreateTenantCommandHandler:
  1. Validate slug uniqueness
  2. Lookup PlanDefinition → Professional (15M VND, 30 users, 500 patients)
  3. Create Tenant entity with plan-based limits
  4. Create TenantSubscription (Professional, Monthly, 15M VND)
  5. Create admin User (with hashed password)
  6. Create TenantUsageRecord (month/year)
  7. SyncTenantFeaturesFromPlanAsync(tenantId, Professional)
     → Creates 19 TenantFeature records (all Professional features enabled)
  8. Provision infrastructure:
     → SeparateSchema: CREATE SCHEMA "tenant_trung_tam_ivf_dong_do"
     → Create 12 tenant tables in new schema
  9. Return tenant details with ID

Result:
  ✅ Tenant created with Professional features (19/24)
  ✅ Schema isolated with dedicated tables
  ✅ Admin user can log in immediately
```

### 6.2 Nâng cấp gói (Plan Upgrade)

```
PUT /api/tenants/{id}/subscription
{ "plan": "Enterprise", "billingCycle": "Annually", "monthlyPrice": 35000000 }

UpdateSubscriptionCommandHandler:
  1. Find active TenantSubscription
  2. Update plan → Enterprise, billing → Annually
  3. SyncTenantFeaturesFromPlanAsync(tenantId, Enterprise)
     ├─ Delete 19 old TenantFeature records (Professional)
     ├─ Create 24 new TenantFeature records (Enterprise)
     └─ Atomic transaction
  4. SaveChanges

Result:
  ✅ Subscription updated to Enterprise
  ✅ 5 new features unlocked: biometrics, sso_saml, sla_999, support_247, custom_domain
  ✅ Menu items with RequiredFeatureCode now visible
```

### 6.3 Feature Visibility Check (Frontend)

```
1. User navigates to dashboard
2. MainLayoutComponent.loadMenuFromApi()
   → GET /api/menu → Response includes requiredFeatureCode per item
3. MainLayoutComponent.loadTenantFeatures()
   → GET /api/tenants/my-features → { enabledFeatures: ["ai", "digital_signing", ...] }
4. For each menu item:
   isMenuItemVisible(item):
   ├─ No requiredFeatureCode → visible ✅
   ├─ isPlatformAdmin → always visible ✅
   ├─ Feature in enabledFeatures → visible ✅
   └─ Feature NOT in enabledFeatures → HIDDEN ❌
5. AI menu item (requiredFeatureCode: "ai")
   ├─ Trial tenant → hidden (ai not in features)
   └─ Professional tenant → visible (ai in features)
```

### 6.4 Enforcement: Chặn truy cập Feature chưa mua

```
Tenant gói Trial cố tạo hoá đơn (billing chưa kích hoạt):

Frontend:
  1. User click menu "Thanh toán"
  2. featureGuard('billing') → TenantFeatureService.isFeatureEnabled('billing')
  3. Trial không có billing → redirect /dashboard?featureBlocked=billing
  4. ❌ CHẶN — không vào được trang

Backend (nếu bypass frontend, gọi API trực tiếp):
  1. POST /api/billing/invoices → CreateInvoiceCommand
  2. MediatR Pipeline → FeatureGateBehavior
  3. [RequiresFeature("billing")] detected
  4. ITenantLimitService.EnsureFeatureEnabledAsync("billing")
  5. tenant_features: billing.IsEnabled = false
  6. Throw FeatureNotEnabledException("billing")
  7. Exception Handler → HTTP 403
     { "code": "FEATURE_NOT_ENABLED", "featureCode": "billing" }
  8. Frontend: tenantLimitInterceptor catches 403
  9. Alert: "Tính năng 'billing' chưa được kích hoạt"
```

### 6.5 Enforcement: Vượt giới hạn tài nguyên

```
Tenant gói Starter (maxUsers = 10) cố tạo user thứ 11:

  1. POST /api/users → CreateUserCommand
  2. MediatR Pipeline → FeatureGateBehavior → pass (no feature attr)
  3. CreateUserCommandHandler:
     └─ ITenantLimitService.EnsureUserLimitAsync()
        ├─ GetEffectiveLimitsAsync():
        │   ├─ Find active subscription → Starter
        │   ├─ PlanDefinition for Starter → maxUsers = 10
        │   └─ return (10, 100, 5120)
        ├─ GetTenantUserCountAsync() → 10 (hiện tại)
        └─ 10 >= 10 → Throw TenantLimitExceededException("MaxUsers", 10, 10)
  4. Exception Handler → HTTP 403
     { "code": "TENANT_LIMIT_EXCEEDED", "limitType": "MaxUsers",
       "currentCount": 10, "maxAllowed": 10 }
  5. tenantLimitInterceptor → Alert:
     "Đã vượt giới hạn số lượng người dùng: 10/10.
      Vui lòng liên hệ quản trị viên hoặc nâng cấp gói dịch vụ."
```

---

## 7. Custom Domain & Reverse Proxy

### 7.1 Tổng quan Custom Domain

Tenant gói Enterprise/Custom có thể sử dụng tên miền riêng (VD: `ivf.benhvienabc.vn`) thay vì subdomain mặc định (`benhvienabc.ivf.clinic`). Hệ thống tự động xác minh quyền sở hữu domain qua DNS records và cấp SSL certificate tự động qua Let's Encrypt.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    Custom Domain Architecture                           │
│                                                                        │
│  Tenant Admin                                                          │
│   │                                                                    │
│   ├─ 1. Nhập domain vào Branding tab (VD: ivf.benhvienabc.vn)         │
│   │     → Backend: regex validation + uniqueness check                 │
│   │     → Tenant.UpdateBranding() → Status = Pending                  │
│   │     → Generate token: "ivf-verify-{guid}"                         │
│   │                                                                    │
│   ├─ 2. Cấu hình DNS tại registrar:                                   │
│   │     CNAME: ivf.benhvienabc.vn → app.ivf.clinic                    │
│   │     TXT:   _ivf-verify.ivf.benhvienabc.vn → ivf-verify-{token}    │
│   │                                                                    │
│   ├─ 3. Click "Xác minh tên miền"                                     │
│   │     → POST /api/tenants/{id}/domain/verify                        │
│   │     → DnsClient.NET query CNAME + TXT records                      │
│   │     → ✅ TXT match → Status = Verified                            │
│   │     → ❌ TXT mismatch → Status = Failed + chi tiết lỗi            │
│   │                                                                    │
│   └─ 4. Caddy auto-SSL:                                               │
│         → GET /api/tenants/domain-check?domain=ivf.benhvienabc.vn     │
│         → 200 OK (domain verified) → Let's Encrypt cấp cert           │
│         → Tenant truy cập qua HTTPS custom domain ✅                  │
└──────────────────────────────────────────────────────────────────────────┘
```

### 7.2 CustomDomainStatus Enum

| Giá trị    | Mô tả                                             |
| ---------- | ------------------------------------------------- |
| `None`     | Chưa cấu hình custom domain                       |
| `Pending`  | Đã nhập domain, chờ xác minh DNS                  |
| `Verified` | DNS đã xác minh thành công, domain hoạt động      |
| `Failed`   | Xác minh DNS thất bại (CNAME hoặc TXT không đúng) |

### 7.3 DNS Verification (DnsClient.NET)

Hệ thống sử dụng package **DnsClient 1.8.0** (thay thế `System.Net.Dns`) để xác minh DNS chính xác:

```csharp
// IDomainVerificationService (Application Layer)
public interface IDomainVerificationService
{
    Task<DomainVerificationResult> VerifyDomainAsync(
        string domain, string expectedToken, CancellationToken ct = default);
}

public record DomainVerificationResult(
    bool CnameVerified,    // CNAME → app.ivf.clinic?
    bool TxtVerified,      // _ivf-verify.{domain} chứa token?
    string? CnameTarget,   // Target thực tế của CNAME
    List<string> TxtRecords // Tất cả TXT records tìm thấy
);
```

**Quy trình xác minh:**

| Bước | DNS Record   | Query                  | Expected Value       | Mục đích                              |
| ---- | ------------ | ---------------------- | -------------------- | ------------------------------------- |
| 1    | CNAME        | `{domain}`             | `app.ivf.clinic`     | Xác nhận domain trỏ về hệ thống       |
| 1b   | A (fallback) | `{domain}`             | Bất kỳ A record      | Nếu không có CNAME, kiểm tra A record |
| 2    | TXT          | `_ivf-verify.{domain}` | `ivf-verify-{token}` | Xác nhận quyền sở hữu                 |

**Cấu hình DnsClient:**

- `UseCache = false` — Luôn query DNS mới nhất
- `Timeout = 10 giây` — Cho phép DNS server phản hồi
- `Retries = 2` — Retry nếu timeout
- Platform CNAME target: `app.ivf.clinic`

### 7.4 Tenant Resolution qua Custom Domain

`TenantResolutionMiddleware` hỗ trợ resolve tenant từ Host header cho custom domain:

```
HTTP Request → Host: ivf.benhvienabc.vn
  │
  ├─ JWT có tenant_id claim? → Dùng tenant từ JWT
  │
  ├─ Host là default? (localhost, ivf.clinic, *.ivf.clinic) → Skip
  │
  └─ Custom domain:
       ├─ ITenantRepository.GetByCustomDomainAsync("ivf.benhvienabc.vn")
       ├─ Tenant found + CustomDomainStatus == Verified
       │   → SetCurrentTenant(tenantId) ✅
       └─ Tenant not found → No tenant context (anonymous)
```

### 7.5 Caddy Reverse Proxy

**Caddy 2** đóng vai trò reverse proxy với automatic HTTPS:

```
┌──────────────────────────────────────────────────────────────────────┐
│                    Caddy Reverse Proxy                               │
│                                                                      │
│  ┌─ Wildcard SSL ─────────────────────────────────────────────────┐  │
│  │  ivf.clinic + *.ivf.clinic                                     │  │
│  │  → Let's Encrypt wildcard certificate (DNS-01 challenge)       │  │
│  │  → /api/*, /hubs/* → reverse_proxy api:8080                   │  │
│  │  → /* → SPA (/srv/frontend, fallback index.html)              │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ┌─ On-Demand TLS ────────────────────────────────────────────────┐  │
│  │  :443 (catch-all cho custom domains)                           │  │
│  │  → On-Demand TLS: tls { on_demand }                           │  │
│  │  → Ask endpoint: GET /api/tenants/domain-check?domain=X       │  │
│  │     ├─ 200 OK → Caddy cấp cert từ Let's Encrypt               │  │
│  │     └─ 404 → Caddy từ chối, không cấp cert                    │  │
│  │  → Same reverse proxy rules                                   │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  Security Headers (cả 2 blocks):                                    │
│    • HSTS: max-age=63072000, includeSubDomains, preload            │
│    • X-Content-Type-Options: nosniff                                │
│    • X-Frame-Options: DENY                                          │
│    • Referrer-Policy: strict-origin-when-cross-origin               │
│    • X-XSS-Protection: 1; mode=block                                │
│    • Server header: stripped                                        │
└──────────────────────────────────────────────────────────────────────┘
```

**Docker Compose — Caddy Service:**

```yaml
caddy:
  image: caddy:2-alpine
  container_name: ivf-caddy
  profiles: [proxy] # Dev: docker compose --profile proxy up
  environment:
    - ACME_EMAIL=admin@ivf.clinic
    - API_UPSTREAM=api:8080
  ports:
    - "80:80" # HTTP → HTTPS redirect
    - "443:443" # HTTPS (wildcard + on-demand)
  volumes:
    - ./docker/caddy/Caddyfile:/etc/caddy/Caddyfile:ro
    - caddy_data:/data # Let's Encrypt certificates
    - caddy_config:/config # Caddy auto config
  depends_on: [api]
  networks: [ivf-public]
  restart: unless-stopped
  healthcheck:
    test: wget --no-verbose --tries=1 --spider http://localhost:2019/metrics
```

**Production Override (`docker-compose.production.yml`):**

- `profiles: []` — Caddy luôn chạy (không cần `--profile`)
- Mount built Angular SPA: `./ivf-client/dist/ivf-client/browser:/srv/frontend:ro`
- API service: ports bị remove (Caddy xử lý TLS termination)
- Memory limit: 256M

### 7.6 Custom Domain API Endpoints

| Method   | Path                                | Auth              | Mô tả                                                    |
| -------- | ----------------------------------- | ----------------- | -------------------------------------------------------- |
| `POST`   | `/api/tenants/{id}/domain/verify`   | ✅ PlatformAdmin  | Xác minh DNS cho custom domain (CNAME + TXT)             |
| `DELETE` | `/api/tenants/{id}/domain`          | ✅ PlatformAdmin  | Xóa custom domain                                        |
| `GET`    | `/api/tenants/domain-check?domain=` | ❌ AllowAnonymous | Caddy On-Demand TLS callback (200=cấp cert, 404=từ chối) |

### 7.7 Custom Domain Flow — Chi tiết kỹ thuật

```
1. Lưu domain (PUT /api/tenants/{id}/branding)
   ├─ Regex validation: ^(?!-)[a-z0-9-]{1,63}(?<!-)(\.[a-z0-9-]{1,63})*\.[a-z]{2,}$
   ├─ Max 200 ký tự, không chứa scheme/path/port
   ├─ CustomDomainExistsAsync() → đảm bảo unique
   ├─ Tenant.UpdateBranding() → Status = Pending
   └─ Token = "ivf-verify-{Guid:N}"

2. Xác minh (POST /api/tenants/{id}/domain/verify)
   ├─ VerifyCustomDomainCommand → VerifyCustomDomainCommandHandler
   ├─ IDomainVerificationService.VerifyDomainAsync(domain, token)
   │   ├─ DnsClient: QueryAsync(domain, CNAME) → check target
   │   └─ DnsClient: QueryAsync(_ivf-verify.{domain}, TXT) → match token
   ├─ TxtVerified = true → Tenant.VerifyCustomDomain() ✅
   └─ TxtVerified = false → Tenant.FailCustomDomainVerification() ❌

3. Xóa domain (DELETE /api/tenants/{id}/domain)
   ├─ RemoveCustomDomainCommand → RemoveCustomDomainCommandHandler
   └─ Tenant.RemoveCustomDomain() → Clear all 4 fields

4. Auto SSL (Caddy On-Demand TLS)
   ├─ First HTTPS request to custom domain
   ├─ Caddy calls GET /api/tenants/domain-check?domain=X
   ├─ Endpoint checks: tenant exists + CustomDomainStatus == Verified
   ├─ 200 → Caddy obtains Let's Encrypt certificate
   └─ 404 → Caddy rejects, no cert issued
```

---

## 8. Bảo mật & Tuân thủ

### 8.1 Kiến trúc bảo mật

```
┌────────────────────────────────────────────────┐
│              Security Layers                    │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ 1. Authentication (JWT RSA256 + MFA)    │   │
│  │    • Token 60 min, Refresh 7 ngày       │   │
│  │    • TOTP, SMS OTP, Passkey             │   │
│  ├─────────────────────────────────────────┤   │
│  │ 2. Tenant Resolution Middleware         │   │
│  │    • Automatic tenant context setting    │   │
│  │    • Platform admin override via header  │   │
│  ├─────────────────────────────────────────┤   │
│  │ 3. Zero Trust Architecture             │   │
│  │    • Conditional Access Policies         │   │
│  │    • Behavioral Analytics               │   │
│  │    • Geo-fencing & IP whitelist          │   │
│  ├─────────────────────────────────────────┤   │
│  │ 4. Row-Level Data Isolation             │   │
│  │    • EF Core Global Query Filters       │   │
│  │    • Automatic TenantId assignment       │   │
│  ├─────────────────────────────────────────┤   │
│  │ 5. Feature & Limit Enforcement          │   │
│  │    • FeatureGateBehavior (MediatR)       │   │
│  │    • ITenantLimitService (3 limit types) │   │
│  │    • 403 structured error responses      │   │
│  │    • Platform Admin bypass               │   │
│  ├─────────────────────────────────────────┤   │
│  │ 6. Encryption                           │   │
│  │    • AES-256 at rest                     │   │
│  │    • TLS 1.3 in transit                  │   │
│  │    • BCrypt password hashing             │   │
│  ├─────────────────────────────────────────┤   │
│  │ 7. Audit & Compliance                   │   │
│  │    • Partitioned audit log               │   │
│  │    • HIPAA, GDPR, ISO 27001, SOC 2      │   │
│  │    • Automated compliance checks         │   │
│  └─────────────────────────────────────────┘   │
└────────────────────────────────────────────────┘
```

### 8.2 Chứng nhận & Framework

| Framework | Trạng thái | Mô tả                  |
| --------- | ---------- | ---------------------- |
| HIPAA     | ✅ Ready   | Bảo vệ dữ liệu y tế    |
| GDPR      | ✅ Ready   | Bảo vệ dữ liệu cá nhân |
| ISO 27001 | ✅ Ready   | Hệ thống quản lý ATTT  |
| SOC 2     | ✅ Ready   | Controls cho SaaS      |
| PCI DSS   | ✅ Ready   | Bảo mật thanh toán     |
| NIST CSF  | ✅ Ready   | Framework an ninh mạng |

---

## 9. Hiệu suất & Khả năng mở rộng

### 9.1 Kiến trúc kỹ thuật

| Component       | Technology                             | Mục đích                                       |
| --------------- | -------------------------------------- | ---------------------------------------------- |
| Backend         | **.NET 10** (Clean Architecture)       | API nhanh, type-safe, CQRS pattern             |
| Frontend        | **Angular 21** (Standalone Components) | SPA responsive, lazy loading                   |
| Database        | **PostgreSQL 16+**                     | ACID, partitioning, full-text search           |
| Cache           | **Redis**                              | In-memory caching, session store               |
| File Storage    | **MinIO** (S3-compatible)              | Hình ảnh y tế, PDF, documents                  |
| Real-time       | **SignalR**                            | WebSocket cho hàng đợi, thông báo              |
| PDF             | **QuestPDF**                           | Xuất báo cáo, biểu mẫu                         |
| Digital Signing | **EJBCA + SignServer**                 | PKI, chữ ký số hợp pháp                        |
| Reverse Proxy   | **Caddy 2** (Alpine)                   | Auto HTTPS, wildcard SSL, On-Demand TLS        |
| DNS Verify      | **DnsClient.NET 1.8.0**                | Xác minh CNAME + TXT records cho custom domain |

### 9.2 Benchmark hiệu suất

| Metric             | Target        | Ghi chú                         |
| ------------------ | ------------- | ------------------------------- |
| API Response Time  | < 200ms (p95) | Database query + business logic |
| Page Load (First)  | < 3s          | Lazy loading, code splitting    |
| Page Load (Cached) | < 1s          | Service Worker + Redis          |
| Concurrent Users   | 500+/tenant   | Connection pooling              |
| Database Queries   | < 50ms (p95)  | Index optimization, query plans |
| WebSocket Latency  | < 100ms       | SignalR hubs                    |

### 9.3 Khả năng mở rộng

```
                    Load Balancer
                         │
            ┌────────────┼────────────┐
            │            │            │
      ┌─────┴─────┐ ┌───┴────┐ ┌────┴─────┐
      │  API #1   │ │ API #2 │ │  API #3  │
      └─────┬─────┘ └───┬────┘ └────┬─────┘
            │            │            │
      ┌─────┴────────────┴────────────┴─────┐
      │           PostgreSQL (Primary)       │
      │         + Read Replicas (2+)        │
      └─────────────────────────────────────┘
            │
      ┌─────┴─────┐
      │   Redis   │
      │  Cluster  │
      └───────────┘
```

- **Horizontal Scaling**: Thêm API instances khi cần
- **Read Replicas**: PostgreSQL replicas cho read-heavy queries
- **Redis Cluster**: Distributed caching
- **CDN**: Static assets qua CloudFlare/CloudFront
- **Auto-scaling**: Kubernetes HPA

---

## 10. ROI & Lợi ích cho Trung tâm IVF

### 10.1 So sánh chi phí

| Hạng mục             | Tự phát triển    | IVF Platform SaaS  |
| -------------------- | ---------------- | ------------------ |
| Phát triển phần mềm  | 2-5 tỷ VNĐ       | **0₫**             |
| Thời gian triển khai | 12-24 tháng      | **< 1 tuần**       |
| Team kỹ thuật        | 5-10 người       | **0 người** (SaaS) |
| Server/Hosting       | 50-100 triệu/năm | **Bao gồm**        |
| Bảo trì & cập nhật   | 30-50 triệu/năm  | **Bao gồm**        |
| SSL/PKI Certificate  | 10-20 triệu/năm  | **Bao gồm**        |
| **Tổng năm đầu**     | **2.1-5.2 tỷ**   | **60-420 triệu**   |
| **Tổng từ năm 2**    | **90-170 triệu** | **60-420 triệu**   |

### 10.2 Lợi ích vận hành

| Lĩnh vực           | Trước            | Sau khi dùng IVF Platform  |
| ------------------ | ---------------- | -------------------------- |
| Đăng ký bệnh nhân  | 5-10 phút (giấy) | **1-2 phút** (điện tử)     |
| Tra cứu hồ sơ      | 10-15 phút       | **< 5 giây**               |
| Báo cáo tháng      | 2-3 ngày         | **Tự động, real-time**     |
| Xác minh bệnh nhân | Thủ công         | **Sinh trắc học tự động**  |
| Hoá đơn            | Viết tay/Excel   | **Tự động, tích hợp**      |
| Hàng đợi           | Bảng thủ công    | **Real-time, tự động gọi** |

### 10.3 Unique Selling Points

1. **Duy nhất tại Việt Nam**: Hệ thống IVF chuyên biệt, thiết kế riêng cho quy trình IVF/IUI/ICSI/IVM
2. **Multi-tenant**: Mở rộng không giới hạn, mỗi trung tâm độc lập
3. **Custom Domain + Auto SSL**: Tenant dùng domain riêng, SSL tự động qua Let's Encrypt
4. **Ký số hợp pháp**: Tích hợp PKI/SignServer, đáp ứng Nghị định 130/2018/NĐ-CP
5. **Sinh trắc học**: Xác minh bệnh nhân bằng vân tay, tránh nhầm lẫn
6. **AI hỗ trợ**: Gợi ý phác đồ, dự đoán kết quả
7. **Tuân thủ quốc tế**: HIPAA, GDPR sẵn sàng ngay từ đầu
8. **Responsive**: Hoạt động trên PC, tablet, mobile

---

## 11. Lộ trình triển khai

```
Tuần 1: Thiết lập trung tâm, tài khoản admin, cấu hình
        ↓
Tuần 2: Import dữ liệu cũ (nếu có), đào tạo nhân viên
        ↓
Tuần 3: Chạy song song (hệ thống cũ + mới)
        ↓
Tuần 4: Go-live chính thức, hỗ trợ chuyên sâu
        ↓
Sau 30 ngày: Review & tối ưu
```

---

## 12. Thông tin Liên hệ

- **Email**: admin@ivf-platform.vn
- **Website**: https://ivf-platform.vn
- **Demo**: https://demo.ivf-platform.vn
- **Hotline**: 1900-xxxx

---

_Tài liệu này được cập nhật tự động từ hệ thống. Phiên bản: 5.0 — Cập nhật: 2026-03-05_

---

## Phụ lục A: Database Schema — Tenant Tables

### A.1 Bảng `tenants`

| Column                          | Type                                     | Mô tả                                                       |
| ------------------------------- | ---------------------------------------- | ----------------------------------------------------------- |
| `Id`                            | `uuid` PK                                | Tenant ID                                                   |
| `Name`                          | `varchar(200)`                           | Tên trung tâm                                               |
| `Slug`                          | `varchar(100)` UNIQUE                    | URL-friendly slug                                           |
| `Status`                        | `varchar(30)`                            | `Active`, `Trial`, `Suspended`, `Cancelled`, `PendingSetup` |
| `IsolationStrategy`             | `varchar(30)` DEFAULT `'SharedDatabase'` | `SharedDatabase`, `SeparateSchema`, `SeparateDatabase`      |
| `IsRootTenant`                  | `boolean` DEFAULT `false`                | Root/Platform tenant flag                                   |
| `ConnectionString`              | `text` NULL                              | Connection string cho SeparateDatabase                      |
| `DatabaseSchema`                | `varchar(100)` NULL                      | Schema name cho SeparateSchema                              |
| `MaxUsers`                      | `integer`                                | Giới hạn users                                              |
| `MaxPatientsPerMonth`           | `integer`                                | Giới hạn bệnh nhân/tháng                                    |
| `StorageLimitMb`                | `bigint`                                 | Giới hạn lưu trữ (MB)                                       |
| `AiEnabled`                     | `boolean`                                | Feature flag: AI                                            |
| `DigitalSigningEnabled`         | `boolean`                                | Feature flag: Ký số                                         |
| `BiometricsEnabled`             | `boolean`                                | Feature flag: Sinh trắc học                                 |
| `AdvancedReportingEnabled`      | `boolean`                                | Feature flag: Báo cáo nâng cao                              |
| `PrimaryColor`                  | `varchar(10)` NULL                       | Màu thương hiệu                                             |
| `LogoUrl`                       | `text` NULL                              | Logo URL                                                    |
| `CustomDomain`                  | `varchar(255)` NULL UNIQUE (filtered)    | Custom domain (VD: ivf.benhvien.vn)                         |
| `CustomDomainStatus`            | `varchar(30)` DEFAULT `'None'`           | `None`, `Pending`, `Verified`, `Failed`                     |
| `CustomDomainVerifiedAt`        | `timestamp` NULL                         | Thời điểm xác minh DNS thành công                           |
| `CustomDomainVerificationToken` | `varchar(100)` NULL                      | Token xác minh (VD: `ivf-verify-{guid}`)                    |
| `Locale`                        | `varchar(10)`                            | Locale (default: `vi-VN`)                                   |
| `TimeZone`                      | `varchar(50)`                            | Timezone (default: `Asia/Ho_Chi_Minh`)                      |
| `CreatedAt`                     | `timestamp`                              | Ngày tạo                                                    |

### A.2 Bảng `tenant_subscriptions`

| Column            | Type             | Mô tả                                                      |
| ----------------- | ---------------- | ---------------------------------------------------------- |
| `Id`              | `uuid` PK        | Subscription ID                                            |
| `TenantId`        | `uuid` FK        | Liên kết tenant                                            |
| `Plan`            | `varchar(30)`    | `Trial`, `Starter`, `Professional`, `Enterprise`, `Custom` |
| `Status`          | `varchar(30)`    | `Active`, `PastDue`, `Cancelled`, `Expired`, `Suspended`   |
| `BillingCycle`    | `varchar(20)`    | `Monthly`, `Quarterly`, `Annually`                         |
| `MonthlyPrice`    | `decimal`        | Giá hàng tháng                                             |
| `DiscountPercent` | `decimal` NULL   | Phần trăm giảm giá                                         |
| `Currency`        | `varchar(3)`     | `VND`                                                      |
| `StartDate`       | `timestamp`      | Ngày bắt đầu                                               |
| `EndDate`         | `timestamp` NULL | Ngày kết thúc                                              |
| `NextBillingDate` | `timestamp` NULL | Ngày thanh toán kế tiếp                                    |
| `AutoRenew`       | `boolean`        | Tự động gia hạn                                            |

### A.3 Bảng `tenant_usage_records`

| Column            | Type      | Mô tả                   |
| ----------------- | --------- | ----------------------- |
| `Id`              | `uuid` PK | Record ID               |
| `TenantId`        | `uuid` FK | Liên kết tenant         |
| `Year`            | `integer` | Năm                     |
| `Month`           | `integer` | Tháng                   |
| `ActiveUsers`     | `integer` | Số users hoạt động      |
| `NewPatients`     | `integer` | Bệnh nhân mới           |
| `TreatmentCycles` | `integer` | Chu kỳ điều trị         |
| `FormResponses`   | `integer` | Biểu mẫu đã điền        |
| `SignedDocuments` | `integer` | Tài liệu đã ký          |
| `StorageUsedMb`   | `bigint`  | Dung lượng đã dùng (MB) |
| `ApiCalls`        | `bigint`  | Số API calls            |

### A.4 Bảng `feature_definitions`

| Column        | Type             | Mô tả                                           |
| ------------- | ---------------- | ----------------------------------------------- |
| `Id`          | `uuid` PK        | Feature Definition ID                           |
| `Code`        | `varchar(50)` UQ | Machine-readable code (e.g. `ai`, `biometrics`) |
| `DisplayName` | `varchar(200)`   | Tên hiển thị tiếng Việt                         |
| `Description` | `varchar(500)`   | Mô tả chi tiết (hiển thị trên pricing)          |
| `Icon`        | `varchar(20)`    | Emoji hoặc icon identifier                      |
| `Category`    | `varchar(50)`    | `core`, `advanced`, `enterprise`                |
| `SortOrder`   | `integer`        | Thứ tự hiển thị trong category                  |
| `IsActive`    | `boolean`        | Tính năng có sẵn để sử dụng                     |
| `CreatedAt`   | `timestamp`      | Ngày tạo                                        |

### A.5 Bảng `plan_definitions`

| Column                | Type             | Mô tả                                    |
| --------------------- | ---------------- | ---------------------------------------- |
| `Id`                  | `uuid` PK        | Plan Definition ID                       |
| `Plan`                | `varchar(20)` UQ | Tên plan (maps to SubscriptionPlan enum) |
| `DisplayName`         | `varchar(100)`   | Tên hiển thị (e.g. "Chuyên nghiệp")      |
| `Description`         | `varchar(500)`   | Mô tả plan                               |
| `MonthlyPrice`        | `decimal(18,2)`  | Giá hàng tháng (VND)                     |
| `Currency`            | `varchar(3)`     | default `VND`                            |
| `Duration`            | `varchar(50)`    | Text hiển thị (e.g. "Tháng", "30 ngày")  |
| `MaxUsers`            | `integer`        | Giới hạn user                            |
| `MaxPatientsPerMonth` | `integer`        | Giới hạn bệnh nhân/tháng                 |
| `StorageLimitMb`      | `bigint`         | Giới hạn lưu trữ (MB)                    |
| `SortOrder`           | `integer`        | Thứ tự hiển thị trên pricing page        |
| `IsFeatured`          | `boolean`        | Highlight "Phổ biến nhất" badge          |
| `IsActive`            | `boolean`        | Plan có sẵn để bán                       |
| `CreatedAt`           | `timestamp`      | Ngày tạo                                 |

### A.6 Bảng `plan_features` (Join Table)

| Column                | Type        | Mô tả                                       |
| --------------------- | ----------- | ------------------------------------------- |
| `Id`                  | `uuid` PK   | Plan Feature ID                             |
| `PlanDefinitionId`    | `uuid` FK   | → `plan_definitions.Id` (CASCADE DELETE)    |
| `FeatureDefinitionId` | `uuid` FK   | → `feature_definitions.Id` (CASCADE DELETE) |
| `SortOrder`           | `integer`   | Thứ tự hiển thị feature trong plan          |
| `CreatedAt`           | `timestamp` | Ngày tạo                                    |

**Unique Index**: `(PlanDefinitionId, FeatureDefinitionId)` — Mỗi feature chỉ map 1 lần cho mỗi plan.

### A.7 Bảng `tenant_features` (Per-Tenant Activation)

| Column                | Type        | Mô tả                                       |
| --------------------- | ----------- | ------------------------------------------- |
| `Id`                  | `uuid` PK   | Tenant Feature ID                           |
| `TenantId`            | `uuid` FK   | → `tenants.Id` (CASCADE DELETE)             |
| `FeatureDefinitionId` | `uuid` FK   | → `feature_definitions.Id` (CASCADE DELETE) |
| `IsEnabled`           | `boolean`   | Tính năng đã kích hoạt cho tenant           |
| `CreatedAt`           | `timestamp` | Ngày tạo                                    |

**Unique Index**: `(TenantId, FeatureDefinitionId)` — Mỗi feature chỉ có 1 record per tenant.

### A.8 Cột mới trong `menu_items`

| Column                | Type          | Mô tả                                                   |
| --------------------- | ------------- | ------------------------------------------------------- |
| `RequiredFeatureCode` | `varchar(50)` | Feature code cần thiết để hiển thị menu item (nullable) |

### A.9 EF Migrations

| Migration                                     | Mô tả                                                                                                                            |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `20260304110904_AddMultiTenancy`              | Tạo bảng tenants, subscriptions, usage records + ITenantEntity cho 12 entities                                                   |
| `20260304114914_AddTenantIsolationStrategy`   | Thêm `IsolationStrategy`, `IsRootTenant` + set root tenant & platform admin                                                      |
| `20260304161344_AddDynamicFeaturePlanMapping` | Tạo `feature_definitions`, `plan_definitions`, `plan_features`, `tenant_features` + RequiredFeatureCode trên menu_items          |
| `20260305071416_AddCustomDomainVerification`  | Thêm `CustomDomainStatus`, `CustomDomainVerifiedAt`, `CustomDomainVerificationToken` + unique filtered index trên `CustomDomain` |

---

## Phụ lục B: Frontend Components

### B.1 Tenant Management (`/admin/tenants`)

- Danh sách tenants với status badge, plan badge, **isolation badge**
- Tạo tenant mới (tên, slug, email, plan, **isolation strategy**)
- Actions: xem chi tiết, quản lý

### B.2 Tenant Detail (`/admin/tenants/:id`)

6 tabs:

1. **Thông tin** — Name, email, phone, website, tax ID, address
2. **Gói dịch vụ** — Plan, billing cycle, price, discount, dates
3. **Sử dụng** — Monthly usage statistics (users, patients, storage, cycles, API calls)
4. **Thương hiệu** — Logo, primary color, locale + **Custom Domain** (nhập domain, DNS instructions, verify/remove)
5. **Giới hạn** — Max users, patients, storage + feature toggles (AI, signing, biometrics, reporting)
6. **Cô lập dữ liệu** — Current isolation strategy, root tenant badge, schema/connection info, change strategy

**Tab Thương hiệu — Custom Domain UI:**

- Input field: nhập domain (VD: `ivf.your-domain.com`)
- Status badge: màu sắc theo `CustomDomainStatus` (xanh/vàng/đỏ)
- DNS Instructions panel (khi chưa verified):
  - CNAME record: `{domain}` → `app.ivf.clinic`
  - TXT record: `_ivf-verify.{domain}` → hiển thị token
  - Lưu ý: DNS propagation 24-48h
- Nút "Xác minh tên miền" (verify) + "Xóa domain" (remove)
- Thông báo kết quả xác minh (success/error) qua signals

### B.3 Pricing Page (`/pricing`) — Dynamic

- Fetch plans từ `GET /api/tenants/pricing` (database-driven, không hardcode)
- Hiển thị 4 gói (Trial, Starter, Professional, Enterprise) dạng card
- Mỗi card hiển thị: icon + displayName cho mỗi feature (từ `PlanFeatureItem`)
- Badge "Phổ biến nhất" tự động theo `isFeatured` flag từ database
- Billing toggle: Monthly / Quarterly (-5%) / Annually (-15%)
- Anonymous access (không cần đăng nhập)

### B.4 Menu System — Feature-Gated

```
platformAdminOnly  → Chỉ Super Admin thấy
adminOnly          → Tenant Admin + Super Admin
permission-based   → Theo quyền user trong tenant
requiredFeatureCode → Ẩn menu nếu tenant chưa mua feature
```

**Feature-gated flow:**

1. `MainLayoutComponent` load menu từ API + `requiredFeatureCode` per item
2. Load tenant features từ `GET /api/tenants/my-features`
3. `isMenuItemVisible()` check: `enabledFeatures.includes(item.requiredFeatureCode)`
4. Platform Admin bypass tất cả feature gates

### B.5 TypeScript Interfaces

```typescript
// tenant.model.ts — Dynamic pricing response
interface PricingPlan {
  plan: string;
  displayName: string;
  description?: string;
  price: number;
  currency: string;
  duration: string;
  maxUsers: number;
  maxPatients: number;
  storageGb: number;
  isFeatured: boolean;
  features: PlanFeatureItem[]; // Dynamic from database
}

interface PlanFeatureItem {
  code: string;
  displayName: string;
  description?: string;
  icon: string;
  category: string;
}

// Dynamic feature check (replaces old boolean flags)
interface TenantFeatures {
  isPlatformAdmin: boolean;
  enabledFeatures: string[]; // e.g. ["ai", "digital_signing", "biometrics"]
  isolationStrategy: DataIsolationStrategy;
  maxUsers: number;
  maxPatients: number;
}

// Menu item with feature gating
interface MenuItemDto {
  id: string;
  label: string;
  icon: string;
  route: string;
  permission?: string;
  adminOnly: boolean;
  platformAdminOnly: boolean;
  requiredFeatureCode?: string; // Feature gate
}
```

---

## Phụ lục C: Application Layer — CQRS Handlers

### C.1 Pricing Queries (MediatR)

| Query                           | Handler                                | Mô tả                               |
| ------------------------------- | -------------------------------------- | ----------------------------------- |
| `GetDynamicPricingQuery`        | `GetDynamicPricingQueryHandler`        | Active plans + features (public)    |
| `GetTenantDynamicFeaturesQuery` | `GetTenantDynamicFeaturesQueryHandler` | Enabled features for current tenant |
| `GetAllFeatureDefinitionsQuery` | `GetAllFeatureDefinitionsQueryHandler` | All features (admin only)           |

### C.2 Tenant Commands (MediatR)

| Command                        | Handler                               | Feature Sync                                |
| ------------------------------ | ------------------------------------- | ------------------------------------------- |
| `CreateTenantCommand`          | `CreateTenantCommandHandler`          | ✅ `SyncTenantFeaturesFromPlanAsync`        |
| `UpdateSubscriptionCommand`    | `UpdateSubscriptionCommandHandler`    | ✅ `SyncTenantFeaturesFromPlanAsync`        |
| `UpdateTenantIsolationCommand` | `UpdateTenantIsolationCommandHandler` | — (isolation only)                          |
| `UpdateTenantCommand`          | `UpdateTenantCommandHandler`          | — (info only)                               |
| `UpdateBrandingCommand`        | `UpdateBrandingCommandHandler`        | — (branding + custom domain validation)     |
| `VerifyCustomDomainCommand`    | `VerifyCustomDomainCommandHandler`    | DNS verify via `IDomainVerificationService` |
| `RemoveCustomDomainCommand`    | `RemoveCustomDomainCommandHandler`    | — (clear domain fields)                     |

### C.3 Repository Interface

```csharp
// IPricingRepository.cs
public interface IPricingRepository
{
    Task<List<PlanDefinition>> GetActivePlansWithFeaturesAsync(CancellationToken ct = default);
    Task<PlanDefinition?> GetPlanByTypeAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<List<FeatureDefinition>> GetAllFeaturesAsync(bool activeOnly = true, CancellationToken ct = default);
    Task<List<string>> GetTenantFeatureCodesAsync(Guid tenantId, CancellationToken ct = default);
    Task SyncTenantFeaturesFromPlanAsync(Guid tenantId, SubscriptionPlan plan, CancellationToken ct = default);
}

// ITenantRepository.cs (custom domain methods)
public interface ITenantRepository
{
    // ... existing methods ...
    Task<Tenant?> GetByCustomDomainAsync(string domain, CancellationToken ct = default);
    Task<bool> CustomDomainExistsAsync(string domain, Guid? excludeTenantId = null, CancellationToken ct = default);
}
```

---

## Phụ lục D: Seeding Data

### D.1 FeaturePlanSeeder

Chạy tự động khi khởi động ứng dụng trong môi trường Development:

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
{
    await FeaturePlanSeeder.SeedAsync(db);
}
```

**Seeder tạo:**

- 24 FeatureDefinitions (10 core + 9 advanced + 5 enterprise)
- 5 PlanDefinitions (Trial, Starter, Professional, Enterprise, Custom)
- 84 PlanFeature mappings (4 + 13 + 19 + 24 + 24)
- 24 TenantFeatures cho Root Tenant (tất cả enabled)

---

## Phụ lục E: Enforcement Components

### E.1 Backend — Application Layer

| File                                                | Loại      | Mô tả                                                                          |
| --------------------------------------------------- | --------- | ------------------------------------------------------------------------------ |
| `Common/FeatureCodes.cs`                            | Constants | 24 const string mapping feature codes (match `feature_definitions.code`)       |
| `Common/Attributes/RequiresFeatureAttribute.cs`     | Attribute | `[RequiresFeature("code")]` — đánh dấu command cần feature, AllowMultiple=true |
| `Common/Behaviors/FeatureGateBehavior.cs`           | Pipeline  | MediatR behavior đọc `[RequiresFeature]` → gọi `EnsureFeatureEnabledAsync`     |
| `Common/Exceptions/TenantLimitExceededException.cs` | Exception | Properties: `LimitType`, `CurrentCount`, `MaxAllowed`                          |
| `Common/Exceptions/FeatureNotEnabledException.cs`   | Exception | Property: `FeatureCode`                                                        |
| `Common/Interfaces/ITenantLimitService.cs`          | Interface | 4 methods: EnsureUser/Patient/Storage/FeatureEnabled                           |
| `Common/Interfaces/IDomainVerificationService.cs`   | Interface | `VerifyDomainAsync()` + `DomainVerificationResult` record                      |

### E.2 Backend — Infrastructure Layer

| File                                    | Loại           | Mô tả                                                                     |
| --------------------------------------- | -------------- | ------------------------------------------------------------------------- |
| `Services/TenantLimitService.cs`        | Implementation | Effective limits: PlanDef → Tenant → Hard defaults. Platform Admin bypass |
| `Services/DomainVerificationService.cs` | Implementation | DnsClient.NET: CNAME + TXT record verification. Singleton                 |

### E.3 Backend — API Layer

| File         | Loại              | Mô tả                                                        |
| ------------ | ----------------- | ------------------------------------------------------------ |
| `Program.cs` | Exception Handler | `TenantLimitExceededException` → 403 `TENANT_LIMIT_EXCEEDED` |
|              |                   | `FeatureNotEnabledException` → 403 `FEATURE_NOT_ENABLED`     |

### E.4 Frontend — Angular

| File                                            | Loại        | Mô tả                                                                   |
| ----------------------------------------------- | ----------- | ----------------------------------------------------------------------- |
| `core/services/tenant-feature.service.ts`       | Service     | Signal-based state: load/reload/isFeatureEnabled/clear                  |
| `core/guards/feature.guard.ts`                  | Guard       | Factory `featureGuard(code)` → CanActivateFn, redirect on blocked       |
| `core/interceptors/tenant-limit.interceptor.ts` | Interceptor | Catches 403 TENANT_LIMIT_EXCEEDED & FEATURE_NOT_ENABLED → alert (vi-VN) |

### E.5 MediatR Pipeline Order

```
Request
 ├─ 1. ValidationBehavior        (FluentValidation)
 ├─ 2. FeatureGateBehavior       (Feature gating — NEW)
 ├─ 3. VaultPolicyBehavior       (Vault policy checks)
 ├─ 4. ZeroTrustBehavior         (Zero Trust checks)
 ├─ 5. FieldAccessBehavior       (Field-level access)
 └─ 6. Handler                   (Business logic + limit checks)
```

**Idempotent:** Seeder kiểm tra `feature_definitions` đã có data trước khi seed. Safe to run nhiều lần.
