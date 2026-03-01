export interface SecurityEvent {
  id: string;
  eventType: string;
  severity: string;
  userId: string | null;
  username: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  deviceFingerprint: string | null;
  country: string | null;
  city: string | null;
  requestPath: string | null;
  requestMethod: string | null;
  responseStatusCode: number | null;
  details: string | null;
  threatIndicators: string | null;
  riskScore: number | null;
  isBlocked: boolean;
  correlationId: string | null;
  sessionId: string | null;
  createdAt: string;
}

export interface SecurityDashboard {
  last24Hours: {
    totalHighSeverity: number;
    blockedRequests: number;
    threatsByType: ThreatByType[];
    uniqueIps: number;
    uniqueUsers: number;
  };
  recentEvents: SecurityEvent[];
}

export interface ThreatByType {
  type: string;
  count: number;
}

export interface ThreatAssessment {
  riskScore: number;
  riskLevel: string;
  recommendedAction: string;
  signals: ThreatSignal[];
  timestamp: string;
}

export interface ThreatSignal {
  category: string;
  description: string;
  score: number;
  details: string | null;
}

export interface IpIntelligence {
  ipAddress: string;
  isTor: boolean;
  isVpn: boolean;
  isProxy: boolean;
  isHosting: boolean;
  isKnownAttacker: boolean;
  country: string | null;
  city: string | null;
  riskScore: number;
}

export interface DeviceTrust {
  fingerprint: string;
  trustLevel: string;
  isRegistered: boolean;
  lastSeenAt: string | null;
  riskScore: number;
}

export interface SessionInfo {
  sessionId: string;
  userId: string;
  ipAddress: string;
  deviceFingerprint: string;
  country: string | null;
  createdAt: string;
  lastActivityAt: string;
  isActive: boolean;
}

export interface AssessRequest {
  ipAddress: string;
  username?: string;
  userAgent?: string;
  country?: string;
  requestPath?: string;
}
