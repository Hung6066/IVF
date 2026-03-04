# Internal Audit Report — Information Security Management System

**Document ID:** IVF-AUDIT-001  
**Version:** 1.0  
**Audit Date:** [YYYY-MM-DD]  
**Auditor:** [Name / Title]  
**Standards:** ISO 27001:2022, SOC 2 Type II, HIPAA Security Rule, HITRUST CSF v11

---

## 1. Audit Summary

| Field                  | Value                                                                          |
| ---------------------- | ------------------------------------------------------------------------------ |
| **Audit Type**         | ☐ Full ISMS ☐ Surveillance ☐ Focused (specific control)                        |
| **Audit Period**       | [Start Date] to [End Date]                                                     |
| **Scope**              | IVF Information System — all layers (API, Application, Infrastructure, Domain) |
| **Previous Audit**     | [Date or "Initial audit"]                                                      |
| **Overall Assessment** | ☐ Conforming ☐ Minor non-conformities ☐ Major non-conformities                 |

### Audit Results Summary

| Category             | Conforming | Minor NC | Major NC | Observation | N/A |
| -------------------- | :--------: | :------: | :------: | :---------: | :-: |
| ISO 27001 Annex A    |            |          |          |             |     |
| HIPAA Security Rule  |            |          |          |             |     |
| SOC 2 Trust Criteria |            |          |          |             |     |
| HITRUST CSF          |            |          |          |             |     |
| **Total**            |            |          |          |             |     |

---

## 2. Audit Checklist — ISO 27001:2022 Annex A

### A.5 — Organizational Controls

| Control | Description                                   | Evidence Required                    |    Status    | Notes                                             |
| :-----: | --------------------------------------------- | ------------------------------------ | :----------: | ------------------------------------------------- |
|  A.5.1  | Policies for information security             | Information Security Policy document | ☐ C ☐ NC ☐ O | See IVF-ISP-001                                   |
|  A.5.2  | Information security roles & responsibilities | RBAC matrix, role definitions        | ☐ C ☐ NC ☐ O | 9 defined roles                                   |
|  A.5.3  | Segregation of duties                         | Role separation evidence             | ☐ C ☐ NC ☐ O |                                                   |
|  A.5.4  | Management responsibilities                   | Security training records            | ☐ C ☐ NC ☐ O | ComplianceTraining entity                         |
|  A.5.5  | Contact with authorities                      | Breach notification SOP              | ☐ C ☐ NC ☐ O | IVF-BN-SOP-001                                    |
|  A.5.7  | Threat intelligence                           | Threat detection logs, SecurityEvent | ☐ C ☐ NC ☐ O | 7+ signal categories                              |
|  A.5.8  | Information security in project management    | DPIA process, security reviews       | ☐ C ☐ NC ☐ O | IVF-DPIA-001                                      |
|  A.5.9  | Inventory of information assets               | Asset inventory (CMDB)               | ☐ C ☐ NC ☐ O | AssetInventory entity                             |
| A.5.10  | Acceptable use of information                 | Acceptable use policy                | ☐ C ☐ NC ☐ O |                                                   |
| A.5.12  | Classification of information                 | Data classification scheme           | ☐ C ☐ NC ☐ O | 4 levels: public/internal/confidential/restricted |
| A.5.13  | Labelling of information                      | Labelling in CMDB                    | ☐ C ☐ NC ☐ O | AssetInventory.Classification                     |
| A.5.14  | Information transfer                          | SCCs, encryption in transit          | ☐ C ☐ NC ☐ O | IVF-SCC-001                                       |
| A.5.23  | Information security for cloud                | Cloud vendor assessment              | ☐ C ☐ NC ☐ O |                                                   |
| A.5.24  | Incident management planning                  | Incident response procedures         | ☐ C ☐ NC ☐ O | IncidentResponseRule entity                       |
| A.5.25  | Assessment of security events                 | SecurityEvent classification         | ☐ C ☐ NC ☐ O | Automated threat scoring                          |
| A.5.26  | Response to security incidents                | Incident response automation         | ☐ C ☐ NC ☐ O | SecurityIncident entity                           |
| A.5.27  | Learning from security incidents              | Post-incident review process         | ☐ C ☐ NC ☐ O |                                                   |
| A.5.28  | Collection of evidence                        | Audit log preservation               | ☐ C ☐ NC ☐ O | Partitioned PostgreSQL tables                     |
| A.5.29  | Information security during disruption        | BCP/DRP                              | ☐ C ☐ NC ☐ O | IVF-BCP-001                                       |
| A.5.30  | ICT readiness for BC                          | Backup verification, DR testing      | ☐ C ☐ NC ☐ O | 3-2-1 backup strategy                             |
| A.5.31  | Legal, statutory requirements                 | ROPA, privacy notice                 | ☐ C ☐ NC ☐ O | IVF-ROPA-001                                      |
| A.5.34  | Privacy and PII protection                    | Privacy notice, DPIA, ROPA           | ☐ C ☐ NC ☐ O | IVF-PRIV-001                                      |
| A.5.35  | Independent review of IS                      | This audit                           | ☐ C ☐ NC ☐ O |                                                   |
| A.5.36  | Compliance with policies                      | Policy compliance monitoring         | ☐ C ☐ NC ☐ O | ConditionalAccessPolicy entity                    |

