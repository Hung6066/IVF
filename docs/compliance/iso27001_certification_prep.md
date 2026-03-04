# ISO 27001:2022 — Certification Preparation & Audit Plan

**Document ID:** IVF-ISO27001-PREP-001  
**Version:** 1.0  
**Assessment Date:** 2026-03-03  
**Target Stage 1 Audit:** 2026-05-01  
**Target Stage 2 Audit:** 2026-06-15  
**Standard:** ISO/IEC 27001:2022

---

## 1. Executive Summary

This document prepares the IVF Information System for ISO 27001:2022 certification by mapping existing controls to the standard's requirements, identifying evidence for Stage 1 (documentation review) and Stage 2 (implementation audit), and providing a certification body selection guide.

**ISMS Readiness:** 80% — Strong technical controls and documentation. Remaining gaps are primarily in formal process maturity and management review records.

---

## 2. ISMS Mandatory Clauses Readiness (Clauses 4-10)

### Clause 4 — Context of the Organization

| Sub-clause | Requirement                                    | Evidence                                      | Status |
| :--------: | ---------------------------------------------- | --------------------------------------------- | :----: |
|    4.1     | Understanding the organization and its context | Risk assessment (IVF-RA-001), DPIA            |   ✅   |
|    4.2     | Understanding needs of interested parties      | Privacy notice, stakeholder list in ISP       |   ✅   |
|    4.3     | Scope of the ISMS                              | ISP scope statement, system boundaries        |   ✅   |
|    4.4     | ISMS requirements                              | This certification prep + all compliance docs |   ✅   |

### Clause 5 — Leadership

| Sub-clause | Requirement                                         | Evidence                                          | Status |
| :--------: | --------------------------------------------------- | ------------------------------------------------- | :----: |
|    5.1     | Leadership and commitment                           | ISP approval signatures                           |   ✅   |
|    5.2     | Policy                                              | Information Security Policy (IVF-ISP-001)         |   ✅   |
|    5.3     | Organizational roles, responsibilities, authorities | RBAC matrix, DPO appointment, AI Governance Board |   ✅   |

### Clause 6 — Planning

| Sub-clause | Requirement                            | Evidence                                     |    Status    |
| :--------: | -------------------------------------- | -------------------------------------------- | :----------: |
|   6.1.1    | Actions to address risks/opportunities | Risk assessment methodology                  |      ✅      |
|   6.1.2    | IS risk assessment                     | Risk Assessment (IVF-RA-001), threat scoring |      ✅      |
|   6.1.3    | IS risk treatment                      | Risk treatment plan, control implementation  |      ✅      |
|    6.2     | IS objectives and planning             | Compliance roadmap, scoring engine targets   |      ✅      |
|    6.3     | Planning of changes                    | Change management in CI/CD                   | 🔶 Formalize |

### Clause 7 — Support

| Sub-clause | Requirement            | Evidence                                    |   Status    |
| :--------: | ---------------------- | ------------------------------------------- | :---------: |
|    7.1     | Resources              | Team structure, budget allocation           | 🔶 Document |
|    7.2     | Competence             | ComplianceTraining entity, training records |     ✅      |
|    7.3     | Awareness              | Security awareness program, training        |     ✅      |
|    7.4     | Communication          | Internal: SignalR, External: Breach SOP     |     ✅      |
|    7.5     | Documented information | Full compliance document suite (15+ docs)   |     ✅      |

### Clause 8 — Operation

| Sub-clause | Requirement                      | Evidence                                      | Status |
| :--------: | -------------------------------- | --------------------------------------------- | :----: |
|    8.1     | Operational planning and control | Docker Compose, CI/CD, operational procedures |   ✅   |
|    8.2     | IS risk assessment               | Automated risk scoring, periodic assessments  |   ✅   |
|    8.3     | IS risk treatment                | Controls implemented per Annex A mapping      |   ✅   |

### Clause 9 — Performance Evaluation

| Sub-clause | Requirement                                   | Evidence                                           |           Status            |
| :--------: | --------------------------------------------- | -------------------------------------------------- | :-------------------------: |
|    9.1     | Monitoring, measurement, analysis, evaluation | ComplianceScoringEngine, security metrics endpoint |             ✅              |
|    9.2     | Internal audit                                | Internal audit template (IVF-AUDIT-001)            |             ✅              |
|    9.3     | Management review                             |                                                    | 🔴 Need first formal review |

### Clause 10 — Improvement

