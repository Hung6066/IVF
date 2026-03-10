# Business Continuity Plan (BCP) & Disaster Recovery Plan (DRP)

**Document ID:** IVF-BCP-001  
**Version:** 1.0  
**Effective Date:** 2026-03-03  
**Owner:** IT Director / Security Officer  
**Review Cycle:** Annual + post-incident  
**Classification:** CONFIDENTIAL

---

## 1. Purpose

Establish procedures to maintain essential system functions during disruptive events and recover critical systems within defined targets. Required by:

- **ISO 27001** A.5.29–A.5.30 (ICT readiness for business continuity)
- **HIPAA** §164.308(a)(7) (Contingency Plan)
- **SOC 2** A1.1–A1.3 (Availability)
- **HITRUST CSF** Domain 12

## 2. Scope

Covers all components of the IVF Information System:

- Backend API (.NET 10)
- Frontend (Angular 21)
- Database (PostgreSQL)
- File storage (MinIO)
- PKI infrastructure (EJBCA + SignServer)
- Cache (Redis)
- Monitoring & security services

## 3. Recovery Objectives

| System                          | RPO (Recovery Point Objective) | RTO (Recovery Time Objective) |   Priority    |
| ------------------------------- | :----------------------------: | :---------------------------: | :-----------: |
| **PostgreSQL Database**         |   5 minutes (WAL streaming)    |          30 minutes           | P0 — Critical |
| **IVF API Service**             |         0 (stateless)          |          15 minutes           | P0 — Critical |
| **MinIO (medical images/docs)** |    24 hours (daily backup)     |            2 hours            |   P1 — High   |
| **Angular Frontend**            |     0 (static assets, CDN)     |          15 minutes           |   P1 — High   |
| **Redis Cache**                 |        N/A (ephemeral)         |           5 minutes           |  P2 — Medium  |
| **EJBCA/SignServer**            |            24 hours            |            4 hours            |  P2 — Medium  |
| **Audit/Security Logs**         |        5 minutes (WAL)         |          30 minutes           |   P1 — High   |

## 4. Current Backup Infrastructure

### 4.1 Backup Strategy (3-2-1 Rule)

| Copy        | Location              | Type                  | Retention          |
| ----------- | --------------------- | --------------------- | ------------------ |
| **Primary** | Production PostgreSQL | Live data             | Current            |
| **Standby** | PostgreSQL Replica    | Streaming replication | Current - 5min lag |
| **Cloud**   | AWS S3 / Azure Blob   | Encrypted backup      | 90 days            |

### 4.2 Backup Types & Schedules

| Type                           | Schedule          | Tool                        | Retention | Encryption               |
| ------------------------------ | ----------------- | --------------------------- | --------- | ------------------------ |
| **pg_dump** (logical)          | Daily 2:00 AM UTC | `scripts/backup.sh`         | 30 days   | gzip + AES-256-CBC       |
| **WAL Archiving** (continuous) | Continuous        | PostgreSQL archiver         | 14 days   | On-disk                  |
| **Base Backup** (physical)     | Weekly            | `pg_basebackup`             | 14 days   | gzip                     |
| **MinIO Bucket Sync**          | Daily 3:00 AM UTC | `mc mirror`                 | 30 days   | S3 server-side (AES-256) |
| **CA Key Backup**              | After rotation    | `scripts/backup-ca-keys.sh` | Permanent | AES-256-CBC + OpenSSL    |
| **Cloud Upload**               | Daily 4:00 AM UTC | `scripts/cloud-backup.sh`   | 90 days   | Brotli + SHA-256 verify  |

### 4.3 Integrity Verification

- SHA-256 checksum for every backup file (stored in `backups/*.sha256`)
- Backup verify script validates checksums before cloud upload
- Monthly restore test (recommended)

## 5. Disaster Scenarios & Response

### Scenario 1: Database Failure (P0)

