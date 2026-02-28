export interface ZTPolicyResponse {
  id: string;
  action: string;
  requiredAuthLevel: string;
  maxAllowedRisk: string;
  requireTrustedDevice: boolean;
  requireFreshSession: boolean;
  blockAnomaly: boolean;
  requireGeoFence: boolean;
  allowedCountries: string | null;
  blockVpnTor: boolean;
  allowBreakGlassOverride: boolean;
  isActive: boolean;
}

export interface ZTAccessDecision {
  allowed: boolean;
  action: string;
  reason: string;
  failedChecks: string[];
  deviceRiskLevel: string | null;
  deviceRiskScore: number | null;
  requiresStepUp: boolean;
  requiredAuthLevel: string | null;
  breakGlassOverrideUsed: boolean;
  decisionTime: string;
}

export interface UpdateZTPolicyRequest {
  action: string;
  requiredAuthLevel: string;
  maxAllowedRisk: string;
  requireTrustedDevice: boolean;
  requireFreshSession: boolean;
  blockAnomaly: boolean;
  requireGeoFence: boolean;
  allowedCountries: string | null;
  blockVpnTor: boolean;
  allowBreakGlassOverride: boolean;
  updatedBy: string;
}
