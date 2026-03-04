# IVF Platform — Kiến trúc Multi-Tenant & Bảng Giá SaaS

## Tổng quan Giải pháp

IVF Platform là hệ thống quản lý trung tâm IVF đầu tiên tại Việt Nam hỗ trợ đa trung tâm (Multi-Tenant SaaS). Mỗi trung tâm có dữ liệu cách ly hoàn toàn theo chiến lược cấu hình (Shared DB / Schema riêng / Database riêng). Platform Super Admin (Root Tenant) quản lý toàn bộ hệ thống, còn Tenant Admin chỉ thấy các tính năng vận hành chính.

---

## 1. Kiến trúc Multi-Tenant

### 1.1 Mô hình tổng quan

```
┌─────────────────────────────────────────────────────────────────────┐
│                         IVF Platform                                │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              Root Tenant (Super Admin)                       │   │
│  │  • Quản lý toàn bộ trung tâm (CRUD, Activate, Suspend)     │   │
│  │  • Cấu hình chiến lược cô lập (Isolation Strategy)          │   │
│  │  • Thống kê doanh thu, tuân thủ, bảo mật toàn platform     │   │
│  │  • Quản lý backup, security events, compliance              │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  ┌─ SharedDatabase ──┐  ┌─ SeparateSchema ─┐  ┌ SeparateDB ────┐  │
│  │  Tenant A (Row)   │  │  Tenant B        │  │  Tenant C       │  │
│  │  IVF Hà Nội       │  │  IVF HCM         │  │  IVF Đà Nẵng   │  │
│  │  ───────────────  │  │  Schema: hcm     │  │  DB: ivf_dn     │  │
│  │  TenantId filter  │  │  Isolated tables │  │  Full isolation │  │
│  │  • Users          │  │  • Users         │  │  • Users        │  │
│  │  • Patients       │  │  • Patients      │  │  • Patients     │  │
│  │  • Cycles         │  │  • Cycles        │  │  • Cycles       │  │
│  │  • Invoices       │  │  • Invoices      │  │  • Invoices     │  │
│  │  • Forms          │  │  • Forms         │  │  • Forms        │  │
│  └───────────────────┘  └──────────────────┘  └─────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
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

### 2.3 Feature Flags (Tenant-based)

Endpoint `GET /api/tenants/my-features` trả về feature flags dựa trên vai trò và gói dịch vụ:

```typescript
interface TenantFeatures {
  isPlatformAdmin: boolean; // true = super admin, all features
  canManageTenants: boolean; // CRUD tenants
  canViewPlatformStats: boolean; // Platform-wide statistics
  canManageCompliance: boolean; // Compliance module
  canManageSecurity: boolean; // Security module
  canManageBackups: boolean; // Backup module
  canManageUsers: boolean; // User management
  canManageForms: boolean; // Form builder
  canViewReports: boolean; // Reports
  canUseAi: boolean; // AI features (Professional+)
  canUseDigitalSigning: boolean; // PKI signing (Professional+)
  canUseBiometrics: boolean; // Fingerprint (Enterprise)
  canUseAdvancedReporting: boolean; // Advanced reports (Starter+)
  isolationStrategy: DataIsolationStrategy;
  maxUsers: number;
  maxPatients: number;
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

| Method | Path                   | Mô tả                 | Auth         |
| ------ | ---------------------- | --------------------- | ------------ |
| `GET`  | `/api/tenants/pricing` | Bảng giá SaaS (4 gói) | ❌ Anonymous |

### 3.2 Authenticated Endpoints

| Method | Path                       | Mô tả                          | Auth   |
| ------ | -------------------------- | ------------------------------ | ------ |
| `GET`  | `/api/tenants/my-features` | Feature flags theo user/tenant | ✅ JWT |

### 3.3 Platform Admin Only Endpoints

| Method | Path                             | Mô tả                        | Auth             |
| ------ | -------------------------------- | ---------------------------- | ---------------- |
| `GET`  | `/api/tenants`                   | Danh sách tenants            | ✅ PlatformAdmin |
| `GET`  | `/api/tenants/{id}`              | Chi tiết tenant              | ✅ PlatformAdmin |
| `POST` | `/api/tenants`                   | Tạo tenant mới               | ✅ PlatformAdmin |
| `PUT`  | `/api/tenants/{id}`              | Cập nhật thông tin           | ✅ PlatformAdmin |
| `PUT`  | `/api/tenants/{id}/branding`     | Cập nhật branding            | ✅ PlatformAdmin |
| `PUT`  | `/api/tenants/{id}/limits`       | Cập nhật giới hạn & features | ✅ PlatformAdmin |
| `PUT`  | `/api/tenants/{id}/isolation`    | Thay đổi chiến lược cô lập   | ✅ PlatformAdmin |
| `PUT`  | `/api/tenants/{id}/subscription` | Cập nhật subscription        | ✅ PlatformAdmin |
| `POST` | `/api/tenants/{id}/activate`     | Kích hoạt tenant             | ✅ PlatformAdmin |
| `POST` | `/api/tenants/{id}/suspend`      | Tạm ngưng tenant             | ✅ PlatformAdmin |
| `POST` | `/api/tenants/{id}/cancel`       | Hủy tenant                   | ✅ PlatformAdmin |
| `GET`  | `/api/tenants/stats`             | Thống kê toàn platform       | ✅ PlatformAdmin |

---

## 4. Bảng Giá SaaS

### 4.1 So sánh Gói dịch vụ

| Tính năng              | Trial        | Starter        | Professional    | Enterprise      |
| ---------------------- | ------------ | -------------- | --------------- | --------------- |
| **Giá/tháng**          | **Miễn phí** | **5,000,000₫** | **15,000,000₫** | **35,000,000₫** |
| **Thời gian**          | 30 ngày      | Không giới hạn | Không giới hạn  | Không giới hạn  |
| Người dùng             | 3            | 10             | 30              | 100             |
| Bệnh nhân/tháng        | 20           | 100            | 500             | 2,000           |
| Lưu trữ                | 512 MB       | 5 GB           | 20 GB           | 100 GB          |
| Quản lý BN             | ✅           | ✅             | ✅              | ✅              |
| Lịch hẹn               | ✅           | ✅             | ✅              | ✅              |
| Hàng đợi               | ✅           | ✅             | ✅              | ✅              |
| Biểu mẫu cơ bản        | ✅           | ✅             | ✅              | ✅              |
| Báo cáo nâng cao       | ❌           | ✅             | ✅              | ✅              |
| Export PDF             | ❌           | ✅             | ✅              | ✅              |
| AI hỗ trợ              | ❌           | ❌             | ✅              | ✅              |
| Ký số (PKI)            | ❌           | ❌             | ✅              | ✅              |
| HIPAA/GDPR             | ❌           | ❌             | ✅              | ✅              |
| Sinh trắc học          | ❌           | ❌             | ❌              | ✅              |
| SSO/SAML               | ❌           | ❌             | ❌              | ✅              |
| SLA 99.9%              | ❌           | ❌             | ❌              | ✅              |
| Hỗ trợ 24/7            | ❌           | ❌             | ❌              | ✅              |
| Custom domain          | ❌           | ❌             | ❌              | ✅              |
| Hỗ trợ email           | ❌           | ✅             | ✅ (ưu tiên)    | ✅ (24/7)       |
| **Isolation mặc định** | SharedDB     | SharedDB       | SeparateSchema  | SeparateDB      |

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

## 5. Bảo mật & Tuân thủ

### 5.1 Kiến trúc bảo mật

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
│  │ 5. Encryption                           │   │
│  │    • AES-256 at rest                     │   │
│  │    • TLS 1.3 in transit                  │   │
│  │    • BCrypt password hashing             │   │
│  ├─────────────────────────────────────────┤   │
│  │ 6. Audit & Compliance                   │   │
│  │    • Partitioned audit log               │   │
│  │    • HIPAA, GDPR, ISO 27001, SOC 2      │   │
│  │    • Automated compliance checks         │   │
│  └─────────────────────────────────────────┘   │
└────────────────────────────────────────────────┘
```

### 5.2 Chứng nhận & Framework

| Framework | Trạng thái | Mô tả                  |
| --------- | ---------- | ---------------------- |
| HIPAA     | ✅ Ready   | Bảo vệ dữ liệu y tế    |
| GDPR      | ✅ Ready   | Bảo vệ dữ liệu cá nhân |
| ISO 27001 | ✅ Ready   | Hệ thống quản lý ATTT  |
| SOC 2     | ✅ Ready   | Controls cho SaaS      |
| PCI DSS   | ✅ Ready   | Bảo mật thanh toán     |
| NIST CSF  | ✅ Ready   | Framework an ninh mạng |

---

## 6. Hiệu suất & Khả năng mở rộng

### 6.1 Kiến trúc kỹ thuật

| Component       | Technology                             | Mục đích                             |
| --------------- | -------------------------------------- | ------------------------------------ |
| Backend         | **.NET 10** (Clean Architecture)       | API nhanh, type-safe, CQRS pattern   |
| Frontend        | **Angular 21** (Standalone Components) | SPA responsive, lazy loading         |
| Database        | **PostgreSQL 16+**                     | ACID, partitioning, full-text search |
| Cache           | **Redis**                              | In-memory caching, session store     |
| File Storage    | **MinIO** (S3-compatible)              | Hình ảnh y tế, PDF, documents        |
| Real-time       | **SignalR**                            | WebSocket cho hàng đợi, thông báo    |
| PDF             | **QuestPDF**                           | Xuất báo cáo, biểu mẫu               |
| Digital Signing | **EJBCA + SignServer**                 | PKI, chữ ký số hợp pháp              |

### 6.2 Benchmark hiệu suất

| Metric             | Target        | Ghi chú                         |
| ------------------ | ------------- | ------------------------------- |
| API Response Time  | < 200ms (p95) | Database query + business logic |
| Page Load (First)  | < 3s          | Lazy loading, code splitting    |
| Page Load (Cached) | < 1s          | Service Worker + Redis          |
| Concurrent Users   | 500+/tenant   | Connection pooling              |
| Database Queries   | < 50ms (p95)  | Index optimization, query plans |
| WebSocket Latency  | < 100ms       | SignalR hubs                    |

### 6.3 Khả năng mở rộng

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

## 7. ROI & Lợi ích cho Trung tâm IVF

### 7.1 So sánh chi phí

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

### 7.2 Lợi ích vận hành

| Lĩnh vực           | Trước            | Sau khi dùng IVF Platform  |
| ------------------ | ---------------- | -------------------------- |
| Đăng ký bệnh nhân  | 5-10 phút (giấy) | **1-2 phút** (điện tử)     |
| Tra cứu hồ sơ      | 10-15 phút       | **< 5 giây**               |
| Báo cáo tháng      | 2-3 ngày         | **Tự động, real-time**     |
| Xác minh bệnh nhân | Thủ công         | **Sinh trắc học tự động**  |
| Hoá đơn            | Viết tay/Excel   | **Tự động, tích hợp**      |
| Hàng đợi           | Bảng thủ công    | **Real-time, tự động gọi** |

### 7.3 Unique Selling Points

1. **Duy nhất tại Việt Nam**: Hệ thống IVF chuyên biệt, thiết kế riêng cho quy trình IVF/IUI/ICSI/IVM
2. **Multi-tenant**: Mở rộng không giới hạn, mỗi trung tâm độc lập
3. **Ký số hợp pháp**: Tích hợp PKI/SignServer, đáp ứng Nghị định 130/2018/NĐ-CP
4. **Sinh trắc học**: Xác minh bệnh nhân bằng vân tay, tránh nhầm lẫn
5. **AI hỗ trợ**: Gợi ý phác đồ, dự đoán kết quả
6. **Tuân thủ quốc tế**: HIPAA, GDPR sẵn sàng ngay từ đầu
7. **Responsive**: Hoạt động trên PC, tablet, mobile

---

## 8. Lộ trình triển khai

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

## 9. Thông tin Liên hệ

- **Email**: admin@ivf-platform.vn
- **Website**: https://ivf-platform.vn
- **Demo**: https://demo.ivf-platform.vn
- **Hotline**: 1900-xxxx

---

_Tài liệu này được cập nhật tự động từ hệ thống. Phiên bản: 2.0 — Cập nhật: 2026-03-04_

---

## Phụ lục A: Database Schema — Tenant Tables

### A.1 Bảng `tenants`

| Column                     | Type                                     | Mô tả                                                       |
| -------------------------- | ---------------------------------------- | ----------------------------------------------------------- |
| `Id`                       | `uuid` PK                                | Tenant ID                                                   |
| `Name`                     | `varchar(200)`                           | Tên trung tâm                                               |
| `Slug`                     | `varchar(100)` UNIQUE                    | URL-friendly slug                                           |
| `Status`                   | `varchar(30)`                            | `Active`, `Trial`, `Suspended`, `Cancelled`, `PendingSetup` |
| `IsolationStrategy`        | `varchar(30)` DEFAULT `'SharedDatabase'` | `SharedDatabase`, `SeparateSchema`, `SeparateDatabase`      |
| `IsRootTenant`             | `boolean` DEFAULT `false`                | Root/Platform tenant flag                                   |
| `ConnectionString`         | `text` NULL                              | Connection string cho SeparateDatabase                      |
| `DatabaseSchema`           | `varchar(100)` NULL                      | Schema name cho SeparateSchema                              |
| `MaxUsers`                 | `integer`                                | Giới hạn users                                              |
| `MaxPatientsPerMonth`      | `integer`                                | Giới hạn bệnh nhân/tháng                                    |
| `StorageLimitMb`           | `bigint`                                 | Giới hạn lưu trữ (MB)                                       |
| `AiEnabled`                | `boolean`                                | Feature flag: AI                                            |
| `DigitalSigningEnabled`    | `boolean`                                | Feature flag: Ký số                                         |
| `BiometricsEnabled`        | `boolean`                                | Feature flag: Sinh trắc học                                 |
| `AdvancedReportingEnabled` | `boolean`                                | Feature flag: Báo cáo nâng cao                              |
| `PrimaryColor`             | `varchar(10)` NULL                       | Màu thương hiệu                                             |
| `LogoUrl`                  | `text` NULL                              | Logo URL                                                    |
| `CustomDomain`             | `varchar(255)` NULL                      | Custom domain                                               |
| `Locale`                   | `varchar(10)`                            | Locale (default: `vi-VN`)                                   |
| `TimeZone`                 | `varchar(50)`                            | Timezone (default: `Asia/Ho_Chi_Minh`)                      |
| `CreatedAt`                | `timestamp`                              | Ngày tạo                                                    |

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

### A.4 EF Migrations

| Migration                                   | Mô tả                                                                          |
| ------------------------------------------- | ------------------------------------------------------------------------------ |
| `20260304110904_AddMultiTenancy`            | Tạo bảng tenants, subscriptions, usage records + ITenantEntity cho 12 entities |
| `20260304114914_AddTenantIsolationStrategy` | Thêm `IsolationStrategy`, `IsRootTenant` + set root tenant & platform admin    |

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
4. **Thương hiệu** — Logo, primary color, custom domain, locale
5. **Giới hạn** — Max users, patients, storage + feature toggles (AI, signing, biometrics, reporting)
6. **Cô lập dữ liệu** — Current isolation strategy, root tenant badge, schema/connection info, change strategy

### B.3 Pricing Page (`/pricing`)

- 4 gói dịch vụ hiển thị dạng card
- So sánh tính năng, giá cả
- Anonymous access (không cần đăng nhập)

### B.4 Menu System

```
platformAdminOnly → Chỉ Super Admin thấy
adminOnly         → Tenant Admin + Super Admin
permission-based  → Theo quyền user trong tenant
```
