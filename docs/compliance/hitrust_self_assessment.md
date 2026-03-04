# HITRUST CSF v11 Self-Assessment

**Document ID:** IVF-HITRUST-SA-001  
**Version:** 1.0  
**Assessment Date:** 2026-03-03  
**Assessor:** [Internal Security Team]  
**Scope:** IVF Information System — Full Platform  
**Assessment Type:** HITRUST CSF v11 Self-Assessment (readiness for Validated Assessment)

---

## 1. Executive Summary

This self-assessment evaluates the IVF Information System against the HITRUST Common Security Framework (CSF) v11. The assessment covers all 14 control categories and maps to existing compliance evidence from ISO 27001, HIPAA, SOC 2, and GDPR implementations.

### Overall Maturity Score

| Maturity Level | Description |     Target     |   Current   |
| :------------: | ----------- | :------------: | :---------: |
|       1        | Policy      |  All domains   | ✅ Achieved |
|       2        | Procedure   |  All domains   | ✅ Achieved |
|       3        | Implemented |  All domains   | ✅ Achieved |
|       4        | Measured    |  Most domains  | 🔶 Partial  |
|       5        | Managed     | Select domains | 🔶 Partial  |

**Overall Readiness:** 78% — Ready for HITRUST e1 (Essential) certification; nearing readiness for i1 (Implemented) certification.

---

## 2. Control Category Assessment

### 2.1 — Information Protection Program (00.a)

| Req ID  | Requirement                                   | Maturity | Evidence                                                                         | Gap                          |
| :-----: | --------------------------------------------- | :------: | -------------------------------------------------------------------------------- | ---------------------------- |
|  00.a   | Information Security Management Program       |    4     | ISP document (IVF-ISP-001), CLAUDE.md architecture docs, ComplianceScoringEngine | None                         |
| 00.01.a | Management Direction for Information Security |    3     | Information Security Policy signed by management                                 | Needs annual review schedule |
| 00.01.b | Review of IS Policies                         |    3     | Version control on all policy documents                                          | Formalize review cadence     |

**Evidence References:**

- [docs/compliance/information_security_policy.md](../compliance/information_security_policy.md)
- [docs/compliance/risk_assessment.md](../compliance/risk_assessment.md)

### 2.2 — Endpoint Protection (01.a-01.y)

| Req ID | Requirement                            | Maturity | Evidence                                                                               | Gap                            |
| :----: | -------------------------------------- | :------: | -------------------------------------------------------------------------------------- | ------------------------------ |
|  01.a  | Access Control Policy                  |    4     | RBAC with 9 roles, 50+ permissions, ConditionalAccessPolicy entity                     | None                           |
|  01.b  | User Registration                      |    4     | User entity with registration flow, UserLoginHistory tracking                          | None                           |
|  01.c  | Privilege Management                   |    4     | Admin role separation, PermissionDelegation entity, ImpersonationRequest with RFC 8693 | None                           |
|  01.d  | User Password Management               |    4     | BCrypt hashing, password policy endpoint, complexity requirements                      | None                           |
|  01.e  | Review of User Access Rights           |    3     | UserGroupPermission, periodic review capability                                        | Formalize quarterly review     |
|  01.h  | Clear Desk and Clear Screen            |    2     | JWT 60-min expiry, session timeout                                                     | Need clear desk policy doc     |
|  01.j  | On-line Transactions                   |    4     | TLS 1.2+, CSRF protection, rate limiting (100 req/min)                                 | None                           |
|  01.q  | User Identification and Authentication |    5     | MFA (TOTP, SMS, Passkey, WebAuthn, Biometric), triple auth pipeline                    | None                           |
|  01.r  | Password Management System             |    4     | BCrypt, configurable complexity via password policy endpoint                           | None                           |
|  01.t  | Session Management                     |    4     | UserSession entity, JWT 60-min + 7-day refresh, concurrent session control             | None                           |
|  01.v  | Information Access Restriction         |    4     | RBAC + field-level access, PHI encryption per role                                     | None                           |
|  01.x  | Mobile Computing and Teleworking       |    2     | ConditionalAccessPolicy covers device compliance                                       | Need mobile device policy      |
|  01.y  | Teleworking                            |    2     | Conditional access covers location                                                     | Need formal remote work policy |

