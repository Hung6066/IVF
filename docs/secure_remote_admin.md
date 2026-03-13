# Secure Remote Admin Access — IVF System

> Enterprise-grade admin access following **Zero Trust** principles (Azure Bastion / AWS Systems Manager pattern).

## Kiến trúc bảo mật (Deployed 2026-03-13)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           INTERNET                                       │
│                                                                          │
│   ┌────────────┐                                                        │
│   │  Developer  │                                                        │
│   │ Workstation │                                                        │
│   │ 115.79.197.x│                                                        │
│   └──────┬─────┘                                                        │
│          │                                                               │
│          ├──── SSH (port 22) ─────────────────────────────┐              │
│          │     publickey + TOTP 2FA (khi activate)        │              │
│          │                                                │              │
│          └──── WireGuard (port 51820/udp) ──┐             │              │
│                10.200.0.2 → 10.200.0.1      │             │              │
│                                              │             │              │
│   ┌──── Fail2ban ────────────────────────────┼─────────────┼────────┐    │
│   │  sshd jail: 5 retries → ban 1h          │             │        │    │
│   │  recidive:  3 bans → ban 1 tuần         │             │        │    │
│   │  ignoreip: 115.79.197.0/24, VPN, LAN    │             │        │    │
│   │                                           │             │        │    │
│   │   ┌── VPS (45.134.226.56) ───────────────▼─────────────▼──┐    │    │
│   │   │                                                        │    │    │
│   │   │  ┌──── UFW Firewall ─────────────────────────────┐    │    │    │
│   │   │  │  ALLOW 22/tcp (SSH)        ← from anywhere     │    │    │    │
│   │   │  │  ALLOW 80,443/tcp (HTTP/S) ← from anywhere     │    │    │    │
│   │   │  │  ALLOW 51820/udp (WireGuard) ← from anywhere   │    │    │    │
│   │   │  │  ALLOW admin ports         ← from wg0 only     │    │    │    │
│   │   │  │  DENY  admin ports         ← from WAN          │    │    │    │
│   │   │  └───────────────────────────────────────────────┘    │    │    │
│   │   │                                                        │    │    │
│   │   │  ┌──── Docker Overlay Networks ──────────────────┐    │    │    │
│   │   │  │                                                │    │    │    │
│   │   │  │  ┌──────────┐  ┌────────────┐  ┌───────────┐ │    │    │    │
│   │   │  │  │  EJBCA   │  │ SignServer │  │   MinIO   │ │    │    │    │
│   │   │  │  │  :8443   │  │   :9443    │  │   :9001   │ │    │    │    │
│   │   │  │  │ mTLS cert│  │  mTLS cert │  │ internal  │ │    │    │    │
│   │   │  │  └──────────┘  └────────────┘  └───────────┘ │    │    │    │
│   │   │  │                                                │    │    │    │
│   │   │  │  ┌──────────┐  ┌────────────┐  ┌───────────┐ │    │    │    │
│   │   │  │  │PostgreSQL│  │   Redis    │  │  Grafana  │ │    │    │    │
│   │   │  │  │ :5432 SSL│  │   :6379    │  │   :3000   │ │    │    │    │
│   │   │  │  │ scram-256│  │  internal  │  │ basic auth│ │    │    │    │
│   │   │  │  └──────────┘  └────────────┘  └───────────┘ │    │    │    │
│   │   │  └────────────────────────────────────────────────┘    │    │    │
│   │   └────────────────────────────────────────────────────────┘    │    │
│   └─────────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
```

## 5 lớp bảo mật

| Lớp | Cơ chế | Trạng thái | Tương đương Enterprise |
|-----|--------|------------|------------------------|
| **1. Network** | Fail2ban + UFW firewall | ✅ Deployed | AWS WAF / Azure DDoS Protection |
| **2. VPN** | WireGuard (10.200.0.0/24) | ✅ Deployed | AWS Client VPN / Azure VPN Gateway |
| **3. Transport** | SSH tunnel (key-based) + PostgreSQL SSL | ✅ Deployed | Azure Bastion / AWS SSM |
| **4. Authentication** | SSH key + TOTP 2FA | ⚠️ Chưa activate | Azure MFA / AWS IAM |
| **5. Application** | mTLS client cert (EJBCA/SignServer) | ✅ Deployed | Mutual TLS / Managed Identity |

---

## Cách dùng: SSH Tunnel (Nhanh nhất)

### PowerShell Script (Windows)

```powershell
# Tất cả service admin
.\scripts\admin-tunnel.ps1

# Chỉ EJBCA
.\scripts\admin-tunnel.ps1 -Service ejbca

# Chỉ PostgreSQL
.\scripts\admin-tunnel.ps1 -Service db

# Chỉ MinIO
.\scripts\admin-tunnel.ps1 -Service minio
```

### SSH thủ công (macOS/Linux/Windows)

> **Quan trọng**: Dùng `127.0.0.1` (không phải `localhost`) — tránh resolve sang IPv6 `::1`.
> Local port dùng prefix `1xxxx` để tránh xung đột với Docker dev containers local.

```bash
# ─── EJBCA Admin ───
ssh -N -L 127.0.0.1:18443:127.0.0.1:8443 root@45.134.226.56
# → Mở https://127.0.0.1:18443/ejbca/adminweb/ (cần client cert)

