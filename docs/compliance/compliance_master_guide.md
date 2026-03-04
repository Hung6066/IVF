# IVF Compliance Master Guide

> **Document ID:** IVF-CMP-MASTER-001  
> **Version:** 1.0  
> **Effective Date:** 2026-03-04  
> **Classification:** CONFIDENTIAL  
> **Owner:** Compliance Officer / DPO  
> **Review Cycle:** Quarterly

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Compliance Program Overview](#2-compliance-program-overview)
3. [Regulatory Landscape](#3-regulatory-landscape)
4. [Architecture Overview](#4-architecture-overview)
5. [Framework Coverage Matrix](#5-framework-coverage-matrix)
6. [Implementation Phases](#6-implementation-phases)
7. [Governance Structure](#7-governance-structure)
8. [Document Inventory](#8-document-inventory)
9. [Quick Reference: Endpoints & UI](#9-quick-reference-endpoints--ui)
10. [Glossary](#10-glossary)

---

## 1. Executive Summary

IVF Information System triển khai chương trình tuân thủ toàn diện đáp ứng **7 tiêu chuẩn quốc tế** cho lĩnh vực y tế sinh sản. Hệ thống xử lý dữ liệu đặc biệt nhạy cảm (PHI — Protected Health Information, dữ liệu sinh trắc học, dữ liệu sinh sản) đòi hỏi mức bảo vệ cao nhất.

### Compliance Scores (Current)

| Framework            |  Score  |     Status     | Certification           |
| :------------------- | :-----: | :------------: | :---------------------- |
| SOC 2 Type II        |   90%   | ✅ Audit Ready | Sẵn sàng engagement     |
| ISO 27001:2022       |   90%   | ✅ Audit Ready | Stage 1/2 ready         |
| HIPAA                |   94%   |  ✅ Compliant  | Self-assessed           |
| GDPR                 |   92%   |  ✅ Compliant  | DPA registered          |
| HITRUST CSF v11      |   84%   |  ⚠️ Maturing   | e1 achieved, nearing i1 |
| NIST AI RMF 1.0      |   84%   |  ⚠️ On Track   | Level 3 → 4             |
| ISO 42001            |   80%   | ⚠️ Developing  | QMS building            |
| **Weighted Average** | **88%** |       —        | —                       |

### Key Achievements

- **79 compliance API endpoints** tích hợp vào hệ thống
- **6 giao diện UI** quản lý tuân thủ (Dashboard, DSR, Schedule, Assets, AI, Training)
- **25 tài liệu tuân thủ** chi tiết
- **Continuous monitoring** với health scoring tự động
- **Zero Trust architecture** trên mọi request
- **Automated incident response** với rule-based triggers
- **AI Governance** theo NIST AI RMF & ISO 42001

---

## 2. Compliance Program Overview

### 2.1 Mission Statement

Đảm bảo IVF Information System tuân thủ đầy đủ các tiêu chuẩn bảo mật thông tin, quyền riêng tư dữ liệu, và quản trị AI — bảo vệ dữ liệu bệnh nhân ở mức cao nhất trong khi duy trì hiệu quả vận hành lâm sàng.

### 2.2 Scope

| Aspect         | Coverage                                                                                                         |
| -------------- | ---------------------------------------------------------------------------------------------------------------- |
| **Data Types** | PHI (IVF medical records), PII, biometric data, reproductive data, financial data                                |
| **Systems**    | Backend API (.NET 10), Frontend (Angular 21), PostgreSQL, Redis, MinIO, SignServer, EJBCA, DigitalPersona        |
| **Users**      | Admin, Doctor, Nurse, LabTech, Embryologist, Receptionist, Cashier, Pharmacist                                   |
| **Geography**  | Vietnam (primary), GDPR scope for EU data subjects                                                               |
| **AI Systems** | 5 production models (threat detection, behavioral analytics, bot detection, contextual auth, biometric matching) |

### 2.3 Compliance Lifecycle

```
┌─────────────┐    ┌──────────────┐    ┌───────────────┐    ┌──────────────┐
│   ASSESS    │───→│   IMPLEMENT  │───→│   MONITOR     │───→│   IMPROVE    │
│             │    │              │    │               │    │              │
│ • Gap audit │    │ • Controls   │    │ • Health score│    │ • Remediate  │
│ • Risk eval │    │ • Automation │    │ • Alert track │    │ • Optimize   │
│ • Baseline  │    │ • Training   │    │ • Audit ready │    │ • Re-assess  │
└─────────────┘    └──────────────┘    └───────────────┘    └──────────────┘
      ↑                                                            │
      └────────────────────────────────────────────────────────────┘
                          Continuous Improvement Cycle
```

---

## 3. Regulatory Landscape

### 3.1 Applicable Regulations

#### HIPAA (Health Insurance Portability and Accountability Act)

- **Applicability:** Bắt buộc cho hệ thống xử lý PHI
- **Key Requirements:** Administrative Safeguards (§164.308), Physical Safeguards (§164.310), Technical Safeguards (§164.312), Breach Notification (§164.400-414)
- **IVF Specific:** Dữ liệu chu kỳ IVF, kết quả phôi, lịch sử điều trị, dữ liệu sinh trắc học
- **Reference:** [hipaa_self_assessment.md](hipaa_self_assessment.md)

#### GDPR (General Data Protection Regulation)

- **Applicability:** Dữ liệu bệnh nhân EU/EEA, đặc biệt là dữ liệu sức khỏe (Art. 9)
- **Key Requirements:** Lawful basis (Art. 6), Special categories (Art. 9), Data subject rights (Art. 15-22), DPIA (Art. 35), DPO (Art. 37-39), ROPA (Art. 30), Breach notification (Art. 33-34)
- **IVF Specific:** Consent for reproductive data processing, right to erasure vs. medical record retention
- **References:** [gdpr_readiness_assessment.md](gdpr_readiness_assessment.md), [dpia.md](dpia.md), [dpo_charter.md](dpo_charter.md), [ropa_register.md](ropa_register.md)

#### SOC 2 Type II

- **Applicability:** Trust Services Criteria cho SaaS healthcare
- **Key Requirements:** CC1-CC9 (Control Environment, Communication, Risk Assessment, Monitoring, IT Operations, Logical Access, Physical Access, System Acquisition, Cryptography) + Availability
- **IVF Specific:** Multi-tenant access controls, encryption in transit/at rest, availability SLAs
- **Reference:** [soc2_readiness.md](soc2_readiness.md)

#### ISO 27001:2022

- **Applicability:** International ISMS standard
- **Key Requirements:** Clauses 4-10 (mandatory ISMS), 93 Annex A controls
- **IVF Specific:** Organizational context of fertility clinic, interested parties (patients, regulators, insurers)
- **Reference:** [iso27001_certification_prep.md](iso27001_certification_prep.md)

#### HITRUST CSF v11

- **Applicability:** Healthcare-specific security framework
- **Key Requirements:** 14 control categories, 5 maturity levels, 3 assessment tiers (e1, i1, r2)
- **IVF Specific:** HITRUST healthcare mappings, banded assessment scoring
- **Reference:** [hitrust_self_assessment.md](hitrust_self_assessment.md)

#### NIST AI RMF 1.0

- **Applicability:** AI systems governance
- **Key Requirements:** GOVERN, MAP, MEASURE, MANAGE functions; 19 sub-categories
- **IVF Specific:** Biometric matching AI, threat detection ML, behavioral analytics
- **Reference:** [nist_ai_rmf_maturity.md](nist_ai_rmf_maturity.md)

#### ISO 42001

- **Applicability:** AI Management System standard
- **Key Requirements:** AI quality management, risk-based approach, lifecycle management
- **IVF Specific:** 5 production AI models requiring governance
- **Reference:** [ai_governance_charter.md](ai_governance_charter.md)

### 3.2 Regulatory Conflicts & Resolutions

| Conflict                                         | Resolution                                                                                                                                     |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| GDPR Right to Erasure vs. HIPAA Record Retention | Medical records retained per HIPAA 6-year rule; non-medical PII erasable. Use pseudonymization for analytics.                                  |
| GDPR Consent vs. HIPAA Treatment Exception       | Healthcare Treatment (HIPAA TPO) as primary basis; GDPR Art. 9(2)(h) health provision exception. Explicit consent for secondary purposes only. |
| Data Localization vs. Cloud Availability         | All primary data on-premise (Vietnam). SCCs in place for any EU cross-border transfers.                                                        |
| AI Transparency vs. Security Effectiveness       | Threat detection models: limited transparency justified by security necessity. Biometric models: full transparency per NIST AI RMF.            |

---

## 4. Architecture Overview

### 4.1 System Architecture (Compliance View)

```
                    ┌──────────────────────────────────────────┐
                    │            PRESENTATION LAYER            │
                    │                                          │
                    │  Angular 21 Frontend                     │
                    │  ├─ Compliance Dashboard (Health Score)  │
                    │  ├─ DSR Management (GDPR Art. 15-22)    │
                    │  ├─ Compliance Schedule (Recurring Tasks)│
                    │  ├─ Asset Inventory (Data Classification)│
                    │  ├─ AI Governance (Bias/Fairness)        │
                    │  └─ Training Management (Awareness)      │
                    └──────────────────┬───────────────────────┘
                                       │ HTTPS/TLS 1.3
                    ┌──────────────────┴───────────────────────┐
                    │          SECURITY GATEWAY LAYER          │
                    │                                          │
                    │  Authentication Pipeline:                │
                    │  ① VaultToken → ② ApiKey → ③ JWT Bearer │
                    │                                          │
                    │  Zero Trust Pipeline:                    │
                    │  ④ ThreatDetection (7 signals, 0-100)   │
                    │  ⑤ BehavioralAnalytics (z-score)        │
                    │  ⑥ ConditionalAccess (location/time/MFA)│
                    │  ⑦ BotDetection (UA + reCAPTCHA)        │
                    │                                          │
                    │  Rate Limiting: 100 req/min global       │
                    │  Digital Signing: 30 ops/min             │
                    └──────────────────┬───────────────────────┘
                                       │
                    ┌──────────────────┴───────────────────────┐
                    │          APPLICATION LAYER (CQRS)        │
                    │                                          │
                    │  Commands (Writes):                      │
                    │  • CreateDsr, CompleteDsr, AssignTraining│
                    │  • CreateBreachNotification, SignDocument│
                    │  • CreateBiasTest, DeployModelVersion    │
                    │                                          │
                    │  Queries (Reads):                        │
                    │  • GetComplianceHealth, GetSecurityTrends│
                    │  • GetAuditReadiness, GetAiPerformance   │
                    │                                          │
                    │  Services:                               │
                    │  • ComplianceScoringEngine               │
                    │  • IncidentResponseService               │
                    │  • DataRetentionService                  │
                    │  • AiBiasTestService                     │
                    └──────────────────┬───────────────────────┘
                                       │
                    ┌──────────────────┴───────────────────────┐
                    │          INFRASTRUCTURE LAYER            │
                    │                                          │
                    │  ┌─────────┐ ┌───────┐ ┌──────┐         │
                    │  │PostgreSQL│ │ Redis │ │ MinIO│         │
                    │  │  :5433   │ │ :6379 │ │ :9000│         │
                    │  │ 8 compliance│ Cache │ │3 buckets│     │
                    │  │ entities │ │Graceful│ │Documents│     │
                    │  │Partitioned│ │Degrade│ │Signed  │     │
                    │  │Audit Logs│ │       │ │Images  │     │
                    │  └─────────┘ └───────┘ └──────┘         │
                    │                                          │
                    │  ┌──────────┐ ┌──────────┐ ┌──────────┐ │
                    │  │  EJBCA   │ │SignServer│ │DigitalP. │ │
                    │  │  :8443   │ │  :9443   │ │Biometric │ │
                    │  │PKI/certs │ │Doc signing│ │Fingerpr.│ │
                    │  │  mTLS    │ │Rate limit│ │Win only  │ │
                    │  └──────────┘ └──────────┘ └──────────┘ │
                    └──────────────────────────────────────────┘
```

### 4.2 Data Classification

|      Level       | Definition                               | Examples                                                 | Controls                                                                  |
| :--------------: | ---------------------------------------- | -------------------------------------------------------- | ------------------------------------------------------------------------- |
|  **RESTRICTED**  | Dữ liệu nhạy cảm nhất, cần bảo vệ tối đa | PHI gốc, biometric templates, PKI private keys, API keys | AES-256-GCM at rest, TLS 1.3 in transit, MFA required, audit every access |
| **CONFIDENTIAL** | Dữ liệu nội bộ nhạy cảm                  | Patient PII, financial records, treatment plans          | Encryption at rest, role-based access, audit trail                        |
|   **INTERNAL**   | Dữ liệu nội bộ thường                    | System configs, internal reports, staff schedules        | Access control, basic audit                                               |
|    **PUBLIC**    | Dữ liệu công khai                        | Privacy notice, public API docs                          | No special controls                                                       |

### 4.3 Compliance Entities (Domain Model)

```
┌─────────────────────────────────────────────────────────────────┐
│                     COMPLIANCE DOMAIN MODEL                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Data Protection:                   Security:                    │
│  ├─ DataSubjectRequest             ├─ SecurityIncident           │
│  ├─ ProcessingActivity             ├─ SecurityEvent              │
│  ├─ DataRetentionPolicy            ├─ ConditionalAccessPolicy    │
│  ├─ BreachNotification             ├─ UserBehaviorProfile        │
│  └─ UserConsent                    └─ IncidentResponseRule       │
│                                                                  │
│  AI Governance:                    Operations:                   │
│  ├─ AiModelVersion                 ├─ ComplianceSchedule         │
│  └─ AiBiasTestResult              ├─ ComplianceTraining          │
│                                    ├─ AssetInventory             │
│  Access Control:                   └─ AuditPartition             │
│  ├─ ImpersonationRequest                                        │
│  ├─ PermissionDelegation           Document Signing:             │
│  ├─ UserSession                    ├─ UserSignature              │
│  ├─ UserGroup(Member/Permission)   └─ DocumentSignature          │
│  └─ UserLoginHistory                                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Framework Coverage Matrix

### 5.1 Cross-Framework Control Mapping

| Domain               | HIPAA §               | ISO 27001 Annex A | SOC 2 CC | GDPR Art.        | HITRUST      | IVF Control               |
| -------------------- | --------------------- | ----------------- | -------- | ---------------- | ------------ | ------------------------- |
| Access Control       | §164.312(a)           | A.5.15-18         | CC6.1-3  | Art. 5(1)(f), 32 | 01.a-y       | JWT + RBAC + Zero Trust   |
| Encryption (transit) | §164.312(e)           | A.8.24            | CC6.7    | Art. 32(1)(a)    | 09.m         | TLS 1.3 mandatory         |
| Encryption (rest)    | §164.312(a)(2)(iv)    | A.8.24            | CC6.1    | Art. 32(1)(a)    | 06.d         | AES-256-GCM               |
| Audit Logging        | §164.312(b)           | A.8.15            | CC7.2    | Art. 5(2)        | 09.aa        | Partitioned PostgreSQL    |
| Breach Notification  | §164.400-414          | A.5.24-28         | CC7.3-4  | Art. 33-34       | 11.a-e       | BreachNotification entity |
| Data Subject Rights  | —                     | —                 | —        | Art. 15-22       | —            | DataSubjectRequest CRUD   |
| Risk Assessment      | §164.308(a)(1)(ii)(A) | A.5.1, A.8.8      | CC3.1-4  | Art. 35          | 03.a-d       | risk_assessment.md        |
| Training             | §164.308(a)(5)        | A.6.3             | CC1.4    | Art. 39(1)(b)    | 02.e         | ComplianceTraining entity |
| BCP/DRP              | §164.308(a)(7)        | A.5.29-30         | CC9.1    | —                | 12.a-e       | 3-2-1 backup strategy     |
| Vendor Management    | §164.308(b)(1)        | A.5.19-23         | CC9.2    | Art. 28          | 05.i-k       | vendor_risk_assessment.md |
| AI Governance        | —                     | —                 | —        | —                | — (emerging) | NIST AI RMF + ISO 42001   |
| Asset Management     | §164.310(d)           | A.5.9-13          | CC6.4    | Art. 30          | 07.a-e       | AssetInventory entity     |
| Incident Response    | §164.308(a)(6)        | A.5.24-28         | CC7.3    | Art. 33          | 11.a         | IncidentResponseService   |
| Physical Security    | §164.310(a-c)         | A.7.1-14          | CC6.4-5  | Art. 32(1)(b)    | 08.a-l       | Docker isolation + infra  |

### 5.2 Coverage Heatmap

```
                  SOC2  ISO   HIPAA  GDPR  HITRUST  NIST-AI  ISO-42001
Access Control    ██▓▓  ████  ████   ████  ████     ░░░░     ░░░░
Encryption        ████  ████  ████   ████  ████     ░░░░     ░░░░
Audit Trail       ████  ████  ████   ████  ████     ▓▓░░     ░░░░
Breach Mgmt       ████  ████  ████   ████  ████     ░░░░     ░░░░
Data Rights       ░░░░  ░░░░  ▓▓░░   ████  ░░░░     ░░░░     ░░░░
Risk Assessment   ████  ████  ████   ████  ████     ████     ████
Training          ████  ████  ████   ████  ████     ▓▓░░     ▓▓░░
BCP/DRP           ████  ████  ████   ▓▓░░  ████     ░░░░     ░░░░
Vendor Mgmt       ████  ████  ████   ████  ████     ░░░░     ░░░░
AI Governance     ░░░░  ░░░░  ░░░░   ▓▓░░  ░░░░     ████     ████
Bias Testing      ░░░░  ░░░░  ░░░░   ░░░░  ░░░░     ████     ████
Asset Inventory   ████  ████  ████   ████  ████     ▓▓░░     ▓▓░░
Incident Response ████  ████  ████   ████  ████     ▓▓░░     ░░░░

████ = Fully Implemented    ▓▓░░ = Partially    ░░░░ = Not Applicable
```

---

## 6. Implementation Phases

### Phase 1: Critical Gaps (P0) — ✅ Completed

- BreachNotification & ComplianceTraining entities
- ComplianceEndpoints (breach, training, dashboard)
- ComplianceScoringEngine v3
- 7 foundational documents (DPIA, DPO Charter, BCP/DRP, Privacy Notice, Breach SOP, Information Security Policy, Pseudonymization)

### Phase 2: Priority Gaps (P1) — ✅ Completed

- AssetInventory, ProcessingActivity, AiBiasTestResult entities
- 3 endpoint files (Asset, Processing Activity, AI Bias)
- ComplianceDashboard endpoint
- 6 documents (Risk Assessment, ROPA Register, Pentest Template, SOC 2, Vendor Risk, Vanta Integration)

### Phase 3: Certification Preparation — ✅ Completed

- HITRUST Self-Assessment
- Cookie Consent component
- AiModelVersion entity + 12 endpoints
- 6 documents (ISO 27001 Prep, HITRUST, HIPAA, GDPR, NIST AI RMF, AI Lifecycle)

### Phase 4: Ongoing Operations — ✅ Completed

- DataSubjectRequest entity (full lifecycle)
- ComplianceSchedule entity (recurring tasks)
- DataSubjectRequestEndpoints (12 endpoints)
- ComplianceScheduleEndpoints (10 endpoints)
- ComplianceMonitoringEndpoints (4 endpoints)
- 5 documents (Phase reports, Internal Audit, Ongoing Operations, AI Governance Charter)

### Phase 5: UI Integration — ✅ Completed

- Compliance TypeScript models (30+ interfaces)
- ComplianceService (35+ API methods)
- 6 feature pages: Dashboard, DSR Management, Compliance Schedule, Asset Inventory, AI Governance, Training Management
- Routes & navigation integration

---

## 7. Governance Structure

### 7.1 Roles & Responsibilities

| Role                              | Responsibilities                                                          | Access Level |
| --------------------------------- | ------------------------------------------------------------------------- | :----------: |
| **Compliance Officer**            | Overall program ownership, policy approval, regulatory liaison            |    Admin     |
| **DPO (Data Protection Officer)** | GDPR compliance, DPIA oversight, data subject rights, regulator interface |    Admin     |
| **CISO**                          | Security controls, incident response, risk management                     |    Admin     |
| **AI Ethics Committee**           | AI model approval, bias review, fairness standards                        |    Admin     |
| **System Administrator**          | Technical control implementation, monitoring                              |    Admin     |
| **Clinical Staff**                | Policy adherence, incident reporting, training completion                 |   Standard   |
| **External Auditor**              | Independent assessment, certification audits                              |  Read-only   |

### 7.2 Decision Authority Matrix

| Decision                           | Authority           | Approval            | Escalation            |
| ---------------------------------- | ------------------- | ------------------- | --------------------- |
| New data processing activity       | DPO                 | Compliance Officer  | Board                 |
| AI model deployment                | AI Ethics Committee | CISO + DPO          | CEO                   |
| Breach notification (HIPAA)        | Compliance Officer  | Legal + CISO        | Board                 |
| Breach notification (GDPR)         | DPO                 | Compliance Officer  | Board (72h rule)      |
| DSR rejection                      | DPO                 | Legal               | Supervisory Authority |
| Policy exception                   | CISO                | Compliance Officer  | Board                 |
| Vendor onboarding (data processor) | DPO                 | Procurement + Legal | Compliance Officer    |
| Control remediation                | System Admin        | CISO                | Compliance Officer    |

---

## 8. Document Inventory

### 8.1 Policy & Governance Documents

| Document                                                      |   ID    | Framework               | Status |
| ------------------------------------------------------------- | :-----: | ----------------------- | :----: |
| [Information Security Policy](information_security_policy.md) | ISP-001 | ISO 27001, SOC 2, HIPAA |   ✅   |
| [DPO Charter](dpo_charter.md)                                 | DPO-001 | GDPR Art. 37-39         |   ✅   |
| [AI Governance Charter](ai_governance_charter.md)             | AIG-001 | NIST AI RMF, ISO 42001  |   ✅   |
| [Privacy Notice](privacy_notice.md)                           | PRV-001 | GDPR Art. 13-14         |   ✅   |

### 8.2 Assessment & Audit Documents

| Document                                                                |   ID    | Framework    | Status |
| ----------------------------------------------------------------------- | :-----: | ------------ | :----: |
| [HIPAA Self-Assessment](hipaa_self_assessment.md)                       | HSA-001 | HIPAA        |   ✅   |
| [GDPR Readiness Assessment](gdpr_readiness_assessment.md)               | GRA-001 | GDPR         |   ✅   |
| [SOC 2 Readiness](soc2_readiness.md)                                    | S2R-001 | SOC 2        |   ✅   |
| [ISO 27001 Certification Prep](iso27001_certification_prep.md)          | I27-001 | ISO 27001    |   ✅   |
| [HITRUST Self-Assessment](hitrust_self_assessment.md)                   | HIT-001 | HITRUST CSF  |   ✅   |
| [NIST AI RMF Maturity](nist_ai_rmf_maturity.md)                         | NAR-001 | NIST AI RMF  |   ✅   |
| [Risk Assessment](risk_assessment.md)                                   | RSK-001 | ISO 27005    |   ✅   |
| [DPIA](dpia.md)                                                         | DPI-001 | GDPR Art. 35 |   ✅   |
| [Internal Audit Report](internal_audit_report.md)                       | IAR-001 | ISO 27001    |   ✅   |
| [Penetration Test Report Template](penetration_test_report_template.md) | PTR-001 | OWASP/PTES   |   ✅   |

### 8.3 Operational Documents

| Document                                                        |   ID    | Framework        | Status |
| --------------------------------------------------------------- | :-----: | ---------------- | :----: |
| [Ongoing Operations Manual](ongoing_operations_manual.md)       | OPS-001 | Multi-framework  |   ✅   |
| [Breach Notification SOP](breach_notification_sop.md)           | BNS-001 | HIPAA, GDPR      |   ✅   |
| [BCP/DRP](bcp_drp.md)                                           | BCP-001 | ISO 22301        |   ✅   |
| [Pseudonymization Procedures](pseudonymization_procedures.md)   | PSE-001 | GDPR Art. 4(5)   |   ✅   |
| [Vendor Risk Assessment](vendor_risk_assessment.md)             | VRA-001 | ISO 27001, SOC 2 |   ✅   |
| [ROPA Register](ropa_register.md)                               | RPA-001 | GDPR Art. 30     |   ✅   |
| [Standard Contractual Clauses](standard_contractual_clauses.md) | SCC-001 | GDPR Art. 46     |   ✅   |

### 8.4 Technical Integration Documents

| Document                                                    |   ID    | Purpose               | Status |
| ----------------------------------------------------------- | :-----: | --------------------- | :----: |
| [Vanta Integration Guide](vanta_integration_guide.md)       | VIG-001 | Compliance automation |   ✅   |
| [AI Lifecycle Documentation](ai_lifecycle_documentation.md) | ALD-001 | AI model registry     |   ✅   |
| [Phase 3 Final Report](phase3_final_report.md)              | P3R-001 | Phase completion      |   ✅   |
| [Phase 4 Final Report](phase4_final_report.md)              | P4R-001 | Phase completion      |   ✅   |

### 8.5 New Comprehensive Documents (This Phase)

| Document                                                                     |   ID    | Purpose                 | Status |
| ---------------------------------------------------------------------------- | :-----: | ----------------------- | :----: |
| [Compliance Master Guide](compliance_master_guide.md)                        | CMG-001 | This document           |   ✅   |
| [Compliance Flows & Procedures](compliance_flows_and_procedures.md)          | CFP-001 | Workflow diagrams       |   ✅   |
| [Implementation & Deployment Guide](compliance_implementation_deployment.md) | CID-001 | Practical deployment    |   ✅   |
| [Evaluation & Audit Guide](compliance_evaluation_audit.md)                   | CEA-001 | Scoring & assessment    |   ✅   |
| [Standards Mapping Matrix](compliance_standards_mapping.md)                  | CSM-001 | Cross-framework mapping |   ✅   |

---

## 9. Quick Reference: Endpoints & UI

### 9.1 API Endpoint Groups

| Group                 | Base Path                               | Endpoints | UI Page              |
| --------------------- | --------------------------------------- | :-------: | -------------------- |
| Monitoring & Health   | `/api/compliance/monitoring`            |     4     | Dashboard            |
| Data Subject Requests | `/api/compliance/dsr`                   |    12     | DSR Management       |
| Compliance Schedule   | `/api/compliance/schedule`              |    10     | Schedule             |
| Breaches & Training   | `/api/compliance`                       |    13     | Dashboard + Training |
| Asset Inventory       | `/api/compliance/assets`                |     7     | Asset Inventory      |
| AI Governance         | `/api/compliance/ai`                    |     9     | AI Governance        |
| Processing Activities | `/api/compliance/processing-activities` |     7     | Dashboard            |
| Enterprise Security   | `/api/security/enterprise`              |    17+    | Enterprise Security  |
| **Total**             | —                                       |  **79+**  | **6 pages**          |

### 9.2 Frontend Routes

| Route                  | Component           | Function                               |
| ---------------------- | ------------------- | -------------------------------------- |
| `/compliance`          | ComplianceDashboard | Health score, overview, all frameworks |
| `/compliance/dsr`      | DsrManagement       | Full DSR lifecycle management          |
| `/compliance/schedule` | ComplianceSchedule  | Recurring compliance task tracking     |
| `/compliance/assets`   | AssetInventory      | Data asset classification & catalog    |
| `/compliance/ai`       | AiGovernance        | AI model versions, bias testing        |
| `/compliance/training` | TrainingManagement  | Staff training assignment & tracking   |

---

## 10. Glossary

| Term            | Definition                                                                                |
| --------------- | ----------------------------------------------------------------------------------------- |
| **BAA**         | Business Associate Agreement — HIPAA contract with data processors                        |
| **BCP**         | Business Continuity Plan — Procedures to maintain operations during disruption            |
| **DPIA**        | Data Protection Impact Assessment — GDPR Art. 35 risk evaluation for high-risk processing |
| **DPO**         | Data Protection Officer — GDPR Art. 37-39 appointed officer                               |
| **DRP**         | Disaster Recovery Plan — Procedures to restore systems after failure                      |
| **DSR**         | Data Subject Request — GDPR rights exercise (access, erasure, portability, etc.)          |
| **FNR**         | False Negative Rate — AI model metric, proportion of missed positives                     |
| **FPR**         | False Positive Rate — AI model metric, proportion of incorrect positives                  |
| **HITRUST CSF** | Health Information Trust Alliance Common Security Framework                               |
| **ISMS**        | Information Security Management System — ISO 27001 core concept                           |
| **MTTR**        | Mean Time to Resolve — Security incident resolution time metric                           |
| **NIST AI RMF** | National Institute of Standards & Technology AI Risk Management Framework                 |
| **PHI**         | Protected Health Information — HIPAA-defined identifiable health data                     |
| **PII**         | Personally Identifiable Information — Data identifying an individual                      |
| **ROPA**        | Records of Processing Activities — GDPR Art. 30 requirement                               |
| **RPO**         | Recovery Point Objective — Maximum acceptable data loss (time)                            |
| **RTO**         | Recovery Time Objective — Maximum acceptable downtime                                     |
| **SCC**         | Standard Contractual Clauses — GDPR cross-border transfer mechanism                       |
| **SOC 2**       | Service Organization Control 2 — AICPA trust services audit                               |
| **TPO**         | Treatment, Payment, Operations — HIPAA permitted use exception                            |
| **Zero Trust**  | Security model requiring continuous verification, never implicit trust                    |

---

_Next: Read [Compliance Flows & Procedures](compliance_flows_and_procedures.md) for detailed workflow diagrams and step-by-step operational procedures._
