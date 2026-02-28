export interface VaultStatus {
  isInitialized: boolean;
  activeKeyCount: number;
  keys: KeyInfo[];
}

export interface KeyInfo {
  keyName: string;
  serviceName: string;
  isActive: boolean;
  version: number;
  expiresAt: string | null;
  lastRotatedAt: string | null;
}

export interface ApiKeyResponse {
  id: string;
  keyName: string;
  serviceName: string;
  keyPrefix: string;
  isActive: boolean;
  environment: string;
  version: number;
  expiresAt: string | null;
  lastRotatedAt: string | null;
  createdAt: string;
}

export interface CreateApiKeyRequest {
  keyName: string;
  serviceName: string;
  keyPrefix: string;
  keyHash: string;
  environment: string;
  createdBy: string;
  rotationIntervalDays?: number;
}

export interface RotateKeyRequest {
  serviceName: string;
  keyName: string;
  newKeyHash: string;
  rotatedBy: string;
}

export interface InitializeVaultRequest {
  masterPassword: string;
  userId: string;
}

// ─── Secrets Management ───────────────────────────
export interface SecretEntry {
  name: string;
  type: 'folder' | 'secret';
}

export interface SecretDetail {
  name: string;
  value: string;
  retrievedAt: string;
}

export interface SecretCreateRequest {
  name: string;
  value: string;
}

// ─── Secret Templates ─────────────────────────────
export interface SecretTemplate {
  label: string;
  icon: string;
  path: string;
  data: string;
}

// ─── Key Wrap / Unwrap (Envelope Encryption) ──────
export interface WrapKeyRequest {
  plaintextBase64: string;
  keyName: string;
}

export interface WrappedKeyResult {
  wrappedKeyBase64: string;
  ivBase64: string;
  algorithm: string;
  keyName: string;
  keyVersion: number;
}

export interface UnwrapKeyRequest {
  wrappedKeyBase64: string;
  ivBase64: string;
  keyName: string;
}

export interface UnwrapKeyResult {
  plaintextBase64: string;
}

export interface EncryptDataRequest {
  plaintextBase64: string;
  purpose: KeyPurpose;
}

export interface EncryptedPayload {
  ciphertextBase64: string;
  ivBase64: string;
  purpose: KeyPurpose;
  algorithm: string;
}

export interface DecryptDataRequest {
  ciphertextBase64: string;
  ivBase64: string;
  purpose: KeyPurpose;
}

export interface DecryptDataResult {
  plaintextBase64: string;
}

export type KeyPurpose = 'Data' | 'Session' | 'Api' | 'Backup' | 'MasterSalt';

// ─── Auto-Unseal ──────────────────────────────────
export interface AutoUnsealStatus {
  isConfigured: boolean;
  keyVaultUrl: string | null;
  keyName: string | null;
  algorithm: string | null;
  configuredAt: string | null;
}

export interface ConfigureAutoUnsealRequest {
  masterPassword: string;
  azureKeyName: string;
}

// ─── Vault Settings ───────────────────────────────
export interface VaultSettings {
  azure: {
    vaultUrl: string;
    keyName: string;
    tenantId: string;
    clientId: string;
    hasClientSecret: boolean;
    enabled: boolean;
    fallbackToLocal: boolean;
    useManagedIdentity: boolean;
  };
  vault: Record<string, string>;
}

export interface SaveVaultSettingsRequest {
  [key: string]: string;
}

export interface TestConnectionResult {
  connected: boolean;
  message: string;
}

// ─── Vault Policies ───────────────────────────────
export interface VaultPolicy {
  id: string;
  name: string;
  description: string;
  pathPattern: string;
  capabilities: string[];
  createdAt?: string;
}

// ─── Vault User Policies ──────────────────────────
export interface VaultUserPolicy {
  id: string;
  userId: string;
  policyId: string;
  userName?: string;
  userEmail?: string;
  policyName?: string;
  grantedAt?: string;
}

// ─── Vault Leases ─────────────────────────────────
export interface VaultLease {
  id: string;
  leaseId: string;
  secretId: string;
  secretPath?: string;
  ttl: number;
  renewable: boolean;
  expiresAt: string;
  revoked: boolean;
  createdAt: string;
}

// ─── Dynamic Credentials ──────────────────────────
export interface DynamicCredential {
  id: string;
  leaseId: string;
  backend: string;
  username: string;
  dbHost: string;
  dbPort: number;
  dbName: string;
  expiresAt: string;
  revoked: boolean;
  createdAt: string;
  isExpired: boolean;
}

export interface DynamicCredentialCreateRequest {
  backend: string;
  username: string;
  dbHost: string;
  dbPort: number;
  dbName: string;
  adminUsername: string;
  adminPassword: string;
  ttlSeconds: number;
}

// ─── Vault Tokens ─────────────────────────────────
export interface VaultToken {
  id: string;
  accessor: string;
  displayName?: string;
  policies: string[];
  tokenType: string;
  ttl?: number;
  numUses?: number;
  usesCount: number;
  expiresAt?: string;
  revoked: boolean;
  createdAt: string;
  lastUsedAt?: string;
  isValid: boolean;
}

