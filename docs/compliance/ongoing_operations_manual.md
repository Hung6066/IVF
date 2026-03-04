# Ongoing Compliance Operations Manual

**Document ID:** IVF-OPS-MANUAL-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** CISO / DPO / Compliance Team  
**Classification:** Internal — Confidential  
**Applicable Frameworks:** SOC 2 Type II, ISO 27001:2022, HIPAA, GDPR, HITRUST CSF, NIST AI RMF 1.0, ISO 42001

---

## 1. Purpose

This manual defines the ongoing compliance operations for the IVF Information System post-certification (Phase 4). It establishes recurring activities, responsibilities, and procedures to maintain continuous compliance across all seven international standards.

---

## 2. Compliance Calendar

### 2.1 Daily Operations (Automated)

| Activity                        | Tool                                | Owner     | Alert Threshold                          |
| ------------------------------- | ----------------------------------- | --------- | ---------------------------------------- |
| Security event monitoring       | SecurityEvent entity + SignalR      | SecOps    | Critical events → immediate notification |
| Vulnerability scan (SAST)       | GitHub Actions pipeline             | DevSecOps | Critical/High findings → block deploy    |
| Audit log integrity check       | Partitioned PostgreSQL              | System    | Gaps in audit sequence → alert           |
| DSR deadline monitoring         | DataSubjectRequest entity           | System    | 7 days before deadline → DPO alert       |
| AI model performance monitoring | ComplianceMonitoring/ai-performance | System    | Metric threshold breach → alert          |
| Breach detection                | ThreatDetectionService              | SecOps    | Anomaly score > threshold → incident     |

### 2.2 Weekly Operations

| Activity                  | Procedure                                          | Owner      | Output                |
| ------------------------- | -------------------------------------------------- | ---------- | --------------------- |
| Vulnerability scan review | Review SAST/DAST/SCA results from CI/CD pipeline   | DevSecOps  | Remediation tickets   |
| Security metrics snapshot | Export ComplianceDashboard data                    | SecOps     | Weekly metrics report |
| Pending DSR review        | Check DataSubjectRequest dashboard                 | DPO        | Escalation if needed  |
| Compliance schedule check | Review upcoming tasks via /api/compliance/schedule | Compliance | Task status update    |

### 2.3 Monthly Operations

| Activity                        | Procedure                                                  | Owner        | Output                   |
| ------------------------------- | ---------------------------------------------------------- | ------------ | ------------------------ |
| **Security Metrics Review**     | Run compliance/monitoring/health endpoint, review all KPIs | CISO         | Monthly security report  |
| **Compliance Dashboard Review** | Review ComplianceScoringEngine output, verify scores       | Compliance   | Score trend analysis     |
| **AI Model Performance Review** | Review deployed model metrics, bias test status            | AI Committee | Model performance report |
| **DSR Status Report**           | Aggregate DSR metrics, compliance rate                     | DPO          | DSR monthly report       |
| **Incident Trend Analysis**     | Review SecurityIncident patterns, root causes              | SecOps       | Trend analysis report    |

**Monthly review meeting agenda:**

1. Overall compliance health score review
2. New/resolved security incidents
3. DSR metrics and compliance rate
4. Upcoming compliance tasks (next 30 days)
5. AI model performance alerts
6. Action items from previous meeting

### 2.4 Quarterly Operations

| Activity                              | Procedure                                                   | Owner          | Duration | Output                           |
| ------------------------------------- | ----------------------------------------------------------- | -------------- | :------: | -------------------------------- |
| **Management Review (ISO 27001 9.3)** | ISMS management review per standard agenda                  | CISO           | 2 hours  | Meeting minutes, action items    |
| **Internal Audit Cycle**              | Audit 2-3 control domains per quarter                       | Internal Audit |  1 week  | Audit report, NCR log            |
| **Access Review**                     | RBAC certification: verify all user permissions appropriate | IT Admin       |  2 days  | Access review report             |
| **AI Bias Re-Testing**                | Re-run bias tests on all deployed AI systems                | AI Committee   |  3 days  | Updated AiBiasTestResult records |
| **Risk Re-Assessment**                | Update risk register with new threats/changes               | CISO           |  3 days  | Updated risk assessment          |
| **Penetration Test Planning**         | Scope and schedule next pentest (semi-annual execution)     | DevSecOps      |  1 day   | Pentest scope document           |
| **Training Content Update**           | Refresh security training materials, schedule sessions      | HR/Compliance  |  2 days  | Updated training modules         |
| **Vendor Risk Review**                | Review third-party vendor security posture changes          | Procurement    |  2 days  | Vendor risk update               |

