# IVF Platform — Hướng dẫn Vận hành Hạ tầng Enterprise

Tài liệu này mô tả các tính năng hạ tầng enterprise đã triển khai: monitoring stack, data retention với S3 archival, read-replica routing, auto-healing, và disaster recovery.

---

## Mục lục

1. [Monitoring Stack (Prometheus + Grafana + Loki)](#1-monitoring-stack)
2. [Data Retention & S3 Archival](#2-data-retention--s3-archival)
3. [Read-Replica Routing](#3-read-replica-routing)
4. [Auto-Healing](#4-auto-healing)
5. [Disaster Recovery (DR)](#5-disaster-recovery)
6. [Infrastructure Admin UI](#6-infrastructure-admin-ui)
7. [API Endpoints](#7-api-endpoints)

---

## 1. Monitoring Stack

### 1.1 Tổng quan

Stack giám sát bao gồm 4 thành phần:

| Service    | Port | Chức năng                     |
| ---------- | ---- | ----------------------------- |
| Prometheus | 9090 | Metrics collection & alerting |
| Grafana    | 3000 | Dashboards & visualization    |
| Loki       | 3100 | Log aggregation               |
| Promtail   | —    | Log shipping từ Docker        |

### 1.2 Triển khai

```bash
# Khởi động monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Kiểm tra trạng thái
docker-compose -f docker-compose.monitoring.yml ps
```

### 1.3 Cấu hình

Các file cấu hình nằm trong `docker/monitoring/`:

```
docker/monitoring/
├── prometheus.yml              # Scrape targets
├── alerts.yml                  # Alert rules
├── loki-config.yml             # Loki storage & retention
├── promtail-config.yml         # Docker log shipping
└── grafana/
    └── provisioning/
        └── datasources/
            └── datasources.yml # Auto-provision Prometheus + Loki
```

### 1.4 Prometheus Scrape Targets

| Target          | Endpoint             | Interval |
| --------------- | -------------------- | -------- |
| IVF API         | ivf-api:8080/metrics | 15s      |
| Caddy           | caddy:2019/metrics   | 15s      |
| MinIO           | minio:9000/minio/v2/metrics/cluster | 30s |
| Redis           | redis-exporter:9121  | 15s      |
| Prometheus self | localhost:9090       | 15s      |

### 1.5 Alert Rules

| Alert              | Điều kiện                | Severity |
| ------------------ | ------------------------ | -------- |
| HighErrorRate      | HTTP 5xx > 5% trong 5m   | critical |
| SlowResponseTime   | P95 > 2s trong 5m        | warning  |
| ApiDown            | API unreachable 1m       | critical |
| PostgresDown       | PG unreachable 1m        | critical |
| RedisDown          | Redis unreachable 1m     | critical |
| MinioDown          | MinIO unreachable 1m     | critical |
| HighMemoryUsage    | API memory > 800MB       | warning  |

### 1.6 Loki Log Retention

Loki lưu trữ logs 30 ngày với TSDB storage backend. Cấu hình trong `loki-config.yml`:

```yaml
limits_config:
  retention_period: 720h  # 30 ngày
```

---

## 2. Data Retention & S3 Archival

### 2.1 Tổng quan

`DataRetentionService` chạy dưới dạng `IHostedService`, quét định kỳ (mặc định mỗi 24h) và áp dụng các chính sách retention:

- **Delete**: Xoá trực tiếp các bản ghi vượt quá retention period
- **Archive**: Export dữ liệu sang JSONL, upload lên MinIO bucket `ivf-audit-archive`, rồi xoá khỏi DB

### 2.2 Chính sách mặc định

Các chính sách retention được seed tự động trong `DatabaseSeeder`. Entity types phổ biến:

| Entity Type       | Retention | Action  |
| ----------------- | --------- | ------- |
| AuditLogEntry     | 365 ngày  | Archive |
| SecurityEvent     | 180 ngày  | Archive |
| ApiCallLog        | 90 ngày   | Delete  |
| UserLoginHistory  | 365 ngày  | Archive |

### 2.3 S3 Archival

Dữ liệu archive được lưu trong MinIO:

- **Bucket**: `ivf-audit-archive` (constant `StorageBuckets.AuditArchive`)
- **Key format**: `tenants/{tenantId}/archive/{entityType}/{timestamp}.jsonl`
- **Format**: JSONL (JSON Lines) — mỗi dòng là một JSON record
- **Encryption**: SSE-S3 (MinIO server-side encryption)

### 2.4 Distributed Lock

Mỗi lần chạy retention, service acquire distributed lock qua `IDistributedLockService` để đảm bảo chỉ một instance chạy tại một thời điểm (quan trọng khi có multiple API replicas).

### 2.5 Thực thi thủ công

```bash
# Qua API endpoint
curl -X POST https://your-domain/api/admin/infrastructure/retention/execute \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json"
```

Hoặc qua Admin UI: **Hạ tầng → Lưu trữ → Thực thi ngay**

---

## 3. Read-Replica Routing

### 3.1 Tổng quan

`ReadReplicaInterceptor` (EF Core `DbCommandInterceptor`) tự động route SELECT queries tới PostgreSQL read replica.

### 3.2 Cách hoạt động

1. Kiểm tra command text bắt đầu bằng `SELECT`
2. Nếu đang trong transaction → giữ nguyên primary connection
3. Nếu có `ReadReplicaConnection` trong config → route tới replica
4. Nếu replica không khả dụng → fallback về primary (retry sau 1 phút)

### 3.3 Cấu hình

Trong `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=pg-primary;Port=5432;Database=ivf_db;...",
    "ReadReplicaConnection": "Host=pg-replica;Port=5432;Database=ivf_db;..."
  }
}
```

### 3.4 Giám sát

API endpoint kiểm tra trạng thái replication:

```bash
curl https://your-domain/api/admin/infrastructure/replica/status \
  -H "Authorization: Bearer <jwt>"

# Response:
{
  "isReplica": false,
  "activeReplicationSlots": 1,
  "streamingReplicas": 1,
  "connectionString": "pg-replica:5432"
}
```

---

## 4. Auto-Healing

### 4.1 Tổng quan

Script `scripts/auto-heal.sh` giám sát Docker Swarm services và tự động restart các service bị lỗi.

### 4.2 Chức năng

- Phát hiện services với replicas = 0 hoặc health check fail
- Tự động force-update service để trigger restart
- **Throttling**: Tối đa 5 restart/service/giờ để tránh restart loop
- Webhook alerts khi restart xảy ra
- Mode `--dry-run` để test mà không restart

### 4.3 Sử dụng

```bash
# Chạy 1 lần
bash scripts/auto-heal.sh

# Dry run (chỉ kiểm tra, không restart)
bash scripts/auto-heal.sh --dry-run

# Chạy liên tục (cron mỗi 5 phút)
*/5 * * * * /opt/ivf/scripts/auto-heal.sh >> /var/log/ivf/auto-heal.log 2>&1
```

### 4.4 Webhook Alert

Set biến `ALERT_WEBHOOK` để nhận thông báo:

```bash
export ALERT_WEBHOOK="https://hooks.slack.com/services/..."
bash scripts/auto-heal.sh
```

---

## 5. Disaster Recovery

### 5.1 Failover Script

`scripts/dr-failover.sh` thực hiện promote PostgreSQL standby thành primary:

```bash
# Dry run — kiểm tra replication lag & readiness
bash scripts/dr-failover.sh --dry-run

# Thực thi failover
bash scripts/dr-failover.sh

# Force failover (bỏ qua safety checks)
bash scripts/dr-failover.sh --force
```

**Quy trình failover:**
1. Kiểm tra replication lag < 1MB
2. Promote standby: `pg_ctl promote`
3. Cập nhật connection string trên API service
4. Verify new primary accessible

### 5.2 DR Drill Script

`scripts/dr-drill.sh` chạy kiểm tra tự động cho DR readiness:

```bash
# Basic drill (8 checks)
bash scripts/dr-drill.sh

# Full drill (bao gồm restore test)
bash scripts/dr-drill.sh --full
```

**8 kiểm tra:**
1. Primary PostgreSQL accessible
2. Standby PostgreSQL accessible
3. Replication streaming active
4. Backup files exist & recent
5. API healthy
6. Redis accessible
7. MinIO accessible
8. Backup restore test (chỉ `--full`)

### 5.3 Lịch chạy DR Drill khuyến nghị

| Tần suất       | Loại       | Ghi chú                   |
| --------------- | ---------- | ------------------------- |
| Hàng tuần       | Basic      | `dr-drill.sh`             |
| Hàng tháng      | Full       | `dr-drill.sh --full`      |
| Sau deploy lớn  | Basic      | Verify sau mỗi deployment |

---

## 6. Infrastructure Admin UI

### 6.1 Truy cập

- **URL**: `/admin/infrastructure`
- **Sidebar**: Nền tảng (Super Admin) → Hạ tầng
- **Quyền**: Platform Admin Only

### 6.2 Các Tab

| Tab         | Chức năng                                    |
| ----------- | -------------------------------------------- |
| Dashboard   | VPS metrics, CPU/RAM/Disk, alerts            |
| Swarm       | Docker services, nodes, scale, logs          |
| Health      | Health checks, component status              |
| S3          | MinIO bucket browser, upload/download        |
| Lưu trữ    | Data retention policies, execute, replica    |
| Giám sát    | Monitoring stack health, quick links         |

### 6.3 Tab Lưu trữ

- Bảng chính sách retention (entity type, thời gian giữ, hành động, trạng thái)
- Nút "Thực thi ngay" để chạy retention thủ công
- Kết quả thực thi (số policies, bản ghi đã xoá, lỗi)
- Trạng thái Read Replica (streaming replicas, replication slots, vai trò)

### 6.4 Tab Giám sát

- Health check cho từng service (Prometheus, Grafana, Loki)
- Links truy cập nhanh tới giao diện monitoring
- Trạng thái PostgreSQL replication

---

## 7. API Endpoints

Tất cả endpoints yêu cầu JWT authentication với quyền Admin.

### 7.1 Data Retention

| Method | Path                                          | Mô tả                          |
| ------ | --------------------------------------------- | ------------------------------- |
| GET    | `/api/admin/infrastructure/retention/policies` | Lấy danh sách chính sách       |
| POST   | `/api/admin/infrastructure/retention/execute`  | Thực thi retention thủ công     |

### 7.2 Read Replica

| Method | Path                                         | Mô tả                    |
| ------ | -------------------------------------------- | ------------------------- |
| GET    | `/api/admin/infrastructure/replica/status`    | Trạng thái replication    |

### 7.3 Monitoring

| Method | Path                                           | Mô tả                       |
| ------ | ---------------------------------------------- | ---------------------------- |
| GET    | `/api/admin/infrastructure/monitoring/status`   | Health check monitoring stack |

### 7.4 Existing Infrastructure Endpoints

| Method | Path                                           | Mô tả                           |
| ------ | ---------------------------------------------- | -------------------------------- |
| GET    | `/api/admin/infrastructure/metrics/{vps}`       | VPS metrics (CPU, RAM, Disk)     |
| GET    | `/api/admin/infrastructure/swarm/services`      | Docker Swarm services            |
| GET    | `/api/admin/infrastructure/swarm/nodes`          | Docker Swarm nodes               |
| POST   | `/api/admin/infrastructure/swarm/scale`          | Scale service replicas           |
| GET    | `/api/admin/infrastructure/health`               | System health checks             |
| GET    | `/api/admin/infrastructure/s3/status`            | MinIO status                     |
| GET    | `/api/admin/infrastructure/s3/objects`            | List S3 objects                  |

---

## Appendix: File Structure

```
docker-compose.monitoring.yml          # Monitoring stack deployment
docker-compose.production.yml          # Production config (2 replicas, resource limits)
docker/monitoring/
├── prometheus.yml                     # Scrape targets
├── alerts.yml                         # Alert rules (7 rules)
├── loki-config.yml                    # Log retention 30d
├── promtail-config.yml                # Docker log shipping
└── grafana/provisioning/datasources/
    └── datasources.yml                # Auto-provision data sources

scripts/
├── auto-heal.sh                       # Swarm auto-healing
├── dr-failover.sh                     # PostgreSQL failover
└── dr-drill.sh                        # DR readiness drill

src/IVF.Infrastructure/
├── Persistence/Interceptors/
│   └── ReadReplicaInterceptor.cs      # EF Core SELECT routing
└── Services/
    └── DataRetentionService.cs        # Retention + S3 archival

ivf-client/src/app/features/admin/
└── infrastructure-monitor/
    ├── infrastructure-monitor.component.ts     # 6 tabs, signals
    ├── infrastructure-monitor.component.html   # Vietnamese UI
    └── infrastructure-monitor.component.scss   # Tailwind + custom styles
```
