# Zero Trust Architecture — IVF Information System

## Overview

This document describes the Zero Trust security architecture implemented in the IVF Information System, benchmarked against the highest standards from **Google BeyondCorp**, **Microsoft Zero Trust**, and **AWS Zero Trust**.

## Zero Trust Maturity Model

Our implementation follows the **CISA Zero Trust Maturity Model** at the **Advanced/Optimal** tier across all five pillars:

| Pillar           | Level    | Implementation                                                                |
| ---------------- | -------- | ----------------------------------------------------------------------------- |
| **Identity**     | Advanced | Multi-layer auth (JWT + VaultToken + APIKey), MFA, biometric, session binding |
| **Devices**      | Advanced | Device fingerprinting, trust scoring, registration, drift detection           |
| **Networks**     | Advanced | mTLS, strict CORS, rate limiting, IP intelligence, geo-fencing                |
| **Applications** | Optimal  | Micro-segmented endpoints, RBAC + ABAC, per-request evaluation                |
| **Data**         | Advanced | AES-256-GCM field-level encryption, envelope encryption, data masking         |

---

## Architecture Comparison

### Google BeyondCorp

| BeyondCorp Principle | IVF Implementation                                                              |
| -------------------- | ------------------------------------------------------------------------------- |
| Network is untrusted | Every request verified by `ZeroTrustMiddleware` regardless of origin            |
| Device inventory     | `DeviceFingerprintService` fingerprints every device, `DeviceRisk` tracks trust |
| Context-aware access | `ThreatDetectionService` evaluates 7+ signals per request                       |
| Single sign-on       | JWT with token binding to device fingerprint and session                        |
| Access proxy         | Middleware pipeline: VaultToken → APIKey → JWT → ZeroTrust                      |

### Microsoft Zero Trust

| Microsoft Principle          | IVF Implementation                                                                 |
| ---------------------------- | ---------------------------------------------------------------------------------- |
| Verify explicitly            | Per-request `ThreatAssessment` with risk scoring (0-100)                           |
| Least privilege              | RBAC (9 roles) + ABAC (VaultPolicies) + field-level access control                 |
| Assume breach                | `SecurityEventService` logs all events, `ThreatDetectionService` detects anomalies |
| Conditional Access           | `AdaptiveSessionService` with context drift detection (IP, device, country)        |
| Continuous Access Evaluation | `ZeroTrustMiddleware` re-evaluates every authenticated request                     |
| Microsoft Sentinel analytics | `SecurityEvent` entity with MITRE ATT&CK categorization                            |

### AWS Zero Trust

| AWS Principle              | IVF Implementation                                                                    |
| -------------------------- | ------------------------------------------------------------------------------------- |
| GuardDuty threat detection | `ThreatDetectionService` with impossible travel, brute force, anomaly detection       |
| CloudTrail audit           | `SecurityEvent` table with full audit trail, correlated by `CorrelationId`            |
| Shield DDoS protection     | Multi-tier rate limiting: global (100/min), auth (10/min), sensitive (30/min)         |
| WAF input validation       | `InputValidationResult` detects SQL injection, XSS, path traversal, command injection |
| IAM granular policies      | VaultPolicy with glob-pattern path matching and capability-based access               |

---

## Security Layers

### Layer 1: Transport Security

- **HSTS**: 2-year max-age with preload
- **mTLS**: Mutual TLS for SignServer/EJBCA communication
- **TLS 1.3**: Enforced in production
- **Certificate pinning**: Custom CA chain validation

### Layer 2: Request Verification

- **CORS**: Strict origin whitelist in production
- **CSP**: `default-src 'none'` with explicit allowlists
- **Security Headers**: 20+ OWASP headers including Cross-Origin-\*, Permissions-Policy, Trusted Types
- **Input Validation**: WAF-level regex detection for SQL injection, XSS, path traversal, command injection
- **CSRF Protection**: `X-Requested-With: XMLHttpRequest` required

### Layer 3: Authentication