### A.6 — People Controls

| Control | Description                    | Evidence Required              |    Status    | Notes                                       |
| :-----: | ------------------------------ | ------------------------------ | :----------: | ------------------------------------------- |
|  A.6.1  | Screening                      | Background check procedures    | ☐ C ☐ NC ☐ O |                                             |
|  A.6.2  | Terms of employment            | Security clauses in contracts  | ☐ C ☐ NC ☐ O |                                             |
|  A.6.3  | Information security awareness | Training completion records    | ☐ C ☐ NC ☐ O | ComplianceTraining entity tracks completion |
|  A.6.4  | Disciplinary process           | Documented process             | ☐ C ☐ NC ☐ O |                                             |
|  A.6.5  | After termination/change       | Account deprovisioning process | ☐ C ☐ NC ☐ O |                                             |
|  A.6.6  | Confidentiality agreements     | NDAs on file                   | ☐ C ☐ NC ☐ O |                                             |
|  A.6.7  | Remote working                 | Remote access policy, VPN      | ☐ C ☐ NC ☐ O | ConditionalAccessPolicy                     |

### A.7 — Physical Controls

| Control | Description                  | Evidence Required            |    Status    | Notes                      |
| :-----: | ---------------------------- | ---------------------------- | :----------: | -------------------------- |
|  A.7.1  | Physical security perimeters | Facility security assessment | ☐ C ☐ NC ☐ O |                            |
|  A.7.2  | Physical entry               | Access control logs          | ☐ C ☐ NC ☐ O |                            |
|  A.7.3  | Securing offices             | Clean desk policy            | ☐ C ☐ NC ☐ O |                            |
|  A.7.4  | Physical security monitoring | CCTV, alarms                 | ☐ C ☐ NC ☐ O |                            |
|  A.7.8  | Equipment siting             | Server room controls         | ☐ C ☐ NC ☐ O |                            |
| A.7.10  | Storage media                | Media handling procedures    | ☐ C ☐ NC ☐ O |                            |
| A.7.14  | Secure disposal              | Media destruction procedures | ☐ C ☐ NC ☐ O | DataRetentionPolicy entity |

### A.8 — Technological Controls

