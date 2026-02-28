# Backup & Restore — Hướng dẫn toàn diện

> Tài liệu mô tả hệ thống backup/restore của IVF Information System, bao gồm kiến trúc, các loại backup, API endpoints, quy trình khôi phục, và hướng dẫn vận hành.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Các loại Backup](#2-các-loại-backup)
3. [Quy trình Restore](#3-quy-trình-restore)
4. [API Endpoints](#4-api-endpoints)
5. [Hạ tầng Docker](#5-hạ-tầng-docker)
6. [Shell Scripts](#6-shell-scripts)
7. [Backup theo lịch (Scheduled)](#7-backup-theo-lịch)
8. [Tuân thủ 3-2-1](#8-tuân-thủ-3-2-1)
9. [Cloud Backup](#9-cloud-backup)
10. [Streaming Replication](#10-streaming-replication)
11. [Real-time Monitoring (SignalR)](#11-real-time-monitoring)
12. [Frontend UI](#12-frontend-ui)
13. [Vận hành & Troubleshooting](#13-vận-hành--troubleshooting)
    - 13.1 Backup khuyến nghị hàng ngày
    - 13.2 Chính sách lưu giữ (Retention Policy)
    - 13.3 Kiểm tra sức khỏe

---

## 1. Tổng quan kiến trúc

### 1.1 Sơ đồ hệ thống

```
┌───────────────────────────────────────────────────────────────────┐
│                        API Endpoints                             │
│  /api/admin/backup/*          /api/admin/data-backup/*           │
│  (CA Keys)                    (DB + MinIO + WAL + PITR)          │
└──────────┬──────────────────────────────┬─────────────────────────┘
           │                              │
  ┌────────▼────────┐           ┌─────────▼──────────┐
  │ BackupRestore   │           │ DataBackupService  │
  │ Service         │           │ (Orchestrator)     │
  │ (CA keys)       │           │ SignalR real-time   │
  └──┬─────┬────────┘           └──┬──────┬─────┬────┘
     │     │                       │      │     │
  Scripts Cloud               ┌────┘      │     └─────────┐
  (bash)  Ops                 │           │               │
                      ┌───────▼──────┐ ┌──▼────────┐ ┌───▼──────────┐
                      │ Database     │ │ MinIO     │ │ WAL Backup   │
                      │ BackupSvc   │ │ BackupSvc │ │ Service      │
                      │ (pg_dump)   │ │ (tar/cp)  │ │ (PITR/base)  │
                      └──────────────┘ └───────────┘ └──────┬───────┘
                                                            │
                                                     restore-pitr.sh
```

### 1.2 Dịch vụ hỗ trợ

| Service                      | Chức năng                            |
| ---------------------------- | ------------------------------------ |
| `BackupSchedulerService`     | Cron job tự động chạy backup         |
| `BackupComplianceService`    | Kiểm tra tuân thủ quy tắc 3-2-1      |
| `ReplicationMonitorService`  | Giám sát streaming replication       |
| `BackupIntegrityService`     | SHA-256 checksum cho mọi file backup |
| `BackupCompressionService`   | Nén Brotli cho cloud upload          |
| `CloudBackupProviderFactory` | Tạo cloud provider (S3/Azure/GCS)    |

### 1.3 Lưu trữ hoạt động

Mọi backup/restore operation được theo dõi trong bảng `BackupOperations`:

| Field                       | Mô tả                                         |
| --------------------------- | --------------------------------------------- |
| `OperationCode`             | Mã định danh duy nhất                         |
| `Type`                      | `Backup` hoặc `Restore`                       |
| `Status`                    | `Running`, `Completed`, `Failed`, `Cancelled` |
| `StartedAt` / `CompletedAt` | Thời gian bắt đầu/kết thúc                    |
| `ArchivePath`               | Đường dẫn file backup kết quả                 |
| `ErrorMessage`              | Chi tiết lỗi (nếu thất bại)                   |
| `StartedBy`                 | Username người thực hiện                      |
| `LogLinesJson`              | Log chi tiết (JSON) sau khi hoàn tất          |

---

## 2. Các loại Backup

### 2.1 CA Keys Backup (Chứng chỉ số)

**Service:** `BackupRestoreService`
**Script:** `scripts/backup-ca-keys.sh`

Sao lưu toàn bộ chứng chỉ và khóa bảo mật của EJBCA + SignServer.

**Nội dung full backup:**

- Thư mục `certs/` và `secrets/` cục bộ
- EJBCA persistent volume (`/opt/keyfactor/persistent`)
- SignServer persistent volume
- EJBCA database dump
- SignServer database dump
- File metadata (`backup-info.txt`)

**Nội dung keys-only backup:**

- Chỉ thư mục `certs/` và `secrets/`

**Output:** `ivf-ca-backup_{timestamp}.tar.gz` trong `backups/`
**Mã hóa:** Tùy chọn AES-256-CBC (OpenSSL) — sẽ hỏi mật khẩu khi tạo

```bash
# Tạo full backup
bash scripts/backup-ca-keys.sh

# Tạo keys-only backup
bash scripts/backup-ca-keys.sh --keys-only

# Chỉ định output directory
bash scripts/backup-ca-keys.sh --output /path/to/backup/
```

### 2.2 Database Backup (pg_dump)

**Service:** `DatabaseBackupService`

Sao lưu PostgreSQL bằng `pg_dump` qua Docker.

**Quy trình:**

1. `docker exec ivf-db pg_dump` → pipe qua `gzip` → copy ra host
2. Tính SHA-256, lưu file `.sha256` kèm theo
3. Kiểm tra integrity file gzip sau khi tạo

**Output:** `ivf_db_{timestamp}.sql.gz` + `ivf_db_{timestamp}.sql.gz.sha256`

### 2.3 MinIO Backup (Object Storage)

**Service:** `MinioBackupService`

Sao lưu toàn bộ dữ liệu MinIO (3 buckets).

**Buckets:**

- `ivf-documents` — Hồ sơ bệnh nhân
- `ivf-signed-pdfs` — PDF đã ký số
- `ivf-medical-images` — Ảnh y khoa

**Quy trình:**

1. `docker cp` từ `/data/{bucket}/` trong container `ivf-minio`
2. Đóng gói tar.gz + SHA-256 checksum

**Output:** `ivf_minio_{timestamp}.tar.gz` + `.sha256`

### 2.4 WAL Archiving (Continuous)

**Service:** `WalBackupService`

PostgreSQL Write-Ahead Log liên tục archive các thay đổi.

**Cấu hình:**

```
wal_level = replica
archive_mode = on
archive_command = 'cp %p /var/lib/postgresql/archive/%f'
archive_timeout = 300   # 5 phút
```

**Hoạt động:**

- Archive tự động mỗi khi WAL segment đầy (16 MB) hoặc mỗi 5 phút
- Scheduler chạy mỗi giờ để copy WAL từ container ra `backups/wal/`
- Giữ lại **14 ngày** WAL cục bộ (cửa sổ PITR 2 tuần)
- Có thể force switch WAL bằng tay

### 2.5 Base Backup (Physical)

**Service:** `WalBackupService`

Sao lưu vật lý toàn bộ PostgreSQL cluster bằng `pg_basebackup`.

```
docker exec ivf-db pg_basebackup -Ft -z -P --checkpoint=fast
```

**Output:** `ivf_basebackup_{timestamp}.tar.gz` + SHA-256

> **Quan trọng:** Base backup là điều kiện tiên quyết cho PITR. Nên tạo base backup đều đặn (ít nhất 1 lần/ngày).

### 2.6 Point-in-Time Recovery (PITR)

**Service:** `DataBackupService` → `WalBackupService` → `scripts/restore-pitr.sh`

Khôi phục database tới một thời điểm bất kỳ sử dụng base backup + WAL segments.

**Xem chi tiết tại [Mục 3.2 — PITR Restore](#32-pitr-restore).**

### 2.7 Cloud Backup

**Service:** `CloudBackupProviderFactory` + Providers

Đồng bộ backup lên cloud storage: AWS S3, Azure Blob, Google Cloud Storage, hoặc S3-compatible (MinIO, DigitalOcean Spaces).

**Xem chi tiết tại [Mục 9 — Cloud Backup](#9-cloud-backup).**

---

## 3. Quy trình Restore

### 3.1 Database Restore (pg_dump)

**Endpoint:** `POST /api/admin/data-backup/restore`
**Service:** `DatabaseBackupService.RestoreDatabaseAsync()`

**Quy trình an toàn 6 bước:**

```
1. Verify checksum     ─── Kiểm tra SHA-256 trước khi restore
        │
2. Restore to staging  ─── Giải nén vào DB tạm: ivf_db_staging
        │
3. Validate staging    ─── Đếm tables + rows, so sánh tính hợp lệ
        │
4. Atomic swap         ─── ALTER DATABASE RENAME:
        │                    ivf_db → ivf_db_pre_restore_{timestamp}
        │                    ivf_db_staging → ivf_db
        │
5. Reconnect           ─── App reconnect vào DB mới
        │
6. Cleanup             ─── Giữ 2 DB rollback gần nhất, xóa cũ hơn
```

**Rollback:** DB cũ được giữ lại dưới tên `ivf_db_pre_restore_{timestamp}`. Để rollback:

```sql
-- Nếu cần rollback
ALTER DATABASE ivf_db RENAME TO ivf_db_failed;
ALTER DATABASE ivf_db_pre_restore_20260226_091500 RENAME TO ivf_db;
```

### 3.2 PITR Restore

**Endpoint:** `POST /api/admin/data-backup/pitr-restore`
**Script:** `scripts/restore-pitr.sh`

PITR cho phép khôi phục database tới bất kỳ thời điểm nào giữa base backup và WAL mới nhất.

**Khi nào dùng PITR:**

- Khôi phục dữ liệu bị xóa nhầm tại thời điểm cụ thể
- Quay lại trạng thái trước một lỗi logic nghiêm trọng
- Disaster recovery khi pg_dump backup quá cũ

**Request:**

```json
{
  "baseBackupFile": "ivf_basebackup_20260226_082910.tar.gz",
  "targetTime": "2026-02-26 09:00:00",
  "dryRun": true
}
```

**7 bước restore:**

| Bước | Hành động           | Chi tiết                                                           |
| ---- | ------------------- | ------------------------------------------------------------------ |
| 1    | Safety dump         | Tạo pg_dump DB hiện tại trước khi restore                          |
| 2    | Stop PostgreSQL     | Dừng container database                                            |
| 3    | Preserve PGDATA     | Đổi tên PGDATA → `{path}_pre_pitr_{timestamp}`                     |
| 4    | Extract base backup | Giải nén base backup vào PGDATA                                    |
| 5    | Copy WAL segments   | Từ 3 nguồn: container archive + `backups/wal/` + extra WAL dir     |
| 6    | Configure recovery  | Tạo `recovery.signal` + `restore_command` + `recovery_target_time` |
| 7    | Start & promote     | Start PG ở recovery mode → chờ promote (tối đa 5 phút)             |

**Post-recovery:**

- Re-enable WAL archiving
- Recreate replication slots
- Verify (table count, row count, current LSN)
- Cleanup (giữ 2 PGDATA cũ gần nhất)

**Sử dụng script trực tiếp:**

```bash
# Dry-run — chỉ kiểm tra, không thực thi
bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226.tar.gz --dry-run

# Restore tới thời điểm cụ thể
bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226.tar.gz \
  --target-time "2026-02-26 10:30:00 UTC"

# Restore tới thời điểm mới nhất (replay toàn bộ WAL)
bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226.tar.gz --target-latest

# Bỏ qua xác nhận + thêm WAL từ thư mục khác
bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226.tar.gz \
  --target-time "2026-02-26 10:30:00 UTC" \
  --wal-dir /mnt/extra-wal/ \
  --yes
```

### 3.3 CA Keys Restore

**Endpoint:** `POST /api/admin/backup/restore`
**Script:** `scripts/restore-ca-keys.sh`

```bash
# Dry-run — kiểm tra archive hợp lệ
bash scripts/restore-ca-keys.sh --dry-run backups/ivf-ca-backup_20260226.tar.gz

# Full restore
bash scripts/restore-ca-keys.sh backups/ivf-ca-backup_20260226.tar.gz

# Keys only
bash scripts/restore-ca-keys.sh --keys-only backups/ivf-ca-backup_20260226.tar.gz

# Skip confirmation
bash scripts/restore-ca-keys.sh --yes backups/ivf-ca-backup_20260226.tar.gz
```

**Quy trình:**

1. Restore cert + secret files cục bộ
2. Restore EJBCA persistent data vào container
3. Restore SignServer persistent data vào container
4. Restore EJBCA database (drop → recreate → restore)
5. Reconcile keystore aliases + regenerate TSA cert nếu cần
6. Reactivate SignServer workers
7. Verify toàn bộ

Hỗ trợ archive đã mã hóa (`.enc`) — tự động decrypt bằng OpenSSL.

### 3.4 MinIO Restore

**Endpoint:** `POST /api/admin/data-backup/restore`

**Quy trình:**

1. Verify checksum SHA-256
2. Extract tar.gz vào thư mục tạm
3. `docker cp` từng bucket vào container MinIO

> ⚠️ MinIO restore **ghi đè** dữ liệu hiện tại (không có rollback tự động).

### 3.5 Cloud Download

**Endpoint:** `POST /api/admin/backup/cloud/download`

Download backup từ cloud storage. Tự động giải nén Brotli nếu file có extension `.br`.

### 3.6 Tóm tắt so sánh

| Loại                      | Ưu điểm             | Nhược điểm                          | RPO                 | RTO                |
| ------------------------- | ------------------- | ----------------------------------- | ------------------- | ------------------ |
| **pg_dump**               | Đơn giản, portable  | Chậm với DB lớn, không granular     | Tới backup gần nhất | Vài phút           |
| **PITR**                  | Granular tới giây   | Phức tạp hơn, cần base backup + WAL | Tới WAL mới nhất    | 5-15 phút          |
| **Streaming Replication** | Near-zero RPO       | Không quay lại quá khứ              | Gần 0               | Failover: < 1 phút |
| **CA Keys**               | Bảo vệ chứng chỉ số | Chỉ cert/keys                       | —                   | Vài phút           |

---

## 4. API Endpoints

Tất cả endpoints yêu cầu JWT authentication với policy `AdminOnly`.

### 4.1 CA Keys — `/api/admin/backup`

| Method | Path                      | Mô tả               | Request                                   | Response                         |
| ------ | ------------------------- | ------------------- | ----------------------------------------- | -------------------------------- |
| `GET`  | `/archives`               | Danh sách backup CA | —                                         | `BackupInfo[]`                   |
| `POST` | `/start`                  | Tạo backup CA       | `{ keysOnly?: bool }`                     | `{ operationId }`                |
| `POST` | `/restore`                | Restore CA          | `{ archiveFileName, keysOnly?, dryRun? }` | `{ operationId }`                |
| `GET`  | `/operations`             | Lịch sử operations  | —                                         | `BackupOperation[]`              |
| `GET`  | `/operations/{id}`        | Chi tiết + logs     | —                                         | `BackupOperation`                |
| `POST` | `/operations/{id}/cancel` | Hủy operation       | —                                         | `{ message }`                    |
| `GET`  | `/schedule`               | Cấu hình lịch       | —                                         | `BackupSchedule`                 |
| `PUT`  | `/schedule`               | Cập nhật lịch       | `UpdateScheduleRequest`                   | Updated config                   |
| `POST` | `/cleanup`                | Dọn backup cũ       | —                                         | `{ deletedCount, deletedFiles }` |

### 4.2 Cloud — `/api/admin/backup/cloud`

| Method   | Path           | Mô tả                           | Request                    | Response                  |
| -------- | -------------- | ------------------------------- | -------------------------- | ------------------------- |
| `GET`    | `/config`      | Cấu hình cloud (secrets masked) | —                          | `CloudConfig`             |
| `PUT`    | `/config`      | Cập nhật cấu hình               | `UpdateCloudConfigRequest` | `{ message, provider }`   |
| `POST`   | `/config/test` | Test kết nối                    | `TestCloudConfigRequest`   | `{ connected, provider }` |
| `GET`    | `/status`      | Trạng thái cloud storage        | —                          | `CloudStatusResult`       |
| `GET`    | `/list`        | Danh sách backup trên cloud     | —                          | `CloudBackupObject[]`     |
| `POST`   | `/upload`      | Upload backup lên cloud         | `{ archiveFileName }`      | `CloudUploadResult`       |
| `POST`   | `/download`    | Download từ cloud               | `{ objectKey }`            | `{ fileName, message }`   |
| `DELETE` | `/{objectKey}` | Xóa backup trên cloud           | —                          | `{ message }`             |

### 4.3 Data Backup (DB + MinIO) — `/api/admin/data-backup`

| Method   | Path            | Mô tả                  | Request                                               | Response                 |
| -------- | --------------- | ---------------------- | ----------------------------------------------------- | ------------------------ |
| `GET`    | `/status`       | Trạng thái DB + MinIO  | —                                                     | `DataBackupStatus`       |
| `POST`   | `/start`        | Tạo data backup        | `{ includeDatabase?, includeMinio?, uploadToCloud? }` | `{ operationId }`        |
| `POST`   | `/restore`      | Restore data           | `{ databaseBackupFile?, minioBackupFile? }`           | `{ operationId }`        |
| `POST`   | `/pitr-restore` | PITR restore           | `{ baseBackupFile, targetTime?, dryRun? }`            | `{ operationId }`        |
| `DELETE` | `/{fileName}`   | Xóa file backup        | —                                                     | `{ message }`            |
| `POST`   | `/validate`     | Kiểm tra tính toàn vẹn | `{ fileName }`                                        | `BackupValidationResult` |

### 4.4 Backup Strategies — `/api/admin/data-backup/strategies`

| Method   | Path        | Mô tả                | Request                           | Response                   |
| -------- | ----------- | -------------------- | --------------------------------- | -------------------------- |
| `GET`    | `/`         | Danh sách strategies | —                                 | `DataBackupStrategy[]`     |
| `POST`   | `/`         | Tạo strategy         | `CreateDataBackupStrategyRequest` | `{ id, message }`          |
| `GET`    | `/{id}`     | Chi tiết strategy    | —                                 | `DataBackupStrategy`       |
| `PUT`    | `/{id}`     | Cập nhật strategy    | `UpdateDataBackupStrategyRequest` | `{ message }`              |
| `DELETE` | `/{id}`     | Xóa strategy         | —                                 | `{ message }`              |
| `POST`   | `/{id}/run` | Chạy strategy ngay   | —                                 | `{ operationId, message }` |

### 4.5 WAL — `/api/admin/data-backup/wal`

| Method | Path            | Mô tả                    | Request | Response                           |
| ------ | --------------- | ------------------------ | ------- | ---------------------------------- |
| `GET`  | `/status`       | Trạng thái WAL + archive | —       | `WalStatusResponse`                |
| `POST` | `/enable`       | Bật WAL archiving        | —       | `{ message }`                      |
| `POST` | `/switch`       | Force switch WAL segment | —       | `{ message }`                      |
| `POST` | `/base-backup`  | Tạo base backup          | —       | `{ fileName, sizeBytes, message }` |
| `GET`  | `/base-backups` | Danh sách base backups   | —       | `DataBackupFile[]`                 |
| `GET`  | `/archives`     | Danh sách WAL archives   | —       | `WalArchiveListResponse`           |

### 4.6 Compliance — `/api/admin/data-backup/compliance`

| Method | Path | Mô tả                  | Response           |
| ------ | ---- | ---------------------- | ------------------ |
| `GET`  | `/`  | Báo cáo tuân thủ 3-2-1 | `ComplianceReport` |

### 4.7 Replication — `/api/admin/data-backup/replication`

| Method   | Path            | Mô tả                    | Request        | Response                      |
| -------- | --------------- | ------------------------ | -------------- | ----------------------------- |
| `GET`    | `/status`       | Trạng thái replication   | —              | `ReplicationStatus`           |
| `GET`    | `/guide`        | Hướng dẫn cài đặt 6 bước | —              | `ReplicationSetupGuide`       |
| `POST`   | `/slots`        | Tạo replication slot     | `{ slotName }` | `{ message }`                 |
| `DELETE` | `/slots/{name}` | Xóa replication slot     | —              | `{ message }`                 |
| `POST`   | `/activate`     | Kích hoạt WAL + slot     | —              | `ReplicationActivationResult` |

---

## 5. Hạ tầng Docker

### 5.1 PostgreSQL Primary (`ivf-db`)

```yaml
# docker-compose.yml
db:
  image: postgres:16-alpine
  container_name: ivf-db
  ports:
    - "5433:5432"
  volumes:
    - postgres_data:/var/lib/postgresql/data
    - postgres_archive:/var/lib/postgresql/archive
    - ./docker/postgres/init-wal-replication.sh:/docker-entrypoint-initdb.d/init-wal-replication.sh
```

**Init script** (`docker/postgres/init-wal-replication.sh`) cấu hình tự động:

- Tạo user `replicator` với quyền `REPLICATION LOGIN`
- Thêm HBA entry cho replication connections
- `ALTER SYSTEM SET`:
  - `wal_level = replica`
  - `archive_mode = on`
  - `archive_command = 'cp %p /var/lib/postgresql/archive/%f'`
  - `archive_timeout = 300`
  - `max_wal_senders = 5`
  - `max_replication_slots = 5`
  - `wal_keep_size = '256MB'`

### 5.2 PostgreSQL Standby (`ivf-db-standby`)

```yaml
# docker-compose.yml (profile: replication)
db-standby:
  image: postgres:16-alpine
  container_name: ivf-db-standby
  profiles: ["replication"]
  ports:
    - "5434:5432"
  volumes:
    - postgres_standby_data:/var/lib/postgresql/data
    - ./docker/postgres/standby-entrypoint.sh:/standby-entrypoint.sh
  entrypoint: ["/bin/bash", "/standby-entrypoint.sh"]
```

**Standby entrypoint** (`docker/postgres/standby-entrypoint.sh`):

1. Chờ primary sẵn sàng
2. Clone qua `pg_basebackup --slot=standby_slot -R`
3. Tạo `standby.signal`
4. Cấu hình `primary_conninfo` và `primary_slot_name`
5. Start PostgreSQL ở hot standby mode (read-only)

**Kích hoạt:**

```bash
docker compose --profile replication up -d db-standby
```

### 5.3 Volumes

| Volume                  | Mục đích             |
| ----------------------- | -------------------- |
| `postgres_data`         | PGDATA của primary   |
| `postgres_archive`      | WAL archive files    |
| `postgres_standby_data` | PGDATA của standby   |
| `minio_data`            | MinIO object storage |

---

## 6. Shell Scripts

### 6.1 `scripts/backup-ca-keys.sh`

Backup chứng chỉ CA (EJBCA + SignServer):

```bash
# Sử dụng
bash scripts/backup-ca-keys.sh [--keys-only] [--output /path/]
```

| Bước | Nội dung                                |
| ---- | --------------------------------------- |
| 1    | Copy `certs/` và `secrets/` cục bộ      |
| 2    | Export EJBCA persistent volume          |
| 3    | Export SignServer persistent volume     |
| 4    | pg_dump EJBCA database                  |
| 5    | pg_dump SignServer database             |
| 6    | Tạo metadata file                       |
| 7    | Đóng gói tar.gz, hỏi mã hóa AES-256-CBC |

### 6.2 `scripts/restore-ca-keys.sh`

Restore chứng chỉ CA:

```bash
# Sử dụng
bash scripts/restore-ca-keys.sh [--keys-only] [--dry-run] [--yes] <backup.tar.gz>
```

| Bước | Nội dung                                         |
| ---- | ------------------------------------------------ |
| 1    | Restore cert + secret files                      |
| 2    | Restore EJBCA persistent data                    |
| 3    | Restore SignServer persistent data               |
| 4    | Restore EJBCA database                           |
| 5    | Reconcile keystore aliases + regenerate TSA cert |
| 6    | Reactivate SignServer workers                    |
| 7    | Verify toàn bộ                                   |

Hỗ trợ archive mã hóa (`.enc`) — tự động decrypt.

### 6.3 `scripts/restore-pitr.sh`

PITR restore script:

```bash
# Sử dụng
bash scripts/restore-pitr.sh <base-backup.tar.gz> [OPTIONS]

# Options:
#   --target-time "YYYY-MM-DD HH:MM:SS [UTC]"  Thời điểm khôi phục
#   --target-latest                              Mới nhất (mặc định)
#   --dry-run                                    Chỉ kiểm tra
#   --wal-dir <path>                             Thư mục WAL bổ sung
#   --yes                                        Bỏ qua xác nhận
```

Xem chi tiết 7 bước tại [Mục 3.2](#32-pitr-restore).

---

## 7. Backup theo lịch

### 7.1 CA Keys Scheduler (`BackupSchedulerService`)

**Loại:** .NET `BackgroundService` chạy trong API process.

**Cấu hình** (lưu trong DB, seed từ `appsettings.json`):

| Field              | Mặc định    | Mô tả                                   |
| ------------------ | ----------- | --------------------------------------- |
| `Enabled`          | `true`      | Bật/tắt scheduler                       |
| `CronExpression`   | `0 2 * * *` | Cron 5-field (mỗi ngày 2:00 AM)         |
| `KeysOnly`         | `false`     | Chỉ backup keys                         |
| `RetentionDays`    | `90`        | Số ngày giữ backup (PKI rất quan trọng) |
| `MaxBackupCount`   | `30`        | Số backup tối đa                        |
| `CloudSyncEnabled` | `false`     | Tự động upload lên cloud                |

**Hoạt động:**

1. Kiểm tra cấu hình mỗi phút
2. Chờ tới cron match tiếp theo
3. Chạy backup → chờ hoàn tất (timeout 10 phút)
4. Auto-upload cloud nếu `CloudSyncEnabled`
5. Cleanup theo retention policy

### 7.2 Data Backup Strategies

Hỗ trợ nhiều strategy tùy chỉnh cho DB + MinIO backup.

**3 strategy mặc định (seeded):**

| Strategy                  | Lịch          | DB  | MinIO | Cloud | Giữ cục bộ       | Ghi chú                      |
| ------------------------- | ------------- | --- | ----- | ----- | ---------------- | ---------------------------- |
| Sao lưu đầy đủ hàng đêm   | `0 2 * * *`   | ✓   | ✓     | ✗     | 14 ngày / 14 bản | Full daily, cloud via weekly |
| Sao lưu DB mỗi 6 giờ      | `0 */6 * * *` | ✓   | ✗     | ✗     | 7 ngày / 28 bản  | Dense short-term coverage    |
| Sao lưu offsite hàng tuần | `0 3 * * 0`   | ✓   | ✓     | ✓     | 90 ngày / 12 bản | 3-2-1 offsite copy           |

> **Lưu ý:** Seeder chỉ chạy khi chưa có strategy nào (`AnyAsync()` guard). Với hệ thống đã triển khai, cập nhật thủ công qua Admin UI hoặc endpoint `PUT /api/admin/data-backup/strategies/{id}`.

**Ví dụ tạo strategy qua API:**

```json
{
  "name": "Daily Full Backup",
  "includeDatabase": true,
  "includeMinio": true,
  "cronExpression": "0 2 * * *",
  "uploadToCloud": false,
  "retentionDays": 14,
  "maxBackupCount": 14
}
```

**API:**

- `POST /api/admin/data-backup/strategies` — Tạo strategy
- `PUT /api/admin/data-backup/strategies/{id}` — Cập nhật
- `POST /api/admin/data-backup/strategies/{id}/run` — Chạy ngay

---

## 8. Tuân thủ 3-2-1

**Endpoint:** `GET /api/admin/data-backup/compliance`

Hệ thống đánh giá tuân thủ **quy tắc 3-2-1 backup**:

- **3** bản sao dữ liệu
- **2** loại lưu trữ khác nhau
- **1** bản offsite

### Bảng đánh giá

| Check                  | Điểm | Mô tả                            |
| ---------------------- | ---- | -------------------------------- |
| **3 copies**           |      |                                  |
| `copy_live_database`   | 1    | Database PostgreSQL đang chạy    |
| `copy_local_backup`    | 1    | Có pg_dump + MinIO backup cục bộ |
| `copy_cloud_offsite`   | 1    | Cloud storage có backup          |
| **2 storage types**    |      |                                  |
| `storage_local_disk`   | 1    | Backup trên ổ đĩa cục bộ         |
| `storage_object_cloud` | 1    | Backup trên cloud object storage |
| **1 offsite**          |      |                                  |
| `offsite_cloud`        | 1    | Ít nhất 1 bản offsite            |

### Bonus scoring

| Check              | Điểm | Mô tả                                |
| ------------------ | ---- | ------------------------------------ |
| `wal_archiving`    | +1   | WAL archiving đã bật                 |
| `replication`      | +1   | Streaming replication đang hoạt động |
| `base_backup`      | +1   | Có base backup                       |
| `backup_freshness` | +1   | Backup gần nhất < 24 giờ             |
| `pki_backup`       | +1   | Có bản sao lưu PKI / CA keys         |

**Tổng điểm:** 6 (rule) + tối đa 5 (bonus) = **11 điểm**

Field trong response: `ruleScore` (max 6), `bonusScore` (max 5), `summary.totalPkiBackups`.

Response bao gồm `recommendations[]` với gợi ý khắc phục cho các check thất bại.

---

## 9. Cloud Backup

### 9.1 Providers hỗ trợ

| Provider                 | SDK                             | Tính năng đặc biệt                                |
| ------------------------ | ------------------------------- | ------------------------------------------------- |
| **AWS S3**               | `AWSSDK.S3` + `TransferUtility` | Hỗ trợ S3-compatible (MinIO, DigitalOcean Spaces) |
| **Azure Blob**           | `Azure.Storage.Blobs`           | Auto-create container                             |
| **Google Cloud Storage** | `Google.Cloud.Storage.V1`       | Service account hoặc default credentials          |

### 9.2 Cấu hình

Lưu trong DB (`CloudBackupConfig` entity), seed từ `appsettings.json`:

```json
{
  "CloudBackup": {
    "Provider": "MinIO",
    "CompressionEnabled": true,
    "S3": {
      "Region": "us-east-1",
      "BucketName": "ivf-backups",
      "ServiceUrl": "http://localhost:9000",
      "ForcePathStyle": true,
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin"
    }
  }
}
```

### 9.3 Nén Brotli

Khi `CompressionEnabled = true`:

- Upload: File → nén Brotli (`.br`) → upload
- Download: Download → tự động giải nén `.br` → file gốc
- Response bao gồm: `compressionRatioPercent`, `compressionDurationMs`

### 9.4 Bảo mật

- Secrets được mask trong API responses (chỉ hiện 2 ký tự đầu + cuối)
- Cloud provider instance cached, invalidated khi config thay đổi
- Auto-create bucket/container nếu chưa tồn tại

---

## 10. Streaming Replication

### 10.1 Kiến trúc

```
┌──────────────┐    Streaming    ┌──────────────┐
│  Primary     │ ──────WAL────→  │  Standby     │
│  ivf-db      │    Replication  │  ivf-db-     │
│  port: 5433  │                 │  standby     │
│  Read/Write  │                 │  port: 5434  │
│              │                 │  Read-Only   │
└──────────────┘                 └──────────────┘
```

### 10.2 Thiết lập

**Bước 1 — Kích hoạt qua API:**

```
POST /api/admin/data-backup/replication/activate
```

→ Bật WAL archiving + tạo `standby_slot`

**Bước 2 — Start standby container:**

```bash
docker compose --profile replication up -d db-standby
```

### 10.3 Giám sát

**Endpoint:** `GET /api/admin/data-backup/replication/status`

Response:

```json
{
  "serverRole": "primary",
  "isReplicating": true,
  "currentLsn": "0/1A000148",
  "connectedReplicas": [
    {
      "applicationName": "walreceiver",
      "clientAddress": "172.20.0.5",
      "state": "streaming",
      "lagBytes": 0,
      "uptimeSeconds": 86400
    }
  ],
  "replicationSlots": [
    {
      "slotName": "standby_slot",
      "active": true,
      "retainedBytes": 16777216
    }
  ]
}
```

### 10.4 Quản lý Replication Slots

```bash
# Tạo slot mới
POST /api/admin/data-backup/replication/slots
{ "slotName": "my_standby_slot" }

# Xóa slot
DELETE /api/admin/data-backup/replication/slots/my_standby_slot
```

> Tên slot phải match pattern `^[a-zA-Z_][a-zA-Z0-9_]*$`

### 10.5 Cloud / External Replication

Replication qua internet tới cloud hoặc server bên ngoài, hỗ trợ cả PostgreSQL và MinIO.

#### Kiến trúc

```
IVF Server (Docker)                    Cloud / Remote Site
┌──────────────┐    SSL/TLS           ┌──────────────────┐
│  ivf-db      │ ──Streaming WAL──→   │  Remote PG       │
│  (Primary)   │    Replication       │  (Standby)       │
└──────────────┘                      └──────────────────┘

┌──────────────┐    TLS               ┌──────────────────┐
│  ivf-minio   │ ──mc mirror──────→   │  Remote S3/MinIO │
│  (3 buckets) │    Incremental Sync  │  (ivf-replica)   │
└──────────────┘                      └──────────────────┘
```

#### Backend Services

| Service                            | File                                                       | Loại          |
| ---------------------------------- | ---------------------------------------------------------- | ------------- |
| `CloudReplicationService`          | `src/IVF.API/Services/CloudReplicationService.cs`          | Singleton     |
| `CloudReplicationSchedulerService` | `src/IVF.API/Services/CloudReplicationSchedulerService.cs` | HostedService |

#### Cấu hình (Entity)

`CloudReplicationConfig` — single-row table, được tạo tự động khi chưa có.

| Nhóm              | Fields                                                                                                                                                       |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| DB Replication    | `DbReplicationEnabled`, `RemoteDbHost/Port/User/Password`, `RemoteDbSslMode`, `RemoteDbSlotName`, `RemoteDbAllowedIps`                                       |
| MinIO Replication | `MinioReplicationEnabled`, `RemoteMinioEndpoint/AccessKey/SecretKey`, `RemoteMinioBucket`, `RemoteMinioUseSsl`, `RemoteMinioSyncMode`, `RemoteMinioSyncCron` |
| Status            | `LastDbSyncAt/Status`, `LastMinioSyncAt/Status/Bytes/Files`                                                                                                  |

#### API Endpoints

Base: `/api/admin/data-backup/cloud-replication`

| Method | Path            | Mô tả                                   |
| ------ | --------------- | --------------------------------------- |
| GET    | `/config`       | Lấy cấu hình (secrets masked)           |
| PUT    | `/db/config`    | Cập nhật cấu hình DB replication        |
| POST   | `/db/test`      | Test kết nối tới remote DB              |
| POST   | `/db/setup`     | Thiết lập: tạo user, slot, pg_hba entry |
| GET    | `/db/status`    | Trạng thái replicas (external vs local) |
| PUT    | `/minio/config` | Cập nhật cấu hình MinIO replication     |
| POST   | `/minio/test`   | Test kết nối tới remote S3/MinIO        |
| POST   | `/minio/setup`  | Tạo mc alias + remote bucket            |
| POST   | `/minio/sync`   | Sync ngay lập tức (mc mirror)           |
| GET    | `/minio/status` | Trạng thái MinIO replication            |
| GET    | `/guide`        | Hướng dẫn thiết lập step-by-step        |

#### Bảo mật

- **PostgreSQL:** SSL mode `require` / `verify-ca` / `verify-full`, IP whitelisting qua `pg_hba.conf`
- **MinIO:** TLS (HTTPS) cho mọi kết nối, access/secret key qua biến môi trường
- **Mạng:** Khuyến nghị dùng WireGuard VPN hoặc SSH tunnel cho kết nối internet
- **Secrets:** Passwords/keys được mask (`****`) trong API responses

#### MinIO Sync Modes

| Mode          | Mô tả                                                            |
| ------------- | ---------------------------------------------------------------- |
| `incremental` | Chỉ sync files mới/thay đổi (`mc mirror --overwrite`) — nhanh    |
| `full`        | Sync toàn bộ kể cả xóa remote files không còn local (`--remove`) |

Scheduler tự động sync theo cron expression (mặc định: `0 */2 * * *` — mỗi 2 giờ).

#### Frontend UI

Tab "🌐 Cloud Repl" trong nhóm Database, gồm:

- PostgreSQL external replication: status, config form, setup wizard
- MinIO S3 external replication: status, sync now, config form, setup
- Hướng dẫn chi tiết (setup guide) với security notes

---

## 11. Real-time Monitoring

### 11.1 SignalR Hub

**URL:** `/hubs/backup`
**Auth:** JWT (AdminOnly policy)

### 11.2 Client → Server

| Method                        | Mô tả                        |
| ----------------------------- | ---------------------------- |
| `JoinOperation(operationId)`  | Subscribe logs cho operation |
| `LeaveOperation(operationId)` | Unsubscribe                  |

### 11.3 Server → Client Events

| Event              | Payload                                                | Khi nào                   |
| ------------------ | ------------------------------------------------------ | ------------------------- |
| `LogLine`          | `{ operationId, timestamp, level, message }`           | Mỗi dòng log mới          |
| `StatusChanged`    | `{ operationId, status, completedAt?, errorMessage? }` | Operation thay đổi status |
| `OperationUpdated` | Broadcast operation update                             | Mọi thay đổi operation    |

### 11.4 Log Levels

| Level   | Màu     | Ý nghĩa         |
| ------- | ------- | --------------- |
| `INFO`  | Xám     | Thông tin chung |
| `OK`    | Xanh lá | Thành công      |
| `WARN`  | Vàng    | Cảnh báo        |
| `ERROR` | Đỏ      | Lỗi             |

### 11.5 Sử dụng trong Angular

```typescript
// Connect và subscribe
await backupService.connectHub(operationId);

backupService.logLine$.subscribe((line) => {
  console.log(`[${line.level}] ${line.message}`);
});

backupService.statusChanged$.subscribe((op) => {
  if (op.status !== "Running") {
    console.log("Operation finished:", op.status);
    backupService.disconnectHub();
  }
});
```

---

## 12. Frontend UI

### 12.1 Tổng quan

**Component:** `BackupRestoreComponent`
**Route:** `/admin/backup-restore`

Tabs được tổ chức theo nhóm:

| Nhóm               | Tab           | Icon | Chức năng                                      |
| ------------------ | ------------- | ---- | ---------------------------------------------- |
| _(ungrouped)_      | Tổng quan     | 📊   | Dashboard tổng quan hệ thống                   |
| **Database**       | PostgreSQL    | 🐘   | Tổng quan DB: size, tables, replication lag    |
|                    | WAL           | 📝   | WAL archiving + Base backup + **PITR Restore** |
|                    | Replication   | 🔄   | Streaming replication management               |
|                    | Cloud Repl    | 🌐   | Cloud/External replication (DB + MinIO)        |
| **Object Storage** | MinIO         | 📦   | Tổng quan MinIO: bucket sizes, object count    |
| **Sao lưu**        | Dữ liệu       | 💾   | DB + MinIO backup/restore                      |
|                    | Chiến lược    | 📋   | Data backup strategies (CRUD + run)            |
|                    | Lịch tự động  | ⏰   | Cấu hình cron scheduler cho CA keys            |
| **PKI**            | PKI / CA Keys | 🔐   | Archives + Restore CA keys (EJBCA/SignServer)  |
| **Giám sát**       | Cloud         | ☁️   | Quản lý cloud backup (cấu hình + upload)       |
|                    | 3-2-1         | 🛡️   | Báo cáo tuân thủ 3-2-1                         |
|                    | Lịch sử       | 📜   | Lịch sử operations                             |
| _(dynamic)_        | Logs          | 📋   | Live log viewer (hiện khi operation đang chạy) |

### 12.2 PITR Panel (trong tab WAL)

Panel PITR nằm cuối tab WAL, mở bằng nút "▼ Mở rộng":

1. **Chọn Base Backup** — Dropdown danh sách base backups
2. **Thời điểm khôi phục** — Datetime picker (để trống = latest)
3. **Dry Run** — Checkbox (mặc định bật) — chỉ kiểm tra, không thực thi
4. **Nút Start** — Hiện "🔍 Dry-Run PITR" hoặc "🚀 Chạy PITR Restore"
5. **Log viewer** — Terminal-style panel với màu sắc theo log level, real-time qua SignalR

### 12.3 Data Backup Panel

- Hiện trạng thái DB size, table count, MinIO bucket sizes
- Tạo backup (chọn DB/MinIO/cả hai + upload cloud)
- Restore từ dropdown danh sách
- Validate file backup (checksum + table count)
- Xóa backup files

---

## 13. Vận hành & Troubleshooting

### 13.1 Backup khuyến nghị hàng ngày

```
┌─────────────────────────────────────────────────────────┐
│  02:00 — Scheduled CA keys backup (auto)                │
│  02:30 — Data backup strategy: DB + MinIO (auto)        │
│  03:00 — Sao lưu offsite hàng tuần: upload lên cloud    │
│  Liên tục — WAL archiving (tự động mỗi 5 phút/16MB)    │
│  Liên tục — WAL copy sang host (mỗi 1 giờ)             │
│  Liên tục — Streaming replication (real-time)           │
└─────────────────────────────────────────────────────────┘
```

### 13.2 Chính sách lưu giữ (Retention Policy)

| Loại backup            | Lưu cục bộ       | Lưu cloud/offsite  | Ghi chú                             |
| ---------------------- | ---------------- | ------------------ | ----------------------------------- |
| **WAL archives**       | 14 ngày          | Upload từng giờ    | PITR window 2 tuần                  |
| **DB full (hàng đêm)** | 14 ngày / 14 bản | Qua weekly offsite | Weekly strategy giữ cloud 90 ngày   |
| **DB 6-hour**          | 7 ngày / 28 bản  | —                  | 4 bản/ngày × 7 ngày = coverage dày  |
| **MinIO (hàng đêm)**   | 14 ngày / 14 bản | Qua weekly offsite | Gộp chung với DB daily strategy     |
| **Weekly offsite**     | 90 ngày / 12 bản | 12 bản / 3 tháng   | Luật 3-2-1: bản sao offsite         |
| **PKI / CA Keys**      | 90 ngày / 30 bản | Thủ công           | Khóa CA không thể tái tạo — giữ lâu |

### 13.3 Kiểm tra sức khỏe

```bash
# Kiểm tra WAL archiving hoạt động
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/admin/data-backup/wal/status

# Kiểm tra replication
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/admin/data-backup/replication/status

# Kiểm tra compliance 3-2-1
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/admin/data-backup/compliance
```

### 13.4 Disaster Recovery Scenarios

#### Scenario 1: Dữ liệu bị xóa nhầm

**Giải pháp:** PITR restore tới thời điểm trước khi xóa.

```bash
# 1. Xác định thời điểm xóa (kiểm tra application logs)
# 2. Dry-run trước
POST /api/admin/data-backup/pitr-restore
{ "baseBackupFile": "ivf_basebackup_20260226.tar.gz", "targetTime": "2026-02-26 09:30:00", "dryRun": true }

# 3. Thực thi
POST /api/admin/data-backup/pitr-restore
{ "baseBackupFile": "ivf_basebackup_20260226.tar.gz", "targetTime": "2026-02-26 09:30:00", "dryRun": false }
```

#### Scenario 2: Database corruption

**Giải pháp:** pg_dump restore từ backup gần nhất.

```bash
POST /api/admin/data-backup/restore
{ "databaseBackupFile": "ivf_db_20260226_020000.sql.gz" }
```

#### Scenario 3: Server mất hoàn toàn

**Giải pháp:**

1. Setup server mới với Docker Compose
2. Restore CA keys: `bash scripts/restore-ca-keys.sh backups/ivf-ca-backup_*.tar.gz`
3. PITR restore database từ base backup + WAL
4. Restore MinIO từ backup
5. Kích hoạt lại replication

#### Scenario 4: Primary DB down, standby available

**Giải pháp:** Promote standby thành primary.

```bash
docker exec ivf-db-standby pg_ctl promote -D /var/lib/postgresql/data
```

### 13.5 Troubleshooting chung

| Vấn đề                             | Nguyên nhân                    | Giải pháp                                    |
| ---------------------------------- | ------------------------------ | -------------------------------------------- |
| WAL archiving không hoạt động      | `archive_mode=off`             | `POST /api/admin/data-backup/wal/enable`     |
| Replication lag cao                | Network chậm / standby quá tải | Kiểm tra `lagBytes` trong replication status |
| Backup operation stuck ở "Running" | Server restart giữa chừng      | Cancel operation qua API                     |
| PITR restore thất bại              | Thiếu WAL segments             | Kiểm tra WAL archive đủ, thêm `--wal-dir`    |
| pg_dump restore thất bại           | Active connections             | API tự disconnect, retry                     |
| Cloud upload thất bại              | Credentials hết hạn            | `POST /api/admin/backup/cloud/config/test`   |
| Base backup chậm                   | DB lớn                         | Sử dụng `--checkpoint=fast` (mặc định)       |

### 13.6 File layout

```
backups/
├── ivf-ca-backup_20260226_020000.tar.gz       # CA keys backup
├── ivf-ca-backup_20260226_020000.tar.gz.sha256
├── ivf_db_20260226_020000.sql.gz              # pg_dump backup
├── ivf_db_20260226_020000.sql.gz.sha256
├── ivf_minio_20260226_023000.tar.gz           # MinIO backup
├── ivf_minio_20260226_023000.tar.gz.sha256
├── ivf_basebackup_20260226_030000.tar.gz      # Base backup (PITR)
├── ivf_basebackup_20260226_030000.tar.gz.sha256
└── wal/                                        # WAL archive copies
    ├── 000000010000000000000001
    ├── 000000010000000000000002
    └── ...
```

### 13.7 Bảo mật

- Mọi endpoint yêu cầu JWT + role Admin
- CA backup hỗ trợ mã hóa AES-256-CBC
- Cloud secrets masked trong API responses
- File name validation chống path traversal (chỉ accept prefix `ivf_db_`, `ivf_minio_`, `ivf_basebackup_`)
- Replication slot names validated với regex
- SignalR hub yêu cầu `AdminOnly` policy
