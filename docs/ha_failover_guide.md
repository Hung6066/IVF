# IVF Platform — Hướng dẫn High Availability & Failover

Tài liệu này mô tả kiến trúc HA, các kịch bản sự cố và quy trình xử lý cụ thể cho hệ thống IVF chạy trên **Docker Swarm 2 node**.

---

## Mục lục

1. [Kiến trúc tổng quan](#1-kiến-trúc-tổng-quan)
2. [Kịch bản 1 — Service đơn lẻ crash](#2-kịch-bản-1--service-đơn-lẻ-crash)
3. [Kịch bản 2 — VPS1 (Manager) bị down](#3-kịch-bản-2--vps1-manager-bị-down)
4. [Kịch bản 3 — Cả 2 VPS bị down](#4-kịch-bản-3--cả-2-vps-bị-down)
5. [Cài đặt ban đầu](#5-cài-đặt-ban-đầu)
6. [Kiểm thử định kỳ (DR Drill)](#6-kiểm-thử-định-kỳ-dr-drill)
7. [Khôi phục VPS1 sau khi down](#7-khôi-phục-vps1-sau-khi-down)
8. [Monitoring & Alerting](#8-monitoring--alerting)
9. [Tham chiếu lệnh nhanh](#9-tham-chiếu-lệnh-nhanh)
10. [Runbook xử lý sự cố](#10-runbook-xử-lý-sự-cố)

---

## 1. Kiến trúc tổng quan

### 1.1 Sơ đồ hạ tầng

```
Internet
   │
   ▼
[DNS: natra.site → 45.134.226.56  (VPS1 — IP chính)]
                → 194.163.181.19 (VPS2 — backup, cần DNS failover thủ công)

┌─────────────────────────────────┐    ┌─────────────────────────────────┐
│  VPS1: 45.134.226.56            │    │  VPS2: 194.163.181.19           │
│  Hostname: vmi3129111           │    │  Hostname: vmi3129107           │
│  Role: Swarm MANAGER            │    │  Role: Swarm WORKER             │
│                                 │    │                                 │
│  Caddy (mode: global) → :80/443 │    │  Caddy (mode: global) → :80/443 │
│  ivf_api replica 1              │    │  ivf_api replica 2              │
│  ivf_db (primary)               │    │  ivf_db-standby (replica)       │
│  ivf_redis (primary)            │    │  ivf_redis-replica              │
│  ivf_minio                      │    │                                 │
│  ivf_signserver                 │    │  [Watchdog cron: */2 * * * *]   │
│  ivf_ejbca                      │    │  → ping VPS1 health             │
│  Prometheus, Grafana, Loki      │    │  → failover-manager.sh nếu down │
└─────────────────────────────────┘    └─────────────────────────────────┘
         │                                          │
         └──────────── Swarm Overlay ───────────────┘
                    (ivf-public, ivf-data)
```

### 1.2 Phân bổ service

| Service | VPS1 | VPS2 | Ghi chú |
|---|---|---|---|
| `ivf_api` | ✅ replica 1 | ✅ replica 2 | Spread 2 node — HA hoàn toàn |
| `ivf_caddy` | ✅ global | ✅ global | Chạy mọi node tự động |
| `ivf_db` | ✅ primary | — | Stateful, chỉ 1 node |
| `ivf_db-standby` | — | ✅ streaming | Readonly replica |
| `ivf_redis` | ✅ primary | — | Stateful, chỉ 1 node |
| `ivf_redis-replica` | — | ✅ | Replicate từ primary |
| `ivf_minio` | ✅ | — | Object storage, cần backup riêng |
| `ivf_signserver` | ✅ | — | PKI service, stateful |
| `ivf_frontend` | ✅ | — | Static SPA, thấp risk |
| Monitoring | ✅ | — | Prometheus/Grafana/Loki |

### 1.3 Thời gian phục hồi mục tiêu (RTO/RPO)

| Kịch bản | RTO mục tiêu | RPO mục tiêu | Cơ chế |
|---|---|---|---|
| Container crash | < 30 giây | 0 | Swarm restart_policy |
| VPS1 down (có VPS2 watchdog) | 6–10 phút | < 2 phút | watchdog-vps1.sh |
| VPS1 down (GitHub Actions) | 10–15 phút | < 2 phút | auto-heal.yml |
| Cả 2 VPS down và reboot | 3–10 phút sau khi VPS1 boot | Từ backup | force-redeploy |
| Cả 2 VPS down hoàn toàn | Thủ công | Từ backup s3/pg_dump | restore-pitr.sh |

---

## 2. Kịch bản 1 — Service đơn lẻ crash

### 2.1 Cơ chế tự động

Docker Swarm tự động xử lý thông qua `restart_policy` trong `docker-compose.stack.yml`:

```yaml
restart_policy:
  condition: on-failure
  delay: 10s
  max_attempts: 5
  window: 120s
```

**Luồng:** Container crash → Swarm scheduler → Tạo container mới trong 10–30 giây.

### 2.2 Auto-heal script (backup)

Script `scripts/auto-heal.sh` chạy cron mỗi 2 phút trên VPS1 để bắt các trường hợp Swarm không tự heal được:

```bash
# Crontab hiện tại trên VPS1
*/2 * * * * /opt/ivf/scripts/auto-heal.sh >> /var/log/ivf-autoheal.log 2>&1
```

### 2.3 Kiểm tra thủ công

```bash
# SSH vào VPS1
ssh root@45.134.226.56

# Xem trạng thái tất cả services
docker service ls

# Xem lịch sử tasks của service cụ thể
docker service ps ivf_api --no-trunc

# Force restart một service
docker service update --force ivf_api

# Xem logs service
docker service logs ivf_api --tail 50 -f
```

---

## 3. Kịch bản 2 — VPS1 (Manager) bị down

> ⚠️ **Nghiêm trọng nhất trong vận hành thường ngày.** VPS1 chứa Swarm manager, PostgreSQL primary, MinIO, SignServer.

### 3.1 Ảnh hưởng tức thời

| Thứ sau khi VPS1 down | Trạng thái |
|---|---|
| T+0 | Caddy trên VPS2 vẫn nhận traffic, nhưng API replica trên VPS1 mất |
| T+0 | ivf_api replica 2 trên VPS2 **vẫn chạy** (sau khi fix placement) |
| T+0 | DB writes **không thể thực hiện** (primary trên VPS1) |
| T+0 | Swarm không thể schedule task mới (mất quorum) |
| T+6 phút | Watchdog VPS2 trigger failover (3 checks × 2 phút) |

> **Quan trọng:** Sau khi fix `placement` (commit `359af9e`), `ivf_api` replica trên VPS2 **sẽ tiếp tục phục vụ traffic đọc**. Chỉ các operations cần ghi DB mới bị lỗi cho đến khi Postgres standby được promote.

### 3.2 Tự động: Watchdog cron trên VPS2

File: `scripts/watchdog-vps1.sh` — chạy mỗi 2 phút.

**Luồng tự động:**

```
VPS2 cron: */2 * * * *
  → Ping https://natra.site/api/health/live
  → HTTP 200? → Reset fail counter, sleep
  → HTTP ≠ 200? → fail_count++
  → fail_count >= 3 (sau ~6 phút)?
      → Kiểm tra lock file (tránh chạy song song)
      → Gọi failover-manager.sh
      → Alert Discord
```

**failover-manager.sh thực thi:**

1. **Xác nhận VPS1 thực sự down** (test 3 lần × 5 giây)
2. **Kiểm tra VPS2 role** trong Swarm
3. **Force-new-cluster** (nếu VPS2 là worker, self-promote thành manager)
4. **Gán node labels** `role=primary` cho VPS2 (để services có constraint schedule được)
5. **Promote PostgreSQL standby → primary**
6. **Promote Redis replica → master**
7. **Redeploy stack** (services reschedule lên VPS2)
8. **Verify** health endpoint, alert Discord kết quả

**Thời gian ước tính:** 8–12 phút từ khi VPS1 down.

### 3.3 Thủ công: Khi watchdog không chạy

Nếu watchdog chưa được cài hoặc cần failover ngay:

```bash
# SSH vào VPS2
ssh root@194.163.181.19

# Chạy failover script trực tiếp
bash /opt/ivf/scripts/failover-manager.sh

# Hoặc dry-run kiểm tra trước
bash /opt/ivf/scripts/failover-manager.sh --dry-run
```

**Failover thủ công từng bước (nếu script thất bại):**

```bash
# B1: Trên VPS2 — Force new Swarm cluster
docker swarm init --force-new-cluster

# B2: Gán labels để services schedule được
VPS2_NODE_ID=$(docker node ls --format '{{.ID}}' | head -1)
docker node update --label-add role=primary "$VPS2_NODE_ID"
docker node update --label-add role=standby "$VPS2_NODE_ID"

# B3: Promote PostgreSQL standby
STANDBY_CONTAINER=$(docker ps -q -f name=ivf_db-standby | head -1)
docker exec "$STANDBY_CONTAINER" pg_ctl promote -D /var/lib/postgresql/data

# B4: Promote Redis replica
REDIS_CONTAINER=$(docker ps -q -f name=ivf_redis-replica | head -1)
docker exec "$REDIS_CONTAINER" redis-cli SLAVEOF NO ONE

# B5: Redeploy stack
cd /opt/ivf
docker stack deploy -c docker-compose.stack.yml ivf --with-registry-auth

# B6: Kiểm tra
docker service ls
curl -sk https://natra.site/api/health/live
```

### 3.4 GitHub Actions: Trigger thủ công qua UI

Vào [GitHub → Actions → Auto-Heal & Health Watchdog → Run workflow]:
- `action: failover-to-vps2`
- `dry_run: false`

---

## 4. Kịch bản 3 — Cả 2 VPS bị down

> Ví dụ: mất điện datacenter, provider maintenance, network partition.

### 4.1 Tình huống A: Cả 2 reboot và tự boot lại

Docker Engine tự khởi động khi reboot (nếu `systemctl enable docker`). Swarm tasks cũng tự khởi động.

**Kiểm tra sau khi reboot:**

```bash
# Trên VPS1
ssh root@45.134.226.56
docker service ls          # Các service có Running không?
docker node ls             # VPS2 còn trong swarm?
curl localhost:8080/health/live   # API local OK?
```

Nếu `docker service ls` cho thấy `0/N` replicas:

```bash
# Force update tất cả services
for svc in $(docker service ls --format '{{.Name}}'); do
  docker service update --force "$svc"
done
```

### 4.2 Tình huống B: GitHub Actions trigger force-redeploy

GitHub Actions chạy mỗi 5 phút từ server GitHub (hoàn toàn ngoài hạ tầng của bạn). Khi phát hiện health fail và VPS1 SSH được:

1. SCP stack file mới nhất lên `/opt/ivf/`
2. SSH vào VPS1 → `systemctl start docker`
3. Kiểm tra Swarm state → reinit nếu cần
4. `docker stack deploy -c docker-compose.stack.yml ivf`
5. Đợi 60 giây → ping health
6. Alert Discord kết quả

**Trigger thủ công:**

Vào [GitHub → Actions → Auto-Heal & Health Watchdog → Run workflow]:
- `action: force-redeploy`

### 4.3 Tình huống C: Cả 2 VPS không boot lại được

Phải restore từ backup:

```bash
# Xem danh sách backup
ls /var/backups/ivf/ | sort -r | head -10

# Hoặc từ remote (nếu đã cấu hình backup-to-s3.sh)
# Restore database
bash scripts/restore-pitr.sh --backup-file ivf_db_YYYYMMDD.sql.gz

# Provision VPS mới, cài Docker, join swarm (hoặc init mới)
# Sau đó deploy stack
docker stack deploy -c docker-compose.stack.yml ivf
```

Chi tiết: xem [docs/backup_and_restore.md](./backup_and_restore.md).

---

## 5. Cài đặt ban đầu

### 5.1 Cài watchdog trên VPS2

```bash
# 1. Tạo thư mục
ssh root@194.163.181.19 "mkdir -p /opt/ivf/scripts /opt/ivf/logs"

# 2. Copy scripts
scp scripts/watchdog-vps1.sh scripts/failover-manager.sh \
    docker-compose.stack.yml \
    root@194.163.181.19:/opt/ivf/scripts/

# 3. Cấp quyền thực thi
ssh root@194.163.181.19 "chmod +x /opt/ivf/scripts/*.sh"

# 4. Copy stack file
scp docker-compose.stack.yml root@194.163.181.19:/opt/ivf/docker-compose.stack.yml

# 5. Cài crontab
ssh root@194.163.181.19 "crontab -l 2>/dev/null | { cat; echo '*/2 * * * * DISCORD_WEBHOOK_URL=YOUR_WEBHOOK /opt/ivf/scripts/watchdog-vps1.sh >> /var/log/ivf-watchdog.log 2>&1'; } | crontab -"

# 6. Kiểm tra crontab đã cài
ssh root@194.163.181.19 "crontab -l"
```

### 5.2 Cài auto-heal trên VPS1

```bash
# Cài crontab trên VPS1 (nếu chưa có)
ssh root@45.134.226.56 \
  "crontab -l 2>/dev/null | { cat; echo '*/2 * * * * AUTOHEAL_WEBHOOK_URL=YOUR_WEBHOOK /opt/ivf/scripts/auto-heal.sh >> /var/log/ivf-autoheal.log 2>&1'; } | crontab -"
```

### 5.3 Cài GitHub Actions secrets

Vào [GitHub → Settings → Secrets and variables → Actions → New repository secret]:

| Secret name | Giá trị | Cách lấy |
|---|---|---|
| `VPS1_SSH_KEY` | Nội dung `~/.ssh/id_rsa` của VPS1 | `cat ~/.ssh/id_rsa` |
| `VPS2_SSH_KEY` | Nội dung `~/.ssh/id_rsa` của VPS2 | `cat ~/.ssh/id_rsa` |
| `DISCORD_WEBHOOK` | Discord webhook URL | Discord → Server Settings → Integrations → Webhooks |
| `GHCR_PAT` | GitHub Personal Access Token | GitHub → Settings → Developer settings → PAT (scope: `read:packages`) |

**Tạo SSH key cho GitHub Actions (khuyến nghị dùng key riêng):**

```bash
# Tạo key pair mới cho GH Actions (không passphrase)
ssh-keygen -t ed25519 -C "github-actions@ivf" -f ~/.ssh/gh_actions_ivf -N ""

# Thêm public key vào authorized_keys trên VPS1
ssh root@45.134.226.56 "echo '$(cat ~/.ssh/gh_actions_ivf.pub)' >> ~/.ssh/authorized_keys"

# Thêm VPS2
ssh root@194.163.181.19 "echo '$(cat ~/.ssh/gh_actions_ivf.pub)' >> ~/.ssh/authorized_keys"

# Private key (gh_actions_ivf) → paste vào GitHub Secrets VPS1_SSH_KEY và VPS2_SSH_KEY
cat ~/.ssh/gh_actions_ivf
```

### 5.4 Kiểm tra node labels

```bash
ssh root@45.134.226.56 "docker node ls -q | xargs -I{} docker node inspect {} --format '{{.Description.Hostname}}: {{.Spec.Labels}}'"
```

Expected output:
```
vmi3129111: map[role:primary]     ← VPS1
vmi3129107: map[role:standby]     ← VPS2
```

Nếu VPS2 chưa có label `role:standby`:

```bash
VPS2_NODE_ID=$(ssh root@45.134.226.56 "docker node ls --filter 'hostname=vmi3129107' --format '{{.ID}}'")
ssh root@45.134.226.56 "docker node update --label-add role=standby $VPS2_NODE_ID"
```

---

## 6. Kiểm thử định kỳ (DR Drill)

> Khuyến nghị: chạy DR drill mỗi tháng, vào giờ thấp điểm (ví dụ: Chủ nhật 2:00 AM).

### 6.1 Drill 1: Kiểm tra watchdog hoạt động

```bash
# Trên VPS2 — dry run
bash /opt/ivf/scripts/watchdog-vps1.sh --dry-run

# Xem log watchdog
tail -50 /var/log/ivf-watchdog.log
```

### 6.2 Drill 2: Giả lập service crash

```bash
# Trên VPS1 — kill 1 container, quan sát Swarm restart
docker kill $(docker ps -q -f name=ivf_api | head -1)

# Quan sát tasks
watch -n2 'docker service ps ivf_api'
# Service phải Restart trong < 30 giây
```

### 6.3 Drill 3: Giả lập VPS1 network down (không reboot VPS1)

> ⚠️ **Chỉ làm trong giờ bảo trì, có thông báo trước.**

```bash
# Trên VPS1 — block incoming HTTP (giả lập down từ bên ngoài)
iptables -I INPUT -p tcp --dport 443 -j DROP
iptables -I INPUT -p tcp --dport 80 -j DROP

# Đợi watchdog VPS2 trigger (tối đa 10 phút)
# Theo dõi Discord để nhận alert

# Sau khi test xong — restore
iptables -D INPUT -p tcp --dport 443 -j DROP
iptables -D INPUT -p tcp --dport 80 -j DROP
```

### 6.4 Drill 4: Kiểm tra GitHub Actions auto-heal

Vào GitHub Actions → Auto-Heal workflow → Run workflow:
- `action: check-only`
- `dry_run: true`

Xem output log để xác nhận health check đúng.

### 6.5 Checklist sau DR drill

```
□ Health endpoint trả về 200 trong thời gian RTO mục tiêu
□ Alert Discord gửi đúng nội dung
□ Logs watchdog/failover không có ERROR ngoài dự kiến
□ PostgreSQL standby vẫn sync sau drill
□ Redis replica vẫn sync sau drill
□ Rollback VPS1 lại làm primary (xem phần 7)
```

---

## 7. Khôi phục VPS1 sau khi down

Sau khi VPS1 đã được sửa chữa và boot lại, cần bước quan trọng: **đưa VPS1 trở lại làm manager, hạ cấp VPS2 về worker, đảm bảo DB sync lại**.

### 7.1 Tình huống A: VPS1 reboot tự nhiên, VPS2 chưa failover

VPS1 boot lại → Docker auto-start → Swarm vẫn hoạt động bình thường. Không cần làm gì thêm.

### 7.2 Tình huống B: VPS2 đã failover (là manager hiện tại)

```bash
# Bước 1: Trên VPS1 — khởi động lại Docker, kiểm tra
ssh root@45.134.226.56
systemctl start docker
docker info | grep "Swarm"
# Nếu Swarm: inactive → join lại swarm với token từ VPS2

# Bước 2: Lấy join token từ VPS2
ssh root@194.163.181.19 "docker swarm join-token manager"
# Output: docker swarm join --token SWMTKN-1-xxxx 194.163.181.19:2377

# Bước 3: Trên VPS1 — join lại swarm với tư cách manager
ssh root@45.134.226.56 "docker swarm join --token SWMTKN-1-xxxx 194.163.181.19:2377"

# Bước 4: Gán lại label cho VPS1
VPS1_NODE=$(ssh root@194.163.181.19 "docker node ls --filter 'hostname=vmi3129111' --format '{{.ID}}'")
ssh root@194.163.181.19 "docker node update --label-add role=primary $VPS1_NODE"

# Bước 5: Tùy chọn — drain VPS2, chuyển services về VPS1
# (Nếu muốn VPS1 là chính, VPS2 là worker)
VPS2_NODE=$(ssh root@194.163.181.19 "docker node ls --filter 'hostname=vmi3129107' --format '{{.ID}}'")
ssh root@194.163.181.19 "docker node demote $VPS2_NODE"  # Hạ về worker

# Bước 6: Sync lại PostgreSQL
# VPS1 trở thành standby trước khi swap lại primary
# (Quá trình phức tạp — xem docs/backup_and_restore.md để restore DB từ backup nếu cần)

# Bước 7: Redeploy stack để reschedule services đúng vị trí
ssh root@45.134.226.56 "cd /opt/ivf && docker stack deploy -c docker-compose.stack.yml ivf"
```

### 7.3 Khôi phục PostgreSQL primary

```bash
# Option A: Restore từ backup (đơn giản nhất)
bash scripts/restore-pitr.sh --target vps1

# Option B: Tạo mới standby từ primary VPS2
# Trên VPS1 — xóa data cũ và chạy pg_basebackup từ VPS2
ssh root@45.134.226.56 << 'EOF'
  CONTAINER=$(docker ps -q -f name=ivf_db | head -1)
  # Stop current db
  docker service scale ivf_db=0
  # Wipe data
  docker volume rm ivf_postgres_data
  # Re-scale → Swarm sẽ pull data từ empty volume
  # (Sau đó restore từ backup hoặc pg_basebackup thủ công)
  docker service scale ivf_db=1
EOF
```

---

## 8. Monitoring & Alerting

### 8.1 Discord alerts hiện có

| Trigger | Nội dung alert |
|---|---|
| VPS1 fail lần 1/3 | `⚠️ VPS1 không phản hồi (lần 1/3)` |
| Failover bắt đầu | `🚨 IVF FAILOVER: VPS1 down, chuyển sang VPS2` |
| Failover thành công | `✅ Failover hoàn thành. API chạy trên VPS2` |
| Failover thất bại | `❌ Failover script thất bại! Cần can thiệp thủ công` |
| VPS1 recovered | `✅ VPS1 đã phục hồi` |
| Force redeploy OK | `✅ IVF Recovery: Force redeploy thành công` |

### 8.2 Grafana alerts liên quan

Log vào https://natra.site/grafana/ → Alerting → Alert rules. Kiểm tra:

- **IVF API Down** — `up{job="ivf-api"} == 0` trong 2 phút
- **IVF High Error Rate** — error rate > 10%
- **IVF DB Connection Failed** — connection errors

### 8.3 Kiểm tra log watchdog

```bash
# Trên VPS2
ssh root@194.163.181.19 "tail -100 /var/log/ivf-watchdog.log"

# Tìm các event failover
ssh root@194.163.181.19 "grep -E '(FAILOVER|ERROR|fail_count)' /var/log/ivf-watchdog.log | tail -50"
```

---

## 9. Tham chiếu lệnh nhanh

### Swarm management

```bash
# Xem tất cả services và trạng thái
docker service ls

# Xem chi tiết replicas của service
docker service ps ivf_api --no-trunc

# Xem nodes trong swarm
docker node ls

# Force update (restart) service
docker service update --force ivf_api

# Scale service
docker service scale ivf_api=2

# Xem logs realtime
docker service logs ivf_api -f --tail 100
```

### Health checks

```bash
# Health API (từ bên ngoài)
curl -sk https://natra.site/api/health/live
curl -sk https://natra.site/api/health/ready

# Health API (trực tiếp từ VPS1)
ssh root@45.134.226.56 \
  "docker exec \$(docker ps -q -f name=ivf_api | head -1) curl -s http://localhost:8080/health/live"

# PostgreSQL primary
ssh root@45.134.226.56 \
  "docker exec \$(docker ps -q -f name=ivf_db | head -1) pg_isready -U postgres"

# PostgreSQL standby lag (đơn vị bytes)
ssh root@194.163.181.19 \
  "docker exec \$(docker ps -q -f name=ivf_db-standby | head -1) \
   psql -U postgres -c 'SELECT pg_wal_lsn_diff(pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn()) AS lag_bytes;'"

# Redis replication
ssh root@45.134.226.56 \
  "docker exec \$(docker ps -q -f name=ivf_redis | head -1) redis-cli info replication | head -10"
```

### Swarm failover manual

```bash
# Promote VPS2 làm manager khẩn cấp
ssh root@194.163.181.19 "docker swarm init --force-new-cluster"

# Redeploy stack
ssh root@194.163.181.19 "cd /opt/ivf && docker stack deploy -c docker-compose.stack.yml ivf"

# Kiểm tra
ssh root@194.163.181.19 "docker service ls"
```

---

## 10. Runbook xử lý sự cố

### Sự cố: API trả về 502/503

```
1. Kiểm tra service
   $ docker service ls | grep api
   → Replicas 0/2? → docker service update --force ivf_api

2. Kiểm tra container logs
   $ docker service logs ivf_api --tail 50

3. Kiểm tra DB connection
   $ docker service logs ivf_api | grep -i "connection\|database"

4. Kiểm tra Redis connection
   $ docker service logs ivf_api | grep -i "redis"

5. Restart API service
   $ docker service update --force ivf_api

6. Nếu vẫn fail → restart DB
   $ docker service update --force ivf_db
   → Đợi 30 giây → test lại
```

### Sự cố: DB không thể ghi (read-only)

```
1. Kiểm tra PostgreSQL có phải primary không
   $ docker exec $(docker ps -q -f name=ivf_db | head -1) \
     psql -U postgres -c "SELECT pg_is_in_recovery();"
   → true = đây là standby (sai), false = primary (đúng)

2. Nếu primary bị mất (VPS1 down), promote standby
   $ docker exec $(docker ps -q -f name=ivf_db-standby | head -1) \
     pg_ctl promote -D /var/lib/postgresql/data

3. Cập nhật connection string nếu cần đổi host
   $ docker service update \
     --env-add ConnectionStrings__DefaultConnection="Host=db-standby;..." \
     ivf_api
```

### Sự cố: Swarm mất quorum

```
1. Symptom: "Error response from daemon: rpc error: code = DeadlineExceeded"

2. Kiểm tra nodes
   $ docker node ls
   → Node nào status = Down?

3. Force new cluster từ node còn sống
   $ docker swarm init --force-new-cluster
   (chỉ làm khi chắc chắn node cũ không thể recover)

4. Redeploy stack
   $ docker stack deploy -c /opt/ivf/docker-compose.stack.yml ivf

5. Sau khi VPS1 quay lại → join làm manager (xem mục 7.2)
```

### Sự cố: Caddy không cấp được SSL cert

```
1. Kiểm tra Caddy logs
   $ docker service logs ivf_caddy --tail 50 | grep -i "error\|acme\|cert"

2. Kiểm tra Let's Encrypt rate limit
   (max 5 failures per hostname per hour)

3. Xóa cache cert cũ
   $ docker exec $(docker ps -q -f name=ivf_caddy | head -1) \
     rm -rf /data/caddy/certificates/acme-v02.api.letsencrypt.org

4. Restart Caddy
   $ docker service update --force ivf_caddy
```

### Sự cố: MinIO không truy cập được

```
1. Minio chỉ chạy trên VPS1 (stateful service)
2. Nếu VPS1 down → uploads/downloads mới sẽ fail
3. API có fallback graceful nếu MinIO không available (tùy config)

4. Kiểm tra MinIO health
   $ curl -sk http://localhost:9000/minio/health/live

5. Restart MinIO
   $ docker service update --force ivf_minio

6. Nếu data volume corrupt → restore từ backup:
   $ bash scripts/restore-pitr.sh --service minio
```

---

## Phụ lục — Cấu trúc files liên quan

```
IVF/
├── docker-compose.stack.yml           # Swarm stack — placement, replicas, HA config
├── scripts/
│   ├── auto-heal.sh                   # VPS1 cron: restart unhealthy containers
│   ├── watchdog-vps1.sh               # VPS2 cron: ping VPS1, trigger failover
│   ├── failover-manager.sh            # VPS2: full failover procedure
│   └── dr-drill.sh                    # DR testing automation
├── .github/workflows/
│   ├── auto-heal.yml                  # GitHub: external health watchdog
│   └── deploy-production.yml          # GitHub: standard CI/CD deploy
└── docs/
    ├── ha_failover_guide.md           # ← File này
    ├── backup_and_restore.md          # Backup/restore DB, MinIO
    └── infrastructure_operations_guide.md  # Monitoring, alerting, DR
```