| Phase         | Action                                     |         Time         | Responsible |
| ------------- | ------------------------------------------ | :------------------: | ----------- |
| Detection     | PostgreSQL monitoring alert                |         T+0          | Automated   |
| Failover      | Promote standby replica to primary         |        T+5min        | DBA/IT      |
| Validation    | Verify data integrity (WAL replay)         |       T+10min        | DBA         |
| DNS/Config    | Update connection string to new primary    |       T+15min        | IT          |
| Recovery      | Application reconnection (automatic retry) |       T+20min        | Automated   |
| Rebuild       | Create new standby from new primary        |       T+30min        | DBA         |
| **Total RTO** |                                            |    **30 minutes**    |             |
| **Data Loss** |                                            | **≤5 minutes (WAL)** |             |

**Commands:**

```bash
# Promote standby to primary
docker exec ivf-postgres-standby pg_ctl promote -D /var/lib/postgresql/data

# Verify timeline
docker exec ivf-postgres-standby psql -U postgres -c "SELECT pg_last_wal_replay_lsn();"
```

### Scenario 2: Complete System Failure (P0)

| Phase         | Action                                           |      Time      |
| ------------- | ------------------------------------------------ | :------------: |
| 1             | Provision new infrastructure (Docker host)       |    T+30min     |
| 2             | Deploy containers from registry (docker-compose) |    T+15min     |
| 3             | Restore database from latest backup (PITR)       |    T+30min     |
| 4             | Restore MinIO data from cloud backup             |    T+60min     |
| 5             | Restore PKI certificates from encrypted backup   |    T+30min     |
| 6             | Update DNS / load balancer                       |    T+15min     |
| 7             | Validation & smoke testing                       |    T+30min     |
| **Total RTO** |                                                  | **~3.5 hours** |

**Khôi phục qua API (khuyến nghị):**

```bash
# System Restore API — khôi phục toàn bộ DB + MinIO + PKI trong 1 operation
curl -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  https://natra.site/api/admin/system-restore/start \
  -d '{
    "databaseBackupFile": "ivf_db_20260310_020000.sql.gz",
    "minioBackupFile": "ivf_minio_20260310_023000.tar.gz",
    "pkiBackupFile": "ivf-ca-backup_20260310_020000.tar.gz"
  }'

# Hoặc sử dụng Frontend UI: Admin → Backup/Restore → Toàn hệ thống
# Xem thêm: swarm_s3_deployment_guide.md Section 16.7
```

**PITR Command (alternative):**

```bash
# Point-in-Time Recovery to specific timestamp
docker exec ivf-postgres bash -c "
  pg_basebackup -D /var/lib/postgresql/data_restore -Fp -Xs -P
  cat >> /var/lib/postgresql/data_restore/recovery.conf << EOF
  restore_command = 'cp /var/lib/postgresql/wal_archive/%f %p'
  recovery_target_time = '2026-03-03 10:00:00 UTC'
  EOF
"
```

### Scenario 3: Ransomware Attack (P0)

| Phase         | Action                                                      |     Time     |
| ------------- | ----------------------------------------------------------- | :----------: |
| 1             | Isolate affected systems (network disconnect)               |    T+5min    |
| 2             | Activate incident response (lock accounts, revoke sessions) |   T+10min    |
| 3             | Assess scope of encryption/damage                           |   T+60min    |
| 4             | Restore from clean backup (pre-infection)                   |   T+120min   |
| 5             | Rebuild affected containers from trusted registry           |   T+60min    |
| 6             | Change all credentials (DB, API keys, JWT secret)           |   T+30min    |
| 7             | Full security scan before reconnecting                      |   T+60min    |
| **Total RTO** |                                                             | **~6 hours** |

### Scenario 4: Cloud Provider Outage (P1)

| Phase      | Action                                      |    Time     |
| ---------- | ------------------------------------------- | :---------: |
| 1          | Confirm outage (cloud provider status page) |   T+5min    |
| 2          | Switch to on-premises backup storage        |   T+15min   |
| 3          | Disable cloud sync until resolved           |   T+5min    |
| 4          | Monitor for resolution                      |   Ongoing   |
| **Impact** | Backup sync delayed, no data loss           | **Minimal** |

### Scenario 5: MinIO Storage Failure (P1)

