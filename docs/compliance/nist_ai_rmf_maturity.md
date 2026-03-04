# NIST AI RMF 1.0 — Maturity Assessment

**Document ID:** IVF-NIST-AI-001  
**Version:** 1.0  
**Assessment Date:** 2026-03-03  
**Organization:** [Clinic Name] — IVF Information System  
**Assessor:** AI Governance Committee  
**Framework:** NIST AI Risk Management Framework 1.0 (AI 100-1)

---

## 1. Executive Summary

This maturity assessment evaluates the IVF Information System's AI components against the NIST AI Risk Management Framework (AI RMF) 1.0 across all four core functions: GOVERN, MAP, MEASURE, and MANAGE.

**AI Components in Scope:**

1. Biometric Patient Identification (DigitalPersona fingerprint matching)
2. AI-Assisted Clinical Decision Support (threshold-based recommendations)
3. Automated Queue Management (priority scoring algorithm)

**Overall Maturity: Level 3 — Defined (Target: Level 4 — Managed by Q4 2026)**

| Function    | Sub-categories | Maturity | Target  |
| ----------- | :------------: | :------: | :-----: |
| GOVERN      |       6        |   3.5    |   4.0   |
| MAP         |       5        |   3.0    |   4.0   |
| MEASURE     |       4        |   3.2    |   4.0   |
| MANAGE      |       4        |   2.8    |   3.5   |
| **Overall** |     **19**     | **3.1**  | **3.9** |

**Maturity Scale:**
1 = Initial (ad hoc) | 2 = Repeatable | 3 = Defined | 4 = Managed | 5 = Optimizing

---

## 2. GOVERN Function Assessment

The GOVERN function establishes policies, processes, procedures, and organizational structures for AI risk management.

### GV.1 — Policies, Processes, Procedures, and Practices

| Sub-category | Requirement                                   | Evidence                                                                    | Maturity |
| :----------: | --------------------------------------------- | --------------------------------------------------------------------------- | :------: |
|    GV.1.1    | Legal/regulatory requirements identified      | GDPR Art. 22, AI Act risk classification documented                         |  **4**   |
|    GV.1.2    | AI risk management integrated into enterprise | AI Governance Charter (IVF-AI-GOV-001), ComplianceScoringEngine includes AI |  **3**   |
|    GV.1.3    | Processes to address AI risks                 | AI lifecycle documentation, bias testing framework, model versioning        |  **4**   |
|    GV.1.4    | Risk management embedded in org culture       | Compliance training includes AI module                                      |  **3**   |

### GV.2 — Accountability Structures

| Sub-category | Requirement                    | Evidence                                                                | Maturity |
| :----------: | ------------------------------ | ----------------------------------------------------------------------- | :------: |
|    GV.2.1    | Roles/responsibilities defined | AI Governance Charter defines committee, roles                          |  **4**   |
|    GV.2.2    | Accountability mechanisms      | Audit trail on all AI decisions, AiModelVersion entity tracks approvals |  **3**   |

### GV.3 — Workforce Diversity, Equity, Inclusion

| Sub-category | Requirement                          | Evidence                                                | Maturity |
| :----------: | ------------------------------------ | ------------------------------------------------------- | :------: |
|    GV.3.1    | Diverse perspectives in AI lifecycle | Documented in AI Governance Charter                     |  **3**   |
|    GV.3.2    | AI workforce competency              | ComplianceTraining entity tracks AI training completion |  **3**   |

### GV.4 — Organizational Context

| Sub-category | Requirement                  | Evidence                                                 | Maturity |
| :----------: | ---------------------------- | -------------------------------------------------------- | :------: |
|    GV.4.1    | Mission/context alignment    | Clinical safety objectives in AI Governance Charter      |  **4**   |
|    GV.4.2    | Stakeholder needs documented | Patient safety, clinical efficacy, regulatory compliance |  **3**   |

**GOVERN Summary:** Maturity 3.5 — Strong governance framework with documented charter, defined roles, and integrated risk management. Gap: Formalize diverse review board composition.

---

## 3. MAP Function Assessment

The MAP function categorizes, contextualizes, and characterizes AI risks.

### MP.1 — Context and Characterization

| Sub-category | Requirement                  | Evidence                                                        | Maturity |
| :----------: | ---------------------------- | --------------------------------------------------------------- | :------: |
|    MP.1.1    | Intended purposes documented | AI lifecycle documentation covers purpose per system            |  **3**   |
|    MP.1.2    | Interdependencies mapped     | System architecture in CLAUDE.md, integration points documented |  **3**   |
|    MP.1.3    | Benefits and costs assessed  | Risk-benefit analysis in DPIA for biometric system              |  **3**   |

### MP.2 — Classification and Categorization

