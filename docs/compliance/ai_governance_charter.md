# AI Governance Charter

**Document ID:** IVF-AI-GOV-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** AI Governance Board  
**Review Cycle:** Annual  
**Frameworks:** NIST AI RMF, ISO 42001:2023

---

## 1. Purpose

This charter establishes the governance framework for AI and machine learning systems used within the IVF Information System. It defines the principles, oversight structure, risk management approach, and accountability mechanisms for responsible AI use.

## 2. Scope

### 2.1 AI/ML Systems in Scope

|  #  | System                         | Purpose                                                                   | Type                               | Risk Level |
| :-: | ------------------------------ | ------------------------------------------------------------------------- | ---------------------------------- | :--------: |
|  1  | **ThreatDetectionService**     | Security threat identification using 7 signal categories                  | Rule-based + statistical           |   Medium   |
|  2  | **BehavioralAnalyticsService** | User behavior anomaly detection using z-score analysis (30-day baselines) | Statistical ML                     |   Medium   |
|  3  | **BotDetectionService**        | Automated bot/scanner detection via UA inspection + reCAPTCHA             | Rule-based + external API          |    Low     |
|  4  | **ContextualAuthService**      | Risk-based adaptive authentication with step-up MFA                       | Rule-based scoring                 |   Medium   |
|  5  | **Biometric Matching**         | Fingerprint identification via DigitalPersona SDK                         | Third-party ML (template matching) |    High    |

### 2.2 AI Use Case Classification

| Use Case             |    Clinical Decision Support?     |     Data Subject Impact     |  Human Override?   |
| -------------------- | :-------------------------------: | :-------------------------: | :----------------: |
| Threat Detection     |                No                 |  Account lockout possible   | Yes (admin review) |
| Behavioral Analytics |                No                 | Session revocation possible | Yes (admin review) |
| Bot Detection        |                No                 |      Request blocking       | Yes (IP whitelist) |
| Contextual Auth      |                No                 |    MFA step-up required     | Yes (admin bypass) |
| Biometric Matching   | Indirect (patient identification) |      Access grant/deny      |  Yes (manual ID)   |

**Important:** No AI/ML system is used for clinical treatment decisions. All clinical decisions are made by qualified medical professionals.

## 3. AI Governance Principles

### 3.1 Core Principles

1. **Safety First** — AI systems must not compromise patient safety or system security
2. **Human Oversight** — All automated decisions must have human review and override capability
3. **Transparency** — AI decision factors must be logged and explainable
4. **Fairness** — AI systems must not discriminate based on demographics, geography, or other protected characteristics
5. **Privacy** — AI systems must comply with HIPAA/GDPR data protection requirements
6. **Accountability** — Clear ownership and responsibility for each AI system
7. **Robustness** — AI systems must handle adversarial inputs and edge cases gracefully

### 3.2 Ethical Guidelines

- AI is used exclusively for security protection and operational efficiency
- No automated profiling of patients for non-clinical purposes
- Biometric data processed only with explicit consent (GDPR Art. 9(2)(a))
- Behavioral analytics data used only for security, not performance evaluation

## 4. AI Governance Board

### 4.1 Composition

| Role                         | Responsibility                             | Member |
| ---------------------------- | ------------------------------------------ | ------ |
| **Chair** (Security Officer) | Overall AI governance, risk decisions      | [Name] |
| **Medical Representative**   | Patient safety implications                | [Name] |
| **IT/Engineering Lead**      | Technical implementation, model management | [Name] |
| **DPO/Privacy Officer**      | GDPR/HIPAA compliance, data protection     | [Name] |
| **Quality Assurance**        | Testing, validation, bias review           | [Name] |

### 4.2 Meeting Schedule

| Meeting              | Frequency     | Agenda                                                |
| -------------------- | ------------- | ----------------------------------------------------- |
| AI Governance Review | Quarterly     | Risk assessment, performance metrics, incident review |
| AI Risk Assessment   | Semi-annually | Updated risk register, new AI use cases               |
| Emergency Review     | As needed     | Critical AI incidents or regulatory changes           |

### 4.3 Decision Authority

