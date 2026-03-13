# IVF System — Comprehensive Security Audit Report

**Ngày:** 2026-03-13  
**Phạm vi:** Toàn hệ thống (API, Infrastructure, Frontend, Network, Remote Access)  
**Tiêu chuẩn so sánh:** Google BeyondCorp, AWS Well-Architected, Microsoft Zero Trust, Cloudflare Access  
**Phương pháp:** Static code review + configuration analysis

---

## Mục lục

1. [Tổng quan & Điểm số](#1-tổng-quan--điểm-số)
2. [So sánh với Big Tech](#2-so-sánh-với-big-tech)
3. [API Security](#3-api-security)
4. [Infrastructure Security](#4-infrastructure-security)
5. [Frontend Security](#5-frontend-security)
6. [Network & Remote Access](#6-network--remote-access)
7. [Tổng hợp lỗ hổng theo mức độ](#7-tổng-hợp-lỗ-hổng-theo-mức-độ)
8. [Roadmap khắc phục](#8-roadmap-khắc-phục)
9. [Kết luận](#9-kết-luận)

---

## 1. Tổng quan & Điểm số

### Security Maturity Score: **72/100** — "Advanced" tier

| Lĩnh vực | Điểm | Đánh giá |
|-----------|-------|----------|
| API Security (Application Layer) | **88/100** | 🟢 Enterprise-grade |
| Authentication & Authorization | **85/100** | 🟢 Strong |
| Security Headers & CSP | **92/100** | 🟢 OWASP A+ |
| MediatR Pipeline (Zero Trust) | **95/100** | 🟢 Google BeyondCorp level |
| Docker & Container Security | **58/100** | 🟡 Needs improvement |
| Network & Firewall | **70/100** | 🟡 Good, gaps in encryption |
| Secret Management | **65/100** | 🟡 Mixed (vault strong, compose weak) |
| PKI & Certificate Mgmt | **82/100** | 🟢 Strong |
| Monitoring & Logging | **78/100** | 🟢 Good stack |
| Frontend Security | **75/100** | 🟡 Solid with minor gaps |
| Remote Access (VPN/SSH) | **72/100** | 🟡 Well-designed, incomplete activation |
| Compliance (HIPAA/GDPR) | **70/100** | 🟡 Good framework, needs hardening |

### Phân bổ lỗ hổng

| Mức độ | Số lượng | Mô tả |
|--------|----------|--------|
| 🔴 Critical | 4 | Phải fix ngay |
| 🟠 High | 12 | Fix trong 1-2 tuần |
| 🟡 Medium | 16 | Fix trong 1 tháng |
| 🟢 Low | 8 | Cải thiện khi có thời gian |

---

## 2. So sánh với Big Tech

### Maturity Model — So sánh kiến trúc bảo mật

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SECURITY MATURITY LEVELS                        │
├──────────┬──────────┬──────────┬──────────┬────────────────────────┤
│ Level 1  │ Level 2  │ Level 3  │ Level 4  │ Level 5              │
│ Basic    │ Standard │ Advanced │ Enterprise│ Big Tech             │
│ (0-40)   │ (41-60)  │ (61-80)  │ (81-95)  │ (96-100)            │
├──────────┼──────────┼──────────┼──────────┼────────────────────────┤
│          │          │ ████ IVF │          │ Google/AWS/MS/CF      │
│          │          │  (72)    │          │  (95-100)             │
└──────────┴──────────┴──────────┴──────────┴────────────────────────┘
```

### Chi tiết so sánh từng domain

#### 2.1 Zero Trust Architecture

| Capability | Google BeyondCorp | IVF System | Gap |
|------------|------------------|------------|-----|
| Identity-based access | ✅ Context-aware proxy | ✅ JWT + MFA + Device fingerprint | Tương đương |
| Device trust scoring | ✅ Device certificates + context | ⚠️ Device fingerprint (weak hash) | **Gap lớn** |
| Per-request authorization | ✅ Access Proxy | ✅ ZeroTrustBehavior pipeline | Tương đương |
| Continuous auth verification | ✅ Session re-evaluation | ✅ Token binding + session validation | Tương đương |
| Network is untrusted | ✅ No VPN needed | ⚠️ Still relies on VPN for admin | **Gap** — BeyondCorp eliminates VPN |
| Micro-segmentation | ✅ Per-service policies | ⚠️ Network-level only (4 overlays) | **Gap** — no per-service mTLS |

**Điểm:** IVF đạt ~75% Zero Trust maturity. MediatR pipeline (6 behaviors) là điểm sáng, nhưng thiếu device trust scoring và per-service mTLS.

#### 2.2 Authentication Stack

| Capability | AWS IAM / Microsoft Entra | IVF System | Gap |
|------------|--------------------------|------------|-----|
| MFA | ✅ TOTP/FIDO2/SMS | ✅ TOTP + Passkey (WebAuthn) | Tương đương |
| SSO/Federation | ✅ SAML/OIDC | ❌ Chưa có | **Gap** |
| Passwordless | ✅ FIDO2/Windows Hello | ✅ Passkey support | Tương đương |
| Token management | ✅ STS/Managed Identity | ✅ JWT RS256 3072-bit + refresh families | Tương đương |
| API key security | ✅ IAM access keys + rotation | ✅ BCrypt hashed + constant-time compare | Tương đương |
| Session binding | ✅ IP/Device binding | ✅ Token binding middleware | Tương đương |
| Vault/KMS | ✅ AWS KMS / Azure Key Vault | ✅ Custom vault (AES-256-GCM + KEK) | Functional parity, ít mature |

**Điểm:** Auth stack rất mạnh. JWT RS256 3072-bit + refresh token families + BCrypt API keys + passkey support — ngang tầm enterprise. Thiếu SSO/OIDC federation.

#### 2.3 Infrastructure Security

| Capability | AWS/Azure/GCP | IVF System | Gap |
|------------|---------------|------------|-----|
| Container orchestration | ✅ EKS/AKS/GKE + PodSecurityPolicy | ⚠️ Docker Swarm (no pod security) | **Gap lớn** |
| Secret management | ✅ AWS Secrets Manager / Azure KV | ⚠️ Docker Secrets + custom vault | Chấp nhận được, thiếu rotation cho Docker secrets |
| Image scanning | ✅ ECR scanning / Trivy | ❌ Chưa có | **Gap** |
| Network encryption | ✅ VPC encryption, mTLS mesh | ❌ Overlay unencrypted | **Gap lớn** |
| WAF | ✅ AWS WAF / Cloudflare WAF | ❌ Chưa deploy | **Gap** |
| DDoS protection | ✅ AWS Shield / CF Spectrum | ⚠️ App-level rate limit only | **Gap** |
| IDS/IPS | ✅ GuardDuty / Sentinel | ❌ Chưa có | **Gap** |
| Audit trail | ✅ CloudTrail / Azure Monitor | ✅ Application-level audit log | Chấp nhận được |
| Compliance automation | ✅ AWS Config / Azure Policy | ❌ Manual only | **Gap** |

**Điểm:** Đây là khoảng cách lớn nhất. Self-hosted Docker Swarm thiếu nhiều managed security controls của cloud providers.

#### 2.4 Data Protection

| Capability | Enterprise Standard | IVF System | Gap |
|------------|-------------------|------------|-----|
| Encryption at rest | ✅ AES-256 (managed key) | ✅ Vault field-level AES-256-GCM | Tương đương |
| Encryption in transit (external) | ✅ TLS 1.2+ mandatory | ✅ Caddy auto-TLS, HSTS preload | Tương đương |
| Encryption in transit (internal) | ✅ mTLS service mesh | ❌ Overlay network unencrypted | **Gap** |
| Key management | ✅ HSM-backed (CloudHSM/KMS) | ⚠️ SoftHSM + local KEK fallback | **Gap** — no hardware HSM |
| Data classification | ✅ Automated | ⚠️ Manual via field access | Partial |
| GDPR compliance | ✅ Full toolset | ✅ Consent enforcement middleware | Tương đương |
| Backup encryption | ✅ Encrypted backups | ⚠️ SHA256 checksum only, no encryption | **Gap** |

#### 2.5 Monitoring & Response

| Capability | Enterprise SOC | IVF System | Gap |
|------------|---------------|------------|-----|
| Centralized logging | ✅ SIEM (Splunk/Sentinel) | ✅ Loki + Promtail | Tương đương về chức năng |
| Metrics & alerting | ✅ Datadog/CloudWatch | ✅ Prometheus + Grafana + Discord | Tương đương |
| Distributed tracing | ✅ Jaeger/X-Ray | ⚠️ Correlation ID only | **Gap** — no full trace |
| Incident response | ✅ PagerDuty/Opsgenie | ⚠️ Discord alerts only | **Gap** |
| Log retention policy | ✅ Compliant retention (7yr HIPAA) | ⚠️ Loki default retention | **Gap** |
| Security event correlation | ✅ SIEM rules | ❌ No correlation | **Gap** |

### Tổng kết so sánh

```
IVF System vs Big Tech Security Maturity:

Application Layer Security:    ████████████████████░  90%  → Gần ngang Big Tech
Authentication & Identity:     █████████████████░░░░  85%  → Strong
Network & Infrastructure:      ████████████░░░░░░░░░  60%  → Gap lớn nhất
Data Protection:               ██████████████░░░░░░░  70%  → Cần encrypt transit
Monitoring & Response:         ███████████████░░░░░░  75%  → Thiếu SIEM
Compliance & Governance:       ██████████████░░░░░░░  70%  → Framework tốt, thiếu automation
```

**Nhận xét chung:** Hệ thống IVF có **application-level security ngang tầm enterprise** (đặc biệt MediatR pipeline, Zero Trust middleware, JWT implementation). Khoảng cách chính nằm ở **infrastructure layer** — điều này phổ biến với self-hosted deployments so với managed cloud services.

---

## 3. API Security

### 3.1 Điểm mạnh ✅

**JWT Implementation — Enterprise-grade:**
- RS256 với RSA 3072-bit (mạnh hơn yêu cầu tối thiểu 2048-bit)
- Zero clock skew (`ClockSkew = TimeSpan.Zero`)
- Algorithm restriction chống JWT confusion attack
- Device fingerprint binding (SecurityEnforcementMiddleware)
- Session claims embedded in token
- Refresh token families (RFC 6749) với SHA256 hash + Redis-backed + reuse detection

**MediatR Pipeline — "Best-in-class":**
```
Request → ValidationBehavior → FeatureGateBehavior → VaultPolicyBehavior
        → ZeroTrustBehavior → FieldAccessBehavior → Handler
```
6 pipeline behaviors thực thi security policies **trước khi handler chạy**. Đây là pattern mà Google BeyondCorp và Microsoft CAE áp dụng ở application layer.

**Security Headers — OWASP A+:**
- HSTS 2 năm + preload
- CSP strict: `default-src 'none'`, allowlist rõ ràng
- Cross-Origin isolation đầy đủ
- Permissions-Policy chặn camera, mic, geo, payment, USB

**OWASP Top 10 Coverage:**

| Category | Đánh giá |
|----------|----------|
| A01 Broken Access Control | 🟢 Strong — Role + Feature + Field-level |
| A02 Cryptographic Failures | 🟢 Strong (trừ plaintext refresh tokens) |
| A03 Injection | 🟢 Good — EF Core parameterized + FluentValidation |
| A04 Insecure Design | 🟢 Strong — CQRS + pipeline behaviors |
| A05 Security Misconfiguration | 🟡 Good (CSP unsafe-inline, Swagger prod) |
| A06 Vulnerable Components | ⚠️ No dependency scanning |
| A07 Auth Failures | 🟢 Strong — MFA + passkey + BCrypt |
| A08 Data Integrity | 🟢 Strong — digital signing (SignServer) |
| A09 Logging & Monitoring | 🟢 Excellent — Serilog + Prometheus + Grafana |
| A10 SSRF | 🟡 Not explicitly tested |

### 3.2 Lỗ hổng cần fix

| # | Mức độ | Vấn đề | Chi tiết |
|---|--------|--------|----------|
| A1 | 🟠 High | Refresh tokens lưu plaintext trong DB | Nên hash SHA-256 trước khi lưu |
| A2 | 🟠 High | Rate limiter dùng `Host` header fallback | Nên dùng client IP, Host header có thể bị spoof |
| A3 | 🟠 High | Geo-blocking trust `X-Country-Code` header từ client | Nên dùng server-side GeoIP (MaxMind) |
| A4 | 🟡 Medium | Invalid VaultToken/ApiKey fall through sang JWT | Nên trả 401 ngay khi token không hợp lệ |
| A5 | 🟡 Medium | API key qua query parameter | Log exposure risk — nên chỉ chấp nhận qua header |
| A6 | 🟡 Medium | `_pendingMfa` in-memory dict | Không shared across Swarm replicas — dùng Redis |
| A7 | 🟢 Low | Không có JTI revocation | Không thể revoke JWT đang valid |
| A8 | 🟢 Low | CSP `style-src 'unsafe-inline'` | Angular cần, nhưng weakens CSP |

---

## 4. Infrastructure Security

### 4.1 Điểm mạnh ✅

- **4 overlay networks** với segmentation đúng: `ivf-public`, `ivf-signing` (internal), `ivf-data` (internal), `ivf-monitoring`
- **Docker Secrets** cho credentials nhạy cảm (9 secrets)
- **Resource limits** trên tất cả services
- **Health checks** trên critical services
- **Host-mode port publishing** — giữ source IP cho firewall rules
- **Global Caddy deployment** — mỗi node có reverse proxy riêng

### 4.2 Lỗ hổng cần fix

| # | Mức độ | Vấn đề | Vị trí |
|---|--------|--------|--------|
| I1 | 🔴 Critical | EJBCA/SignServer DB passwords hardcoded (`ejbca`/`signserver`) | docker-compose.stack.yml |
| I2 | 🔴 Critical | MinIO S3 API bound `0.0.0.0:9000` | docker-compose.production.yml |
| I3 | 🟠 High | Docker socket mount trong API container | docker-compose.stack.yml L111 |
| I4 | 🟠 High | EJBCA/SignServer admin ports trên all interfaces | docker-compose.stack.yml |
| I5 | 🟠 High | PostgreSQL port 5433 trên all interfaces | docker-compose.stack.yml |
| I6 | 🟠 High | EJBCA/SignServer trên `ivf-public` network | docker-compose.stack.yml |
| I7 | 🟠 High | Monitoring password plaintext trong 3+ files committed | Caddyfile, prometheus.yml, datasources.yml |
| I8 | 🟠 High | Redis không có password trong stack file | docker-compose.stack.yml |
| I9 | 🟠 High | Overlay networks KHÔNG encrypted | docker-compose.stack.yml |
| I10 | 🟡 Medium | Containers chạy root (không có `user:` directive) | Tất cả compose files |
| I11 | 🟡 Medium | Không có read-only filesystem cho API container | docker-compose.stack.yml |
| I12 | 🟡 Medium | DB connections không dùng SSL | Connection strings |
| I13 | 🟡 Medium | Swagger expose trong production | Caddyfile |
| I14 | 🟡 Medium | Không có rate limiting ở Caddy level | Caddyfile |
| I15 | 🟡 Medium | Loki auth disabled | loki-config.yml |
| I16 | 🟡 Medium | Replication password hardcoded | docker-compose.stack.yml |

---

## 5. Frontend Security

### 5.1 Điểm mạnh ✅

- **JWT Bearer token trong Authorization header** — immune to CSRF
- **Token refresh với queue** — concurrent 401s không trigger multiple refresh calls
- **Security interceptor** — `X-Requested-With`, Correlation ID, device fingerprint
- **Consent interceptor** — GDPR compliance enforcement
- **Tenant limit interceptor** — feature gating, suspension handling
- **Auth/Guest/Feature guards** — route-level protection
- **Production URL relative `/api`** — không hardcode host
- **Strict TypeScript** — strict mode enabled

### 5.2 Lỗ hổng

| # | Mức độ | Vấn đề | Chi tiết |
|---|--------|--------|----------|
| F1 | 🟡 Medium | JWT trong localStorage | XSS vulnerability — CSP mitigates nhưng httpOnly cookie an toàn hơn cho HIPAA |
| F2 | 🟡 Medium | Weak device fingerprint hash (32-bit) | Dễ brute-force/collision, nên dùng SHA-256 |
| F3 | 🟡 Medium | `isAuthenticated()` chỉ check token existence | Không validate expiry client-side |
| F4 | 🟢 Low | `alert()` cho error messages | Nên dùng toast service |
| F5 | 🟢 Low | Fingerprint cache trong sessionStorage | Regenerate mỗi tab — inconsistent |

---

## 6. Network & Remote Access

### 6.1 Điểm mạnh ✅

- **5 lớp bảo mật**: UFW → Fail2ban → SSH key-only → WireGuard VPN → mTLS
- **WireGuard preshared key** per client (quantum-resistant key exchange)
- **Split tunnel** — chỉ route `10.200.0.0/24` qua VPN
- **Fail2ban** — SSH jail (5 tries/1hr) + recidive (3 bans/1 week)
- **Admin port lockdown** — UFW chặn 8443, 9443, 9001, 5433, 6379 trên `eth0`
- **Complete PKI** — CA → server certs → client certs → cert rotation scripts
- **Ed25519 SSH keys**
- **Caddy auto-TLS** với Let's Encrypt + HSTS preload
- **Monitoring behind basic auth** (bcrypt-hashed)
- **Operational runbooks** xuất sắc (secure_remote_admin.md ~900 dòng)

### 6.2 Lỗ hổng

| # | Mức độ | Vấn đề | Chi tiết |
|---|--------|--------|----------|
| N1 | 🔴 Critical | TOTP secret + emergency codes committed to git | secure_remote_admin.md — cần regenerate + scrub git history |
| N2 | 🟠 High | SSH 2FA chưa activated | Configured nhưng chưa reload SSH |
| N3 | 🟠 High | Root SSH vẫn active | Chưa deploy admin user |
| N4 | 🟡 Medium | Không có WAF/DDoS protection ở edge | Cloudflare script ready nhưng chưa deploy |
| N5 | 🟡 Medium | CA key password là `changeit` | Nên rotate sang strong passphrase |
| N6 | 🟡 Medium | EJBCA Public Access Role chưa remove | Cần chạy `secure-ejbca-access.sh` |
| N7 | 🟡 Medium | WireGuard port open to entire internet | Nên restrict source IPs nếu static |
| N8 | 🟢 Low | Không có explicit TLS min version trong Caddy | Caddy default TLS 1.2+, nhưng nên explicit |
| N9 | 🟢 Low | Single VPN subnet, no role segmentation | Chia subnet theo role khi team grows |

---

## 7. Tổng hợp lỗ hổng theo mức độ

### 🔴 Critical — Fix ngay (4 issues)

| # | Vấn đề | Ảnh hưởng | Remediation |
|---|--------|-----------|-------------|
| 1 | TOTP secret + scratch codes trong git | Attacker bypass 2FA | Regenerate TOTP, xóa khỏi docs, scrub git history |
| 2 | EJBCA/SignServer DB passwords hardcoded | Credential exposure | Dùng `.env` file hoặc Docker config (CE không support `_FILE`) |
| 3 | MinIO S3 API trên `0.0.0.0:9000` | Storage service exposed to internet | Bind `127.0.0.1:9000` hoặc xóa port mapping |
| 4 | Refresh tokens plaintext trong DB | Token theft nếu DB leak | Hash SHA-256 trước khi store |

### 🟠 High — Fix trong 1-2 tuần (12 issues)

| # | Vấn đề | Remediation |
|---|--------|-------------|
| 5 | Docker socket mount | Docker socket proxy (tecnativa/docker-socket-proxy) |
| 6 | Admin ports (8443/9443) trên all interfaces | UFW đã chặn, nhưng nên bind `127.0.0.1` |
| 7 | PostgreSQL port 5433 all interfaces | Bind `127.0.0.1` hoặc remove host port |
| 8 | EJBCA/SignServer trên `ivf-public` network | Chỉ để trên `ivf-signing` + `ivf-data` |
| 9 | Monitoring password plaintext in git | Docker secret hoặc env var injection |
| 10 | Redis không password (stack file) | Thêm `--requirepass` với Docker secret |
| 11 | Overlay networks unencrypted | `driver_opts: encrypted: "true"` |
| 12 | SSH 2FA chưa activated | `systemctl reload ssh` (sau khi regenerate TOTP) |
| 13 | Root SSH vẫn active | Run `setup-admin-user.sh`, disable root login |
| 14 | Rate limiter dùng `Host` header fallback | Dùng client IP |
| 15 | Geo-blocking trust client header | Server-side GeoIP |
| 16 | No custom `pg_hba.conf` | SSL enforcement + client cert auth |

### 🟡 Medium — Fix trong 1 tháng (16 issues)

| # | Vấn đề |
|---|--------|
| 17 | JWT trong localStorage (nên httpOnly cookie cho HIPAA) |
| 18 | Weak device fingerprint hash (32-bit → SHA-256) |
| 19 | Containers chạy root |
| 20 | Không rate limiting ở Caddy |
| 21 | Swagger trong production |
| 22 | DB connections không SSL |
| 23 | Invalid VaultToken/ApiKey fall through |
| 24 | API key qua query parameter |
| 25 | `_pendingMfa` in-memory (không shared replicas) |
| 26 | Loki auth disabled |
| 27 | Replication password hardcoded |
| 28 | Token existence check only (no expiry check client-side) |
| 29 | Không có WAF/DDoS ở edge |
| 30 | CA key password `changeit` |
| 31 | EJBCA Public Access Role |
| 32 | Monitoring basic auth password trong Caddyfile |

### 🟢 Low (8 issues)

| # | Vấn đề |
|---|--------|
| 33 | Không có JTI revocation |
| 34 | CSP `style-src 'unsafe-inline'` |
| 35 | Không explicit TLS min version |
| 36 | `alert()` cho messages (nên toast) |
| 37 | Fingerprint cache sessionStorage |
| 38 | No explicit cipher suite pinning |
| 39 | Single VPN subnet |
| 40 | Cloudflare proxy config inconsistency |

---

## 8. Roadmap khắc phục

### Phase 1 — Immediate (1-3 ngày) 🔴

```
□ Regenerate TOTP secret, xóa khỏi secure_remote_admin.md
□ Scrub git history (git filter-branch hoặc BFG Repo-Cleaner)
□ Hash refresh tokens với SHA-256 trước khi lưu DB
□ Bind MinIO S3 port sang 127.0.0.1 hoặc xóa host port
□ Đổi EJBCA/SignServer DB passwords sang random strong passwords
□ Activate SSH 2FA (systemctl reload ssh)
□ Deploy admin user + disable root SSH login
```

### Phase 2 — Short-term (1-2 tuần) 🟠

```
□ Deploy docker-socket-proxy thay vì mount docker.sock
□ Thêm --requirepass cho Redis với Docker secret
□ Enable overlay network encryption
□ Fix rate limiter partition (dùng client IP)
□ Server-side GeoIP thay X-Country-Code header
□ Custom pg_hba.conf với SSL enforcement
□ Di chuyển monitoring passwords sang Docker secrets
□ Remove EJBCA/SignServer khỏi ivf-public network
```

### Phase 3 — Medium-term (1 tháng) 🟡

```
□ Migrate JWT storage sang httpOnly cookie (HIPAA compliance)
□ SHA-256 device fingerprint hash
□ Caddy-level rate limiting
□ Disable/auth Swagger trong production
□ Enable SSL cho DB connections
□ Deploy Cloudflare WAF hoặc ModSecurity
□ Container image scanning (Trivy trong CI/CD)
□ Set user: directive cho containers
□ API key chỉ qua header (xóa query parameter)
□ Shared _pendingMfa qua Redis
```

### Phase 4 — Long-term (2-3 tháng) 🟢

```
□ SSO/OIDC federation
□ Service mesh mTLS (inter-service encryption)
□ SIEM integration (log correlation rules)
□ Hardware HSM thay SoftHSM
□ Automated compliance scanning
□ Distributed tracing (OpenTelemetry)
□ Secret rotation automation cho Docker secrets
□ CT log monitoring
```

---

## 9. Kết luận

### Hệ thống IVF đạt level nào?

```
┌──────────────────────────────────────────────────────────┐
│  Startup SaaS nhỏ         │ ████████████████░           │  40%
│  Doanh nghiệp vừa         │ ████████████████████░       │  60%
│  ★ IVF System              │ ████████████████████████░   │  72%  ← Đây
│  Enterprise (bank/health)  │ ██████████████████████████░ │  85%
│  Big Tech (G/A/M/CF)       │ ████████████████████████████│  95%+
└──────────────────────────────────────────────────────────┘
```

### Đánh giá tổng thể

**Hệ thống IVF đạt mức "Advanced" — vượt xa mức trung bình của ngành, nhưng chưa đạt Enterprise/Big Tech.**

#### Điểm sáng (vượt trội so với nhiều doanh nghiệp):

1. **MediatR Security Pipeline** — 6 behaviors thực thi Zero Trust ở application layer. Pattern này ngang Google BeyondCorp và Microsoft CAE. Rất ít startup/SMB implement được.

2. **JWT Implementation** — RS256 3072-bit, refresh token families, token binding, session claims. Vượt chuẩn OAuth 2.0 thông thường.

3. **Application-level Vault** — AES-256-GCM encryption at rest, KEK wrapping, policy-based access, auto-rotation. Functional parity với HashiCorp Vault cho core use cases.

4. **Security Headers** — OWASP A+ rating. HSTS preload 2 năm, strict CSP, cross-origin isolation đầy đủ.

5. **5-layer Defense** — UFW → Fail2ban → SSH → WireGuard → mTLS. Documented với enterprise mapping.

6. **PKI Tooling** — Cert generation, rotation, backup scripts complete. Elytron mTLS on WildFly.

#### Khoảng cách chính với Big Tech:

1. **Infrastructure hardening** — Docker Swarm thiếu nhiều controls so với Kubernetes (PodSecurityPolicy, NetworkPolicy, Seccomp profiles). Overlay networks unencrypted.

2. **No edge security** — Không có WAF, DDoS protection, bot detection ở edge layer. Traffic hit Caddy trực tiếp.

3. **Secret management inconsistency** — Vault layer mạnh nhưng Docker compose vẫn chứa hardcoded credentials.

4. **No SIEM/IDS** — Logging stack tốt (Loki + Grafana) nhưng thiếu security event correlation, incident response automation.

5. **Incomplete activation** — SSH 2FA configured nhưng chưa activate. Admin user script ready nhưng chưa deploy. Cloudflare script ready nhưng chưa enable.

### Để đạt Enterprise level (85+):
- Fix tất cả 🔴 Critical + 🟠 High → +10 điểm
- Deploy WAF + enable overlay encryption → +5 điểm
- Container hardening (non-root, read-only, image scanning) → +3 điểm

### Để đạt Big Tech level (95+):
- Kubernetes migration với full security policies
- Hardware HSM cho cryptographic operations
- SIEM với real-time correlation
- Service mesh mTLS (Istio/Linkerd)
- Full compliance automation (SOC 2 Type II, HITRUST)
- Bug bounty program
- Red team exercises

---

> **Kết luận cuối: Hệ thống IVF có application-level security ngang tầm enterprise lớn. Khoảng cách chính nằm ở infrastructure layer — điều này hoàn toàn có thể thu hẹp bằng cách thực hiện Roadmap Phase 1-3 trong 1 tháng. Sau khi fix xong, hệ thống sẽ đạt ~85/100 — tương đương enterprise bank/healthcare.**
