// ==================== COMPLIANCE DASHBOARD ====================

export interface ComplianceHealthDashboard {
  overallHealthScore: number;
  healthStatus: 'Healthy' | 'Warning' | 'Critical';
  gdpr: GdprMetrics;
  security: SecurityMetrics;
  tasks: TaskMetrics;
  training: TrainingMetrics;
  assets: AssetMetrics;
  ai: AiMetrics;
  alerts: ComplianceAlert[];
}

export interface GdprMetrics {
  dsrTotal: number;
  dsrPending: number;
  dsrOverdue: number;
  dsrComplianceRate: number;
  ropaActivities: number;
  breachesLast90Days: number;
}

export interface SecurityMetrics {
  totalIncidents: number;
  openIncidents: number;
  criticalIncidents: number;
  resolvedLast30Days: number;
  mttrHours: number;
}

export interface TaskMetrics {
  totalActive: number;
  overdue: number;
  upcoming: number;
  completedThisMonth: number;
  completionRate: number;
}

export interface TrainingMetrics {
  totalAssigned: number;
  completed: number;
  overdue: number;
  complianceRate: number;
}

export interface AssetMetrics {
  totalAssets: number;
  byClassification: Record<string, number>;
}

export interface AiMetrics {
  deployedModels: number;
  biasTestsRun: number;
  biasTestPassRate: number;
  modelsRequiringAttention: number;
}

export interface ComplianceAlert {
  severity: 'Critical' | 'High' | 'Medium' | 'Low';
  message: string;
  category: string;
}

// ==================== DATA SUBJECT REQUESTS ====================

export interface DataSubjectRequest {
  id: string;
  requestReference: string;
  patientId?: string;
  dataSubjectName: string;
  dataSubjectEmail: string;
  requestType: DsrType;
  status: DsrStatus;
  identityVerified: boolean;
  identityVerificationMethod?: string;
  identityVerifiedAt?: string;
  identityVerifiedBy?: string;
  receivedAt: string;
  deadline: string;
  extendedDeadline?: string;
  completedAt?: string;
  assignedTo?: string;
  responseSummary?: string;
  rejectionReason?: string;
  legalBasis?: string;
  notifiedDataSubject: boolean;
  notifiedAt?: string;
  escalatedToDpo: boolean;
  escalatedAt?: string;
  notes?: string;
  isOverdue: boolean;
  daysRemaining: number;
  createdAt: string;
  updatedAt?: string;
}

export type DsrType =
  | 'Access'
  | 'Rectification'
  | 'Erasure'
  | 'Restriction'
  | 'Portability'
  | 'Objection';
export type DsrStatus =
  | 'Received'
  | 'IdentityVerified'
  | 'InProgress'
  | 'EscalatedToDpo'
  | 'Completed'
  | 'Rejected';

export interface CreateDsrRequest {
  patientId?: string;
  dataSubjectName: string;
  dataSubjectEmail: string;
  requestType: DsrType;
  description?: string;
}

export interface DsrDashboard {
  total: number;
  pending: number;
  overdue: number;
  completedLast30Days: number;
  avgResponseDays: number;
  byType: Record<string, number>;
  byStatus: Record<string, number>;
}

// ==================== COMPLIANCE SCHEDULE ====================

export interface ComplianceScheduleTask {
  id: string;
  taskName: string;
  description: string;
  framework: string;
  frequency: ComplianceFrequency;
  category: string;
  owner: string;
  assignedUserId?: string;
  status: ScheduleStatus;
  lastCompletedAt?: string;
  lastCompletedBy?: string;
  lastCompletedNotes?: string;
  nextDueDate?: string;
  completionCount: number;
  priority: string;
  isOverdue: boolean;
  isUpcoming: boolean;
  createdAt: string;
}

export type ComplianceFrequency =
  | 'Daily'
  | 'Weekly'
  | 'Monthly'
  | 'Quarterly'
  | 'SemiAnnual'
  | 'Annual';
export type ScheduleStatus = 'Active' | 'Paused' | 'Completed';

export interface CreateScheduleRequest {
  taskName: string;
  description: string;
  framework: string;
  frequency: ComplianceFrequency;
  category: string;
  owner: string;
  nextDueDate: string;
  evidenceRequired: string;
  priority?: string;
}

export interface ScheduleDashboard {
  totalActive: number;
  overdue: number;
  upcoming: number;
  completedTotal: number;
  byFramework: Record<string, { total: number; overdue: number }>;
  byCategory: Record<string, number>;
}

// ==================== BREACH NOTIFICATIONS ====================

export interface BreachNotification {
  id: string;
  incidentId?: string;
  breachType: string;
  severity: string;
  status: string;
  detectedAt: string;
  containedAt?: string;
  dpaNotifiedAt?: string;
  dpaReference?: string;
  subjectsNotifiedAt?: string;
  affectedRecordCount?: number;
  rootCause?: string;
  attackVector?: string;
  createdAt: string;
}

// ==================== COMPLIANCE TRAINING ====================

