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
