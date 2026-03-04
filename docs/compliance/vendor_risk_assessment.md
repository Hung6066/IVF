# Vendor Risk Assessment Framework

**Document ID:** IVF-VRA-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** Security Officer / DPO  
**Review Cycle:** Annual  
**Frameworks:** SOC 2 CC9.2, ISO 27001 A.5.19-A.5.23, HIPAA §164.314, HITRUST CSF 09.0

---

## 1. Purpose

This framework establishes the process for assessing, monitoring, and managing risks associated with third-party vendors and service providers that process, store, or transmit data within the IVF Information System.

## 2. Scope

All third-party vendors, service providers, and open-source components that interact with or process IVF system data.

## 3. Current Vendor Inventory

### 3.1 Infrastructure Vendors

|  #  | Vendor         | Service                        |       Data Exposure       | Criticality | Last Assessment |
| :-: | -------------- | ------------------------------ | :-----------------------: | :---------: | :-------------: |
|  1  | **PostgreSQL** | Primary database               |         PHI, PII          |  Critical   |   ****\_****    |
|  2  | **MinIO**      | Object storage (S3-compatible) |     Documents, images     |  Critical   |   ****\_****    |
|  3  | **Redis**      | Caching layer                  | Session data, temp tokens |    High     |   ****\_****    |
|  4  | **Docker**     | Container runtime              |       All services        |  Critical   |   ****\_****    |
|  5  | **EJBCA**      | PKI / Certificate Authority    |   Digital certificates    |    High     |   ****\_****    |
|  6  | **SignServer** | Digital signing service        |      Document hashes      |    High     |   ****\_****    |

### 3.2 Software Vendors

|  #  | Vendor                  | Component          |         Data Exposure          | Criticality | Last Assessment |
| :-: | ----------------------- | ------------------ | :----------------------------: | :---------: | :-------------: |
|  7  | **DigitalPersona**      | Biometric SDK      |     Fingerprint templates      |  Critical   |   ****\_****    |
|  8  | **QuestPDF**            | PDF generation     |      Report content (PHI)      |   Medium    |   ****\_****    |
|  9  | **Google reCAPTCHA**    | Bot detection      | IP addresses, browser metadata |     Low     |   ****\_****    |
| 10  | **IdentityModel**       | JWT/OIDC libraries |          Auth tokens           |    High     |   ****\_****    |
| 11  | **StackExchange.Redis** | Redis client       |     Connection credentials     |   Medium    |   ****\_****    |
| 12  | **AWSSDK.S3**           | MinIO client       |      Storage credentials       |   Medium    |   ****\_****    |
| 13  | **Serilog**             | Logging            |           Audit data           |   Medium    |   ****\_****    |
| 14  | **MediatR**             | CQRS mediator      |       None (in-process)        |     Low     |   ****\_****    |
| 15  | **FluentValidation**    | Input validation   |           User input           |     Low     |   ****\_****    |
| 16  | **BCrypt.Net**          | Password hashing   |        Password hashes         |    High     |   ****\_****    |
| 17  | **Fido2Net**            | WebAuthn/Passkeys  |        Auth credentials        |    High     |   ****\_****    |

## 4. Risk Assessment Process

### 4.1 Assessment Workflow

```
Vendor Identification → Risk Classification → Due Diligence →
  Assessment → Remediation → Approval → Monitoring → Reassessment
```

### 4.2 Risk Classification

|    Class     | Criteria                                         |       Assessment Level        | Reassessment Frequency |
| :----------: | ------------------------------------------------ | :---------------------------: | :--------------------: |
| **Critical** | Directly accesses/processes PHI/PII              |        Full assessment        |     Semi-annually      |
|   **High**   | Accesses credentials or security controls        |      Standard assessment      |        Annually        |
|  **Medium**  | Processes non-sensitive data or provides tooling |    Streamlined assessment     |        Annually        |
|   **Low**    | No data access, development-only tools           | Self-assessment questionnaire |       Biennially       |

### 4.3 Assessment Criteria

#### 4.3.1 Security Controls (40% weight)

|  #  | Control                               | Max Score | Evidence Required      |
| :-: | ------------------------------------- | :-------: | ---------------------- |
| S1  | Encryption at rest (AES-256)          |    10     | Configuration docs     |
| S2  | Encryption in transit (TLS 1.2+)      |    10     | TLS testing results    |
| S3  | Access control (RBAC/least privilege) |    10     | Access control policy  |
| S4  | Vulnerability management program      |    10     | Scan reports, SLAs     |
| S5  | Incident response plan                |    10     | IR plan document       |
| S6  | Security certifications               |    10     | SOC 2, ISO 27001, etc. |
| S7  | Secure SDLC practices                 |    10     | SAST/DAST evidence     |
| S8  | Penetration testing                   |    10     | Pentest reports        |
| S9  | Data backup & recovery                |    10     | Backup policy, RTO/RPO |
| S10 | Network segmentation                  |    10     | Architecture diagrams  |

