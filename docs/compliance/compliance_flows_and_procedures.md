# Compliance Flows & Procedures

> **Document ID:** IVF-CMP-CFP-001  
> **Version:** 1.0  
> **Effective Date:** 2026-03-04  
> **Classification:** CONFIDENTIAL  
> **Owner:** Compliance Officer / DPO  
> **Review Cycle:** Quarterly

---

## Table of Contents

1. [Data Subject Request (DSR) Flow](#1-data-subject-request-dsr-flow)
2. [Breach Detection & Notification Flow](#2-breach-detection--notification-flow)
3. [AI Model Governance Flow](#3-ai-model-governance-flow)
4. [Compliance Schedule & Task Flow](#4-compliance-schedule--task-flow)
5. [Incident Response Flow](#5-incident-response-flow)
6. [Data Retention & Deletion Flow](#6-data-retention--deletion-flow)
7. [Asset Registration & Classification Flow](#7-asset-registration--classification-flow)
8. [Training & Certification Flow](#8-training--certification-flow)
9. [Vendor Onboarding Flow](#9-vendor-onboarding-flow)
10. [Audit Preparation Flow](#10-audit-preparation-flow)
11. [Continuous Monitoring Flow](#11-continuous-monitoring-flow)
12. [Consent Management Flow](#12-consent-management-flow)

---

## 1. Data Subject Request (DSR) Flow

### 1.1 Overview

DSR là quy trình xử lý quyền của chủ thể dữ liệu theo GDPR Art. 15-22. Bao gồm 8 loại yêu cầu: Access, Rectification, Erasure, Restriction, Portability, Objection, Automated Decision Opt-Out, và Withdrawal of Consent.

### 1.2 End-to-End Flow

```
┌──────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  Intake  │───→│   Verify     │───→│   Process    │───→│   Deliver    │
│          │    │   Identity   │    │   Request    │    │   Response   │
│ POST /dsr│    │ POST /verify │    │ (internal)   │    │ POST /compl. │
└──────────┘    └──────────────┘    └──────────────┘    └──────────────┘
     │                │                    │                    │
     ▼                ▼                    ▼                    ▼
  Created        Verified/Rejected     In Progress          Completed
  (30-day SLA)   (ID confirmation)    (data retrieval)     (notify subject)
```

### 1.3 Detailed Steps

#### Step 1: Intake (Tiếp nhận)

- **Trigger:** Bệnh nhân gửi yêu cầu qua email, trực tiếp, hoặc portal
- **API:** `POST /api/compliance/dsr`
- **UI:** DSR Management → ➕ Tạo yêu cầu DSR
- **Data Required:**
  ```json
  {
    "dataSubjectName": "Nguyen Van A",
    "dataSubjectEmail": "patient@example.com",
    "requestType": "Access",
    "description": "Yêu cầu truy cập toàn bộ hồ sơ IVF"
  }
  ```
- **Automatic Actions:**
  - Tạo bản ghi DSR với status `Pending`
  - Ghi nhận timestamp tiếp nhận
  - Tính toán deadline (30 ngày theo GDPR Art. 12(3))
  - Gửi xác nhận tiếp nhận cho chủ thể dữ liệu

#### Step 2: Identity Verification (Xác minh danh tính)

- **Trigger:** Staff xử lý DSR
- **API:** `POST /api/compliance/dsr/{id}/verify`
- **Verification Methods:**
  1. **Biometric:** So khớp vân tay qua DigitalPersona (nếu đã enroll)
  2. **Document:** Kiểm tra CMND/CCCD/Passport
  3. **Knowledge:** Các câu hỏi xác minh (ngày sinh, ID bệnh nhân, lịch sử điều trị)
- **Decision:**
  - ✅ **Verified** → Chuyển sang xử lý
  - ❌ **Rejected** → `POST /api/compliance/dsr/{id}/reject` (lý do: không xác minh được danh tính)
  - ⚠️ **Cần thêm thông tin** → Liên hệ lại chủ thể dữ liệu

#### Step 3: Assignment & Processing (Phân công & Xử lý)

- **API:** `POST /api/compliance/dsr/{id}/assign`
- **Process by Request Type:**

| Type              | Process                                                                           | Time       |
| ----------------- | --------------------------------------------------------------------------------- | ---------- |
| **Access**        | Export toàn bộ dữ liệu bệnh nhân từ DB (treatment cycles, lab results, documents) | 5-10 ngày  |
| **Rectification** | Xác minh data sai → cập nhật → ghi audit trail                                    | 3-5 ngày   |
| **Erasure**       | Kiểm tra HIPAA retention → pseudonymize nếu phải giữ, xóa nếu có thể              | 10-15 ngày |
| **Restriction**   | Set `Patient.IsRestricted = true` → `RestrictProcessing()`                        | 1-2 ngày   |
| **Portability**   | Export dữ liệu structured (JSON/XML) theo FHIR format                             | 5-10 ngày  |
| **Objection**     | Review cơ sở pháp lý → tạm dừng processing nếu hợp lệ                             | 5-7 ngày   |

- **Extension:** Nếu cần thêm thời gian → `POST /api/compliance/dsr/{id}/extend` (tối đa 2 tháng thêm, phải thông báo lý do)
- **Escalation:** Nếu phức tạp → `POST /api/compliance/dsr/{id}/escalate` (lên DPO/Legal)

#### Step 4: Completion & Notification (Hoàn thành & Thông báo)

- **API:** `POST /api/compliance/dsr/{id}/complete` → `POST /api/compliance/dsr/{id}/notify`
- **Deliverables:**
  - Access/Portability: File dữ liệu được mã hóa, gửi qua email bảo mật hoặc portal
  - Erasure: Xác nhận deletion certificate
  - Rectification: Xác nhận dữ liệu đã sửa
- **Audit Trail:** Toàn bộ quy trình được ghi nhận trong audit log

### 1.4 SLA & Escalation Matrix

| Metric                 |       Target        |         Warning         |        Critical         |
| ---------------------- | :-----------------: | :---------------------: | :---------------------: |
| Response time          |      ≤ 30 ngày      | 20 ngày chưa hoàn thành | 28 ngày chưa hoàn thành |
| Extension notification | Ngay khi quyết định |            —            |            —            |
| Identity verification  |  ≤ 3 ngày làm việc  |            —            |  5 ngày chưa xác minh   |

### 1.5 Special Cases

- **Erasure vs. HIPAA Retention:** Dữ liệu y tế giữ tối thiểu 6 năm (HIPAA). Pseudonymize thay vì xóa, chỉ xóa identifier mapping.
- **Automated Decision Opt-Out:** IVF system không sử dụng automated decision-making ảnh hưởng pháp lý (Art. 22). Ghi nhận yêu cầu, trả lời không áp dụng.
- **Minor Data Subjects:** Yêu cầu phải từ phụ huynh/người giám hộ hợp pháp.

---

## 2. Breach Detection & Notification Flow

### 2.1 Overview

Quy trình phát hiện, đánh giá, ngăn chặn, và thông báo vi phạm dữ liệu theo HIPAA §164.400-414 và GDPR Art. 33-34.

### 2.2 End-to-End Flow

```
┌───────────┐    ┌──────────┐    ┌───────────┐    ┌──────────┐    ┌──────────┐
│  Detect   │───→│  Assess  │───→│  Contain  │───→│  Notify  │───→│  Review  │
│           │    │          │    │           │    │          │    │          │
│ Auto/Manual│   │ Impact   │    │ Isolate   │    │ 72h GDPR │    │ Lessons  │
│ Detection │    │ Scoring  │    │ Remediate │    │ 60d HIPAA│    │ Learned  │
└───────────┘    └──────────┘    └───────────┘    └──────────┘    └──────────┘
```

### 2.3 Detection Phase

#### Automated Detection (7 Signal Categories)

|  #  | Signal                  | Source                        | Threshold                      |
| :-: | ----------------------- | ----------------------------- | ------------------------------ |
|  1  | **IP Intelligence**     | ThreatDetectionService        | Score ≥ 70                     |
|  2  | **User Agent Analysis** | BotDetectionService           | Suspicious UA                  |
|  3  | **Impossible Travel**   | BehavioralAnalyticsService    | Same user, different geo, < 2h |
|  4  | **Brute Force**         | Rate limiting + login history | ≥ 5 failed attempts / 15 min   |
|  5  | **Anomalous Access**    | BehavioralAnalyticsService    | z-score > 3σ deviation         |
|  6  | **Input Validation**    | WAF / middleware              | SQL injection, XSS patterns    |
|  7  | **Time-Based**          | ConditionalAccessPolicy       | Access outside allowed hours   |

#### Manual Detection

- Staff báo cáo sự cố qua `POST /api/security/enterprise/incidents`
- Physical security incidents (mất thiết bị, truy cập trái phép vào phòng server)
- Vendor thông báo breach
- External researcher disclosure

### 2.4 Assessment Phase

```
              ┌─────────────────────────────┐
              │     BREACH ASSESSMENT       │
              ├─────────────────────────────┤
              │ ① Number of affected records│
              │ ② Type of data exposed      │
              │ ③ Was data encrypted?        │
              │ ④ Who had unauthorized access│
              │ ⑤ Duration of exposure       │
              │ ⑥ Potential for harm         │
              │ ⑦ Was data actually viewed?  │
              └──────────┬──────────────────┘
                         │
              ┌──────────┴──────────────────┐
              │   SEVERITY CLASSIFICATION   │
              ├─────────────────────────────┤
              │ CRITICAL: PHI/biometric     │───→ Immediate containment
              │ HIGH: PII bulk exposure     │───→ 4-hour containment
              │ MEDIUM: Internal data leak  │───→ 24-hour containment
              │ LOW: Minor policy violation │───→ 72-hour review
              └─────────────────────────────┘
```

- **API:** `POST /api/compliance/breaches/{id}/assess`
- **UI:** Compliance Dashboard → Breach Management → Assess

### 2.5 Containment Phase

| Action                | Description                      | API                                                                  |
| --------------------- | -------------------------------- | -------------------------------------------------------------------- |
| Account Lockout       | Khóa tài khoản bị compromise     | `POST /api/security/advanced/account-lockouts/{id}/unlock` (reverse) |
| Session Termination   | Kill all active sessions         | UserSession management                                               |
| IP Blocking           | Block suspicious IPs             | `POST /api/security/advanced/ip-whitelist`                           |
| Access Restriction    | Restrict patient data processing | `Patient.RestrictProcessing()`                                       |
| Key Rotation          | Rotate compromised keys/tokens   | KeyVaultService                                                      |
| Evidence Preservation | Snapshot logs, DB state          | AuditLogService                                                      |

- **API:** `POST /api/compliance/breaches/{id}/contain`

### 2.6 Notification Phase

#### GDPR (Art. 33-34)

| Requirement           | Deadline              | Recipient            | Content                                                         |
| --------------------- | --------------------- | -------------------- | --------------------------------------------------------------- |
| Supervisory Authority | ≤ 72 hours            | DPA                  | Nature, categories, approximate records, consequences, measures |
| Data Subjects         | "Without undue delay" | Affected individuals | If "high risk to rights and freedoms"                           |

#### HIPAA (§164.400-414)

| Requirement            | Deadline   | Recipient              | Content                                                      |
| ---------------------- | ---------- | ---------------------- | ------------------------------------------------------------ |
| Individuals            | ≤ 60 days  | Each affected patient  | Description, types of info, steps to protect, entity contact |
| HHS (< 500 records)    | Annual log | HHS OCR                | Aggregate report                                             |
| HHS (≥ 500 records)    | ≤ 60 days  | HHS OCR                | Detailed report                                              |
| Media (≥ 500 in state) | ≤ 60 days  | Prominent media outlet | Press release                                                |

- **API:** `POST /api/compliance/breaches/{id}/notify`

### 2.7 Post-Incident Review

- [ ] Root cause analysis completed
- [ ] Timeline reconstructed from audit logs
- [ ] Additional controls identified & implemented
- [ ] Staff re-training scheduled (if human error)
- [ ] Monitoring rules updated (if detection gap)
- [ ] Documentation updated
- [ ] Compliance health score recalculated

---

## 3. AI Model Governance Flow

### 3.1 AI Model Lifecycle

```
┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐
│   Draft    │───→│  Testing   │───→│  Staging   │───→│  Deployed  │───→│  Retired   │
│            │    │            │    │            │    │            │    │            │
│ Model dev  │    │ Validation │    │ Pre-prod   │    │ Production │    │ Archived   │
│ Training   │    │ Bias tests │    │ A/B test   │    │ Monitoring │    │ Documented │
└────────────┘    └────────────┘    └────────────┘    └────────────┘    └────────────┘
     │                 │                 │                 │                 │
     ▼                 ▼                 ▼                 ▼                 ▼
  Registry         Fairness          Sign-off          Continuous       Lessons
  Entry            Threshold         Committee         Bias Check       Learned
```

### 3.2 Bias Testing Protocol

#### Pre-Deployment Testing (Mandatory)

| Test                    | Metric                           | Threshold             | Action if Fail        |
| ----------------------- | -------------------------------- | --------------------- | --------------------- |
| **Demographic Parity**  | P(ŷ=1\|A=a) across groups        | < 0.1 difference      | Block deployment      |
| **Equalized Odds**      | FPR/FNR parity                   | Disparity ratio ≤ 1.2 | Review with committee |
| **Predictive Parity**   | Precision across groups          | < 0.15 difference     | Remediate & retest    |
| **Individual Fairness** | Similar inputs → similar outputs | Lipschitz bound       | Additional testing    |

#### Post-Deployment Monitoring

- **Frequency:** Weekly automated bias checks
- **API:** `GET /api/compliance/monitoring/ai-performance`
- **Alerts:** When FPR > 5% or FNR > 5% on any protected group
- **Dashboard:** AI Governance → Bias Tests tab

```
AI Model Performance Monitoring
┌─────────────────────────────────────────────┐
│ Model: Threat Detection v2.3               │
│ Status: Deployed 🚀                        │
│ Accuracy: 94.2%  Precision: 91.8%         │
│ Recall: 96.1%    F1: 93.9%               │
│                                             │
│ Protected Attributes:                       │
│ ├─ Gender: FPR disparity 1.02 ✅           │
│ ├─ Age group: FPR disparity 1.15 ⚠️        │
│ └─ Nationality: FPR disparity 0.98 ✅      │
│                                             │
│ ⚠ Age group approaching threshold (1.2)    │
│ → Recommended: retrain with balanced data   │
└─────────────────────────────────────────────┘
```

### 3.3 Model Deployment Checklist

- [ ] Version registered in AiModelVersion registry
- [ ] All bias tests passing (passesFairnessThreshold = true)
- [ ] FPR < 5% and FNR < 5% across all protected groups
- [ ] DPIA completed for new AI processing activities
- [ ] AI Ethics Committee sign-off obtained
- [ ] Rollback procedure documented and tested
- [ ] Monitoring alerts configured
- [ ] Previous version archived with rollback capability
- [ ] ROPA updated with new processing activity
- [ ] User documentation updated

### 3.4 Rollback Procedure

1. **Trigger:** Bias test failure, performance degradation, or Committee decision
2. **API:** `POST /api/compliance/ai/model-versions/{id}/rollback`
3. **Process:**
   - Current version status → `Rolled Back`
   - Previous version reactivated → `Deployed`
   - All active sessions re-routed to previous version
   - Incident record created
   - Root cause investigation initiated

---

## 4. Compliance Schedule & Task Flow

### 4.1 Recurring Task Framework

```
┌──────────────────────────────────────────────────────────┐
│              COMPLIANCE TASK LIFECYCLE                     │
├──────────────────────────────────────────────────────────┤
│                                                           │
│  ┌─────────┐    ┌──────────┐    ┌──────────┐             │
│  │  Seed   │───→│  Active  │───→│ Complete │             │
│  │ Default │    │ Assigned │    │ Verified │             │
│  └─────────┘    └──────────┘    └──────────┘             │
│       │              │              │                     │
│       ▼              ▼              ▼                     │
│  Auto-create    Overdue check   Next occurrence           │
│  from templates 7-day warning   auto-generated           │
│                                                           │
│  Frequencies:                                            │
│  • Daily    — Security log review                        │
│  • Weekly   — Vulnerability scan review                  │
│  • Monthly  — Access rights review, training compliance  │
│  • Quarterly — Risk assessment, policy review            │
│  • Annual   — Full audit, BCP/DRP test, vendor review    │
│                                                           │
└──────────────────────────────────────────────────────────┘
```

### 4.2 Default Compliance Tasks

| Task                       | Frequency   | Category       | Framework            | Owner              |
| -------------------------- | ----------- | -------------- | -------------------- | ------------------ |
| Security log review        | Daily       | Security       | SOC 2 CC7.2          | Security Analyst   |
| Vulnerability scan review  | Weekly      | Security       | ISO 27001 A.8.8      | CISO               |
| Overdue DSR check          | Weekly      | Privacy        | GDPR Art. 12         | DPO                |
| Access rights review       | Monthly     | Access Control | SOC 2 CC6.2          | IT Admin           |
| Training compliance check  | Monthly     | Training       | HIPAA §164.308(a)(5) | HR                 |
| AI bias test review        | Monthly     | AI Governance  | NIST AI RMF MEASURE  | AI Committee       |
| Risk assessment update     | Quarterly   | Risk           | ISO 27005            | CISO               |
| Policy review              | Quarterly   | Governance     | ISO 27001 Clause 10  | Compliance Officer |
| BCP/DRP test               | Semi-Annual | BC/DR          | ISO 22301            | IT Manager         |
| Full internal audit        | Annual      | Audit          | ISO 27001 Clause 9.2 | Internal Auditor   |
| Vendor risk re-assessment  | Annual      | Third Party    | SOC 2 CC9.2          | Procurement        |
| DPIA review                | Annual      | Privacy        | GDPR Art. 35         | DPO                |
| Penetration test           | Annual      | Security       | SOC 2 CC7.1          | External vendor    |
| Data retention enforcement | Quarterly   | Data Mgmt      | HIPAA/GDPR           | Data Steward       |

### 4.3 Overdue Escalation

| Days Overdue | Action                                           | Notification               |
| :----------: | ------------------------------------------------ | -------------------------- |
|     1-3      | Email reminder to assignee                       | System notification        |
|     4-7      | Escalate to manager                              | Manager notification       |
|     8-14     | Escalate to Compliance Officer                   | Dashboard alert (Warning)  |
|     15+      | Critical alert, compliance health score impacted | Dashboard alert (Critical) |

---

## 5. Incident Response Flow

### 5.1 Incident Classification

|    Severity     | Definition                                          | Response Time | Examples                                                  |
| :-------------: | --------------------------------------------------- | :-----------: | --------------------------------------------------------- |
| **P1 Critical** | Active breach, data exfiltration, system compromise |    15 min     | Ransomware, unauthorized PHI access, credential theft     |
|   **P2 High**   | Potential breach, vulnerability exploitation        |    1 hour     | Failed brute force, suspicious admin activity, WAF alerts |
|  **P3 Medium**  | Policy violation, anomalous behavior                |    4 hours    | Excessive data downloads, after-hours access, failed MFA  |
|   **P4 Low**    | Minor policy deviation, informational               |   24 hours    | Password policy violation, unused account activity        |

### 5.2 Response Flow

```
Detection → Triage → Investigate → Contain → Eradicate → Recover → Review
   │           │          │            │          │           │         │
   ▼           ▼          ▼            ▼          ▼           ▼         ▼
Auto/Manual  Severity   Evidence    Isolate    Root cause  Restore   Lessons
ThreatDet.   Classify   Preserve   Systems    Remove       Services  Learned
BehavAnal.   Assign     Timeline   Accounts   Vulnerability Validate Document
CondAccess   Notify     Scope      Network    Patch         Monitor  Train
```

### 5.3 Automated Response Rules

| Trigger                        | Auto-Action                                     | Manual Follow-Up                   |
| ------------------------------ | ----------------------------------------------- | ---------------------------------- |
| Brute force (5+ failed logins) | Account lockout, IP temporary block             | Investigate source, audit logs     |
| Impossible travel detected     | Session termination, re-authentication required | Review access patterns             |
| Anomalous data download        | Access restriction, alert to manager            | Volume analysis, intent assessment |
| Malicious input detected       | Request blocked, IP logged                      | WAF rule update, pentest           |
| Unauthorized API key usage     | Key revocation, new key generation              | API key audit, access review       |
| Zero Trust score > 80          | Request blocked with challenge                  | Risk assessment update             |

### 5.4 Evidence Preservation

| Evidence           | Source                   | Retention | Format             |
| ------------------ | ------------------------ | --------- | ------------------ |
| Audit logs         | PostgreSQL (partitioned) | 7 years   | Structured JSON    |
| User sessions      | Redis + DB               | 1 year    | Session records    |
| Network logs       | Infrastructure           | 90 days   | System logs        |
| Security events    | SecurityEvent table      | 3 years   | Structured records |
| Application logs   | .NET logging             | 1 year    | Text/JSON          |
| Database snapshots | PostgreSQL WAL           | 30 days   | Binary backup      |

---

## 6. Data Retention & Deletion Flow

### 6.1 Retention Schedule

| Data Category         |          Retention Period           | Legal Basis       | Deletion Method            |
| --------------------- | :---------------------------------: | ----------------- | -------------------------- |
| Medical records (PHI) |     6 years post last treatment     | HIPAA §164.530(j) | Pseudonymize identifiers   |
| Financial records     |               7 years               | Tax regulations   | Archive then purge         |
| Audit logs            |               7 years               | SOC 2 CC7.2       | Auto-partition, no pruning |
| Biometric templates   | Until consent withdrawal + 30 days  | GDPR Art. 17      | Cryptographic erasure      |
| User sessions         |               90 days               | Operational need  | Auto-purge                 |
| Consent records       |  Duration of processing + 5 years   | GDPR Art. 7(1)    | Archive                    |
| AI training data      |      Model lifecycle + 3 years      | NIST AI RMF       | De-identify                |
| Backup files          | 90 days (primary), 1 year (archive) | BCP/DRP           | Secure deletion            |
| Security events       |               3 years               | SOC 2, ISO 27001  | Auto-partition             |

### 6.2 Deletion Flow

```
┌────────────┐    ┌───────────────┐    ┌──────────────┐    ┌────────────┐
│  Identify  │───→│  Legal Check  │───→│   Execute    │───→│   Verify   │
│  Expired   │    │  Retention    │    │   Deletion   │    │   & Audit  │
│  Data      │    │  Conflicts    │    │              │    │            │
└────────────┘    └───────────────┘    └──────────────┘    └────────────┘
     │                  │                    │                    │
     ▼                  ▼                    ▼                    ▼
  DataRetention     HIPAA > GDPR?        Pseudonymize or      Deletion
  Policy check      Litigation hold?     Cryptographic erase  certificate
```

### 6.3 Pseudonymization vs. Erasure Decision Tree

```
Data expired? ──→ NO ──→ Continue retention
     │
    YES
     │
Medical record? ──→ YES ──→ HIPAA retention still active? ──→ YES ──→ Keep
     │                            │
     │                           NO
     │                            │
     │                            ▼
     │                     Pseudonymize (replace PII with GUID,
     │                     keep medical data for research)
     │
    NO ──→ Biometric template? ──→ YES ──→ Cryptographic erasure
     │                                      (destroy encryption key)
     │
    NO ──→ Standard PII? ──→ Full deletion (DB + backups + MinIO)
```

---

## 7. Asset Registration & Classification Flow

### 7.1 Asset Lifecycle

```
Discovery → Register → Classify → Assign Owner → Monitor → Retire
    │           │           │           │            │          │
    ▼           ▼           ▼           ▼            ▼          ▼
  Scan/      Catalog     Data        Department   Compliance  Secure
  Manual     in system   class.      head         audits      disposal
```

### 7.2 Classification Process

| Step | Action                                                                                            | Criteria                                      |
| :--: | ------------------------------------------------------------------------------------------------- | --------------------------------------------- |
|  1   | Xác định loại asset (Database, Application, Server, Workstation, Mobile, Cloud Service, Document) | Physical or logical                           |
|  2   | Xác định dữ liệu được lưu trữ/xử lý                                                               | PHI? PII? Financial? Internal?                |
|  3   | Phân loại theo highest data sensitivity                                                           | Restricted > Confidential > Internal > Public |
|  4   | Gán owner (department head hoặc system admin)                                                     | RACI matrix                                   |
|  5   | Áp dụng controls theo classification level                                                        | See control requirements                      |

### 7.3 Classification Controls

|  Classification  |             Encryption              |          Access           |        Audit        |  Incident   |
| :--------------: | :---------------------------------: | :-----------------------: | :-----------------: | :---------: |
|  **Restricted**  | AES-256 at rest, TLS 1.3 in transit |  MFA + RBAC + Zero Trust  |    Every access     | P1 Critical |
| **Confidential** | AES-256 at rest, TLS 1.3 in transit | RBAC + conditional access |  Write/delete ops   |   P2 High   |
|   **Internal**   |         TLS 1.3 in transit          |           RBAC            | Significant changes |  P3 Medium  |
|    **Public**    |         TLS 1.3 in transit          |      Open / API key       |        None         |   P4 Low    |

---

## 8. Training & Certification Flow

### 8.1 Training Lifecycle

```
┌────────────┐    ┌────────────┐    ┌────────────┐    ┌────────────┐
│   Assign   │───→│   Study    │───→│    Test    │───→│  Certify   │
│            │    │            │    │            │    │            │
│ By Manager │    │ Self-paced │    │ Scored     │    │ Certificate│
│ or Auto    │    │ Content    │    │ Pass/Fail  │    │ Expiry date│
└────────────┘    └────────────┘    └────────────┘    └────────────┘
```

### 8.2 Required Training Programs

| Training                       | Audience       | Frequency   |  Pass Threshold  | Framework            |
| ------------------------------ | -------------- | ----------- | :--------------: | -------------------- |
| HIPAA Privacy & Security       | All staff      | Annual      |       80%        | HIPAA §164.308(a)(5) |
| GDPR Data Protection           | All staff      | Annual      |       80%        | GDPR Art. 39(1)(b)   |
| Information Security Awareness | All staff      | Annual      |       75%        | ISO 27001 A.6.3      |
| Incident Response Procedures   | IT + Security  | Semi-Annual |       85%        | SOC 2 CC7.4          |
| Data Handling & Classification | Clinical staff | Annual      |       80%        | ISO 27001 A.5.12     |
| AI Ethics & Bias Recognition   | AI team        | Annual      |       85%        | NIST AI RMF GOVERN   |
| Phishing Awareness             | All staff      | Quarterly   | N/A (simulation) | HITRUST 02.e         |
| Secure Coding Practices        | Developers     | Annual      |       80%        | OWASP Top 10         |

### 8.3 Non-Compliance Escalation

| Status                          | Action                                      |
| ------------------------------- | ------------------------------------------- |
| Assignment missed (not started) | Reminder email on day 1, 7, 14              |
| Failed test (< threshold)       | Allow 2 retakes within 14 days              |
| Overdue (past due date)         | Access restricted notification to manager   |
| 30+ days overdue                | Account access review, formal warning       |
| Persistent non-compliance       | Escalate to HR, potential access revocation |

---

## 9. Vendor Onboarding Flow

### 9.1 Vendor Assessment Process

```
┌─────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Request   │───→│  Risk Assess │───→│   Contract   │───→│   Monitor    │
│   Intake    │    │  & Due Dili. │    │   & BAA/DPA  │    │   & Review   │
└─────────────┘    └──────────────┘    └──────────────┘    └──────────────┘
```

### 9.2 Vendor Risk Scoring

| Factor                    | Weight | Scoring                                               |
| ------------------------- | :----: | ----------------------------------------------------- |
| Data access level         |  30%   | Restricted=10, Confidential=7, Internal=4, None=0     |
| Service criticality       |  25%   | Core=10, Supporting=6, Ancillary=2                    |
| Compliance certifications |  20%   | SOC 2 + ISO 27001 = 0, One cert = 4, None = 10        |
| Geographic risk           |  15%   | Adequate jurisdiction=0, SCC required=5, High risk=10 |
| Incident history          |  10%   | None=0, Minor=4, Major=8, Critical=10                 |

### 9.3 Contractual Requirements

| Vendor Risk  |    BAA/DPA     |       SCC       |  Audit Rights  | Insurance | Review Freq |
| :----------: | :------------: | :-------------: | :------------: | :-------: | :---------: |
| **Critical** |    Required    | If cross-border | Annual on-site |   $5M+    |  Quarterly  |
|   **High**   |    Required    | If cross-border | Annual remote  |   $2M+    | Semi-Annual |
|  **Medium**  | If data access | If cross-border |    Biennial    |   $1M+    |   Annual    |
|   **Low**    |      N/A       |       N/A       |   On request   | Standard  |  Biennial   |

---

## 10. Audit Preparation Flow

### 10.1 Audit Types & Timeline

| Audit Type                         | Framework | Frequency         | Duration    | Lead Time |
| ---------------------------------- | --------- | ----------------- | ----------- | --------- |
| ISO 27001 Stage 1 (Documentation)  | ISO 27001 | One-time          | 2-3 days    | 3 months  |
| ISO 27001 Stage 2 (Implementation) | ISO 27001 | One-time          | 3-5 days    | 6 months  |
| ISO 27001 Surveillance             | ISO 27001 | Annual            | 1-2 days    | 2 months  |
| SOC 2 Type I (Point-in-time)       | SOC 2     | One-time          | 1-2 weeks   | 3 months  |
| SOC 2 Type II (Observation)        | SOC 2     | Annual            | 6-12 months | 6 months  |
| HITRUST e1 Assessment              | HITRUST   | Biennial          | 2-4 weeks   | 4 months  |
| Internal Audit                     | ISO 27001 | Annual            | 1-2 weeks   | 1 month   |
| HIPAA Compliance Review            | HIPAA     | Annual (internal) | 1 week      | 1 month   |

### 10.2 Evidence Collection Matrix

| Control Area       | Evidence Type                         | Source                    | Collection      |
| ------------------ | ------------------------------------- | ------------------------- | --------------- |
| Access Control     | User access lists, role assignments   | DB export                 | Auto via API    |
| Encryption         | TLS certificates, encryption configs  | System configs            | Manual + script |
| Audit Logs         | Sample audit trail entries            | PostgreSQL partitions     | Auto via API    |
| Training           | Completion records, test scores       | ComplianceTraining table  | Auto via API    |
| Incident Response  | Incident tickets, response times      | SecurityIncident table    | Auto via API    |
| Change Management  | Git commit history, PR reviews        | GitHub API                | Auto via CI     |
| Vulnerability Mgmt | Scan reports, patch records           | Vanta / manual scans      | Semi-auto       |
| BCP/DRP            | Test results, recovery records        | bcp_drp.md + test logs    | Manual          |
| Vendor Management  | BAAs, risk assessments, due diligence | vendor_risk_assessment.md | Manual          |
| Policy Documents   | All policy versions, approval records | docs/compliance/          | Auto via Git    |

### 10.3 Pre-Audit Checklist

- [ ] Compliance health score ≥ 85%
- [ ] All overdue DSRs resolved (0 overdue)
- [ ] All compliance schedule tasks current (0 overdue)
- [ ] Training compliance rate ≥ 90%
- [ ] AI bias tests all passing
- [ ] Security incidents resolved or documented
- [ ] All policies reviewed within last 12 months
- [ ] ROPA up to date
- [ ] BCP/DRP tested within last 12 months
- [ ] Vendor risk assessments current
- [ ] Evidence repository organized and accessible
- [ ] Staff briefed on audit procedures

---

## 11. Continuous Monitoring Flow

### 11.1 Health Score Dashboard

```
┌─────────────────────────────────────────────────────────────┐
│                  COMPLIANCE HEALTH SCORE                     │
│                                                              │
│                    ┌────────────┐                            │
│                    │  88/100    │                            │
│                    │  ⚠️ Warning │                            │
│                    └────────────┘                            │
│                                                              │
│  Components:                                                 │
│  ├─ DSR Compliance (25%): 95 ✅  (0 overdue / 20 total)    │
│  ├─ Task Completion (25%): 82 ⚠️  (3 overdue / 45 total)   │
│  ├─ Security Incidents (20%): 90 ✅  (1 open / 12 resolved)│
│  ├─ Training Rate (15%): 88 ⚠️  (42/48 completed)          │
│  └─ AI Bias Pass Rate (15%): 85 ⚠️  (17/20 passing)       │
│                                                              │
│  Status Thresholds:                                         │
│  90-100 ✅ Healthy  │  70-89 ⚠️ Warning  │  <70 🔴 Critical │
└─────────────────────────────────────────────────────────────┘
```

### 11.2 Monitoring Cadence

| Metric              | Frequency | Source                  |   Alert Threshold    |
| ------------------- | --------- | ----------------------- | :------------------: |
| Health score        | Real-time | ComplianceScoringEngine |         < 85         |
| DSR compliance rate | Daily     | DSR dashboard           |     Any overdue      |
| Overdue tasks       | Daily     | ComplianceSchedule      |         > 0          |
| Security incidents  | Real-time | IncidentResponseService |      Any P1/P2       |
| Training compliance | Weekly    | ComplianceTraining      |        < 85%         |
| AI bias tests       | Weekly    | AiBiasTestResult        |     Any failure      |
| Audit readiness     | Monthly   | Audit checklist         |        < 80%         |
| Vendor compliance   | Quarterly | Vendor assessments      | Any critical finding |

### 11.3 Alert Response Playbook

| Alert                            | Immediate Action                                    | Owner              |  SLA   |
| -------------------------------- | --------------------------------------------------- | ------------------ | :----: |
| Health score dropped to Warning  | Review dashboard components, identify root cause    | Compliance Officer |  24h   |
| Health score dropped to Critical | Emergency compliance review, escalate to management | CISO + CO          |   4h   |
| DSR overdue                      | Contact assignee, escalate if no response in 24h    | DPO                |   1h   |
| AI bias test failed              | Pause model, initiate investigation                 | AI Committee       |   4h   |
| Security incident P1             | Activate incident response, contain immediately     | CISO               | 15 min |
| Training compliance < 80%        | Send reminders, restrict access for non-compliant   | HR + IT            |  48h   |

---

## 12. Consent Management Flow

### 12.1 Consent Categories

| Category         | Legal Basis                           |   Revocable   | IVF Context                    |
| ---------------- | ------------------------------------- | :-----------: | ------------------------------ |
| **Treatment**    | HIPAA TPO / GDPR Art. 9(2)(h)         | No (required) | Core medical treatment         |
| **Research**     | GDPR Art. 6(1)(a) explicit consent    |      Yes      | De-identified data for studies |
| **Marketing**    | GDPR Art. 6(1)(a) consent             |      Yes      | Newsletters, follow-up offers  |
| **Analytics**    | GDPR Art. 6(1)(f) legitimate interest |    Opt-out    | System usage analytics         |
| **Biometric**    | GDPR Art. 9(2)(a) explicit consent    |      Yes      | Fingerprint enrollment         |
| **Data Sharing** | GDPR Art. 6(1)(a) consent             |      Yes      | Sharing with partner clinics   |

### 12.2 Consent Lifecycle

```
Collect → Store → Verify → Update → Withdraw → Audit
   │        │        │        │         │          │
   ▼        ▼        ▼        ▼         ▼          ▼
Explicit  UserConsent Check   Re-consent Process   Complete
Informed  entity    before   when terms within    trail of
Specific  versioned each use  change    30 days   all changes
```

### 12.3 Withdrawal Process

1. **Request:** Patient requests consent withdrawal (verbal, written, or portal)
2. **Scope:** Identify which consent categories are being withdrawn
3. **Legal Check:** Verify no mandatory basis (HIPAA TPO) prevents withdrawal
4. **Process:**
   - Mark UserConsent record as withdrawn
   - Stop all processing under that consent
   - If biometric withdrawal: delete fingerprint templates
   - If research withdrawal: remove from active studies, retain pseudonymized existing data
5. **Confirm:** Written confirmation to patient within 30 days
6. **Audit:** Log all actions in audit trail

---

_Next: Read [Implementation & Deployment Guide](compliance_implementation_deployment.md) for practical deployment procedures._