**Evidence References:**

- RBAC: 9 roles (Admin, Doctor, Nurse, LabTech, Embryologist, Receptionist, Cashier, Pharmacist)
- Triple auth: VaultToken → ApiKey → JWT Bearer
- Entities: User, UserSession, UserGroup, UserGroupMember, ConditionalAccessPolicy

### 2.3 — Portable Media Security (02.a-02.e)

| Req ID | Requirement                   | Maturity | Evidence                                                      | Gap               |
| :----: | ----------------------------- | :------: | ------------------------------------------------------------- | ----------------- |
|  02.a  | Management of Removable Media |    2     | No removable media policy                                     | Create policy     |
|  02.d  | Physical Media in Transit     |    2     | Data primarily digital (MinIO storage)                        | Document controls |
|  02.e  | Electronic Media              |    3     | AES-256-GCM encryption for all PHI at rest, encrypted backups | None              |

### 2.4 — Risk Management (03.a-03.d)

| Req ID | Requirement             | Maturity | Evidence                                                                   | Gap                             |
| :----: | ----------------------- | :------: | -------------------------------------------------------------------------- | ------------------------------- |
|  03.a  | Risk Assessment         |    4     | Formal risk assessment (IVF-RA-001), automated threat scoring (7 signals)  | None                            |
|  03.b  | Risk Mitigation         |    4     | Automated incident response, SecurityIncident entity, remediation tracking | None                            |
|  03.c  | Risk Evaluation         |    3     | AssetInventory.CalculateRiskScore(), ComplianceScoringEngine               | Formalize risk appetite         |
|  03.d  | Risk Management Program |    3     | Risk assessment + vendor risk assessment + DPIA                            | Integrate into single framework |

**Evidence References:**

- [docs/compliance/risk_assessment.md](../compliance/risk_assessment.md)
- [docs/compliance/vendor_risk_assessment.md](../compliance/vendor_risk_assessment.md)
- [docs/compliance/dpia.md](../compliance/dpia.md)
- AssetInventory entity with risk scoring

### 2.5 — Security Policy (04.a-04.b)

| Req ID | Requirement                           | Maturity | Evidence                                | Gap                                              |
| :----: | ------------------------------------- | :------: | --------------------------------------- | ------------------------------------------------ |
|  04.a  | Information Security Policy           |    4     | Comprehensive ISP (IVF-ISP-001), signed | None                                             |
|  04.b  | Review of Information Security Policy |    3     | Version control on policy docs          | Formalize annual review with management sign-off |

### 2.6 — Organization of Information Security (05.a-05.k)

| Req ID | Requirement                                 | Maturity | Evidence                                                  | Gap                             |
| :----: | ------------------------------------------- | :------: | --------------------------------------------------------- | ------------------------------- |
|  05.a  | Management Commitment                       |    3     | ISP approval signatures, AI Governance Board defined      | None                            |
|  05.b  | Information Security Coordination           |    3     | DPO appointed, security team roles documented             | None                            |
|  05.c  | Allocation of IS Responsibilities           |    4     | RBAC, UserGroup assignments, documented roles             | None                            |
|  05.d  | Authorization Process for IT Facilities     |    3     | Docker infrastructure controlled, change management       | Formalize change advisory board |
|  05.h  | Independent Review                          |    3     | Internal audit template (IVF-AUDIT-001), pentest template | Schedule first external review  |
|  05.i  | Identification of Risks (3rd party)         |    4     | Vendor Risk Assessment Framework (IVF-VRA-001)            | None                            |
|  05.k  | Addressing Security in 3rd Party Agreements |    3     | SCCs template, DPA requirements defined                   | None                            |

### 2.7 — Compliance (06.a-06.j)

