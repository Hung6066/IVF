# Lộ trình Nâng cấp Toàn diện & Đạt Chứng chỉ Quốc tế — IVF Information System

> **Document ID:** IVF-CERT-ROADMAP-001  
> **Version:** 1.0  
> **Ngày tạo:** 2026-03-04  
> **Phân loại:** CONFIDENTIAL  
> **Chủ sở hữu:** Compliance Officer / CISO  
> **Chu kỳ review:** Monthly

---

## Mục lục

1. [Tổng quan hiện trạng](#1-tổng-quan-hiện-trạng)
2. [Ma trận chứng chỉ mục tiêu](#2-ma-trận-chứng-chỉ-mục-tiêu)
3. [Phase 1 — Nền tảng & HIPAA + GDPR (Tháng 3–4/2026)](#3-phase-1--nền-tảng--hipaa--gdpr-tháng-34)
4. [Phase 2 — SOC 2 Type I (Tháng 4–6/2026)](#4-phase-2--soc-2-type-i-tháng-46)
5. [Phase 3 — ISO 27001:2022 (Tháng 5–8/2026)](#5-phase-3--iso-270012022-tháng-58)
6. [Phase 4 — SOC 2 Type II (Tháng 6–12/2026)](#6-phase-4--soc-2-type-ii-tháng-612)
7. [Phase 5 — HITRUST CSF i1 (Tháng 9/2026–2/2027)](#7-phase-5--hitrust-csf-i1-tháng-92026227)
8. [Phase 6 — NIST AI RMF Level 4 + ISO 42001 (Tháng 1–6/2027)](#8-phase-6--nist-ai-rmf-level-4--iso-42001-tháng-162027)
9. [Chi tiết kỹ thuật cần nâng cấp](#9-chi-tiết-kỹ-thuật-cần-nâng-cấp)
10. [Ngân sách & Chi phí](#10-ngân-sách--chi-phí)
11. [Ma trận trách nhiệm (RACI)](#11-ma-trận-trách-nhiệm-raci)
12. [KPI & Metrics theo dõi](#12-kpi--metrics-theo-dõi)
13. [Bảng tham chiếu tài liệu](#13-bảng-tham-chiếu-tài-liệu)
14. [Checklist tổng quan](#14-checklist-tổng-quan)

---

## 1. Tổng quan hiện trạng

### 1.1 Điểm tuân thủ hiện tại (03/2026)

| Framework          | Điểm hiện tại | Điểm mục tiêu | Gap | Thời gian ước tính |
| :----------------- | :-----------: | :-----------: | :-: | :----------------: |
| **HIPAA**          |     94%       |    97%+       |  3% |     4 tuần         |
| **GDPR**           |     92%       |    96%+       |  4% |     4 tuần         |
| **SOC 2 Type II**  |     90%       |    95%+       |  5% |    6–12 tháng      |
| **ISO 27001:2022** |     90%       |    95%+       |  5% |    4–6 tháng       |
| **HITRUST CSF v11**|     84%       |    90%+       |  6% |    6–9 tháng       |
| **NIST AI RMF**    |     84%       |    90%+       |  6% |    6–9 tháng       |
| **ISO 42001:2023** |     80%       |    90%+       | 10% |    8–12 tháng      |

### 1.2 Những gì đã hoàn thành

```
✅ 79 compliance API endpoints
✅ 6 giao diện UI quản lý tuân thủ  
✅ 30 tài liệu tuân thủ chi tiết
✅ Trust Page công khai (localhost:4200/trust)
✅ Zero Trust Architecture (7 threat signals)
✅ Automated incident response
✅ Evidence collection script (collect-evidence.ps1)
✅ ComplianceScoringEngine (real-time scoring)
✅ AI Governance framework (model versioning, bias testing)
✅ 3-2-1 backup strategy với PITR
✅ Field-level encryption (AES-256-GCM)
✅ 50+ security event types
✅ Internal PKI / Certificate Authority
```

### 1.3 Gap tổng hợp theo loại

| Loại Gap                          | Số lượng | Ảnh hưởng             |
| --------------------------------- | :------: | -------------------- |
| Quy trình tổ chức (process)      |    8     | Tất cả frameworks    |
| Tài liệu bổ sung (documentation) |    5     | ISO 27001, SOC 2     |
| Bằng chứng thực tế (evidence)    |    3     | SOC 2, HITRUST       |
| Nâng cấp kỹ thuật (technical)    |    4     | NIST AI, ISO 42001   |
| Audit bên ngoài (external audit) |    3     | SOC 2, ISO, HITRUST  |

---

## 2. Ma trận chứng chỉ mục tiêu

### 2.1 Thứ tự ưu tiên đạt chứng chỉ

```
Tháng 3-4     Tháng 4-6       Tháng 5-8       Tháng 6-12       Tháng 9+         2027
    │             │               │               │               │               │
    ▼             ▼               ▼               ▼               ▼               ▼
┌────────┐  ┌──────────┐  ┌────────────┐  ┌──────────────┐  ┌──────────┐  ┌──────────┐
│ HIPAA  │  │ SOC 2    │  │ ISO 27001  │  │ SOC 2        │  │ HITRUST  │  │ NIST AI  │
│ GDPR   │  │ Type I   │  │ Stage 1+2  │  │ Type II      │  │ CSF i1   │  │ ISO42001 │
│ (self) │  │          │  │            │  │ (6mo obs.)   │  │          │  │          │
└────────┘  └──────────┘  └────────────┘  └──────────────┘  └──────────┘  └──────────┘
  Miễn phí    $8K-15K       $10K-20K        $15K-25K        $30K-50K      $10K-20K
```

### 2.2 Tại sao thứ tự này?

| # | Chứng chỉ | Lý do ưu tiên |
|---|-----------|---------------|
| 1 | HIPAA + GDPR | Miễn phí, tự đánh giá, đã đạt 92-94%, hoàn thành nhanh nhất |
| 2 | SOC 2 Type I | Phổ biến nhất, chỉ đánh giá 1 thời điểm, cần cho khách hàng enterprise |
| 3 | ISO 27001 | Uy tín toàn cầu, 90% sẵn sàng, CB có thể audit cùng ISO 42001 sau |
| 4 | SOC 2 Type II | Upgrade từ Type I, cần 6 tháng observation |
| 5 | HITRUST i1 | Yêu cầu maturity cao, cần thời gian tích lũy metrics |
| 6 | NIST AI + ISO 42001 | Mới nhất, ít tổ chức đạt, lợi thế cạnh tranh lớn |

---

## 3. Phase 1 — Nền tảng & HIPAA + GDPR (Tháng 3–4/2026)

**Mục tiêu:** Hoàn thiện self-assessment, đạt compliance status chính thức, xây nền tảng cho tất cả phases sau.

### 3.1 HIPAA — Từ 94% lên 97%

| # | Hành động | Chi tiết | Trạng thái | Deadline |
|---|-----------|----------|:----------:|----------|
| 1 | **Hoàn thành Security Risk Assessment** | Sử dụng HHS SRA Tool (miễn phí), map vào `risk_assessment.md` đã có | ⬜ | W1 |
| 2 | **BAA với vendors** | Ký BAA với: PostgreSQL hosting, MinIO/S3, Email provider | ⬜ | W2 |
| 3 | **Physical safeguards documentation** | Tài liệu hóa biện pháp vật lý cho server room / cloud datacenter | ⬜ | W2 |
| 4 | **Employee training records** | Tạo training log trong hệ thống (endpoint `/api/compliance/training` đã có) | ⬜ | W3 |
| 5 | **Contingency plan testing** | Chạy BCP/DRP drill, ghi nhận kết quả vào `bcp_drp.md` | ⬜ | W3 |
| 6 | **Disposable media policy** | Bổ sung chính sách xử lý phương tiện lưu trữ vào ISP | ⬜ | W4 |

**Tài liệu tham chiếu:**
- `docs/compliance/hipaa_self_assessment.md` — HIPAA self-assessment hiện tại
- `docs/compliance/bcp_drp.md` — Business Continuity Plan
- `docs/compliance/breach_notification_sop.md` — Breach notification SOP (60 ngày)

### 3.2 GDPR — Từ 92% lên 96%

| # | Hành động | Chi tiết | Trạng thái | Deadline |
|---|-----------|----------|:----------:|----------|
| 1 | **Chỉ định DPO chính thức** | Bổ nhiệm bằng văn bản, thông báo cho DPA (nếu cần) | ⬜ | W1 |
| 2 | **Hoàn thiện Art. 20 — Data Portability** | Endpoint export dữ liệu bệnh nhân (JSON/CSV) qua DSR | ⬜ | W1 |
| 3 | **Art. 18 — Restriction of Processing** | Thêm flag `processingRestricted` cho Patient entity | ⬜ | W2 |
| 4 | **Art. 11 — Pseudonymization formalization** | Activate `pseudonymization_procedures.md`, tạo SOP vận hành | ⬜ | W2 |
| 5 | **Cookie consent (nếu có web)** | Implement cookie banner cho Trust Page + public pages | ⬜ | W3 |
| 6 | **DPA registration** | Đăng ký với cơ quan bảo vệ dữ liệu cá nhân (Vietnam/EU nếu applicable) | ⬜ | W4 |
| 7 | **Cross-border transfer mechanisms** | Finalize SCCs cho EU↔VN data transfer (template đã có) | ⬜ | W4 |

**Tài liệu tham chiếu:**
- `docs/compliance/gdpr_readiness_assessment.md` — GDPR article-by-article
- `docs/compliance/dpia.md` — Data Protection Impact Assessment
- `docs/compliance/ropa_register.md` — Records of Processing Activities
- `docs/compliance/privacy_notice.md` — Privacy Notice (VN+EN)
- `docs/compliance/dpo_charter.md` — DPO Charter
- `docs/compliance/standard_contractual_clauses.md` — SCCs

### 3.3 Nền tảng chung (dùng cho tất cả phases sau)

| # | Hành động | Ảnh hưởng | Trạng thái | Deadline |
|---|-----------|-----------|:----------:|----------|
| 1 | **Tích hợp SAST vào CI/CD** | Thêm `dotnet-security-scan` + `npm audit` vào GitHub Actions | ⬜ | W1 |
| 2 | **Tích hợp DAST** | Thêm OWASP ZAP scan vào pipeline (scheduled weekly) | ⬜ | W2 |
| 3 | **Security awareness training** | Tạo 4 module training, assign cho tất cả users | ⬜ | W2 |
| 4 | **Populate evidence folders** | Điền thực evidence vào `access_control/`, `incident_response/`, `training/` | ⬜ | W3 |
| 5 | **Vulnerability scanning schedule** | Trivy (Docker), Dependabot (GitHub), weekly scan report | ⬜ | W3 |
| 6 | **Management commitment letter** | CEO/CTO ký cam kết bảo mật thông tin | ⬜ | W1 |
| 7 | **RACI Matrix** | Assign security roles và trách nhiệm cho team | ⬜ | W1 |

**Chi phí Phase 1: $0–500** (chủ yếu thời gian nội bộ)

---

## 4. Phase 2 — SOC 2 Type I (Tháng 4–6/2026)

**Mục tiêu:** Đạt SOC 2 Type I certification — đánh giá controls tại một thời điểm.

### 4.1 Chuẩn bị (Tháng 4 — 4 tuần)

| # | Hành động | Chi tiết | Trạng thái |
|---|-----------|----------|:----------:|
| 1 | **Chọn Trust Service Criteria (TSC)** | Security (bắt buộc) + Availability + Confidentiality | ⬜ |
| 2 | **Control documentation** | Map 50+ controls vào SOC 2 criteria using `soc2_readiness.md` | ⬜ |
| 3 | **Risk assessment formal** | Finalize formal risk assessment (đã có template) | ⬜ |
| 4 | **Vendor risk assessments** | Đánh giá top 5 vendors: PostgreSQL, MinIO, GitHub, npm, DigitalPersona | ⬜ |
| 5 | **Chạy internal audit** | Sử dụng `internal_audit_report.md` template | ⬜ |
| 6 | **Penetration test** | Chạy formal pentest, sử dụng `penetration_test_report_template.md` | ⬜ |

### 4.2 Chọn Auditor — So sánh

| Auditor | Chi phí Type I | Timeline | Ưu điểm | Nhược điểm |
|---------|:-------------:|:--------:|----------|-----------|
| **A-LIGN** | $8K–12K | 4–6 tuần | Phổ biến nhất cho startup, nhanh | Mid-tier |
| **Schellman** | $10K–15K | 6–8 tuần | Uy tín cao, healthcare expertise | Đắt hơn |
| **Coalfire** | $12K–18K | 6–8 tuần | Enterprise-grade, multi-framework | Đắt nhất |
| **Johanson Group** | $6K–10K | 4–6 tuần | Giá tốt cho startup nhỏ | Ít tên tuổi |
| **Sensiba San Filippo** | $8K–12K | 4–6 tuần | SaaS + healthcare focus | Trung bình |

**Khuyến nghị cho startup nhỏ: A-LIGN hoặc Johanson Group**

### 4.3 Engagement Process

```
Tuần 1-2: Ký engagement letter → Auditor gửi control questionnaire
Tuần 3-4: Thu thập evidence → Upload lên portal (hoặc gửi qua email)
Tuần 5-6: Auditor review → Remediation (nếu có findings)
Tuần 7-8: Final report → SOC 2 Type I report issued
```

### 4.4 Evidence Package cần chuẩn bị

| Control Area | Evidence cần | Nguồn trong hệ thống |
|--------------|-------------|----------------------|
| **CC6.1 Logical Access** | User list, RBAC config, MFA records | `GET /api/users`, jwt config, MFA enrollment |
| **CC6.6 Privileged Access** | Admin access review, impersonation logs | AuditLog, ImpersonationRequest records |
| **CC7.2 System Monitoring** | Security event logs (30 ngày) | `GET /api/security-events`, SecurityEvent table |
| **CC7.4 Incident Response** | Incident records, response procedures | SecurityIncident, `breach_notification_sop.md` |
| **CC8.1 Change Management** | Git history, CI/CD logs, deployment records | GitHub Actions, `evidence/change_management/` |
| **CC9.1 Risk Assessment** | Risk register, assessment report | `risk_assessment.md`, ThreatAssessment records |
| **A1.1 Availability** | Backup logs, recovery testing | Backup records, `bcp_drp.md` |
| **PI1.1 Privacy** | Encryption config, consent records | EncryptionConfig, UserConsent table |

**Công cụ automation:**
```bash
# Chạy evidence collection script
.\scripts\collect-evidence.ps1

# Output → docs/compliance/evidence/
```

**Chi phí Phase 2: $8,000–15,000**

---

## 5. Phase 3 — ISO 27001:2022 (Tháng 5–8/2026)

**Mục tiêu:** Đạt ISO 27001:2022 certification (3 năm validity).

### 5.1 Chọn Certification Body (CB)

| CB | Chi phí | Accreditation | Healthcare exp. |
|----|:-------:|:-------------:|:---------------:|
| **BSI** | $12K–20K | UKAS (UK) | ✅ Rất tốt |
| **TÜV SÜD** | $10K–18K | DAkkS (DE) | ✅ Tốt |
| **Bureau Veritas** | $10K–16K | UKAS/COFRAC | ✅ Tốt |
| **SGS** | $8K–15K | SAS/UKAS | ✅ Khá |
| **DNV** | $8K–14K | Akkreditert (NO) | ⚠️ Trung bình |

**Khuyến nghị: SGS hoặc Bureau Veritas** (giá tốt, coverage toàn cầu)

### 5.2 Lộ trình Stage 1 + Stage 2

```
Tháng 5 (Tuần 1–2): Ký hợp đồng với CB
                      ↓
Tháng 5 (Tuần 3–4): Stage 1 Audit — Document Review
                     • CB review ISMS documentation
                     • Đánh giá scope, Statement of Applicability (SoA)
                     • Xác nhận sẵn sàng cho Stage 2
                      ↓
Tháng 6 (Tuần 1–2): Remediation — Fix Stage 1 findings (nếu có)
                      ↓
Tháng 6–7:          Stage 2 Audit — Implementation Review (3–5 ngày on-site/remote)
                     • Kiểm tra controls hoạt động thực tế
                     • Phỏng vấn nhân viên
                     • Sample testing evidence
                      ↓
Tháng 7–8:          Certification Decision
                     • CB review audit report
                     • Cấp chứng nhận ISO 27001:2022
                      ↓
Hàng năm:           Surveillance Audit (Year 1, Year 2)
                      ↓
Năm thứ 3:          Recertification Audit
```

### 5.3 Chuẩn bị cho Stage 1 — ISMS Documentation

| # | Tài liệu bắt buộc | Tình trạng | File tham chiếu |
|---|-------------------|:----------:|-----------------|
| 1 | **ISMS Scope** | ✅ Có | `compliance_master_guide.md` §2.2 |
| 2 | **Information Security Policy** | ✅ Có | `information_security_policy.md` |
| 3 | **Risk Assessment methodology** | ✅ Có | `risk_assessment.md` |
| 4 | **Risk Treatment Plan** | ✅ Có | `risk_assessment.md` §Risk Treatment |
| 5 | **Statement of Applicability (SoA)** | ⬜ Cần tạo | Map 93 Annex A controls → applicability |
| 6 | **ISMS Objectives** | ⬜ Cần tạo | Measurable security objectives |
| 7 | **Competence records** | ⚠️ Partial | Training records qua compliance training |
| 8 | **Internal audit procedure** | ✅ Có | `internal_audit_report.md` |
| 9 | **Management review records** | ⬜ Cần tạo | Quarterly review minutes |
| 10 | **Corrective action procedure** | ⚠️ Partial | Incident response có, cần CAR procedure |
| 11 | **Change management (Clause 6.3)** | ⬜ Cần tạo | ISMS change management procedure |

### 5.4 Statement of Applicability (SoA) — 93 Annex A Controls

| Domain | Controls | Đã xong | Gap |
|--------|:--------:|:-------:|:---:|
| **A.5** Organizational (37) | 37 | 32 | 5 |
| **A.6** People (8) | 8 | 5 | 3 |
| **A.7** Physical (14) | 14 | 5 | 9 |
| **A.8** Technological (34) | 34 | 31 | 3 |
| **Tổng** | **93** | **73** | **20** |

**A.7 Physical gaps (9):** Hầu hết ngoài scope phần mềm — cần tài liệu hóa biện pháp vật lý datacenter/office hoặc ghi nhận exclusion trong SoA.

### 5.5 Hành động cụ thể

| # | Hành động | Tuần | Owner |
|---|-----------|:----:|:-----:|
| 1 | Tạo Statement of Applicability (SoA) | W1 | CISO |
| 2 | Tạo ISMS Objectives document (KPI-based) | W1 | CISO |
| 3 | Tạo Management Review procedure + minutes template | W2 | CISO |
| 4 | Tạo Change Management procedure (Clause 6.3) | W2 | CTO |
| 5 | Tạo Corrective Action Request (CAR) form + procedure | W3 | CISO |
| 6 | Conduct formal internal audit (dùng template có sẵn) | W3-4 | Internal Auditor |
| 7 | Conduct management review meeting (ghi minutes) | W4 | CEO + CISO |
| 8 | Contact CB, ký hợp đồng Stage 1 | W4 | CISO |

**Chi phí Phase 3: $10,000–20,000**

---

## 6. Phase 4 — SOC 2 Type II (Tháng 6–12/2026)

**Mục tiêu:** Upgrade SOC 2 Type I thành Type II (observation period 6 tháng).

### 6.1 Observation Period (Tháng 6–11/2026)

Type II yêu cầu auditor kiểm tra controls hoạt động **liên tục** trong 6–12 tháng. Trong thời gian này:

| Hoạt động | Tần suất | Công cụ hỗ trợ |
|-----------|:--------:|-----------------|
| **Access review** | Monthly | `GET /api/enterprise-users`, user permission report |
| **Security event review** | Weekly | `GET /api/security-events`, compliance health dashboard |
| **Vulnerability scan** | Monthly | Trivy + Dependabot + OWASP ZAP |
| **Backup verification** | Monthly | `GET /api/data-backup/status`, SHA-256 checksums |
| **Change management log** | Per change | Git history, deployment records |
| **Incident response drill** | Quarterly | SecurityIncident simulation |
| **Penetration test** | Bi-annual | External + internal pentest |
| **Training completion** | Quarterly | `GET /api/compliance/training` |
| **Risk assessment update** | Quarterly | `risk_assessment.md` update |

### 6.2 Continuous Monitoring Setup

```
┌─────────────────────────────────────────────────────────┐
│                Continuous Monitoring Stack                │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ Health Score  │  │ Security     │  │ Compliance   │  │
│  │ Dashboard    │  │ Event Stream │  │ Schedule     │  │
│  │ /monitoring  │  │ /sec-events  │  │ /schedule    │  │
│  │  /health     │  │              │  │              │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  │
│         │                 │                  │          │
│         └────────┬────────┘──────────┬───────┘          │
│                  │                   │                   │
│           ┌──────▼───────┐   ┌──────▼───────┐          │
│           │  Evidence    │   │  Alert       │          │
│           │  Collection  │   │  System      │          │
│           │  (automated) │   │  (SignalR)   │          │
│           └──────────────┘   └──────────────┘          │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### 6.3 Auditor kỳ vọng gì trong Type II?

| Lĩnh vực | Evidence cần 6 tháng liên tục |
|-----------|-------------------------------|
| **Access Reviews** | 6 monthly access review reports với evidence |
| **Monitoring** | 6 tháng security event logs, response documentation |
| **Changes** | Tất cả significant changes có approval records |
| **Backups** | 6 tháng backup success/failure logs |
| **Incidents** | Tất cả incidents có response + root cause analysis |
| **Training** | Training records cho tất cả employees |
| **Vulnerabilities** | Monthly scan reports + remediation tracking |

### 6.4 Automation để giảm effort

Tận dụng hệ thống đã xây:

```bash
# Monthly evidence collection (cron/Task Scheduler)
.\scripts\collect-evidence.ps1

# Real-time monitoring qua endpoint
GET /api/compliance/monitoring/health       # Health score
GET /api/compliance/monitoring/audit-readiness  # Audit readiness by framework

# Automated alerts qua SignalR
# → DSR overdue, task overdue, incident open, training gap, bias test failed
```

**Chi phí Phase 4: $15,000–25,000** (auditor fee for Type II)

---

## 7. Phase 5 — HITRUST CSF i1 (Tháng 9/2026–2/2027)

**Mục tiêu:** Đạt HITRUST i1 certification (implemented tier).

### 7.1 HITRUST Certification Tiers

```
┌─────────────────────────────────────────────────┐
│              HITRUST Certification Tiers          │
├──────────┬──────────┬──────────┬────────────────┤
│   e1     │    i1    │    r2    │                │
│ Essential│ Implement│ Risk-    │  ← Mục tiêu   │
│          │ ed ✓     │ based    │    Phase 5: i1 │
│ 44 req.  │ 182 req. │ 350 req. │                │
│ 1 year   │ 2 year   │ 2 year   │                │
├──────────┼──────────┼──────────┼────────────────┤
│ $15-25K  │ $30-50K  │ $80-150K │  Chi phí       │
└──────────┴──────────┴──────────┴────────────────┘
```

### 7.2 HITRUST 19 Domains — Đánh giá

| # | Domain | Score hiện tại | Mục tiêu | Hành động |
|---|--------|:--------------:|:---------:|-----------|
| 1 | Information Protection Program | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Formalize program charter |
| 2 | Endpoint Protection | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Device compliance policies |
| 3 | Portable Media Security | ⭐⭐⭐ | ⭐⭐⭐⭐ | USB/removable media policy |
| 4 | Mobile Device Security | ⭐⭐⭐ | ⭐⭐⭐⭐ | MDM policies |
| 5 | Wireless Security | ⭐⭐⭐ | ⭐⭐⭐⭐ | Wireless access policy |
| 6 | Configuration Management | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Docker config auditing |
| 7 | Vulnerability Management | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | SAST/DAST pipeline (Phase 1) |
| 8 | Network Protection | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Docker network segmentation ✅ |
| 9 | Transmission Protection | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | TLS 1.3 + mTLS ✅ |
| 10 | Password Management | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Password policy ✅ |
| 11 | Access Control | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | RBAC + ZeroTrust ✅ |
| 12 | Audit Logging & Monitoring | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 50+ event types ✅ |
| 13 | Education & Awareness | ⭐⭐⭐ | ⭐⭐⭐⭐ | Training program (Phase 1) |
| 14 | Third Party Assurance | ⭐⭐⭐ | ⭐⭐⭐⭐ | Vendor assessments |
| 15 | Incident Management | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Automated response ✅ |
| 16 | Business Continuity | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | BCP/DRP testing |
| 17 | Risk Management | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Formal risk assessment ✅ |
| 18 | Physical & Environmental | ⭐⭐ | ⭐⭐⭐ | Datacenter documentation |
| 19 | Data Protection & Privacy | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Encryption + consent ✅ |

### 7.3 HITRUST Engagement Process

| Bước | Timeline | Chi tiết |
|------|:--------:|---------|
| 1. Chọn External Assessor (EAO) | Tháng 9 | HITRUST-approved assessor list |
| 2. Readiness Assessment | Tháng 9-10 | EAO đánh giá gaps, tạo remediation plan |
| 3. Remediation | Tháng 10-11 | Fix gaps identified in readiness |
| 4. Validated Assessment | Tháng 12 | EAO thực hiện formal assessment |
| 5. HITRUST QA Review | Tháng 1/2027 | HITRUST internal review |
| 6. Certification Issued | Tháng 2/2027 | i1 certificate (2 năm validity) |

**Chi phí Phase 5: $30,000–50,000**

---

## 8. Phase 6 — NIST AI RMF Level 4 + ISO 42001 (Tháng 1–6/2027)

**Mục tiêu:** Đạt NIST AI RMF maturity Level 4 + ISO 42001:2023 certification.

### 8.1 NIST AI RMF — Từ Level 3 (Defined) lên Level 4 (Managed)

| Function | Score hiện tại | Mục tiêu | Gap Actions |
|----------|:--------------:|:---------:|-------------|
| **GOVERN** | 3.2 | 4.0 | Board-level AI oversight, AI risk register into ERM |
| **MAP** | 3.0 | 4.0 | Formalize AI impact assessment per system, stakeholder analysis |
| **MEASURE** | 3.0 | 4.0 | Automate bias monitoring cadence, explainability benchmarks |
| **MANAGE** | 2.8 | 4.0 | Post-deployment monitoring SOP, AI incident response plan, model retirement |

### 8.2 Nâng cấp kỹ thuật NIST AI RMF

| # | Nâng cấp | Chi tiết | Status |
|---|----------|----------|:------:|
| 1 | **Automated bias monitoring** | Schedule bias tests daily/weekly per model | ⬜ |
| 2 | **Explainability dashboard** | SHAP/LIME values per prediction | ⬜ |
| 3 | **Model performance alerts** | Alert when accuracy/precision drops below threshold | ⬜ |
| 4 | **AI incident response plan** | Separate SOP cho AI-specific incidents (hallucination, bias, drift) | ⬜ |
| 5 | **Model cards** | Standardized model documentation per Google/Hugging Face format | ⬜ |
| 6 | **Human oversight workflow** | Approval gates for high-risk AI decisions | ⬜ |
| 7 | **AI risk register** | Dedicated risk register integrated with enterprise risk | ⬜ |

### 8.3 ISO 42001:2023 — AI Management System (AIMS)

| Clause | Yêu cầu | Hiện trạng | Gap |
|:------:|----------|:----------:|-----|
| 4 | Context of the organization | ✅ | — |
| 5 | Leadership | ⚠️ | AI governance board charter |
| 6 | Planning | ⚠️ | AI risk assessment methodology |
| 7 | Support | ✅ | Competence records via training |
| 8 | Operation | ⚠️ | AI development lifecycle formalization |
| 9 | Performance evaluation | ⚠️ | AI KPIs, internal AI audit |
| 10 | Improvement | ⬜ | Corrective actions for AI |
| A.2 | AI policy | ✅ | `ai_governance_charter.md` |
| A.3 | AI system lifecycle | ⚠️ | `ai_lifecycle_documentation.md` — need formalization |
| A.4 | Data management | ✅ | Data governance via GDPR compliance |
| A.5 | Technology | ✅ | Model versioning, bias testing ✅ |
| A.6 | AI human oversight | ⬜ | Human-in-the-loop procedures |
| A.7 | Stakeholders | ⬜ | Stakeholder mapping for AI systems |
| A.8 | Operation | ✅ | Deployment logs, rollback ✅ |

### 8.4 Lợi thế: Audit kết hợp ISO 27001 + ISO 42001

Nếu cùng CB audit ISO 27001, có thể **kết hợp surveillance audit ISO 27001 (Year 1) + ISO 42001 initial certification** → tiết kiệm ~30% chi phí.

**Chi phí Phase 6: $10,000–20,000** (add-on nếu cùng CB)

---

## 9. Chi tiết kỹ thuật cần nâng cấp

### 9.1 CI/CD Security Pipeline

```yaml
# .github/workflows/security.yml (cần tạo)
name: Security Pipeline
on: [push, pull_request]

jobs:
  sast:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: .NET Security Scan
        run: |
          dotnet tool install --global security-scan
          security-scan src/IVF.API/IVF.API.csproj
      - name: npm audit  
        run: |
          cd ivf-client
          npm audit --production --audit-level=moderate

  dast:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - name: OWASP ZAP Scan
        uses: zaproxy/action-full-scan@v0.7.0
        with:
          target: 'http://localhost:5000'

  container-scan:
    runs-on: ubuntu-latest
    steps:
      - name: Trivy Scan
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: 'ivf-api:latest'
          severity: 'CRITICAL,HIGH'

  dependency-check:
    runs-on: ubuntu-latest
    steps:
      - name: OWASP Dependency Check
        uses: dependency-check/Dependency-Check_Action@main
```

### 9.2 Evidence Collection Automation

```powershell
# Mở rộng scripts/collect-evidence.ps1 để chạy monthly automated

# Task Scheduler (Windows) hoặc cron (Linux)
# Monthly: First Monday at 2 AM
# Output: docs/compliance/evidence/YYYY-MM/

# Thêm vào collect-evidence.ps1:
# - Access review export (user permissions snapshot)
# - Security event summary (monthly digest)
# - Backup verification report
# - Training completion report
# - Vulnerability scan results
# - Change management log
```

### 9.3 GDPR Art. 18 — Processing Restriction

```csharp
// Thêm vào Patient entity:
public bool IsProcessingRestricted { get; private set; }
public DateTime? ProcessingRestrictedAt { get; private set; }
public string? ProcessingRestrictionReason { get; private set; }

public void RestrictProcessing(string reason)
{
    IsProcessingRestricted = true;
    ProcessingRestrictedAt = DateTime.UtcNow;
    ProcessingRestrictionReason = reason;
}
```

### 9.4 AI Model Cards (NIST AI RMF)

```
Thêm endpoint: GET /api/ai/model-versions/{id}/model-card
Trả về:
- Model name & version
- Intended use & limitations  
- Training data summary (no PII)
- Performance metrics (accuracy, precision, recall)
- Bias test results
- Ethical considerations
- Deployment date & environment
```

---

## 10. Ngân sách & Chi phí

### 10.1 Chi phí theo Phase

| Phase | Chứng chỉ | Internal Cost | External Cost | Tổng |
|:-----:|-----------|:-------------:|:-------------:|:----:|
| 1 | HIPAA + GDPR | ~40 giờ | $0–500 | **$500** |
| 2 | SOC 2 Type I | ~80 giờ | $8K–15K | **$12K** |
| 3 | ISO 27001 | ~60 giờ | $10K–20K | **$15K** |
| 4 | SOC 2 Type II | ~40 giờ | $15K–25K | **$20K** |
| 5 | HITRUST i1 | ~80 giờ | $30K–50K | **$40K** |
| 6 | NIST AI + ISO 42001 | ~60 giờ | $10K–20K | **$15K** |
| — | **Compliance platform** | — | $2.5K–10K/năm | **$5K** |
| | | | | |
| | **TỔNG YEAR 1** | **~360 giờ** | **$75K–130K** | **~$107K** |

### 10.2 Chi phí tiết kiệm cho startup

| Phương án | Chi phí | Bao gồm |
|-----------|:-------:|---------|
| **Tối thiểu** | **$13K** | HIPAA (free) + GDPR (free) + SOC 2 Type I ($8K) + Vanta startup ($2.5K) + pentest tools ($2.5K) |
| **Khuyến nghị** | **$35K** | Tối thiểu + ISO 27001 ($12K) + SOC 2 Type II upgrade ($10K) |
| **Đầy đủ** | **$107K** | All 7 frameworks + all certifications |

### 10.3 Ongoing Costs (hàng năm)

| Hạng mục | Chi phí/năm |
|----------|:-----------:|
| Compliance platform (Vanta/Drata) | $2.5K–10K |
| SOC 2 Type II renewal | $15K–20K |
| ISO 27001 surveillance audit | $5K–8K |
| HITRUST renewal (biennial) | $15K–25K |
| Penetration testing (bi-annual) | $5K–15K |
| Vulnerability scanning tools | $0–3K |
| Security training platform | $0–2K |
| **Tổng ongoing** | **$42K–83K** |

---

## 11. Ma trận trách nhiệm (RACI)

| Hoạt động | CEO | CTO/CISO | Dev Team | Compliance | External |
|-----------|:---:|:--------:|:--------:|:----------:|:--------:|
| Management commitment | **A** | R | I | C | — |
| Risk assessment | I | **A** | C | R | C |
| Security policy | A | **R** | I | C | — |
| Technical controls | I | A | **R** | C | — |
| Evidence collection | — | C | **R** | A | — |
| Internal audit | I | C | C | **R** | — |
| Management review | **R** | A | I | C | — |
| External audit | I | A | C | **R** | **R** |
| Training program | I | C | R | **A** | — |
| Incident response | I | **A** | R | C | C |
| Vendor assessment | — | A | C | **R** | — |
| DPO duties | I | C | — | **R** | — |
| AI governance | I | **A** | R | C | — |

> R = Responsible, A = Accountable, C = Consulted, I = Informed

---

## 12. KPI & Metrics theo dõi

### 12.1 KPIs phải đạt trước audit

| KPI | Mục tiêu | Đo bằng |
|-----|:--------:|---------|
| Compliance Health Score | ≥ 90 | `/api/compliance/monitoring/health` |
| DSR response time | ≤ 30 ngày (GDPR) / ≤ 45 ngày (HIPAA) | DSR average response time |
| Security training completion | ≥ 95% | Training compliance rate |
| Critical vulnerability remediation | ≤ 7 ngày | Vuln scan → fix time |
| High vulnerability remediation | ≤ 30 ngày | Vuln scan → fix time |
| Incident response time | ≤ 1 giờ (P1) / ≤ 4 giờ (P2) | SecurityIncident records |
| Backup success rate | ≥ 99.9% | Backup verification logs |
| MFA enrollment | 100% | User MFA status |
| Access review completion | 100% monthly | Monthly access review records |
| AI bias test pass rate | ≥ 80% | Bias test results |

### 12.2 Dashboard Monitoring

```
URL: http://localhost:4200/compliance

┌─────────────────────────────────────────────────────┐
│         Compliance Health Dashboard                  │
├──────────┬──────────┬──────────┬──────────┬─────────┤
│ Health   │ DSR      │ Training │ Incidents│ Backup  │
│ Score    │ Status   │ Rate     │ Open     │ Status  │
│   92     │  0 overdue│  85%    │    0     │  ✅     │
├──────────┴──────────┴──────────┴──────────┴─────────┤
│                                                      │
│    SOC 2: 90%  │  ISO: 90%  │  HIPAA: 94%          │
│    GDPR: 92%   │  HITRUST: 84% │                    │
│                                                      │
└─────────────────────────────────────────────────────┘
```

---

## 13. Bảng tham chiếu tài liệu

### 13.1 Cross-reference: Tài liệu ↔ Framework

| Tài liệu | SOC 2 | ISO 27001 | HIPAA | GDPR | HITRUST | NIST AI | ISO 42001 |
|-----------|:-----:|:---------:|:-----:|:----:|:-------:|:-------:|:---------:|
| `information_security_policy.md` | ✅ | ✅ | ✅ | ✅ | ✅ | — | — |
| `risk_assessment.md` | ✅ | ✅ | ✅ | — | ✅ | ✅ | ✅ |
| `bcp_drp.md` | ✅ | ✅ | ✅ | — | ✅ | — | — |
| `breach_notification_sop.md` | ✅ | — | ✅ | ✅ | ✅ | — | — |
| `dpia.md` | — | — | — | ✅ | ✅ | — | ✅ |
| `ropa_register.md` | — | — | — | ✅ | — | — | — |
| `privacy_notice.md` | — | — | — | ✅ | — | — | — |
| `dpo_charter.md` | — | — | — | ✅ | — | — | — |
| `pseudonymization_procedures.md` | — | ✅ | — | ✅ | — | — | — |
| `standard_contractual_clauses.md` | — | — | — | ✅ | — | — | — |
| `vendor_risk_assessment.md` | ✅ | ✅ | ✅ | — | ✅ | — | — |
| `penetration_test_report_template.md` | ✅ | ✅ | — | — | ✅ | — | — |
| `internal_audit_report.md` | ✅ | ✅ | — | — | ✅ | — | — |
| `ai_governance_charter.md` | — | — | — | — | — | ✅ | ✅ |
| `ai_lifecycle_documentation.md` | — | — | — | — | — | ✅ | ✅ |
| `compliance_master_guide.md` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### 13.2 Tài liệu cần tạo mới

| # | Tài liệu | Framework | Deadline | Template |
|---|----------|-----------|----------|----------|
| 1 | **Statement of Applicability (SoA)** | ISO 27001 | Phase 3 W1 | Excel/spreadsheet |
| 2 | **ISMS Objectives** | ISO 27001 | Phase 3 W1 | Markdown |
| 3 | **Management Review Minutes** | ISO 27001, SOC 2 | Phase 3 W2 | Template |
| 4 | **Change Management Procedure** | ISO 27001 | Phase 3 W2 | Markdown |
| 5 | **Corrective Action Procedure** | ISO 27001 | Phase 3 W3 | Template + form |
| 6 | **AI Incident Response Plan** | NIST AI, ISO 42001 | Phase 6 W2 | Markdown |
| 7 | **Model Cards** (per AI system) | NIST AI, ISO 42001 | Phase 6 W3 | JSON/Markdown |
| 8 | **AI Risk Register** | NIST AI, ISO 42001 | Phase 6 W1 | Spreadsheet |

---

## 14. Checklist tổng quan

### Phase 1 — Nền tảng (Tháng 3–4/2026)
- [ ] HIPAA Security Risk Assessment (HHS SRA Tool)
- [ ] BAA với vendors
- [ ] Physical safeguards documentation
- [ ] DPO chỉ định chính thức
- [ ] GDPR Art. 20 Data Portability endpoint
- [ ] GDPR Art. 18 Processing Restriction
- [ ] SAST tích hợp CI/CD (`security-scan`, `npm audit`)
- [ ] DAST setup (OWASP ZAP scheduled)
- [ ] Security awareness training (4 modules)
- [ ] Populate evidence folders (access_control, incident_response, training)
- [ ] Vulnerability scanning schedule (Trivy + Dependabot)
- [ ] Management commitment letter (CEO/CTO)
- [ ] RACI Matrix

### Phase 2 — SOC 2 Type I (Tháng 4–6/2026)
- [ ] Chọn Trust Service Criteria
- [ ] Map controls → SOC 2 criteria
- [ ] Formal risk assessment
- [ ] Top 5 vendor risk assessments
- [ ] Internal audit
- [ ] Penetration test
- [ ] Chọn auditor & ký engagement
- [ ] Evidence package upload
- [ ] Auditor review & remediation
- [ ] **SOC 2 Type I report issued ✓**

### Phase 3 — ISO 27001:2022 (Tháng 5–8/2026)
- [ ] Statement of Applicability (SoA)
- [ ] ISMS Objectives
- [ ] Management Review procedure + minutes
- [ ] Change Management procedure (Clause 6.3)
- [ ] Corrective Action procedure
- [ ] Internal audit (formal)
- [ ] Management review meeting
- [ ] Chọn CB & ký hợp đồng
- [ ] Stage 1 audit (document review)
- [ ] Remediation (fix Stage 1 findings)
- [ ] Stage 2 audit (implementation review)
- [ ] **ISO 27001:2022 certificate issued ✓**

### Phase 4 — SOC 2 Type II (Tháng 6–12/2026)
- [ ] Start observation period
- [ ] Monthly access reviews (×6)
- [ ] Monthly vulnerability scans (×6)
- [ ] Monthly backup verifications (×6)
- [ ] Quarterly risk assessment updates (×2)
- [ ] Quarterly incident response drills (×2)
- [ ] Quarterly training reviews (×2)
- [ ] Bi-annual penetration test
- [ ] Evidence package (6 months)
- [ ] **SOC 2 Type II report issued ✓**

### Phase 5 — HITRUST i1 (Tháng 9/2026–2/2027)
- [ ] Chọn External Assessor (EAO)
- [ ] Readiness assessment
- [ ] Remediation per readiness findings
- [ ] Validated assessment
- [ ] HITRUST QA review
- [ ] **HITRUST i1 certificate issued ✓**

### Phase 6 — NIST AI RMF + ISO 42001 (Tháng 1–6/2027)
- [ ] AI incident response plan
- [ ] Automated bias monitoring (scheduled)
- [ ] Model cards per AI system
- [ ] AI risk register
- [ ] Human oversight workflow
- [ ] AI governance board meeting
- [ ] ISO 42001 Stage 1 audit
- [ ] ISO 42001 Stage 2 audit
- [ ] **NIST AI RMF Level 4 achieved ✓**
- [ ] **ISO 42001:2023 certificate issued ✓**

---

## Tổng kết Timeline

```
2026                                                              2027
Mar    Apr    May    Jun    Jul    Aug    Sep    Oct    Nov    Dec    Jan    Feb    Mar    Apr    May    Jun
 │      │      │      │      │      │      │      │      │      │      │      │      │      │      │      │
 ├──P1──┤      │      │      │      │      │      │      │      │      │      │      │      │      │      │
 │HIPAA │      │      │      │      │      │      │      │      │      │      │      │      │      │      │
 │GDPR  │      │      │      │      │      │      │      │      │      │      │      │      │      │      │
 │      ├──P2──┤      │      │      │      │      │      │      │      │      │      │      │      │      │
 │      │SOC2 I│      │      │      │      │      │      │      │      │      │      │      │      │      │
 │      │      ├──P3──┼──────┤      │      │      │      │      │      │      │      │      │      │      │
 │      │      │ ISO 27001   │      │      │      │      │      │      │      │      │      │      │      │
 │      │      │      ├──────P4─────┼──────┼──────┼──────┤      │      │      │      │      │      │      │
 │      │      │      │   SOC 2 Type II (6mo observation)│      │      │      │      │      │      │      │
 │      │      │      │      │      │      ├──────P5─────┼──────┼──────┼──────┤      │      │      │      │
 │      │      │      │      │      │      │     HITRUST i1     │      │      │      │      │      │      │
 │      │      │      │      │      │      │      │      │      │      ├──────P6─────┼──────┼──────┤      │
 │      │      │      │      │      │      │      │      │      │      │  NIST AI + ISO 42001      │      │
```

**Kết quả kỳ vọng: Đến 06/2027 đạt đủ 7 chứng chỉ/compliance frameworks quốc tế.**

---

> **Lưu ý:** Tài liệu này là living document — cập nhật monthly theo tiến độ thực tế. Tất cả chi phí là ước tính và có thể thay đổi tùy auditor/CB được chọn.
