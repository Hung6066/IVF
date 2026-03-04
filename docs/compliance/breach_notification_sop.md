# Breach Notification Standard Operating Procedure (SOP)

**Document ID:** IVF-SOP-BREACH-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** Security Officer / DPO  
**Review Cycle:** Annual  
**Classification:** CONFIDENTIAL

---

## 1. Purpose

This SOP establishes the procedures for detecting, assessing, containing, and reporting data breaches in compliance with:

- **HIPAA** Breach Notification Rule (45 CFR §§164.400–414)
- **GDPR** Articles 33–34
- **SOC 2** CC7.3–CC7.4
- **HITRUST CSF** 11.a–11.e

## 2. Scope

Applies to all IVF Information System personnel who access, process, or store Protected Health Information (PHI), Personally Identifiable Information (PII), or any sensitive data managed by the system.

## 3. Definitions

| Term               | Definition                                                                                           |
| ------------------ | ---------------------------------------------------------------------------------------------------- |
| **Breach**         | Unauthorized acquisition, access, use, or disclosure of PHI/PII that compromises security or privacy |
| **PHI**            | Protected Health Information — any health data linked to an individual (HIPAA)                       |
| **PII**            | Personally Identifiable Information — data that can identify an individual (GDPR)                    |
| **DPA**            | Data Protection Authority — supervisory authority under GDPR                                         |
| **HHS**            | U.S. Department of Health and Human Services                                                         |
| **Covered Entity** | Organization subject to HIPAA that maintains PHI                                                     |

## 4. Breach Detection

### 4.1 Automated Detection

The system employs automated breach detection via:

| Component                    | Detection Method                                                                                                        | Reference                                    |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| `ThreatDetectionService`     | 7 threat signals: IP intel, UA analysis, impossible travel, brute force, anomalous access, input validation, time-based | `/api/security/events`                       |
| `IncidentResponseService`    | Rule-based automated incident creation with pattern matching                                                            | `/api/security/enterprise/incidents`         |
| `BehavioralAnalyticsService` | Z-score anomaly detection (threshold ≥25), 30-day behavioral baselines                                                  | `/api/security/enterprise/behavior-profiles` |
| `ZeroTrustMiddleware`        | Continuous request evaluation, risk scoring 0-100                                                                       | Every API request                            |

### 4.2 Manual Detection

Personnel must report suspected breaches immediately to the Security Officer via:

- **In-app:** Security incident report form
- **Email:** security@[organization].com
- **Phone:** Security hotline (24/7)

## 5. Breach Response Workflow

```
Detection → Assessment → Containment → Notification → Remediation → Resolution → Post-Incident Review
```

### 5.1 Phase 1: Detection (T+0h)

| Step | Action                            | Responsible                   | System                          |
| ---- | --------------------------------- | ----------------------------- | ------------------------------- |
| 1    | Breach detected (auto or manual)  | System/Staff                  | `SecurityEvent` created         |
| 2    | SecurityIncident created          | `IncidentResponseService`     | Status: `Open`                  |
| 3    | Initial severity classification   | System                        | Low/Medium/High/Critical        |
| 4    | Security Officer notified         | `SecurityNotificationService` | Email + SMS for Critical        |
| 5    | BreachNotification record created | Security Officer              | `POST /api/compliance/breaches` |

### 5.2 Phase 2: Assessment (T+0 to T+24h)

| Step | Action                                                  | Responsible      | Deadline                                    |
| ---- | ------------------------------------------------------- | ---------------- | ------------------------------------------- |
| 1    | Determine scope (affected records, data types, systems) | Incident Team    | T+12h                                       |
| 2    | Identify root cause and attack vector                   | Incident Team    | T+24h                                       |
| 3    | Classify affected data (PHI, PII, financial, biometric) | DPO              | T+12h                                       |
| 4    | Count affected individuals                              | Incident Team    | T+24h                                       |
| 5    | Update BreachNotification                               | Security Officer | `POST /api/compliance/breaches/{id}/assess` |

**Data Type Classification:**

- **PHI:** Patient records, medical images, lab results, treatment cycles
- **PII:** Name, email, phone, address, date of birth
- **Financial:** Billing records, payment information
- **Biometric:** Fingerprint templates, biometric identifiers
- **Credentials:** Passwords (hashed), API keys, tokens

