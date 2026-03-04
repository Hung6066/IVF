# Pseudonymization & De-identification Procedures

**Document ID:** IVF-PSEUDO-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** DPO  
**Classification:** Internal — Confidential  
**Regulatory Basis:** GDPR Art. 4(5), Art. 11, Art. 25, Art. 32, Art. 89; HIPAA §164.514

---

## 1. Purpose

This document defines the procedures for pseudonymizing and de-identifying personal data and protected health information (PHI/ePHI) within the IVF Information System, ensuring compliance with GDPR, HIPAA, and supporting the data protection by design principle.

---

## 2. Definitions

| Term                  | Definition                                                                                                                                                                                    | Standard        |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------- |
| **Pseudonymization**  | Processing personal data so it can no longer be attributed to a specific data subject without use of additional information, kept separately and subject to technical/organizational measures | GDPR Art. 4(5)  |
| **Anonymization**     | Irreversible de-identification where no re-identification is possible                                                                                                                         | GDPR Recital 26 |
| **De-identification** | Removal of identifiers so data is not individually identifiable                                                                                                                               | HIPAA §164.514  |
| **Re-identification** | Reversing pseudonymization to link data back to a data subject                                                                                                                                | Internal        |

---

## 3. Pseudonymization Methods Used

### 3.1 GUID-Based Identifier Substitution

**Applied to:** All entities in the IVF system

| Aspect                 | Implementation                                                                     |
| ---------------------- | ---------------------------------------------------------------------------------- |
| Method                 | Every entity uses `Guid Id` as primary key (UUID v4)                               |
| Original identifiers   | Natural keys (PatientCode, IdentityNumber) stored separately                       |
| Separation of concerns | API responses use GUID only; natural keys require authorized access                |
| Reversibility          | Controlled — only through authenticated database access with appropriate RBAC role |

