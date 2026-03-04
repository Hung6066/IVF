# HIPAA Security Rule — Self-Assessment Report

**Document ID:** IVF-HIPAA-SA-001  
**Version:** 1.0  
**Assessment Date:** 2026-03-03  
**Covered Entity:** [Clinic Name]  
**Assessment Scope:** IVF Information System — ePHI handling  
**Assessor:** [Internal Security Team / External Consultant]

---

## 1. Executive Summary

This self-assessment evaluates the IVF Information System's compliance with the HIPAA Security Rule (45 CFR Part 164, Subpart C). The assessment covers all Administrative, Physical, and Technical Safeguards applicable to electronic Protected Health Information (ePHI).

**Overall Compliance Score: 90%**

| Safeguard Category             | Total Standards | Compliant | Partial | Non-Compliant |
| ------------------------------ | :-------------: | :-------: | :-----: | :-----------: |
| Administrative (§164.308)      |        9        |     8     |    1    |       0       |
| Physical (§164.310)            |        4        |     2     |    2    |       0       |
| Technical (§164.312)           |        5        |     5     |    0    |       0       |
| Org Requirements (§164.314)    |        2        |     1     |    1    |       0       |
| Documentation (§164.316)       |        2        |     2     |    0    |       0       |
| Breach Notification (§164.400) |        4        |     4     |    0    |       0       |
| **Total**                      |     **26**      |  **22**   |  **4**  |     **0**     |

---

## 2. Administrative Safeguards (§164.308)

### §164.308(a)(1) — Security Management Process

|   Implementation Spec   | Type  | Evidence                                                                     |  Compliance  |
| :---------------------: | :---: | ---------------------------------------------------------------------------- | :----------: |
|    (i) Risk Analysis    | **R** | Risk Assessment (IVF-RA-001), automated threat scoring, DPIA                 | ✅ Compliant |
|  (ii) Risk Management   | **R** | Risk treatment plan, security controls per asset risk score                  | ✅ Compliant |
|  (iii) Sanction Policy  | **R** | Documented in ISP Section 8                                                  | ✅ Compliant |
| (iv) IS Activity Review | **R** | Partitioned audit log tables, SecurityEvent monitoring, behavioral analytics | ✅ Compliant |

### §164.308(a)(2) — Assigned Security Responsibility

| Implementation Spec | Type  | Evidence                                  |  Compliance  |
| :-----------------: | :---: | ----------------------------------------- | :----------: |
|  Security Officer   | **R** | DPO appointed, Security Lead role defined | ✅ Compliant |

### §164.308(a)(3) — Workforce Security

|      Implementation Spec      | Type  | Evidence                                                  |  Compliance  |
| :---------------------------: | :---: | --------------------------------------------------------- | :----------: |
| (i) Authorization/Supervision | **A** | RBAC (9 roles), UserGroup, ConditionalAccessPolicy        | ✅ Compliant |
|   (ii) Workforce Clearance    | **A** | Background check process (documented, not system-tracked) |  🔶 Partial  |
| (iii) Termination Procedures  | **A** | Session invalidation, account deprovisioning capability   | ✅ Compliant |

### §164.308(a)(4) — Information Access Management

|           Implementation Spec           | Type  | Evidence                                             |  Compliance  |
| :-------------------------------------: | :---: | ---------------------------------------------------- | :----------: |
|     (i) Isolating HC Clearinghouse      | **R** | N/A — not a clearinghouse                            |     N/A      |
|        (ii) Access Authorization        | **A** | UserGroup + PermissionDelegation, AdminOnly policies | ✅ Compliant |
| (iii) Access Establishment/Modification | **A** | RBAC assignment, audit trail for changes             | ✅ Compliant |

### §164.308(a)(5) — Security Awareness and Training