| Sub-category | Requirement                     | Evidence                                            | Maturity |
| :----------: | ------------------------------- | --------------------------------------------------- | :------: |
|    MP.2.1    | AI systems classified by risk   | Classification matrix in AI lifecycle documentation |  **3**   |
|    MP.2.2    | Trustworthiness characteristics | Mapped to NIST AI RMF trustworthiness criteria      |  **3**   |

### MP.3 — AI Risks Identified

| AI System                 |     Risk Category     | Risk Level | Mitigation                                             |
| ------------------------- | :-------------------: | :--------: | ------------------------------------------------------ |
| Biometric matching        | Privacy, civil rights |  **High**  | Opt-in consent, fallback to manual ID, bias testing    |
| Clinical decision support |   Safety, accuracy    |  **High**  | Human-in-the-loop, explainability, override capability |
| Queue management          |   Fairness, equity    | **Medium** | Priority transparency, manual override                 |

### MP.4 — Impacts to Individuals, Groups, Organizations

| Sub-category | Requirement             | Evidence                                       | Maturity |
| :----------: | ----------------------- | ---------------------------------------------- | :------: |
|    MP.4.1    | Impact assessment       | DPIA covers biometric system impacts           |  **3**   |
|    MP.4.2    | Likelihood and severity | Threat assessment in risk assessment framework |  **3**   |

**MAP Summary:** Maturity 3.0 — AI risks identified and characterized for all three systems. Gap: Formalize ongoing risk re-assessment cadence and expand impact analysis to all AI components.

---

## 4. MEASURE Function Assessment

The MEASURE function quantifies, assesses, benchmarks, and monitors AI risks and related impacts.

### MS.1 — Appropriate Methods and Metrics

| Sub-category | Requirement            | Evidence                                                        | Maturity |
| :----------: | ---------------------- | --------------------------------------------------------------- | :------: |
|    MS.1.1    | Measurement approaches | AiBiasTestResult entity measures FPR/FNR/demographic parity     |  **4**   |
|    MS.1.2    | Metrics defined        | AiModelVersion tracks Accuracy, Precision, Recall, F1, FPR, FNR |  **4**   |
|    MS.1.3    | Thresholds established | ThresholdsJson in AiModelVersion, configurable per system       |  **3**   |

### MS.2 — AI Systems Evaluated

| Metric             |    Biometric Matching     | Clinical Decision Support | Queue Management  |
| ------------------ | :-----------------------: | :-----------------------: | :---------------: |
| Accuracy           |           99.7%           |     N/A (rule-based)      |        N/A        |
| FPR                |          0.001%           |             —             |         —         |
| FNR                |           0.3%            |             —             |         —         |
| Demographic Parity | Tested (AiBiasTestResult) |            N/A            |      Pending      |
| Explainability     |    Feature importance     |     Rule transparency     | Priority factors  |
| Human Override     |   ✅ Manual ID fallback   |    ✅ Doctor override     | ✅ Staff override |

### MS.3 — Tracking and Documentation

| Sub-category | Requirement                     | Evidence                                                        | Maturity |
| :----------: | ------------------------------- | --------------------------------------------------------------- | :------: |
|    MS.3.1    | Performance monitored over time | AiModelVersion changelog per system                             |  **3**   |
|    MS.3.2    | Measurement updates             | Model version lifecycle (Draft→PendingReview→Approved→Deployed) |  **3**   |

### MS.4 — Feedback Mechanisms

| Sub-category | Requirement         | Evidence                                                | Maturity |
| :----------: | ------------------- | ------------------------------------------------------- | :------: |
|    MS.4.1    | Feedback collected  | Incident reporting via SecurityIncident entity          |  **3**   |
|    MS.4.2    | Feedback integrated | IncidentResponseRule auto-action with AI review trigger |  **2**   |

**MEASURE Summary:** Maturity 3.2 — Strong bias testing and metrics framework. Model versioning provides lifecycle tracking. Gap: Automate feedback loop from incidents to model re-training triggers.

---

## 5. MANAGE Function Assessment

The MANAGE function allocates resources and plans, responds to, and recovers from AI risks.

### MG.1 — Allocation of Resources

| Sub-category | Requirement                   | Evidence                                              | Maturity |
| :----------: | ----------------------------- | ----------------------------------------------------- | :------: |
|    MG.1.1    | Resources for risk management | AI governance committee in charter                    |  **3**   |
|    MG.1.2    | Processes for risk response   | AiModelVersion rollback capability, incident response |  **3**   |

### MG.2 — Risk Response