**Quarterly ISO 27001 Management Review agenda (Clause 9.3):**

1. Status of actions from previous reviews
2. Changes in external/internal issues
3. Nonconformities and corrective actions
4. Monitoring and measurement results
5. Audit results
6. Fulfilment of information security objectives
7. Feedback from interested parties
8. Risk assessment and treatment plan status
9. Opportunities for continual improvement

### 2.5 Semi-Annual Operations

| Activity                      | Procedure                                                    | Owner           | Duration | Output                           |
| ----------------------------- | ------------------------------------------------------------ | --------------- | :------: | -------------------------------- |
| **Penetration Testing**       | External penetration test (network, app, social engineering) | External Vendor | 2 weeks  | Pentest report                   |
| **BCP/DRP Tabletop Exercise** | Simulate disaster scenario, test recovery procedures         | Operations      |  1 day   | Exercise report, lessons learned |
| **Privacy Impact Review**     | Review all DPIAs for changes in processing                   | DPO             |  3 days  | Updated DPIAs                    |

### 2.6 Annual Operations

| Activity                          | Procedure                                               | Owner               | Duration | Output                        |
| --------------------------------- | ------------------------------------------------------- | ------------------- | :------: | ----------------------------- |
| **SOC 2 Type II Renewal Audit**   | Engage auditor, evidence collection, observation period | External Auditor    | 6 months | SOC 2 Type II report          |
| **ISO 27001 Surveillance Audit**  | CB surveillance visit (1/3 of ISMS scope)               | Certification Body  | 2-3 days | Surveillance report           |
| **HIPAA Risk Assessment**         | Comprehensive HIPAA Security Rule assessment            | Compliance          |  1 week  | Updated HIPAA self-assessment |
| **GDPR DPIA Review**              | Annual review of all DPIAs                              | DPO                 |  1 week  | Updated DPIA documents        |
| **HITRUST CSF Reassessment**      | Update HITRUST self-assessment                          | Compliance          | 2 weeks  | Updated HITRUST assessment    |
| **AI Governance Review**          | Review AI governance charter, policies, model inventory | AI Committee        |  1 week  | Updated AI governance charter |
| **Training Renewal**              | Annual compliance training for all staff                | HR/Compliance       | 2 weeks  | Training completion records   |
| **Data Retention Enforcement**    | Execute retention policies, anonymize expired data      | DPO + System        | Ongoing  | Retention execution log       |
| **Insurance Review**              | Review cyber insurance coverage adequacy                | CFO                 |  1 day   | Insurance coverage report     |
| **Compliance Program Assessment** | Assess overall compliance program effectiveness         | External Consultant |  1 week  | Program assessment report     |

---

## 3. Incident Response Operating Procedures

### 3.1 Security Incident Workflow

```
Detection → Triage → Containment → Investigation → Remediation → Post-Incident Review

Timeline Requirements:
  - Detection → Triage: ≤ 15 minutes (automated via ThreatDetectionService)
  - Triage → Containment: ≤ 1 hour (critical), ≤ 4 hours (high)
  - Investigation → Resolution: ≤ 24 hours (critical), ≤ 72 hours (high)

Escalation Matrix:
  - Low: SecOps team
  - Medium: SecOps + CISO notification
  - High: CISO + Management notification
  - Critical: CISO + CEO + Board + DPO (if personal data)
```

### 3.2 Breach Notification Workflow (GDPR 72h / HIPAA 60d)

```
1. Breach detected → SecurityIncident created (automated)
2. CISO assesses scope and impact (≤ 4 hours)
3. DPO assesses personal data involvement (≤ 8 hours)
4. If personal data breach:
   a. Supervisory authority notification ≤ 72 hours (GDPR Art. 33)
   b. Data subject notification if high risk (GDPR Art. 34)
   c. HHS notification ≤ 60 days for ≥ 500 individuals (HIPAA)
5. BreachNotification entity tracks all timelines
6. Post-breach review within 2 weeks
```

### 3.3 DSR Processing Workflow

```
1. DSR received → DataSubjectRequest created with 30-day deadline
2. Identity verification (≤ 3 business days)
3. Assign handler (DPO or delegated staff)
4. Process request per type:
   - Access: Export patient data, deliver securely
   - Rectification: Update records, confirm to subject
   - Erasure: Anonymize (medical) or hard delete (non-medical)
   - Restriction: Patient.RestrictProcessing(), notify subject
   - Portability: Export as structured JSON, deliver
   - Objection: Assess legal basis, respond
5. Complete within 30 days (extend up to 60 days if complex)
6. Notify data subject of outcome
7. Record completion in DSR system
```

