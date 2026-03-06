# IVF Platform — Triển khai 2 VPS Contabo + Docker Swarm + AWS S3

> **Tài liệu hướng dẫn triển khai chi tiết — từ zero đến production**
>
> Phiên bản: 4.0 | Cập nhật: 2026-03-07
>
> Áp dụng: IVF Platform v5.0+ | .NET 10 | Angular 21 | PostgreSQL 16
>
> Thay đổi v4.0: VPS 2 multi-service deployment (API replica 2, PostgreSQL standby streaming replication, Redis replica, Caddy global mode), CI/CD deploy both nodes, Ansible replication setup, DigitalSigning enabled

---

## Mục lục

1. [Tổng quan Kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Yêu cầu VPS Contabo](#2-yêu-cầu-vps-contabo)
3. [Chuẩn bị VPS (Cả 2 VPS)](#3-chuẩn-bị-vps-cả-2-vps)
4. [Thiết lập Docker Swarm Cluster](#4-thiết-lập-docker-swarm-cluster)
5. [Chuẩn bị Source Code & Secrets](#5-chuẩn-bị-source-code--secrets)
6. [CI/CD & Container Registry (GHCR)](#6-cicd--container-registry-ghcr)
7. [Tạo Swarm Stack File](#7-tạo-swarm-stack-file)
8. [Deploy Stack lên Swarm](#8-deploy-stack-lên-swarm)
9. [Ansible Automation (Tự động hóa)](#9-ansible-automation-tự-động-hóa)
10. [Cấu hình PostgreSQL Replication](#10-cấu-hình-postgresql-replication)
11. [Cấu hình PKI — EJBCA & SignServer](#11-cấu-hình-pki--ejbca--signserver)
12. [Cấu hình DNS & Caddy SSL](#12-cấu-hình-dns--caddy-ssl)
13. [Thiết lập AWS S3 Backup](#13-thiết-lập-aws-s3-backup)
14. [Scripts Backup tự động](#14-scripts-backup-tự-động)
15. [Restore từ S3](#15-restore-từ-s3)
16. [Vận hành hàng ngày (Operations)](#16-vận-hành-hàng-ngày-operations)
17. [Monitoring & Alerting](#17-monitoring--alerting)
18. [Xử lý Sự cố (Troubleshooting)](#18-xử-lý-sự-cố-troubleshooting)
19. [Checklist Triển khai](#19-checklist-triển-khai)

---

## 1. Tổng quan Kiến trúc

```
             Cloudflare (CDN + DDoS + DNS Failover)
                    │                    │
        ┌───────────▼──────┐  ┌──────────▼────────┐
        │ VPS 1 (Manager)  │  │  VPS 2 (Worker)    │       ┌─────────────────┐
        │ $15/tháng        │  │  $4.95/tháng       │       │  AWS S3          │
        │ 8vCPU / 24GB     │  │  4vCPU / 8GB       │       │  ap-southeast-1  │
        │                  │  │                     │       │  (Singapore)     │
        │ ┌──────────────┐ │  │                     │       │                  │
        │ │Caddy (:80)   │ │  │                     │       │ ivf-backups/     │
        │ │ reverse proxy│ │  │                     │       │ ├─ daily/        │
        │ └──┬────────┬──┘ │  │                     │       │ ├─ wal/          │
        │    │        │    │  │                     │       │ ├─ minio/        │
        │ ┌──▼──┐ ┌───▼──┐ │  │                     │       │ └─ config/       │
        │ │Front│ │ API  │ │  │                     │       │                  │
        │ │end  │ │:8080 │ │  │                     │  3AM  │ Lifecycle:       │
        │ │:80  │ │.NET  │ │  │                     │ ────► │ 0-30d: Standard  │
        │ └─────┘ └──┬───┘ │  │                     │       │ 30-90d: IA       │
        │            │     │  │                     │       │ 90d+: Glacier    │
        │ ┌──────────▼───┐ │  │                     │       │                  │
        │ │ PostgreSQL   │ │  │                     │       │ ~$5/tháng        │
        │ │ port 5432    │ │  │                     │       │ 99.999999999%    │
        │ └──────────────┘ │  │                     │       └─────────────────┘
        │                  │  │                     │
        │ ┌──────────────┐ │  │                     │
        │ │ Redis        │ │  │                     │
        │ └──────────────┘ │  │                     │
        │                  │  │                     │
        │ ┌──────────────┐ │  │                     │
        │ │ MinIO        │ │  │                     │
        │ │ 3 buckets    │ │  │                     │
        │ └──────────────┘ │  │                     │
        │                  │  │                     │
        │ ┌──────────────┐ │  │                     │
        │ │ EJBCA + DB   │ │  │  (PKI chỉ VPS 1)   │
        │ │ SignSvr + DB │ │  │                     │
        │ └──────────────┘ │  │                     │
        │                  │  │                     │
        │ ══overlay net══  │  │  ══overlay net══   │
        │ ivf-public       │  │  ivf-public        │
        │ ivf-signing(int) │  │                     │
        │ ivf-data(int)    │  │  ivf-data(int)     │
        └──────────────────┘  └─────────────────────┘

        ┌─────── CI/CD (GitHub Actions) ────────┐
        │ Push main → Build → Test → GHCR Push  │
        │ ghcr.io/hung6066/ivf:latest (API)     │
        │ ghcr.io/hung6066/ivf-client:latest    │
        │ → Auto deploy to Swarm via SSH        │
        └───────────────────────────────────────┘

Chi phí tổng: ~$25-28/tháng
├─ VPS 1: $15 (Cloud VPS 30 — 24 GB)
├─ VPS 2: $4.95-7.95 (Cloud VPS 10/20 — 8-12 GB)
├─ AWS S3: ~$5
├─ Cloudflare: $0 (Free plan)
└─ Domain: ~$5
```

**Lý do chọn Docker Swarm:**

| Tính năng         | Docker Compose          | Docker Swarm                 |
| ----------------- | ----------------------- | ---------------------------- |
| Deploy            | SSH mỗi VPS riêng       | `docker stack deploy` 1 lệnh |
| Rolling update    | Tắt → bật (downtime)    | Zero-downtime native         |
| Auto-healing      | Chỉ restart local       | Reschedule sang VPS khác     |
| Cross-VPS network | Cần VPN/SSH tunnel      | Encrypted overlay tự động    |
| Load balancing    | Caddy upstream thủ công | Routing mesh tự phân bổ      |
| Secrets           | File mount              | Encrypted in Raft store      |
| Scale             | Sửa file + restart      | `docker service scale api=3` |
| Overhead thêm     | —                       | Chỉ +70 MB RAM               |

---

## 2. Yêu cầu VPS Contabo

### 2.1 VPS 1 — Manager + Primary Services

| Spec          | Yêu cầu                     |
| ------------- | --------------------------- |
| **Plan**      | Contabo Cloud VPS 30        |
| **CPU**       | 8 vCPU (AMD EPYC)           |
| **RAM**       | 24 GB                       |
| **Disk**      | 200 GB NVMe SSD             |
| **OS**        | Ubuntu 24.04 LTS            |
| **Bandwidth** | Unlimited (400 Mbit/s)      |
| **Giá**       | ~$15/tháng                  |
| **Location**  | EU (Germany) hoặc Singapore |

**RAM usage ước tính VPS 1:**

```
Docker Engine (Swarm mode)    ~150 MB
Caddy (reverse proxy)         ~30 MB
Frontend (Angular/Caddy)      ~50 MB
API (.NET 10, replica 1)      ~500 MB
PostgreSQL Primary            ~1,000 MB (+ shared_buffers 4-6GB)
Redis                         ~256 MB
MinIO                         ~500 MB
EJBCA                         ~1,200 MB
EJBCA-DB                      ~300 MB
SignServer                    ~1,200 MB
SignServer-DB                 ~300 MB
──────────────────────────────────────
Tổng:                         ~5,486 MB (~5.4 GB)
Còn lại cho OS + buffer:      ~18.6 GB
```

### 2.2 VPS 2 — Worker + Standby Services

| Spec     | Yêu cầu              |
| -------- | -------------------- |
| **Plan** | Contabo Cloud VPS 10 |
| **CPU**  | 4 vCPU               |
| **RAM**  | 8 GB                 |
| **Disk** | 75 GB NVMe SSD       |
| **OS**   | Ubuntu 24.04 LTS     |
| **Giá**  | ~$4.95/tháng         |

**RAM usage ước tính VPS 2:**

```
Docker Engine (Swarm worker)  ~120 MB
Caddy (global mode)           ~30 MB
API (.NET 10, replica 2)      ~500 MB
PostgreSQL Standby            ~1,000 MB
Redis Replica                 ~256 MB
──────────────────────────────────────
Tổng:                         ~1,906 MB (~1.9 GB)
Còn lại cho OS + buffer:      ~6.1 GB
```

### 2.3 Đặt mua VPS

1. Truy cập [contabo.com](https://contabo.com/en/vps/)
2. Chọn **Cloud VPS 30** (VPS 1 — 24 GB, $15/tháng) và **Cloud VPS 10** (VPS 2 — 8 GB, $4.95/tháng)
3. Chọn **Location**: cùng datacenter (EU-DE hoặc Singapore) để giảm latency
4. **OS**: Ubuntu 24.04 LTS (64-bit)
5. **Storage**: Chọn NVMe SSD
6. **Networking**: Mỗi VPS có 1 public IPv4
7. Ghi lại IP: `VPS1_IP` và `VPS2_IP`

> **Nâng cấp nếu cần:** Nếu muốn thoải mái hơn cho VPS 2, chọn **Cloud VPS 20** (12 GB, $7.95/tháng)

> **QUAN TRỌNG:** Chọn cùng datacenter cho cả 2 VPS → latency ~0.5ms → Swarm Raft consensus ổn định

---

## 3. Chuẩn bị VPS (Cả 2 VPS)

> Thực hiện các bước sau trên **CẢ 2 VPS**. SSH vào từng VPS:
>
> ```bash
> ssh root@<VPS_IP>
> ```

### 3.1 Cập nhật hệ thống

```bash
apt update && apt upgrade -y
apt install -y \
  curl wget git unzip htop iotop \
  ufw fail2ban \
  ca-certificates gnupg lsb-release \
  jq tree

# Timezone
timedatectl set-timezone Asia/Ho_Chi_Minh
```

### 3.2 Tạo user deploy (không dùng root)

```bash
# Tạo user
adduser deploy
usermod -aG sudo deploy

# SSH key authentication
mkdir -p /home/deploy/.ssh
# Copy public key của bạn vào:
nano /home/deploy/.ssh/authorized_keys
chmod 700 /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh

# Disable root login + password auth
sed -i 's/^PermitRootLogin yes/PermitRootLogin no/' /etc/ssh/sshd_config
sed -i 's/^#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
systemctl restart sshd
```

### 3.3 Cấu hình Firewall (UFW)

```bash
# Default policies
ufw default deny incoming
ufw default allow outgoing

# SSH
ufw allow 22/tcp

# HTTP/HTTPS (Caddy)
ufw allow 80/tcp
ufw allow 443/tcp

# Docker Swarm (chỉ cho IP VPS kia)
# VPS 1 → cho phép VPS 2 kết nối, và ngược lại
ufw allow from <OTHER_VPS_IP> to any port 2377 proto tcp   # Swarm management
ufw allow from <OTHER_VPS_IP> to any port 7946 proto tcp   # Node communication
ufw allow from <OTHER_VPS_IP> to any port 7946 proto udp   # Node communication
ufw allow from <OTHER_VPS_IP> to any port 4789 proto udp   # Overlay network (VXLAN)

# PostgreSQL replication (chỉ VPS kia)
ufw allow from <OTHER_VPS_IP> to any port 5432 proto tcp

# Enable
ufw enable
ufw status verbose
```

> **Lưu ý:** Thay `<OTHER_VPS_IP>` bằng IP thực của VPS còn lại. VPS 1 cho phép VPS 2 và ngược lại.

### 3.4 Cấu hình Fail2ban

```bash
cat > /etc/fail2ban/jail.local << 'EOF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true
port = 22
filter = sshd
logpath = /var/log/auth.log
maxretry = 3
bantime = 86400
EOF

systemctl enable fail2ban
systemctl start fail2ban
```

### 3.5 Cài Docker Engine

```bash
# Xóa Docker cũ (nếu có)
for pkg in docker.io docker-doc docker-compose podman-docker containerd runc; do
  apt-get remove -y $pkg 2>/dev/null
done

# Cài Docker CE từ official repo
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "${VERSION_CODENAME}") stable" | \
  tee /etc/apt/sources.list.d/docker.list > /dev/null

apt update
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Cho user deploy chạy docker
usermod -aG docker deploy

# Verify
docker --version
docker compose version
```

### 3.6 Cấu hình Docker daemon

```bash
mkdir -p /etc/docker
cat > /etc/docker/daemon.json << 'EOF'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "50m",
    "max-file": "5"
  },
  "storage-driver": "overlay2",
  "default-address-pools": [
    {"base": "172.20.0.0/14", "size": 24}
  ],
  "metrics-addr": "127.0.0.1:9323",
  "experimental": true
}
EOF

systemctl restart docker
systemctl enable docker
```

> **Lưu ý:** KHÔNG dùng `"live-restore": true` — không tương thích với Docker Swarm mode.

### 3.7 Cài AWS CLI (cho S3 backup)

```bash
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install
rm -rf awscliv2.zip aws/

aws --version
```

### 3.8 Tạo thư mục dự án

```bash
# Trên cả 2 VPS
sudo mkdir -p /opt/ivf
sudo chown deploy:deploy /opt/ivf
```

---

## 4. Thiết lập Docker Swarm Cluster

### 4.1 Khởi tạo Swarm trên VPS 1 (Manager)

```bash
# SSH vào VPS 1 với user deploy
ssh deploy@<VPS1_IP>

# Khởi tạo Swarm — advertise trên public IP
docker swarm init --advertise-addr <VPS1_IP>

# Output sẽ hiện token, ví dụ:
# docker swarm join --token SWMTKN-1-xxxxx <VPS1_IP>:2377
# ↓ LƯU LẠI TOKEN NÀY
```

### 4.2 Join Worker trên VPS 2

```bash
# SSH vào VPS 2
ssh deploy@<VPS2_IP>

# Dán lệnh join từ output ở VPS 1
docker swarm join --token SWMTKN-1-xxxxx <VPS1_IP>:2377

# Output: "This node joined a swarm as a worker."
```

### 4.3 Label các nodes (trên VPS 1)

```bash
# Quay lại VPS 1
ssh deploy@<VPS1_IP>

# Xem danh sách nodes
docker node ls
# ID                   HOSTNAME   STATUS   AVAILABILITY   MANAGER STATUS   ENGINE VERSION
# abc123 *             vps1       Ready    Active         Leader           27.x
# def456               vps2       Ready    Active                          27.x

# Gán label cho VPS 1 (primary)
docker node update --label-add role=primary $(hostname)

# Gán label cho VPS 2 (standby)
# Lấy node ID của VPS 2 từ docker node ls
docker node update --label-add role=standby <VPS2_NODE_ID>

# Verify labels
docker node inspect --format '{{.Spec.Labels}}' $(hostname)
# map[role:primary]
docker node inspect --format '{{.Spec.Labels}}' <VPS2_NODE_ID>
# map[role:standby]
```

### 4.4 Verify Swarm cluster

```bash
docker node ls
docker info | grep -A5 Swarm

# Swarm: active
#  NodeID: abc123
#  Is Manager: true
#  ClusterID: xyz789
#  Managers: 1
#  Nodes: 2
```

---

## 5. Chuẩn bị Source Code & Secrets

### 5.1 Clone repository (trên VPS 1)

```bash
ssh deploy@<VPS1_IP>
cd /opt/ivf

# Clone (private repo — cần SSH key hoặc token)
git clone git@github.com:Hung6066/IVF.git .
# hoặc dùng HTTPS + PAT:
# git clone https://<PAT>@github.com/Hung6066/IVF.git .
```

### 5.2 Tạo Secrets

```bash
cd /opt/ivf

# Tạo thư mục secrets
mkdir -p secrets

# ─── Database passwords ───
openssl rand -base64 32 > secrets/ivf_db_password.txt
openssl rand -base64 32 > secrets/ejbca_db_password.txt
openssl rand -base64 32 > secrets/signserver_db_password.txt

# ─── JWT secret (RS256 private key) ───
openssl genrsa -out secrets/jwt_private.pem 2048
openssl rsa -in secrets/jwt_private.pem -pubout -out secrets/jwt_public.pem
echo "$(cat secrets/jwt_private.pem)" > secrets/jwt_secret.txt

# ─── MinIO credentials ───
echo "ivf-admin-$(openssl rand -hex 8)" > secrets/minio_access_key.txt
openssl rand -base64 32 > secrets/minio_secret_key.txt

# ─── API cert password (cho mTLS với SignServer) ───
openssl rand -base64 24 > secrets/api_cert_password.txt

# ─── SoftHSM2 PINs (Phase 4 PKI) ───
openssl rand -hex 16 > secrets/softhsm_pin.txt
openssl rand -hex 16 > secrets/softhsm_so_pin.txt

# ─── GPG passphrase cho backup encryption ───
openssl rand -base64 32 > secrets/gpg_passphrase.txt

# Set permissions
chmod 600 secrets/*
chown deploy:deploy secrets/*

# Verify
ls -la secrets/
```

### 5.3 Tạo appsettings.Production.json

```bash
cat > src/IVF.API/appsettings.Production.json << 'SETTINGS'
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=db;Port=5432;Database=ivf_db;Username=postgres;Password=DOCKER_SECRET;SSL Mode=Prefer",
    "Redis": "redis:6379,abortConnect=false,connectTimeout=5000,syncTimeout=3000"
  },
  "JwtSettings": {
    "Secret": "DOCKER_SECRET",
    "Issuer": "IVF-System",
    "Audience": "IVF-Users",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "MinIO": {
    "Endpoint": "minio:9000",
    "AccessKey": "DOCKER_SECRET",
    "SecretKey": "DOCKER_SECRET",
    "UseSSL": false,
    "DocumentsBucket": "ivf-documents",
    "SignedPdfsBucket": "ivf-signed-pdfs",
    "MedicalImagesBucket": "ivf-medical-images",
    "BaseUrl": "https://your-domain.ivf.clinic:9000"
  },
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "WorkerName": "PDFSigner",
    "SkipTlsValidation": false,
    "RequireMtls": true,
    "EnableAuditLogging": true,
    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem",
    "SigningRateLimitPerMinute": 30
  },
  "BackupSchedule": {
    "Enabled": true,
    "CronExpression": "0 2 * * *",
    "RetentionDays": 90,
    "MaxBackupCount": 30
  },
  "CloudBackup": {
    "Provider": "S3",
    "CompressionEnabled": true,
    "S3": {
      "Region": "ap-southeast-1",
      "BucketName": "ivf-backups-production"
    }
  },
  "Cors": {
    "AllowedOrigins": [
      "https://ivf.clinic",
      "https://*.ivf.clinic"
    ]
  }
}
SETTINGS
```

> **Lưu ý:** `DOCKER_SECRET` là placeholder — trong production, application đọc từ Docker Secrets files (`/run/secrets/...`) thông qua `ASPNETCORE_` environment variable overrides hoặc startup code.

---

## 6. CI/CD & Container Registry (GHCR)

> **Thay đổi v2.0:** Images không còn build thủ công trên VPS. CI/CD tự động build + push lên GitHub Container Registry (GHCR) khi push code lên `main`.

### 6.1 Tổng quan CI/CD Pipeline

```
Push code → GitHub Actions CI/CD
  ├─ Backend: dotnet restore → build → test (255 tests)
  ├─ Frontend: npm ci → lint → build --production
  ├─ Secret Scanning: Gitleaks v8.21.2
  ├─ Docker Build + Push:
  │   ├─ ghcr.io/hung6066/ivf:latest        (API — .NET 10)
  │   └─ ghcr.io/hung6066/ivf-client:latest  (Frontend — Angular 21)
  └─ Auto Deploy → SSH vào VPS Manager → docker service update
```

**Workflows:**

| File                    | Trigger                         | Mục đích                                                    |
| ----------------------- | ------------------------------- | ----------------------------------------------------------- |
| `ci-cd.yml`             | Push `main`/`develop`, PR       | Build, test, push Docker images to GHCR                     |
| `deploy-production.yml` | CI/CD thành công trên `main`    | Deploy production (backup DB → pull image → rolling update) |
| `deploy-staging.yml`    | CI/CD thành công trên `develop` | Deploy staging                                              |
| `security-scan.yml`     | Hàng tuần / thủ công            | Gitleaks secret scanning                                    |
| `pr-check.yml`          | Pull Request                    | PR validation                                               |
| `release.yml`           | Tag `v*`                        | Create GitHub Release                                       |

### 6.2 Container Images trên GHCR

```bash
# API image (build từ src/IVF.API/Dockerfile)
ghcr.io/hung6066/ivf:latest
ghcr.io/hung6066/ivf:main
ghcr.io/hung6066/ivf:sha-abc1234

# Frontend image (build từ ivf-client/Dockerfile)
ghcr.io/hung6066/ivf-client:latest
ghcr.io/hung6066/ivf-client:main
ghcr.io/hung6066/ivf-client:sha-abc1234
```

### 6.3 GHCR Authentication trên VPS

```bash
# Login GHCR trên VPS (cần 1 lần, hoặc khi token hết hạn)
echo "<GHCR_TOKEN>" | docker login ghcr.io -u hung6066 --password-stdin

# Pull images thủ công (CI/CD tự pull khi deploy)
docker pull ghcr.io/hung6066/ivf:latest
docker pull ghcr.io/hung6066/ivf-client:latest
```

### 6.4 GitHub Secrets cần thiết

Cấu hình trong repo Settings → Secrets and variables → Actions:

| Secret             | Giá trị                     | Dùng cho                      |
| ------------------ | --------------------------- | ----------------------------- |
| `SSH_PRIVATE_KEY`  | ed25519 private key         | SSH vào VPS từ GitHub Actions |
| `SSH_HOST_MANAGER` | `45.134.226.56` (VPS 1 IP)  | Production deploy target      |
| `SSH_HOST_WORKER`  | `194.163.181.19` (VPS 2 IP) | Worker node                   |
| `SSH_USER`         | `root`                      | SSH user                      |
| `GHCR_TOKEN`       | GitHub PAT (packages:read)  | Pull images trên VPS          |
| `DISCORD_WEBHOOK`  | Discord webhook URL         | Deploy notifications          |

### 6.5 SSH Deploy Key

```bash
# Tạo ed25519 deploy key (đã thực hiện)
ssh-keygen -t ed25519 -C "github-actions-deploy@ivf" -f ~/.ssh/id_deploy -N ""

# Public key đã thêm vào cả 2 VPS (/root/.ssh/authorized_keys)
# Private key → GitHub Secret SSH_PRIVATE_KEY
```

### 6.6 Dockerfile — API (.NET 10)

File: `src/IVF.API/Dockerfile` — Multi-stage build:

- Stage 1 (`sdk`): `mcr.microsoft.com/dotnet/sdk:10.0` → restore + build + publish
- Stage 2 (`runtime`): `mcr.microsoft.com/dotnet/aspnet:10.0` → install Docker CLI + copy published output

> **Quan trọng:** Runtime stage cài `docker-ce-cli` từ Docker official Ubuntu repo. Docker CLI cần thiết cho `InfrastructureMonitorService` — quản lý Swarm services, nodes, events qua Docker socket (`/var/run/docker.sock`) mount vào container.

### 6.7 Dockerfile — Frontend (Angular 21)

File: `ivf-client/Dockerfile` — Multi-stage build:

- Stage 1 (`build`): `node:20-alpine` → npm ci + npm run build --production
- Stage 2 (`runtime`): `caddy:2-alpine` → copy dist + Caddyfile (SPA serving)

> **Quan trọng:** `Directory.Build.props` ở repo root chứa `<EnableWindowsTargeting>true</EnableWindowsTargeting>` để CI Linux build hỗ trợ `net9.0-windows` project (`IVF.FingerprintClient`).

---

## 7. Tạo Swarm Stack File

> **Thay đổi v2.0:** File đổi tên từ `stack.yml` thành `docker-compose.stack.yml`. Images pull từ GHCR thay vì build local. Secrets dùng `external: true` (tạo trước bằng `docker secret create`). Thêm Frontend service. Bỏ `version: "3.8"` (deprecated). Bỏ `db-standby` (chưa triển khai).

### 7.1 File `docker-compose.stack.yml`

File này nằm ở repo root: `docker-compose.stack.yml`. Copy lên VPS tại `/opt/ivf/docker-compose.stack.yml`.

```yaml
# =====================================================
# IVF System — Docker Swarm Stack (Self-contained)
# =====================================================
# Standalone file for: docker stack deploy -c docker-compose.stack.yml ivf
# No build, depends_on, container_name, restart, profiles (Swarm-incompatible)
# =====================================================

services:
  # ─── PostgreSQL (IVF Main DB) ───
  db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD_FILE=/run/secrets/ivf_db_password
    secrets:
      - ivf_db_password
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - postgres_archive:/var/lib/postgresql/archive
    networks:
      - ivf-data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5
    deploy:
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 2G

  # ─── IVF API ───
  api:
    image: ghcr.io/hung6066/ivf:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_HTTP_PORTS=8080
      - ConnectionStrings__DefaultConnection=Host=db;Database=ivf_db;Username=postgres;Password=FILE:/run/secrets/ivf_db_password
      - ConnectionStrings__Redis=redis:6379,abortConnect=false
      - JwtSettings__Secret=FILE:/run/secrets/jwt_secret
      - JwtSettings__Issuer=IVF_System
      - JwtSettings__Audience=IVF_Users
      # Digital signing disabled until EJBCA client certs are provisioned
      - DigitalSigning__Enabled=false
      - Cors__AllowedOrigins__0=https://natra.site
      - MinIO__Endpoint=minio:9000
      - MinIO__AccessKey=FILE:/run/secrets/minio_access_key
      - MinIO__SecretKey=FILE:/run/secrets/minio_secret_key
      - MinIO__UseSSL=false
      - MinIO__DocumentsBucket=ivf-documents
      - MinIO__SignedPdfsBucket=ivf-signed-pdfs
      - MinIO__MedicalImagesBucket=ivf-medical-images
    secrets:
      - ivf_db_password
      - jwt_secret
      - minio_access_key
      - minio_secret_key
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro # Infrastructure monitoring (Swarm management)
    networks:
      - ivf-public
      - ivf-data
    deploy:
      replicas: 1
      update_config:
        parallelism: 1
        delay: 30s
        order: start-first
      rollback_config:
        parallelism: 1
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 1G

  # ─── Redis ───
  redis:
    image: redis:alpine
    command: >
      redis-server
      --maxmemory 256mb
      --maxmemory-policy allkeys-lru
    networks:
      - ivf-data
    deploy:
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 512M

  # ─── EJBCA (Certificate Authority) ───
  ejbca:
    image: keyfactor/ejbca-ce:latest
    hostname: ejbca.ivf.local
    environment:
      - DATABASE_JDBC_URL=jdbc:postgresql://ejbca-db:5432/ejbca
      - DATABASE_USER=ejbca
      - DATABASE_PASSWORD_FILE=/run/secrets/ejbca_db_password
      - TLS_SETUP_ENABLED=later
    secrets:
      - ejbca_db_password
    ports:
      - "8443:8443"
    volumes:
      - ejbca_persistent:/opt/keyfactor/persistent
    networks:
      - ivf-public
      - ivf-signing
      - ivf-data
    deploy:
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 2G

  # ─── EJBCA Database ───
  ejbca-db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_USER=ejbca
      - POSTGRES_PASSWORD_FILE=/run/secrets/ejbca_db_password
      - POSTGRES_DB=ejbca
    secrets:
      - ejbca_db_password
    volumes:
      - ejbca_db_data:/var/lib/postgresql/data
    networks:
      - ivf-data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ejbca"]
      interval: 5s
      timeout: 5s
      retries: 5
    deploy:
      placement:
        constraints:
          - node.labels.role == primary

  # ─── SignServer (Document Signing) ───
  signserver:
    image: keyfactor/signserver-ce:latest
    hostname: signserver.ivf.local
    environment:
      - DATABASE_JDBC_URL=jdbc:postgresql://signserver-db:5432/signserver
      - DATABASE_USER=signserver
      - DATABASE_PASSWORD_FILE=/run/secrets/signserver_db_password
      - TLS_SETUP_ENABLED=later
    secrets:
      - signserver_db_password
      - keystore_password
      - softhsm_pin
      - softhsm_so_pin
    ports:
      - "9443:8443"
    volumes:
      - signserver_persistent:/opt/keyfactor/persistent
      - softhsm_tokens:/opt/keyfactor/persistent/softhsm/tokens
    networks:
      - ivf-public
      - ivf-signing
      - ivf-data
    deploy:
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 2G

  # ─── SignServer Database ───
  signserver-db:
    image: postgres:16-alpine
    environment:
      - POSTGRES_USER=signserver
      - POSTGRES_PASSWORD_FILE=/run/secrets/signserver_db_password
      - POSTGRES_DB=signserver
    secrets:
      - signserver_db_password
    volumes:
      - signserver_db_data:/var/lib/postgresql/data
    networks:
      - ivf-data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U signserver"]
      interval: 5s
      timeout: 5s
      retries: 5
    deploy:
      placement:
        constraints:
          - node.labels.role == primary

  # ─── MinIO (Object Storage) ───
  minio:
    image: minio/minio:latest
    environment:
      - MINIO_ROOT_USER_FILE=/run/secrets/minio_access_key
      - MINIO_ROOT_PASSWORD_FILE=/run/secrets/minio_secret_key
    secrets:
      - minio_access_key
      - minio_secret_key
    ports:
      - "9000:9000"
      - "127.0.0.1:9001:9001"
    volumes:
      - minio_data:/data
    command: server /data --console-address ":9001"
    networks:
      - ivf-data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 1G

  # ─── MinIO Init (one-shot) ───
  minio-init:
    image: minio/mc:latest
    entrypoint: >
      /bin/sh -c "
      sleep 10;
      mc alias set ivf http://minio:9000 $$(cat /run/secrets/minio_access_key) $$(cat /run/secrets/minio_secret_key);
      mc mb ivf/ivf-documents --ignore-existing;
      mc mb ivf/ivf-signed-pdfs --ignore-existing;
      mc mb ivf/ivf-medical-images --ignore-existing;
      echo 'MinIO buckets initialized successfully';
      exit 0;
      "
    secrets:
      - minio_access_key
      - minio_secret_key
    networks:
      - ivf-data
    deploy:
      restart_policy:
        condition: on-failure
        max_attempts: 3

  # ─── Frontend (Angular) ───
  frontend:
    image: ghcr.io/hung6066/ivf-client:latest
    networks:
      - ivf-public
    deploy:
      replicas: 1
      update_config:
        parallelism: 1
        delay: 10s
        order: start-first
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 256M

  # ─── Caddy Reverse Proxy ───
  caddy:
    image: caddy:2-alpine
    ports:
      - target: 80
        published: 80
        mode: host # Preserve real client IP (bypass Swarm ingress NAT)
      - target: 443
        published: 443
        mode: host # Required for IP whitelist / SecurityEnforcementMiddleware
    configs:
      - source: caddyfile_v2
        target: /etc/caddy/Caddyfile
    volumes:
      - caddy_data:/data
      - caddy_config:/config
    networks:
      - ivf-public
    deploy:
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 256M

# ─── Docker Configs ───
configs:
  caddyfile_v2:
    file: ./Caddyfile

# ─── Docker Secrets (external — tạo trước bằng docker secret create) ───
secrets:
  ivf_db_password:
    external: true
  ejbca_db_password:
    external: true
  signserver_db_password:
    external: true
  keystore_password:
    external: true
  jwt_secret:
    external: true
  minio_access_key:
    external: true
  minio_secret_key:
    external: true
  api_cert_password:
    external: true
  softhsm_pin:
    external: true
  softhsm_so_pin:
    external: true

# ─── Volumes ───
volumes:
  postgres_data:
  postgres_archive:
  ejbca_persistent:
  ejbca_db_data:
  signserver_persistent:
  signserver_db_data:
  softhsm_tokens:
  minio_data:
  caddy_data:
  caddy_config:

# ─── Networks ───
networks:
  ivf-public:
    driver: overlay
  ivf-signing:
    driver: overlay
    internal: true
  ivf-data:
    driver: overlay
    internal: true
```

### 7.2 Caddyfile

File `Caddyfile` nằm ở repo root, được mount vào Caddy container qua Docker configs:

```caddyfile
natra.site {
    # API requests
    handle /api/* {
        reverse_proxy api:8080
    }

    # SignalR hubs
    handle /hubs/* {
        reverse_proxy api:8080
    }

    # Swagger UI
    handle /swagger/* {
        reverse_proxy api:8080
    }

    # Everything else -> Angular frontend
    handle {
        reverse_proxy frontend:80
    }

    # Security headers
    header {
        X-Content-Type-Options nosniff
        X-Frame-Options DENY
        Referrer-Policy strict-origin-when-cross-origin
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        -Server
    }

    # Compression
    encode gzip zstd
}
```

> **Lưu ý:** Sử dụng domain name thay vì `:80` — Caddy tự động xin SSL certificate từ Let's Encrypt và redirect HTTP → HTTPS. HSTS header bật preload.

### 7.3 Khác biệt chính so với v1.0

| Thay đổi         | v1.0 (cũ)                  | v4.0 (hiện tại)                              |
| ---------------- | -------------------------- | -------------------------------------------- |
| File name        | `stack.yml`                | `docker-compose.stack.yml`                   |
| API image        | `ivf-api:latest` (local)   | `ghcr.io/hung6066/ivf:v4-signing` (GHCR)     |
| API Dockerfile   | Chỉ aspnet runtime         | + Docker CLI (infrastructure monitor)        |
| API volumes      | Không                      | `/var/run/docker.sock:ro` (Swarm mgmt)       |
| API replicas     | 1 (chỉ VPS 1)              | 2 (VPS 1 + VPS 2)                            |
| Frontend         | ❌ Không có                | ✅ `ghcr.io/hung6066/ivf-client:latest`      |
| Caddy mode       | `replicated` (VPS 1 only)  | `global` (chạy mọi node)                     |
| Caddy ports      | `"80:80"` (ingress)        | `mode: host` (preserve real client IP)       |
| Caddy config     | `caddyfile`                | `caddyfile_v2` (versioned, immutable)        |
| Caddyfile        | `:80` (HTTP only)          | `natra.site` (auto HTTPS + HSTS)             |
| Caddyfile source | `./docker/caddy/Caddyfile` | `./Caddyfile` (repo root)                    |
| Caddy routing    | Static files trực tiếp     | Reverse proxy → frontend container           |
| CORS             | Không cấu hình             | `Cors__AllowedOrigins__0=https://natra.site` |
| DB Standby       | ❌ Chưa có                 | ✅ Streaming replication trên VPS 2           |
| Redis Replica    | ❌ Chưa có                 | ✅ `--replicaof redis 6379` trên VPS 2        |
| Secrets          | `file:` (local files)      | `external: true` (Swarm raft store)          |
| DigitalSigning   | `Enabled=false`            | `Enabled=true` (SkipTlsValidation, no mTLS)  |
| PG configs       | Không cần                  | WAL replication + standby entrypoint config  |
| `version` key    | `"3.8"`                    | Bỏ (deprecated trong Docker 25+)             |

### 7.4 Placement constraints hiện tại

```
┌──────────────────────────────────────┬──────────────────────────────────┐
│       VPS 1 — primary (24 GB)        │     VPS 2 — standby (11 GB)      │
├──────────────────────────────────────┼──────────────────────────────────┤
│ ✅ caddy         (global mode)       │ ✅ caddy         (global mode)   │
│ ✅ frontend      (Angular SPA)       │ ✅ api replica 2 (.NET 10)       │
│ ✅ api replica 1 (.NET 10)           │ ✅ db-standby    (PG streaming)  │
│ ✅ db            (PostgreSQL 16)     │ ✅ redis-replica (read replica)  │
│ ✅ redis         (cache primary)     │                                  │
│ ✅ minio         (object storage)    │                                  │
│ ✅ minio-init    (one-shot, done)    │                                  │
│ ✅ ejbca         (CA server)         │                                  │
│ ✅ ejbca-db                          │                                  │
│ ✅ signserver    (document signing)  │                                  │
│ ✅ signserver-db                     │                                  │
├──────────────────────────────────────┼──────────────────────────────────┤
│  RAM ~5.5 GB / 24 GB                │  RAM ~1.9 GB / 11 GB            │
│  12 services                        │  4 services                      │
└──────────────────────────────────────┴──────────────────────────────────┘
```

**Replication:**
- PostgreSQL: async streaming replication (primary → standby via replicator user + standby_slot)
- Redis: `--replicaof redis 6379` (async read-only replica)
- Caddy: `mode: global` (runs on every Swarm node, `mode: host` ports for real client IP)

---

## 8. Deploy Stack lên Swarm

### 8.1 Tạo Secrets (chỉ cần cho lần deploy đầu)

```bash
ssh root@45.134.226.56

# Tạo secrets trong Swarm Raft store
echo "your_db_password" | docker secret create ivf_db_password -
echo "your_jwt_secret" | docker secret create jwt_secret -
echo "your_minio_access_key" | docker secret create minio_access_key -
echo "your_minio_secret_key" | docker secret create minio_secret_key -
echo "your_ejbca_db_password" | docker secret create ejbca_db_password -
echo "your_signserver_db_password" | docker secret create signserver_db_password -
echo "your_keystore_password" | docker secret create keystore_password -
echo "your_api_cert_password" | docker secret create api_cert_password -
echo "your_softhsm_pin" | docker secret create softhsm_pin -
echo "your_softhsm_so_pin" | docker secret create softhsm_so_pin -

# Verify
docker secret ls
```

### 8.2 Deploy lần đầu

```bash
cd /opt/ivf

# Login GHCR để pull images
echo "<GHCR_TOKEN>" | docker login ghcr.io -u hung6066 --password-stdin

# Deploy stack
docker stack deploy -c docker-compose.stack.yml ivf

# Theo dõi quá trình deploy
watch docker service ls
```

### 8.3 Kiểm tra services

```bash
# Xem tất cả services
docker service ls

# Output mong đợi:
# ID     NAME               MODE       REPLICAS  IMAGE                              PORTS
# xxx    ivf_api            replicated 1/1       ghcr.io/hung6066/ivf:latest
# xxx    ivf_frontend       replicated 1/1       ghcr.io/hung6066/ivf-client:latest
# xxx    ivf_caddy          replicated 1/1       caddy:2-alpine                     *:80->80, *:443->443
# xxx    ivf_db             replicated 1/1       postgres:16-alpine
# xxx    ivf_redis          replicated 1/1       redis:alpine
# xxx    ivf_minio          replicated 1/1       minio/minio:latest                 *:9000->9000
# xxx    ivf_minio-init     replicated 0/1       minio/mc:latest                    (completed)
# xxx    ivf_ejbca          replicated 1/1       keyfactor/ejbca-ce:latest           *:8443->8443
# xxx    ivf_ejbca-db       replicated 1/1       postgres:16-alpine
# xxx    ivf_signserver     replicated 0/1       keyfactor/signserver-ce:latest      *:9443->8443 (needs PKI)
# xxx    ivf_signserver-db  replicated 1/1       postgres:16-alpine

# Xem logs
docker service logs -f ivf_api --tail=20
docker service logs -f ivf_frontend --tail=20
docker service logs -f ivf_caddy --tail=20
```

### 8.4 CI/CD Auto-Deploy

Khi push code lên `main`, GitHub Actions tự động:

1. Build + test → Push images lên GHCR
2. SSH vào VPS Manager (45.134.226.56)
3. Backup database (nếu DB container tồn tại)
4. Pull images mới + rolling update

```bash
# CI/CD chạy lệnh tương đương:
docker service update --image ghcr.io/hung6066/ivf:latest ivf_api
docker service update --image ghcr.io/hung6066/ivf-client:latest ivf_frontend
```

> **Không cần deploy thủ công** khi code đã merge vào `main`. CI/CD tự xử lý.

### 8.5 Xử lý lỗi deploy thường gặp

```bash
# Service không start? Xem task errors:
docker service ps ivf_api --no-trunc

# GHCR authentication error?
docker login ghcr.io -u hung6066
# → Cần GHCR_TOKEN có quyền packages:read

# Container crash loop?
docker service logs ivf_api --since 5m

# Secret không đọc được?
docker secret ls
docker secret inspect ivf_db_password

# Network không tạo?
docker network ls | grep ivf

# Force re-deploy (pull latest image):
docker service update --force --image ghcr.io/hung6066/ivf:latest ivf_api
```

---

## 9. Ansible Automation (Tùy chọn)

> **Thay đổi v2.0:** Thêm Ansible playbook để tự động hóa toàn bộ quá trình setup VPS + deploy, thay cho các bước thủ công ở Section 3-8.

### 9.1 Tổng quan

Ansible playbook trong thư mục `ansible/` tự động hóa 3 giai đoạn:

```
Phase 1: common  → Update OS, packages, user deploy, SSH hardening, UFW, fail2ban
Phase 2: docker  → Cài Docker CE, khởi tạo Swarm cluster, node labels, GHCR login
Phase 3: app     → Generate secrets, tạo Docker secrets, pull images, deploy stack
```

### 9.2 Cấu trúc thư mục

```
ansible/
├── site.yml                    # Master playbook (3 phases)
├── hosts.example.yml           # Inventory template
└── roles/
    ├── common/tasks/main.yml   # OS setup, SSH, firewall, fail2ban
    ├── docker/tasks/main.yml   # Docker CE, Swarm init, node labels
    └── app/tasks/main.yml      # Secrets, clone, deploy stack
```

### 9.3 Setup

```bash
# Cần cài Ansible trên máy local (hoặc WSL2)
pip install ansible

# Copy inventory template
cd ansible
cp hosts.example.yml hosts.yml

# Sửa hosts.yml: thay <VPS1_IP> và <VPS2_IP> bằng IP thực
# VPS1: 45.134.226.56 (manager), VPS2: 194.163.181.19 (worker)
```

### 9.4 Sử dụng

```bash
# Setup toàn bộ từ zero (OS + Docker + Deploy)
ansible-playbook -i hosts.yml site.yml --extra-vars "ghcr_token=ghp_xxx"

# Chỉ setup cơ bản (OS, Docker) — không deploy app
ansible-playbook -i hosts.yml site.yml --tags setup

# Chỉ deploy app (skip OS/Docker setup)
ansible-playbook -i hosts.yml site.yml --tags deploy --extra-vars "ghcr_token=ghp_xxx"

# Deploy image cụ thể (version tag)
ansible-playbook -i hosts.yml site.yml --tags deploy \
  --extra-vars "ghcr_token=ghp_xxx image_tag=v1.2.0"

# Dry run (xem sẽ làm gì, không thay đổi gì)
ansible-playbook -i hosts.yml site.yml --check --diff
```

### 9.5 Chạy từ WSL2

Nếu dùng WSL2 Ubuntu trên Windows, cần bật **mirrored networking** để SSH từ WSL2 tới VPS:

```powershell
# File: %USERPROFILE%\.wslconfig
[wsl2]
networkingMode=mirrored
```

```bash
# WSL2: copy SSH key từ Windows
cp /mnt/c/Users/<username>/.ssh/id_ed25519 ~/.ssh/
chmod 600 ~/.ssh/id_ed25519

# Test SSH
ssh -o StrictHostKeyChecking=no root@45.134.226.56 "hostname"
```

### 9.6 So sánh: Thủ công vs Ansible

| Bước                | Thủ công                           | Ansible                   |
| ------------------- | ---------------------------------- | ------------------------- |
| Setup OS + firewall | ~30 phút × 2 VPS                   | `--tags setup` (5 phút)   |
| Cài Docker + Swarm  | ~20 phút × 2 VPS                   | Tự động trong Phase 2     |
| Tạo secrets         | Manual `docker secret create` × 10 | Tự động generate + create |
| Deploy stack        | SSH + `docker stack deploy`        | `--tags deploy` (2 phút)  |
| **Tổng**            | **~2 giờ**                         | **~10 phút**              |

> **Lưu ý:** Ansible là **tùy chọn**. Bạn có thể setup thủ công theo Section 3-8 hoặc dùng Ansible. CI/CD (Section 6) xử lý deploy hàng ngày — Ansible chỉ cần cho lần setup đầu hoặc khi thêm VPS mới.

---

## 10. Cấu hình PostgreSQL Replication

### 10.1 Verify Primary (VPS 1)

```bash
# Exec vào container db trên VPS 1
docker exec -it $(docker ps -q -f name=ivf_db.1) psql -U postgres

-- Kiểm tra WAL level
SHOW wal_level;          -- Phải là 'replica'
SHOW max_wal_senders;    -- Phải >= 5
SHOW archive_mode;       -- Phải là 'on'

-- Kiểm tra replication user
SELECT usename, userepl FROM pg_user WHERE usename = 'replicator';
-- usename    | userepl
-- replicator | t

-- Tạo replication slot cho standby
SELECT pg_create_physical_replication_slot('standby_slot');

-- Verify slot
SELECT slot_name, active FROM pg_replication_slots;

\q
```

### 10.2 Setup Standby (VPS 2)

Nếu standby-entrypoint.sh đã chạy tự động, verify:

```bash
# Exec vào standby
docker exec -it $(docker ps -q -f name=ivf_db-standby.1) psql -U postgres

-- Kiểm tra mode
SELECT pg_is_in_recovery();    -- Phải là 't' (true = standby mode)

-- Kiểm tra replication status
SELECT status, sender_host, sender_port FROM pg_stat_wal_receiver;
-- status    | sender_host | sender_port
-- streaming | db          | 5432

\q
```

### 10.3 Kiểm tra replication từ Primary

```bash
docker exec -it $(docker ps -q -f name=ivf_db.1) psql -U postgres

-- Xem replication connections
SELECT client_addr, state, sent_lsn, replay_lsn,
       pg_size_pretty(pg_wal_lsn_diff(sent_lsn, replay_lsn)) AS lag
FROM pg_stat_replication;

-- client_addr | state     | sent_lsn  | replay_lsn | lag
-- 10.0.x.x   | streaming | 0/3000148 | 0/3000148  | 0 bytes

\q
```

> **Mong đợi:** `lag = 0 bytes` nghĩa là standby đã replay hết WAL, dữ liệu đồng bộ real-time.

---

## 11. Cấu hình PKI — EJBCA & SignServer

### 11.1 Verify EJBCA khởi động

```bash
# Đợi EJBCA start (mất ~2-3 phút lần đầu)
docker service logs -f ivf_ejbca --tail=20

# Chờ thấy: "EJBCA ... started"
# Health check
docker service ps ivf_ejbca
```

### 11.2 Setup mTLS cho SignServer

```bash
# Chạy init-mtls script
docker exec $(docker ps -q -f name=ivf_signserver.1) \
  bash /opt/keyfactor/persistent/init-mtls.sh

# Script sẽ:
# 1. Tạo JKS truststore với internal CA
# 2. Configure WildFly SSL context
# 3. Bật want-client-auth trên HTTPS listener
# 4. Register API client cert
```

### 11.3 Tạo API client certificate

```bash
# Export CA cert từ EJBCA
docker exec $(docker ps -q -f name=ivf_ejbca.1) \
  bash -c "cat /opt/keyfactor/ejbca-ce/p12/ca.pem" > certs/ca-chain.pem

# Tạo API client cert (P12)
# → Thường tạo qua EJBCA Admin UI hoặc REST API
# Kết quả: certs/api/api-client.p12

# Copy certs vào API volume
docker cp certs/. $(docker ps -q -f name=ivf_api.1):/app/certs/
```

> **Chi tiết setup PKI:** Xem `docs/digital_signing.md` và scripts `scripts/init-mtls.sh`

---

## 12. Cấu hình DNS & Caddy SSL

### 12.1 Cloudflare DNS Setup

```
Đăng nhập Cloudflare → DNS Records:

# Primary domain
A    ivf.clinic          → <VPS1_IP>    (Proxied ☁️)
A    ivf.clinic          → <VPS2_IP>    (Proxied ☁️)   ← DNS round-robin

# Wildcard cho tenants
CNAME *.ivf.clinic       → ivf.clinic   (DNS Only ☁️)
  ↑ Wildcard record — Caddy sẽ auto-issue cert cho *.ivf.clinic

# Health check (Cloudflare Load Balancing nếu dùng Pro plan)
# Origin 1: VPS1_IP, Origin 2: VPS2_IP
# Health check: HTTPS /health/live
# Failover: automatic
```

### 12.2 Caddy Health Check Endpoint

Caddy dùng `api:8080` làm upstream — Swarm DNS tự resolve tới tất cả API replicas.

### 12.3 Custom Tenant Domains

Khi tenant cấu hình custom domain (ví dụ: `fertility.hospital.vn`):

1. Tenant tạo CNAME record: `fertility.hospital.vn → ivf.clinic`
2. API verify domain ownership qua endpoint `/api/tenants/domain-check`
3. Caddy nhận request → `on_demand_tls` → ask API → nếu verified → auto-issue cert
4. Từ lần sau: cert đã cached, phục vụ bình thường

```
# Luồng On-Demand TLS:
Browser → https://fertility.hospital.vn
  ↓
Caddy (chưa có cert)
  ↓ ask http://api:8080/api/tenants/domain-check?domain=fertility.hospital.vn
  ↓ API response: 200 OK (domain verified)
  ↓
Caddy → Let's Encrypt → Issue cert → TLS handshake → Serve request
```

---

## 13. Thiết lập AWS S3 Backup

### 13.1 Tạo AWS Account & IAM

```bash
# 1. Đăng ký AWS account: https://aws.amazon.com
# 2. Bật MFA cho root account
# 3. Tạo IAM user riêng cho backup

# ─── Từ AWS CLI (hoặc Console) ───

# Tạo S3 bucket (Singapore — gần VN)
aws s3 mb s3://ivf-backups-production --region ap-southeast-1

# Bật versioning (chống accidental delete)
aws s3api put-bucket-versioning \
  --bucket ivf-backups-production \
  --versioning-configuration Status=Enabled

# Bật encryption mặc định (AES-256)
aws s3api put-bucket-encryption \
  --bucket ivf-backups-production \
  --server-side-encryption-configuration '{
    "Rules": [{
      "ApplyServerSideEncryptionByDefault": {
        "SSEAlgorithm": "AES256"
      }
    }]
  }'

# Block ALL public access
aws s3api put-public-access-block \
  --bucket ivf-backups-production \
  --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
```

### 13.2 S3 Lifecycle Policy (Auto-tier để tiết kiệm)

```bash
aws s3api put-bucket-lifecycle-configuration \
  --bucket ivf-backups-production \
  --lifecycle-configuration '{
    "Rules": [
      {
        "ID": "DatabaseBackupTiering",
        "Status": "Enabled",
        "Filter": {"Prefix": "daily/"},
        "Transitions": [
          {"Days": 30, "StorageClass": "STANDARD_IA"},
          {"Days": 90, "StorageClass": "GLACIER"},
          {"Days": 365, "StorageClass": "DEEP_ARCHIVE"}
        ]
      },
      {
        "ID": "WALRetention",
        "Status": "Enabled",
        "Filter": {"Prefix": "wal/"},
        "Transitions": [
          {"Days": 30, "StorageClass": "STANDARD_IA"}
        ],
        "Expiration": {"Days": 90}
      },
      {
        "ID": "MinIOFileTiering",
        "Status": "Enabled",
        "Filter": {"Prefix": "minio/"},
        "Transitions": [
          {"Days": 60, "StorageClass": "STANDARD_IA"},
          {"Days": 180, "StorageClass": "GLACIER"}
        ]
      }
    ]
  }'
```

### 13.3 Tạo IAM User (Least Privilege)

```bash
# Tạo IAM user
aws iam create-user --user-name ivf-backup-agent

# Attach policy (chỉ đọc/ghi vào bucket này)
aws iam put-user-policy --user-name ivf-backup-agent \
  --policy-name ivf-s3-backup-only \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Action": [
          "s3:PutObject",
          "s3:GetObject",
          "s3:ListBucket",
          "s3:DeleteObject",
          "s3:GetBucketLocation"
        ],
        "Resource": [
          "arn:aws:s3:::ivf-backups-production",
          "arn:aws:s3:::ivf-backups-production/*"
        ]
      }
    ]
  }'

# Tạo access key
aws iam create-access-key --user-name ivf-backup-agent
# ↓ LƯU LẠI:
# {
#   "AccessKeyId": "AKIA...",
#   "SecretAccessKey": "wJal..."
# }
```

### 13.4 Cấu hình AWS CLI trên VPS 1

```bash
ssh deploy@<VPS1_IP>

# Configure (chỉ VPS 1 — nơi chạy backup script)
aws configure
# AWS Access Key ID:     AKIA...
# AWS Secret Access Key: wJal...
# Default region:        ap-southeast-1
# Default output:        json

# Verify connection
aws s3 ls s3://ivf-backups-production/
# (empty — chưa có gì)

# Test upload
echo "test" > /tmp/test.txt
aws s3 cp /tmp/test.txt s3://ivf-backups-production/test.txt
aws s3 ls s3://ivf-backups-production/
# → test.txt
aws s3 rm s3://ivf-backups-production/test.txt
rm /tmp/test.txt
```

### 13.5 Chi phí ước tính

```
┌─────────────────────────────────────────────────────────────┐
│  AWS S3 Chi phí thực tế (~170 GB tổng)                     │
│                                                             │
│  Database backups (daily pg_dump):                          │
│    30 ngày × 5 GB = 50 GB S3 Standard  = $1.15/tháng      │
│    60 ngày × 5 GB = 50 GB S3-IA        = $0.63/tháng      │
│    Older            50 GB Glacier       = $0.20/tháng      │
│                                                             │
│  WAL archives (compressed):                                │
│    ~20 GB/tháng S3 Standard             = $0.46/tháng      │
│                                                             │
│  MinIO mirror (medical files):                              │
│    ~50 GB                                = $1.15/tháng      │
│                                                             │
│  PUT/GET requests:                                          │
│    ~100K/tháng                           = $0.50/tháng      │
│                                                             │
│  Data transfer in:                MIỄN PHÍ                  │
│                                                             │
│  ═══════════════════════════════════════════════            │
│  TỔNG:                              ~$4-6/tháng             │
│  ═══════════════════════════════════════════════            │
│                                                             │
│  Alternatives rẻ hơn:                                       │
│  • Backblaze B2:   ~$1/tháng  (S3-compatible)              │
│  • Cloudflare R2:  ~$2.50/tháng (zero egress)              │
│  • Wasabi:         ~$1.20/tháng (no egress)                │
└─────────────────────────────────────────────────────────────┘
```

---

## 14. Scripts Backup tự động

### 14.1 Script chính: backup-to-s3.sh

```bash
cat > /opt/ivf/scripts/backup-to-s3.sh << 'BACKUP_SCRIPT'
#!/bin/bash
# ═══════════════════════════════════════════════════════════
#  IVF Platform — Daily Backup to AWS S3
#  Chạy: 0 3 * * * /opt/ivf/scripts/backup-to-s3.sh
# ═══════════════════════════════════════════════════════════

set -euo pipefail

# ── Configuration ──
BUCKET="s3://ivf-backups-production"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/tmp/ivf-backup-${DATE}"
LOG_DIR="/var/log/ivf"
LOG_FILE="${LOG_DIR}/backup-s3.log"
HEALTHCHECK_URL="${IVF_HEALTHCHECK_URL:-}"  # Optional: healthchecks.io URL
GPG_PASSPHRASE_FILE="/opt/ivf/secrets/gpg_passphrase.txt"

# ── Helpers ──
mkdir -p "$BACKUP_DIR" "$LOG_DIR"

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

cleanup() {
  rm -rf "$BACKUP_DIR"
  if [ $? -ne 0 ] && [ -n "$HEALTHCHECK_URL" ]; then
    curl -fsS --retry 3 "${HEALTHCHECK_URL}/fail" > /dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

log "═══ Starting IVF backup to S3: ${DATE} ═══"

# ── 1. PostgreSQL Full Backup ──
log "[1/5] PostgreSQL full backup..."

# Tìm container db đang chạy (Swarm naming)
DB_CONTAINER=$(docker ps -q -f name=ivf_db.1 -f status=running)
if [ -z "$DB_CONTAINER" ]; then
  log "ERROR: PostgreSQL container not found!"
  exit 1
fi

docker exec "$DB_CONTAINER" pg_dump -U postgres ivf_db -Fc 2>/dev/null | \
  gzip > "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz"

DUMP_SIZE=$(du -sh "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" | cut -f1)
log "  Database dump: ${DUMP_SIZE}"

# SHA256 checksum
sha256sum "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" > "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz.sha256"

# Upload database backup
aws s3 cp "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" \
  "${BUCKET}/daily/ivf_db_${DATE}.dump.gz" \
  --storage-class STANDARD --sse AES256 --quiet
aws s3 cp "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz.sha256" \
  "${BUCKET}/daily/ivf_db_${DATE}.dump.gz.sha256" --quiet

log "  Uploaded to S3: daily/ivf_db_${DATE}.dump.gz"

# ── 2. WAL Archives ──
log "[2/5] WAL archives sync..."

# Copy WAL from container
docker cp "${DB_CONTAINER}:/var/lib/postgresql/archive/" "${BACKUP_DIR}/wal/" 2>/dev/null || true

if [ -d "${BACKUP_DIR}/wal" ] && [ "$(ls -A "${BACKUP_DIR}/wal" 2>/dev/null)" ]; then
  WAL_COUNT=$(ls "${BACKUP_DIR}/wal" | wc -l)
  aws s3 sync "${BACKUP_DIR}/wal/" "${BUCKET}/wal/" \
    --storage-class STANDARD --sse AES256 --exclude "*.partial" --quiet
  log "  Synced ${WAL_COUNT} WAL segments"
else
  log "  No WAL archives to sync"
fi

# ── 3. MinIO Objects ──
log "[3/5] MinIO objects sync..."

# Lấy MinIO credentials từ Docker secrets
MINIO_CONTAINER=$(docker ps -q -f name=ivf_minio.1 -f status=running)
if [ -n "$MINIO_CONTAINER" ]; then
  MINIO_KEY=$(docker exec "$MINIO_CONTAINER" cat /run/secrets/minio_access_key 2>/dev/null || echo "minioadmin")
  MINIO_SECRET=$(docker exec "$MINIO_CONTAINER" cat /run/secrets/minio_secret_key 2>/dev/null || echo "minioadmin123")

  for BUCKET_NAME in ivf-documents ivf-signed-pdfs ivf-medical-images; do
    log "  Syncing bucket: ${BUCKET_NAME}..."
    docker run --rm --network ivf_ivf-data \
      minio/mc:latest \
      bash -c "
        mc alias set local http://minio:9000 '${MINIO_KEY}' '${MINIO_SECRET}' 2>/dev/null
        mc mirror --overwrite local/${BUCKET_NAME} /tmp/${BUCKET_NAME}/ 2>/dev/null
      " || log "  Warning: failed to sync ${BUCKET_NAME}"

    # Upload to S3
    if [ -d "/tmp/${BUCKET_NAME}" ]; then
      aws s3 sync "/tmp/${BUCKET_NAME}/" "${BUCKET}/minio/${BUCKET_NAME}/" \
        --storage-class STANDARD --sse AES256 --quiet
      rm -rf "/tmp/${BUCKET_NAME}"
    fi
  done
  log "  MinIO sync completed"
else
  log "  Warning: MinIO container not running, skipping"
fi

# ── 4. Config & Secrets (encrypted) ──
log "[4/5] Config backup (GPG encrypted)..."

cd /opt/ivf

# Config files (non-secret)
tar czf "${BACKUP_DIR}/config_${DATE}.tar.gz" \
  stack.yml \
  docker/caddy/Caddyfile \
  docker/postgres/*.sh \
  src/IVF.API/appsettings.Production.json \
  2>/dev/null || true

aws s3 cp "${BACKUP_DIR}/config_${DATE}.tar.gz" \
  "${BUCKET}/config/config_${DATE}.tar.gz" --quiet

# Secrets (GPG encrypted before upload)
if [ -f "$GPG_PASSPHRASE_FILE" ]; then
  tar czf - secrets/ 2>/dev/null | \
    gpg --symmetric --cipher-algo AES256 \
      --batch --passphrase-file "$GPG_PASSPHRASE_FILE" \
      --output "${BACKUP_DIR}/secrets_${DATE}.tar.gz.gpg"

  aws s3 cp "${BACKUP_DIR}/secrets_${DATE}.tar.gz.gpg" \
    "${BUCKET}/config/secrets_${DATE}.tar.gz.gpg" --quiet
  log "  Secrets backed up (GPG encrypted)"
else
  log "  Warning: GPG passphrase not found, skipping secrets backup"
fi

# ── 5. PKI Backup (EJBCA + SignServer volumes) ──
log "[5/5] PKI volumes backup..."

EJBCA_CONTAINER=$(docker ps -q -f name=ivf_ejbca.1 -f status=running)
if [ -n "$EJBCA_CONTAINER" ]; then
  docker cp "${EJBCA_CONTAINER}:/opt/keyfactor/ejbca-ce" "${BACKUP_DIR}/ejbca-persistent/" 2>/dev/null || true
  if [ -d "${BACKUP_DIR}/ejbca-persistent" ]; then
    tar czf "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" -C "${BACKUP_DIR}" ejbca-persistent/
    aws s3 cp "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" \
      "${BUCKET}/pki/pki_ejbca_${DATE}.tar.gz" --sse AES256 --quiet
    log "  EJBCA volume backed up"
  fi
fi

SIGNSRV_CONTAINER=$(docker ps -q -f name=ivf_signserver.1 -f status=running)
if [ -n "$SIGNSRV_CONTAINER" ]; then
  docker cp "${SIGNSRV_CONTAINER}:/opt/keyfactor/signserver-ce" "${BACKUP_DIR}/signserver-persistent/" 2>/dev/null || true
  if [ -d "${BACKUP_DIR}/signserver-persistent" ]; then
    tar czf "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" -C "${BACKUP_DIR}" signserver-persistent/
    aws s3 cp "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" \
      "${BUCKET}/pki/pki_signserver_${DATE}.tar.gz" --sse AES256 --quiet
    log "  SignServer volume backed up"
  fi
fi

# ── Summary ──
TOTAL_SIZE=$(aws s3 ls "${BUCKET}/daily/" --recursive --summarize | tail -1)
log "═══ Backup completed successfully ═══"
log "  S3 bucket: ${BUCKET}"
log "  ${TOTAL_SIZE}"

# ── Healthcheck ping ──
if [ -n "$HEALTHCHECK_URL" ]; then
  curl -fsS --retry 3 "$HEALTHCHECK_URL" > /dev/null 2>&1 || true
fi

BACKUP_SCRIPT

chmod +x /opt/ivf/scripts/backup-to-s3.sh
```

### 14.2 Script WAL sync: sync-wal-s3.sh

```bash
cat > /opt/ivf/scripts/sync-wal-s3.sh << 'WAL_SCRIPT'
#!/bin/bash
# ═══════════════════════════════════════════════════════════
#  IVF — WAL Archive Sync to S3 (every 15 minutes)
#  Chạy: */15 * * * * /opt/ivf/scripts/sync-wal-s3.sh
# ═══════════════════════════════════════════════════════════

set -euo pipefail

BUCKET="s3://ivf-backups-production"
TEMP_DIR="/tmp/ivf-wal-sync"
LOG_FILE="/var/log/ivf/wal-s3.log"

mkdir -p "$TEMP_DIR" "$(dirname $LOG_FILE)"

DB_CONTAINER=$(docker ps -q -f name=ivf_db.1 -f status=running)
if [ -z "$DB_CONTAINER" ]; then
  exit 0  # Silently skip if DB not running
fi

# Copy new WAL files
docker cp "${DB_CONTAINER}:/var/lib/postgresql/archive/" "${TEMP_DIR}/" 2>/dev/null || exit 0

if [ "$(ls -A "${TEMP_DIR}" 2>/dev/null)" ]; then
  SYNCED=$(aws s3 sync "${TEMP_DIR}/" "${BUCKET}/wal/" \
    --storage-class STANDARD --sse AES256 \
    --exclude "*.partial" --quiet 2>&1 | wc -l)

  if [ "$SYNCED" -gt 0 ]; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Synced WAL segments" >> "$LOG_FILE"
  fi
fi

rm -rf "$TEMP_DIR"

WAL_SCRIPT

chmod +x /opt/ivf/scripts/sync-wal-s3.sh
```

### 14.3 Cài crontab

```bash
# Trên VPS 1
sudo crontab -u deploy -e

# Thêm các dòng sau:
# ═══ IVF Backup Schedule ═══
# Daily full backup at 3 AM
0 3 * * * /opt/ivf/scripts/backup-to-s3.sh >> /var/log/ivf/backup-s3.log 2>&1

# WAL sync every 15 minutes
*/15 * * * * /opt/ivf/scripts/sync-wal-s3.sh >> /var/log/ivf/wal-s3.log 2>&1

# Log rotation weekly
0 0 * * 0 find /var/log/ivf/ -name "*.log" -mtime +30 -delete
```

### 14.4 Test backup

```bash
# Chạy backup thủ công lần đầu
/opt/ivf/scripts/backup-to-s3.sh

# Verify trên S3
aws s3 ls s3://ivf-backups-production/ --recursive --human-readable
# daily/ivf_db_20260306_030000.dump.gz        4.2 GiB
# daily/ivf_db_20260306_030000.dump.gz.sha256  100 Bytes
# wal/000000010000000000000001                 16.0 MiB
# config/config_20260306_030000.tar.gz         50.0 KiB
# config/secrets_20260306_030000.tar.gz.gpg    10.0 KiB
```

---

## 15. Restore từ S3

### 15.1 Restore Database (Full)

```bash
#!/bin/bash
# Sử dụng: ./restore-db.sh [YYYYMMDD]
# Ví dụ:   ./restore-db.sh 20260305

DATE=${1:-$(date +%Y%m%d)}

echo "=== Restoring database from S3 backup: ${DATE} ==="

# 1. Download
BACKUP_FILE=$(aws s3 ls s3://ivf-backups-production/daily/ | grep "ivf_db_${DATE}" | grep -v sha256 | sort | tail -1 | awk '{print $4}')
aws s3 cp "s3://ivf-backups-production/daily/${BACKUP_FILE}" /tmp/restore.dump.gz

# 2. Verify checksum
aws s3 cp "s3://ivf-backups-production/daily/${BACKUP_FILE}.sha256" /tmp/restore.sha256
cd /tmp && sha256sum -c restore.sha256
echo "Checksum verified ✓"

# 3. Stop API (giữ DB chạy)
docker service scale ivf_api=0
echo "API stopped"
sleep 5

# 4. Restore
gunzip /tmp/restore.dump.gz
DB_CONTAINER=$(docker ps -q -f name=ivf_db.1)
docker exec -i "$DB_CONTAINER" pg_restore -U postgres -d ivf_db --clean --if-exists < /tmp/restore.dump
echo "Database restored"

# 5. Restart API
docker service scale ivf_api=2
echo "API restarted with 2 replicas"

# Cleanup
rm -f /tmp/restore.dump /tmp/restore.sha256 /tmp/restore.dump.gz

echo "=== Restore completed ==="
```

### 15.2 Point-in-Time Recovery (PITR)

```bash
# Restore đến thời điểm cụ thể
# Ví dụ: dữ liệu bị xóa nhầm lúc 14:30, restore đến 14:29

# 1. Download base backup gần nhất trước thời điểm đó
aws s3 cp "s3://ivf-backups-production/daily/ivf_db_20260305*.dump.gz" /tmp/

# 2. Download tất cả WAL segments sau base backup
aws s3 sync "s3://ivf-backups-production/wal/" /tmp/wal-restore/

# 3. Stop API + DB
docker service scale ivf_api=0
docker service scale ivf_db=0

# 4. Remove old data + restore base backup
# (Cần volume access — thực hiện trực tiếp trên VPS 1)
docker run --rm -v ivf_postgres_data:/data -v /tmp:/restore postgres:16-alpine \
  bash -c "
    rm -rf /data/*
    pg_restore -U postgres -d ivf_db --clean /restore/ivf_db_20260305*.dump
  "

# 5. Copy WAL files + create recovery.conf
docker run --rm -v ivf_postgres_data:/data -v /tmp/wal-restore:/wal postgres:16-alpine \
  bash -c "
    cp /wal/* /data/pg_wal/
    echo \"restore_command = 'cp /var/lib/postgresql/archive/%f %p'\" >> /data/postgresql.auto.conf
    echo \"recovery_target_time = '2026-03-05 14:29:00+07'\" >> /data/postgresql.auto.conf
    echo \"recovery_target_action = 'promote'\" >> /data/postgresql.auto.conf
    touch /data/recovery.signal
  "

# 6. Start DB (sẽ replay WAL đến target time)
docker service scale ivf_db=1

# 7. Start API
docker service scale ivf_api=2
```

### 15.3 Restore MinIO Files

```bash
# Download từ S3 và restore vào MinIO
for BUCKET_NAME in ivf-documents ivf-signed-pdfs ivf-medical-images; do
  aws s3 sync "s3://ivf-backups-production/minio/${BUCKET_NAME}/" "/tmp/${BUCKET_NAME}/"

  MINIO_CONTAINER=$(docker ps -q -f name=ivf_minio.1)
  docker run --rm --network ivf_ivf-data -v "/tmp/${BUCKET_NAME}:/restore" \
    minio/mc:latest bash -c "
      mc alias set local http://minio:9000 \$(cat /run/secrets/minio_access_key) \$(cat /run/secrets/minio_secret_key)
      mc mirror /restore/ local/${BUCKET_NAME}/
    "
done
```

### 15.4 Restore Secrets (GPG encrypted)

```bash
aws s3 cp "s3://ivf-backups-production/config/secrets_YYYYMMDD*.tar.gz.gpg" /tmp/secrets.tar.gz.gpg

gpg --decrypt --batch \
  --passphrase-file /opt/ivf/secrets/gpg_passphrase.txt \
  /tmp/secrets.tar.gz.gpg | tar xzf - -C /opt/ivf/
```

### 15.5 Full Disaster Recovery (mất cả 2 VPS)

```
Scenario: Cả VPS 1 và VPS 2 đồng thời down (datacenter fire)

Thời gian phục hồi ước tính: 2-4 giờ

Bước 1: Mua 2 VPS mới (10 phút)
Bước 2: Chuẩn bị VPS — Section 3 (30 phút)
Bước 3: Setup Swarm — Section 4 (10 phút)
Bước 4: Restore secrets từ S3 — Section 14.4 (5 phút)
Bước 5: Deploy stack (không có data) — Section 8 (15 phút)
Bước 6: Restore database từ S3 — Section 14.1 (30-60 phút tùy size)
Bước 7: Restore MinIO từ S3 — Section 14.3 (30-60 phút tùy size)
Bước 8: Restore PKI từ S3 — Download pki/ folder (10 phút)
Bước 9: Update DNS → IP mới (5 phút, propagation ~5-30 phút)
Bước 10: Verify — Section 18 checklist (15 phút)

RPO (data loss): Tối đa 15 phút (WAL sync interval)
RTO (downtime):  2-4 giờ (từ khi bắt đầu restore)
```

---

## 16. Vận hành hàng ngày (Operations)

### 16.1 Deploy code mới (Zero-downtime)

> **v2.0:** Deploy tự động qua CI/CD. Push code lên `main` → GitHub Actions build + push images → SSH deploy lên VPS.

**Cách 1: Tự động (CI/CD — khuyến nghị)**

```bash
# Chỉ cần push code lên main
git push origin main

# GitHub Actions sẽ tự động:
# 1. Build + test backend (.NET 10)
# 2. Build frontend (Angular 21)
# 3. Push images lên GHCR (ghcr.io/hung6066/ivf:latest, ghcr.io/hung6066/ivf-client:latest)
# 4. SSH vào VPS → backup DB → rolling update services

# Theo dõi: GitHub repo → Actions tab
```

**Cách 2: Thủ công (fallback)**

```bash
ssh root@45.134.226.56

# Pull images mới từ GHCR
docker pull ghcr.io/hung6066/ivf:latest
docker pull ghcr.io/hung6066/ivf-client:latest

# Rolling update (ZERO DOWNTIME)
docker service update --image ghcr.io/hung6066/ivf:latest ivf_api
docker service update --image ghcr.io/hung6066/ivf-client:latest ivf_frontend

# Verify
docker service ps ivf_api
docker service ps ivf_frontend
docker service logs ivf_api --tail=10
```

### 16.2 Rollback nếu lỗi

```bash
# Rollback về version trước (1 lệnh, ~10 giây)
docker service rollback ivf_api
docker service rollback ivf_frontend

# Verify
docker service ps ivf_api
```

### 16.3 Scale API

```bash
# Tăng lên 2 replicas (cần VPS 2 có image)
docker service scale ivf_api=2

# Scale xuống 0 (maintenance mode)
docker service scale ivf_api=0

# Trở lại bình thường
docker service scale ivf_api=1
```

### 16.4 Bảo trì VPS (Node drain)

```bash
# Bảo trì VPS 2 (ví dụ: upgrade kernel)
docker node update --availability drain <VPS2_NODE_ID>
# → Swarm tự move containers (nếu có) sang VPS 1

# ─── Thực hiện bảo trì VPS 2 ───
ssh root@194.163.181.19
sudo apt update && sudo apt upgrade -y
sudo reboot

# ─── Sau khi VPS 2 online lại ───
# Trên VPS 1:
docker node update --availability active <VPS2_NODE_ID>
```

### 16.5 Xem logs

```bash
# API logs (tất cả replicas)
docker service logs -f ivf_api --tail=50

# Frontend logs
docker service logs -f ivf_frontend --tail=20

# Database logs
docker service logs -f ivf_db --tail=20

# EJBCA logs
docker service logs -f ivf_ejbca --tail=20

# Specific container log (1 replica)
docker logs -f $(docker ps -q -f name=ivf_api.1) --tail=50

# Caddy logs
docker service logs -f ivf_caddy --tail=20
```

### 16.6 Database migrations

```bash
# Chạy EF Core migration
DB_CONTAINER=$(docker ps -q -f name=ivf_db.1)

# Option 1: Từ API container (auto-migration on startup)
docker service update --force ivf_api
# → API sẽ chạy DatabaseSeeder.SeedAsync() on startup

# Option 2: Manual migration
# Build migration bundle
dotnet ef migrations bundle --project src/IVF.Infrastructure --startup-project src/IVF.API
# Copy + run trên server
```

### 16.7 Update Secrets

```bash
# Secrets trong Swarm KHÔNG thể update — phải recreate

# 1. Tạo secret mới
echo "new-password" > /tmp/new_password.txt
docker secret create ivf_db_password_v2 /tmp/new_password.txt
rm /tmp/new_password.txt

# 2. Update service để dùng secret mới
docker service update \
  --secret-rm ivf_db_password \
  --secret-add source=ivf_db_password_v2,target=ivf_db_password \
  ivf_api

# 3. Xóa secret cũ
docker secret rm ivf_db_password
```

---

## 17. Monitoring & Alerting

### 17.1 Healthcheck endpoints

```bash
# API health
curl -s https://ivf.clinic/health/live
# {"status":"Healthy"}

curl -s https://ivf.clinic/health/ready
# {"status":"Healthy","checks":{"database":"Healthy","redis":"Healthy","minio":"Healthy"}}
```

### 17.2 Docker Swarm monitoring

```bash
# ─── Script: /opt/ivf/scripts/health-check.sh ───
#!/bin/bash
# Chạy mỗi 5 phút qua cron

ALERT_EMAIL="admin@ivf.clinic"
ALERT_WEBHOOK="${IVF_ALERT_WEBHOOK:-}"

check_service() {
  local service=$1
  local replicas=$(docker service ls --filter "name=${service}" --format "{{.Replicas}}")
  local desired=$(echo $replicas | cut -d'/' -f2)
  local running=$(echo $replicas | cut -d'/' -f1)

  if [ "$running" != "$desired" ]; then
    echo "[ALERT] Service ${service}: ${running}/${desired} replicas running"
    if [ -n "$ALERT_WEBHOOK" ]; then
      curl -s -X POST "$ALERT_WEBHOOK" \
        -H "Content-Type: application/json" \
        -d "{\"text\":\"⚠️ IVF Alert: ${service} has ${running}/${desired} replicas\"}"
    fi
  fi
}

# Check all services
for svc in ivf_api ivf_db ivf_redis ivf_minio ivf_ejbca ivf_signserver ivf_caddy; do
  check_service "$svc"
done

# Check disk usage
DISK_USAGE=$(df -h / | tail -1 | awk '{print $5}' | tr -d '%')
if [ "$DISK_USAGE" -gt 85 ]; then
  echo "[ALERT] Disk usage: ${DISK_USAGE}%"
fi

# Check replication lag
DB_CONTAINER=$(docker ps -q -f name=ivf_db.1)
if [ -n "$DB_CONTAINER" ]; then
  LAG=$(docker exec "$DB_CONTAINER" psql -U postgres -tAc \
    "SELECT COALESCE(pg_size_pretty(pg_wal_lsn_diff(sent_lsn, replay_lsn)), 'N/A') FROM pg_stat_replication LIMIT 1" 2>/dev/null)
  if [ "$LAG" != "0 bytes" ] && [ -n "$LAG" ] && [ "$LAG" != "N/A" ]; then
    echo "[ALERT] Replication lag: ${LAG}"
  fi
fi

# Check S3 backup age
LATEST_BACKUP=$(aws s3 ls s3://ivf-backups-production/daily/ | sort | tail -1 | awk '{print $1}')
if [ -n "$LATEST_BACKUP" ]; then
  BACKUP_EPOCH=$(date -d "$LATEST_BACKUP" +%s 2>/dev/null || echo 0)
  CURRENT_EPOCH=$(date +%s)
  AGE_HOURS=$(( (CURRENT_EPOCH - BACKUP_EPOCH) / 3600 ))
  if [ "$AGE_HOURS" -gt 26 ]; then
    echo "[ALERT] Latest S3 backup is ${AGE_HOURS} hours old (should be <24h)"
  fi
fi
```

### 17.3 Crontab monitoring

```bash
# Thêm vào crontab (VPS 1)
*/5 * * * * /opt/ivf/scripts/health-check.sh >> /var/log/ivf/health-check.log 2>&1
```

### 17.4 Optional: Prometheus + Grafana

```bash
# Thêm services vào stack.yml (optional, thêm ~500 MB RAM)

# prometheus:
#   image: prom/prometheus:latest
#   volumes:
#     - prometheus_data:/prometheus
#     - ./docker/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml
#   deploy:
#     placement:
#       constraints:
#         - node.labels.role == primary

# grafana:
#   image: grafana/grafana:latest
#   volumes:
#     - grafana_data:/var/lib/grafana
#   deploy:
#     placement:
#       constraints:
#         - node.labels.role == primary
```

---

## 18. Xử lý Sự cố (Troubleshooting)

### 18.1 Service không start

```bash
# Xem lỗi chi tiết
docker service ps <SERVICE_NAME> --no-trunc --format "{{.Error}}"

# Lỗi thường gặp:
# "no suitable node" → Kiểm tra placement constraints + node labels
# "image not found"  → Image chưa được pull từ GHCR (cần docker login ghcr.io)
# "port already in use" → Có process khác chiếm port
```

### 18.2 API crash — exit code 139 (DigitalSigning)

> **Lỗi đã gặp thực tế.** API crash ngay khi start với exit code 139.

```bash
# Xem logs
docker service logs ivf_api --since 10m

# Nếu thấy lỗi:
# System.InvalidOperationException: Client certificate not found: /app/certs/api-client.p12

# Nguyên nhân: ASPNETCORE_ENVIRONMENT=Production + DigitalSigning__Enabled=true
#   → ValidateProduction() yêu cầu file /app/certs/api-client.p12
#   → File không tồn tại → crash

# Fix: Tắt DigitalSigning cho đến khi EJBCA client cert được provisioned
# Trong docker-compose.stack.yml, API service:
#   - DigitalSigning__Enabled=false
```

### 18.3 VPS 1 (Manager) down

```bash
# ⚠️ Swarm manager down = không thể quản lý cluster

# Cách 1: Chờ VPS 1 recovery
# → Containers trên VPS 2 VẪN CHẠY (worker tự trị)
# → Không thể deploy/scale/update

# Cách 2: Promote VPS 2 thành manager mới
ssh root@194.163.181.19
docker swarm init --force-new-cluster --advertise-addr 194.163.181.19

# → VPS 2 trở thành manager duy nhất
# → Sau khi VPS 1 phục hồi: join lại cluster
```

### 18.4 PostgreSQL Replication broken

```bash
# Kiểm tra status
docker exec $(docker ps -q -f name=ivf_db.1) \
  psql -U postgres -c "SELECT * FROM pg_stat_replication;"

# Nếu standby không kết nối:
# 1. Xóa standby data + recreate
docker service scale ivf_db-standby=0
docker volume rm ivf_postgres_standby
docker service scale ivf_db-standby=1
# → standby-entrypoint.sh sẽ pg_basebackup lại từ đầu
```

### 18.5 Disk đầy

```bash
# Xem disk usage
df -h /
docker system df

# Cleanup
docker system prune -af --volumes  # ⚠️ XÓA unused images + volumes
# Hoặc chỉ images:
docker image prune -af
# Hoặc chỉ build cache:
docker builder prune -af

# Xóa old backup logs
find /var/log/ivf/ -name "*.log" -mtime +30 -delete
```

### 18.6 SSL/TLS certificate issues

```bash
# Xem Caddy logs
docker service logs ivf_caddy --tail=50

# Force Caddy reload config
docker exec $(docker ps -q -f name=ivf_caddy) caddy reload --config /etc/caddy/Caddyfile

# Xem certificates
docker exec $(docker ps -q -f name=ivf_caddy) caddy list-certificates
```

### 18.7 API crash loop (khác)

```bash
# Xem crash reason
docker service logs ivf_api --since 10m

# Known issues:
# "Connection refused: db:5432" → DB chưa ready, API sẽ tự retry
# "Redis connection failed"     → Redis down, API chạy với fallback (no cache)
# "Migration failed"            → DB schema mismatch, cần chạy migration
# "Certificate expired"         → Renew mTLS cert (scripts/init-mtls.sh)
```

### 18.8 live-restore incompatible with Swarm

```bash
# Nếu Docker daemon không start hoặc Swarm lỗi:
# Kiểm tra /etc/docker/daemon.json KHÔNG có "live-restore": true
# Docker Swarm không tương thích với live-restore

cat /etc/docker/daemon.json
# Nếu thấy "live-restore": true → xóa dòng đó → restart docker
sudo systemctl restart docker
```

### 18.9 GHCR image pull failed

```bash
# Kiểm tra GHCR authentication
docker login ghcr.io -u hung6066

# Kiểm tra image tồn tại
docker pull ghcr.io/hung6066/ivf:latest

# Nếu "denied: permission denied":
# → GHCR_TOKEN cần quyền packages:read
# → Token có thể hết hạn, cần tạo mới

# Force update service với image mới:
docker service update --force --with-registry-auth --image ghcr.io/hung6066/ivf:latest ivf_api
```

### 18.10 IP Whitelist blocks all requests (IP_NOT_WHITELISTED)

> **Lỗi đã gặp thực tế.** Sau khi bật IP whitelist, tất cả API requests bị block với 403 `IP_NOT_WHITELISTED`.

```bash
# Nguyên nhân: Docker Swarm ingress NAT thay đổi client IP → API thấy 10.0.0.x
# thay vì real IP. SecurityEnforcementMiddleware so sánh IP nội bộ với whitelist → block.

# Fix 1 (đã áp dụng): Caddy dùng mode: host để bypass ingress NAT
# Trong docker-compose.stack.yml → caddy ports:
#   ports:
#     - target: 80
#       published: 80
#       mode: host
#     - target: 443
#       published: 443
#       mode: host

# Fix 2 (tạm thời): Deactivate whitelist trong DB
DB=$(docker ps -q -f name=ivf_db)
docker exec $DB psql -U postgres -d ivf_db -c \
  'UPDATE "IpWhitelistEntries" SET "IsActive" = false WHERE "IsActive" = true;'

# Kiểm tra IP mà API nhận được
docker service logs ivf_api --since 5m 2>&1 | grep -i 'blocked\|whitelist'
# Output nên hiện real IP (115.x.x.x) thay vì Docker IP (10.0.0.x)
```

### 18.11 Infrastructure Monitor 500 — Docker CLI not found

> **Lỗi đã gặp thực tế.** `/api/admin/infrastructure/swarm/*` endpoints trả về 500.
> API logs: `docker service ls failed: No such file or directory`

```bash
# Nguyên nhân: API container không có Docker CLI.
# InfrastructureMonitorService shells out to `docker service ls`, `docker node ls`, etc.

# Fix: 2 thay đổi cần thiết:

# 1. Dockerfile (src/IVF.API/Dockerfile) — cài docker-ce-cli trong runtime stage:
#    RUN apt-get update && apt-get install -y --no-install-recommends docker-ce-cli

# 2. docker-compose.stack.yml — mount Docker socket vào API:
#    api:
#      volumes:
#        - /var/run/docker.sock:/var/run/docker.sock:ro

# Verify sau fix:
API=$(docker ps -q -f name=ivf_api)
docker exec $API docker --version
# → Docker version 29.x.x
docker exec $API docker service ls
# → Hiện danh sách services
```

### 18.12 Docker config immutable — "only updates to Labels are allowed"

> **Lỗi đã gặp thực tế.** `docker stack deploy` fail khi thay đổi nội dung Caddyfile.

```bash
# Nguyên nhân: Docker configs are immutable — không thể update nội dung.
# Khi đổi `:80` → `natra.site` trong Caddyfile, deploy sẽ fail.

# Fix: Version config name (caddyfile → caddyfile_v2 → caddyfile_v3, ...)
# Trong docker-compose.stack.yml:
#   configs:
#     caddyfile_v2:       # ← đổi tên mỗi khi thay đổi nội dung
#       file: ./Caddyfile
#   caddy:
#     configs:
#       - source: caddyfile_v2  # ← phải match
#         target: /etc/caddy/Caddyfile

# Sau khi deploy thành công, xóa config cũ:
docker config rm caddyfile
```

---

## 19. Checklist Triển khai

### 19.1 Pre-deployment

```
□ 2 VPS Contabo đã mua, SSH hoạt động
□ Domain đã đăng ký (ivf.clinic)
□ Cloudflare đã setup, DNS records đã tạo
□ AWS account đã tạo, MFA bật, IAM user ready
□ GitHub repo access ready (SSH deploy key)
□ GHCR token tạo (packages:read scope)
□ SSL wildcard certificate strategy decided (Caddy auto)
```

### 19.2 CI/CD Pipeline (v2.0)

```
□ GitHub Actions workflows configured (ci-cd.yml, deploy-production.yml)
□ GitHub Secrets đã set (SSH_PRIVATE_KEY, SSH_HOST_MANAGER, GHCR_TOKEN)
□ SSH deploy key (ed25519) thêm vào VPS authorized_keys
□ GHCR images build + push thành công
  □ ghcr.io/hung6066/ivf:latest (API)
  □ ghcr.io/hung6066/ivf-client:latest (Frontend)
□ Auto-deploy khi push main hoạt động
□ Discord webhook notifications (tùy chọn)
```

### 19.3 VPS Setup

```
□ Ubuntu 24.04 đã cài, đã update
□ User deploy đã tạo, SSH key auth
□ Root login disabled, password auth disabled
□ UFW firewall configured (22, 80, 443, Swarm ports)
□ Fail2ban configured
□ Docker CE installed trên CẢ 2 VPS
□ Docker daemon.json configured (log rotation, overlay2, KHÔNG có live-restore)
□ AWS CLI installed trên VPS 1
□ Timezone set: Asia/Ho_Chi_Minh
□ GHCR logged in trên các VPS (docker login ghcr.io)
```

### 19.4 Docker Swarm

```
□ Swarm init trên VPS 1 (Manager)
□ VPS 2 joined as Worker
□ Node labels applied (role=primary, role=standby)
□ docker node ls shows 2 Ready nodes
```

### 19.5 Ansible (Tùy chọn — v2.0)

```
□ Ansible installed trên máy local / WSL2
□ hosts.yml created từ hosts.example.yml
□ Test SSH connectivity: ansible -i hosts.yml all -m ping
□ Dry run thành công: ansible-playbook -i hosts.yml site.yml --check
□ Full run thành công (nếu setup từ zero)
```

### 19.6 Application

```
□ docker-compose.stack.yml verified
□ Caddyfile verified (repo root)
□ Secrets tạo trong Swarm raft store (10 secrets)
□ DigitalSigning__Enabled=false (cho đến khi có EJBCA client cert)
```

### 19.7 Deploy & Verify

```
□ docker stack deploy -c docker-compose.stack.yml ivf
□ Tất cả services Running (docker service ls)
□ API: 1/1 — ghcr.io/hung6066/ivf:latest
□ Frontend: 1/1 — ghcr.io/hung6066/ivf-client:latest
□ Caddy: 1/1 — routing /api/* → API, /* → Frontend
□ DB health check pass (pg_isready)
□ Redis health check pass
□ MinIO buckets created (3 buckets)
□ EJBCA healthy (8443)
□ SignServer: 0/1 accepted (cần PKI setup)
□ Web accessible tại http://<VPS1_IP>/ (Angular app loads)
□ API accessible tại http://<VPS1_IP>/api/* (returns JSON)
```

### 19.8 Replication & HA (tương lai)

```
□ PostgreSQL replication: streaming, lag = 0 bytes
□ Replication slot created (standby_slot)
□ WAL archiving active
□ Standby pg_is_in_recovery() = true
□ API replicas: 2 (1 per VPS)
```

### 19.9 SSL & Domain

```
□ https://ivf.clinic accessible
□ https://*.ivf.clinic auto-cert working
□ On-Demand TLS working for custom domains
□ HSTS header present
□ Security headers verified (CSP, X-Frame-Options, etc.)
```

### 19.10 Backup & S3

```
□ AWS S3 bucket created (ap-southeast-1)
□ Bucket versioning enabled
□ Bucket encryption enabled (AES-256)
□ Public access blocked
□ Lifecycle policy applied (Standard → IA → Glacier)
□ IAM user created (least privilege)
□ AWS CLI configured trên VPS 1
□ backup-to-s3.sh executable + tested
□ sync-wal-s3.sh executable + tested
□ Crontab entries added (3 AM daily + 15-min WAL)
□ First backup verified on S3
□ Restore procedure tested (!!!)
```

### 19.11 Monitoring

```
□ health-check.sh configured
□ Crontab health check every 5 minutes
□ Alert webhook configured (Telegram/Slack/Email)
□ Backup age monitoring active
□ Disk usage monitoring active
□ Replication lag monitoring active
```

### 19.12 Security Final

```
□ Root login disabled trên cả 2 VPS
□ UFW active, only required ports open
□ Fail2ban active
□ Docker secrets in use (not env vars)
□ PKI mTLS configured (SignServer) — khi ready
□ DB ports NOT exposed externally
□ MinIO console: localhost only (127.0.0.1:9001)
□ Backup secrets GPG encrypted before S3 upload
□ S3 bucket: no public access
□ GHCR_TOKEN rotated định kỳ
```

---

## Appendix A: Thứ tự thực hiện tổng hợp

```
Cách 1: Ansible (khuyến nghị, ~30 phút)
  └─ ansible-playbook -i hosts.yml site.yml --extra-vars "ghcr_token=ghp_xxx"
     ├─ Phase 1: common (OS, SSH, firewall, fail2ban)
     ├─ Phase 2: docker (Docker CE, Swarm init, node labels, GHCR login)
     └─ Phase 3: app (secrets, pull images, deploy stack)

Cách 2: Thủ công (~2-3 ngày)
  Ngày 1 (3-4 giờ):
    ├─ Mua 2 VPS Contabo
    ├─ Chuẩn bị VPS (Section 3) — 45 phút
    ├─ Setup Docker Swarm (Section 4) — 15 phút
    ├─ Clone code + tạo secrets (Section 5) — 30 phút
    ├─ Setup CI/CD + GHCR (Section 6) — 30 phút
    ├─ Copy stack file + Caddyfile (Section 7) — 15 phút
    └─ Deploy stack (Section 8) — 15 phút

  Ngày 2 (2-3 giờ):
    ├─ Setup PKI nếu cần (Section 11) — 30-60 phút
    ├─ DNS + SSL (Section 12) — 30 phút
    ├─ Setup AWS S3 (Section 13) — 30 phút
    ├─ Backup scripts (Section 14) — 30 phút
    ├─ Test restore (Section 15) — 30 phút
    └─ Monitoring setup (Section 17) — 30 phút

  Ngày 3 (1 giờ):
    ├─ Chạy Checklist (Section 19) — 30 phút
    ├─ Stress test
    └─ Sign-off
```

## Appendix B: Quick Reference Commands

```bash
# ═══ Deploy ═══
docker stack deploy -c docker-compose.stack.yml ivf  # Deploy/update stack
docker service update --image ghcr.io/hung6066/ivf:latest ivf_api  # Rolling update API
docker service update --image ghcr.io/hung6066/ivf-client:latest ivf_frontend  # Rolling update Frontend
docker service rollback ivf_api             # Rollback

# ═══ Monitor ═══
docker service ls                            # List services
docker service ps ivf_api                    # API replica detail
docker service ps ivf_frontend               # Frontend replica detail
docker service logs -f ivf_api --tail=50    # Live logs
docker node ls                               # Cluster nodes

# ═══ Scale ═══
docker service scale ivf_api=3              # Scale up
docker service scale ivf_api=0              # Maintenance mode

# ═══ Maintenance ═══
docker node update --availability drain X   # Drain node
docker node update --availability active X  # Activate node
docker service update --force ivf_api       # Force rebalance

# ═══ Backup ═══
/opt/ivf/scripts/backup-to-s3.sh            # Manual backup
aws s3 ls s3://ivf-backups-production/      # List backups

# ═══ Debug ═══
docker service ps X --no-trunc              # Task errors
docker exec -it $(docker ps -q -f name=ivf_db.1) psql -U postgres
docker exec -it $(docker ps -q -f name=ivf_api.1) sh
```

---

_Tài liệu triển khai IVF Platform — Docker Swarm + AWS S3_
_Phiên bản: 2.0 | Ngày: 2026-03-07_