#### 4.3.2 Privacy & Compliance (30% weight)

|  #  | Control                         | Max Score | Evidence Required     |
| :-: | ------------------------------- | :-------: | --------------------- |
| P1  | Data Processing Agreement (DPA) |    10     | Signed DPA            |
| P2  | HIPAA BAA (if PHI access)       |    10     | Signed BAA            |
| P3  | GDPR compliance (if EU data)    |    10     | GDPR assessment       |
| P4  | Data retention & deletion       |    10     | Retention policy      |
| P5  | Data breach notification SLA    |    10     | Contract terms        |
| P6  | Data residency controls         |    10     | Hosting documentation |
| P7  | Privacy impact assessment       |    10     | PIA/DPIA              |

#### 4.3.3 Operational Resilience (20% weight)

|  #  | Control                   | Max Score | Evidence Required |
| :-: | ------------------------- | :-------: | ----------------- |
| O1  | SLA (uptime, response)    |    10     | Contract SLAs     |
| O2  | Business continuity plan  |    10     | BCP document      |
| O3  | Disaster recovery plan    |    10     | DRP with RTO/RPO  |
| O4  | Scalability/capacity      |    10     | Architecture docs |
| O5  | Change management process |    10     | Change policy     |

#### 4.3.4 Financial & Legal (10% weight)

|  #  | Control                          | Max Score | Evidence Required      |
| :-: | -------------------------------- | :-------: | ---------------------- |
| F1  | Financial stability              |    10     | Financial statements   |
| F2  | Insurance (cyber, liability)     |    10     | Insurance certificates |
| F3  | Contract terms (termination, IP) |    10     | Legal review           |
| F4  | Regulatory compliance            |    10     | Compliance attestation |

### 4.4 Scoring & Approval

| Score Range | Risk Level | Action                                              |
| :---------: | :--------: | --------------------------------------------------- |
|   90–100%   |    Low     | Approve, annual reassessment                        |
|   75–89%    |   Medium   | Approve with conditions, remediation plan           |
|   60–74%    |    High    | Approve with compensating controls + 6-month review |
|    <60%     |  Critical  | Reject / require full remediation before use        |

## 5. Open-Source Component Assessment

### 5.1 Automated Checks

| Check                       | Tool                               |  Frequency  |         Threshold          |
| --------------------------- | ---------------------------------- | :---------: | :------------------------: |
| Known vulnerabilities (CVE) | `dotnet list package --vulnerable` | Weekly (CI) |     Zero critical/high     |
| License compliance          | `dotnet-project-licenses`          | Per release | No copyleft for commercial |
| Outdated packages           | `dotnet list package --outdated`   |   Monthly   |  <2 major versions behind  |
| npm vulnerabilities         | `npm audit`                        | Weekly (CI) |     Zero critical/high     |
| Container vulnerabilities   | Trivy                              |  Per build  |       Zero critical        |
| Secret scanning             | Gitleaks                           | Per commit  |       Zero findings        |

### 5.2 Open-Source Risk Criteria

| Factor          | Low Risk                             | Medium Risk                           | High Risk                     |
| --------------- | ------------------------------------ | ------------------------------------- | ----------------------------- |
| **Maintenance** | Active development, >10 contributors | Occasional updates, 3-10 contributors | Stale (>1yr), <3 contributors |
| **Community**   | >10k stars, foundation-backed        | 1k-10k stars                          | <1k stars                     |
| **License**     | MIT, Apache 2.0                      | BSD, MPL 2.0                          | GPL, AGPL, SSPL               |
| **CVE History** | 0 critical in 2 years                | 1-2 critical, patched quickly         | >2 critical or slow patches   |
| **Data Access** | None                                 | Indirect                              | Direct PHI/PII access         |

## 6. Business Associate Agreements (BAA)

### 6.1 BAA Requirements (HIPAA §164.314)

Required for all vendors classified as **Business Associates** — those who create, receive, maintain, or transmit PHI.

#### Required BAA Clauses