---

## 4. Evidence Collection & Retention

### 4.1 Automated Evidence

| Evidence Type     | Source                    |    Collection    | Retention |
| ----------------- | ------------------------- | :--------------: | :-------: |
| Audit logs        | AuditLog entity           |    Continuous    |  7 years  |
| Security events   | SecurityEvent entity      |    Continuous    |  3 years  |
| Access logs       | UserLoginHistory entity   |    Continuous    |  3 years  |
| Compliance scores | ComplianceDashboard API   | Monthly snapshot |  5 years  |
| AI metrics        | AiModelVersion entity     |  On deployment   | Permanent |
| Bias test results | AiBiasTestResult entity   |   On execution   |  5 years  |
| DSR records       | DataSubjectRequest entity |   On creation    |  5 years  |
| Encryption config | EncryptionConfig entity   |    On change     |  5 years  |
| Backup logs       | BackupOperation entity    |   On execution   |  3 years  |

### 4.2 Manual Evidence

| Evidence Type             | Creator         | Storage                   |     Retention      |
| ------------------------- | --------------- | ------------------------- | :----------------: |
| Management review minutes | CISO            | MinIO (ivf-documents)     |      5 years       |
| Internal audit reports    | Internal Audit  | MinIO (ivf-documents)     |      5 years       |
| Pentest reports           | External vendor | MinIO (ivf-documents)     |      5 years       |
| Training certificates     | HR              | ComplianceTraining entity |      5 years       |
| Vendor assessments        | Procurement     | MinIO (ivf-documents)     | Contract + 2 years |
| BCP/DRP drill reports     | Operations      | MinIO (ivf-documents)     |      5 years       |

---

## 5. Continuous Improvement Process

### 5.1 Nonconformity Management

```
1. NCR raised (from audit, incident, or observation)
2. Root cause analysis (5 Whys or Fishbone)
3. Corrective action plan with timeline
4. Implementation and verification
5. Effectiveness review (next quarterly audit)
6. Close NCR or escalate
```

### 5.2 Metrics-Driven Improvement

| Metric                             |  Target  | Review Frequency | Escalation                           |
| ---------------------------------- | :------: | :--------------: | ------------------------------------ |
| Overall compliance health score    |  ≥ 90%   |     Monthly      | < 80% → CISO review                  |
| DSR 30-day compliance rate         |  ≥ 95%   |     Monthly      | < 90% → DPO escalation               |
| Training completion rate           |  ≥ 90%   |    Quarterly     | < 80% → HR escalation                |
| Penetration test critical findings |    0     |   Semi-annual    | Any critical → immediate remediation |
| AI bias test pass rate             |   100%   |    Quarterly     | Any failure → model review           |
| Schedule task completion rate      |  ≥ 95%   |     Monthly      | < 90% → Compliance review            |
| Incident response time (critical)  | ≤ 1 hour |   Per incident   | Breach → post-incident review        |

---

## 6. Tool & System References

| System                  | Purpose                               | Access                                  |
| ----------------------- | ------------------------------------- | --------------------------------------- |
| ComplianceScoringEngine | Automated scoring across 6 frameworks | `/api/compliance/dashboard`             |
| ComplianceSchedule      | Recurring task management             | `/api/compliance/schedule`              |
| ComplianceMonitoring    | Health dashboard & alerts             | `/api/compliance/monitoring/health`     |
| DataSubjectRequest      | GDPR DSR tracking                     | `/api/compliance/dsr`                   |
| BreachNotification      | Breach lifecycle tracking             | `/api/compliance/breaches`              |
| ComplianceTraining      | Training management                   | `/api/compliance/training`              |
| AssetInventory          | CMDB (ISO 27001 A.5.9)                | `/api/compliance/assets`                |
| ProcessingActivity      | ROPA (GDPR Art. 30)                   | `/api/compliance/processing-activities` |
| AiBiasEndpoints         | AI bias testing dashboard             | `/api/ai/bias`                          |
| AiModelVersion          | AI model lifecycle                    | `/api/ai/model-versions`                |
| SecurityIncident        | Incident management                   | `/api/enterprise/security/incidents`    |

---

## 7. Document Control

| Version |    Date    | Author     | Changes                   |
| :-----: | :--------: | ---------- | ------------------------- |
|   1.0   | 2026-03-03 | CISO / DPO | Initial operations manual |

**Approved by:**

| Role              | Name                 | Date         | Signature  |
| ----------------- | -------------------- | ------------ | ---------- |
| CISO              | ******\_\_\_\_****** | **\_\_\_\_** | ****\_**** |
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