| Decision                    | Authority                | Quorum |
| --------------------------- | ------------------------ | :----: |
| New AI system deployment    | Board approval required  |  3/5   |
| AI model parameter changes  | Chair + Engineering Lead |  2/5   |
| AI system decommissioning   | Board approval required  |  3/5   |
| Emergency AI system disable | Chair (sole authority)   |  1/5   |
| Bias investigation          | Chair + DPO              |  2/5   |

## 5. AI Risk Register

|   #   | Risk                                            | Likelihood |  Impact  | Risk Level | Mitigation                                                       | Owner       |
| :---: | ----------------------------------------------- | :--------: | :------: | :--------: | ---------------------------------------------------------------- | ----------- |
| AI-R1 | False positive threat blocking legitimate users |   Medium   |  Medium  | **Medium** | Tunable thresholds, admin review, false positive workflow        | Engineering |
| AI-R2 | False negative missing real threats             |    Low     | Critical |  **High**  | Multiple signal correlation, severity escalation, manual review  | Security    |
| AI-R3 | Geolocation bias in threat detection            |    Low     |   High   | **Medium** | Bias testing, configurable country allow/block lists             | DPO         |
| AI-R4 | Biometric false match/non-match                 |  Very Low  | Critical | **Medium** | DigitalPersona certified algorithms, dual-ID verification        | Engineering |
| AI-R5 | Behavioral baseline poisoning                   |    Low     |   High   | **Medium** | Baseline recalculation limits, admin reset capability            | Security    |
| AI-R6 | Third-party AI vendor vulnerability             |    Low     |   High   | **Medium** | Vendor assessment, contract SLAs, alternative providers          | Chair       |
| AI-R7 | Model drift over time                           |   Medium   |  Medium  | **Medium** | Performance monitoring, threshold review, periodic recalibration | Engineering |
| AI-R8 | Inadequate explainability                       |   Medium   |  Medium  | **Medium** | ThreatIndicators logging, user-facing explanations (planned)     | Engineering |

## 6. AI Performance Metrics

### 6.1 Key Performance Indicators

| Metric                       |  Target   | Measurement                                      | Frequency |
| ---------------------------- | :-------: | ------------------------------------------------ | --------- |
| Threat detection accuracy    |   >95%    | True positive / (True positive + False negative) | Monthly   |
| False positive rate          |    <5%    | False positive / Total flagged                   | Monthly   |
| Mean time to detect (MTTD)   |  <5 min   | Detection timestamp - event timestamp            | Monthly   |
| Mean time to respond (MTTR)  |  <15 min  | Response timestamp - detection timestamp         | Monthly   |
| Behavioral anomaly precision |   >90%    | Validated anomalies / Total anomalies            | Quarterly |
| Biometric match accuracy     |  >99.9%   | Correct matches / Total attempts                 | Quarterly |
| User appeal resolution       | <24 hours | Time from appeal to resolution                   | Monthly   |

### 6.2 Fairness Metrics (Bias Testing)

| Metric            | Measurement                            |    Target     | Frequency |
| ----------------- | -------------------------------------- | :-----------: | --------- |
| Geographic parity | Flag rate variance across countries    | <10% variance | Quarterly |
| Temporal parity   | Flag rate variance across time zones   | <15% variance | Quarterly |
| Device parity     | Flag rate variance across device types | <10% variance | Quarterly |
| Role parity       | Flag rate variance across user roles   | <5% variance  | Quarterly |

## 7. AI System Lifecycle

### 7.1 Stages

```
Design → Develop → Test → Validate → Deploy → Monitor → Review → Retire
```

### 7.2 Stage Requirements

| Stage        | Requirements                                             | Artifacts              |
| ------------ | -------------------------------------------------------- | ---------------------- |
| **Design**   | Use case charter, risk assessment, stakeholder approval  | Design document        |
| **Develop**  | Follow secure coding guidelines, version control         | Source code, changelog |
| **Test**     | Unit tests, integration tests, bias tests                | Test results           |
| **Validate** | Performance benchmarks, false positive/negative analysis | Validation report      |
| **Deploy**   | Board approval, rollback plan, monitoring setup          | Deployment plan        |
| **Monitor**  | KPI tracking, alert thresholds, incident response        | Dashboard, alerts      |
| **Review**   | Quarterly performance review, bias audit                 | Review report          |
| **Retire**   | Decommission plan, data cleanup, archive                 | Retirement record      |

