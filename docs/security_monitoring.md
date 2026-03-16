# Security Monitoring — Tài liệu vận hành

## Tổng quan kiến trúc

```
┌──────────────┐  SSH/OSSEC   ┌─────────────────────────────────┐
│  VPS1 (mgr)  │◄─────────────┤  Wazuh Agent (mỗi VPS)          │
│  VPS2 (wkr)  │              │  - FIM: /etc, /opt/ivf           │
└──────────────┘              │  - Rootcheck / SCA               │
                              │  - Vuln detector                 │
                              │  - Log analysis (syslog, docker) │
                              └─────────────────────────────────┘
                                           │
                              ┌─────────────────────────────────┐
                              │  Lynis (cron hàng tuần)         │
                              │  → .dat report                  │
                              │  → lynis-ship.sh → JSON         │
                              │  → MinIO ivf-documents/         │
                              └─────────────────────────────────┘
                                           │
                              ┌─────────────────────────────────┐
                              │  IVF API: GET /api/admin/lynis/ │
                              │  Angular: /admin/lynis          │
                              └─────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│ Docker Swarm — VPS1                                   │
│                                                      │
│  wazuh.manager  ──►  wazuh.indexer (OpenSearch)      │
│  wazuh.dashboard ◄──────────────────────────────┐   │
│       ▲                                          │   │
│  ivf-monitoring network (overlay)                │   │
│       │                                          │   │
│  Caddy → /wazuh/* → wazuh.dashboard:5601         │   │
│       │              (TLS skip verify)           │   │
└──────────────────────────────────────────────────────┘
```

---

## 1. Wazuh SIEM

### Triển khai lần đầu

```bash
# Trên VPS1 (manager node)
cd /opt/ivf
git pull origin main

# 1. Setup SSL certs + Docker secrets
bash scripts/deploy-wazuh.sh
```

Deploy script thực hiện:

1. Gen SSL certs cho wazuh.manager / wazuh.indexer / wazuh.dashboard
2. Tạo Docker secrets: `wazuh_indexer_password`, `wazuh_api_password`, `wazuh_dashboard_password`
3. `docker stack deploy wazuh`
4. Update Caddy stack với `caddyfile_v12`

### Truy cập Dashboard

| URL                         | Auth                                            |
| --------------------------- | ----------------------------------------------- |
| `https://natra.site/wazuh/` | HTTP Basic: `monitor` / `<monitoring_password>` |

> Sau khi qua basic auth, đăng nhập Wazuh bằng user `admin` / `<wazuh_indexer_password>` được lưu trong Docker secret.

### Quản lý agents

```bash
# Xem agents đã đăng ký
docker exec -it $(docker ps -qf name=wazuh.manager) \
  /var/ossec/bin/agent_control -l

# Force re-enroll agent trên VPS
# Trên VPS cần re-enroll (chạy bằng Ansible hoặc manual):
systemctl stop wazuh-agent
rm -f /var/ossec/etc/client.keys
systemctl start wazuh-agent
```

### Cấu hình quan trọng

| File                                                   | Mục đích                |
| ------------------------------------------------------ | ----------------------- |
| `docker/wazuh/config/wazuh_cluster/wazuh_manager.conf` | ossec.conf cho Manager  |
| `ansible/roles/wazuh-agent/templates/ossec.conf.j2`    | ossec.conf cho agents   |
| `ansible/roles/wazuh-agent/defaults/main.yml`          | IP manager, port, group |

#### Kích hoạt Vulnerability Detector

Vulnerability Detector trong Wazuh Manager config (`wazuh_manager.conf`):

```xml
<vulnerability-detection>
  <enabled>yes</enabled>
  <interval>12h</interval>
  <min_full_scan_interval>6h</min_full_scan_interval>
  <run_on_start>yes</run_on_start>
</vulnerability-detection>
```

### Alerts và tích hợp Discord

Thêm vào `wazuh_manager.conf`:

