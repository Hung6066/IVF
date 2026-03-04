# Records of Processing Activities (ROPA) Register

**Document ID:** IVF-ROPA-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Data Controller:** [Clinic Name]  
**DPO Contact:** dpo@[clinic-domain].com  
**Legal Reference:** GDPR Article 30

---

## 1. Controller Information

| Field                             | Value                   |
| --------------------------------- | ----------------------- |
| Controller Name                   | [Clinic Name]           |
| Controller Address                | [Address]               |
| Controller Contact                | [Phone / Email]         |
| DPO Name                          | [DPO Name]              |
| DPO Contact                       | dpo@[clinic-domain].com |
| EU Representative (if applicable) | [Name / N/A]            |

---

## 2. Processing Activities Register

### PA-001: Patient Registration & Identity Management

| Field                       | Details                                                                |
| --------------------------- | ---------------------------------------------------------------------- |
| **Activity Name**           | Patient Registration & Identity Management                             |
| **Purpose**                 | Register patients, manage identity, link to treatment records          |
| **Legal Basis**             | Art. 6(1)(b) Contract performance; Art. 9(2)(h) Healthcare provision   |
| **Data Categories**         | Name, DOB, gender, national ID, passport, address, email, phone, photo |
| **Special Categories**      | None at registration stage                                             |
| **Data Subject Categories** | Patients, Partners (couples)                                           |
| **Recipients**              | Clinical staff, administrative staff, referring physicians             |
| **Third Country Transfers** | None (data stored locally)                                             |
| **Retention Period**        | 10 years after last treatment                                          |
| **Technical Measures**      | AES-256-GCM encryption, RBAC, MFA, audit logging                       |
| **DPIA Required**           | No                                                                     |
| **Status**                  | Active                                                                 |

### PA-002: Biometric Patient Identification

| Field                       | Details                                                                                                          |
| --------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **Activity Name**           | Biometric Fingerprint Identification                                                                             |
| **Purpose**                 | Verify patient identity using fingerprint matching to prevent misidentification                                  |
| **Legal Basis**             | Art. 9(2)(a) Explicit consent                                                                                    |
| **Data Categories**         | Fingerprint templates (mathematical representation, not images)                                                  |
| **Special Categories**      | Biometric data (Art. 9)                                                                                          |
| **Data Subject Categories** | Patients who opt-in                                                                                              |
| **Recipients**              | Biometric matching system (server-side only)                                                                     |
| **Third Country Transfers** | None                                                                                                             |
| **Retention Period**        | Until patient withdraws consent                                                                                  |
| **Technical Measures**      | Templates stored encrypted (AES-256-GCM), server-side matching only, no raw images stored, dedicated SignalR hub |
| **DPIA Required**           | Yes — Completed (see IVF-DPIA-001)                                                                               |
| **Status**                  | Active                                                                                                           |

### PA-003: Clinical Treatment Management

| Field                       | Details                                                                                                               |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **Activity Name**           | IVF/IUI/ICSI/IVM Treatment Cycle Management                                                                           |
| **Purpose**                 | Manage fertility treatment cycles, track medications, procedures, outcomes                                            |
| **Legal Basis**             | Art. 6(1)(b) Contract; Art. 9(2)(h) Healthcare; Art. 6(1)(c) Legal obligation                                         |
| **Data Categories**         | Treatment plans, medication protocols, procedure records, embryo data, sperm/oocyte data, lab results, clinical notes |
| **Special Categories**      | Health data, Genetic data, Reproductive data                                                                          |
| **Data Subject Categories** | Patients, Partners, Donors                                                                                            |
| **Recipients**              | Doctors, nurses, embryologists, lab technicians                                                                       |
| **Third Country Transfers** | None                                                                                                                  |
| **Retention Period**        | 10 years post-treatment (per medical records regulations)                                                             |
| **Technical Measures**      | Field-level encryption (PHI), RBAC by role, audit trail, data masking                                                 |
| **DPIA Required**           | Yes — Completed (see IVF-DPIA-001)                                                                                    |
| **Status**                  | Active                                                                                                                |

### PA-004: Billing & Financial Management

