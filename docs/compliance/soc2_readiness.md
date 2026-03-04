# SOC 2 Type II — Readiness Assessment & Auditor Engagement Plan

**Document ID:** IVF-SOC2-READY-001  
**Version:** 1.0  
**Assessment Date:** 2026-03-03  
**Target Audit Period:** 2026-06-01 to 2026-11-30 (6-month observation)  
**Auditor:** [To be selected]

---

## 1. Executive Summary

This document assesses the IVF Information System's readiness for SOC 2 Type II certification and provides a structured engagement plan for auditor selection and observation period preparation.

**Overall Readiness:** 85% — System demonstrates strong technical controls with comprehensive evidence. Minor gaps in formal process documentation and observation period evidence.

---

## 2. Trust Services Criteria Readiness

### CC1 — Control Environment

| Criteria | Requirement                           | Evidence                                                | Readiness |
| :------: | ------------------------------------- | ------------------------------------------------------- | :-------: |
|  CC1.1   | COSO control environment              | ISP (IVF-ISP-001), organizational chart, security roles | ✅ Ready  |
|  CC1.2   | Board/management oversight            | Compliance dashboard, AI Governance Board               | ✅ Ready  |
|  CC1.3   | Management authority & responsibility | RBAC (9 roles), documented responsibilities             | ✅ Ready  |
|  CC1.4   | Competence & accountability           | ComplianceTraining entity, training tracking            | ✅ Ready  |
|  CC1.5   | Accountability enforcement            | Audit logging, behavioral analytics                     | ✅ Ready  |

### CC2 — Communication and Information

| Criteria | Requirement            | Evidence                                | Readiness |
| :------: | ---------------------- | --------------------------------------- | :-------: |
|  CC2.1   | Informed stakeholders  | Privacy Notice (VN+EN), ISP, breach SOP | ✅ Ready  |
|  CC2.2   | Internal communication | SignalR notifications, audit logs       | ✅ Ready  |
|  CC2.3   | External communication | Breach notification SOP, privacy notice | ✅ Ready  |

### CC3 — Risk Assessment

| Criteria | Requirement              | Evidence                                                | Readiness |
| :------: | ------------------------ | ------------------------------------------------------- | :-------: |
|  CC3.1   | Objectives specification | Risk assessment (IVF-RA-001), DPIA                      | ✅ Ready  |
|  CC3.2   | Risk identification      | Automated threat detection (7 signals), risk assessment | ✅ Ready  |
|  CC3.3   | Fraud risk assessment    | Bot detection, behavioral analytics, z-score analysis   | ✅ Ready  |
|  CC3.4   | Change impact assessment | Change management in CI/CD, vendor risk assessment      | ✅ Ready  |

### CC4 — Monitoring Activities

| Criteria | Requirement              | Evidence                                                    | Readiness |
| :------: | ------------------------ | ----------------------------------------------------------- | :-------: |
|  CC4.1   | Ongoing monitoring       | Real-time SecurityEvent monitoring, ComplianceScoringEngine | ✅ Ready  |
|  CC4.2   | Deficiency communication | IncidentResponseRule automated alerts, breach notification  | ✅ Ready  |

### CC5 — Control Activities

| Criteria | Requirement               | Evidence                                     | Readiness |
| :------: | ------------------------- | -------------------------------------------- | :-------: |
|  CC5.1   | Risk mitigation selection | Multiple control layers (network, app, data) | ✅ Ready  |
|  CC5.2   | Technology controls       | SAST/DAST/SCA pipeline, encryption, MFA      | ✅ Ready  |
|  CC5.3   | Policy-driven controls    | ISP, conditional access policies             | ✅ Ready  |

### CC6 — Logical and Physical Access Controls

