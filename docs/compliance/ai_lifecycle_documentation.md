# AI System Lifecycle Documentation

**Document ID:** IVF-AI-LC-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Standards Reference:** ISO 42001 Clause 8, NIST AI RMF (GOVERN, MAP, MEASURE, MANAGE)

---

## 1. AI Systems Inventory

### 1.1 System Registry

|   ID   | System Name               |           Type           | Risk Level |   Status   | Owner         |
| :----: | ------------------------- | :----------------------: | :--------: | :--------: | ------------- |
| AI-001 | Threat Detection Engine   | Security Classification  |    High    | Production | Security Team |
| AI-002 | Behavioral Analytics      |    Anomaly Detection     |    High    | Production | Security Team |
| AI-003 | Biometric Matcher         | Fingerprint Verification |    High    | Production | Security Team |
| AI-004 | Bot Detection             |  Traffic Classification  |   Medium   | Production | Security Team |
| AI-005 | Contextual Authentication |       Risk Scoring       |   Medium   | Production | Security Team |

### 1.2 System Classification

All AI systems in the IVF platform are **security-focused** — none make clinical treatment decisions. They operate in the access control and system security domain.

**Impact Assessment:**

- **No clinical impact:** AI systems do not influence diagnosis, treatment selection, or patient outcomes
- **Access impact:** May restrict user access based on security threat assessment
- **Override available:** All automated decisions can be overridden by authorized administrators

---

## 2. AI System Lifecycle Stages

### 2.1 Stage 1: Problem Definition & Scoping (GOVERN)

| Aspect                          | Requirements                                                       |
| ------------------------------- | ------------------------------------------------------------------ |
| **Business need documentation** | Written justification for AI use vs. rule-based alternative        |
| **Stakeholder identification**  | Security team, clinical staff, patients (indirect), administrators |
| **Risk categorization**         | Per ISO 42001 Annex A and NIST AI RMF risk taxonomy                |
| **Ethical review**              | DPO review for privacy implications. Bias impact pre-assessment    |
| **Approval**                    | AI Governance Board (Security Lead + DPO + Clinical Director)      |

**Gate Criteria:** Documented business case, risk categorization, ethical pre-assessment approved.

### 2.2 Stage 2: Data Collection & Preparation (MAP)

| Aspect                 | Requirements                                                                        |
| ---------------------- | ----------------------------------------------------------------------------------- |
| **Data sources**       | Only internal system logs, security events, and explicitly consented biometric data |
| **Data quality**       | Completeness >95%, accuracy validation, bias screening                              |
| **Privacy compliance** | DPIA completed if processing special categories. Minimization applied               |
| **Representativeness** | Training data must include diverse demographic groups                               |
| **Documentation**      | Data dictionary, source lineage, preprocessing steps, feature engineering rationale |

**Current Data Sources:**
| AI System | Data Source | Volume | Special Categories |
|-----------|-----------|:------:|:------------------:|
| Threat Detection | SecurityEvent table | ~10K events/month | No |
| Behavioral Analytics | UserBehaviorProfile, login patterns | Per-user profiles | No |
| Biometric Matcher | Fingerprint templates | Per-patient (opt-in) | Yes (Art. 9) |
| Bot Detection | HTTP request patterns | Request metadata | No |
| Contextual Auth | Session context (IP, device, time) | Per-login | No |

**Gate Criteria:** Data quality report, bias screening report, DPIA (if applicable).

### 2.3 Stage 3: Model Development & Training (MAP)

| Aspect                    | Requirements                                                              |
| ------------------------- | ------------------------------------------------------------------------- |
| **Algorithm selection**   | Documented rationale for algorithm choice                                 |
| **Training methodology**  | Train/validation/test split documented. Cross-validation where applicable |
| **Hyperparameter tuning** | Grid/random search with documented parameters                             |
| **Baseline comparison**   | Performance vs. rule-based baseline                                       |
| **Explainability**        | SHAP/LIME feature importance for all classification models                |
| **Version control**       | Model artifacts versioned and stored                                      |

**Current Model Approaches:**
| AI System | Approach | Explainability |
|-----------|----------|:-------------:|
| Threat Detection | Rule-based + scoring (7 signal categories) | Transparent rules |
| Behavioral Analytics | Z-score statistical model | Feature-level scores |
| Biometric Matcher | DigitalPersona SDK (vendor) | Match probability |
| Bot Detection | Heuristic rules + rate analysis | Rule-based transparent |
| Contextual Auth | Multi-factor risk scoring | Factor breakdown |

