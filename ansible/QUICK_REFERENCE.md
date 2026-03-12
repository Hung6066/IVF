# 🚀 Quick Deployment Reference

## 📌 Files Created

| File                    | Purpose                               |
| ----------------------- | ------------------------------------- |
| `ansible/deploy.yml`    | Main Ansible playbook (parameterized) |
| `ansible/deploy.sh`     | Bash script wrapper cho WSL           |
| `ansible/deploy.ps1`    | PowerShell wrapper cho Windows        |
| `ansible/DEPLOYMENT.md` | Full deployment guide (chi tiết)      |

---

## ⚡ Quick Start (90 seconds)

### Windows PowerShell

```powershell
# 1. Navigate to project
cd d:\Pr.Net\IVF

# 2. Deploy (mặc định: backend + frontend)
.\ansible\deploy.ps1 -Full

# 3. Nhập GitHub token khi prompt
# (hoặc set environment variable trước: $env:GHCR_TOKEN="ghp_xxx")

# 4. Done! Check progress at https://natra.site/grafana/
```

### WSL Bash

```bash
# 1. Navigate to project
cd ~/IVF

# 2. Deploy backend only
./ansible/deploy.sh --backend --tag sha-c7d4766

# 3. Hoặc set token từ env var
export GHCR_TOKEN="ghp_xxx..."
./ansible/deploy.sh --full

# 4. Done!
```

---

## 🎯 Common Commands

```powershell
# PowerShell Examples

# Deploy both (auto-detect tag from git)
.\ansible\deploy.ps1 -Full

# Backend only
.\ansible\deploy.ps1 -Backend

# Frontend only
.\ansible\deploy.ps1 -Frontend

# With specific tag
.\ansible\deploy.ps1 -Full -Tag v1.2.3

# With GitHub token
.\ansible\deploy.ps1 -Full -Token "ghp_xxx..."

# Dry run (show what would deploy)
.\ansible\deploy.ps1 -DryRun

# Help
.\ansible\deploy.ps1 -Help
```

```bash
# Bash Examples (WSL)

# Deploy both (auto-detect)
./deploy.sh --full

# Backend only
./deploy.sh --backend

# Frontend only
./deploy.sh --frontend

# Custom tag
./deploy.sh --tag sha-c7d4766 --full

# With token from env
export GHCR_TOKEN="ghp_xxx..."
./deploy.sh --full

# Help
./deploy.sh --help
```

---

## 🔑 GitHub Token Setup (One-time)

1. **Create token**: https://github.com/settings/tokens
2. **Choose**: Generate new token (classic)
3. **Scope**: `read:packages`
4. **Save it** (starts with `ghp_`)

### Use token:

```powershell
# Option 1: Set environment variable
$env:GHCR_TOKEN = "ghp_abc123..."
.\ansible\deploy.ps1 -Full

# Option 2: Pass as parameter
.\ansible\deploy.ps1 -Full -Token "ghp_abc123..."

# Option 3: Prompt for input (most secure)
.\ansible\deploy.ps1 -Full
# → Script will ask for token interactively
```

---

## 📊 Deployment Targets

| Option      | Backend | Frontend |
| ----------- | ------- | -------- |
| `-Full`     | ✅      | ✅       |
| `-Backend`  | ✅      | ❌       |
| `-Frontend` | ❌      | ✅       |
| (default)   | ✅      | ✅       |

---

## 🎬 Deployment Stages

```
1️⃣  Validate prerequisites (Ansible, SSH)
2️⃣  Test SSH connection to VPS
3️⃣  Detect git tag (or use provided tag)
4️⃣  Authenticate with GitHub Container Registry
5️⃣  Pull Docker images
6️⃣  Deploy backend (if enabled)
7️⃣  Deploy frontend (if enabled)
8️⃣  Wait for services to converge
9️⃣  Verify deployment
🔟 Show results and monitoring links
```

---

## 📈 Monitor Deployment

While deploying, check:

- **Grafana**: https://natra.site/grafana/
  - Username: `monitor`
  - Password: `wDDaI8zzSTBPyzfGp3wRc6JkDGgIv6ZF`

- **API Health**: https://natra.site/api/health/live
  - Returns `{"status":"Healthy","timestamp":"..."}`

- **Frontend**: https://natra.site/

---

## 🔄 Deployment Behavior

### Rolling Update Strategy

```yaml
parallelism: 1 # 1 task at a time
delay: 30s # Wait 30s between tasks
order: start-first # New container before old shutdown
failure-action: rollback # Auto rollback on failure
monitor-time: 60s # Monitor 60s after update
```

### Auto-Rollback

If new version fails health checks, Swarm automatically rolls back to previous version.

---

## 🛠️ Troubleshooting

### Script won't run (WSL)

```bash
chmod +x ~/IVF/ansible/deploy.sh
```

### Permission denied (PowerShell)

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### SSH connection fails

```bash
# Test SSH
ssh root@45.134.226.56 "docker ps"

# If fails, copy SSH key
ssh-copy-id -i ~/.ssh/id_ed25519 root@45.134.226.56
```

### Token invalid/expired

Create new token: https://github.com/settings/tokens

### Check deployment logs

```bash
# SSH to VPS and view service logs
ssh root@45.134.226.56 "docker service logs ivf_api --tail=100"
```

---

## 📚 More Info

**Full guide**: See `ansible/DEPLOYMENT.md`

**Ansible playbook**: `ansible/deploy.yml` (parameterized, reusable)

**Hosts inventory**: `ansible/hosts.yml` (edit for custom VPS IPs)

---

## ✅ Pre-Deployment Checklist

- [ ] Code committed to git
- [ ] GitHub token created and valid
- [ ] WSL with Ansible installed
- [ ] SSH keys configured (or run `ssh-copy-id` first)
- [ ] Production VPS accessible (ping or SSH test)
- [ ] Docker images built (GitHub Actions or manual)

---

## 🎉 Success Indicators

After deployment completes successfully:

1. ✅ Script says "✅ Deployment completed successfully"
2. ✅ Both services running new image:
   ```bash
   ssh root@45.134.226.56 "docker service ps ivf_api ivf_frontend"
   ```
3. ✅ Health check returns `Healthy`:
   ```bash
   curl -s https://natra.site/api/health/live
   ```
4. ✅ Frontend loads at https://natra.site/
5. ✅ No alerts in Grafana

---

**Created**: March 12, 2026  
**Version**: 1.0  
**Status**: Production Ready ✅
