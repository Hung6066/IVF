
# 🚀 IVF Production Deployment Guide

Hướng dẫn triển khai ứng dụng đến production sử dụng **Ansible** và **WSL** (Windows Subsystem for Linux).

---

## 📋 Yêu cầu

### Windows
- Windows 10 hoặc cao hơn
- WSL 2 được cài đặt
- PowerShell 7+ (khuyến nghị)

### WSL
- Ubuntu 20.04+ (hoặc distro khác)
- Python 3.8+
- pip
- SSH client
- Git (tùy chọn, để auto-detect commit tag)

### Production VPS
- SSH key-based authentication được cấu hình
- Docker và Docker Swarm
- Acccess tới GitHub Container Registry (GHCR)

---

## 🛠️ Cài đặt

### 1. Cài đặt WSL 2 (nếu chưa có)

```powershell
# Run as Administrator
wsl --install
wsl --set-default-version 2
```

Khởi động lại máy, sau đó:

```bash
# Cập nhật packages
sudo apt update && sudo apt upgrade -y
```

### 2. Cài đặt Ansible trong WSL

```bash
sudo apt install -y python3 python3-pip
pip install ansible
```

Kiểm tra:
```bash
ansible-playbook --version
```

### 3. Cấu hình SSH Keys

Từ WSL:
```bash
# Tạo SSH key (nếu chưa có)
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519

# Copy public key tới VPS manager
ssh-copy-id -i ~/.ssh/id_ed25519 root@45.134.226.56

# Copy tới worker node (nếu sử dụng)
ssh-copy-id -i ~/.ssh/id_ed25519 root@194.163.181.19

# Kiểm tra kết nối
ssh root@45.134.226.56 "docker ps"
```

### 4. Cấu hình Ansible Inventory

File: `ansible/hosts.yml`

```yaml
all:
  vars:
    # ... (các biến khác)
    
  children:
    managers:
      hosts:
        vps1:
          ansible_host: 45.134.226.56
          ansible_user: root
          ansible_ssh_private_key_file: ~/.ssh/id_ed25519  # ✅ Đảm bảo path chính xác

    workers:
      hosts:
        vps2:
          ansible_host: 194.163.181.19
          ansible_user: root
          ansible_ssh_private_key_file: ~/.ssh/id_ed25519

    swarm:
      children:
        managers:
        workers:
```

### 5. Lấy GitHub Container Registry Token

1. Truy cập: https://github.com/settings/tokens
2. Click "Generate new token (classic)"
3. Chọn scope: `read:packages`
4. Copy token (bắt đầu bằng `ghp_`)

---

## 📦 Deployment Scripts

### PowerShell Wrapper (Windows)

Chạy từ `d:\Pr.Net\IVF\`:

```powershell
# Show help
.\ansible\deploy.ps1 -Help

# Deploy cả backend và frontend (mặc định)
.\ansible\deploy.ps1 -Full

# Deploy chỉ backend
.\ansible\deploy.ps1 -Backend

# Deploy chỉ frontend
.\ansible\deploy.ps1 -Frontend

# Deploy với tag cụ thể
.\ansible\deploy.ps1 -Full -Tag sha-c7d4766

# Deploy với token từ env var
$env:GHCR_TOKEN="ghp_xxx..."
.\ansible\deploy.ps1 -Full

# Dry run (xem sẽ làm gì, không thực hiện)
.\ansible\deploy.ps1 -DryRun
```

### Bash Script (WSL)

```bash
cd ~/IVF/ansible

# Show help
./deploy.sh --help

# Deploy cả hai
./deploy.sh --full

# Deploy chỉ backend
./deploy.sh --backend

# Deploy chỉ frontend
./deploy.sh --frontend

# Deploy với tag cụ thể
./deploy.sh --tag sha-c7d4766 --full

# Từ environment variable
export GHCR_TOKEN="ghp_xxx..."
./deploy.sh --full