# ─── SignServer Admin ───
ssh -N -L 127.0.0.1:19443:127.0.0.1:9443 root@45.134.226.56
# → Mở https://127.0.0.1:19443/signserver/adminweb/ (cần client cert)

# ─── MinIO Console ───
ssh -N -L 127.0.0.1:19001:127.0.0.1:9001 root@45.134.226.56
# → Mở http://127.0.0.1:19001

# ─── PostgreSQL (DBeaver / DataGrip) ───
ssh -N -L 127.0.0.1:15433:127.0.0.1:5433 root@45.134.226.56
# → Host: 127.0.0.1, Port: 15433, Database: ivf_db
# → User: postgres, Password: xem Docker secret ivf_db_password
# → SSL: disable (SSL off trong container)

# ─── Redis (RedisInsight) ───
ssh -N -L 127.0.0.1:26379:127.0.0.1:6379 root@45.134.226.56
# → Connect: 127.0.0.1:26379

# ─── Grafana (nếu không dùng Caddy proxy) ───
ssh -N -L 127.0.0.1:13000:127.0.0.1:3000 root@45.134.226.56
# → Mở http://127.0.0.1:13000

# ─── Tất cả cùng lúc ───
ssh -N \
  -L 127.0.0.1:18443:127.0.0.1:8443 \
  -L 127.0.0.1:19443:127.0.0.1:9443 \
  -L 127.0.0.1:19001:127.0.0.1:9001 \
  -L 127.0.0.1:15433:127.0.0.1:5433 \
  -L 127.0.0.1:26379:127.0.0.1:6379 \
  -L 127.0.0.1:13000:127.0.0.1:3000 \
  -o ServerAliveInterval=60 \
  -o ExitOnForwardFailure=yes \
  root@45.134.226.56
```

### Grafana / Prometheus (Đã có Caddy proxy)

Không cần SSH tunnel vì đã được Caddy proxy với basic auth:
- **Grafana**: https://natra.site/grafana/ (user: `monitor`)
- **Prometheus**: https://natra.site/prometheus/ (user: `monitor`)

---

## Cách dùng: WireGuard VPN (Khuyến nghị)

> Nhanh hơn SSH tunnel, auto-reconnect, truy cập tất cả services cùng lúc.

### Yêu cầu

1. Cài [WireGuard client](https://www.wireguard.com/install/)
2. Import config file `secrets/wg-admin1.conf` vào WireGuard app
3. Activate tunnel

### Truy cập services qua VPN

Sau khi connect WireGuard, truy cập trực tiếp (không cần tunnel):

| Service | URL / Host | Ghi chú |
|---------|-----------|---------|
| **EJBCA** | `https://10.200.0.1:8443/ejbca/adminweb/` | Cần client cert (`admin.p12`) |
| **SignServer** | `https://10.200.0.1:9443/signserver/adminweb/` | Cần client cert (`admin.p12`) |
| **MinIO Console** | `http://10.200.0.1:9001` | MinIO root credentials |
| **PostgreSQL** | `10.200.0.1:5433` | SSL require, scram-sha-256 |
| **Redis** | `10.200.0.1:6379` | No auth (internal only) |
| **Grafana** | `http://10.200.0.1:3000` | Hoặc qua Caddy: `https://natra.site/grafana/` |

### PostgreSQL qua WireGuard

```powershell
# PowerShell
$env:PGPASSWORD = "<password từ Docker secret>"
$env:PGSSLMODE = "require"
psql -h 10.200.0.1 -p 5433 -U postgres -d ivf_db

# Verify SSL
psql -h 10.200.0.1 -p 5433 -U postgres -d ivf_db -c "SELECT ssl, version FROM pg_stat_ssl WHERE pid = pg_backend_pid();"
# → ssl=t, version=TLSv1.3
```

### SignServer/EJBCA qua WireGuard (mTLS)

1. Import `certs/admin/admin.p12` vào browser (password trong `secrets/admin_cert_password.txt`)
2. Firefox: Settings → Privacy & Security → Certificates → View Certificates → Your Certificates → Import
3. Truy cập `https://10.200.0.1:9443/signserver/adminweb/`
4. Accept self-signed cert warning → chọn IVF Admin cert khi được hỏi
5. SignServer admin web sẽ hiển thị với user authorized

---

## Thiết lập ban đầu (Chỉ chạy 1 lần)

### 1. SSH Key-Only Authentication

```bash
# Trên máy local (nếu chưa có key)
ssh-keygen -t ed25519 -C "ivf-admin"

# Copy key lên VPS
ssh-copy-id -i ~/.ssh/id_ed25519.pub root@45.134.226.56

# Disable password login trên VPS
ssh root@45.134.226.56 "
  sed -i 's/^#*PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
  sed -i 's/^#*ChallengeResponseAuthentication.*/ChallengeResponseAuthentication no/' /etc/ssh/sshd_config
  systemctl restart sshd
"
```

