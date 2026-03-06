# IVF Platform — Ansible Setup & Deploy

Tự động hóa setup VPS Contabo + Docker Swarm + deploy từ zero đến production.

## Yêu cầu

- **Ansible >= 2.15** trên máy local (không cần cài trên VPS)
- 2 VPS Contabo đã mua (Ubuntu 24.04 LTS)
- SSH key đã copy vào cả 2 VPS

### Cài Ansible

```bash
# macOS
brew install ansible

# Ubuntu/Debian
sudo apt install ansible

# Windows (WSL2)
wsl --install
sudo apt install ansible

# Hoặc pip
pip install ansible
```

## Bước 1: Cấu hình inventory

```bash
cd ansible
cp hosts.example.yml hosts.yml
```

Sửa `hosts.yml` — thay `<VPS1_IP>` và `<VPS2_IP>` bằng IP thực:

```yaml
managers:
  hosts:
    vps1:
      ansible_host: 203.0.113.10 # ← IP VPS 1
workers:
  hosts:
    vps2:
      ansible_host: 203.0.113.20 # ← IP VPS 2
```

## Bước 2: Test kết nối

```bash
ansible -i hosts.yml all -m ping
# vps1 | SUCCESS
# vps2 | SUCCESS
```

## Bước 3: Chạy setup toàn bộ

```bash
# Setup VPS + Docker + Swarm + Deploy (tất cả từ zero)
ansible-playbook -i hosts.yml site.yml \
  --extra-vars "ghcr_token=ghp_your_github_pat"
```

## Các lệnh hữu ích

```bash
# Chỉ setup OS + Docker (không deploy app)
ansible-playbook -i hosts.yml site.yml --tags setup

# Chỉ deploy/redeploy app
ansible-playbook -i hosts.yml site.yml --tags deploy \
  --extra-vars "ghcr_token=ghp_xxx"

# Deploy image version cụ thể
ansible-playbook -i hosts.yml site.yml --tags deploy \
  --extra-vars "ghcr_token=ghp_xxx image_tag=v1.2.0"

# Dry run — xem sẽ làm gì (không thực thi)
ansible-playbook -i hosts.yml site.yml --check --diff

# Chỉ chạy common role
ansible-playbook -i hosts.yml site.yml --tags common

# Chỉ chạy docker role
ansible-playbook -i hosts.yml site.yml --tags docker
```

## Cấu trúc

```
ansible/
├── ansible.cfg              # Ansible configuration
├── hosts.example.yml        # Inventory template (copy → hosts.yml)
├── hosts.yml                # ← Tạo từ template, KHÔNG commit (có IP thực)
├── site.yml                 # Master playbook
├── README.md
└── roles/
    ├── common/              # OS setup, user, SSH, firewall, fail2ban
    │   ├── tasks/main.yml
    │   └── handlers/main.yml
    ├── docker/              # Docker Engine + Swarm cluster
    │   ├── tasks/main.yml
    │   └── handlers/main.yml
    └── app/                 # Secrets, source clone, stack deploy
        └── tasks/main.yml
```

## Playbook thực hiện gì

### Phase 1: `common` (cả 2 VPS)

- Cập nhật OS, cài packages
- Tạo user `deploy` với sudo
- Copy SSH authorized_keys
- Tắt root login + password auth
- Cấu hình UFW firewall (SSH, HTTP/S, Swarm ports)
- Cấu hình Fail2ban
- Tạo thư mục `/opt/ivf/{secrets,certs,backups,scripts,logs}`

### Phase 2: `docker` (cả 2 VPS)

- Cài Docker CE từ official repo
- Cấu hình Docker daemon (log rotation, overlay2)
- Khởi tạo Swarm trên VPS 1 (Manager)
- Join VPS 2 vào Swarm (Worker)
- Gán labels: `role=primary` (VPS 1), `role=standby` (VPS 2)
- Login GHCR

### Phase 3: `app` (manager only)

- Generate secrets (DB passwords, JWT RSA key, MinIO creds, HSM PINs)
- Tạo Docker Secrets trong Swarm Raft store
- Clone repository từ GitHub
- Pull Docker images từ GHCR
- Deploy stack (`docker stack deploy -c stack.yml ivf`)

## Lưu ý

- **`hosts.yml` không commit** — chứa IP thực, thêm vào `.gitignore`
- **GHCR token** truyền qua `--extra-vars`, không lưu trong file
- Secrets chỉ tạo 1 lần — chạy lại sẽ không ghi đè
- SSH hardening tắt root login sau khi tạo user `deploy`
- Sau khi chạy xong, SSH bằng: `ssh deploy@<VPS_IP>`