- **JWT Bearer**: HS256, 60-minute expiry, zero clock skew
- **Token Binding**: JWT contains `device_fingerprint` and `session_id` claims
- **Vault Tokens**: SHA-256 hashed, policy-based, usage-limited
- **API Keys**: BCrypt hashed, prefix-based lookup, expiry-tracked
- **Biometric**: DigitalPersona fingerprint matching (Windows)
- **Refresh Tokens**: Cryptographically random, 7-day expiry, single-use

### Layer 4: Authorization

- **RBAC**: 9 role-based policies (Admin, Doctor, Nurse, LabTech, Embryologist, etc.)
- **ABAC**: VaultPolicy with glob-pattern path matching (`secrets/**`)
- **Field-Level**: `FieldAccessPolicy` with masking/redaction per role
- **Vault Policy Evaluation**: Path-based capability checking (read, create, update, delete, list, sudo)

### Layer 5: Continuous Verification (Zero Trust Middleware)

- **Threat Assessment**: Every authenticated request scores 0-100 risk
- **7 Signal Categories**: IP intelligence, User-Agent analysis, impossible travel, brute force, anomalous access, input validation, time-based anomaly
- **Risk Levels**: Low (0-24), Medium (25-49), High (50-69), Critical (70-100)
- **Actions**: Allow → AllowWithMonitoring → RequireStepUp → RequireMFA → BlockTemporary → BlockPermanent
- **Session Drift Detection**: IP change (+30), device change (+50), country change (+60)

### Layer 6: Session Management

- **Adaptive Sessions**: Bound to IP, device fingerprint, and country
- **Concurrent Session Limit**: Maximum 3 active sessions per user
- **Session Drift**: Blocked at score ≥50 (prevents session hijacking)
- **Automatic Revocation**: Oldest session revoked when limit exceeded

### Layer 7: Threat Detection

- **Impossible Travel**: Login from different countries within 30 minutes
- **Brute Force**: 5 failed attempts in 15-minute window
- **Behavioral Baseline**: 30-day access pattern analysis per user
- **Bot/Scanner Detection**: UA pattern matching for known tools
- **IP Reputation**: Tor exit, VPN, proxy, hosting provider, known attacker detection

### Layer 8: Audit & Monitoring

- **Security Events**: All events logged to `security_events` table with MITRE ATT&CK categorization
- **Audit Logs**: Immutable EF Core change tracking with before/after snapshots
- **Vault Audit**: Separate `vault_audit_logs` for key management operations
- **Certificate Audit**: `certificate_audit_events` for PKI operations
- **Real-time Dashboard**: `/api/security/dashboard` with 24-hour metrics

---

## Security Event Types

### Authentication Events

| Event                | Severity | Description                           |
| -------------------- | -------- | ------------------------------------- |
| `AUTH_LOGIN_SUCCESS` | Info     | Successful login with risk assessment |
| `AUTH_LOGIN_FAILED`  | Medium   | Failed login attempt                  |
| `AUTH_BRUTE_FORCE`   | High     | Brute force threshold exceeded        |
| `AUTH_TOKEN_REFRESH` | Info     | Token refresh operation               |
| `AUTH_TOKEN_REVOKED` | Medium   | Token revoked (admin or security)     |

### Zero Trust Events

| Event                        | Severity    | Description                                |
| ---------------------------- | ----------- | ------------------------------------------ |
| `ZT_ACCESS_DENIED`           | Critical    | Request blocked by Zero Trust policy       |
| `ZT_RISK_ELEVATED`           | Medium      | Elevated risk on sensitive path            |
| `ZT_CONTINUOUS_VERIFICATION` | Medium/High | Risk detected during continuous evaluation |
| `ZT_BREAK_GLASS`             | Critical    | Emergency override activated               |

### Threat Detection Events

| Event                      | Severity | Description                                   |
| -------------------------- | -------- | --------------------------------------------- |
| `THREAT_IMPOSSIBLE_TRAVEL` | High     | Login from geographically impossible location |
| `THREAT_ANOMALOUS_ACCESS`  | Medium   | Unusual access pattern                        |
| `THREAT_SQL_INJECTION`     | High     | SQL injection attempt                         |
| `THREAT_XSS_ATTEMPT`       | High     | Cross-site scripting attempt                  |

