# SSH Key Authorization Scripts

## Overview

Complete toolkit for SSH public key authentication on VPS - includes diagnostic tools and auto-remediation scripts for both Windows (PowerShell) and Linux/Mac/WSL (Bash).

### Available Scripts

- **`diagnose-ssh-keys.ps1`** (PowerShell) - Compare local keys vs VPS authorized_keys
- **`diagnose-ssh-keys.sh`** (Bash) - Same functionality for Unix/Linux environments
- **`add-ssh-key-to-vps.ps1`** (PowerShell) - Auto-add missing key to VPS authorized_keys
- **`add-ssh-key-to-vps.sh`** (Bash) - Same functionality for Unix/Linux environments
- **`fix-ssh-permissions.ps1`** (PowerShell) - Legacy: Fix .ssh directory permissions
- **`fix-ssh-permissions.sh`** (Bash) - Legacy: Manual permission fixes on VPS

**Start here:** If SSH access fails, run diagnostic script first.

---

## Option 1: PowerShell Script (Automatic) ⭐ Recommended

### Prerequisites

- WireGuard connected (access to 10.200.0.1)
- Root password
- PowerShell 5.0+ (or pwsh 7.0+)

### Usage

#### Step 1: Install sshpass (optional, makes auth easier)

```powershell
# Windows via Chocolatey
choco install sshpass

# Or use Git Bash with: apt install sshpass
```

#### Step 2: Run the script

```powershell
# With password only
.\scripts\fix-ssh-permissions.ps1 -RootPassword "your_root_password"

# With TOTP 2FA code
.\scripts\fix-ssh-permissions.ps1 -RootPassword "your_root_password" -TotpCode "123456"

# Custom VPS IP
.\scripts\fix-ssh-permissions.ps1 -RootPassword "your_root_password" -VpsIp "10.200.0.1"
```

### What it does

1. ✅ Connects to VPS via SSH
2. ✅ Fixes `.ssh` directory permissions (700)
3. ✅ Fixes `authorized_keys` file permissions (600)
4. ✅ Displays all authorized keys with their comments
5. ✅ Verifies setup is complete

### Output Example

```
🔧 SSH Permissions Fix Script for VPS
=====================================

📡 Connecting to VPS at 10.200.0.1...
✓ Using sshpass for automated authentication

✅ VPS SSH Setup Results:
=====================================
=== SSH Directory Permissions ===
drwx------ 2 root root 4096 Mar 14 12:30 /root/.ssh

=== authorized_keys File Permissions ===
-rw------- 1 root root 2048 Mar 14 12:30 /root/.ssh/authorized_keys

=== Public Keys in authorized_keys ===
Total keys: 5

=== Key Details ===
  Type: ssh-ed25519 | Comment: deploy@ivf
  Type: ssh-rsa | Comment: hung.pt@myduchospital.vn
  Type: ssh-ed25519 | Comment: github-actions-deploy@ivf
  Type: ssh-ed25519 | Comment: root@vmi3129111
  Type: ssh-rsa | Comment: user@local-machine

✨ Setup complete! Try SSH again:
  ssh root@10.200.0.1
```

---

## Option 2: Bash Script (Manual)

### Usage

#### Step 1: Connect to VPS console

Via WireGuard SSH or VPS web console:

```bash
ssh root@10.200.0.1
```

#### Step 2: Run the bash script on VPS

**Option A: Download and run**

```bash
curl https://raw.githubusercontent.com/hung6066/ivf/main/scripts/fix-ssh-permissions.sh | bash
```

**Option B: Copy-paste commands directly**

```bash
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

# Verify
echo "=== SSH Permissions ==="
ls -ld ~/.ssh
ls -l ~/.ssh/authorized_keys

echo "=== Authorized Keys ==="
wc -l < ~/.ssh/authorized_keys
grep -E 'ssh-rsa|ssh-ed25519' ~/.ssh/authorized_keys
```

---

## Troubleshooting

### ❌ "Permission denied (publickey)" still appears after fix

**Causes:**

1. Key not actually in `authorized_keys`
2. Key format issue (extra spaces, corrupted)
3. SSH config not allowing your key

