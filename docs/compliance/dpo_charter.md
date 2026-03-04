# Data Protection Officer (DPO) Charter

**Document ID:** IVF-DPO-CHARTER-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Classification:** Internal — Restricted  
**Regulatory Basis:** GDPR Art. 37-39; HIPAA §164.530(a)(1)

---

## 1. Purpose

This charter formalizes the appointment, responsibilities, independence, reporting structure, and authority of the Data Protection Officer (DPO) within the IVF Information System organization, in compliance with GDPR Articles 37-39 and as recommended by ISO 27001:2022.

---

## 2. DPO Appointment

| Field                     | Detail                                                                                                   |
| ------------------------- | -------------------------------------------------------------------------------------------------------- |
| **Title**                 | Data Protection Officer                                                                                  |
| **Appointment Authority** | Board of Directors / Clinical Director                                                                   |
| **Appointment Basis**     | GDPR Art. 37(1)(b) — processing special category data (health, biometric, reproductive) on a large scale |
| **Term**                  | Minimum 3 years, renewable                                                                               |
| **Removal Protection**    | Cannot be dismissed or penalized for performing DPO duties (Art. 38(3))                                  |
| **Contact**               | dpo@[clinic-domain].com                                                                                  |

---

## 3. Reporting Structure & Independence

### 3.1 Organizational Position

```
Board of Directors
├── Clinical Director
│   ├── Medical Staff
│   └── Administrative Staff
├── Chief Information Security Officer (CISO)
│   ├── IT Security Team
│   └── DevSecOps
└── Data Protection Officer (DPO) ★ [Direct report to Board]
    ├── Compliance Team (dotted line)
    └── Privacy Champions (network across departments)
```

### 3.2 Independence Guarantees (Art. 38)

| Guarantee                                       | Implementation                                                                                                    |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| **No instructions regarding exercise of tasks** | DPO operates independently on data protection matters; no management override                                     |
| **No dismissal or penalty for DPO duties**      | Contract clause protecting against retaliation                                                                    |
| **Direct report to highest management**         | DPO reports directly to Board of Directors, not through CISO or CTO                                               |
| **No conflict of interest**                     | DPO does not hold roles determining purposes/means of processing (not IT Director, HR, or C-level simultaneously) |
| **Adequate resources**                          | Dedicated budget for tools, training, external counsel                                                            |
| **Access to all processing operations**         | Unrestricted access to all systems, data flows, and personnel                                                     |
| **Maintaining expert knowledge**                | Annual CIPP/E or equivalent certification renewal funded                                                          |

### 3.3 Reporting Cadence

| Report                           | Audience              | Frequency | Content                                                       |
| -------------------------------- | --------------------- | :-------: | ------------------------------------------------------------- |
| **Board Data Protection Report** | Board of Directors    | Quarterly | Compliance status, DSR metrics, breach summary, risk overview |
| **CISO Coordination Meeting**    | CISO                  |  Monthly  | Technical security measures, incident review                  |
| **Staff Data Protection Update** | All staff             | Quarterly | Awareness, policy changes, training reminders                 |
| **Regulatory Correspondence**    | Supervisory Authority | As needed | Complaints, consultations, breach notifications               |
| **Annual Privacy Report**        | Board + Management    |  Annual   | Comprehensive privacy program assessment                      |

---

## 4. DPO Responsibilities (Art. 39)

### 4.1 Advisory Role

| Responsibility                             | IVF System Implementation                                   |
| ------------------------------------------ | ----------------------------------------------------------- |
| Inform and advise on GDPR obligations      | Privacy Impact Assessments for new features, policy reviews |
| Advise on DPIA (Art. 35)                   | Lead DPIA process, documented in IVF-DPIA-001               |
| Ensure data protection by design (Art. 25) | Review all new system features before deployment            |
| Advise on international transfers          | Oversee SCCs (IVF-SCC-001), TIA process                     |

### 4.2 Monitoring Role

| Responsibility                   | IVF System Implementation                                       |
| -------------------------------- | --------------------------------------------------------------- |
| Monitor GDPR compliance          | ComplianceScoringEngine dashboard review (monthly)              |
| Monitor internal policies        | Compliance schedule monitoring via ComplianceSchedule entity    |
| Monitor staff awareness/training | ComplianceTraining entity tracking, quarterly training programs |
| Monitor DSR handling             | DataSubjectRequest entity, 30-day deadline tracking             |
| Monitor breach response          | BreachNotification entity, 72-hour notification SOP             |

### 4.3 Cooperation & Contact Point

