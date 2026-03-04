# GDPR Readiness Assessment

**Document ID:** IVF-GDPR-READY-001  
**Version:** 1.0  
**Assessment Date:** 2026-03-03  
**Data Controller:** [Clinic Name]  
**DPO Contact:** dpo@[clinic-domain].com  
**Assessment Type:** Internal readiness assessment (pre-external audit)

---

## 1. Executive Summary

This assessment evaluates the IVF Information System's readiness for GDPR compliance, covering all applicable Articles and key recitals. The system processes special category data (health, biometric, reproductive) under GDPR Article 9, requiring enhanced protection measures.

**Overall GDPR Readiness: 82%**

| Chapter                   |  Articles  | Applicable | Compliant | Partial |  Gap  |
| ------------------------- | :--------: | :--------: | :-------: | :-----: | :---: |
| II — Principles           | Art. 5-11  |     7      |     6     |    1    |   0   |
| III — Data Subject Rights | Art. 12-23 |     10     |     7     |    3    |   0   |
| IV — Controller/Processor | Art. 24-43 |     12     |    10     |    2    |   0   |
| V — Transfers             | Art. 44-49 |     3      |     3     |    0    |   0   |
| VI — Supervisory Auth     | Art. 51-59 |     1      |     1     |    0    |   0   |
| IX — Special Processing   | Art. 85-91 |     1      |     0     |    1    |   0   |
| **Total**                 |            |   **34**   |  **27**   |  **7**  | **0** |

---

## 2. Detailed Assessment

### Chapter II — Principles (Art. 5-11)

|     Article      | Requirement                             | Evidence                                                                               | Status | Notes                      |
| :--------------: | --------------------------------------- | -------------------------------------------------------------------------------------- | :----: | -------------------------- |
| **Art. 5(1)(a)** | Lawfulness, fairness, transparency      | Privacy Notice (VN+EN), ROPA with legal basis                                          |   ✅   |                            |
| **Art. 5(1)(b)** | Purpose limitation                      | ROPA maps purposes per activity, ProcessingActivity entity                             |   ✅   |                            |
| **Art. 5(1)(c)** | Data minimization                       | Minimal data collection per ROPA, field-level access                                   |   ✅   |                            |
| **Art. 5(1)(d)** | Accuracy                                | User-editable profiles, rectification capability                                       |   ✅   |                            |
| **Art. 5(1)(e)** | Storage limitation                      | DataRetentionPolicy entity, defined retention periods                                  |   ✅   |                            |
| **Art. 5(1)(f)** | Integrity and confidentiality           | AES-256-GCM, TLS 1.2+, RBAC, MFA, audit logs                                           |   ✅   |                            |
|  **Art. 5(2)**   | Accountability                          | Comprehensive documentation suite, audit trails                                        |   ✅   |                            |
|    **Art. 6**    | Lawful basis for processing             | Legal basis documented per activity in ROPA                                            |   ✅   |                            |
|    **Art. 7**    | Conditions for consent                  | UserConsent entity, granular consent tracking                                          |   ✅   |                            |
|    **Art. 8**    | Child consent                           | Not applicable (IVF patients are adults)                                               |  N/A   |                            |
|    **Art. 9**    | Special categories                      | Explicit consent for biometrics, healthcare exemption for medical data, DPIA completed |   ✅   |                            |
|   **Art. 10**    | Criminal convictions                    | Not processed                                                                          |  N/A   |                            |
|   **Art. 11**    | Processing not requiring identification | De-identification capability via encryption                                            |   🔶   | Formalize pseudonymization |

### Chapter III — Data Subject Rights (Art. 12-23)