| Control | Description                             | Evidence Required                    |    Status    | Notes                                           |
| :-----: | --------------------------------------- | ------------------------------------ | :----------: | ----------------------------------------------- |
|  A.8.1  | User endpoint devices                   | Endpoint security policy             | ☐ C ☐ NC ☐ O |                                                 |
|  A.8.2  | Privileged access rights                | Admin role audit, MFA for admins     | ☐ C ☐ NC ☐ O | AdminOnly policy, MFA required                  |
|  A.8.3  | Information access restriction          | RBAC implementation review           | ☐ C ☐ NC ☐ O | 9 roles, 50+ permissions                        |
|  A.8.4  | Access to source code                   | Repository access controls           | ☐ C ☐ NC ☐ O |                                                 |
|  A.8.5  | Secure authentication                   | MFA, password policy, JWT            | ☐ C ☐ NC ☐ O | TOTP/SMS/Passkey/WebAuthn/Biometric             |
|  A.8.6  | Capacity management                     | Resource monitoring                  | ☐ C ☐ NC ☐ O |                                                 |
|  A.8.7  | Protection against malware              | Scanning pipeline results            | ☐ C ☐ NC ☐ O | CI/CD security pipeline                         |
|  A.8.8  | Management of technical vulnerabilities | Vulnerability scan reports, patching | ☐ C ☐ NC ☐ O | Trivy SCA, CodeQL SAST                          |
|  A.8.9  | Configuration management                | Baseline configurations              | ☐ C ☐ NC ☐ O | Docker configs, appsettings                     |
| A.8.10  | Information deletion                    | Data retention automation            | ☐ C ☐ NC ☐ O | DataRetentionPolicy entity                      |
| A.8.11  | Data masking                            | PHI masking in non-prod              | ☐ C ☐ NC ☐ O |                                                 |
| A.8.12  | Data leakage prevention                 | DLP controls                         | ☐ C ☐ NC ☐ O |                                                 |
| A.8.15  | Logging                                 | Audit log review                     | ☐ C ☐ NC ☐ O | Partitioned audit tables, comprehensive logging |
| A.8.16  | Monitoring activities                   | Security monitoring review           | ☐ C ☐ NC ☐ O | Real-time threat detection                      |
| A.8.20  | Networks security                       | Network segmentation review          | ☐ C ☐ NC ☐ O | Docker: ivf-public, ivf-signing, ivf-data       |
| A.8.21  | Security of network services            | TLS configuration                    | ☐ C ☐ NC ☐ O | TLS 1.2+                                        |
| A.8.22  | Segregation of networks                 | Network isolation verification       | ☐ C ☐ NC ☐ O | 3 Docker networks                               |
| A.8.24  | Use of cryptography                     | Encryption inventory                 | ☐ C ☐ NC ☐ O | AES-256-GCM, RS256 3072-bit, TLS 1.2+           |
| A.8.25  | SDLC                                    | Secure SDLC evidence                 | ☐ C ☐ NC ☐ O | CI/CD security pipeline                         |
| A.8.26  | Application security requirements       | Security requirements docs           | ☐ C ☐ NC ☐ O | CLAUDE.md, security docs                        |
| A.8.28  | Secure coding                           | Code review process, SAST            | ☐ C ☐ NC ☐ O | CodeQL in CI/CD                                 |

---

## 3. Audit Checklist — HIPAA Security Rule (Selected Controls)

|   Reference    | Requirement                      | Evidence                          |    Status    | Notes                     |
| :------------: | -------------------------------- | --------------------------------- | :----------: | ------------------------- |
| §164.308(a)(1) | Security Management Process      | Risk assessment, risk management  | ☐ C ☐ NC ☐ O | IVF-RA-001                |
| §164.308(a)(2) | Assigned Security Responsibility | Security officer designation      | ☐ C ☐ NC ☐ O |                           |
| §164.308(a)(3) | Workforce Security               | Access authorization, termination | ☐ C ☐ NC ☐ O | RBAC                      |
| §164.308(a)(4) | Information Access Management    | Access controls, RBAC             | ☐ C ☐ NC ☐ O | 9 roles                   |
| §164.308(a)(5) | Security Awareness & Training    | Training program, records         | ☐ C ☐ NC ☐ O | ComplianceTraining        |
| §164.308(a)(6) | Security Incident Procedures     | Incident response plan            | ☐ C ☐ NC ☐ O | Automated IR              |
| §164.308(a)(7) | Contingency Plan                 | BCP/DRP, backup procedures        | ☐ C ☐ NC ☐ O | IVF-BCP-001               |
| §164.308(a)(8) | Evaluation                       | This audit, penetration testing   | ☐ C ☐ NC ☐ O | IVF-PENTEST-001           |
|  §164.310(a)   | Facility Access Controls         | Physical security                 | ☐ C ☐ NC ☐ O |                           |
|  §164.310(b)   | Workstation Use                  | Workstation security policy       | ☐ C ☐ NC ☐ O |                           |
|  §164.310(d)   | Device and Media Controls        | Media handling, disposal          | ☐ C ☐ NC ☐ O |                           |
|  §164.312(a)   | Access Control                   | Unique user ID, MFA, encryption   | ☐ C ☐ NC ☐ O | JWT + MFA                 |
|  §164.312(b)   | Audit Controls                   | Audit logging mechanism           | ☐ C ☐ NC ☐ O | Partitioned tables        |
|  §164.312(c)   | Integrity                        | Data integrity controls           | ☐ C ☐ NC ☐ O |                           |
|  §164.312(d)   | Authentication                   | Person/entity authentication      | ☐ C ☐ NC ☐ O | Triple auth pipeline      |
|  §164.312(e)   | Transmission Security            | Encryption in transit             | ☐ C ☐ NC ☐ O | TLS 1.2+                  |
|  §164.314(a)   | Business Associate Contracts     | BAAs on file                      | ☐ C ☐ NC ☐ O |                           |
|  §164.316(a)   | Policies and Procedures          | Written policies                  | ☐ C ☐ NC ☐ O | ISP, SOP docs             |
|  §164.316(b)   | Documentation Requirements       | 6-year retention of policies      | ☐ C ☐ NC ☐ O |                           |
|  §164.404-408  | Breach Notification              | Breach notification procedures    | ☐ C ☐ NC ☐ O | BreachNotification entity |