### 7.3 Model Versioning

All AI system parameters and thresholds must be:

- Version-controlled in Git
- Documented with rationale for changes
- Reviewed by at least one AI Governance Board member before deployment
- Rollback-capable to previous version

## 8. Third-Party AI Assessment

| Vendor           | AI Component                   | Last Assessment | Next Assessment | Risk Level |
| ---------------- | ------------------------------ | :-------------: | :-------------: | :--------: |
| DigitalPersona   | Fingerprint matching algorithm |   ****\_****    |   ****\_****    |    High    |
| Google reCAPTCHA | Bot detection API              |   ****\_****    |   ****\_****    |    Low     |
| Fido2Net         | WebAuthn authentication        |   ****\_****    |   ****\_****    |   Medium   |

### Assessment Criteria

1. Algorithm transparency (documentation quality)
2. Bias testing results (vendor-provided)
3. Security certifications (SOC 2, ISO 27001)
4. Data processing practices (GDPR compliance)
5. Incident response capabilities
6. Contract SLAs for accuracy and uptime

## 9. User Transparency & Recourse

### 9.1 Transparency

Users affected by AI decisions will be informed:

- **Why** their access was flagged (threat indicators in SecurityEvent)
- **What** action was taken (account lock, MFA step-up, etc.)
- **How** to appeal (contact admin, false positive report)

### 9.2 Appeal Process

```
User blocked → Contact admin → Admin reviews SecurityEvent →
  ├─ False Positive → Mark as FalsePositive, restore access
  └─ True Positive → Maintain block, explain remediation steps
```

## 10. Compliance Mapping

| Requirement            | NIST AI RMF | ISO 42001  | Implementation                         |
| ---------------------- | :---------: | :--------: | -------------------------------------- |
| AI Governance          | GOVERN 1-6  |  Clause 5  | This charter + AI Governance Board     |
| Risk Assessment        |   MAP 1-5   |  Clause 6  | AI Risk Register (Section 5)           |
| Performance Metrics    | MEASURE 1-4 |  Clause 9  | KPIs (Section 6)                       |
| Bias Testing           |  MEASURE 3  | Annex A.8  | Fairness Metrics (Section 6.2)         |
| Lifecycle Management   | MANAGE 1-4  |  Clause 8  | AI Lifecycle (Section 7)               |
| Human Oversight        |  GOVERN 2   | Annex A.10 | Board structure + appeal process       |
| Transparency           |  GOVERN 4   | Annex A.8  | ThreatIndicators logging + user notice |
| Third-party management |  GOVERN 5   | Annex A.7  | Vendor Assessment (Section 8)          |

---

**Approval:**

| Role                      | Name                 | Date         | Signature  |
| ------------------------- | -------------------- | ------------ | ---------- |
| AI Governance Board Chair | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Medical Representative    | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| IT/Engineering Lead       | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO                       | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| CEO/Director              | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

---

## Related Documents — Compliance Documentation Suite

### Comprehensive Guides

- [Compliance Master Guide](compliance_master_guide.md) — Executive overview, program structure, governance, glossary
- [Compliance Flows & Procedures](compliance_flows_and_procedures.md) — Detailed workflows for DSR, breach, AI governance, incident response
- [Implementation & Deployment Guide](compliance_implementation_deployment.md) — Practical deployment, configuration, runbooks, DR procedures
- [Evaluation & Audit Guide](compliance_evaluation_audit.md) — Scoring methodology, health score, maturity model, audit preparation
- [Standards Mapping Matrix](compliance_standards_mapping.md) — Cross-framework traceability (HIPAA, GDPR, SOC 2, ISO 27001, HITRUST, NIST AI, ISO 42001)

### Related Policy & Governance

- [Information Security Policy](information_security_policy.md)
- [DPO Charter](dpo_charter.md)
- [AI Governance Charter](ai_governance_charter.md)
- [Privacy Notice](privacy_notice.md)
