# IVF Platform — CI/CD & Deploy Pipeline

> **Tài liệu hướng dẫn CI/CD toàn diện — từ commit đến production**
>
> Phiên bản: 1.0 | Cập nhật: 2026-03-06
>
> Áp dụng: IVF Platform v5.0+ | .NET 10 | Angular 21 | Docker Swarm

---

## Mục lục

1. [Tổng quan Pipeline](#1-tổng-quan-pipeline)
2. [Branching Strategy](#2-branching-strategy)
3. [Environments](#3-environments)
4. [GitHub Actions Workflows](#4-github-actions-workflows)
5. [Docker Images](#5-docker-images)
6. [Deploy tự động lên Swarm](#6-deploy-tự-động-lên-swarm)
7. [Rollback](#7-rollback)
8. [Database Migrations](#8-database-migrations)
9. [Health Check & Smoke Test](#9-health-check--smoke-test)
10. [Security Scanning](#10-security-scanning)
11. [Monitoring & Notifications](#11-monitoring--notifications)
12. [Secrets Management](#12-secrets-management)
13. [Troubleshooting](#13-troubleshooting)
14. [Checklist Release](#14-checklist-release)

---

## 1. Tổng quan Pipeline

### 1.1 Luồng CI/CD End-to-End

```
Developer Push
    │
    ▼
┌──────────────────────────────────────────────────────────────────────┐
│                      GitHub Actions CI                               │
│                                                                      │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌───────────┐            │
│  │ Restore │→│  Build   │→│  Test    │→│ Lint/Fmt  │            │
│  │ deps    │  │ backend  │  │ xUnit   │  │ dotnet-   │            │
│  │         │  │ frontend │  │ vitest  │  │ format    │            │
│  └─────────┘  └──────────┘  └──────────┘  └───────────┘            │
│                                                                      │
│  ┌──────────────────────┐  ┌──────────────────────────┐             │
│  │  Security Scanning   │  │  Docker Build & Push     │             │
│  │  • CodeQL (SAST)     │  │  • API image → GHCR      │             │
│  │  • Trivy (Container) │  │  • Frontend image → GHCR  │             │
│  │  • Gitleaks (Secret) │  │  • Multi-arch (amd64)    │             │
│  │  • npm audit (SCA)   │  │  • Layer caching         │             │
│  └──────────────────────┘  └──────────────────────────┘             │
└──────────────────────────────────┬───────────────────────────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              │                    │                    │
              ▼                    ▼                    ▼
    ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
    │   PR Check      │  │   Staging       │  │   Production    │
    │                 │  │                 │  │                 │
    │ • Build verify  │  │ • Auto deploy   │  │ • Manual approve│
    │ • Test pass     │  │   on develop    │  │   on main/tag   │
    │ • Comment on PR │  │ • Smoke test    │  │ • Rolling update│
    │                 │  │ • Discord alert │  │ • Health verify │
    └─────────────────┘  └─────────────────┘  │ • Discord alert │
                                               │ • Auto rollback │
                                               └─────────────────┘
```

### 1.2 Tổng quan Workflows

| Workflow                | Trigger                   | Mục đích                              |
| ----------------------- | ------------------------- | ------------------------------------- |
| `ci-cd.yml`             | push `main`/`develop`, PR | Build, test, scan, Docker push        |
| `pr-check.yml`          | PR opened/sync            | Validate + comment kết quả            |
| `deploy-staging.yml`    | push `develop` (CI pass)  | Auto deploy staging (VPS staging)     |
| `deploy-production.yml` | push `main` / tag `v*`    | Deploy production (manual approve)    |
| `release.yml`           | tag `v*`                  | GitHub Release + changelog + archives |
| `security-scan.yml`     | push, PR, weekly schedule | SAST/DAST/SCA/Container scan          |

### 1.3 Nguyên tắc

- **Mọi deploy đều qua CI/CD** — không SSH thủ công deploy code
- **Staging trước Production** — code phải pass staging trước
- **Rollback < 60 giây** — Docker Swarm rollback tự động
- **Zero-downtime** — rolling update với health check
- **Immutable images** — mỗi commit tạo Docker image duy nhất (SHA tag)
- **Secrets không commit** — Docker Secrets + GitHub Secrets

---

## 2. Branching Strategy

### 2.1 Git Flow (Simplified)

```
main (production)
  │
  ├── v1.0.0 (tag → release)
  ├── v1.1.0 (tag → release)
  │
  └── develop (staging)
        │
        ├── feature/patient-search
        ├── feature/billing-v2
        ├── fix/queue-notification
        └── hotfix/auth-bypass
```

### 2.2 Branch Rules

| Branch      | Bảo vệ                       | Deploy tới | Merge từ                |
| ----------- | ---------------------------- | ---------- | ----------------------- |
| `main`      | Protected, require PR review | Production | `develop` (PR)          |
| `develop`   | Protected                    | Staging    | `feature/*`, `fix/*`    |
| `feature/*` | —                            | —          | `develop` (PR)          |
| `fix/*`     | —                            | —          | `develop` (PR)          |
| `hotfix/*`  | —                            | Production | `main` (PR, fast-track) |

### 2.3 Luồng phát triển

```bash
# 1. Tạo feature branch
git checkout develop
git pull
git checkout -b feature/my-feature

# 2. Phát triển & commit
git add .
git commit -m "feat: thêm chức năng X"

# 3. Push & tạo PR → develop
git push -u origin feature/my-feature
# GitHub → New Pull Request → base: develop

# 4. CI chạy: pr-check.yml (build + test)
#    Code review → Approve → Merge

# 5. develop merge → ci-cd.yml chạy → deploy-staging.yml tự động deploy

# 6. Test staging OK → PR từ develop → main
# GitHub → New Pull Request → base: main, compare: develop

# 7. Approve → Merge → deploy-production.yml (cần manual approval)

# 8. Tag release
git tag -a v1.2.0 -m "Release v1.2.0 — chức năng X"
git push origin v1.2.0
```

### 2.4 Hotfix (sửa lỗi khẩn cấp production)

```bash
# 1. Branch từ main
git checkout main && git pull
git checkout -b hotfix/critical-fix

# 2. Fix + commit
git commit -m "fix: sửa lỗi authentication bypass"

# 3. PR → main (fast-track review, 1 reviewer)
# CI pass → Approve → Merge → deploy-production.yml

# 4. Merge hotfix vào develop
git checkout develop
git merge main
git push
```

---

## 3. Environments

### 3.1 Environment Matrix

| Aspect             | Development           | Staging                   | Production                      |
| ------------------ | --------------------- | ------------------------- | ------------------------------- |
| **URL**            | `localhost:4200/5000` | `staging.ivf.clinic`      | `ivf.clinic`                    |
| **Infra**          | Docker Compose local  | VPS staging (single node) | 2 VPS Swarm cluster             |
| **Database**       | PostgreSQL local      | PostgreSQL staging        | PostgreSQL HA (Primary+Standby) |
| **Auto deploy**    | —                     | Mỗi push `develop`        | Manual approve sau push `main`  |
| **Secrets**        | appsettings.Dev.json  | GitHub Secrets → Docker   | GitHub Secrets → Docker         |
| **Debug**          | Full logs + Swagger   | Info logs + Swagger       | Warning logs, no Swagger        |
| **ASPNETCORE_ENV** | Development           | Staging                   | Production                      |
| **Seed data**      | Auto-seed             | Auto-seed                 | No auto-seed                    |

### 3.2 GitHub Environments

Cấu hình trong GitHub → Settings → Environments:

**Environment: `staging`**

- No protection rules (auto-deploy)
- Secrets:
  - `SSH_HOST` — IP VPS staging
  - `SSH_USER` — `deploy`
  - `SSH_PRIVATE_KEY` — SSH private key
  - `DISCORD_WEBHOOK` — Discord channel webhook

**Environment: `production`**

- Protection rules:
  - ✅ Required reviewers: 1+ approver
  - ✅ Wait timer: 0 (manual approve)
- Secrets:
  - `SSH_HOST_MANAGER` — IP VPS 1 (Swarm Manager)
  - `SSH_HOST_WORKER` — IP VPS 2 (Swarm Worker)
  - `SSH_USER` — `deploy`
  - `SSH_PRIVATE_KEY` — SSH private key
  - `DISCORD_WEBHOOK` — Discord channel webhook

---

## 4. GitHub Actions Workflows

### 4.1 CI Pipeline — `ci-cd.yml`

**File:** `.github/workflows/ci-cd.yml`

Trigger: push `main`/`develop`, PR → `main`

| Job               | Mô tả                                              | Chạy khi         |
| ----------------- | -------------------------------------------------- | ---------------- |
| `backend`         | Restore → Build → Test (.NET + PostgreSQL service) | Luôn luôn        |
| `frontend`        | npm ci → lint → build production                   | Luôn luôn        |
| `docker`          | Build Docker images → push GHCR                    | Push `main` only |
| `secret-scanning` | Gitleaks secret detection                          | Luôn luôn        |
| `code-quality`    | dotnet format check                                | Luôn luôn        |

**Docker Images được push:**

```
ghcr.io/hung6066/ivf:sha-abc1234      (commit SHA)
ghcr.io/hung6066/ivf:main             (branch tag)
ghcr.io/hung6066/ivf:v1.2.0           (semver tag)
```

### 4.2 PR Check — `pr-check.yml`

Chạy khi PR được mở/cập nhật. Build + test cả backend lẫn frontend, comment kết quả trên PR.

### 4.3 Deploy Staging — `deploy-staging.yml`

**File:** `.github/workflows/deploy-staging.yml`

- Trigger: CI workflow hoàn thành trên `develop`
- Tự động deploy không cần approve
- Chạy smoke test sau deploy
- Gửi Discord notification

### 4.4 Deploy Production — `deploy-production.yml`

**File:** `.github/workflows/deploy-production.yml`

- Trigger: CI workflow hoàn thành trên `main`, hoặc tag `v*`
- Yêu cầu **manual approval** từ reviewer
- Rolling update 2 VPS Swarm
- Health check verify
- Auto-rollback nếu health check fail
- Discord notification (success/failure)

### 4.5 Release — `release.yml`

- Trigger: push tag `v*`
- Build archives (API + Frontend ZIPs)
- Generate changelog từ git history
- Tạo GitHub Release

### 4.6 Security — `security-scan.yml`

- Trigger: push, PR, weekly (Monday 6 AM UTC)
- SAST: CodeQL (.NET + JS/TS)
- SCA: NuGet vulnerable packages, npm audit
- Container: Trivy image scan → SARIF upload
- Secrets: Gitleaks
- DAST: OWASP ZAP baseline (weekly only)

---

## 5. Docker Images

### 5.1 API Image — `src/IVF.API/Dockerfile`

Multi-stage build:

```
Stage 1: sdk:10.0 → restore → build → publish
Stage 2: aspnet:10.0 (runtime only) → copy publish output
```

- Base: `mcr.microsoft.com/dotnet/aspnet:10.0`
- Expose: 8080 (HTTP)
- Entrypoint: `dotnet IVF.API.dll`
- No AppHost (container-optimized)

### 5.2 Frontend Image — `ivf-client/Dockerfile`

Multi-stage build:

```
Stage 1: node:20-alpine → npm ci → ng build --production
Stage 2: caddy:2-alpine → copy dist + Caddyfile
```

- Base: `caddy:2-alpine`
- Serve Angular SPA với auto-fallback `index.html`
- GZIP compression
- Security headers

### 5.3 Image Tagging Strategy

| Tag Format     | Ví dụ             | Mục đích          |
| -------------- | ----------------- | ----------------- |
| `sha-<7chars>` | `sha-abc1234`     | Unique per commit |
| `<branch>`     | `main`, `develop` | Latest of branch  |
| `v<semver>`    | `v1.2.0`          | Release version   |
| `latest`       | `latest`          | Alias cho main    |

### 5.4 Container Registry

- **GHCR** (GitHub Container Registry): `ghcr.io/hung6066/ivf`
- Auth: GitHub Actions tự authenticate qua `GITHUB_TOKEN`
- Visibility: Private (cùng organization)

### 5.5 Pull Image trên VPS

```bash
# Login GHCR (chỉ cần 1 lần)
echo "$GHCR_TOKEN" | docker login ghcr.io -u hung6066 --password-stdin

# Pull specific version
docker pull ghcr.io/hung6066/ivf:v1.2.0
docker pull ghcr.io/hung6066/ivf-client:v1.2.0
```

---

## 6. Deploy tự động lên Swarm

### 6.1 Luồng Deploy

```
GitHub Actions                         VPS Swarm Cluster
┌──────────────┐                      ┌────────────────────────────┐
│ Build & Push │                      │                            │
│ Docker image │                      │  VPS 1 (Manager)           │
│ → ghcr.io    │                      │  ┌────────┐ ┌────────┐   │
└──────┬───────┘                      │  │ API v1 │ │ API v2 │   │
       │                              │  └────────┘ └────────┘   │
       ▼                              │                            │
┌──────────────┐    SSH               │  VPS 2 (Worker)            │
│ Deploy Job   │──────────────────────│  ┌────────┐               │
│              │    docker service    │  │ API v1 │               │
│ Environment: │    update --image    │  └────────┘               │
│  production  │                      └────────────────────────────┘
│              │
│ Manual       │    1. Pull new image
│ Approval ✓   │    2. Start new container (v2)
│              │    3. Health check pass
│              │    4. Stop old container (v1)
│              │    5. Repeat for next replica
│              │    6. Health check all replicas
└──────────────┘
```

### 6.2 Deploy Script — `scripts/deploy.sh`

Script tự động hóa deploy lên Docker Swarm:

```bash
# Sử dụng:
./scripts/deploy.sh <IMAGE_TAG> [--skip-frontend] [--dry-run]

# Ví dụ:
./scripts/deploy.sh sha-abc1234
./scripts/deploy.sh v1.2.0
./scripts/deploy.sh v1.2.0 --dry-run
```

Chức năng:

- Login GHCR + Pull image trên tất cả nodes
- Rolling update API service (start-first, 1 replica at a time)
- Health check sau mỗi replica
- Auto-rollback nếu health check fail 3 lần
- Hiển thị deployment log chi tiết

### 6.3 Cấu hình Rolling Update

```yaml
# Trong stack.yml, service api:
deploy:
  update_config:
    parallelism: 1 # Update 1 replica tại 1 thời điểm
    delay: 30s # Đợi 30s giữa các replica
    order: start-first # Start mới trước, stop cũ sau
    failure_action: rollback # Auto-rollback nếu fail
    monitor: 60s # Theo dõi 60s sau update
  rollback_config:
    parallelism: 1
    delay: 10s
```

**Giải thích `order: start-first`:**

```
Timeline:
  t0: Container v1 đang chạy  [●v1]
  t1: Start container v2       [●v1] [○v2 starting...]
  t2: v2 health check pass     [●v1] [●v2]
  t3: Stop v1                  [     ] [●v2]
  → Luôn có ít nhất 1 container healthy → ZERO DOWNTIME
```

### 6.4 So sánh deploy strategies

| Strategy       | Downtime | Tốc độ   | Risk       | Áp dụng       |
| -------------- | -------- | -------- | ---------- | ------------- |
| **Rolling**    | Zero     | Chậm     | Thấp       | ✅ Production |
| **Blue-Green** | ~5 giây  | Nhanh    | Trung bình | Staging       |
| **Recreate**   | 30-60s   | Nhanh    | Cao        | Dev only      |
| **Canary**     | Zero     | Rất chậm | Rất thấp   | Chưa áp dụng  |

---

## 7. Rollback

### 7.1 Rollback tự động (Docker Swarm)

Nếu health check fail sau update:

```
Update monitor:
  → New container starts
  → Health check fail (3 retries)
  → failure_action: rollback triggers
  → Swarm tự động revert về image trước
  → Thời gian: < 60 giây
```

### 7.2 Rollback thủ công

```bash
# Cách 1: Docker Swarm rollback (về version ngay trước)
docker service rollback ivf_api
# Thời gian: ~10 giây

# Cách 2: Update về image cụ thể
docker service update --image ghcr.io/hung6066/ivf:sha-abc1234 ivf_api

# Cách 3: Deploy lại tag cũ
./scripts/deploy.sh v1.1.0
```

### 7.3 Rollback database migration

```bash
# 1. Xem migration history
docker exec $(docker ps -q -f name=ivf_api) \
  dotnet ef migrations list --project /app

# 2. Rollback về migration cụ thể
docker exec $(docker ps -q -f name=ivf_api) \
  dotnet ef database update <PreviousMigrationName> --project /app

# 3. Hoặc restore từ S3 backup (xem swarm_s3_deployment_guide.md Section 14)
```

---

## 8. Database Migrations

### 8.1 Migration trong CI/CD

Migrations được chạy **tự động** khi API khởi động (`DatabaseSeeder.SeedAsync()` trong Development/Staging).

Trong Production, migrations chạy thông qua:

```bash
# Trước khi deploy version mới (nếu có migration):
# 1. Backup database
/opt/ivf/scripts/backup-to-s3.sh

# 2. Apply migration (API sẽ tự chạy khi start)
# EF Core auto-migration on startup

# 3. Nếu migration fail → rollback image + restore DB
```

### 8.2 Tạo migration mới

```bash
# Local development
cd d:\Pr.Net\IVF
dotnet ef migrations add <MigrationName> \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Verify migration
dotnet ef database update \
  --project src/IVF.Infrastructure \
  --startup-project src/IVF.API

# Commit migration files
git add src/IVF.Infrastructure/Migrations/
git commit -m "migration: add XYZ table"
```

### 8.3 Migration Checklist

- [ ] Migration file committed (`src/IVF.Infrastructure/Migrations/`)
- [ ] Migration tested locally
- [ ] Backward compatible (không drop column trực tiếp)
- [ ] Production backup trước khi deploy
- [ ] Rollback plan documented

---

## 9. Health Check & Smoke Test

### 9.1 Health Check Endpoints

| Endpoint                           | Mô tả                      | Dùng cho             |
| ---------------------------------- | -------------------------- | -------------------- |
| `/health/live`                     | API process alive          | Swarm health check   |
| `/health/ready`                    | API + DB + Redis ready     | Load balancer        |
| `/api/admin/infrastructure/health` | Full infrastructure health | Monitoring dashboard |

### 9.2 Smoke Test Script

Script `scripts/health-check.sh` kiểm tra sau deploy:

```bash
# Sử dụng:
./scripts/health-check.sh <BASE_URL> [--timeout 120]

# Ví dụ:
./scripts/health-check.sh https://ivf.clinic
./scripts/health-check.sh https://staging.ivf.clinic --timeout 60
```

Kiểm tra:

1. API health endpoint trả 200
2. Frontend SPA trả 200 + có Angular app
3. Database connectivity
4. Redis connectivity
5. Response time < threshold

### 9.3 Post-Deploy Verification

```bash
# Từ GitHub Actions hoặc thủ công:

# 1. API health
curl -sf https://ivf.clinic/health/live
# → 200 OK

# 2. API ready (bao gồm DB + Redis)
curl -sf https://ivf.clinic/health/ready
# → 200 OK

# 3. Frontend SPA
curl -sf https://ivf.clinic/ | grep -q "app-root"
# → Found

# 4. Check replicas
docker service ls --format "{{.Name}} {{.Replicas}}" | grep ivf_api
# → ivf_api 2/2

# 5. Check no error logs
docker service logs ivf_api --since 5m 2>&1 | grep -c "ERROR"
# → 0
```

---

## 10. Security Scanning

### 10.1 Pipeline Security

| Scan Type  | Tool             | Trigger         | Gate         |
| ---------- | ---------------- | --------------- | ------------ |
| SAST (C#)  | CodeQL           | Push, PR        | Advisory     |
| SAST (TS)  | CodeQL           | Push, PR        | Advisory     |
| SCA (.NET) | dotnet list vuln | Push, PR        | Warning      |
| SCA (npm)  | npm audit        | Push, PR        | Warning      |
| Container  | Trivy            | Push main       | Advisory     |
| Secrets    | Gitleaks         | Push, PR        | **Blocking** |
| DAST       | OWASP ZAP        | Weekly schedule | Advisory     |

### 10.2 Secret Scanning

```yaml
# Gitleaks chạy trên mọi push/PR
# Nếu phát hiện secret → FAIL pipeline → không merge được

# Projects cần file .gitleaks.toml để whitelist false positives
```

### 10.3 Container Scanning

```yaml
# Trivy scan Docker image trước khi deploy
# Kết quả upload lên GitHub Security tab (SARIF)
# CRITICAL/HIGH → cảnh báo, không block (vì base image issues)
```

---

## 11. Monitoring & Notifications

### 11.1 Discord Notifications

Deploy events được gửi tới Discord channel:

| Event                    | Màu       | Nội dung                             |
| ------------------------ | --------- | ------------------------------------ |
| Deploy bắt đầu           | 🟡 Yellow | Branch, commit, người deploy         |
| Deploy thành công        | 🟢 Green  | Version, thời gian deploy, health OK |
| Deploy thất bại          | 🔴 Red    | Error details, rollback status       |
| Rollback tự động         | 🟠 Orange | Lý do rollback, version cũ           |
| Security scan có finding | 🔴 Red    | Scan type, severity, count           |

### 11.2 Cấu hình Discord Webhook

```bash
# 1. Discord → Server Settings → Integrations → Webhooks → New Webhook
# 2. Copy Webhook URL
# 3. GitHub → Settings → Secrets → Actions:
#    DISCORD_WEBHOOK = https://discord.com/api/webhooks/xxx/yyy
```

### 11.3 Deploy Dashboard

Real-time monitoring qua Infrastructure Monitor UI:

- Path: `/admin/infrastructure`
- Tab Swarm: services, replicas, rolling update status
- Tab Health: check API, DB, Redis, MinIO, PKI
- Tab Dashboard: CPU, RAM, disk usage

---

## 12. Secrets Management

### 12.1 Secret Layers

```
┌──────────────────────────────────────────────────┐
│               GitHub Secrets                      │
│  (CI/CD: SSH keys, registry tokens, webhooks)   │
└──────────────────────┬───────────────────────────┘
                       │ Injected as env vars in workflow
                       ▼
┌──────────────────────────────────────────────────┐
│              Docker Secrets                       │
│  (Runtime: DB pass, JWT key, API keys)           │
│  Encrypted in Swarm Raft store                   │
│  Mounted as /run/secrets/<name> in containers    │
└──────────────────────────────────────────────────┘
```

### 12.2 GitHub Secrets cần cấu hình

| Secret Name        | Mô tả                              | Dùng ở            |
| ------------------ | ---------------------------------- | ----------------- |
| `SSH_PRIVATE_KEY`  | SSH key để deploy lên VPS          | deploy-\*.yml     |
| `SSH_HOST_MANAGER` | IP VPS 1 (Swarm Manager)           | deploy-production |
| `SSH_HOST_WORKER`  | IP VPS 2 (Swarm Worker)            | deploy-production |
| `SSH_HOST_STAGING` | IP VPS staging                     | deploy-staging    |
| `SSH_USER`         | Username SSH (`deploy`)            | deploy-\*.yml     |
| `DISCORD_WEBHOOK`  | Discord webhook URL                | deploy-\*.yml     |
| `GHCR_TOKEN`       | GitHub PAT cho pull image trên VPS | deploy-\*.yml     |

### 12.3 Docker Secrets trên Swarm

```bash
# Tạo secrets (chạy 1 lần trên Manager node)
docker secret create ivf_db_password secrets/ivf_db_password.txt
docker secret create jwt_secret secrets/jwt_secret.txt
docker secret create minio_access_key secrets/minio_access_key.txt
docker secret create minio_secret_key secrets/minio_secret_key.txt
docker secret create api_cert_password secrets/api_cert_password.txt

# Rotate secret
echo "new_password" | docker secret create ivf_db_password_v2 -
docker service update --secret-rm ivf_db_password --secret-add ivf_db_password_v2 ivf_api
```

---

## 13. Troubleshooting

### 13.1 CI Pipeline Failures

| Lỗi                    | Nguyên nhân                  | Giải pháp                          |
| ---------------------- | ---------------------------- | ---------------------------------- |
| `dotnet restore` fail  | NuGet source down            | Retry hoặc add fallback source     |
| `npm ci` fail          | package-lock.json outdated   | `npm install` + commit lock file   |
| `dotnet test` fail     | Test code hoặc DB connection | Fix test, check PostgreSQL service |
| `ng build` fail        | TypeScript error             | Fix compilation errors             |
| Docker build fail      | Dockerfile context wrong     | Check COPY paths, .dockerignore    |
| GHCR push unauthorized | Token expired                | Check `GITHUB_TOKEN` permissions   |

### 13.2 Deploy Failures

| Lỗi                            | Nguyên nhân                    | Giải pháp                              |
| ------------------------------ | ------------------------------ | -------------------------------------- |
| SSH connection refused         | IP sai, SSH key wrong          | Verify secrets, VPS firewall           |
| `docker pull` unauthorized     | GHCR token expired             | Re-create `GHCR_TOKEN` secret          |
| Service update stuck           | Container crash loop           | `docker service logs`, fix code        |
| Health check fail after deploy | App startup slow, DB migration | Increase `start_period`, fix migration |
| Image not found on worker      | Pull failed on VPS 2           | `docker pull` manually, check network  |
| Rollback triggered             | New version unhealthy          | Check logs, fix, redeploy              |

### 13.3 Debug Commands

```bash
# Service status
docker service ls
docker service ps ivf_api --no-trunc

# Container logs
docker service logs ivf_api --since 10m -f

# Inspect service update status
docker service inspect ivf_api --pretty | grep -A10 "UpdateStatus"

# Check image on each node
docker node ls
docker inspect <node_id> --format '{{.Description.Engine.EngineVersion}}'

# Verify secrets mounted
docker exec $(docker ps -q -f name=ivf_api.1) ls -la /run/secrets/

# Network troubleshooting
docker network ls | grep ivf
docker network inspect ivf_ivf-public
```

---

## 14. Checklist Release

### 14.1 Pre-Release

- [ ] Tất cả features merged vào `develop`
- [ ] Staging deploy thành công
- [ ] QA testing on staging passed
- [ ] Security scan no critical findings
- [ ] Database migration tested
- [ ] API backward compatibility verified
- [ ] Performance test (nếu có thay đổi lớn)
- [ ] Release notes drafted

### 14.2 Release

- [ ] PR `develop` → `main` created
- [ ] Code review approved (≥ 1 reviewer)
- [ ] CI pipeline passed (all green)
- [ ] Merge PR
- [ ] Deploy production approved
- [ ] Health check passed
- [ ] Smoke test passed

### 14.3 Post-Release

- [ ] Git tag created (`v1.x.x`)
- [ ] GitHub Release published
- [ ] Discord notification sent
- [ ] Monitoring dashboard checked (15 min)
- [ ] Error rate normal
- [ ] Response time normal
- [ ] Customer notification (nếu có breaking change)

### 14.4 Rollback Decision Matrix

| Tình huống                 | Hành động                   | Thời gian |
| -------------------------- | --------------------------- | --------- |
| API 500 errors > 5%        | Auto-rollback (Swarm)       | < 60 giây |
| Health check fail          | Auto-rollback (Swarm)       | < 60 giây |
| Feature bug (non-critical) | Hotfix branch → deploy mới  | 1-4 giờ   |
| Data corruption            | Rollback + DB restore từ S3 | 1-2 giờ   |
| Security vulnerability     | Immediate rollback + hotfix | < 30 phút |

---

## Appendix A: File Structure

```
.github/
├── workflows/
│   ├── ci-cd.yml                 # Main CI pipeline
│   ├── pr-check.yml              # PR validation
│   ├── deploy-staging.yml        # Auto deploy staging
│   ├── deploy-production.yml     # Manual deploy production
│   ├── release.yml               # GitHub Release
│   └── security-scan.yml         # Security scanning
│
scripts/
├── deploy.sh                     # Swarm deploy automation
├── health-check.sh               # Post-deploy health verification
├── backup-to-s3.sh               # Daily backup to AWS S3
├── sync-wal-s3.sh                # WAL archive sync
└── ...
│
src/IVF.API/
├── Dockerfile                    # API multi-stage build
│
ivf-client/
├── Dockerfile                    # Frontend multi-stage build
│
docker/
├── caddy/Caddyfile               # Reverse proxy config
└── postgres/                     # PG replication scripts
│
stack.yml                         # Docker Swarm stack definition
docker-compose.yml                # Development compose
docker-compose.production.yml     # Production overrides
```

## Appendix B: Tham khảo

- [Docker Swarm Deploy Guide](swarm_s3_deployment_guide.md) — hướng dẫn triển khai chi tiết VPS + Swarm + S3
- [Deployment Operations Guide](deployment_operations_guide.md) — kiến trúc production, HA, monitoring
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Docker Swarm Mode](https://docs.docker.com/engine/swarm/)
