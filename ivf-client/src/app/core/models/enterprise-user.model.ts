// ═══════════════════════════════════════════════════════════════
// Enterprise User Management Models
// Google/Amazon/Facebook-grade user management TypeScript interfaces
// ═══════════════════════════════════════════════════════════════

// ─── User Detail (Extended) ───
export interface UserDetail {
  id: string;
  username: string;
  fullName: string;
  role: string;
  department: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
  mfaEnabled: boolean;
  mfaMethod: string | null;
  passkeyCount: number;
  activeSessionCount: number;
  groups: string[];
  permissions: string[];
  groupPermissions: string[];
  loginSummary: UserLoginSummary;
}

export interface UserLoginSummary {
  totalLogins: number;
  failedLogins: number;
  suspiciousLogins: number;
  lastLoginAt: string | null;
  lastLoginIp: string | null;
  lastLoginCountry: string | null;
  lastFailedAt: string | null;
  avgRiskScore: number;
}

// ─── User Sessions ───
export interface UserSession {
  id: string;
  userId: string;
  ipAddress: string | null;
  country: string | null;
  city: string | null;
  deviceType: string | null;
  operatingSystem: string | null;
  browser: string | null;
  startedAt: string;
  expiresAt: string;
  lastActivityAt: string;
  isRevoked: boolean;
  revokedReason: string | null;
  revokedAt: string | null;
}

// ─── User Groups ───
export interface UserGroup {
  id: string;
  name: string;
  displayName: string | null;
  description: string | null;
  groupType: string;
  parentGroupId: string | null;
  isSystem: boolean;
  isActive: boolean;
  memberCount: number;
  permissionCount: number;
}

export interface UserGroupMember {
  id: string;
  userId: string;
  username: string;
  fullName: string;
  role: string;
  memberRole: string;
  joinedAt: string;
}

export interface UserGroupDetail {
  id: string;
  name: string;
  displayName: string | null;
  description: string | null;
  groupType: string;
  isSystem: boolean;
  isActive: boolean;
  members: UserGroupMember[];
  permissions: string[];
}

export interface UserGroupListResponse {
  items: UserGroup[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CreateUserGroupRequest {
  name: string;
  displayName?: string;
  description?: string;
  groupType: string;
  parentGroupId?: string | null;
}

export interface UpdateUserGroupRequest {
  id: string;
  name: string;
  displayName?: string;
  description?: string;
  groupType: string;
}

// ─── Login History ───
export interface UserLoginHistory {
  id: string;
  userId: string;
  loginMethod: string;
  isSuccess: boolean;
  failureReason: string | null;
  ipAddress: string | null;
  country: string | null;
  city: string | null;
  deviceType: string | null;
  operatingSystem: string | null;
  browser: string | null;
  riskScore: number | null;
  isSuspicious: boolean;
  riskFactors: string | null;
  sessionDuration: string | null;
  loginAt: string;
  logoutAt: string | null;
}

export interface LoginHistoryListResponse {
  items: UserLoginHistory[];
  total: number;
  page: number;
  pageSize: number;
}

// ─── User Analytics ───
export interface UserAnalytics {
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  mfaEnabledCount: number;
  passkeyCount: number;
  usersByRole: Record<string, number>;
  totalLogins24h: number;
  failedLogins24h: number;
  suspiciousLogins24h: number;
  activeSessions: number;
  totalGroups: number;
  loginTrend7Days: LoginTrend[];
  mostActiveUsers: TopUser[];
  highRiskUsers: RiskUser[];
}

export interface LoginTrend {
  date: string;
  successCount: number;
  failedCount: number;
  suspiciousCount: number;
}

export interface TopUser {
  userId: string;
  username: string;
  fullName: string;
  loginCount: number;
  lastLogin: string;
}

export interface RiskUser {
  userId: string;
  username: string;
  fullName: string;
  avgRiskScore: number;
  suspiciousCount: number;
}

// ─── Consent Management ───
export interface UserConsent {
  id: string;
  userId: string;
  consentType: string;
  isGranted: boolean;
  consentVersion: string | null;
  consentedAt: string;
  revokedAt: string | null;
  expiresAt: string | null;
}

export interface GrantConsentRequest {
  userId: string;
  consentType: string;
  consentVersion?: string;
  ipAddress?: string;
  userAgent?: string;
  expiresAt?: string;
}

// ─── Constants ───
export const GROUP_TYPES = [
  { value: 'team', label: 'Nhóm làm việc', icon: '👥' },
  { value: 'department', label: 'Phòng ban', icon: '🏢' },
  { value: 'role-group', label: 'Nhóm vai trò', icon: '🔐' },
  { value: 'custom', label: 'Tùy chỉnh', icon: '⚙️' },
];

export const CONSENT_TYPES = [
  { value: 'data_processing', label: 'Xử lý dữ liệu', icon: '📊' },
  { value: 'medical_records', label: 'Hồ sơ y tế', icon: '🏥' },
  { value: 'marketing', label: 'Tiếp thị', icon: '📧' },
  { value: 'analytics', label: 'Phân tích', icon: '📈' },
  { value: 'research', label: 'Nghiên cứu', icon: '🔬' },
  { value: 'third_party', label: 'Chia sẻ bên thứ ba', icon: '🤝' },
  { value: 'biometric_data', label: 'Dữ liệu sinh trắc', icon: '🔏' },
  { value: 'cookies', label: 'Cookie', icon: '🍪' },
];

export const LOGIN_METHOD_LABELS: Record<string, string> = {
  password: 'Mật khẩu',
  passkey: 'Passkey',
  mfa: 'MFA',
  sso: 'SSO',
  'api-key': 'API Key',
};

export const DEVICE_TYPE_ICONS: Record<string, string> = {
  Desktop: '🖥️',
  Mobile: '📱',
  Tablet: '📱',
  API: '🔗',
};

export const MEMBER_ROLES = [
  { value: 'owner', label: 'Chủ sở hữu' },
  { value: 'admin', label: 'Quản trị' },
  { value: 'member', label: 'Thành viên' },
];
