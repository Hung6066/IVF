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
  name: string;
  type: string;
  classification: string;
  owner: string;
  description?: string;
  location?: string;
  status: string;
  riskLevel?: string;
  lastReviewedAt?: string;
  nextReviewDate?: string;
  createdAt: string;
}

export interface CreateAssetRequest {
  name: string;
  type: string;
  classification: string;
  owner: string;
  description?: string;
  location?: string;
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