| Sub-clause | Requirement                         | Evidence                                   | Status |
| :--------: | ----------------------------------- | ------------------------------------------ | :----: |
|    10.1    | Continual improvement               | Compliance scoring trend tracking, roadmap |   ✅   |
|    10.2    | Nonconformity and corrective action | NC tracking in audit template              |   ✅   |

---

## 3. Annex A Controls — Statement of Applicability (SoA) Summary

### Control Coverage

| Category                | Total Controls | Applicable | Implemented |  Gap   |
| ----------------------- | :------------: | :--------: | :---------: | :----: |
| A.5 Organizational (37) |       37       |     34     |     30      |   4    |
| A.6 People (8)          |       8        |     8      |      6      |   2    |
| A.7 Physical (14)       |       14       |     10     |      5      |   5    |
| A.8 Technological (34)  |       34       |     32     |     29      |   3    |
| **Total**               |     **93**     |   **84**   |   **70**    | **14** |

### Implementation Percentage: 83% (70/84 applicable controls)

### Key Gaps for Stage 2

| Control | Description                 | Gap                                     | Remediation                          |
| :-----: | --------------------------- | --------------------------------------- | ------------------------------------ |
| A.5.36  | Compliance with IS policies | No formal compliance monitoring process | Use ComplianceScoringEngine reports  |
|  A.6.1  | Screening                   | Not system-tracked                      | Document background check process    |
|  A.6.6  | Confidentiality agreements  | NDAs not centrally tracked              | Create NDA register                  |
| A.7.1-4 | Physical security           | Not formally assessed                   | Conduct physical security assessment |
|  A.7.8  | Equipment siting            | Server room not documented              | Document server location controls    |
|  A.8.1  | User endpoint devices       | No formal MDM                           | Create endpoint security policy      |
| A.8.11  | Data masking                | Not in non-prod environments            | Implement data masking for test DB   |
| A.8.12  | Data leakage prevention     | No DLP tool                             | Evaluate DLP solutions               |

---

## 4. Certification Body Selection

### 4.1 Selection Criteria

| Criteria              | Requirement                                       |
| --------------------- | ------------------------------------------------- |
| Accreditation         | Accredited by national body (UKAS, ANAB, JAS-ANZ) |
| Healthcare experience | Prior ISO 27001 certs for healthcare/medtech      |
| Language              | English and/or Vietnamese capability              |
| Methodology           | Risk-based approach aligned with ISO 27006        |
| Availability          | Can schedule Stage 1+2 within target timeline     |

### 4.2 Recommended Bodies

|   Category    | Examples                           | Estimated Cost  |
| :-----------: | ---------------------------------- | :-------------: |
| International | BSI, TÜV SÜD, Bureau Veritas, DNV  | $15,000-$30,000 |
|   Regional    | SGS Vietnam, TÜV Rheinland Vietnam | $10,000-$20,000 |

### 4.3 Certification Timeline

|       Phase        | Target Date | Activities                                 |
| :----------------: | :---------: | ------------------------------------------ |
|    CB Selection    | 2026-03-15  | RFQ to 3+ certification bodies             |
|      Contract      | 2026-04-01  | Engagement agreement signed                |
|     Pre-audit      | 2026-04-15  | Optional gap assessment with CB            |
|      Stage 1       | 2026-05-01  | Documentation review (remote, 2 days)      |
|    Gap Closure     | 2026-05-15  | Address Stage 1 findings                   |
|      Stage 2       | 2026-06-15  | Implementation audit (on-site, 3-4 days)   |
| Corrective Actions | 2026-07-15  | Close any non-conformities (90-day window) |
|   Certification    | 2026-08-01  | Certificate issued (3-year validity)       |
|   Surveillance 1   | 2027-08-01  | Annual surveillance audit                  |
|   Surveillance 2   | 2028-08-01  | Annual surveillance audit                  |
|  Recertification   | 2029-08-01  | Full recertification audit                 |

---

## 5. Stage 1 Audit Preparation Checklist

### Documentation Required

