# Risk Assessment Report

**Document ID:** IVF-RA-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** Security Officer  
**Review Cycle:** Annual (or after significant changes)  
**Frameworks:** ISO 27001 Clause 6.1.2, HIPAA §164.308(a)(1)(ii)(A), SOC 2 CC3.2, HITRUST CSF 03.0

---

## 1. Purpose

This document provides a formal risk assessment of the IVF Information System, identifying threats, vulnerabilities, and risks to the confidentiality, integrity, and availability of electronic protected health information (ePHI) and other sensitive data.

## 2. Scope

All components of the IVF Information System:

- Backend API (.NET 10)
- Frontend application (Angular 21)
- Database (PostgreSQL)
- Object storage (MinIO)
- Caching layer (Redis)
- PKI infrastructure (EJBCA + SignServer)
- Biometric subsystem (DigitalPersona)
- Supporting infrastructure (Docker, networking)

## 3. Methodology

Risk assessment follows:

- **ISO 27005:2022** — Information security risk management
- **NIST SP 800-30 Rev. 1** — Guide for Conducting Risk Assessments

### 3.1 Risk Formula

$$Risk = Likelihood \times Impact$$

### 3.2 Likelihood Scale

| Level |  Rating   | Description                              |
| :---: | :-------: | ---------------------------------------- |
|   1   | Very Low  | Unlikely (<10% probability in 12 months) |
|   2   |    Low    | Possible (10-30% probability)            |
|   3   |  Medium   | Likely (30-60% probability)              |
|   4   |   High    | Very likely (60-90% probability)         |
|   5   | Very High | Almost certain (>90% probability)        |

### 3.3 Impact Scale

| Level |   Rating   | Description                                                                       |
| :---: | :--------: | --------------------------------------------------------------------------------- |
|   1   | Negligible | No patient impact, no data exposure                                               |
|   2   |   Minor    | Minor operational disruption, no PHI exposure                                     |
|   3   |  Moderate  | Service degradation, limited data exposure (<100 records)                         |
|   4   |   Major    | Extended outage, significant PHI exposure (100-5000 records)                      |
|   5   |  Critical  | Complete system failure, massive PHI breach (>5000 records), regulatory penalties |

### 3.4 Risk Matrix

|                  | Impact 1 | Impact 2 | Impact 3 | Impact 4 | Impact 5 |
| :--------------: | :------: | :------: | :------: | :------: | :------: |
| **Likelihood 5** |  5 (M)   |  10 (M)  |  15 (H)  | 20 (VH)  | 25 (VH)  |
| **Likelihood 4** |  4 (L)   |  8 (M)   |  12 (H)  |  16 (H)  | 20 (VH)  |
| **Likelihood 3** |  3 (L)   |  6 (M)   |  9 (M)   |  12 (H)  |  15 (H)  |
| **Likelihood 2** |  2 (L)   |  4 (L)   |  6 (M)   |  8 (M)   |  10 (M)  |
| **Likelihood 1** |  1 (L)   |  2 (L)   |  3 (L)   |  4 (L)   |  5 (M)   |

**Risk Levels:** L (Low: 1-4) | M (Medium: 5-10) | H (High: 11-16) | VH (Very High: 17-25)

## 4. Asset Inventory

### 4.1 Information Assets

|  #  | Asset                              |  Classification  | Owner             | Location                  |
| :-: | ---------------------------------- | :--------------: | ----------------- | ------------------------- |
| A1  | Patient medical records            | PHI/Confidential | Clinical Director | PostgreSQL                |
| A2  | Biometric templates (fingerprints) |  Sensitive PII   | Security Officer  | PostgreSQL (encrypted)    |
| A3  | Treatment cycle data               | PHI/Confidential | Clinical Director | PostgreSQL                |
| A4  | Medical images                     | PHI/Confidential | Clinical Director | MinIO                     |
| A5  | Signed PDF documents               | PHI/Confidential | DPO               | MinIO                     |
| A6  | User credentials                   |      Secret      | Security Officer  | PostgreSQL (hashed)       |
| A7  | Digital certificates & keys        |      Secret      | Security Officer  | EJBCA, file system        |
| A8  | API keys & tokens                  |      Secret      | Security Officer  | Configuration (encrypted) |
| A9  | Audit logs                         |     Internal     | Security Officer  | PostgreSQL (partitioned)  |
| A10 | System configuration               |     Internal     | IT Manager        | appsettings.json          |
| A11 | Backup files                       | PHI/Confidential | IT Manager        | Backup storage            |

