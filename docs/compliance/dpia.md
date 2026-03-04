# Data Protection Impact Assessment (DPIA)

**Document ID:** IVF-DPIA-001  
**Version:** 1.0  
**Date:** 2026-03-03  
**Assessor:** Security & Compliance Team  
**DPO Review:** Pending  
**Status:** Draft  
**Legal Basis:** GDPR Article 35, HIPAA §164.308(a)(1)(ii)(A)

---

## 1. Overview

### 1.1 Purpose

This Data Protection Impact Assessment evaluates the risks to data subjects arising from the processing of personal data in the IVF Information System. This assessment is required under GDPR Article 35 due to:

- **Large-scale processing** of special category data (health records)
- **Biometric data processing** for patient identification
- **Automated decision-making** in threat detection and access control
- **Systematic monitoring** of clinical workflows and user behavior

### 1.2 System Description

| Attribute       | Detail                                                               |
| --------------- | -------------------------------------------------------------------- |
| System Name     | IVF Information System                                               |
| Purpose         | Fertility clinic management (patient care, billing, lab, scheduling) |
| Data Controller | [Organization Name]                                                  |
| Data Processor  | Internal (self-hosted)                                               |
| Technology      | .NET 10 backend, Angular 21 frontend, PostgreSQL, MinIO, Redis       |
| Deployment      | Docker containers (on-premises / private cloud)                      |

## 2. Data Processing Activities

### 2.1 Personal Data Categories

| Category                  | Data Elements                                                                   | Legal Basis (GDPR Art. 6)                             | Retention                               |
| ------------------------- | ------------------------------------------------------------------------------- | ----------------------------------------------------- | --------------------------------------- |
| **Patient Identity**      | Name, DOB, national ID, phone, email, address                                   | Art. 6(1)(b) Contract + Art. 6(1)(c) Legal obligation | Duration of treatment + 7 years (HIPAA) |
| **Medical Records (PHI)** | Diagnosis, treatment plans, lab results, ultrasound, embryo grading, medication | Art. 9(2)(h) Healthcare provision                     | Duration of treatment + 7 years         |
| **Reproductive Data**     | Sperm analysis, oocyte count, embryo quality, pregnancy outcomes                | Art. 9(2)(h) Healthcare provision                     | Duration of treatment + 7 years         |
| **Biometric Data**        | Fingerprint templates (DigitalPersona)                                          | Art. 9(2)(a) Explicit consent                         | Duration of treatment + 30 days         |
| **Financial Data**        | Billing records, payment history, insurance                                     | Art. 6(1)(b) Contract                                 | 7 years (tax/accounting)                |
| **Staff Data**            | Employee names, credentials, access logs, login history                         | Art. 6(1)(f) Legitimate interest                      | Employment duration + 3 years           |
| **Security Data**         | IP addresses, device fingerprints, behavioral profiles, threat assessments      | Art. 6(1)(f) Legitimate interest                      | 3 years (security audit)                |

### 2.2 Data Flows

```
                    ┌──────────────┐
                    │   Patient    │
                    │  (Web/App)   │
                    └──────┬───────┘
                           │ TLS 1.3
                    ┌──────▼───────┐
                    │   Angular    │◄──── Consent Collection
                    │   Frontend   │      (8 consent types)
                    └──────┬───────┘
                           │ JWT + TLS
                    ┌──────▼───────┐
                    │   IVF API    │◄──── Zero Trust Evaluation
                    │   (.NET 10)  │      (every request)
                    └──┬───┬───┬───┘
                       │   │   │
          ┌────────────┘   │   └────────────┐
          ▼                ▼                ▼
   ┌──────────┐     ┌──────────┐     ┌──────────┐
   │PostgreSQL│     │  MinIO   │     │  Redis   │
   │(PHI,PII) │     │(Images,  │     │(Cache,   │
   │AES-256   │     │ PDFs)    │     │ Sessions)│
   └──────────┘     └──────────┘     └──────────┘
```

### 2.3 Data Recipients

| Recipient                       | Data Shared                    | Purpose               | Safeguards                                     |
| ------------------------------- | ------------------------------ | --------------------- | ---------------------------------------------- |
| Clinicians (internal)           | Full patient records           | Healthcare provision  | Role-based access (Doctor/Nurse roles)         |
| Lab technicians (internal)      | Lab results, sperm/embryo data | Laboratory processing | Role-based access (LabTech/Embryologist roles) |
| Administrative staff (internal) | Patient demographics, billing  | Administration        | Role-based access (Receptionist/Cashier)       |
| Backup storage (cloud)          | Encrypted backups              | Disaster recovery     | AES-256 encryption + 3-2-1 strategy            |
| SignServer/EJBCA (internal)     | Document hashes (no PHI)       | Digital signing       | mTLS, network-isolated                         |

### 2.4 International Transfers

| Transfer              | From    | To                | Mechanism                    | Risk                   |
| --------------------- | ------- | ----------------- | ---------------------------- | ---------------------- |
| Cloud backups         | Vietnam | AWS S3 (regional) | Encryption + access controls | Medium — requires SCCs |
| None other identified | —       | —                 | —                            | —                      |