```xml
<integration>
  <name>custom-discord</name>
  <hook_url>https://discord.com/api/webhooks/YOUR_WEBHOOK</hook_url>
  <level>10</level>
  <alert_format>json</alert_format>
</integration>
```

---

## 2. Lynis Security Audit

### Cách hoạt động

```
Mỗi Chủ nhật 02:30 → lynis audit system
  → /var/log/lynis/reports/lynis-YYYY-MM-DD.dat
  → lynis-ship.sh parses .dat → JSON
  → mc cp → MinIO: ivf-documents/system/lynis/<hostname>/lynis-YYYY-MM-DD.json
  → MinIO: ivf-documents/system/lynis/<hostname>/latest.json (overwrite)
```

### Cấu hình MinIO endpoint

Trong `ansible/hosts.yml` hoặc `ansible/group_vars/all.yml`:

```yaml
all:
  vars:
    lynis_minio_endpoint: "http://45.134.226.56:9000"
    lynis_minio_access_key: "minioadmin"
    lynis_minio_secret_key: "minioadmin123"
```

### Chạy audit thủ công

```bash
# Trên VPS (sau khi Ansible đã deploy)
lynis audit system \
  --profile /etc/lynis/custom.prf \
  --report-file /var/log/lynis/reports/lynis-$(date +%Y-%m-%d).dat \
  --logfile /var/log/lynis/lynis.log \
  --no-colors --quiet

# Ship lên MinIO ngay
/usr/local/bin/lynis-ship.sh
```

### Đọc báo cáo Lynis — giải thích chỉ số

| Chỉ số                  | Mô tả                              | Mức tốt |
| ----------------------- | ---------------------------------- | ------- |
| **Hardening Index**     | Điểm bảo mật tổng thể (0-100)      | ≥ 70    |
| **Warnings**            | Vấn đề cần xử lý ngay              | = 0     |
| **Suggestions**         | Cải thiện bảo mật (không khẩn cấp) | < 30    |
| **Vulnerable packages** | Package có CVE đã biết             | = 0     |

### Xem báo cáo qua UI

Đăng nhập IVF Admin → **Lynis Audit** (`/admin/lynis`) → chọn host → xem 3 tab:

- **Tổng quan**: score, warnings, suggestions, system info
- **Lịch sử**: danh sách báo cáo theo thời gian
- **Chi tiết**: toàn bộ warnings và suggestions

### Xem báo cáo qua API

```bash
# Headers cần thiết: Authorization: Bearer <jwt>

# Danh sách hosts có report
GET /api/admin/lynis/hosts

# Danh sách reports (tất cả hosts)
GET /api/admin/lynis/reports

# Reports theo host
GET /api/admin/lynis/reports?hostname=vmi3129111

# Report cụ thể
GET /api/admin/lynis/reports/vmi3129111/2026-03-15

# Report mới nhất
GET /api/admin/lynis/reports/vmi3129111/latest
```

---

## 3. Best Practices & Hardening Checklist

### Hàng tuần (tự động)

- [ ] Lynis audit chạy lúc 02:30 CN → kiểm tra report mới trên `/admin/lynis`
- [ ] Wazuh alerts mức ≥ 10 → Discord notification
- [ ] Review vulnerable packages → `apt upgrade <package>`

### Hàng tháng (manual)

- [ ] Review Wazuh Dashboard → Agents → xem alerts theo mức độ
- [ ] Chạy `lynis audit system --pentest` trên mỗi VPS để kiểm tra sâu hơn
- [ ] Cập nhật Wazuh agents: thay `4.9.2` trong Ansible vars
- [ ] Kiểm tra Docker secrets còn hợp lệ: `docker secret ls`

### Xử lý khi Hardening Index < 60

**Các bước ưu tiên:**

1. **Fix warnings trước** — thường là cấu hình SSH, sudo, kernel params
2. Xem suggestions theo mã (LYNIS-xxxx):
   ```bash
   # Tìm suggestion chi tiết
   lynis show details LYNIS-xxxx
   ```