### 4.2 System Assets

|  #  | Asset                  | Type         | Criticality |      Redundancy       |
| :-: | ---------------------- | ------------ | :---------: | :-------------------: |
| S1  | IVF.API server         | Application  |  Critical   |  Horizontal scaling   |
| S2  | PostgreSQL database    | Data store   |  Critical   | Streaming replication |
| S3  | MinIO storage          | Object store |    High     |  Bucket replication   |
| S4  | Redis cache            | Cache        |   Medium    | Graceful degradation  |
| S5  | EJBCA CA               | PKI          |    High     |  Certificate backup   |
| S6  | SignServer             | Signing      |    High     |     Rate limiting     |
| S7  | Docker host            | Runtime      |  Critical   |   Container restart   |
| S8  | Network infrastructure | Network      |  Critical   | Network segmentation  |

## 5. Threat Assessment

### 5.1 External Threats

|  #  | Threat                                 | Source                | Target Assets | Likelihood | Impact |    Risk    |
| :-: | -------------------------------------- | --------------------- | :-----------: | :--------: | :----: | :--------: |
| T1  | **Ransomware attack**                  | Cybercriminal         |     S1-S8     |     3      |   5    | **15 (H)** |
| T2  | **SQL injection**                      | Attacker              |     A1-A6     |     2      |   5    | **10 (M)** |
| T3  | **Credential stuffing**                | Attacker              |    A6, A8     |     3      |   4    | **12 (H)** |
| T4  | **Supply chain attack**                | Nation-state/Criminal |     S1-S7     |     2      |   5    | **10 (M)** |
| T5  | **DDoS attack**                        | Attacker              |    S1, S8     |     3      |   3    | **9 (M)**  |
| T6  | **MITM / eavesdropping**               | Attacker              |   A1-A5, A8   |     1      |   5    | **5 (M)**  |
| T7  | **Social engineering**                 | Attacker              |    A6, A8     |     3      |   4    | **12 (H)** |
| T8  | **Web application attack (XSS, CSRF)** | Attacker              |  S1, A1, A6   |     2      |   4    | **8 (M)**  |

### 5.2 Internal Threats

|  #  | Threat                            | Source               | Target Assets | Likelihood | Impact |    Risk    |
| :-: | --------------------------------- | -------------------- | :-----------: | :--------: | :----: | :--------: |
| T9  | **Insider data theft**            | Disgruntled employee |     A1-A5     |     2      |   5    | **10 (M)** |
| T10 | **Privilege escalation**          | Compromised account  |     A1-A8     |     2      |   4    | **8 (M)**  |
| T11 | **Accidental data exposure**      | Employee error       |     A1-A5     |     3      |   3    | **9 (M)**  |
| T12 | **Unauthorized biometric access** | Authenticated user   |      A2       |     1      |   4    | **4 (L)**  |

### 5.3 Environmental/Operational Threats

|  #  | Threat               | Source          | Target Assets | Likelihood | Impact |   Risk    |
| :-: | -------------------- | --------------- | :-----------: | :--------: | :----: | :-------: |
| T13 | **Hardware failure** | Equipment aging |     S2-S7     |     3      |   3    | **9 (M)** |
| T14 | **Power outage**     | Utility failure |     S1-S8     |     2      |   4    | **8 (M)** |
| T15 | **Natural disaster** | Environmental   |     S1-S8     |     1      |   5    | **5 (M)** |
| T16 | **Data corruption**  | Software bug    |   A1-A5, A9   |     2      |   4    | **8 (M)** |

## 6. Existing Controls Assessment

### 6.1 Authentication & Access Control

| Control                                                               |  Status   | Effectiveness | Threats Mitigated |
| --------------------------------------------------------------------- | :-------: | :-----------: | :---------------: |
| Multi-factor authentication (TOTP, SMS, Passkey, WebAuthn, Biometric) | ✅ Active |     High      |    T3, T7, T10    |
| Role-based access control (9 roles, 50+ permissions)                  | ✅ Active |     High      |   T9, T10, T12    |
| Password policy (NIST SP 800-63B: 12 char, breach DB check)           | ✅ Active |     High      |        T3         |
| Account lockout (5 attempts, 15-min window)                           | ✅ Active |     High      |        T3         |
| Session management (60-min JWT, 7-day refresh)                        | ✅ Active |     High      |      T3, T10      |
| Conditional access policies (geo, device, time, risk)                 | ✅ Active |     High      |      T3, T7       |
| User behavior analytics (z-score anomaly detection)                   | ✅ Active |    Medium     |      T9, T10      |