**Gate Criteria:** Model performance metrics, explainability report, algorithm justification.

### 2.4 Stage 4: Testing & Validation (MEASURE)

| Aspect                  | Requirements                                                          |
| ----------------------- | --------------------------------------------------------------------- |
| **Performance metrics** | Accuracy, Precision, Recall, F1, FPR, FNR per demographic group       |
| **Bias testing**        | Fairness testing across protected attributes (age, gender, ethnicity) |
| **Fairness threshold**  | Four-fifths rule (disparate impact ratio ≥ 0.80 or disparity ≤ 25%)   |
| **Adversarial testing** | Test against known evasion techniques                                 |
| **Red team exercise**   | Security-focused AI systems must undergo red team evaluation          |
| **User acceptance**     | Sample of affected users validates acceptable FPR                     |

**Required Test Protocol:**

```
For each AI system:
1. Collect test dataset (minimum 1000 samples per demographic group)
2. Calculate confusion matrix (TP, FP, TN, FN) per group
3. Compute FPR, FNR, Accuracy, Precision, Recall, F1
4. Calculate disparity ratios (FPR_group / FPR_baseline)
5. Apply four-fifths rule threshold
6. Document results in AiBiasTestResult entity
7. If fail: remediate and re-test
8. If pass: proceed to deployment gate
```

**Gate Criteria:** All demographic groups pass fairness threshold. FPR < 5%. FNR < 10%. Red team report.

### 2.5 Stage 5: Deployment (MANAGE)

| Aspect                 | Requirements                                         |
| ---------------------- | ---------------------------------------------------- |
| **Deployment plan**    | Staged rollout (canary → 10% → 50% → 100%)           |
| **Rollback procedure** | Automated rollback if FPR exceeds threshold          |
| **Monitoring setup**   | Real-time FPR/FNR dashboards operational             |
| **Human override**     | Override mechanism verified and documented           |
| **User notification**  | Privacy notice updated to reflect AI decision-making |
| **Incident response**  | AI-specific incident response runbook created        |

**Gate Criteria:** Monitoring active, rollback tested, privacy notice updated.

### 2.6 Stage 6: Monitoring & Continuous Evaluation (MANAGE)

| Aspect                    | Requirements                                             |  Frequency  |
| ------------------------- | -------------------------------------------------------- | :---------: |
| **FPR/FNR tracking**      | Monitor via `/api/ai/fpr-fnr/trends` endpoint            | Continuous  |
| **Bias re-testing**       | Run full bias test suite                                 |  Quarterly  |
| **Model drift detection** | Compare current vs. baseline metrics                     |   Monthly   |
| **Fairness audit**        | Independent review of AI fairness                        |   Annual    |
| **Stakeholder feedback**  | Collect feedback from affected users                     |  Quarterly  |
| **Transparency report**   | Publish via `/api/ai/explainability/transparency-report` | Semi-annual |

**Alert Thresholds:**
| Metric | Warning | Critical | Action |
|--------|:-------:|:--------:|--------|
| FPR | > 3% | > 5% | Investigate → Retrain |
| FNR | > 7% | > 10% | Investigate → Retrain |
| Disparity Ratio | < 0.85 | < 0.80 | Bias review → Remediate |
| Accuracy | < 92% | < 90% | Model review → Retrain |

### 2.7 Stage 7: Retirement / Decommission

| Aspect                  | Requirements                                                                  |
| ----------------------- | ----------------------------------------------------------------------------- |
| **Retirement criteria** | Performance below threshold, replaced by superior system, no longer needed    |
| **Data handling**       | Training data retained per retention policy. Model artifacts archived 3 years |
| **Notification**        | Stakeholders notified 30 days before retirement                               |
| **Documentation**       | Retirement report with lessons learned                                        |
| **Transition**          | Alternative system validated before retirement                                |

---

## 3. Roles & Responsibilities

| Role                        | Responsibilities                                                   |
| --------------------------- | ------------------------------------------------------------------ |
| **AI Governance Board**     | Approve new AI systems, review fairness reports, policy decisions  |
| **AI System Owner**         | Day-to-day oversight, performance monitoring, incident escalation  |
| **Data Protection Officer** | Privacy review, DPIA oversight, consent management                 |
| **Security Lead**           | Threat model review, adversarial testing coordination              |
| **Clinical Director**       | Validate no clinical impact, approve patient-facing AI disclosures |
| **Development Team**        | Implementation, testing, monitoring setup, bias testing execution  |