|           Implementation Spec           | Type  | Evidence                                                      |  Compliance  |
| :-------------------------------------: | :---: | ------------------------------------------------------------- | :----------: |
|         (i) Security Reminders          | **A** | ComplianceTraining entity, training tracking                  | ✅ Compliant |
| (ii) Protection from Malicious Software | **A** | SAST/DAST/SCA CI/CD pipeline                                  | ✅ Compliant |
|         (iii) Log-in Monitoring         | **A** | UserLoginHistory, behavioral analytics, failed login tracking | ✅ Compliant |
|        (iv) Password Management         | **A** | Password policy endpoint, BCrypt, complexity rules            | ✅ Compliant |

### §164.308(a)(6) — Security Incident Procedures

|    Implementation Spec     | Type  | Evidence                                                                                                        |  Compliance  |
| :------------------------: | :---: | --------------------------------------------------------------------------------------------------------------- | :----------: |
| (i) Response and Reporting | **R** | SecurityIncident entity, IncidentResponseRule automation, Breach Notification SOP, SecurityEvent classification | ✅ Compliant |

### §164.308(a)(7) — Contingency Plan

|          Implementation Spec          | Type  | Evidence                                                 |  Compliance  |
| :-----------------------------------: | :---: | -------------------------------------------------------- | :----------: |
|         (i) Data Backup Plan          | **R** | 3-2-1 backup strategy, automated, SHA-256 verified       | ✅ Compliant |
|      (ii) Disaster Recovery Plan      | **R** | BCP/DRP (IVF-BCP-001), tiered RTO/RPO                    | ✅ Compliant |
|    (iii) Emergency Mode Operation     | **R** | Emergency access procedures in BCP                       | ✅ Compliant |
|       (iv) Testing and Revision       | **A** | Plan documented, testing needed                          |  🔶 Partial  |
| (v) Applications and Data Criticality | **A** | Tiered classification in BCP, AssetInventory risk scores | ✅ Compliant |

### §164.308(a)(8) — Evaluation

|             Implementation Spec             | Type  | Evidence                                                                               |  Compliance  |
| :-----------------------------------------: | :---: | -------------------------------------------------------------------------------------- | :----------: |
| Periodic Technical/Non-technical Evaluation | **R** | This assessment + Internal Audit template + Pentest template + ComplianceScoringEngine | ✅ Compliant |

### §164.308(b)(1) — Business Associate Contracts

|  Implementation Spec  | Type  | Evidence                                        |          Compliance           |
| :-------------------: | :---: | ----------------------------------------------- | :---------------------------: |
| Written BAA Contracts | **R** | SCCs template, vendor risk assessment framework | 🔶 Partial — need signed BAAs |

---

## 3. Physical Safeguards (§164.310)

### §164.310(a)(1) — Facility Access Controls

|       Implementation Spec       | Type  | Evidence                                         |  Compliance  |
| :-----------------------------: | :---: | ------------------------------------------------ | :----------: |
|   (i) Contingency Operations    | **A** | BCP covers facility access                       | ✅ Compliant |
|   (ii) Facility Security Plan   | **A** | Not formally documented                          |  🔶 Partial  |
| (iii) Access Control/Validation | **A** | Physical access exists but not formally assessed |  🔶 Partial  |
|    (iv) Maintenance Records     | **A** | Not system-tracked                               |  🔶 Partial  |

### §164.310(b) — Workstation Use

|  Implementation Spec   | Type  | Evidence                                   |           Compliance            |
| :--------------------: | :---: | ------------------------------------------ | :-----------------------------: |
| Workstation Use Policy | **R** | Session timeout (JWT 60-min), auto-lockout | 🔶 Partial — need formal policy |

### §164.310(c) — Workstation Security

| Implementation Spec | Type  | Evidence              | Compliance |
| :-----------------: | :---: | --------------------- | :--------: |
| Physical Safeguards | **R** | Not formally assessed | 🔶 Partial |

### §164.310(d)(1) — Device and Media Controls

|     Implementation Spec      | Type  | Evidence                                                |  Compliance  |
| :--------------------------: | :---: | ------------------------------------------------------- | :----------: |
|         (i) Disposal         | **R** | DataRetentionPolicy entity, crypto-shred                | ✅ Compliant |
|      (ii) Media Re-use       | **R** | Docker containers rebuildable, no persistent media risk | ✅ Compliant |
|     (iii) Accountability     | **A** | AssetInventory CMDB                                     | ✅ Compliant |
| (iv) Data Backup and Storage | **A** | 3-2-1 strategy, encrypted, off-site                     | ✅ Compliant |