| Criteria | Requirement                    | Evidence                                                  | Readiness |
| :------: | ------------------------------ | --------------------------------------------------------- | :-------: |
|  CC6.1   | Access control implementation  | RBAC, MFA, ConditionalAccessPolicy, triple auth           | ✅ Ready  |
|  CC6.2   | Access provisioning            | UserGroup, PermissionDelegation                           | ✅ Ready  |
|  CC6.3   | Access modification/removal    | Session management, deprovisioning                        | ✅ Ready  |
|  CC6.6   | System boundaries              | Docker network segmentation, API gateway, rate limiting   | ✅ Ready  |
|  CC6.7   | Information restriction        | Field-level AES-256-GCM encryption, RBAC                  | ✅ Ready  |
|  CC6.8   | Unauthorized access prevention | Zero Trust, threat detection, automated incident response | ✅ Ready  |

### CC7 — System Operations

| Criteria | Requirement                  | Evidence                                                 | Readiness |
| :------: | ---------------------------- | -------------------------------------------------------- | :-------: |
|  CC7.1   | Infrastructure monitoring    | Automated threat detection, security event monitoring    | ✅ Ready  |
|  CC7.2   | Anomaly detection            | Behavioral analytics (z-score), 7+ threat signals        | ✅ Ready  |
|  CC7.3   | Security incident evaluation | SecurityIncident triage, severity classification         | ✅ Ready  |
|  CC7.4   | Incident response            | IncidentResponseRule automation, breach notification SOP | ✅ Ready  |
|  CC7.5   | Incident recovery            | BCP/DRP, backup strategy, rollback procedures            | ✅ Ready  |

### CC8 — Change Management

| Criteria | Requirement          | Evidence                                      |     Readiness      |
| :------: | -------------------- | --------------------------------------------- | :----------------: |
|  CC8.1   | Change authorization | Git-based workflow, CI/CD with security gates | 🔶 Need formal CAB |

### CC9 — Risk Mitigation

| Criteria | Requirement            | Evidence                                       | Readiness |
| :------: | ---------------------- | ---------------------------------------------- | :-------: |
|  CC9.1   | Vendor risk management | Vendor Risk Assessment Framework (IVF-VRA-001) | ✅ Ready  |
|  CC9.2   | Vendor monitoring      | SCCs, DPA requirements, periodic review        | ✅ Ready  |

### A1 — Availability (if in scope)

| Criteria | Requirement              | Evidence                             |      Readiness      |
| :------: | ------------------------ | ------------------------------------ | :-----------------: |
|   A1.1   | Availability commitment  | BCP with tiered RTO/RPO              |      ✅ Ready       |
|   A1.2   | Availability measurement | Backup verification, health checks   |      ✅ Ready       |
|   A1.3   | Recovery testing         | 3-2-1 backup strategy, DR procedures | 🔶 Need formal test |

### C1 — Confidentiality (if in scope)

| Criteria | Requirement                             | Evidence                                       | Readiness |
| :------: | --------------------------------------- | ---------------------------------------------- | :-------: |
|   C1.1   | Confidential information identification | Data classification (4 levels), AssetInventory | ✅ Ready  |
|   C1.2   | Confidential information disposal       | DataRetentionPolicy entity, crypto-shred       | ✅ Ready  |

### PI1 — Processing Integrity (if in scope)

| Criteria | Requirement                    | Evidence                            | Readiness |
| :------: | ------------------------------ | ----------------------------------- | :-------: |
|  PI1.1   | Processing quality objectives  | FluentValidation, input validation  | ✅ Ready  |
|  PI1.2   | Processing accuracy monitoring | Audit trails, data integrity checks | ✅ Ready  |

---

## 3. Auditor Selection Criteria

### 3.1 Required Qualifications

| Criteria              | Requirement                                                          |  Weight  |
| --------------------- | -------------------------------------------------------------------- | :------: |
| AICPA accreditation   | Licensed CPA firm with SOC 2 attestation capability                  | Required |
| Healthcare experience | Prior SOC 2 audits for healthcare/medtech organizations              |   High   |
| Technical depth       | Understanding of .NET, Docker, PostgreSQL, cloud-native architecture |  Medium  |
| AI/ML familiarity     | Experience auditing AI governance controls                           |  Medium  |
| PHI expertise         | Understanding of HIPAA Technical Safeguards                          |   High   |
| International         | Ability to conduct remote audit if needed                            |  Medium  |

### 3.2 Recommended Auditor Firms

