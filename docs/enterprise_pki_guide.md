# Enterprise PKI Infrastructure Guide

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture Diagram](#2-architecture-diagram)
3. [CA Hierarchy](#3-ca-hierarchy)
4. [Certificate Profiles](#4-certificate-profiles)
5. [SoftHSM2 PKCS#11 Architecture](#5-softhsm2-pkcs11-architecture)
6. [Complete Setup Flow](#6-complete-setup-flow)
7. [PKCS#11 Worker Configuration Flow](#7-pkcs11-worker-configuration-flow)
8. [PDF Signing Flow](#8-pdf-signing-flow)
9. [Multi-Tenant PKI](#9-multi-tenant-pki)
10. [Docker Compose Services](#10-docker-compose-services)
11. [Network Architecture](#11-network-architecture)
12. [Truststore Configuration](#12-truststore-configuration)
13. [Admin Web UI Access](#13-admin-web-ui-access)
14. [Configuration Reference](#14-configuration-reference)
15. [CLI Reference](#15-cli-reference)
16. [Troubleshooting](#16-troubleshooting)
17. [Security Considerations](#17-security-considerations)
18. [Script Reference](#18-script-reference)
19. [File Inventory](#19-file-inventory)
20. [Live Deployment Status](#20-live-deployment-status)

---

## 1. Overview

The IVF Enterprise PKI system provides a complete public key infrastructure for digitally signing clinical PDF documents (medical reports, prescriptions, consent forms). It is built on open-source components running as Docker containers, orchestrated by a single idempotent setup script.

### Components

| Component         | Role                                                                              | Image                                                      |
| ----------------- | --------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| **EJBCA CE**      | Certificate Authority -- issues, manages, and revokes X.509 certificates          | `keyfactor/ejbca-ce:latest`                                |
| **SignServer CE** | Document signing service -- signs PDFs using keys and certificates from EJBCA     | `keyfactor/signserver-ce:latest` (or custom SoftHSM build) |
| **SoftHSM2**      | Software-based PKCS#11 cryptographic token -- stores private keys non-extractably | Installed into SignServer container via custom Dockerfile  |
| **WireGuard VPN** | Secure remote access to EJBCA/SignServer admin interfaces                         | Host-level (10.200.0.1)                                    |
| **PostgreSQL 16** | Dedicated databases for EJBCA and SignServer                                      | `postgres:16-alpine`                                       |

### Security Goals

- **Non-extractable keys**: Private signing keys are stored inside SoftHSM2 PKCS#11 tokens. The `CKA_EXTRACTABLE` attribute is set to `false`, meaning keys cannot be exported or copied, even by an administrator with database access.
- **FIPS 140-2 Level 1 compliance**: SoftHSM2 provides software-based HSM functionality compliant with FIPS 140-2 Level 1. This is an upgrade path to hardware HSMs (Luna, Utimaco, nCipher) which use the same PKCS#11 interface.
- **Proper CA hierarchy**: Two-tier CA structure (Root CA + Subordinate Signing CA) following X.509 best practices. The Root CA can be kept offline after initial setup.
- **Certificate lifecycle management**: EJBCA handles issuance, renewal, and revocation. CRL distribution and OCSP are configured for real-time revocation checking.
- **Network isolation**: Signing traffic flows over internal Docker networks with no internet access. Only admin web UIs are exposed, through WireGuard VPN.

---

## 2. Architecture Diagram

```mermaid
graph TB
    subgraph "WireGuard VPN (10.200.0.1)"
        ADMIN["Administrator Browser<br/>superadmin.p12 client cert"]
    end

    subgraph "Docker Host"
        subgraph "ivf-public network (bridge)"
            EJBCA_PUB["ivf-ejbca<br/>:8443 HTTPS Admin<br/>:8442 HTTP Public"]
            SS_PUB["ivf-signserver<br/>:9443 HTTPS Admin"]
            API_PUB["ivf-api<br/>:5000 HTTP"]
        end

        subgraph "ivf-signing network (internal, no internet)"
            API_SIGN["ivf-api"]
            SS_SIGN["ivf-signserver<br/>REST: /signserver/process"]
            EJBCA_SIGN["ivf-ejbca<br/>CLI: ejbca.sh"]
        end

        subgraph "ivf-data network (internal, no internet)"
            EJBCA_DB["ivf-ejbca-db<br/>PostgreSQL 16<br/>DB: ejbca"]
            SS_DB["ivf-signserver-db<br/>PostgreSQL 16<br/>DB: signserver"]
        end

        subgraph "SignServer Container"
            SS_APP["SignServer CE"]
            SOFTHSM["SoftHSM2<br/>PKCS#11 Token<br/>Label: SignServerToken"]
            SOFTHSM_LIB["libsofthsm2.so<br/>/usr/lib64/pkcs11/"]
            TOKEN_DIR["Token Storage<br/>/opt/keyfactor/persistent/<br/>softhsm/tokens<br/>(Docker Volume)"]
        end
    end

    ADMIN -->|"HTTPS :8443<br/>client cert auth"| EJBCA_PUB
    ADMIN -->|"HTTPS :9443<br/>client cert auth"| SS_PUB

    API_SIGN -->|"POST /signserver/process<br/>mTLS (api-client.p12)"| SS_SIGN
    SS_SIGN -->|"PKCS#11 sign"| SOFTHSM
    SOFTHSM --> SOFTHSM_LIB
    SOFTHSM_LIB --> TOKEN_DIR

    EJBCA_SIGN -->|"Issues certificates<br/>to SignServer workers"| SS_SIGN
    EJBCA_PUB --> EJBCA_DB
    SS_PUB --> SS_DB
```

---

## 3. CA Hierarchy

```mermaid
graph TD
    ROOT["IVF-Root-CA<br/>RSA 4096 | SHA256WithRSA<br/>Validity: 20 years (7305 days)<br/>DN: CN=IVF Root Certificate Authority,<br/>O=IVF Healthcare,OU=PKI,C=VN<br/>Self-signed | Soft token"]

    SUBCA["IVF-Signing-SubCA<br/>RSA 4096 | SHA256WithRSA<br/>Validity: 10 years (3652 days)<br/>DN: CN=IVF Document Signing CA,<br/>O=IVF Healthcare,OU=Digital Signing,C=VN<br/>Signed by IVF-Root-CA"]

    TENANT["IVF-Tenant-{id}-SubCA<br/>RSA 4096 | SHA256WithRSA<br/>Validity: 5 years (1826 days)<br/>DN: CN=IVF Tenant {id} Signing CA,<br/>O=IVF Healthcare,OU=Tenant {id},C=VN<br/>Signed by IVF-Root-CA<br/>(optional, per --tenant flag)"]

    W1["PDFSigner (Worker 1)<br/>CN=IVF PDF Signer<br/>Key alias: signer"]
    W272["PDFSigner_technical (Worker 272)<br/>CN=Ky Thuat Vien IVF<br/>Key alias: pdfsigner_technical"]
    W444["PDFSigner_head_department (Worker 444)<br/>CN=Truong Khoa IVF<br/>Key alias: pdfsigner_head_department"]
    W597["PDFSigner_doctor1 (Worker 597)<br/>CN=Bac Si IVF<br/>Key alias: pdfsigner_doctor1"]
    W907["PDFSigner_admin (Worker 907)<br/>CN=Quan Tri IVF<br/>Key alias: pdfsigner_admin"]
    W100["TimeStampSigner (Worker 100)<br/>CN=IVF Timestamp Authority<br/>Key alias: tsa"]
    APICLIENT["ivf-api-client<br/>CN=ivf-api-client<br/>Key alias: api-client<br/>(mTLS client cert)"]

    TENANT_SIGNER["Tenant-specific signer<br/>CN=IVF Tenant {id} Signer<br/>Key alias: tenant-{id}-signer"]

    ROOT --> SUBCA
    ROOT --> TENANT

    SUBCA --> W1
    SUBCA --> W272
    SUBCA --> W444
    SUBCA --> W597
    SUBCA --> W907
    SUBCA --> W100
    SUBCA --> APICLIENT

    TENANT --> TENANT_SIGNER
```

### Pre-existing CAs in EJBCA CE

EJBCA CE ships with a **ManagementCA** that is automatically created on first startup. This CA issues the `superadmin` client certificate used for admin web access. The IVF PKI hierarchy is separate from the ManagementCA:

| CA                        | Purpose                                     | Created By                                   |
| ------------------------- | ------------------------------------------- | -------------------------------------------- |
| **ManagementCA**          | EJBCA admin authentication (superadmin.p12) | EJBCA auto-init                              |
| **IVF Internal Root CA**  | Legacy CA from initial testing (DN: `CN=IVF Internal Root CA,OU=IT Department,O=IVF Clinic,ST=Ho Chi Minh,C=VN`, ID: 995596930, expires 2036) | Earlier manual setup |
| **IVF-Root-CA**           | Root of the IVF PKI trust chain (ID: 1031502430, expires 2046) | `setup-enterprise-pki.sh` Phase 1a |
| **IVF-Signing-SubCA**     | Issues end-entity signing certificates (ID: 1728368285, signed by IVF-Root-CA, expires 2036) | `setup-enterprise-pki.sh` Phase 1b |
| **IVF-Tenant-{id}-SubCA** | Tenant-isolated signing certificates        | `setup-enterprise-pki.sh` Phase 7 (optional) |

---

## 4. Certificate Profiles

EJBCA CE ships with a limited set of built-in certificate profiles. The setup script uses the `ENDUSER` profile (the CE default) and customizes it with CRL Distribution Point and Authority Information Access extensions.

### ENDUSER Profile Customizations (Applied by Script)

| Field                            | Value                                                                |
| -------------------------------- | -------------------------------------------------------------------- |
| `useCRLDistributionPoint`        | `true`                                                               |
| `useDefaultCRLDistributionPoint` | `true`                                                               |
| `CRLDistributionPointURI`        | `http://<VPN_HOST>:8442/ejbca/publicweb/webdist/certdist?cmd=crl`    |
| `useAuthorityInformationAccess`  | `true`                                                               |
| `caIssuers`                      | `http://<VPN_HOST>:8442/ejbca/publicweb/webdist/certdist?cmd=cacert` |

### Deployed Certificate Profiles

These profiles were imported into EJBCA via XML files generated from EJBCA source code. The XML files are stored in `certs/ejbca/` and imported with:

```bash
docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh ca importprofile \
    --infile /tmp/certprofile_IVF-PDFSigner-Profile-5001.xml
```

> **Warning — XML profile import bugs in EJBCA CE**: Profiles MUST be generated from `CertificateProfile(1)` (ENDUSER) and `EndEntityProfile(true)` constructors to include all required internal fields. Partial XML profiles will throw NPE on import. See [Troubleshooting: EJBCA XML profile import bugs](#ejbca-xml-profile-import-bugs).

| Profile Name               | Profile ID | Key Usage                        | Extended Key Usage                  | Validity | Purpose                        |
| -------------------------- | ---------- | -------------------------------- | ----------------------------------- | -------- | ------------------------------ |
| **IVF-PDFSigner-Profile**  | 5001       | digitalSignature, nonRepudiation | --                                  | 3 years  | PDF document signing workers   |
| **IVF-TSA-Profile**        | 5002       | digitalSignature                 | timeStamping (1.3.6.1.5.5.7.3.8)   | 5 years  | Timestamp Authority worker     |
| **IVF-TLS-Client-Profile** | 5003       | digitalSignature                 | clientAuth (1.3.6.1.5.5.7.3.2)     | 2 years  | mTLS client certificates (API) |
| **IVF-OCSP-Profile**       | --         | digitalSignature                 | OCSPSigning (1.3.6.1.5.5.7.48.1.5) | 2 years  | Dedicated OCSP responder       |

### Deployed End Entity Profiles

| Profile Name                     | Profile ID | Allowed CAs          | Allowed Cert Profiles    | Purpose                     |
| -------------------------------- | ---------- | -------------------- | ------------------------ | --------------------------- |
| **IVF-PDFSigner-EEProfile**      | 6001       | IVF-Signing-SubCA    | IVF-PDFSigner-Profile    | PDF signer end entities     |
| **IVF-TSA-EEProfile**            | 6002       | IVF-Signing-SubCA    | IVF-TSA-Profile          | TSA end entities            |
| **IVF-TLS-Client-EEProfile**     | 6003       | IVF-Signing-SubCA    | IVF-TLS-Client-Profile   | API mTLS client certs       |

Profile XML files saved in `certs/ejbca/`:
- `certprofile_IVF-PDFSigner-Profile-5001.xml`
- `certprofile_IVF-TSA-Profile-5002.xml`
- `entityprofile_IVF-PDFSigner-EEProfile-6001.xml`
- `entityprofile_IVF-TSA-EEProfile-6002.xml`
- `entityprofile_IVF-TLS-Client-EEProfile-6003.xml`

---

## 5. SoftHSM2 PKCS#11 Architecture

The IVF deployment uses the **standard `keyfactor/signserver-ce:7.3.2` image** (no custom build). SoftHSM2 is **pre-installed** in the SignServer CE 7.3.2 image as a system package (`/usr/lib64/pkcs11/libsofthsm2.so`) and is **pre-registered** in the image's `signserver_deploy.properties` at index 1. No custom binary deployment is required.

The persistent **`environment-hsm` hook** on the Docker volume handles: (1) writing `softhsm2.conf` to a location writable by uid=10001, (2) exporting `SOFTHSM2_CONF` so the JVM finds the correct token directory, and (3) conditionally initializing the token on first use.

```mermaid
graph TD
    subgraph "One-time Setup (setup-pkcs11-workers.sh Phase 3)"
        HOOK["Install environment-hsm startup hook:<br/>/opt/keyfactor/persistent/environment-hsm<br/>(sourced by start.sh on every container restart)"]
        RESTART["docker service update --force ivf_signserver<br/>(WildFly restarts, start.sh sources hook)<br/>"]
        INITTOKEN["Hook writes softhsm2.conf (persistent) + exports SOFTHSM2_CONF<br/>Hook initializes token if not present (idempotent):<br/>softhsm2-util --init-token --free --label SignServerToken<br/>PIN from Docker secret softhsm_pin"]

        HOOK --> RESTART --> INITTOKEN
    end

    subgraph "Every Container Restart (start.sh lifecycle)"
        STARTH["start.sh<br/>(container entrypoint)"]
        SOURCEHOOK["source /opt/keyfactor/persistent/environment-hsm"]
        WRITECONF["Hook writes persistent softhsm2.conf:<br/>/opt/keyfactor/persistent/softhsm/softhsm2.conf<br/>tokendir = /opt/keyfactor/persistent/softhsm-tokens\nHook exports: SOFTHSM2_CONF=..../softhsm/softhsm2.conf"]
        WILDFLY["WildFly 35 starts<br/>deploy.properties already has SoftHSM (image default):<br/>cryptotoken.p11.lib.1.name = SoftHSM<br/>cryptotoken.p11.lib.1.file = /usr/lib64/pkcs11/libsofthsm2.so<br/>SOFTHSM2_CONF env var points JVM to persistent token directory"]

        STARTH --> SOURCEHOOK --> WRITECONF --> WILDFLY
    end

    subgraph "Runtime Key Operations (Phase 5)"
        SETPROP["signserver setproperty WID<br/>CRYPTOTOKEN_IMPLEMENTATION_CLASS = PKCS11CryptoToken<br/>SHAREDLIBRARYNAME = SoftHSM<br/>SLOTLABELTYPE = SLOT_LABEL<br/>SLOTLABELVALUE = SignServerToken<br/>PIN = (from Docker secret softhsm_pin)"]
        ACTIVATE["signserver activatecryptotoken WID PIN"]
        GENKEY["signserver generatekey WID<br/>-keyalg RSA -keyspec 4096 -alias key_alias"]
        GENCSR["signserver generatecertreq WID<br/>'CN=...,O=...,OU=...,C=VN'<br/>SHA256WithRSA /tmp/csr.pem"]

        SETPROP --> ACTIVATE --> GENKEY --> GENCSR
    end
```

### Key Storage Details

| Parameter                       | Value                                                                                |
| ------------------------------- | ------------------------------------------------------------------------------------ |
| Token label                     | `SignServerToken`                                                                    |
| User PIN                        | From Docker secret `softhsm_pin` (mounted at `/run/secrets/softhsm_pin`)             |
| SO PIN                          | From Docker secret `softhsm_so_pin` (mounted at `/run/secrets/softhsm_so_pin`)       |
| Library path (system)           | `/usr/lib64/pkcs11/libsofthsm2.so` **(pre-installed in SignServer CE 7.3.2 image)**  |
| SoftHSM2 utility                | `softhsm2-util` **(system binary, pre-installed in image)**                          |
| softhsm2.conf (persistent)      | `/opt/keyfactor/persistent/softhsm/softhsm2.conf` (written by hook on every restart) |
| `SOFTHSM2_CONF` env var         | Exported by hook; JVM and softhsm2-util both use this to find the conf file          |
| Token directory                 | `/opt/keyfactor/persistent/softhsm-tokens` (Docker volume `ivf_signserver_persistent`)|
| Startup hook                    | `/opt/keyfactor/persistent/environment-hsm` (sourced by `start.sh` on every restart) |
| deploy.properties registration  | Pre-configured in image: `cryptotoken.p11.lib.1.name = SoftHSM` / `.file = /usr/lib64/pkcs11/libsofthsm2.so` |
| SoftHSM2 version                | v2.6.1 (AlmaLinux 9 system package — pre-installed in SignServer CE 7.3.2)           |
| Key algorithm                   | RSA 4096                                                                              |
| Key extractable                 | **No** (`CKA_EXTRACTABLE = false`)                                                    |
| SignServer user UID             | `10001`                                                                               |

### environment-hsm Hook (Current Content)

The hook at `/opt/keyfactor/persistent/environment-hsm` performs four tasks on every container restart:

```bash
#!/bin/bash
# 1. Copy truststore.jks to WildFly config dir (enables mTLS admin web)
CONF_DIR=/opt/keyfactor/appserver/standalone/configuration
if [ -f /opt/keyfactor/persistent/truststore.jks ]; then
    cp /opt/keyfactor/persistent/truststore.jks "${CONF_DIR}/truststore.jks"
fi

# 2. Fix TLS protocols (remove TLSv1.3 — breaks Firefox SSL client cert auth)
python3 /opt/keyfactor/persistent/fix-tls.py

# 3. Write softhsm2.conf to writable subdir + export SOFTHSM2_CONF
#    NOTE: /opt/keyfactor/persistent/ is owned by root — NOT writable by uid=10001
#    BUT /opt/keyfactor/persistent/softhsm/ subdir IS owned by uid=10001
SOFTHSM_CONF_FILE=/opt/keyfactor/persistent/softhsm/softhsm2.conf
SOFTHSM_TOKEN_DIR=/opt/keyfactor/persistent/softhsm-tokens
mkdir -p "${SOFTHSM_TOKEN_DIR}"
cat > "${SOFTHSM_CONF_FILE}" << EOF
directories.tokendir = ${SOFTHSM_TOKEN_DIR}
log.level = INFO
EOF
export SOFTHSM2_CONF="${SOFTHSM_CONF_FILE}"

# 4. Initialize SoftHSM token if not already present
TOKEN_LABEL="${SOFTHSM_TOKEN_LABEL:-SignServerToken}"
HSM_PIN="$(cat "${SOFTHSM_USER_PIN_FILE}" 2>/dev/null || echo '3ac33a807af6b22fe9f22e4ba2c56a3b')"
HSM_SO_PIN="$(cat "${SOFTHSM_SO_PIN_FILE}" 2>/dev/null || echo "${HSM_PIN}")"
if softhsm2-util --show-slots 2>/dev/null | grep -q "Label.*${TOKEN_LABEL}"; then
    echo "[hook] SoftHSM token '${TOKEN_LABEL}' already initialized"
else
    softhsm2-util --init-token --free --label "${TOKEN_LABEL}" \
        --pin "${HSM_PIN}" --so-pin "${HSM_SO_PIN}"
fi
```

> **Key design decisions**:
> - The hook is **sourced** (not executed) by `start.sh`, so `export SOFTHSM2_CONF` propagates to the WildFly JVM process.
> - Writing to `/opt/keyfactor/persistent/softhsm/softhsm2.conf` works because that subdirectory is owned by uid=10001. Writing to the persistent volume root fails (owned by root).
> - `deploy.properties` does **not** need modification — `SoftHSM` is pre-registered in the image at index 1.

### Why the Persistent Hook Approach

`/etc/softhsm2.conf` is in the **ephemeral** container filesystem — it does not exist in the base image and is reset on container recreation. The `SOFTHSM2_CONF` environment variable overrides the default config file location, so the hook exports it to point to the persistent volume copy.

The `start.sh` entrypoint sources every file matching `/opt/keyfactor/*/environment-hsm` before starting WildFly. Placing the hook on the persistent volume (`/opt/keyfactor/persistent/environment-hsm`) means it runs on every container restart, writes the `softhsm2.conf` to the persistent volume, exports `SOFTHSM2_CONF`, and WildFly/the PKCS#11 library correctly finds the token directory on every restart.

### Why PKCS#11 Over P12

| Feature            | P12 (KeystoreCryptoToken)           | PKCS#11 (SoftHSM2)                            |
| ------------------ | ----------------------------------- | --------------------------------------------- |
| Key extractability | Keys can be exported from .p12 file | Keys are non-extractable                      |
| FIPS compliance    | None                                | FIPS 140-2 Level 1                            |
| HSM migration path | Requires re-enrollment              | Same PKCS#11 interface -- just change library |
| Access control     | File system permissions + password  | PIN-protected token access                    |
| Key backup         | Copy .p12 file                      | Backup SoftHSM token directory                |

---

## 6. Complete Setup Flow

```mermaid
sequenceDiagram
    participant Script as setup-enterprise-pki.sh
    participant EJBCA as ivf-ejbca
    participant SS as ivf-signserver
    participant Host as Host filesystem

    Note over Script: Phase 0: Pre-flight Checks
    Script->>EJBCA: docker inspect (verify running)
    Script->>SS: docker inspect (verify running)
    Script->>EJBCA: curl healthcheck/ejbcahealth
    Script->>SS: curl healthcheck/signserverhealth
    Script->>Host: mkdir -p certs/{ca,signers,tsa,api}

    Note over Script: Phase 1a: Create Root CA
    Script->>EJBCA: ejbca.sh ca init --caname IVF-Root-CA<br/>--dn "CN=IVF Root Certificate Authority,..."<br/>--keytype RSA --keyspec 4096<br/>-v 7305 -s SHA256WithRSA
    Script->>EJBCA: ejbca.sh ca listcas (verify)

    Note over Script: Phase 1b: Create Sub-CA
    Script->>EJBCA: ejbca.sh ca listcas (get Root CA ID)
    EJBCA-->>Script: CA Name: IVF-Root-CA / Id: <CAID>
    Script->>EJBCA: ejbca.sh ca init --caname IVF-Signing-SubCA<br/>--signedby <CAID> --keytype RSA --keyspec 4096<br/>-v 3652 -s SHA256WithRSA

    Note over Script: Phase 1c: Export Certificates
    Script->>EJBCA: ejbca.sh ca getcacert --caname IVF-Root-CA -f /tmp/root-ca.pem
    Script->>Host: docker cp root-ca.pem -> certs/ca/root-ca.pem
    Script->>Host: cp root-ca.pem -> certs/ca/ca.pem
    Script->>EJBCA: ejbca.sh ca getcacert --caname IVF-Signing-SubCA -f /tmp/sub-ca.pem
    Script->>Host: docker cp sub-ca.pem -> certs/ca/sub-ca.pem
    Script->>Host: cat sub-ca.pem + root-ca.pem -> certs/ca-chain.pem
    Script->>SS: docker cp ca-chain.pem -> /opt/.../keys/ca-chain.pem

    Note over Script: Phase 2: Certificate Profile
    Script->>EJBCA: ejbca.sh ca editcertificateprofile --cpname ENDUSER<br/>--field useCRLDistributionPoint --value true
    Script->>EJBCA: ejbca.sh ca editcertificateprofile --cpname ENDUSER<br/>--field useAuthorityInformationAccess --value true
    Script->>EJBCA: (set CRL DP URI, CA Issuers AIA URI)

    Note over Script: Phase 3: SoftHSM2 Persistent Setup + environment-hsm Hook
    Script->>Host: Check /tmp/libsofthsm2.so (AlmaLinux 9 native)<br/>If missing: pull from temp almalinux:9 container via dnf
    Script->>SS: (root) mkdir -p .../persistent/softhsm/{lib,bin,tokens}
    Script->>SS: docker cp /tmp/libsofthsm2.so → .../softhsm/lib/libsofthsm2.so
    Script->>SS: docker cp /tmp/softhsm2-util → .../softhsm/bin/softhsm2-util
    Script->>SS: (root) Write .../softhsm/softhsm2.conf (persistent volume)
    Script->>SS: (root) Write /opt/keyfactor/persistent/environment-hsm<br/>(startup hook: init token + write to deploy.properties)
    Script->>Host: docker service update --force ivf_signserver<br/>(triggers start.sh → hook → WildFly loads SoftHSM)
    Script->>Script: Wait for healthcheck/signserverhealth
    Script->>SS: Read SOFTHSM_PIN from /run/secrets/softhsm_pin
    Script->>SS: Verify SoftHSM registered in deploy.properties

    Note over Script: Phase 4: End Entity Enrollment (x7)
    loop For each worker (5 PDF + 1 TSA + 1 API client)
        Script->>EJBCA: ra addendentity --username ivf-signer-<WID><br/>--dn "CN=...,O=IVF Healthcare,..." --caname IVF-Signing-SubCA<br/>--type 1 --token P12 --certprofile ENDUSER --eeprofile EMPTY
        Script->>EJBCA: ra setclearpwd ivf-signer-<WID> changeit
        Script->>EJBCA: batch --username ivf-signer-<WID> -dir /tmp/ejbca-certs
        Script->>SS: docker cp P12 -> /opt/.../keys/<alias>.p12
        Script->>SS: keytool -changealias (normalize key alias)
        Script->>Host: docker cp P12 -> certs/signers/<alias>.p12
    end

    Note over Script: Phase 5: Worker Configuration (see Section 7)
    Note over Script: Phase 6: OCSP
    Script->>EJBCA: config protocols enable --name OCSP
    Script->>EJBCA: curl OCSP endpoint (verify reachable)

    Note over Script: Phase 7: Multi-Tenant (if --tenant)
    Script->>EJBCA: ca init --caname IVF-Tenant-{id}-SubCA<br/>--signedby <ROOT_CA_ID> -v 1826
    Script->>Host: Export tenant CA + chain to certs/tenants/{id}/

    Note over Script: Phase 8: Verification
    Script->>EJBCA: ca listcas (verify all CAs)
    Script->>SS: test -f <keystore> (verify all keystores)
    Script->>SS: softhsm2-util --show-slots (verify token)
    Script->>SS: getstatus brief all (verify workers Active)
    Script->>SS: curl POST /signserver/process (test sign PDF)
    Script->>Host: Verify exported certs exist
```

---

## 7. PKCS#11 Worker Configuration Flow

This is the detailed sequence for Phase 5 -- configuring a single SignServer worker with PKCS#11 crypto token and EJBCA-signed certificate.

```mermaid
sequenceDiagram
    participant Script as setup-enterprise-pki.sh
    participant SS as ivf-signserver (CLI)
    participant HSM as SoftHSM2 Token
    participant EJBCA as ivf-ejbca (CLI)

    Note over Script: Step 1: Remove conflicting P12 properties
    Script->>SS: removeproperty <WID> KEYSTOREPATH
    Script->>SS: removeproperty <WID> KEYSTOREPASSWORD
    Script->>SS: removeproperty <WID> KEYSTORETYPE
    Script->>SS: removeproperty <WID> SHAREDLIBRARY
    Script->>SS: removeproperty <WID> SET_PERMISSIONS

    Note over Script: Step 2: Set PKCS#11 worker properties
    Script->>SS: setproperty <WID> NAME <WorkerName>
    Script->>SS: setproperty <WID> TYPE PROCESSABLE
    Script->>SS: setproperty <WID> IMPLEMENTATION_CLASS<br/>org.signserver.module.pdfsigner.PDFSigner
    Script->>SS: setproperty <WID> CRYPTOTOKEN_IMPLEMENTATION_CLASS<br/>org.signserver.server.cryptotokens.PKCS11CryptoToken
    Script->>SS: setproperty <WID> AUTHTYPE NOAUTH
    Script->>SS: setproperty <WID> SHAREDLIBRARYNAME SoftHSM
    Script->>SS: setproperty <WID> SLOTLABELTYPE SLOT_LABEL
    Script->>SS: setproperty <WID> SLOTLABELVALUE SignServerToken
    Script->>SS: setproperty <WID> PIN <value-from-softhsm_pin-secret>
    Script->>SS: setproperty <WID> DEFAULTKEY <key_alias>
    Script->>SS: (set PDF properties: REASON, LOCATION,<br/>TSA_WORKER, DIGESTALGORITHM, etc.)

    Note over Script: Step 3: Reload + activate crypto token
    Script->>SS: reload <WID>
    SS->>HSM: Connect to SignServerToken
    Note over Script: PIN is read from Docker secret softhsm_pin<br/>(not hardcoded "changeit")
    Script->>SS: activatecryptotoken <WID> <PIN-from-secret>
    SS->>HSM: Authenticate with PIN

    Note over Script: Step 4: Generate RSA 4096 key in SoftHSM
    Script->>SS: generatekey <WID> -keyalg RSA -keyspec 4096 -alias <key_alias>
    SS->>HSM: CKM_RSA_PKCS_KEY_PAIR_GEN<br/>CKA_EXTRACTABLE=false

    Note over Script: Step 5: Reload + re-activate after key gen
    Script->>SS: reload <WID>
    Script->>SS: activatecryptotoken <WID> <PIN-from-secret>
    SS-->>Script: Activation successful

    Note over Script: Step 6: Generate CSR from PKCS#11 key
    Script->>SS: generatecertreq <WID><br/>"CN=IVF PDF Signer,O=IVF Healthcare,OU=Digital Signing,C=VN"<br/>SHA256WithRSA /tmp/worker_<WID>_csr.pem
    SS->>HSM: Sign CSR with private key
    SS-->>Script: CSR written to /tmp/worker_<WID>_csr.pem

    Note over Script: Step 7: Pipe CSR from SignServer to EJBCA
    Script->>SS: cat /tmp/worker_<WID>_csr.pem
    SS-->>Script: CSR data
    Script->>EJBCA: cat > /tmp/worker_<WID>_csr.pem

    Note over Script: Step 8: Reset end entity for re-signing
    Script->>EJBCA: ra setendentitystatus ivf-signer-<WID> 10
    Script->>EJBCA: ra setclearpwd ivf-signer-<WID> <KEYSTORE_PASSWORD>

    Note over Script: Step 9: Sign CSR with EJBCA
    Script->>EJBCA: createcert --username ivf-signer-<WID><br/>--password <KEYSTORE_PASSWORD> -c /tmp/...csr.pem -f /tmp/...signed.pem
    EJBCA-->>Script: Signed certificate

    Note over Script: Step 10: Extract PEM and pipe to SignServer
    Script->>EJBCA: sed -n '/BEGIN CERT/,/END CERT/p' /tmp/...signed.pem
    EJBCA-->>Script: PEM certificate
    Script->>SS: cat > /tmp/worker_<WID>_cert.pem

    Note over Script: Step 11: Upload signer certificate
    Script->>SS: uploadsignercertificate <WID> GLOB<br/>/tmp/worker_<WID>_cert.pem

    Note over Script: Step 12: Build and upload certificate chain
    Script->>EJBCA: cat signed.pem + sub-ca.pem + root-ca.pem
    EJBCA-->>Script: Full chain PEM
    Script->>SS: cat > /tmp/worker_<WID>_chain.pem
    Script->>SS: uploadsignercertificatechain <WID> GLOB<br/>/tmp/worker_<WID>_chain.pem

    Note over Script: Step 13: Final reload + verify
    Script->>SS: reload <WID>
    Script->>SS: getstatus brief <WID>
    SS-->>Script: Worker status: Active
```

### P12 Fallback Path

If SoftHSM2 is not available (library not found at `/usr/lib64/pkcs11/libsofthsm2.so`), or if PKCS#11 configuration fails for a specific worker, the script falls back to P12 (KeystoreCryptoToken) mode:

1. Write a `.properties` file with `KEYSTORETYPE=PKCS12`, `KEYSTOREPATH`, `KEYSTOREPASSWORD`
2. Load via `signserver setproperties`
3. Set additional worker properties (REASON, LOCATION, TSA_WORKER, etc.)
4. Reload and activate with keystore password

The P12 keystore file is the one generated during Phase 4 (EJBCA batch enrollment).

---

## 8. PDF Signing Flow

```mermaid
sequenceDiagram
    participant Client as Client Application
    participant API as IVF API<br/>(SignServerDigitalSigningService)
    participant SS as SignServer<br/>(ivf-signserver)
    participant HSM as SoftHSM2<br/>(PKCS#11 Token)
    participant TSA as TimeStampSigner<br/>(Worker 100)
    participant MinIO as MinIO<br/>(ivf-signed-pdfs)

    Client->>API: POST /api/signing/sign-pdf<br/>(multipart/form-data: PDF + metadata)

    Note over API: SignPdfWithUserAsync()
    opt Handwritten signature image provided
        API->>API: PdfSignatureImageService<br/>.OverlaySignatureImage()
    end

    Note over API: SendToSignServerAsync()
    API->>API: Generate correlationId<br/>SHA256 hash of input PDF (if audit)

    API->>SS: POST /signserver/process<br/>Content-Type: multipart/form-data<br/>- workerName=PDFSigner<br/>- data=document.pdf (PDF bytes)
    Note over API,SS: Connection via ivf-signing network<br/>mTLS with api-client.p12 (if RequireMtls=true)

    SS->>HSM: PKCS#11: C_SignInit + C_Sign<br/>(RSA-SHA256 with non-extractable key)
    HSM-->>SS: Digital signature bytes

    SS->>TSA: Internal: RFC 3161 timestamp request<br/>(TSA_WORKER=TimeStampSigner)
    TSA->>HSM: PKCS#11: Sign timestamp token
    HSM-->>TSA: Timestamp signature
    TSA-->>SS: RFC 3161 timestamp token

    SS-->>API: HTTP 200: Signed PDF bytes<br/>(PAdES signature + timestamp embedded)

    API->>API: Validate response is PDF<br/>(check %PDF magic bytes)
    API->>API: SHA256 hash of output (if audit)

    opt Audit logging enabled
        API->>API: Log AUDIT: correlationId, worker,<br/>inputHash, outputHash, durationMs
    end

    API->>MinIO: Upload signed PDF<br/>Bucket: ivf-signed-pdfs
    MinIO-->>API: Object URL

    API-->>Client: HTTP 200: Download URL
```

### SignServer REST API Protocol

The SignServer CE REST endpoint accepts `multipart/form-data` at:

```
POST {SignServerUrl}/process
```

Request fields:

| Field        | Type   | Description                                          |
| ------------ | ------ | ---------------------------------------------------- |
| `workerName` | string | Worker name (e.g., `PDFSigner`, `PDFSigner_doctor1`) |
| `data`       | binary | PDF file bytes (`application/pdf`)                   |

Response: signed PDF bytes (binary) with HTTP 200 on success.

Worker-level properties control the signature appearance and behavior (REASON, LOCATION, ADD_VISIBLE_SIGNATURE, etc.). These cannot be overridden per-request in SignServer CE.

### Per-User Signing

Each user role has a dedicated SignServer worker with its own certificate:

| Worker                                   | Usage                               |
| ---------------------------------------- | ----------------------------------- |
| `PDFSigner` (Worker 1)                   | Default/generic signer              |
| `PDFSigner_technical` (Worker 272)       | Technical staff signatures          |
| `PDFSigner_head_department` (Worker 444) | Department head approval signatures |
| `PDFSigner_doctor1` (Worker 597)         | Doctor signatures                   |
| `PDFSigner_admin` (Worker 907)           | Administrative signatures           |

The API's `SignPdfWithUserAsync()` method accepts a `workerName` parameter to select the appropriate worker.

---

## 9. Multi-Tenant PKI

```mermaid
graph TD
    ROOT["IVF-Root-CA<br/>(RSA 4096, 20yr)"]

    SUBCA["IVF-Signing-SubCA<br/>(shared, 10yr)"]
    TENANT1["IVF-Tenant-clinic-01-SubCA<br/>(5yr, RSA 4096)"]
    TENANT2["IVF-Tenant-clinic-02-SubCA<br/>(5yr, RSA 4096)"]

    SHARED_SIGNER["Shared workers<br/>(PDFSigner, TSA, etc.)"]
    T1_SIGNER["Tenant clinic-01 Signer<br/>CN=IVF Tenant clinic-01 Signer"]
    T2_SIGNER["Tenant clinic-02 Signer<br/>CN=IVF Tenant clinic-02 Signer"]

    ROOT --> SUBCA
    ROOT --> TENANT1
    ROOT --> TENANT2

    SUBCA --> SHARED_SIGNER
    TENANT1 --> T1_SIGNER
    TENANT2 --> T2_SIGNER

    subgraph "Host: certs/tenants/"
        T1_DIR["clinic-01/<br/>ca.pem<br/>ca-chain.pem<br/>signer.p12"]
        T2_DIR["clinic-02/<br/>ca.pem<br/>ca-chain.pem<br/>signer.p12"]
    end
```

### Tenant Isolation

Each tenant gets its own Sub-CA with a 5-year validity, signed directly by the Root CA. This provides:

- **Certificate isolation**: Revoking a tenant's Sub-CA revokes all its certificates without affecting other tenants.
- **Independent certificate lifecycle**: Each tenant's certificates have their own expiry schedule.
- **Audit trail**: Certificates are traceable to a specific tenant via the Sub-CA chain.

### Creating a Tenant

```bash
bash scripts/setup-enterprise-pki.sh --tenant clinic-01
```

This creates:

1. `IVF-Tenant-clinic-01-SubCA` in EJBCA (signed by IVF-Root-CA, 5yr validity)
2. Tenant CA cert exported to `certs/tenants/clinic-01/ca.pem`
3. Tenant CA chain (tenant CA + root CA) at `certs/tenants/clinic-01/ca-chain.pem`
4. Tenant signer certificate enrolled and exported to `certs/tenants/clinic-01/signer.p12`

### Tenant DN Format

```
Sub-CA:  CN=IVF Tenant {id} Signing CA, O=IVF Healthcare, OU=Tenant {id}, C=VN
Signer:  CN=IVF Tenant {id} Signer, O=IVF Healthcare, OU=Tenant {id}, C=VN
```

### Configuration Override

In `appsettings.json`, tenant-specific signing can override the default CA:

```json
{
  "DigitalSigning": {
    "EjbcaDefaultCaName": "IVF Signing CA",
    "TenantSubCa": {
      "EjbcaCaName": "IVF-Tenant-clinic-01-SubCA"
    }
  }
}
```

---

## 10. Docker Compose Services

| Service         | Container Name      | Image                            | Ports (host:container)                               | Networks                          | Volumes                                                                     | Profile |
| --------------- | ------------------- | -------------------------------- | ---------------------------------------------------- | --------------------------------- | --------------------------------------------------------------------------- | ------- |
| `ejbca`         | `ivf-ejbca`         | `keyfactor/ejbca-ce:latest`      | `8443:8443` (HTTPS Admin), `8442:8080` (HTTP Public) | ivf-public, ivf-signing, ivf-data | `ejbca_persistent:/opt/keyfactor/persistent`, `./certs/ca/ca.pem` (ro)      | default |
| `ejbca-db`      | `ivf-ejbca-db`      | `postgres:16-alpine`             | --                                                   | ivf-data                          | `ejbca_db_data:/var/lib/postgresql/data`                                    | default |
| `signserver`    | `ivf-signserver`    | `keyfactor/signserver-ce:latest` | `9443:8443` (HTTPS Admin)                            | ivf-public, ivf-signing, ivf-data | `signserver_persistent:/opt/keyfactor/persistent`, `./certs/ca/ca.pem` (ro) | default |
| `signserver-db` | `ivf-signserver-db` | `postgres:16-alpine`             | --                                                   | ivf-data                          | `signserver_db_data:/var/lib/postgresql/data`                               | default |

### SoftHSM2 Integration (Swarm Stack)

The **standard `keyfactor/signserver-ce:latest` image** is used — no custom Dockerfile is needed. SoftHSM2 support is activated by running `setup-pkcs11-workers.sh` once after the stack is deployed. The script:

1. Copies AlmaLinux 9 native SoftHSM2 binaries onto the `signserver_persistent` Docker volume
2. Installs a persistent `environment-hsm` startup hook on the same volume
3. Restarts the service — on every subsequent restart, the hook runs before WildFly, registers the library, and initializes the token using PINs from Docker secrets

### Docker Secrets Required

| Secret              | Purpose                                                            |
| ------------------- | ------------------------------------------------------------------ |
| `softhsm_pin`       | SoftHSM2 user PIN (read by `environment-hsm` hook and workers)     |
| `softhsm_so_pin`    | SoftHSM2 Security Officer PIN (used for token initialization only) |
| `jwt_private_key`   | JWT RSA signing key (shared across all API replicas)               |
| `api_cert_password` | API mTLS client certificate password                               |

### Important Notes

- **Read-only filesystem**: The standard `signserver` service may have `read_only: true`. Ensure `signserver_persistent` tmpfs mounts allow writes to `/opt/keyfactor/persistent/`.
- **Database credentials**:

  | Service       | User         | Password            | Database     |
  | ------------- | ------------ | ------------------- | ------------ |
  | EJBCA DB      | `ejbca`      | `ejbca_secret`      | `ejbca`      |
  | SignServer DB | `signserver` | `signserver_secret` | `signserver` |

  > **Note**: EJBCA/SignServer CE do NOT support `DATABASE_PASSWORD_FILE` Docker secrets for passwords — pass the password directly as an environment variable.

- **Health checks**: Both EJBCA and SignServer have generous start periods (`start_period: 120s`) because Java application startup takes 1-2 minutes.

- **EJBCA environment**:
  - `TLS_SETUP_ENABLED=simple` — auto-generates TLS keypair on first start
  - `INITIAL_ADMIN=;PublicAccessAuthenticationToken:TRANSPORT_ANY;` — allows initial admin access without client certificate (must be secured after setup)

---

## 11. Network Architecture

```mermaid
graph TB
    subgraph "ivf-public (bridge, external access)"
        API["ivf-api :5000"]
        EJBCA_P["ivf-ejbca :8443 :8442"]
        SS_P["ivf-signserver :9443"]
        REDIS_P["ivf-redis :6379"]
        MINIO_P["ivf-minio :9000 :9001"]
    end

    subgraph "ivf-signing (bridge, internal=true)"
        direction LR
        API_S["ivf-api"]
        SS_S["ivf-signserver"]
        EJBCA_S["ivf-ejbca"]
    end

    subgraph "ivf-data (bridge, internal=true)"
        direction LR
        DB["ivf-db :5432"]
        EJBCA_DB["ivf-ejbca-db :5432"]
        SS_DB["ivf-signserver-db :5432"]
        MINIO_D["ivf-minio"]
        REDIS_D["ivf-redis"]
    end

    INTERNET["External Access<br/>(WireGuard VPN)"]
    INTERNET --> API
    INTERNET --> EJBCA_P
    INTERNET --> SS_P

    API_S -.->|"mTLS signing requests"| SS_S
    SS_S -.->|"Certificate operations"| EJBCA_S

    EJBCA_P -.-> EJBCA_DB
    SS_P -.-> SS_DB
    API -.-> DB
    API -.-> MINIO_D
    API -.-> REDIS_D

    style API_S fill:#f9f,stroke:#333
    style SS_S fill:#f9f,stroke:#333
    style EJBCA_S fill:#f9f,stroke:#333
```

### Network Properties

| Network       | Driver | `internal` | Purpose                                                                                   |
| ------------- | ------ | ---------- | ----------------------------------------------------------------------------------------- |
| `ivf-public`  | bridge | `false`    | External-facing services. Ports are published to the Docker host.                         |
| `ivf-signing` | bridge | **`true`** | Signing traffic only. API communicates with SignServer and EJBCA. **No internet access.** |
| `ivf-data`    | bridge | **`true`** | Database and storage access. **No internet access.**                                      |

### Service Network Membership

| Service           | ivf-public | ivf-signing | ivf-data |
| ----------------- | ---------- | ----------- | -------- |
| ivf-api           | Y          | Y           | Y        |
| ivf-ejbca         | Y          | Y           | Y        |
| ivf-signserver    | Y          | Y           | Y        |
| ivf-ejbca-db      | --         | --          | Y        |
| ivf-signserver-db | --         | --          | Y        |
| ivf-minio         | Y          | --          | Y        |
| ivf-redis         | Y          | --          | Y        |
| ivf-db            | --         | --          | Y        |

---

## 12. Truststore Configuration

### EJBCA Truststore

The EJBCA instance needs to trust the following CAs to validate client certificates for admin web access and inter-service communication:

| CA                    | Purpose                                             |
| --------------------- | --------------------------------------------------- |
| **ManagementCA**      | Validates superadmin.p12 for admin web login        |
| **IVF-Root-CA**       | Root of the IVF PKI chain                           |
| **IVF-Signing-SubCA** | Validates certificates issued to SignServer workers |

EJBCA CE manages its own truststore automatically. CAs created via `ca init` are automatically available for validation.

### SignServer Truststore

SignServer needs to trust:

| CA                    | Purpose                                       |
| --------------------- | --------------------------------------------- |
| **ManagementCA**      | Admin web client certificate validation       |
| **IVF-Root-CA**       | Root of trust for uploaded certificate chains |
| **IVF-Signing-SubCA** | Validates worker signer certificates          |

The CA chain is deployed to SignServer at `/opt/keyfactor/persistent/keys/ca-chain.pem` during Phase 1c.

### API Client (IVF.API)

The API server needs the following for mTLS communication with SignServer:

| File             | Path in Container           | Purpose                                                                      |
| ---------------- | --------------------------- | ---------------------------------------------------------------------------- |
| `api-client.p12` | `/app/certs/api-client.p12` | Client certificate for mTLS                                                  |
| `ca-chain.pem`   | `/app/certs/ca-chain.pem`   | Trusted CA chain (Root CA + Sub-CA) to validate SignServer's TLS certificate |

Configured in `docker-compose.yml`:

```yaml
volumes:
  - ./certs/api/api-client.p12:/app/certs/api-client.p12:ro
  - ./certs/ca-chain.pem:/app/certs/ca-chain.pem:ro
  - ./secrets/api_cert_password.txt:/run/secrets/api_cert_password:ro
```

And via environment variables:

```yaml
environment:
  - DigitalSigning__ClientCertificatePath=/app/certs/api-client.p12
  - DigitalSigning__ClientCertificatePasswordFile=/run/secrets/api_cert_password
  - DigitalSigning__TrustedCaCertPath=/app/certs/ca-chain.pem
```

---

## 13. Admin Web UI Access

### Prerequisites

1. WireGuard VPN connection established (VPN server at `10.200.0.1`)
2. `superadmin.p12` client certificate generated from EJBCA's ManagementCA

### Step 1: Generate superadmin.p12

On first EJBCA startup with `INITIAL_ADMIN=;PublicAccessAuthenticationToken:TRANSPORT_ANY;`, admin access is open. Use this to generate a proper admin certificate:

```bash
# Access EJBCA admin web (no client cert needed initially)
# Navigate to: RA Functions → Add End Entity
# Create entity with ManagementCA, ENDUSER profile
# Generate keystore (PKCS#12)

# Or via CLI:
docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh ra addendentity \
    --username superadmin \
    --dn "CN=SuperAdmin,O=IVF Healthcare,C=VN" \
    --caname ManagementCA \
    --type 1 \
    --token P12 \
    --password changeit \
    --certprofile ENDUSER \
    --eeprofile EMPTY

docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh ra setclearpwd superadmin changeit
docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh batch --username superadmin -dir /tmp

# Copy to host
docker cp ivf-ejbca:/tmp/superadmin.p12 ./certs/ejbca-admin.p12
```

### Step 2: Add Admin Role

```bash
docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh roles addrolemember \
    --role "Super Administrator Role" \
    --caname ManagementCA \
    --with CertificateAuthenticationToken \
    --value "CN=SuperAdmin,O=IVF Healthcare,C=VN"
```

### Step 3: Import P12 into Browser

1. Open your browser's certificate management:
   - **Chrome**: Settings > Privacy and Security > Security > Manage certificates
   - **Firefox**: Settings > Privacy & Security > Certificates > View Certificates
2. Import `superadmin.p12` (password: `changeit`)
3. The ManagementCA must also be in the browser's trusted certificate authorities for the connection to succeed

### Step 4: Access Admin Web UIs

| Application           | URL                                                               | Authentication                      |
| --------------------- | ----------------------------------------------------------------- | ----------------------------------- |
| **EJBCA Admin**       | `https://10.200.0.1:8443/ejbca/adminweb/`                         | Client certificate (superadmin.p12) |
| **EJBCA Public**      | `https://10.200.0.1:8443/ejbca/publicweb/`                        | No auth required                    |
| **EJBCA RA**          | `https://10.200.0.1:8443/ejbca/ra/`                               | Client certificate                  |
| **SignServer Admin**  | `https://10.200.0.1:9443/signserver/adminweb/`                    | Client certificate (superadmin.p12) |
| **SignServer Health** | `https://10.200.0.1:9443/signserver/healthcheck/signserverhealth` | No auth                             |

### Step 5: Allow Any Admin in SignServer (Development)

For development, SignServer can be configured to accept any client certificate:

```bash
docker exec ivf-signserver /opt/signserver/bin/signserver wsadmins -allowany
```

For production, add specific admin certificates via the SignServer Admin Web or CLI.

---

## 14. Configuration Reference

All settings are in `DigitalSigningOptions` (bound from `appsettings.json` section `"DigitalSigning"`).

### Core Settings

| Property                  | Type     | Default                            | Description                                                                        |
| ------------------------- | -------- | ---------------------------------- | ---------------------------------------------------------------------------------- |
| `Enabled`                 | `bool`   | `false`                            | Enable/disable digital signing globally. When `false`, PDFs are returned unsigned. |
| `SignServerUrl`           | `string` | `http://localhost:9080/signserver` | Base URL of SignServer REST API. In Docker: `https://signserver:8443/signserver`   |
| `SignServerContainerName` | `string` | `ivf-signserver`                   | Docker container name for CLI access via `docker exec`                             |
| `WorkerName`              | `string` | `PDFSigner`                        | Default SignServer PDF signing worker name                                         |
| `WorkerId`                | `int?`   | `null`                             | Alternative worker identification (takes priority over WorkerName)                 |
| `TimeoutSeconds`          | `int`    | `30`                               | HTTP timeout for SignServer requests                                               |

### Signature Appearance

| Property               | Type     | Default                     | Description                                |
| ---------------------- | -------- | --------------------------- | ------------------------------------------ |
| `DefaultReason`        | `string` | `Xac nhan bao cao y te IVF` | Signature reason embedded in PDF           |
| `DefaultLocation`      | `string` | `IVF Clinic`                | Signature location                         |
| `DefaultContactInfo`   | `string` | `support@ivf-clinic.vn`     | Contact info in signature                  |
| `AddVisibleSignature`  | `bool`   | `true`                      | Whether to overlay visible signature stamp |
| `VisibleSignaturePage` | `int`    | `0`                         | Page for visible signature (0 = last page) |

### EJBCA Integration

| Property                  | Type     | Default                        | Description                        |
| ------------------------- | -------- | ------------------------------ | ---------------------------------- |
| `EjbcaUrl`                | `string` | `https://localhost:8443/ejbca` | EJBCA admin URL                    |
| `EjbcaContainerName`      | `string` | `ivf-ejbca`                    | EJBCA Docker container name        |
| `EjbcaDefaultCaName`      | `string` | `IVF Signing CA`               | CA name for certificate enrollment |
| `EjbcaDefaultCertProfile` | `string` | `ENDUSER`                      | Certificate profile for enrollment |
| `EjbcaDefaultEeProfile`   | `string` | `EMPTY`                        | End entity profile for enrollment  |
| `EjbcaKeystorePassword`   | `string` | `changeit`                     | PKCS#12 keystore password          |

### TLS / mTLS

| Property                        | Type      | Default | Description                                                                    |
| ------------------------------- | --------- | ------- | ------------------------------------------------------------------------------ |
| `SkipTlsValidation`             | `bool`    | `true`  | Skip TLS cert validation (dev only, **must be `false` in production**)         |
| `RequireMtls`                   | `bool`    | `false` | Require mutual TLS for SignServer communication                                |
| `ClientCertificatePath`         | `string?` | `null`  | Path to client certificate P12 for mTLS                                        |
| `ClientCertificatePassword`     | `string?` | `null`  | Client cert password (direct value)                                            |
| `ClientCertificatePasswordFile` | `string?` | `null`  | Path to file containing client cert password (Docker Secret, takes precedence) |
| `TrustedCaCertPath`             | `string?` | `null`  | Path to trusted CA chain PEM for custom CA validation                          |

### PKCS#11 / SoftHSM2

| Property                  | Type              | Default           | Description                                                   |
| ------------------------- | ----------------- | ----------------- | ------------------------------------------------------------- |
| `CryptoTokenType`         | `CryptoTokenType` | `P12`             | `P12` (file-based) or `PKCS11` (SoftHSM2/HSM)                 |
| `Pkcs11SharedLibraryName` | `string`          | `SOFTHSM`         | PKCS#11 library name registered in SignServer                 |
| `Pkcs11SlotLabel`         | `string`          | `SignServerToken` | PKCS#11 token label                                           |
| `Pkcs11Pin`               | `string?`         | `null`            | Token PIN (direct value)                                      |
| `Pkcs11PinFile`           | `string?`         | `null`            | Path to file containing PIN (Docker Secret, takes precedence) |

### TSA and OCSP

| Property           | Type      | Default | Description                                                                 |
| ------------------ | --------- | ------- | --------------------------------------------------------------------------- |
| `TsaWorkerName`    | `string?` | `null`  | TimeStampSigner worker name for RFC 3161 timestamps                         |
| `OcspResponderUrl` | `string?` | `null`  | OCSP responder URL (e.g., `https://ejbca:8443/ejbca/publicweb/status/ocsp`) |

### Monitoring

| Property                         | Type   | Default | Description                                                                |
| -------------------------------- | ------ | ------- | -------------------------------------------------------------------------- |
| `EnableAuditLogging`             | `bool` | `false` | Log detailed signing audit events (correlationId, SHA256 hashes, duration) |
| `CertExpiryWarningDays`          | `int`  | `30`    | Days before expiry to trigger warnings                                     |
| `CertExpiryCheckIntervalMinutes` | `int`  | `60`    | How often to check certificate expiry                                      |
| `SigningRateLimitPerMinute`      | `int`  | `30`    | Max signing requests per minute per user                                   |

### Production Validation

The `ValidateProduction()` method enforces these rules:

- If `RequireMtls=true`, `ClientCertificatePath` must exist and password must be configured
- `SkipTlsValidation` cannot be `true` when `RequireMtls` is enabled
- `SignServerUrl` must use HTTPS when `RequireMtls` is enabled
- If `CryptoTokenType=PKCS11`, `Pkcs11SharedLibraryName` and PIN must be configured

### Example: Development Configuration

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "WorkerName": "PDFSigner",
    "SkipTlsValidation": true,
    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem",
    "CryptoTokenType": "P12"
  }
}
```

### Example: Production Configuration

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "WorkerName": "PDFSigner",
    "SkipTlsValidation": false,
    "RequireMtls": true,
    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem",
    "CryptoTokenType": "PKCS11",
    "Pkcs11SharedLibraryName": "SOFTHSM",
    "Pkcs11SlotLabel": "SignServerToken",
    "Pkcs11PinFile": "/run/secrets/softhsm_pin",
    "TsaWorkerName": "TimeStampSigner",
    "OcspResponderUrl": "https://ejbca:8443/ejbca/publicweb/status/ocsp",
    "EnableAuditLogging": true,
    "CertExpiryWarningDays": 60,
    "SigningRateLimitPerMinute": 30
  }
}
```

---

## 15. CLI Reference

### EJBCA CLI

All commands are run via: `docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh <command>`

#### Certificate Authority Management

```bash
# Initialize a self-signed Root CA (RSA 4096, 20yr)
# NOTE: Do NOT pass -certprofile -- EJBCA CE uses built-in defaults
ejbca.sh ca init \
    --caname "IVF-Root-CA" \
    --dn "CN=IVF Root Certificate Authority,O=IVF Healthcare,OU=PKI,C=VN" \
    --tokenType soft --tokenPass null \
    --keytype RSA --keyspec 4096 \
    -v 7305 -s SHA256WithRSA --policy null

# Initialize a Sub-CA signed by Root CA
# --signedby requires the numeric CA ID, not the name
ejbca.sh ca init \
    --caname "IVF-Signing-SubCA" \
    --dn "CN=IVF Document Signing CA,O=IVF Healthcare,OU=Digital Signing,C=VN" \
    --tokenType soft --tokenPass null \
    --keytype RSA --keyspec 4096 \
    -v 3652 -s SHA256WithRSA \
    --signedby <ROOT_CA_ID> --policy null

# List all CAs (output includes CA Name and Id on separate lines)
ejbca.sh ca listcas

# Export CA certificate to PEM file
ejbca.sh ca getcacert --caname "IVF-Root-CA" -f /tmp/root-ca.pem

# Edit certificate profile fields
ejbca.sh ca editcertificateprofile \
    --cpname "ENDUSER" \
    --field "useCRLDistributionPoint" \
    --value "true"
```

#### End Entity / Registration Authority

```bash
# Add a new end entity for certificate enrollment
ejbca.sh ra addendentity \
    --username "ivf-signer-1" \
    --dn "CN=IVF PDF Signer,O=IVF Healthcare,OU=Digital Signing,C=VN" \
    --caname "IVF-Signing-SubCA" \
    --type 1 --token P12 --password changeit \
    --certprofile ENDUSER --eeprofile EMPTY

# Reset end entity status to NEW (10) for re-enrollment
ejbca.sh ra setendentitystatus "ivf-signer-1" 10

# Set clear password for batch enrollment
ejbca.sh ra setclearpwd "ivf-signer-1" "changeit"

# Batch generate PKCS#12 keystore
ejbca.sh batch --username "ivf-signer-1" -dir /tmp/ejbca-certs

# Sign a CSR (create certificate from existing end entity)
ejbca.sh createcert \
    --username "ivf-signer-1" \
    --password changeit \
    -c /tmp/csr.pem \
    -f /tmp/signed-cert.pem
```

#### Admin Roles

```bash
# Add a certificate-based admin
ejbca.sh roles addrolemember \
    --role "Super Administrator Role" \
    --caname ManagementCA \
    --with CertificateAuthenticationToken \
    --value "CN=SuperAdmin,O=IVF Healthcare,C=VN"

# List admin role members
ejbca.sh roles listadmins
```

#### Protocol Configuration

```bash
# Enable OCSP protocol
ejbca.sh config protocols enable --name "OCSP"
```

### SignServer CLI

All commands are run via: `docker exec ivf-signserver /opt/signserver/bin/signserver <command>`

#### Worker Properties

```bash
# Set a single property on a worker
signserver setproperty <WORKER_ID> <PROPERTY_NAME> <VALUE>

# Example: set PKCS#11 crypto token
signserver setproperty 1 CRYPTOTOKEN_IMPLEMENTATION_CLASS \
    org.signserver.server.cryptotokens.PKCS11CryptoToken
signserver setproperty 1 SHAREDLIBRARYNAME SoftHSM
signserver setproperty 1 SLOTLABELTYPE SLOT_LABEL
signserver setproperty 1 SLOTLABELVALUE SignServerToken
signserver setproperty 1 PIN changeit

# Remove a property
signserver removeproperty <WORKER_ID> <PROPERTY_NAME>

# Load properties from file
signserver setproperties /tmp/worker.properties

# Get worker configuration
signserver getconfig <WORKER_ID>
```

#### Worker Lifecycle

```bash
# Reload worker after property changes
signserver reload <WORKER_ID>

# Activate crypto token (connect to keystore/HSM)
signserver activatecryptotoken <WORKER_ID> <PIN_OR_PASSWORD>

# Get worker status (brief)
signserver getstatus brief <WORKER_ID>

# Get status of all workers
signserver getstatus brief all
```

#### Key and Certificate Management

```bash
# Generate RSA 4096 key in PKCS#11 token
signserver generatekey <WORKER_ID> -keyalg RSA -keyspec 4096 -alias <KEY_ALIAS>

# Generate CSR from worker's key
signserver generatecertreq <WORKER_ID> \
    "CN=IVF PDF Signer,O=IVF Healthcare,OU=Digital Signing,C=VN" \
    SHA256WithRSA /tmp/csr.pem

# Upload signer certificate (GLOB = read from file)
signserver uploadsignercertificate <WORKER_ID> GLOB /tmp/cert.pem

# Upload certificate chain
signserver uploadsignercertificatechain <WORKER_ID> GLOB /tmp/chain.pem
```

#### Admin Access

```bash
# Allow any client certificate for admin access (development only)
signserver wsadmins -allowany

# Production: enforce admin list (disable allowany first, then add cert)
signserver wsadmins -allowany false
signserver wsadmins -add -cert /tmp/superadmin-cert.pem

# List current wsadmins
signserver wsadmins -list
```

#### Web Service Role Management (Auditors)

SignServer CE 7.3.2 has three Web Service roles managed independently:
- **wsadmins** — can configure workers and manage the system
- **wsauditors** — can read audit logs via WS/REST
- **wsarchiveauditors** — can access the archive (signed documents) via WS

```bash
# Add certificate to wsauditors (by PEM file)
signserver wsauditors -add -cert /tmp/superadmin-cert.pem

# Add by serial number + issuer DN
signserver wsauditors -add \
    -certserialno 4CC00E72EC60BA41AE2A91A386ABF9EBAAC2D33F \
    -issuerdn "CN=IVF Root Certificate Authority,OU=PKI,O=IVF Healthcare,C=VN"

# List authorized auditors
signserver wsauditors -list

# Remove by serial + issuer
signserver wsauditors -remove \
    -certserialno 4CC00E72EC60BA41AE2A91A386ABF9EBAAC2D33F \
    -issuerdn "CN=IVF Root Certificate Authority,OU=PKI,O=IVF Healthcare,C=VN"

# Same commands for archive auditors
signserver wsarchiveauditors -add -cert /tmp/superadmin-cert.pem
signserver wsarchiveauditors -list
```

> **Current state**: The superadmin cert (serial `4cc00e72ec60ba41ae2a91a386abf9ebaac2d33f`, issuer `CN=IVF Root Certificate Authority`) has been added to both `wsauditors` and `wsarchiveauditors`. `wsadmins` is still in `allowany` mode (any certificate accepted) — lock down for production.

## 16. Troubleshooting

### "SHAREDLIBRARYNAME SoftHSM is not referring to a defined value. Available library names: (empty)"

**Cause**: WildFly loaded `signserver_deploy.properties` before SoftHSM was registered. In SignServer CE 7.3.2, SoftHSM is **pre-registered** in the image's deploy.properties at index 1 (`cryptotoken.p11.lib.1.name = SoftHSM`). This error should not occur unless the deploy.properties was overwritten.

**Fix**: Verify the current state and trigger a restart if needed:

```bash
CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1)

# Check if SoftHSM is registered (should show index 1)
docker exec "$CONT" grep -i softhsm \
    /opt/keyfactor/signserver-custom/conf/signserver_deploy.properties

# Expected output:
# cryptotoken.p11.lib.1.name = SoftHSM
# cryptotoken.p11.lib.1.file = /usr/lib64/pkcs11/libsofthsm2.so

# If not present, force restart (the image default will be restored):
docker service update --force ivf_signserver
```

### "softhsm2.conf: Permission denied" in environment-hsm hook

**Cause**: The hook runs as uid=10001 (SignServer process user). The persistent volume root (`/opt/keyfactor/persistent/`) is owned by root with no group-write permission. Writing `softhsm2.conf` directly to the root fails.

**Fix**: Write to `/opt/keyfactor/persistent/softhsm/softhsm2.conf` (the `softhsm/` subdirectory is owned by uid=10001) and export `SOFTHSM2_CONF`:

```bash
# In environment-hsm hook:
SOFTHSM_CONF_FILE=/opt/keyfactor/persistent/softhsm/softhsm2.conf  # writable by uid=10001
cat > "${SOFTHSM_CONF_FILE}" << EOF
directories.tokendir = /opt/keyfactor/persistent/softhsm-tokens
log.level = INFO
EOF
export SOFTHSM2_CONF="${SOFTHSM_CONF_FILE}"
```

Since the hook is **sourced** (not executed) by `start.sh`, the `export` propagates to the WildFly JVM process.

> **Important**: Do NOT try to write `/etc/softhsm2.conf` from the hook. It requires root. Use `SOFTHSM2_CONF` env var instead.

### EJBCA XML profile import bugs {#ejbca-xml-profile-import-bugs}

**Cause**: EJBCA CE's XML profile import fails unless the XML was generated from the correct base objects. Four known bugs:

1. **`SUBJECTALTNAMEFIELDORDER` key**: The constant has no underscore in the internal DataMap key. Generating from an empty `EndEntityProfile()` misses this.
2. **`NUMBERARRAY` missing in EndEntityProfile**: Must use `EndEntityProfile(true)` constructor (loads hardcoded defaults). An empty `new EndEntityProfile()` is missing critical internal arrays.
3. **`CertificateProfile` incomplete**: Must use `CertificateProfile(1)` (ENDUSER type) as base. An empty `new CertificateProfile()` causes NPE in `getCRLDistributionPointCritical()` during import.
4. **PASSWORD data entry**: `paramNum=1`, `use_key=1000001` — requires `data.put(1000001, Boolean.TRUE)` in the DataContainer, which is only present when the PASSWORD field is explicitly configured.

**Fix**: Generate profile XMLs using a Java helper compiled against the EJBCA JAR, using the correct constructors:
```java
CertificateProfile cp = new CertificateProfile(CertificateProfileConstants.CERTPROFILE_FIXED_ENDUSER); // NOT new CertificateProfile()
EndEntityProfile eep = new EndEntityProfile(true);  // NOT new EndEntityProfile()
```

**Workaround**: If generating from source is not feasible, use the EJBCA Admin Web UI to create profiles and export them for re-import.

### "signserver_deploy.properties changes are lost after container restart"

**Cause**: `/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties` is in the **ephemeral** container filesystem — it is reset to the image default every time the container restarts. Only the `/opt/keyfactor/persistent/` directory (Docker volume `ivf_signserver_persistent`) survives restarts.

**Fix**: Never write PKCS#11 library registration directly to `signserver_deploy.properties` at runtime. Instead, use the persistent startup hook:

```
/opt/keyfactor/persistent/environment-hsm
```

This file is sourced by `start.sh` on every container startup and writes the library entries before WildFly loads. See Phase 3 of `setup-pkcs11-workers.sh` for the exact hook content.

### "SoftHSM2 library not found in SignServer container"

**Note**: In **SignServer CE 7.3.2**, SoftHSM2 is **pre-installed** as a system package (`/usr/lib64/pkcs11/libsofthsm2.so`). This error should not occur on 7.3.2+.

If you are on an older custom image that does NOT include SoftHSM2, or if the library is missing:

**Cause**: The SignServer CE image is based on **AlmaLinux 9** (minimal). Ubuntu-built `libsofthsm2.so` is incompatible (requires `GLIBC_2.38` which is not present on AlmaLinux 9).

**Fix** (for older images without pre-installed SoftHSM2): Pull AlmaLinux 9 native SoftHSM2 binaries via a temporary container:

```bash
docker rm -f softhsm-src 2>/dev/null || true
docker run -d --name softhsm-src almalinux:9 \
    sh -c "dnf install -y softhsm 2>&1 | tail -2 && sleep 30"
sleep 20
docker cp softhsm-src:/usr/lib64/pkcs11/libsofthsm2.so /tmp/libsofthsm2.so
docker cp softhsm-src:/usr/bin/softhsm2-util            /tmp/softhsm2-util
docker rm -f softhsm-src

# Copy to persistent volume in SignServer container
CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1)
docker exec --user root "$CONT" mkdir -p /opt/keyfactor/persistent/softhsm/lib \
                                         /opt/keyfactor/persistent/softhsm/bin
docker cp /tmp/libsofthsm2.so "$CONT:/opt/keyfactor/persistent/softhsm/lib/libsofthsm2.so"
docker cp /tmp/softhsm2-util  "$CONT:/opt/keyfactor/persistent/softhsm/bin/softhsm2-util"
docker exec --user root "$CONT" \
    chmod 644 /opt/keyfactor/persistent/softhsm/lib/libsofthsm2.so
docker exec --user root "$CONT" \
    chmod 755 /opt/keyfactor/persistent/softhsm/bin/softhsm2-util
```

### Container filter matches `ivf_signserver-db` instead of `ivf_signserver`

**Cause**: `docker ps --filter name=ivf_signserver` matches any container whose name contains the string, including `ivf_signserver-db.1.xxx`.

**Fix**: Always filter out `-db` suffixes:

```bash
CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1)
```

### SoftHSM2 PIN is not "changeit"

**Cause**: In a Docker Swarm deployment the actual PIN values come from Docker secrets, not hardcoded defaults. The secrets `softhsm_pin` and `softhsm_so_pin` are mounted into the container.

**Read the actual PIN at runtime**:

```bash
CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1)
SOFTHSM_PIN=$(docker exec "$CONT" cat /run/secrets/softhsm_pin)
SOFTHSM_SO_PIN=$(docker exec "$CONT" cat /run/secrets/softhsm_so_pin)
```

The `environment-hsm` hook reads the PIN from these same secret files automatically.

### "Token label not found" / SoftHSM library not available (older Dockerfile approach)

**Cause**: Applies only if you are using a **custom-built SignServer image** that registers SoftHSM during `ant deploy`. The SoftHSM2 PKCS#11 library was not registered in `signserver_deploy.properties` at Docker image build time — or was registered but with the wrong index or name.

**Fix** (custom image approach): Rebuild the Docker image to include SoftHSM registration:

```bash
docker compose --profile softhsm build signserver-softhsm
docker compose --profile softhsm up -d signserver-softhsm
```

For the standard image + persistent hook approach, see the two troubleshooting entries above instead.

### "SHAREDLIBRARY is not permitted"

**Cause**: You used `SHAREDLIBRARY` (full path) instead of `SHAREDLIBRARYNAME` (registered name).

**Fix**: Use the registered library name, not the file path:

```bash
# Wrong:
signserver setproperty 1 SHAREDLIBRARY /usr/lib64/pkcs11/libsofthsm2.so

# Correct:
signserver setproperty 1 SHAREDLIBRARYNAME SoftHSM
```

### "Can not read private key" / Key not found

**Cause**: Key was generated after the crypto token was activated, or the worker was not reloaded after key generation.

**Fix**: Reload and re-activate after generating the key:

```bash
docker exec ivf-signserver /opt/signserver/bin/signserver reload 1
docker exec ivf-signserver /opt/signserver/bin/signserver activatecryptotoken 1 changeit
```

### "Activation FAILED"

**Cause**: No key exists in the PKCS#11 token for this worker. The crypto token cannot activate without at least one key.

**Fix**: Generate a key first:

```bash
docker exec ivf-signserver /opt/signserver/bin/signserver \
    generatekey 1 -keyalg RSA -keyspec 4096 -alias signer
docker exec ivf-signserver /opt/signserver/bin/signserver reload 1
docker exec ivf-signserver /opt/signserver/bin/signserver activatecryptotoken 1 changeit
```

### "SET_PERMISSIONS unknown property"

**Cause**: `SET_PERMISSIONS` is a P12-specific property that does not apply to PKCS#11 workers.

**Fix**: Remove the property:

```bash
docker exec ivf-signserver /opt/signserver/bin/signserver removeproperty 1 SET_PERMISSIONS
docker exec ivf-signserver /opt/signserver/bin/signserver reload 1
```

### "Enforce unique DN" / End entity already exists

**Cause**: EJBCA enforces unique Distinguished Names. The end entity was previously enrolled and its status is not NEW.

**Fix**: Reset the existing end entity to status 10 (NEW) and re-use it:

```bash
docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh ra setendentitystatus ivf-signer-1 10
docker exec ivf-ejbca /opt/keyfactor/bin/ejbca.sh ra setclearpwd ivf-signer-1 changeit
```

### "Signer certificate not included in certificate chain"

**Cause**: The uploaded certificate chain is incomplete. SignServer requires the full chain: signer cert + Sub-CA cert + Root CA cert.

**Fix**: Build and upload the complete chain:

```bash
# Combine: signer cert + Sub-CA + Root CA
cat signer-cert.pem sub-ca.pem root-ca.pem > chain.pem

docker cp chain.pem ivf-signserver:/tmp/chain.pem
docker exec ivf-signserver /opt/signserver/bin/signserver \
    uploadsignercertificatechain 1 GLOB /tmp/chain.pem
docker exec ivf-signserver /opt/signserver/bin/signserver reload 1
```

### Ports not published / cannot access admin web

**Cause**: The service is not on the `ivf-public` network, so ports are not reachable from the Docker host.

**Fix**: Ensure the service has `ivf-public` in its `networks` list in `docker-compose.yml`.

### Admin web "Client certificate required"

**Cause**: The browser does not have a client certificate that is trusted by the EJBCA/SignServer instance.

**Fix**:

1. Import `superadmin.p12` into your browser (password: `changeit`)
2. Ensure the ManagementCA root certificate is in your browser's trusted authorities
3. Clear SSL state and retry

### MSYS path conversion on Windows (Git Bash)

**Cause**: Git Bash on Windows (MSYS) automatically converts Unix-style paths in command arguments, breaking Docker exec commands that contain paths like `/opt/keyfactor/...`.

**Fix**: Set environment variables before running the script:

```bash
export MSYS_NO_PATHCONV=1
export MSYS2_ARG_CONV_EXCL="*"
bash scripts/setup-enterprise-pki.sh
```

The setup script sets these automatically at the top of the file.

### EJBCA CE: no apt-get

**Cause**: EJBCA CE is based on AlmaLinux 9 (minimal), which does not include `apt-get`.

**Fix**: Use `microdnf` instead:

```bash
docker exec --user root ivf-ejbca microdnf install -y <package>
```

### SignServer health OK but signing fails

**Cause**: The health endpoint (`/signserver/healthcheck/signserverhealth`) checks basic service health, not individual worker status. A worker may be configured but not Active.

**Fix**: Check individual worker status:

```bash
docker exec ivf-signserver /opt/signserver/bin/signserver getstatus brief all
```

Look for workers with errors. Common causes: crypto token not activated, certificate not uploaded, key alias mismatch.

---

## 17. Security Considerations

### SoftHSM2 vs P12 Security Comparison

| Aspect                       | P12 (KeystoreCryptoToken)       | PKCS#11 (SoftHSM2)                                   |
| ---------------------------- | ------------------------------- | ---------------------------------------------------- |
| Key storage                  | PKCS#12 file on disk            | PKCS#11 token (file-backed, encrypted)               |
| Key extraction               | Possible with keystore password | **Not possible** (CKA_EXTRACTABLE=false)             |
| FIPS compliance              | None                            | FIPS 140-2 Level 1                                   |
| Access control               | File permissions + password     | PIN-authenticated token session                      |
| Key compromise via file copy | Yes                             | No (token files are encrypted, keys non-extractable) |
| Upgrade path to hardware HSM | Requires re-enrollment          | Change `SHAREDLIBRARYNAME` only                      |

### Network Security

- **Internal networks**: The `ivf-signing` and `ivf-data` networks are configured with `internal: true`, which means containers on these networks cannot reach the internet. All signing traffic stays within Docker.
- **No HTTP signing endpoint**: The SignServer HTTP port (9080) is not published. All signing goes through HTTPS on port 8443 within the Docker network. The API accesses SignServer via the internal hostname `signserver:8443`.
- **mTLS**: When `RequireMtls=true`, the API presents `api-client.p12` to SignServer. SignServer validates the client certificate against its truststore.

### Certificate Expiry Monitoring

The `CertExpiryWarningDays` (default: 30) and `CertExpiryCheckIntervalMinutes` (default: 60) settings control automatic monitoring. When a worker's certificate is within the warning threshold, the system logs warnings.

To manually check certificate expiry:

```bash
# Check all worker certificates
docker exec ivf-signserver /opt/signserver/bin/signserver getstatus brief all

# Check specific certificate details
docker exec ivf-signserver keytool -list -v \
    -keystore /opt/keyfactor/persistent/keys/signer.p12 \
    -storepass changeit -storetype PKCS12
```

### Audit Logging

When `EnableAuditLogging=true`, every signing operation logs:

- **Correlation ID**: 12-character unique identifier linking request and response
- **Input document SHA256 hash**: Before signing
- **Output document SHA256 hash**: After signing
- **Worker name**: Which worker performed the signing
- **Signer name**: User who initiated the request
- **Duration**: Milliseconds taken
- **Timestamp**: UTC ISO 8601

Format: `AUDIT[{CorrelationId}]: Signing {SUCCESS|FAILED} -- Worker={Worker}, ...`

### Key Backup Strategy

For SoftHSM2 tokens:

- The token data is stored on Docker volume `signserver_persistent` at path `/opt/keyfactor/persistent/softhsm/tokens`
- Back up this volume as part of your regular Docker volume backup procedure
- The token files are encrypted by SoftHSM2, but the backup should still be stored securely
- To restore: restore the volume and restart SignServer. Workers will need to be re-activated with the PIN

For P12 keystores:

- Keystores are exported to `certs/signers/` on the host during setup
- Store these files securely (encrypted at rest)
- The keystore password (`changeit`) should be changed for production

### Security Hardening Checklist

- [ ] Change `INITIAL_ADMIN` from `PublicAccessAuthenticationToken:TRANSPORT_ANY` to certificate-based auth after initial setup
- [ ] Change all default passwords (`changeit`, `ejbca_secret`, `signserver_secret`)
- [ ] Set `SkipTlsValidation=false` and `RequireMtls=true` for production
- [ ] Use Docker Secrets for all passwords (`ClientCertificatePasswordFile`, `Pkcs11PinFile`)
- [ ] Restrict WireGuard VPN access to authorized administrators only
- [ ] Enable audit logging (`EnableAuditLogging=true`)
- [ ] Create dedicated EJBCA certificate profiles with proper key usage constraints
- [ ] Set `read_only: true` on SignServer container where possible
- [ ] Use `no-new-privileges` security option (already set in docker-compose.yml)
- [ ] Monitor certificate expiry and set up alerting
- [ ] **Disable `wsadmins -allowany`**: Run `signserver wsadmins -allowany false` and add specific admin certs with `signserver wsadmins -add -cert /path/superadmin.pem`
- [ ] **wsauditors/wsarchiveauditors**: Add admin certificates to `wsauditors` and `wsarchiveauditors` for audit log access (done: superadmin cert `4cc00e72ec60ba41ae2a91a386abf9ebaac2d33f` added)

---

## 18. Script Reference

### Usage

```bash
# Full PKI setup (CAs + cert profiles + workers with P12)
bash scripts/setup-enterprise-pki.sh [OPTIONS]

# SoftHSM2 + PKCS#11 worker migration (run ON the VPS)
scp scripts/setup-pkcs11-workers.sh root@10.200.0.1:/tmp/
ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/setup-pkcs11-workers.sh && \
    bash /tmp/setup-pkcs11-workers.sh 2>&1 | tee /tmp/pkcs11-setup.log"
```

### setup-enterprise-pki.sh Options

### setup-enterprise-pki.sh Options

| Flag             | Description                                                                                                                                       |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--dry-run`      | Preview all actions without making any changes. Commands are printed but not executed.                                                            |
| `--skip-ca`      | Skip Phase 1 (CA creation). Use when CAs already exist and you only need to re-enroll certificates or reconfigure workers.                        |
| `--skip-workers` | Skip Phase 5 (SignServer worker configuration). Use when workers are already configured and you only need to update CA hierarchy or certificates. |
| `--force`        | Recreate everything, even if already exists. **Destructive**: re-initializes SoftHSM tokens, re-enrolls certificates.                             |
| `--tenant <id>`  | Create a tenant-specific Sub-CA and signer certificate. Can be combined with other flags.                                                         |
| `-h`, `--help`   | Show help message and exit.                                                                                                                       |

### Environment Variables

| Variable   | Default      | Description                                                                              |
| ---------- | ------------ | ---------------------------------------------------------------------------------------- |
| `VPN_HOST` | `10.200.0.1` | EJBCA/SignServer host address. Override for non-VPN setups (e.g., `VPN_HOST=localhost`). |

### Examples

```bash
# Full setup (first time)
bash scripts/setup-enterprise-pki.sh

# Preview without changes
bash scripts/setup-enterprise-pki.sh --dry-run

# Re-configure workers only (CAs already exist)
bash scripts/setup-enterprise-pki.sh --skip-ca

# Recreate everything from scratch
bash scripts/setup-enterprise-pki.sh --force

# Create tenant PKI
bash scripts/setup-enterprise-pki.sh --tenant clinic-01

# Use localhost instead of VPN
VPN_HOST=localhost bash scripts/setup-enterprise-pki.sh

# Re-enroll certificates without touching CAs or workers
bash scripts/setup-enterprise-pki.sh --skip-ca --skip-workers
```

### Idempotency

The script is designed to be re-run safely:

- CA creation checks if the CA already exists (`ca listcas | grep`)
- End entity enrollment catches "already exists" errors and resets the entity to NEW (status 10)
- SoftHSM token initialization checks for existing tokens before re-creating
- Worker property changes are additive (set/remove individual properties)
- `--force` is required to override existing resources

### Exit Behavior

- `set -euo pipefail` is enabled: the script exits on any unhandled error
- Individual commands that may legitimately fail (e.g., removing a non-existent property) are suffixed with `|| true`
- Phase 8 verification counts pass/fail but does not exit non-zero on failures

---

## 19. File Inventory

### Scripts

| File                              | Purpose                                                                                                                                                                                  |
| --------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `scripts/setup-enterprise-pki.sh`   | Main PKI setup script — Phases 1–8 (CAs, cert profiles, P12 enrollment, worker config)                                                                                                   |
| `scripts/setup-pkcs11-workers.sh`   | **SoftHSM2 + PKCS#11 migration** — Phases 3–5 (persistent SoftHSM2 setup, `environment-hsm` hook, EJBCA EE creation, PKCS#11 worker config + RSA key gen + cert enrollment). Run on VPS. |
| `scripts/init-ejbca-rest.sh`        | EJBCA REST API initialization (mounted into container)                                                                                                                                   |
| `scripts/init-mtls.sh`              | mTLS configuration script for SignServer                                                                                                                                                 |
| `scripts/init-tsa.sh`               | TSA (Timestamp Authority) initialization script                                                                                                                                          |
| `scripts/probe-rest-auth.sh`        | Probes SignServer REST authentication — tests mTLS and basic auth modes, reports which endpoints are reachable                                                                            |
| `scripts/test-importprofiles.sh`    | Tests EJBCA certificate and end entity profile XML import — validates XML format and import success for profiles in `certs/ejbca/`                                                        |

### Docker

| File                                   | Purpose                                                            |
| -------------------------------------- | ------------------------------------------------------------------ |
| `docker-compose.yml`                   | Full stack definition (EJBCA, SignServer, databases, MinIO, Redis) |
| `docker/signserver-softhsm/Dockerfile` | Custom SignServer image with SoftHSM2 PKCS#11 support              |

### Certificates (generated by setup script)

| File                                          | Purpose                                         |
| --------------------------------------------- | ----------------------------------------------- |
| `certs/ca/root-ca.pem`                        | IVF-Root-CA certificate (PEM)                   |
| `certs/ca/sub-ca.pem`                         | IVF-Signing-SubCA certificate (PEM)             |
| `certs/ca/ca.pem`                             | Copy of root-ca.pem (backward compatibility)    |
| `certs/ca-chain.pem`                          | CA chain: Sub-CA + Root CA (PEM)                |
| `certs/signers/signer.p12`                    | PDFSigner (Worker 1) keystore                   |
| `certs/signers/pdfsigner_technical.p12`       | PDFSigner_technical (Worker 272) keystore       |
| `certs/signers/pdfsigner_head_department.p12` | PDFSigner_head_department (Worker 444) keystore |
| `certs/signers/pdfsigner_doctor1.p12`         | PDFSigner_doctor1 (Worker 597) keystore         |
| `certs/signers/pdfsigner_admin.p12`           | PDFSigner_admin (Worker 907) keystore           |
| `certs/tsa/tsa.p12`                           | TimeStampSigner (Worker 100) keystore           |
| `certs/api/api-client.p12`                    | API mTLS client certificate keystore            |
| `certs/ejbca-admin.p12`                       | EJBCA admin client certificate (ManagementCA)   |
| `certs/tenants/{id}/ca.pem`                   | Tenant Sub-CA certificate                       |
| `certs/tenants/{id}/ca-chain.pem`             | Tenant CA chain (tenant CA + root CA)           |
| `certs/tenants/{id}/signer.p12`               | Tenant signer keystore                          |

### EJBCA Profile XMLs (`certs/ejbca/`)

These XML files are imported into EJBCA to configure certificate and end entity profiles.

| File                                                        | Profile Type        | ID   | Purpose                            |
| ----------------------------------------------------------- | ------------------- | ---- | ---------------------------------- |
| `certs/ejbca/certprofile_IVF-PDFSigner-Profile-5001.xml`   | Certificate profile | 5001 | PDF signer key usage + constraints |
| `certs/ejbca/certprofile_IVF-TSA-Profile-5002.xml`         | Certificate profile | 5002 | TSA (Timestamp Authority) profile  |
| `certs/ejbca/certprofile_IVF-TLS-Client-Profile-5003.xml`  | Certificate profile | 5003 | TLS client certificate profile     |
| `certs/ejbca/entityprofile_IVF-PDFSigner-EEProfile-6001.xml` | End entity profile | 6001 | End entity for PDF signers         |
| `certs/ejbca/entityprofile_IVF-TSA-EEProfile-6002.xml`     | End entity profile  | 6002 | End entity for TSA                 |
| `certs/ejbca/entityprofile_IVF-TLS-Client-EEProfile-6003.xml` | End entity profile | 6003 | End entity for TLS clients         |

> **Import command**: `docker exec ivf-ejbca ejbca.sh ca importcertificateprofile -cpf /tmp/certprofile_....xml` and `ejbca.sh ra importeeprofile -eepf /tmp/entityprofile_....xml`  
> Copy files first: `docker cp certs/ejbca/certprofile_*.xml <container>:/tmp/`

### Secrets

| File                            | Purpose                                                |
| ------------------------------- | ------------------------------------------------------ |
| `secrets/api_cert_password.txt` | Password for api-client.p12 (mounted as Docker Secret) |

### Source Code

| File                                                              | Purpose                                                                             |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `src/IVF.API/Services/DigitalSigningOptions.cs`                   | Configuration options class (`CryptoTokenType` enum, `DigitalSigningOptions` class) |
| `src/IVF.API/Services/SignServerDigitalSigningService.cs`         | SignServer REST client implementation                                               |
| `src/IVF.API/Services/PdfSignatureImageService.cs`                | Visible signature image overlay service                                             |
| `src/IVF.Application/Common/Interfaces/IDigitalSigningService.cs` | Signing service interface (Application layer)                                       |

### Container Paths

| Path (inside container)                                              | Container      | Persistent?                                         | Purpose                                                                            |
| -------------------------------------------------------------------- | -------------- | --------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `/opt/keyfactor/bin/ejbca.sh`                                        | ivf-ejbca      | —                                                   | EJBCA CLI                                                                          |
| `/opt/signserver/bin/signserver`                                     | ivf-signserver | —                                                   | SignServer CLI                                                                     |
| `/opt/keyfactor/persistent/`                                         | ivf-signserver | **YES** (Docker volume `ivf_signserver_persistent`) | All persistent data                                                                |
| `/opt/keyfactor/persistent/environment-hsm`                          | ivf-signserver | **YES**                                             | Startup hook — sourced by `start.sh` on every restart. Writes `softhsm2.conf` and exports `SOFTHSM2_CONF`. |
| `/usr/lib64/pkcs11/libsofthsm2.so`                                   | ivf-signserver | **NO (ephemeral)**                                  | SoftHSM2 PKCS#11 library — **pre-installed in SignServer CE 7.3.2 image**, no custom build needed. Also pre-registered in `deploy.properties` at index 1. |
| `/opt/keyfactor/persistent/softhsm/softhsm2.conf`                    | ivf-signserver | **YES**                                             | SoftHSM2 configuration — written by `environment-hsm` hook on every restart. Sets `tokendir = ../softhsm-tokens`. |
| `/opt/keyfactor/persistent/softhsm-tokens/`                          | ivf-signserver | **YES**                                             | **Active** SoftHSM2 token storage (private key material). Located directly under persistent volume root. |
| `/opt/keyfactor/persistent/keys/`                                    | ivf-signserver | **YES**                                             | P12 keystore files                                                                 |
| `/opt/keyfactor/persistent/keys/ca-chain.pem`                        | ivf-signserver | **YES**                                             | CA chain for trust validation                                                      |
| `/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties` | ivf-signserver | **NO (ephemeral)**                                  | WildFly PKCS#11 library config — **pre-configured in image** with `cryptotoken.p11.lib.1.name = SoftHSM` at index 1. Hook no longer rewrites this file. |
| `/run/secrets/softhsm_pin`                                           | ivf-signserver | —                                                   | Docker secret: SoftHSM2 user PIN                                                   |
| `/run/secrets/softhsm_so_pin`                                        | ivf-signserver | —                                                   | Docker secret: SoftHSM2 SO (Security Officer) PIN                                  |
| `/tmp/ejbca-certs/`                                                  | ivf-ejbca      | —                                                   | Temporary directory for batch-generated keystores                                  |
| `/app/certs/`                                                        | ivf-api        | —                                                   | Mounted certificates for API                                                       |
| `/run/secrets/api_cert_password`                                     | ivf-api        | —                                                   | API cert password (Docker Secret mount)                                            |

### Docker Volumes

| Volume (Swarm stack name)   | Purpose                                                                                      |
| --------------------------- | -------------------------------------------------------------------------------------------- |
| `ivf_ejbca_persistent`      | EJBCA application data and crypto tokens                                                     |
| `ivf_ejbca_db_data`         | EJBCA PostgreSQL data                                                                        |
| `ivf_signserver_persistent` | SignServer persistent data: P12 keystores, `softhsm-tokens/`, `softhsm/softhsm2.conf`, `environment-hsm` hook |
| `ivf_signserver_db_data`    | SignServer PostgreSQL data                                                                                      |
| `ivf_softhsm_tokens`        | **Legacy / unused.** Mounted at `/opt/keyfactor/persistent/softhsm/tokens` inside the container. Active tokens are in `ivf_signserver_persistent/softhsm-tokens`. |

## 20. Live Deployment Status

*Last verified: March 16, 2026*

### SignServer Workers

| Worker ID | Name                        | Status       | Key Alias                   | Crypto Token    |
| --------- | --------------------------- | ------------ | --------------------------- | --------------- |
| 1         | PDFSigner                   | ✅ ACTIVE    | `signer`                    | SoftHSM2 PKCS#11 |
| 100       | TimeStampSigner             | ✅ ACTIVE    | `tsa`                       | SoftHSM2 PKCS#11 |
| 272       | PDFSigner_technical         | ✅ ACTIVE    | `pdfsigner_technical`       | SoftHSM2 PKCS#11 |
| 444       | PDFSigner_head_department   | ✅ ACTIVE    | `pdfsigner_head_department` | SoftHSM2 PKCS#11 |
| 597       | PDFSigner_doctor1           | ✅ ACTIVE    | `pdfsigner_doctor1`         | SoftHSM2 PKCS#11 |
| 907       | PDFSigner_admin             | ✅ ACTIVE    | `pdfsigner_admin`           | SoftHSM2 PKCS#11 |

All 6 workers are ACTIVE with keys stored in SoftHSM2 inside the `ivf_signserver_persistent` volume (`softhsm-tokens/`).

### Web Service Roles

| Role                  | Mode / Status                                   | Authorized Certificates                                           |
| --------------------- | ----------------------------------------------- | ----------------------------------------------------------------- |
| `wsadmins`            | ⚠️ **allowany** (all client certs accepted)     | ANY — **should be restricted before production**                  |
| `wsauditors`          | ✅ Cert-based                                   | Serial `4cc00e72ec60ba41ae2a91a386abf9ebaac2d33f` (superadmin)   |
| `wsarchiveauditors`   | ✅ Cert-based                                   | Serial `4cc00e72ec60ba41ae2a91a386abf9ebaac2d33f` (superadmin)   |

> **Action required**: Disable allowany on `wsadmins` and add specific admin certificate(s) before going to production.  
> See Section 15 for commands and Section 17 checklist for the security action item.

### EJBCA Certificate Authorities

| CA Name               | CA ID       | Status             | Subject DN                                                                           | Expires |
| --------------------- | ----------- | ------------------ | ------------------------------------------------------------------------------------ | ------- |
| ManagementCA          | (auto)      | Active (internal)  | `CN=ManagementCA`                                                                    | ~2027   |
| IVF Internal Root CA  | 995596930   | Active (legacy)    | `CN=IVF Internal Root CA,OU=IT Department,O=IVF Clinic,ST=Ho Chi Minh,C=VN`         | 2036    |
| IVF-Root-CA           | 1031502430  | ✅ Active (primary) | `CN=IVF Root Certificate Authority,OU=PKI,O=IVF Healthcare,C=VN`                   | 2046    |
| IVF-Signing-SubCA     | 1728368285  | ✅ Active           | `CN=IVF Document Signing CA,OU=Digital Signing,O=IVF Healthcare,C=VN`              | 2036    |

> **Note**: `IVF Internal Root CA` (ID 995596930) is a legacy CA created during initial testing. It uses a different DN from `IVF-Root-CA`. All production certificates should be issued under `IVF-Signing-SubCA` chaining to `IVF-Root-CA`.

### EJBCA Certificate Profiles

| Profile Name               | ID   | Issuing CA          | Status        |
| -------------------------- | ---- | ------------------- | ------------- |
| IVF-PDFSigner-Profile      | 5001 | IVF-Signing-SubCA   | ✅ Imported   |
| IVF-TSA-Profile            | 5002 | IVF-Signing-SubCA   | ✅ Imported   |
| IVF-TLS-Client-Profile     | 5003 | IVF-Signing-SubCA   | ✅ Imported   |

### EJBCA End Entity Profiles

| Profile Name                   | ID   | Certificate Profile       | Status        |
| ------------------------------ | ---- | ------------------------- | ------------- |
| IVF-PDFSigner-EEProfile        | 6001 | IVF-PDFSigner-Profile     | ✅ Imported   |
| IVF-TSA-EEProfile              | 6002 | IVF-TSA-Profile           | ✅ Imported   |
| IVF-TLS-Client-EEProfile       | 6003 | IVF-TLS-Client-Profile    | ✅ Imported   |

### Superadmin Certificate

| Attribute  | Value                                                         |
| ---------- | ------------------------------------------------------------- |
| Serial     | `4cc00e72ec60ba41ae2a91a386abf9ebaac2d33f`                    |
| Issuer CN  | `CN=IVF Root Certificate Authority`                           |
| Usage      | EJBCA admin, `wsauditors`, `wsarchiveauditors` web service roles |
| Store      | `certs/ejbca/superadmin.p12`                                  |

### SoftHSM2 Status

| Property                       | Value                                                              |
| ------------------------------ | ------------------------------------------------------------------ |
| Library (image)                | `/usr/lib64/pkcs11/libsofthsm2.so` (pre-installed, ephemeral)     |
| `deploy.properties` entry      | `cryptotoken.p11.lib.1.name = SoftHSM` (index 1, pre-configured)  |
| Token directory                | `/opt/keyfactor/persistent/softhsm-tokens/` (persistent volume)   |
| `softhsm2.conf`                | `/opt/keyfactor/persistent/softhsm/softhsm2.conf` (written by hook) |
| `SOFTHSM2_CONF` env var        | Exported by `environment-hsm` hook on every restart               |
| `ivf_softhsm_tokens` volume    | Legacy/unused — mounted at `softhsm/tokens`, not the active path  |
