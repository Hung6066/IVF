# Vanta Platform Integration Guide

**Document ID:** IVF-VANTA-INT-001  
**Version:** 1.0  
**Date:** 2026-03-03  
**Owner:** DevSecOps / Compliance  
**Platform:** Vanta (https://www.vanta.com/)

---

## 1. Overview

This guide covers the integration of the IVF Information System with Vanta's compliance automation platform for continuous monitoring, evidence collection, and audit preparation across SOC 2, ISO 27001, HIPAA, GDPR, and HITRUST CSF.

---

## 2. Integration Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Vanta Platform                         │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │ Policy   │  │ Evidence │  │ Monitor  │  │ Report   │ │
│  │ Manager  │  │ Collect  │  │ Engine   │  │ Builder  │ │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘ │
│       │              │              │              │       │
└───────┼──────────────┼──────────────┼──────────────┼───────┘
        │              │              │              │
  ┌─────┴──────────────┴──────────────┴──────────────┴─────┐
  │                 Integration Layer                       │
  │  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌────────┐ │
  │  │ GitHub  │  │ Cloud    │  │ IVF API  │  │ Docker │ │
  │  │ Connect │  │ Infra    │  │ Webhook  │  │ Scan   │ │
  │  └────┬────┘  └────┬─────┘  └────┬─────┘  └────┬───┘ │
  └───────┼────────────┼──────────────┼──────────────┼─────┘
          │            │              │              │
  ┌───────┴────┐ ┌─────┴─────┐ ┌─────┴──────┐ ┌────┴──────┐
  │  GitHub    │ │ PostgreSQL│ │  IVF API   │ │  Docker   │
  │  Actions   │ │ + Redis   │ │  Backend   │ │  Compose  │
  │  CI/CD     │ │ + MinIO   │ │  (.NET 10) │ │  Infra    │
  └────────────┘ └───────────┘ └────────────┘ └───────────┘
```

---

## 3. Integration Steps

### 3.1 GitHub Repository Integration

**Purpose:** Automated code review evidence, PR tracking, vulnerability scanning

**Configuration:**

1. Connect GitHub organization (Hung6066) to Vanta
2. Grant read access to IVF repository
3. Vanta auto-collects:
   - Branch protection rules
   - PR approval requirements
   - Security scanning workflows (`.github/workflows/security-scan.yml`)
   - Dependency vulnerability alerts (Dependabot/Trivy)
   - Code review history

**Evidence mapped to:**

- SOC 2 CC8.1 (Change Management)
- ISO 27001 A.8.25 (Secure Development Life Cycle)
- HITRUST 10.a (Security Requirements for Software Development)

### 3.2 Infrastructure Monitoring

**Purpose:** Container security, database access, infrastructure compliance

**PostgreSQL monitoring:**

```yaml
# Vanta agent config for PostgreSQL monitoring
vanta_agent:
  database:
    type: postgresql
    host: localhost
    port: 5433
    database: ivf_db
    monitor:
      - access_logs
      - encryption_status
      - backup_verification
```

**Docker container scanning:**

```yaml
# Add to docker-compose.yml for Vanta container scanning
services:
  vanta-agent:
    image: vanta/agent:latest
    environment:
      VANTA_KEY: "${VANTA_AGENT_KEY}"
      VANTA_OWNER_EMAIL: "${VANTA_OWNER_EMAIL}"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
    restart: unless-stopped
    networks:
      - ivf-public
```

**Evidence mapped to:**

- SOC 2 CC6.1 (Logical Access)
- ISO 27001 A.8.1 (User Endpoint Devices)
- HIPAA §164.312 (Technical Safeguards)

### 3.3 IVF API Compliance Webhook

**Purpose:** Push compliance data from IVF system to Vanta

**Webhook endpoint configuration:**

```json
{
  "vanta_webhook": {
    "base_url": "https://api.vanta.com/v1",
    "endpoints": {
      "evidence": "/evidence/upload",
      "controls": "/controls/status",
      "vulnerabilities": "/vulnerabilities/report"
    },
    "auth": "Bearer ${VANTA_API_TOKEN}",
    "schedule": {
      "compliance_scores": "0 0 * * *",
      "dsr_metrics": "0 6 * * 1",
      "security_events": "*/15 * * * *",
      "training_status": "0 0 1 * *"
    }
  }
}
```

**Data exports to Vanta:**

| IVF Endpoint                            | Vanta Category      | Frequency |
| --------------------------------------- | ------------------- | :-------: |
| `/api/compliance/dashboard`             | Compliance Scores   |   Daily   |
| `/api/compliance/monitoring/health`     | Health Metrics      |   Daily   |
| `/api/compliance/dsr/dashboard`         | Privacy (GDPR)      |  Weekly   |
| `/api/compliance/schedule/dashboard`    | Task Tracking       |  Weekly   |
| `/api/compliance/training`              | Training Evidence   |  Monthly  |
| `/api/compliance/breaches`              | Incident Management | On event  |
| `/api/compliance/assets`                | Asset Inventory     |  Weekly   |
| `/api/compliance/processing-activities` | ROPA                |  Monthly  |
| `/api/ai/bias/dashboard`                | AI Governance       |  Monthly  |
| `/api/ai/model-versions/dashboard`      | AI Governance       | On change |

### 3.4 Employee Management Integration

**Purpose:** Background checks, training tracking, onboarding/offboarding

**Configuration:**

1. Connect HR system to Vanta (if applicable)
2. Map ComplianceTraining entity data:
   - Training assignments and completions
   - Certificate tracking
   - Renewal schedules
3. Configure onboarding checklist:
   - Security awareness training
   - HIPAA training
   - GDPR awareness
   - Acceptable use policy acknowledgment
   - Data handling procedures

### 3.5 Vulnerability Scanning Integration

**Purpose:** Continuous vulnerability monitoring

**Existing pipeline integration:**

```yaml
# .github/workflows/security-scan.yml already provides:
# - Trivy container scanning
# - OWASP ZAP DAST
# - CodeQL SAST
# - npm audit for frontend

# Vanta integration: export results
- name: Upload to Vanta
  if: always()
  uses: vanta/upload-scan-results@v1
  with:
    api-token: ${{ secrets.VANTA_API_TOKEN }}
    scan-type: vulnerability
    results-path: scan-results/
```

### 3.6 Access Review Configuration

**Purpose:** Automated access certification

**Setup:**

1. Connect IVF RBAC system to Vanta
2. Map roles and permissions:
   - Admin, Doctor, Nurse, LabTech, Embryologist, Receptionist, Cashier, Pharmacist
3. Configure quarterly access review campaigns
4. Auto-collect evidence from UserSession, UserLoginHistory entities

---

## 4. Vanta Dashboard Configuration

### 4.1 Framework Mapping

| Vanta Framework | IVF Status | License Required |
| --------------- | :--------: | :--------------: |
| SOC 2 Type II   | ✅ Enable  |       Yes        |
| ISO 27001       | ✅ Enable  |       Yes        |
| HIPAA           | ✅ Enable  |       Yes        |
| GDPR            | ✅ Enable  |       Yes        |
| HITRUST CSF     | ✅ Enable  |      Add-on      |

### 4.2 Custom Controls

Map IVF-specific controls not covered by default Vanta tests:

| Custom Control            | Framework     | Evidence Source        |
| ------------------------- | ------------- | ---------------------- |
| AI Bias Testing           | NIST AI RMF   | AiBiasTestResult API   |
| AI Model Versioning       | ISO 42001     | AiModelVersion API     |
| Biometric Data Processing | GDPR Art. 9   | DPIA document          |
| Clinical Data Encryption  | HIPAA         | EncryptionConfig API   |
| Digital Signing (PKI)     | SOC 2         | Certificate API        |
| Cookie Consent            | GDPR ePrivacy | CookieConsentComponent |

### 4.3 Alert Policies

| Alert                     | Condition              | Severity | Channel           |
| ------------------------- | ---------------------- | :------: | ----------------- |
| Failed vulnerability scan | Critical/High findings |   High   | Slack + Email     |
| DSR deadline approaching  | 7 days before due      |  Medium  | Email to DPO      |
| Training overdue          | > 30 days past due     |  Medium  | Email to HR       |
| Access review pending     | Quarterly review due   |   Low    | Email to IT Admin |
| Compliance score drop     | > 5% decrease          |   High   | Slack + Email     |
| New security incident     | Critical severity      | Critical | PagerDuty + Slack |

---

## 5. Evidence Auto-Collection Schedule

| Evidence Category   | Collection Method         | Frequency  | Vanta Test      |
| ------------------- | ------------------------- | :--------: | --------------- |
| Code reviews        | GitHub API                | Continuous | SOC2-CC8.1      |
| Vulnerability scans | CI/CD pipeline            | On commit  | SOC2-CC7.1      |
| Access logs         | UserLoginHistory API      |   Daily    | SOC2-CC6.1      |
| Encryption status   | EncryptionConfig API      |   Weekly   | HIPAA-164.312   |
| Backup verification | BackupOperation API       |   Daily    | ISO27001-A.8.13 |
| Security training   | ComplianceTraining API    |  Monthly   | HIPAA-164.308   |
| Incident response   | SecurityIncident API      |  On event  | SOC2-CC7.3      |
| Privacy impact      | DPIA documents            | On change  | GDPR-Art.35     |
| Vendor assessment   | VendorRiskAssessment docs | Quarterly  | SOC2-CC9.2      |
| Change management   | GitHub PR history         | Continuous | ISO27001-A.8.32 |

---

## 6. Cost Estimate

| Component                      |   Annual Cost (USD)   | Notes                              |
| ------------------------------ | :-------------------: | ---------------------------------- |
| Vanta Platform (Growth)        |   $10,000 - $18,000   | SOC 2 + ISO 27001 + HIPAA + GDPR   |
| HITRUST CSF Add-on             |    $3,000 - $5,000    | If HITRUST certification pursued   |
| Vanta Agent deployment         |       Included        | Container agent for infrastructure |
| Custom integration development |    $2,000 - $5,000    | One-time webhook + API integration |
| **Total Year 1**               | **$15,000 - $28,000** |                                    |
| **Total Year 2+**              | **$10,000 - $23,000** | Reduced after initial setup        |

---

## 7. Implementation Timeline

| Week | Activity                                               | Owner                  |
| :--: | ------------------------------------------------------ | ---------------------- |
|  1   | Vanta account setup, GitHub integration                | DevSecOps              |
|  2   | Infrastructure agent deployment, PostgreSQL monitoring | DevSecOps              |
|  3   | API webhook development, compliance data export        | Development            |
|  4   | Custom control mapping, alert policy configuration     | Compliance             |
|  5   | Employee management integration, training sync         | HR + IT                |
|  6   | End-to-end testing, dashboard review                   | Compliance + DevSecOps |

---

## 8. Document Control

| Version |    Date    | Author                 | Changes                         |
| :-----: | :--------: | ---------------------- | ------------------------------- |
|   1.0   | 2026-03-03 | DevSecOps / Compliance | Initial Vanta integration guide |

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