### 6.2 Data Protection

| Control                             |  Status   | Effectiveness | Threats Mitigated |
| ----------------------------------- | :-------: | :-----------: | :---------------: |
| AES-256-GCM field-level encryption  | ✅ Active |   Very High   |    T2, T6, T9     |
| TLS 1.2+ in transit                 | ✅ Active |   Very High   |        T6         |
| mTLS for signing services           | ✅ Active |   Very High   |      T6, T8       |
| Parameterized queries (EF Core)     | ✅ Active |   Very High   |        T2         |
| Input validation (FluentValidation) | ✅ Active |     High      |      T2, T8       |
| 3-2-1 backup strategy               | ✅ Active |     High      | T1, T13, T15, T16 |
| Database streaming replication      | ✅ Active |     High      |     T13, T16      |

### 6.3 Network Security

| Control                                            |   Status   | Effectiveness | Threats Mitigated |
| -------------------------------------------------- | :--------: | :-----------: | :---------------: |
| Docker network segmentation (public/signing/data)  | ✅ Active  |     High      |      T5, T6       |
| Rate limiting (100 req/min global, 30/min signing) | ✅ Active  |    Medium     |      T3, T5       |
| IP whitelisting (admin endpoints)                  | ✅ Active  |     High      |    T3, T5, T10    |
| CORS configuration                                 | ✅ Active  |    Medium     |        T8         |
| CSP headers                                        | ⚠️ Partial |    Medium     |        T8         |
| HSTS                                               | ✅ Active  |     High      |        T6         |

### 6.4 Monitoring & Response

| Control                                   |  Status   | Effectiveness |  Threats Mitigated  |
| ----------------------------------------- | :-------: | :-----------: | :-----------------: |
| Comprehensive audit logging (partitioned) | ✅ Active |     High      |         All         |
| Security event tracking                   | ✅ Active |     High      |         All         |
| Threat detection (7 signal categories)    | ✅ Active |    Medium     | T1, T3, T5, T7, T10 |
| Incident response automation              | ✅ Active |     High      |       T1-T12        |
| Real-time notifications (SignalR)         | ✅ Active |    Medium     |         All         |

## 7. Residual Risk Assessment

After applying existing controls:

|  #  | Threat                 | Inherent Risk | Controls Applied                                       | Residual Risk | Acceptable? |
| :-: | ---------------------- | :-----------: | ------------------------------------------------------ | :-----------: | :---------: |
| T1  | Ransomware             |    15 (H)     | Backup 3-2-1, replication, network segmentation        |   **6 (M)**   |     ✅      |
| T2  | SQL injection          |    10 (M)     | EF Core parameterized, FluentValidation, AES-256, WAF  |   **2 (L)**   |     ✅      |
| T3  | Credential stuffing    |    12 (H)     | MFA, lockout, conditional access, behavior analytics   |   **4 (L)**   |     ✅      |
| T4  | Supply chain           |    10 (M)     | Vendor assessment, SCA scanning, container scanning    |   **6 (M)**   |  ⚠️ Accept  |
| T5  | DDoS                   |     9 (M)     | Rate limiting, network segmentation                    |   **6 (M)**   |  ⚠️ Accept  |
| T6  | MITM                   |     5 (M)     | TLS 1.2+, mTLS, HSTS, AES-256-GCM                      |   **1 (L)**   |     ✅      |
| T7  | Social engineering     |    12 (H)     | MFA, security training, conditional access             |   **6 (M)**   |  ⚠️ Accept  |
| T8  | Web app attack         |     8 (M)     | Input validation, CORS, CSP (partial), HSTS            |   **4 (L)**   |     ✅      |
| T9  | Insider theft          |    10 (M)     | RBAC, field encryption, behavior analytics, audit logs |   **4 (L)**   |     ✅      |
| T10 | Privilege escalation   |     8 (M)     | RBAC, conditional access, behavior analytics           |   **3 (L)**   |     ✅      |
| T11 | Accidental exposure    |     9 (M)     | RBAC, field encryption, audit logging                  |   **4 (L)**   |     ✅      |
| T12 | Unauthorized biometric |     4 (L)     | Consent-based, RBAC, encryption                        |   **1 (L)**   |     ✅      |
| T13 | Hardware failure       |     9 (M)     | Replication, backups, container restart                |   **3 (L)**   |     ✅      |
| T14 | Power outage           |     8 (M)     | Docker auto-restart, backup strategy                   |   **4 (L)**   |  ⚠️ Accept  |
| T15 | Natural disaster       |     5 (M)     | Off-site backups, BCP/DRP                              |   **3 (L)**   |     ✅      |
| T16 | Data corruption        |     8 (M)     | WAL archiving, replication, backups                    |   **3 (L)**   |     ✅      |