# Dry run
./deploy.sh --dry-run
```

---

## 🚀 Ví dụ Sử dụng

### Scenario 1: Triển khai thay đổi backend

```powershell
# Windows PowerShell
cd d:\Pr.Net\IVF
.\ansible\deploy.ps1 -Backend
# → Sẽ prompt cho GitHub token
# → Tự detect tag từ git commit (sha-c7d4766)
# → Deploy chỉ API
```

### Scenario 2: Triển khai thay đổi frontend

```bash
# WSL
cd ~/IVF/ansible
export GHCR_TOKEN="ghp_xxx..."
./deploy.sh --frontend --tag sha-c7d4766
# → Deploy chỉ frontend từ tag cụ thể
```

### Scenario 3: Triển khai full (backend + frontend)

```powershell
# Windows
$env:GHCR_TOKEN = "ghp_xxx..."
.\ansible\deploy.ps1 -Full
# → Deploy cả API và frontend
# → Tự detect tag từ git
```

### Scenario 4: Test deployment (dry-run)

```bash
# WSL
export GHCR_TOKEN="ghp_xxx..."
./deploy.sh --dry-run
# → Xem deployment plan mà không thực hiện
```

---

## 📊 Monitoring Deployment

Khi triển khai, theo dõi tại:

- **Grafana Dashboard**: https://natra.site/grafana/
  - Username: `monitor`
  - Password: `wDDaI8zzSTBPyzfGp3wRc6JkDGgIv6ZF`

- **API Health Check**: https://natra.site/api/health/live

- **Frontend**: https://natra.site/

---

## 🔧 Troubleshooting

### 1. "ansible-playbook not found"

WSL chưa cài Ansible:
```bash
pip install ansible
```

### 2. "Cannot connect to VPS manager"

Kiểm tra SSH:
```bash
ssh -v root@45.134.226.56 "echo OK"
```

Nếu lỗi key, copy lại:
```bash
ssh-copy-id -i ~/.ssh/id_ed25519 root@45.134.226.56
```

### 3. "Invalid GHCR token"

Token hết hạn hoặc sai. Tạo token mới tại: https://github.com/settings/tokens

### 4. "Update failed, service rolled back"

Kiểm tra logs:
```bash
ssh root@45.134.226.56 "docker service logs ivf_api --tail=100"
```

### 5. Script không execute được (WSL)

```bash
chmod +x ~/IVF/ansible/deploy.sh
```

---

## 🏗️ Deployment Playbook Details

File: `ansible/deploy.yml`

### Các bước thực hiện:

1. **Validate Variables** - Kiểm tra token được cung cấp
2. **GHCR Authentication** - Đăng nhập vào Container Registry
3. **Pull Backend Image** - Tải API image (nếu `deploy_backend=true`)
4. **Deploy Backend** - Rolling update API service
5. **Wait for Convergence** - Chờ API service ổn định
6. **Pull Frontend Image** - Tải Frontend image (nếu `deploy_frontend=true`)
7. **Deploy Frontend** - Rolling update Frontend service
8. **Verification** - Kiểm tra service status

### Rolling Update Strategy:

```yaml
--update-parallelism 1        # 1 task cùng lúc
--update-delay 30s            # Chờ 30s giữa updates
--update-order start-first    # Start container mới trước khi shutdown cũ
--update-failure-action rollback  # Rollback nếu fail
--update-monitor 60s          # Monitor 60s sau khi update
```

---

## 🔐 Security Notes

### GitHub Token

- ❌ Không commit token vào git
- ✅ Lưu trong environment variable: `$env:GHCR_TOKEN`
- ✅ Hoặc pass qua command line khi prompt
- ✅ Sử dụng fine-grained token với `read:packages` scope

### SSH Keys

- ✅ Sử dụng Ed25519 keys (modern, secure)
- ✅ Lưu key permission: `chmod 600 ~/.ssh/id_ed25519`
- ❌ Không commit private keys
- ✅ Distribute public keys an toàn tới VPS

---

## 📝 Thêm Deployment Tags

Script tự động detect git commit tag. Để manual:

```powershell
# PowerShell
.\ansible\deploy.ps1 -Full -Tag "v1.2.3"
```

```bash
# WSL
./deploy.sh --full --tag v1.2.3
```

Format tag:
- `sha-c7d4766` - Git commit short hash (auto-detected)
- `v1.2.3` - Version tag
- `prod-20240312` - Custom format
- `latest` - Latest build

---

## 🔄 Continuous Deployment

Để tự động deploy khi push tới main:

**GitHub Actions** sẽ tự động:
1. Build Docker images
2. Push tới GHCR
3. Trigger deployment workflow

Xem: `.github/workflows/deploy-production.yml`

---

## 📞 Support

Nếu gặp vấn đề:

1. Kiểm tra logs:
   ```bash
   ssh root@45.134.226.56 "docker service logs ivf_api --tail=200"
   ```

2. Xem Ansible output chi tiết:
   ```bash
   ./deploy.sh --full -v  # (WSL)
   # hoặc
   .\ansible\deploy.ps1 -Full  # (PowerShell with verbose)
   ```

3. Test SSH connection:
   ```bash
   ssh -vvv root@45.134.226.56 "docker ps"
   ```

---

**Last Updated**: March 12, 2026  
**Maintainers**: IVF DevOps Team