## 3. Necessity and Proportionality Assessment

### 3.1 Necessity

| Processing Activity      | Necessity Justification                                             |
| ------------------------ | ------------------------------------------------------------------- |
| Patient medical records  | Essential for fertility treatment — legal requirement               |
| Biometric identification | Prevents patient misidentification in critical medical procedures   |
| Behavioral analytics     | Secures PHI/PII against unauthorized access — proportionate to risk |
| Audit logging            | HIPAA §164.312(b) requirement, GDPR Art. 5(2) accountability        |

### 3.2 Proportionality

| Principle              | Implementation                                                                        | Score |
| ---------------------- | ------------------------------------------------------------------------------------- | :---: |
| **Data minimization**  | Field-level access policies, role-based data masking (PiiMasker), consent enforcement |  ✅   |
| **Purpose limitation** | Consent types mapped to specific endpoints, no secondary use without consent          |  ✅   |
| **Storage limitation** | DataRetentionPolicy with automated purging (daily at 2 AM UTC)                        |  ✅   |
| **Accuracy**           | Audit trail with before/after change tracking via EF Core interceptor                 |  ✅   |
| **Integrity**          | AES-256-GCM encryption, immutable audit logs, SHA-256 backup verification             |  ✅   |

## 4. Risk Assessment

### 4.1 Risk Identification

|  #  | Risk                                   | Category        | Likelihood |  Impact  | Risk Level | Mitigation                                                              |
| :-: | -------------------------------------- | --------------- | :--------: | :------: | :--------: | ----------------------------------------------------------------------- |
| R1  | Unauthorized access to patient records | Confidentiality |    Low     | Critical |  **High**  | Zero Trust (7 signals), MFA, Conditional Access, field-level encryption |
| R2  | Patient misidentification              | Integrity       |    Low     | Critical |  **High**  | Biometric verification (fingerprint), dual-ID check                     |
| R3  | Data breach via SQL injection          | Confidentiality |  Very Low  | Critical | **Medium** | EF Core parameterized queries, WAF input validation, CodeQL SAST        |
| R4  | Insider threat (staff data abuse)      | Confidentiality |    Low     |   High   | **Medium** | Behavioral analytics, session binding, audit trail, impersonation audit |
| R5  | Ransomware/data loss                   | Availability    |    Low     | Critical |  **High**  | 3-2-1 backup, PITR (14-day), streaming replication, encrypted backups   |
| R6  | Biometric template theft               | Confidentiality |  Very Low  | Critical | **Medium** | AES-256-GCM encryption, consent-gated access, template-only storage     |
| R7  | Unauthorized international transfer    | Compliance      |    Low     |   High   | **Medium** | Encrypted backups only, no PHI in logs, SCCs planned                    |
| R8  | False positive threat blocking         | Availability    |   Medium   |  Medium  | **Medium** | Human review for incidents, false positive workflow, tunable thresholds |
| R9  | Consent withdrawal data processing     | Compliance      |    Low     |  Medium  |  **Low**   | ConsentEnforcementMiddleware blocks access without valid consent        |
| R10 | Excessive data retention               | Compliance      |    Low     |  Medium  |  **Low**   | Automated DataRetentionPolicy, configurable per entity type             |

### 4.2 Risk Matrix

```
         Impact →
         Low    Medium    High    Critical
  L  ┌─────────┬─────────┬─────────┬─────────┐
  i  │         │         │         │         │
  k  │ Very    │  LOW    │  LOW    │ MEDIUM  │ MEDIUM  │
  e  │ Low     │         │   R3    │   R6    │         │
  l  ├─────────┼─────────┼─────────┼─────────┤
  i  │         │         │         │         │
  h  │ Low     │  LOW    │  LOW    │ MEDIUM  │  HIGH   │
  o  │         │  R9,R10 │   R7    │   R4    │ R1,R2,R5│
  o  ├─────────┼─────────┼─────────┼─────────┤
  d  │         │         │         │         │
  ↓  │ Medium  │  LOW    │ MEDIUM  │  HIGH   │  HIGH   │
     │         │         │   R8    │         │         │
     └─────────┴─────────┴─────────┴─────────┘
```

## 5. Risk Mitigation Measures

### 5.1 Technical Measures (Already Implemented)

| Measure                         | Description                                             | Frameworks Addressed    |
| ------------------------------- | ------------------------------------------------------- | ----------------------- |
| **AES-256-GCM Encryption**      | Field-level encryption for PHI/PII at rest              | HIPAA, GDPR, SOC 2      |
| **TLS 1.3 + HSTS**              | All data in transit encrypted with modern protocols     | HIPAA, GDPR, SOC 2      |
| **Zero Trust Architecture**     | Continuous request evaluation, 7 threat signals         | NIST 800-207, SOC 2     |
| **Multi-Factor Authentication** | TOTP, SMS, Passkey/WebAuthn, biometric                  | HIPAA, GDPR, ISO 27001  |
| **Role-Based Access Control**   | 9 roles, 50+ permissions, Conditional Access            | HIPAA, SOC 2, ISO 27001 |
| **Audit Logging**               | 50+ SecurityEvent types, immutable, MITRE ATT&CK mapped | HIPAA, SOC 2, GDPR      |
| **Consent Management**          | 8 consent types, ConsentEnforcementMiddleware           | GDPR Art. 6-7           |
| **Data Retention**              | Automated purging (DataRetentionPolicy)                 | GDPR Art. 5(1)(e)       |
| **Incident Response**           | Automated actions (lock, revoke, notify, block)         | SOC 2, ISO 27001        |
| **Backup 3-2-1**                | Primary + Standby + Cloud, PITR 14 days                 | HIPAA, ISO 27001        |

