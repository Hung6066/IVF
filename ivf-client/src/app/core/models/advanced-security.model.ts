// ─── Security Score ───
export interface SecurityScore {
  score: number;
  level: 'good' | 'warning' | 'critical';
  factors: SecurityFactor[];
  totalEvents24h: number;
  blockedRequests: number;
  criticalAlerts: number;
  failedLogins: number;
  suspiciousLogins: number;
  trustedDevices: number;
  activeSessions: number;
  activeLockouts: number;
  mfaEnabledUsers: number;
  passkeyCount: number;
  lastUpdated: string;
}

export interface SecurityFactor {
  factor: string;
  count: number;
  impact: number;
}

// ─── Login History ───
export interface LoginHistoryEntry {
  id: string;
  eventType: string;
  severity: string;
  userId: string | null;
  username: string | null;
  ipAddress: string | null;
  country: string | null;
  city: string | null;
  deviceFingerprint: string | null;
  riskScore: number | null;
  isSuspicious: boolean;
  riskFactors: string[];
  createdAt: string;
}

// ─── Rate Limit ───
export interface RateLimitBuiltInPolicy {
  name: string;
  window: string;
  limit: number;
  policy: string;
  builtIn: boolean;
}

export interface RateLimitCustomConfig {
  id: string;
  policyName: string;
  windowType: string;
  windowSeconds: number;
  permitLimit: number;
  appliesTo: string | null;
  isEnabled: boolean;
  description: string | null;
  createdBy: string;
  createdAt: string;
}

export interface RateLimitStatus {
  builtInPolicies: RateLimitBuiltInPolicy[];
  customConfigs: RateLimitCustomConfig[];
  updatedAt: string;
}

export interface CreateRateLimitRequest {
  policyName: string;
  windowType: string;
  windowSeconds: number;
  permitLimit: number;
  appliesTo?: string;
  createdBy?: string;
  description?: string;
}

export interface UpdateRateLimitRequest {
  windowType: string;
  windowSeconds: number;
  permitLimit: number;
  description?: string;
}

export interface RateLimitEvent {
  id: string;
  ipAddress: string | null;
  username: string | null;
  requestPath: string | null;
  riskScore: number | null;
  details: string | null;
  createdAt: string;
}

// ─── Geo Security ───
export interface GeoDistribution {
  country: string;
  totalEvents: number;
  suspiciousEvents: number;
  blockedEvents: number;
  uniqueIps: number;
  lastSeen: string;
}

export interface ImpossibleTravelAlert {
  id: string;
  userId: string | null;
  username: string | null;
  ipAddress: string | null;
  country: string | null;
  city: string | null;
  details: string | null;
  riskScore: number | null;
  createdAt: string;
}

export interface GeoBlockRule {
  id: string;
  countryCode: string;
  countryName: string;
  isBlocked: boolean;
  reason: string | null;
  createdBy: string;
  createdAt: string;
}

export interface CreateGeoBlockRuleRequest {
  countryCode: string;
  countryName: string;
  isBlocked: boolean;
  reason?: string;
  createdBy?: string;
}

export interface GeoSecurityData {
  geoDistribution: GeoDistribution[];
  impossibleTravelAlerts: ImpossibleTravelAlert[];
  totalCountries: number;
  geoBlockRules: GeoBlockRule[];
}

// ─── Threats ───
export interface ThreatEvent {
  id: string;
  ipAddress: string | null;
  username: string | null;
  severity: string;
  riskScore: number | null;
  details: string | null;
  isBlocked: boolean;
  createdAt: string;
}

export interface ThreatCategory {
  type: string;
  count: number;
  severity: string;
  latestRiskScore: number | null;
  events: ThreatEvent[];
}

export interface ThreatSummary {
  totalThreats: number;
  criticalCount: number;
  highCount: number;
  blockedCount: number;
  topIps: { ip: string; count: number }[];
}

export interface ThreatOverview {
  summary: ThreatSummary;
  categories: ThreatCategory[];
}

// ─── Account Lockout ───
export interface AccountLockout {
  id: string;
  userId: string;
  username: string;
  reason: string;
  lockedAt: string;
  unlocksAt: string;
  failedAttempts: number;
  lockedBy: string | null;
  isManualLock: boolean;
  isLocked: boolean;
}

export interface LockAccountRequest {
  userId: string;
  username: string;
  reason: string;
  durationMinutes: number;
  failedAttempts: number;
  lockedBy?: string;
}

// ─── IP Whitelist ───
export interface WhitelistedIp {
  id: string;
  ipAddress: string;
  cidrRange: string | null;
  description: string | null;
  addedBy: string;
  addedAt: string;
  expiresAt: string | null;
  isActive: boolean;
}

export interface AddIpWhitelistRequest {
  ipAddress: string;
  description?: string;
  addedBy?: string;
  expiresInDays?: number;
  cidrRange?: string;
}

export interface UpdateIpWhitelistRequest {
  description?: string;
  expiresInDays?: number;
}

// ─── User Devices ───
export interface UserDevice {
  id: string;
  fingerprint: string;
  ipAddress: string | null;
  country: string | null;
  userAgent: string | null;
  isTrusted: boolean;
  riskLevel: string;
  riskScore: number;
  factors: string | null;
  firstSeen: string;
  lastSeen: string;
}

// ─── Passkeys / WebAuthn ───
export interface PasskeyCredential {
  id: string;
  credentialId: string;
  deviceName: string | null;
  credentialType: string;
  attestationFormat: string | null;
  aaGuid: string | null;
  signatureCounter: number;
  isActive: boolean;
  lastUsedAt: string | null;
  registeredAt: string;
}

export interface PasskeyRegisterBeginRequest {
  userId: string;
}

export interface PasskeyRegisterCompleteRequest {
  userId: string;
  attestationResponse: any;
  deviceName?: string;
}

export interface RenamePasskeyRequest {
  deviceName: string;
}

// ─── MFA Settings ───
export interface MfaSettings {
  userId: string;
  isMfaEnabled: boolean;
  mfaMethod: string;
  isTotpVerified: boolean;
  isPhoneVerified: boolean;
  phoneNumber: string | null;
  lastMfaAt: string | null;
  failedMfaAttempts: number;
}

export interface TotpSetupResponse {
  secret: string;
  otpauthUri: string;
  message: string;
}

export interface TotpVerifyRequest {
  userId: string;
  code: string;
}

export interface SmsRegisterRequest {
  userId: string;
  phoneNumber: string;
}

export interface SmsVerifyRequest {
  userId: string;
  code: string;
}

// ─── Risk Factor Labels ───
export const RISK_FACTOR_LABELS: Record<string, string> = {
  multiple_failed_attempts: 'Nhiều lần thất bại',
  impossible_travel: 'Di chuyển bất thường',
  login_failed: 'Đăng nhập thất bại',
  off_hours_access: 'Ngoài giờ làm việc',
  high_risk_score: 'Điểm rủi ro cao',
  was_blocked: 'Đã bị chặn',
  new_device: 'Thiết bị mới',
  new_ip: 'IP mới',
};

// ─── Severity Colors ───
export const SEVERITY_COLORS: Record<string, string> = {
  critical: 'severity-critical',
  high: 'severity-high',
  medium: 'severity-medium',
  low: 'severity-low',
  info: 'severity-info',
};
