// ─── Digital Signing Models ─────────────────────────────────

export interface SigningDashboard {
  signing: SigningConfig;
  health: SigningHealthStatus;
  signServer: ServiceStatus;
  ejbca: ServiceStatus;
}

export interface SigningConfig {
  enabled: boolean;
  signServerUrl: string;
  ejbcaUrl: string;
  workerName: string;
  workerId: number | null;
  defaultReason: string;
  defaultLocation: string;
  defaultContactInfo: string;
  addVisibleSignature: boolean;
  visibleSignaturePage: number;
  timeoutSeconds: number;
  skipTlsValidation: boolean;
  hasClientCertificate: boolean;
}

export interface SigningHealthStatus {
  isHealthy: boolean;
  signServerReachable: boolean;
  workerConfigured: boolean;
  errorMessage: string | null;
  signServerVersion: string | null;
  certificateSubject: string | null;
  certificateExpiry: string | null;
}

export interface ServiceStatus {
  status: 'healthy' | 'unhealthy' | 'unreachable' | 'disabled';
  url: string;
  healthBody?: string;
  error?: string;
}

export interface ServiceHealthDetail {
  reachable: boolean;
  statusCode: number;
  healthy: boolean;
  body: string;
  url: string;
}

export interface SignServerWorker {
  id: number;
  name: string;
  status?: string;
  type?: string;
  tokenStatus?: string;
  signings?: number;
  error?: string;
  message?: string;
}

export interface EjbcaCA {
  id: number;
  name: string;
  subjectDn: string;
  issuerDn: string;
  status: string;
  expirationDate: string;
}

export interface TestSignResult {
  success: boolean;
  originalSize?: number;
  signedSize?: number;
  durationMs?: number;
  containsSignature?: boolean;
  error?: string;
  errorType?: string;
  timestamp: string;
}

// ─── User Signature Models ──────────────────────────────────

export interface UserSignature {
  id: string;
  userId: string;
  signatureImageBase64: string;
  imageMimeType: string;
  isActive: boolean;
  certificateSubject: string | null;
  certificateSerialNumber: string | null;
  certificateExpiry: string | null;
  workerName: string | null;
  certStatus: CertificateStatus;
  createdAt: string;
  updatedAt: string | null;
}

export interface UserSignatureListItem {
  id: string | null;
  userId: string;
  userFullName: string | null;
  userRole: string | null;
  userDepartment: string | null;
  isActive: boolean;
  certificateSubject: string | null;
  certificateSerialNumber: string | null;
  certificateExpiry: string | null;
  workerName: string | null;
  certStatus: CertificateStatus;
  hasSignatureImage: boolean;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface UserSignatureListResponse {
  items: UserSignatureListItem[];
  total: number;
}

export interface CertProvisionResult {
  success: boolean;
  certificateSubject?: string;
  workerName?: string;
  expiry?: string;
  message: string;
  error?: string;
}

export interface UserTestSignResult {
  success: boolean;
  workerName?: string;
  originalSize?: number;
  signedSize?: number;
  durationMs?: number;
  signer?: string;
  error?: string;
}

export type CertificateStatus = 'None' | 'Pending' | 'Active' | 'Expired' | 'Revoked' | 'Error';

// ─── EJBCA Management Models ────────────────────────────────

export interface EjbcaCertSearchRequest {
  subject?: string;
  issuer?: string;
  serialNumber?: string;
  status?: string;
  maxResults?: number;
}

export interface EjbcaCertificate {
  serial_number: string;
  subject_dn?: string;
  issuer_dn?: string;
  not_before?: string;
  not_after?: string;
  status?: string;
  fingerprint?: string;
  username?: string;
  subject_alt_name?: string;
  subject_key_id?: string;
  certificate_profile?: string;
  end_entity_profile?: string;
  cert_type?: string;
  revocation_reason?: string;
  revocation_date?: string;
  certificate?: string;
  response_format?: string;
  certificate_chain?: string[];
}

export interface EjbcaCertSearchResponse {
  certificates: EjbcaCertificate[];
  more_results: boolean;
  total_count?: number;
  error?: string;
}

export interface EjbcaRevokeResult {
  success: boolean;
  message?: string;
  error?: string;
  data?: string;
}

export interface EjbcaProfile {
  name: string;
  id?: number;
  description?: string;
}

export const EJBCA_CERT_STATUSES = [
  { value: 'CERT_ACTIVE', label: 'Hoạt động' },
  { value: 'CERT_REVOKED', label: 'Đã thu hồi' },
  { value: 'CERT_EXPIRED', label: 'Hết hạn' },
  { value: 'CERT_ARCHIVED', label: 'Đã lưu trữ' },
  { value: 'CERT_TEMP_REVOKED', label: 'Tạm thu hồi' },
  { value: 'CERT_NOTIFIEDABOUTEXPIRATION', label: 'Sắp hết hạn' },
] as const;

export const EJBCA_REVOKE_REASONS = [
  { value: 'UNSPECIFIED', label: 'Không xác định' },
  { value: 'KEY_COMPROMISE', label: 'Khóa bị xâm phạm' },
  { value: 'CA_COMPROMISE', label: 'CA bị xâm phạm' },
  { value: 'AFFILIATION_CHANGED', label: 'Thay đổi liên kết' },
  { value: 'SUPERSEDED', label: 'Đã thay thế' },
  { value: 'CESSATION_OF_OPERATION', label: 'Ngừng hoạt động' },
  { value: 'CERTIFICATE_HOLD', label: 'Tạm giữ' },
  { value: 'PRIVILEGES_WITHDRAWN', label: 'Thu hồi quyền' },
] as const;