export interface ComplianceTraining {
  id: string;
  userId: string;
  trainingType: string;
  trainingName: string;
  description?: string;
  assignedAt: string;
  dueDate: string;
  completedAt?: string;
  isCompleted: boolean;
  scorePercent?: number;
  isPassed?: boolean;
  passThreshold: number;
  certificateId?: string;
  expiresAt?: string;
  assignedBy?: string;
  createdAt: string;
}

export interface AssignTrainingRequest {
  userId: string;
  trainingType: string;
  trainingName: string;
  description?: string;
  dueDate: string;
  passThreshold?: number;
  assignedBy?: string;
}

// ==================== ASSET INVENTORY ====================

export interface AssetInventory {
  id: string;
  assetName: string;
  assetType: string;
  classification: string;
  owner: string;
  criticalityLevel: string;
  containsPhi: boolean;
  containsPii: boolean;
  department?: string;
  location?: string;
  environment?: string;
  version?: string;
  hasEncryption: boolean;
  hasBackup: boolean;
  hasAccessControl: boolean;
  hasMonitoring: boolean;
  status: string;
  lastAuditedAt?: string;
  nextAuditDueAt?: string;
  decommissionedAt?: string;
  createdAt: string;
}

export interface CreateAssetRequest {
  assetName: string;
  assetType: string;
  classification: string;
  owner: string;
  criticalityLevel: string;
  containsPhi: boolean;
  containsPii: boolean;
  department?: string;
  location?: string;
  environment?: string;
  version?: string;
  hasEncryption?: boolean;
  hasBackup?: boolean;
  hasAccessControl?: boolean;
  hasMonitoring?: boolean;
}

// ==================== AI GOVERNANCE ====================

export interface AiModelVersion {
  id: string;
  aiSystemName: string;
  modelVersion: string;
  previousVersion?: string;
  accuracy?: number;
  precision?: number;
  recall?: number;
  f1Score?: number;
  fpr?: number;
  fnr?: number;
  status: string;
  changeDescription?: string;
  changeReason?: string;
  approvedBy?: string;
  approvedAt?: string;
  deployedAt?: string;
  retiredAt?: string;
  biasTestPassed: boolean;
  gitCommitHash?: string;
  gitTag?: string;
  createdAt: string;
}

export interface AiBiasTestResult {
  id: string;
  aiSystemName: string;
  testType: string;
  protectedAttribute: string;
  protectedGroupValue: string;
  sampleSize: number;
  falsePositiveRate: number;
  falseNegativeRate: number;
  accuracy: number;
  precision: number;
  recall: number;
  f1Score: number;
  disparityRatioFpr?: number;
  disparityRatioFnr?: number;
  passesFairnessThreshold: boolean;
  fairnessThreshold: number;
  createdAt: string;
}

export interface AiPerformanceDashboard {
  deployedModels: AiModelPerformance[];
  alerts: AiPerformanceAlert[];
}

export interface AiModelPerformance {
  aiSystemName: string;
  currentVersion: string;
  accuracy?: number;
  precision?: number;
  recall?: number;
  f1Score?: number;
  fpr?: number;
  fnr?: number;
  biasTestPassed: boolean;
  deployedAt?: string;
}

export interface AiPerformanceAlert {
  aiSystemName: string;
  alertType: string;
  message: string;
}

// ==================== PROCESSING ACTIVITIES (ROPA) ====================

export interface ProcessingActivity {
  id: string;
  activityName: string;
  purpose: string;
  legalBasis: string;
  dataCategories: string;
  dataSubjectCategories: string;
  recipients?: string;
  crossBorderTransfers?: string;
  retentionPeriod?: string;
  securityMeasures?: string;
  dpiaRequired: boolean;
  dpiaCompletedAt?: string;
  status: string;
  createdAt: string;
}

// ==================== SECURITY TRENDS ====================

export interface SecurityTrend {
  month: string;
  incidentCount: number;
  criticalCount: number;
  resolvedCount: number;
  avgResponseHours: number;
}

// ==================== AUDIT READINESS ====================

export interface AuditReadiness {
  frameworks: FrameworkReadiness[];
  overallScore: number;
}

export interface FrameworkReadiness {
  framework: string;
  readinessScore: number;
  controlsTotal: number;
  controlsMet: number;
  controlsPartial: number;
  controlsGaps: number;
}

// ==================== PAGED RESULT ====================

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ==================== EVIDENCE EXPORT ====================

export interface EvidenceAccessControl {
  exportedAt: string;
  totalUsers: number;
  activeUsers: number;
  privilegedCount: number;
  roleDistribution: { role: string; count: number }[];
  users: EvidenceUser[];
  privilegedUsers: EvidenceUser[];
}

export interface EvidenceUser {
  id: string;
  username: string;
  fullName: string;
  role: string;
  department: string;
  isActive: boolean;
  createdAt: string;
}

export interface EvidenceTraining {
  exportedAt: string;
  totalAssigned: number;
  completed: number;
  overdue: number;
  byType: EvidenceTrainingType[];
  records: EvidenceTrainingRecord[];
}

export interface EvidenceTrainingType {
  type: string;
  total: number;
  completed: number;
  passed: number;
  overdue: number;
  completionRate: number;
}