| Sub-category | Requirement                       | Evidence                                                | Maturity |
| :----------: | --------------------------------- | ------------------------------------------------------- | :------: |
|    MG.2.1    | Response plans                    | Rollback endpoint, previous version tracking            |  **3**   |
|    MG.2.2    | Risk mitigation                   | Bias test required before deployment, approval workflow |  **3**   |
|    MG.2.3    | Mechanisms to supersede/disengage | Biometric fallback to manual, clinical override         |  **3**   |

### MG.3 — Risk Treatment and Documentation

| Sub-category | Requirement                  | Evidence                                                       | Maturity |
| :----------: | ---------------------------- | -------------------------------------------------------------- | :------: |
|    MG.3.1    | Risks documented and treated | Risk assessment framework, AI-specific risks in DPIA           |  **3**   |
|    MG.3.2    | Post-deployment monitoring   | AiModelVersion tracks deployment status, metrics over versions |  **2**   |

### MG.4 — Communication and Reporting

| Sub-category | Requirement               | Evidence                                                    | Maturity |
| :----------: | ------------------------- | ----------------------------------------------------------- | :------: |
|    MG.4.1    | Stakeholder communication | AI transparency in privacy notice, explainability endpoints |  **3**   |
|    MG.4.2    | AI risk reporting         | AI dashboard endpoint aggregates all systems' performance   |  **2**   |

**MANAGE Summary:** Maturity 2.8 — Rollback and override mechanisms in place. Gap: Implement continuous post-deployment monitoring with automated alerting and structured AI risk reporting dashboard.

---

## 6. Trustworthiness Characteristics Assessment

| Characteristic                  | Implementation                                                    | Maturity |
| ------------------------------- | ----------------------------------------------------------------- | :------: |
| **Valid & Reliable**            | Metrics tracking (Accuracy, F1), version control                  |  **3**   |
| **Safe**                        | Human-in-the-loop for all clinical decisions, fallback mechanisms |  **4**   |
| **Secure & Resilient**          | AES-256, MFA, audit logs, rollback capability                     |  **4**   |
| **Accountable & Transparent**   | AI Governance Charter, audit trail, explainability                |  **3**   |
| **Explainable & Interpretable** | Explainability endpoints, rule transparency                       |  **3**   |
| **Privacy-Enhanced**            | GDPR-compliant, consent management, DPIA                          |  **4**   |
| **Fair — Harmful Bias Managed** | AiBiasTestResult, demographic parity testing, FPR/FNR monitoring  |  **3**   |

---

## 7. Gap Remediation Roadmap

|  #  | Gap                                  | Function | Current | Target | Action                                            | Timeline |
| :-: | ------------------------------------ | :------: | :-----: | :----: | ------------------------------------------------- | :------: |
|  1  | Automated post-deployment monitoring |  MANAGE  |    2    |   4    | Real-time model performance alerting              | Q2 2026  |
|  2  | AI risk dashboard                    |  MANAGE  |    2    |   4    | Structured reporting with trend analysis          | Q2 2026  |
|  3  | Incident-to-retrain feedback loop    | MEASURE  |    2    |   4    | Automated triggers from incidents to model review | Q3 2026  |
|  4  | Queue management bias testing        | MEASURE  |    1    |   3    | Extend AiBiasTestResult to queue algorithms       | Q2 2026  |
|  5  | Risk re-assessment cadence           |   MAP    |    2    |   4    | Quarterly AI risk re-assessment process           | Q2 2026  |
|  6  | Diverse review board                 |  GOVERN  |    2    |   4    | Formalize patient/community representation        | Q3 2026  |
|  7  | AI Act preparedness                  |  GOVERN  |    2    |   3    | Classify systems under EU AI Act risk tiers       | Q3 2026  |

---

## 8. Cross-Reference to ISO 42001

|        ISO 42001 Clause         | NIST AI RMF Function |   Status   |
| :-----------------------------: | :------------------: | :--------: |
| 4 — Context of the organization |     GOVERN, MAP      | ✅ Aligned |
|         5 — Leadership          |        GOVERN        | ✅ Aligned |
|          6 — Planning           |         MAP          | ✅ Aligned |
|           7 — Support           |        GOVERN        | ✅ Aligned |
|          8 — Operation          |   MEASURE, MANAGE    | 🔶 Partial |
|   9 — Performance evaluation    |       MEASURE        | ✅ Aligned |
|        10 — Improvement         |        MANAGE        | 🔶 Partial |

---

## 9. Document Control

| Version |    Date    | Author                  | Changes                                 |
| :-----: | :--------: | ----------------------- | --------------------------------------- |
|   1.0   | 2026-03-03 | AI Governance Committee | Initial NIST AI RMF maturity assessment |

**Approved by:**

| Role               | Name                 | Date         | Signature  |
| ------------------ | -------------------- | ------------ | ---------- |
| AI Governance Lead | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Clinical Director  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO                | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