### 2. Lock down admin ports (Firewall)

```bash
# Upload và chạy script trên VPS
ssh root@45.134.226.56 'bash -s' < scripts/secure-admin-ports.sh
```

### 3. Redeploy stack với port bindings đã fix

```bash
# Stack file đã được cập nhật để dùng host mode cho admin ports
docker stack deploy -c docker-compose.stack.yml ivf
```

---

## So sánh với Azure / AWS

| Tính năng | Azure | AWS | IVF System |
|-----------|-------|-----|------------|
| Bastion/Jump Host | Azure Bastion | AWS SSM Session Manager | SSH tunnel (key-based) |
| Network Isolation | NSG + Private Endpoint | Security Groups + VPC | UFW/iptables + Docker overlay |
| Identity | Entra ID + MFA | IAM + MFA | SSH key + mTLS client cert |
| Admin UI Access | Azure Portal (RBAC) | AWS Console (IAM) | SSH tunnel → localhost |
| DB Access | Private Link | RDS Private Subnet | No port exposure + SSH tunnel |
| Secret Management | Key Vault | Secrets Manager | Docker Secrets (/run/secrets/) |
| Audit | Activity Log | CloudTrail | Serilog + Loki + audit_logs |
| Encryption at Rest | AES-256 (auto) | KMS (auto) | PostgreSQL TDE + MinIO SSE |
| Encryption in Transit | TLS 1.2+ (auto) | TLS 1.2+ (auto) | Caddy auto-TLS + mTLS signing |

---

## Mô hình nâng cao (Optional)

### Option A: WireGuard VPN (Thay thế SSH tunnel)

Giống **Azure VPN Gateway** / **AWS Client VPN**. Chỉ cần khi:
- Có nhiều admin cùng truy cập
- Cần truy cập thường xuyên (WireGuard auto-reconnect)
- Muốn full network-level isolation

```bash
# Trên VPS
apt install wireguard
wg genkey | tee /etc/wireguard/server.key | wg pubkey > /etc/wireguard/server.pub

# /etc/wireguard/wg0.conf
[Interface]
PrivateKey = <server-private-key>
Address = 10.200.0.1/24
ListenPort = 51820

[Peer]
PublicKey = <client-public-key>
AllowedIPs = 10.200.0.2/32

# Admin ports chỉ cho phép từ WireGuard subnet
ufw allow in on wg0 to any port 8443,9443,9001,5432,6379
```

### Option B: Cloudflare Access (Zero Trust SaaS)

Giống **Azure Entra Application Proxy**. Thêm identity layer (email OTP, SSO):

```
# Cloudflare Access policy cho admin subdomain
# admin.natra.site → tunnel → localhost:8443/9443/9001
# Yêu cầu: email trong allow-list + OTP
```

### Option C: mTLS Client Certificate (Đã cấu hình cho EJBCA/SignServer)

EJBCA và SignServer production đã cấu hình mTLS client certificate authentication:

**Client Certificate**: `certs/admin/admin.p12`
- Subject: `CN=IVF Admin, OU=IT Department, O=IVF Clinic, S=Ho Chi Minh, C=VN`
- Issuer: `CN=IVF Internal Root CA`
- Password: xem `secrets/admin_cert_password.txt`
- Hết hạn: 02/2027

**Import vào trình duyệt:**
1. **Chrome/Edge**: Settings → Privacy and Security → Security → Manage certificates → Import → chọn `admin.p12` → nhập password
2. **Firefox**: Settings → Privacy & Security → Certificates → View Certificates → Your Certificates → Import

**EJBCA**: IVF Internal Root CA đã được import → IVF Admin là Super Administrator
**SignServer**: Truststore + Elytron mTLS đã cấu hình (2026-03-13)
- Truststore: `/opt/keyfactor/persistent/truststore.jks` (chứa IVF Internal Root CA)
- Elytron: `trustKS` (key-store) → `httpsTM` (trust-manager) → `httpsSSC` (ssl-context)
- `want-client-auth=true` → server yêu cầu client cert khi TLS handshake
- Admin authorized via `wsadmins`: serial `2eb6eb968de282d3d8e731f79081ca1405836e09`
- Truy cập: `https://10.200.0.1:9443/signserver/adminweb/` (qua WireGuard + client cert)

> ✅ **Elytron persistence đã fix**: WildFly standalone configuration được persist qua Docker volume (`signserver_wildfly_cfg` / `ejbca_wildfly_cfg`). Sau lần đầu chạy `init-mtls-production.sh`, Elytron config tồn tại qua container restart/redeploy.

> ✅ **EJBCA Public Access Role**: Đã có script `scripts/secure-ejbca-access.sh` để xóa role này. Chỉ client cert holders mới admin được.

---

## Hardening — Xóa EJBCA Public Access Role

Sau khi confirm client cert auth hoạt động trong browser:

```bash
# Xóa Public Access Group (chỉ client cert holders admin được)
ssh root@45.134.226.56 'bash -s' < scripts/secure-ejbca-access.sh

# Nếu bị lock out — restore lại
ssh root@45.134.226.56 'bash -s -- --restore' < scripts/secure-ejbca-access.sh
```

> ⚠️ **Trước khi chạy**: Import `admin.p12` vào browser và verify truy cập được EJBCA admin web.

---

## Hardening — Admin User (thay root)

Tạo user riêng `ivfadmin` với sudo + Docker access, sau đó disable root SSH:

```bash
# Bước 1: Tạo user (chạy khi đang SSH root)
ssh root@45.134.226.56 'bash -s' < scripts/setup-admin-user.sh

# Bước 2: TEST trong terminal MỚI (giữ session root mở!)
ssh ivfadmin@45.134.226.56 "whoami && sudo docker ps --format 'table {{.Names}}\t{{.Status}}' | head -5"

# Bước 3: Nếu OK → disable root SSH
ssh root@45.134.226.56 'bash -s -- --disable-root' < scripts/setup-admin-user.sh
```

Sau khi disable root, cập nhật `scripts/admin-tunnel.ps1`:
```powershell
# Đổi $SshUser = "root" → $SshUser = "ivfadmin" trong admin-tunnel.ps1
```

---

## Kết nối PostgreSQL

### Cách 1: WireGuard VPN (khuyến nghị)

Kết nối trực tiếp qua VPN — không cần SSH tunnel, nhanh hơn:

| Setting | Value |
|---------|-------|
| Host | `10.200.0.1` |
| Port | `5433` |
| Database | `ivf_db` (underscore, KHÔNG phải `ivf-db`) |
| Username | `postgres` |
| Password | Docker secret `ivf_db_password` (xem bên dưới) |
| SSL | **require** (verified TLSv1.3) |
| Auth | Database (scram-sha-256) |

```powershell
# PowerShell — kết nối qua WireGuard
$env:PGPASSWORD = "<password>"
$env:PGSSLMODE = "require"
psql -h 10.200.0.1 -p 5433 -U postgres -d ivf_db
```

### Cách 2: SSH Tunnel

| Setting | Value |
|---------|-------|
| Host | `127.0.0.1` (KHÔNG dùng `localhost`) |
| Port | `15433` |
| Database | `ivf_db` |
| Username | `postgres` |
| Password | Docker secret `ivf_db_password` (xem bên dưới) |
| SSL | **require** |
| Auth | Database (scram-sha-256) |

```bash
# Mở tunnel trước
ssh -N -L 127.0.0.1:15433:127.0.0.1:5433 root@45.134.226.56

# Rồi kết nối
PGSSLMODE=require psql -h 127.0.0.1 -p 15433 -U postgres -d ivf_db
```

### Lấy password

```bash
ssh root@45.134.226.56 'docker exec $(docker ps -q -f name=ivf_db) cat /run/secrets/ivf_db_password'
```

> **Lưu ý SSL**: PostgreSQL SSL đã được bật (2026-03-13). Client có thể dùng `sslmode=require` hoặc `sslmode=verify-ca` (cần CA cert).

---

## Checklist bảo mật

### Đã triển khai ✅

**Lớp 1 — Network & Firewall**
- [x] SSH key-only authentication (no password login)
- [x] Admin ports: firewall-blocked (UFW deny trên eth0)
- [x] SSH tunnel: `127.0.0.1` binding, `1xxxx` prefix ports
- [x] PostgreSQL: no WAN exposure, scram-sha-256 auth
- [x] Redis: no WAN exposure
- [x] MinIO Console: `127.0.0.1:9001` only
- [x] Fail2ban — SSH brute-force protection (triển khai 2026-03-13)
  - sshd jail: 5 retries / 1 giờ ban
  - recidive jail: 3 bans / 1 tuần ban
  - IP whitelist: `115.79.197.0/24` (admin IP range)
- [x] WireGuard VPN — multi-admin secure access (triển khai 2026-03-13)
  - Server: `10.200.0.1/24`, port `51820/udp`
  - Public key: `d9OmaqufAT2Tgo1DjV1LmtJqMVoVxUN+dXL5OvodPks=`
  - Split tunnel: chỉ route `10.200.0.0/24` qua VPN

**Lớp 2 — Encryption**
- [x] PostgreSQL SSL — TLS encryption cho DB connections (triển khai 2026-03-13)
  - CA: `IVF PostgreSQL CA` (10-year, RSA 4096)
  - SANs: `db`, `localhost`, `ivf_db`, `127.0.0.1`
  - CA cert: `/root/ivf/certs/postgres/pg-ca.crt` trên VPS