| Responsibility                        | IVF System Implementation                                      |
| ------------------------------------- | -------------------------------------------------------------- |
| Cooperate with supervisory authority  | Point of contact for all regulatory inquiries                  |
| Contact point for data subjects       | Published in privacy notice (VN+EN), accessible via DSR system |
| Consultation on processing operations | Pre-processing review for new data activities                  |

---

## 5. DPO Authority

### 5.1 System Access Rights

| System/Data                   |   Access Level    |
| ----------------------------- | :---------------: |
| All patient data (PHI/PII)    | Full read access  |
| Audit logs                    | Full read access  |
| Security incidents            | Full read access  |
| ComplianceDashboard           | Full read + admin |
| DataSubjectRequest management |     Full CRUD     |
| BreachNotification management |     Full CRUD     |
| User access logs              | Full read access  |
| ProcessingActivity registry   |     Full CRUD     |
| AI bias test results          | Full read access  |

### 5.2 Decision Authority

| Decision                               | DPO Authority |                   Escalation                   |
| -------------------------------------- | :-----------: | :--------------------------------------------: |
| Approve/reject new processing activity |  **Binding**  |                 Board (appeal)                 |
| Require DPIA for new features          |  **Binding**  |                 Board (appeal)                 |
| Halt processing pending DPIA           |  **Binding**  | Board (override with documented justification) |
| Approve DSR response                   |  **Binding**  |         Legal counsel (complex cases)          |
| Report breach to supervisory authority |  **Binding**  |             CEO (co-notification)              |
| Recommend staff disciplinary action    | **Advisory**  |                HR + Management                 |
| Recommend system changes               | **Advisory**  |                   CTO + CISO                   |

---

## 6. DPO Qualifications

| Requirement                                   | Standard      | Evidence                                           |
| --------------------------------------------- | ------------- | -------------------------------------------------- |
| Expert knowledge of data protection law       | Art. 37(5)    | CIPP/E certification or equivalent                 |
| Expert knowledge of data protection practices | Art. 37(5)    | 3+ years in privacy/compliance role                |
| Healthcare sector knowledge                   | Best practice | Experience with HIPAA, medical data                |
| Technical understanding                       | Best practice | Understanding of IT systems, encryption, databases |
| Language requirement                          | Operational   | Vietnamese + English proficiency                   |

---

## 7. DPO Support Structure

### 7.1 Privacy Champions Network

Each department designates a Privacy Champion who:

- Serves as local point of contact for privacy questions
- Assists DPO with privacy impact screening for departmental changes
- Reports potential privacy incidents to DPO immediately
- Participates in quarterly privacy training

| Department       | Privacy Champion Role       |
| ---------------- | --------------------------- |
| Clinical/Medical | Senior Doctor designated    |
| Laboratory       | Lab Manager designated      |
| Reception/Admin  | Office Manager designated   |
| IT/Development   | Senior Developer designated |
| Finance/Billing  | Accounting Lead designated  |

### 7.2 External Resources

| Resource               | Purpose                                                 | Engagement |
| ---------------------- | ------------------------------------------------------- | ---------- |
| External legal counsel | Complex GDPR interpretations, regulatory correspondence | Retainer   |
| Supervisory authority  | Guidance, prior consultation (Art. 36)                  | As needed  |
| Industry peer network  | Best practice sharing                                   | Ongoing    |
| External auditor       | Annual privacy assessment                               | Annual     |

---

## 8. Performance Metrics

| KPI                              |     Target      | Measurement                      |
| -------------------------------- | :-------------: | -------------------------------- |
| DSR response time                |    ≤ 30 days    | DataSubjectRequest.DaysRemaining |
| DSR compliance rate              |      ≥ 95%      | DSR dashboard metric             |
| Breach notification time         |   ≤ 72 hours    | BreachNotification timestamps    |
| Training completion rate         |      ≥ 90%      | ComplianceTraining metrics       |
| DPIA completion for new features |      100%       | Internal tracking                |
| Board reporting cadence          |    Quarterly    | Meeting minutes                  |
| Privacy complaints               | 0 substantiated | Complaint log                    |

---

## 9. Document Control

| Version |    Date    | Author             | Changes             |
| :-----: | :--------: | ------------------ | ------------------- |
|   1.0   | 2026-03-03 | Board of Directors | Initial DPO Charter |

**Approved by:**

| Role               | Name                 | Date         | Signature  |
| ------------------ | -------------------- | ------------ | ---------- |
| Board Chair        | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| Clinical Director  | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
| DPO (Acknowledged) | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |

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