### 5.3 Phase 3: Containment (T+0 to T+4h)

| Step | Action                     | Responsible               | System                                       |
| ---- | -------------------------- | ------------------------- | -------------------------------------------- |
| 1    | Isolate affected systems   | IT/Security               | Manual                                       |
| 2    | Lock compromised accounts  | `IncidentResponseService` | `lock_account` action                        |
| 3    | Revoke active sessions     | `IncidentResponseService` | `revoke_sessions` action                     |
| 4    | Block suspicious IPs       | `IncidentResponseService` | `block_ip` action                            |
| 5    | Force password resets      | `IncidentResponseService` | `require_password_change`                    |
| 6    | Preserve forensic evidence | IT/Security               | Audit logs, SecurityEvents                   |
| 7    | Document containment steps | Security Officer          | `POST /api/compliance/breaches/{id}/contain` |

### 5.4 Phase 4: Notification

#### GDPR — Data Protection Authority (Art. 33)

| Requirement       | Detail                                                                                           |
| ----------------- | ------------------------------------------------------------------------------------------------ |
| **Deadline**      | **72 hours** from detection                                                                      |
| **Who to notify** | Relevant Data Protection Authority                                                               |
| **Content**       | Nature of breach, categories/numbers of data subjects, DPO contact, consequences, measures taken |
| **System**        | `POST /api/compliance/breaches/{id}/notify-dpa`                                                  |
| **Exemption**     | Notification not required if breach unlikely to result in risk to data subjects                  |

#### GDPR — Data Subjects (Art. 34)

| Requirement       | Detail                                                                      |
| ----------------- | --------------------------------------------------------------------------- |
| **Deadline**      | Without undue delay (if high risk to rights/freedoms)                       |
| **Who to notify** | Affected individuals                                                        |
| **Content**       | Clear language describing breach, DPO contact, consequences, measures taken |
| **System**        | `POST /api/compliance/breaches/{id}/notify-subjects`                        |
| **Exemption**     | Not required if data was encrypted or measures eliminate risk               |

#### HIPAA — Individual Notification (§164.404)

| Requirement       | Detail                                                                                              |
| ----------------- | --------------------------------------------------------------------------------------------------- |
| **Deadline**      | **60 days** from discovery                                                                          |
| **Who to notify** | Each affected individual                                                                            |
| **Method**        | Written notice (first-class mail) or email (if individual consented)                                |
| **Content**       | Description, types of information, steps individual should take, what entity is doing, contact info |
| **System**        | `POST /api/compliance/breaches/{id}/notify-subjects`                                                |

#### HIPAA — HHS Notification (§164.408)

| Requirement       | Detail                                            |
| ----------------- | ------------------------------------------------- |
| **Deadline**      | Within 60 days if ≥500 affected; annually if <500 |
| **Who to notify** | HHS Secretary via breach portal                   |
| **Threshold**     | **≥500 affected records**                         |
| **System**        | `POST /api/compliance/breaches/{id}/notify-hhs`   |

#### HIPAA — Media Notification (§164.406)

| Requirement       | Detail                                                 |
| ----------------- | ------------------------------------------------------ |
| **Deadline**      | Within 60 days                                         |
| **Who to notify** | Prominent media outlets in affected state/jurisdiction |
| **Threshold**     | **≥500 affected in a single state/jurisdiction**       |
| **System**        | `POST /api/compliance/breaches/{id}/notify-media`      |

### 5.5 Phase 5: Remediation (T+24h to T+30d)

| Step | Action                                                      |
| ---- | ----------------------------------------------------------- |
| 1    | Patch vulnerabilities that caused the breach                |
| 2    | Update security controls (firewall rules, access policies)  |
| 3    | Conduct additional security training for affected personnel |
| 4    | Review and update incident response rules                   |
| 5    | Implement additional monitoring for similar attack patterns |

### 5.6 Phase 6: Resolution & Lessons Learned (T+30d)

| Step | Action                                  | System                                       |
| ---- | --------------------------------------- | -------------------------------------------- |
| 1    | Document lessons learned                | `POST /api/compliance/breaches/{id}/resolve` |
| 2    | Update security policies and procedures | Manual                                       |
| 3    | Conduct post-incident review meeting    | Meeting minutes                              |
| 4    | Update risk assessment                  | Risk register                                |
| 5    | Close breach record                     | `POST /api/compliance/breaches/{id}/close`   |