3. Một số cải thiện nhanh:

   ```bash
   # SSH hardening
   echo "Protocol 2" >> /etc/ssh/sshd_config
   echo "PermitRootLogin prohibit-password" >> /etc/ssh/sshd_config
   systemctl reload ssh

   # Kernel hardening
   cat >> /etc/sysctl.d/99-hardening.conf <<EOF
   kernel.dmesg_restrict = 1
   kernel.kptr_restrict = 2
   net.ipv4.conf.all.rp_filter = 1
   net.ipv4.conf.all.log_martians = 1
   net.ipv6.conf.all.accept_ra = 0
   EOF
   sysctl -p /etc/sysctl.d/99-hardening.conf

   # Disable compiler (nếu không cần develop trên server)
   chmod 000 /usr/bin/gcc 2>/dev/null || true
   chmod 000 /usr/bin/cc 2>/dev/null || true
   ```

### FIM (File Integrity Monitoring) — Wazuh

Wazuh agent theo dõi realtime các thư mục:

- `/etc` — cấu hình hệ thống
- `/opt/ivf` — code IVF platform

**Khi có alert FIM:**

1. Wazuh Dashboard → Modules → Integrity Monitoring
2. Xem file thay đổi + diff
3. Nếu unauthorized: `git diff` trên `/opt/ivf`, kiểm tra audit logs

### Rootkit Detection

Wazuh rootcheck chạy mỗi 12 giờ. Alert level ≥ 7 khi phát hiện:

- Hidden processes
- Hidden files
- Suspicious ports

**Xử lý:**

```bash
# Chạy rkhunter thủ công
rkhunter --check --skip-keypress --report-warnings-only

# Chamsrootkit
chkrootkit
```

---

## 4. Sự cố thường gặp

### Wazuh Dashboard không load

```bash
# Kiểm tra trạng thái
docker service ps wazuh_wazuh.dashboard --no-trunc

# Xem logs
docker service logs wazuh_wazuh.dashboard --tail 50

# Restart dashboard
docker service update --force wazuh_wazuh.dashboard
```

### Wazuh agent không kết nối

```bash
# Trên VPS agent
systemctl status wazuh-agent
tail -100 /var/ossec/logs/ossec.log | grep -i error

# Kiểm tra port 1514/1515 trên manager
nc -zv 45.134.226.56 1514
nc -zv 45.134.226.56 1515
```

### MinIO upload thất bại

```bash
# Test kết nối MinIO
mc alias set test http://45.134.226.56:9000 minioadmin minioadmin123
mc ls test/ivf-documents/system/lynis/

# Chạy lại ship script
/usr/local/bin/lynis-ship.sh
```

### Wazuh Indexer không healthy

```bash
# Kiểm tra OpenSearch health
docker exec -it $(docker ps -qf name=wazuh.indexer) \
  curl -sk -u admin:<password> https://localhost:9200/_cluster/health | python3 -m json.tool
```

---

## 5. Cấu trúc file MinIO cho Lynis

```
ivf-documents/
└── system/
    └── lynis/
        ├── vmi3129111/           ← hostname VPS1
        │   ├── lynis-2026-03-15.json
        │   ├── lynis-2026-03-22.json
        │   └── latest.json       ← luôn là report mới nhất
        └── vmi3129107/           ← hostname VPS2
            ├── lynis-2026-03-15.json
            └── latest.json
```

### Cấu trúc JSON của một Lynis report

```json
{
  "hostname": "vmi3129111",
  "report_date": "2026-03-15",
  "generated_at": "2026-03-15T02:35:12Z",
  "lynis_version": "3.1.1",
  "os": "Ubuntu 24.04.1 LTS",
  "kernel": "6.8.0-51-generic",
  "hardening_index": 72,
  "tests_executed": 254,
  "firewall_active": "yes",
  "malware_scanner": "clamav",
  "compiler_installed": "no",
  "warnings": ["AUTH-9262", "SSH-7408"],
  "suggestions": ["KRNL-6000", "FIRE-4513", "..."],
  "vulnerable_packages": [],
  "warning_count": 2,
  "suggestion_count": 18
}
```