|   Article   | Right                                      | Implementation                                                             | Status | Notes                                       |
| :---------: | ------------------------------------------ | -------------------------------------------------------------------------- | :----: | ------------------------------------------- |
| **Art. 12** | Transparent communication                  | Privacy Notice (VN+EN), layered notices                                    |   🔶   | Add cookie consent ✅ (Phase 3)             |
| **Art. 13** | Information at collection                  | Privacy Notice covers all required fields                                  |   ✅   |                                             |
| **Art. 14** | Information not obtained from data subject | Privacy Notice covers                                                      |   ✅   |                                             |
| **Art. 15** | Right of access                            | DPO contact in privacy notice, can export patient data                     |   ✅   |                                             |
| **Art. 16** | Right to rectification                     | Patients can request via DPO, staff can update records                     |   ✅   |                                             |
| **Art. 17** | Right to erasure                           | Soft delete (IsDeleted), DPO process, DataRetentionPolicy                  |   🔶   | Add hard delete option for non-medical data |
| **Art. 18** | Right to restriction                       | Can restrict processing via DPO                                            |   🔶   | Add restriction flag to Patient entity      |
| **Art. 20** | Right to data portability                  | Can export to JSON/PDF format                                              |   ✅   |                                             |
| **Art. 21** | Right to object                            | DPO process for legitimate interest objection                              |   ✅   |                                             |
| **Art. 22** | Automated decision-making                  | AI explainability endpoints, human override available, transparency report |   ✅   |                                             |

### Chapter IV — Controller Obligations (Art. 24-43)

|   Article   | Requirement                          | Evidence                                                                | Status | Notes                    |
| :---------: | ------------------------------------ | ----------------------------------------------------------------------- | :----: | ------------------------ |
| **Art. 24** | Controller responsibility            | Comprehensive security controls, documentation                          |   ✅   |                          |
| **Art. 25** | Data protection by design & default  | Field-level encryption, minimization, RBAC                              |   ✅   |                          |
| **Art. 28** | Processor obligations                | DPA template in SCCs, vendor risk assessment                            |   ✅   |                          |
| **Art. 30** | Records of processing                | ROPA Register (IVF-ROPA-001), ProcessingActivity entity                 |   ✅   |                          |
| **Art. 32** | Security of processing               | AES-256-GCM, MFA, audit logs, pseudonymization capability               |   ✅   |                          |
| **Art. 33** | Breach notification to authority     | BreachNotification entity, SOP with 72-hour timeline                    |   ✅   |                          |
| **Art. 34** | Breach communication to data subject | Breach SOP covers individual notification                               |   ✅   |                          |
| **Art. 35** | DPIA                                 | DPIA completed (IVF-DPIA-001) for biometric + medical data              |   ✅   |                          |
| **Art. 36** | Prior consultation                   | DPIA did not identify high residual risk — no prior consultation needed |   ✅   |                          |
| **Art. 37** | DPO designation                      | DPO role defined, contact in privacy notice                             |   ✅   |                          |
| **Art. 38** | DPO position                         | DPO independence documented                                             |   🔶   | Formalize reporting line |
| **Art. 39** | DPO tasks                            | DPO responsibilities in ISP and privacy notice                          |   ✅   |                          |

### Chapter V — International Transfers (Art. 44-49)

|   Article   | Requirement                    | Evidence                                  | Status |
| :---------: | ------------------------------ | ----------------------------------------- | :----: |
| **Art. 44** | General principle on transfers | SCCs framework (IVF-SCC-001)              |   ✅   |
| **Art. 46** | Appropriate safeguards         | SCCs Module 2 template, TIA checklist     |   ✅   |
| **Art. 49** | Derogations                    | Documented in SCCs for specific scenarios |   ✅   |

---

## 3. Technical Measures Assessment

### 3.1 Data Protection by Design (Art. 25)

| Measure                   | Implementation                                       |  Assessment  |
| ------------------------- | ---------------------------------------------------- | :----------: |
| **Encryption at Rest**    | AES-256-GCM field-level for all PHI/PII              | ✅ Excellent |
| **Encryption in Transit** | TLS 1.2+ for all API traffic, mTLS for signing       | ✅ Excellent |
| **Pseudonymization**      | Guid-based IDs, no natural keys exposed              |   ✅ Good    |
| **Data Minimization**     | ROPA defines minimal data per purpose                |   ✅ Good    |
| **Access Control**        | RBAC with 9 roles, 50+ permissions, MFA              | ✅ Excellent |
| **Audit Trail**           | Partitioned PostgreSQL, comprehensive logging        | ✅ Excellent |
| **Consent Management**    | UserConsent entity, granular tracking                |   ✅ Good    |
| **Breach Detection**      | Automated threat detection, incident response        | ✅ Excellent |
| **Data Portability**      | JSON/PDF export capability                           |   ✅ Good    |
| **Right to Erasure**      | Soft delete implemented, hard delete for non-medical |  🔶 Partial  |