1. Permitted uses and disclosures of PHI
2. Obligations to safeguard PHI (administrative, physical, technical)
3. Obligation to report breaches of unsecured PHI
4. Subcontractor flow-down requirements
5. Return or destruction of PHI upon termination
6. Compliance with HIPAA Security Rule
7. Access to records for HHS compliance audits
8. Breach notification timeline (≤30 days after discovery)

### 6.2 BAA Status Tracker

| Vendor           |  BAA Required?   | BAA Signed? | Expiration | Notes                     |
| ---------------- | :--------------: | :---------: | :--------: | ------------------------- |
| PostgreSQL       | No (self-hosted) |     N/A     |    N/A     | Infrastructure control    |
| MinIO            | No (self-hosted) |     N/A     |    N/A     | Infrastructure control    |
| DigitalPersona   |       Yes        | ****\_****  | ****\_**** | Biometric data processing |
| Google reCAPTCHA |        No        |     N/A     |    N/A     | No PHI exposure           |

## 7. Monitoring & Ongoing Assurance

### 7.1 Continuous Monitoring

| Activity                           |          Frequency          | Responsible      |
| ---------------------------------- | :-------------------------: | ---------------- |
| CVE monitoring for vendor products |      Daily (automated)      | Engineering      |
| Vendor security advisory review    |           Weekly            | Security Officer |
| SLA performance review             |           Monthly           | IT Manager       |
| Access privilege review            |          Quarterly          | Security Officer |
| Vendor compliance attestation      |          Annually           | DPO              |
| Full vendor reassessment           | Per classification schedule | Compliance Team  |

### 7.2 Vendor Incident Response

1. **Vendor notifies** of security incident affecting IVF data
2. **Security Officer** evaluates impact, initiates Breach Notification SOP if PHI affected
3. **Engineering** implements compensating controls (IP blocks, credential rotation, etc.)
4. **DPO** assesses GDPR/HIPAA notification requirements
5. **AI Governance Board** reviews if AI vendor is affected
6. **Post-incident** — vendor remediation review, reassessment

### 7.3 Vendor Exit Strategy

| Requirement        | Action                                       | Timeline  |
| ------------------ | -------------------------------------------- | :-------: |
| Data return        | Request all IVF data in portable format      | T+30 days |
| Data deletion      | Certify permanent deletion of all copies     | T+60 days |
| Access revocation  | Revoke all system access, rotate credentials | Immediate |
| Alternative vendor | Activate pre-assessed alternative            |  Per BCP  |
| Knowledge transfer | Document integration dependencies            | T+14 days |

## 8. Compliance Mapping

| Requirement                  | Framework   |    Section    | Status |
| ---------------------------- | ----------- | :-----------: | :----: |
| Third-party risk management  | SOC 2       |     CC9.2     |   ✅   |
| Supplier relationships       | ISO 27001   | A.5.19-A.5.23 |   ✅   |
| Business associate contracts | HIPAA       |   §164.314    |   ✅   |
| Third-party management       | HITRUST CSF |     09.0      |   ✅   |
| Data processor requirements  | GDPR        |    Art. 28    |   ✅   |
| AI third-party assessment    | ISO 42001   |   Annex A.7   |   ✅   |
| Supply chain risk            | NIST AI RMF |   GOVERN 5    |   ✅   |

---

**Approval:**

| Role             | Name                 | Date         | Signature  |
| ---------------- | -------------------- | ------------ | ---------- |
| Security Officer | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO              | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| IT Manager       | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| CEO/Director     | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

---

## Related Documents — Compliance Documentation Suite

### Comprehensive Guides

- [Compliance Master Guide](compliance_master_guide.md) — Executive overview, program structure, governance, glossary
- [Compliance Flows & Procedures](compliance_flows_and_procedures.md) — Detailed workflows for DSR, breach, AI governance, incident response
- [Implementation & Deployment Guide](compliance_implementation_deployment.md) — Practical deployment, configuration, runbooks, DR procedures
- [Evaluation & Audit Guide](compliance_evaluation_audit.md) — Scoring methodology, health score, maturity model, audit preparation
- [Standards Mapping Matrix](compliance_standards_mapping.md) — Cross-framework traceability (HIPAA, GDPR, SOC 2, ISO 27001, HITRUST, NIST AI, ISO 42001)

### Related Operational Procedures

- [Ongoing Operations Manual](ongoing_operations_manual.md)
- [Breach Notification SOP](breach_notification_sop.md)
- [BCP/DRP](bcp_drp.md)
- [Pseudonymization Procedures](pseudonymization_procedures.md)
- [Vendor Risk Assessment](vendor_risk_assessment.md)
- [ROPA Register](ropa_register.md)
