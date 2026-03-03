// ==================== PATIENT CORE MODELS ====================
export interface Patient {
  id: string;
  patientCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female';
  identityNumber?: string;
  phone?: string;
  email?: string;
  address?: string;
  patientType: PatientType;
  status: PatientStatus;
  // Demographics
  ethnicity?: string;
  nationality?: string;
  occupation?: string;
  insuranceNumber?: string;
  insuranceProvider?: string;
  bloodType?: BloodType;
  allergies?: string;
  // Emergency contact
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelation?: string;
  // Referral
  referralSource?: string;
  referringDoctorId?: string;
  medicalNotes?: string;
  // Consent & compliance
  consentDataProcessing: boolean;
  consentDataProcessingDate?: string;
  consentResearch: boolean;
  consentResearchDate?: string;
  consentMarketing: boolean;
  consentMarketingDate?: string;
  dataRetentionExpiryDate?: string;
  isAnonymized: boolean;
  // Risk & priority
  riskLevel: RiskLevel;
  riskNotes?: string;
  priority: PatientPriority;
  // Activity
  lastVisitDate?: string;
  totalVisits: number;
  tags?: string;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export type PatientType = 'Infertility' | 'EggDonor' | 'SpermDonor';
export type PatientStatus =
  | 'Active'
  | 'Inactive'
  | 'Discharged'
  | 'Transferred'
  | 'Deceased'
  | 'Anonymized'
  | 'Suspended';
export type PatientPriority = 'Normal' | 'High' | 'VIP' | 'Emergency';
export type RiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';
export type BloodType =
  | 'APositive'
  | 'ANegative'
  | 'BPositive'
  | 'BNegative'
  | 'ABPositive'
  | 'ABNegative'
  | 'OPositive'
  | 'ONegative';

export interface PatientListResponse {
  items: Patient[];
  totalCount: number;
  total: number; // alias for backward compat
  page: number;
  pageSize: number;
  totalPages: number;
}

// ==================== ADVANCED SEARCH ====================
export interface PatientAdvancedSearchParams {
  q?: string;
  gender?: string;
  patientType?: PatientType;
  status?: PatientStatus;
  priority?: PatientPriority;
  riskLevel?: RiskLevel;
  bloodType?: string;
  dobFrom?: string;
  dobTo?: string;
  createdFrom?: string;
  createdTo?: string;
  sortBy?: string;
  sortDesc?: boolean;
  page?: number;
  pageSize?: number;
}

// ==================== DEMOGRAPHICS ====================
export interface UpdateDemographicsRequest {
  id: string;
  email?: string;
  ethnicity?: string;
  nationality?: string;
  occupation?: string;
  insuranceNumber?: string;
  insuranceProvider?: string;
  bloodType?: BloodType;
  allergies?: string;
}

export interface UpdateEmergencyContactRequest {
  name?: string;
  phone?: string;
  relation?: string;
}

// ==================== CONSENT ====================
export interface UpdateConsentRequest {
  consentDataProcessing: boolean;
  consentResearch: boolean;
  consentMarketing: boolean;
}

// ==================== RISK ====================
export interface SetRiskRequest {
  riskLevel: RiskLevel;
  riskNotes?: string;
}

// ==================== ANALYTICS ====================
export interface PatientAnalytics {
  totalPatients: number;
  activePatients: number;
  inactivePatients: number;
  byGender: Record<string, number>;
  byType: Record<string, number>;
  byAgeGroup: Record<string, number>;
  byRiskLevel: Record<string, number>;
  registrationTrend: Record<string, number>;
  recentPatients: Patient[];
}

// ==================== AUDIT TRAIL ====================
export interface PatientAuditEntry {
  id: string;
  action: string;
  username?: string;
  oldValues?: string;
  newValues?: string;
  changedColumns?: string;
  ipAddress?: string;
  createdAt: string;
}

export interface PatientAuditTrail {
  patientId: string;
  entries: PatientAuditEntry[];
  totalCount: number;
}

// ==================== DISPLAY CONSTANTS ====================
export const PATIENT_TYPE_LABELS: Record<string, string> = {
  Infertility: 'Hiếm muộn',
  EggDonor: 'Cho trứng',
  SpermDonor: 'Cho tinh trùng',
};

export const PATIENT_STATUS_LABELS: Record<string, string> = {
  Active: 'Đang theo dõi',
  Inactive: 'Không hoạt động',
  Discharged: 'Xuất viện',
  Transferred: 'Chuyển viện',
  Deceased: 'Đã mất',
  Anonymized: 'Ẩn danh',
  Suspended: 'Tạm ngưng',
};

export const PATIENT_STATUS_COLORS: Record<string, string> = {
  Active: '#22c55e',
  Inactive: '#9ca3af',
  Discharged: '#3b82f6',
  Transferred: '#f59e0b',
  Deceased: '#ef4444',
  Anonymized: '#6b7280',
  Suspended: '#f97316',
};

export const PRIORITY_LABELS: Record<string, string> = {
  Normal: 'Bình thường',
  High: 'Cao',
  VIP: 'VIP',
  Emergency: 'Cấp cứu',
};

export const PRIORITY_COLORS: Record<string, string> = {
  Normal: '#6b7280',
  High: '#f59e0b',
  VIP: '#8b5cf6',
  Emergency: '#ef4444',
};

export const RISK_LEVEL_LABELS: Record<string, string> = {
  Low: 'Thấp',
  Medium: 'Trung bình',
  High: 'Cao',
  Critical: 'Nguy hiểm',
};

export const RISK_LEVEL_COLORS: Record<string, string> = {
  Low: '#22c55e',
  Medium: '#f59e0b',
  High: '#ef4444',
  Critical: '#dc2626',
};

export const BLOOD_TYPE_LABELS: Record<string, string> = {
  APositive: 'A+',
  ANegative: 'A-',
  BPositive: 'B+',
  BNegative: 'B-',
  ABPositive: 'AB+',
  ABNegative: 'AB-',
  OPositive: 'O+',
  ONegative: 'O-',
};
