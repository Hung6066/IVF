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

| Service    | Port | Chức năng                     | External Access |
| ---------- | ---- | ----------------------------- | --------------- |
| Prometheus | 9090 | Metrics collection & alerting | `127.0.0.1` only, reverse proxy via Caddy `/prometheus/` |
| Grafana    | 3000 | Dashboards & visualization    | `127.0.0.1` only, reverse proxy via Caddy `/grafana/` |
| Loki       | 3100 | Log aggregation               | `127.0.0.1` only |
| Promtail   | —    | Log shipping từ Docker        | No port exposed |

### 1.1.1 Bảo mật Monitoring

**Port Binding**: Tất cả monitoring ports bind `127.0.0.1` only — không thể truy cập từ bên ngoài trực tiếp.

**Authentication**:

| Component  | Auth Method           | Credentials                    |
| ---------- | --------------------- | ------------------------------ |
| Prometheus | Basic Auth (`web.yml`)| `monitor:<password>`           |
| Grafana    | Built-in login        | `admin:<password>` (changed from default) |
| Loki       | No auth (localhost only) | N/A                         |

**Truy cập từ xa**: Grafana và Prometheus được reverse proxy qua Caddy với basic auth. MinIO Console chỉ truy cập qua SSH tunnel.

```bash
# Truy cập Grafana từ xa
https://natra.site/grafana/       # Basic auth: monitor/<password>

# Truy cập Prometheus từ xa
https://natra.site/prometheus/     # Basic auth: monitor/<password>

# Truy cập MinIO Console qua SSH tunnel
ssh -L 9001:localhost:9001 root@VPS_IP
# Mở http://localhost:9001
```

