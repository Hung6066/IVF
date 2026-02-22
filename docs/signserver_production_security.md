# SignServer Production Security — Hướng dẫn triển khai không dùng HSM

> **Phiên bản:** 1.0  
> **Ngày:** 2026-02-21  
> **Áp dụng cho:** IVF System — SignServer CE 7.3.2 + EJBCA CE

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Đánh giá hiện trạng](#2-đánh-giá-hiện-trạng)
3. [Kiến trúc bảo mật Production](#3-kiến-trúc-bảo-mật-production)
4. [So sánh các phương án bảo vệ Private Key](#4-so-sánh-các-phương-án-bảo-vệ-private-key)
5. [Hướng dẫn triển khai từng bước](#5-hướng-dẫn-triển-khai-từng-bước)
6. [Cấu hình chi tiết](#6-cấu-hình-chi-tiết)
7. [Kiểm tra và xác minh](#7-kiểm-tra-và-xác-minh)
8. [Vận hành và giám sát](#8-vận-hành-và-giám-sát)
9. [Disaster Recovery](#9-disaster-recovery)
10. [Checklist triển khai](#10-checklist-triển-khai)

---

## 1. Tổng quan

### 1.1. Vấn đề

Hệ thống IVF sử dụng SignServer CE ký số PDF báo cáo y tế. Hiện tại, private key (PKCS#12) được lưu trữ **không an toàn** trong container, không có HSM phần cứng. Tài liệu này hướng dẫn cách **hardening production** mà không cần đầu tư HSM vật lý.

### 1.2. Kiến trúc hiện tại (After Phase 2)

```
┌──────────┐  HTTPS (9443)  ┌──────────────┐   PKCS#12     ┌─────────────────────┐
│  IVF API │ ──────────────▶│  SignServer   │◀─────────────▶│ persistent/keys/*.p12│
│          │  mTLS (P12)    │  CE 7.3.2    │  chmod 400    │ (ClientCertAuth)     │
│          │  ClientCert    │  WildFly     │               └─────────────────────┘
└──────────┘                └──────┬───────┘
                                   │ cert issue
                            ┌──────▼───────┐
                            │   EJBCA CE   │
                            └──────────────┘
```

### 1.3. Worker hiện tại

| Worker ID | Tên                       | Crypto Token   | Key Path                                                       | Auth                 | Signings |
| --------- | ------------------------- | -------------- | -------------------------------------------------------------- | -------------------- | -------- |
| 1         | PDFSigner                 | P12CryptoToken | `/opt/keyfactor/persistent/keys/signer.p12`                    | ClientCertAuthorizer | 36       |
| 272       | PDFSigner_techinical      | P12CryptoToken | `/opt/keyfactor/persistent/keys/pdfsigner_techinical.p12`      | ClientCertAuthorizer | 4        |
| 444       | PDFSigner_head_department | P12CryptoToken | `/opt/keyfactor/persistent/keys/pdfsigner_head_department.p12` | ClientCertAuthorizer | 1        |
| 597       | PDFSigner_doctor1         | P12CryptoToken | `/opt/keyfactor/persistent/keys/pdfsigner_doctor1.p12`         | ClientCertAuthorizer | 2        |
| 907       | PDFSigner_admin           | P12CryptoToken | `/opt/keyfactor/persistent/keys/pdfsigner_admin.p12`           | ClientCertAuthorizer | 12       |

---

## 2. Đánh giá hiện trạng

### 2.1. Lỗ hổng nghiêm trọng

| #   | Lỗ hổng                             | Mô tả                                                                                            | Mức độ      | CVSS |
| --- | ----------------------------------- | ------------------------------------------------------------------------------------------------ | ----------- | ---- |
| V1  | **Private key ở `/tmp/`**           | File `.p12` permission `644` (world-readable), nằm trong thư mục tạm                             | 🔴 Critical | 9.1  |
| V2  | **PublicAccessAuthenticationToken** | Admin web không yêu cầu xác thực — bất kỳ ai truy cập được port 9443 đều quản lý được SignServer | 🔴 Critical | 9.8  |
| V3  | **AUTHTYPE=NOAUTH**                 | Worker chấp nhận mọi request ký — không kiểm tra identity caller                                 | 🔴 Critical | 9.0  |
| V4  | **HTTP signing (không TLS)**        | API ↔ SignServer giao tiếp qua plain HTTP — dữ liệu PDF truyền không mã hóa                      | 🟠 High     | 7.5  |
| V5  | **Password plaintext**              | DB password, MinIO credentials, JWT secret lưu trực tiếp trong `docker-compose.yml`              | 🟠 High     | 7.0  |
| V6  | **Port 9080 exposed**               | SignServer HTTP API lộ ra host network — có thể bị gọi từ bên ngoài container                    | 🟡 Medium   | 5.3  |
| V7  | **Không có audit log**              | Không ghi log chi tiết ai ký gì, khi nào                                                         | 🟡 Medium   | 4.0  |

### 2.2. Chuỗi tấn công tiềm tàng

```
Attack Scenario 1: Unauthorized Signing
─────────────────────────────────────────
Attacker ──▶ Port 9080 (exposed)
         ──▶ POST /signserver/process (NOAUTH)
         ──▶ Ký bất kỳ PDF nào bằng private key bệnh viện
         ──▶ Tạo báo cáo y tế giả mạo có chữ ký hợp lệ

Attack Scenario 2: Key Extraction
─────────────────────────────────
Attacker ──▶ Container access (docker exec / volume mount)
         ──▶ cp /tmp/signer.p12 (world-readable)
         ──▶ Cracked P12 password (có thể yếu)
         ──▶ Extract private key → ký offline vô hạn

Attack Scenario 3: Admin Takeover
─────────────────────────────────
Attacker ──▶ Port 9443 (exposed)
         ──▶ /signserver/adminweb/ (PublicAccessAuthenticationToken)
         ──▶ Thêm worker với key riêng → ký thay bệnh viện
         ──▶ Xóa worker existing → denial of service
```

---

## 3. Kiến trúc bảo mật Production

### 3.1. Kiến trúc mục tiêu

```
                    ┌─────── DMZ / Public ────────┐
                    │                              │
                    │  ┌──────────────────────┐    │
                    │  │   Reverse Proxy      │    │
                    │  │   (Nginx/Traefik)    │    │
                    │  │   TLS Termination    │    │
                    │  └──────────┬───────────┘    │
                    │             │                 │
                    └─────────────┼─────────────────┘
                                  │
                    ┌─────── Internal Network ─────┐
                    │             │                 │
                    │  ┌──────────▼───────────┐    │
                    │  │      IVF API         │    │
                    │  │  (client cert auth)  │    │
                    │  └──────────┬───────────┘    │
                    │             │ mTLS            │
                    │  ┌──────────▼───────────┐    │
                    │  │    SignServer CE     │    │
                    │  │  ClientCertAuth     │    │
                    │  │  ┌────────────────┐ │    │
                    │  │  │ P12 Keystore   │ │    │
                    │  │  │ (encrypted)    │ │    │
                    │  │  │ chmod 400      │ │    │
                    │  │  │ persistent vol │ │    │
                    │  │  └────────────────┘ │    │
                    │  └──────────┬───────────┘    │
                    │             │                 │
                    │  ┌──────────▼───────────┐    │
                    │  │     EJBCA CE         │    │
                    │  │  Certificate Auth    │    │
                    │  └─────────────────────┘    │
                    │                              │
                    └─── isolated (no internet) ───┘

                    ┌─────── Data Network ─────────┐
                    │                              │
                    │  ┌──────────────────────┐    │
                    │  │   PostgreSQL DBs     │    │
                    │  │  (encrypted at rest) │    │
                    │  └──────────────────────┘    │
                    │                              │
                    │  ┌──────────────────────┐    │
                    │  │      MinIO S3        │    │
                    │  │  (encrypted bucket)  │    │
                    │  └──────────────────────┘    │
                    │                              │
                    └─── isolated (no internet) ───┘
```

### 3.2. Nguyên tắc bảo mật

| Nguyên tắc                | Áp dụng                                                                             |
| ------------------------- | ----------------------------------------------------------------------------------- |
| **Defense in Depth**      | Nhiều lớp bảo vệ: network isolation + mTLS + keystore encryption + file permissions |
| **Least Privilege**       | Mỗi service chỉ có quyền tối thiểu cần thiết                                        |
| **Zero Trust**            | Mọi request phải authenticate, kể cả internal network                               |
| **Encryption at Rest**    | Key files, databases, object storage đều được mã hóa                                |
| **Encryption in Transit** | TLS 1.3 cho mọi giao tiếp giữa services                                             |
| **Audit Trail**           | Ghi log mọi signing operation với correlation ID                                    |
| **Secret Management**     | Không lưu password trong source code hoặc env vars                                  |

---

## 4. So sánh các phương án bảo vệ Private Key

### 4.1. Bảng so sánh

| Tiêu chí                | P12 Hardened  |     SoftHSM2     |      Cloud KMS      |     HSM vật lý      |
| ----------------------- | :-----------: | :--------------: | :-----------------: | :-----------------: |
| **Chi phí**             |      $0       |        $0        |   $50-3,500/tháng   |    $5,000-20,000    |
| **Độ phức tạp setup**   |    ⭐ Thấp    | ⭐⭐ Trung bình  |     ⭐⭐⭐ Cao      |  ⭐⭐⭐⭐ Rất cao   |
| **Chống extract key**   | ❌ File copy  |  ⚠️ Memory dump  | ✅ Key never leaves | ✅ Key never leaves |
| **FIPS 140-2**          |     Không     |     Level 1      |      Level 2-3      |       Level 3       |
| **Pháp lý VN (NĐ 130)** |      ❌       |        ❌        |   ⚠️ Tùy provider   |         ✅          |
| **Performance**         | ✅ Nhanh nhất |     ✅ Nhanh     | ⚠️ Network latency  |  ⚠️ Hardware speed  |
| **Backup/DR**           | ✅ Copy file  | ⚠️ Token export  |  ✅ Cloud managed   |     ⚠️ Phức tạp     |
| **Migration lên HSM**   |   3-5 ngày    |      1 ngày      |         N/A         |         N/A         |
| **Phù hợp cho**         | Internal apps | Staging/Prod nhỏ |   Production lớn    |   Enterprise/Gov    |

### 4.2. Khuyến nghị cho IVF

**Giai đoạn 1 (Ngay bây giờ):** P12 Hardened — đủ cho production nội bộ bệnh viện
**Giai đoạn 2 (Khi mở rộng):** SoftHSM2 — chuẩn PKCS#11, dễ migrate lên HSM thật
**Giai đoạn 3 (Khi cần compliance):** Cloud HSM hoặc HSM vật lý

### 4.3. Key Protection Chain

```
Private Key (RSA 2048/4096)
    └── [Encrypted by] PKCS#12 Password (AES-256-CBC)
        └── [Stored in] SignServer DB → worker config (masked)
            └── [Protected by] DB Password
                └── [Stored in] Docker Secret (file-based, 0400 permission)
                    └── [Protected by] Host OS file permissions + encrypted volume
```

---

## 5. Hướng dẫn triển khai từng bước

### Phase 1: Immediate Hardening (1-2 ngày)

#### Bước 1: Chuẩn bị thư mục secrets

```bash
# Trên host machine
mkdir -p ./secrets ./certs/signserver ./certs/api ./keys/signserver
chmod 700 ./secrets ./certs ./keys

# Generate strong passwords
openssl rand -base64 48 > ./secrets/signserver_db_password.txt
openssl rand -base64 48 > ./secrets/ejbca_db_password.txt
openssl rand -base64 48 > ./secrets/minio_root_password.txt
openssl rand -base64 48 > ./secrets/keystore_password.txt
openssl rand -base64 64 > ./secrets/jwt_secret.txt
openssl rand -base64 48 > ./secrets/ivf_db_password.txt

# Lock down permissions
chmod 400 ./secrets/*.txt
```

#### Bước 2: Di chuyển P12 files

```bash
# Backup existing keystores
docker cp ivf-signserver:/tmp/signer.p12 ./keys/signserver/
docker cp ivf-signserver:/tmp/pdfsigner_techinical.p12 ./keys/signserver/
docker cp ivf-signserver:/tmp/pdfsigner_head_department.p12 ./keys/signserver/
docker cp ivf-signserver:/tmp/pdfsigner_doctor1.p12 ./keys/signserver/
docker cp ivf-signserver:/tmp/pdfsigner_admin.p12 ./keys/signserver/

# Re-encrypt với password mạnh
STRONG_PASS=$(cat ./secrets/keystore_password.txt)
for f in ./keys/signserver/*.p12; do
    openssl pkcs12 -in "$f" -out "${f}.tmp" \
        -passin pass:foo123 -passout "pass:${STRONG_PASS}" -aes256
    mv "${f}.tmp" "$f"
done

# Set strict permissions
chmod 400 ./keys/signserver/*.p12
```

#### Bước 3: Update SignServer worker paths

```bash
# Update KEYSTOREPATH cho mỗi worker
docker exec ivf-signserver bash -c "
/opt/keyfactor/signserver/bin/signserver setproperty 1 KEYSTOREPATH /opt/keyfactor/persistent/keys/signer.p12
/opt/keyfactor/signserver/bin/signserver setproperty 272 KEYSTOREPATH /opt/keyfactor/persistent/keys/pdfsigner_techinical.p12
/opt/keyfactor/signserver/bin/signserver setproperty 444 KEYSTOREPATH /opt/keyfactor/persistent/keys/pdfsigner_head_department.p12
/opt/keyfactor/signserver/bin/signserver setproperty 597 KEYSTOREPATH /opt/keyfactor/persistent/keys/pdfsigner_doctor1.p12
/opt/keyfactor/signserver/bin/signserver setproperty 907 KEYSTOREPATH /opt/keyfactor/persistent/keys/pdfsigner_admin.p12
/opt/keyfactor/signserver/bin/signserver reload all
"
```

#### Bước 4: Tắt port 9080 khỏi host

Xóa `"9080:8080"` khỏi docker-compose — chỉ giữ HTTPS admin `9443:8443`. API gọi SignServer qua internal Docker network (không qua host port).

### Phase 2: mTLS & Authentication (3-5 ngày)

#### Bước 5: Tạo client certificate cho API

```bash
# Sử dụng EJBCA để cấp client cert cho IVF API
# 1. Tạo End Entity Profile "API Client" trên EJBCA Admin
# 2. Tạo Certificate Profile "TLS Client Auth"
# 3. Enroll certificate cho IVF API

# Hoặc dùng openssl self-signed cho internal network:
./scripts/generate-certs.sh
```

#### Bước 6: Bật ClientCertAuthorizer trên SignServer workers

```bash
docker exec ivf-signserver bash -c "
# Cho mỗi worker, bật client cert auth
for WORKER_ID in 1 272 444 597 907; do
    /opt/keyfactor/signserver/bin/signserver setproperty \$WORKER_ID AUTHTYPE org.signserver.server.ClientCertAuthorizer
done
/opt/keyfactor/signserver/bin/signserver reload all
"
```

#### Bước 7: Cấu hình mTLS trong IVF API

Cập nhật `appsettings.Production.json`:

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "SkipTlsValidation": false,
    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePassword": "${SIGNING_CLIENT_CERT_PASSWORD}"
  }
}
```

### Phase 3: Network Isolation & Monitoring (1 tuần)

#### Bước 8: Network segmentation

Tách thành 3 networks:

- `ivf-public`: API + frontend
- `ivf-signing`: API + SignServer + EJBCA (internal, no internet)
- `ivf-data`: Databases + MinIO (internal, no internet)

#### Bước 9: Audit logging

Bật audit log trên SignServer và ghi vào centralized logging.

---

## 6. Cấu hình chi tiết

### 6.1. File Structure sau Hardening

```
project/
├── docker-compose.yml              # Development
├── docker-compose.production.yml   # Production overrides
├── secrets/                        # 🔒 chmod 700, gitignored
│   ├── signserver_db_password.txt
│   ├── ejbca_db_password.txt
│   ├── minio_root_password.txt
│   ├── keystore_password.txt
│   ├── jwt_secret.txt
│   └── ivf_db_password.txt
├── keys/                           # 🔒 chmod 700, gitignored
│   └── signserver/
│       ├── signer.p12              # chmod 400
│       ├── pdfsigner_techinical.p12
│       ├── pdfsigner_head_department.p12
│       ├── pdfsigner_doctor1.p12
│       └── pdfsigner_admin.p12
├── certs/                          # 🔒 chmod 700, gitignored
│   ├── api/
│   │   ├── api-client.p12          # Client cert cho API → SignServer
│   │   └── api-client.pem
│   └── signserver/
│       ├── signserver-tls.p12      # TLS cert cho SignServer
│       └── ca-chain.pem
└── scripts/
    ├── generate-certs.sh           # Tạo certificates
    ├── signserver-init.sh          # Init SignServer workers
    └── rotate-keys.sh             # Key rotation
```

### 6.2. Docker Compose Production Override

File `docker-compose.production.yml` override các cấu hình insecure:

- **SignServer**: Không expose port 9080, dùng Docker Secrets, mount key read-only
- **EJBCA**: Certificate-based admin, không PublicAccessAuth
- **MinIO**: Strong password từ Docker Secret, TLS enabled
- **Database**: Password từ Docker Secret, encrypted volume
- **Networks**: 3 isolated networks (public, signing, data)

### 6.3. API Configuration

File `appsettings.Production.json`:

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "WorkerName": "PDFSigner",
    "TimeoutSeconds": 30,
    "SkipTlsValidation": false,
    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePassword": null,
    "TrustedCaCertPath": "/app/certs/ca-chain.pem",
    "RequireMtls": true
  }
}
```

**Lưu ý**: `ClientCertificatePassword` được đọc từ environment variable `SIGNING_CLIENT_CERT_PASSWORD`, không lưu trong appsettings.

---

## 7. Kiểm tra và xác minh

### 7.1. Verify key file security

```bash
# Check permissions
docker exec ivf-signserver bash -c "
ls -la /opt/keyfactor/persistent/keys/*.p12
stat -c '%a %U %G' /opt/keyfactor/persistent/keys/*.p12
"
# Expected: 400 (owner read-only)
```

### 7.2. Verify worker authentication

```bash
# Test signing without client cert (should FAIL)
curl -X POST http://signserver:8080/signserver/process \
  -F "workerName=PDFSigner" \
  -F "data=@test.pdf"
# Expected: 401 Unauthorized or Connection Refused

# Test signing with client cert (should SUCCEED)
curl -X POST https://signserver:8443/signserver/process \
  --cert api-client.pem --key api-client-key.pem \
  -F "workerName=PDFSigner" \
  -F "data=@test.pdf"
# Expected: 200 OK with signed PDF
```

### 7.3. Verify admin access

```bash
# Test admin without cert (should FAIL)
curl -k https://localhost:9443/signserver/adminweb/
# Expected: 403 Forbidden

# Test admin with admin cert (should SUCCEED)
curl -k --cert admin.pem --key admin-key.pem \
  https://localhost:9443/signserver/adminweb/
# Expected: 200 OK
```

### 7.4. Verify network isolation

```bash
# From external network, try to reach SignServer (should FAIL)
docker run --rm --network ivf-public alpine wget -qO- http://signserver:8080/
# Expected: DNS/connection failure

# From signing network (should SUCCEED)
docker run --rm --network ivf-signing alpine wget -qO- http://signserver:8080/
# Expected: Connection success (but auth required)
```

---

## 8. Vận hành và giám sát

### 8.1. Monitoring checklist

| Metric               | Cách kiểm tra                            | Threshold          |
| -------------------- | ---------------------------------------- | ------------------ |
| Worker status        | `signserver getstatus brief all`         | Phải Active        |
| Certificate expiry   | `signserver getconfig <id>` → cert dates | ≥ 30 ngày          |
| Signing count        | `getstatus brief all` → Signings         | Monitor trend      |
| Key file integrity   | SHA256 checksum                          | Không đổi bất ngờ  |
| Failed sign attempts | Application logs                         | Alert nếu > 5/phút |

### 8.2. Certificate rotation

```bash
# Khi cert sắp hết hạn (30 ngày trước):
# 1. Issue new cert từ EJBCA
# 2. Export new P12
# 3. Upload vào worker
# 4. Reload worker
# 5. Verify signing
# 6. Remove old cert

docker exec ivf-signserver bash -c "
/opt/keyfactor/signserver/bin/signserver uploadsignercertificatechain \
    1 /opt/keyfactor/persistent/keys/new_signer.p12 -host localhost
/opt/keyfactor/signserver/bin/signserver reload 1
/opt/keyfactor/signserver/bin/signserver getstatus brief 1
"
```

### 8.3. Key backup

```bash
# Encrypted backup of all keystores
tar czf - ./keys/signserver/ | \
  openssl aes-256-cbc -salt -pbkdf2 \
  -out backup/keys_$(date +%Y%m%d).tar.gz.enc

# Verify backup
openssl aes-256-cbc -d -pbkdf2 \
  -in backup/keys_$(date +%Y%m%d).tar.gz.enc | tar tzf -
```

---

## 9. Disaster Recovery

### 9.1. Scenario: Key file corrupted/lost

```bash
# 1. Restore from encrypted backup
openssl aes-256-cbc -d -pbkdf2 -in backup/keys_latest.tar.gz.enc | tar xzf -

# 2. Re-mount vào container
docker cp ./keys/signserver/signer.p12 ivf-signserver:/opt/keyfactor/persistent/keys/
docker exec ivf-signserver chmod 400 /opt/keyfactor/persistent/keys/signer.p12

# 3. Reload workers
docker exec ivf-signserver /opt/keyfactor/signserver/bin/signserver reload all
```

### 9.2. Scenario: Key compromised

```bash
# 1. NGAY LẬP TỨC: Deactivate tất cả workers
docker exec ivf-signserver bash -c "
for ID in 1 272 444 597 907; do
    /opt/keyfactor/signserver/bin/signserver deactivatesigntoken \$ID
done
"

# 2. Revoke certificates trên EJBCA
# → EJBCA Admin UI → RA Functions → Search End Entities → Revoke

# 3. Generate new key pairs
# 4. Issue new certificates
# 5. Upload new keys + certs to workers
# 6. Reactivate workers
# 7. Update CRL/OCSP
```

---

## 10. Checklist triển khai

### Phase 1: Immediate (Bắt buộc trước production) ✅ COMPLETED

- [x] Tạo thư mục `secrets/`, `keys/`, `certs/` với permission 700
- [x] Generate strong passwords (≥48 chars random) cho tất cả services
- [x] Di chuyển P12 files từ `/tmp/` sang persistent volume
- [x] Re-encrypt P12 với AES-256 + strong password
- [x] Set file permission 400 cho `.p12` files
- [x] Xóa port `9080:8080` khỏi docker-compose (chỉ giữ internal)
- [x] Dùng Docker Secrets thay environment variables cho passwords
- [x] Thêm `secrets/`, `keys/`, `certs/` vào `.gitignore`
- [x] Tạo `docker-compose.production.yml` override
- [x] Update `appsettings.Production.json`
- [x] Tạo backup encrypted cho key files

### Phase 2: Authentication (Mạnh khuyến nghị) ✅ COMPLETED

- [x] Tạo Internal Root CA + client cert cho IVF API (`scripts/generate-certs.sh`)
- [x] Cấu hình mTLS giữa API ↔ SignServer (HTTPS 8443 + client cert P12)
- [x] Bật `ClientCertAuthorizer` trên tất cả workers (1, 272, 444, 597, 907)
- [x] Authorized client: `2EB6EB968...;CN=IVF Internal Root CA,...`
- [x] WildFly `want-client-auth=true` + truststore with Internal CA
- [x] API `appsettings.json` → `SignServerUrl: https://localhost:9443/signserver`
- [x] API loads P12 client cert via `X509CertificateLoader.LoadPkcs12FromFile()`
- [x] Provisioning code sets `ClientCertAuthorizer` + `addauthorizedclient` for new workers
- [x] SignServer init script (`scripts/init-mtls.sh`) for container restarts
- [x] Test signing workflow end-to-end: `test-sign` → 224ms, `containsSignature: true`
- [x] Unauthenticated HTTP requests return HTTP 400 ("client authentication is required")

### Phase 3: Hardening (Nên làm) ✅ COMPLETED

- [x] Tách networks: `ivf-public`, `ivf-signing` (internal), `ivf-data` (internal)
- [x] Enable container read-only filesystem (`read_only: true` + tmpfs cho SignServer)
- [x] Set `no-new-privileges` security option (tất cả services)
- [x] Bật audit logging với correlation ID + duration tracking
- [x] Setup monitoring cho certificate expiry (`CertificateExpiryMonitorService`)
- [x] Rate limiting: `signing` (30/min), `signing-provision` (3/min)
- [x] Production mTLS: `need-client-auth=true` + health port 8081
- [x] Xóa port 9080 hoàn toàn (chỉ HTTPS 8443)
- [x] Security-status endpoint: cert expiry, container security, rate limit info

### Phase 4: Compliance ✅

- [x] Đánh giá SoftHSM2 — PKCS#11 FIPS 140-2 Level 1 provider
  - Custom Docker image: `docker/signserver-softhsm/Dockerfile`
  - Init script: `scripts/init-softhsm.sh`
  - Migration script: `scripts/migrate-p12-to-pkcs11.sh`
  - `CryptoTokenType` enum: `P12` (default) hoặc `PKCS11`
  - PKCS#11 keys: `CKA_EXTRACTABLE=FALSE`, `CKA_SENSITIVE=TRUE`
- [x] FIPS 140-2 readiness — SoftHSM2 cung cấp FIPS 140-2 Level 1
  - Production compose: `DigitalSigning__CryptoTokenType=PKCS11`
  - SoftHSM2 PIN qua Docker Secret (`softhsm_pin`, `softhsm_so_pin`)
- [x] Security compliance audit service (`SecurityComplianceService`)
  - 21 checks across 4 phases (KEY, MTLS, TLS, AUTH, NET, CTR, RL, AUD, CERT, HSM, FIPS, HDR, ENV, PEN)
  - Scoring: Pass=100%, Warning=50%, Fail=0% → Grade A–F
  - Endpoint: `GET /api/admin/signing/compliance-audit`
- [x] Security headers hardening (HSTS, Permissions-Policy, COEP, COOP, CORP)
- [x] Certificate rotation automation (`scripts/rotate-certs.sh`)
  - Supports: `api-client`, `admin`, `worker` certificate types
  - Grace period, dry-run, force rotation, backup
- [x] Container vulnerability scanning (Trivy Docker service, profile: `security-scan`)
- [x] Penetration testing — automated OWASP Top 10 script + inline API endpoint
  - Script: `scripts/pentest.sh --target all` (OWASP A01-A10, SignServer, EJBCA, headers)
  - Endpoint: `POST /api/admin/signing/pentest` (inline API security checks)
  - Report generation: Markdown + JSON output in `./pentest-results/`
- [x] Security audit evidence — third-party audit support (`SecurityAuditService`)
  - Endpoint: `GET /api/admin/signing/security-audit-evidence`
  - Package: system info, certificate inventory, 17 security controls, access matrix,
    network topology, data protection, incident response, pentest coverage
  - All secrets redacted — safe for external auditor review

---

## Phase 4: Chi tiết triển khai

### 4.1. SoftHSM2 / PKCS#11 Integration

SoftHSM2 cung cấp PKCS#11 interface cho key storage, đáp ứng FIPS 140-2 Level 1.

**Docker Setup:**

```bash
# Activate SoftHSM2 profile
docker compose --profile softhsm up -d signserver-softhsm

# Initialize PKCS#11 tokens
docker exec ivf-signserver /opt/keyfactor/persistent/init-softhsm.sh

# Migrate existing P12 workers to PKCS#11
docker exec ivf-signserver /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh --dry-run
docker exec ivf-signserver /opt/keyfactor/persistent/migrate-p12-to-pkcs11.sh
```

**CryptoTokenType Configuration:**

```json
{
  "DigitalSigning": {
    "CryptoTokenType": "PKCS11",
    "Pkcs11SharedLibraryName": "SOFTHSM",
    "Pkcs11SlotLabel": "SignServerToken",
    "Pkcs11PinFile": "/run/secrets/softhsm_pin"
  }
}
```

**Key Properties (PKCS#11 mode):**

- `SHAREDLIBRARYNAME`: Registered PKCS#11 library name in SignServer
- `SLOT`: Token slot label
- `CKA_EXTRACTABLE=FALSE`: Keys cannot be exported
- `CKA_SENSITIVE=TRUE`: Keys cannot be viewed in plaintext

### 4.2. Security Compliance Audit

Endpoint: `GET /api/admin/signing/compliance-audit`

**Response format:**

```json
{
  "auditDate": "2025-01-01T00:00:00Z",
  "summary": {
    "totalChecks": 21,
    "passed": 17,
    "warnings": 3,
    "failed": 1,
    "score": 85.5,
    "grade": "B"
  },
  "checks": [...],
  "recommendations": [...]
}
```

**Check Categories:**
| Phase | Check IDs | Category |
|-------|-----------|----------|
| 1 | KEY-001, KEY-002, NET-001, KEY-003 | Key Storage & Network |
| 2 | MTLS-001, TLS-001, AUTH-001, TLS-002 | mTLS & TLS |
| 3 | NET-002, CTR-001, RL-001, AUD-001, CERT-001 | Container & Monitoring |
| 4 | HSM-001, FIPS-001, CERT-002, HDR-001, ENV-001, PEN-001, AUD-002 | Compliance, Pentest & Audit |

### 4.3. Certificate Rotation

```bash
# Check all certificate expiry status
./scripts/rotate-certs.sh --check

# Rotate API client certificate (dry run)
./scripts/rotate-certs.sh --type api-client --dry-run

# Force rotate a worker certificate
./scripts/rotate-certs.sh --type worker --worker-id 444 --force

# Rotate admin certificate with 60-day grace period
./scripts/rotate-certs.sh --type admin --grace-days 60
```

### 4.4. Container Security Scanning

```bash
# Run Trivy vulnerability scan
docker compose --profile security-scan up trivy-scan
```

### 4.5. Security Headers (Phase 4 Enhancements)

| Header                         | Value                                          | Purpose                            |
| ------------------------------ | ---------------------------------------------- | ---------------------------------- |
| `Strict-Transport-Security`    | `max-age=63072000; includeSubDomains; preload` | Force HTTPS                        |
| `Permissions-Policy`           | `camera=(), microphone=(), geolocation=()...`  | Restrict browser APIs              |
| `Cross-Origin-Embedder-Policy` | `require-corp`                                 | Prevent cross-origin embedding     |
| `Cross-Origin-Opener-Policy`   | `same-origin`                                  | Isolate browsing context           |
| `Cross-Origin-Resource-Policy` | `same-origin`                                  | Block cross-origin resource access |
| `X-XSS-Protection`             | `0`                                            | Disabled (CSP replaces)            |

### 4.6. Penetration Testing

**Automated Script (`scripts/pentest.sh`):**

```bash
# Full penetration test (API + SignServer + EJBCA + headers)
./scripts/pentest.sh --target all

# API-only test
./scripts/pentest.sh --target api

# SignServer-only test
./scripts/pentest.sh --target signserver

# Custom output directory
./scripts/pentest.sh --target all --output /tmp/pentest-results
```

**Test Coverage (OWASP Top 10 2021):**

| Category                       | Tests                                           | Automated |
| ------------------------------ | ----------------------------------------------- | --------- |
| A01: Broken Access Control     | Auth bypass, IDOR, privilege escalation         | ✅        |
| A02: Cryptographic Failures    | HSTS, TLS 1.1 rejection, cert validation        | ✅        |
| A03: Injection                 | SQL injection, XSS, command injection           | ✅        |
| A04: Insecure Design           | Rate limiting verification                      | ✅        |
| A05: Security Misconfiguration | Headers, CORS, swagger, server identity         | ✅        |
| A06: Vulnerable Components     | Trivy container scanning                        | ✅        |
| A07: Auth Failures             | JWT invalid token, JWT `none` algorithm         | ✅        |
| A08: Data Integrity            | CSP header validation                           | ✅        |
| A09: Logging & Monitoring      | Compliance audit endpoint                       | ✅        |
| A10: SSRF                      | Metadata service access prevention              | ✅        |
| SS-\*: SignServer              | mTLS, port 9080, admin access, health, TLS cert | ✅        |
| EJBCA-\*: EJBCA                | Admin access, REST API, enrollment, health      | ✅        |
| HDR-\*: Headers                | 9 OWASP headers + server identification         | ✅        |

**Inline API Endpoint:**

```
POST /api/admin/signing/pentest
```

Runs non-destructive API-level security checks. Returns pass/fail/warn results with scoring.

**Output:**

- Markdown report: `pentest-results/pentest_report_YYYYMMDD_HHMMSS.md`
- JSON results: `pentest-results/pentest_results_YYYYMMDD_HHMMSS.json`

### 4.7. Third-Party Security Audit Support

**Evidence Package Endpoint:**

```
GET /api/admin/signing/security-audit-evidence
```

Generates a comprehensive audit evidence package containing:

| Section               | Contents                                                                                       |
| --------------------- | ---------------------------------------------------------------------------------------------- |
| System Info           | Application name, runtime, OS, .NET version, container status                                  |
| Security Config       | Signing, mTLS, TLS, PKCS#11, audit logging settings (sanitized)                                |
| Compliance Audit      | Full 21-check compliance assessment with A-F grading                                           |
| Certificate Inventory | API client cert, CA cert details, worker cert info                                             |
| Security Controls     | 17 controls across Phases 1-4 with implementation status                                       |
| Access Control Matrix | Endpoint permissions + service-to-service auth methods                                         |
| Network Topology      | 3 network zones, exposed ports, removed ports                                                  |
| Data Protection       | Encryption at rest/in transit, data classification                                             |
| Incident Response     | 4 response procedures (key compromise, cert expiry, unauthorized access, container compromise) |
| Pentest Capabilities  | Available tools, test coverage, manual testing requirements                                    |
| Audit Trail Config    | Logged events, correlation tracking, log destinations                                          |
| Recommendations       | Actionable items based on current configuration                                                |

**All secrets are redacted** — the package is safe to share with external auditors.

**Example usage for third-party audit:**

```bash
# Generate evidence package
curl -H "Authorization: Bearer <admin-jwt>" \
  http://localhost:5000/api/admin/signing/security-audit-evidence \
  | jq . > audit_evidence_$(date +%Y%m%d).json

# Run pentest and attach results
./scripts/pentest.sh --target all --output ./audit-attachments

# Run Trivy scan
docker compose --profile security-scan up trivy-scan 2>&1 > ./audit-attachments/trivy_scan.txt
```

---

## Phụ lục

### A. Tham khảo

- [SignServer CE Documentation](https://doc.primekey.com/signserver)
- [EJBCA CE Documentation](https://doc.primekey.com/ejbca)
- [Docker Secrets](https://docs.docker.com/compose/use-secrets/)
- [PKCS#11 / SoftHSM2](https://www.opendnssec.org/softhsm/)
- [Nghị định 130/2018/NĐ-CP](https://thuvienphapluat.vn/) — Quy định về chữ ký số

### B. Configs liên quan

| File                                                | Mô tả                                     |
| --------------------------------------------------- | ----------------------------------------- |
| `docker-compose.production.yml`                     | Production override                       |
| `docker/signserver-softhsm/Dockerfile`              | SignServer + SoftHSM2 image (Phase 4)     |
| `src/IVF.API/appsettings.Production.json`           | API production config                     |
| `src/IVF.API/Services/SecurityComplianceService.cs` | Compliance audit service (Phase 4)        |
| `src/IVF.API/Services/DigitalSigningOptions.cs`     | Signing + PKCS#11 config                  |
| `scripts/generate-certs.sh`                         | Certificate generation                    |
| `scripts/signserver-init.sh`                        | Worker initialization                     |
| `scripts/init-softhsm.sh`                           | SoftHSM2 PKCS#11 token init (Phase 4)     |
| `scripts/migrate-p12-to-pkcs11.sh`                  | P12 → PKCS#11 migration (Phase 4)         |
| `scripts/rotate-certs.sh`                           | Certificate rotation automation (Phase 4) |
| `scripts/pentest.sh`                                | OWASP penetration testing (Phase 4)       |
| `src/IVF.API/Services/SecurityAuditService.cs`      | Third-party audit evidence (Phase 4)      |