### 3.2 Cookie Compliance (ePrivacy Directive)

| Requirement           | Implementation                                                    |          Status           |
| --------------------- | ----------------------------------------------------------------- | :-----------------------: |
| Cookie consent banner | CookieConsentComponent (Angular)                                  | ✅ Implemented in Phase 3 |
| Granular consent      | Essential (required) + Security (required) + Analytics (optional) |            ✅             |
| Consent storage       | localStorage with version tracking                                |            ✅             |
| Consent withdrawal    | Re-accessible via settings                                        |            ✅             |
| No pre-ticked boxes   | Analytics unchecked by default                                    |            ✅             |

---

## 4. GDPR Compliance Gaps & Remediation

|  #  | Gap                              | Article |  Risk  | Remediation                                        |   Target   |
| :-: | -------------------------------- | :-----: | :----: | -------------------------------------------------- | :--------: |
|  1  | Hard delete for non-medical data | Art. 17 | Medium | Implement permanent erasure for non-regulated data | 2026-04-15 |
|  2  | Processing restriction flag      | Art. 18 |  Low   | Add IsRestricted flag to Patient entity            | 2026-04-01 |
|  3  | Formalize pseudonymization       | Art. 11 |  Low   | Document pseudonymization procedures               | 2026-03-30 |
|  4  | DPO reporting line formalization | Art. 38 |  Low   | Document DPO independence and reporting            | 2026-03-15 |
|  5  | Data subject request tracking    | Art. 12 | Medium | Add DSR tracking entity/endpoint                   | 2026-04-30 |

---

## 5. External Assessment Preparation

### 5.1 Auditor Requirements

| Criteria              | Requirement                                         |
| --------------------- | --------------------------------------------------- |
| Qualification         | GDPR certification (CIPP/E, CIPM, or equivalent)    |
| Healthcare experience | Familiarity with Art. 9 special category processing |
| Language              | Vietnamese and/or English                           |
| Methodology           | Structured assessment against all GDPR articles     |

### 5.2 Evidence Package for External Auditor

|  #  | Document                            | Reference                          |
| :-: | ----------------------------------- | ---------------------------------- |
|  1  | Privacy Notice (bilingual)          | IVF-PRIV-001                       |
|  2  | ROPA Register                       | IVF-ROPA-001                       |
|  3  | DPIA                                | IVF-DPIA-001                       |
|  4  | Breach Notification SOP             | IVF-BN-SOP-001                     |
|  5  | SCCs Implementation                 | IVF-SCC-001                        |
|  6  | Information Security Policy         | IVF-ISP-001                        |
|  7  | Risk Assessment                     | IVF-RA-001                         |
|  8  | AI Governance (automated decisions) | IVF-AI-GOV-001 + AI lifecycle docs |
|  9  | Cookie Consent Implementation       | Angular CookieConsentComponent     |
| 10  | Technical Security Architecture     | CLAUDE.md + security docs          |
| 11  | Vendor Risk Assessment              | IVF-VRA-001                        |
| 12  | Training Records                    | ComplianceTraining entity data     |
| 13  | Consent Management                  | UserConsent entity data            |
| 14  | Audit Logs                          | Partitioned audit table samples    |

---

## 6. Document Control

| Version |    Date    | Author | Changes                           |
| :-----: | :--------: | ------ | --------------------------------- |
|   1.0   | 2026-03-03 | DPO    | Initial GDPR readiness assessment |

**Approved by:**

| Role              | Name                 | Date         | Signature  |
| ----------------- | -------------------- | ------------ | ---------- |
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