**Hardening áp dụng**:
- `--web.enable-admin-api` đã bị **gỡ bỏ** khỏi Prometheus (ngăn xóa data/shutdown từ API)
- `--web.config.file=/etc/prometheus/web.yml` — Basic Auth via bcrypt hash
- `--web.route-prefix=/prometheus` — Prometheus UI served under `/prometheus/` path
- `--web.external-url=https://natra.site/prometheus` — External URL for links/redirects
- Grafana: `GF_SECURITY_COOKIE_SECURE`, `GF_SECURITY_CONTENT_SECURITY_POLICY`, `GF_SNAPSHOTS_EXTERNAL_ENABLED=false`, disable gravatar, disable sign-up/org-create
- Caddy admin API: restricted `origins` chỉ cho private networks (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`)
- MinIO: ports **không publish** — chỉ truy cập qua internal Docker networking
- `security_opt: no-new-privileges:true` trên tất cả containers
- Prometheus self-scrape sử dụng basic auth credentials
- Caddy reverse proxy chuyển tiếp basic auth credentials tới Prometheus upstream via `header_up Authorization`

### 1.2 Caddy Reverse Proxy cho Monitoring

Caddy reverse proxy cung cấp truy cập external cho Grafana và Prometheus tại `natra.site`:

| Path             | Upstream              | Auth               | Ghi chú                                  |
| ---------------- | --------------------- | ------------------- | ----------------------------------------- |
| `/grafana/*`     | `ivf-grafana:3000`    | Caddy basic auth    | Grafana tự handle login sau khi qua auth  |
| `/prometheus/*`  | `ivf-prometheus:9090` | Caddy + upstream auth | Caddy gửi Authorization header tới Prometheus |

**Cấu hình Caddy** (trong `Caddyfile`, Docker config `caddyfile_v6`):

```caddyfile
handle /grafana* {
    basic_auth {
        monitor <bcrypt-hash>
    }
    reverse_proxy ivf-grafana:3000
}

handle /prometheus* {
    basic_auth {
        monitor <bcrypt-hash>
    }
    reverse_proxy ivf-prometheus:9090 {
        header_up Authorization "Basic <base64-credentials>"
    }
}
```

**Lưu ý**: MinIO Console không hỗ trợ sub-path proxy (JS assets sử dụng hardcoded absolute paths). Chỉ truy cập qua SSH tunnel.

### 1.3 Triển khai

```bash
# Khởi động monitoring stack
docker-compose -f docker-compose.monitoring.yml up -d

# Kiểm tra trạng thái
docker-compose -f docker-compose.monitoring.yml ps
```

### 1.4 Cấu hình

Các file cấu hình nằm trong `docker/monitoring/`:

```
docker/monitoring/
├── prometheus.yml                          # Scrape targets & global config
├── prometheus-web.yml                      # Basic auth configuration (bcrypt)
├── alerts.yml                              # 31 alert rules + 8 recording rules
├── loki-config.yml                         # Loki storage, retention, ruler, cache
├── promtail-config.yml                     # Docker log shipping + pipeline stages
├── loki-rules/
│   └── fake/
│       └── rules.yml                       # 9 Loki log-based alert rules
└── grafana/
    └── provisioning/
        ├── datasources/
        │   └── datasources.yml             # Auto-provision Prometheus + Loki
        └── dashboards/
            ├── dashboards.yml              # Dashboard provisioning config
            └── json/
                ├── ivf-system-overview.json # Service health, RED metrics, latency
                ├── ivf-logs-errors.json     # Log viewer, error tracking
                └── ivf-infrastructure.json  # Alerts, targets, Prometheus perf
```

### 1.5 Prometheus Exporters

Hệ thống sử dụng các exporter chuyên dụng để thu thập metrics từ infrastructure services:

| Service    | Exporter                | Swarm Service Name     | Port | Ghi chú                                           |
| ---------- | ----------------------- | ---------------------- | ---- | -------------------------------------------------- |
| IVF API    | Built-in (OpenTelemetry)| `ivf_api`              | 8080 | `/metrics` endpoint, scrape 10s                    |
| PostgreSQL | `postgres-exporter`     | `ivf_postgres-exporter`| 9187 | Kết nối DB qua Docker secret                       |
| Redis      | `redis-exporter`        | `ivf_redis-exporter`   | 9121 | Kết nối `redis://redis:6379`                       |
| MinIO      | Built-in                | `minio-metrics`        | 9000 | `/minio/v2/metrics/cluster` (needs `MINIO_PROMETHEUS_AUTH_TYPE=public`) |
| Caddy      | Built-in                | `ivf_caddy`            | 2019 | Native Prometheus endpoint                         |
| Prometheus | Self-monitoring         | `ivf-prometheus`       | 9090 | `localhost:9090/prometheus/metrics` (route-prefix)  |

**Network**: Tất cả exporters kết nối vào cả `ivf-data` (truy cập services) và `ivf-monitoring` (Prometheus scrape).

### 1.6 Prometheus Scrape Targets

| Target     | Endpoint                                   | Interval |
| ---------- | ------------------------------------------ | -------- |
| IVF API    | `ivf_api:8080/metrics`                     | 10s      |
| PostgreSQL | `ivf_postgres-exporter:9187/metrics`       | 15s      |
| Redis      | `ivf_redis-exporter:9121/metrics`          | 15s      |
| MinIO      | `minio-metrics:9000/minio/v2/metrics/cluster`  | 15s      |
| Caddy      | `ivf_caddy:2019/metrics`                   | 15s      |
| Prometheus | `localhost:9090/prometheus/metrics`        | 15s      |

### 1.7 Alert Rules (Prometheus)

**9 nhóm alert, 31 rules + 8 recording rules:**

#### API Alerts

| Alert                | Điều kiện                     | Severity | For  |
| -------------------- | ----------------------------- | -------- | ---- |
| ApiDown              | Instance unreachable           | critical | 1m   |
| ApiAllInstancesDown  | Tất cả instances down          | critical | 30s  |
| HighLatencyWarning   | P95 > 1s                      | warning  | 5m   |
| HighLatencyCritical  | P95 > 3s                      | critical | 3m   |
| P99LatencyExtreme    | P99 > 5s                      | warning  | 5m   |
| SlowEndpoint         | Endpoint P95 > 5s             | warning  | 10m  |
| ErrorRateWarning     | 5xx > 1%                      | warning  | 5m   |
| ErrorRateCritical    | 5xx > 5%                      | critical | 3m   |
| High4xxRate          | 4xx > 25%                     | warning  | 10m  |
| NoTraffic            | Zero requests                 | warning  | 10m  |
| HighMemoryWarning    | Memory > 800MB                | warning  | 10m  |
| HighMemoryCritical   | Memory > 950MB                | critical | 5m   |
| HighGCPressure       | Gen2 GC > 0.5/s               | warning  | 10m  |
| ThreadPoolStarvation | Queue length > 100            | warning  | 5m   |
| TrafficSpike         | 3x normal rate                | warning  | 10m  |

#### Infrastructure Alerts

| Alert                    | Điều kiện                    | Severity | For  |
| ------------------------ | ---------------------------- | -------- | ---- |
| PostgresDown             | PG unreachable               | critical | 1m   |
| RedisDown                | Redis unreachable            | warning  | 1m   |
| MinioDown                | MinIO unreachable            | critical | 2m   |
| CaddyDown                | Caddy unreachable            | critical | 1m   |
| PostgresHighConnections  | Connections > 80% max        | warning  | 5m   |
| PostgresReplicationLag   | Replication lag > 100MB      | warning  | 5m   |
| PostgresDeadlocks        | Deadlocks detected           | warning  | 0m   |
| RedisHighMemory          | Memory > 80% maxmemory      | warning  | 5m   |
| RedisHighClients         | Connected clients > 100     | warning  | 5m   |
| RedisRejectedConnections | Rejected connections         | critical | 0m   |
| MinioHighDiskUsage       | Disk > 80%                  | warning  | 10m  |
| MinioDiskCritical        | Disk > 90%                  | critical | 5m   |

#### Prometheus Self-Monitoring

| Alert                           | Điều kiện              | Severity |
| ------------------------------- | ---------------------- | -------- |
| PrometheusConfigReloadFailed    | Config reload failed   | warning  |
| PrometheusTargetDown            | Any target down > 5m   | warning  |
| PrometheusTSDBCompactionsFailing| TSDB compaction errors | warning  |
| PrometheusRuleEvaluationFailures| Rule eval failures     | warning  |

#### Recording Rules (Pre-computed queries)

| Rule Name                          | Mô tả                       |
| ---------------------------------- | ---------------------------- |
| `ivf:http_requests:rate5m`         | Tổng request rate/s          |
| `ivf:http_requests_by_status:rate5m`| Request rate theo status code|
| `ivf:http_requests_by_route:rate5m` | Request rate theo endpoint  |
| `ivf:http_error_rate:ratio5m`      | Tỉ lệ lỗi 5xx/total        |
| `ivf:http_latency_p50:5m`         | P50 latency                  |
| `ivf:http_latency_p95:5m`         | P95 latency                  |
| `ivf:http_latency_p99:5m`         | P99 latency                  |
| `ivf:api_availability:ratio`      | % API instances UP           |

### 1.8 Alert Rules (Loki — Log-based)

**3 nhóm, 9 rules** — Dựa trên phân tích log patterns:

| Alert                   | Nguồn log       | Điều kiện                          | Severity |
| ----------------------- | --------------- | ---------------------------------- | -------- |
| HighErrorLogRate        | API logs        | > 20 error logs/5min               | warning  |
| UnhandledExceptions     | API logs        | NullRef/StackOverflow/OOM detected | critical |
| DatabaseConnectionErrors| API logs        | Npgsql exceptions > 3/5min        | critical |
| RedisConnectionErrors   | API logs        | Redis exceptions > 5/5min         | warning  |
| BruteForceAttempt       | API logs        | > 20 auth failures/5min           | warning  |
| ForbiddenAccessSpike    | API logs        | > 15 forbidden responses/5min     | warning  |
| PostgresFatalError      | PostgreSQL logs | FATAL/PANIC detected              | critical |
| MinioErrors             | MinIO logs      | > 5 errors/5min                   | warning  |
| ContainerRestarting     | All containers  | > 3 restart events/10min          | warning  |

### 1.9 Grafana Dashboards

3 dashboards tự động provisioning vào thư mục **IVF System**:

#### IVF System Overview (`/d/ivf-system-overview`)

| Row               | Panels                                                                |
| ------------------ | --------------------------------------------------------------------- |
| Service Health     | 6 stat panels: API, PostgreSQL, Redis, MinIO, Caddy, Prometheus (UP/DOWN) |
| RED Metrics        | Request Rate, Error Rate (5xx), P95 Latency, API Memory              |
| HTTP Traffic       | Request rate by status code, Response time percentiles (P50/P95/P99) |
| Top Endpoints      | Request rate by endpoint (stacked), Slowest endpoints P95 (top 10)   |
| API Resources      | Process memory (RSS/Virtual/GC Heap), .NET Runtime (GC, threads)     |

#### IVF Logs & Errors (`/d/ivf-logs-errors`)

| Row                | Panels                                                      |
| ------------------- | ------------------------------------------------------------ |
| Error Stats         | 4 stat panels: Errors, Exceptions, DB Errors, Auth Failures (last 1h) |
| Error Timeline      | Error rate over time (Exceptions/Errors/Warnings)           |
| Error Log Viewer    | Live log panel with error/exception filter                  |
| All API Logs        | Unfiltered API log stream (collapsed)                       |
| Database Logs       | PostgreSQL logs + error filter (collapsed)                  |
| Security & Auth     | Auth/permission logs (collapsed)                            |
| Reverse Proxy       | Caddy access/error logs (collapsed)                         |
| Storage & Services  | Redis, MinIO, SignServer, EJBCA logs (collapsed)            |

#### IVF Infrastructure & Alerts (`/d/ivf-infrastructure`)

| Row                | Panels                                                       |
| ------------------- | ------------------------------------------------------------- |
| Active Alerts       | Alert list (firing/pending/error states)                     |
| Prometheus Targets  | Scrape target status table (UP/DOWN per job)                 |
| Prometheus Perf.    | Scrape duration, Samples scraped, Rule eval duration, TSDB   |
| Loki Log Volume     | Log volume by swarm_service, Error log volume                |

### 1.10 Promtail Pipeline

Promtail thu thập logs từ **tất cả Docker containers** trên host với pipeline stages:

```
Docker Container Logs
    │
    ▼
┌─────────────────────────────────────────────┐
│  1. Docker Service Discovery                │
│     Labels: container, compose_service,     │
│     swarm_service, stack, logstream          │
├─────────────────────────────────────────────┤
│  2. Regex: Extract .NET log level           │
│     (trce/dbug/info/warn/fail/crit)         │
│     → Label: level                          │
├─────────────────────────────────────────────┤
│  3. Drop: Health check noise                │
│     Pattern: (health|liveness|readiness).*200│
├─────────────────────────────────────────────┤
│  4. Regex: Extract HTTP request info        │
│     → Labels: http_method, http_status      │
└─────────────────────────────────────────────┘
    │
    ▼
  Loki (3100)
```

### 1.11 Loki Config

| Setting                     | Giá trị     | Mô tả                                |
| --------------------------- | ----------- | ------------------------------------- |
| `retention_period`          | 30d         | Tự động xóa logs cũ hơn 30 ngày      |
| `schema`                    | v13 (TSDB)  | Storage engine hiệu quả nhất         |
| `results_cache`             | 100MB       | Embedded cache cho query nhanh hơn     |
| `ingestion_rate_mb`         | 10 MB/s     | Rate limit ingestion                   |
| `ingestion_burst_size_mb`   | 20 MB       | Burst ingest cho spike                 |
| `per_stream_rate_limit`     | 5 MB/s      | Rate limit mỗi stream                 |
| `max_entries_limit_per_query`| 5000       | Giới hạn kết quả query                |
| `max_query_series`          | 500         | Giới hạn số series mỗi query          |
| `ruler`                     | enabled     | Log-based alerting (9 rules)           |

---

## 2. Data Retention & S3 Archival

### 2.1 Tổng quan

`DataRetentionService` chạy dưới dạng `IHostedService`, quét định kỳ (mặc định mỗi 24h) và áp dụng các chính sách retention:

- **Delete**: Xoá trực tiếp các bản ghi vượt quá retention period
- **Archive**: Export dữ liệu sang JSONL, upload lên MinIO bucket `ivf-audit-archive`, rồi xoá khỏi DB

### 2.2 Chính sách mặc định

Các chính sách retention được seed tự động trong `DatabaseSeeder`. Entity types phổ biến:

| Entity Type      | Retention | Action  |
| ---------------- | --------- | ------- |
| AuditLogEntry    | 365 ngày  | Archive |
| SecurityEvent    | 180 ngày  | Archive |
| ApiCallLog       | 90 ngày   | Delete  |
| UserLoginHistory | 365 ngày  | Archive |

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

| Tần suất       | Loại  | Ghi chú                   |
| -------------- | ----- | ------------------------- |
| Hàng tuần      | Basic | `dr-drill.sh`             |
| Hàng tháng     | Full  | `dr-drill.sh --full`      |
| Sau deploy lớn | Basic | Verify sau mỗi deployment |

---

## 6. Infrastructure Admin UI

### 6.1 Truy cập

- **URL**: `/admin/infrastructure`
- **Sidebar**: Nền tảng (Super Admin) → Hạ tầng
- **Quyền**: Platform Admin Only

### 6.2 Các Tab

| Tab       | Chức năng                                 |
| --------- | ----------------------------------------- |
| Dashboard | VPS metrics, CPU/RAM/Disk, alerts         |
| Swarm     | Docker services, nodes, scale, logs       |
| Health    | Health checks, component status           |
| S3        | MinIO bucket browser, upload/download     |
| Lưu trữ   | Data retention policies, execute, replica |
| Giám sát  | Monitoring stack health, quick links      |

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

| Method | Path                                           | Mô tả                       |
| ------ | ---------------------------------------------- | --------------------------- |
| GET    | `/api/admin/infrastructure/retention/policies` | Lấy danh sách chính sách    |
| POST   | `/api/admin/infrastructure/retention/execute`  | Thực thi retention thủ công |

### 7.2 Read Replica

| Method | Path                                       | Mô tả                  |
| ------ | ------------------------------------------ | ---------------------- |
| GET    | `/api/admin/infrastructure/replica/status` | Trạng thái replication |

### 7.3 Monitoring

| Method | Path                                          | Mô tả                         |
| ------ | --------------------------------------------- | ----------------------------- |
| GET    | `/api/admin/infrastructure/monitoring/status` | Health check monitoring stack |

### 7.4 Existing Infrastructure Endpoints

| Method | Path                                       | Mô tả                        |
| ------ | ------------------------------------------ | ---------------------------- |
| GET    | `/api/admin/infrastructure/metrics/{vps}`  | VPS metrics (CPU, RAM, Disk) |
| GET    | `/api/admin/infrastructure/swarm/services` | Docker Swarm services        |
| GET    | `/api/admin/infrastructure/swarm/nodes`    | Docker Swarm nodes           |
| POST   | `/api/admin/infrastructure/swarm/scale`    | Scale service replicas       |
| GET    | `/api/admin/infrastructure/health`         | System health checks         |
| GET    | `/api/admin/infrastructure/s3/status`      | MinIO status                 |
| GET    | `/api/admin/infrastructure/s3/objects`     | List S3 objects              |

---

## Appendix: File Structure

```
docker-compose.stack.yml               # Swarm stack (bao gồm exporters)
docker-compose.monitoring.yml          # Monitoring stack deployment
docker-compose.production.yml          # Production config (2 replicas, resource limits)
docker/monitoring/
├── prometheus.yml                     # Scrape targets (6 jobs)
├── alerts.yml                         # 31 alert rules + 8 recording rules
├── loki-config.yml                    # Log retention 30d, cache, ruler
├── promtail-config.yml                # Docker log shipping + pipeline stages
├── loki-rules/
│   └── fake/
│       └── rules.yml                  # 9 Loki log-based alert rules
└── grafana/provisioning/
    ├── datasources/
    │   └── datasources.yml            # Auto-provision Prometheus + Loki
    └── dashboards/
        ├── dashboards.yml             # Dashboard auto-provisioning
        └── json/
            ├── ivf-system-overview.json   # Service health, RED, latency
            ├── ivf-logs-errors.json       # Log viewer, error tracking
            └── ivf-infrastructure.json    # Alerts, targets, Prometheus perf

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
