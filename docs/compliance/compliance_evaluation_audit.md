# Compliance Evaluation & Audit Guide

> **Document ID:** IVF-CMP-CEA-001  
> **Version:** 1.0  
> **Effective Date:** 2026-03-04  
> **Classification:** CONFIDENTIAL  
> **Owner:** Compliance Officer / Internal Auditor  
> **Review Cycle:** Quarterly

---

## Table of Contents

1. [Scoring Methodology](#1-scoring-methodology)
2. [Framework-Specific Assessment Criteria](#2-framework-specific-assessment-criteria)
3. [Health Score Calculation](#3-health-score-calculation)
4. [Maturity Model](#4-maturity-model)
5. [Self-Assessment Procedures](#5-self-assessment-procedures)
6. [Internal Audit Process](#6-internal-audit-process)
7. [External Audit Preparation](#7-external-audit-preparation)
8. [Evidence Collection & Management](#8-evidence-collection--management)
9. [Gap Analysis & Remediation](#9-gap-analysis--remediation)
10. [Continuous Improvement Metrics](#10-continuous-improvement-metrics)
11. [Certification Roadmap](#11-certification-roadmap)
12. [Benchmarking](#12-benchmarking)

---

## 1. Scoring Methodology

### 1.1 Overall Compliance Score

Điểm tuân thủ tổng thể được tính dựa trên **weighted average** của 7 framework:

$$\text{Overall Score} = \sum_{i=1}^{7} w_i \times s_i$$

Trong đó:

| Framework ($i$) | Weight ($w_i$) | Score ($s_i$) | Rationale                                        |
| :-------------: | :------------: | :-----------: | ------------------------------------------------ |
|      HIPAA      |      0.20      |      94%      | Highest weight — mandatory for healthcare PHI    |
|      GDPR       |      0.18      |      92%      | High weight — sensitive health data (Art. 9)     |
|      SOC 2      |      0.18      |      90%      | High weight — customer trust & audit requirement |
|    ISO 27001    |      0.15      |      90%      | Important — international ISMS standard          |
|     HITRUST     |      0.12      |      84%      | Moderate — healthcare-specific integration       |
|   NIST AI RMF   |      0.10      |      84%      | Moderate — growing AI governance importance      |
|    ISO 42001    |      0.07      |      80%      | Lower — newer standard, still developing         |

$$\text{Overall} = 0.20(94) + 0.18(92) + 0.18(90) + 0.15(90) + 0.12(84) + 0.10(84) + 0.07(80) = 88.96 \approx 89\%$$

### 1.2 Per-Control Scoring

Mỗi control được đánh giá theo scale 4 cấp:

| Score | Status                   | Definition                                               | Evidence Required            |
| :---: | ------------------------ | -------------------------------------------------------- | ---------------------------- |
|   0   | ❌ Not Implemented       | Control không tồn tại                                    | None                         |
|   1   | 🟡 Partially Implemented | Control tồn tại nhưng chưa đầy đủ hoặc chưa hiệu quả     | Design documentation         |
|   2   | 🟢 Fully Implemented     | Control hoạt động đúng thiết kế                          | Design + operating evidence  |
|   3   | ⭐ Optimized             | Control được tự động hóa, đo lường, và cải tiến liên tục | Design + operating + metrics |

### 1.3 Framework Score Calculation

$$\text{Framework Score} = \frac{\sum_{j=1}^{n} c_j \times p_j}{\sum_{j=1}^{n} c_{max} \times p_j} \times 100\%$$

Trong đó:

- $c_j$ = điểm của control $j$ (0-3)
- $c_{max}$ = 3 (điểm tối đa)
- $p_j$ = priority weight của control $j$
- $n$ = tổng số controls

---

## 2. Framework-Specific Assessment Criteria

### 2.1 HIPAA (94% — Target: 95%)

#### Administrative Safeguards (§164.308)

| Control                                           | Requirement                       | IVF Implementation                                | Score |
| ------------------------------------------------- | --------------------------------- | ------------------------------------------------- | :---: |
| Risk Analysis §164.308(a)(1)(ii)(A)               | Conduct risk assessment           | `risk_assessment.md` + ComplianceScoringEngine    | 3 ⭐  |
| Risk Management §164.308(a)(1)(ii)(B)             | Implement measures to reduce risk | Zero Trust + encryption + access control          | 3 ⭐  |
| Sanction Policy §164.308(a)(1)(ii)(C)             | Sanctions for non-compliance      | HR policy + automated lockouts                    | 2 🟢  |
| Information System Activity §164.308(a)(1)(ii)(D) | Review audit logs                 | Partitioned audit logs + daily review task        | 3 ⭐  |
| Workforce Security §164.308(a)(3)                 | Authorization & supervision       | RBAC + Zero Trust + behavioral analytics          | 3 ⭐  |
| Security Awareness §164.308(a)(5)                 | Training program                  | ComplianceTraining entity + TrainingManagement UI | 3 ⭐  |
| Incident Procedures §164.308(a)(6)                | Response procedures               | IncidentResponseService + auto-response rules     | 3 ⭐  |
| Contingency Plan §164.308(a)(7)                   | BCP/DRP                           | `bcp_drp.md` + 3-2-1 backup + WAL                 | 3 ⭐  |
| Business Associates §164.308(b)(1)                | BAA contracts                     | `vendor_risk_assessment.md`                       | 2 🟢  |

#### Technical Safeguards (§164.312)

| Control                           | Requirement                                   | IVF Implementation                  | Score |
| --------------------------------- | --------------------------------------------- | ----------------------------------- | :---: |
| Access Control §164.312(a)        | Unique user ID, emergency access, auto-logoff | JWT + session management + timeout  | 3 ⭐  |
| Audit Controls §164.312(b)        | Hardware/software audit mechanisms            | Partitioned PostgreSQL audit logs   | 3 ⭐  |
| Integrity Controls §164.312(c)    | ePHI integrity mechanisms                     | DB constraints + digital signing    | 3 ⭐  |
| Person Authentication §164.312(d) | Verify identity                               | JWT + MFA + biometrics + Zero Trust | 3 ⭐  |
| Transmission Security §164.312(e) | Encryption in transit                         | TLS 1.3 enforced                    | 3 ⭐  |

#### Gap: §164.310 Physical Safeguards — Score: 2 🟢

- Docker container isolation provides logical separation
- Physical server access documented but relies on hosting provider
- **Remediation:** Physical access audit of server location, formalize physical security policy

### 2.2 GDPR (92% — Target: 95%)

#### Principles & Rights

|  Article   | Requirement                      | IVF Implementation                                                                                                                        | Score |
| :--------: | -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | :---: |
|   Art. 5   | Data processing principles       | All 7 principles implemented (lawfulness, purpose limitation, data minimization, accuracy, storage limitation, integrity, accountability) | 3 ⭐  |
|   Art. 6   | Lawful basis for processing      | Treatment (legitimate interest) + explicit consent for secondary purposes                                                                 | 3 ⭐  |
|   Art. 9   | Special categories (health data) | Art. 9(2)(h) health provision + explicit consent for research                                                                             | 3 ⭐  |
| Art. 12-14 | Transparency & notices           | `privacy_notice.md` (bilingual EN/VI)                                                                                                     | 3 ⭐  |
|  Art. 15   | Right of access                  | DSR Management → Access type                                                                                                              | 3 ⭐  |
|  Art. 16   | Right to rectification           | DSR Management → Rectification type                                                                                                       | 3 ⭐  |
|  Art. 17   | Right to erasure                 | DSR Management → Erasure + HIPAA conflict resolution                                                                                      | 2 🟢  |
|  Art. 18   | Right to restriction             | `Patient.RestrictProcessing()`                                                                                                            | 3 ⭐  |
|  Art. 20   | Right to data portability        | DSR Management → Portability type (FHIR format)                                                                                           | 2 🟢  |
|  Art. 22   | Automated decision-making        | No fully automated decisions affecting patients                                                                                           | 3 ⭐  |
|  Art. 25   | Data protection by design        | Encryption, pseudonymization, minimal collection                                                                                          | 3 ⭐  |
|  Art. 30   | ROPA                             | `ropa_register.md` + ProcessingActivity entity                                                                                            | 3 ⭐  |
| Art. 33-34 | Breach notification              | BreachNotification entity + SOP                                                                                                           | 3 ⭐  |
|  Art. 35   | DPIA                             | `dpia.md` completed                                                                                                                       | 3 ⭐  |
| Art. 37-39 | DPO                              | `dpo_charter.md`                                                                                                                          | 3 ⭐  |
| Art. 44-49 | International transfers          | `standard_contractual_clauses.md` (all local, SCCs ready)                                                                                 | 2 🟢  |

### 2.3 SOC 2 Type II (90% — Target: 92%)

|    Trust Services Criteria    | IVF Implementation                                        | Score | Evidence                              |
| :---------------------------: | --------------------------------------------------------- | :---: | ------------------------------------- |
|    CC1 Control Environment    | Governance structure, roles defined, policies published   | 3 ⭐  | information_security_policy.md        |
|   CC2 Communication & Info    | Privacy notice, internal comms, incident alerts           | 2 🟢  | privacy_notice.md, SignalR hubs       |
|      CC3 Risk Assessment      | Formal risk assessment, ComplianceScoringEngine           | 3 ⭐  | risk_assessment.md                    |
|   CC4 Monitoring Activities   | Continuous monitoring, health dashboard, automated alerts | 3 ⭐  | ComplianceMonitoringEndpoints         |
|    CC5 Control Activities     | IT general controls, change management, code review       | 2 🟢  | GitHub PR process                     |
| CC6 Logical & Physical Access | JWT + RBAC + Zero Trust + MFA + biometrics                | 3 ⭐  | Auth pipeline                         |
|     CC7 System Operations     | Vulnerability management, incident response, audit trail  | 3 ⭐  | IncidentResponseService               |
|     CC8 Change Management     | Version control, migration procedures, testing            | 2 🟢  | Git + EF migrations                   |
|      CC9 Risk Mitigation      | Vendor management, BCP/DRP, redundancy                    | 3 ⭐  | vendor_risk_assessment.md, bcp_drp.md |
|        A1 Availability        | 3-2-1 backup, WAL, replication, monitoring                | 3 ⭐  | BackupComplianceService               |

**Type II Observation Period:** 6-12 months of continuous operation. During this period, auditors sample controls at random intervals. **Status:** Ready to begin observation period.

### 2.4 ISO 27001:2022 (90% — Target: 93%)

#### Mandatory Clauses (4-10)

| Clause | Area                    | IVF Implementation                              | Score |
| :----: | ----------------------- | ----------------------------------------------- | :---: |
|   4    | Context of Organization | Scope defined, interested parties identified    | 3 ⭐  |
|   5    | Leadership              | CISO + CO + DPO appointed, policy statement     | 2 🟢  |
|   6    | Planning                | Risk assessment + treatment plan                | 3 ⭐  |
|   7    | Support                 | Resources, competence, awareness, communication | 2 🟢  |
|   8    | Operation               | Controls implemented, changes managed           | 3 ⭐  |
|   9    | Performance Evaluation  | Internal audit + management review              | 2 🟢  |
|   10   | Improvement             | Nonconformity handling, corrective actions      | 2 🟢  |

#### Annex A Controls (93 total, summary)

|      Control Group      | Controls |     Implemented     | Score |
| :---------------------: | :------: | :-----------------: | :---: |
| A.5 Organizational (37) |    37    | 34 fully, 3 partial |  89%  |
|     A.6 People (8)      |    8     | 7 fully, 1 partial  |  91%  |
|    A.7 Physical (14)    |    14    | 11 fully, 3 partial |  82%  |
| A.8 Technological (34)  |    34    | 32 fully, 2 partial |  94%  |

### 2.5 HITRUST CSF v11 (84% — Target: 88%)

#### Maturity Levels

| Level | Name        | Criteria                                       |   Current   |
| :---: | ----------- | ---------------------------------------------- | :---------: |
|   1   | Policy      | Documented policies exist                      |     ✅      |
|   2   | Procedures  | Formal procedures documented                   |     ✅      |
|   3   | Implemented | Controls operating as designed                 |     ✅      |
|   4   | Measured    | Metrics tracked, effectiveness evaluated       |  ✅ (Most)  |
|   5   | Optimized   | Continuous improvement, automated optimization | 🔄 (Target) |

#### Assessment Tier Progress

|       Tier       | Controls |     Status      | Next Steps              |
| :--------------: | :------: | :-------------: | ----------------------- |
|  e1 (Essential)  |    44    |   ✅ Achieved   | Maintain                |
| i1 (Implemented) |   150+   | 🔄 84% complete | Close 24 remaining gaps |
| r2 (Risk-based)  |   300+   |    ⏳ Future    | After i1 certification  |

### 2.6 NIST AI RMF 1.0 (84% — Target: 90%)

| Function    | Sub-Categories |     Maturity      | Key Gaps                             |
| ----------- | :------------: | :---------------: | ------------------------------------ |
| **GOVERN**  |     GV.1-7     | Level 3 (Defined) | Need formal AI Board charter         |
| **MAP**     |     MP.1-5     | Level 3 (Defined) | Context mapping for all 5 AI systems |
| **MEASURE** |     MS.1-7     | Level 3 (Defined) | More diverse bias metrics needed     |
| **MANAGE**  |     MG.1-4     | Level 3 (Defined) | Automated remediation for bias drift |

**Level Definitions:**

| Level | Name       | Criteria                                              |
| :---: | ---------- | ----------------------------------------------------- | -------- |
|   1   | Ad Hoc     | AI risk managed inconsistently                        |
|   2   | Managed    | Basic AI risk processes exist                         |
|   3   | Defined    | Formal AI governance program documented and operating |
|   4   | Measured   | AI risk quantitatively measured and tracked           | ← Target |
|   5   | Optimizing | Continuous AI governance improvement                  |

### 2.7 ISO 42001 (80% — Target: 85%)

| Clause | Requirement              | Status | Gap                                                         |
| :----: | ------------------------ | :----: | ----------------------------------------------------------- |
|   4    | Context                  |   ✅   | —                                                           |
|   5    | Leadership & AI Policy   |   🟡   | Formal AI Quality Policy needed                             |
|   6    | Planning (AI Risk)       |   ✅   | —                                                           |
|   7    | Support (AI competence)  |   🟡   | AI Ethics training coverage at 60%                          |
|   8    | Operation (AI lifecycle) |   ✅   | —                                                           |
|   9    | Performance Evaluation   |   🟡   | Quarterly AI effectiveness review not yet formalized        |
|   10   | Improvement              |   🟡   | Corrective action process for AI issues needs documentation |

---

## 3. Health Score Calculation

### 3.1 Formula

$$\text{Health Score} = \sum_{k=1}^{5} w_k \times \text{component}_k$$

### 3.2 Component Calculations

#### Component 1: DSR Compliance (Weight: 25%)

$$\text{DSR Score} = \begin{cases} 100 & \text{if total DSR = 0} \\ \frac{\text{DSR}_{active} - \text{DSR}_{overdue}}{\text{DSR}_{active}} \times 100 & \text{otherwise} \end{cases}$$

| Metric        | Source API                                  | Example |
| ------------- | ------------------------------------------- | ------- |
| Active DSRs   | `GET /api/compliance/dsr?status=InProgress` | 5       |
| Overdue DSRs  | `GET /api/compliance/dsr?overdue=true`      | 0       |
| **DSR Score** | $(5-0)/5 \times 100$                        | **100** |

#### Component 2: Task Completion (Weight: 25%)

$$\text{Task Score} = \frac{\text{Tasks}_{active} - \text{Tasks}_{overdue}}{\text{Tasks}_{active}} \times 100$$

| Metric         | Source API                                   | Example  |
| -------------- | -------------------------------------------- | -------- |
| Active tasks   | `GET /api/compliance/schedule?status=Active` | 45       |
| Overdue tasks  | `GET /api/compliance/schedule/overdue`       | 3        |
| **Task Score** | $(45-3)/45 \times 100$                       | **93.3** |

#### Component 3: Security Incidents (Weight: 20%)

$$\text{Security Score} = \max\left(0, 100 - (\text{Open}_{critical} \times 30 + \text{Open}_{high} \times 15 + \text{Open}_{medium} \times 5)\right)$$

| Metric             | Source API                                            | Example |
| ------------------ | ----------------------------------------------------- | ------- |
| Open Critical      | SecurityIncident where severity=Critical, status=Open | 0       |
| Open High          | SecurityIncident where severity=High, status=Open     | 1       |
| Open Medium        | SecurityIncident where severity=Medium, status=Open   | 0       |
| **Security Score** | $100 - (0 \times 30 + 1 \times 15 + 0 \times 5)$      | **85**  |

#### Component 4: Training Rate (Weight: 15%)

$$\text{Training Score} = \frac{\text{Completed}}{\text{Total Assigned}} \times 100$$

| Metric             | Source API                                | Example  |
| ------------------ | ----------------------------------------- | -------- |
| Completed          | ComplianceTraining where isCompleted=true | 42       |
| Total Assigned     | ComplianceTraining total count            | 48       |
| **Training Score** | $42/48 \times 100$                        | **87.5** |

#### Component 5: AI Bias Pass Rate (Weight: 15%)

$$\text{AI Score} = \frac{\text{Tests Passing}}{\text{Total Tests}} \times 100$$

| Metric        | Source API                                          | Example |
| ------------- | --------------------------------------------------- | ------- |
| Tests Passing | AiBiasTestResult where passesFairnessThreshold=true | 17      |
| Total Tests   | AiBiasTestResult total count                        | 20      |
| **AI Score**  | $17/20 \times 100$                                  | **85**  |

#### Final Score Calculation

$$\text{Health} = 0.25(100) + 0.25(93.3) + 0.20(85) + 0.15(87.5) + 0.15(85)$$
$$= 25 + 23.33 + 17 + 13.125 + 12.75 = 91.2$$

**Status: ✅ Healthy** (≥ 90)

### 3.3 Status Thresholds

| Score Range |  Status  | Indicator | Action                             |
| :---------: | :------: | :-------: | ---------------------------------- |
|   90-100    | Healthy  | ✅ Green  | Maintain current level             |
|    70-89    | Warning  | ⚠️ Amber  | Review and remediate within 7 days |
|    0-69     | Critical |  🔴 Red   | Emergency review within 24 hours   |

---

## 4. Maturity Model

### 4.1 IVF Compliance Maturity Model (5 Levels)

```
Level 5 ┃  ╔══════════════════╗
        ┃  ║   OPTIMIZING     ║  Continuous improvement, predictive compliance
        ┃  ║  Auto-remediate   ║  AI-driven risk prediction
        ┃  ╚══════════════════╝  ML-based anomaly detection
        ┃
Level 4 ┃  ╔══════════════════╗
        ┃  ║   MEASURED       ║  KPIs tracked, trends analyzed  ◄── CURRENT TARGET
        ┃  ║  Quantitative    ║  Statistical process control
        ┃  ╚══════════════════╝  Evidence-based decisions
        ┃
Level 3 ┃  ╔══════════════════╗
        ┃  ║   DEFINED        ║  Formal processes documented     ◄── CURRENT STATE
        ┃  ║  Standardized    ║  Organization-wide standards
        ┃  ╚══════════════════╝  Training programs operational
        ┃
Level 2 ┃  ╔══════════════════╗
        ┃  ║   MANAGED        ║  Reactive, project-level
        ┃  ║  Repeatable      ║  Basic controls in place
        ┃  ╚══════════════════╝
        ┃
Level 1 ┃  ╔══════════════════╗
        ┃  ║   AD HOC         ║  No formal process
        ┃  ║  Unpredictable   ║  Individual-dependent
        ┃  ╚══════════════════╝
```

### 4.2 Domain Maturity Assessment

| Domain               | Current Level | Target Level | Gap Actions                                            |
| -------------------- | :-----------: | :----------: | ------------------------------------------------------ |
| Security Controls    |  4 Measured   | 5 Optimizing | Automated remediation, predictive analytics            |
| Data Protection      |   3 Defined   |  4 Measured  | Quantitative privacy metrics, automated DSAR tracking  |
| AI Governance        |   3 Defined   |  4 Measured  | Continuous bias monitoring dashboards, drift detection |
| Incident Response    |  4 Measured   | 4 (maintain) | Already measuring MTTR, incident counts                |
| Training & Awareness |   3 Defined   |  4 Measured  | Training effectiveness metrics beyond completion rate  |
| Vendor Management    |   3 Defined   | 3 (maintain) | Formalize SLA monitoring                               |
| Asset Management     |   3 Defined   |  4 Measured  | Automated asset discovery, real-time classification    |
| Audit & Compliance   |   3 Defined   |  4 Measured  | Automated evidence collection, continuous audit        |

### 4.3 Level Transition Criteria

**Level 3 → Level 4 (Defined → Measured):**

- [ ] All compliance KPIs defined with quantitative targets
- [ ] Health score trending data available (90-day history minimum)
- [ ] Statistical analysis of security events (trends, seasonality)
- [ ] Training effectiveness measured (not just completion)
- [ ] AI model performance tracked with SLOs
- [ ] Vendor SLA compliance measured
- [ ] Management review based on data, not just reports

**Level 4 → Level 5 (Measured → Optimizing):**

- [ ] Predictive compliance risk scoring
- [ ] Automated remediation for common issues
- [ ] ML-based anomaly detection in compliance data
- [ ] Self-healing compliance controls
- [ ] Real-time regulatory change impact analysis
- [ ] Automated evidence collection for all audit requirements

---

## 5. Self-Assessment Procedures

### 5.1 Quarterly Self-Assessment Process

| Week | Activity                           | Responsible        | Deliverable                 |
| :--: | ---------------------------------- | ------------------ | --------------------------- |
|  1   | Review all framework scores        | Compliance Officer | Score update per framework  |
|  1   | Check all control implementations  | System Admin       | Control status report       |
|  2   | Review open findings & remediation | CISO               | Finding tracker update      |
|  2   | Verify training compliance rates   | HR                 | Training status report      |
|  3   | Test incident response procedures  | Security team      | Tabletop exercise report    |
|  3   | Review vendor compliance status    | Procurement        | Vendor status report        |
|  4   | Update documentation               | All departments    | Updated policies/procedures |
|  4   | Management briefing                | Compliance Officer | Quarterly compliance report |

### 5.2 Self-Assessment Checklist

#### Security Controls (Monthly)

- [ ] Review Zero Trust middleware metrics (blocks, challenges, passes)
- [ ] Verify TLS certificates expiration dates (> 60 days remaining)
- [ ] Check API key rotation status (< 90 days old)
- [ ] Review conditional access policy effectiveness
- [ ] Verify rate limiting is operational
- [ ] Check encryption key rotation schedule

#### Data Protection (Monthly)

- [ ] Count active/overdue DSRs → verify 0 overdue
- [ ] Review consent records for completeness
- [ ] Verify data retention schedules executing correctly
- [ ] Check pseudonymization procedures operational
- [ ] Review ROPA for accuracy

#### AI Governance (Monthly)

- [ ] Run bias tests on all 5 production models
- [ ] Review FPR/FNR trends for each model
- [ ] Verify model version registry is current
- [ ] Check AI Ethics Committee meeting minutes
- [ ] Review any model incidents or rollbacks

---

## 6. Internal Audit Process

### 6.1 Audit Cycle

```
Plan → Execute → Report → Remediate → Verify → Close
 │        │         │          │           │        │
 ▼        ▼         ▼          ▼           ▼        ▼
Scope   Evidence  Findings   Action      Re-test  Archive
Charter  Sampling  Matrix    Plans       Controls  Records
```

### 6.2 Audit Planning

| Parameter | Value                                                            |
| --------- | ---------------------------------------------------------------- |
| Frequency | Annual (minimum), Semi-annual (recommended)                      |
| Duration  | 5-10 business days                                               |
| Scope     | ISO 27001 Annex A controls + framework-specific requirements     |
| Auditor   | Internal auditor (independent from audited function) or external |
| Standards | ISO 19011:2018 (audit guidelines)                                |

### 6.3 Audit Sampling Strategy

| Population Size | Sample Size | Method                                |
| :-------------: | :---------: | ------------------------------------- |
|   1-10 items    |    100%     | Review all                            |
|   11-50 items   |  10 items   | Random sampling                       |
|  51-200 items   |  15 items   | Stratified random                     |
|  201-500 items  |  25 items   | Stratified random                     |
|   500+ items    |  40 items   | Statistical sampling (95% confidence) |

### 6.4 Finding Classification

|     Classification      | Definition                                                  | Resolution Timeline |
| :---------------------: | ----------------------------------------------------------- | :-----------------: |
| **Major Nonconformity** | Control missing or completely ineffective; systemic failure |       30 days       |
| **Minor Nonconformity** | Control exists but partially ineffective; isolated failure  |       90 days       |
|     **Observation**     | Area for improvement; not a nonconformity                   |  Next review cycle  |
|     **Opportunity**     | Best practice suggestion                                    |      Optional       |

### 6.5 Internal Audit Report Template

```markdown
## Internal Audit Report — [Framework] [Date]

### 1. Audit Scope & Objectives

- Frameworks covered: [...]
- Controls sampled: [.../total]
- Period covered: [...]

### 2. Executive Summary

- Total findings: X
- Major nonconformities: X
- Minor nonconformities: X
- Observations: X
- Overall opinion: [Satisfactory / Needs Improvement / Unsatisfactory]

### 3. Findings

#### Finding #1

- Control: [Control ID]
- Classification: [Major/Minor/Observation]
- Description: [...]
- Evidence: [...]
- Root Cause: [...]
- Recommendation: [...]
- Responsible: [...]
- Due Date: [...]

### 4. Previous Findings Follow-Up

- [Finding ID] [Status: Closed/Open/In Progress]

### 5. Conclusion & Recommendations
```

---

## 7. External Audit Preparation

### 7.1 ISO 27001 Certification

#### Stage 1 Audit (Documentation Review)

| Evidence Category | Documents                        |              Status               |
| :---------------: | -------------------------------- | :-------------------------------: |
|         1         | ISMS Scope Statement             | ✅ information_security_policy.md |
|         2         | Information Security Policy      | ✅ information_security_policy.md |
|         3         | Risk Assessment Methodology      |       ✅ risk_assessment.md       |
|         4         | Statement of Applicability (SoA) |  ✅ Derived from control mapping  |
|         5         | Risk Treatment Plan              |     ✅ risk_assessment.md §4      |
|         6         | Internal Audit Procedure         |    ✅ internal_audit_report.md    |
|         7         | Management Review Records        |    🟡 Need formalized minutes     |
|         8         | Competence Records               |   ✅ ComplianceTraining records   |

#### Stage 2 Audit (Implementation Verification)

| Area                | Evidence Source                  | Collection Method |
| ------------------- | -------------------------------- | :---------------: |
| Access Control      | JWT logs, RBAC configs           |    API export     |
| Incident Management | SecurityIncident records         |     DB query      |
| Business Continuity | BCP test results, backup logs    |   Manual + API    |
| Change Management   | Git history, PR reviews          |    GitHub API     |
| Cryptography        | TLS configs, encryption settings | System inspection |
| Asset Management    | AssetInventory records           |    API export     |
| Supplier Management | Vendor risk assessments          |  Document review  |
| Audit Logging       | Sample audit trail entries       |     DB query      |

### 7.2 SOC 2 Type II

#### Observation Period Requirements

| Criterion                 | Evidence Collection Method          |  Frequency  |
| ------------------------- | ----------------------------------- | :---------: |
| CC1 (Control Environment) | Policy review, org charts           |  Quarterly  |
| CC2 (Communication)       | Security awareness records          |   Monthly   |
| CC3 (Risk Assessment)     | Risk assessment updates             |  Quarterly  |
| CC4 (Monitoring)          | Health score history, alert logs    | Continuous  |
| CC5 (Control Activities)  | Change tickets, deployment logs     | Per change  |
| CC6 (Access Control)      | Access reviews, provisioning logs   |   Monthly   |
| CC7 (System Operations)   | Incident logs, vulnerability scans  |   Weekly    |
| CC8 (Change Management)   | Git commits, test results           | Per release |
| CC9 (Risk Mitigation)     | BCP tests, vendor reviews           | Semi-annual |
| A1 (Availability)         | Uptime records, backup verification |    Daily    |

### 7.3 HITRUST Assessment

#### e1 (Essential) — ✅ Achieved

|          Domain          | Controls | Status |
| :----------------------: | :------: | :----: |
|      Access Control      |   5/5    |   ✅   |
|  Audit & Accountability  |   4/4    |   ✅   |
| Configuration Management |   3/3    |   ✅   |
|  Identification & Auth   |   4/4    |   ✅   |
|    Incident Response     |   3/3    |   ✅   |
|     Risk Management      |   4/4    |   ✅   |
|     Security Program     |   5/5    |   ✅   |
|    System Protection     |   8/8    |   ✅   |
|          Other           |   8/8    |   ✅   |

#### i1 (Implemented) — 🔄 In Progress (84%)

Focus areas to reach i1: Privacy practices, media protection, physical security, personnel security

---

## 8. Evidence Collection & Management

### 8.1 Evidence Repository Structure

```
docs/compliance/              → Policy & assessment documents
├── evidence/                 → Audit evidence (create per audit)
│   ├── access_control/       → User lists, role configs, MFA logs
│   ├── incident_response/    → Incident tickets, response times
│   ├── training/             → Completion records, test scores
│   ├── change_management/    → Git history, deployment records
│   ├── encryption/           → TLS configs, cipher suites
│   ├── backup/               → Backup logs, restore tests
│   ├── vendor/               → BAAs, risk assessments, due diligence
│   └── policy_versions/      → All policy revision history (via Git)
```

### 8.2 Automated Evidence Collection

| Evidence             | API Source                                    |    Automation    |
| -------------------- | --------------------------------------------- | :--------------: |
| User access list     | `GET /api/admin/users`                        | Script per audit |
| Active permissions   | RBAC configuration                            |     DB query     |
| Audit trail sample   | `GET /api/audit/logs?startDate=X&endDate=Y`   |      Script      |
| DSR handling records | `GET /api/compliance/dsr?page=1&pageSize=100` |      Script      |
| Training completions | `GET /api/compliance/training?completed=true` |      Script      |
| Security incidents   | `GET /api/security/enterprise/incidents`      |      Script      |
| Health score history | `GET /api/compliance/monitoring/health`       |    Scheduled     |
| AI bias test results | `GET /api/compliance/ai/bias-tests`           |      Script      |
| Asset inventory      | `GET /api/compliance/assets`                  |      Script      |
| Backup compliance    | `GET /api/admin/data-backup/compliance`       |      Script      |

### 8.3 Evidence Retention

| Evidence Type             |            Retention             | Format             |
| ------------------------- | :------------------------------: | ------------------ |
| Audit reports             |             7 years              | PDF + markdown     |
| Evidence artifacts        |        3 years post-audit        | Mixed              |
| Policy versions           |            Indefinite            | Git history        |
| Corrective action records |             5 years              | Structured records |
| Management review minutes |             7 years              | Meeting notes      |
| Training records          | Duration of employment + 3 years | DB records         |

---

## 9. Gap Analysis & Remediation

### 9.1 Current Gap Summary

| Framework   | Score | Gaps | Priority | Remediation                                                             |
| ----------- | :---: | :--: | :------: | ----------------------------------------------------------------------- |
| HIPAA       |  94%  |  1   |    P2    | Formalize physical security policy                                      |
| GDPR        |  92%  |  3   |    P2    | FHIR portability format, cross-border SCC execution, erasure edge cases |
| SOC 2       |  90%  |  2   |    P2    | Change management formalization, CC2 communication metrics              |
| ISO 27001   |  90%  |  4   |    P1    | Management review formalization, physical security Annex A.7 gaps       |
| HITRUST     |  84%  |  6   |    P1    | Privacy practices, media protection, personnel security for i1          |
| NIST AI RMF |  84%  |  4   |    P2    | Formal AI Board, automated bias remediation, Level 4 metrics            |
| ISO 42001   |  80%  |  5   |    P2    | AI Quality Policy, effectiveness review process, corrective actions     |

### 9.2 Remediation Tracking

| Gap ID  | Framework | Description                           | Owner    |     Status     | Target Date |
| :-----: | --------- | ------------------------------------- | -------- | :------------: | :---------: |
| GAP-001 | ISO 27001 | Formalize management review meetings  | CO       | 🟡 In Progress |   2026-Q2   |
| GAP-002 | HITRUST   | Implement media protection controls   | IT Admin | 🟡 In Progress |   2026-Q2   |
| GAP-003 | NIST AI   | Establish formal AI Governance Board  | CISO     | ❌ Not Started |   2026-Q2   |
| GAP-004 | ISO 42001 | Create AI Quality Management Policy   | AI Lead  | ❌ Not Started |   2026-Q3   |
| GAP-005 | GDPR      | Implement FHIR export for portability | Dev Team | ❌ Not Started |   2026-Q3   |
| GAP-006 | SOC 2     | Formalize change management process   | Dev Lead | 🟡 In Progress |   2026-Q2   |

### 9.3 Remediation Prioritization Matrix

```
                    Impact
            Low          High
         ┌──────────┬──────────┐
    Low  │  P4      │  P2      │
 Effort  │  Defer   │  Plan    │
         ├──────────┼──────────┤
    High │  P3      │  P1      │
         │  Queue   │  Prioritize│
         └──────────┴──────────┘
```

---

## 10. Continuous Improvement Metrics

### 10.1 Key Performance Indicators (KPIs)

| KPI                             |        Target         | Frequency | Source                        |
| ------------------------------- | :-------------------: | :-------: | ----------------------------- |
| Overall compliance score        |         ≥ 88%         |  Monthly  | ComplianceScoringEngine       |
| Health score                    |         ≥ 90          | Real-time | ComplianceMonitoringEndpoints |
| DSR response time (avg)         |       ≤ 15 days       |  Monthly  | DSR records                   |
| DSR overdue rate                |          0%           |  Weekly   | DSR dashboard                 |
| Security incident MTTR          |         ≤ 24h         |  Monthly  | SecurityIncident records      |
| Training completion rate        |         ≥ 90%         |  Monthly  | ComplianceTraining records    |
| AI bias test pass rate          |         ≥ 90%         |  Monthly  | AiBiasTestResult records      |
| Asset classification coverage   |         100%          | Quarterly | AssetInventory records        |
| Policy review timeliness        |     100% current      | Quarterly | Document reviews              |
| Vendor risk assessment coverage | 100% critical vendors | Quarterly | vendor_risk_assessment.md     |

### 10.2 Trend Analysis

| Metric              | Q4 2025 | Q1 2026 | Q2 2026 (Target) |    Trend    |
| ------------------- | :-----: | :-----: | :--------------: | :---------: |
| Overall score       |   78%   |   88%   |       92%        | ↗ Improving |
| HIPAA               |   82%   |   94%   |       95%        |      ↗      |
| GDPR                |   75%   |   92%   |       95%        |      ↗      |
| DSR avg response    | 22 days | 12 days |     10 days      |      ↗      |
| MTTR                |   48h   |   18h   |       12h        |      ↗      |
| Training completion |   72%   |   87%   |       92%        |      ↗      |

### 10.3 Continuous Improvement Process

```
Measure → Analyze → Plan → Execute → Verify
   │          │        │       │         │
   ▼          ▼        ▼       ▼         ▼
Collect    Root     Action  Implement  Confirm
KPIs &     cause    plans   changes    improvement
metrics    analysis
```

---

## 11. Certification Roadmap

### 11.1 Certification Timeline

```
          2026 Q1          2026 Q2          2026 Q3          2026 Q4
    ┌─────────────────┬─────────────────┬─────────────────┬─────────────────┐
ISO │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│▓▓▓▓▓▓▓▓░░░░░░░│░░░░░░░░░░░░░░░│                 │
27001│Prep complete     │Stage 1  Stage 2│ Certification  │ Surveillance    │
    ├─────────────────┼─────────────────┼─────────────────┼─────────────────┤
SOC │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│░░░░░░░░░░░░░░░│
2   │ Controls ready   │ --- Observation Period (Type II) -│ Report issued   │
    ├─────────────────┼─────────────────┼─────────────────┼─────────────────┤
HIT │ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│░░░░░░░░░░░░░░░│                 │                 │
RUST│ e1 achieved      │ i1 certification│                 │                 │
    ├─────────────────┼─────────────────┼─────────────────┼─────────────────┤
NIST│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│░░░░░░░░░░░░░░░│                 │
AI  │ Level 3 achieved │ Level 4 gaps    │ Level 4 assessed│                 │
    └─────────────────┴─────────────────┴─────────────────┴─────────────────┘

▓▓▓ = In progress   ░░░ = Planned   (blank) = Not scheduled
```

### 11.2 Certification Cost Estimates

|      Certification      |  Estimated Cost   |            Recurring            | Notes                         |
| :---------------------: | :---------------: | :-----------------------------: | ----------------------------- |
|  ISO 27001 (Stage 1+2)  |  $15,000-30,000   | $8,000-15,000/yr (surveillance) | Depends on certification body |
|      SOC 2 Type II      |  $30,000-75,000   |        $20,000-50,000/yr        | CPA firm engagement           |
|       HITRUST i1        |  $20,000-40,000   |       $15,000-30,000/2yr        | Assessor + MyCSF platform     |
|  HIPAA (self-assessed)  |   $0 (internal)   |          $0 (internal)          | No formal certification       |
| GDPR (DPA registration) | Varies by country |             Annual              | Supervisory authority fees    |

---

## 12. Benchmarking

### 12.1 Industry Benchmarks (Healthcare SaaS)

| Metric                    |  IVF System  | Industry Average | Top Quartile |          Gap           |
| ------------------------- | :----------: | :--------------: | :----------: | :--------------------: |
| HIPAA score               |     94%      |       82%        |     92%      | +2% above top quartile |
| GDPR score                |     92%      |       75%        |     88%      | +4% above top quartile |
| SOC 2 controls            |     90%      |       78%        |     88%      | +2% above top quartile |
| MTTR (security incidents) |     18h      |       72h        |     24h      |   Above top quartile   |
| DSR response time         |   12 days    |     25 days      |   15 days    |  Exceeding benchmark   |
| Training completion       |     87%      |       72%        |     85%      |   +2% above average    |
| Data encryption (at rest) |     100%     |       68%        |     95%      |     Best-in-class      |
| MFA adoption              | 100% (staff) |       54%        |     90%      |     Best-in-class      |

### 12.2 Peer Comparison (Fertility Clinic IT Systems)

| Feature                             | IVF System | Typical EMR | Competitive Advantage                          |
| ----------------------------------- | :--------: | :---------: | ---------------------------------------------- |
| Zero Trust Architecture             |     ✅     |     ❌      | Advanced — most use perimeter security only    |
| AI Governance Framework             |     ✅     |     ❌      | Pioneering — few healthcare systems govern AI  |
| Biometric Authentication            |     ✅     |     ❌      | Advanced — fingerprint patient identification  |
| Digital Document Signing            |     ✅     |     🟡      | Full PKI — most use basic e-signatures         |
| Real-time Compliance Health         |     ✅     |     ❌      | Innovation — most rely on periodic assessments |
| GDPR Data Subject Rights Automation |     ✅     |     ❌      | Advanced — most use manual processes           |
| Automated Incident Response         |     ✅     |     🟡      | Advanced — rule-based automation               |

### 12.3 Assessment Improvement Targets

| Framework   | Current | 6-Month Target | 12-Month Target |
| ----------- | :-----: | :------------: | :-------------: |
| HIPAA       |   94%   |      95%       |       96%       |
| GDPR        |   92%   |      95%       |       97%       |
| SOC 2       |   90%   |      92%       |       94%       |
| ISO 27001   |   90%   |      93%       |       95%       |
| HITRUST     |   84%   |      88%       |       90%       |
| NIST AI RMF |   84%   |      88%       |       90%       |
| ISO 42001   |   80%   |      85%       |       88%       |
| **Overall** | **89%** |    **92%**     |     **94%**     |

---

_Next: Read [Standards Mapping Matrix](compliance_standards_mapping.md) for detailed cross-framework control mapping._