---

## 4. Technical Safeguards (§164.312)

### §164.312(a)(1) — Access Control

|       Implementation Spec       | Type  | Evidence                                                       |  Compliance  |
| :-----------------------------: | :---: | -------------------------------------------------------------- | :----------: |
| (i) Unique User Identification  | **R** | Guid-based User entity, unique username enforcement            | ✅ Compliant |
| (ii) Emergency Access Procedure | **R** | Emergency access in BCP, impersonation flow (RFC 8693)         | ✅ Compliant |
|     (iii) Automatic Logoff      | **A** | JWT 60-min expiry, session timeout, concurrent session control | ✅ Compliant |
| (iv) Encryption and Decryption  | **A** | AES-256-GCM field-level encryption for all ePHI                | ✅ Compliant |

### §164.312(b) — Audit Controls

|   Implementation Spec   | Type  | Evidence                                                                           |  Compliance  |
| :---------------------: | :---: | ---------------------------------------------------------------------------------- | :----------: |
| Audit Logging Mechanism | **R** | Partitioned PostgreSQL audit tables, comprehensive event logging, 5-year retention | ✅ Compliant |

### §164.312(c)(1) — Integrity

|        Implementation Spec         | Type  | Evidence                                                                  |  Compliance  |
| :--------------------------------: | :---: | ------------------------------------------------------------------------- | :----------: |
| (i) Mechanism to Authenticate ePHI | **A** | Digital signing (PKI), SHA-256 backup verification, data integrity checks | ✅ Compliant |

### §164.312(d) — Person or Entity Authentication

|   Implementation Spec    | Type  | Evidence                                                                                    |  Compliance  |
| :----------------------: | :---: | ------------------------------------------------------------------------------------------- | :----------: |
| Authentication Mechanism | **R** | Triple auth pipeline (VaultToken → ApiKey → JWT), MFA (TOTP/SMS/Passkey/WebAuthn/Biometric) | ✅ Compliant |

### §164.312(e)(1) — Transmission Security

|  Implementation Spec   | Type  | Evidence                                             |  Compliance  |
| :--------------------: | :---: | ---------------------------------------------------- | :----------: |
| (i) Integrity Controls | **A** | TLS 1.2+, mTLS for signing services                  | ✅ Compliant |
|    (ii) Encryption     | **A** | TLS 1.2+ for all API traffic, mTLS for inter-service | ✅ Compliant |

---

## 5. Breach Notification Rule (§164.400-414)

| Requirement                            | Evidence                                                    |  Compliance  |
| -------------------------------------- | ----------------------------------------------------------- | :----------: |
| §164.404 — Individual Notification     | BreachNotification entity, privacy notice with contact info | ✅ Compliant |
| §164.406 — Media Notification (500+)   | Breach SOP covers media notification for 500+ individuals   | ✅ Compliant |
| §164.408 — HHS Secretary Notification  | Breach SOP includes HHS notification within 60 days         | ✅ Compliant |
| §164.410 — BA Notification to CE       | SCCs + DPA require processor breach notification <72 hours  | ✅ Compliant |
| §164.414 — Administrative Requirements | Breach log (BreachNotification entity), <500 annual report  | ✅ Compliant |

---

## 6. Risk Analysis Summary

### ePHI Inventory

