# IVF Platform — Hướng dẫn Triển khai & Vận hành Production

## Mục lục

1. [Tổng quan Kiến trúc Triển khai](#1-tổng-quan-kiến-trúc-triển-khai)
2. [Yêu cầu Hạ tầng](#2-yêu-cầu-hạ-tầng)
3. [Giải pháp Hạ tầng & Phân tích Chi phí](#3-giải-pháp-hạ-tầng--phân-tích-chi-phí)
4. [Orchestration: Docker Compose vs Docker Swarm vs K8s](#4-orchestration-docker-compose-vs-docker-swarm-vs-k8s)
5. [Bảo vệ Dữ liệu: AWS S3 & Chiến lược 3-2-1](#5-bảo-vệ-dữ-liệu-aws-s3--chiến-lược-3-2-1)
6. [Triển khai từng bước](#6-triển-khai-từng-bước)
7. [Tối ưu Hiệu suất](#7-tối-ưu-hiệu-suất)
8. [Observability & Truy vết Lỗi](#8-observability--truy-vết-lỗi)
9. [Khả năng Chịu lỗi & Phục hồi](#9-khả-năng-chịu-lỗi--phục-hồi)
10. [Kiến trúc SLA 99.99%](#10-kiến-trúc-sla-9999)
11. [Xử lý Sự cố (Incident Response)](#11-xử-lý-sự-cố-incident-response)
12. [Bảo trì Định kỳ](#12-bảo-trì-định-kỳ)
13. [Checklist Triển khai](#13-checklist-triển-khai)

> **Xem thêm**: [Hướng dẫn Vận hành Hạ tầng Enterprise](infrastructure_operations_guide.md) — Monitoring stack, data retention, read-replica, auto-healing, DR scripts, Admin UI.

---

## 1. Tổng quan Kiến trúc Triển khai

### 1.1 Kiến trúc Production SLA 99.99%

```
                        ┌──────────────────────────────┐
                        │        Cloudflare CDN         │
                        │  DDoS Protection + WAF        │
                        │  Static Assets (Angular SPA)  │
                        └──────────────┬───────────────┘
                                       │
                        ┌──────────────▼───────────────┐
                        │      Load Balancer (L7)       │
                        │  HAProxy / Cloud LB            │
                        │  Health check: /api/health     │
                        │  SSL Termination (optional)   │
                        └──────┬───────────┬───────────┘
                               │           │
                 ┌─────────────▼───┐ ┌─────▼─────────────┐
                 │   Node A (AZ-1)  │ │   Node B (AZ-2)    │
                 │                  │ │                     │
                 │ ┌──────────────┐│ │ ┌──────────────┐   │
                 │ │  Caddy       ││ │ │  Caddy       │   │
                 │ │  Auto HTTPS  ││ │ │  Auto HTTPS  │   │
                 │ └──────┬───────┘│ │ └──────┬───────┘   │
                 │        │        │ │        │           │
                 │ ┌──────▼───────┐│ │ ┌──────▼───────┐   │
                 │ │  API (.NET)  ││ │ │  API (.NET)  │   │
                 │ │  Port 8080   ││ │ │  Port 8080   │   │
                 │ └──────┬───────┘│ │ └──────┬───────┘   │
                 │        │        │ │        │           │
                 │ ┌──────▼───────┐│ │ ┌──────▼───────┐   │
                 │ │  Redis       ││ │ │  Redis       │   │
                 │ │  (Sentinel)  ││ │ │  (Replica)   │   │
                 │ └──────────────┘│ │ └──────────────┘   │
                 └────────┬────────┘ └────────┬───────────┘
                          │                    │
              ┌───────────▼────────────────────▼──────────┐
              │        PostgreSQL Cluster                   │
              │                                            │
              │  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
              │  │ Primary  │→→│ Standby  │→→│  Cloud   │ │
              │  │  (AZ-1)  │  │  (AZ-2)  │  │ Replica  │ │
              │  │  R/W     │  │  R/O     │  │  R/O DR  │ │
              │  └──────────┘  └──────────┘  └──────────┘ │
              └───────────────────────────────────────────┘
              ┌─────────────────────────────────────────────┐
              │  MinIO Cluster (Erasure Coding)              │
              │  Site Replication: Primary ↔ Cloud Replica  │
              └─────────────────────────────────────────────┘
              ┌─────────────────────────────────────────────┐
              │  PKI (EJBCA + SignServer)                    │
              │  Isolated signing network (no internet)     │
              │  SoftHSM2 (PKCS#11) → Future: Cloud HSM    │
              └─────────────────────────────────────────────┘
```

### 1.2 Các thành phần hệ thống

| Thành phần         | Công nghệ          | Vai trò                                          | Port       |
| ------------------ | ------------------ | ------------------------------------------------ | ---------- |
| **API Server**     | .NET 10 (Kestrel)  | REST API, SignalR, business logic                | 8080       |
| **Reverse Proxy**  | Caddy 2            | Auto HTTPS, wildcard SSL, On-Demand TLS          | 80, 443    |
| **Database**       | PostgreSQL 16      | ACID, partitioned audit, WAL replication         | 5432       |
| **Cache**          | Redis 7            | Session, API response cache, rate limit counters | 6379       |
| **Object Storage** | MinIO              | Hình ảnh y tế, PDF, documents (S3-compatible)    | 9000       |
| **PKI**            | EJBCA + SignServer | Chữ ký số, certificate management                | 8443, 9443 |
| **CDN**            | Cloudflare         | Static assets (Angular SPA), DDoS, WAF           | —          |
| **Load Balancer**  | HAProxy / Cloud LB | L7 load balancing, health checks                 | —          |

### 1.3 Network Isolation (3 zones)

```
┌─────────────────────────────────────────────────────────────┐
│  ivf-public (bridge)         ← Internet-facing              │
│  ├─ Caddy (reverse proxy)                                   │
│  ├─ API (.NET)                                              │
│  ├─ MinIO (console localhost-only in production)            │
│  └─ Redis                                                   │
├─────────────────────────────────────────────────────────────┤
│  ivf-signing (bridge, internal: true)  ← No internet       │
│  ├─ API ↔ SignServer ↔ EJBCA                                │
│  └─ mTLS mutual authentication                             │
├─────────────────────────────────────────────────────────────┤
│  ivf-data (bridge, internal: true)     ← No internet       │
│  ├─ PostgreSQL Primary + Standby                            │
│  ├─ EJBCA DB, SignServer DB                                 │
│  ├─ Redis (data path)                                       │
│  └─ MinIO (data path)                                       │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Yêu cầu Hạ tầng

### 2.1 Hardware tối thiểu (Single Node)

| Tài nguyên  | Minimum                    | Recommended      |
| ----------- | -------------------------- | ---------------- |
| **CPU**     | 4 vCPU                     | 8 vCPU           |
| **RAM**     | 16 GB                      | 32 GB            |
| **Disk**    | 100 GB SSD                 | 500 GB NVMe      |
| **Network** | 100 Mbps                   | 1 Gbps           |
| **OS**      | Ubuntu 22.04+ / Debian 12+ | Ubuntu 24.04 LTS |

### 2.2 Recommended: Multi-Node HA (SLA 99.99%)

| Node                  | Specs                        | Vai trò                                  |
| --------------------- | ---------------------------- | ---------------------------------------- |
| **Node A (AZ-1)**     | 8 vCPU, 32GB RAM, 500GB NVMe | API + Caddy + Redis Primary + PG Primary |
| **Node B (AZ-2)**     | 8 vCPU, 32GB RAM, 500GB NVMe | API + Caddy + Redis Replica + PG Standby |
| **Node C (Cloud DR)** | 4 vCPU, 16GB RAM, 200GB SSD  | PG Cloud Replica + MinIO Replica         |
| **Load Balancer**     | Cloud LB (AWS ALB / GCP LB)  | L7 routing, health checks, SSL offload   |

### 2.3 Container Resource Limits

| Container           | CPU       | Memory | Disk         |
| ------------------- | --------- | ------ | ------------ |
| **api**             | 2 cores   | 1 GB   | —            |
| **db (PostgreSQL)** | 2 cores   | 4 GB   | 200 GB       |
| **redis**           | 0.5 cores | 512 MB | —            |
| **minio**           | 1 core    | 1 GB   | 200 GB       |
| **caddy**           | 0.5 cores | 256 MB | 1 GB (certs) |
| **ejbca**           | 1 core    | 2 GB   | 1 GB         |
| **signserver**      | 1 core    | 2 GB   | —            |

### 2.4 Software Requirements

```bash
# Docker + Docker Compose V2
docker --version          # >= 24.0
docker compose version    # >= 2.20

# DNS
# Wildcard DNS: *.ivf.clinic → Load Balancer IP
# hoặc A record cho từng subdomain

# Firewall
# Inbound:  80 (HTTP→HTTPS redirect), 443 (HTTPS)
# Outbound: 5432 (PG replication), 53 (DNS), 443 (ACME, package repos)
```

---

## 3. Giải pháp Hạ tầng & Phân tích Chi phí

### 3.1 Tổng quan 4 phương án triển khai

| #     | Phương án                                  | Phù hợp khi                                 | SLA khả thi  | TCO 3 năm (ước tính) |
| ----- | ------------------------------------------ | ------------------------------------------- | ------------ | -------------------- |
| **A** | Cloud Managed Services (AWS/Azure/GCP)     | Startup, scale nhanh, ít DevOps             | 99.99%       | $$$$                 |
| **B** | Cloud VPS (Hetzner, Contabo, DigitalOcean) | Budget-friendly, self-managed               | 99.9–99.95%  | $$                   |
| **C** | On-Premise (Mua server vật lý)             | Data sovereignty, HIPAA strict, >50 tenants | 99.9–99.99%  | $$$ (trả trước cao)  |
| **D** | Hybrid (VPS + Cloud DR)                    | Cân bằng chi phí & reliability              | 99.95–99.99% | $$–$$$               |

---

### 3.2 Phương án A: Cloud Managed Services

**Phù hợp:** Clinic muốn SLA cao nhất, không cần quản lý infra, chấp nhận chi phí cao.

#### AWS Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        AWS Architecture                             │
│                                                                     │
│  CloudFront (CDN) ─→ ALB (Application Load Balancer)               │
│                       ├─ Target Group A: ECS Fargate (API x2)      │
│                       └─ Target Group B: ECS Fargate (API x2) [AZ2]│
│                                                                     │
│  ┌─ Amazon RDS PostgreSQL 16 ──────────────────────────────┐       │
│  │  db.r6g.xlarge (Multi-AZ)                                │       │
│  │  Primary (AZ-1) ←→ Standby (AZ-2) + Read Replica        │       │
│  │  Automated backups, PITR, encryption at rest             │       │
│  └──────────────────────────────────────────────────────────┘       │
│                                                                     │
│  ┌─ Amazon ElastiCache Redis ──────────────────────────────┐       │
│  │  cache.r6g.large (Multi-AZ with failover)               │       │
│  └──────────────────────────────────────────────────────────┘       │
│                                                                     │
│  ┌─ Amazon S3 ─────────────────────────────────────────────┐       │
│  │  3 buckets + versioning + lifecycle rules               │       │
│  │  Cross-region replication → S3 DR bucket                │       │
│  └──────────────────────────────────────────────────────────┘       │
│                                                                     │
│  ┌─ Fargate (PKI) ─────────────────────────────────────────┐       │
│  │  EJBCA + SignServer (private subnet, no internet)       │       │
│  │  AWS CloudHSM thay thế SoftHSM (FIPS 140-2 Level 3)    │       │
│  └──────────────────────────────────────────────────────────┘       │
│                                                                     │
│  Monitoring: CloudWatch + X-Ray + CloudTrail                       │
│  Secrets: AWS Secrets Manager thay Docker secrets                   │
│  CI/CD: GitHub Actions → ECR → ECS rolling deploy                  │
└─────────────────────────────────────────────────────────────────────┘
```

#### AWS Chi phí hàng tháng (ước tính, region ap-southeast-1)

| Service                  | Spec                               | Chi phí/tháng (USD) |
| ------------------------ | ---------------------------------- | ------------------- |
| **ECS Fargate** (API x2) | 2 vCPU, 4GB RAM × 2 tasks          | ~$150               |
| **RDS PostgreSQL**       | db.r6g.xlarge, Multi-AZ, 200GB gp3 | ~$520               |
| **ElastiCache Redis**    | cache.r6g.large, Multi-AZ          | ~$260               |
| **S3**                   | 100GB + requests                   | ~$5                 |
| **ALB**                  | Application Load Balancer          | ~$25                |
| **CloudFront**           | 100GB transfer/month               | ~$15                |
| **ECR**                  | Container registry                 | ~$5                 |
| **Secrets Manager**      | 10 secrets                         | ~$5                 |
| **CloudWatch**           | Logs, metrics, alarms              | ~$30                |
| **CloudHSM** (optional)  | 1 HSM instance                     | ~$1,500             |
| **Data Transfer**        | 500GB outbound                     | ~$45                |
| **Route 53**             | DNS hosting                        | ~$1                 |
|                          |                                    |                     |
| **Tổng (không HSM)**     |                                    | **~$1,060/tháng**   |
| **Tổng (có HSM)**        |                                    | **~$2,560/tháng**   |

#### Azure tương đương

| Service              | Azure Equivalent                                       | Chi phí/tháng   |
| -------------------- | ------------------------------------------------------ | --------------- |
| ECS Fargate          | **Azure Container Apps** (2 instances)                 | ~$140           |
| RDS PostgreSQL       | **Azure Database for PostgreSQL Flexible** (GP, 4vCPU) | ~$450           |
| ElastiCache          | **Azure Cache for Redis** (C2 Standard)                | ~$200           |
| S3                   | **Azure Blob Storage** (Hot, 100GB)                    | ~$5             |
| ALB                  | **Azure Application Gateway v2**                       | ~$40            |
| CloudFront           | **Azure CDN** (Standard)                               | ~$15            |
| CloudHSM             | **Azure Dedicated HSM**                                | ~$4,500         |
|                      |                                                        |                 |
| **Tổng (không HSM)** |                                                        | **~$900/tháng** |

#### GCP tương đương

| Service              | GCP Equivalent                                   | Chi phí/tháng   |
| -------------------- | ------------------------------------------------ | --------------- |
| ECS Fargate          | **Cloud Run** (2 instances, always-on)           | ~$120           |
| RDS PostgreSQL       | **Cloud SQL PostgreSQL** (db-custom-4-16384, HA) | ~$400           |
| ElastiCache          | **Memorystore Redis** (Standard, 5GB)            | ~$180           |
| S3                   | **Cloud Storage** (Standard, 100GB)              | ~$3             |
| ALB                  | **Cloud Load Balancing** (HTTPS)                 | ~$20            |
| CloudFront           | **Cloud CDN**                                    | ~$12            |
| CloudHSM             | **Cloud HSM**                                    | ~$1,200         |
|                      |                                                  |                 |
| **Tổng (không HSM)** |                                                  | **~$750/tháng** |

**Ưu điểm:**

- ✅ SLA 99.99% từ provider (RDS Multi-AZ: 99.95%, ECS: 99.99%)
- ✅ Auto-scaling, auto-backup, auto-patching
- ✅ Compliance certifications (HIPAA BAA, SOC 2, ISO 27001)
- ✅ Managed monitoring (CloudWatch, X-Ray)
- ✅ Không cần DevOps team cho infra

**Nhược điểm:**

- ❌ Chi phí cao nhất (~$750–$1,060/tháng minimum)
- ❌ Vendor lock-in (migration phức tạp)
- ❌ Chi phí network egress cao khi scale
- ❌ CloudHSM rất đắt (~$1,200–$4,500/tháng)

---

### 3.3 Phương án B: Cloud VPS (Self-Managed)

**Phù hợp:** Budget có hạn, team có kinh nghiệm Docker/Linux, chấp nhận tự quản lý.

#### So sánh VPS Providers

| Provider            | Spec (8vCPU, 32GB, 400GB NVMe) | Chi phí/tháng  | Datacenter                    |
| ------------------- | ------------------------------ | -------------- | ----------------------------- |
| **Hetzner Cloud**   | CPX51                          | **€35 (~$38)** | Germany, Finland, US          |
| **Contabo**         | Cloud VPS XL                   | **€18 (~$20)** | Germany, US, Singapore, Japan |
| **DigitalOcean**    | Premium AMD 8C/32GB            | **$192**       | Singapore, multiple           |
| **Vultr**           | High Performance 8C/32GB       | **$192**       | Singapore, Tokyo, multiple    |
| **Linode (Akamai)** | Dedicated 8C/32GB              | **$192**       | Singapore, Tokyo              |
| **OVHcloud**        | B3-32                          | **€72 (~$78)** | Singapore, France             |

#### Setup 2 VPS (HA) — Hetzner

```
┌──────────────────────────────────────────────────────────────┐
│              Hetzner Cloud — 2 VPS Setup                     │
│                                                              │
│  VPS 1: CPX51 (Nuremberg, €35/mo)                           │
│  ├─ Caddy + API (.NET) + Redis Primary                      │
│  ├─ PostgreSQL Primary                                      │
│  ├─ MinIO Primary                                           │
│  └─ EJBCA + SignServer                                      │
│                                                              │
│  VPS 2: CPX41 (Helsinki, €26/mo)                            │
│  ├─ Caddy + API (.NET) + Redis Replica                      │
│  ├─ PostgreSQL Standby (streaming replication)              │
│  └─ MinIO Replica                                           │
│                                                              │
│  Hetzner Load Balancer: LB11 (€6/mo)                        │
│  ├─ L7 HTTPS, health checks                                │
│  └─ 25 targets, 50 Mbps                                     │
│                                                              │
│  Volumes: 200GB × 2 (€18/mo)                                │
│  Floating IP: 1 (€4/mo)                                     │
│  Snapshot backup: 20% of VPS cost (~€12/mo)                 │
└──────────────────────────────────────────────────────────────┘
```

| Hạng mục                   | Chi phí/tháng           |
| -------------------------- | ----------------------- |
| VPS 1 (CPX51, 8vCPU/32GB)  | €35                     |
| VPS 2 (CPX41, 8vCPU/16GB)  | €26                     |
| Load Balancer (LB11)       | €6                      |
| Volume 200GB × 2           | €18                     |
| Floating IP                | €4                      |
| Snapshot backup            | €12                     |
| Cloudflare (Free/Pro plan) | $0–$20                  |
| **Tổng**                   | **~€101 (~$110/tháng)** |

#### Setup 2 VPS — Contabo (Budget tối đa)

| Hạng mục                               | Chi phí/tháng         |
| -------------------------------------- | --------------------- |
| VPS 1 (Cloud VPS XL, 8vCPU/32GB/400GB) | €18                   |
| VPS 2 (Cloud VPS L, 6vCPU/16GB/200GB)  | €12                   |
| Snapshot                               | €3                    |
| Cloudflare Free                        | $0                    |
| **Tổng**                               | **~€33 (~$36/tháng)** |

> ⚠️ **Lưu ý Contabo:** Performance I/O thấp hơn Hetzner, support chậm, không có managed LB. Phù hợp dev/staging, cẩn thận cho production y tế.

#### Setup VPS — DigitalOcean (Asia-Pacific)

| Hạng mục                                   | Chi phí/tháng   |
| ------------------------------------------ | --------------- |
| Droplet 1 (Premium 8C/32GB, SGP1)          | $192            |
| Droplet 2 (Premium 4C/16GB, SGP1)          | $96             |
| Managed PostgreSQL (4vCPU/8GB, HA standby) | $175            |
| Managed Redis (2GB)                        | $30             |
| Spaces (250GB S3-compatible)               | $5              |
| Load Balancer                              | $12             |
| Cloudflare Pro                             | $20             |
| **Tổng (self-managed DB)**                 | **~$325/tháng** |
| **Tổng (managed DB)**                      | **~$530/tháng** |

**Ưu điểm VPS:**

- ✅ Chi phí rất thấp ($36–$325/tháng)
- ✅ Full control, không vendor lock-in
- ✅ Docker Compose deploy y như development
- ✅ Dễ migrate giữa các providers

**Nhược điểm:**

- ❌ Tự quản lý mọi thứ (backup, patching, monitoring, security)
- ❌ Cần DevOps/SysAdmin kinh nghiệm
- ❌ SLA phụ thuộc vào khả năng vận hành của team
- ❌ Không có compliance certifications sẵn

---

### 3.4 Phương án C: On-Premise (Mua Server Vật lý)

**Phù hợp:** Yêu cầu data sovereignty nghiêm ngặt (dữ liệu y tế không được rời Việt Nam), phòng khám lớn >50 tenants.

#### Hardware Recommendation

| Server                 | Specs                                                | Giá (ước tính VNĐ)     |
| ---------------------- | ---------------------------------------------------- | ---------------------- |
| **Server 1 (Primary)** | Dell PowerEdge R660xs / HPE DL360 Gen11              |                        |
|                        | CPU: Intel Xeon Silver 4416+ (20C/40T)               |                        |
|                        | RAM: 64GB DDR5 ECC (expandable 512GB)                |                        |
|                        | Disk: 2× 960GB NVMe SSD (RAID 1) + 2× 4TB SAS (data) |                        |
|                        | Network: 2× 10GbE                                    |                        |
|                        | iDRAC/iLO: Remote management                         |                        |
|                        | **Giá:**                                             | **~120–180 triệu VNĐ** |
| **Server 2 (Standby)** | Tương tự Server 1 hoặc spec thấp hơn                 |                        |
|                        | CPU: Xeon Silver 4410Y (12C/24T)                     |                        |
|                        | RAM: 32GB DDR5 ECC                                   |                        |
|                        | Disk: 2× 960GB NVMe SSD (RAID 1)                     |                        |
|                        | **Giá:**                                             | **~80–120 triệu VNĐ**  |
| **UPS**                | APC Smart-UPS 3000VA                                 | **~15–25 triệu VNĐ**   |
| **Switch**             | MikroTik CRS326 / Ubiquiti USW-Pro-24                | **~5–10 triệu VNĐ**    |
| **Firewall**           | MikroTik RB5009 / pfSense appliance                  | **~5–8 triệu VNĐ**     |

#### Chi phí On-Premise tổng quan

| Hạng mục                    | Chi phí (VNĐ)            | Ghi chú                      |
| --------------------------- | ------------------------ | ---------------------------- |
| **Server Primary**          | 150,000,000              | Khấu hao 5 năm               |
| **Server Standby**          | 100,000,000              | Khấu hao 5 năm               |
| **UPS + Switch + Firewall** | 40,000,000               | Khấu hao 5 năm               |
| **Tổng hardware (1 lần)**   | **290,000,000**          | ~$11,600                     |
|                             |                          |                              |
| **Internet leased line**    | 3,000,000/tháng          | VNPT/Viettel/FPT, IP tĩnh    |
| **Điện**                    | 2,000,000/tháng          | ~500W × 2 server, 24/7       |
| **Colocation** (nếu đặt DC) | 5,000,000/tháng          | Rack 1U-2U, VNDC/Viettel IDC |
| **Cloud DR backup**         | 500,000/tháng            | S3-compatible, 200GB         |
| **Tổng vận hành/tháng**     | **5,500,000–10,500,000** | ~$220–$420/tháng             |

#### Khấu hao + Vận hành qua 3 năm

```
Năm 1:  290,000,000 (hardware) + 10,500,000 × 12 = 416,000,000 VNĐ (~$16,640)
Năm 2:                            10,500,000 × 12 = 126,000,000 VNĐ (~$5,040)
Năm 3:                            10,500,000 × 12 = 126,000,000 VNĐ (~$5,040)
─────────────────────────────────────────────────────────────────────────────
Tổng 3 năm:                                        668,000,000 VNĐ (~$26,720)
Trung bình:                                      18,556,000 VNĐ/tháng (~$742)
```

**Ưu điểm:**

- ✅ Data sovereignty hoàn toàn (dữ liệu tại Việt Nam)
- ✅ Performance cao nhất (dedicated hardware)
- ✅ Không phụ thuộc internet cho internal operations
- ✅ Chi phí cố định, dự đoán được sau năm 1
- ✅ Phù hợp compliance HIPAA strict (data residency)

**Nhược điểm:**

- ❌ Chi phí ban đầu rất cao (~290 triệu VNĐ)
- ❌ Cần nhân lực quản trị (1 SysAdmin part-time tối thiểu)
- ❌ Single point of failure nếu không có colocation
- ❌ Hardware replacement: 1–5 ngày (vs cloud: phút)
- ❌ Capacity planning khó (mua dư → lãng phí, thiếu → bottleneck)

---

### 3.5 Phương án D: Hybrid (Recommended)

**Phù hợp:** Cân bằng chi phí, performance, và reliability. Giải pháp được khuyến nghị cho hầu hết phòng khám IVF.

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Hybrid Architecture                               │
│                                                                     │
│  ┌─── VPS Primary (Hetzner/DO) ──────────────────────────────────┐ │
│  │  8vCPU, 32GB RAM, 400GB NVMe                                   │ │
│  │  Location: Singapore hoặc Germany                              │ │
│  │  Run: Full IVF stack (API, DB, Redis, MinIO, PKI)              │ │
│  │  Cost: €35–$192/month                                          │ │
│  └────────────────────────────────────────────────────────────────┘ │
│           │                                                         │
│           │  Streaming Replication (SSL)                            │
│           ▼                                                         │
│  ┌─── Cloud DR (AWS/GCP) ────────────────────────────────────────┐ │
│  │  PostgreSQL standby (Cloud SQL/RDS) — chỉ chạy khi cần DR    │ │
│  │  S3 backup (lifecycle: Standard → Glacier sau 30 ngày)        │ │
│  │  Cost: ~$50–80/month (DB standby off → $15 backup only)       │ │
│  └────────────────────────────────────────────────────────────────┘ │
│           │                                                         │
│           │  Optional: Auto-failover                               │
│           ▼                                                         │
│  ┌─── Cloudflare (Free/Pro) ─────────────────────────────────────┐ │
│  │  CDN, DDoS protection, WAF, DNS failover                      │ │
│  │  Health checks → auto-switch DR nếu primary down              │ │
│  │  Cost: $0–$20/month                                           │ │
│  └────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

#### Chi phí Hybrid (Hetzner + AWS S3 DR)

| Hạng mục                        | Chi phí/tháng   |
| ------------------------------- | --------------- |
| VPS Primary (Hetzner CPX51)     | €35 (~$38)      |
| VPS Standby (Hetzner CPX31)     | €16 (~$18)      |
| Hetzner Load Balancer (LB11)    | €6 (~$7)        |
| Hetzner Volume 200GB × 2        | €18 (~$20)      |
| AWS S3 Backup (200GB + Glacier) | ~$10            |
| Cloudflare Pro                  | $20             |
| Domain (.clinic TLD)            | ~$5             |
| **Tổng**                        | **~$118/tháng** |

#### Chi phí Hybrid (DigitalOcean + Cloudflare)

| Hạng mục                        | Chi phí/tháng   |
| ------------------------------- | --------------- |
| Droplet Primary (8C/32GB, SGP1) | $192            |
| Droplet Standby (4C/8GB, SGP1)  | $48             |
| DO Spaces (250GB backup)        | $5              |
| DO Load Balancer                | $12             |
| Cloudflare Free                 | $0              |
| **Tổng**                        | **~$257/tháng** |

---

### 3.6 So sánh Tổng chi phí 3 năm (TCO)

| Phương án                | Tháng 1      | /tháng (ổn định) | TCO 3 năm   | /tháng trung bình |
| ------------------------ | ------------ | ---------------- | ----------- | ----------------- |
| **A: AWS Managed**       | $1,060       | $1,060           | **$38,160** | **$1,060**        |
| **A: GCP Managed**       | $750         | $750             | **$27,000** | **$750**          |
| **B: Hetzner VPS × 2**   | $110         | $110             | **$3,960**  | **$110**          |
| **B: Contabo VPS × 2**   | $36          | $36              | **$1,296**  | **$36**           |
| **B: DO VPS × 2**        | $325         | $325             | **$11,700** | **$325**          |
| **C: On-Premise**        | $11,600+$420 | $420             | **$26,720** | **$742**          |
| **D: Hybrid Hetzner+S3** | $118         | $118             | **$4,248**  | **$118**          |
| **D: Hybrid DO+CF**      | $257         | $257             | **$9,252**  | **$257**          |

```
TCO 3 năm (USD) — Biểu đồ so sánh:

AWS Managed      ████████████████████████████████████████ $38,160
GCP Managed      ████████████████████████████  $27,000
On-Premise       ███████████████████████████   $26,720
DO VPS × 2       ████████████  $11,700
Hybrid DO+CF     █████████  $9,252
Hybrid Hetzner   ████  $4,248
Hetzner VPS × 2  ████  $3,960
Contabo VPS × 2  █  $1,296
```

### 3.7 Decision Matrix — Chọn Phương án

| Tiêu chí                 | Trọng số | A: Cloud Managed | B: VPS         | C: On-Prem     | D: Hybrid    |
| ------------------------ | -------- | ---------------- | -------------- | -------------- | ------------ |
| **Chi phí**              | 25%      | ⭐⭐ (2)         | ⭐⭐⭐⭐⭐ (5) | ⭐⭐⭐ (3)     | ⭐⭐⭐⭐ (4) |
| **SLA / Reliability**    | 25%      | ⭐⭐⭐⭐⭐ (5)   | ⭐⭐⭐ (3)     | ⭐⭐⭐⭐ (4)   | ⭐⭐⭐⭐ (4) |
| **Bảo mật & Compliance** | 20%      | ⭐⭐⭐⭐⭐ (5)   | ⭐⭐⭐ (3)     | ⭐⭐⭐⭐⭐ (5) | ⭐⭐⭐⭐ (4) |
| **Dễ vận hành**          | 15%      | ⭐⭐⭐⭐⭐ (5)   | ⭐⭐ (2)       | ⭐⭐ (2)       | ⭐⭐⭐ (3)   |
| **Scalability**          | 10%      | ⭐⭐⭐⭐⭐ (5)   | ⭐⭐⭐ (3)     | ⭐⭐ (2)       | ⭐⭐⭐⭐ (4) |
| **Data Sovereignty**     | 5%       | ⭐⭐⭐ (3)       | ⭐⭐⭐⭐ (4)   | ⭐⭐⭐⭐⭐ (5) | ⭐⭐⭐⭐ (4) |
|                          |          |                  |                |                |              |
| **Tổng điểm**            | **100%** | **4.05**         | **3.35**       | **3.45**       | **3.80**     |

### 3.8 Khuyến nghị theo quy mô

| Quy mô                       | Recommendation                                  | Lý do                                                |
| ---------------------------- | ----------------------------------------------- | ---------------------------------------------------- |
| **1–5 tenants** (startup)    | **B: Hetzner VPS** (~$110/tháng)                | Chi phí thấp nhất, đủ performance cho <1000 patients |
| **5–20 tenants** (growth)    | **D: Hybrid Hetzner + S3 DR** (~$118/tháng)     | Cân bằng chi phí & reliability với DR sẵn sàng       |
| **20–50 tenants** (scale)    | **D: Hybrid DO/Vultr + Cloud DR** (~$257/tháng) | Asia-Pacific latency thấp, managed backup option     |
| **50+ tenants** (enterprise) | **A: AWS/GCP Managed** (~$750–$1,060/tháng)     | Auto-scale, compliance certs, multi-region           |
| **Data residency bắt buộc**  | **C: On-Premise** (colocation VN)               | Dữ liệu y tế phải ở Việt Nam theo quy định           |

### 3.9 Cloud-Specific Managed Services Mapping

Khi migrate từ Docker Compose self-hosted sang Cloud Managed:

| IVF Component      | Docker (Self-hosted)   | AWS Managed                | GCP Managed                | Azure Managed              |
| ------------------ | ---------------------- | -------------------------- | -------------------------- | -------------------------- |
| **API (.NET)**     | Docker container       | ECS Fargate / App Runner   | Cloud Run                  | Container Apps             |
| **PostgreSQL**     | postgres:16 container  | RDS PostgreSQL             | Cloud SQL                  | Azure DB for PostgreSQL    |
| **Redis**          | redis:alpine container | ElastiCache                | Memorystore                | Azure Cache for Redis      |
| **Object Storage** | MinIO container        | S3                         | Cloud Storage              | Blob Storage               |
| **Reverse Proxy**  | Caddy container        | ALB + CloudFront           | Cloud Load Balancing + CDN | Application Gateway + CDN  |
| **SSL Certs**      | Caddy auto HTTPS       | ACM (free)                 | Google-managed SSL         | App Service Managed Cert   |
| **Secrets**        | Docker secrets         | Secrets Manager            | Secret Manager             | Key Vault                  |
| **PKI/HSM**        | EJBCA + SoftHSM        | CloudHSM + ACM PCA         | Cloud HSM + CA Service     | Dedicated HSM + Key Vault  |
| **Monitoring**     | Prometheus + Grafana   | CloudWatch + X-Ray         | Cloud Monitoring + Trace   | Application Insights       |
| **CI/CD**          | GitHub Actions         | GitHub Actions → ECR → ECS | GitHub Actions → GAR → Run | GitHub Actions → ACR → ACA |
| **DNS**            | Cloudflare             | Route 53                   | Cloud DNS                  | Azure DNS                  |
| **WAF**            | Cloudflare WAF         | AWS WAF                    | Cloud Armor                | Azure WAF                  |

### 3.10 Cost Optimization Tips

| Tip                                   | Tiết kiệm       | Áp dụng cho                      |
| ------------------------------------- | --------------- | -------------------------------- |
| **Reserved Instances** (1y/3y commit) | 30–60%          | AWS/GCP/Azure                    |
| **Spot/Preemptible** cho batch jobs   | 60–90%          | CI/CD, backup processing         |
| **S3 Intelligent-Tiering**            | 20–40%          | AWS (auto-move infrequent data)  |
| **Glacier** cho backup >30 ngày       | 80% vs Standard | AWS                              |
| **Turn off DR standby** khi không cần | 50%+            | Hybrid (chỉ bật khi DR)          |
| **Cloudflare Free plan**              | $20/tháng       | CDN + DDoS (free tier đủ dùng)   |
| **Right-sizing** DB instances         | 20–40%          | Tất cả (monitor actual usage)    |
| **Auto-scaling to zero**              | Theo usage      | Cloud Run / Azure Container Apps |
| **Domestic VPS** (VN providers)       | Local pricing   | FPT Cloud, Viettel Cloud, CMC    |

#### VPS Providers Việt Nam (Data Residency)

| Provider        | Spec (8vCPU/16GB/200GB) | Chi phí/tháng          | Ghi chú                      |
| --------------- | ----------------------- | ---------------------- | ---------------------------- |
| **FPT Cloud**   | fCIS-8vCPU-16GB         | ~3,500,000 VNĐ (~$140) | ISO 27001, datacenter VN     |
| **Viettel IDC** | Cloud Server Pro        | ~3,000,000 VNĐ (~$120) | Tier III DC, DDoS protection |
| **CMC Cloud**   | Standard 8vCPU          | ~2,800,000 VNĐ (~$112) | Tier III DC                  |
| **VNPT Cloud**  | Cloud Server            | ~3,200,000 VNĐ (~$128) | Nationwide DC                |

> **Lưu ý:** Các provider VN thường có performance và support thấp hơn international providers, nhưng đáp ứng yêu cầu data residency cho dữ liệu y tế.

---

## 4. Orchestration: Docker Compose vs Docker Swarm vs K8s

### 4.1 Bối cảnh: IVF Platform trên 2 VPS Contabo

Với đặc thù IVF Platform:

- **9 containers** chạy đồng thời (API, PostgreSQL, Redis, MinIO, Caddy, EJBCA, SignServer + 2 DB phụ)
- **~8 GB RAM** tối thiểu cho tất cả containers
- **7 stateful services** (3 PostgreSQL, MinIO, EJBCA, SignServer, Caddy certs)
- **5 SignalR hubs** (WebSocket — cần sticky sessions khi scale API)
- **3 isolated networks** (public, signing/internal, data/internal)
- **2 VPS Contabo** (ví dụ: Cloud VPS XL 8vCPU/32GB/400GB, ~€18/tháng)

Câu hỏi: Docker Compose, Docker Swarm, hay Kubernetes?

---

### 4.2 So sánh 4 phương án Orchestration

#### Phương án 1: Docker Compose (Hiện tại)

```
┌──────────────────────────────────────────────────────────────────┐
│            Docker Compose trên 2 VPS Contabo                     │
│                                                                  │
│  VPS 1 (Primary)                          RAM Usage:             │
│  ├─ Docker Engine                         ~100 MB                │
│  ├─ Caddy (reverse proxy, auto HTTPS)     ~30 MB                 │
│  ├─ API (.NET 10)                         ~500 MB                │
│  ├─ PostgreSQL Primary                    ~1,000 MB              │
│  ├─ Redis                                 ~256 MB                │
│  ├─ MinIO                                 ~500 MB                │
│  ├─ EJBCA + EJBCA-DB                      ~2,500 MB              │
│  └─ SignServer + SignServer-DB            ~2,500 MB              │
│  Total: ~7,386 MB (~7.2 GB)                                      │
│                                                                  │
│  VPS 2 (Standby — docker-compose.standby.yml riêng)             │
│  ├─ Docker Engine                         ~100 MB                │
│  ├─ Caddy                                 ~30 MB                 │
│  ├─ API (.NET 10, replica)                ~500 MB                │
│  ├─ PostgreSQL Standby                    ~1,000 MB              │
│  ├─ Redis Replica                         ~256 MB                │
│  └─ MinIO Replica                         ~500 MB                │
│  Total: ~2,386 MB (~2.3 GB)                                      │
│                                                                  │
│  Orchestration overhead: ~100 MB (Docker Engine only)            │
│  Deploy: SSH + docker compose up -d (mỗi VPS riêng)             │
│  Failover: Cloudflare DNS health check (30-60s switch)           │
└──────────────────────────────────────────────────────────────────┘
```

**Hạn chế với 2 VPS:**

- ❌ Hai VPS hoạt động độc lập, không biết nhau
- ❌ Deploy phải SSH vào từng VPS chạy riêng
- ❌ Failover thủ công (hoặc phụ thuộc Cloudflare DNS)
- ❌ Không có rolling update native
- ❌ Scale API phải sửa compose file + nginx/caddy upstream

---

#### Phương án 2: Docker Swarm (KHUYẾN NGHỊ cho 2 VPS)

```
┌──────────────────────────────────────────────────────────────────┐
│              Docker Swarm trên 2 VPS Contabo                     │
│                                                                  │
│  VPS 1 (Manager + Worker)                 RAM Usage:             │
│  ├─ Docker Engine (Swarm mode)            ~150 MB (+50 MB Raft)  │
│  ├─ Caddy (global mode — chạy cả 2 VPS)  ~30 MB                 │
│  ├─ API (replicas=2, spread across VPS)   ~500 MB                │
│  ├─ PostgreSQL Primary (constraint VPS1)  ~1,000 MB              │
│  ├─ Redis                                 ~256 MB                │
│  ├─ MinIO                                 ~500 MB                │
│  ├─ EJBCA + EJBCA-DB (constraint VPS1)    ~2,500 MB              │
│  └─ SignServer + SignServer-DB (VPS1)     ~2,500 MB              │
│  Total: ~7,436 MB (~7.3 GB)                                      │
│                                                                  │
│  VPS 2 (Worker)                                                  │
│  ├─ Docker Engine (Swarm worker)          ~120 MB                │
│  ├─ Caddy (global mode replica)           ~30 MB                 │
│  ├─ API (replica tự động phân bổ)         ~500 MB                │
│  ├─ PostgreSQL Standby (constraint VPS2)  ~1,000 MB              │
│  └─ Redis Replica                         ~256 MB                │
│  Total: ~1,906 MB (~1.9 GB)                                      │
│                                                                  │
│  Orchestration overhead: ~170 MB (Docker Engine + Raft consensus)│
│  vs Compose: +70 MB | vs K8s: -1,330 MB saved                   │
│                                                                  │
│  ✅ Single deploy command: docker stack deploy -c stack.yml ivf  │
│  ✅ Rolling updates: update_config → 1 at a time, 30s delay     │
│  ✅ Auto-restart: restart_policy → any, max 10 attempts         │
│  ✅ Service discovery: built-in DNS (service_name resolves)      │
│  ✅ Overlay network: encrypted cross-VPS communication           │
│  ✅ Docker Secrets: native (đã dùng trong production.yml)        │
│  ✅ Health checks → auto-replace unhealthy containers            │
└──────────────────────────────────────────────────────────────────┘
```

**Docker Swarm mang lại gì so với Compose:**

| Tính năng             | Docker Compose            | Docker Swarm                                        | Lợi ích                             |
| --------------------- | ------------------------- | --------------------------------------------------- | ----------------------------------- |
| **Deploy**            | SSH mỗi VPS, chạy riêng   | `docker stack deploy` 1 lần                         | Giảm human error, tự động hóa       |
| **Rolling update**    | Tắt → bật (downtime)      | `--update-delay 30s --update-parallelism 1`         | **Zero-downtime deploy**            |
| **Auto-healing**      | `restart: unless-stopped` | `restart_policy` + health check → reschedule        | Tự phát hiện + tự phục hồi          |
| **Service discovery** | Hardcode IP/hostname      | Built-in DNS (`tasks.api` resolves tất cả replicas) | API replicas tự tìm nhau            |
| **Overlay network**   | Không (mỗi VPS riêng)     | Encrypted overlay network cross-VPS                 | **VPS 1 ↔ VPS 2 giao tiếp an toàn** |
| **Load balancing**    | Caddy upstream thủ công   | Ingress routing mesh (round-robin)                  | API requests tự phân bổ             |
| **Secrets**           | File mount                | `docker secret` (encrypted in Raft)                 | Secrets encrypted at rest           |
| **Scale**             | Sửa file, chạy lại        | `docker service scale api=3`                        | Scale tức thì, 1 lệnh               |
| **Placement**         | Không có                  | `constraints: [node.hostname == vps1]`              | PKI luôn ở VPS 1                    |

**Swarm Stack file (stack.yml):**

```yaml
version: "3.8"

services:
  # ╔══════════════════════════════════════════════════════╗
  # ║  API — Stateless, replicated across both VPS        ║
  # ╚══════════════════════════════════════════════════════╝
  api:
    image: ivf-api:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    secrets:
      - ivf_db_password
      - jwt_secret
    networks:
      - ivf-public
      - ivf-signing
      - ivf-data
    deploy:
      replicas: 2 # 1 per VPS
      update_config:
        parallelism: 1 # Update 1 at a time
        delay: 30s # Wait 30s between updates
        order: start-first # New container starts before old stops
        failure_action: rollback # Auto-rollback on failure
      rollback_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: any
        delay: 5s
        max_attempts: 10
        window: 120s
      resources:
        limits:
          memory: 1G
          cpus: "2"
    healthcheck:
      test: ["CMD", "wget", "-q", "-O-", "http://localhost:8080/health/live"]
      interval: 15s
      timeout: 5s
      retries: 3
      start_period: 30s

  # ╔══════════════════════════════════════════════════════╗
  # ║  Caddy — Global mode (1 per VPS), handles SSL       ║
  # ╚══════════════════════════════════════════════════════╝
  caddy:
    image: caddy:2-alpine
    ports:
      - target: 80
        published: 80
        mode: host # Direct host port (no ingress mesh)
      - target: 443
        published: 443
        mode: host
    volumes:
      - caddy_data:/data
      - caddy_config:/config
      - ./docker/caddy/Caddyfile:/etc/caddy/Caddyfile:ro
      - ./ivf-client/dist/ivf-client/browser:/srv/frontend:ro
    networks:
      - ivf-public
    deploy:
      mode: global # 1 instance per VPS
      restart_policy:
        condition: any

  # ╔══════════════════════════════════════════════════════╗
  # ║  PostgreSQL Primary — Pinned to VPS 1               ║
  # ╚══════════════════════════════════════════════════════╝
  db:
    image: postgres:16-alpine
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - postgres_archive:/var/lib/postgresql/archive
    secrets:
      - ivf_db_password
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/ivf_db_password
      POSTGRES_DB: ivf_db
    networks:
      - ivf-data
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.role == primary # VPS 1 only
      resources:
        limits:
          memory: 4G
          cpus: "2"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  # ╔══════════════════════════════════════════════════════╗
  # ║  PostgreSQL Standby — Pinned to VPS 2               ║
  # ╚══════════════════════════════════════════════════════╝
  db-standby:
    image: postgres:16-alpine
    volumes:
      - postgres_standby:/var/lib/postgresql/data
    environment:
      PGDATA: /var/lib/postgresql/data
    networks:
      - ivf-data
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.role == standby # VPS 2 only
      resources:
        limits:
          memory: 2G

  # ╔══════════════════════════════════════════════════════╗
  # ║  Redis — Lightweight, runs anywhere                  ║
  # ╚══════════════════════════════════════════════════════╝
  redis:
    image: redis:alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    networks:
      - ivf-data
      - ivf-public
    deploy:
      replicas: 1
      resources:
        limits:
          memory: 512M

  # ╔══════════════════════════════════════════════════════╗
  # ║  MinIO — Pinned to VPS 1 (primary storage)          ║
  # ╚══════════════════════════════════════════════════════╝
  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    volumes:
      - minio_data:/data
    secrets:
      - minio_access_key
      - minio_secret_key
    networks:
      - ivf-data
      - ivf-public
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 1G

  # ╔══════════════════════════════════════════════════════╗
  # ║  EJBCA + SignServer — Pinned to VPS 1, signing net   ║
  # ╚══════════════════════════════════════════════════════╝
  ejbca:
    image: keyfactor/ejbca-ce:latest
    volumes:
      - ejbca_persistent:/opt/keyfactor/ejbca-ce
    networks:
      - ivf-signing
      - ivf-data
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.role == primary # PKI stays on VPS 1
      resources:
        limits:
          memory: 2G

  signserver:
    image: keyfactor/signserver-ce:latest
    volumes:
      - signserver_persistent:/opt/keyfactor/signserver-ce
    networks:
      - ivf-signing
      - ivf-data
    deploy:
      replicas: 1
      placement:
        constraints:
          - node.labels.role == primary
      resources:
        limits:
          memory: 2G

  # DB phụ cho EJBCA + SignServer (cùng constraint VPS 1)
  ejbca-db:
    image: postgres:16-alpine
    volumes:
      - ejbca_db_data:/var/lib/postgresql/data
    networks:
      - ivf-data
    deploy:
      placement:
        constraints:
          - node.labels.role == primary

  signserver-db:
    image: postgres:16-alpine
    volumes:
      - signserver_db_data:/var/lib/postgresql/data
    networks:
      - ivf-data
    deploy:
      placement:
        constraints:
          - node.labels.role == primary

# ╔══════════════════════════════════════════════════════════╗
# ║  Networks — Swarm overlay (encrypted cross-VPS)         ║
# ╚══════════════════════════════════════════════════════════╝
networks:
  ivf-public:
    driver: overlay
  ivf-signing:
    driver: overlay
    internal: true # No external access
    driver_opts:
      encrypted: "true" # IPSec encryption
  ivf-data:
    driver: overlay
    internal: true
    driver_opts:
      encrypted: "true"

# ╔══════════════════════════════════════════════════════════╗
# ║  Volumes — Local per-node (stateful data stays put)     ║
# ╚══════════════════════════════════════════════════════════╝
volumes:
  postgres_data:
  postgres_archive:
  postgres_standby:
  minio_data:
  ejbca_persistent:
  ejbca_db_data:
  signserver_persistent:
  signserver_db_data:
  caddy_data:
  caddy_config:

# ╔══════════════════════════════════════════════════════════╗
# ║  Secrets — Encrypted in Swarm Raft store                ║
# ╚══════════════════════════════════════════════════════════╝
secrets:
  ivf_db_password:
    file: ./secrets/ivf_db_password.txt
  jwt_secret:
    file: ./secrets/jwt_secret.txt
  minio_access_key:
    file: ./secrets/minio_access_key.txt
  minio_secret_key:
    file: ./secrets/minio_secret_key.txt
```

**Khởi tạo Swarm cluster:**

```bash
# === VPS 1 (Manager) ===
docker swarm init --advertise-addr <VPS1_PUBLIC_IP>
# Output: docker swarm join --token SWMTKN-xxx <VPS1_IP>:2377

# Label VPS 1 as primary
docker node update --label-add role=primary $(hostname)

# === VPS 2 (Worker) ===
docker swarm join --token SWMTKN-xxx <VPS1_IP>:2377

# === VPS 1: Label VPS 2 as standby ===
docker node update --label-add role=standby <VPS2_NODE_ID>

# Deploy stack
docker stack deploy -c stack.yml ivf

# Verify
docker service ls
docker service ps ivf_api    # Xem API chạy ở VPS nào
docker node ls                # Xem node status
```

**Swarm operations hàng ngày:**

```bash
# Rolling update API (zero-downtime)
docker service update --image ivf-api:v2.1 ivf_api
# Swarm tự: stop 1 old → start 1 new → health check → stop 2nd old → start 2nd new

# Scale API
docker service scale ivf_api=3    # Thêm 1 replica

# Rollback nếu update lỗi
docker service rollback ivf_api

# Xem logs
docker service logs -f ivf_api --tail=50

# Drain 1 node (bảo trì VPS)
docker node update --availability drain <VPS2_NODE_ID>
# → Swarm tự move containers sang VPS 1
# Sau khi xong: docker node update --availability active <VPS2_NODE_ID>

# Force rebalance
docker service update --force ivf_api
```

**Swarm vs Compose — lợi ích cụ thể cho IVF:**

```
┌──────────────────────────────────────────────────────────────────┐
│  Swarm mang lại cho IVF 2-VPS Contabo:                          │
│                                                                  │
│  1. ZERO-DOWNTIME DEPLOY                                        │
│     Compose: docker compose down → up (30s downtime)            │
│     Swarm:   docker service update (0s downtime)                │
│                                                                  │
│  2. TỰ PHỤC HỒI (Auto-healing)                                 │
│     Compose: restart:unless-stopped (chỉ restart local)         │
│     Swarm:   health check fail → reschedule lên VPS khác        │
│     Ví dụ: API crash trên VPS 2 → Swarm tự chạy trên VPS 1     │
│                                                                  │
│  3. SINGLE DEPLOY POINT                                          │
│     Compose: ssh vps1 "compose up" && ssh vps2 "compose up"     │
│     Swarm:   docker stack deploy -c stack.yml ivf (1 lệnh)      │
│                                                                  │
│  4. ENCRYPTED OVERLAY NETWORK                                    │
│     Compose: VPS 1 ↔ VPS 2 qua public internet (cần VPN/SSH)   │
│     Swarm:   overlay network IPSec tự động mã hóa               │
│                                                                  │
│  5. NATIVE LOAD BALANCING                                        │
│     Compose: Caddy upstream thủ công                             │
│     Swarm:   Routing mesh tự phân bổ requests                    │
│                                                                  │
│  6. NODE DRAIN (bảo trì)                                         │
│     Compose: Tắt VPS 2 → mất services trên đó                  │
│     Swarm:   drain VPS 2 → tự move sang VPS 1 → bảo trì → active│
│                                                                  │
│  7. ROLLBACK TỰ ĐỘNG                                            │
│     Compose: git revert + rebuild + restart                      │
│     Swarm:   docker service rollback (1 lệnh, 10s)              │
│                                                                  │
│  Overhead: chỉ +70 MB RAM so với Compose thuần                  │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

#### Phương án 3: Kubernetes (K3s)

```
Overhead: ~1,500 MB RAM cho control plane
Learning curve: 6-12 tháng
Files: 30+ YAML manifests

Không phù hợp cho 2 VPS Contabo vì:
- 1.5 GB RAM wasted (vs Swarm: 170 MB)
- etcd single-node = no HA for control plane
- 7/9 containers stateful → K8s excels at stateless
- Complexity quá cao cho monolith 1 API service
- Contabo network latency → etcd leader election issues
```

#### Phương án 4: HashiCorp Nomad Stack

```
Overhead: ~1,410 MB (Nomad + Consul + Vault + Traefik + sidecars)
Learning curve: 3-6 tháng

Không phù hợp cho 2 VPS Contabo vì:
- Consul cần 3+ servers cho HA (2 nodes = split-brain)
- Nomad server cần quorum 3 nodes
- Traefik mất On-Demand TLS (Caddy feature)
- Varnish không dùng cho healthcare (HIPAA: no-cache PHI)
- NATS không cần (SignalR + MediatR đã xử lý messaging)
- ~1.4 GB RAM overhead cho stack mà Swarm làm với 170 MB
```

---

### 4.3 Decision Matrix

| Tiêu chí                 | Trọng số | Docker Compose         | Docker Swarm            | K8s (K3s)       | Nomad Stack      |
| ------------------------ | -------- | ---------------------- | ----------------------- | --------------- | ---------------- |
| **RAM overhead**         | 15%      | ⭐⭐⭐⭐⭐ (100 MB)    | ⭐⭐⭐⭐⭐ (170 MB)     | ⭐⭐ (1.5 GB)   | ⭐⭐ (1.4 GB)    |
| **Automation**           | 20%      | ⭐⭐ (manual SSH)      | ⭐⭐⭐⭐⭐ (1 lệnh)     | ⭐⭐⭐⭐⭐      | ⭐⭐⭐⭐         |
| **Zero-downtime deploy** | 15%      | ⭐⭐ (script thủ công) | ⭐⭐⭐⭐⭐ (native)     | ⭐⭐⭐⭐⭐      | ⭐⭐⭐⭐         |
| **Auto-healing**         | 10%      | ⭐⭐⭐ (local restart) | ⭐⭐⭐⭐ (reschedule)   | ⭐⭐⭐⭐⭐      | ⭐⭐⭐⭐         |
| **Complexity**           | 15%      | ⭐⭐⭐⭐⭐ (1 file)    | ⭐⭐⭐⭐ (1 stack file) | ⭐ (30+ files)  | ⭐⭐ (HCL)       |
| **Multi-node native**    | 10%      | ⭐⭐ (SSH riêng)       | ⭐⭐⭐⭐⭐ (overlay)    | ⭐⭐⭐⭐⭐      | ⭐⭐⭐⭐         |
| **Learning curve**       | 5%       | ⭐⭐⭐⭐⭐ (1 tuần)    | ⭐⭐⭐⭐ (2-3 tuần)     | ⭐ (6-12 tháng) | ⭐⭐ (3-6 tháng) |
| **Rollback**             | 5%       | ⭐⭐ (git revert)      | ⭐⭐⭐⭐⭐ (1 lệnh)     | ⭐⭐⭐⭐⭐      | ⭐⭐⭐⭐         |
| **IVF features**         | 5%       | ⭐⭐⭐⭐⭐             | ⭐⭐⭐⭐                | ⭐⭐⭐          | ⭐⭐             |
|                          |          |                        |                         |                 |                  |
| **Tổng (weighted)**      | **100%** | **3.55**               | **4.50**                | **3.05**        | **2.90**         |

### 4.4 Kết luận: Docker Swarm cho 2 VPS Contabo

```
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│   KHUYẾN NGHỊ: Docker Swarm + Caddy                             │
│                                                                  │
│   Docker Swarm là lựa chọn TỐI ƯU cho 2 VPS Contabo vì:       │
│                                                                  │
│   1. Overhead chỉ +70 MB RAM so với Docker Compose              │
│      → Nhẹ hơn K8s 1,330 MB, nhẹ hơn Nomad 1,240 MB           │
│                                                                  │
│   2. ĐÃ CÓ SẴN trong Docker Engine (docker swarm init)         │
│      → Không cần cài thêm gì, không cần binary mới             │
│                                                                  │
│   3. Compose file → Stack file = thay đổi TỐI THIỂU            │
│      → Thêm deploy: section, đổi networks sang overlay         │
│      → docker-compose.yml gần như giữ nguyên                    │
│                                                                  │
│   4. Zero-downtime deploy + auto-rollback + auto-healing        │
│      → Giải quyết đúng nhu cầu tự động hóa + tin cậy           │
│                                                                  │
│   5. Encrypted overlay networks                                  │
│      → VPS 1 ↔ VPS 2 giao tiếp mã hóa tự động (IPSec)        │
│      → Không cần setup VPN/WireGuard riêng                      │
│                                                                  │
│   6. Placement constraints giữ PKI trên VPS 1                   │
│      → EJBCA, SignServer, PKI DB luôn ở signing network         │
│                                                                  │
│   7. Docker Secrets encrypted in Raft                            │
│      → Đã dùng trong docker-compose.production.yml              │
│      → Swarm encrypt at rest trong consensus store              │
│                                                                  │
│   Lưu ý: Caddy vẫn giữ vì On-Demand TLS cho custom domains    │
│   Swarm routing mesh + Caddy = load balancing + auto SSL       │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

**Swarm + Caddy architecture trên 2 VPS Contabo:**

```
             Cloudflare (CDN + DDoS + DNS Failover)
                    │                    │
        ┌───────────▼──────┐  ┌──────────▼────────┐
        │  VPS 1 (€18/mo)  │  │  VPS 2 (€12/mo)   │
        │  Manager + Worker│  │  Worker             │
        │                  │  │                     │
        │  Caddy (:443)───overlay──Caddy (:443)    │
        │       │          │  │       │             │
        │  API replica 1   │  │  API replica 2     │
        │       │          │  │       │             │
        │  PG Primary ──repl──→ PG Standby        │
        │  Redis           │  │  Redis Replica     │
        │  MinIO           │  │                     │
        │  EJBCA+SignSvr   │  │                     │
        └──────────────────┘  └─────────────────────┘

        Chi phí: €30/tháng (~$33)
        Overhead: +70 MB vs Compose
        Features: zero-downtime, auto-heal, overlay net, rollback
```

### 4.5 Caddy trên Swarm — WebSocket & SignalR

Swarm dùng `mode: host` cho Caddy ports để tránh double-NAT với routing mesh, cho phép Caddy handle SSL termination + WebSocket proxy trực tiếp:

```
# Caddyfile — tương thích Swarm
{
    email admin@ivf.clinic
}

*.ivf.clinic, ivf.clinic {
    # API reverse proxy — Swarm DNS tự resolve tất cả API replicas
    handle /api/* {
        reverse_proxy api:8080 {
            lb_policy       round_robin
            health_uri      /health/live
            health_interval 10s
        }
    }

    # SignalR WebSocket — sticky sessions qua cookie
    handle /hubs/* {
        reverse_proxy api:8080 {
            lb_policy       cookie
            lb_cookie_name  ivf_hub_affinity
        }
    }

    # Angular SPA
    handle {
        root * /srv/frontend
        try_files {path} /index.html
        file_server
    }
}
```

### 4.6 Migration path: Compose → Swarm → K8s

```
Giai đoạn hiện tại:
  Docker Compose ← BẠN Ở ĐÂY
        │
        │  Chuyển sang Swarm (1-2 giờ migration)
        ▼
Giai đoạn 2 (RECOMMENDED):
  Docker Swarm (2 VPS Contabo)
  + Caddy (giữ On-Demand TLS)
  + Encrypted overlay networks
  + Redis as SignalR backplane (cho multi-replica API)
        │
        │  Khi cần >3 nodes, service mesh, canary
        ▼
Giai đoạn 3 (5+ nodes):
  K3s cluster (lightweight K8s)
  + Traefik Ingress + cert-manager
  + CloudNativePG operator
  + ArgoCD (GitOps)
        │
        │  Khi enterprise scale
        ▼
Giai đoạn 4 (Cloud):
  Managed K8s (EKS/GKE/AKS)
  + Terraform IaC
```

### 4.7 Swarm Limitations cần biết

| Hạn chế                         | Giải pháp                                                                                              |
| ------------------------------- | ------------------------------------------------------------------------------------------------------ |
| **Single manager = SPOF**       | Chấp nhận được cho 2 nodes. Nếu VPS 1 down, SSH vào VPS 2 chạy `docker swarm init --force-new-cluster` |
| **Không có ingress controller** | Caddy global mode thay thế (tốt hơn cho IVF vì On-Demand TLS)                                          |
| **Volume không replicate**      | Đã có PostgreSQL streaming replication + MinIO site replication + AWS S3 backup                        |
| **Không có auto-scaling**       | Không cần — 2 API replicas đủ cho <50 tenants                                                          |
| **Docker Swarm "deprecated"?**  | KHÔNG deprecated — vẫn trong Docker Engine, Mirantis maintain. Chỉ Swarm standalone toolkit bị remove  |
| **Monitoring**                  | Giữ Prometheus + Grafana stack (như Section 8)                                                         |

---

## 5. Bảo vệ Dữ liệu: AWS S3 & Chiến lược 3-2-1

### 5.1 Tại sao CẦN AWS S3?

```
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  CÓ — AWS S3 là BẮT BUỘC cho dữ liệu y tế                     │
│                                                                  │
│  Rủi ro với chỉ 2 VPS Contabo:                                 │
│                                                                  │
│  ❌ Contabo KHÔNG cam kết dữ liệu trên disk                    │
│     → VPS terminate = mất volume (không persistence guarantee)  │
│     → Không có SLA cho data durability                           │
│                                                                  │
│  ❌ Cả 2 VPS cùng 1 datacenter                                  │
│     → Datacenter fire/flood = mất CẢ HAI BẢN                  │
│     → Ví dụ thực: OVH Strasbourg datacenter fire (2021)        │
│                                                                  │
│  ❌ Ransomware encrypt cả 2 VPS                                 │
│     → Backup trên cùng infra = cũng bị encrypt                │
│                                                                  │
│  ❌ Contabo account bị suspend/hack                              │
│     → Mất access cả 2 VPS cùng lúc                             │
│                                                                  │
│  ❌ Dữ liệu y tế = KHÔNG THỂ MẤT                              │
│     → Hồ sơ bệnh nhân, kết quả xét nghiệm, chu kỳ điều trị   │
│     → Hình ảnh y tế (siêu âm, phôi)                            │
│     → PDF ký số (giá trị pháp lý)                               │
│     → Mất = vi phạm HIPAA + thiệt hại không thể khôi phục     │
│                                                                  │
│  AWS S3 durability: 99.999999999% (11 nines)                    │
│  = Mất 1 object trong 10,000 năm nếu lưu 10 triệu objects     │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 5.2 Kiến trúc Bảo vệ Dữ liệu 3-2-1

```
          Quy tắc 3-2-1:
          3 bản copy → 2 loại media → 1 bản off-site

┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│  Bản 1: VPS 1 (Primary)           ← Live data                  │
│  ├─ PostgreSQL Primary            (patients, cycles, forms...)  │
│  ├─ MinIO Primary                 (medical images, signed PDFs) │
│  └─ Redis                         (cache — không cần backup)   │
│                                                                  │
│  Bản 2: VPS 2 (Standby)           ← Real-time replica          │
│  ├─ PostgreSQL Standby            (streaming replication, ~0s)  │
│  └─ Redis Replica                 (real-time sync)              │
│                                                                  │
│  Bản 3: AWS S3 (Off-site)         ← Daily backup + WAL         │
│  ├─ S3 Bucket: ivf-backups/                                     │
│  │   ├─ daily/                    (pg_dump gzip, ~30 ngày hot)  │
│  │   ├─ wal/                      (WAL archives, continuous)    │
│  │   ├─ minio/                    (MinIO objects mirror)        │
│  │   └─ config/                   (secrets, certs, compose)     │
│  │                                                               │
│  ├─ Lifecycle Policy:                                           │
│  │   0-30 ngày:  S3 Standard     ($0.023/GB/mo)                │
│  │   30-90 ngày: S3 Standard-IA  ($0.0125/GB/mo)               │
│  │   90+ ngày:   S3 Glacier      ($0.004/GB/mo)                │
│  │   365+ ngày:  Glacier Deep    ($0.00099/GB/mo)              │
│  │                                                               │
│  └─ Encryption: AES-256 (SSE-S3) + Bucket Policy (no public)   │
│                                                                  │
│  (Optional) Bản 4: Cross-region replication                     │
│  └─ S3 replica bucket (ap-southeast-2) cho DR cấp region       │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 5.3 Chi phí AWS S3 thực tế

| Hạng mục                             | Dung lượng                | Chi phí/tháng   |
| ------------------------------------ | ------------------------- | --------------- |
| **Database backups** (daily pg_dump) | ~5 GB × 30 ngày = 150 GB  |                 |
| → S3 Standard (30 ngày gần nhất)     | 50 GB                     | $1.15           |
| → S3 Standard-IA (30-90 ngày)        | 50 GB                     | $0.63           |
| → S3 Glacier (90-365 ngày)           | 50 GB                     | $0.20           |
| **WAL archives** (continuous)        | ~20 GB/tháng (compressed) | $0.46           |
| **MinIO mirror** (medical files)     | ~50 GB                    | $1.15           |
| **Config/secrets backup**            | <1 GB                     | $0.02           |
| **PUT/GET requests**                 | ~100K requests/tháng      | $0.50           |
| **Data transfer** (upload only)      | Free inbound              | $0.00           |
|                                      |                           |                 |
| **Tổng ước tính**                    | ~170 GB active            | **~$4-6/tháng** |

> Với S3 Lifecycle (auto-tier), chi phí giảm dần theo thời gian. Glacier Deep Archive cho backup >1 năm chỉ $0.00099/GB = **50 GB backup 10 năm = $6**.

### 5.4 Thiết lập AWS S3 Backup

#### Bước 1: Tạo S3 Bucket + IAM User

```bash
# 1. Tạo S3 bucket (ap-southeast-1 = Singapore, gần VN)
aws s3 mb s3://ivf-backups-production --region ap-southeast-1

# 2. Bật versioning (chống accidental delete)
aws s3api put-bucket-versioning \
  --bucket ivf-backups-production \
  --versioning-configuration Status=Enabled

# 3. Bật encryption mặc định
aws s3api put-bucket-encryption \
  --bucket ivf-backups-production \
  --server-side-encryption-configuration '{
    "Rules": [{"ApplyServerSideEncryptionByDefault": {"SSEAlgorithm": "AES256"}}]
  }'

# 4. Block public access
aws s3api put-public-access-block \
  --bucket ivf-backups-production \
  --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true

# 5. Lifecycle policy (auto-tier)
aws s3api put-bucket-lifecycle-configuration \
  --bucket ivf-backups-production \
  --lifecycle-configuration '{
    "Rules": [
      {
        "ID": "TierToIA",
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
      }
    ]
  }'

# 6. Tạo IAM user riêng cho backup (principle of least privilege)
aws iam create-user --user-name ivf-backup-agent
aws iam put-user-policy --user-name ivf-backup-agent \
  --policy-name ivf-s3-backup \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:GetObject", "s3:ListBucket", "s3:DeleteObject"],
      "Resource": [
        "arn:aws:s3:::ivf-backups-production",
        "arn:aws:s3:::ivf-backups-production/*"
      ]
    }]
  }'
aws iam create-access-key --user-name ivf-backup-agent > aws_credentials.json
```

#### Bước 2: Cài AWS CLI trên VPS

```bash
# Trên VPS 1
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip && sudo ./aws/install

# Configure credentials
aws configure
# AWS Access Key ID: <from step 1>
# AWS Secret Access Key: <from step 1>
# Default region: ap-southeast-1
# Default output: json
```

#### Bước 3: Script Backup tự động

```bash
#!/bin/bash
# /opt/ivf/scripts/backup-to-s3.sh
# Chạy daily qua cron: 0 3 * * * /opt/ivf/scripts/backup-to-s3.sh

set -euo pipefail

BUCKET="s3://ivf-backups-production"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/tmp/ivf-backup-${DATE}"
LOG_FILE="/var/log/ivf/backup-s3.log"

log() { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"; }

mkdir -p "$BACKUP_DIR"

# ── 1. PostgreSQL Full Backup ──
log "Starting PostgreSQL backup..."
docker exec ivf-db pg_dump -U postgres ivf_db -Fc | \
  gzip > "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz"

DUMP_SIZE=$(du -h "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" | cut -f1)
log "PostgreSQL dump: ${DUMP_SIZE}"

# Checksum
sha256sum "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" > "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz.sha256"

# Upload to S3
aws s3 cp "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" \
  "${BUCKET}/daily/ivf_db_${DATE}.dump.gz" \
  --storage-class STANDARD \
  --sse AES256
aws s3 cp "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz.sha256" \
  "${BUCKET}/daily/ivf_db_${DATE}.dump.gz.sha256"

log "PostgreSQL backup uploaded to S3"

# ── 2. WAL Archives ──
log "Syncing WAL archives..."
docker cp ivf-db:/var/lib/postgresql/archive/ "${BACKUP_DIR}/wal/"
aws s3 sync "${BACKUP_DIR}/wal/" "${BUCKET}/wal/" \
  --storage-class STANDARD \
  --sse AES256 \
  --exclude "*.partial"
log "WAL archives synced"

# ── 3. MinIO Objects (medical images, signed PDFs) ──
log "Syncing MinIO objects..."
# Dùng mc (MinIO Client) mirror → S3
docker run --rm --network ivf_ivf-data \
  -e MC_HOST_local=http://minioadmin:minioadmin@minio:9000 \
  -e MC_HOST_s3=https://${AWS_ACCESS_KEY}:${AWS_SECRET_KEY}@s3.ap-southeast-1.amazonaws.com \
  minio/mc:latest \
  mirror --overwrite local/ivf-documents s3/ivf-backups-production/minio/ivf-documents/
docker run --rm --network ivf_ivf-data \
  -e MC_HOST_local=http://minioadmin:minioadmin@minio:9000 \
  -e MC_HOST_s3=https://${AWS_ACCESS_KEY}:${AWS_SECRET_KEY}@s3.ap-southeast-1.amazonaws.com \
  minio/mc:latest \
  mirror --overwrite local/ivf-signed-pdfs s3/ivf-backups-production/minio/ivf-signed-pdfs/
docker run --rm --network ivf_ivf-data \
  -e MC_HOST_local=http://minioadmin:minioadmin@minio:9000 \
  -e MC_HOST_s3=https://${AWS_ACCESS_KEY}:${AWS_SECRET_KEY}@s3.ap-southeast-1.amazonaws.com \
  minio/mc:latest \
  mirror --overwrite local/ivf-medical-images s3/ivf-backups-production/minio/ivf-medical-images/
log "MinIO sync completed"

# ── 4. Config & Secrets backup (encrypted) ──
log "Backing up config..."
tar czf "${BACKUP_DIR}/config_${DATE}.tar.gz" \
  docker-compose.yml docker-compose.production.yml stack.yml \
  docker/caddy/Caddyfile docker/postgres/ .env

# Encrypt with GPG before uploading secrets
gpg --symmetric --cipher-algo AES256 \
  --batch --passphrase-file /opt/ivf/secrets/gpg_passphrase.txt \
  --output "${BACKUP_DIR}/secrets_${DATE}.tar.gz.gpg" \
  <(tar czf - secrets/)

aws s3 cp "${BACKUP_DIR}/config_${DATE}.tar.gz" "${BUCKET}/config/config_${DATE}.tar.gz"
aws s3 cp "${BACKUP_DIR}/secrets_${DATE}.tar.gz.gpg" "${BUCKET}/config/secrets_${DATE}.tar.gz.gpg"
log "Config backup uploaded"

# ── 5. Cleanup local temp ──
rm -rf "$BACKUP_DIR"

# ── 6. Verify upload ──
LATEST=$(aws s3 ls "${BUCKET}/daily/" --recursive | sort | tail -1)
log "Latest S3 backup: ${LATEST}"
log "=== Backup completed successfully ==="
```

```bash
# Crontab
crontab -e
# Daily backup at 3 AM
0 3 * * * /opt/ivf/scripts/backup-to-s3.sh >> /var/log/ivf/backup-s3.log 2>&1
# WAL sync every 15 minutes
*/15 * * * * /opt/ivf/scripts/sync-wal-s3.sh >> /var/log/ivf/wal-s3.log 2>&1
```

### 5.5 Restore từ S3

```bash
#!/bin/bash
# /opt/ivf/scripts/restore-from-s3.sh
# Sử dụng: ./restore-from-s3.sh [date] [pitr_timestamp]
# Ví dụ: ./restore-from-s3.sh 20260305 "2026-03-05 14:30:00+07"

DATE=${1:-$(date +%Y%m%d)}
PITR_TARGET=$2

echo "=== Restoring IVF from S3 backup: ${DATE} ==="

# 1. Download backup
aws s3 cp "s3://ivf-backups-production/daily/ivf_db_${DATE}*.dump.gz" /tmp/restore.dump.gz

# 2. Verify checksum
aws s3 cp "s3://ivf-backups-production/daily/ivf_db_${DATE}*.sha256" /tmp/restore.sha256
sha256sum -c /tmp/restore.sha256

# 3. Stop API
docker service scale ivf_api=0

# 4. Restore database
gunzip /tmp/restore.dump.gz
docker exec -i ivf-db pg_restore -U postgres -d ivf_db --clean --if-exists < /tmp/restore.dump

# 5. PITR replay (nếu có target timestamp)
if [ -n "$PITR_TARGET" ]; then
    echo "Replaying WAL to: ${PITR_TARGET}"
    aws s3 sync "s3://ivf-backups-production/wal/" /tmp/wal-restore/
    # ... PITR recovery process
fi

# 6. Restore MinIO objects
docker run --rm --network ivf_ivf-data \
  minio/mc:latest \
  mirror s3/ivf-backups-production/minio/ivf-documents/ local/ivf-documents/

# 7. Start API
docker service scale ivf_api=2

echo "=== Restore completed ==="
```

### 5.6 Monitoring Backup Health

```bash
# Thêm vào backup script — gửi alert nếu backup fail

# Healthcheck via webhook (UptimeRobot, Healthchecks.io)
HEALTHCHECK_URL="https://hc-ping.com/your-uuid"

# Success
curl -fsS --retry 3 "$HEALTHCHECK_URL" > /dev/null

# Hoặc fail
curl -fsS --retry 3 "$HEALTHCHECK_URL/fail" > /dev/null
```

```
┌──────────────────────────────────────────────────────────────────┐
│  Backup monitoring checklist (tự động alert):                    │
│                                                                  │
│  ✅ Backup chạy hàng ngày (cron 3 AM)                          │
│  ✅ SHA256 checksum verify sau upload                            │
│  ✅ S3 object count tăng hàng ngày                              │
│  ✅ WAL sync mỗi 15 phút                                       │
│  ✅ MinIO mirror hoàn tất                                       │
│  ✅ Backup size hợp lý (không đột biến)                         │
│  ✅ Monthly restore test (critical!)                             │
│                                                                  │
│  Alert channels: Email + Telegram/Slack webhook                  │
└──────────────────────────────────────────────────────────────────┘
```

### 5.7 Chi phí Tổng: 2 VPS Contabo + Swarm + S3

| Hạng mục                                 | Chi phí/tháng  |
| ---------------------------------------- | -------------- |
| VPS 1 (Contabo Cloud VPS XL, 8vCPU/32GB) | €18 (~$20)     |
| VPS 2 (Contabo Cloud VPS L, 6vCPU/16GB)  | €12 (~$13)     |
| AWS S3 (backup ~170 GB with lifecycle)   | ~$5            |
| Cloudflare (Free plan)                   | $0             |
| Domain (.clinic TLD)                     | ~$5            |
| **Tổng**                                 | **~$43/tháng** |

```
So sánh TCO 3 năm:

Docker Compose (2 VPS, no S3):    $33 × 36 = $1,188    ← Rủi ro mất dữ liệu!
Docker Swarm (2 VPS + S3):        $43 × 36 = $1,548    ← RECOMMENDED
AWS Managed (RDS + ECS):          $750 × 36 = $27,000
On-Premise (server + UPS):        $742 × 36 = $26,712

Swarm + S3 = 5.7% chi phí của AWS Managed, nhưng:
✅ Zero-downtime deploy
✅ Auto-healing
✅ Data durability 99.999999999% (S3)
✅ Off-site backup (khác provider, khác region)
✅ PITR capability
```

### 5.8 Alternatives S3 — nếu không muốn AWS

| Provider                | Service                 | Giá ~170 GB/tháng              | S3-compatible?       |
| ----------------------- | ----------------------- | ------------------------------ | -------------------- |
| **AWS S3**              | S3 Standard + Lifecycle | ~$5                            | ✅ Native            |
| **Backblaze B2**        | B2 Cloud Storage        | ~$1 ($0.006/GB)                | ✅ S3-compatible API |
| **Cloudflare R2**       | R2 Object Storage       | ~$2.50 (no egress fee!)        | ✅ S3-compatible     |
| **Wasabi**              | Hot Storage             | ~$1.20 ($0.0069/GB, no egress) | ✅ S3-compatible     |
| **Hetzner Storage Box** | BX21 (1TB)              | €3.81                          | ❌ (SFTP/WebDAV)     |
| **MinIO on 3rd VPS**    | Self-hosted             | ~$5 VPS cost                   | ✅ Native MinIO      |

> **Budget tối ưu:** **Backblaze B2** ($1/tháng) hoặc **Cloudflare R2** ($2.50/tháng, zero egress) thay AWS S3 nếu muốn tiết kiệm thêm. Cả hai đều S3-compatible nên script backup không cần thay đổi.

---

## 6. Triển khai từng bước

### 6.1 Bước 1: Chuẩn bị Server

```bash
# 1. Cập nhật OS
sudo apt update && sudo apt upgrade -y

# 2. Cài đặt Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# 3. Cài đặt Docker Compose V2
sudo apt install docker-compose-plugin -y

# 4. Tối ưu kernel cho production
cat >> /etc/sysctl.conf << 'EOF'
# Network performance
net.core.somaxconn = 65535
net.core.netdev_max_backlog = 65535
net.ipv4.tcp_max_syn_backlog = 65535
net.ipv4.tcp_fin_timeout = 10
net.ipv4.tcp_tw_reuse = 1
net.ipv4.tcp_keepalive_time = 60
net.ipv4.tcp_keepalive_intvl = 10
net.ipv4.tcp_keepalive_probes = 6

# Memory
vm.overcommit_memory = 1
vm.swappiness = 10

# File descriptors
fs.file-max = 2097152
fs.nr_open = 2097152
EOF
sudo sysctl -p

# 5. Tăng file descriptor limits
cat >> /etc/security/limits.conf << 'EOF'
*  soft  nofile  1048576
*  hard  nofile  1048576
EOF

# 6. Tạo thư mục project
mkdir -p /opt/ivf && cd /opt/ivf
```

### 6.2 Bước 2: Clone & Cấu hình

```bash
# 1. Clone repository
git clone https://github.com/Hung6066/IVF.git /opt/ivf
cd /opt/ivf

# 2. Tạo Docker secrets
mkdir -p secrets
# Tạo password riêng cho mỗi service (sử dụng random 32+ chars)
openssl rand -base64 32 > secrets/ivf_db_password.txt
openssl rand -base64 32 > secrets/ejbca_db_password.txt
openssl rand -base64 32 > secrets/signserver_db_password.txt
openssl rand -base64 32 > secrets/keystore_password.txt
openssl rand -base64 64 > secrets/jwt_secret.txt
openssl rand -base64 32 > secrets/minio_access_key.txt
openssl rand -base64 32 > secrets/minio_secret_key.txt
openssl rand -base64 32 > secrets/api_cert_password.txt
openssl rand -base64 32 > secrets/softhsm_pin.txt
openssl rand -base64 32 > secrets/softhsm_so_pin.txt

# 3. Bảo vệ secrets
chmod 600 secrets/*.txt
chmod 700 secrets/

# 4. Cấu hình environment
cp .env.example .env
# Chỉnh sửa .env với domain, email, và production settings
```

### 6.3 Bước 3: Build Angular Frontend

```bash
cd ivf-client

# Install dependencies
npm ci --production=false

# Build production
npm run build

# Verify output
ls -la dist/ivf-client/browser/
# Caddy sẽ serve từ /srv/frontend (mount trong docker-compose.production.yml)

cd ..
```

### 6.4 Bước 4: Khởi động Production

```bash
# Build API Docker image
docker compose build api

# Khởi động tất cả services (production mode)
docker compose -f docker-compose.yml -f docker-compose.production.yml up -d

# Kiểm tra trạng thái
docker compose ps

# Xem logs startup
docker compose logs -f api --tail=50

# Chờ database migration + seeding hoàn tất
# Log: "Application started. Press Ctrl+C to shut down."
```

### 6.5 Bước 5: Cấu hình PKI (Ký số)

```bash
# 1. Chờ EJBCA khởi động (2-3 phút)
docker compose logs -f ejbca --tail=20

# 2. Khởi tạo SoftHSM2
./scripts/init-softhsm.sh

# 3. Setup mTLS production
./scripts/init-mtls-production.sh

# 4. Khởi tạo TSA & OCSP
./scripts/init-tsa.sh
./scripts/init-ocsp.sh
```

### 6.6 Bước 6: Setup DNS & SSL

```bash
# 1. DNS Records (tại registrar)
# A record:     ivf.clinic         → {Server IP}
# CNAME record:  *.ivf.clinic      → ivf.clinic
# (hoặc A record wildcard nếu registrar hỗ trợ)

# 2. Caddy tự động lấy SSL certificate từ Let's Encrypt
# Kiểm tra cert status
docker compose exec caddy caddy list-modules | grep tls
docker compose exec caddy wget -q -O- http://localhost:2019/config/ | head -20

# 3. Verify HTTPS
curl -I https://ivf.clinic/api/health
# Expect: HTTP/2 200
```

### 6.7 Bước 7: Setup Replication (HA)

```bash
# === Trên Node A (Primary) ===

# 1. Khởi tạo WAL replication
docker compose exec db bash /docker-entrypoint-initdb.d/init-wal-replication.sh

# 2. Verify replication config
docker compose exec db psql -U postgres -c "SHOW wal_level;"       # replica
docker compose exec db psql -U postgres -c "SHOW max_wal_senders;" # 5

# === Trên Node B (Local Standby) ===

# 3. Khởi động standby (đã cấu hình trong docker-compose.yml)
docker compose --profile replication up -d db-standby

# 4. Verify streaming replication
docker compose exec db psql -U postgres -c \
  "SELECT pid, state, client_addr, sent_lsn, replay_lsn FROM pg_stat_replication;"

# === Trên Node C (Cloud Replica) ===

# 5. Deploy cloud replica
scp docker-compose.replica.yml cloud-server:/opt/ivf/
scp .env cloud-server:/opt/ivf/
ssh cloud-server "cd /opt/ivf && docker compose -f docker-compose.replica.yml up -d"
```

### 6.8 Bước 8: Verify Production

```bash
# 1. Health check
curl -s https://ivf.clinic/api/health | jq .

# 2. Test API
curl -s https://ivf.clinic/api/tenants/pricing | jq '.[] | .plan'

# 3. Test login
curl -s -X POST https://ivf.clinic/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}' | jq .token

# 4. Test WebSocket
# Mở browser → https://ivf.clinic → DevTools → Network → WS
# Verify SignalR connection established

# 5. Test MinIO
curl -s https://ivf.clinic/api/health | jq .storage

# 6. Database health
docker compose exec db pg_isready -U postgres
```

---

## 7. Tối ưu Hiệu suất

### 7.1 PostgreSQL Tuning

```ini
# postgresql.conf — Tối ưu cho 32GB RAM server

# ─── Memory ───
shared_buffers = 8GB                   # 25% of RAM
effective_cache_size = 24GB            # 75% of RAM
work_mem = 64MB                        # Per-operation sort/hash
maintenance_work_mem = 2GB             # VACUUM, CREATE INDEX
wal_buffers = 256MB                    # WAL write buffer
huge_pages = try                       # OS huge pages

# ─── CPU & Parallelism ───
max_worker_processes = 8
max_parallel_workers_per_gather = 4
max_parallel_workers = 8
max_parallel_maintenance_workers = 4

# ─── WAL & Checkpoints ───
wal_level = replica
max_wal_size = 4GB                     # Reduce checkpoint frequency
min_wal_size = 1GB
checkpoint_completion_target = 0.9
checkpoint_timeout = 15min

# ─── Connection Management ───
max_connections = 200                  # API pool: 100 + admin: 20 + replication: 5
superuser_reserved_connections = 3

# ─── Query Optimization ───
random_page_cost = 1.1                 # SSD optimization (default 4.0)
effective_io_concurrency = 200         # SSD concurrent reads
default_statistics_target = 200        # Better query plans

# ─── Autovacuum (aggressive) ───
autovacuum_max_workers = 4
autovacuum_naptime = 30s               # Check every 30s
autovacuum_vacuum_threshold = 50
autovacuum_analyze_threshold = 50
autovacuum_vacuum_scale_factor = 0.05  # 5% instead of 20%
autovacuum_analyze_scale_factor = 0.02

# ─── Logging ───
log_min_duration_statement = 200       # Log slow queries > 200ms
log_checkpoints = on
log_lock_waits = on
log_temp_files = 0                     # Log all temp file usage
```

**Mount vào container:**

```yaml
# docker-compose.production.yml
db:
  volumes:
    - ./docker/postgres/postgresql-production.conf:/etc/postgresql/postgresql.conf:ro
  command: postgres -c config_file=/etc/postgresql/postgresql.conf
```

### 7.2 PostgreSQL Indexes — Đã tối ưu

Hệ thống đã có sẵn các index tối ưu:

| Bảng                  | Index                                  | Mục đích                     |
| --------------------- | -------------------------------------- | ---------------------------- |
| Tất cả tenant tables  | `ix_{table}_tenant_id`                 | Row-level isolation O(log n) |
| `patients`            | `ix_patients_tenant_fullname`          | Tìm kiếm bệnh nhân           |
| `audit_logs`          | Partitioned by month                   | Phân vùng tự động            |
| `tenants`             | `ix_tenants_custom_domain` (filtered)  | Custom domain lookup         |
| `feature_definitions` | `ix_feature_definitions_code` (unique) | Feature gate O(1)            |

### 7.3 Connection Pooling

```
┌─────────────────────────────────────────────────────────────┐
│                Connection Pooling Stack                      │
│                                                             │
│  API Instance (Kestrel)                                     │
│  └─ EF Core DbContext (scoped per request)                  │
│     └─ Npgsql Connection Pool                               │
│        ├─ Min Pool Size: 10                                 │
│        ├─ Max Pool Size: 100                                │
│        ├─ Connection Idle Timeout: 300s                     │
│        ├─ Retry on Failure: 3 attempts                      │
│        └─ Connection Lifetime: 3600s (recycle)              │
│                                                             │
│  (Optional) PgBouncer — External connection pooler          │
│  └─ Transaction-level pooling                               │
│     ├─ max_client_conn: 1000                                │
│     ├─ default_pool_size: 50                                │
│     └─ reserve_pool_size: 10                                │
└─────────────────────────────────────────────────────────────┘
```

**PgBouncer (Optional — cho scale > 500 concurrent users):**

```yaml
# docker-compose.production.yml
pgbouncer:
  image: edoburu/pgbouncer:latest
  environment:
    - DATABASE_URL=postgres://postgres:${DB_PASSWORD}@db:5432/ivf_db
    - POOL_MODE=transaction
    - MAX_CLIENT_CONN=1000
    - DEFAULT_POOL_SIZE=50
    - RESERVE_POOL_SIZE=10
    - SERVER_IDLE_TIMEOUT=300
  ports:
    - "6432:6432"
  depends_on:
    db:
      condition: service_healthy
  networks:
    - ivf-data
```

### 7.4 Redis Tuning

```ini
# redis.conf
maxmemory 512mb
maxmemory-policy allkeys-lru          # Evict least recently used
tcp-keepalive 60
timeout 0
hz 100                                # Internal timer resolution
save ""                                # Disable RDB snapshots (cache-only)
appendonly no                          # Disable AOF (cache-only)

# Connection limits
maxclients 10000
tcp-backlog 511
```

### 7.5 .NET API Tuning

```csharp
// Program.cs — Production performance tuning

// 1. Kestrel server
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxConcurrentConnections = 1000;
    o.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB (medical images)
    o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    o.Limits.Http2.MaxStreamsPerConnection = 100;
});

// 2. Response Compression
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

// 3. Output Caching (API responses)
builder.Services.AddOutputCache(o =>
{
    o.AddPolicy("pricing", p => p.Expire(TimeSpan.FromMinutes(5)));
    o.AddPolicy("features", p => p.Expire(TimeSpan.FromMinutes(1)));
});

// 4. Response caching headers
// Static resources: Cache-Control: public, max-age=31536000
// API responses: Cache-Control: no-store (sensitive data)
// Pricing API: Cache-Control: public, max-age=300
```

### 7.6 Angular Frontend Optimization

```bash
# 1. Production build with AOT + tree shaking
npm run build -- --configuration production

# 2. Build output analysis
npx source-map-explorer dist/ivf-client/browser/*.js

# 3. Key optimizations (đã bật)
# ✅ Lazy loading: Tất cả feature modules (loadComponent/loadChildren)
# ✅ AOT compilation: Ahead-of-Time (production default)
# ✅ Tree shaking: Remove unused imports
# ✅ Code splitting: Per-route bundles
# ✅ Standalone components: Smaller bundles (no NgModule overhead)
```

**Caddy Caching cho Static Assets:**

```
# Caddyfile — tự động bật cho Angular hashed assets
@static path *.js *.css *.woff2 *.png *.jpg *.svg *.ico
header @static Cache-Control "public, max-age=31536000, immutable"

@html path *.html
header @html Cache-Control "no-cache, must-revalidate"
```

### 7.7 Tổng kết Tối ưu

| Lớp            | Tối ưu                                                 | Hiệu quả                       |
| -------------- | ------------------------------------------------------ | ------------------------------ |
| **Database**   | shared_buffers, SSD random_page_cost, parallel workers | Query 3-5x nhanh hơn           |
| **Connection** | Npgsql pool (100), PgBouncer cho scale                 | Giảm connection overhead 80%   |
| **Cache**      | Redis LRU 512MB, Output caching                        | API response < 5ms (cache hit) |
| **API**        | Kestrel tuning, response compression, HTTP/2           | Throughput tăng 2-3x           |
| **Frontend**   | Lazy loading, AOT, code splitting, immutable cache     | First paint < 2s               |
| **Network**    | Cloudflare CDN, Brotli compression                     | Latency giảm 50-70%            |

---

## 8. Observability & Truy vết Lỗi

### 8.1 Logging Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    Logging & Observability Stack                  │
│                                                                  │
│  ┌─────────────┐   ┌────────────────┐   ┌──────────────────┐   │
│  │ Serilog     │──→│ Console Sink   │   │ File Sink        │   │
│  │ Structured  │   │ (docker logs)  │   │ /var/log/ivf/    │   │
│  │ JSON        │   └────────┬───────┘   └────────┬─────────┘   │
│  └──────┬──────┘            │                     │             │
│         │                   ▼                     ▼             │
│         │            ┌──────────────────────────────────┐       │
│         │            │         Loki / ELK / Seq          │       │
│         │            │   Centralized Log Aggregation     │       │
│         │            └──────────────┬───────────────────┘       │
│         │                           │                           │
│         ▼                           ▼                           │
│  ┌──────────────┐   ┌──────────────────────────────┐           │
│  │ Seq Server   │   │ Grafana Dashboard             │           │
│  │ (structured  │   │ ├─ Request rate / latency     │           │
│  │  log search) │   │ ├─ Error rate by endpoint     │           │
│  │              │   │ ├─ DB query duration           │           │
│  │              │   │ ├─ Active connections          │           │
│  │              │   │ └─ Business metrics            │           │
│  └──────────────┘   └──────────────────────────────┘           │
│                                                                  │
│  ┌──────────────────────────────────────┐                       │
│  │ API Call Logging (built-in)          │                       │
│  │ ├─ Per-tenant API call tracking      │                       │
│  │ ├─ Method, Path, Status, Duration    │                       │
│  │ ├─ IP, UserAgent, UserId            │                       │
│  │ └─ Stored in PostgreSQL (queryable) │                       │
│  └──────────────────────────────────────┘                       │
└──────────────────────────────────────────────────────────────────┘
```

### 8.2 Thiết lập Serilog (Recommended)

```bash
# Thêm package Serilog
cd src/IVF.API
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Seq         # Optional: structured log server
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Thread
dotnet add package Serilog.Enrichers.ClientInfo
```

```json
// appsettings.Production.json — Serilog config
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "System.Net.Http.HttpClient": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/ivf/api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600,
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId",
      "WithClientIp"
    ]
  }
}
```

```csharp
// Program.cs — Add Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Middleware: Request logging (after UseRouting, before UseEndpoints)
app.UseSerilogRequestLogging(o =>
{
    o.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TenantId", httpContext.Items["TenantId"]?.ToString());
        diagnosticContext.Set("UserId", httpContext.User?.FindFirst("sub")?.Value);
    };
});
```

### 8.3 OpenTelemetry (Distributed Tracing)

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol  # For Jaeger/Tempo
```

```csharp
// Program.cs — OpenTelemetry setup
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("ivf-api"))
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
        .AddSource("IVF.Application") // Custom traces
        .AddOtlpExporter(o => o.Endpoint = new Uri("http://jaeger:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter("IVF.API") // Custom metrics
        .AddPrometheusExporter());

// Expose /metrics for Prometheus scraping
app.MapPrometheusScrapingEndpoint();
```

### 8.4 Prometheus + Grafana Stack

> **Lưu ý**: Cấu hình thực tế production đã được hardening bảo mật. Xem file `docker-compose.monitoring.yml` và `docs/infrastructure_operations_guide.md` để biết chi tiết.

```yaml
# docker-compose.monitoring.yml (simplified — see actual file for full config)
services:
  prometheus:
    image: prom/prometheus:latest
    container_name: ivf-prometheus
    volumes:
      - ./docker/monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ./docker/monitoring/alerts.yml:/etc/prometheus/alerts.yml:ro
      - ./docker/monitoring/prometheus-web.yml:/etc/prometheus/web.yml:ro
      - prometheus_data:/prometheus
    command:
      - "--config.file=/etc/prometheus/prometheus.yml"
      - "--storage.tsdb.retention.time=30d"
      - "--web.enable-lifecycle"
      - "--web.config.file=/etc/prometheus/web.yml"
      - "--web.external-url=https://natra.site/prometheus"
      - "--web.route-prefix=/prometheus"
    ports:
      - "127.0.0.1:9090:9090"  # localhost only
    security_opt:
      - no-new-privileges:true

  grafana:
    image: grafana/grafana:latest
    container_name: ivf-grafana
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=<strong-password>
      - GF_USERS_ALLOW_SIGN_UP=false
      - GF_AUTH_ANONYMOUS_ENABLED=false
      - GF_SERVER_ROOT_URL=https://natra.site/grafana/
      - GF_SERVER_SERVE_FROM_SUB_PATH=true
    volumes:
      - grafana_data:/var/lib/grafana
      - ./docker/monitoring/grafana/provisioning:/etc/grafana/provisioning:ro
    ports:
      - "127.0.0.1:3000:3000"  # localhost only
    security_opt:
      - no-new-privileges:true

  loki:
    image: grafana/loki:latest
    container_name: ivf-loki
    ports:
      - "127.0.0.1:3100:3100"  # localhost only
    security_opt:
      - no-new-privileges:true
```

**Truy cập external** (qua Caddy reverse proxy với basic auth):
- Grafana: `https://natra.site/grafana/`
- Prometheus: `https://natra.site/prometheus/`
- MinIO Console: SSH tunnel only (`ssh -L 9001:localhost:9001 root@VPS_IP`)

```yaml
# docker/monitoring/prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: "ivf-api"
    scrape_interval: 10s
    static_configs:
      - targets: ["ivf_api:8080"]
    metrics_path: /metrics

  - job_name: "caddy"
    static_configs:
      - targets: ["ivf_caddy:2019"]
    metrics_path: /metrics

  - job_name: "postgres"
    static_configs:
      - targets: ["ivf_postgres-exporter:9187"]

  - job_name: "redis"
    static_configs:
      - targets: ["ivf_redis-exporter:9121"]

  - job_name: "minio"
    static_configs:
      - targets: ["minio-metrics:9000"]
    metrics_path: /minio/v2/metrics/cluster

  - job_name: "prometheus"
    metrics_path: /prometheus/metrics
    basic_auth:
      username: monitor
      password: "<password>"
    static_configs:
      - targets: ["localhost:9090"]
```

### 8.5 Grafana Dashboards (Recommended)

| Dashboard        | Metrics                                                              | Alert Threshold              |
| ---------------- | -------------------------------------------------------------------- | ---------------------------- |
| **API Overview** | Request rate, error rate, latency p50/p95/p99                        | Error rate > 1%, p99 > 2s    |
| **Database**     | Active connections, query duration, cache hit ratio, replication lag | Lag > 30s, connections > 80% |
| **Redis**        | Hit rate, memory usage, evictions, connections                       | Hit rate < 80%, memory > 90% |
| **MinIO**        | Disk usage, request rate, errors                                     | Disk > 80%                   |
| **Tenant Usage** | API calls per tenant, active users, storage                          | Per-plan limit thresholds    |
| **Security**     | Failed logins, blocked IPs, rate limit hits                          | > 100 failed logins/hour     |
| **PKI**          | Signing operations, cert expiry countdown                            | Cert expiry < 30 days        |

### 8.6 Health Check Endpoints (Recommended)

```csharp
// Program.cs — Comprehensive health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["db", "ready"])
    .AddRedis(redisConnection, name: "redis", tags: ["cache", "ready"])
    .AddUrlGroup(new Uri("http://minio:9000/minio/health/live"), "minio", tags: ["storage", "ready"])
    .AddCheck("ejbca", () =>
    {
        // Check EJBCA availability
        return HealthCheckResult.Healthy();
    }, tags: ["pki"])
    .AddCheck("signserver", () =>
    {
        // Check SignServer availability
        return HealthCheckResult.Healthy();
    }, tags: ["pki"]);

// Endpoints
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }); // Liveness
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") }); // Readiness
app.MapHealthChecks("/health", new()
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse // Detailed JSON
});
```

### 8.7 Truy vết Lỗi (Error Tracing)

**Đã có sẵn:**

| Cơ chế                | Dữ liệu                                                         | Truy vấn                                                                        |
| --------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| **API Call Logging**  | Method, Path, Status, Duration, IP, UserAgent, UserId, TenantId | `SELECT * FROM api_call_logs WHERE status_code >= 500 ORDER BY created_at DESC` |
| **Audit Log**         | Partitioned by month, UserId, Action, Entity, TenantId          | `SELECT * FROM audit_logs WHERE action = 'Error' AND tenant_id = ?`             |
| **Exception Handler** | Validation, TenantLimit, FeatureNotEnabled → structured JSON    | Client receives structured error codes                                          |
| **SecurityEvent**     | IP, geo, risk level, incident type                              | `SELECT * FROM security_events WHERE severity = 'Critical'`                     |

**Recommended thêm:**

```csharp
// Correlation ID middleware — thêm trace ID vào mọi request
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                       ?? Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

**Quy trình truy vết lỗi:**

```
1. User báo lỗi → Lấy Correlation ID từ response header
   ↓
2. Tìm trong API Call Logs:
   SELECT * FROM api_call_logs WHERE correlation_id = '{id}';
   → Biết: endpoint, status_code, duration, tenant_id, user_id
   ↓
3. Tìm trong Serilog (JSON):
   grep "CorrelationId.*{id}" /var/log/ivf/api-*.log | jq .
   → Biết: full stack trace, context
   ↓
4. Tìm trong Jaeger (distributed tracing):
   http://jaeger:16686 → Search by trace ID
   → Biết: timing breakdown, DB queries, external calls
   ↓
5. Tìm trong Audit Log:
   SELECT * FROM audit_logs WHERE correlation_id = '{id}';
   → Biết: business action, before/after data
```

---

## 9. Khả năng Chịu lỗi & Phục hồi

### 9.1 Fault Tolerance Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                    Fault Tolerance Layers                          │
│                                                                    │
│  Layer 1: Load Balancer                                           │
│  ├─ Active health checks (10s interval)                           │
│  ├─ Auto-remove unhealthy backends                                │
│  └─ Session affinity for WebSocket (SignalR)                      │
│                                                                    │
│  Layer 2: Application (API)                                       │
│  ├─ Graceful shutdown (SIGTERM → drain connections → exit)        │
│  ├─ Circuit breaker for external services (SignServer, MinIO)     │
│  ├─ Retry with exponential backoff (DB, Redis, MinIO)            │
│  ├─ Redis fallback: degrade gracefully if unavailable            │
│  ├─ Rate limiting: protect against traffic spikes                │
│  └─ Exception handler: never crash, always return structured err │
│                                                                    │
│  Layer 3: Database (PostgreSQL)                                   │
│  ├─ Streaming replication (Primary → Standby → Cloud Replica)    │
│  ├─ Auto-failover via Patroni/repmgr                             │
│  ├─ WAL archiving for point-in-time recovery (PITR)              │
│  ├─ Connection retry on failure (3 attempts)                     │
│  └─ Partitioned tables (audit_logs) for write performance        │
│                                                                    │
│  Layer 4: Storage (MinIO)                                         │
│  ├─ Site replication (Primary ↔ Cloud Replica)                    │
│  ├─ Erasure coding (data protection)                             │
│  └─ Bucket versioning (accidental delete protection)             │
│                                                                    │
│  Layer 5: Infrastructure                                          │
│  ├─ Docker restart: unless-stopped (auto-restart on crash)       │
│  ├─ Container health checks (pg_isready, curl, wget)             │
│  ├─ Resource limits (prevent OOM killing other containers)       │
│  └─ Network isolation (compromised container limited blast radius)│
└────────────────────────────────────────────────────────────────────┘
```

### 9.2 Database Failover

#### Phương án 1: Patroni (Recommended cho SLA 99.99%)

```yaml
# docker-compose.ha.yml — PostgreSQL HA with Patroni
services:
  patroni1:
    image: patroni/patroni:latest
    environment:
      - PATRONI_NAME=pg1
      - PATRONI_SCOPE=ivf-cluster
      - PATRONI_POSTGRESQL_DATA_DIR=/var/lib/postgresql/data
      - PATRONI_REPLICATION_USERNAME=replicator
      - PATRONI_REPLICATION_PASSWORD=${REPL_PASSWORD}
      - PATRONI_SUPERUSER_USERNAME=postgres
      - PATRONI_SUPERUSER_PASSWORD=${DB_PASSWORD}
      - PATRONI_ETCD_URL=http://etcd:2379
    volumes:
      - patroni1_data:/var/lib/postgresql/data

  patroni2:
    image: patroni/patroni:latest
    environment:
      - PATRONI_NAME=pg2
      # ... same config, different name

  etcd:
    image: quay.io/coreos/etcd:v3.5
    command: etcd --name etcd1 --initial-cluster etcd1=http://etcd:2380
```

**Failover timeline:**

- Detection: 10-30 giây (Patroni health check interval)
- Promotion: 5-10 giây (standby → primary)
- DNS update: 0 giây (HAProxy re-routes)
- **Total downtime: 15-40 giây**

#### Phương án 2: Manual Failover (hiện tại)

```bash
# === Khi Primary DB chết ===

# 1. Promote standby thành primary
docker compose exec db-standby pg_ctl promote -D /var/lib/postgresql/data

# 2. Verify
docker compose exec db-standby psql -U postgres -c "SELECT pg_is_in_recovery();"
# false = đã thành primary

# 3. Cập nhật connection string
# Sửa .env hoặc appsettings.json: Host=db-standby

# 4. Restart API
docker compose restart api

# 5. Tạo standby mới từ primary mới
# ... pg_basebackup từ db-standby
```

### 9.3 Backup & Recovery Strategy

```
┌──────────────────────────────────────────────────────────────────┐
│                    Backup Strategy (3-2-1 Rule)                   │
│                                                                   │
│  3 bản copy:  1. Primary DB  2. Local backup  3. Cloud backup    │
│  2 loại media: 1. Local disk  2. Cloud (S3/Azure/GCS)            │
│  1 bản off-site: Cloud Replica                                   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  Automated Backups (BackupSchedulerService)                  │ │
│  │                                                               │ │
│  │  ┌─── Daily Full Backup ──────────────────────────────────┐ │ │
│  │  │  Schedule: 02:00 UTC daily (cron: 0 2 * * *)           │ │ │
│  │  │  Retention: 90 days, max 30 backups                    │ │ │
│  │  │  Method: pg_dump → gzip → SHA256 checksum              │ │ │
│  │  │  Storage: local + cloud (S3/Azure/GCS/MinIO)           │ │ │
│  │  └────────────────────────────────────────────────────────┘ │ │
│  │                                                               │ │
│  │  ┌─── Continuous WAL Archiving ───────────────────────────┐ │ │
│  │  │  Mode: Streaming replication + WAL archiving            │ │ │
│  │  │  Archive: /var/lib/postgresql/archive/                  │ │ │
│  │  │  Enables: Point-in-Time Recovery (PITR)                │ │ │
│  │  │  WAL keep size: 256 MB                                 │ │ │
│  │  └────────────────────────────────────────────────────────┘ │ │
│  │                                                               │ │
│  │  ┌─── MinIO (Object Storage) Backup ──────────────────────┐ │ │
│  │  │  Method: mc mirror (site replication)                   │ │ │
│  │  │  Buckets: ivf-documents, ivf-signed-pdfs, ivf-medical  │ │ │
│  │  │  Versioning: enabled (accidental delete protection)    │ │ │
│  │  └────────────────────────────────────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

### 9.4 Point-in-Time Recovery (PITR)

```bash
# Khôi phục database đến thời điểm cụ thể
# Ví dụ: Khôi phục đến 14:30:00 ngày 05/03/2026

# 1. Dừng PostgreSQL hiện tại
docker compose stop db

# 2. Chạy script PITR
./scripts/restore-pitr.sh "2026-03-05 14:30:00+07"

# Script sẽ:
# - Tìm base backup gần nhất trước thời điểm target
# - Restore base backup
# - Replay WAL archives đến đúng thời điểm
# - Khởi động PostgreSQL ở recovery mode

# 3. Verify data
docker compose exec db psql -U postgres ivf_db -c "SELECT max(created_at) FROM patients;"
# Phải <= 2026-03-05 14:30:00

# 4. Restart services
docker compose up -d
```

### 9.5 Disaster Recovery Plan

| Scenario                   | RTO    | RPO    | Phương án                                         |
| -------------------------- | ------ | ------ | ------------------------------------------------- |
| **API crash**              | 0s     | 0      | Docker auto-restart                               |
| **Single container OOM**   | 10s    | 0      | Docker restart + resource limits                  |
| **Primary DB failure**     | 15-40s | 0      | Patroni auto-failover hoặc manual promote standby |
| **Disk corruption**        | 30 min | ~5 min | PITR từ WAL archive                               |
| **Ransomware / data loss** | 1h     | 24h    | Restore từ daily backup                           |
| **Full node failure**      | 5 min  | 0      | LB routes to Node B + promote standby             |
| **Datacenter outage**      | 30 min | ~1 min | Cloud Replica promotion + DNS failover            |
| **Region-wide disaster**   | 2h     | 24h    | Cloud backup restore trên cloud mới               |

### 9.6 Redis Failover

```
┌─────────────────────────────────────────────┐
│  Redis Sentinel (HA mode)                    │
│                                              │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐     │
│  │Sentinel1│  │Sentinel2│  │Sentinel3│     │
│  └────┬────┘  └────┬────┘  └────┬────┘     │
│       │            │            │           │
│  ┌────▼────┐  ┌────▼────┐                  │
│  │ Master  │→→│ Replica │                  │
│  │ (R/W)   │  │ (R/O)   │                  │
│  └─────────┘  └─────────┘                  │
│                                              │
│  Failover: 5-10 giây (Sentinel quorum vote)  │
└─────────────────────────────────────────────┘
```

**Lưu ý:** IVF API đã có Redis fallback — nếu Redis unavailable, hệ thống tiếp tục hoạt động (chỉ mất cache, không crash).

---

## 10. Kiến trúc SLA 99.99%

### 10.1 SLA 99.99% = Tối đa 52.6 phút downtime/năm

| SLA        | Downtime/năm | Downtime/tháng | Downtime/ngày |
| ---------- | ------------ | -------------- | ------------- |
| 99.9%      | 8h 45m       | 43m            | 1m 26s        |
| **99.99%** | **52.6m**    | **4.3m**       | **8.6s**      |
| 99.999%    | 5.3m         | 26s            | 0.86s         |

### 10.2 Yêu cầu bắt buộc cho SLA 99.99%

```
┌──────────────────────────────────────────────────────────────────┐
│                SLA 99.99% Requirements                            │
│                                                                   │
│  ✅ Đã có trong IVF Platform:                                    │
│  ├─ PostgreSQL streaming replication (Primary + Standby)         │
│  ├─ Cloud replica cho DR                                         │
│  ├─ Docker auto-restart (unless-stopped)                         │
│  ├─ Container health checks                                      │
│  ├─ Network isolation (blast radius)                              │
│  ├─ WAL archiving + PITR                                         │
│  ├─ Automated daily backup (90-day retention)                    │
│  ├─ Redis graceful fallback                                       │
│  ├─ Rate limiting (4 tiers)                                      │
│  ├─ Security scanning (SAST, SCA, Container, DAST)               │
│  ├─ CI/CD pipeline (GitHub Actions)                              │
│  └─ MinIO site replication                                       │
│                                                                   │
│  🔲 Cần thêm:                                                    │
│  ├─ Load Balancer (HAProxy / Cloud LB)                           │
│  ├─ Multi-node API (≥ 2 instances)                               │
│  ├─ Patroni auto-failover cho PostgreSQL                         │
│  ├─ Redis Sentinel (3 sentinels)                                 │
│  ├─ Centralized logging (Loki/ELK)                               │
│  ├─ Metrics & alerting (Prometheus + Grafana + PagerDuty)        │
│  ├─ Distributed tracing (Jaeger/Tempo)                           │
│  ├─ Automated incident response                                 │
│  ├─ Chaos engineering (định kỳ)                                   │
│  └─ Runbook automation                                           │
└──────────────────────────────────────────────────────────────────┘
```

### 10.3 Multi-Instance API Deployment

```yaml
# docker-compose.ha.yml — Multi-instance API
services:
  api-1:
    build:
      context: .
      dockerfile: src/IVF.API/Dockerfile
    container_name: ivf-api-1
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - INSTANCE_ID=api-1
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: "2"
    networks:
      - ivf-public
      - ivf-signing
      - ivf-data

  api-2:
    build:
      context: .
      dockerfile: src/IVF.API/Dockerfile
    container_name: ivf-api-2
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - INSTANCE_ID=api-2
    deploy:
      resources:
        limits:
          memory: 1G
          cpus: "2"
    networks:
      - ivf-public
      - ivf-signing
      - ivf-data
```

**Caddy upstream cho multiple API instances:**

```
# Caddyfile — Load balancing giữa API instances
(api_upstream) {
    reverse_proxy api-1:8080 api-2:8080 {
        lb_policy       round_robin
        health_uri      /health/live
        health_interval 10s
        health_timeout  5s
        fail_duration   30s
    }
}
```

### 10.4 HAProxy Configuration (External LB)

```
# /etc/haproxy/haproxy.cfg
global
    maxconn 10000
    log stdout format raw local0

defaults
    mode http
    timeout connect 5s
    timeout client  30s
    timeout server  30s
    option httpchk GET /health/live
    retries 3

frontend https_in
    bind *:443 ssl crt /etc/ssl/certs/ivf.pem
    default_backend api_servers

    # WebSocket upgrade
    acl is_websocket hdr(Upgrade) -i WebSocket
    use_backend ws_servers if is_websocket

backend api_servers
    balance roundrobin
    option httpchk GET /health/ready HTTP/1.1\r\nHost:\ ivf.clinic
    server api1 node-a:8080 check inter 10s fall 3 rise 2 maxconn 500
    server api2 node-b:8080 check inter 10s fall 3 rise 2 maxconn 500

backend ws_servers
    balance source    # Sticky sessions for SignalR
    server api1 node-a:8080 check
    server api2 node-b:8080 check

listen stats
    bind *:8404
    stats enable
    stats uri /stats
    stats refresh 5s
```

### 10.5 Zero-Downtime Deployment (Rolling Update)

```bash
#!/bin/bash
# deploy.sh — Zero-downtime deployment script

set -e

echo "=== IVF Zero-Downtime Deployment ==="

# 1. Pull latest code
git pull origin main

# 2. Build new image
docker compose build api

# 3. Build frontend
cd ivf-client && npm ci && npm run build && cd ..

# 4. Rolling update: API instance 1
echo ">>> Updating api-1..."
docker compose stop api-1

# Chờ LB detect unhealthy (drain connections)
sleep 15

docker compose up -d api-1

# Chờ api-1 healthy
until docker compose exec api-1 wget -q -O- http://localhost:8080/health/live > /dev/null 2>&1; do
    echo "Waiting for api-1 to be healthy..."
    sleep 5
done
echo "api-1 is healthy ✅"

# 5. Rolling update: API instance 2
echo ">>> Updating api-2..."
docker compose stop api-2
sleep 15
docker compose up -d api-2

until docker compose exec api-2 wget -q -O- http://localhost:8080/health/live > /dev/null 2>&1; do
    echo "Waiting for api-2 to be healthy..."
    sleep 5
done
echo "api-2 is healthy ✅"

echo "=== Deployment Complete — Zero Downtime ==="
```

### 10.6 Alerting Rules

```yaml
# docker/prometheus/alerts.yml
groups:
  - name: ivf-critical
    rules:
      - alert: APIDown
        expr: up{job="ivf-api"} == 0
        for: 30s
        labels:
          severity: critical
        annotations:
          summary: "IVF API instance {{ $labels.instance }} is down"

      - alert: HighErrorRate
        expr: rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]) / rate(http_server_request_duration_seconds_count[5m]) > 0.01
        for: 2m
        labels:
          severity: critical
        annotations:
          summary: "Error rate > 1% for 2 minutes"

      - alert: HighLatency
        expr: histogram_quantile(0.99, rate(http_server_request_duration_seconds_bucket[5m])) > 2
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "p99 latency > 2 seconds"

      - alert: DatabaseReplicationLag
        expr: pg_replication_lag > 30
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL replication lag > 30 seconds"

      - alert: DiskSpaceLow
        expr: node_filesystem_avail_bytes{mountpoint="/"} / node_filesystem_size_bytes{mountpoint="/"} < 0.15
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Disk space < 15%"

      - alert: CertificateExpiringSoon
        expr: ivf_certificate_expiry_days < 30
        for: 1h
        labels:
          severity: warning
        annotations:
          summary: "SSL certificate expires in {{ $value }} days"

      - alert: TenantLimitApproaching
        expr: ivf_tenant_usage_ratio > 0.9
        for: 1h
        labels:
          severity: info
        annotations:
          summary: "Tenant {{ $labels.tenant }} at 90%+ of plan limits"
```

---

## 11. Xử lý Sự cố (Incident Response)

### 11.1 Severity Levels

| Level                | Mô tả                      | Response Time | Resolution Target |
| -------------------- | -------------------------- | ------------- | ----------------- |
| **SEV-1 (Critical)** | Hệ thống down, mất dữ liệu | **5 phút**    | 30 phút           |
| **SEV-2 (Major)**    | Feature chính bị ảnh hưởng | **15 phút**   | 2 giờ             |
| **SEV-3 (Minor)**    | Feature phụ bị ảnh hưởng   | **1 giờ**     | 8 giờ             |
| **SEV-4 (Low)**      | UI/cosmetic issues         | **4 giờ**     | Theo sprint       |

### 11.2 Runbook: Các Sự cố Thường gặp

#### 10.2.1 API không phản hồi (SEV-1)

```bash
# 1. Kiểm tra container status
docker compose ps api

# 2. Xem logs
docker compose logs --tail=100 api

# 3. Kiểm tra resource usage
docker stats --no-stream ivf-api

# 4. Restart nếu cần
docker compose restart api

# 5. Nếu restart không giúp — kiểm tra dependencies
docker compose exec api wget -q -O- http://db:5432 2>&1 || echo "DB unreachable"
docker compose exec api wget -q -O- http://redis:6379 2>&1 || echo "Redis unreachable"

# 6. Rollback nếu deploy gần đây gây lỗi
git log --oneline -5
git revert HEAD
docker compose build api && docker compose up -d api
```

#### 10.2.2 Database không connect được (SEV-1)

```bash
# 1. Kiểm tra container
docker compose ps db
docker compose logs --tail=50 db

# 2. Kiểm tra disk space
docker compose exec db df -h /var/lib/postgresql/data

# 3. Kiểm tra connections
docker compose exec db psql -U postgres -c "SELECT count(*) FROM pg_stat_activity;"
docker compose exec db psql -U postgres -c "SELECT state, count(*) FROM pg_stat_activity GROUP BY state;"

# 4. Kill idle connections nếu max_connections đầy
docker compose exec db psql -U postgres -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE state = 'idle' AND query_start < now() - interval '5 minutes';"

# 5. Nếu DB corrupted — failover to standby
docker compose exec db-standby pg_ctl promote -D /var/lib/postgresql/data
# Cập nhật connection string → db-standby
```

#### 10.2.3 Hết disk space (SEV-2)

```bash
# 1. Kiểm tra disk
df -h /

# 2. Tìm file lớn
du -sh /var/lib/docker/volumes/* | sort -rh | head -10

# 3. Dọn Docker resources
docker system prune -f                    # Unused containers, networks, images
docker volume prune -f                     # Unused volumes (CẨN THẬN!)

# 4. Dọn WAL archive cũ
docker compose exec db find /var/lib/postgresql/archive -mtime +7 -delete

# 5. Dọn logs cũ
find /var/log/ivf/ -name "*.log" -mtime +30 -delete

# 6. VACUUM database
docker compose exec db psql -U postgres ivf_db -c "VACUUM FULL;"
```

#### 10.2.4 Memory leak / OOM (SEV-2)

```bash
# 1. Xem memory usage
docker stats --no-stream

# 2. Kiểm tra .NET memory
docker compose exec api dotnet-counters monitor --process-id 1 \
  --counters System.Runtime[gc-heap-size,gen-0-gc-count,gen-1-gc-count,gen-2-gc-count]

# 3. Tạo memory dump (nếu cần phân tích)
docker compose exec api dotnet-dump collect --process-id 1 --output /tmp/dump.dmp

# 4. Restart với resource limit
docker compose restart api
# Resource limits trong docker-compose.production.yml ngăn OOM ảnh hưởng containers khác
```

#### 10.2.5 SSL certificate hết hạn (SEV-2)

```bash
# Caddy tự động renew Let's Encrypt certificates
# Nếu renewal thất bại:

# 1. Kiểm tra Caddy logs
docker compose logs caddy | grep -i "certificate\|acme\|tls"

# 2. Force renewal
docker compose exec caddy caddy reload --config /etc/caddy/Caddyfile

# 3. Nếu ACME bị block — kiểm tra rate limit
# Let's Encrypt rate limits: 50 certs/domain/week
# Giải pháp: chờ hoặc dùng staging endpoint

# 4. Manual cert (emergency)
# Sử dụng cert từ provider khác, mount vào Caddy
```

#### 10.2.6 Replication lag cao (SEV-3)

```bash
# 1. Kiểm tra replication status
docker compose exec db psql -U postgres -c \
  "SELECT pid, state, client_addr, sent_lsn, replay_lsn,
          pg_wal_lsn_diff(sent_lsn, replay_lsn) AS lag_bytes
   FROM pg_stat_replication;"

# 2. Nguyên nhân thường gặp:
# - Network latency giữa primary và standby
# - Standby disk I/O chậm
# - Long-running transaction trên primary

# 3. Kiểm tra long-running queries
docker compose exec db psql -U postgres -c \
  "SELECT pid, now() - query_start AS duration, query
   FROM pg_stat_activity
   WHERE state = 'active' AND now() - query_start > interval '1 minute'
   ORDER BY duration DESC;"

# 4. Kill long queries nếu cần
docker compose exec db psql -U postgres -c "SELECT pg_terminate_backend({pid});"
```

### 11.3 Post-Incident Review

```
Sau mỗi SEV-1 hoặc SEV-2, thực hiện Post-Mortem:

1. Timeline
   - Khi nào phát hiện?
   - Khi nào bắt đầu xử lý?
   - Khi nào khôi phục?

2. Root Cause Analysis (5 Whys)
   - Tại sao xảy ra?
   - Tại sao không phát hiện sớm hơn?
   - Tại sao monitoring không alert?

3. Impact
   - Bao nhiêu tenant bị ảnh hưởng?
   - Bao nhiêu minutes downtime?
   - Dữ liệu bị mất? (nếu có)

4. Action Items
   - Preventive: Ngăn ngừa tái diễn
   - Detective: Cải thiện monitoring/alerting
   - Process: Cập nhật runbook
```

---

## 12. Bảo trì Định kỳ

### 12.1 Daily (Tự động)

| Task                       | Schedule        | Service                           |
| -------------------------- | --------------- | --------------------------------- |
| Database backup (full)     | 02:00 UTC       | `BackupSchedulerService`          |
| WAL archiving              | Continuous      | PostgreSQL                        |
| Certificate expiry check   | Startup + daily | `CertificateExpiryMonitorService` |
| Partition maintenance      | Daily           | `PartitionMaintenanceService`     |
| Data retention enforcement | Daily           | `DataRetentionService`            |
| Vault lease maintenance    | Periodic        | `VaultLeaseMaintenanceService`    |
| MinIO replication sync     | Continuous      | CloudReplicationScheduler         |

### 12.2 Weekly

```bash
# 1. Kiểm tra disk usage
df -h / && docker system df

# 2. Kiểm tra backup integrity
ls -la backups/*.sha256
# Verify checksums
for f in backups/*.gz; do sha256sum -c "${f}.sha256"; done

# 3. Kiểm tra replication health
docker compose exec db psql -U postgres -c \
  "SELECT * FROM pg_stat_replication;"

# 4. Review security events
docker compose exec db psql -U postgres ivf_db -c \
  "SELECT severity, count(*) FROM security_events
   WHERE created_at > now() - interval '7 days'
   GROUP BY severity ORDER BY count DESC;"

# 5. Dọn Docker images cũ
docker image prune -f --filter "until=168h"

# 6. Review error logs
grep -c "error\|Error\|ERROR" /var/log/ivf/api-$(date +%Y%m%d).log
```

### 12.3 Monthly

```bash
# 1. VACUUM ANALYZE (toàn database)
docker compose exec db psql -U postgres ivf_db -c "VACUUM ANALYZE;"

# 2. Reindex (nếu bloat > 30%)
docker compose exec db psql -U postgres ivf_db -c \
  "SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename))
   FROM pg_tables WHERE schemaname = 'public' ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC LIMIT 20;"

# 3. Update Docker images (security patches)
docker compose pull
docker compose -f docker-compose.yml -f docker-compose.production.yml up -d

# 4. Test backup restore (CRITICAL!)
# Restore backup lên server test → verify data → destroy
docker compose exec db pg_restore -U postgres -d ivf_db_test /backups/latest.dump

# 5. Review và rotate secrets
# Rotate JWT secret, DB passwords, API keys mỗi 90 ngày

# 6. Security scan
docker compose --profile security-scan up trivy-scan

# 7. Kiểm tra certificate expiry
./scripts/rotate-certs.sh --check
```

### 12.4 Quarterly

```bash
# 1. Load testing
# Dùng k6, Gatling, hoặc JMeter
k6 run --vus 100 --duration 10m load-test.js

# 2. Disaster recovery drill
# Simulate: primary DB failure → failover → restore
# Document: thời gian failover thực tế

# 3. Penetration testing
./scripts/pentest.sh

# 4. Review & update documentation
# Cập nhật runbooks, deployment guide

# 5. Capacity planning
# Analyze growth trends: users, storage, API calls
```

---

## 13. Checklist Triển khai

### 13.1 Pre-Deployment Checklist

```
□ Server đáp ứng yêu cầu hardware (vCPU, RAM, disk)
□ Docker + Docker Compose V2 đã cài
□ Kernel parameters đã tối ưu (sysctl)
□ File descriptor limits đã tăng
□ Firewall: chỉ mở port 80, 443
□ DNS records đã cấu hình (A + wildcard CNAME)
□ Docker secrets đã tạo (10 files)
□ .env file đã cấu hình
□ SSL certificate hoặc Let's Encrypt ACME ready
□ Angular frontend đã build production
```

### 13.2 Post-Deployment Checklist

```
□ docker compose ps — tất cả containers Running + Healthy
□ HTTPS hoạt động (curl -I https://domain/api/health)
□ Login hoạt động (admin/Admin@123 → change immediately!)
□ SignalR WebSocket connected
□ MinIO buckets created (3 buckets)
□ Database migration completed (check logs)
□ PKI/SignServer initialized (nếu sử dụng ký số)
□ Streaming replication hoạt động (pg_stat_replication)
□ Backup schedule đã chạy (kiểm tra backup folder)
□ Monitoring stack hoạt động (Prometheus/Grafana)
□ Alerting rules configured (PagerDuty/SMS/Email)
□ Change default admin password!
□ Tạo tenant đầu tiên (test CRUD)
```

### 13.3 Security Hardening Checklist

```
□ Đổi tất cả default passwords (admin, DB, MinIO, Redis)
□ Docker secrets thay vì environment variables
□ Port DB, Redis, EJBCA không expose ra public
□ MinIO console chỉ localhost (127.0.0.1:9001)
□ ASPNETCORE_ENVIRONMENT=Production
□ TLS enforced (SkipTlsValidation=false)
□ mTLS cho SignServer (RequireMtls=true)
□ Rate limiting enabled (4 tiers)
□ CORS restricted (không wildcard origin)
□ Security headers (HSTS, CSP, X-Frame-Options)
□ Trivy scan passed (no HIGH/CRITICAL vulnerabilities)
□ Gitleaks scan passed (no secrets in code)
□ CodeQL scan passed (no security findings)
□ OWASP ZAP scan passed (no high-risk findings)
□ Network isolation enforced (signing + data = internal)
□ Container security: no-new-privileges, non-root user
□ WAF enabled (Cloudflare / ModSecurity)
```

### 13.4 Scale-up Decision Matrix

| Metric                   | Threshold        | Action                                    |
| ------------------------ | ---------------- | ----------------------------------------- |
| API response p95 > 500ms | Sustained 5 min  | Scale API instances                       |
| CPU usage > 80%          | Sustained 10 min | Add vCPU or instance                      |
| Memory usage > 85%       | Sustained 10 min | Add RAM or instance                       |
| DB connections > 80% max | Sustained 5 min  | Add PgBouncer or increase max_connections |
| Disk usage > 80%         | Any              | Expand disk or cleanup                    |
| Replication lag > 60s    | Sustained 5 min  | Check network/disk I/O                    |
| Redis memory > 90%       | Any              | Increase maxmemory or add cluster node    |
| SSL cert expiry < 30d    | Any              | Force renewal                             |
| Backup age > 48h         | Any              | Investigate backup scheduler              |

---

## Phụ lục A: Quick Reference Commands

```bash
# ─── Deployment ───
docker compose -f docker-compose.yml -f docker-compose.production.yml up -d
docker compose -f docker-compose.yml -f docker-compose.production.yml down

# ─── Logs ───
docker compose logs -f api --tail=100
docker compose logs -f --since=1h

# ─── Health ───
docker compose ps
docker stats --no-stream
curl -s http://localhost:8080/health | jq .

# ─── Database ───
docker compose exec db psql -U postgres ivf_db
docker compose exec db pg_isready
docker compose exec db psql -U postgres -c "SELECT * FROM pg_stat_replication;"

# ─── Backup ───
docker compose exec db pg_dump -U postgres ivf_db | gzip > backup_$(date +%Y%m%d).sql.gz

# ─── Replication ───
docker compose --profile replication up -d db-standby
docker compose exec db-standby psql -U postgres -c "SELECT pg_is_in_recovery();"

# ─── Security Scan ───
docker compose --profile security-scan up trivy-scan

# ─── Certificate ───
docker compose exec caddy caddy reload --config /etc/caddy/Caddyfile
openssl s_client -connect ivf.clinic:443 -servername ivf.clinic </dev/null 2>/dev/null | openssl x509 -noout -dates

# ─── Emergency Rollback ───
git revert HEAD && docker compose build api && docker compose up -d api
```

## Phụ lục B: Biến môi trường Production

| Variable                   | Mô tả                            | Ví dụ                           |
| -------------------------- | -------------------------------- | ------------------------------- |
| `ASPNETCORE_ENVIRONMENT`   | Environment                      | `Production`                    |
| `POSTGRES_PASSWORD_FILE`   | DB password (Docker secret)      | `/run/secrets/ivf_db_password`  |
| `JWT_SECRET_FILE`          | JWT signing key                  | `/run/secrets/jwt_secret`       |
| `MINIO_ROOT_USER_FILE`     | MinIO access key                 | `/run/secrets/minio_access_key` |
| `MINIO_ROOT_PASSWORD_FILE` | MinIO secret key                 | `/run/secrets/minio_secret_key` |
| `ACME_EMAIL`               | Let's Encrypt ACME email         | `admin@ivf.clinic`              |
| `API_UPSTREAM`             | API backend for Caddy            | `api:8080`                      |
| `PRIMARY_HOST`             | PostgreSQL primary (for replica) | `primary-server.example.com`    |
| `REPLICATOR_PASSWORD`      | Replication user password        | (from secret)                   |

## Phụ lục C: Port Reference

| Port | Service            | Internal/External | Production        |
| ---- | ------------------ | ----------------- | ----------------- |
| 80   | Caddy (HTTP→HTTPS) | External          | ✅                |
| 443  | Caddy (HTTPS)      | External          | ✅                |
| 5432 | PostgreSQL         | Internal only     | ❌ (no host port) |
| 6379 | Redis              | Internal only     | ❌ (no host port) |
| 8080 | API (Kestrel)      | Internal only     | ❌ (via Caddy)    |
| 8443 | EJBCA              | Internal only     | ❌                |
| 9000 | MinIO (API)        | Internal only     | ❌                |
| 9001 | MinIO (Console)    | localhost only    | 127.0.0.1:9001    |
| 9443 | SignServer         | Internal only     | ❌                |
| 2019 | Caddy Admin        | Internal only     | ❌                |

---

_Tài liệu này được cập nhật: 2026-03-08 — Phiên bản: 4.0_
_Áp dụng cho: IVF Platform v5.0+_
