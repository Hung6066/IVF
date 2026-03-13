// ═══════════════════════════════════════════════════════════════
// Application-Level WAF Models
// ═══════════════════════════════════════════════════════════════

// ─── App WAF Rule ───
export interface AppWafRule {
  id: string;
  name: string;
  description: string | null;
  priority: number;
  isEnabled: boolean;
  ruleGroup: string;
  isManaged: boolean;
  action: string;
  matchType: string;
  negateMatch: boolean;
  expression: string | null;
  uriPathPatterns: string[] | null;
  queryStringPatterns: string[] | null;
  headerPatterns: string[] | null;
  bodyPatterns: string[] | null;
  methods: string[] | null;
  ipCidrList: string[] | null;
  countryCodes: string[] | null;
  userAgentPatterns: string[] | null;
  rateLimitRequests: number | null;
  rateLimitWindowSeconds: number | null;
  blockResponseMessage: string | null;
  hitCount: number;
  createdBy: string | null;
  lastModifiedBy: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateWafRuleRequest {
  name: string;
  description?: string;
  priority: number;
  ruleGroup: string;
  action: string;
  matchType: string;
  negateMatch: boolean;
  expression?: string;
  uriPathPatterns?: string[];
  queryStringPatterns?: string[];
  headerPatterns?: string[];
  bodyPatterns?: string[];
  methods?: string[];
  ipCidrList?: string[];
  countryCodes?: string[];
  userAgentPatterns?: string[];
  rateLimitRequests?: number;
  rateLimitWindowSeconds?: number;
  blockResponseMessage?: string;
  createdBy?: string;
}

export interface UpdateWafRuleRequest {
  id: string;
  name: string;
  description?: string;
  priority: number;
  action: string;
  matchType: string;
  negateMatch: boolean;
  expression?: string;
  uriPathPatterns?: string[];
  queryStringPatterns?: string[];
  headerPatterns?: string[];
  bodyPatterns?: string[];
  methods?: string[];
  ipCidrList?: string[];
  countryCodes?: string[];
  userAgentPatterns?: string[];
  rateLimitRequests?: number;
  rateLimitWindowSeconds?: number;
  blockResponseMessage?: string;
  modifiedBy?: string;
}

// ─── App WAF Event ───
export interface AppWafEvent {
  id: string;
  wafRuleId: string | null;
  ruleName: string;
  ruleGroup: string;
  action: string;
  clientIp: string;
  country: string | null;
  requestPath: string;
  requestMethod: string;
  queryString: string | null;
  userAgent: string | null;
  matchedPattern: string | null;
  matchedValue: string | null;
  responseStatusCode: number | null;
  correlationId: string | null;
  processingTimeMs: number;
  createdAt: string;
}

export interface AppWafEventsResponse {
  items: AppWafEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ─── App WAF Analytics ───
export interface AppWafAnalytics {
  totalEvents: number;
  blockedCount: number;
  challengedCount: number;
  loggedCount: number;
  rateLimitedCount: number;
  blockRate: number;
  topBlockedIps: { ip: string; count: number }[];
  topTriggeredRules: { ruleName: string; count: number }[];
  hourlyBreakdown: { hour: string; count: number }[];
}

// ─── Constants ───
export const WAF_ACTIONS = [
  { value: 'Block', label: 'Chặn', icon: '🚫', color: 'danger' },
  { value: 'Challenge', label: 'Thách thức', icon: '🔐', color: 'warning' },
  { value: 'Log', label: 'Ghi log', icon: '📝', color: 'info' },
  { value: 'RateLimit', label: 'Giới hạn', icon: '🚦', color: 'warning' },
  { value: 'AllowBypass', label: 'Cho phép', icon: '✅', color: 'success' },
];

export const WAF_RULE_GROUPS = [
  { value: 'Custom', label: 'Tùy chỉnh', icon: '⚡' },
  { value: 'OwaspCore', label: 'OWASP Core', icon: '🔒' },
  { value: 'BotManagement', label: 'Bot Management', icon: '🤖' },
  { value: 'ProtocolEnforcement', label: 'Protocol Enforcement', icon: '📡' },
];

export const WAF_MATCH_TYPES = [
  { value: 'Any', label: 'Bất kỳ (OR)' },
  { value: 'All', label: 'Tất cả (AND)' },
];

// ─── Cloudflare Edge WAF (existing) ───
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

// ─── SSO (kept here for backward compatibility) ───
export interface SsoProvider {
  id: string;
  displayName: string;
  iconUrl: string;
  clientId: string;
  scopes: string;
}
