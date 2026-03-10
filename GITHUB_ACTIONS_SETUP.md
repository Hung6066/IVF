# GitHub Actions Secrets Setup Guide

## ✅ SSH Keys Generated

Both VPS now have SSH keys. Use the keys below for GitHub Actions.

---

## 📋 GitHub Actions Secrets to Add

**Location:** GitHub → Settings → Secrets and variables → Actions → New repository secret

### Secret 1: VPS1_SSH_KEY

**Name:** `VPS1_SSH_KEY`

**Value:** Copy and paste the entire key below (including BEGIN/END lines):

```
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
QyNTUxOQAAACA6i3j/07C4BUeEqAauV/irU2+3HHKieqmnwDFJPgoTIAAAAJghVq/PIVav
zwAAAAtzc2gtZWQyNTUxOQAAACA6i3j/07C4BUeEqAauV/irU2+3HHKieqmnwDFJPgoTIA
AAAEBpRhGrqQ4QfBPuJGcsDRM+R8QXPhsJauHjt8GMVQjTgjqLeP/TsLgFR4SoBq5X+KtT
b7cccqJ6qafAMUk+ChMgAAAAD3Jvb3RAdm1pMzEyOTExMQECAwQFBg==
-----END OPENSSH PRIVATE KEY-----
```

---

### Secret 2: VPS2_SSH_KEY

**Name:** `VPS2_SSH_KEY`

**Value:** Copy and paste the entire key below (including BEGIN/END lines):

```
-----BEGIN OPENSSH PRIVATE KEY-----
b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW
QyNTUxOQAAACDoiETAhZ4+znsWr+/z2FH+qhyzDnX48NbvugBb8rkPSAAAAJgiDZ3YIg2d
2AAAAAtzc2gtZWQyNTUxOQAAACDoiETAhZ4+znsWr+/z2FH+qhyzDnX48NbvugBb8rkPSA
AAAEBt0m5Xm1MDQbITkVOmzdQvbAFuccvZngwlfk+VziSDpeiIRMCFnj7Oexav7/PYUf6q
HLMOdfjw1u+6AFvyuQ9IAAAAD3Jvb3RAdm1pMzEyOTEwNwECAwQFBg==
-----END OPENSSH PRIVATE KEY-----
```

---

### Secret 3: DISCORD_WEBHOOK

**Name:** `DISCORD_WEBHOOK`

**Value:** (Already configured)

```
https://discord.com/api/webhooks/1480732613051420805/aUjG-AmoRHukTqKwkbrwl2WoSaQ5BAxQLketbdyYOWx7xdfTymTyzPeD6a3fdJ-4X--a
```

---

### Secret 4: GHCR_PAT

**Name:** `GHCR_PAT`

**Value:** (Your GitHub Personal Access Token)

Steps to create:
1. Go to https://github.com/settings/tokens
2. Click **Generate new token (classic)**
3. Token name: `ivf-ghcr-read`
4. Expiration: 90 days (or No expiration)
5. Select scope: ☑️ **read:packages** (only this one needed)
6. Click **Generate token**
7. Copy the token and paste it here

---

## ✅ Verification

After adding all 4 secrets, test the GitHub Actions workflow:

1. Go to GitHub → Actions → **Auto-Heal & Health Watchdog**
2. Click **Run workflow**
3. Select:
   - **Use workflow from:** `main`
   - **action:** `check-only`
   - **dry_run:** `true`
4. Click **Run workflow**

Expected output in logs:
- ✅ Health check passed
- ✅ All VPS reachable
- ✅ Discord webhook configured

---

## 📝 Additional Notes

- SSH keys are now generated on both VPS: `/root/.ssh/id_rsa`
- Keys are **ed25519** (secure, compact)
- GitHub Actions will use these keys to SSH into VPS for recovery operations
- All secrets are now ready for GitHub Actions workflows