## 6. Notification Templates

### 6.1 GDPR DPA Notification Template

```
Subject: Data Breach Notification — [Organization Name]
Date: [Date]
Reference: [Breach ID]

Dear Data Protection Authority,

Pursuant to Article 33 of the General Data Protection Regulation (EU) 2016/679,
we are notifying you of a personal data breach affecting our organization.

1. NATURE OF THE BREACH
   - Type: [unauthorized_access / data_loss / ransomware / insider_threat]
   - Date detected: [Date]
   - Date contained: [Date]

2. CATEGORIES AND APPROXIMATE NUMBER OF DATA SUBJECTS
   - Categories: [patients / staff / both]
   - Approximate number: [N]

3. CATEGORIES OF PERSONAL DATA CONCERNED
   - [PHI / PII / financial / biometric]

4. DATA PROTECTION OFFICER CONTACT
   - Name: [DPO Name]
   - Email: [DPO Email]
   - Phone: [DPO Phone]

5. LIKELY CONSEQUENCES
   - [Description of potential impact on data subjects]

6. MEASURES TAKEN OR PROPOSED
   - Containment: [Steps taken]
   - Prevention: [Measures to prevent recurrence]

Yours faithfully,
[Security Officer Name]
[Organization Name]
```

### 6.2 Individual Notification Template (HIPAA/GDPR)

```
Subject: Important Notice About Your Personal Data

Dear [Patient/User Name],

We are writing to inform you of a security incident that may have affected
your personal information.

WHAT HAPPENED:
[Brief description of the breach]

INFORMATION INVOLVED:
[Types of data that may have been affected]

WHAT WE ARE DOING:
[Steps taken to address the breach and prevent future occurrences]

WHAT YOU CAN DO:
- Monitor your accounts for suspicious activity
- Change your password: [link]
- Contact us if you notice anything unusual

CONTACT INFORMATION:
- Security Team: security@[organization].com
- Data Protection Officer: dpo@[organization].com
- Phone: [Contact Number]

We sincerely apologize for this incident and are committed to protecting
your information.

Sincerely,
[Organization Name]
```

## 7. Breach Risk Assessment Matrix

| Factor           | Low Risk      | Medium Risk         | High Risk    | Critical Risk               |
| ---------------- | ------------- | ------------------- | ------------ | --------------------------- |
| Records affected | <100          | 100-499             | 500-5,000    | >5,000                      |
| Data sensitivity | Non-sensitive | PII                 | PHI          | PHI + Financial + Biometric |
| Access type      | View only     | Download            | Modification | Exfiltration                |
| Duration         | <1 hour       | 1-24 hours          | 1-7 days     | >7 days                     |
| Encryption       | Encrypted     | Partially encrypted | Unencrypted  | Decrypted by attacker       |

## 8. Roles and Responsibilities

| Role                 | Responsibilities                                                            |
| -------------------- | --------------------------------------------------------------------------- |
| **Security Officer** | Lead response, coordinate teams, ensure notifications, report to management |
| **DPO**              | Assess GDPR implications, advise on notifications, interface with DPA       |
| **IT/Security Team** | Contain breach, preserve evidence, implement remediation                    |
| **Legal**            | Review notification requirements, advise on regulatory obligations          |
| **Communications**   | Draft public communications if required                                     |
| **Management**       | Authorize actions, receive reports, strategic decisions                     |

## 9. Record Keeping

All breach notifications and response actions must be recorded for a minimum of:

- **HIPAA:** 6 years from date of creation or last effective date
- **GDPR:** Duration of processing + 5 years
- **System:** Automated via `BreachNotification` entity and `AuditLog`

## 10. Review and Updates

This SOP must be reviewed:

- **Annually** (routine review)
- **After every breach incident** (lessons learned)
- **Upon regulatory changes** (HIPAA/GDPR amendments)

---

**Approval:**

| Role             | Name                 | Date         | Signature  |
| ---------------- | -------------------- | ------------ | ---------- |
| Security Officer | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO              | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| CTO/IT Director  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
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
