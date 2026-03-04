# Compliance Implementation & Deployment Guide

> **Document ID:** IVF-CMP-CID-001  
> **Version:** 1.0  
> **Effective Date:** 2026-03-04  
> **Classification:** CONFIDENTIAL  
> **Owner:** CISO / System Administrator  
> **Review Cycle:** Semi-Annual

---

## Table of Contents

1. [Prerequisites & Environment Setup](#1-prerequisites--environment-setup)
2. [Backend Deployment](#2-backend-deployment)
3. [Frontend Deployment](#3-frontend-deployment)
4. [Database & Migration](#4-database--migration)
5. [Infrastructure Security Hardening](#5-infrastructure-security-hardening)
6. [Compliance Feature Configuration](#6-compliance-feature-configuration)
7. [Monitoring & Alerting Setup](#7-monitoring--alerting-setup)
8. [Production Readiness Checklist](#8-production-readiness-checklist)
9. [Operational Runbooks](#9-operational-runbooks)
10. [Disaster Recovery Procedures](#10-disaster-recovery-procedures)
11. [Rollback Procedures](#11-rollback-procedures)
12. [Performance Tuning](#12-performance-tuning)

---

## 1. Prerequisites & Environment Setup

### 1.1 Infrastructure Requirements

| Component         |        Development        |   Staging    |           Production            |
| ----------------- | :-----------------------: | :----------: | :-----------------------------: |
| **CPU**           |          4 cores          |   8 cores    |            16+ cores            |
| **RAM**           |           8 GB            |    16 GB     |             32+ GB              |
| **Storage**       |         50 GB SSD         |  200 GB SSD  |          500+ GB NVMe           |
| **OS**            | Windows 11 / Ubuntu 22.04 | Ubuntu 22.04 |        Ubuntu 22.04 LTS         |
| **Biometric SDK** |         Optional          |   Optional   | Windows Server (DigitalPersona) |

### 1.2 Software Stack

| Component  | Version |   Port    | Purpose                     |
| ---------- | ------- | :-------: | --------------------------- |
| .NET SDK   | 10.0    |     —     | Backend runtime             |
| Node.js    | 22 LTS  |     —     | Frontend build              |
| PostgreSQL | 16+     |   5433    | Primary database            |
| Redis      | 7+      |   6379    | Caching & sessions          |
| MinIO      | Latest  | 9000/9001 | Object storage              |
| EJBCA      | CE      |   8443    | PKI / Certificate Authority |
| SignServer | CE      |   9443    | Digital document signing    |
| Docker     | 24+     |     —     | Container orchestration     |

### 1.3 Docker Compose Bootstrap

```bash
# Start all infrastructure services
docker-compose up -d

# Verify all services are running
docker-compose ps

# Expected: PostgreSQL (5433), Redis (6379), MinIO (9000), EJBCA (8443), SignServer (9443)
```

### 1.4 Environment Configuration Files

| File                                              | Purpose                   | Secrets |
| ------------------------------------------------- | ------------------------- | :-----: |
| `src/IVF.API/appsettings.json`                    | Base configuration        |   ❌    |
| `src/IVF.API/appsettings.Development.json`        | Dev overrides, API keys   |   ⚠️    |
| `src/IVF.API/appsettings.Production.json`         | Production configuration  |   🔴    |
| `ivf-client/src/environments/environment.ts`      | Frontend dev config       |   ❌    |
| `ivf-client/src/environments/environment.prod.ts` | Frontend prod config      |   ❌    |
| `docker-compose.yml`                              | Infrastructure services   |   ⚠️    |
| `docker-compose.production.yml`                   | Production infrastructure |   🔴    |
| `secrets/`                                        | Certificates, keys        |   🔴    |

---

## 2. Backend Deployment

### 2.1 Build & Publish

```bash
# Restore dependencies
dotnet restore

# Build (verify 0 errors)
dotnet build --configuration Release

# Run tests
dotnet test tests/IVF.Tests/IVF.Tests.csproj --configuration Release

# Publish for deployment
dotnet publish src/IVF.API/IVF.API.csproj \
  --configuration Release \
  --output ./publish \
  --self-contained false
```

### 2.2 Compliance-Specific Backend Components

| Component                     | File                                                     |   Configuration Required    |
| ----------------------------- | -------------------------------------------------------- | :-------------------------: |
| ComplianceEndpoints           | `src/IVF.API/Endpoints/ComplianceEndpoints.cs`           | ✅ Registered in Program.cs |
| DataSubjectRequestEndpoints   | `src/IVF.API/Endpoints/DataSubjectRequestEndpoints.cs`   | ✅ Registered in Program.cs |
| ComplianceScheduleEndpoints   | `src/IVF.API/Endpoints/ComplianceScheduleEndpoints.cs`   | ✅ Registered in Program.cs |
| ComplianceMonitoringEndpoints | `src/IVF.API/Endpoints/ComplianceMonitoringEndpoints.cs` | ✅ Registered in Program.cs |
| EnterpriseSecurityEndpoints   | `src/IVF.API/Endpoints/EnterpriseSecurityEndpoints.cs`   | ✅ Registered in Program.cs |
| AdvancedSecurityEndpoints     | `src/IVF.API/Endpoints/AdvancedSecurityEndpoints.cs`     | ✅ Registered in Program.cs |

### 2.3 Database Entities (Auto-Migration)

Compliance entities are automatically created via EF Core migrations on startup (Development mode):

| Entity             | Table                   | Indexes                               |
| ------------------ | ----------------------- | ------------------------------------- |
| BreachNotification | `breach_notifications`  | Status, DetectedAt                    |
| ComplianceTraining | `compliance_trainings`  | UserId, TrainingType, IsCompleted     |
| AssetInventory     | `asset_inventories`     | Classification, Type, Owner           |
| ProcessingActivity | `processing_activities` | Purpose, LegalBasis                   |
| AiBiasTestResult   | `ai_bias_test_results`  | AiSystemName, PassesFairnessThreshold |
| AiModelVersion     | `ai_model_versions`     | AiSystemName, Status                  |
| DataSubjectRequest | `data_subject_requests` | Status, RequestType, IsOverdue        |
| ComplianceSchedule | `compliance_schedules`  | Status, Framework, IsOverdue          |

### 2.4 Seeding (Development)

On startup, `DatabaseSeeder.SeedAsync()` runs automatically in development mode:

```csharp
// Executed in order:
1. DatabaseSeeder.SeedAsync()     // Roles, default admin user
2. FlowSeeder.SeedAsync()         // Clinical workflows
3. FormTemplateSeeder.SeedAsync() // Form templates
4. MenuSeeder.SeedAsync()         // Navigation menu
5. PermissionDefinitionSeeder.SeedAsync() // Permission definitions
```

Compliance data seeding is handled through UI (Compliance Schedule → Seed Defaults button) or API calls.

---

## 3. Frontend Deployment

### 3.1 Build Process

```bash
cd ivf-client

# Install dependencies
npm install

# Build for production
npm run build
# Output: ivf-client/dist/ivf-client/

# Run tests
npm test
```

### 3.2 Compliance UI Components

| Component           | Route                  |        Files        |
| ------------------- | ---------------------- | :-----------------: |
| ComplianceDashboard | `/compliance`          | 3 (.ts/.html/.scss) |
| DsrManagement       | `/compliance/dsr`      |          3          |
| ComplianceSchedule  | `/compliance/schedule` |          3          |
| AssetInventory      | `/compliance/assets`   |          3          |
| AiGovernance        | `/compliance/ai`       |          3          |
| TrainingManagement  | `/compliance/training` |          3          |

### 3.3 Service Integration

```typescript
// ComplianceService provides 35+ methods across these domains:
// - Monitoring & Health (4 endpoints)
// - DSR Management (12 endpoints)
// - Schedule Management (10 endpoints)
// - Breach Management (6 endpoints)
// - Training Management (3 endpoints)
// - Asset Management (7 endpoints)
// - AI Governance (9 endpoints)
// - Processing Activities (7 endpoints)
// - Compliance Scoring Dashboard (1 endpoint)
```

### 3.4 Navigation Integration

Compliance menu section is added to `FALLBACK_MENU` in `main-layout.component.ts`:

| Menu Item          | Route                  | Permission |
| ------------------ | ---------------------- | ---------- |
| 📊 Tổng quan       | `/compliance`          | adminOnly  |
| 📋 Yêu cầu DSR     | `/compliance/dsr`      | adminOnly  |
| 📅 Lịch tuân thủ   | `/compliance/schedule` | adminOnly  |
| 🗃️ Tài sản dữ liệu | `/compliance/assets`   | adminOnly  |
| 🤖 Quản trị AI     | `/compliance/ai`       | adminOnly  |
| 📚 Đào tạo         | `/compliance/training` | adminOnly  |

---

## 4. Database & Migration

### 4.1 EF Core Migrations

```bash
# Create new migration
dotnet ef migrations add ComplianceFeature \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Apply migrations
dotnet ef database update \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Generate SQL script for review
dotnet ef migrations script \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API \
  --output migration.sql
```

### 4.2 Production Migration Procedure

1. **Pre-migration backup:**

   ```bash
   pg_dump -h localhost -p 5433 -U postgres ivf_db > pre_migration_backup.sql
   ```

2. **Review migration SQL:**

   ```bash
   dotnet ef migrations script --idempotent --output review.sql
   ```

3. **Apply in maintenance window:**
   - Notify users of scheduled downtime
   - Stop application
   - Apply migration
   - Verify schema
   - Start application
   - Smoke test

4. **Rollback if needed:**
   ```bash
   dotnet ef database update PreviousMigrationName
   ```

### 4.3 Audit Log Partitioning

Audit logs use PostgreSQL table partitioning for performance:

```sql
-- Partitions are auto-created for future months
-- Current partition: audit_logs_2026_03
-- Each partition covers one month
-- Old partitions are never deleted (7-year retention)
```

---

## 5. Infrastructure Security Hardening

### 5.1 Network Layer

| Control             | Configuration                             | Verification              |
| ------------------- | ----------------------------------------- | ------------------------- |
| TLS 1.3 enforcement | `appsettings.json` → Kestrel HTTPS config | `openssl s_client` test   |
| CORS policy         | `Program.cs` → `AddCors()`                | Verify allowed origins    |
| Rate limiting       | 100 req/min global                        | Load test verification    |
| API key validation  | BCrypt hash comparison                    | API test with invalid key |
| IP whitelisting     | AdvancedSecurityEndpoints                 | Verify blocked requests   |

### 5.2 Application Layer

| Control                  | Implementation                        | Compliance Requirement |
| ------------------------ | ------------------------------------- | ---------------------- |
| JWT authentication       | 60-min expiry, 7-day refresh          | SOC 2 CC6.1            |
| Zero Trust middleware    | Continuous risk scoring per request   | ISO 27001 A.5.15       |
| Input validation         | FluentValidation on all CQRS commands | OWASP Top 10           |
| CSRF protection          | Antiforgery tokens                    | OWASP Top 10           |
| SQL injection prevention | EF Core parameterized queries         | HIPAA §164.312(a)      |
| XSS prevention           | Angular built-in sanitization         | OWASP Top 10           |
| Audit trail              | Every state-changing operation logged | SOC 2 CC7.2            |

### 5.3 Data Layer

| Control               | Implementation                              | Compliance Requirement   |
| --------------------- | ------------------------------------------- | ------------------------ |
| Encryption at rest    | AES-256-GCM (EncryptionService)             | HIPAA §164.312(a)(2)(iv) |
| Encryption in transit | TLS 1.3                                     | HIPAA §164.312(e)        |
| Database access       | Connection string in secrets, pooling       | SOC 2 CC6.1              |
| Backup encryption     | GPG encrypted backups                       | ISO 27001 A.8.24         |
| Key management        | KeyVaultService (in-app), rotated quarterly | SOC 2 CC6.1              |
| Data masking          | Pseudonymization for analytics              | GDPR Art. 4(5)           |

### 5.4 Container Security

```yaml
# docker-compose.production.yml security controls:
services:
  postgres:
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password # Secret management
    volumes:
      - pg_data:/var/lib/postgresql/data # Persistent volume
    networks:
      - backend # Isolated network
    deploy:
      resources:
        limits:
          memory: 4G
```

---

## 6. Compliance Feature Configuration

### 6.1 Health Score Configuration

```json
// Health score component weights (in ComplianceScoringEngine)
{
  "complianceScoring": {
    "dsrWeight": 0.25, // DSR compliance rate
    "taskWeight": 0.25, // Schedule task completion
    "securityWeight": 0.2, // Open security incidents
    "trainingWeight": 0.15, // Training compliance rate
    "aiWeight": 0.15, // AI bias test pass rate
    "healthyThreshold": 90, // Score ≥ 90 = Healthy
    "warningThreshold": 70, // Score 70-89 = Warning
    "criticalThreshold": 0 // Score < 70 = Critical
  }
}
```

### 6.2 DSR Configuration

| Setting                       |        Default         | Description                |
| ----------------------------- | :--------------------: | -------------------------- |
| Response deadline             |        30 days         | GDPR Art. 12(3)            |
| Max extension                 |        60 days         | GDPR Art. 12(3) additional |
| Identity verification timeout |         3 days         | Internal SLA               |
| Auto-reminder frequency       | 7 days before deadline | Email notification         |

### 6.3 Breach Notification Configuration

| Setting                     |      Default       | Description              |
| --------------------------- | :----------------: | ------------------------ |
| GDPR notification deadline  |      72 hours      | Art. 33                  |
| HIPAA notification deadline |      60 days       | §164.404                 |
| Auto-escalation trigger     | Critical severity  | P1 auto-escalate to CISO |
| Containment SLA             | 4 hours (Critical) | Internal target          |

### 6.4 AI Governance Configuration

| Setting                   |       Default       | Description                            |
| ------------------------- | :-----------------: | -------------------------------------- |
| FPR threshold             |         5%          | Maximum acceptable False Positive Rate |
| FNR threshold             |         5%          | Maximum acceptable False Negative Rate |
| Disparity ratio threshold |         1.2         | Maximum FPR disparity between groups   |
| Bias test frequency       |       Weekly        | Automated bias checks                  |
| Model deployment approval | AI Ethics Committee | Required sign-off                      |

### 6.5 Data Retention Configuration

| Data Category         |       Retention        | Deletion Method       |
| --------------------- | :--------------------: | --------------------- |
| PHI (medical records) |        6 years         | Pseudonymize          |
| Financial records     |        7 years         | Archive → purge       |
| Audit logs            |        7 years         | Retain (partitioned)  |
| Biometric templates   | Until withdrawal + 30d | Cryptographic erasure |
| User sessions         |        90 days         | Auto-purge            |
| Security events       |        3 years         | Auto-partition        |
| AI training data      |  Model lifecycle + 3y  | De-identify           |

---

## 7. Monitoring & Alerting Setup

### 7.1 Compliance Monitoring Dashboard

Accessible at `/compliance` after login as Admin:

```
┌────────────────────────────────────────────────────────────┐
│  📊 COMPLIANCE HEALTH DASHBOARD                           │
│                                                            │
│  Health Score: 88/100 ⚠️ Warning                          │
│                                                            │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │
│  │ DSR  │ │Tasks │ │Sec.  │ │Train │ │ AI   │ │Asset │  │
│  │  5   │ │  45  │ │  12  │ │  48  │ │  20  │ │  35  │  │
│  │active│ │active│ │events│ │total │ │models│ │total │  │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘  │
│                                                            │
│  Tabs: Overview │ GDPR │ Security │ AI │ Audit           │
│                                                            │
│  Alerts:                                                   │
│  ⚠ 3 compliance tasks overdue                             │
│  ⚠ AI bias test approaching threshold                     │
│  ✅ DSR compliance rate: 100%                              │
│  ✅ Training compliance: 87.5%                             │
└────────────────────────────────────────────────────────────┘
```

### 7.2 Real-Time Alerts

| Alert Type              | Channel                 | Recipients         | Condition          |
| ----------------------- | ----------------------- | ------------------ | ------------------ |
| Health score < 85       | Dashboard + Email       | Compliance Officer | Check every hour   |
| Health score < 70       | Dashboard + Email + SMS | CISO + CO + Board  | Check every 15 min |
| DSR overdue             | Dashboard + Email       | DPO + Assignee     | Daily check        |
| P1 security incident    | Dashboard + Email + SMS | CISO + On-call     | Real-time          |
| AI bias test failure    | Dashboard + Email       | AI Committee       | On test completion |
| Training non-compliance | Dashboard + Email       | HR + Manager       | Weekly check       |
| Breach detected         | Dashboard + Email + SMS | CISO + DPO + Legal | Real-time          |

### 7.3 Monitoring APIs

```bash
# Health check
GET /api/compliance/monitoring/health

# Security trends (last N days)
GET /api/compliance/monitoring/security-trends?days=30

# AI performance metrics
GET /api/compliance/monitoring/ai-performance

# Audit readiness status
GET /api/compliance/monitoring/audit-readiness

# Overdue items
GET /api/compliance/schedule/overdue
GET /api/compliance/dsr?overdue=true
```

---

## 8. Production Readiness Checklist

### 8.1 Pre-Deployment Verification

#### Security

- [ ] TLS 1.3 configured and verified
- [ ] JWT secrets rotated (not default development keys)
- [ ] API keys hashed with BCrypt (not plaintext)
- [ ] Rate limiting enabled (100 req/min global, 30 ops/min signing)
- [ ] CORS origins whitelisted (production domains only)
- [ ] Zero Trust middleware enabled
- [ ] Conditional access policies configured
- [ ] Input validation on all endpoints
- [ ] SQL injection protection verified
- [ ] XSS prevention verified

#### Data Protection

- [ ] AES-256-GCM encryption at rest configured
- [ ] Database passwords in secrets management (not env vars)
- [ ] Backup encryption enabled (GPG)
- [ ] WAL archiving active
- [ ] Replication configured and verified
- [ ] Data retention policies defined and automated
- [ ] Pseudonymization procedures documented

#### Compliance Features

- [ ] All 79 compliance endpoints registered
- [ ] ComplianceScoringEngine configured with correct weights
- [ ] DSR workflow tested end-to-end
- [ ] Breach notification workflow tested
- [ ] AI bias testing configured for all 5 models
- [ ] Training programs created and assigned
- [ ] Asset inventory populated
- [ ] ROPA register completed
- [ ] Compliance schedule seeded with default tasks

#### Infrastructure

- [ ] PostgreSQL 16+ with partitioned audit logs
- [ ] Redis configured (graceful degradation if unavailable)
- [ ] MinIO buckets created (ivf-documents, ivf-signed-pdfs, ivf-medical-images)
- [ ] EJBCA certificates issued
- [ ] SignServer configured with signing profile
- [ ] Docker containers resource-limited
- [ ] Network segmentation in place
- [ ] Monitoring and alerting configured

#### Documentation

- [ ] All 25+ compliance documents current
- [ ] Privacy notice published (bilingual)
- [ ] Data processing agreements with all vendors
- [ ] Standard Contractual Clauses where needed
- [ ] Internal audit scheduled
- [ ] Staff training plan in place

---

## 9. Operational Runbooks

### 9.1 Runbook: DSR Handling (Urgent — Approaching Deadline)

**Trigger:** DSR within 5 days of deadline, not completed

**Steps:**

1. Login to `/compliance/dsr`
2. Filter: Overdue = Yes, or sort by due date
3. Open the DSR detail
4. If identity not verified → expedite verification (call patient)
5. If not assigned → assign immediately with priority note
6. If extension needed → `POST /api/compliance/dsr/{id}/extend` with justification
7. If extension already used → escalate to DPO
8. Document all actions in DSR notes
9. Update compliance health check after resolution

### 9.2 Runbook: Breach Containment (P1 Critical)

**Trigger:** SecurityIncident created with Severity = Critical

**Steps (execute within 15 minutes):**

1. **Isolate:** Lock compromised accounts immediately
2. **Review:** Check SecurityEvent table for scope
3. **Contain:**
   - Revoke sessions: terminate active user sessions
   - Block IPs: add to IP blocklist via Advanced Security
   - Restrict data: if patient data involved, activate Patient.RestrictProcessing()
4. **Preserve:** Export audit logs covering incident timeframe
5. **Assess:** Determine if breach notification required (see Section 2 of Flows doc)
6. **Notify:**
   - Internal: CISO + DPO + Legal within 1 hour
   - GDPR: Supervisory Authority within 72 hours if personal data
   - HIPAA: HHS within 60 days if PHI
7. **Document:** Create BreachNotification record via `POST /api/compliance/breaches`

### 9.3 Runbook: Health Score Drop

**Trigger:** Compliance health score drops below 85 (Warning) or 70 (Critical)

**Steps:**

1. Open Dashboard (`/compliance`)
2. Review component scores:
   - **DSR low?** → Go to `/compliance/dsr`, resolve overdue items
   - **Tasks low?** → Go to `/compliance/schedule`, complete/reassign overdue tasks
   - **Security low?** → Check SecurityIncident table, resolve open incidents
   - **Training low?** → Go to `/compliance/training`, send reminders
   - **AI low?** → Go to `/compliance/ai`, investigate failing bias tests
3. After remediation, verify health score recovery
4. If Critical: file incident report and brief management

### 9.4 Runbook: AI Bias Test Failure

**Trigger:** AiBiasTestResult.passesFairnessThreshold = false

**Steps:**

1. Open `/compliance/ai` → Bias Tests tab
2. Identify failing test: which model, which protected attribute, what metric
3. If FPR disparity > 1.2:
   - Notify AI Ethics Committee
   - Consider model rollback: `POST /api/compliance/ai/model-versions/{id}/rollback`
4. Root cause analysis:
   - Check training data balance for affected group
   - Review feature importance for bias indicators
   - Check for data drift since last deployment
5. Remediation:
   - Retrain with balanced/augmented dataset
   - Re-run full bias test suite
   - Only deploy when all tests pass
6. Document findings and update AI Lifecycle documentation

### 9.5 Runbook: Annual Compliance Audit Preparation

**Timeline: 3 months before audit**

| Week | Activity                                      | Owner              |
| :--: | --------------------------------------------- | ------------------ |
| 1-2  | Review all policies, update if needed         | Compliance Officer |
| 3-4  | Conduct internal audit using checklist        | Internal Auditor   |
| 5-6  | Remediate findings from internal audit        | System Admin       |
| 7-8  | Collect evidence per framework requirements   | All departments    |
| 9-10 | Review evidence completeness, dry run         | Compliance Officer |
|  11  | Staff briefing on audit procedures            | HR + Compliance    |
|  12  | Final readiness check, dashboard verification | CISO               |

---

## 10. Disaster Recovery Procedures

### 10.1 Recovery Priorities

| Priority | System              |   RPO    |   RTO   | Recovery Method                    |
| :------: | ------------------- | :------: | :-----: | ---------------------------------- |
|    1     | PostgreSQL Database |  5 min   | 30 min  | WAL replay + streaming replication |
|    2     | IVF API Application |    0     | 15 min  | Container restart / failover       |
|    3     | Redis Cache         |   N/A    |  5 min  | Restart (rebuild from DB)          |
|    4     | MinIO (Documents)   |  1 hour  | 1 hour  | Backup restore                     |
|    5     | EJBCA / SignServer  | 24 hours | 4 hours | Certificate backup restore         |
|    6     | Frontend            |    0     | 10 min  | CDN / rebuild                      |

### 10.2 Database Recovery Procedure

```bash
# Scenario: Primary database failure

# 1. Promote replica (if streaming replication active)
pg_ctl promote -D /var/lib/postgresql/data

# 2. Or restore from WAL backup
pg_basebackup -h backup-host -D /var/lib/postgresql/data
pg_wal_replay --target-time="2026-03-04 10:00:00"

# 3. Or restore from daily backup
gunzip ivf_db_latest.sql.gz
psql -h localhost -p 5433 -U postgres -d ivf_db < ivf_db_latest.sql

# 4. Verify compliance data integrity
psql -c "SELECT COUNT(*) FROM data_subject_requests WHERE status = 'InProgress';"
psql -c "SELECT COUNT(*) FROM compliance_schedules WHERE is_overdue = true;"
psql -c "SELECT COUNT(*) FROM breach_notifications WHERE status = 'Detected';"
```

### 10.3 Post-Recovery Verification

- [ ] All compliance endpoints responding (health check)
- [ ] Audit logs intact (check latest partition)
- [ ] DSR state preserved (no in-progress DSRs lost)
- [ ] Compliance schedule tasks restored
- [ ] AI model versions in correct state
- [ ] SignalR hubs reconnected
- [ ] Redis cache rebuilt
- [ ] Health score recalculated

---

## 11. Rollback Procedures

### 11.1 Application Rollback

```bash
# If new deploy causes compliance issues:

# 1. Stop current version
systemctl stop ivf-api

# 2. Restore previous version
cp -r /opt/ivf/previous/* /opt/ivf/current/

# 3. Rollback database migration (if applicable)
dotnet ef database update PreviousMigrationName \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# 4. Start previous version
systemctl start ivf-api

# 5. Verify compliance endpoints
curl -s https://localhost/api/compliance/monitoring/health | jq .
```

### 11.2 Rollback Decision Matrix

| Scenario                             |      Rollback?      | Approval           |  Timeline  |
| ------------------------------------ | :-----------------: | ------------------ | :--------: |
| Compliance endpoints returning 500   |    ✅ Immediate     | System Admin       |  < 15 min  |
| Data corruption in compliance tables |    ✅ DB restore    | CISO + DBA         |  < 1 hour  |
| Health score calculation incorrect   | ⚠️ Hotfix preferred | Compliance Officer | < 4 hours  |
| UI rendering issues                  | ⚠️ Frontend hotfix  | Frontend Dev       | < 2 hours  |
| Performance degradation only         |    ❌ Tune first    | System Admin       | < 24 hours |

---

## 12. Performance Tuning

### 12.1 Database Optimization

```sql
-- Key indexes for compliance queries
CREATE INDEX CONCURRENTLY idx_dsr_status_overdue ON data_subject_requests (status, is_overdue);
CREATE INDEX CONCURRENTLY idx_schedule_framework_status ON compliance_schedules (framework, status);
CREATE INDEX CONCURRENTLY idx_training_user_completed ON compliance_trainings (user_id, is_completed);
CREATE INDEX CONCURRENTLY idx_breach_status_date ON breach_notifications (status, detected_at);
CREATE INDEX CONCURRENTLY idx_ai_model_system_status ON ai_model_versions (ai_system_name, status);
CREATE INDEX CONCURRENTLY idx_bias_test_system_pass ON ai_bias_test_results (ai_system_name, passes_fairness_threshold);
```

### 12.2 Caching Strategy

| Data            | Cache Duration | Invalidation                  |
| --------------- | :------------: | ----------------------------- |
| Health score    |     5 min      | On any compliance data change |
| Security trends |     15 min     | Time-based                    |
| AI performance  |     30 min     | On new bias test result       |
| Audit readiness |     1 hour     | On compliance task completion |
| Asset list      |     10 min     | On asset CRUD                 |
| DSR list        |    No cache    | Always fresh (SLA-sensitive)  |

### 12.3 Query Optimization

```sql
-- Compliance health score query optimized with materialized view
CREATE MATERIALIZED VIEW compliance_health_snapshot AS
SELECT
  (SELECT COUNT(*) FILTER (WHERE is_overdue) FROM data_subject_requests WHERE status != 'Completed') as dsr_overdue,
  (SELECT COUNT(*) FROM data_subject_requests WHERE status != 'Completed') as dsr_total,
  (SELECT COUNT(*) FILTER (WHERE is_overdue) FROM compliance_schedules WHERE status = 'Active') as tasks_overdue,
  (SELECT COUNT(*) FROM compliance_schedules WHERE status = 'Active') as tasks_total,
  (SELECT COUNT(*) FILTER (WHERE status = 'Open') FROM security_incidents) as incidents_open,
  (SELECT COUNT(*) FILTER (WHERE is_completed) FROM compliance_trainings) as training_completed,
  (SELECT COUNT(*) FROM compliance_trainings) as training_total,
  (SELECT COUNT(*) FILTER (WHERE passes_fairness_threshold) FROM ai_bias_test_results) as ai_passing,
  (SELECT COUNT(*) FROM ai_bias_test_results) as ai_total;

-- Refresh every 5 minutes
-- REFRESH MATERIALIZED VIEW CONCURRENTLY compliance_health_snapshot;
```

---

_Next: Read [Evaluation & Audit Guide](compliance_evaluation_audit.md) for scoring methodology and audit preparation details._
