# Vanta Compliance Assessment Report — IVF Information System

**Ngày đánh giá:** 03/03/2026  
**Hệ thống:** IVF Information System v1.0  
**Đánh giá bởi:** Security & Compliance Assessment Team  
**Phạm vi:** Full Stack (Backend .NET 10 + Frontend Angular 21 + Infrastructure)

---

## Mục lục

1. [Tổng quan đánh giá](#1-tổng-quan-đánh-giá)
2. [SOC 2 Type II](#2-soc-2-type-ii)
3. [ISO 27001:2022](#3-iso-270012022)
4. [HIPAA](#4-hipaa)
5. [GDPR](#5-gdpr)
6. [HITRUST CSF](#6-hitrust-csf)
7. [NIST AI RMF](#7-nist-ai-rmf)
8. [ISO 42001:2023](#8-iso-420012023)
9. [Gap Analysis tổng hợp](#9-gap-analysis-tổng-hợp)
10. [Kế hoạch triển khai (Roadmap)](#10-kế-hoạch-triển-khai-roadmap)
11. [Ưu tiên Quick Wins (30 ngày đầu)](#11-ưu-tiên-quick-wins-30-ngày-đầu)

---

## 1. Tổng quan đánh giá

### Điểm tuân thủ tổng thể

| Framework          | Mức hoàn thành |   Mức độ sẵn sàng   | Nỗ lực còn lại |
| ------------------ | :------------: | :-----------------: | :------------: |
| **SOC 2 Type II**  |      85%       |   ⭐⭐⭐⭐⭐ Cao    |    2-3 tuần    |
| **ISO 27001:2022** |      80%       |   ⭐⭐⭐⭐⭐ Cao    |    4-6 tuần    |
| **HIPAA**          |      90%       | ⭐⭐⭐⭐⭐ Rất cao  |    2-4 tuần    |
| **GDPR**           |      75%       |    ⭐⭐⭐⭐☆ Khá    |    4-8 tuần    |
| **HITRUST CSF**    |      80%       |   ⭐⭐⭐⭐⭐ Cao    |    3-5 tuần    |
| **NIST AI RMF**    |      60%       | ⭐⭐⭐☆☆ Trung bình |   6-12 tuần    |
| **ISO 42001:2023** |      40%       | ⭐⭐⭐☆☆ Trung bình |   8-12 tuần    |

### Kiến trúc bảo mật hiện tại — Tóm tắt

Hệ thống IVF đạt mức **Advanced Security Maturity (⭐⭐⭐⭐/5)** với:

- **Xác thực đa lớp:** JWT RS256 (3072-bit) + VaultToken + API Key + MFA (TOTP/SMS/Passkey/WebAuthn)
- **Zero Trust Architecture:** 7 tín hiệu threat detection, risk scoring 0-100, behavioral analytics
- **Mã hóa:** AES-256-GCM (field-level), TLS 1.3, mTLS, RSA-3072 signing
- **Audit:** 50+ loại SecurityEvent, EF Core change tracking, MITRE ATT&CK mapping
- **Incident Response:** Tự động (lock_account, revoke_sessions, notify_admin, block_ip)
- **Backup 3-2-1:** Primary + Standby + Cloud (PITR 14 ngày)
- **RBAC/ABAC:** 9 roles, 50+ permissions, Conditional Access Policies

---

## 2. SOC 2 Type II

### Trust Service Criteria — Phân tích chi tiết

#### ✅ ĐÃ TRIỂN KHAI — Tuân thủ đầy đủ

| Control ID | Tiêu chí                         | Bằng chứng triển khai                                                                                  |
| :--------: | -------------------------------- | ------------------------------------------------------------------------------------------------------ |
| **CC1.1**  | Tổ chức có cam kết về bảo mật    | Kiến trúc được document hóa (advanced_security.md, enterprise_security.md, zero_trust_architecture.md) |
| **CC6.1**  | Kiểm soát truy cập logic         | JWT Bearer (RS256), VaultToken (SHA-256), APIKey (BCrypt), biometric, RBAC (9 roles), ABAC             |
| **CC6.2**  | Quản lý quyền truy cập thông tin | Field-level access policies, role-based masking, consent enforcement middleware                        |
| **CC6.3**  | Đăng ký & hủy đăng ký tài khoản  | UserManagement endpoints (CRUD + bulk), session management, account lockout                            |
| **CC6.6**  | Kiểm soát truy cập đặc quyền     | Impersonation dual-approval (RFC 8693), permission delegation với time-bound                           |
| **CC6.7**  | Xóa quyền truy cập               | Session revocation, permission auto-revocation, account disable                                        |
| **CC6.8**  | Ngăn chặn truy cập trái phép     | Brute force protection (5 attempts → lockout), IP geofencing, Conditional Access                       |
| **CC7.1**  | Phát hiện thay đổi               | EF Core AuditInterceptor (before/after snapshots), change tracking                                     |
| **CC7.2**  | Giám sát hoạt động hệ thống      | SecurityEvent (50+ types), login history, behavioral analytics                                         |
| **CC7.3**  | Đánh giá sự kiện bảo mật         | ThreatDetectionService (7 signal categories), z-score anomaly detection                                |
| **CC7.4**  | Phản ứng sự cố bảo mật           | IncidentResponseService (auto-escalation: lock_account, revoke_sessions, notify_admin)                 |
| **CC8.1**  | Quản lý thay đổi                 | CI/CD pipeline (GitHub Actions), semantic versioning, CertDeploymentLog                                |
| **CC9.1**  | Nhận diện & đánh giá rủi ro      | ThreatAssessment records, risk scoring 0-100, behavioral baselines (30 ngày)                           |
| **CC9.2**  | Ứng phó sự cố                    | SecurityIncident state machine (Open → Investigating → Resolved → Closed)                              |
|  **A1.1**  | Khả năng phục hồi                | 3-2-1 backup, PostgreSQL streaming replication, PITR                                                   |
|  **A1.2**  | Truy cập khẩn cấp                | Break-glass impersonation via ImpersonationRequest                                                     |
|  **A1.3**  | Kiểm thử phục hồi                | Backup verification (SHA-256 checksums), compliance endpoint                                           |
| **PI1.1**  | Bảo vệ thông tin cá nhân         | Field-level encryption (AES-256-GCM), PiiMasker, consent enforcement                                   |

#### ⚠️ CẦN BỔ SUNG

| Gap ID | Yêu cầu                             |  Trạng thái  | Hành động cần thiết                                                          | Ưu tiên |
| :----: | ----------------------------------- | :----------: | ---------------------------------------------------------------------------- | :-----: |
| SOC-G1 | Engagement letter audit bên ngoài   |  ❌ Chưa có  | Ký hợp đồng với auditor (Deloitte/EY/KPMG/PwC hoặc firm SOC 2 certified)     |   P0    |
| SOC-G2 | Control assertion evidence package  | ⚠️ Một phần  | Đóng gói evidence từ ComplianceScoringEngine thành audit workpapers          |   P0    |
| SOC-G3 | Management review documentation     | ⚠️ Một phần  | Tạo quy trình quarterly control review với sign-off                          |   P1    |
| SOC-G4 | Penetration testing report          | ⚠️ Script có | Thực hiện pentest formal (quarterly), lưu báo cáo                            |   P1    |
| SOC-G5 | Vendor security assessment          |  ❌ Chưa có  | Đánh giá bảo mật cho vendors (DigitalPersona, npm packages, cloud providers) |   P1    |
| SOC-G6 | Security awareness training records |  ❌ Chưa có  | Triển khai chương trình training + tracking completion                       |   P1    |
| SOC-G7 | Exception handling documentation    |  ❌ Chưa có  | Quy trình xử lý ngoại lệ bảo mật (exception form + approval workflow)        |   P2    |

### Hành động Vanta

```
Vanta > Frameworks > SOC 2 Type II > Connect:
  ✅ Cloud Infrastructure (Docker/PostgreSQL/MinIO) → Auto-evidence
  ✅ Identity Provider (JWT/RBAC) → Manual evidence upload
  ✅ Version Control (GitHub) → Auto-connect
  ✅ CI/CD (GitHub Actions) → Auto-connect
  ⬜ Background Check Provider → Connect HR system
  ⬜ Security Awareness Training → Connect training platform
  ⬜ Vulnerability Scanner → Connect Trivy/Snyk
  ⬜ Endpoint Management → Connect device management
```

---

## 3. ISO 27001:2022

### Annex A Controls — Phân tích chi tiết

#### ✅ ĐÃ TRIỂN KHAI (75 controls)

| Domain             | Controls đạt | Total |  %  |
| ------------------ | :----------: | :---: | :-: |
| A.5 Organizational |     6/8      |   8   | 75% |
| A.6 People         |     4/8      |   8   | 50% |
| A.7 Physical       |     3/14     |  14   | 21% |
| A.8 Technological  |    30/34     |  34   | 88% |

**Highlights:**

| Control                                  | Chi tiết triển khai                                                                                      |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| **A.5.1** Chính sách ATTT                | 5+ tài liệu kiến trúc bảo mật (advanced_security.md, enterprise_security.md, zero_trust_architecture.md) |
| **A.8.1** User endpoint devices          | Device fingerprinting (SHA-256, 9 signals), session binding                                              |
| **A.8.2** Privileged access              | Dual-approval impersonation, Conditional Access, VaultPolicy                                             |
| **A.8.3** Information access restriction | Field-level encryption, role-based masking, consent enforcement                                          |
| **A.8.5** Secure authentication          | 6 phương thức (JWT/TOTP/SMS/Passkey/WebAuthn/Biometric)                                                  |
| **A.8.9** Configuration management       | Docker secrets, environment-based config, cert deployment log                                            |
| **A.8.10** Information deletion          | DataRetentionPolicy (Delete/Anonymize/Archive), daily schedule                                           |
| **A.8.11** Data masking                  | PiiMasker (SSN, email, phone), field-level access policies                                               |
| **A.8.12** Data leakage prevention       | Input validation WAF (SQL/XSS/Command injection detection)                                               |
| **A.8.15** Logging                       | Immutable SecurityEvent, AuditLog, VaultAuditLog                                                         |
| **A.8.16** Monitoring activities         | Behavioral analytics (z-score), real-time threat detection                                               |
| **A.8.20** Networks security             | Docker network segmentation (public/signing/data), TLS 1.3                                               |
| **A.8.24** Use of cryptography           | AES-256-GCM, RSA-3072, Azure Key Vault KEK wrapping                                                      |
| **A.8.25** SDLC                          | Clean Architecture, CQRS pattern, FluentValidation                                                       |
| **A.8.28** Secure coding                 | WAF-level input validation, parameterized queries (EF Core), output encoding                             |

#### ⚠️ GAPS CẦN BỔ SUNG

| Gap ID  | Control   | Yêu cầu                    | Hành động                                                              | Ưu tiên |
| :-----: | --------- | -------------------------- | ---------------------------------------------------------------------- | :-----: |
| ISO-G1  | A.5.2     | Vai trò & trách nhiệm ATTT | Tạo RACI matrix cho security roles, chỉ định CISO/Security Officer     |   P0    |
| ISO-G2  | A.5.4     | Trách nhiệm quản lý        | Ghi nhận management commitment letter, annual security review          |   P0    |
| ISO-G3  | A.5.7     | Threat intelligence        | Tích hợp threat feeds (STIX/TAXII), automated IoC ingestion            |   P1    |
| ISO-G4  | A.5.23-25 | Cloud security             | Tạo cloud security policy cho MinIO/S3, shared responsibility matrix   |   P1    |
| ISO-G5  | A.5.29-30 | ICT business continuity    | Tài liệu hóa RTO/RPO targets, BCP/DRP testing schedule                 |   P0    |
| ISO-G6  | A.6.1-2   | Screening & employment     | Tích hợp background check, NDA/confidentiality agreements              |   P1    |
| ISO-G7  | A.6.3     | Security awareness         | Triển khai training platform + phishing simulation                     |   P1    |
| ISO-G8  | A.6.7     | Remote working             | Chính sách remote access, VPN requirements, device compliance          |   P2    |
| ISO-G9  | A.7.1-14  | Physical security          | Đánh giá bảo mật vật lý datacenter/office (ngoài scope software)       |   P2    |
| ISO-G10 | A.5.5     | Contact with authorities   | Tạo danh sách liên lạc cơ quan chức năng (công an, CERT)               |   P1    |
| ISO-G11 | A.5.8     | Project security           | Checklist bảo mật cho mỗi project/sprint                               |   P2    |
| ISO-G12 | A.8.8     | Vulnerability management   | Triển khai SAST/DAST pipeline (SonarQube + OWASP ZAP), SLA remediation |   P0    |

### Hành động Vanta

```
Vanta > Frameworks > ISO 27001 > Checklist:
  ✅ Information Security Policy → Upload docs
  ✅ Risk Assessment → Map ThreatDetection to risk register
  ⬜ Statement of Applicability (SoA) → Create formal SoA document
  ⬜ Risk Treatment Plan → Document risk mitigation measures
  ⬜ Internal Audit Program → Schedule annual internal audit
  ⬜ Management Review → Quarterly meeting minutes template
  ⬜ Training Records → Connect training platform
  ⬜ Asset Inventory → Create CMDB (hardware, software, data assets)
  ⬜ Supplier Assessment → Create vendor evaluation form
```

---

## 4. HIPAA

### Security Rule (45 CFR §164.300-318) — Đánh giá chi tiết

#### ✅ ĐÃ TRIỂN KHAI — Tuân thủ cao (90%)

| Safeguard          | Requirement                      | Implementation                                                  | Status |
| ------------------ | -------------------------------- | --------------------------------------------------------------- | :----: |
| **Administrative** |                                  |                                                                 |        |
| §164.308(a)(1)     | Security Management Process      | ThreatDetectionService, risk scoring, incident response         |   ✅   |
| §164.308(a)(2)     | Assigned Security Responsibility | Admin role, VaultPolicy management                              |   ✅   |
| §164.308(a)(3)     | Workforce Security               | RBAC (9 roles), session management, login analytics             |   ✅   |
| §164.308(a)(4)     | Information Access Management    | Conditional Access, permission delegation, group-based IAM      |   ✅   |
| §164.308(a)(5)     | Security Awareness & Training    | ⚠️ Menu structure indicates, no formal tracking                 |   ⚠️   |
| §164.308(a)(6)     | Security Incident Procedures     | SecurityIncident state machine, IncidentResponseRule automation |   ✅   |
| §164.308(a)(7)     | Contingency Plan                 | 3-2-1 backup, PITR, streaming replication                       |   ✅   |
| §164.308(a)(8)     | Evaluation                       | ComplianceScoringEngine, compliance endpoint                    |   ✅   |
| **Physical**       |                                  |                                                                 |        |
| §164.310(a)        | Facility Access Controls         | IP geofencing, mTLS, container hardening                        |   ✅   |
| §164.310(b)        | Workstation Use                  | Device fingerprinting, session binding                          |   ✅   |
| §164.310(c)        | Workstation Security             | Conditional Access (device trust)                               |   ✅   |
| §164.310(d)        | Device & Media Controls          | Backup encryption (AES-256-CBC), SHA-256 verification           |   ✅   |
| **Technical**      |                                  |                                                                 |        |
| §164.312(a)        | Access Control                   | JWT RS256 + MFA + biometric + Conditional Access                |   ✅   |
| §164.312(b)        | Audit Controls                   | Immutable SecurityEvent, AuditLog (change tracking)             |   ✅   |
| §164.312(c)        | Integrity Controls               | Field-level encryption (AES-256-GCM), data validation           |   ✅   |
| §164.312(d)        | Person/Entity Authentication     | 6 authentication methods, device fingerprinting                 |   ✅   |
| §164.312(e)        | Transmission Security            | TLS 1.3, HSTS (2-year preload), mTLS for PKI                    |   ✅   |

#### ⚠️ GAPS CẦN BỔ SUNG

| Gap ID | Requirement                              | Hành động                                                                   | Ưu tiên |
| :----: | ---------------------------------------- | --------------------------------------------------------------------------- | :-----: |
| HIP-G1 | Breach Notification SOP (§164.404-408)   | Tạo quy trình thông báo vi phạm: 60 ngày cho cá nhân, LTV cho > 500 records |   P0    |
| HIP-G2 | Workforce Training (§164.308(a)(5))      | Training HIPAA cho toàn bộ nhân viên, chứng chỉ + tracking                  |   P0    |
| HIP-G3 | Business Associate Agreements            | Template BAA cho mỗi vendor xử lý PHI                                       |   P1    |
| HIP-G4 | Sanctions Policy (§164.308(a)(1)(ii)(C)) | Chính sách xử lý vi phạm nội bộ                                             |   P1    |
| HIP-G5 | Emergency Access Procedure               | Document hóa quy trình break-glass (đã có code, cần SOP)                    |   P2    |
| HIP-G6 | Maintenance Records                      | Log bảo trì hệ thống, patches applied                                       |   P2    |
| HIP-G7 | Risk Assessment Annual Review            | Đánh giá rủi ro hàng năm (formal document)                                  |   P0    |

### PHI Data Flow Mapping

```
Patient → [Angular Client] →(TLS 1.3)→ [IVF.API]
  → JWT Validation → Consent Check → Field-Level Encryption
  → [PostgreSQL] (AES-256-GCM at rest)
  → [MinIO] (medical images, PDFs, signed documents)
  → [AuditLog] (immutable, all PHI access tracked)
  → [Backup] (encrypted, SHA-256 verified, 3-2-1 strategy)
```

---

## 5. GDPR

### Đánh giá theo Articles — Chi tiết

#### ✅ ĐÃ TRIỂN KHAI (75%)

|  Article   | Quyền/Nguyên tắc             | Triển khai                                            |
| :--------: | ---------------------------- | ----------------------------------------------------- |
| Art. 5(1a) | Lawfulness                   | UserConsent entity (8 loại consent), version tracking |
| Art. 5(1b) | Purpose limitation           | Consent types gắn với endpoint cụ thể                 |
| Art. 5(1c) | Data minimization            | Field-level masking, PiiMasker                        |
| Art. 5(1e) | Storage limitation           | DataRetentionPolicy (Delete/Anonymize/Archive)        |
| Art. 5(1f) | Integrity & confidentiality  | AES-256-GCM, TLS 1.3, access control                  |
|   Art. 6   | Legal basis                  | 8 consent types mapping to Art. 6 bases               |
|   Art. 7   | Conditions for consent       | Grant/withdraw tracking, timestamp, IP, version       |
|  Art. 15   | Right of access              | GET /api/user-consents/my-status                      |
|  Art. 16   | Right to rectification       | User profile update + audit trail                     |
|  Art. 17   | Right to erasure             | DataRetentionPolicy Delete action                     |
|  Art. 18   | Right to restrict processing | Consent withdrawal blocks processing                  |
|  Art. 20   | Right to data portability    | Export via SecurityAuditService                       |
|  Art. 21   | Right to object              | Consent withdrawal mechanism                          |
|  Art. 25   | Data protection by design    | Privacy-by-default consent, encryption, masking       |
|  Art. 32   | Security of processing       | Full security stack (encrypt, access control, audit)  |

#### ❌ GAPS CẦN TRIỂN KHAI

|  Gap ID  |  Article   | Yêu cầu                                  | Hành động                                                               | Ưu tiên |
| :------: | :--------: | ---------------------------------------- | ----------------------------------------------------------------------- | :-----: |
| GDPR-G1  | Art. 33-34 | Breach notification (72h)                | Automated breach notification workflow → DPA & data subjects            |   P0    |
| GDPR-G2  |  Art. 35   | DPIA (Data Protection Impact Assessment) | DPIA cho xử lý sinh trắc học + hồ sơ y tế (high-risk)                   |   P0    |
| GDPR-G3  | Art. 37-39 | DPO (Data Protection Officer)            | Chỉ định DPO, document trách nhiệm                                      |   P0    |
| GDPR-G4  | Art. 13-14 | Privacy notice                           | Privacy policy cho bệnh nhân (VN + EN), hiển thị trước thu thập dữ liệu |   P0    |
| GDPR-G5  | Art. 44-49 | International transfers                  | SCCs (Standard Contractual Clauses) cho data transfer EU↔VN             |   P1    |
| GDPR-G6  |  Art. 28   | Data processing agreements               | DPA với mỗi vendor xử lý dữ liệu cá nhân                                |   P1    |
| GDPR-G7  |  Art. 30   | ROPA (Records of Processing Activities)  | Tạo register chính thức cho mỗi hoạt động xử lý dữ liệu                 |   P1    |
| GDPR-G8  |  Art. 12   | Transparent communication                | Cookie consent banner, layered privacy notices                          |   P2    |
| GDPR-G9  |  Art. 22   | Automated decision-making                | Thông báo cho data subjects về threat detection AI                      |   P2    |
| GDPR-G10 |  Art. 47   | Binding Corporate Rules                  | BCRs nếu group company transfer data                                    |   P3    |

### Hành động Vanta

```
Vanta > Frameworks > GDPR > Tasks:
  ✅ Data Processing Register → Map from AuditLog
  ✅ Consent Management → Already implemented (8 types)
  ⬜ Privacy Policy → Draft & publish
  ⬜ DPIA → Conduct for biometric + medical records
  ⬜ DPO Appointment → Document & register
  ⬜ Breach Notification Process → Create 72-hour workflow
  ⬜ Subject Access Request (SAR) Process → Formalize API workflow
  ⬜ Data Transfer Mechanisms → Implement SCCs
  ⬜ Cookie Banner → Implement on Angular frontend
  ⬜ Data Retention Schedule → Formalize & document per data type
```

---

## 6. HITRUST CSF

### HITRUST CSF v11 — Assessment Readiness

HITRUST tích hợp ISO 27001 + HIPAA + NIST CSF. Dựa trên đánh giá từ hai framework trên:

#### ✅ DOMAINS ĐÃ TRIỂN KHAI (80%)

| Domain | Category                                 | Coverage | Evidence                                                  |
| :----: | ---------------------------------------- | :------: | --------------------------------------------------------- |
|   00   | Information Security Management Program  |   85%    | SecurityPolicy docs, ComplianceScoringEngine              |
|   01   | Access Control                           |   95%    | RBAC, ABAC, MFA, Conditional Access, session management   |
|   02   | Human Resources Security                 |   50%    | Role assignment, ⚠️ training tracking missing             |
|   03   | Risk Management                          |   85%    | ThreatDetection, risk scoring, incident response          |
|   04   | Security Policy                          |   80%    | Architecture docs, ⚠️ formal policy documents needed      |
|   05   | Organization of Information Security     |   60%    | Roles defined, ⚠️ governance structure needed             |
|   06   | Compliance                               |   75%    | Consent, retention, audit, ⚠️ formal compliance program   |
|   07   | Asset Management                         |   40%    | Device fingerprinting, ⚠️ CMDB missing                    |
|   08   | Physical & Environmental Security        |   30%    | Container hardening, ⚠️ physical security N/A             |
|   09   | Communications & Operations Management   |   90%    | Network segmentation, TLS, rate limiting, monitoring      |
|   10   | Information Systems Acquisition          |   85%    | SDLC, FluentValidation, EF Core (parameterized)           |
|   11   | Information Security Incident Management |   90%    | SecurityIncident, IncidentResponseRule, automated actions |
|   12   | Business Continuity Management           |   75%    | 3-2-1 backup, replication, ⚠️ RTO/RPO needed              |
|   13   | Privacy Practices                        |   80%    | Consent, retention, masking, ⚠️ DPIA needed               |

#### ⚠️ GAPS CẦN BỔ SUNG

| Gap ID | Domain | Hành động                                                          | Ưu tiên |
| :----: | ------ | ------------------------------------------------------------------ | :-----: |
| HIT-G1 | 02     | Employee security training program + tracking                      |   P0    |
| HIT-G2 | 05     | Information security committee charter                             |   P1    |
| HIT-G3 | 07     | Formal asset inventory (CMDB)                                      |   P1    |
| HIT-G4 | 08     | Physical security assessment (nếu hosting tại chỗ)                 |   P2    |
| HIT-G5 | 12     | BCP/DRP testing + RTO/RPO documentation                            |   P0    |
| HIT-G6 | 04     | Formal information security policy document (signed by management) |   P0    |

---

## 7. NIST AI RMF

### AI/ML Features trong hệ thống

Hệ thống IVF sử dụng AI/ML cho:

1. **Threat Detection** — ThreatDetectionService (7 tín hiệu, risk scoring)
2. **Behavioral Analytics** — BehavioralAnalyticsService (z-score anomaly, 30-day baseline)
3. **Bot Detection** — BotDetectionService (reCAPTCHA + UA inspection)
4. **Contextual Authentication** — ContextualAuthService (risk-based MFA step-up)
5. **Biometric Matching** — DigitalPersona SDK (server-side fingerprint matching)

### NIST AI RMF Core Functions — Đánh giá

#### GOVERN (Quản trị)

| Subcategory                            | Status | Evidence                     | Gap                                  |
| -------------------------------------- | :----: | ---------------------------- | ------------------------------------ |
| GV.1 - AI governance policies          | ⚠️ 30% | Threat detection documented  | Cần AI governance charter chính thức |
| GV.2 - AI risk management strategy     | ⚠️ 40% | Risk scoring implemented     | Cần AI risk register                 |
| GV.3 - AI oversight roles              | ❌ 10% | Admin role reviews incidents | Cần AI governance board              |
| GV.4 - AI transparency & documentation | ⚠️ 50% | ThreatIndicators JSON logged | Cần user-facing explainability       |
| GV.5 - Third-party AI management       | ❌ 20% | DigitalPersona integrated    | Cần vendor AI assessment             |
| GV.6 - Workforce AI competency         | ❌ 15% | No formal training           | Cần AI training program              |

#### MAP (Lập bản đồ)

| Subcategory                          | Status | Evidence                         | Gap                                            |
| ------------------------------------ | :----: | -------------------------------- | ---------------------------------------------- |
| MP.1 - AI use case definition        | ⚠️ 50% | 5 AI features identified         | Cần formal use case charter                    |
| MP.2 - AI stakeholder identification | ⚠️ 40% | Security team identified         | Cần stakeholder mapping (patients, clinicians) |
| MP.3 - AI context & deployment       | ✅ 70% | Production deployment documented | Cần deployment impact assessment               |
| MP.4 - AI risks & benefits           | ⚠️ 45% | Risk scoring documents risks     | Cần formal risk-benefit analysis               |
| MP.5 - AI impact on individuals      | ❌ 20% | Account lockout impact           | Cần impact assessment cho patients             |

#### MEASURE (Đo lường)

| Subcategory                   | Status | Evidence                              | Gap                            |
| ----------------------------- | :----: | ------------------------------------- | ------------------------------ |
| MS.1 - AI performance metrics | ⚠️ 55% | Z-score threshold (≥25), risk 0-100   | Cần FPR/FNR tracking           |
| MS.2 - AI safety metrics      | ⚠️ 50% | MITRE ATT&CK mapping, severity levels | Cần safety boundary testing    |
| MS.3 - AI fairness metrics    | ❌ 10% | No bias testing                       | Cần demographic parity testing |
| MS.4 - AI explainability      | ⚠️ 30% | ThreatIndicators JSON                 | Cần user-facing explanations   |

#### MANAGE (Quản lý)

| Subcategory               | Status | Evidence                     | Gap                                  |
| ------------------------- | :----: | ---------------------------- | ------------------------------------ |
| MG.1 - AI risk treatment  | ✅ 70% | Incident response automation | Cần AI-specific risk treatment plan  |
| MG.2 - Human oversight    | ⚠️ 50% | Admin reviews incidents      | Cần formal human-in-the-loop process |
| MG.3 - AI risk monitoring | ✅ 65% | Real-time threat detection   | Cần AI model monitoring dashboard    |
| MG.4 - AI decommissioning | ❌ 10% | No documented process        | Cần AI model retirement policy       |

#### ❌ GAPS CẦN TRIỂN KHAI

| Gap ID | Yêu cầu                   | Hành động                                                       | Effort | Ưu tiên |
| :----: | ------------------------- | --------------------------------------------------------------- | :----: | :-----: |
| AI-G1  | AI Governance Charter     | Tạo tài liệu: mục đích, phạm vi, stakeholders, đạo đức AI       |  Low   |   P0    |
| AI-G2  | AI Governance Board       | Thành lập team cross-functional (security, medical, IT, ethics) | Medium |   P0    |
| AI-G3  | AI Risk Register          | Document mỗi AI use case, risks, likelihood, impact, mitigation | Medium |   P0    |
| AI-G4  | Bias & Fairness Testing   | Test geolocation bias, demographic parity cho threat detection  |  High  |   P1    |
| AI-G5  | Model Versioning          | Git-track thresholds, maintain changelog, rollback capability   |  Low   |   P1    |
| AI-G6  | User Explainability       | "Tại sao bạn bị flag" explanations trong UI                     | Medium |   P1    |
| AI-G7  | FPR/FNR Tracking          | Dashboard: false positive rate, false negative rate, per signal | Medium |   P1    |
| AI-G8  | AI Training Program       | Training cho security team về AI/ML logic, bias awareness       |  Low   |   P2    |
| AI-G9  | Third-Party AI Assessment | Audit DigitalPersona, Fido2 provider, UA classification         |  High  |   P2    |
| AI-G10 | Model Retirement Policy   | Quy trình decommission AI models khi obsolete                   |  Low   |   P3    |

---

## 8. ISO 42001:2023

### AI Management System (AIMS) — Đánh giá

ISO 42001 yêu cầu một hệ thống quản lý AI chính thức. Đây là framework mới nhất và hệ thống cần nhiều phát triển nhất.

#### Clause-by-Clause Assessment

| Clause | Yêu cầu                     | Mức hoàn thành | Evidence                                  | Gaps                                                                              |
| :----: | --------------------------- | :------------: | ----------------------------------------- | --------------------------------------------------------------------------------- |
| **4**  | Context of the organization |      35%       | AI features identified in security docs   | Formal AI context analysis, interested parties list                               |
| **5**  | Leadership                  |      25%       | Admin role exists                         | AI policy statement, management commitment, AI governance board                   |
| **6**  | Planning                    |      35%       | Threat scoring = risk assessment          | AI risk assessment, AI objectives, AI treatment plan                              |
| **7**  | Support                     |      45%       | Documentation exists for threat detection | AI competency requirements, AI training records, AI communication plan            |
| **8**  | Operation                   |      50%       | AI features operational in production     | AI development lifecycle, AI testing & validation, AI impact assessment           |
| **9**  | Performance evaluation      |      40%       | Compliance scoring, metrics collected     | AI internal audit, AI management review, AI monitoring program                    |
| **10** | Improvement                 |      25%       | Bug fixes = improvement                   | Corrective action process, continuous improvement plan, non-conformity management |

#### Annex A — AI Controls

| Control | Description           | Status | Action Required                                                  |
| ------- | --------------------- | :----: | ---------------------------------------------------------------- |
| A.2     | AI Policy             |   ❌   | Tạo chính sách AI chính thức                                     |
| A.3     | AI Risk Assessment    | ⚠️ 40% | Formal AI risk assessment methodology                            |
| A.4     | AI System Life Cycle  | ⚠️ 30% | Document AI model lifecycle (design → deploy → monitor → retire) |
| A.5     | Data for AI Systems   | ⚠️ 50% | Data quality management cho AI training data                     |
| A.6     | AI System Performance | ⚠️ 45% | Formal performance testing & benchmarking                        |
| A.7     | Third-party AI        | ❌ 20% | Vendor AI risk assessments                                       |
| A.8     | AI Transparency       | ⚠️ 35% | User-facing explainability                                       |
| A.9     | AI System Security    | ✅ 80% | Security controls for AI (already strong)                        |
| A.10    | AI Accountability     | ⚠️ 30% | Accountability framework, audit trail for AI decisions           |

#### ❌ GAPS CẦN TRIỂN KHAI

| Gap ID  | Yêu cầu                        | Hành động                                                                  | Effort | Ưu tiên |
| :-----: | ------------------------------ | -------------------------------------------------------------------------- | :----: | :-----: |
| AMS-G1  | AI Policy Statement            | Tạo chính sách AI tại cấp lãnh đạo (CEO/CTO sign-off)                      |  Low   |   P0    |
| AMS-G2  | AI Management System Scope     | Document phạm vi AIMS: threat detection, biometric, behavioral analytics   |  Low   |   P0    |
| AMS-G3  | AI Risk Assessment Methodology | Tạo framework đánh giá rủi ro AI (likelihood × impact matrix)              | Medium |   P0    |
| AMS-G4  | AI Lifecycle Documentation     | Document: Design → Train → Test → Deploy → Monitor → Retire per AI feature | Medium |   P1    |
| AMS-G5  | AI Impact Assessment           | AIIA cho mỗi AI feature (impact trên patients, clinicians, operations)     |  High  |   P1    |
| AMS-G6  | AI Internal Audit              | Lịch trình audit nội bộ AIMS (quarterly)                                   | Medium |   P1    |
| AMS-G7  | AI Management Review           | Template review meeting (quarterly) với metrics, risks, improvements       |  Low   |   P2    |
| AMS-G8  | AI Non-conformity Process      | Quy trình xử lý khi AI system không tuân thủ yêu cầu                       |  Low   |   P2    |
| AMS-G9  | AI Continual Improvement Plan  | Roadmap cải tiến AI capabilities (annual)                                  | Medium |   P2    |
| AMS-G10 | AI Competency Framework        | Ma trận năng lực cho nhân sự vận hành AI systems                           |  Low   |   P3    |

---

## 9. Gap Analysis tổng hợp

### Ma trận gaps theo Framework & Ưu tiên

| Gap                                | SOC2 | ISO27001 | HIPAA | GDPR | HITRUST | NIST AI | ISO42001 | Ưu tiên |
| ---------------------------------- | :--: | :------: | :---: | :--: | :-----: | :-----: | :------: | :-----: |
| Security Awareness Training        |  ✓   |    ✓     |   ✓   |      |    ✓    |         |          | **P0**  |
| Formal Risk Assessment             |  ✓   |    ✓     |   ✓   |      |    ✓    |    ✓    |    ✓     | **P0**  |
| Breach Notification SOP            |  ✓   |          |   ✓   |  ✓   |    ✓    |         |          | **P0**  |
| DPO Appointment                    |      |          |       |  ✓   |         |         |          | **P0**  |
| DPIA                               |      |          |       |  ✓   |    ✓    |         |    ✓     | **P0**  |
| Vulnerability Scanning (SAST/DAST) |  ✓   |    ✓     |       |      |    ✓    |         |          | **P0**  |
| BCP/DRP + RTO/RPO                  |  ✓   |    ✓     |   ✓   |      |    ✓    |         |          | **P0**  |
| AI Governance Charter              |      |          |       |      |         |    ✓    |    ✓     | **P0**  |
| Vendor Risk Assessments            |  ✓   |    ✓     |   ✓   |  ✓   |    ✓    |    ✓    |    ✓     | **P1**  |
| Asset Inventory (CMDB)             |      |    ✓     |       |      |    ✓    |         |          | **P1**  |
| Formal Security Policy (signed)    |  ✓   |    ✓     |       |      |    ✓    |         |          | **P1**  |
| Privacy Notice                     |      |          |       |  ✓   |         |         |          | **P1**  |
| ROPA                               |      |          |       |  ✓   |         |         |          | **P1**  |
| Bias & Fairness Testing            |      |          |       |  ✓   |         |    ✓    |    ✓     | **P1**  |
| AI Lifecycle Documentation         |      |          |       |      |         |    ✓    |    ✓     | **P1**  |
| Pentest Reports (formal)           |  ✓   |    ✓     |       |      |    ✓    |         |          | **P1**  |
| SCCs for Data Transfer             |      |          |       |  ✓   |         |         |          | **P1**  |
| Cookie Consent Banner              |      |          |       |  ✓   |         |         |          | **P2**  |
| Physical Security Assessment       |      |    ✓     |       |      |    ✓    |         |          | **P2**  |
| Remote Working Policy              |      |    ✓     |       |      |         |         |          | **P2**  |
| AI Model Versioning                |      |          |       |      |         |    ✓    |    ✓     | **P2**  |

### Tổng số Gaps theo Ưu tiên

|      Ưu tiên      | Số lượng | Effort ước tính | Impact                      |
| :---------------: | :------: | :-------------: | --------------------------- |
| **P0 — Critical** |    8     |    4-6 tuần     | Bắt buộc cho certification  |
|   **P1 — High**   |    10    |    6-8 tuần     | Cần thiết cho certification |
|  **P2 — Medium**  |    5     |    3-4 tuần     | Tốt để có                   |
|   **P3 — Low**    |    3     |    2-3 tuần     | Nice-to-have                |

---

## 10. Kế hoạch triển khai (Roadmap)

### Phase 1: Foundation (Tuần 1-4) — P0 Gaps

```
┌─────────┬──────────────────────────────────────────────────────────────┐
│ Tuần 1  │ ▓▓▓ Security Awareness Training Program Setup                │
│         │ ▓▓▓ Formal Risk Assessment (ISO 27001 + HIPAA)               │
│         │ ▓▓▓ DPO Appointment & DPIA Initiation                        │
│         │ ▓▓▓ AI Governance Charter Draft                              │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 2  │ ▓▓▓ Breach Notification SOP (HIPAA 60-day + GDPR 72-hour)   │
│         │ ▓▓▓ SAST/DAST Pipeline (SonarQube + OWASP ZAP)              │
│         │ ▓▓▓ BCP/DRP Document + RTO/RPO Targets                      │
│         │ ▓▓▓ DPIA Completion (biometric + medical records)            │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 3  │ ▓▓▓ Formal Security Policy (management sign-off)            │
│         │ ▓▓▓ AI Risk Register                                        │
│         │ ▓▓▓ AI Governance Board Establishment                       │
│         │ ▓▓▓ Vendor Risk Assessment Framework                        │
├─────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 4  │ ▓▓▓ ComplianceScoringEngine Enhancement → SOC2 Evidence     │
│         │ ▓▓▓ First Quarterly Management Review                       │
│         │ ▓▓▓ Phase 1 Validation & Gap Reassessment                   │
└─────────┴──────────────────────────────────────────────────────────────┘
```

**Deliverables Phase 1:**

- [ ] Security Awareness Training Program (platform selection + first training)
- [ ] Formal Risk Assessment Document (ISO 27001 format)
- [ ] HIPAA Breach Notification SOP
- [ ] GDPR DPIA Document
- [ ] DPO Appointment Letter
- [ ] SAST/DAST integrated in CI/CD
- [ ] BCP/DRP with RTO/RPO targets
- [ ] AI Governance Charter
- [ ] Formal Information Security Policy (signed)

### Phase 2: Compliance Build-Out (Tuần 5-10) — P1 Gaps

```
┌──────────┬──────────────────────────────────────────────────────────────┐
│ Tuần 5-6 │ ▓▓▓ Vendor Risk Assessment Program (top 10 vendors)         │
│          │ ▓▓▓ Asset Inventory / CMDB Setup                            │
│          │ ▓▓▓ GDPR Privacy Notice (VN + EN)                           │
│          │ ▓▓▓ ROPA (Records of Processing Activities)                 │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 7-8 │ ▓▓▓ First Formal Penetration Test                          │
│          │ ▓▓▓ AI Bias & Fairness Testing Framework                    │
│          │ ▓▓▓ AI Lifecycle Documentation                              │
│          │ ▓▓▓ SCCs Implementation (EU↔VN data transfer)               │
├──────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 9-10│ ▓▓▓ AI FPR/FNR Tracking Dashboard                          │
│          │ ▓▓▓ User Explainability Feature (threat flagging)           │
│          │ ▓▓▓ First Internal Audit (ISO 27001 scope)                  │
│          │ ▓▓▓ Phase 2 Validation & Certification Readiness            │
└──────────┴──────────────────────────────────────────────────────────────┘
```

**Deliverables Phase 2:**

- [ ] Vendor Risk Assessment Reports (top 10)
- [ ] CMDB / Asset Register
- [ ] GDPR Privacy Notice published
- [ ] ROPA document
- [ ] Penetration Test Report
- [ ] AI Bias Testing Results
- [ ] AI Lifecycle Documentation
- [ ] SCCs signed with relevant parties
- [ ] AI FPR/FNR Dashboard
- [ ] Internal Audit Report

### Phase 3: Certification Preparation (Tuần 11-16) — P2 + Audit Prep

```
┌───────────┬──────────────────────────────────────────────────────────────┐
│ Tuần 11-12│ ▓▓▓ SOC 2 Type II Auditor Engagement                       │
│           │ ▓▓▓ ISO 27001 Certification Body Selection                  │
│           │ ▓▓▓ HITRUST CSF Self-Assessment                             │
│           │ ▓▓▓ Cookie Consent Implementation (Angular)                  │
├───────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 13-14│ ▓▓▓ SOC 2 Observation Period Start                         │
│           │ ▓▓▓ ISO 27001 Stage 1 Audit (documentation review)          │
│           │ ▓▓▓ HIPAA Self-Assessment Submission                        │
│           │ ▓▓▓ AI Model Versioning Implementation                      │
├───────────┼──────────────────────────────────────────────────────────────┤
│ Tuần 15-16│ ▓▓▓ ISO 27001 Stage 2 Audit (on-site/remote)               │
│           │ ▓▓▓ GDPR Readiness Assessment (external)                    │
│           │ ▓▓▓ NIST AI RMF Maturity Assessment                        │
│           │ ▓▓▓ Phase 3 Final Report & Certification Status             │
└───────────┴──────────────────────────────────────────────────────────────┘
```

### Phase 4: Ongoing Compliance (Post-Certification)

```
Monthly:
  - Security metrics review
  - Vulnerability scan results review
  - AI model performance monitoring

Quarterly:
  - Management review (ISO 27001 clause 9.3)
  - Internal audit cycle
  - Penetration testing
  - AI bias testing
  - Security training update

Annually:
  - SOC 2 Type II renewal audit
  - ISO 27001 surveillance audit
  - HIPAA risk assessment
  - GDPR DPIA review
  - HITRUST CSF reassessment
  - AI governance review
  - BCP/DRP testing exercise
```

---

## 11. Ưu tiên Quick Wins (30 ngày đầu)

### Top 10 hành động có impact cao nhất, thực hiện nhanh nhất

|  #  | Hành động                                                                | Effort |   Impact   | Framework Coverage             |
| :-: | ------------------------------------------------------------------------ | :----: | :--------: | ------------------------------ |
|  1  | **SAST/DAST Pipeline** — Thêm SonarQube + OWASP ZAP vào GitHub Actions   | 2 ngày |    Cao     | SOC2, ISO27001, HITRUST        |
|  2  | **Breach Notification SOP** — Tạo document quy trình thông báo vi phạm   | 1 ngày |    Cao     | HIPAA, GDPR, SOC2, HITRUST     |
|  3  | **DPO Appointment** — Chỉ định + document trách nhiệm                    | 1 ngày |    Cao     | GDPR                           |
|  4  | **AI Governance Charter** — Draft document 2-3 trang                     | 1 ngày |    Cao     | NIST AI RMF, ISO 42001         |
|  5  | **Formal Security Policy** — Template + management sign-off              | 2 ngày |    Cao     | ISO27001, HITRUST, SOC2        |
|  6  | **BCP/DRP + RTO/RPO** — Document từ backup infrastructure đã có          | 2 ngày |    Cao     | ISO27001, HIPAA, HITRUST       |
|  7  | **Risk Assessment Document** — Formalize từ ThreatDetection đã có        | 3 ngày |    Cao     | Tất cả 7 frameworks            |
|  8  | **DPIA** — Đánh giá tác động bảo vệ dữ liệu cho biometric + medical      | 3 ngày |    Cao     | GDPR, HITRUST                  |
|  9  | **Vendor Inventory** — Liệt kê tất cả vendors + security assessment form | 2 ngày | Trung bình | Tất cả 7 frameworks            |
| 10  | **Training Program Setup** — Chọn platform + schedule first training     | 2 ngày | Trung bình | SOC2, ISO27001, HIPAA, HITRUST |

### Tích hợp Vanta Platform

```
Vanta Dashboard Setup:
  1. Connect GitHub repository → Automated code review evidence
  2. Connect Docker infrastructure → Container security monitoring
  3. Connect PostgreSQL → Database access logging
  4. Upload existing security documentation → Policy evidence
  5. Configure automated tests → Continuous monitoring
  6. Set up employee onboarding → Background check + training tracking
  7. Configure vulnerability scanning → Trivy/Snyk integration
  8. Enable access reviews → Quarterly access certification
  9. Configure alert policies → Real-time compliance violations
  10. Generate compliance reports → Board-ready dashboards
```

### Chi phí ước tính cho Certification

| Framework          | Loại chi phí         |    Ước tính (USD)    | Ghi chú                           |
| ------------------ | -------------------- | :------------------: | --------------------------------- |
| SOC 2 Type II      | Auditor engagement   |   $15,000-$50,000    | Tùy scope, first-year higher      |
| ISO 27001          | Certification body   |   $10,000-$30,000    | Stage 1 + Stage 2 + surveillance  |
| HIPAA              | Self-assessment      |    $5,000-$15,000    | Consultant + remediation          |
| GDPR               | DPO + DPIA           |    $5,000-$20,000    | DPO salary/outsource + assessment |
| HITRUST            | Validated assessment |   $20,000-$60,000    | Assessor + Vanta fee              |
| NIST AI RMF        | Self-assessment      |    $3,000-$10,000    | Internal effort + consultant      |
| ISO 42001          | Certification body   |   $10,000-$25,000    | New standard, limited assessors   |
| **Vanta Platform** | Annual subscription  |   $10,000-$25,000    | Automation + evidence collection  |
| **Total Year 1**   |                      | **$78,000-$235,000** |                                   |

---

## Kết luận

Hệ thống IVF Information System đã đạt **mức bảo mật enterprise-grade (⭐⭐⭐⭐/5)** với kiến trúc Zero Trust, mã hóa toàn diện, audit logging đầy đủ, và incident response tự động. Các gap chính nằm ở **documentation & formal processes** hơn là **technical controls**.

**Đánh giá tổng thể:** Hệ thống sẵn sàng ~80% cho SOC 2/ISO 27001/HIPAA/HITRUST. Cần phát triển thêm cho NIST AI RMF (~60%) và ISO 42001 (~40%).

**Khuyến nghị hàng đầu:** Bắt đầu với Vanta platform integration + Phase 1 (4 tuần) để đạt mức certification-ready cho SOC 2 Type II và HIPAA.

---

_Report generated: 03/03/2026_  
_Next review: 03/04/2026 (Monthly)_