**Code reference:**

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    // ...
}
```

### 3.2 Field-Level Encryption

**Applied to:** All PHI/PII fields marked as sensitive

| Aspect         | Implementation                                                             |
| -------------- | -------------------------------------------------------------------------- |
| Algorithm      | AES-256-GCM (via EncryptionConfig entity)                                  |
| Key management | HashiCorp Vault (self-hosted) with auto-unseal                             |
| Scope          | IdentityNumber, Phone, Email, Address, medical notes                       |
| Reversibility  | Only through Vault-authenticated decryption with field-level access policy |

### 3.3 Anonymization (Irreversible)

**Applied to:** Patient records upon data retention expiry or data subject request

| Aspect          | Implementation                                                         |
| --------------- | ---------------------------------------------------------------------- |
| Method          | `Patient.Anonymize()` domain method                                    |
| Scope           | All 13 identifying fields set to null/"ANONYMIZED"                     |
| Status tracking | `IsAnonymized = true`, `AnonymizedAt` timestamp, `Status = Anonymized` |
| Reversibility   | **None** — irreversible by design                                      |

**Code reference:**

```csharp
public void Anonymize()
{
    FullName = "ANONYMIZED";
    IdentityNumber = null;
    Phone = null;
    Email = null;
    Address = null;
    // ... all identifiers cleared
    IsAnonymized = true;
    AnonymizedAt = DateTime.UtcNow;
    Status = PatientStatus.Anonymized;
}
```

---

## 4. Procedures

### 4.1 Pseudonymization at Collection (Default)

**Trigger:** Patient registration  
**Automatic actions:**

1. System generates GUID primary key for Patient entity
2. PatientCode assigned as sequential identifier (not PII)
3. Natural identifiers (IdentityNumber, Phone, Email) stored with field-level encryption
4. API responses return GUID-based references only
5. Consent recorded via `UpdateConsent()` with timestamp

### 4.2 De-identification for Research (On Request)

**Trigger:** Data subject consent for research OR authorized research export  
**Process:**

1. Verify `ConsentResearch = true` for all included patients
2. Export data with GUID-only identifiers (no natural keys)
3. Remove or generalize quasi-identifiers:
   - Date of birth → Year of birth only
   - Address → Province/region only
   - Age → Age range (e.g., 25-29)
4. Apply k-anonymity (k ≥ 5) or differential privacy
5. Record export in AuditLog with purpose "Research"

### 4.3 Anonymization (On Retention Expiry or DSR)

**Trigger:** DataRetentionExpiryDate reached OR approved GDPR erasure request  
**Process:**

1. Verify no legal hold prevents erasure (medical records retention: 10 years per Vietnamese law)
2. For non-regulated data: Hard delete (Phase 4 capability)
3. For regulated medical data: Call `Patient.Anonymize()` to irreversibly remove identifiers
4. Retain anonymized medical data for aggregated research/statistics
5. Record in AuditLog and DSR system (if triggered by DSR)
6. Notify data subject via `DataSubjectRequest.NotifyDataSubject()`

### 4.4 Processing Restriction (GDPR Art. 18)

**Trigger:** Data subject restriction request via DSR  
**Process:**

1. Receive DSR with `RequestType = "Restriction"`
2. Verify identity via `DataSubjectRequest.VerifyIdentity()`
3. Call `Patient.RestrictProcessing(reason)` — sets `IsRestricted = true`
4. System enforces restriction: restricted patients excluded from automated processing
5. Lifted only when data subject requests OR legal obligation requires

---

## 5. Technical Controls

### 5.1 Access Control Matrix for Re-identification

| Role         | GUID Access | Natural Key Access |    Decrypt PHI    | Anonymize | Re-identify |
| ------------ | :---------: | :----------------: | :---------------: | :-------: | :---------: |
| Admin        |     ✅      |         ✅         |        ✅         |    ✅     |     ✅      |
| Doctor       |     ✅      | ✅ (own patients)  | ✅ (own patients) |    ❌     |     ❌      |
| Nurse        |     ✅      |   ✅ (assigned)    |   ✅ (assigned)   |    ❌     |     ❌      |
| LabTech      |     ✅      |         ❌         |      Limited      |    ❌     |     ❌      |
| Receptionist |     ✅      |  ✅ (search only)  |        ❌         |    ❌     |     ❌      |
| DPO          |     ✅      |         ✅         |        ✅         |    ✅     |     ✅      |
| Researcher   |     ✅      |         ❌         |        ❌         |    ❌     |     ❌      |

### 5.2 Logging & Audit

All pseudonymization/anonymization operations are logged:

- **AuditLog**: entity type, action, user, timestamp
- **DataSubjectRequest**: tracks DSR-driven operations
- **Patient entity**: `AnonymizedAt`, `RestrictedAt` timestamps

---

## 6. HIPAA Safe Harbor De-identification (§164.514(b))

For HIPAA compliance, the following 18 identifiers are removed/generalized:

|  #  | Identifier              | IVF System Field | De-identification Method          |
| :-: | ----------------------- | ---------------- | --------------------------------- |
|  1  | Names                   | FullName         | Set to "ANONYMIZED"               |
|  2  | Geographic data         | Address          | Remove (set null)                 |
|  3  | Dates                   | DateOfBirth      | Generalize to year                |
|  4  | Phone numbers           | Phone            | Remove                            |
|  5  | Fax numbers             | N/A              | Not collected                     |
|  6  | Email addresses         | Email            | Remove                            |
|  7  | SSN                     | IdentityNumber   | Remove                            |
|  8  | Medical record numbers  | PatientCode      | Replaced with GUID                |
|  9  | Health plan beneficiary | InsuranceNumber  | Remove                            |
| 10  | Account numbers         | N/A              | Not collected                     |
| 11  | Certificate/license     | N/A              | Not collected                     |
| 12  | Vehicle identifiers     | N/A              | Not collected                     |
| 13  | Device identifiers      | N/A              | Biometric templates hashed        |
| 14  | Web URLs                | N/A              | Not collected                     |
| 15  | IP addresses            | AuditLog         | Generalized in exports            |
| 16  | Biometric identifiers   | Fingerprints     | Templates deleted on anonymize    |
| 17  | Full-face photographs   | PatientPhoto     | Deleted on anonymize              |
| 18  | Any unique number       | GUID             | Replaced with new GUID in exports |

---

## 7. Review & Maintenance

| Activity                       |    Frequency     | Owner      |
| ------------------------------ | :--------------: | ---------- |
| Procedure review               |      Annual      | DPO        |
| Encryption key rotation        | Per Vault policy | SecOps     |
| Access control audit           |    Quarterly     | IT Admin   |
| Anonymization execution        |    On trigger    | System/DPO |
| HIPAA Safe Harbor verification |      Annual      | Compliance |

---

## 8. Document Control

| Version |    Date    | Author | Changes                             |
| :-----: | :--------: | ------ | ----------------------------------- |
|   1.0   | 2026-03-03 | DPO    | Initial pseudonymization procedures |

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
