# EJBCA Client Certificate Setup

This guide explains how to extract and configure the EJBCA admin certificate for REST API access.

## Quick Setup (Automated)

Run the PowerShell setup script:

```powershell
.\setup-ejbca-client-cert.ps1
```

This script will:

1. ✅ Check EJBCA container status
2. ⏳ Wait for EJBCA to be fully ready
3. 📦 Extract `superadmin.p12` from the container
4. ⚙️ Update `appsettings.json` with certificate path
5. 🐳 Update Dockerfile and docker-compose.yml

After running the script, rebuild and restart:

```powershell
docker compose up -d --build api
```

---

## Manual Setup (Alternative)

### 1. Extract Certificate from EJBCA Container

```powershell
# Create certs directory
mkdir -p certs

# Copy superadmin.p12 from EJBCA container
docker cp ivf-ejbca:/opt/keyfactor/appserver/standalone/configuration/keystore/superadmin.p12 ./certs/ejbca-admin.p12

# If the above path doesn't work, try:
docker cp ivf-ejbca:/opt/keyfactor/persistent/conf/p12/superadmin.p12 ./certs/ejbca-admin.p12

# Or find all .p12 files in the container:
docker exec ivf-ejbca find /opt/keyfactor -name "*.p12"
```

**Default Password:** `ejbca`

### 2. Configure API (Local Development)

Update `src/IVF.API/appsettings.json`:

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "EjbcaUrl": "https://localhost:8443/ejbca",
    "ClientCertificatePath": "./certs/ejbca-admin.p12",
    "ClientCertificatePassword": "ejbca"
  }
}
```

### 3. Configure API (Docker)

The `docker-compose.yml` is already configured to:

- Mount `./certs/ejbca-admin.p12` → `/app/certs/ejbca-admin.p12`
- Set environment variables:
  - `DigitalSigning__ClientCertificatePath=/app/certs/ejbca-admin.p12`
  - `DigitalSigning__ClientCertificatePassword=ejbca`

Rebuild and restart:

```powershell
docker compose up -d --build api
```

---

## Verify Setup

### 1. Check Admin Dashboard

Navigate to: http://localhost:4200/admin/signing

Go to the **EJBCA** tab. You should now see:

- ✅ Certificate Authorities list
- ✅ CA details (Subject DN, status, expiry)

### 2. Check API Logs

```powershell
# Local development
# Watch the console output when API starts

# Docker
docker logs -f ivf-api
```

Look for successful EJBCA REST API calls.

### 3. Test EJBCA REST API Endpoint

```powershell
# Via browser or Postman
GET http://localhost:5000/api/admin/signing/ejbca/cas
```

Should return a list of Certificate Authorities instead of Forbidden error.

---

## Troubleshooting

### Certificate Not Found

If you get "File not found" errors:

1. **Find the actual certificate path:**

   ```powershell
   docker exec ivf-ejbca find /opt/keyfactor -name "*.p12" -o -name "*superadmin*"
   ```

2. **Check EJBCA logs:**

   ```powershell
   docker logs ivf-ejbca | Select-String "superadmin|certificate"
   ```

3. **Access EJBCA Admin UI:**
   - Navigate to: https://localhost:8443/ejbca/adminweb/
   - Accept the self-signed certificate warning
   - Create a new admin credential if needed

### Invalid Password

The default password is `ejbca`. If it doesn't work:

1. Check EJBCA environment variables in `docker-compose.yml`
2. Recreate the certificate:
   ```powershell
   docker exec -it ivf-ejbca bash
   # Inside container, use EJBCA CLI tools to generate new admin cert
   ```

### Permission Denied

Ensure the certificate file is readable:

```powershell
# Windows
icacls .\certs\ejbca-admin.p12

# Linux/Mac
chmod 644 ./certs/ejbca-admin.p12
```

### Certificate Not Mounted in Container

Verify volume mount:

```powershell
docker exec ivf-api ls -la /app/certs/
```

Should show `ejbca-admin.p12` with read permissions.

---

## Security Notes

⚠️ **Important:**

- The `certs/` directory is added to `.gitignore` - **never commit certificates to Git**
- `ejbca-admin.p12` has full admin access to EJBCA - protect it carefully
- For production, use proper certificate management (Azure Key Vault, HashiCorp Vault, etc.)
- Consider creating a dedicated REST API client certificate with limited permissions instead of using superadmin

---

## Production Considerations

For production deployments:

1. **Use Kubernetes Secrets or Cloud Key Vaults:**

   ```yaml
   # Example: Kubernetes Secret
   apiVersion: v1
   kind: Secret
   metadata:
     name: ejbca-client-cert
   type: Opaque
   data:
     admin.p12: <base64-encoded-certificate>
   ```

2. **Use Azure Key Vault (for Azure deployments):**

   ```csharp
   // In Program.cs
   builder.Configuration.AddAzureKeyVault(
       new Uri($"https://{keyVaultName}.vault.azure.net/"),
       new DefaultAzureCredential());
   ```

3. **Create dedicated API client certificates:**
   - Don't use superadmin for API access
   - Create role-specific certificates with minimal required permissions
   - Set shorter expiry periods and automate renewal

4. **Enable mutual TLS (mTLS):**
   - Configure EJBCA to require and validate client certificates
   - Use certificate revocation lists (CRL) or OCSP

---

## Alternative: Using EJBCA Without REST API

If you don't need the EJBCA REST API in the admin dashboard:

1. Remove the client certificate configuration
2. The EJBCA tab will show a Forbidden message (expected)
3. Use the EJBCA Admin Web UI directly: https://localhost:8443/ejbca/adminweb/
4. **SignServer continues to work normally** - it doesn't require EJBCA REST API access

The digital signing functionality (PDF signing) will work regardless of EJBCA REST API access.