| Req ID | Requirement                              | Maturity | Evidence                                                                          | Gap                          |
| :----: | ---------------------------------------- | :------: | --------------------------------------------------------------------------------- | ---------------------------- |
|  06.a  | Identification of Applicable Legislation |    3     | GDPR, HIPAA, local medical records law identified                                 | Document all applicable laws |
|  06.c  | Protection of Organizational Records     |    4     | Partitioned PostgreSQL audit tables, 5-year retention, DataRetentionPolicy entity | None                         |
|  06.d  | Data Protection and Privacy              |    4     | Privacy Notice (VN+EN), ROPA, DPIA, DPO appointed, field-level encryption         | None                         |
|  06.e  | Prevention of Misuse                     |    3     | User behavior monitoring, conditional access, session controls                    | None                         |
|  06.g  | Compliance with Security Policies        |    3     | ComplianceScoringEngine, automated monitoring                                     | None                         |
|  06.h  | Technical Compliance Checking            |    3     | SAST/DAST/SCA pipeline, penetration test template                                 | None                         |
|  06.i  | Information Systems Audit Controls       |    3     | Audit partitioned tables, log integrity                                           | None                         |

### 2.8 — Asset Management (07.a-07.e)

| Req ID | Requirement               | Maturity | Evidence                                                             | Gap                          |
| :----: | ------------------------- | :------: | -------------------------------------------------------------------- | ---------------------------- |
|  07.a  | Inventory of Assets       |    4     | AssetInventory CMDB entity + endpoints, 10 asset types, risk scoring | None                         |
|  07.b  | Ownership of Assets       |    4     | AssetInventory.Owner field, classification scheme                    | None                         |
|  07.c  | Acceptable Use of Assets  |    2     | Not formally documented                                              | Create acceptable use policy |
|  07.d  | Classification Guidelines |    4     | 4-level classification (public/internal/confidential/restricted)     | None                         |
|  07.e  | Labeling and Handling     |    3     | AssetInventory.Classification, data labels                           | None                         |

### 2.9 — Human Resources Security (08.a-08.j)

| Req ID | Requirement                       | Maturity | Evidence                                                             | Gap                                |
| :----: | --------------------------------- | :------: | -------------------------------------------------------------------- | ---------------------------------- |
|  08.a  | Roles and Responsibilities        |    4     | 9 RBAC roles, documented responsibilities                            | None                               |
|  08.b  | Screening                         |    2     | Process exists but not system-tracked                                | Add to ComplianceTraining tracking |
|  08.c  | Terms of Employment               |    2     | Not system-tracked                                                   | Document security clauses          |
|  08.d  | Management Responsibilities       |    3     | ComplianceTraining entity, training tracking                         | None                               |
|  08.e  | IS Awareness, Education, Training |    4     | ComplianceTraining entity with type/score/status, training endpoints | None                               |
|  08.j  | Management of Access Rights       |    4     | RBAC, UserGroup, session management, deprovisioning capability       | None                               |

### 2.10 — Physical and Environmental Security (09.a-09.p)

| Req ID | Requirement                        | Maturity | Evidence                                            | Gap                               |
| :----: | ---------------------------------- | :------: | --------------------------------------------------- | --------------------------------- |
|  09.a  | Physical Security Perimeter        |    2     | On-premises server                                  | Need physical security assessment |
|  09.b  | Physical Entry Controls            |    2     | Facility access exists                              | Document controls                 |
|  09.c  | Securing Offices                   |    2     | Basic physical controls                             | Document controls                 |
|  09.l  | Cabling Security                   |    2     | Standard deployment                                 | Need assessment                   |
|  09.m  | Equipment Maintenance              |    2     | Docker containerization simplifies                  | Document procedures               |
|  09.o  | Security of Equipment Off-Premises |    2     | Encrypted data only                                 | Document policy                   |
|  09.p  | Secure Disposal                    |    3     | DataRetentionPolicy entity, crypto-shred capability | None                              |

### 2.11 — Communications and Operations Management (10.a-10.m)