- [x] Caddy: auto-TLS (Let's Encrypt) cho public endpoints
- [x] Docker Secrets cho DB password (`ivf_db_password`)
- [x] JWT key shared via Docker Secret (`jwt_private_key`)

**Lớp 3 — Authentication & mTLS**
- [x] EJBCA: mTLS client cert (IVF Admin → Super Administrator)
- [x] SignServer: mTLS client cert (Elytron truststore + wsadmins), verified via WireGuard (2026-03-13)
- [x] API: Triple auth pipeline (VaultToken → ApiKey → JWT)
- [x] SSH 2FA — Google Authenticator TOTP (cấu hình 2026-03-13, chưa activate)
  - TOTP secret: `5DMVMQFID7RY7ZRR2BI3NCW32Q`
  - 5 emergency scratch codes đã lưu
  - AuthenticationMethods: `publickey,keyboard-interactive`
  - **Chưa active** — cần `systemctl reload ssh` sau khi add TOTP vào app

**Lớp 4 — Monitoring & Logging**
- [x] Grafana/Prometheus: Caddy reverse proxy với basic auth
- [x] Serilog structured logging + Loki aggregation
- [x] Audit logging (partitioned PostgreSQL table)

**Lớp 5 — PKI & Certificate**
- [x] WildFly Elytron config persistent (Docker volume `signserver_wildfly_cfg` + `ejbca_wildfly_cfg`)
- [x] EJBCA Public Access Role removal script (`scripts/secure-ejbca-access.sh`)
- [x] Dedicated admin user script (`scripts/setup-admin-user.sh` — thay root SSH)

### Cần thực hiện ⚡
- [ ] **Activate SSH 2FA**: Add TOTP secret vào authenticator app → `systemctl reload ssh`
- [x] **WireGuard client `admin1`**: Connected, `10.200.0.2/32`, config: `secrets/wg-admin1.conf`
- [x] **SignServer mTLS via WireGuard**: Elytron truststore + trust-manager + want-client-auth configured, admin cert authorized
- [x] **PostgreSQL via WireGuard**: Kết nối SSL TLSv1.3 trực tiếp qua `10.200.0.1:5433`
- [ ] `bash scripts/secure-ejbca-access.sh` → xóa Public Access Role
- [ ] `bash scripts/setup-admin-user.sh` → tạo `ivfadmin` user → disable root SSH
- [ ] `docker stack deploy -c docker-compose.stack.yml ivf` → apply WildFly volume mounts
- [ ] `bash scripts/init-mtls-production.sh` → re-init mTLS (sau khi redeploy với volume mới)

### Optional — Scripts sẵn sàng
- [ ] Cloudflare Access → `scripts/setup-cloudflare-access.sh`

---

## Hardening — Fail2ban (SSH brute-force protection)

> **Trạng thái: ĐÃ TRIỂN KHAI** (2026-03-13) — 2 jails active, IP whitelist configured.

Tự động ban IP sau 5 lần đăng nhập sai. Recidive jail: ban 1 tuần nếu tái phạm.

### Kiến trúc & Flow

```
┌────────────────┐      SSH (port 22)      ┌─────────────────────────┐
│  Attacker IP   │ ───────────────────────► │       sshd              │
│  (Internet)    │  5 failures / 10 min     │  ┌─────────────────┐   │
└────────────────┘                          │  │ systemd journal  │   │
                                            │  └────────┬────────┘   │
                                            └───────────┼────────────┘
                                                        │ monitor
                                            ┌───────────▼────────────┐
                                            │      fail2ban          │
                                            │  ┌─────────────────┐   │
                                            │  │   sshd jail      │   │
                                            │  │  5 retries → ban │──┤ ban 1 giờ
                                            │  │  (findtime 10m)  │  │
                                            │  └─────────────────┘   │
                                            │  ┌─────────────────┐   │
                                            │  │  recidive jail   │   │
                                            │  │  3 bans → ban    │──┤ ban 1 tuần
                                            │  │  (findtime 12h)  │  │
                                            │  └─────────────────┘   │
                                            │                        │
                                            │  ignoreip:             │
                                            │    127.0.0.1/8         │
                                            │    10.0.0.0/8          │
                                            │    172.16.0.0/12       │
                                            │    192.168.0.0/16      │
                                            │    115.79.197.0/24 ◄── │ admin IP range
                                            └────────────────────────┘
```

### Cấu hình hiện tại trên VPS

| Setting | sshd jail | recidive jail |
|---------|-----------|---------------|
| **enabled** | `true` | `true` |
| **maxretry** | 5 | 3 (bans) |
| **findtime** | 600s (10 phút) | 43200s (12 giờ) |
| **bantime** | 3600s (1 giờ) | 604800s (1 tuần) |
| **backend** | systemd | auto |
| **banaction** | iptables-multiport | iptables-allports |
| **ignoreip** | `127.0.0.1/8 ::1 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16 115.79.197.0/24` | (inherits DEFAULT) |

### Vận hành

```bash
# Triển khai / cập nhật
Get-Content scripts/setup-fail2ban.sh -Raw | ssh root@45.134.226.56 "bash -s"

# Kiểm tra status
ssh root@45.134.226.56 "fail2ban-client status sshd"
ssh root@45.134.226.56 "fail2ban-client status recidive"

# Xem IP bị ban
ssh root@45.134.226.56 "fail2ban-client status sshd | grep 'Banned IP'"

# Unban IP cụ thể
ssh root@45.134.226.56 "fail2ban-client set sshd unbanip <IP>"

# Unban tất cả
ssh root@45.134.226.56 "fail2ban-client unban --all"

# Thêm IP vào whitelist
ssh root@45.134.226.56 "fail2ban-client set sshd addignoreip <IP>"
```

### Gotchas & Lessons Learned

1. **Rapid SSH connections trigger ban**: Mỗi script deploy = nhiều SSH connections. Nếu bị timeout/drop → tích lũy failure count → bị ban. **Fix**: Whitelist admin IP trong `ignoreip`.
2. **Custom filter recursion bug**: Nếu tồn tại `/etc/fail2ban/filter.d/sshd.local` với `%(known/sshd)s`, fail2ban crash do circular inclusion. Script tự xóa file này.
3. **Ubuntu dùng `ssh` service**: Không phải `sshd`. `systemctl reload ssh` (không phải `systemctl reload sshd`).
4. **Windows line endings**: Khi pipe script qua SSH (`Get-Content -Raw | ssh "bash -s"`), `\r` ở cuối gây `$'\r': command not found`. Harmless — chỉ xảy ra ở dòng cuối cùng.

---

## Hardening — SSH 2FA (Google Authenticator)

> **Trạng thái: ĐÃ CÀI ĐẶT, CHƯA ACTIVATE** (2026-03-13) — PAM configured, sshd NOT reloaded.

TOTP two-factor: SSH key + mã 6 số từ app (Google Authenticator / Authy).

### Flow xác thực (sau khi activate)

```
┌────────────┐     SSH connect     ┌───────────────────┐
│  Developer  │ ──────────────────► │       sshd        │
│ Workstation │                     │                    │
└────────────┘                     │  Step 1: SSH Key   │
       │                            │  ✓ publickey       │
       │                            │                    │
       │   ◄─── "Verification      │  Step 2: TOTP 2FA  │
       │         code: "            │  PAM google-auth   │
       │                            │                    │
       │   ───► 6-digit code ──────►│  ✓ TOTP matches    │
       │                            │                    │
       │   ◄─── Shell access ──────│  ✓ Authenticated   │
       │                            └───────────────────┘
       │
       │   Nếu mất điện thoại:
       │   ───► emergency scratch code (8 digits, one-time use)
```

### Trạng thái hiện tại

- **PAM module**: `libpam-google-authenticator` installed
- **PAM config** (`/etc/pam.d/sshd`): `auth required pam_google_authenticator.so nullok`
- **sshd config**: `AuthenticationMethods publickey,keyboard-interactive`
- **TOTP secret**: Đã generate (file `/root/.google_authenticator` tồn tại)
- **sshd CHƯA reload**: 2FA chưa có hiệu lực — vẫn chỉ cần SSH key

### Cách activate 2FA

```bash
# Step 1: Thêm TOTP secret vào authenticator app
# Secret hiện tại đã lưu trên VPS tại /root/.google_authenticator
# Scan QR hoặc nhập manual secret vào Google Authenticator / Authy

# Step 2: Test xem app generate đúng code không
ssh root@45.134.226.56 "head -1 /root/.google_authenticator"
# → So sánh TOTP code từ app vs VPS

# Step 3: Reload sshd để activate (⚠️ GIỮ SESSION CŨ MỞ!)
ssh root@45.134.226.56 "systemctl reload ssh"

# Step 4: Test trong terminal MỚI (KHÔNG đóng session cũ!)
ssh root@45.134.226.56    # sẽ hỏi "Verification code:"

# Nếu OK → đóng session cũ
# Nếu FAIL → trong session cũ, disable 2FA:
ssh root@45.134.226.56 "sed -i '/pam_google_authenticator/d' /etc/pam.d/sshd && systemctl reload ssh"
```

### Emergency scratch codes

Lưu ở nơi an toàn. Mỗi code chỉ dùng được **1 lần**:

```
36072330
43070711
15299740
91819660
44054059
```

### Vận hành

```bash
# Disable 2FA (nếu bị lock out)
Get-Content scripts/setup-ssh-2fa.sh -Raw | ssh root@45.134.226.56 "bash -s -- --disable"

# Re-enable / reset 2FA (generate secret mới)
Get-Content scripts/setup-ssh-2fa.sh -Raw | ssh root@45.134.226.56 "bash -s"

# Xem secret hiện tại
ssh root@45.134.226.56 "head -1 /root/.google_authenticator"
```

### Gotchas

1. **Ubuntu dùng service `ssh` không phải `sshd`**: `systemctl reload ssh` (không phải `sshd`)
2. **nullok flag**: Cho phép login không cần 2FA nếu user chưa setup TOTP. An toàn hơn khi deploy từ từ.
3. **Emergency recovery**: Nếu mất codes + mất phone → chỉ có thể recovery qua VPS provider console (VNC).
4. **GIỮ session cũ mở khi test**: Nếu reload sshd và 2FA fail, cần session cũ để rollback.

---

## Hardening — PostgreSQL SSL

> **Trạng thái: ĐÃ TRIỂN KHAI** (2026-03-12) — SSL = `on`, scram-sha-256.

Enable TLS encryption cho kết nối PostgreSQL. Tự tạo CA + server cert.

### Flow kết nối

```
┌────────────┐                    ┌────────────────────┐
│  IVF API   │  SSL/TLS (require) │   PostgreSQL       │
│  Container │ ──────────────────► │   Container        │
│            │  scram-sha-256      │   (ivf_db)         │
│            │                     │                     │
│            │  ◄── Server cert ── │   server.crt/key   │
│            │  verify: optional   │   signed by pg-ca  │
└────────────┘                    └────────────────────┘

Clients bên ngoài (admin) — 2 cách kết nối:
┌────────────┐  SSH Tunnel  ┌─────────┐  internal  ┌──────────┐
│  DBeaver   │ ──────────── │   VPS   │ ─────────► │ PostgreSQL│
│  pgAdmin   │  :15433      │  :5433  │  SSL/TLS   │  :5432   │
└────────────┘              └─────────┘            └──────────┘

┌────────────┐  WireGuard   ┌─────────┐  direct    ┌──────────┐
│  psql      │ ════════════ │   VPS   │ ─────────► │ PostgreSQL│
│  DBeaver   │  10.200.0.1  │  :5433  │  SSL/TLS   │  :5432   │
└────────────┘              └─────────┘  TLSv1.3   └──────────┘
```

### Cấu hình hiện tại

| Setting | Value |
|---------|-------|
| **ssl** | `on` |
| **ssl_cert_file** | `/var/lib/postgresql/certs/server.crt` |
| **ssl_key_file** | `/var/lib/postgresql/certs/server.key` |
| **ssl_ca_file** | `/var/lib/postgresql/certs/pg-ca.crt` |
| **Auth method** | `scram-sha-256` |
| **pg_hba.conf** | `hostssl all all 0.0.0.0/0 scram-sha-256` |

### Vận hành

```bash
# Triển khai / cập nhật certs
Get-Content scripts/setup-postgres-ssl.sh -Raw | ssh root@45.134.226.56 "bash -s"

# Verify SSL đang bật
ssh root@45.134.226.56 "docker exec \$(docker ps -q -f name=ivf_db) psql -U postgres -t -c 'SHOW ssl;'"

# Copy CA cert về local (cho verify-ca mode)
scp root@45.134.226.56:/root/ivf/certs/postgres/pg-ca.crt certs/postgres/

# Test kết nối SSL qua SSH tunnel
# (cần mở tunnel trước: ssh -L 15433:localhost:5433 root@45.134.226.56)
PGSSLMODE=require psql -h 127.0.0.1 -p 15433 -U postgres -d ivf_db -c "SELECT ssl, version FROM pg_stat_ssl WHERE pid = pg_backend_pid();"

# Test kết nối SSL qua WireGuard VPN (nhanh hơn, không cần tunnel)
$env:PGPASSWORD="<password từ Docker secret>"
$env:PGSSLMODE="require"
psql -h 10.200.0.1 -p 5433 -U postgres -d ivf_db -c "SELECT ssl, version FROM pg_stat_ssl WHERE pid = pg_backend_pid();"
# Kết quả: ssl=t, version=TLSv1.3
```

### Kết nối từ DBeaver/pgAdmin

**Cách 1: Qua WireGuard VPN (khuyến nghị)**

| Setting | Value |
|---------|-------|
| Host | `10.200.0.1` (qua WireGuard VPN) |
| Port | `5433` |
| Database | `ivf_db` |
| User | `postgres` |
| Password | Docker secret `ivf_db_password` |
| SSL | **require** |
| CA cert | `certs/postgres/pg-ca.crt` (optional, cho verify-ca) |

**Cách 2: Qua SSH Tunnel**

| Setting | Value |
|---------|-------|
| Host | `127.0.0.1` (qua SSH tunnel) |
| Port | `15433` (local) → `5433` (VPS) → `5432` (container) |
| Database | `ivf_db` |
| User | `postgres` |
| Password | Docker secret `ivf_db_password` |
| SSL | **require** |
| CA cert | `certs/postgres/pg-ca.crt` (optional, cho verify-ca) |

Lấy password:
```bash
ssh root@45.134.226.56 'docker exec $(docker ps -q -f name=ivf_db) cat /run/secrets/ivf_db_password'
```

> 📝 SSL đã bật từ 2026-03-12, verified TLSv1.3 qua WireGuard (2026-03-13). Kết nối mới nên dùng `sslmode=require` trở lên.

---

## Hardening — WireGuard VPN (multi-admin)

> **Trạng thái: ĐÃ TRIỂN KHAI** (2026-03-13) — Server running, wg0 interface up, 1 client (`admin1`) connected.

VPN cho phép nhiều admin truy cập admin ports mà không cần SSH tunnel.

### Flow kết nối

```
┌──────────────────┐                             ┌──────────────────┐
│  Admin Machine    │     WireGuard Tunnel        │   VPS Server     │
│                   │     (51820/udp)              │                  │
│  WireGuard Client │ ◄══════════════════════════► │  WireGuard Server│
│  10.200.0.2/32    │   Encrypted (ChaCha20)      │  10.200.0.1/24   │
│                   │                              │                  │
│  Routes:          │                              │  PostUp rules:   │
│  10.200.0.0/24    │                              │  FORWARD accept  │
│  → via wg0        │                              │  NAT masquerade  │
└──────────────────┘                             └──────────────────┘
         │                                                  │
         │  Qua VPN, truy cập trực tiếp:                    │
         │  10.200.0.1:8443 → EJBCA                         │
         │  10.200.0.1:9443 → SignServer                    │
         │  10.200.0.1:9001 → MinIO Console                 │
         │  10.200.0.1:5433 → PostgreSQL                    │
         │  10.200.0.1:6379 → Redis                         │
         │  10.200.0.1:3000 → Grafana                       │
         └──────────────────────────────────────────────────┘
```

### Cấu hình server hiện tại

| Setting | Value |
|---------|-------|
| **Interface** | `wg0` |
| **Address** | `10.200.0.1/24` |
| **ListenPort** | `51820` |
| **Server PublicKey** | `d9OmaqufAT2Tgo1DjV1LmtJqMVoVxUN+dXL5OvodPks=` |
| **Config file** | `/etc/wireguard/wg0.conf` |
| **Systemd** | `wg-quick@wg0.service` (enabled, active) |

### UFW rules cho WireGuard

```
51820/udp              ALLOW    Anywhere          # WireGuard port
8443/tcp on wg0        ALLOW    Anywhere          # EJBCA via VPN
9443/tcp on wg0        ALLOW    Anywhere          # SignServer via VPN
9001/tcp on wg0        ALLOW    Anywhere          # MinIO via VPN
5433/tcp on wg0        ALLOW    Anywhere          # PostgreSQL via VPN
6379/tcp on wg0        ALLOW    Anywhere          # Redis via VPN
3000/tcp on wg0        ALLOW    Anywhere          # Grafana via VPN
```

### Clients hiện tại

| Client | IP | Trạng thái | Config file |
|--------|-----|-----------|-------------|
| `admin1` | `10.200.0.2/32` | ✅ Connected | `secrets/wg-admin1.conf` |

### Thêm client mới

```powershell
# Từ PowerShell (Windows)
Get-Content scripts/setup-wireguard.sh -Raw | ssh root@45.134.226.56 "bash -s -- --add-client admin2"

# Output sẽ hiển thị:
# 1. Client config file (copy vào WireGuard app)
# 2. QR code (scan trên mobile)
# Client IP: auto-increment (admin2 = 10.200.0.3, admin3 = 10.200.0.4, ...)
```

### Import config vào WireGuard client

- **Windows**: WireGuard → Import tunnel(s) from file → paste config
- **macOS**: WireGuard → Import tunnel(s) from file
- **Mobile**: WireGuard app → scan QR code từ terminal output
- **Linux**: `sudo cp client.conf /etc/wireguard/ivf.conf && sudo wg-quick up ivf`

### Vận hành

```bash
# Xem trạng thái
ssh root@45.134.226.56 "wg show"

# Xem danh sách clients
Get-Content scripts/setup-wireguard.sh -Raw | ssh root@45.134.226.56 "bash -s -- --status"

# Xóa client
Get-Content scripts/setup-wireguard.sh -Raw | ssh root@45.134.226.56 "bash -s -- --remove-client admin1"

# Restart WireGuard
ssh root@45.134.226.56 "systemctl restart wg-quick@wg0"
```

### So sánh: SSH Tunnel vs WireGuard

| | SSH Tunnel | WireGuard VPN |
|---|-----------|---------------|
| **Cách dùng** | Mỗi port cần 1 `-L` flag | Connect VPN → truy cập tất cả |
| **Multi-admin** | Chỉ ai có SSH key | Mỗi admin có WG config riêng |
| **Performance** | TCP-over-TCP (chậm) | UDP-based (nhanh) |
| **Persistent** | Phải mở terminal SSH | Chạy nền, auto-reconnect |
| **Setup** | Đơn giản (SSH sẵn có) | Cần cài WireGuard client |

---

## Hardening — Cloudflare Access (Zero Trust)

Identity-based access qua Cloudflare tunnel. Không cần VPN hay SSH tunnel.

```bash
# Xem hướng dẫn chi tiết
bash scripts/setup-cloudflare-access.sh

# Cài đặt (cần tunnel token từ Cloudflare dashboard)
ssh root@45.134.226.56 'bash -s -- <TUNNEL_TOKEN>' < scripts/setup-cloudflare-access.sh

# Gỡ cài đặt
ssh root@45.134.226.56 'bash -s -- --uninstall' < scripts/setup-cloudflare-access.sh
```

**Setup trong Cloudflare Dashboard:**
1. Zero Trust → Networks → Tunnels → Create → `ivf-admin`
2. Copy tunnel token → chạy script trên VPS
3. Zero Trust → Access → Applications → Add:
   - Domain: `ejbca.natra.site` → Service: `https://localhost:8443`
   - Policy: Allow emails in allow-list + OTP
   - Session: 24 hours
