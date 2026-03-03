# Quản lý Bệnh nhân Nâng cao (Enterprise Patient Management)

> Tài liệu kỹ thuật chi tiết cho module quản lý bệnh nhân enterprise-grade trong hệ thống IVF Information System.

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
3. [Domain Model](#3-domain-model)
4. [API Endpoints](#4-api-endpoints)
5. [CQRS — Commands](#5-cqrs--commands)
6. [CQRS — Queries](#6-cqrs--queries)
7. [Repository Layer](#7-repository-layer)
8. [Frontend Architecture](#8-frontend-architecture)
9. [Phân tích & Báo cáo (Analytics)](#9-phân-tích--báo-cáo-analytics)
10. [Nhật ký thay đổi (Audit Trail)](#10-nhật-ký-thay-đổi-audit-trail)
11. [GDPR & Tuân thủ quy định](#11-gdpr--tuân-thủ-quy-định)
12. [Quản lý rủi ro & Ưu tiên](#12-quản-lý-rủi-ro--ưu-tiên)
13. [Quy trình nghiệp vụ (Workflows)](#13-quy-trình-nghiệp-vụ-workflows)
14. [Tích hợp UI](#14-tích-hợp-ui)
15. [Bảo mật & Phân quyền](#15-bảo-mật--phân-quyền)
16. [Hướng dẫn phát triển](#16-hướng-dẫn-phát-triển)

---

## 1. Tổng quan

Module quản lý bệnh nhân là trung tâm của hệ thống IVF, cung cấp:

- **Quản lý hồ sơ toàn diện**: Thông tin cá nhân, nhân khẩu học, bảo hiểm, liên hệ khẩn cấp
- **Tuân thủ GDPR/HIPAA**: Quản lý đồng ý (consent), ẩn danh hóa dữ liệu, quy định lưu trữ
- **Phân tích enterprise**: Dashboard thống kê, xu hướng đăng ký, phân bố nhân khẩu học
- **Nhật ký kiểm toán (Audit Trail)**: Theo dõi mọi thay đổi trên hồ sơ bệnh nhân
- **Đánh giá rủi ro**: Phân loại rủi ro 4 cấp (Low → Critical), ghi chú rủi ro
- **Ưu tiên bệnh nhân**: Normal, High, VIP, Emergency
- **Tìm kiếm nâng cao**: Lọc đa chiều, sắp xếp linh hoạt, phân trang
- **Tích hợp sinh trắc học**: Vân tay, ảnh chân dung (DigitalPersona SDK)
- **Ký số & Lưu trữ**: Hồ sơ bệnh án ký số, lưu trữ MinIO (S3-compatible)

### Sơ đồ module

```
┌──────────────────────────────────────────────────────────────┐
│                     Patient Management                        │
├──────────┬──────────┬──────────┬──────────┬─────────────────┤
│  Profile │ Analytics│  Audit   │ Consent  │  Biometrics     │
│  CRUD    │ Dashboard│  Trail   │ GDPR     │  Fingerprint    │
├──────────┼──────────┼──────────┼──────────┼─────────────────┤
│  Risk    │ Follow-up│ Data     │ Emergency│  Documents      │
│  Mgmt    │ Tracking │ Retention│ Contact  │  S3 Storage     │
└──────────┴──────────┴──────────┴──────────┴─────────────────┘
```

---

## 2. Kiến trúc hệ thống

### Clean Architecture — Luồng phụ thuộc

```
┌─────────────────────────────────────────────┐
│                  API Layer                    │
│  PatientEndpoints.cs (18 endpoints)          │
│  → MediatR pipeline → CQRS                  │
├─────────────────────────────────────────────┤
│              Application Layer                │
│  Commands: 11 commands + handlers            │
│  Queries:  7 queries + handlers              │
│  Interfaces: IPatientRepository              │
│  Validators: FluentValidation                │
├─────────────────────────────────────────────┤
│            Infrastructure Layer               │
│  PatientRepository.cs (EF Core)              │
│  IvfDbContext (PostgreSQL)                    │
│  Auto-migration on startup                   │
├─────────────────────────────────────────────┤
│               Domain Layer                    │
│  Patient.cs (Aggregate Root)                 │
│  Enums: Gender, PatientType, PatientStatus,  │
│         RiskLevel, PatientPriority, BloodType│
└─────────────────────────────────────────────┘
```

### Vị trí các file chính

| Layer          | File                                                                | Mô tả                            |
| -------------- | ------------------------------------------------------------------- | -------------------------------- |
| Domain         | `src/IVF.Domain/Entities/Patient.cs`                                | Entity aggregate root            |
| Domain         | `src/IVF.Domain/Enums/Enums.cs`                                     | Các enum nghiệp vụ               |
| Application    | `src/IVF.Application/Features/Patients/Commands/PatientCommands.cs` | Commands + handlers + validators |
| Application    | `src/IVF.Application/Features/Patients/Queries/PatientQueries.cs`   | Queries + handlers + DTOs        |
| Application    | `src/IVF.Application/Common/Interfaces/IPatientRepository.cs`       | Repository interface             |
| Infrastructure | `src/IVF.Infrastructure/Repositories/PatientRepository.cs`          | EF Core implementation           |
| API            | `src/IVF.API/Endpoints/PatientEndpoints.cs`                         | Minimal API endpoints            |
| Frontend       | `ivf-client/src/app/core/models/patient.models.ts`                  | TypeScript interfaces            |
| Frontend       | `ivf-client/src/app/core/services/patient.service.ts`               | HTTP service                     |
| Frontend       | `ivf-client/src/app/features/patients/`                             | Angular components               |

---

## 3. Domain Model

### 3.1. Entity: Patient (Aggregate Root)

```csharp
public class Patient : BaseEntity
{
    // === Định danh ===
    string PatientCode          // Auto: "BN-{yyyy}-{000001}"
    string FullName
    DateTime DateOfBirth
    Gender Gender               // Male | Female

    // === Phân loại ===
    PatientType PatientType     // Infertility | EggDonor | SpermDonor
    PatientStatus Status        // Active | Inactive | Discharged | Transferred | Deceased | Anonymized | Suspended
    PatientPriority Priority    // Normal | High | VIP | Emergency
    RiskLevel RiskLevel         // Low | Medium | High | Critical
    string? RiskNotes

    // === Liên hệ ===
    string? IdentityNumber      // CCCD/CMND
    string? Phone
    string? Email
    string? Address

    // === Nhân khẩu học ===
    string? Ethnicity           // Dân tộc
    string? Nationality         // Quốc tịch
    string? Occupation          // Nghề nghiệp

    // === Bảo hiểm ===
    string? InsuranceNumber
    string? InsuranceProvider

    // === Y tế ===
    BloodType? BloodType        // A+/A-/B+/B-/AB+/AB-/O+/O-
    string? Allergies           // Dị ứng
    string? MedicalNotes        // Ghi chú y khoa

    // === Liên hệ khẩn cấp ===
    string? EmergencyContactName
    string? EmergencyContactPhone
    string? EmergencyContactRelation

    // === Giới thiệu ===
    string? ReferralSource
    Guid? ReferringDoctorId

    // === Đồng ý & GDPR ===
    bool ConsentDataProcessing          // Đồng ý xử lý dữ liệu
    DateTime? ConsentDataProcessingDate
    bool ConsentResearch                // Đồng ý nghiên cứu
    DateTime? ConsentResearchDate
    bool ConsentMarketing               // Đồng ý marketing
    DateTime? ConsentMarketingDate
    DateTime? DataRetentionExpiryDate   // Hạn lưu trữ dữ liệu
    bool IsAnonymized                   // Đã ẩn danh hóa
    DateTime? AnonymizedAt

    // === Theo dõi hoạt động ===
    DateTime? LastVisitDate
    int TotalVisits
    string? Tags                // Nhãn phân loại
    string? Notes               // Ghi chú nội bộ

    // === Quan hệ ===
    PatientPhoto? Photo                          // 1:1
    ICollection<PatientFingerprint> Fingerprints  // 1:N
    ICollection<Couple> AsWife                    // 1:N
    ICollection<Couple> AsHusband                 // 1:N
    ICollection<QueueTicket> QueueTickets          // 1:N
}
```

### 3.2. Domain Methods (Encapsulated Business Logic)

| Method                                      | Mô tả                                      | Validation                         |
| ------------------------------------------- | ------------------------------------------ | ---------------------------------- |
| `Patient.Create(...)`                       | Factory tạo bệnh nhân mới                  | PatientCode, FullName bắt buộc     |
| `.Update(fullName, phone, address)`         | Cập nhật thông tin cơ bản                  | —                                  |
| `.UpdateDemographics(...)`                  | Cập nhật nhân khẩu học, bảo hiểm, nhóm máu | —                                  |
| `.UpdateEmergencyContact(...)`              | Cập nhật liên hệ khẩn cấp                  | —                                  |
| `.UpdateConsent(data, research, marketing)` | Cập nhật đồng ý GDPR                       | Auto-set timestamp                 |
| `.SetRiskLevel(level, notes)`               | Đánh giá rủi ro                            | Enum validation                    |
| `.SetPriority(priority)`                    | Thiết lập mức ưu tiên                      | Enum validation                    |
| `.ChangeStatus(newStatus)`                  | Chuyển trạng thái                          | State machine                      |
| `.RecordVisit()`                            | Ghi nhận lượt khám                         | TotalVisits++, LastVisitDate = now |
| `.SetMedicalNotes(notes)`                   | Cập nhật ghi chú y khoa                    | —                                  |
| `.UpdateTags(tags)`                         | Cập nhật nhãn                              | —                                  |
| `.SetNotes(notes)`                          | Cập nhật ghi chú                           | —                                  |
| `.Anonymize()`                              | Ẩn danh hóa (GDPR)                         | Xóa PII, set IsAnonymized          |
| `.SetDataRetentionExpiry(date)`             | Thiết lập hạn lưu trữ GDPR                 | —                                  |

### 3.3. Enums

```csharp
enum Gender           { Male, Female }
enum PatientType      { Infertility, EggDonor, SpermDonor }
enum PatientStatus    { Active, Inactive, Discharged, Transferred, Deceased, Anonymized, Suspended }
enum PatientPriority  { Normal, High, VIP, Emergency }
enum RiskLevel        { Low, Medium, High, Critical }
enum BloodType        { APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative }
```

### 3.4. Mã bệnh nhân (Patient Code)

- **Format**: `BN-{yyyy}-{NNNNNN}` (ví dụ: `BN-2026-000001`)
- **Auto-generated**: Tự động tăng dần theo năm hiện tại
- **Unique index**: Đảm bảo không trùng lặp
- **Algorithm**: Đếm số bệnh nhân hiện có + 1, format 6 ký tự

---

## 4. API Endpoints

**Base route**: `/api/patients` (yêu cầu xác thực JWT)

### 4.1. CRUD Cơ bản

| HTTP   | Endpoint     | Mô tả                     | Request Body                       | Response                  |
| ------ | ------------ | ------------------------- | ---------------------------------- | ------------------------- |
| GET    | `/`          | Tìm kiếm bệnh nhân        | Query: `q, gender, page, pageSize` | `PagedResult<PatientDto>` |
| GET    | `/{id:guid}` | Lấy chi tiết bệnh nhân    | —                                  | `PatientDto`              |
| POST   | `/`          | Tạo bệnh nhân mới         | `CreatePatientCommand`             | `PatientDto` (201)        |
| PUT    | `/{id:guid}` | Cập nhật thông tin cơ bản | `UpdatePatientCommand`             | `PatientDto`              |
| DELETE | `/{id:guid}` | Xóa mềm bệnh nhân         | —                                  | `200 OK`                  |

### 4.2. Thông tin Chi tiết

| HTTP | Endpoint                  | Mô tả                     | Request Body                       |
| ---- | ------------------------- | ------------------------- | ---------------------------------- |
| PUT  | `/{id}/demographics`      | Cập nhật nhân khẩu học    | `UpdatePatientDemographicsCommand` |
| PUT  | `/{id}/emergency-contact` | Cập nhật liên hệ khẩn cấp | `UpdateEmergencyContactCommand`    |
| PUT  | `/{id}/medical-notes`     | Cập nhật ghi chú y khoa   | `UpdatePatientMedicalNotesCommand` |

### 4.3. Tuân thủ & An toàn

| HTTP | Endpoint             | Mô tả                    |
| ---- | -------------------- | ------------------------ |
| PUT  | `/{id}/consent`      | Cập nhật đồng ý GDPR     |
| PUT  | `/{id}/risk`         | Đánh giá mức rủi ro      |
| PUT  | `/{id}/status`       | Thay đổi trạng thái      |
| POST | `/{id}/record-visit` | Ghi nhận lượt khám       |
| POST | `/{id}/anonymize`    | Ẩn danh hóa hồ sơ (GDPR) |

### 4.4. Tìm kiếm & Phân tích

| HTTP | Endpoint                  | Mô tả                        |
| ---- | ------------------------- | ---------------------------- |
| GET  | `/search/advanced`        | Tìm kiếm nâng cao đa chiều   |
| GET  | `/analytics`              | Dashboard thống kê tổng quan |
| GET  | `/{id}/audit-trail`       | Nhật ký thay đổi hồ sơ       |
| GET  | `/follow-up?days=90`      | Bệnh nhân cần tái khám       |
| GET  | `/data-retention/expired` | Hồ sơ hết hạn lưu trữ (GDPR) |

### 4.5. Ví dụ Request/Response

#### Tạo bệnh nhân mới

```http
POST /api/patients
Content-Type: application/json
Authorization: Bearer <jwt-token>

{
  "fullName": "Nguyễn Thị Hoa",
  "dateOfBirth": "1990-05-15",
  "gender": "Female",
  "patientType": "Infertility",
  "identityNumber": "079090123456",
  "phone": "0901234567",
  "address": "123 Nguyễn Huệ, Q1, TP.HCM",
  "email": "hoa.nguyen@email.com",
  "ethnicity": "Kinh",
  "nationality": "Việt Nam",
  "emergencyContactName": "Trần Văn A",
  "emergencyContactPhone": "0912345678",
  "emergencyContactRelation": "Chồng"
}
```

**Response (201 Created)**:

```json
{
  "id": "a1b2c3d4-...",
  "patientCode": "BN-2026-000021",
  "fullName": "Nguyễn Thị Hoa",
  "dateOfBirth": "1990-05-15T00:00:00Z",
  "gender": "Female",
  "patientType": "Infertility",
  "status": "Active",
  "riskLevel": "Low",
  "priority": "Normal",
  "totalVisits": 0,
  "isAnonymized": false,
  "createdAt": "2026-03-03T08:00:00Z"
}
```

#### Tìm kiếm nâng cao

```http
GET /api/patients/search/advanced?q=Nguyễn&gender=Female&patientType=Infertility&status=Active&riskLevel=High&sortBy=lastvisit&sortDescending=true&page=1&pageSize=20
```

**Response**:

```json
{
  "items": [ { "id": "...", "patientCode": "BN-2026-000001", "fullName": "Nguyễn Thị Hoa", ... } ],
  "totalCount": 5,
  "page": 1,
  "pageSize": 20
}
```

#### Lấy phân tích

```http
GET /api/patients/analytics
```

**Response**:

```json
{
  "totalPatients": 20,
  "activePatients": 18,
  "inactivePatients": 2,
  "byGender": { "Male": 10, "Female": 10 },
  "byType": { "Infertility": 18, "EggDonor": 1, "SpermDonor": 1 },
  "byAgeGroup": { "<20": 0, "20-24": 2, "25-29": 5, "30-34": 8, "35-39": 3, "40-44": 1, "45+": 1 },
  "byRiskLevel": { "Low": 15, "Medium": 3, "High": 1, "Critical": 1 },
  "registrationTrend": { "2025-04": 2, "2025-05": 3, "2025-06": 1, ... "2026-03": 4 },
  "recentPatients": [ { "patientCode": "BN-2026-000020", ... }, ... ]
}
```

---

## 5. CQRS — Commands

### 5.1. Danh sách Commands

| #   | Command                            | Mô tả                                                   | Trả về               |
| --- | ---------------------------------- | ------------------------------------------------------- | -------------------- |
| 1   | `CreatePatientCommand`             | Tạo bệnh nhân mới với đầy đủ thông tin                  | `Result<PatientDto>` |
| 2   | `UpdatePatientCommand`             | Cập nhật FullName, Phone, Address                       | `Result<PatientDto>` |
| 3   | `UpdatePatientDemographicsCommand` | Email, dân tộc, nghề nghiệp, bảo hiểm, nhóm máu, dị ứng | `Result<PatientDto>` |
| 4   | `UpdateEmergencyContactCommand`    | Tên, SĐT, quan hệ liên hệ khẩn cấp                      | `Result<PatientDto>` |
| 5   | `UpdatePatientConsentCommand`      | 3 loại consent + auto timestamp                         | `Result<PatientDto>` |
| 6   | `SetPatientRiskCommand`            | RiskLevel + RiskNotes                                   | `Result<PatientDto>` |
| 7   | `ChangePatientStatusCommand`       | Chuyển trạng thái (state machine)                       | `Result<PatientDto>` |
| 8   | `RecordPatientVisitCommand`        | Ghi nhận lượt khám                                      | `Result<PatientDto>` |
| 9   | `AnonymizePatientCommand`          | Ẩn danh hóa GDPR                                        | `Result`             |
| 10  | `UpdatePatientMedicalNotesCommand` | Cập nhật ghi chú y khoa                                 | `Result<PatientDto>` |
| 11  | `DeletePatientCommand`             | Xóa mềm (soft delete)                                   | `Result`             |

### 5.2. CreatePatientCommand — Chi tiết

```csharp
public record CreatePatientCommand(
    string FullName,
    DateTime DateOfBirth,
    string Gender,
    string PatientType,
    // Liên hệ
    string? IdentityNumber,
    string? Phone,
    string? Email,
    string? Address,
    // Nhân khẩu học
    string? Ethnicity,
    string? Nationality,
    string? Occupation,
    // Bảo hiểm
    string? InsuranceNumber,
    string? InsuranceProvider,
    // Y tế
    string? BloodType,
    string? Allergies,
    // Liên hệ khẩn cấp
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    // Giới thiệu
    string? ReferralSource,
    Guid? ReferringDoctorId
) : IRequest<Result<PatientDto>>;
```

### 5.3. Validation (FluentValidation)

```csharp
// CreatePatientValidator
RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateTime.UtcNow);
RuleFor(x => x.Gender).NotEmpty().Must(g => g == "Male" || g == "Female");
RuleFor(x => x.PatientType).NotEmpty();
RuleFor(x => x.Phone).MaximumLength(20);
RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
RuleFor(x => x.IdentityNumber).MaximumLength(20);
```

### 5.4. Command Handler Flow

```
CreatePatientCommand
  │
  ├─ FluentValidation Pipeline → 400 Bad Request nếu lỗi
  │
  ├─ CreatePatientHandler
  │   ├─ _patientRepository.GenerateCodeAsync()
  │   │   └─ "BN-2026-000021"
  │   ├─ Patient.Create(code, fullName, dob, gender, type, identity, phone, address)
  │   ├─ patient.UpdateDemographics(email, ethnicity, ...)
  │   ├─ patient.UpdateEmergencyContact(name, phone, relation)
  │   ├─ _patientRepository.AddAsync(patient)
  │   └─ _unitOfWork.SaveChangesAsync()
  │
  └─ Return PatientDto.FromEntity(patient) → 201 Created
```

---

## 6. CQRS — Queries

### 6.1. Danh sách Queries

| #   | Query                          | Mô tả                          | Trả về                              |
| --- | ------------------------------ | ------------------------------ | ----------------------------------- |
| 1   | `GetPatientByIdQuery`          | Lấy chi tiết 1 bệnh nhân       | `Result<PatientDto>`                |
| 2   | `SearchPatientsQuery`          | Tìm kiếm cơ bản (tên, mã, SĐT) | `Result<PagedResult<PatientDto>>`   |
| 3   | `AdvancedSearchPatientsQuery`  | Tìm kiếm nâng cao đa chiều     | `Result<PagedResult<PatientDto>>`   |
| 4   | `GetPatientAnalyticsQuery`     | Dashboard thống kê             | `Result<PatientAnalyticsDto>`       |
| 5   | `GetPatientAuditTrailQuery`    | Nhật ký kiểm toán              | `Result<PatientAuditTrailDto>`      |
| 6   | `GetPatientsFollowUpQuery`     | Bệnh nhân cần tái khám         | `Result<IReadOnlyList<PatientDto>>` |
| 7   | `GetExpiredDataRetentionQuery` | Hồ sơ hết hạn lưu trữ          | `Result<IReadOnlyList<PatientDto>>` |

### 6.2. AdvancedSearchPatientsQuery — Bộ lọc

```csharp
public record AdvancedSearchPatientsQuery(
    string? Query,            // Tìm theo tên, mã BN, SĐT
    string? Gender,           // Male | Female
    PatientType? PatientType, // Infertility | EggDonor | SpermDonor
    PatientStatus? Status,    // Active | Inactive | ...
    PatientPriority? Priority,// Normal | High | VIP | Emergency
    RiskLevel? RiskLevel,     // Low | Medium | High | Critical
    string? BloodType,        // A+ | A- | B+ | ...
    DateTime? DobFrom,        // Lọc ngày sinh từ
    DateTime? DobTo,          // Lọc ngày sinh đến
    DateTime? CreatedFrom,    // Lọc ngày tạo từ
    DateTime? CreatedTo,      // Lọc ngày tạo đến
    string? SortBy,           // name | code | dob | lastvisit | totalvisits
    bool SortDescending,      // true = giảm dần
    int Page,
    int PageSize
) : IRequest<Result<PagedResult<PatientDto>>>;
```

### 6.3. PatientDto — Data Transfer Object

```csharp
public record PatientDto(
    Guid Id,
    string PatientCode,
    string FullName,
    DateTime DateOfBirth,
    string Gender,
    string PatientType,
    string? IdentityNumber,
    string? Phone,
    string? Email,
    string? Address,
    string Status,
    // Nhân khẩu học
    string? Ethnicity,
    string? Nationality,
    string? Occupation,
    string? InsuranceNumber,
    string? InsuranceProvider,
    string? BloodType,
    string? Allergies,
    // Liên hệ khẩn cấp
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    // Giới thiệu
    string? ReferralSource,
    Guid? ReferringDoctorId,
    string? MedicalNotes,
    // Consent
    bool ConsentDataProcessing,
    DateTime? ConsentDataProcessingDate,
    bool ConsentResearch,
    DateTime? ConsentResearchDate,
    bool ConsentMarketing,
    DateTime? ConsentMarketingDate,
    // Rủi ro & Ưu tiên
    string RiskLevel,
    string? RiskNotes,
    string Priority,
    // Hoạt động
    DateTime? LastVisitDate,
    int TotalVisits,
    string? Tags,
    string? Notes,
    bool IsAnonymized,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
```

### 6.4. PatientAnalyticsDto

```csharp
public record PatientAnalyticsDto(
    int TotalPatients,
    int ActivePatients,
    int InactivePatients,
    Dictionary<string, int> ByGender,         // { "Male": 10, "Female": 10 }
    Dictionary<string, int> ByType,           // { "Infertility": 18, ... }
    Dictionary<string, int> ByAgeGroup,       // { "<20": 0, "20-24": 2, ... }
    Dictionary<string, int> ByRiskLevel,      // { "Low": 15, "High": 1, ... }
    Dictionary<string, int> RegistrationTrend,// { "2026-01": 5, "2026-02": 3, ... }
    IReadOnlyList<PatientDto> RecentPatients  // 10 bệnh nhân gần nhất
);
```

---

## 7. Repository Layer

### 7.1. Interface: IPatientRepository

```csharp
public interface IPatientRepository
{
    // === CRUD ===
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Patient?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Patient> AddAsync(Patient patient, CancellationToken ct = default);
    Task UpdateAsync(Patient patient, CancellationToken ct = default);
    Task<string> GenerateCodeAsync(CancellationToken ct = default);

    // === Tìm kiếm ===
    Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, string? gender, int page, int pageSize, CancellationToken ct);

    Task<(IReadOnlyList<Patient> Items, int Total)> AdvancedSearchAsync(
        string? query, string? gender, PatientType? patientType, PatientStatus? status,
        PatientPriority? priority, RiskLevel? riskLevel, string? bloodType,
        DateTime? dobFrom, DateTime? dobTo, DateTime? createdFrom, DateTime? createdTo,
        string? sortBy, bool sortDescending, int page, int pageSize, CancellationToken ct);

    // === Analytics ===
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(PatientStatus status, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByGenderAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByTypeAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByAgeGroupAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsByRiskLevelAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetPatientsRegistrationTrendAsync(int months, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> GetRecentPatientsAsync(int count, CancellationToken ct = default);

    // === GDPR & Follow-up ===
    Task<IReadOnlyList<Patient>> GetPatientsRequiringFollowUpAsync(int daysSinceLastVisit, CancellationToken ct = default);
    Task<IReadOnlyList<Patient>> GetExpiredDataRetentionAsync(CancellationToken ct = default);
}
```

### 7.2. Triển khai (Implementation Details)

#### Tìm kiếm cơ bản

```csharp
// Tìm theo FullName, PatientCode, hoặc Phone (Contains, case-insensitive)
// Lọc Gender nếu có
// Sắp xếp: CreatedAt giảm dần (mới nhất trước)
// Phân trang: Skip((page-1)*pageSize).Take(pageSize)
```

#### Tìm kiếm nâng cao — Bộ lọc

```
1. Query text     → FullName | PatientCode | Phone (Contains)
2. Gender         → Exact enum match
3. PatientType    → Exact enum match
4. Status         → Exact enum match
5. Priority       → Exact enum match
6. RiskLevel      → Exact enum match
7. BloodType      → String match
8. DobFrom/DobTo  → DateOfBirth range
9. CreatedFrom/To → CreatedAt range

Tất cả filter áp dụng AND logic.
```

#### Sắp xếp (Sort Options)

| `sortBy`      | Cột sắp xếp   | Mặc định |
| ------------- | ------------- | -------- |
| `name`        | FullName      | —        |
| `code`        | PatientCode   | —        |
| `dob`         | DateOfBirth   | —        |
| `lastvisit`   | LastVisitDate | —        |
| `totalvisits` | TotalVisits   | —        |
| _(không set)_ | CreatedAt     | ✅ Desc  |

#### Phân nhóm tuổi (Age Buckets)

```
<20 | 20-24 | 25-29 | 30-34 | 35-39 | 40-44 | 45+
```

#### Xu hướng đăng ký (Registration Trend)

```
// Lấy N tháng gần nhất, group by Year-Month
// Output: { "2025-04": 2, "2025-05": 3, ..., "2026-03": 4 }
```

---

## 8. Frontend Architecture

### 8.1. Cấu trúc Components

```
ivf-client/src/app/features/patients/
├── patient-list/               # Danh sách bệnh nhân
│   ├── patient-list.component.ts
│   ├── patient-list.component.html
│   └── patient-list.component.scss
├── patient-form/               # Form tạo/chỉnh sửa
│   ├── patient-form.component.ts
│   ├── patient-form.component.html
│   └── patient-form.component.scss
├── patient-detail/             # Chi tiết bệnh nhân
│   ├── patient-detail.component.ts
│   ├── patient-detail.component.html
│   └── patient-detail.component.scss
├── patient-analytics/          # Dashboard phân tích  ← MỚI
│   ├── patient-analytics.component.ts
│   ├── patient-analytics.component.html
│   └── patient-analytics.component.scss
├── patient-audit-trail/        # Nhật ký kiểm toán    ← MỚI
│   ├── patient-audit-trail.component.ts
│   ├── patient-audit-trail.component.html
│   └── patient-audit-trail.component.scss
├── patient-biometrics/         # Sinh trắc học
│   ├── patient-biometrics.component.ts
│   ├── patient-biometrics.component.html
│   └── patient-biometrics.component.scss
└── patient-documents/          # Hồ sơ bệnh án
    ├── patient-documents.component.ts
    ├── patient-documents.component.html
    └── patient-documents.component.scss
```

### 8.2. Routing (Lazy-loaded)

```typescript
// app.routes.ts — Thứ tự quan trọng: static paths TRƯỚC dynamic paths

{ path: 'patients',            component: PatientListComponent }
{ path: 'patients/new',        component: PatientFormComponent }
{ path: 'patients/analytics',  component: PatientAnalyticsComponent }   // ← PHẢI trước :id
{ path: 'patients/:id',        component: PatientDetailComponent }
{ path: 'patients/:id/biometrics',  component: PatientBiometricsComponent }
{ path: 'patients/:id/documents',   component: PatientDocumentsComponent }
{ path: 'patients/:id/audit-trail', component: PatientAuditTrailComponent }
```

> **Lưu ý quan trọng**: Route `patients/analytics` PHẢI được khai báo **trước** `patients/:id`, nếu không Angular sẽ match "analytics" như một `:id` parameter.

### 8.3. TypeScript Models

```typescript
// === Enums & Types ===
type PatientType = "Infertility" | "EggDonor" | "SpermDonor";
type PatientStatus =
  | "Active"
  | "Inactive"
  | "Discharged"
  | "Transferred"
  | "Deceased"
  | "Anonymized"
  | "Suspended";
type PatientPriority = "Normal" | "High" | "VIP" | "Emergency";
type RiskLevel = "Low" | "Medium" | "High" | "Critical";
type BloodType =
  | "APositive"
  | "ANegative"
  | "BPositive"
  | "BNegative"
  | "ABPositive"
  | "ABNegative"
  | "OPositive"
  | "ONegative";

// === Display Constants (Vietnamese labels) ===
PATIENT_TYPE_LABELS: Record<string, string>; // { Infertility: 'Hiếm muộn', ... }
PATIENT_STATUS_LABELS: Record<string, string>; // { Active: 'Đang theo dõi', ... }
PATIENT_STATUS_COLORS: Record<string, string>; // { Active: '#22c55e', ... }
PRIORITY_LABELS: Record<string, string>; // { Normal: 'Bình thường', ... }
PRIORITY_COLORS: Record<string, string>; // { Normal: '#6b7280', ... }
RISK_LEVEL_LABELS: Record<string, string>; // { Low: 'Thấp', ... }
RISK_LEVEL_COLORS: Record<string, string>; // { Low: '#22c55e', ... }
BLOOD_TYPE_LABELS: Record<string, string>; // { APositive: 'A+', ... }
```

> **Quy ước TypeScript**: Tất cả label/color maps sử dụng `Record<string, string>` thay vì `Record<EnumType, string>` để tương thích với `Object.entries()` trong template Angular.

### 8.4. Patient Service

```typescript
@Injectable({ providedIn: "root" })
export class PatientService {
  // CRUD
  searchPatients(
    query?,
    page?,
    pageSize?,
    gender?,
  ): Observable<PatientListResponse>;
  getPatient(id: string): Observable<Patient>;
  createPatient(patient: Partial<Patient>): Observable<Patient>;
  updatePatient(id, patient): Observable<Patient>;
  deletePatient(id): Observable<void>;

  // Tìm kiếm nâng cao
  advancedSearch(
    params: PatientAdvancedSearchParams,
  ): Observable<PatientListResponse>;

  // Nhân khẩu học & Liên hệ
  updateDemographics(request: UpdateDemographicsRequest): Observable<Patient>;
  updateEmergencyContact(patientId, request): Observable<Patient>;
  updateMedicalNotes(patientId, medicalNotes): Observable<Patient>;

  // Consent & GDPR
  updateConsent(patientId, request: UpdateConsentRequest): Observable<Patient>;
  anonymizePatient(patientId): Observable<void>;

  // Rủi ro & Trạng thái
  setRiskLevel(patientId, request: SetRiskRequest): Observable<Patient>;
  changeStatus(patientId, status: PatientStatus): Observable<Patient>;
  recordVisit(patientId): Observable<void>;

  // Phân tích
  getAnalytics(): Observable<PatientAnalytics>;

  // Kiểm toán & Tuân thủ
  getAuditTrail(patientId, page?, pageSize?): Observable<PatientAuditTrail>;
  getFollowUpPatients(days?): Observable<Patient[]>;
  getExpiredDataRetention(): Observable<Patient[]>;
}
```

---

## 9. Phân tích & Báo cáo (Analytics)

### 9.1. Analytics Dashboard

Component `PatientAnalyticsComponent` (standalone, OnPush) cung cấp 4 tab:

#### Tab 1: Tổng quan (Overview)

- **KPI Cards**: Tổng BN, Đang theo dõi, Không hoạt động, Cần theo dõi lại
- **Biểu đồ xu hướng**: Đăng ký 12 tháng gần nhất (bar chart)
- **Phân bố giới tính**: Nam/Nữ với tỷ lệ phần trăm
- **Bảng bệnh nhân gần nhất**: 10 BN đăng ký gần nhất

#### Tab 2: Nhân khẩu học (Demographics)

- Phân bố theo nhóm tuổi (7 nhóm)
- Phân bố theo loại bệnh nhân
- Phân bố theo nhóm máu

#### Tab 3: Tuân thủ & Rủi ro (Compliance)

- Phân bố theo mức rủi ro (4 cấp với color-coded)
- Tỷ lệ đồng ý GDPR (data processing, research, marketing)
- Cảnh báo bệnh nhân rủi ro cao

#### Tab 4: Theo dõi & Lưu trữ (Follow-up)

- Danh sách BN cần tái khám (> 90 ngày chưa khám)
- Danh sách hồ sơ hết hạn lưu trữ GDPR
- Nút hành động: Liên hệ, Ẩn danh hóa

### 9.2. Signals & State Management

```typescript
// Reactive signals (Angular 17+)
analytics = signal<PatientAnalytics | null>(null);
loading = signal(true);
followUpPatients = signal<Patient[]>([]);
expiredRetention = signal<Patient[]>([]);
activeTab = signal<"overview" | "demographics" | "compliance" | "followup">(
  "overview",
);
```

### 9.3. API Calls Flow

```
PatientAnalyticsComponent
  │
  ├─ ngOnInit()
  │   └─ loadAnalytics() → GET /api/patients/analytics
  │
  ├─ Tab "Theo dõi" clicked
  │   ├─ loadFollowUp()  → GET /api/patients/follow-up?days=90
  │   └─ loadExpiredRetention() → GET /api/patients/data-retention/expired
  │
  └─ "Làm mới" button → loadAnalytics() + clear caches
```

---

## 10. Nhật ký thay đổi (Audit Trail)

### 10.1. Cơ chế hoạt động

PostgreSQL audit log tự động ghi nhận mọi thay đổi trên bảng `patients` thông qua EF Core change tracking và audit middleware.

### 10.2. Cấu trúc AuditEntry

```typescript
interface PatientAuditEntry {
  id: string; // Unique ID
  action: string; // "Create" | "Update" | "Delete" | "StatusChange" | ...
  username?: string; // Người thực hiện
  oldValues?: string; // JSON giá trị cũ
  newValues?: string; // JSON giá trị mới
  changedColumns?: string; // Các cột thay đổi (comma-separated)
  ipAddress?: string; // IP address người thực hiện
  createdAt: string; // Thời điểm thay đổi (ISO 8601)
}
```

### 10.3. Audit Trail Component

```typescript
// Standalone, phân trang, collapsible entries
auditTrail = signal<PatientAuditTrail | null>(null);
page = signal(1);
pageSize = 20;
expandedEntry = signal<string | null>(null); // Toggle expand/collapse

// Methods
loadAuditTrail(); // GET /api/patients/{id}/audit-trail?page=1&pageSize=20
toggleEntry(id); // Expand/collapse chi tiết thay đổi
getActionLabel(action); // Map action → Vietnamese label
parseJson(str); // Parse oldValues/newValues JSON
nextPage() / prevPage(); // Phân trang
```

### 10.4. Hiển thị

```
📋 Nhật ký thay đổi — BN-2026-000001
├─ [03/03/2026 08:30] Cập nhật rủi ro — admin
│   Thay đổi: RiskLevel, RiskNotes
│   Cũ: { "RiskLevel": "Low" }
│   Mới: { "RiskLevel": "High", "RiskNotes": "Tiền sử..." }
├─ [02/03/2026 15:00] Cập nhật nhân khẩu — doctor1
│   Thay đổi: Email, BloodType
│   Cũ: { "Email": null }
│   Mới: { "Email": "hoa@email.com", "BloodType": "APositive" }
└─ [01/03/2026 10:00] Tạo hồ sơ — reception1
    Giá trị: { "FullName": "Nguyễn Thị Hoa", ... }
```

---

## 11. GDPR & Tuân thủ quy định

### 11.1. Quản lý đồng ý (Consent Management)

| Loại đồng ý   | Field                   | Timestamp Field             | Mô tả                         |
| ------------- | ----------------------- | --------------------------- | ----------------------------- |
| Xử lý dữ liệu | `ConsentDataProcessing` | `ConsentDataProcessingDate` | Bắt buộc để xử lý hồ sơ       |
| Nghiên cứu    | `ConsentResearch`       | `ConsentResearchDate`       | Sử dụng cho nghiên cứu y khoa |
| Marketing     | `ConsentMarketing`      | `ConsentMarketingDate`      | Gửi thông tin quảng cáo       |

**API cập nhật consent**:

```http
PUT /api/patients/{id}/consent
{
  "consentDataProcessing": true,
  "consentResearch": false,
  "consentMarketing": false
}
```

Hệ thống tự động ghi timestamp khi consent được bật (`DateTime.UtcNow`).

### 11.2. Ẩn danh hóa (Anonymization)

**Quy trình ẩn danh hóa GDPR**:

```
POST /api/patients/{id}/anonymize
  │
  ├─ Patient.Anonymize()
  │   ├─ FullName = "ANONYMIZED"
  │   ├─ Phone = null
  │   ├─ Email = null
  │   ├─ Address = null
  │   ├─ IdentityNumber = null
  │   ├─ IsAnonymized = true
  │   └─ AnonymizedAt = DateTime.UtcNow
  │
  ├─ Status → Anonymized
  │
  └─ Audit log ghi nhận anonymization event
```

> **Lưu ý**: Ẩn danh hóa là **không thể đảo ngược**. Dữ liệu cá nhân sẽ bị xóa vĩnh viễn.

### 11.3. Quản lý thời hạn lưu trữ (Data Retention)

```csharp
// Thiết lập hạn lưu trữ
patient.SetDataRetentionExpiry(DateTime.UtcNow.AddYears(5));

// Query hồ sơ hết hạn
GET /api/patients/data-retention/expired
// Trả về danh sách BN có DataRetentionExpiryDate <= UtcNow && !IsAnonymized
```

**Workflow xử lý hết hạn**:

```
1. Scheduled job kiểm tra hồ sơ hết hạn (hàng ngày)
2. Thông báo admin danh sách hồ sơ cần xử lý
3. Admin review → Gia hạn hoặc Ẩn danh hóa
4. Audit trail ghi nhận quyết định
```

### 11.4. Bệnh nhân cần tái khám (Follow-up)

```http
GET /api/patients/follow-up?days=90
// Bệnh nhân Active có LastVisitDate > 90 ngày trước
// Giới hạn: 100 bệnh nhân
```

---

## 12. Quản lý rủi ro & Ưu tiên

### 12.1. Mức rủi ro (Risk Levels)

| Mức        | Enum       | Màu          | Mô tả                                         |
| ---------- | ---------- | ------------ | --------------------------------------------- |
| Thấp       | `Low`      | 🟢 `#22c55e` | Bệnh nhân bình thường, không có yếu tố rủi ro |
| Trung bình | `Medium`   | 🟡 `#f59e0b` | Có một số yếu tố cần lưu ý                    |
| Cao        | `High`     | 🔴 `#ef4444` | Nhiều yếu tố rủi ro, cần theo dõi chặt        |
| Nguy hiểm  | `Critical` | 🔴 `#dc2626` | Cần can thiệp ngay, cảnh báo toàn hệ thống    |

**API đánh giá rủi ro**:

```http
PUT /api/patients/{id}/risk
{
  "riskLevel": "High",
  "riskNotes": "Tiền sử sảy thai 3 lần, tuổi > 40, BMI > 30"
}
```

### 12.2. Mức ưu tiên (Priority Levels)

| Mức         | Enum        | Màu          | Mô tả               |
| ----------- | ----------- | ------------ | ------------------- |
| Bình thường | `Normal`    | ⚪ `#6b7280` | Xử lý theo thứ tự   |
| Cao         | `High`      | 🟡 `#f59e0b` | Ưu tiên xử lý trước |
| VIP         | `VIP`       | 🟣 `#8b5cf6` | Bệnh nhân VIP       |
| Cấp cứu     | `Emergency` | 🔴 `#ef4444` | Xử lý ngay lập tức  |

### 12.3. Trạng thái bệnh nhân (Patient Status)

```
                    ┌──────────┐
                    │  Active  │ ← Trạng thái mặc định khi tạo
                    └────┬─────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
   ┌──────────┐   ┌──────────┐   ┌────────────┐
   │ Inactive │   │Discharged│   │ Transferred│
   └──────────┘   └──────────┘   └────────────┘
         │
         ▼
   ┌──────────┐        ┌───────────┐
   │ Suspended│        │  Deceased │
   └──────────┘        └───────────┘

   Bất kỳ trạng thái nào → ┌────────────┐
                            │ Anonymized │ (GDPR, không thể đảo ngược)
                            └────────────┘
```

| Trạng thái      | Enum          | Mô tả                                      |
| --------------- | ------------- | ------------------------------------------ |
| Đang theo dõi   | `Active`      | Bệnh nhân đang trong chương trình điều trị |
| Không hoạt động | `Inactive`    | Tạm ngừng, chưa có lịch hẹn tiếp theo      |
| Xuất viện       | `Discharged`  | Kết thúc điều trị                          |
| Chuyển viện     | `Transferred` | Đã chuyển sang cơ sở y tế khác             |
| Tử vong         | `Deceased`    | —                                          |
| Đã ẩn danh      | `Anonymized`  | Dữ liệu cá nhân đã bị xóa (GDPR)           |
| Tạm đình chỉ    | `Suspended`   | Tạm dừng dịch vụ (vi phạm quy định)        |

---

## 13. Quy trình nghiệp vụ (Workflows)

### 13.1. Đăng ký bệnh nhân mới

```
1. Lễ Ttan nhập thông tin cơ bản (Họ tên, ngày sinh, giới tính, loại BN)
2. Hệ thống tự động tạo mã BN: BN-{yyyy}-{NNNNNN}
3. Cập nhật nhân khẩu học bổ sung (email, dân tộc, bảo hiểm...)
4. Thu thập đồng ý xử lý dữ liệu (GDPR)
5. Chụp ảnh chân dung & lấy vân tay (sinh trắc học)
6. Trạng thái: Active, RiskLevel: Low, Priority: Normal
```

### 13.2. Khám định kỳ

```
1. Bệnh nhân check-in tại quầy lễ tân → QueueTicket
2. Gọi API: POST /api/patients/{id}/record-visit
3. TotalVisits++ & LastVisitDate = now
4. Bác sĩ cập nhật ghi chú y khoa: PUT /api/patients/{id}/medical-notes
5. Đánh giá rủi ro nếu cần: PUT /api/patients/{id}/risk
```

### 13.3. Xuất báo cáo tháng

```
1. Truy cập Analytics Dashboard: /patients/analytics
2. Tab "Tổng quan": Số liệu KPI (tổng BN, BN mới, xu hướng)
3. Tab "Nhân khẩu học": Phân bố tuổi, giới tính, loại BN
4. Tab "Tuân thủ & Rủi ro": Mức rủi ro, tỷ lệ consent
5. Tab "Theo dõi": Kiểm tra BN cần tái khám, hồ sơ hết hạn
```

### 13.4. Kiểm toán hồ sơ

```
1. Mở chi tiết bệnh nhân: /patients/{id}
2. Click "📋 Nhật ký hoạt động" trong Thao tác nhanh
3. Xem timeline thay đổi: /patients/{id}/audit-trail
4. Expand entry để xem giá trị cũ/mới (JSON diff)
5. Phân trang nếu nhiều entries (20 entries/page)
```

---

## 14. Tích hợp UI

### 14.1. Danh sách bệnh nhân (Patient List)

Header chứa 2 nút hành động:

```html
<div class="header-actions">
  <a routerLink="/patients/analytics" class="btn btn-outline">📊 Phân tích</a>
  <button (click)="openCreateForm()" class="btn btn-primary">
    ➕ Thêm bệnh nhân
  </button>
</div>
```

### 14.2. Chi tiết bệnh nhân (Patient Detail)

4 thao tác nhanh (Quick Actions):

| Icon | Hành động           | Route                       | Mô tả                    |
| ---- | ------------------- | --------------------------- | ------------------------ |
| 📷   | Sinh trắc học       | `/patients/:id/biometrics`  | Ảnh chân dung & vân tay  |
| 📁   | Hồ sơ bệnh án       | `/patients/:id/documents`   | Tài liệu, file ký số, S3 |
| 📋   | Nhật ký hoạt động   | `/patients/:id/audit-trail` | Lịch sử thay đổi         |
| 📊   | Phân tích bệnh nhân | `/patients/analytics`       | Thống kê, xu hướng       |

### 14.3. Navigation Links

```
patient-list ──[📊 Phân tích]──→ patient-analytics
  ↑                                    │
  └────[← Danh sách bệnh nhân]────────┘

patient-detail ──[📋 Nhật ký]──→ patient-audit-trail
  ↑                                     │
  └─────[← Chi tiết bệnh nhân]─────────┘
```

### 14.4. Sidebar Menu

Menu "📊 Phân tích BN" được thêm vào sidebar navigation (route: `/patients/analytics`, permission: `ViewPatients`), nằm sau mục "👥 Bệnh nhân".

---

## 15. Bảo mật & Phân quyền

### 15.1. Xác thực

Tất cả patient endpoints yêu cầu JWT Bearer token:

```
Authorization: Bearer <jwt-token>
```

Pipeline xác thực: VaultToken → ApiKey → JWT Bearer

### 15.2. Phân quyền RBAC

| Quyền              | Endpoint                                   | Vai trò mặc định         |
| ------------------ | ------------------------------------------ | ------------------------ |
| `ViewPatients`     | GET `/`, `/{id}`, `/search/advanced`       | Tất cả                   |
| `CreatePatient`    | POST `/`                                   | Admin, Reception, Doctor |
| `EditPatient`      | PUT `/{id}`, PUT `/{id}/demographics`, ... | Admin, Doctor, Nurse     |
| `DeletePatient`    | DELETE `/{id}`                             | Admin                    |
| `AnonymizePatient` | POST `/{id}/anonymize`                     | Admin                    |
| `ViewAnalytics`    | GET `/analytics`                           | Admin, Doctor            |
| `ViewAuditTrail`   | GET `/{id}/audit-trail`                    | Admin                    |

### 15.3. Audit Trail tự động

Mọi thay đổi trên hồ sơ bệnh nhân đều được ghi lại:

- **Who**: Username của người thực hiện
- **What**: Các cột thay đổi, giá trị cũ/mới
- **When**: Timestamp UTC
- **Where**: IP address

---

## 16. Hướng dẫn phát triển

### 16.1. Thêm Command mới

```csharp
// 1. Tạo command record
public record MyNewPatientCommand(Guid PatientId, ...) : IRequest<Result<PatientDto>>;

// 2. Tạo handler
public class MyNewPatientHandler(
    IPatientRepository patientRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<MyNewPatientCommand, Result<PatientDto>>
{
    public async Task<Result<PatientDto>> Handle(MyNewPatientCommand request, CancellationToken ct)
    {
        var patient = await patientRepository.GetByIdAsync(request.PatientId, ct);
        if (patient is null) return Result.Failure<PatientDto>("Patient not found");
        // ... domain logic ...
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(PatientDto.FromEntity(patient));
    }
}

// 3. Thêm FluentValidation validator
public class MyNewPatientValidator : AbstractValidator<MyNewPatientCommand> { ... }

// 4. Đăng ký endpoint
group.MapPut("/{id:guid}/my-action", async (Guid id, ..., ISender sender) =>
{
    var result = await sender.Send(new MyNewPatientCommand(id, ...));
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
});
```

### 16.2. Thêm Analytics Metric mới

```csharp
// 1. Thêm method vào IPatientRepository
Task<Dictionary<string, int>> GetPatientsByNewMetricAsync(CancellationToken ct);

// 2. Implement trong PatientRepository
public async Task<Dictionary<string, int>> GetPatientsByNewMetricAsync(CancellationToken ct)
    => await db.Patients
        .Where(p => !p.IsDeleted)
        .GroupBy(p => p.NewField)
        .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count(), ct);

// 3. Thêm vào PatientAnalyticsDto
public record PatientAnalyticsDto(..., Dictionary<string, int> ByNewMetric);

// 4. Cập nhật GetPatientAnalyticsHandler
var byNewMetric = await _patientRepository.GetPatientsByNewMetricAsync(ct);
// ... thêm vào DTO

// 5. Frontend: Cập nhật PatientAnalytics interface & template
```

### 16.3. Quy ước Code

| Quy ước                             | Mô tả                                                 |
| ----------------------------------- | ----------------------------------------------------- |
| Tất cả query dùng `.AsNoTracking()` | Tối ưu performance cho read-only                      |
| Filter `!p.IsDeleted`               | Luôn loại bỏ bản ghi đã xóa mềm                       |
| Frontend: `Record<string, string>`  | Cho label/color maps (tương thích `Object.entries()`) |
| Signals cho state                   | Angular 17+ signals thay vì BehaviorSubject           |
| ChangeDetectionStrategy.OnPush      | Performance optimization                              |
| Standalone components               | Không dùng NgModules                                  |
| Routes: static trước dynamic        | `/patients/analytics` TRƯỚC `/patients/:id`           |

### 16.4. Testing

```bash
# Backend unit tests
dotnet test --filter "FullyQualifiedName~Patient"

# Frontend unit tests
cd ivf-client && npm test -- --grep "Patient"

# Build verification
dotnet build                         # Backend: 0 errors
cd ivf-client && npx ng build       # Frontend: 0 errors
```

---

## Phụ lục

### A. Database Schema

```sql
CREATE TABLE patients (
    "Id"                        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PatientCode"               VARCHAR(20) NOT NULL UNIQUE,
    "FullName"                  VARCHAR(200) NOT NULL,
    "DateOfBirth"               TIMESTAMP WITH TIME ZONE NOT NULL,
    "Gender"                    INTEGER NOT NULL,        -- 0=Male, 1=Female
    "PatientType"               INTEGER NOT NULL DEFAULT 0,
    "Status"                    INTEGER NOT NULL DEFAULT 0,
    "Priority"                  INTEGER NOT NULL DEFAULT 0,
    "RiskLevel"                 INTEGER NOT NULL DEFAULT 0,
    "RiskNotes"                 TEXT,
    "IdentityNumber"            VARCHAR(20),
    "Phone"                     VARCHAR(20),
    "Email"                     VARCHAR(200),
    "Address"                   TEXT,
    "Ethnicity"                 VARCHAR(100),
    "Nationality"               VARCHAR(100),
    "Occupation"                VARCHAR(200),
    "InsuranceNumber"           VARCHAR(50),
    "InsuranceProvider"         VARCHAR(200),
    "BloodType"                 INTEGER,
    "Allergies"                 TEXT,
    "MedicalNotes"              TEXT,
    "EmergencyContactName"      VARCHAR(200),
    "EmergencyContactPhone"     VARCHAR(20),
    "EmergencyContactRelation"  VARCHAR(100),
    "ReferralSource"            VARCHAR(200),
    "ReferringDoctorId"         UUID,
    "ConsentDataProcessing"     BOOLEAN NOT NULL DEFAULT FALSE,
    "ConsentDataProcessingDate" TIMESTAMP WITH TIME ZONE,
    "ConsentResearch"           BOOLEAN NOT NULL DEFAULT FALSE,
    "ConsentResearchDate"       TIMESTAMP WITH TIME ZONE,
    "ConsentMarketing"          BOOLEAN NOT NULL DEFAULT FALSE,
    "ConsentMarketingDate"      TIMESTAMP WITH TIME ZONE,
    "DataRetentionExpiryDate"   TIMESTAMP WITH TIME ZONE,
    "IsAnonymized"              BOOLEAN NOT NULL DEFAULT FALSE,
    "AnonymizedAt"              TIMESTAMP WITH TIME ZONE,
    "LastVisitDate"             TIMESTAMP WITH TIME ZONE,
    "TotalVisits"               INTEGER NOT NULL DEFAULT 0,
    "Tags"                      TEXT,
    "Notes"                     TEXT,
    "CreatedAt"                 TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt"                 TIMESTAMP WITH TIME ZONE,
    "IsDeleted"                 BOOLEAN NOT NULL DEFAULT FALSE
);

-- Indexes
CREATE UNIQUE INDEX ix_patients_code ON patients ("PatientCode");
CREATE INDEX ix_patients_fullname ON patients ("FullName");
CREATE INDEX ix_patients_phone ON patients ("Phone");
CREATE INDEX ix_patients_status ON patients ("Status") WHERE "IsDeleted" = FALSE;
CREATE INDEX ix_patients_type ON patients ("PatientType") WHERE "IsDeleted" = FALSE;
CREATE INDEX ix_patients_created ON patients ("CreatedAt" DESC) WHERE "IsDeleted" = FALSE;
```

### B. Error Codes

| HTTP | Mã lỗi              | Mô tả                                           |
| ---- | ------------------- | ----------------------------------------------- |
| 400  | `validation_error`  | Dữ liệu đầu vào không hợp lệ (FluentValidation) |
| 401  | `unauthorized`      | Chưa xác thực                                   |
| 403  | `forbidden`         | Không có quyền truy cập                         |
| 404  | `patient_not_found` | Không tìm thấy bệnh nhân                        |
| 429  | `rate_limited`      | Vượt quá giới hạn request (100/phút)            |

### C. Tài liệu liên quan

- [Developer Guide](developer_guide.md) — Hướng dẫn phát triển tổng quan
- [Advanced Security](advanced_security.md) — Module bảo mật nâng cao
- [Enterprise User Management](enterprise_user_management.md) — Quản lý người dùng
- [Form/Report Builder](form_report_builder.md) — Hệ thống biểu mẫu động
- [Digital Signing](digital_signing.md) — Ký số tài liệu
