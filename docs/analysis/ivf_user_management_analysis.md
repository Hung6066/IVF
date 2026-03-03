# IVF User Management System - Comprehensive Analysis

## BACKEND DOMAIN ENTITIES

### User Entity
- **File**: [src/IVF.Domain/Entities/User.cs](src/IVF.Domain/Entities/User.cs)
- **Purpose**: Core user domain aggregate root
- **Properties**:
  - Username, PasswordHash (BCrypt), FullName, Role, Department
  - IsActive, RefreshToken, RefreshTokenExpiryTime
  - Methods: Create(), UpdateRefreshToken(), RevokeRefreshToken(), Deactivate(), Activate(), UpdateInfo(), UpdatePassword()

### Role Enum
- **File**: [src/IVF.Domain/Enums/UserRole.cs](src/IVF.Domain/Enums/UserRole.cs)
- **Roles**: Admin, Director, Doctor, Embryologist, Nurse, LabTech, Receptionist, Cashier, Pharmacist

### Permission Enum
- **File**: [src/IVF.Domain/Enums/Permission.cs](src/IVF.Domain/Enums/Permission.cs)
- **Fine-grained permissions**: ViewPatients, ManagePatients, ViewCycles, ManageCycles, PerformUltrasound, ManageEmbryos, ViewLabResults, ManageBilling, ProcessPayment, etc.

### UserPermission Entity
- **File**: [src/IVF.Domain/Entities/UserPermission.cs](src/IVF.Domain/Entities/UserPermission.cs)
- **Purpose**: Join entity for User-Permission many-to-many
- **Features**: String-based permission codes (supports dynamic permissions), GrantedBy tracking, GrantedAt timestamp

### UserMfaSetting Entity
- **File**: [src/IVF.Domain/Entities/UserMfaSetting.cs](src/IVF.Domain/Entities/UserMfaSetting.cs)
- **Methods**:
  - EnableTotp(totpSecretKey), VerifyTotp(), SetPhoneNumber(), VerifyPhone()
  - SetRecoveryCodes(), RecordMfaSuccess(), RecordMfaFailure(), DisableMfa()
- **Features**: TOTP (Authenticator), SMS OTP, Recovery codes, Failed attempts tracking

### UserSignature Entity
- **File**: [src/IVF.Domain/Entities/UserSignature.cs](src/IVF.Domain/Entities/UserSignature.cs)
- **Purpose**: Stores handwritten signatures + signing certificates
- **Properties**: SignatureImageBase64, CertificateInfo (subject, serial, expiry), WorkerName, KeystorePath
- **Status**: CertificateStatus enum tracking provisioning

### PasskeyCredential Entity
- **File**: [src/IVF.Domain/Entities/PasskeyCredential.cs](src/IVF.Domain/Entities/PasskeyCredential.cs)
- **Purpose**: FIDO2/WebAuthn passwordless credentials
- **Properties**: CredentialId, PublicKey, UserHandle, SignatureCounter, DeviceName, AttestationFormat, AaGuid, LastUsedAt
- **Methods**: UpdateCounter(), Revoke(), Rename()

### AccountLockout Entity
- **File**: [src/IVF.Domain/Entities/AccountLockout.cs](src/IVF.Domain/Entities/AccountLockout.cs)
- **Purpose**: Account lockout tracking for brute force protection
- **Properties**: UserId, Username, Reason, LockedAt, UnlocksAt, FailedAttempts, LockedBy, IsManualLock
- **Methods**: IsCurrentlyLocked(), Unlock(), IncrementFailedAttempts()

### PermissionDefinition Entity
- **File**: [src/IVF.Domain/Entities/PermissionDefinition.cs](src/IVF.Domain/Entities/PermissionDefinition.cs)
- **Purpose**: Database-driven permission metadata (grouping, display names, ordering)
- **Properties**: Code, DisplayName, GroupCode, GroupDisplayName, GroupIcon, SortOrder, GroupSortOrder, IsActive