|  #  | Document                        | IVF Reference                      | Status |
| :-: | ------------------------------- | ---------------------------------- | :----: |
|  1  | ISMS scope statement            | ISP Section 1                      |   ✅   |
|  2  | Information Security Policy     | IVF-ISP-001                        |   ✅   |
|  3  | Risk assessment methodology     | IVF-RA-001                         |   ✅   |
|  4  | Risk assessment results         | IVF-RA-001 + automated scoring     |   ✅   |
|  5  | Risk treatment plan             | Control mapping in risk assessment |   ✅   |
|  6  | Statement of Applicability      | Section 3 of this document         |   ✅   |
|  7  | IS objectives                   | Compliance roadmap targets         |   ✅   |
|  8  | Competence evidence             | ComplianceTraining records         |   ✅   |
|  9  | Documented operating procedures | Docker setup, API docs, CLAUDE.md  |   ✅   |
| 10  | Incident management procedure   | Breach SOP + automated IR          |   ✅   |
| 11  | BCP/DRP                         | IVF-BCP-001                        |   ✅   |
| 12  | Internal audit procedure        | IVF-AUDIT-001                      |   ✅   |
| 13  | Internal audit results          | Pending (schedule first audit)     |   🔶   |
| 14  | Management review minutes       | Pending (schedule first review)    |   🔴   |
| 15  | Corrective action procedure     | NC tracking in audit template      |   ✅   |

### Stage 1 Readiness: 87% (13/15 items ready)

---

## 6. Stage 2 Audit Preparation

### 6.1 Implementation Evidence to Demonstrate

| Area                     | Evidence Type       | How to Demonstrate                          |
| ------------------------ | ------------------- | ------------------------------------------- |
| Access control           | Live RBAC demo      | Show role creation, assignment, restriction |
| MFA                      | Live authentication | Demonstrate TOTP/Passkey MFA flow           |
| Encryption               | Technical review    | Show AES-256-GCM config, TLS certificates   |
| Audit logging            | Log review          | Query partitioned audit tables              |
| Incident response        | Walkthrough         | Show SecurityEvent → SecurityIncident flow  |
| Backup/recovery          | Test results        | Demonstrate backup restoration              |
| Vulnerability management | CI/CD reports       | Show CodeQL, Trivy, ZAP results             |
| Training                 | Records             | Show ComplianceTraining completion records  |
| Asset management         | CMDB                | Demonstrate AssetInventory with risk scores |
| Monitoring               | Dashboard           | Show compliance dashboard + scoring engine  |
| Change management        | Git history         | Show PR reviews, CI/CD gates                |

### 6.2 Interview Preparation

| Interviewee   | Topics                                              | Preparation                     |
| ------------- | --------------------------------------------------- | ------------------------------- |
| Management    | IS commitment, objectives, resource allocation      | Review ISP, compliance roadmap  |
| DPO           | Privacy, DPIA, data subject rights                  | GDPR compliance evidence        |
| Security Lead | Incident handling, threat detection, access control | Demo security features          |
| Developers    | Secure SDLC, code review, CI/CD                     | Show pipeline, coding standards |
| Operations    | Backup, monitoring, change management               | Demo operational procedures     |

---

## 7. Management Review Agenda Template

### Meeting Minutes — ISMS Management Review

**Date:** [Schedule quarterly]  
**Attendees:** [Management, DPO, Security Lead, Clinical Director]

**Required Inputs (ISO 27001 Clause 9.3):**

|  #  | Input                                   | Source                               |
| :-: | --------------------------------------- | ------------------------------------ |
|  1  | Status of actions from previous reviews | Previous minutes                     |
|  2  | Changes in external/internal issues     | Risk assessment update               |
|  3  | Feedback on IS performance              | Compliance dashboard, scoring engine |
|  4  | Nonconformities and corrective actions  | Audit report, NC tracker             |
|  5  | Monitoring and measurement results      | Security metrics, KPIs               |
|  6  | Audit results                           | Internal audit report                |
|  7  | Fulfillment of IS objectives            | Roadmap progress                     |
|  8  | Interested parties feedback             | Stakeholder feedback                 |
|  9  | Risk assessment/treatment results       | Risk assessment update               |
| 10  | Opportunities for improvement           | Suggestions, trend analysis          |

**Expected Outputs:**

- Decisions on continual improvement
- Changes to IS objectives
- Resource allocation decisions
- Schedule for next review

---

## 8. Document Control

| Version |    Date    | Author          | Changes                                     |
| :-----: | :--------: | --------------- | ------------------------------------------- |
|   1.0   | 2026-03-03 | Compliance Team | Initial ISO 27001 certification preparation |

**Approved by:**

| Role                      | Name                 | Date         | Signature  |
| ------------------------- | -------------------- | ------------ | ---------- |
| Management Representative | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO                       | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