| Req ID | Requirement                       | Maturity | Evidence                                                 | Gap                         |
| :----: | --------------------------------- | :------: | -------------------------------------------------------- | --------------------------- |
|  10.a  | Documented Operating Procedures   |    3     | Docker Compose deployment, appsettings, CLAUDE.md        | None                        |
|  10.b  | Change Management                 |    3     | Git-based, CI/CD pipeline, GitHub Actions                | Formalize CAB               |
|  10.c  | Segregation of Duties             |    4     | 9 distinct RBAC roles, AdminOnly policies                | None                        |
|  10.d  | Separation of Dev/Test/Prod       |    3     | appsettings.Development.json, environment separation     | Formalize promotion process |
|  10.f  | Service Delivery Management       |    3     | Vendor risk assessment, SLAs defined                     | None                        |
|  10.h  | Capacity Management               |    2     | Basic monitoring                                         | Add resource alerts         |
|  10.j  | Protection Against Malicious Code |    4     | CodeQL SAST, Trivy SCA, Gitleaks, ZAP DAST in CI/CD      | None                        |
|  10.k  | Back-up                           |    4     | 3-2-1 strategy, encrypted, SHA-256 verified, automated   | None                        |
|  10.l  | Network Controls                  |    4     | Docker network segmentation (3 networks), firewall rules | None                        |
|  10.m  | Security of Network Services      |    4     | mTLS for signing, TLS 1.2+, VPN for admin                | None                        |

### 2.12 — Information Systems Acquisition, Development, and Maintenance (11.a-11.b)

| Req ID | Requirement                    | Maturity | Evidence                                                  | Gap  |
| :----: | ------------------------------ | :------: | --------------------------------------------------------- | ---- |
|  11.a  | Security Requirements Analysis |    3     | CLAUDE.md security architecture, threat modeling          | None |
|  11.b  | Correct Processing             |    4     | FluentValidation, input validation, parameterized queries | None |

### 2.13 — Information Security Incident Management (12.a-12.e)

| Req ID | Requirement                     | Maturity | Evidence                                                                      | Gap                       |
| :----: | ------------------------------- | :------: | ----------------------------------------------------------------------------- | ------------------------- |
|  12.a  | Reporting IS Events             |    4     | SecurityEvent entity, automated detection, SecurityIncident entity            | None                      |
|  12.b  | Reporting Security Weaknesses   |    3     | Vulnerability scanning CI/CD, pentest template                                | None                      |
|  12.c  | Responsibilities and Procedures |    4     | IncidentResponseRule entity with automated responses, Breach Notification SOP | None                      |
|  12.d  | Learning from Incidents         |    3     | Post-incident review capability, behavioral analytics                         | Formalize lessons learned |
|  12.e  | Collection of Evidence          |    4     | Partitioned audit tables, preserved logs, chain of custody                    | None                      |

**Evidence References:**

- [docs/compliance/breach_notification_sop.md](../compliance/breach_notification_sop.md)
- BreachNotification entity, SecurityIncident entity, IncidentResponseRule entity

### 2.14 — Business Continuity Management (13.a-13.e)

| Req ID | Requirement                          | Maturity | Evidence                                              | Gap                        |
| :----: | ------------------------------------ | :------: | ----------------------------------------------------- | -------------------------- |
|  13.a  | Including IS in BC Management        |    4     | BCP/DRP document (IVF-BCP-001) with IT-specific plans | None                       |
|  13.b  | BC and Risk Assessment               |    3     | Risk assessment covers BC scenarios                   | None                       |
|  13.c  | Developing and Implementing BC Plans |    3     | BCP with RTO/RPO for each tier                        | Test BC plan               |
|  13.d  | BC Planning Framework                |    3     | Single framework document                             | None                       |
|  13.e  | Testing, Maintaining BC Plans        |    2     | Plan documented but not formally tested               | Schedule tabletop exercise |

**Evidence References:**

- [docs/compliance/bcp_drp.md](../compliance/bcp_drp.md)

---

## 3. Maturity Score Summary

| Control Category                    | # Controls | Avg Maturity | Target |    Status    |
| ----------------------------------- | :--------: | :----------: | :----: | :----------: |
| 00 — Information Protection Program |     3      |     3.3      |   3    |   ✅ Meets   |
| 01 — Endpoint Protection            |     13     |     3.5      |   3    |  ✅ Exceeds  |
| 02 — Portable Media Security        |     3      |     2.3      |   3    |   🔶 Below   |
| 03 — Risk Management                |     4      |     3.5      |   3    |  ✅ Exceeds  |
| 04 — Security Policy                |     2      |     3.5      |   3    |  ✅ Exceeds  |
| 05 — Organization of IS             |     7      |     3.1      |   3    |   ✅ Meets   |
| 06 — Compliance                     |     7      |     3.3      |   3    |   ✅ Meets   |
| 07 — Asset Management               |     5      |     3.2      |   3    |   ✅ Meets   |
| 08 — Human Resources                |     6      |     3.0      |   3    |   ✅ Meets   |
| 09 — Physical Security              |     7      |     2.1      |   3    |    🔴 Gap    |
| 10 — Comm & Ops Management          |     10     |     3.4      |   3    |  ✅ Exceeds  |
| 11 — Systems Development            |     2      |     3.5      |   3    |  ✅ Exceeds  |
| 12 — Incident Management            |     5      |     3.6      |   3    |  ✅ Exceeds  |
| 13 — Business Continuity            |     5      |     2.8      |   3    |   🔶 Near    |
| **Overall**                         |   **79**   |   **3.1**    | **3**  | **✅ Meets** |