### SecurityEvent Entity
- **File**: [src/IVF.Domain/Entities/SecurityEvent.cs](src/IVF.Domain/Entities/SecurityEvent.cs)
- **Purpose**: Zero Trust continuous monitoring events
- **Properties**: EventType (50+ types), Severity, UserId, Username, IpAddress, UserAgent, DeviceFingerprint, Country, City, RequestPath, RequestMethod, ResponseStatusCode, Details (JSON), ThreatIndicators, RiskScore, IsBlocked, CorrelationId, SessionId
- **Event Types**: LoginSuccess, LoginFailed, LoginBruteForce, MfaRequired, MfaFailed, AccessDenied, PrivilegeEscalation, ZtAccessDenied, ZtBreakGlass, ImpossibleTravel, AnomalousAccess, TorExit, VpnProxy, RateLimitExceeded, SqlInjectionAttempt, SessionHijackAttempt, ConcurrentSession, etc.

### DeviceRisk Entity
- **File**: [src/IVF.Domain/Entities/DeviceRisk.cs](src/IVF.Domain/Entities/DeviceRisk.cs)
- **Purpose**: Device risk scoring + trust state
- **Properties**: UserId, DeviceId, RiskLevel, RiskScore, Factors (JSON), IsTrusted, IpAddress, Country, UserAgent
- **Methods**: MarkTrusted(), UpdateRisk()

## BACKEND APPLICATION LAYER (CQRS)

### User Commands
- **File**: [src/IVF.Application/Features/Users/Commands/UserCommands.cs](src/IVF.Application/Features/Users/Commands/UserCommands.cs)
- **Commands**:
  - CreateUserCommand: Creates new user with BCrypt password hashing
  - UpdateUserCommand: Updates user info, role, department, active status, optional password change
  - DeleteUserCommand: Soft delete (deactivation)
- **Handlers**: All use IUserRepository + IUnitOfWork pattern

### User Queries
- **File**: [src/IVF.Application/Features/Users/Queries/UserQueries.cs](src/IVF.Application/Features/Users/Queries/UserQueries.cs)
- **Queries**:
  - GetUsersQuery: List with search, role filter, active filter, pagination
  - SearchDoctorsQuery: Returns DoctorDto with specialty

### Repositories
- **IUserRepository**: GetByIdAsync, GetByUsernameAsync, GetByRefreshTokenAsync, UpdateAsync, AddAsync, DeleteAsync, GetUsersByRoleAsync, SearchUsersAsync, CountUsersAsync
- **IUserPermissionRepository**: GetByUserIdAsync, HasPermissionAsync, AddAsync, AddRangeAsync, DeleteAsync, DeleteAllByUserIdAsync

## BACKEND INFRASTRUCTURE

### EF Core Configuration
- **File**: [src/IVF.Infrastructure/Persistence/Configurations/UserConfiguration.cs](src/IVF.Infrastructure/Persistence/Configurations/UserConfiguration.cs)
- TableName: "users"
- Unique constraint on Username
- HasQueryFilter: !IsDeleted (soft deletes)

### Implementations
- **UserRepository**: [src/IVF.Infrastructure/Repositories/UserRepository.cs](src/IVF.Infrastructure/Repositories/UserRepository.cs)
- **UserPermissionRepository**: [src/IVF.Infrastructure/Repositories/UserPermissionRepository.cs](src/IVF.Infrastructure/Repositories/UserPermissionRepository.cs)

## BACKEND API ENDPOINTS

### Authentication Endpoints
- **File**: [src/IVF.API/Endpoints/AuthEndpoints.cs](src/IVF.API/Endpoints/AuthEndpoints.cs)
- **Endpoints**:
  - POST /api/auth/login: Pre-auth threat assessment → credential verification → MFA check → device fingerprinting → adaptive session creation → JWT + refresh token generation
  - POST /api/auth/refresh: Token family reuse detection → new JWT + refresh token
  - GET /api/auth/me: Current user info
  - GET /api/auth/me/permissions: Current user's permissions
  - POST /api/auth/mfa-verify: TOTP/SMS code verification
  - POST /api/auth/mfa-send-sms: Send SMS OTP
  - POST /api/auth/passkey-login/begin: FIDO2 assertion options
  - POST /api/auth/passkey-login/complete: FIDO2 assertion verification

### User Management Endpoints
- **File**: [src/IVF.API/Endpoints/UserEndpoints.cs](src/IVF.API/Endpoints/UserEndpoints.cs)
- **Endpoints** (all require AdminOnly authorization):
  - GET /api/users: Search users (query, role, isActive, pagination)
  - GET /api/users/roles: List available roles
  - GET /api/users/permissions: List all available permissions
  - GET /api/users/{id}/permissions: Get user's permissions
  - POST /api/users/{id}/permissions: Assign permissions (bulk)
  - DELETE /api/users/{id}/permissions/{permission}: Revoke single permission
  - POST /api/users: Create user
  - PUT /api/users/{id}: Update user
  - DELETE /api/users/{id}: Deactivate user