| Field                       | Details                                                                     |
| --------------------------- | --------------------------------------------------------------------------- |
| **Activity Name**           | Billing, Invoicing & Insurance Processing                                   |
| **Purpose**                 | Generate invoices, process payments, manage insurance claims                |
| **Legal Basis**             | Art. 6(1)(b) Contract performance; Art. 6(1)(c) Legal obligation (tax)      |
| **Data Categories**         | Billing records, payment history, insurance policy numbers, treatment codes |
| **Special Categories**      | Health data (treatment codes revealing diagnoses)                           |
| **Data Subject Categories** | Patients                                                                    |
| **Recipients**              | Cashiers, finance staff, insurance providers, tax authorities               |
| **Third Country Transfers** | None                                                                        |
| **Retention Period**        | 7 years (tax/financial regulations)                                         |
| **Technical Measures**      | Encrypted storage, role-restricted access (Cashier role), audit logging     |
| **DPIA Required**           | No                                                                          |
| **Status**                  | Active                                                                      |

### PA-005: Queue Management & Appointments

| Field                       | Details                                                               |
| --------------------------- | --------------------------------------------------------------------- |
| **Activity Name**           | Real-time Queue Management                                            |
| **Purpose**                 | Manage patient flow, queue positioning, waiting time notifications    |
| **Legal Basis**             | Art. 6(1)(b) Contract performance                                     |
| **Data Categories**         | Patient name, queue number, appointment time, assigned doctor, status |
| **Special Categories**      | None (no clinical data in queue display)                              |
| **Data Subject Categories** | Patients                                                              |
| **Recipients**              | Receptionists, clinical staff, patients (via SignalR)                 |
| **Third Country Transfers** | None                                                                  |
| **Retention Period**        | 90 days                                                               |
| **Technical Measures**      | SignalR with JWT auth, no PHI in queue display, RBAC                  |
| **DPIA Required**           | No                                                                    |
| **Status**                  | Active                                                                |

### PA-006: Digital Document Signing

| Field                       | Details                                                                               |
| --------------------------- | ------------------------------------------------------------------------------------- |
| **Activity Name**           | Digital PDF Signing (PKI)                                                             |
| **Purpose**                 | Digitally sign consent forms, prescriptions, medical reports using PKI infrastructure |
| **Legal Basis**             | Art. 6(1)(c) Legal obligation; Art. 6(1)(b) Contract                                  |
| **Data Categories**         | Document content (may contain PHI), digital signatures, certificates, timestamps      |
| **Special Categories**      | Health data (within signed documents)                                                 |
| **Data Subject Categories** | Patients (document subjects), Doctors (signers)                                       |
| **Recipients**              | SignServer (signing), EJBCA (PKI), MinIO (storage)                                    |
| **Third Country Transfers** | None (all infrastructure local)                                                       |
| **Retention Period**        | 10 years (aligned with medical records)                                               |
| **Technical Measures**      | mTLS, rate limiting (30 ops/min), encrypted storage in MinIO, certificate pinning     |
| **DPIA Required**           | No                                                                                    |
| **Status**                  | Active                                                                                |

### PA-007: System Security & Threat Detection

| Field                         | Details                                                                                               |
| ----------------------------- | ----------------------------------------------------------------------------------------------------- |
| **Activity Name**             | Automated Security Monitoring & Threat Detection                                                      |
| **Purpose**                   | Detect unauthorized access, anomalous behavior, security threats                                      |
| **Legal Basis**               | Art. 6(1)(f) Legitimate interest (security)                                                           |
| **Data Categories**           | IP addresses, user agents, login patterns, behavioral profiles, threat scores, security events        |
| **Special Categories**        | None                                                                                                  |
| **Data Subject Categories**   | All system users (staff)                                                                              |
| **Recipients**                | Security administrators, incident response team                                                       |
| **Third Country Transfers**   | None                                                                                                  |
| **Retention Period**          | 2 years (security logs), 5 years (audit trails)                                                       |
| **Technical Measures**        | Zero Trust architecture, 7+ threat signals, z-score behavioral analytics, automated incident response |
| **DPIA Required**             | Yes — Completed (see IVF-DPIA-001)                                                                    |
| **Automated Decision-Making** | Yes — Threat scoring may restrict access. Human override available.                                   |
| **Status**                    | Active                                                                                                |

### PA-008: User Session & Access Management

