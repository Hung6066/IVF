# Cloud KMS Deployment Guide — IVF Digital Signing

> **Version:** 1.0
> **Ngày:** 2026-02-22
> **Dự án:** IVF Digital Signing Infrastructure
> **Stack:** SignServer CE 7.3.2 · .NET 10 · PKCS#11 · Cloud KMS
> **Tài liệu liên quan:** `signserver_implementation_guide.md` (Phase 1–4), `signserver_production_security.md`

---

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [So sánh giải pháp KMS](#2-so-sánh-giải-pháp-kms)
3. [Kiến trúc tích hợp](#3-kiến-trúc-tích-hợp)
4. [AWS CloudHSM](#4-aws-cloudhsm)
5. [AWS KMS](#5-aws-kms)
6. [Azure Key Vault / Managed HSM](#6-azure-key-vault--managed-hsm)
7. [Google Cloud KMS](#7-google-cloud-kms)
8. [HashiCorp Vault Transit](#8-hashicorp-vault-transit)
9. [Mở rộng CryptoTokenType](#9-mở-rộng-cryptotokentype)
10. [Cập nhật cấu hình](#10-cập-nhật-cấu-hình)
11. [Docker & Network](#11-docker--network)
12. [Migration SoftHSM2 → Cloud KMS](#12-migration-softhsm2--cloud-kms)
13. [Compliance & Audit](#13-compliance--audit)
14. [Monitoring & Alerting](#14-monitoring--alerting)
15. [Disaster Recovery](#15-disaster-recovery)
16. [Chi phí ước tính](#16-chi-phí-ước-tính)
17. [Checklist triển khai](#17-checklist-triển-khai)

---

## 1. Tổng quan

### 1.1. Bối cảnh

Hệ thống IVF hiện tại sử dụng **SoftHSM2** (PKCS#11, FIPS 140-2 Level 1) để bảo vệ signing keys. Đây là giải pháp phù hợp cho on-premise deployment. Khi chuyển lên cloud hoặc cần compliance cao hơn (FIPS 140-2 Level 2/3), cần tích hợp Cloud KMS.

### 1.2. Mục tiêu

| Mục tiêu                 | Mô tả                                                                 |
| ------------------------ | --------------------------------------------------------------------- |
| **FIPS 140-2 Level 2/3** | Key material không bao giờ rời HSM vật lý                             |
| **Key non-extractable**  | `CKA_EXTRACTABLE=FALSE`, `CKA_SENSITIVE=TRUE` — enforced bởi hardware |
| **High availability**    | Multi-AZ / multi-region key replication                               |
| **Audit trail**          | Cloud-native audit logs (CloudTrail, Azure Monitor, Cloud Audit Logs) |
| **Zero trust**           | IAM-based access, no static credentials                               |
| **Backward compatible**  | Không thay đổi signing API, chỉ thay crypto backend                   |

### 1.3. Điểm tích hợp hiện tại

SignServer CE sử dụng **PKCS#11 interface** để giao tiếp với crypto token. Cloud KMS tích hợp qua:

```
IVF API ──(mTLS)──> SignServer CE ──(PKCS#11)──> Cloud KMS PKCS#11 Library ──(API)──> Cloud HSM
```

**Hai phương thức tích hợp:**

| Phương thức        | Mô tả                                                           | Providers hỗ trợ                             |
| ------------------ | --------------------------------------------------------------- | -------------------------------------------- |
| **PKCS#11 Native** | Cloud provider cung cấp `.so` library, SignServer gọi trực tiếp | AWS CloudHSM, Azure Managed HSM, Thales Luna |
| **PKCS#11 Bridge** | Wrapper converts PKCS#11 → REST API                             | Google Cloud KMS, HashiCorp Vault            |

---

## 2. So sánh giải pháp KMS

| Tiêu chí           | SoftHSM2 (hiện tại) | AWS CloudHSM | AWS KMS     | Azure Managed HSM | Azure Key Vault | Google Cloud KMS | HashiCorp Vault            |
| ------------------ | ------------------- | ------------ | ----------- | ----------------- | --------------- | ---------------- | -------------------------- |
| **FIPS Level**     | Level 1             | Level 3      | Level 2     | Level 3           | Level 2         | Level 1–3\*      | Level 1 (software)         |
| **PKCS#11**        | ✅ Native           | ✅ Native    | ❌ API only | ✅ Native         | ❌ REST only    | ⚠️ Bridge        | ⚠️ Bridge                  |
| **Chi phí/tháng**  | $0                  | ~$1,500      | ~$1–50      | ~$3,500           | ~$5–50          | ~$1–100          | $0 (OSS) / $$ (Enterprise) |
| **Multi-AZ**       | ❌                  | ✅           | ✅          | ✅                | ✅              | ✅               | ✅ (config)                |
| **Key extraction** | Software            | Impossible   | Impossible  | Impossible        | Configurable    | Impossible       | Configurable               |
| **Audit**          | App-level           | CloudTrail   | CloudTrail  | Azure Monitor     | Azure Monitor   | Cloud Audit      | Vault Audit                |
| **Latency**        | <1ms                | 2–5ms        | 5–10ms      | 2–5ms             | 10–20ms         | 5–15ms           | 2–10ms                     |
| **Setup**          | Đơn giản            | Phức tạp     | Đơn giản    | Phức tạp          | Đơn giản        | Trung bình       | Trung bình                 |

> \* Google Cloud HSM sử dụng Cavium hardware (FIPS Level 3), standard Cloud KMS là Level 1.

### Khuyến nghị theo scenario

| Scenario                      | Khuyến nghị                         | Lý do                                  |
| ----------------------------- | ----------------------------------- | -------------------------------------- |
| **Startup / MVP**             | SoftHSM2 (giữ hiện tại)             | Chi phí $0, đủ cho development         |
| **Production on AWS**         | AWS CloudHSM                        | FIPS Level 3, PKCS#11 native           |
| **Production on Azure**       | Azure Managed HSM                   | FIPS Level 3, PKCS#11 native           |
| **Production on GCP**         | Google Cloud HSM                    | FIPS Level 3, giá cạnh tranh           |
| **Multi-cloud**               | HashiCorp Vault + Cloud KMS backend | Abstraction layer                      |
| **Cost-sensitive production** | AWS KMS / Azure Key Vault           | Low cost, API-based signing            |
| **Maximum compliance**        | AWS CloudHSM / Azure Managed HSM    | FIPS 140-2 Level 3, dedicated hardware |

---

## 3. Kiến trúc tích hợp

### 3.1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         IVF Application                             │
│                                                                     │
│  ┌──────────┐    ┌───────────┐    ┌──────────────────────────────┐  │
│  │ Angular  │───>│  IVF API  │───>│  SignServer CE 7.3.2         │  │
│  │ Client   │    │ (.NET 10) │    │                              │  │
│  │          │    │           │    │  ┌─────────────────────────┐  │  │
│  │          │    │ mTLS ─────│───>│  │ PKCS11CryptoToken       │  │  │
│  │          │    │           │    │  │                         │  │  │
│  └──────────┘    └───────────┘    │  │  SharedLibraryName:     │  │  │
│                                   │  │  ┌─────────────────┐   │  │  │
│                                   │  │  │ SOFTHSM (local) │   │  │  │
│                                   │  │  │ AWSCLOUDHSM     │   │  │  │
│                                   │  │  │ AZUREHSM        │   │  │  │
│                                   │  │  │ GCPKMS          │   │  │  │
│                                   │  │  └────────┬────────┘   │  │  │
│                                   │  └───────────│────────────┘  │  │
│                                   └──────────────│───────────────┘  │
│                                                  │                  │
└──────────────────────────────────────────────────│──────────────────┘
                                                   │
                              ┌────────────────────┴────────────────────┐
                              │            Cloud KMS Provider           │
                              │                                         │
                              │  ┌──────────┐  ┌──────────┐  ┌──────┐  │
                              │  │ AWS      │  │ Azure    │  │ GCP  │  │
                              │  │ CloudHSM │  │ Managed  │  │ Cloud│  │
                              │  │          │  │ HSM      │  │ HSM  │  │
                              │  │ FIPS L3  │  │ FIPS L3  │  │FIPS  │  │
                              │  │          │  │          │  │L1-L3 │  │
                              │  └──────────┘  └──────────┘  └──────┘  │
                              │                                         │
                              └─────────────────────────────────────────┘
```

### 3.2. Flow ký số

```
1. User → Angular → POST /api/user-signatures/sign-document
2. IVF API → validates JWT + permissions
3. IVF API → POST /signserver/process (mTLS)
4. SignServer → PKCS11CryptoToken → Cloud KMS PKCS#11 library
5. Cloud KMS PKCS#11 → Cloud API → HSM hardware signs digest
6. Signed hash → SignServer → embed in PDF → IVF API → User
```

> **Quan trọng:** Private key KHÔNG BAO GIỜ rời Cloud HSM. Chỉ digest (hash) được gửi lên, nhận về signature.

---

## 4. AWS CloudHSM

### 4.1. Prerequisites

| Component            | Yêu cầu                                                             |
| -------------------- | ------------------------------------------------------------------- |
| **VPC**              | SignServer container phải trong VPC có kết nối đến CloudHSM cluster |
| **CloudHSM Cluster** | ≥ 2 HSMs (multi-AZ)                                                 |
| **CloudHSM Client**  | `cloudhsm-client` + PKCS#11 library                                 |
| **IAM**              | EC2 instance role hoặc ECS task role với `cloudhsm:*` permissions   |
| **Security Group**   | Outbound TCP 2223-2225 (ENI của CloudHSM)                           |
| **CU (Crypto User)** | Tạo trong HSM cluster                                               |

### 4.2. Setup CloudHSM Cluster

```bash
# 1. Tạo cluster
aws cloudhsmv2 create-cluster \
    --hsm-type hsm1.medium \
    --subnet-ids subnet-xxx subnet-yyy \
    --region ap-southeast-1

# 2. Tạo HSM instance (≥ 2 cho HA)
aws cloudhsmv2 create-hsm --cluster-id cluster-xxx --availability-zone ap-southeast-1a
aws cloudhsmv2 create-hsm --cluster-id cluster-xxx --availability-zone ap-southeast-1b

# 3. Initialize cluster (download CSR → sign → upload cert)
aws cloudhsmv2 describe-clusters --filters clusterIds=cluster-xxx \
    --query 'Clusters[0].Certificates.ClusterCsr' --output text > cluster.csr

# 4. Sign CSR with your CA
openssl x509 -req -days 3652 -in cluster.csr \
    -CA customerCA.crt -CAkey customerCA.key -CAcreateserial \
    -out cluster.crt

# 5. Upload signed cert
aws cloudhsmv2 initialize-cluster \
    --cluster-id cluster-xxx \
    --signed-cert file://cluster.crt \
    --trust-anchor file://customerCA.crt
```

### 4.3. Install PKCS#11 Library

**Dockerfile — CloudHSM variant:**

```dockerfile
FROM keyfactor/signserver-ce:latest

USER root

# AWS CloudHSM Client SDK (PKCS#11 only — minimal install)
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget ca-certificates jq libssl-dev \
    && rm -rf /var/lib/apt/lists/*

# Download CloudHSM PKCS#11 provider
# https://docs.aws.amazon.com/cloudhsm/latest/userguide/pkcs11-library-install.html
RUN wget -qO /tmp/cloudhsm-pkcs11.deb \
    "https://s3.amazonaws.com/cloudhsmv2-software/CloudHsmClient/Focal/cloudhsm-pkcs11_latest_u20.04_amd64.deb" \
    && dpkg -i /tmp/cloudhsm-pkcs11.deb \
    && rm /tmp/cloudhsm-pkcs11.deb

# CloudHSM config directory
RUN mkdir -p /opt/cloudhsm/etc \
    && chown -R 10001:root /opt/cloudhsm

# PKCS#11 library path for SignServer
ENV PKCS11_LIBRARY_PATH=/opt/cloudhsm/lib/libcloudhsm_pkcs11.so

USER 10001
```

### 4.4. Cấu hình CloudHSM Client

**File:** `config/cloudhsm/customerCA.crt` — CA cert đã sign cho cluster

**File:** `config/cloudhsm/cloudhsm.cfg`

```json
{
  "clusters": [
    {
      "cluster_id": "cluster-xxx",
      "region": "ap-southeast-1",
      "servers": [
        { "hostname": "10.0.1.100", "port": 2223, "enable": true },
        { "hostname": "10.0.2.100", "port": 2223, "enable": true }
      ]
    }
  ],
  "server_client_cert_file": "",
  "logging": {
    "log_type": "file",
    "log_file": "/opt/cloudhsm/log/cloudhsm-pkcs11.log",
    "log_level": "warn"
  }
}
```

### 4.5. Tạo Crypto User & Keys

```bash
# Connect to CloudHSM management utility
docker exec -it ivf-signserver /opt/cloudhsm/bin/cloudhsm-cli interactive

# Login as admin
aws-cloudhsm > login --username admin --role admin

# Create Crypto User (CU) cho SignServer
aws-cloudhsm > user create --username signserver_cu --role crypto-user --password <STRONG_PASSWORD>

# Generate signing key pair (RSA-2048)
aws-cloudhsm > key generate-asymmetric-pair \
    --key-type rsa \
    --public-key-attributes modulus-size-bits=2048 \
    --private-key-attributes sign=true extractable=false sensitive=true \
    --label ivf-signing-key

# List keys
aws-cloudhsm > key list
```

### 4.6. Đăng ký trong SignServer

```bash
# Đăng ký PKCS#11 library
docker exec ivf-signserver bin/signserver setproperty global \
    GLOB.WORKER_PKCS11_LIBRARY.AWSCLOUDHSM \
    /opt/cloudhsm/lib/libcloudhsm_pkcs11.so

# Worker config sử dụng CloudHSM
docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    SIGNERTOKEN_CLASSPATH org.signserver.server.cryptotokens.PKCS11CryptoToken

docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    SHAREDLIBRARYNAME AWSCLOUDHSM

docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    SLOT "signserver_cu"

docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    PIN "<CU_PASSWORD>"

docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    DEFAULTKEY "ivf-signing-key"

docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    CKA_EXTRACTABLE FALSE

docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    CKA_SENSITIVE TRUE

# Reload + activate
docker exec ivf-signserver bin/signserver reload <WORKER_ID>
docker exec ivf-signserver bin/signserver activatecryptotoken <WORKER_ID> <CU_PASSWORD>
```

### 4.7. Docker Compose — AWS CloudHSM

```yaml
# docker-compose.aws-cloudhsm.yml
services:
  signserver:
    build:
      context: ./docker/signserver-cloudhsm
      dockerfile: Dockerfile
    container_name: ivf-signserver
    hostname: signserver.ivf.local
    environment:
      - DATABASE_JDBC_URL=jdbc:postgresql://signserver-db:5432/signserver
      - DATABASE_USER=signserver
      - DATABASE_PASSWORD_FILE=/run/secrets/signserver_db_password
      - TLS_SETUP_ENABLED=simple
      - INITIAL_ADMIN=CertificateAuthenticationToken
      - SIGNSERVER_NODEID=node1
      # CloudHSM config
      - CLOUDHSM_CLUSTER_ID=cluster-xxx
      - CLOUDHSM_REGION=ap-southeast-1
      - CLOUDHSM_CU_USER=signserver_cu
      - CLOUDHSM_CU_PASSWORD_FILE=/run/secrets/cloudhsm_cu_password
    ports:
      - "9443:8443"
    volumes:
      - signserver_persistent:/opt/keyfactor/persistent
      - ./config/cloudhsm/cloudhsm.cfg:/opt/cloudhsm/etc/cloudhsm.cfg:ro
      - ./config/cloudhsm/customerCA.crt:/opt/cloudhsm/etc/customerCA.crt:ro
    networks:
      - ivf-signing
      - ivf-data
    security_opt: ["no-new-privileges:true"]
    read_only: true
    tmpfs: ["/tmp", "standalone/tmp", "standalone/data", "standalone/log"]
    secrets:
      - signserver_db_password
      - cloudhsm_cu_password
    deploy:
      resources:
        limits: { cpus: "2.0", memory: "2G" }
        reservations: { cpus: "0.5", memory: "512M" }

secrets:
  cloudhsm_cu_password:
    file: ./secrets/cloudhsm_cu_password.txt
  signserver_db_password:
    file: ./secrets/signserver_db_password.txt
```

### 4.8. IAM Policy (ECS / EKS)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "CloudHSMAccess",
      "Effect": "Allow",
      "Action": [
        "cloudhsm:DescribeClusters",
        "cloudhsm:DescribeBackups",
        "cloudhsm:ListTags"
      ],
      "Resource": "arn:aws:cloudhsm:ap-southeast-1:ACCOUNT:cluster/cluster-xxx"
    },
    {
      "Sid": "CloudWatchLogs",
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ],
      "Resource": "arn:aws:logs:ap-southeast-1:ACCOUNT:log-group:/ivf/signserver:*"
    }
  ]
}
```

> **Lưu ý:** CloudHSM PKCS#11 authentication dùng CU username/password, KHÔNG dùng IAM credentials. IAM chỉ cần cho management API.

---

## 5. AWS KMS

### 5.1. Khi nào dùng AWS KMS thay CloudHSM?

| Tiêu chí       | AWS KMS                           | AWS CloudHSM               |
| -------------- | --------------------------------- | -------------------------- |
| **Cost**       | $1/key/tháng + $0.03/10K requests | ~$1,500/HSM/tháng          |
| **FIPS Level** | Level 2                           | Level 3                    |
| **PKCS#11**    | ❌ Không hỗ trợ                   | ✅ Native                  |
| **Tích hợp**   | REST API / SDK                    | PKCS#11 library            |
| **Use case**   | API signing, envelope encryption  | PDF signing với SignServer |

### 5.2. Tích hợp qua AWS KMS PKCS#11 Bridge

Vì AWS KMS không cung cấp PKCS#11 library, cần dùng bridge:

**Option A:** [aws-kms-pkcs11](https://github.com/JackOfMostTrades/aws-kms-pkcs11) (open-source)

```dockerfile
FROM keyfactor/signserver-ce:latest

USER root

# Build KMS PKCS#11 bridge
RUN apt-get update && apt-get install -y --no-install-recommends \
    git cmake build-essential libssl-dev libcurl4-openssl-dev libjson-c-dev \
    && rm -rf /var/lib/apt/lists/*

RUN git clone https://github.com/JackOfMostTrades/aws-kms-pkcs11.git /tmp/kms-pkcs11 \
    && cd /tmp/kms-pkcs11 && mkdir build && cd build \
    && cmake .. && make && make install \
    && rm -rf /tmp/kms-pkcs11

# Config
COPY kms-pkcs11.conf /etc/aws-kms-pkcs11/config.json

ENV PKCS11_LIBRARY_PATH=/usr/local/lib/libaws_kms_pkcs11.so

USER 10001
```

**Config file (`kms-pkcs11.conf`):**

```json
{
  "slots": [
    {
      "label": "IVF-Signing",
      "kms_key_id": "arn:aws:kms:ap-southeast-1:ACCOUNT:key/KEY-ID",
      "aws_region": "ap-southeast-1",
      "certificate_path": "/opt/keyfactor/persistent/keys/signing-cert.pem"
    }
  ]
}
```

**Option B:** Sử dụng application-level signing (bypass SignServer PKCS#11)

Với approach này, IVF API gọi trực tiếp AWS KMS SDK thay vì qua SignServer:

```csharp
// Thêm vào DigitalSigningOptions
public string? AwsKmsKeyId { get; set; }
public string? AwsKmsRegion { get; set; } = "ap-southeast-1";

// Signing service
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

public async Task<byte[]> SignWithKmsAsync(byte[] digest)
{
    var client = new AmazonKeyManagementServiceClient(
        Amazon.RegionEndpoint.GetBySystemName(_options.AwsKmsRegion));

    var request = new SignRequest
    {
        KeyId = _options.AwsKmsKeyId,
        Message = new MemoryStream(digest),
        MessageType = MessageType.DIGEST,
        SigningAlgorithm = SigningAlgorithmSpec.RSASSA_PKCS1_V1_5_SHA_256
    };

    var response = await client.SignAsync(request);
    return response.Signature.ToArray();
}
```

> ⚠️ **Không khuyến nghị Option B** cho production signing vì bypass SignServer audit + authorization layer.

---

## 6. Azure Key Vault / Managed HSM

### 6.1. So sánh Azure offerings

| Feature          | Azure Key Vault        | Azure Managed HSM                |
| ---------------- | ---------------------- | -------------------------------- |
| **FIPS Level**   | Level 2                | Level 3 (Marvell LiquidSecurity) |
| **PKCS#11**      | ❌ REST only           | ✅ Native                        |
| **Price**        | $5/key + $0.03/10K ops | ~$3,500/tháng                    |
| **Multi-region** | ✅                     | ✅                               |
| **RBAC**         | Azure AD               | Azure AD + local roles           |

### 6.2. Azure Managed HSM Setup

```bash
# 1. Tạo Managed HSM
az keyvault create --hsm-name ivf-signing-hsm \
    --resource-group ivf-production \
    --location southeastasia \
    --administrators "$(az ad signed-in-user show --query id -o tsv)" \
    --retention-days 90

# 2. Activate HSM (cần ≥ 3 RSA keys cho security domain)
openssl req -newkey rsa:2048 -nodes -keyout sd_key1.pem -x509 -days 365 -out sd_cert1.pem
openssl req -newkey rsa:2048 -nodes -keyout sd_key2.pem -x509 -days 365 -out sd_cert2.pem
openssl req -newkey rsa:2048 -nodes -keyout sd_key3.pem -x509 -days 365 -out sd_cert3.pem

az keyvault security-domain download \
    --hsm-name ivf-signing-hsm \
    --sd-wrapping-keys sd_cert1.pem sd_cert2.pem sd_cert3.pem \
    --sd-quorum 2 \
    --security-domain-file ivf_hsm_sd.json

# 3. Tạo signing key
az keyvault key create --hsm-name ivf-signing-hsm \
    --name ivf-pdf-signer \
    --kty RSA-HSM --size 2048 \
    --ops sign verify \
    --exportable false
```

### 6.3. PKCS#11 library cho Azure Managed HSM

Azure Managed HSM hỗ trợ PKCS#11 qua **Azure Key Vault PKCS#11 Provider**:

```dockerfile
FROM keyfactor/signserver-ce:latest

USER root

# Azure Managed HSM PKCS#11 provider
# https://learn.microsoft.com/en-us/azure/key-vault/managed-hsm/pkcs11-library
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget ca-certificates \
    && rm -rf /var/lib/apt/lists/*

RUN wget -qO /tmp/azure-mhsm-pkcs11.deb \
    "https://github.com/AzureAD/microsoft-authentication-library-for-cpp/releases/download/latest/azure-mhsm-pkcs11_amd64.deb" \
    && dpkg -i /tmp/azure-mhsm-pkcs11.deb \
    && rm /tmp/azure-mhsm-pkcs11.deb

# PKCS#11 library path
ENV PKCS11_LIBRARY_PATH=/usr/local/lib/libazure_mhsm_pkcs11.so

USER 10001
```

**Config (`/etc/azure-mhsm-pkcs11.json`):**

```json
{
  "hsm_url": "https://ivf-signing-hsm.managedhsm.azure.net",
  "auth": {
    "type": "managed_identity",
    "client_id": "CLIENT_ID_OF_MANAGED_IDENTITY"
  },
  "slot": {
    "label": "IVF-Signing",
    "key_name": "ivf-pdf-signer"
  },
  "logging": {
    "level": "warn",
    "file": "/var/log/azure-mhsm-pkcs11.log"
  }
}
```

### 6.4. Azure Key Vault (REST API approach)

Với Key Vault standard, sử dụng REST API signing — cần custom `CryptoTokenType.AzureKeyVault`:

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

public async Task<byte[]> SignWithAzureKvAsync(byte[] digest)
{
    var credential = new DefaultAzureCredential();
    var client = new CryptographyClient(
        new Uri($"https://{_options.AzureKeyVaultName}.vault.azure.net/keys/{_options.AzureKeyName}/{_options.AzureKeyVersion}"),
        credential);

    var result = await client.SignAsync(SignatureAlgorithm.RS256, digest);
    return result.Signature;
}
```

### 6.5. Managed Identity (AKS / Container Apps)

```yaml
# Azure Container Apps — Managed Identity
resources:
  - type: Microsoft.ManagedIdentity/userAssignedIdentities
    name: ivf-signing-identity

  - type: Microsoft.KeyVault/managedHSMs/roleAssignments
    properties:
      roleDefinitionId: "Managed HSM Crypto User"
      principalId: "<managed-identity-object-id>"
```

---

## 7. Google Cloud KMS

### 7.1. Setup

```bash
# 1. Tạo key ring
gcloud kms keyrings create ivf-signing \
    --location asia-southeast1

# 2. Tạo signing key (HSM protection level = FIPS 140-2 Level 3)
gcloud kms keys create pdf-signer \
    --location asia-southeast1 \
    --keyring ivf-signing \
    --purpose asymmetric-signing \
    --default-algorithm rsa-sign-pkcs1-2048-sha256 \
    --protection-level hsm

# 3. Lấy public key
gcloud kms keys versions get-public-key 1 \
    --key pdf-signer \
    --keyring ivf-signing \
    --location asia-southeast1 \
    --output-file signer-public-key.pem

# 4. Grant signing permission
gcloud kms keys add-iam-policy-binding pdf-signer \
    --location asia-southeast1 \
    --keyring ivf-signing \
    --member "serviceAccount:ivf-signing@PROJECT.iam.gserviceaccount.com" \
    --role "roles/cloudkms.signerVerifier"
```

### 7.2. PKCS#11 Bridge — `libkmsp11`

Google cung cấp [PKCS#11 library](https://cloud.google.com/kms/docs/reference/pkcs11-library):

```dockerfile
FROM keyfactor/signserver-ce:latest

USER root

# Google Cloud KMS PKCS#11 library
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Download libkmsp11
RUN wget -qO /tmp/libkmsp11.deb \
    "https://storage.googleapis.com/cloud-kms-pkcs11/latest/libkmsp11-linux-amd64.deb" \
    && dpkg -i /tmp/libkmsp11.deb \
    && rm /tmp/libkmsp11.deb

ENV PKCS11_LIBRARY_PATH=/usr/local/lib/libkmsp11.so
ENV KMS_PKCS11_CONFIG=/etc/libkmsp11/pkcs11-config.yaml

USER 10001
```

**Config (`pkcs11-config.yaml`):**

```yaml
---
tokens:
  - key_ring: "projects/PROJECT/locations/asia-southeast1/keyRings/ivf-signing"
    label: "IVF-Signing"
    # Service account credentials (prefer Workload Identity in GKE)
    # credentials: "/etc/gcp/sa-key.json"
```

**Đăng ký trong SignServer:**

```bash
docker exec ivf-signserver bin/signserver setproperty global \
    GLOB.WORKER_PKCS11_LIBRARY.GCPKMS \
    /usr/local/lib/libkmsp11.so

# Worker config
docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    SHAREDLIBRARYNAME GCPKMS
docker exec ivf-signserver bin/signserver setproperty <WORKER_ID> \
    SLOT "IVF-Signing"
```

### 7.3. Workload Identity (GKE)

```yaml
# GKE ServiceAccount annotation
apiVersion: v1
kind: ServiceAccount
metadata:
  name: ivf-signserver
  annotations:
    iam.gke.io/gcp-service-account: ivf-signing@PROJECT.iam.gserviceaccount.com

---
# GCP IAM binding
# gcloud iam service-accounts add-iam-policy-binding \
#   ivf-signing@PROJECT.iam.gserviceaccount.com \
#   --member "serviceAccount:PROJECT.svc.id.goog[ivf/ivf-signserver]" \
#   --role "roles/iam.workloadIdentityUser"
```

---

## 8. HashiCorp Vault Transit

### 8.1. Khi nào dùng Vault?

- **Multi-cloud abstraction**: Vault Transit có thể wrap Cloud KMS backends
- **On-premise + cloud hybrid**: Vault chạy on-prem, dùng Auto-Unseal với Cloud KMS
- **Centralized secrets**: Vault quản lý tất cả secrets + signing keys
- **Policy as code**: Vault policies cho fine-grained access control

### 8.2. Setup Vault Transit Engine

```bash
# Enable transit engine
vault secrets enable transit

# Create signing key
vault write transit/keys/ivf-pdf-signer \
    type=rsa-2048 \
    exportable=false \
    allow_plaintext_backup=false

# Create policy
vault policy write ivf-signer - <<EOF
path "transit/sign/ivf-pdf-signer" {
  capabilities = ["update"]
}
path "transit/verify/ivf-pdf-signer" {
  capabilities = ["update"]
}
path "transit/keys/ivf-pdf-signer" {
  capabilities = ["read"]
}
EOF

# Create AppRole for SignServer
vault auth enable approle
vault write auth/approle/role/signserver \
    token_policies="ivf-signer" \
    token_ttl=1h \
    token_max_ttl=24h
```

### 8.3. PKCS#11 Bridge — `vault-pkcs11-provider`

```bash
# Download Vault PKCS#11 provider
wget https://releases.hashicorp.com/vault-pkcs11-provider/0.3.0/vault-pkcs11-provider_0.3.0_linux_amd64.zip
unzip vault-pkcs11-provider_0.3.0_linux_amd64.zip -d /usr/local/lib/
```

**Config:**

```hcl
# /etc/vault-pkcs11.hcl
vault_addr = "https://vault.ivf.local:8200"

auth {
  type = "approle"
  config {
    role_id_file   = "/run/secrets/vault_role_id"
    secret_id_file = "/run/secrets/vault_secret_id"
  }
}

slot {
  label   = "IVF-Vault-Transit"
  key     = "ivf-pdf-signer"
  transit = "transit"
}

tls {
  ca_cert = "/etc/vault/ca.pem"
}
```

### 8.4. Docker Compose — Vault + SignServer

```yaml
# docker-compose.vault.yml
services:
  vault:
    image: hashicorp/vault:1.16
    container_name: ivf-vault
    cap_add: ["IPC_LOCK"]
    environment:
      - VAULT_ADDR=http://0.0.0.0:8200
      - VAULT_API_ADDR=http://vault:8200
    volumes:
      - vault_data:/vault/data
      - ./config/vault:/vault/config:ro
    command: vault server -config=/vault/config/vault.hcl
    networks: [ivf-signing]
    ports:
      - "8200:8200"
    healthcheck:
      test: ["CMD", "vault", "status"]
      interval: 30s
      timeout: 5s
      retries: 3

  signserver:
    # ... existing config ...
    environment:
      - VAULT_ADDR=http://vault:8200
    volumes:
      - ./config/vault-pkcs11.hcl:/etc/vault-pkcs11.hcl:ro
    depends_on:
      vault: { condition: service_healthy }

volumes:
  vault_data:
```

---

## 9. Mở rộng CryptoTokenType

### 9.1. Enum mở rộng

```csharp
// src/IVF.API/Services/DigitalSigningOptions.cs

/// <summary>
/// Loại crypto token cho SignServer workers.
/// </summary>
public enum CryptoTokenType
{
    /// PKCS#12 file-based keystore (Phase 1-3)
    P12,

    /// SoftHSM2 or hardware HSM — FIPS 140-2 Level 1 (Phase 4)
    PKCS11,

    /// AWS CloudHSM — FIPS 140-2 Level 3 (Phase 5+)
    AwsCloudHsm,

    /// Azure Managed HSM — FIPS 140-2 Level 3 (Phase 5+)
    AzureManagedHsm,

    /// Azure Key Vault — FIPS 140-2 Level 2 (Phase 5+)
    AzureKeyVault,

    /// Google Cloud KMS/HSM — FIPS 140-2 Level 1-3 (Phase 5+)
    GoogleCloudKms,

    /// HashiCorp Vault Transit — FIPS 140-2 Level 1 (Phase 5+)
    VaultTransit
}
```

### 9.2. Properties mở rộng

```csharp
public class DigitalSigningOptions
{
    // ... existing properties ...

    // ──── Cloud KMS Common ────
    /// <summary>Cloud provider region (ap-southeast-1, southeastasia, asia-southeast1)</summary>
    public string? CloudKmsRegion { get; set; }

    // ──── AWS CloudHSM ────
    public string? AwsCloudHsmClusterId { get; set; }
    public string? AwsCloudHsmCuUser { get; set; }
    public string? AwsCloudHsmCuPasswordFile { get; set; }

    // ──── AWS KMS ────
    public string? AwsKmsKeyArn { get; set; }

    // ──── Azure Managed HSM ────
    public string? AzureManagedHsmUrl { get; set; }
    public string? AzureManagedIdentityClientId { get; set; }

    // ──── Azure Key Vault ────
    public string? AzureKeyVaultUrl { get; set; }
    public string? AzureKeyName { get; set; }
    public string? AzureKeyVersion { get; set; }

    // ──── Google Cloud KMS ────
    public string? GcpKmsKeyRing { get; set; }
    public string? GcpKmsKeyName { get; set; }
    public string? GcpKmsKeyVersion { get; set; }
    public string? GcpServiceAccountKeyFile { get; set; }

    // ──── HashiCorp Vault ────
    public string? VaultAddr { get; set; }
    public string? VaultTransitMount { get; set; } = "transit";
    public string? VaultTransitKeyName { get; set; }
    public string? VaultRoleIdFile { get; set; }
    public string? VaultSecretIdFile { get; set; }

    /// <summary>
    /// Resolve PKCS#11 shared library path based on CryptoTokenType.
    /// </summary>
    public string ResolvePkcs11Library() => CryptoTokenType switch
    {
        CryptoTokenType.P12 => throw new InvalidOperationException("P12 does not use PKCS#11"),
        CryptoTokenType.PKCS11 => "/usr/lib/softhsm/libsofthsm2.so",
        CryptoTokenType.AwsCloudHsm => "/opt/cloudhsm/lib/libcloudhsm_pkcs11.so",
        CryptoTokenType.AzureManagedHsm => "/usr/local/lib/libazure_mhsm_pkcs11.so",
        CryptoTokenType.GoogleCloudKms => "/usr/local/lib/libkmsp11.so",
        CryptoTokenType.VaultTransit => "/usr/local/lib/libvault_pkcs11.so",
        _ => throw new NotSupportedException($"Unsupported CryptoTokenType: {CryptoTokenType}")
    };

    /// <summary>
    /// Validate Cloud KMS configuration for production.
    /// </summary>
    public List<string> ValidateCloudKms()
    {
        var errors = new List<string>();

        switch (CryptoTokenType)
        {
            case CryptoTokenType.AwsCloudHsm:
                if (string.IsNullOrEmpty(AwsCloudHsmClusterId))
                    errors.Add("AwsCloudHsmClusterId is required");
                if (string.IsNullOrEmpty(AwsCloudHsmCuUser))
                    errors.Add("AwsCloudHsmCuUser is required");
                break;

            case CryptoTokenType.AzureManagedHsm:
                if (string.IsNullOrEmpty(AzureManagedHsmUrl))
                    errors.Add("AzureManagedHsmUrl is required");
                break;

            case CryptoTokenType.AzureKeyVault:
                if (string.IsNullOrEmpty(AzureKeyVaultUrl))
                    errors.Add("AzureKeyVaultUrl is required");
                if (string.IsNullOrEmpty(AzureKeyName))
                    errors.Add("AzureKeyName is required");
                break;

            case CryptoTokenType.GoogleCloudKms:
                if (string.IsNullOrEmpty(GcpKmsKeyRing))
                    errors.Add("GcpKmsKeyRing is required");
                if (string.IsNullOrEmpty(GcpKmsKeyName))
                    errors.Add("GcpKmsKeyName is required");
                break;

            case CryptoTokenType.VaultTransit:
                if (string.IsNullOrEmpty(VaultAddr))
                    errors.Add("VaultAddr is required");
                if (string.IsNullOrEmpty(VaultTransitKeyName))
                    errors.Add("VaultTransitKeyName is required");
                break;
        }

        return errors;
    }
}
```

### 9.3. Compliance Check mở rộng

```csharp
// SecurityComplianceService.cs — thêm Cloud KMS checks

// HSM-001 updated
new ComplianceCheck
{
    Id = "HSM-001",
    Name = "Crypto Token Type",
    Phase = 4,
    Category = "Compliance",
    Status = opts.CryptoTokenType switch
    {
        CryptoTokenType.P12 => ComplianceStatus.Warning,
        CryptoTokenType.PKCS11 => ComplianceStatus.Pass,
        CryptoTokenType.AwsCloudHsm => ComplianceStatus.Pass,
        CryptoTokenType.AzureManagedHsm => ComplianceStatus.Pass,
        CryptoTokenType.GoogleCloudKms => ComplianceStatus.Pass,
        _ => ComplianceStatus.Info
    },
    Detail = $"CryptoTokenType={opts.CryptoTokenType}"
};

// New: CLOUD-001
new ComplianceCheck
{
    Id = "CLOUD-001",
    Name = "Cloud KMS Configuration",
    Phase = 5,
    Category = "Cloud Security",
    Status = opts.ValidateCloudKms().Count == 0
        ? ComplianceStatus.Pass
        : ComplianceStatus.Fail,
    Detail = string.Join("; ", opts.ValidateCloudKms())
};
```

---

## 10. Cập nhật cấu hình

### 10.1. appsettings.json — AWS CloudHSM

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "CryptoTokenType": "AwsCloudHsm",
    "SkipTlsValidation": false,
    "RequireMtls": true,
    "EnableAuditLogging": true,

    "Pkcs11SharedLibraryName": "AWSCLOUDHSM",
    "Pkcs11SlotLabel": "signserver_cu",

    "CloudKmsRegion": "ap-southeast-1",
    "AwsCloudHsmClusterId": "cluster-xxx",
    "AwsCloudHsmCuUser": "signserver_cu",
    "AwsCloudHsmCuPasswordFile": "/run/secrets/cloudhsm_cu_password",

    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem"
  }
}
```

### 10.2. appsettings.json — Azure Managed HSM

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "CryptoTokenType": "AzureManagedHsm",
    "SkipTlsValidation": false,
    "RequireMtls": true,
    "EnableAuditLogging": true,

    "Pkcs11SharedLibraryName": "AZUREHSM",
    "Pkcs11SlotLabel": "IVF-Signing",

    "AzureManagedHsmUrl": "https://ivf-signing-hsm.managedhsm.azure.net",
    "AzureManagedIdentityClientId": "CLIENT_ID",

    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem"
  }
}
```

### 10.3. appsettings.json — Google Cloud KMS

```json
{
  "DigitalSigning": {
    "Enabled": true,
    "SignServerUrl": "https://signserver:8443/signserver",
    "CryptoTokenType": "GoogleCloudKms",
    "SkipTlsValidation": false,
    "RequireMtls": true,
    "EnableAuditLogging": true,

    "Pkcs11SharedLibraryName": "GCPKMS",
    "Pkcs11SlotLabel": "IVF-Signing",

    "CloudKmsRegion": "asia-southeast1",
    "GcpKmsKeyRing": "projects/PROJECT/locations/asia-southeast1/keyRings/ivf-signing",
    "GcpKmsKeyName": "pdf-signer",
    "GcpKmsKeyVersion": "1",

    "ClientCertificatePath": "/app/certs/api-client.p12",
    "ClientCertificatePasswordFile": "/run/secrets/api_cert_password",
    "TrustedCaCertPath": "/app/certs/ca-chain.pem"
  }
}
```

### 10.4. Environment Variables Override (Docker)

```yaml
# docker-compose.cloud-kms.yml
services:
  ivf-api:
    environment:
      # AWS CloudHSM
      - DigitalSigning__CryptoTokenType=AwsCloudHsm
      - DigitalSigning__Pkcs11SharedLibraryName=AWSCLOUDHSM
      - DigitalSigning__CloudKmsRegion=ap-southeast-1
      - DigitalSigning__AwsCloudHsmClusterId=cluster-xxx
      - DigitalSigning__AwsCloudHsmCuUser=signserver_cu
      - DigitalSigning__AwsCloudHsmCuPasswordFile=/run/secrets/cloudhsm_cu_password

      # OR Azure Managed HSM
      # - DigitalSigning__CryptoTokenType=AzureManagedHsm
      # - DigitalSigning__AzureManagedHsmUrl=https://ivf-signing-hsm.managedhsm.azure.net

      # OR Google Cloud KMS
      # - DigitalSigning__CryptoTokenType=GoogleCloudKms
      # - DigitalSigning__GcpKmsKeyRing=projects/PROJECT/locations/.../keyRings/ivf-signing
```

---

## 11. Docker & Network

### 11.1. Network Requirements

| Cloud KMS Provider | Outbound Access                    | Port      | Protocol |
| ------------------ | ---------------------------------- | --------- | -------- |
| AWS CloudHSM       | CloudHSM ENI (VPC)                 | 2223-2225 | TCP      |
| AWS KMS            | `kms.ap-southeast-1.amazonaws.com` | 443       | HTTPS    |
| Azure Managed HSM  | `*.managedhsm.azure.net`           | 443       | HTTPS    |
| Azure Key Vault    | `*.vault.azure.net`                | 443       | HTTPS    |
| Google Cloud KMS   | `cloudkms.googleapis.com`          | 443       | HTTPS    |
| HashiCorp Vault    | Vault server address               | 8200      | HTTPS    |

### 11.2. Docker Network Changes

Với Cloud KMS, SignServer container cần **egress** ra ngoài:

```yaml
networks:
  ivf-public:
    driver: bridge
  ivf-signing:
    driver: bridge
    internal: false # ← Changed from true! Cần egress cho Cloud KMS
    ipam:
      config:
        - subnet: 172.20.0.0/24
  ivf-data:
    driver: bridge
    internal: true
```

> ⚠️ **Security trade-off:** `ivf-signing` network không còn internal. Thay vào đó, dùng firewall rules / security groups để giới hạn egress chỉ đến Cloud KMS endpoints.

### 11.3. Firewall Rules (iptables)

```bash
# Allow egress only to Cloud KMS endpoints
# AWS CloudHSM (VPC ENI - restrict to cluster IPs)
iptables -A DOCKER-USER -s 172.20.0.0/24 -d 10.0.1.100 -p tcp --dport 2223:2225 -j ACCEPT
iptables -A DOCKER-USER -s 172.20.0.0/24 -d 10.0.2.100 -p tcp --dport 2223:2225 -j ACCEPT

# AWS KMS API
iptables -A DOCKER-USER -s 172.20.0.0/24 -d 0.0.0.0/0 -p tcp --dport 443 -m string \
    --string "kms.ap-southeast-1.amazonaws.com" --algo bm -j ACCEPT

# Block all other egress from signing network
iptables -A DOCKER-USER -s 172.20.0.0/24 -j DROP
```

### 11.4. Kubernetes Deployment (EKS / AKS / GKE)

```yaml
# k8s/signserver-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ivf-signserver
  namespace: ivf
spec:
  replicas: 2
  selector:
    matchLabels: { app: signserver }
  template:
    metadata:
      labels: { app: signserver }
    spec:
      serviceAccountName: ivf-signserver # Workload Identity
      securityContext:
        runAsNonRoot: true
        runAsUser: 10001
        fsGroup: 10001
        seccompProfile: { type: RuntimeDefault }
      containers:
        - name: signserver
          image: registry.ivf.local/signserver-cloudhsm:latest
          ports:
            - containerPort: 8443
          env:
            - name: DATABASE_JDBC_URL
              value: "jdbc:postgresql://signserver-db:5432/signserver"
            - name: DATABASE_USER
              valueFrom:
                secretKeyRef: { name: signserver-secrets, key: db-user }
            - name: DATABASE_PASSWORD
              valueFrom:
                secretKeyRef: { name: signserver-secrets, key: db-password }
          volumeMounts:
            - name: persistent
              mountPath: /opt/keyfactor/persistent
            - name: cloudhsm-config
              mountPath: /opt/cloudhsm/etc
              readOnly: true
          resources:
            requests: { cpu: "500m", memory: "512Mi" }
            limits: { cpu: "2000m", memory: "2Gi" }
          readinessProbe:
            httpGet:
              {
                path: /signserver/healthcheck/signserverhealth,
                port: 8443,
                scheme: HTTPS,
              }
            initialDelaySeconds: 60
            periodSeconds: 30
          livenessProbe:
            httpGet:
              {
                path: /signserver/healthcheck/signserverhealth,
                port: 8443,
                scheme: HTTPS,
              }
            initialDelaySeconds: 120
            periodSeconds: 60
      volumes:
        - name: persistent
          persistentVolumeClaim: { claimName: signserver-pvc }
        - name: cloudhsm-config
          configMap: { name: cloudhsm-config }

---
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: signserver-egress
  namespace: ivf
spec:
  podSelector:
    matchLabels: { app: signserver }
  policyTypes: ["Egress"]
  egress:
    # Allow DNS
    - to: [{ namespaceSelector: {} }]
      ports: [{ port: 53, protocol: UDP }, { port: 53, protocol: TCP }]
    # Allow CloudHSM ENI
    - to: [{ ipBlock: { cidr: "10.0.0.0/16" } }]
      ports:
        [
          { port: 2223, protocol: TCP },
          { port: 2224, protocol: TCP },
          { port: 2225, protocol: TCP },
        ]
    # Allow DB
    - to: [{ podSelector: { matchLabels: { app: signserver-db } } }]
      ports: [{ port: 5432, protocol: TCP }]
```

---

## 12. Migration SoftHSM2 → Cloud KMS

### 12.1. Migration Strategy

> **Quan trọng:** Private keys trong SoftHSM2 có `CKA_EXTRACTABLE=FALSE` — KHÔNG THỂ export ra ngoài. Migration phải **generate new keys** trong Cloud KMS và **re-sign certificates**.

```
Migration Flow:
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Phase A     │────>│  Phase B     │────>│  Phase C     │
│  Parallel    │     │  Cutover     │     │  Cleanup     │
│  (1-2 weeks) │     │  (1 day)     │     │  (1 week)    │
└──────────────┘     └──────────────┘     └──────────────┘
```

### 12.2. Phase A — Parallel Run

```bash
#!/bin/bash
# scripts/migrate-to-cloud-kms.sh

set -euo pipefail

CLOUD_PROVIDER="${1:?Usage: $0 <aws|azure|gcp>}"
WORKER_IDS="${2:-all}"

echo "=== Phase A: Generate Cloud KMS keys ==="

case "$CLOUD_PROVIDER" in
    aws)
        LIB_NAME="AWSCLOUDHSM"
        LIB_PATH="/opt/cloudhsm/lib/libcloudhsm_pkcs11.so"
        ;;
    azure)
        LIB_NAME="AZUREHSM"
        LIB_PATH="/usr/local/lib/libazure_mhsm_pkcs11.so"
        ;;
    gcp)
        LIB_NAME="GCPKMS"
        LIB_PATH="/usr/local/lib/libkmsp11.so"
        ;;
esac

# 1. Register Cloud KMS PKCS#11 library in SignServer
docker exec ivf-signserver bin/signserver setproperty global \
    "GLOB.WORKER_PKCS11_LIBRARY.${LIB_NAME}" "${LIB_PATH}"

# 2. For each worker — create parallel Cloud KMS worker
get_worker_ids() {
    if [ "$WORKER_IDS" = "all" ]; then
        docker exec ivf-signserver bin/signserver getstatus brief all \
            | grep "Worker" | awk '{print $2}' | tr -d ':'
    else
        echo "$WORKER_IDS" | tr ',' '\n'
    fi
}

for WID in $(get_worker_ids); do
    echo "--- Worker $WID: Creating Cloud KMS parallel worker ---"

    # Get current worker name
    WORKER_NAME=$(docker exec ivf-signserver bin/signserver getconfig "$WID" \
        | grep "NAME" | awk -F'=' '{print $2}' | tr -d ' ')

    NEW_WID=$((WID + 10000))
    NEW_NAME="${WORKER_NAME}_cloudkms"

    # Generate new key in Cloud KMS
    docker exec ivf-signserver bin/signserver generatekey \
        -worker "$NEW_WID" \
        -keyalg RSA -keyspec 2048 \
        -alias "${NEW_NAME}_signer"

    # Clone worker config with Cloud KMS token
    docker exec ivf-signserver bin/signserver setproperties - <<EOF
WORKER${NEW_WID}.TYPE=PROCESSABLE
WORKER${NEW_WID}.NAME=${NEW_NAME}
WORKER${NEW_WID}.IMPLEMENTATION_CLASS=org.signserver.module.pdfsigner.PDFSigner
WORKER${NEW_WID}.SIGNERTOKEN_CLASSPATH=org.signserver.server.cryptotokens.PKCS11CryptoToken
WORKER${NEW_WID}.SHAREDLIBRARYNAME=${LIB_NAME}
WORKER${NEW_WID}.DEFAULTKEY=${NEW_NAME}_signer
WORKER${NEW_WID}.CKA_EXTRACTABLE=FALSE
WORKER${NEW_WID}.CKA_SENSITIVE=TRUE
WORKER${NEW_WID}.AUTHTYPE=org.signserver.server.ClientCertAuthorizer
EOF

    # Generate CSR → sign with EJBCA → install cert
    docker exec ivf-signserver bin/signserver generatecertreq \
        "$NEW_WID" "${NEW_NAME}_signer" \
        "CN=${WORKER_NAME},O=IVF,C=VN" > "/tmp/${NEW_NAME}.csr"

    echo "Worker ${NEW_WID} (${NEW_NAME}) created — pending cert installation"
done

echo "=== Phase A complete ==="
echo "Next: Install certificates, test signing, then run Phase B cutover"
```

### 12.3. Phase B — Cutover

```bash
#!/bin/bash
# scripts/cutover-cloud-kms.sh

set -euo pipefail

echo "=== Phase B: Cutover to Cloud KMS workers ==="

# 1. Test all Cloud KMS workers
for WID in $(docker exec ivf-signserver bin/signserver getstatus brief all \
    | grep "_cloudkms" | awk '{print $2}' | tr -d ':'); do
    echo "Testing worker $WID..."
    STATUS=$(docker exec ivf-signserver bin/signserver getstatus complete "$WID" \
        | grep "Status" | head -1)
    if [[ "$STATUS" != *"Active"* ]]; then
        echo "ERROR: Worker $WID is not Active: $STATUS"
        exit 1
    fi
done

echo "All Cloud KMS workers active. Proceeding with cutover..."

# 2. Update IVF API config
echo "Update DigitalSigning__CryptoTokenType in docker-compose / appsettings"
echo "Restart IVF API container"

# 3. Deactivate old SoftHSM2 workers
for WID in $(docker exec ivf-signserver bin/signserver getstatus brief all \
    | grep -v "_cloudkms" | grep "Worker" | awk '{print $2}' | tr -d ':'); do
    echo "Deactivating SoftHSM2 worker $WID..."
    docker exec ivf-signserver bin/signserver deactivatecryptotoken "$WID"
done

echo "=== Phase B complete — Cloud KMS is now active ==="
```

### 12.4. Phase C — Cleanup

```bash
# After 7-day monitoring period
# 1. Remove old SoftHSM2 workers
docker exec ivf-signserver bin/signserver removeworker <OLD_WID>

# 2. Remove SoftHSM2 library registration
docker exec ivf-signserver bin/signserver removeproperty global \
    GLOB.WORKER_PKCS11_LIBRARY.SOFTHSM

# 3. Remove SoftHSM2 volumes
docker volume rm ivf_softhsm_tokens

# 4. Update Docker image (remove SoftHSM2 packages)
```

---

## 13. Compliance & Audit

### 13.1. FIPS 140-2 Level Mapping

| CryptoTokenType        | FIPS Level  | Certification                | Audit evidence                                     |
| ---------------------- | ----------- | ---------------------------- | -------------------------------------------------- |
| `P12`                  | None        | N/A                          | `compliance-audit` → Warning                       |
| `PKCS11` (SoftHSM2)    | Level 1     | Software-only                | `security-audit-evidence`                          |
| `AwsCloudHsm`          | **Level 3** | Cavium/Marvell hardware cert | AWS compliance reports + `security-audit-evidence` |
| `AzureManagedHsm`      | **Level 3** | Marvell LiquidSecurity cert  | Azure compliance docs + `security-audit-evidence`  |
| `AzureKeyVault`        | Level 2     | Hardware-backed              | Azure compliance docs                              |
| `GoogleCloudKms` (HSM) | **Level 3** | Cavium hardware cert         | GCP compliance docs + `security-audit-evidence`    |
| `VaultTransit`         | Level 1     | Software-only                | Vault audit logs                                   |

### 13.2. Cloud Provider Compliance Reports

| Provider  | Report type                          | URL / location         |
| --------- | ------------------------------------ | ---------------------- |
| **AWS**   | SOC 1/2/3, ISO 27001, PCI DSS, HIPAA | AWS Artifact Console   |
| **Azure** | SOC 1/2/3, ISO 27001, PCI DSS, HIPAA | Azure Trust Center     |
| **GCP**   | SOC 1/2/3, ISO 27001, PCI DSS, HIPAA | GCP Compliance Reports |
| **Vault** | SOC 2 Type II (Enterprise)           | HashiCorp Trust Center |

### 13.3. Audit Trail Integration

| Event          | SoftHSM2 (hiện tại)    | Cloud KMS                                |
| -------------- | ---------------------- | ---------------------------------------- |
| Key creation   | App-level log          | CloudTrail / Azure Monitor / Cloud Audit |
| Sign operation | SignServer audit log   | Cloud KMS API log + SignServer audit log |
| Key access     | App-level log          | IAM access log                           |
| Key rotation   | App-level log + script | Cloud KMS automatic rotation             |
| Policy change  | Manual tracking        | IAM policy change log                    |

### 13.4. Combined Audit Package

Khi sử dụng Cloud KMS, `GET /security-audit-evidence` response bổ sung:

```json
{
  "cloudKmsInfo": {
    "provider": "AwsCloudHsm",
    "region": "ap-southeast-1",
    "clusterId": "cluster-xxx",
    "fipsLevel": "Level 3",
    "certificationId": "NIST FIPS 140-2 #3254",
    "keyCount": 5,
    "auditTrail": "AWS CloudTrail - ap-southeast-1",
    "complianceDocs": [
      "SOC 2 Type II (AWS Artifact)",
      "ISO 27001 (AWS Artifact)",
      "FIPS 140-2 Level 3 Certificate"
    ]
  }
}
```

---

## 14. Monitoring & Alerting

### 14.1. Health Check Flow

```
IVF API ──(30s interval)──> SignServer /healthcheck
                         ──> Cloud KMS status API
                         ──> Certificate expiry check
```

### 14.2. Monitoring Targets

| Metric                 | Source                                     | Alert threshold           |
| ---------------------- | ------------------------------------------ | ------------------------- |
| SignServer health      | `/signserver/healthcheck/signserverhealth` | Status ≠ `ALLOK`          |
| Cloud KMS latency      | PKCS#11 operation timing                   | > 100ms p99               |
| Cloud KMS availability | Provider status page / SDK error rate      | > 0.1% error rate         |
| Key usage count        | Cloud KMS API logs                         | Anomaly detection         |
| Certificate expiry     | `rotate-certs.sh --check`                  | < 30 days remaining       |
| HSM cluster status     | Provider API                               | Degraded / single-AZ      |
| Signing throughput     | SignServer stats                           | < 10 signs/sec (baseline) |

### 14.3. CloudWatch Alarms (AWS)

```yaml
# cloudformation/monitoring.yaml
Resources:
  CloudHSMAvailabilityAlarm:
    Type: AWS::CloudWatch::Alarm
    Properties:
      AlarmName: IVF-CloudHSM-Availability
      MetricName: HSMAvailability
      Namespace: AWS/CloudHSM
      Statistic: Minimum
      Period: 300
      EvaluationPeriods: 2
      Threshold: 1
      ComparisonOperator: LessThanThreshold
      AlarmActions: ["arn:aws:sns:ap-southeast-1:ACCOUNT:ivf-alerts"]

  SigningLatencyAlarm:
    Type: AWS::CloudWatch::Alarm
    Properties:
      AlarmName: IVF-Signing-Latency
      MetricName: SigningLatencyMs
      Namespace: IVF/DigitalSigning
      Statistic: p99
      Period: 300
      Threshold: 100
      ComparisonOperator: GreaterThanThreshold
```

### 14.4. Azure Monitor Alerts

```json
{
  "properties": {
    "severity": 1,
    "enabled": true,
    "scopes": [
      "/subscriptions/SUB/resourceGroups/ivf-production/providers/Microsoft.KeyVault/managedHSMs/ivf-signing-hsm"
    ],
    "evaluationFrequency": "PT5M",
    "windowSize": "PT15M",
    "criteria": {
      "allOf": [
        {
          "metricName": "Availability",
          "operator": "LessThan",
          "threshold": 99.9,
          "timeAggregation": "Average"
        }
      ]
    },
    "actions": [
      {
        "actionGroupId": "/subscriptions/SUB/resourceGroups/ivf/providers/microsoft.insights/actionGroups/ivf-critical"
      }
    ]
  }
}
```

---

## 15. Disaster Recovery

### 15.1. Backup Strategy

| Component             | Backup method                         | RTO           | RPO             |
| --------------------- | ------------------------------------- | ------------- | --------------- |
| **AWS CloudHSM**      | Cross-region cluster clone            | 4h            | 0 (synchronous) |
| **Azure Managed HSM** | Security domain backup + key export   | 8h            | 1h              |
| **Google Cloud KMS**  | Multi-region key rings                | 0 (automatic) | 0               |
| **SignServer config** | `signserver dumpproperties` → S3/Blob | 1h            | daily           |
| **Certificates**      | Encrypted backup to object storage    | 1h            | daily           |
| **Application DB**    | Cloud DB snapshots                    | 1h            | 5min (PITR)     |

### 15.2. Failover Scenarios

#### Scenario 1: Single HSM failure

```
AWS CloudHSM: Automatic failover to second HSM (multi-AZ)
Azure Managed HSM: Automatic failover within pool
Google Cloud KMS: Automatic (managed service)
Impact: None — transparent to application
```

#### Scenario 2: Cloud region failure

```
1. Activate DR region SignServer instance
2. Update DNS to point to DR region
3. Cloud KMS: Use cross-region key replica
4. IVF API: Update SignServerUrl config
5. Verify signing operations
```

#### Scenario 3: Cloud KMS service outage

```
1. Alert fires (availability < 99.9%)
2. IF extended outage (> 30 min):
   a. Activate local SoftHSM2 fallback
   b. Update CryptoTokenType → PKCS11
   c. Restart SignServer with SoftHSM2 library
3. When Cloud KMS recovers:
   a. Switch back to Cloud KMS
   b. Re-sign any documents signed with fallback key
```

### 15.3. Fallback Script

```bash
#!/bin/bash
# scripts/cloud-kms-failover.sh

ACTION="${1:?Usage: $0 <activate-fallback|restore-cloud>}"
CLOUD_LIB="${2:-AWSCLOUDHSM}"

case "$ACTION" in
    activate-fallback)
        echo "=== Activating SoftHSM2 fallback ==="
        # Re-register SoftHSM2 if removed
        docker exec ivf-signserver bin/signserver setproperty global \
            GLOB.WORKER_PKCS11_LIBRARY.SOFTHSM \
            /usr/lib/softhsm/libsofthsm2.so

        # Switch workers to SoftHSM2
        for WID in $(get_active_workers); do
            docker exec ivf-signserver bin/signserver setproperty "$WID" \
                SHAREDLIBRARYNAME SOFTHSM
            docker exec ivf-signserver bin/signserver reload "$WID"
        done
        echo "Fallback active. Update IVF API: CryptoTokenType=PKCS11"
        ;;

    restore-cloud)
        echo "=== Restoring Cloud KMS ==="
        for WID in $(get_active_workers); do
            docker exec ivf-signserver bin/signserver setproperty "$WID" \
                SHAREDLIBRARYNAME "$CLOUD_LIB"
            docker exec ivf-signserver bin/signserver reload "$WID"
        done
        echo "Cloud KMS restored. Update IVF API: CryptoTokenType=<CloudType>"
        ;;
esac
```

---

## 16. Chi phí ước tính

### 16.1. Monthly Cost Comparison

| Component                  | SoftHSM2 (now) | AWS CloudHSM        | Azure Managed HSM | Google Cloud HSM     |
| -------------------------- | -------------- | ------------------- | ----------------- | -------------------- |
| **HSM Instance**           | $0             | $1,500 × 2 = $3,000 | $3,488            | $0 (per-key pricing) |
| **Keys (5 workers)**       | $0             | included            | included          | 5 × $2.50 = $12.50   |
| **Operations (10K/month)** | $0             | included            | $0.15/10K = $0.15 | 10K × $0.03 = $300\* |
| **Network**                | $0             | VPC (included)      | VNet (included)   | $0                   |
| **Backup**                 | $5 (S3)        | included            | $3/backup         | $0                   |
| **Monitoring**             | $0             | CloudWatch $5       | Azure Monitor $5  | Stackdriver $5       |
| **Total/tháng**            | **~$5**        | **~$3,010**         | **~$3,498**       | **~$318**            |

> \* Google Cloud HSM signing operations: $0.03/operation. Với 10K operations/tháng, chi phí thấp nhưng scale linearly.

### 16.2. Cost Optimization

| Strategy                             | Savings       | Trade-off                                     |
| ------------------------------------ | ------------- | --------------------------------------------- |
| **AWS KMS thay CloudHSM**            | ~$2,900/tháng | FIPS Level 2 (not Level 3), no PKCS#11 native |
| **Azure Key Vault thay Managed HSM** | ~$3,450/tháng | FIPS Level 2, REST API only                   |
| **GCP standard KMS**                 | ~$300/tháng   | FIPS Level 1, PKCS#11 bridge                  |
| **Single HSM + standby**             | ~$1,500/tháng | Reduced HA                                    |
| **Reserved capacity (AWS)**          | 20-30%        | 1-year commitment                             |

---

## 17. Checklist triển khai

### Pre-deployment

- [ ] Chọn Cloud KMS provider phù hợp (AWS/Azure/GCP/Vault)
- [ ] Xác nhận FIPS compliance level cần thiết
- [ ] Ước tính chi phí hàng tháng
- [ ] Setup VPC/VNet peering (nếu CloudHSM/Managed HSM)
- [ ] Tạo IAM roles / service accounts
- [ ] Setup security groups / firewall rules
- [ ] Build Docker image với PKCS#11 library tương ứng

### Cloud KMS Setup

- [ ] Tạo KMS cluster / key ring / vault
- [ ] Initialize cluster (HSM-specific steps)
- [ ] Tạo signing keys (RSA-2048, non-exportable)
- [ ] Configure key rotation policy
- [ ] Test PKCS#11 library connectivity

### SignServer Integration

- [ ] Đăng ký PKCS#11 library trong SignServer (`GLOB.WORKER_PKCS11_LIBRARY.*`)
- [ ] Tạo parallel Cloud KMS workers cho mỗi signer
- [ ] Generate CSR → sign with EJBCA → install cert
- [ ] Activate và test từng worker
- [ ] Verify signing output (PDF valid, signature chain valid)

### IVF API Configuration

- [ ] Update `CryptoTokenType` trong appsettings / env vars
- [ ] Update `Pkcs11SharedLibraryName` cho Cloud KMS
- [ ] Thêm Cloud KMS-specific properties
- [ ] Mount secrets (credentials, passwords) qua Docker Secrets / K8s Secrets
- [ ] Test `GET /compliance-audit` — verify HSM-001 = Pass
- [ ] Test `GET /security-audit-evidence` — verify Cloud KMS section

### Monitoring

- [ ] Setup CloudWatch / Azure Monitor / Stackdriver alerts
- [ ] Configure signing latency alarms (p99 < 100ms)
- [ ] Configure HSM availability alarms (> 99.9%)
- [ ] Setup certificate expiry monitoring
- [ ] Setup anomaly detection cho key usage

### Cutover

- [ ] Parallel run ≥ 1 tuần — both SoftHSM2 + Cloud KMS workers
- [ ] Verify all Cloud KMS workers healthy
- [ ] Run `compliance-audit` — Grade ≥ A
- [ ] Run `pentest.sh --target all` — all pass
- [ ] Deactivate SoftHSM2 workers
- [ ] Update DNS / load balancer (nếu multi-region)
- [ ] Monitor 24h — no errors

### Post-deployment

- [ ] Remove SoftHSM2 workers (sau 7 ngày)
- [ ] Remove SoftHSM2 Docker volumes
- [ ] Update documentation
- [ ] Generate `security-audit-evidence` package
- [ ] Share compliance reports với auditor
- [ ] Setup DR / failover procedure
- [ ] Train ops team

---

## Phụ lục A: Quick Reference Commands

```bash
# ── AWS CloudHSM ──
# Check cluster status
aws cloudhsmv2 describe-clusters --filters clusterIds=cluster-xxx

# List keys
docker exec ivf-signserver /opt/cloudhsm/bin/cloudhsm-cli interactive
> login --username signserver_cu --role crypto-user
> key list

# ── Azure Managed HSM ──
# Check HSM status
az keyvault show --hsm-name ivf-signing-hsm

# List keys
az keyvault key list --hsm-name ivf-signing-hsm

# ── Google Cloud KMS ──
# Check key status
gcloud kms keys describe pdf-signer \
    --location asia-southeast1 --keyring ivf-signing

# List key versions
gcloud kms keys versions list --key pdf-signer \
    --location asia-southeast1 --keyring ivf-signing

# ── SignServer (all providers) ──
# Worker status
docker exec ivf-signserver bin/signserver getstatus brief all

# Test sign
docker exec ivf-signserver bin/signserver signdocument -workerId <WID> \
    -data "test" -encoding BASE64

# Compliance audit
curl -H "Authorization: Bearer $TOKEN" \
    http://localhost:5000/api/admin/signing/compliance-audit | jq '.summary'

# Security audit evidence
curl -H "Authorization: Bearer $TOKEN" \
    http://localhost:5000/api/admin/signing/security-audit-evidence | jq . \
    > audit_evidence_$(date +%Y%m%d).json
```

## Phụ lục B: Troubleshooting

| Vấn đề                    | Nguyên nhân                                  | Giải pháp                                         |
| ------------------------- | -------------------------------------------- | ------------------------------------------------- |
| `CKR_TOKEN_NOT_PRESENT`   | PKCS#11 library không kết nối được Cloud KMS | Kiểm tra network egress, credentials, config file |
| `CKR_PIN_INCORRECT`       | Sai CU password / PIN                        | Kiểm tra Docker Secret file, trailing newline     |
| SignServer `OFFLINE`      | Crypto token chưa activate                   | `signserver activatecryptotoken <WID> <PIN>`      |
| Signing latency > 200ms   | Network latency đến Cloud KMS                | Kiểm tra region, VPC routing, consider caching    |
| `CKR_GENERAL_ERROR`       | Library version mismatch                     | Update PKCS#11 library, check provider docs       |
| Health check fail         | Client cert rejected bởi Cloud KMS endpoint  | Dùng `CreateHandler(attachClientCert: false)`     |
| `FIPS-001 = Fail`         | Chưa config đủ mTLS + audit + PKCS#11        | Check all 4 conditions trong compliance audit     |
| `CKR_KEY_NOT_FOUND`       | Key label không match                        | `pkcs11-tool --list-objects` kiểm tra key label   |
| Certificate chain invalid | Self-signed cert chưa được replace           | Request cert từ EJBCA CA, install vào worker      |
| Multi-AZ failover slow    | DNS caching                                  | Giảm TTL DNS, dùng health check endpoint          |

---

> **Tài liệu tiếp theo:**
>
> - `signserver_implementation_guide.md` — Chi tiết Phase 1–4
> - `signserver_production_security.md` — Security reference cho production
> - Từng provider docs: [AWS CloudHSM](https://docs.aws.amazon.com/cloudhsm/), [Azure Managed HSM](https://learn.microsoft.com/azure/key-vault/managed-hsm/), [Google Cloud KMS](https://cloud.google.com/kms/docs/), [HashiCorp Vault](https://developer.hashicorp.com/vault/docs)
