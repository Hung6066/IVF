// ─── Conditional Access Policies ───
export interface ConditionalAccessPolicy {
  id: string;
  name: string;
  description?: string;
  isEnabled: boolean;
  priority: number;
  targetRoles?: string;
  targetUsers?: string;
  allowedCountries?: string;
  blockedCountries?: string;
  allowedIpRanges?: string;
  allowedTimeWindows?: string;
  maxRiskLevel?: string;
  requireMfa: boolean;
  requireCompliantDevice: boolean;
  blockVpnTor: boolean;
  requiredDeviceTrust?: string;
  action: string;
  customMessage?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateConditionalAccessRequest {
  name: string;
  description?: string;
  priority: number;
  maxRiskLevel: number;
  requireMfa: boolean;
  requireCompliantDevice: boolean;
  blockVpnTor: boolean;
  action: string;
  targetRoles?: string[];
  allowedCountries?: string[];
  blockedCountries?: string[];
  allowedIpRanges?: string[];
  allowedTimeWindows?: object[];
}

// ─── Incident Response Rules ───
export interface IncidentResponseRule {
  id: string;
  name: string;
  description?: string;
  isEnabled: boolean;
  priority: number;
  triggerEventTypes?: string;
  triggerSeverities?: string;
  triggerThreshold?: number;
  triggerWindowMinutes?: number;
  actions: string;
  incidentSeverity: string;
  createdAt: string;
}

export interface CreateIncidentRuleRequest {
  name: string;
  description?: string;
  priority: number;
  triggerEventTypes: string[];
  triggerSeverities: string[];
  triggerThreshold?: number;
  triggerWindowMinutes?: number;
  actions: string[];
  incidentSeverity: string;
}

// ─── Security Incidents ───
export interface SecurityIncident {
  id: string;
  incidentType: string;
  severity: string;
  status: string;
  userId?: string;
  username?: string;
  ipAddress?: string;
  description?: string;
  details?: string;
  actionsTaken?: string;
  assignedTo?: string;
  resolution?: string;
  resolvedAt?: string;
  resolvedBy?: string;
  relatedEventIds?: string;
  createdAt: string;
}

// ─── Data Retention Policies ───
export interface DataRetentionPolicy {
  id: string;
  entityType: string;
  retentionDays: number;
  action: string;
  isEnabled: boolean;
  description?: string;
  lastExecutedAt?: string;
  lastPurgedCount?: number;
  createdAt: string;
}

export interface CreateDataRetentionRequest {
  entityType: string;
  retentionDays: number;
  action: string;
  description?: string;
}

// ─── Impersonation ───
export interface ImpersonationRequest {
  id: string;
  requestedBy: string;
  targetUserId: string;
  reason: string;
  status: string;
  approvedBy?: string;
  deniedBy?: string;
  denialReason?: string;
  sessionToken?: string;
  expiresAt?: string;
  startedAt?: string;
  endedAt?: string;
  endReason?: string;
  createdAt: string;
}

export interface CreateImpersonationRequest {
  targetUserId: string;
  reason: string;
  durationHours?: number;
}

// ─── Permission Delegation ───
export interface PermissionDelegation {
  id: string;
  fromUserId: string;
  toUserId: string;
  permissions: string;
  validFrom: string;
  validUntil: string;
  isRevoked: boolean;
  revokedAt?: string;
  revokeReason?: string;
  reason?: string;
  createdAt: string;
}

export interface CreateDelegationRequest {
  toUserId: string;
  permissions: string[];
  validFrom?: string;
  validUntil: string;
  reason?: string;
}

// ─── Behavioral Analytics ───
export interface UserBehaviorProfile {
  id: string;
  userId: string;
  typicalLoginHours?: string;
  commonIpAddresses?: string;
  commonCountries?: string;
  commonDeviceFingerprints?: string;
  commonUserAgents?: string;
  averageSessionDurationMinutes: number;
  totalLogins: number;
  failedLogins: number;
  lastLoginAt?: string;
  lastFailedLoginAt?: string;
  createdAt: string;
}

// ─── Notification Preferences ───
export interface NotificationPreference {
  id: string;
  userId: string;
  channel: string;
  eventTypes: string;
  isEnabled: boolean;
  createdAt: string;
}

export interface CreateNotificationPrefRequest {
  userId: string;
  channel: string;
  eventTypes: string[];
}

// ─── Paged Response ───
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