## 8. Risk Treatment Plan

### 8.1 Risks Requiring Additional Treatment

|  #  |        Residual Risk        | Treatment                                                | Priority | Target Date | Owner            |
| :-: | :-------------------------: | -------------------------------------------------------- | :------: | :---------: | ---------------- |
| RT1 |    T4 Supply chain (6/M)    | Implement vendor risk assessment framework, SCA pipeline |    P1    |   2026-Q2   | Security Officer |
| RT2 |        T5 DDoS (6/M)        | Evaluate CDN/WAF service (Cloudflare, AWS Shield)        |    P2    |   2026-Q3   | IT Manager       |
| RT3 | T7 Social engineering (6/M) | Security awareness training program, phishing simulation |    P1    |   2026-Q2   | Security Officer |
| RT4 |     T1 Ransomware (6/M)     | Immutable backup storage, isolated recovery environment  |    P1    |   2026-Q2   | IT Manager       |
| RT5 |   T14 Power outage (4/L)    | Evaluate UPS and generator for on-premise deployments    |    P3    |   2026-Q4   | IT Manager       |
| RT6 |      CSP partial (T8)       | Complete Content-Security-Policy header implementation   |    P2    |   2026-Q2   | Engineering      |

### 8.2 Risk Acceptance

The following residual risks are accepted by the Risk Owner:

| Risk                  | Residual Level | Justification                                                                                               | Accepted By |
| --------------------- | :------------: | ----------------------------------------------------------------------------------------------------------- | ----------- |
| T4 Supply chain       |   Medium (6)   | Automated SCA scanning in CI/CD mitigates most risk. Full vendor assessment in progress.                    | ****\_****  |
| T5 DDoS               |   Medium (6)   | Rate limiting + network segmentation provide adequate protection for current scale. CDN evaluation planned. | ****\_****  |
| T7 Social engineering |   Medium (6)   | MFA significantly reduces impact. Training program being implemented.                                       | ****\_****  |
| T14 Power outage      |    Low (4)     | Docker auto-restart and backup strategy cover most scenarios. UPS evaluation for future.                    | ****\_****  |

## 9. Summary

### 9.1 Risk Distribution

| Risk Level | Count | Percentage |
| :--------: | :---: | :--------: |
| Very High  |   0   |     0%     |
|    High    |   0   |     0%     |
|   Medium   |   4   |    25%     |
|    Low     |  12   |    75%     |

### 9.2 Key Findings

1. **Strengths:** The IVF system has comprehensive security controls including MFA (5 methods), advanced encryption (AES-256-GCM), robust audit trail, behavior analytics, and automated incident response.
2. **Areas for Improvement:** Supply chain risk management, DDoS protection, security awareness training, and CSP header completion.
3. **Overall Risk Posture:** **LOW** — The system demonstrates mature security practices with extensive defense-in-depth. No residual risks rated High or Very High.

### 9.3 Recommendations

1. Execute Risk Treatment Plan items RT1-RT4 within the specified timelines
2. Conduct annual reassessment (or after any significant system change)
3. Perform quarterly reviews of threat landscape updates (MITRE ATT&CK)
4. Consider external penetration testing to validate control effectiveness
5. Implement security awareness training program with annual completion requirement

---

**Approval:**

| Role                        | Name                 | Date         | Signature  |
| --------------------------- | -------------------- | ------------ | ---------- |
| Security Officer (Assessor) | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| IT Manager                  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Clinical Director           | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| CEO/Director (Risk Owner)   | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