### 5.2 Organizational Measures (Phase 1 Implementation)

| Measure                             |        Status         | Target Date |
| ----------------------------------- | :-------------------: | :---------: |
| Breach Notification SOP             |      ✅ Created       | 2026-03-03  |
| Security Awareness Training Program | ✅ System implemented | 2026-03-03  |
| DPO Appointment                     |      ⬜ Pending       | 2026-03-10  |
| Formal Security Policy              |       ⬜ Draft        | 2026-03-14  |
| Business Continuity Plan            |       ⬜ Draft        | 2026-03-17  |
| Vendor Risk Assessment              | ⬜ Framework created  | 2026-03-24  |

## 6. Data Subject Rights

| Right                       | Implementation                                            | API Endpoint                          |
| --------------------------- | --------------------------------------------------------- | ------------------------------------- |
| **Access (Art. 15)**        | Patient can view all personal data via portal             | `GET /api/user-consents/my-status`    |
| **Rectification (Art. 16)** | Patient can request data correction                       | `PUT /api/patients/{id}` (with audit) |
| **Erasure (Art. 17)**       | Automated deletion via retention policy; manual via admin | DataRetentionPolicy (Delete action)   |
| **Restriction (Art. 18)**   | Consent withdrawal blocks processing                      | `UserConsent.Revoke()`                |
| **Portability (Art. 20)**   | Data export capability                                    | Export endpoints (planned)            |
| **Objection (Art. 21)**     | Opt-out for analytics/marketing                           | Consent management per type           |

## 7. Consultation

### 7.1 Internal Stakeholders

| Stakeholder             | Consulted | Key Concerns                                      |
| ----------------------- | :-------: | ------------------------------------------------- |
| Medical Director        |    ⬜     | Clinical data accuracy, patient safety            |
| IT/Security Team        |    ✅     | Technical controls, monitoring, incident response |
| Legal Counsel           |    ⬜     | Regulatory requirements, liability                |
| DPO                     |    ⬜     | GDPR compliance, data subject rights              |
| Patient Representatives |    ⬜     | Consent clarity, access to records                |

### 7.2 DPA Consultation (Art. 36)

Prior consultation with the DPA is required if residual risk remains high after mitigation. Based on this assessment:

- **Current residual risk: MEDIUM** — DPA consultation recommended but not mandatory.

## 8. DPIA Conclusion

### 8.1 Summary

| Metric                         | Value      |
| ------------------------------ | ---------- |
| Total risks identified         | 10         |
| High risks (pre-mitigation)    | 5          |
| High risks (post-mitigation)   | 0          |
| Medium risks (post-mitigation) | 4          |
| Low risks (post-mitigation)    | 6          |
| **Overall residual risk**      | **MEDIUM** |

### 8.2 Decision

The processing **may proceed** with the following conditions:

1. All Phase 1 organizational measures must be completed by 2026-04-01
2. DPO must be formally appointed by 2026-03-10
3. Annual DPIA review must be scheduled
4. SCCs must be implemented for any international data transfers

### 8.3 Review Schedule

| Review        | Date       | Trigger                                                     |
| ------------- | ---------- | ----------------------------------------------------------- |
| First review  | 2026-09-03 | 6 months post-assessment                                    |
| Annual review | 2027-03-03 | Annual                                                      |
| Ad-hoc review | As needed  | System changes, new processing activities, breach incidents |

---

**Approval:**

| Role            | Name                 | Date         | Signature  |
| --------------- | -------------------- | ------------ | ---------- |
| Assessor        | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO             | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Data Controller | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

---

## Related Documents — Compliance Documentation Suite

### Comprehensive Guides

- [Compliance Master Guide](compliance_master_guide.md) — Executive overview, program structure, governance, glossary
- [Compliance Flows & Procedures](compliance_flows_and_procedures.md) — Detailed workflows for DSR, breach, AI governance, incident response
- [Implementation & Deployment Guide](compliance_implementation_deployment.md) — Practical deployment, configuration, runbooks, DR procedures
- [Evaluation & Audit Guide](compliance_evaluation_audit.md) — Scoring methodology, health score, maturity model, audit preparation
- [Standards Mapping Matrix](compliance_standards_mapping.md) — Cross-framework traceability (HIPAA, GDPR, SOC 2, ISO 27001, HITRUST, NIST AI, ISO 42001)

### Related Risk & Audit

- [Risk Assessment](risk_assessment.md)
- [DPIA](dpia.md)
- [Internal Audit Report](internal_audit_report.md)
- [Penetration Test Report Template](penetration_test_report_template.md)