export interface TokenCreateRequest {
  displayName?: string;
  policies?: string[];
  tokenType?: string;
  ttl?: number;
  numUses?: number;
}

export interface TokenCreateResponse {
  success: boolean;
  id: string;
  accessor: string;
  token: string; // raw token, only shown once!
  expiresAt?: string;
}

// ─── Vault Audit Log ──────────────────────────────
export interface VaultAuditLog {
  id: string;
  action: string;
  resourceType?: string;
  resourceId?: string;
  userId?: string;
  details?: string;
  ipAddress?: string;
  createdAt: string;
}

export interface VaultAuditLogResponse {
  items: VaultAuditLog[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ─── Policy Create Request ────────────────────────
export interface PolicyCreateRequest {
  name: string;
  pathPattern: string;
  capabilities: string[];
  description?: string;
}

export interface UserPolicyAssignRequest {
  userId: string;
  policyId: string;
  grantedBy?: string;
}

// ─── Secret Import ───────────────────────────────
export interface SecretImportRequest {
  secrets: Record<string, string>;
  prefix?: string;
}

export interface SecretImportResult {
  imported: number;
  failed: number;
  errors: string[];
}

export const SECRET_TEMPLATES: SecretTemplate[] = [
  {
    label: 'Credentials',
    icon: '🔑',
    path: 'credentials',
    data: '{\n  "username": "",\n  "password": "",\n  "email": ""\n}',
  },
  {
    label: 'Config',
    icon: '⚙️',
    path: 'config',
    data: '{\n  "host": "",\n  "port": 3000,\n  "debug": false,\n  "environment": "production"\n}',
  },
  {
    label: 'Token',
    icon: '🎟️',
    path: 'token',
    data: '{\n  "access_token": "",\n  "refresh_token": "",\n  "expires_in": 3600\n}',
  },
  {
    label: 'SSH Key',
    icon: '🔐',
    path: 'ssh-key',
    data: '{\n  "private_key": "",\n  "public_key": "",\n  "passphrase": ""\n}',
  },
  {
    label: 'Certificate',
    icon: '📜',
    path: 'certificate',
    data: '{\n  "certificate": "",\n  "private_key": "",\n  "chain": "",\n  "expires_at": ""\n}',
  },
  {
    label: 'Env Vars',
    icon: '📦',
    path: 'env-vars',
    data: '{\n  "NODE_ENV": "production",\n  "SECRET_KEY": "",\n  "DATABASE_URL": ""\n}',
  },
  {
    label: 'Database',
    icon: '🗄️',
    path: 'database',
    data: '{\n  "host": "localhost",\n  "port": 5432,\n  "username": "",\n  "password": "",\n  "database": ""\n}',
  },
  {
    label: 'SMTP',
    icon: '📧',
    path: 'smtp',
    data: '{\n  "host": "smtp.example.com",\n  "port": 587,\n  "username": "",\n  "password": "",\n  "encryption": "tls"\n}',
  },
  {
    label: 'MinIO/S3',
    icon: '☁️',
    path: 'cloud/minio',
    data: '{\n  "endpoint": "",\n  "access_key": "",\n  "secret_key": "",\n  "bucket": "",\n  "use_ssl": true\n}',
  },
];

// ─── Encryption Config ────────────────────────────
export interface EncryptionConfigResponse {
  id: string;
  tableName: string;
  encryptedFields: string[];
  dekPurpose: string;
  isEnabled: boolean;
  isDefault: boolean;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface EncryptionConfigCreateRequest {
  tableName: string;
  encryptedFields: string[];
  dekPurpose: string;
  description?: string;
  isDefault?: boolean;
}

export interface EncryptionConfigUpdateRequest {
  encryptedFields: string[];
  dekPurpose: string;
  description?: string;
}

// ─── Field Access Policy ──────────────────────────
export type AccessLevel = 'full' | 'partial' | 'masked' | 'none';

export interface FieldAccessPolicyResponse {
  id: string;
  tableName: string;
  fieldName: string;
  role: string;
  accessLevel: AccessLevel;
  maskPattern: string;
  partialLength: number;
  description?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface FieldAccessPolicyCreateRequest {
  tableName: string;
  fieldName: string;
  role: string;
  accessLevel: AccessLevel;
  maskPattern?: string;
  partialLength?: number;
  description?: string;
}

export interface FieldAccessPolicyUpdateRequest {
  accessLevel: AccessLevel;
  maskPattern?: string;
  partialLength?: number;
  description?: string;
}

// ─── Security Dashboard ──────────────────────────
export interface SecurityDashboard {
  securityScore: number;
  vaultStatus: string;
  trustedDevices: number;
  recentAlerts: number;
  blockedAttempts: number;
  encryptionConfigCount: number;
  recentEvents: SecurityEvent[];
}

export interface SecurityEvent {
  id: string;
  action: string;
  resourceType?: string;
  ipAddress?: string;
  createdAt: string;
}

// ─── DB Schema Introspection ─────────────────────
export interface DbTableSchema {
  tableName: string;
  columns: DbColumnInfo[];
}

export interface DbColumnInfo {
  name: string;
  dataType: string;
}