### Session Events

| Event                    | Severity | Description                       |
| ------------------------ | -------- | --------------------------------- |
| `SESSION_CREATED`        | Info     | New session with bound context    |
| `SESSION_HIJACK_ATTEMPT` | Critical | Session context drift detected    |
| `SESSION_CONCURRENT`     | Medium   | Concurrent session limit exceeded |

---

## Rate Limiting Matrix

| Endpoint Category | Limit   | Window | Queue | Purpose                   |
| ----------------- | ------- | ------ | ----- | ------------------------- |
| Global            | 100 req | 1 min  | 5     | DDoS prevention           |
| Authentication    | 10 req  | 1 min  | 0     | Brute force prevention    |
| Signing           | 30 req  | 1 min  | 2     | SignServer protection     |
| Cert Provisioning | 3 req   | 1 min  | 0     | Expensive operation       |
| Sensitive Admin   | 30 req  | 1 min  | 2     | Admin endpoint protection |

---

## Security Headers Matrix

| Header                              | Value                                          | Standard          |
| ----------------------------------- | ---------------------------------------------- | ----------------- |
| `Content-Security-Policy`           | `default-src 'none'; script-src 'self'; ...`   | OWASP             |
| `Strict-Transport-Security`         | `max-age=63072000; includeSubDomains; preload` | HSTS Preload      |
| `X-Content-Type-Options`            | `nosniff`                                      | OWASP             |
| `X-Frame-Options`                   | `DENY`                                         | OWASP             |
| `Referrer-Policy`                   | `no-referrer`                                  | W3C               |
| `Permissions-Policy`                | All features disabled                          | W3C               |
| `Cross-Origin-Embedder-Policy`      | `require-corp`                                 | COEP              |
| `Cross-Origin-Opener-Policy`        | `same-origin`                                  | COOP              |
| `Cross-Origin-Resource-Policy`      | `same-origin`                                  | CORP              |
| `X-Permitted-Cross-Domain-Policies` | `none`                                         | Adobe             |
| `X-Download-Options`                | `noopen`                                       | IE/Edge           |
| `X-DNS-Prefetch-Control`            | `off`                                          | Privacy           |
| `Cache-Control`                     | `no-store, no-cache, must-revalidate, private` | OWASP             |
| `require-trusted-types-for`         | `'script'`                                     | W3C Trusted Types |

---

## Compliance Framework Mapping

| Standard                         | Coverage | Key Controls                                              |
| -------------------------------- | -------- | --------------------------------------------------------- |
| **NIST SP 800-207** (Zero Trust) | Full     | All 7 tenets implemented                                  |
| **OWASP Top 10**                 | Full     | Injection, broken auth, XSS, CSRF, security headers       |
| **HIPAA** (Healthcare)           | Full     | Audit logging, encryption, access control, PHI protection |
| **SOC 2 Type II**                | Majority | Security events, access reviews, encryption, monitoring   |
| **ISO 27001**                    | Majority | Risk assessment, access control, asset management         |
| **CISA Zero Trust**              | Advanced | 5-pillar maturity at Advanced/Optimal level               |

---

## API Endpoints

### Security Monitoring (`/api/security`) — AdminOnly

| Method | Path                                   | Description                   |
| ------ | -------------------------------------- | ----------------------------- |
| GET    | `/events/recent?count=50`              | Recent security events        |
| GET    | `/events/user/{userId}?hours=24`       | Events for a specific user    |
| GET    | `/events/ip/{ipAddress}?hours=24`      | Events for a specific IP      |
| GET    | `/events/high-severity?hours=24`       | High/Critical severity events |
| POST   | `/assess`                              | Manual threat assessment      |
| GET    | `/sessions/{userId}`                   | Active sessions for a user    |
| DELETE | `/sessions/{sessionId}`                | Revoke a session              |
| GET    | `/ip-intelligence/{ipAddress}`         | IP reputation check           |
| GET    | `/device-trust/{userId}/{fingerprint}` | Device trust status           |
| GET    | `/dashboard`                           | 24-hour security summary      |

