# Standard Contractual Clauses (SCCs) — Implementation Guide & Template

**Document ID:** IVF-SCC-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Legal Reference:** GDPR Article 46(2)(c), European Commission Implementing Decision (EU) 2021/914

---

## 1. Overview

This document provides the framework for Standard Contractual Clauses (SCCs) used when transferring personal data from the IVF Information System to recipients outside the European Economic Area (EEA) or countries without an adequacy decision.

### 1.1 When SCCs Are Required

SCCs must be executed when:

- Patient data is transferred to a third-party processor in a non-adequate country
- System infrastructure components are hosted outside the EEA
- Technical support providers access data remotely from a non-adequate country
- Backup or disaster recovery data is replicated to a non-adequate location

### 1.2 Current Transfer Assessment

| Transfer                  | From       |     To     | SCC Required |   Status    |
| ------------------------- | ---------- | :--------: | :----------: | :---------: |
| Primary database          | Local (VN) | Local (VN) |     N/A      | No transfer |
| MinIO storage             | Local (VN) | Local (VN) |     N/A      | No transfer |
| Backup replication        | Local (VN) | Local (VN) |     N/A      | No transfer |
| Cloud monitoring (if any) | Local (VN) |    TBD     |     TBD      |   Assess    |
| Vendor support access     | Local (VN) |    TBD     |     TBD      |   Assess    |

---

## 2. SCC Module Selection Guide

The EU SCCs (2021/914) have four modules. Select based on the transfer relationship:

|    Module    | Relationship              | IVF Use Case                                             |
| :----------: | ------------------------- | -------------------------------------------------------- |
| **Module 1** | Controller → Controller   | Sharing data with partner clinics abroad                 |
| **Module 2** | Controller → Processor    | Cloud hosting, SaaS vendors, IT support providers        |
| **Module 3** | Processor → Sub-processor | Vendor uses a sub-processor in a third country           |
| **Module 4** | Processor → Controller    | Rare — data sent back to a controller in a third country |

**Primary module for IVF system:** Module 2 (Controller → Processor) for any future cloud or vendor relationships.

---

## 3. SCC Template — Module 2 (Controller to Processor)

### SECTION I

#### Clause 1: Purpose and Scope

These Clauses set out appropriate safeguards for the transfer of personal data from the data exporter to the data importer, in the context of:

**Processing description:**

| Element                  | Description                                                             |
| ------------------------ | ----------------------------------------------------------------------- |
| **Data Exporter**        | [Clinic Name], operating the IVF Information System                     |
| **Data Importer**        | [Vendor/Processor Name]                                                 |
| **Purpose of transfer**  | [Specific purpose, e.g., "cloud hosting of encrypted database backups"] |
| **Duration**             | For the duration of the service agreement or [specific period]          |
| **Nature of processing** | [Storage / Analysis / Technical support / etc.]                         |

#### Clause 2: Invariability

These Clauses set out appropriate safeguards including enforceable data subject rights and effective legal remedies pursuant to Article 46(1) and Article 46(2)(c) of Regulation (EU) 2016/679.

#### Clause 3: Third-Party Beneficiaries

Data subjects may invoke and enforce these Clauses as third-party beneficiaries.

#### Clause 4: Interpretation

These Clauses shall be interpreted in light of the provisions of GDPR.

### SECTION II — OBLIGATIONS OF THE PARTIES

#### Clause 5: Data Protection Safeguards

The data importer warrants that it has no reason to believe that applicable legislation prevents it from fulfilling its obligations under these Clauses.

#### Clause 6: Description of Transfer(s)

**Annex I.A — List of Parties:**

| Role          | Details                          |
| ------------- | -------------------------------- |
| Data Exporter | Name: [Clinic Name]              |
|               | Address: [Address]               |
|               | Contact: dpo@[clinic-domain].com |
|               | Role: Controller                 |
| Data Importer | Name: [Vendor Name]              |
|               | Address: [Vendor Address]        |
|               | Contact: [Vendor DPO/Contact]    |
|               | Role: Processor                  |

**Annex I.B — Description of Transfer:**

| Element                     | Description                                                                                                  |
| --------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Categories of data subjects | Patients, clinical staff                                                                                     |
| Categories of personal data | [Select applicable: Identity data, contact data, health data, biometric data, financial data, security logs] |
| Sensitive data              | [If applicable: health data per Art. 9]                                                                      |
| Frequency of transfer       | [Continuous / Daily / On-demand / etc.]                                                                      |
| Nature of processing        | [E.g., "Encrypted storage of database backups. Processor does not access decrypted content."]                |
| Purpose                     | [Specific purpose]                                                                                           |
| Retention period            | [As per service agreement, max: aligned with ROPA retention]                                                 |

**Annex I.C — Competent Supervisory Authority:**

[Relevant EU supervisory authority, if applicable]

#### Clause 7: Docking Clause

Additional parties may accede to these Clauses by completing Annex I.A and signing.