| Firm Category | Examples                    | Estimated Cost  |
| :-----------: | --------------------------- | :-------------: |
|     Big 4     | Deloitte, PwC, EY, KPMG     | $30,000-$50,000 |
|   Mid-tier    | BDO, Grant Thornton, RSM    | $15,000-$30,000 |
|  Specialized  | Coalfire, A-LIGN, Schellman | $15,000-$25,000 |

### 3.3 Engagement Timeline

| Phase |  Timeline   | Activities                               |
| :---: | :---------: | ---------------------------------------- |
|   1   |  Week 1-2   | RFP to 3-5 firms, scope discussion       |
|   2   |   Week 3    | Proposal review, firm selection          |
|   3   |   Week 4    | Engagement letter, kickoff meeting       |
|   4   |  Week 5-6   | Readiness assessment (gap closure)       |
|   5   |  Month 3-8  | Observation period (6 months minimum)    |
|   6   |   Month 9   | Fieldwork, testing, evidence collection  |
|   7   |  Month 10   | Draft report review, management response |
|   8   | Month 10-11 | Final SOC 2 Type II Report issued        |

---

## 4. Observation Period Plan

### 4.1 Evidence Collection Schedule

| Evidence Type        | Collection Method                     |      Frequency       | Responsible |
| -------------------- | ------------------------------------- | :------------------: | ----------- |
| Access reviews       | Export UserGroup + permissions        |      Quarterly       | Admin       |
| Security events      | SecurityEvent summary export          |       Monthly        | Security    |
| Vulnerability scans  | CI/CD pipeline reports                | Per commit + monthly | DevOps      |
| Incident reports     | SecurityIncident + BreachNotification |     As occurred      | Security    |
| Change management    | Git commit logs, PR reviews           |      Per change      | Dev Team    |
| Training completion  | ComplianceTraining records            |      Quarterly       | HR          |
| Backup testing       | Backup restoration test logs          |       Monthly        | DevOps      |
| Policy reviews       | Document version history              |     Semi-annual      | DPO         |
| Vendor assessments   | VRA reports                           |        Annual        | Procurement |
| Compliance dashboard | ComplianceScoringEngine snapshot      |       Monthly        | Admin       |

### 4.2 Continuous Monitoring Automation

Already automated via IVF system:

- [x] Audit logging (partitioned PostgreSQL tables)
- [x] Threat detection (7+ signal categories)
- [x] Behavioral analytics (z-score)
- [x] Security event classification
- [x] Compliance scoring (6 frameworks)
- [x] Asset inventory with risk scoring
- [x] Training completion tracking
- [x] Breach notification lifecycle

---

## 5. Scope Definition

### 5.1 Recommended Trust Service Categories

| Category                      |  Include   | Rationale                          |
| ----------------------------- | :--------: | ---------------------------------- |
| **Security** (CC)             |   ✅ Yes   | Required for all SOC 2             |
| **Availability** (A)          |   ✅ Yes   | Critical for clinical operations   |
| **Confidentiality** (C)       |   ✅ Yes   | PHI handling requires this         |
| **Processing Integrity** (PI) |   ✅ Yes   | Medical data accuracy critical     |
| **Privacy** (P)               | ☐ Optional | Covered by GDPR compliance instead |

### 5.2 System Description Boundaries

| Boundary       | Description                                                       |
| -------------- | ----------------------------------------------------------------- |
| Infrastructure | Docker containers (PostgreSQL, Redis, MinIO, SignServer, EJBCA)   |
| Software       | IVF API (.NET 10), Angular 21 Frontend, SignalR Hubs              |
| People         | 8 clinical roles + Admin, Security Team, DevOps                   |
| Data           | PHI, PII, biometric data, financial data, security logs           |
| Processes      | Patient registration, treatment cycles, billing, document signing |

---

## 6. Document Control

| Version |    Date    | Author          | Changes                            |
| :-----: | :--------: | --------------- | ---------------------------------- |
|   1.0   | 2026-03-03 | Compliance Team | Initial SOC 2 readiness assessment |

**Approved by:**

| Role          | Name                 | Date         | Signature  |
| ------------- | -------------------- | ------------ | ---------- |
| Security Lead | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| CEO/Director  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