### Zero Trust Policies (`/api/zerotrust`) — AdminOnly

| Method | Path        | Description                    |
| ------ | ----------- | ------------------------------ |
| GET    | `/policies` | Get all Zero Trust policies    |
| PUT    | `/policies` | Update a policy                |
| POST   | `/check`    | Check access against ZT policy |

---

## Frontend Integration

### Security Interceptor

The Angular frontend includes a `securityInterceptor` that adds Zero Trust context to every HTTP request:

- `X-Device-Fingerprint`: Browser-generated device identity hash
- `X-Session-Id`: Current session identifier (from sessionStorage)
- `X-Correlation-Id`: Unique request tracing ID
- `X-Requested-With: XMLHttpRequest`: CSRF protection

### Security Service (`core/services/security.service.ts`)

Centralized HTTP client wrapping all `/api/security/*` endpoints:

| Method                         | Endpoint                           | Description                      |
| ------------------------------ | ---------------------------------- | -------------------------------- |
| `getDashboard()`               | GET `/dashboard`                   | 24-hour security metrics summary |
| `getRecentEvents(count)`       | GET `/events/recent?count=`        | Recent security events           |
| `getUserEvents(userId, hours)` | GET `/events/user/{userId}?hours=` | Events for a specific user       |
| `getIpEvents(ip, hours)`       | GET `/events/ip/{ip}?hours=`       | Events from a specific IP        |
| `getHighSeverityEvents(hours)` | GET `/events/high-severity?hours=` | High/Critical severity events    |
| `assessThreat(request)`        | POST `/assess`                     | Manual threat assessment         |
| `getActiveSessions(userId)`    | GET `/sessions/{userId}`           | Active sessions for a user       |
| `revokeSession(sessionId)`     | DELETE `/sessions/{sessionId}`     | Revoke a session                 |
| `checkIpIntelligence(ip)`      | GET `/ip-intelligence/{ip}`        | IP reputation check              |
| `checkDeviceTrust(userId, fp)` | GET `/device-trust/{userId}/{fp}`  | Device trust status              |

### TypeScript Models (`core/models/security.model.ts`)