| Field                       | Details                                                                                                               |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| **Activity Name**           | Enterprise User Session Management                                                                                    |
| **Purpose**                 | Manage user sessions, login history, group permissions, consent tracking                                              |
| **Legal Basis**             | Art. 6(1)(f) Legitimate interest; Art. 6(1)(c) Legal obligation                                                       |
| **Data Categories**         | Session tokens, login timestamps, IP addresses, device info, user group memberships, permission sets, consent records |
| **Special Categories**      | None                                                                                                                  |
| **Data Subject Categories** | All system users (staff)                                                                                              |
| **Recipients**              | System administrators, audit team                                                                                     |
| **Third Country Transfers** | None                                                                                                                  |
| **Retention Period**        | Session data: 90 days; Login history: 2 years; Consent records: duration of employment + 3 years                      |
| **Technical Measures**      | JWT RS256 3072-bit, 60-min token expiry, refresh token rotation, session monitoring                                   |
| **DPIA Required**           | No                                                                                                                    |
| **Status**                  | Active                                                                                                                |

### PA-009: Dynamic Forms & Reports

| Field                       | Details                                                                                         |
| --------------------------- | ----------------------------------------------------------------------------------------------- |
| **Activity Name**           | Dynamic Clinical Form & Report Generation                                                       |
| **Purpose**                 | Create, fill, and export dynamic clinical forms and reports                                     |
| **Legal Basis**             | Art. 6(1)(b) Contract; Art. 9(2)(h) Healthcare                                                  |
| **Data Categories**         | Form responses (may contain any clinical data), report outputs (PDF)                            |
| **Special Categories**      | Health data (form content)                                                                      |
| **Data Subject Categories** | Patients                                                                                        |
| **Recipients**              | Clinical staff (creators/viewers), patients (report recipients)                                 |
| **Third Country Transfers** | None                                                                                            |
| **Retention Period**        | 10 years (aligned with medical records)                                                         |
| **Technical Measures**      | RBAC, field-level encryption for sensitive fields, QuestPDF generation, encrypted MinIO storage |
| **DPIA Required**           | No                                                                                              |
| **Status**                  | Active                                                                                          |

### PA-010: File & Document Storage

| Field                       | Details                                                                                          |
| --------------------------- | ------------------------------------------------------------------------------------------------ |
| **Activity Name**           | Medical Document & Image Storage                                                                 |
| **Purpose**                 | Store medical documents, signed PDFs, medical images                                             |
| **Legal Basis**             | Art. 6(1)(b) Contract; Art. 9(2)(h) Healthcare                                                   |
| **Data Categories**         | Medical documents, signed consent forms, medical images (ultrasound, embryo images)              |
| **Special Categories**      | Health data                                                                                      |
| **Data Subject Categories** | Patients                                                                                         |
| **Recipients**              | Clinical staff with appropriate role                                                             |
| **Third Country Transfers** | None                                                                                             |
| **Retention Period**        | 10 years post-treatment                                                                          |
| **Technical Measures**      | MinIO S3-compatible storage, 3 segregated buckets, encrypted at rest, presigned URLs with expiry |
| **DPIA Required**           | No                                                                                               |
| **Status**                  | Active                                                                                           |

---

## 3. Review Schedule

| Review Type             |    Frequency    |  Next Due  |
| ----------------------- | :-------------: | :--------: |
| Full ROPA review        |     Annual      | 2027-03-03 |
| New activity assessment | Per new feature |  Ongoing   |
| Legal basis validation  |   Semi-annual   | 2026-09-03 |
| Retention period audit  |     Annual      | 2027-03-03 |

---

## 4. Document Control

| Version |    Date    | Author | Changes                                             |
| :-----: | :--------: | ------ | --------------------------------------------------- |
|   1.0   | 2026-03-03 | DPO    | Initial ROPA register with 10 processing activities |

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

### Related Operational Procedures

- [Ongoing Operations Manual](ongoing_operations_manual.md)
- [Breach Notification SOP](breach_notification_sop.md)
- [BCP/DRP](bcp_drp.md)
- [Pseudonymization Procedures](pseudonymization_procedures.md)
- [Vendor Risk Assessment](vendor_risk_assessment.md)
- [ROPA Register](ropa_register.md)