#### Clause 8: Data Protection Safeguards

The data importer shall:

**(a) Purpose limitation:** Process data only on documented instructions from the exporter.

**(b) Transparency:** Make available information about processing to data subjects upon request.

**(c) Security (Annex II — Technical & Organizational Measures):**

| Measure                  | Requirement                            | IVF System Implementation                           |
| ------------------------ | -------------------------------------- | --------------------------------------------------- |
| Encryption in transit    | TLS 1.2+ minimum                       | TLS 1.2 enforced                                    |
| Encryption at rest       | AES-256 minimum                        | AES-256-GCM field-level                             |
| Access control           | Role-based, least privilege            | 9-role RBAC, MFA required                           |
| Logging                  | Comprehensive audit trail              | Partitioned audit tables, 5-year retention          |
| Data segregation         | Logical or physical isolation          | Dedicated Docker networks, separate MinIO buckets   |
| Backup                   | Encrypted backups with tested recovery | 3-2-1 strategy, encrypted off-site                  |
| Vulnerability management | Regular scanning                       | CodeQL SAST, Trivy SCA, ZAP DAST (CI/CD)            |
| Incident response        | <72 hours notification                 | Automated breach detection + notification           |
| Personnel                | Background checks, training            | Annual security training, ComplianceTraining entity |
| Physical security        | Data center controls                   | [Per vendor certification]                          |

**(d) Sub-processing:** Prior specific or general written authorization from the exporter required.

**(e) Assistance:** Assist the exporter in responding to data subject requests.

**(f) Deletion/Return:** Upon termination, delete or return all data. Certify deletion.

**(g) Audit:** Allow and contribute to audits by the exporter or appointed auditor.

**(h) Transfer Impact Assessment:** Assess whether local laws affect compliance with these Clauses.

#### Clause 9: Use of Sub-processors

Option selected: **☐ General written authorization** / **☐ Specific prior authorization**

The data importer shall:

- Maintain and share a list of sub-processors
- Inform the exporter of new sub-processors (minimum 14 days notice)
- Impose same data protection obligations on sub-processors
- Remain liable for sub-processor compliance

#### Clause 10: Data Subject Rights

The data importer shall promptly notify the exporter of any data subject request.

#### Clause 11: Redress

Data subjects may lodge complaints with the data importer and/or the exporter.

#### Clause 12: Liability

Each party shall be liable to data subjects for damages caused by breach of these Clauses.

### SECTION III — LOCAL LAWS

#### Clause 13: Local Laws and Practices Affecting Compliance

**Transfer Impact Assessment (TIA) Checklist:**

| Factor                                             | Assessment                                                  |
| -------------------------------------------------- | ----------------------------------------------------------- |
| Rule of law and respect for human rights           | [Assessment]                                                |
| Relevant legislation (surveillance, access)        | [Cite specific laws]                                        |
| Government access requests received (last 3 years) | [Number or "None"]                                          |
| Supplementary measures applied                     | [List: encryption, pseudonymization, etc.]                  |
| Overall assessment                                 | [Adequate / Requires supplementary measures / Not adequate] |

### SECTION IV — FINAL PROVISIONS

#### Clause 14: Non-Compliance and Termination

The data exporter may suspend or terminate the transfer if:

- The importer breaches these Clauses
- Local laws prevent compliance
- Supplementary measures are insufficient

#### Clause 15: Governing Law and Jurisdiction

Governing law: [EU Member State law]  
Jurisdiction: [Courts of EU Member State]

---

## 4. Supplementary Measures

When the TIA identifies risks, apply these technical supplementary measures:

| Measure                   | Description                                                |   When Required   |
| ------------------------- | ---------------------------------------------------------- | :---------------: |
| **End-to-end encryption** | Data encrypted before transfer, keys held only by exporter |     High risk     |
| **Pseudonymization**      | Replace identifiers with tokens before transfer            | Medium-High risk  |
| **Split processing**      | Split data so importer never has full records              |  High risk (PHI)  |
| **Transport encryption**  | Additional layer (e.g., VPN + TLS)                         |    Medium risk    |
| **Access restrictions**   | Importer cannot access decrypted data                      | All PHI transfers |

---

## 5. SCC Execution Tracker

| Vendor                           |  Module  | TIA Status | SCC Signed | Review Due |
| -------------------------------- | :------: | :--------: | :--------: | :--------: |
| [Example: Cloud Backup Provider] | Module 2 | Completed  |  Pending   |     —      |
| [Example: Remote IT Support]     | Module 2 |  Pending   |     —      |     —      |

---

## 6. Document Control

| Version |    Date    | Author | Changes                                       |
| :-----: | :--------: | ------ | --------------------------------------------- |
|   1.0   | 2026-03-03 | DPO    | Initial SCC implementation guide and template |

**Approved by:**

| Role          | Name                 | Date         | Signature  |
| ------------- | -------------------- | ------------ | ---------- |
| DPO           | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Legal Counsel | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| CEO/Director  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