**Fix:**

```bash
# On VPS:
cat ~/.ssh/authorized_keys | grep -c "ssh-rsa\|ssh-ed25519"

# Should show > 0. If 0, no keys are present.

# Check format of your key
ssh-keygen -lf ~/.ssh/authorized_keys

# If error, the file is corrupted. Rebuild it:
rm ~/.ssh/authorized_keys
echo "ssh-rsa AAAA... your-full-public-key-content-here" > ~/.ssh/authorized_keys
chmod 600 ~/.ssh/authorized_keys
```

### ❌ "No such file or directory" for sshpass

Use the expect-based fallback (slower but works):

```powershell
.\scripts\fix-ssh-permissions.ps1 -RootPassword "password"
```

### ❌ TOTP code timing out

Re-run script with fresh code:

```bash
# Get fresh code from Google Authenticator, then:
.\scripts\fix-ssh-permissions.ps1 -RootPassword "password" -TotpCode "123456"
```

---

## Troubleshoot: Key Still Not Working?

If SSH still fails after fixing permissions, your **public key isn't in authorized_keys**.

### Step 1: Diagnose the problem

**Windows (PowerShell):**

```powershell
# Compare local keys with what's on VPS
.\scripts\diagnose-ssh-keys.ps1 -RootPassword "your_root_password"
```

**Linux/Mac/WSL (Bash):**

```bash
# Compare local keys with what's on VPS
./scripts/diagnose-ssh-keys.sh -p "your_root_password"
```

**Output will show:**

- ✅ Which local keys are authorized on VPS
- ❌ Which local keys are MISSING from VPS
- 🔑 Full key content ready to copy

### Step 2: Auto-add missing key

**Windows (PowerShell):**

```powershell
# Automatically add id_rsa.pub to VPS
.\scripts\add-ssh-key-to-vps.ps1 -RootPassword "your_root_password" -LocalKeyFile "~/.ssh/id_rsa.pub"

# Or for Ed25519 key
.\scripts\add-ssh-key-to-vps.ps1 -RootPassword "your_root_password" -LocalKeyFile "~/.ssh/id_ed25519_wsl.pub"
```

**Linux/Mac/WSL (Bash):**

```bash
# Automatically add id_rsa.pub to VPS
./scripts/add-ssh-key-to-vps.sh -p "your_root_password" -f ~/.ssh/id_rsa.pub

# Or for Ed25519 key
./scripts/add-ssh-key-to-vps.sh -p "your_root_password" -f ~/.ssh/id_ed25519_wsl.pub
```

### Step 3: Verify

```bash
ssh root@10.200.0.1
# Should now prompt for 2FA code (not "Permission denied")
```

---

## Next Steps

After SSH key is authorized:

1. **Test SSH access:**

   ```bash
   ssh root@10.200.0.1
   # Enter 2FA code when prompted
   ```

2. **Deploy Docker containers:**

   ```bash
   # Backend
   docker build -t ghcr.io/hung6066/ivf:manual -f src/IVF.API/Dockerfile . && \
   docker save ghcr.io/hung6066/ivf:manual | ssh root@10.200.0.1 "docker load" && \
   ssh root@10.200.0.1 "docker service update --image ghcr.io/hung6066/ivf:manual --update-order start-first --force ivf_api"
   ```

3. **Deploy Docker image** (using the fixed SSH):
   ```bash
   docker build -t ghcr.io/hung6066/ivf:manual -f src/IVF.API/Dockerfile . && \
   docker save ghcr.io/hung6066/ivf:manual | ssh root@10.200.0.1 "docker load" && \
   ssh root@10.200.0.1 "docker service update --image ghcr.io/hung6066/ivf:manual --update-order start-first --force ivf_api"
   ```

---

## Reference

**SSH Permission Requirements:**

- `~/.ssh`: 700 (rwx------)
- `~/.ssh/authorized_keys`: 600 (rw-------)
- `~/.ssh/id_rsa`: 600 (rw-------)

**Why this matters:** SSH refuses to use keys if permissions are too open (security feature).
