# Compliance Standards Mapping Matrix

> **Document ID:** IVF-CMP-CSM-001  
> **Version:** 1.0  
> **Effective Date:** 2026-03-04  
> **Classification:** CONFIDENTIAL  
> **Owner:** Compliance Officer / CISO  
> **Review Cycle:** Semi-Annual

---

## Table of Contents

1. [Unified Control Framework](#1-unified-control-framework)
2. [Cross-Framework Requirement Traceability](#2-cross-framework-requirement-traceability)
3. [Control-to-Implementation Mapping](#3-control-to-implementation-mapping)
4. [Evidence Mapping Matrix](#4-evidence-mapping-matrix)
5. [Regulatory Conflict Resolution](#5-regulatory-conflict-resolution)
6. [IVF-Specific Requirements](#6-ivf-specific-requirements)
7. [Vietnam Regulatory Requirements](#7-vietnam-regulatory-requirements)
8. [Compliance API Coverage Map](#8-compliance-api-coverage-map)

---

## 1. Unified Control Framework

### 1.1 Control Domains

IVF system controls are organized into 14 unified domains that map across all 7 frameworks:

| ID  | Domain                      | Controls |           Frameworks Covered           |
| :-: | --------------------------- | :------: | :------------------------------------: |
| D01 | Access Control & Identity   |    18    |                 All 7                  |
| D02 | Audit & Accountability      |    12    |                 All 7                  |
| D03 | Data Protection & Privacy   |    22    |    HIPAA, GDPR, SOC 2, ISO, HITRUST    |
| D04 | Encryption & Key Management |    10    |    HIPAA, GDPR, SOC 2, ISO, HITRUST    |
| D05 | Incident Response           |    14    |                 All 7                  |
| D06 | Business Continuity         |    8     |       SOC 2, ISO 27001, HITRUST        |
| D07 | Risk Management             |    10    |                 All 7                  |
| D08 | Security Operations         |    16    |                 All 7                  |
| D09 | Vendor & Supply Chain       |    8     |   GDPR, SOC 2, ISO, HITRUST, NIST AI   |
| D10 | Training & Awareness        |    6     |                 All 7                  |
| D11 | Configuration Management    |    8     |          SOC 2, ISO, HITRUST           |
| D12 | AI Governance & Ethics      |    15    | NIST AI RMF, ISO 42001, GDPR (Art. 22) |
| D13 | Physical Security           |    6     |              ISO, HITRUST              |
| D14 | Compliance Operations       |    10    |                 All 7                  |
|     | **TOTAL**                   | **163**  |                                        |

---

## 2. Cross-Framework Requirement Traceability

### 2.1 D01 — Access Control & Identity

| IVF Control                 |         HIPAA         |     GDPR      | SOC 2 | ISO 27001 | HITRUST | NIST AI | ISO 42001 |
| --------------------------- | :-------------------: | :-----------: | :---: | :-------: | :-----: | :-----: | :-------: |
| Unique User ID              |   §164.312(a)(2)(i)   | Art. 5(1)(f)  | CC6.1 |   A.8.2   |  01.b   |    —    |     —     |
| Role-Based Access (RBAC)    |    §164.312(a)(1)     |    Art. 25    | CC6.3 |   A.8.3   |  01.c   | GV.1.3  |    5.2    |
| Multi-Factor Auth (MFA)     |      §164.312(d)      | Art. 32(1)(b) | CC6.1 |   A.8.5   |  01.q   |    —    |     —     |
| Biometric Auth              |      §164.312(d)      |   Art. 9(4)   | CC6.1 |   A.8.5   |  01.q   | MP.3.2  |     —     |
| Auto Session Timeout        |  §164.312(a)(2)(iii)  |       —       | CC6.1 |   A.8.1   |  01.t   |    —    |     —     |
| Emergency Access            |  §164.312(a)(2)(ii)   |       —       | CC6.1 |   A.8.6   |  01.b   |    —    |     —     |
| Access Reviews              | §164.308(a)(3)(ii)(A) | Art. 5(1)(f)  | CC6.2 |   A.8.2   |  01.e   |    —    |     —     |
| Zero Trust Architecture     |           —           |  Art. 25, 32  | CC6.1 |  A.8.20   |  01.x   | GV.3.1  |     —     |
| JWT Token Management        |      §164.312(d)      |    Art. 32    | CC6.1 |   A.8.5   |  01.v   |    —    |     —     |
| Passkey/FIDO2 Support       |           —           |    Art. 32    | CC6.1 |   A.8.5   |  01.q   |    —    |     —     |
| Conditional Access Policies |           —           |    Art. 32    | CC6.1 |  A.8.20   |  01.x   | GV.3.1  |     —     |
| Impersonation Controls      |           —           | Art. 5(2)/32  | CC6.3 |   A.8.2   |  01.c   |    —    |     —     |
| Permission Delegation       |    §164.308(a)(3)     | Art. 5(1)(f)  | CC6.3 |   A.8.2   |  01.c   |    —    |     —     |
| User Behavior Analytics     |           —           |    Art. 32    | CC7.2 |  A.8.16   |  09.ab  | MG.2.3  |     —     |
| Login History & Analytics   |      §164.312(b)      |    Art. 30    | CC7.2 |  A.8.15   |  09.ab  |    —    |     —     |
| Group-Based Permissions     |    §164.308(a)(3)     | Art. 5(1)(f)  | CC6.3 |   A.8.2   |  01.c   |    —    |     —     |
| Geo-Fencing                 |           —           |    Art. 32    | CC6.1 |  A.8.20   |  01.x   |    —    |     —     |
| IP Whitelist                |           —           |    Art. 32    | CC6.1 |  A.8.20   |  01.x   |    —    |     —     |

### 2.2 D02 — Audit & Accountability

| IVF Control                   |         HIPAA         |     GDPR     |    SOC 2     | ISO 27001 | HITRUST | NIST AI | ISO 42001 |
| ----------------------------- | :-------------------: | :----------: | :----------: | :-------: | :-----: | :-----: | :-------: |
| Audit Trail (all actions)     |      §164.312(b)      |  Art. 5(2)   | CC4.1, CC7.2 |  A.8.15   |  09.aa  | GV.4.1  |    9.2    |
| Partitioned Audit Logs        |      §164.312(b)      |   Art. 30    |    CC7.2     |  A.8.15   |  09.aa  |    —    |     —     |
| Tamper-Proof Logging          |    §164.312(c)(2)     | Art. 5(1)(f) |    CC7.2     |  A.8.15   |  09.aa  |    —    |     —     |
| Log Review (automated alerts) | §164.308(a)(1)(ii)(D) |  Art. 5(2)   |    CC4.1     |  A.8.16   |  09.ab  | MG.2.3  |    9.1    |
| Audit Log Retention (7yr)     |      §164.530(j)      |   Art. 30    |    CC7.2     |  A.8.15   |  09.aa  |    —    |     —     |
| Security Event Logging        |      §164.312(b)      |  Art. 33/34  |    CC7.2     |  A.8.15   |  09.aa  | GV.4.1  |     —     |
| AI Decision Audit Trail       |           —           |  Art. 22(3)  |      —       |     —     |    —    | GV.4.1  |   6.1.3   |
| Patient Access Logging        |      §164.312(b)      |  Art. 15(3)  |    CC7.2     |  A.8.15   |  09.aa  |    —    |     —     |
| Admin Action Logging          |      §164.312(b)      |  Art. 5(2)   |    CC7.2     |  A.8.15   |  09.aa  |    —    |     —     |
| Consent Change Logging        |      §164.530(j)      |  Art. 7(1)   |      —       |     —     |    —    |    —    |     —     |
| Data Export Logging           |      §164.312(b)      |   Art. 20    |    CC7.2     |     —     |  09.aa  |    —    |     —     |
| API Access Logging            |      §164.312(b)      |   Art. 32    |    CC7.2     |  A.8.15   |  09.aa  |    —    |     —     |

### 2.3 D03 — Data Protection & Privacy

| IVF Control                            |    HIPAA     |     GDPR      | SOC 2  |   ISO 27001    | HITRUST |
| -------------------------------------- | :----------: | :-----------: | :----: | :------------: | :-----: |
| Consent Management (6 categories)      |   §164.508   | Art. 6, 7, 9  |   —    |       —        |  13.a   |
| Data Subject Rights (7 types)          | §164.524/526 |  Art. 15-22   | P6.1-7 |       —        |  13.d   |
| Privacy Notice (bilingual)             |   §164.520   |  Art. 12-14   |  P1.1  |       —        |  13.a   |
| Data Minimization                      |      —       | Art. 5(1)(c)  |   —    |     A.8.11     |  13.c   |
| Purpose Limitation                     |      —       | Art. 5(1)(b)  |  P3.1  |       —        |  13.c   |
| Pseudonymization                       |      —       | Art. 4(5), 25 |   —    |     A.8.11     |    —    |
| Processing Activity Records (ROPA)     |      —       |    Art. 30    |   —    |       —        |  13.a   |
| DPO Designation                        |      —       |  Art. 37-39   |   —    |       —        |    —    |
| DPIA                                   |      —       |    Art. 35    |   —    |       —        |    —    |
| Cross-Border Transfer Safeguards       |      —       |  Art. 44-49   |   —    |       —        |    —    |
| Patient Data Restriction               |   §164.522   |    Art. 18    |   —    |       —        |    —    |
| Data Portability (FHIR)                |      —       |    Art. 20    |   —    |       —        |    —    |
| Right to Erasure (with HIPAA override) |      —       |    Art. 17    |   —    |       —        |    —    |
| Breach Notification (72h)              |   §164.408   |  Art. 33-34   | CC7.3  |     A.6.8      |  11.a   |
| Data Classification (4 levels)         | §164.312(a)  | Art. 5(1)(f)  | CC6.1  | A.5.12, A.5.13 |  07.d   |
| Data Retention Policies                | §164.530(j)  | Art. 5(1)(e)  |   —    |       —        |    —    |
| Minimum Necessary Standard             | §164.502(b)  | Art. 5(1)(c)  |   —    |       —        |  13.c   |
| Genetic Data Protection                |   §164.502   |    Art. 9     |   —    |       —        |    —    |
| Embryo Data Handling                   |   §164.502   |    Art. 9     |   —    |       —        |    —    |
| Reproductive Health Data               |   §164.502   |    Art. 9     |   —    |       —        |    —    |
| Partner Data Correlation               |   §164.502   | Art. 6(1)(c)  |   —    |       —        |    —    |
| Donor Anonymization                    |   §164.514   |    Art. 89    |   —    |       —        |    —    |

### 2.4 D04 — Encryption & Key Management

| IVF Control               |       HIPAA        |     GDPR      | SOC 2 | ISO 27001 | HITRUST |
| ------------------------- | :----------------: | :-----------: | :---: | :-------: | :-----: |
| TLS 1.3 (transit)         |   §164.312(e)(1)   | Art. 32(1)(a) | CC6.7 |  A.8.24   |  09.m   |
| AES-256 (at rest)         | §164.312(a)(2)(iv) | Art. 32(1)(a) | CC6.1 |  A.8.24   |  06.d   |
| Database Encryption       | §164.312(a)(2)(iv) | Art. 32(1)(a) | CC6.1 |  A.8.24   |  06.d   |
| MinIO Encryption (S3-SSE) | §164.312(a)(2)(iv) | Art. 32(1)(a) | CC6.1 |  A.8.24   |  06.d   |
| Digital Signatures (PKI)  |   §164.312(c)(1)   |    Art. 32    | CC6.1 |  A.8.24   |  06.f   |
| BCrypt Password Hashing   |    §164.312(d)     | Art. 32(1)(a) | CC6.1 |   A.8.5   |  01.d   |
| JWT Token Encryption      |   §164.312(e)(1)   |    Art. 32    | CC6.1 |  A.8.24   |  09.m   |
| mTLS (SignServer/EJBCA)   |         —          |    Art. 32    | CC6.7 |  A.8.24   |  09.m   |
| Key Rotation Schedule     |         —          |    Art. 32    | CC6.1 |  A.8.24   |  06.d   |
| Backup Encryption         | §164.312(a)(2)(iv) | Art. 32(1)(a) | A1.2  |  A.8.13   |  06.d   |

### 2.5 D05 — Incident Response

| IVF Control                     |         HIPAA         |    GDPR    | SOC 2 | ISO 27001 | HITRUST | NIST AI | ISO 42001 |
| ------------------------------- | :-------------------: | :--------: | :---: | :-------: | :-----: | :-----: | :-------: |
| Incident Response Plan          |    §164.308(a)(6)     |  Art. 33   | CC7.3 |  A.5.24   |  11.a   | MG.4.1  |    8.2    |
| Automated Detection (7 signals) | §164.308(a)(1)(ii)(D) |  Art. 33   | CC7.2 |  A.8.16   |  11.b   | MG.2.3  |    9.1    |
| Priority Classification (P1-P4) |           —           |     —      | CC7.3 |  A.5.25   |  11.a   |    —    |     —     |
| Auto-Response Rules             |    §164.308(a)(6)     | Art. 33(2) | CC7.4 |  A.5.26   |  11.c   | MG.4.2  |     —     |
| Breach Notification (72h GDPR)  |           —           | Art. 33(1) | CC7.3 |   A.6.8   |  11.a   |    —    |     —     |
| Breach Notification (60d HIPAA) |       §164.408        |     —      | CC7.3 |   A.6.8   |  11.a   |    —    |     —     |
| SecurityIncident Entity         |    §164.308(a)(6)     | Art. 33(5) | CC7.3 |  A.5.27   |  11.d   | GV.4.1  |     —     |
| Root Cause Analysis             |           —           |     —      | CC7.5 |  A.5.27   |  11.e   | MG.4.2  |   10.2    |
| Forensic Evidence Preservation  |           —           |     —      | CC7.4 |  A.5.28   |  11.c   |    —    |     —     |
| Stakeholder Communication       |       §164.408        |  Art. 34   | CC7.3 |   A.6.8   |  11.a   | MG.4.3  |    8.2    |
| Post-Incident Review            |           —           |     —      | CC7.5 |  A.5.27   |  11.e   | MG.4.2  |   10.1    |
| MTTR Tracking                   |           —           |     —      | CC4.1 |     —     |    —    | MG.2.3  |    9.1    |
| Incident Trend Analysis         |           —           |     —      | CC4.1 |  A.8.16   |  09.ab  | MG.2.3  |    9.1    |
| Tabletop Exercises              | §164.308(a)(7)(ii)(D) |     —      | CC7.5 |  A.5.24   |  11.a   | MG.4.1  |     —     |

### 2.6 D06 — Business Continuity

| IVF Control                   | SOC 2 | ISO 27001 | HITRUST |
| ----------------------------- | :---: | :-------: | :-----: |
| Business Continuity Plan      | A1.2  |  A.5.29   |  12.a   |
| Disaster Recovery Plan        | A1.2  |  A.5.30   |  12.c   |
| 3-2-1 Backup Strategy         | A1.2  |  A.8.13   |  09.l   |
| WAL Archiving                 | A1.2  |  A.8.13   |  09.l   |
| Recovery Testing (quarterly)  | A1.3  |  A.5.30   |  12.e   |
| RPO/RTO Targets               | A1.2  |  A.5.30   |  12.c   |
| Backup Integrity Verification | A1.2  |  A.8.13   |  09.l   |
| Failover Procedures           | A1.2  |  A.5.30   |  12.c   |

### 2.7 D07 — Risk Management

| IVF Control               |         HIPAA         |      GDPR      | SOC 2 | ISO 27001 | HITRUST |   NIST AI    | ISO 42001 |
| ------------------------- | :-------------------: | :------------: | :---: | :-------: | :-----: | :----------: | :-------: |
| Risk Assessment (formal)  | §164.308(a)(1)(ii)(A) | Art. 32(2), 35 | CC3.1 |   6.1.2   |  03.a   |    GV.2.1    |    6.1    |
| Risk Treatment Plan       | §164.308(a)(1)(ii)(B) |    Art. 32     | CC3.2 |   6.1.3   |  03.b   |    MG.1.1    |   6.1.3   |
| Vendor Risk Assessment    |    §164.308(b)(1)     |    Art. 28     | CC9.2 |  A.5.21   |  05.i   |   MAP.5.1    |     —     |
| Risk Register Maintenance |           —           |       —        | CC3.1 |   6.1.2   |  03.a   |    GV.2.2    |   6.1.2   |
| Risk Appetite Statement   |           —           |       —        | CC3.1 |    5.2    |  03.a   |    GV.1.2    |    5.2    |
| Compliance Scoring Engine |           —           |       —        | CC4.1 |    9.1    |    —    |    MS.1.1    |    9.1    |
| Health Score Monitoring   |           —           |       —        | CC4.1 |    9.1    |    —    |    MS.2.1    |    9.1    |
| AI Risk Assessment        |           —           |    Art. 35     |   —   |     —     |    —    | MAP.1, MAP.2 |    6.1    |
| Threat Modeling           |           —           |       —        | CC3.2 |   6.1.2   |    —    |   MAP.1.5    |     —     |
| Residual Risk Acceptance  |           —           |       —        | CC3.2 |   6.1.3   |  03.c   |    MG.1.2    |   6.1.3   |

### 2.8 D08 — Security Operations

| IVF Control                             |         HIPAA         | SOC 2 | ISO 27001 | HITRUST |
| --------------------------------------- | :-------------------: | :---: | :-------: | :-----: |
| Vulnerability Scanning                  |    §164.308(a)(8)     | CC7.1 |   A.8.8   |  10.m   |
| Penetration Testing                     |    §164.308(a)(8)     | CC7.1 |   A.8.8   |  10.m   |
| Rate Limiting (100 req/min)             |           —           | CC6.1 |  A.8.20   |  09.h   |
| Digital Signing Rate Limit (30 ops/min) |           —           | CC6.1 |  A.8.20   |  09.h   |
| WAF / Input Validation                  |           —           | CC6.6 |  A.8.26   |  09.h   |
| CORS Configuration                      |           —           | CC6.6 |  A.8.26   |  09.h   |
| Container Security (Docker)             |           —           | CC6.1 |   A.8.9   |  09.h   |
| Secret Management                       |           —           | CC6.1 |   A.8.9   |  09.h   |
| Network Segmentation                    |           —           | CC6.6 |  A.8.22   |  09.e   |
| Endpoint Protection                     |           —           | CC6.8 |   A.8.7   |  09.j   |
| Patch Management                        | §164.308(a)(5)(ii)(B) | CC7.1 |   A.8.8   |  10.a   |
| Anti-Malware                            |           —           | CC6.8 |   A.8.7   |  09.j   |
| DDoS Protection                         |           —           | CC6.6 |  A.8.20   |  09.h   |
| API Security (3-layer auth)             |      §164.312(d)      | CC6.1 |   A.8.5   |  01.v   |
| Redis Security                          |           —           | CC6.1 |  A.8.24   |  09.m   |
| MinIO Access Control                    |           —           | CC6.1 |   A.8.3   |  01.c   |

### 2.9 D09 — Vendor & Supply Chain

| IVF Control                   |     GDPR      | SOC 2 | ISO 27001 | HITRUST | NIST AI |
| ----------------------------- | :-----------: | :---: | :-------: | :-----: | :-----: |
| Vendor Risk Assessment        |  Art. 28(1)   | CC9.2 |  A.5.21   |  05.i   | MAP.5.1 |
| Business Associate Agreements |  Art. 28(3)   | CC9.2 |  A.5.21   |  05.i   |    —    |
| Data Processing Agreements    |    Art. 28    | CC2.3 |  A.5.21   |  05.i   |    —    |
| Standard Contractual Clauses  | Art. 46(2)(c) |   —   |     —     |    —    |    —    |
| Vendor SLA Monitoring         |       —       | CC9.2 |  A.5.22   |  05.j   |    —    |
| Sub-Processor Management      |  Art. 28(2)   | CC9.2 |  A.5.21   |  05.i   |    —    |
| Vendor Security Reviews       |       —       | CC9.2 |  A.5.23   |  05.k   | MAP.5.2 |
| Vendor Decommissioning        | Art. 28(3)(g) | CC9.2 |  A.5.21   |    —    |    —    |

### 2.10 D10 — Training & Awareness

| IVF Control                 |       HIPAA       |     GDPR      | SOC 2 | ISO 27001 | HITRUST | NIST AI | ISO 42001 |
| --------------------------- | :---------------: | :-----------: | :---: | :-------: | :-----: | :-----: | :-------: |
| Security Awareness Training |  §164.308(a)(5)   | Art. 39(1)(b) | CC1.4 |   A.6.3   |  02.e   | GV.1.5  |    7.2    |
| HIPAA Training              | §164.308(a)(5)(i) |       —       |   —   |     —     |  02.e   |    —    |     —     |
| GDPR Training               |         —         | Art. 39(1)(b) |   —   |     —     |    —    |    —    |     —     |
| AI Ethics Training          |         —         |       —       |   —   |     —     |    —    | GV.1.5  |    7.2    |
| Incident Response Training  | §164.308(a)(6)(i) |    Art. 33    | CC7.4 |   A.6.3   |  11.a   | MG.4.1  |     —     |
| Annual Refresher Training   | §164.308(a)(5)(i) | Art. 39(1)(b) | CC1.4 |   A.6.3   |  02.e   |    —    |     —     |

### 2.11 D11 — Configuration Management

| IVF Control               | SOC 2 | ISO 27001 | HITRUST |
| ------------------------- | :---: | :-------: | :-----: |
| Secure Baseline Configs   | CC8.1 |   A.8.9   |  10.h   |
| Change Management Process | CC8.1 |  A.8.32   |  10.k   |
| Version Control (Git)     | CC8.1 |  A.8.32   |  10.k   |
| EF Core Migrations        | CC8.1 |  A.8.32   |  10.k   |
| Docker Image Versioning   | CC8.1 |   A.8.9   |  10.k   |
| Environment Separation    | CC8.1 |  A.8.31   |  10.i   |
| Code Review Requirements  | CC8.1 |  A.8.32   |  10.k   |
| Rollback Procedures       | CC8.1 |  A.8.32   |  10.k   |

### 2.12 D12 — AI Governance & Ethics

| IVF Control                    |    GDPR    |   NIST AI RMF   | ISO 42001  |
| ------------------------------ | :--------: | :-------------: | :--------: |
| AI Governance Charter          |     —      |     GV.1.1      |    5.2     |
| AI Model Version Registry      |     —      |     GV.4.1      |    8.3     |
| AI Model Lifecycle (5 stages)  |     —      |  GV.1.3, MAP.1  |    8.1     |
| Bias Testing Framework         |  Art. 22   | MS.2.1, MS.2.6  | 6.1.3, 9.1 |
| FPR/FNR Tracking               |  Art. 22   |     MS.2.1      |    9.1     |
| Fairness Threshold Enforcement |  Art. 22   |     MS.2.6      |   6.1.3    |
| Pre-Deployment Testing         |     —      |     MS.1.1      |    8.2     |
| Post-Deployment Monitoring     |     —      | MG.2.3, MG.3.1  |    9.1     |
| AI Incident Response           |     —      |     MG.4.1      |    8.2     |
| Model Rollback Capability      |     —      |     MG.2.4      |    8.2     |
| Explainability Documentation   | Art. 22(3) | MAP.2.2, MS.2.7 |    7.5     |
| Human Oversight Requirement    | Art. 22(3) |     GV.1.7      |    5.4     |
| AI Impact Assessment           |  Art. 35   |     MAP.5.1     |    6.1     |
| AI Risk Register               |     —      | GV.2.1, MAP.1.3 |   6.1.2    |
| Ethical Use Policy             |     —      | GV.1.1, GV.6.1  |    5.2     |

### 2.13 D13 — Physical Security

| IVF Control               |   ISO 27001    | HITRUST |
| ------------------------- | :------------: | :-----: |
| Physical Access Control   |     A.7.1      |  08.b   |
| Secure Areas              |     A.7.2      |  08.c   |
| Equipment Security        |     A.7.8      |  08.d   |
| Media Disposal            | A.7.10, A.7.14 |  08.l   |
| Environmental Protection  |     A.7.5      |  08.e   |
| Clear Desk / Clear Screen |     A.7.7      |  08.i   |

### 2.14 D14 — Compliance Operations

| IVF Control                    |     HIPAA      |     GDPR     | SOC 2 | ISO 27001 | HITRUST | NIST AI | ISO 42001 |
| ------------------------------ | :------------: | :----------: | :---: | :-------: | :-----: | :-----: | :-------: |
| Compliance Health Dashboard    |       —        |      —       | CC4.1 |    9.1    |    —    | MS.1.1  |    9.1    |
| Compliance Scoring Engine      |       —        |      —       | CC4.1 |    9.1    |    —    | MS.1.1  |    9.1    |
| Compliance Schedule (14 tasks) | §164.308(a)(8) |  Art. 5(2)   | CC4.1 |    9.1    |    —    | GV.3.2  |    9.1    |
| Internal Audit Program         | §164.308(a)(8) |  Art. 5(2)   | CC4.1 |    9.2    |  06.g   | GV.4.2  |    9.2    |
| Management Review              |       —        |      —       | CC1.2 |    9.3    |  06.a   | GV.1.4  |    9.3    |
| Corrective Action Process      |       —        |      —       | CC4.2 |   10.2    |  06.i   | MG.1.3  |   10.2    |
| Document Control               |       —        |      —       | CC1.4 |    7.5    |  05.a   | GV.4.1  |    7.5    |
| Regulatory Change Monitoring   |       —        |      —       | CC3.3 |     —     |    —    | GV.1.6  |    4.1    |
| Data Retention Management      |  §164.530(j)   | Art. 5(1)(e) |   —   |     —     |    —    |    —    |     —     |
| Privacy Impact Assessment      |       —        |   Art. 35    |   —   |     —     |    —    | MAP.5.1 |    6.1    |

---

## 3. Control-to-Implementation Mapping

### 3.1 Backend Implementation References

|     Control Domain      | Primary Implementation                      | Key Files                                                                         |
| :---------------------: | ------------------------------------------- | --------------------------------------------------------------------------------- |
|   D01 Access Control    | JWT + RBAC + MFA + Biometrics               | `AdvancedSecurityEndpoints.cs`, `EnterpriseSecurityEndpoints.cs`, `JwtService.cs` |
|        D02 Audit        | Partitioned PostgreSQL audit table          | `AuditLog` entity, `AuditLoggingMiddleware`                                       |
|   D03 Data Protection   | DSR endpoints, Consent, Breach notification | `DataSubjectRequestEndpoints.cs`, `ComplianceEndpoints.cs`                        |
|     D04 Encryption      | TLS 1.3, AES-256, BCrypt, PKI               | `appsettings.json` (DigitalSigning), `PasswordService.cs`                         |
|  D05 Incident Response  | SecurityIncident + auto-response            | `EnterpriseSecurityEndpoints.cs` (incidents section)                              |
| D06 Business Continuity | Backup service + DR procedures              | `BackupComplianceService.cs`, `bcp_drp.md`                                        |
|   D07 Risk Management   | ComplianceScoringEngine + Health monitoring | `ComplianceEndpoints.cs`, `ComplianceMonitoringEndpoints.cs`                      |
| D08 Security Operations | Rate limiting, validation, auth pipeline    | `RateLimitingMiddleware`, auth pipeline in `Program.cs`                           |
|       D09 Vendor        | Vendor risk assessment docs                 | `vendor_risk_assessment.md`                                                       |
|      D10 Training       | ComplianceTraining entity + UI              | `ComplianceEndpoints.cs` (training section)                                       |
|  D11 Config Management  | EF migrations, Git, Docker                  | `IVF.Infrastructure/Migrations/`                                                  |
|    D12 AI Governance    | AiBiasTestResult + AiModelVersion           | AI Bias endpoints, AI governance charter                                          |
|      D13 Physical       | Documentation-based                         | Physical security addendum                                                        |
|   D14 Compliance Ops    | Health dashboard + scheduling               | `ComplianceScheduleEndpoints.cs`, `ComplianceMonitoringEndpoints.cs`              |

### 3.2 Frontend Implementation References

|         Control Domain          | Component            | Route                  |
| :-----------------------------: | -------------------- | ---------------------- |
|       D03 Data Protection       | DSR Management       | `/compliance/dsr`      |
|       D07 Risk Management       | Compliance Dashboard | `/compliance`          |
|          D10 Training           | Training Management  | `/compliance/training` |
|        D12 AI Governance        | AI Governance        | `/compliance/ai`       |
|       D14 Compliance Ops        | Compliance Schedule  | `/compliance/schedule` |
| D01 Access Control (Asset view) | Asset Inventory      | `/compliance/assets`   |

---

## 4. Evidence Mapping Matrix

### 4.1 Evidence Type to Control Mapping

|        Evidence Artifact        | Controls Satisfied | Collection Method                       |    Frequency     |
| :-----------------------------: | ------------------ | --------------------------------------- | :--------------: |
|   **User access list export**   | D01.1-3, D01.5-6   | `GET /api/admin/users`                  |    Quarterly     |
|     **RBAC configuration**      | D01.2, D01.6       | DB export of roles & permissions        |    Quarterly     |
|   **MFA enrollment records**    | D01.3              | Authentication logs                     |     Monthly      |
|     **Audit trail samples**     | D02.1-5            | `GET /api/audit/logs` (sampled)         |    Per audit     |
|    **DSR handling records**     | D03.2              | `GET /api/compliance/dsr`               |     Monthly      |
|       **Consent records**       | D03.1              | UserConsent table export                |    Quarterly     |
|       **Privacy notice**        | D03.3              | `privacy_notice.md` (Git versioned)     |  Annual review   |
|   **Breach notification log**   | D03.14, D05.5-6    | BreachNotification records              |   Per incident   |
|      **TLS scan results**       | D04.1              | SSL Labs scan / Qualys                  |    Quarterly     |
|      **Encryption config**      | D04.2-4            | System configuration export             |      Annual      |
|  **Incident response records**  | D05.1-7            | SecurityIncident table                  |   Per incident   |
|    **BCP/DRP test results**     | D06.5              | Test execution records                  |   Semi-annual    |
|  **Backup verification logs**   | D06.7              | Backup compliance API                   |     Monthly      |
|  **Risk assessment document**   | D07.1              | `risk_assessment.md`                    |      Annual      |
|     **Vendor assessments**      | D09.1-2            | `vendor_risk_assessment.md`             |      Annual      |
| **Training completion records** | D10.1-6            | ComplianceTraining table                |     Monthly      |
|   **Change management logs**    | D11.2              | Git commit + PR history                 |    Per change    |
|    **AI bias test results**     | D12.4-6            | AiBiasTestResult table                  | Per model change |
|  **AI model version history**   | D12.2-3            | AiModelVersion table                    |   Per version    |
|    **Health score history**     | D14.1              | `GET /api/compliance/monitoring/health` |      Daily       |
| **Compliance schedule status**  | D14.3              | `GET /api/compliance/schedule`          |      Weekly      |

### 4.2 Evidence Collection Scripts

```bash
# Export evidence for audit period
# Run from project root

# User access list
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/admin/users?pageSize=1000" > evidence/access_list.json

# DSR records for period
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/compliance/dsr?startDate=$START&endDate=$END" > evidence/dsr_records.json

# Training completions
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/compliance/training?pageSize=1000" > evidence/training_records.json

# AI bias test results
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/compliance/ai/bias-tests" > evidence/ai_bias_results.json

# Asset inventory
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/compliance/assets" > evidence/asset_inventory.json

# Health score snapshot
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/compliance/monitoring/health" > evidence/health_score.json

# Compliance schedule status
curl -H "Authorization: Bearer $TOKEN" \
  "$API_URL/api/compliance/schedule?pageSize=100" > evidence/schedule_status.json
```

---

## 5. Regulatory Conflict Resolution

### 5.1 GDPR vs HIPAA Conflicts

| Scenario           |         GDPR Requirement         |                 HIPAA Requirement                  | Resolution                                                                                          | IVF Implementation                                                                       |
| ------------------ | :------------------------------: | :------------------------------------------------: | --------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| Right to Erasure   |  Art. 17: Erasure upon request   |           §164.530(j): 6-year retention            | **HIPAA prevails** for treatment records; erasure applies to non-treatment data                     | DSR type=Erasure checks `IsRestricted` flag and retention requirements before processing |
| Consent Withdrawal |   Art. 7(3): Withdraw any time   | §164.508: Required consent for certain disclosures | **Both apply**: GDPR withdrawal stops secondary processing; HIPAA consent for specified disclosures | Consent management tracks 6 categories independently                                     |
| Data Portability   | Art. 20: Machine-readable format |                   No equivalent                    | **GDPR applies**: Provide FHIR-formatted export                                                     | DSR type=Portability with FHIR output                                                    |
| Breach Timeline    |     Art. 33: 72 hours to DPA     |          §164.408: 60 days to individuals          | **Shorter timeline**: Notify DPA within 72h AND individuals within 60 days                          | BreachNotification entity tracks both timelines                                          |
| Minimum Necessary  | Art. 5(1)(c): Data minimization  |      §164.502(b): Minimum necessary standard       | **Aligned**: Both require minimization                                                              | Data classification + access control enforce both                                        |

### 5.2 Treatment vs Research Data

| Data Use                |      Legal Basis (GDPR)       |         HIPAA Requirement          | IVF Approach                              |
| ----------------------- | :---------------------------: | :--------------------------------: | ----------------------------------------- |
| Direct Treatment        |         Art. 9(2)(h)          | Treatment/Payment/Operations (TPO) | No consent required (legitimate interest) |
| Quality Improvement     |         Art. 9(2)(h)          |       Healthcare operations        | Anonymize where possible                  |
| Clinical Research       | Art. 9(2)(a) explicit consent |       §164.508 authorization       | Separate consent + IRB approval required  |
| AI Model Training       |       Art. 22 + consent       |   De-identified data (§164.514)    | Pseudonymization + bias testing           |
| Public Health Reporting |         Art. 9(2)(i)          |            §164.512(b)             | Mandatory reporting per jurisdiction      |

### 5.3 Cross-Border Transfer Rules

|    Transfer Scenario    |        GDPR Mechanism         |       HIPAA Requirement        | IVF Implementation                              |
| :---------------------: | :---------------------------: | :----------------------------: | ----------------------------------------------- |
| All cloud infra (local) | Not applicable (no transfer)  |       BAA with provider        | All data stays in-country; SCCs ready if needed |
|   Backup replication    |      Art. 46(2)(c) SCCs       |         BAA extension          | `standard_contractual_clauses.md` prepared      |
|  Third-party analytics  |      Art. 46(2)(c) SCCs       |  De-identification preferred   | Pseudonymization before any sharing             |
|  Regulatory reporting   | Art. 49(1)(e) public interest | §164.512 permitted disclosures | Direct submission to local authority            |

---

## 6. IVF-Specific Requirements

### 6.1 Reproductive Health Data Controls

|      Data Category      | Special Requirements                                                   | IVF Control                                                           |
| :---------------------: | ---------------------------------------------------------------------- | --------------------------------------------------------------------- |
|     Embryo Records      | Unique lifecycle tracking, creation-to-disposition                     | Separate data lifecycle with extended retention (25 years)            |
|    Gamete Donor Data    | Anonymization required post-conception in many jurisdictions           | `Patient.DonorAnonymization()` with configurable timeline             |
| Genetic Testing Results | Heightened sensitivity, GINA compliance (US), genetic data protections | Classification: Critical-Restricted, additional consent               |
|   Partner/Couple Data   | Cross-referenced between partners, joint consent scenarios             | Couple entity links two Patient records; independent consent tracking |
|   Treatment Outcomes    | Quality reporting, success rates, statistical obligations              | Anonymized aggregate reporting; individual records retained per HIPAA |
|    Surrogacy Records    | Complex legal relationships, jurisdiction-dependent                    | Consent management supports surrogate-specific categories             |

### 6.2 Medical Device Regulation (if applicable)

|     Regulation     | Applicability                          |                  IVF System Status                  |
| :----------------: | -------------------------------------- | :-------------------------------------------------: |
| FDA 21 CFR Part 11 | Electronic records in US               |        Ready (audit trail + digital signing)        |
|  EU MDR 2017/745   | If classified as medical device        |          Not classified as medical device           |
|     IEC 62304      | Software lifecycle for medical devices |          Aligned (SDLC meets requirements)          |
|     ISO 14971      | Risk management for medical devices    | Aligned (risk assessment covers clinical scenarios) |

---

## 7. Vietnam Regulatory Requirements

### 7.1 Applicable Laws

|                 Law                  | Effective | Key Requirements                                         |                IVF Compliance                 |
| :----------------------------------: | :-------: | -------------------------------------------------------- | :-------------------------------------------: |
|      Law on Cybersecurity 2018       |   2019    | Data localization, security measures, incident reporting |            ✅ Local infrastructure            |
|   Law on Information Security 2015   |   2015    | Information classification, security controls            |           ✅ 4-level classification           |
|     Decree 13/2023/ND-CP (PDPD)      |   2023    | Personal data protection, consent, cross-border transfer |       ✅ Consent + DSR + local storage        |
|       Circular 09/2023/TT-BCA        |   2023    | Cybersecurity incident reporting                         |        ✅ Incident response procedures        |
| Health Examination and Treatment Law |   2023    | Patient data confidentiality, record retention           | ✅ HIPAA-aligned controls exceed requirements |

### 7.2 Vietnam-Specific Control Mapping

|              Vietnam Requirement               | IVF Control                                              | Compliance Evidence                              |
| :--------------------------------------------: | -------------------------------------------------------- | ------------------------------------------------ |
|     Data localization (Decree 13, Art. 26)     | All infrastructure in-country, no cross-border transfers | Infrastructure diagram, cloud provider contracts |
| Consent before processing (Decree 13, Art. 11) | 6-category consent management                            | UserConsent records, consent audit trail         |
|     Impact assessment (Decree 13, Art. 24)     | DPIA completed                                           | `dpia.md`                                        |
| Right of access/correction (Decree 13, Art. 9) | DSR Management (Access, Rectification types)             | DSR handling records                             |
| Data breach notification (Decree 13, Art. 23)  | 72h notification to Ministry of Public Security          | BreachNotification SOP + auto-notification       |
| Health data as sensitive (Decree 13, Art. 2.4) | Classification: Sensitive/Critical for all health data   | Data classification matrix                       |
|   Record retention (Health Examination Law)    | 10-year minimum for medical records                      | Data retention policy (10-25 years by category)  |

---

## 8. Compliance API Coverage Map

### 8.1 Endpoint-to-Framework Mapping

|        Endpoint Group         | Endpoints | HIPAA | GDPR | SOC 2 | ISO 27001 | HITRUST | NIST AI | ISO 42001 |
| :---------------------------: | :-------: | :---: | :--: | :---: | :-------: | :-----: | :-----: | :-------: |
|      ComplianceEndpoints      |    12     |  ✅   |  ✅  |  ✅   |    ✅     |   ✅    |    —    |     —     |
|  DataSubjectRequestEndpoints  |    12     |  ✅   |  ✅  |   —   |     —     |    —    |    —    |     —     |
|  ComplianceScheduleEndpoints  |    10     |  ✅   |  ✅  |  ✅   |    ✅     |   ✅    |   ✅    |    ✅     |
| ComplianceMonitoringEndpoints |     4     |   —   |  —   |  ✅   |    ✅     |    —    |   ✅    |    ✅     |
|  EnterpriseSecurityEndpoints  |    20+    |  ✅   |  ✅  |  ✅   |    ✅     |   ✅    |    —    |     —     |
|   AdvancedSecurityEndpoints   |    10+    |  ✅   |  —   |  ✅   |    ✅     |   ✅    |    —    |     —     |
|       AI Bias Endpoints       |     5     |   —   |  ✅  |   —   |     —     |    —    |   ✅    |    ✅     |
|  Asset/Processing Endpoints   |     6     |   —   |  ✅  |   —   |    ✅     |    —    |    —    |     —     |

### 8.2 Coverage Summary

|  Framework  | Total Controls in IVF | API Endpoints Supporting |   Coverage    |
| :---------: | :-------------------: | :----------------------: | :-----------: |
|    HIPAA    |          45           |           30+            | 90% automated |
|    GDPR     |          50           |           35+            | 85% automated |
|    SOC 2    |          40           |           25+            | 80% automated |
|  ISO 27001  |          55           |           30+            | 75% automated |
|   HITRUST   |        44 (e1)        |           25+            | 85% automated |
| NIST AI RMF |          20           |           10+            | 70% automated |
|  ISO 42001  |          15           |            8+            | 60% automated |

---

_Reference Documents:_

- [Master Compliance Guide](compliance_master_guide.md)
- [Compliance Flows & Procedures](compliance_flows_and_procedures.md)
- [Implementation & Deployment Guide](compliance_implementation_deployment.md)
- [Evaluation & Audit Guide](compliance_evaluation_audit.md)