### User Signature Endpoints
- **File**: [src/IVF.API/Endpoints/UserSignatureEndpoints.cs](src/IVF.API/Endpoints/UserSignatureEndpoints.cs)
- **Endpoints**:
  - GET /api/user-signatures/me: Get current user's active signature
  - POST /api/user-signatures/me: Upload/update signature (validates PNG/JPEG)
  - DELETE /api/user-signatures/me: Delete signatures
  - GET /api/user-signatures/users/{userId}: Get user's signature (admin)
  - GET /api/user-signatures: List all users with signature status (LEFT JOIN)

## BACKEND MIDDLEWARE (Auth Pipeline)

### Middleware Stack Order
1. **SecurityEnforcementMiddleware**: IP whitelist, geo-blocking
2. **VaultTokenMiddleware**: X-Vault-Token header auth
3. **ApiKeyMiddleware**: X-API-Key header or apiKey query param auth
4. **ZeroTrustMiddleware**: Continuous verification, threat assessment, session binding
5. **Standard JWT Bearer**: UseAuthentication()

### VaultTokenMiddleware
- **File**: [src/IVF.API/Middleware/VaultTokenMiddleware.cs](src/IVF.API/Middleware/VaultTokenMiddleware.cs)
- Validates X-Vault-Token header
- Creates ClaimsPrincipal with vault policies

### ApiKeyMiddleware
- **File**: [src/IVF.API/Middleware/ApiKeyMiddleware.cs](src/IVF.API/Middleware/ApiKeyMiddleware.cs)
- Falls back to JWT if already authenticated
- Validates API key from header or query param