| Interface           | Description                                                                                                                                                                                                                             |
| ------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SecurityEvent`     | Full event record (18 fields): eventType, severity, userId, ipAddress, userAgent, deviceFingerprint, country/city, requestPath/method/statusCode, riskScore, isBlocked, correlationId, sessionId, details, threatIndicators, timestamps |
| `SecurityDashboard` | Aggregate metrics: totalHighSeverity, blockedRequests, uniqueIps, uniqueUsers, threatsByType[], recentEvents[]                                                                                                                          |
| `ThreatAssessment`  | Risk result: riskScore (0-100), riskLevel, recommendedAction, signals[]                                                                                                                                                                 |
| `ThreatSignal`      | Individual signal: category, description, score, details                                                                                                                                                                                |
| `IpIntelligence`    | IP flags: isTor, isVpn, isProxy, isHosting, isKnownAttacker, country/city, riskScore                                                                                                                                                    |
| `DeviceTrust`       | Device profile: fingerprint, trustLevel (FullyManaged/Registered/Known/Unknown), isRegistered, lastSeenAt, riskScore                                                                                                                    |
| `SessionInfo`       | Session: sessionId, userId, ipAddress, deviceFingerprint, country/city, createdAt, lastActivityAt, isActive                                                                                                                             |
| `AssessRequest`     | Input: ipAddress (required), username?, userAgent?, country?, requestPath?                                                                                                                                                              |

### Admin UI Components

Four standalone Angular components provide full admin visibility into the Zero Trust system. All use signals for reactive state, Tailwind CSS + component-scoped SCSS, and Vietnamese (vi-VN) labels.

#### 🛡️ Security Monitor (`features/admin/security-monitor/`)

Real-time Zero Trust dashboard with:

- **Stats grid**: Total high-severity events, blocked requests, unique IPs, unique users (last 24h)
- **Threats by type**: Visual bar chart of threat categories with proportional bars
- **Quick actions**: Navigation cards to Security Events, Active Sessions, and Threat Assessment
- **Recent events table**: Last 10 events with severity badges, event type icons, IP, user, risk score, and blocked status
- **Auto-refresh**: Toggleable 30-second auto-refresh with countdown indicator
- Route: `/admin/security`

#### 📋 Security Events (`features/admin/security-events/`)

Filterable security event viewer with:

- **View toggle**: Switch between "All Events" (recent 100) and "High Severity" (last 48h)
- **5 filter controls**: Severity dropdown, event type category (AUTH/ZT/THREAT/SESSION/DEVICE/DATA/API), IP address, username, blocked status
- **Data table**: 9 columns — time, type, severity, IP, user, request path, risk score, blocked, actions
- **Detail modal**: Full event information with grid layout, parsed JSON views for `details` and `threatIndicators` fields
- Route: `/admin/security-events`

#### 🖥️ Security Sessions (`features/admin/security-sessions/`)

Active session manager with:

- **User lookup**: Search sessions by User ID (GUID)
- **Collapsible session groups**: Expandable cards per user showing all active sessions
- **Session details**: Session ID, IP address, country, created time, last activity, status, device fingerprint
- **Revoke actions**: Revoke individual session or all sessions for a user (with confirmation)
- **Status toasts**: Success/error notifications for revoke operations
- Route: `/admin/security-sessions`

#### 🔍 Security Threats (`features/admin/security-threats/`)

Threat assessment and intelligence tool with three tabs:

- **⚡ Threat Assessment**: Input form (IP, username, user-agent, country, request path) → risk score visualization with bar gauge (0-100), risk level badge, recommended action, and detected signals list with per-signal scores
- **🌐 IP Intelligence**: IP address lookup → flag grid showing Tor/VPN/Proxy/Hosting/Known Attacker status with color-coded indicators, geo-location, and risk score badge
- **📱 Device Trust**: User ID + fingerprint lookup → trust level badge (FullyManaged/Registered/Known/Unknown), registration status, last seen time, risk score
- Route: `/admin/security-threats`

### Navigation

Four menu items added to the admin section of the sidebar (fallback menu, `adminOnly: true`):

| Icon | Label               | Route                      |
| ---- | ------------------- | -------------------------- |
| 🛡️   | Zero Trust          | `/admin/security`          |
| 📋   | Sự kiện bảo mật     | `/admin/security-events`   |
| 🖥️   | Phiên hoạt động     | `/admin/security-sessions` |
| 🔍   | Đánh giá mối đe dọa | `/admin/security-threats`  |

### Route Configuration

All routes are lazy-loaded under the authenticated `MainLayoutComponent` with `authGuard`:

```typescript
{ path: 'admin/security',          loadComponent: () => SecurityMonitorComponent }
{ path: 'admin/security-events',   loadComponent: () => SecurityEventsComponent }
{ path: 'admin/security-sessions', loadComponent: () => SecuritySessionsComponent }
{ path: 'admin/security-threats',  loadComponent: () => SecurityThreatsComponent }
```

---

## Threat Response Workflow

```
Request → Security Headers → Authentication → Authorization
    ↓
Zero Trust Middleware
    ↓
┌─────────────────────────────┐
│ Build Security Context      │
│ (IP, UA, device, session)   │
├─────────────────────────────┤
│ Threat Assessment           │
│ • IP Intelligence           │
│ • User-Agent Analysis       │
│ • Impossible Travel         │
│ • Brute Force Detection     │
│ • Anomalous Access Pattern  │
│ • Input Validation (WAF)    │
│ • Time-based Anomaly        │
├─────────────────────────────┤
│ Risk Scoring (0-100)        │
│ Low → Medium → High → Crit │
├─────────────────────────────┤
│ Decision                    │
│ Critical → BLOCK (403)      │
│ High → Require MFA          │
│ Medium → Monitor + Log      │
│ Low → Allow                 │
├─────────────────────────────┤
│ Session Drift Detection     │
│ IP + Device + Country       │
│ Drift ≥ 50 → BLOCK (401)   │
├─────────────────────────────┤
│ Security Event Logging      │
│ → PostgreSQL security_events│
│ → Structured logging (SIEM) │
└─────────────────────────────┘
    ↓