| Phase         | Action                                |     Time     |
| ------------- | ------------------------------------- | :----------: |
| 1             | Deploy new MinIO instance             |   T+15min    |
| 2             | Restore from cloud backup (mc mirror) |   T+120min   |
| 3             | Verify SHA-256 checksums              |   T+30min    |
| 4             | Update application configuration      |   T+10min    |
| **Total RTO** |                                       | **~3 hours** |

## 6. Communication Plan

### Notification Chain

```
Detection → IT/Security Team → IT Director → CEO/Director
                             → DPO (if data breach)
                             → Legal (if regulatory)
                             → Staff notification
                             → Patient notification (if PHI affected)
```

### Contact Matrix

| Role             | Primary Contact | Backup Contact | Method        |
| ---------------- | --------------- | -------------- | ------------- |
| IT Director      | [Name]          | [Name]         | Phone + Email |
| Security Officer | [Name]          | [Name]         | Phone + Email |
| DPO              | [Name]          | [Name]         | Phone + Email |
| DBA              | [Name]          | [Name]         | Phone         |
| Cloud Admin      | [Name]          | [Name]         | Phone         |
| CEO/Director     | [Name]          | [Name]         | Phone         |

## 7. Testing Schedule

| Test Type                                | Frequency     |  Last Tested   |   Next Test    |
| ---------------------------------------- | ------------- | :------------: | :------------: |
| Backup restore verification              | Monthly       | \***\*\_\*\*** | \***\*\_\*\*** |
| Database failover (standby promotion)    | Quarterly     | \***\*\_\*\*** | \***\*\_\*\*** |
| Full disaster recovery simulation        | Annually      | \***\*\_\*\*** | \***\*\_\*\*** |
| Tabletop exercise (scenario walkthrough) | Semi-annually | \***\*\_\*\*** | \***\*\_\*\*** |
| Cloud backup restore                     | Quarterly     | \***\*\_\*\*** | \***\*\_\*\*** |
| Communication plan drill                 | Annually      | \***\*\_\*\*** | \***\*\_\*\*** |

## 8. Monitoring & Alerts

| Monitor               | Tool                          | Alert Threshold        | Notification        |
| --------------------- | ----------------------------- | ---------------------- | ------------------- |
| Database availability | PostgreSQL health check       | Connection failure     | IT Team (immediate) |
| Replication lag       | Streaming replication monitor | >60 seconds lag        | DBA (warning)       |
| Backup completion     | Backup script exit code       | Non-zero exit          | IT Team (immediate) |
| Disk usage            | Docker volume monitoring      | >85% capacity          | IT Team (warning)   |
| WAL archive status    | WAL archive monitor           | Archive failure        | DBA (immediate)     |
| API health            | Health check endpoint         | 3 consecutive failures | IT Team (immediate) |

## 9. Recovery Procedures Checklist

### Pre-Recovery

- [ ] Confirm incident type and scope
- [ ] Notify IT Director and Security Officer
- [ ] Assess whether data breach occurred (trigger Breach SOP if yes)
- [ ] Document timeline of events

### During Recovery

- [ ] Follow appropriate scenario response plan
- [ ] Monitor recovery progress
- [ ] Document all actions taken
- [ ] Verify data integrity after restore (SHA-256 checksums)
- [ ] Run application smoke tests

### Post-Recovery

- [ ] Confirm all systems operational
- [ ] Verify audit logging is active
- [ ] Update security monitoring baselines
- [ ] Conduct post-incident review
- [ ] Update BCP/DRP with lessons learned
- [ ] File incident report

---

**Approval:**

| Role             | Name                     | Date         | Signature      |
| ---------------- | ------------------------ | ------------ | -------------- |
| IT Director      | **\*\***\_\_\_\_**\*\*** | **\_\_\_\_** | \***\*\_\*\*** |
| Security Officer | **\*\***\_\_\_\_**\*\*** | **\_\_\_\_** | \***\*\_\*\*** |
| CEO/Director     | **\*\***\_\_\_\_**\*\*** | **\_\_\_\_** | \***\*\_\*\*** |

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