| System/Component           | ePHI Type                       |  Storage   |       Encryption        |  Access Control   |
| -------------------------- | ------------------------------- | :--------: | :---------------------: | :---------------: |
| PostgreSQL DB              | Patient records, treatment data |  At rest   | AES-256-GCM field-level | RBAC + row-level  |
| MinIO (ivf-documents)      | Medical documents               |  At rest   |    Encrypted bucket     |  Presigned URLs   |
| MinIO (ivf-signed-pdfs)    | Signed consent forms            |  At rest   |    Encrypted bucket     |  Presigned URLs   |
| MinIO (ivf-medical-images) | Ultrasound, embryo images       |  At rest   |    Encrypted bucket     |  Presigned URLs   |
| Redis Cache                | Session data (no PHI)           | In memory  |   N/A (no PHI cached)   | Network isolation |
| Backup files               | Full DB backups                 |  At rest   |   Encrypted + SHA-256   | Access restricted |
| API responses              | PHI in transit                  | In transit |        TLS 1.2+         |    JWT + RBAC     |
| SignalR messages           | Queue data (no PHI displayed)   | In transit |        WSS (TLS)        |     JWT auth      |

### Threat Assessment

| Threat              | Likelihood |  Impact  | Risk Level | Mitigation                              |
| ------------------- | :--------: | :------: | :--------: | --------------------------------------- |
| Unauthorized access |    Low     |   High   |   Medium   | MFA, RBAC, Zero Trust                   |
| SQL injection       |  Very Low  | Critical |    Low     | Parameterized queries, FluentValidation |
| Ransomware          |    Low     | Critical |   Medium   | 3-2-1 backup, network segmentation      |
| Insider threat      |    Low     |   High   |   Medium   | Audit logging, behavioral analytics     |
| Data interception   |  Very Low  |   High   |    Low     | TLS 1.2+, mTLS, field encryption        |
| Social engineering  |   Medium   |  Medium  |   Medium   | Training program, MFA                   |
| Physical theft      |    Low     |  Medium  |    Low     | Encryption at rest, remote wipe         |

---

## 7. Remediation Plan

|  #  | Gap                          |  HIPAA Reference   | Priority | Target Date | Owner      |
| :-: | ---------------------------- | :----------------: | :------: | :---------: | ---------- |
|  1  | Formal DR testing            | §164.308(a)(7)(iv) |   High   | 2026-04-15  | DevOps     |
|  2  | Signed BAAs for vendors      |   §164.308(b)(1)   |   High   | 2026-04-01  | Legal      |
|  3  | Physical security assessment |    §164.310(a)     |  Medium  | 2026-05-01  | Facilities |
|  4  | Workstation use policy       |    §164.310(b)     |  Medium  | 2026-03-30  | Security   |
|  5  | Background check tracking    |   §164.308(a)(3)   |   Low    | 2026-04-30  | HR         |

---

## 8. Document Control

| Version |    Date    | Author        | Changes                       |
| :-----: | :--------: | ------------- | ----------------------------- |
|   1.0   | 2026-03-03 | Security Team | Initial HIPAA self-assessment |

**Approved by:**

| Role               | Name                 | Date         | Signature  |
| ------------------ | -------------------- | ------------ | ---------- |
| Security Officer   | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Compliance Officer | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Clinical Director  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

---

## Related Documents — Compliance Documentation Suite

### Comprehensive Guides

- [Compliance Master Guide](compliance_master_guide.md) — Executive overview, program structure, governance, glossary
- [Compliance Flows & Procedures](compliance_flows_and_procedures.md) — Detailed workflows for DSR, breach, AI governance, incident response
- [Implementation & Deployment Guide](compliance_implementation_deployment.md) — Practical deployment, configuration, runbooks, DR procedures
- [Evaluation & Audit Guide](compliance_evaluation_audit.md) — Scoring methodology, health score, maturity model, audit preparation
- [Standards Mapping Matrix](compliance_standards_mapping.md) — Cross-framework traceability (HIPAA, GDPR, SOC 2, ISO 27001, HITRUST, NIST AI, ISO 42001)

### Related Assessments & Frameworks

- [HIPAA Self-Assessment](hipaa_self_assessment.md)
- [GDPR Readiness Assessment](gdpr_readiness_assessment.md)
- [SOC 2 Readiness](soc2_readiness.md)
- [ISO 27001 Certification Prep](iso27001_certification_prep.md)
- [HITRUST Self-Assessment](hitrust_self_assessment.md)
- [NIST AI RMF Maturity](nist_ai_rmf_maturity.md)
- [Risk Assessment](risk_assessment.md)