---

## 4. Non-Conformity Report Template

### NC-[ID]: [Title]

| Field                 | Value                                    |
| --------------------- | ---------------------------------------- |
| **Severity**          | ☐ Major ☐ Minor ☐ Observation            |
| **Control Reference** | [ISO/HIPAA/SOC2/HITRUST reference]       |
| **Description**       | [What was found]                         |
| **Evidence**          | [What was reviewed/tested]               |
| **Root Cause**        | [Why the non-conformity exists]          |
| **Corrective Action** | [What needs to be done]                  |
| **Owner**             | [Responsible person]                     |
| **Due Date**          | [Remediation deadline]                   |
| **Status**            | ☐ Open ☐ In Progress ☐ Closed ☐ Verified |
| **Verification Date** | [Date NC was verified as closed]         |

**Corrective Action SLA:**

|  Severity   |     Deadline     |
| :---------: | :--------------: |
|    Major    |     30 days      |
|    Minor    |     90 days      |
| Observation | Next audit cycle |

---

## 5. Audit Evidence Inventory

| Evidence ID | Description                  | Source                                               | Reviewed |
| :---------: | ---------------------------- | ---------------------------------------------------- | :------: |
|    E-001    | Information Security Policy  | docs/compliance/information_security_policy.md       |    ☐     |
|    E-002    | Risk Assessment Report       | docs/compliance/risk_assessment.md                   |    ☐     |
|    E-003    | DPIA                         | docs/compliance/dpia.md                              |    ☐     |
|    E-004    | BCP/DRP                      | docs/compliance/bcp_drp.md                           |    ☐     |
|    E-005    | Breach Notification SOP      | docs/compliance/breach_notification_sop.md           |    ☐     |
|    E-006    | Vendor Risk Assessment       | docs/compliance/vendor_risk_assessment.md            |    ☐     |
|    E-007    | AI Governance Charter        | docs/compliance/ai_governance_charter.md             |    ☐     |
|    E-008    | Privacy Notice               | docs/compliance/privacy_notice.md                    |    ☐     |
|    E-009    | ROPA Register                | docs/compliance/ropa_register.md                     |    ☐     |
|    E-010    | AI Lifecycle Documentation   | docs/compliance/ai_lifecycle_documentation.md        |    ☐     |
|    E-011    | SCCs Implementation Guide    | docs/compliance/standard_contractual_clauses.md      |    ☐     |
|    E-012    | Penetration Test Report      | docs/compliance/penetration_test_report_template.md  |    ☐     |
|    E-013    | Security Scan CI/CD Pipeline | .github/workflows/security-scan.yml                  |    ☐     |
|    E-014    | RBAC Configuration           | src/IVF.Domain/Enums/RoleType.cs + Permission seeder |    ☐     |
|    E-015    | Encryption Implementation    | AES-256-GCM field encryption in Infrastructure       |    ☐     |
|    E-016    | Audit Logging                | Partitioned PostgreSQL audit tables                  |    ☐     |
|    E-017    | Asset Inventory System       | AssetInventory entity + endpoints                    |    ☐     |
|    E-018    | Incident Response System     | SecurityIncident + IncidentResponseRule entities     |    ☐     |
|    E-019    | Training Records             | ComplianceTraining entity                            |    ☐     |
|    E-020    | Compliance Scoring Dashboard | ComplianceEndpoints dashboard                        |    ☐     |

---

## 6. Audit Conclusions

### 6.1 Strengths Identified

1. [List areas of strong compliance]
2. [...]

### 6.2 Areas for Improvement

1. [List improvement recommendations]
2. [...]

### 6.3 Overall Assessment

[Summary paragraph: The IVF Information System ISMS is assessed as [conforming / conforming with minor non-conformities / non-conforming]. The system demonstrates [strengths]. The following areas require attention: [weaknesses].]

---

## 7. Audit Sign-Off

| Role                      | Name                 | Date         | Signature  |
| ------------------------- | -------------------- | ------------ | ---------- |
| Lead Auditor              | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Auditee Representative    | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Management Representative | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

**Next Internal Audit Due:** [Date — recommended every 6 months for initial ISMS, annually thereafter]

---

## 8. Document Control

| Version |    Date    | Author              | Changes                                     |
| :-----: | :--------: | ------------------- | ------------------------------------------- |
|   1.0   | 2026-03-03 | Internal Audit Team | Initial audit checklist and report template |

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
