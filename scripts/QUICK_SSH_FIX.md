# Quick SSH Fix Guide

## Common Problem: "Permission denied (publickey)"

### What's Happening?

Your local public key (`id_rsa.pub`, `id_ed25519.pub`, etc.) is not in VPS `/root/.ssh/authorized_keys`

### Fix (Pick Your OS)

#### 🪟 Windows PowerShell

```powershell
cd d:\Pr.Net\IVF

# Step 1: See what's wrong
.\scripts\diagnose-ssh-keys.ps1 -RootPassword "your-root-password"

# Step 2: Auto-fix (add key to VPS)
.\scripts\add-ssh-key-to-vps.ps1 -RootPassword "your-root-password"

# Step 3: Test
ssh root@10.200.0.1
# When prompted: enter 6-digit TOTP from Google Authenticator
```

#### 🐧 Linux/Mac/WSL Bash

```bash
cd ~/Pr.Net/IVF  # or wherever your repo is

# Step 1: See what's wrong
./scripts/diagnose-ssh-keys.sh -p "your-root-password"

# Step 2: Auto-fix (add key to VPS)
./scripts/add-ssh-key-to-vps.sh -p "your-root-password"

# Step 3: Test
ssh root@10.200.0.1
# When prompted: enter 6-digit TOTP from Google Authenticator
```

### Troubleshooting

| Issue                              | Solution                                                                  |
| ---------------------------------- | ------------------------------------------------------------------------- |
| `bash: sshpass: command not found` | Install: `apt install sshpass` or `brew install sshpass`                  |
| Still getting "Permission denied"  | Run diagnostic again, double-check missing key was added                  |
| Timeout during SSH                 | Make sure WireGuard is connected (VPN active)                             |
| Wrong TOTP code                    | Get fresh 6-digit code from Google Authenticator (codes rotate every 30s) |

### Verify Success

When SSH works, you should see:

```
root@10.200.0.1's password:
Verification code:  [← enter 6-digit code from Google Authenticator]
Welcome to Ubuntu 22.04 LTS (GNU/Linux ...)
```

### Next: Deploy Docker

Once SSH works:

```bash
ssh root@10.200.0.1 "docker service ls"  # Test docker access
```

Then follow [production.md](../docs/deployment_operations_guide.md) deployment commands.

---

## Scripts Reference

### Diagnostic (Read-only, safe to run anytime)

- **diagnose-ssh-keys.ps1** (PowerShell) - Shows missing keys
- **diagnose-ssh-keys.sh** (Bash) - Shows missing keys

### Remediation (Makes changes, requires password)

- **add-ssh-key-to-vps.ps1** (PowerShell) - Adds local key to VPS
- **add-ssh-key-to-vps.sh** (Bash) - Adds local key to VPS

### Advanced (Old methods, for reference)

- **fix-ssh-permissions.ps1** - Fixes directory permissions
- **fix-ssh-permissions.sh** - Manual permission fixing

---

## Manual Fix (If Scripts Don't Work)

1. **Get your public key:**

   ```bash
   cat ~/.ssh/id_rsa.pub
   # Output: ssh-rsa AAAA... your@comment
   ```

2. **Copy it to VPS manually:**

   ```bash
   ssh root@10.200.0.1
   # Enter password + TOTP code

   # Then on VPS:
   echo 'ssh-rsa AAAA... your@comment' >> ~/.ssh/authorized_keys
   chmod 600 ~/.ssh/authorized_keys
   ```

3. **Test:**

   ```bash
   # Exit VPS first
   exit

   # Now try SSH again
   ssh root@10.200.0.1
   ```

---

## Prerequisites

- ✅ WireGuard VPN connected (access to 10.200.0.1)
- ✅ Root password for VPS
- ✅ 2FA TOTP codes (Google Authenticator)
- ✅ Local SSH key exists (~/.ssh/id_rsa.pub or similar)

Not sure if you have sshpass? Check:

```bash
which sshpass      # Mac/Linux
Get-Command sshpass  # PowerShell
```

If not found, install:

```bash
# macOS
brew install sshpass

# Linux (Ubuntu/Debian)
apt install sshpass

# Windows (Git Bash)
apt install sshpass

# Or chocolatey
choco install sshpass
```