Application Logic
```

---

## Configuration

### Production CORS (`appsettings.Production.json`)

```json
{
  "Cors": {
    "AllowedOrigins": ["https://ivf.clinic", "https://admin.ivf.clinic"]
  }
}
```

### Zero Trust Policies (Database — seeded by `ZTPolicySeeder`)

Policies are managed via the `/api/zerotrust/policies` API and stored in the `zt_policies` table.

---

## Files Created/Modified

### Backend — New Files

| File                                                                      | Purpose                                                |
| ------------------------------------------------------------------------- | ------------------------------------------------------ |
| `Domain/Entities/SecurityEvent.cs`                                        | Security event entity + event type constants           |
| `Domain/Enums/SecurityEnums.cs`                                           | Security severity, threat category, device trust enums |
| `Application/Common/Interfaces/ISecurityServices.cs`                      | Interfaces for all ZT services                         |
| `Infrastructure/Services/ThreatDetectionService.cs`                       | Threat detection engine (7 signal types)               |
| `Infrastructure/Services/SecurityEventService.cs`                         | Security event logging service                         |
| `Infrastructure/Services/DeviceFingerprintService.cs`                     | Device fingerprinting and trust                        |
| `Infrastructure/Services/AdaptiveSessionService.cs`                       | Session binding and drift detection                    |
| `Infrastructure/Persistence/Configurations/SecurityEventConfiguration.cs` | EF Core config                                         |
| `Infrastructure/Persistence/DesignTimeDbContextFactory.cs`                | Migration design-time factory                          |
| `API/Middleware/ZeroTrustMiddleware.cs`                                   | Core ZT continuous verification middleware             |
| `API/Endpoints/SecurityEventEndpoints.cs`                                 | Security monitoring REST API                           |

### Backend — Modified Files

| File                                         | Change                                                                               |
| -------------------------------------------- | ------------------------------------------------------------------------------------ |
| `API/Program.cs`                             | Added ZT services, middleware, hardened CORS/headers/CSP, `AddHttpContextAccessor()` |
| `API/Endpoints/AuthEndpoints.cs`             | Added threat detection, device fingerprint, session binding to login                 |
| `Infrastructure/Persistence/IvfDbContext.cs` | Added `SecurityEvents` DbSet                                                         |

### Frontend — New Files

| File                                                | Purpose                                                                             |
| --------------------------------------------------- | ----------------------------------------------------------------------------------- |
| `core/interceptors/security.interceptor.ts`         | HTTP interceptor adding ZT headers (device fingerprint, session ID, correlation ID) |
| `core/models/security.model.ts`                     | TypeScript interfaces matching backend DTOs (9 interfaces)                          |
| `core/services/security.service.ts`                 | HTTP service wrapping all `/api/security/*` endpoints (10 methods)                  |
| `features/admin/security-monitor/*.{ts,html,scss}`  | 🛡️ Zero Trust dashboard — stats, threat chart, recent events, auto-refresh          |
| `features/admin/security-events/*.{ts,html,scss}`   | 📋 Filterable event viewer — view modes, 5 filters, detail modal                    |
| `features/admin/security-sessions/*.{ts,html,scss}` | 🖥️ Session manager — user lookup, collapsible groups, revoke actions                |
| `features/admin/security-threats/*.{ts,html,scss}`  | 🔍 Threat assessment — 3-tab interface (assess, IP intel, device trust)             |

### Frontend — Modified Files

| File                                          | Change                                                                                                                      |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `app.config.ts`                               | Registered `securityInterceptor` in HTTP interceptor chain                                                                  |
| `core/models/api.models.ts`                   | Added barrel export for `security.model`                                                                                    |
| `app.routes.ts`                               | Added 4 lazy-loaded routes (`admin/security`, `admin/security-events`, `admin/security-sessions`, `admin/security-threats`) |
| `layout/main-layout/main-layout.component.ts` | Added 4 admin menu items (Zero Trust, Sự kiện bảo mật, Phiên hoạt động, Đánh giá mối đe dọa)                                |