### Certification Readiness

|     Certification Level      | Requirement                     |     Status      |
| :--------------------------: | ------------------------------- | :-------------: |
|  **HITRUST e1 (Essential)**  | 44 security controls at Level 1 |    ✅ Ready     |
| **HITRUST i1 (Implemented)** | 219 controls at Level 1         |  🔶 85% ready   |
| **HITRUST r2 (Risk-based)**  | Full scope, 2+ year evidence    | 🔴 12-18 months |

---

## 4. Gap Remediation Plan

| Priority | Gap                          | Category | Effort | Owner      | Target Date |
| :------: | ---------------------------- | :------: | :----: | ---------- | :---------: |
|   High   | Physical security assessment |    09    | 5 days | Facilities | 2026-04-01  |
|   High   | BC plan testing (tabletop)   |    13    | 2 days | Security   | 2026-04-15  |
|  Medium  | Removable media policy       |    02    | 1 day  | Security   | 2026-03-15  |
|  Medium  | Clear desk policy            |    01    | 1 day  | HR         | 2026-03-15  |
|  Medium  | Change advisory board        |  05/10   | 2 days | IT         | 2026-03-30  |
|  Medium  | Mobile device policy         |    01    | 1 day  | Security   | 2026-03-15  |
|  Medium  | Acceptable use policy        |    07    | 1 day  | HR         | 2026-03-15  |
|   Low    | Capacity monitoring alerts   |    10    | 2 days | DevOps     | 2026-04-30  |
|   Low    | Formalize lessons learned    |    12    | 1 day  | Security   | 2026-04-15  |
|   Low    | Document all applicable laws |    06    | 1 day  | Legal      | 2026-03-30  |

---

## 5. Evidence Cross-Reference

| HITRUST Domain      | ISO 27001  |     HIPAA      |   SOC 2   | IVF System Evidence                    |
| ------------------- | :--------: | :------------: | :-------: | -------------------------------------- |
| 00 — IS Program     |   A.5.1    | §164.308(a)(1) |   CC1.1   | ISP, ComplianceScoringEngine           |
| 01 — Access Control |  A.5-A.8   | §164.312(a)(d) | CC6.1-6.3 | RBAC, MFA, ConditionalAccessPolicy     |
| 03 — Risk Mgmt      |   A.5.7    | §164.308(a)(1) | CC3.1-3.4 | Risk Assessment, ThreatDetection       |
| 07 — Asset Mgmt     | A.5.9-5.13 |  §164.310(d)   |   CC6.1   | AssetInventory CMDB                    |
| 10 — Operations     |   A.8.x    | §164.308(a)(5) | CC7.1-7.5 | CI/CD, Docker, Backup                  |
| 12 — Incidents      | A.5.24-28  | §164.308(a)(6) | CC7.3-7.5 | SecurityIncident, IncidentResponseRule |
| 13 — BC             | A.5.29-30  | §164.308(a)(7) | A1.1-A1.3 | BCP/DRP                                |

---

## 6. Document Control

| Version |    Date    | Author        | Changes                                 |
| :-----: | :--------: | ------------- | --------------------------------------- |
|   1.0   | 2026-03-03 | Security Team | Initial HITRUST CSF v11 self-assessment |

**Approved by:**

| Role              | Name                 | Date         | Signature  |
| ----------------- | -------------------- | ------------ | ---------- |
| Security Lead     | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO               | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Clinical Director | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
