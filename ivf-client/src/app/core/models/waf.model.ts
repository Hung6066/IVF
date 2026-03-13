export interface WafStatus {
  configured: boolean;
  managedRules: WafRule[];
  customRules: WafRule[];
  rateLimitRules: WafRule[];
  securityLevel: string;
  minTlsVersion: string;
  totalRuleCount: number;
}

export interface WafRule {
  id: string;
  description: string;
  action: string;
  enabled: boolean;
  expression: string;
}

export interface WafEvent {
  action: string;
  clientIp: string;
  path: string;
  method: string;
  ruleId: string;
  source: string;
  timestamp: string;
  userAgent: string;
  country: string;
}

export interface SsoProvider {
  id: string;
  displayName: string;
  iconUrl: string;
  clientId: string;
  scopes: string;
}