export interface EvidenceTrainingRecord {
  id: string;
  userId: string;
  trainingType: string;
  trainingName: string;
  assignedAt: string;
  dueDate: string;
  isCompleted: boolean;
  completedAt: string | null;
  scorePercent: number;
  isPassed: boolean;
  isOverdue: boolean;
}

export interface EvidenceIncidents {
  exportedAt: string;
  incidents: {
    total: number;
    open: number;
    resolved: number;
    bySeverity: { severity: string; count: number }[];
    records: EvidenceIncidentRecord[];
  };
  breaches: {
    total: number;
    records: EvidenceBreachRecord[];
  };
}

export interface EvidenceIncidentRecord {
  id: string;
  incidentType: string;
  severity: string;
  status: string;
  detectedAt: string;
  resolvedAt: string | null;
  description: string;
  username: string;
}

export interface EvidenceBreachRecord {
  id: string;
  breachType: string;
  severity: string;
  status: string;
  detectedAt: string;
  affectedRecordCount: number;
  dpaNotified: boolean;
  subjectsNotified: boolean;
  deadlineAtRisk: boolean;
}

export interface EvidenceBackup {
  exportedAt: string;
  retentionPolicies: {
    id: string;
    entityType: string;
    retentionDays: number;
    isEnabled: boolean;
    lastExecutedAt: string | null;
  }[];
}

export interface EvidenceAssets {
  exportedAt: string;
  totalAssets: number;
  active: number;
  containingPhi: number;
  containingPii: number;
  records: {
    id: string;
    assetName: string;
    assetType: string;
    status: string;
    owner: string;
    department: string;
    containsPhi: boolean;
    containsPii: boolean;
    riskScore: number;
    lastAuditedAt: string | null;
    nextAuditDueAt: string | null;
    overdue: boolean;
  }[];
}

export interface EvidenceSummary {
  exportedAt: string;
  type: string;
  compliance: {
    overallScore: number;
    maxScore: number;
    percentage: number;
    grade: string;
    frameworks: EvidenceFramework[];
  };
}

export interface EvidenceFramework {
  name: string;
  score: number;
  maxScore: number;
  percentage: number;
  controls: {
    controlId: string;
    name: string;
    status: string;
    score: number;
    maxScore: number;
    finding: string;
    remediation: string;
  }[];
}

export type EvidenceCategory =
  | 'access-control'
  | 'training'
  | 'incidents'
  | 'backup'
  | 'assets'
  | 'summary';

// ─── Evidence Collection Script Runner ───

export interface EvidenceCollectRequest {
  categories?: string[];
  skipApi?: boolean;
}

export interface EvidenceCollectResult {
  operationId: string;
  status: string;
  totalCategories: number;
}

export interface EvidenceProgress {
  operationId: string;
  completed: number;
  total: number;
  percentage: number;
  currentCategory: string | null;
  completedCategories: string[];
}

export interface EvidenceLogLine {
  operationId: string;
  timestamp: string;
  level: 'INFO' | 'OK' | 'WARN' | 'ERROR' | 'HEADER';
  message: string;
}

export interface EvidenceFile {
  path: string;
  category: string;
  name: string;
  size: number;
  lastModified: string;
}

// ==================== COMPLIANCE AUDITOR (Vanta-style) ====================

export interface AuditDashboard {
  lastScanAt: string;
  overallScore: number;
  grade: string;
  totalControls: number;
  passedControls: number;
  failedControls: number;
  warningControls: number;
  frameworks: AuditFrameworkSummary[];
  alerts: AuditAlert[];
  trends: AuditTrend[];
  systemHealth: SystemHealth;
  lastScan: AuditScan;
}

export interface AuditFrameworkSummary {
  id: string;
  name: string;
  icon: string;
  score: number;
  passed: number;
  failed: number;
  warning: number;
  total: number;
}

export interface AuditAlert {
  severity: string;
  title: string;
  message: string;
  controlId: string;
  framework: string;
}

export interface AuditTrend {
  date: string;
  score: number;
  passed: number;
  failed: number;
}

export interface SystemHealth {
  databaseOnline: boolean;
  cacheOnline: boolean;
  storageOnline: boolean;
  authConfigured: boolean;
  encryptionConfigured: boolean;
  auditLoggingEnabled: boolean;
  backupConfigured: boolean;
  activeUsers: number;
  totalPatients: number;
  serverTime: string;
}

export interface AuditScan {
  scanId: string;
  startedAt: string;
  completedAt: string | null;
  status: string;
  overallScore: number;
  totalControls: number;
  passedControls: number;
  failedControls: number;
  warningControls: number;
  frameworks: AuditFramework[];
}

export interface AuditFramework {
  id: string;
  name: string;
  description: string;
  icon: string;
  score: number;
  totalControls: number;
  passedControls: number;
  failedControls: number;
  warningControls: number;
  controls: AuditControl[];
}

export interface AuditControl {
  id: string;
  frameworkId: string;
  category: string;
  name: string;
  description: string;
  severity: string;
  status: string;
  finding: string | null;
  remediation: string | null;
  evidence: string | null;
  testedAt: string;
  durationMs: number;
}