### ZeroTrustMiddleware
- **File**: [src/IVF.API/Middleware/ZeroTrustMiddleware.cs](src/IVF.API/Middleware/ZeroTrustMiddleware.cs)
- Exempt paths: /api/auth/*, /health, /swagger
- Sensitive paths: /api/zerotrust, /api/keyvault, /api/audit, /api/users, /api/signing-admin, /api/backup, /api/ca
- Evaluates: Device fingerprint, behavioral anomalies, IP intelligence, session binding, input validation, rate limiting
- Action: Block (403), stepped up verification, or monitor

## FRONTEND MODELS

### Auth Models
- **File**: [ivf-client/src/app/core/models/auth.models.ts](ivf-client/src/app/core/models/auth.models.ts)
- **Interfaces**:
  - LoginRequest (username, password)
  - AuthResponse (accessToken, refreshToken, expiresIn, user)
  - MfaRequiredResponse (mfaToken, mfaMethod, user)
  - MfaVerifyRequest (mfaToken, code)
  - User (id, username, fullName, role, department)

### Advanced Security Models
- **File**: [ivf-client/src/app/core/models/advanced-security.model.ts](ivf-client/src/app/core/models/advanced-security.model.ts)
- **Types**:
  - SecurityScore: score, level, factors, totalEvents24h, blockedRequests, criticalAlerts, failedLogins, suspiciousLogins, trustedDevices, activeSessions, activeLockouts, mfaEnabledUsers, passkeyCount
  - LoginHistoryEntry: eventType, severity, userId, username, ipAddress, country, city, deviceFingerprint, riskScore, isSuspicious, riskFactors, createdAt
  - RateLimitBuiltInPolicy, RateLimitCustomConfig, RateLimitStatus
  - GeoDistribution, ImpossibleTravelAlert, GeoBlockRule
  - ThreatEvent, ThreatCategory, ThreatSummary, ThreatOverview
  - AccountLockout, LockAccountRequest
  - IpWhitelist

## FRONTEND SERVICES

### AuthService
- **File**: [ivf-client/src/app/core/services/auth.service.ts](ivf-client/src/app/core/services/auth.service.ts)
- **State Management**: Signals for user, permissions, isAuthenticated
- **Methods**:
  - login(credentials): POST /api/auth/login
  - refreshToken(): POST /api/auth/refresh
  - logout(): Clear localStorage, navigate to /login
  - getToken(): Return JWT from localStorage
  - hasRole(role), hasPermission(permission), hasAnyPermission(permissions)
  - setPermissions(), loadUserPermissions()
  - verifyMfa(request), sendMfaSms(mfaToken)
  - passkeyLoginBegin(username), passkeyLoginComplete(userId, assertionResponse)

### UserService
- **File**: [ivf-client/src/app/core/services/user.service.ts](ivf-client/src/app/core/services/user.service.ts)
- **Methods**:
  - getUsers(search, role, isActive, page, pageSize)
  - getRoles()
  - createUser(data), updateUser(id, data), deleteUser(id)
  - searchDoctors(query), createDoctor(data)
  - getAllPermissions(), getUserPermissions(userId)
  - assignPermissions(userId, permissions, grantedBy)
  - revokePermission(userId, permission)

### SecurityService
- **File**: [ivf-client/src/app/core/services/security.service.ts](ivf-client/src/app/core/services/security.service.ts)
- **Methods**:
  - getDashboard(): SecurityScore
  - getRecentEvents(count), getUserEvents(userId, hours), getIpEvents(ipAddress, hours)
  - getHighSeverityEvents(hours)
  - assessThreat(request)
  - getActiveSessions(userId), revokeSession(sessionId)
  - checkIpIntelligence(ipAddress)
  - checkDeviceTrust(userId, fingerprint)

### PermissionDefinitionService
- **File**: [ivf-client/src/app/core/services/permission-definition.service.ts](ivf-client/src/app/core/services/permission-definition.service.ts)
- **Methods**:
  - loadPermissionGroups(): Cached in signal
  - getAll(), create(), update(), toggle(), delete()

## FRONTEND COMPONENTS

### User Management Component
- **File**: [ivf-client/src/app/features/admin/users/user-management.component.ts](ivf-client/src/app/features/admin/users/user-management.component.ts)
- **Features**:
  - List users with search, role filter, active filter, pagination
  - Create/Edit/Delete users
  - Promote user to Doctor role
  - Assign permissions to users (modal with grouped permissions)
  - Change user password

### Permission Management Component
- **File**: [ivf-client/src/app/features/admin/permissions/permission-management.component.ts](ivf-client/src/app/features/admin/permissions/permission-management.component.ts)
- **Tabs**: Users tab, RBAC matrix tab
- **Features**:
  - Expand user rows to view permissions
  - Edit permissions for multiple users
  - Display permissions grouped with icons and Vietnamese labels

## FRONTEND AUTH FLOW

### Auth Guard
- **File**: [ivf-client/src/app/core/guards/auth.guard.ts](ivf-client/src/app/core/guards/auth.guard.ts)
- **Guards**: authGuard (requires authentication), guestGuard (redirects authenticated users)

### Auth Interceptor
- **File**: [ivf-client/src/app/core/interceptors/auth.interceptor.ts](ivf-client/src/app/core/interceptors/auth.interceptor.ts)
- **Features**:
  - Adds Authorization: Bearer token to all requests
  - Skips token for login/refresh/mfa/passkey endpoints
  - Handles 401 errors: attempts token refresh
  - On refresh failure: logs out user

## CURRENT CAPABILITIES

### User Management
- ✅ CRUD operations (creates -> BC
rypt hashing, updates, soft deletes)
- ✅ 9 predefined roles (Admin, Director, Doctor, Embryologist, Nurse, LabTech, Receptionist, Cashier, Pharmacist)
- ✅ Fine-grained permission system (35+ permissions)
- ✅ Dynamic permission definitions (database-driven, UI-manageable)
- ✅ User permission assignment (bulk)

### Authentication
- ✅ Username/password login with threat assessment
- ✅ JWT token generation (3600s expiry)
- ✅ Refresh token with 7-day expiry
- ✅ Refresh token family reuse detection
- ✅ Account lockout (brute force protection)
- ✅ Credential validation via BCrypt

### MFA (Multi-Factor Authentication)
- ✅ TOTP (Time-based One-Time Password) - Authenticator apps
- ✅ SMS OTP
- ✅ Recovery codes
- ✅ MFA enforcement on login (if enabled)
- ✅ Pending MFA token tracking

### Passwordless Authentication
- ✅ FIDO2/WebAuthn passkey registration and login
- ✅ Device naming and management
- ✅ Signature counter validation

### Digital Signing
- ✅ User signature (handwritten/drawn)
- ✅ Signature image storage (PNG/JPEG)
- ✅ Certificate provisioning (EJBCA integration)
- ✅ Certificate metadata tracking (subject, serial, expiry)
- ✅ SignServer worker assignment

### Security & Monitoring
- ✅ Zero Trust continuous verification middleware
- ✅ Device fingerprinting (user agent, IP, screen resolution, timezone, etc.)
- ✅ Device trust scoring
- ✅ Session creation with context binding
- ✅ Session hijacking detection
- ✅ Security event logging (50+ event types)
- ✅ Threat assessment (brute force, impossible travel, anomalies, etc.)
- ✅ IP intelligence integration
- ✅ Geo-fencing support
- ✅ Impossible travel detection
- ✅ Rate limiting (built-in policies + custom configs)
- ✅ Audit logging with correlation IDs

### Authentication Methods
- ✅ Triple auth middleware pipeline:
  1. X-Vault-Token (HashiCorp Vault)
  2. X-API-Key (API key header or query param)
  3. JWT Bearer token

## CURRENT SECURITY GAPS vs ENTERPRISE SYSTEMS

### Missing Features (Google/Amazon/Facebook Level)

#### Adaptive Authentication
- ❌ Risk-based step-up (e.g., require MFA if high-risk login detected)
- ❌ Contextual authentication (e.g., require additional verification for new device + unusual time)
- ❌ Conditional access policies (not framework for device compliance requirements)

#### Advanced Identity
- ❌ Social login (OAuth2/OIDC integration with Google, Microsoft, etc.)
- ❌ Federated identity management (SAML2, OIDC provider)
- ❌ Just-in-time (JIT) user provisioning
- ❌ User groups/teams concept (only flat roles)
- ❌ Hierarchical/dynamic roles (RBAC only)

#### Advanced MFA
- ❌ Push notifications (e.g., "Approve login on mobile")
- ❌ Biometric authentication on server-side (client-side with DigitalPersona only)
- ❌ Hardware security key management (basic passkey support, not full HSM)
- ❌ MFA enrollment flows (basic setup, no guided onboarding)

#### Threat Detection & Response
- ❌ Behavioral analytics (login patterns, access patterns, etc.)
- ❌ Anomaly scoring (ML-based)
- ❌ Real-time SIEM integration (logs only)
- ❌ Incident response workflows (no automated actions)
- ❌ Adaptive throttling (only static rate limits)
- ❌ Bot detection (no CAPTCHA or behavioral analysis)

#### Privacy & Compliance
- ❌ GDPR consent management (no consent framework)
- ❌ Data retention/deletion (no automated purging)
- ❌ PII masking in logs
- ❌ Audit trail immutability (uses PostgreSQL, not append-only ledger)
- ❌ Encryption key rotation automation
- ❌ Secrets scanning (no detection of exposed credentials in logs)

#### Session Management
- ❌ Concurrent session limits per user (no built-in limit)
- ❌ Session revocation batch operations
- ❌ Device management portal (users can't self-manage trusted devices)
- ❌ Timeout policies (adaptive or granular)

#### Attestation & Verification
- ❌ Device attestation parsing (stores raw attestation, doesn't verify)
- ❌ Malware score integration (no endpoint security check)
- ❌ OS/browser version enforcement
- ❌ Location verification (has IP geo, not precise location)

#### Delegation & Impersonation
- ❌ Admin impersonation with dual approval
- ❌ Service accounts with separate audit trail
- ❌ OAuth2 authorization servers (no ability for third-party access)
- ❌ Delegated privileges (only direct permission assignment)

#### Attack Surface Reduction
- ❌ Passwordless by default enforcement
- ❌ Credential stuffing detection (no password breach monitoring)
- ❌ Account takeover protection (multi-signal only, not ML)
- ❌ Login anomaly notifications (events logged, no notifications sent)
- ❌ Recovery code consumption notifications

## RECOMMENDATIONS FOR ENTERPRISE ENHANCEMENT

### High Priority
1. Risk-based step-up authentication (use existing threat assessment)
2. Social login via OAuth2 (OpenID Connect provider)
3. Push notification MFA
4. Behavioral analytics + ML anomaly detection
5. GDPR consent/data management
6. Bot detection (reCAPTCHA or Cloudflare)

### Medium Priority
7. User groups & team concept
8. Session concurrency limits
9. Device management portal
10. Incident response workflows
11. Hardware security key support
12. Audit trail immutability (Kafka/append-only DB)

### Low Priority (nice-to-have)
13. SAML2 federation
14. Secrets scanning in logs
15. Adaptive throttling via ML
16. OS/browser version enforcement
17. Malware score integration