---

## 4. AI Risk Management Framework

### 4.1 Risk Categories (per NIST AI RMF)

| Category           | Description                            | Mitigation                                 |
| ------------------ | -------------------------------------- | ------------------------------------------ |
| **Reliability**    | System produces inconsistent results   | Automated monitoring, regression testing   |
| **Fairness**       | Disparate impact on demographic groups | Quarterly bias testing, four-fifths rule   |
| **Transparency**   | Users cannot understand decisions      | Explainability endpoints, factor breakdown |
| **Privacy**        | Excessive data collection or inference | Data minimization, DPIA, consent           |
| **Security**       | Adversarial manipulation of AI         | Red team testing, input validation         |
| **Accountability** | No clear ownership or oversight        | AI Governance Board, documented roles      |

### 4.2 Current Risk Assessment

| AI System            | Reliability | Fairness | Transparency | Privacy | Security |  Overall   |
| -------------------- | :---------: | :------: | :----------: | :-----: | :------: | :--------: |
| Threat Detection     |     Low     |  Medium  |     Low      |   Low   |  Medium  |   Medium   |
| Behavioral Analytics |     Low     |  Medium  |     Low      |   Low   |   Low    | Low-Medium |
| Biometric Matcher    |     Low     |   High   |    Medium    |  High   |  Medium  |    High    |
| Bot Detection        |     Low     |   Low    |     Low      |   Low   |   Low    |    Low     |
| Contextual Auth      |     Low     |  Medium  |     Low      |   Low   |   Low    | Low-Medium |

---

## 5. Integration with IVF System

### 5.1 API Endpoints for AI Governance

| Endpoint                                           | Purpose                               |
| -------------------------------------------------- | ------------------------------------- |
| `POST /api/ai/bias-tests`                          | Record bias test results              |
| `GET /api/ai/bias-tests/summary`                   | View bias test summary by system      |
| `GET /api/ai/fpr-fnr/trends`                       | FPR/FNR trends over time              |
| `GET /api/ai/fpr-fnr/dashboard`                    | Comprehensive AI governance dashboard |
| `GET /api/ai/explainability/event/{id}`            | Explain a specific security decision  |
| `GET /api/ai/explainability/user/{userId}/blocked` | All blocked events for a user         |
| `GET /api/ai/explainability/transparency-report`   | System-wide transparency report       |

### 5.2 Data Model

The `AiBiasTestResult` entity captures:

- Confusion matrix (TP, FP, TN, FN)
- Performance metrics (FPR, FNR, Accuracy, Precision, Recall, F1)
- Fairness metrics (Disparity ratios, four-fifths rule pass/fail)
- Protected attributes tested (age, gender, ethnicity, nationality, disability)
- SHAP feature importance values
- Remediation actions taken

---

## 6. Document Control

| Version |    Date    | Author              | Changes                            |
| :-----: | :--------: | ------------------- | ---------------------------------- |
|   1.0   | 2026-03-03 | AI Governance Board | Initial AI Lifecycle Documentation |

**Approved by:**

| Role                      | Name                 | Date         | Signature  |
| ------------------------- | -------------------- | ------------ | ---------- |
| AI Governance Board Chair | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO                       | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Clinical Director         | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

---

## Related Documents — Compliance Documentation Suite

### Comprehensive Guides

- [Compliance Master Guide](compliance_master_guide.md) — Executive overview, program structure, governance, glossary
- [Compliance Flows & Procedures](compliance_flows_and_procedures.md) — Detailed workflows for DSR, breach, AI governance, incident response
- [Implementation & Deployment Guide](compliance_implementation_deployment.md) — Practical deployment, configuration, runbooks, DR procedures
- [Evaluation & Audit Guide](compliance_evaluation_audit.md) — Scoring methodology, health score, maturity model, audit preparation
- [Standards Mapping Matrix](compliance_standards_mapping.md) — Cross-framework traceability (HIPAA, GDPR, SOC 2, ISO 27001, HITRUST, NIST AI, ISO 42001)

### Related Technical

- [Vanta Integration Guide](vanta_integration_guide.md)
- [AI Lifecycle Documentation](ai_lifecycle_documentation.md)
- [AI Governance Charter](ai_governance_charter.md)
