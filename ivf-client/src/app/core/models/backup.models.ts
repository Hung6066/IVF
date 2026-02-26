export interface BackupInfo {
  fileName: string;
  fullPath: string;
  sizeBytes: number;
  createdAt: string;
}

export interface BackupLogLine {
  timestamp: string;
  level: 'INFO' | 'OK' | 'WARN' | 'ERROR';
  message: string;
}

export type BackupOperationType = 'Backup' | 'Restore';
export type BackupOperationStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export interface BackupOperation {
  id: string;
  type: BackupOperationType;
  status: BackupOperationStatus;
  startedAt: string;
  completedAt?: string;
  archivePath?: string;
  errorMessage?: string;
  startedBy?: string;
  logLineCount?: number;
  logLines?: BackupLogLine[];
}

export interface BackupSchedule {
  enabled: boolean;
  cronExpression: string;
  keysOnly: boolean;
  retentionDays: number;
  maxBackupCount: number;
  cloudSyncEnabled: boolean;
  nextScheduledRun?: string;
  lastScheduledRun?: string;
  lastScheduledOperationId?: string;
}

export interface UpdateScheduleRequest {
  enabled?: boolean;
  cronExpression?: string;
  keysOnly?: boolean;
  retentionDays?: number;
  maxBackupCount?: number;
  cloudSyncEnabled?: boolean;
}

// ─── Cloud backup models ────────────────────────────────

export interface CloudBackupObject {
  objectKey: string;
  fileName: string;
  sizeBytes: number;
  lastModified: string;
  eTag?: string;
}

export interface CloudUploadResult {
  objectKey: string;
  cloudSizeBytes: number;
  eTag?: string;
  providerName: string;
  originalSizeBytes: number;
  compressedSizeBytes?: number;
  compressionRatioPercent?: number;
  compressionDurationMs?: number;
}

export interface CloudStatusResult {
  providerName: string;
  connected: boolean;
  compressionEnabled: boolean;
  backupCount: number;
  totalSizeBytes: number;
}

// ─── Cloud config models ────────────────────────────────

export type CloudProvider = 'MinIO' | 'S3' | 'Azure' | 'GCS';

export interface CloudConfig {
  provider: CloudProvider;
  compressionEnabled: boolean;
  s3Region: string;
  s3BucketName: string;
  s3AccessKey?: string;
  s3SecretKey?: string;
  s3ServiceUrl?: string;
  s3ForcePathStyle: boolean;
  azureConnectionString?: string;
  azureContainerName: string;
  gcsProjectId?: string;
  gcsBucketName: string;
  gcsCredentialsPath?: string;
}

export interface UpdateCloudConfigRequest {
  provider?: string;
  compressionEnabled?: boolean;
  s3Region?: string;
  s3BucketName?: string;
  s3AccessKey?: string;
  s3SecretKey?: string;
  s3ServiceUrl?: string;
  s3ForcePathStyle?: boolean;
  azureConnectionString?: string;
  azureContainerName?: string;
  gcsProjectId?: string;
  gcsBucketName?: string;
  gcsCredentialsPath?: string;
}

export interface TestCloudConfigRequest extends UpdateCloudConfigRequest {}

export interface TestCloudResult {
  connected: boolean;
  provider: string;
}

// ─── Data backup models (DB + MinIO) ────────────────────

export interface DatabaseInfo {
  databaseName: string;
  sizeBytes: number;
  tableCount: number;
  connected: boolean;
}

export interface BucketInfo {
  name: string;
  objectCount: number;
  sizeBytes: number;
}

export interface MinioStorageInfo {
  totalObjects: number;
  totalSizeBytes: number;
  connected: boolean;
  buckets: BucketInfo[];
}

export interface DataBackupFile {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
  checksum?: string;
}

export interface DataBackupStatus {
  database: DatabaseInfo;
  minio: MinioStorageInfo;
  backups: {
    databaseBackups: DataBackupFile[];
    minioBackups: DataBackupFile[];
  };
}

export interface StartDataBackupRequest {
  includeDatabase?: boolean;
  includeMinio?: boolean;
  uploadToCloud?: boolean;
}

export interface StartDataRestoreRequest {
  databaseBackupFile?: string;
  minioBackupFile?: string;
}

// ─── Data Backup Strategy models ────────────────────────

export interface DataBackupStrategy {
  id: string;
  name: string;
  description?: string;
  enabled: boolean;
  includeDatabase: boolean;
  includeMinio: boolean;
  cronExpression: string;
  uploadToCloud: boolean;
  retentionDays: number;
  maxBackupCount: number;
  lastRunAt?: string;
  lastRunOperationCode?: string;
  lastRunStatus?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateDataBackupStrategyRequest {
  name: string;
  description?: string;
  includeDatabase?: boolean;
  includeMinio?: boolean;
  cronExpression?: string;
  uploadToCloud?: boolean;
  retentionDays?: number;
  maxBackupCount?: number;
}

export interface UpdateDataBackupStrategyRequest {
  name?: string;
  description?: string;
  enabled?: boolean;
  includeDatabase?: boolean;
  includeMinio?: boolean;
  cronExpression?: string;
  uploadToCloud?: boolean;
  retentionDays?: number;
  maxBackupCount?: number;
}

// ─── Backup validation models ───────────────────────────

export interface BackupValidationResult {
  type: 'database' | 'minio';
  isValid: boolean;
  error?: string;
  checksum?: string;
  tableCount?: number;
  rowCount?: number;
  bucketCount?: number;
  entryCount?: number;
}

// ─── 3-2-1 Backup Compliance models ─────────────────────

export interface ComplianceCheck {
  id: string;
  label: string;
  passed: boolean;
  detail: string;
}

export interface ComplianceSummary {
  totalDbBackups: number;
  totalMinioBackups: number;
  totalBaseBackups: number;
  latestBackupTime?: string;
  walArchivingEnabled: boolean;
  replicationActive: boolean;
  cloudConfigured: boolean;
  replicaCount: number;
}

export interface ComplianceReport {
  isCompliant: boolean;
  ruleScore: number;
  maxRuleScore: number;
  bonusScore: number;
  maxBonusScore: number;
  copiesCount: number;
  storageTypesCount: number;
  offsiteCopiesCount: number;
  checks: ComplianceCheck[];
  recommendations: string[];
  summary: ComplianceSummary;
}

// ─── WAL models ─────────────────────────────────────────

export interface WalStatus {
  walLevel: string;
  archiveMode: string;
  archiveCommand: string;
  archiveTimeout: string;
  walSegmentSize: string;
  currentLsn: string;
  walBytesWritten: number;
  lastArchivedWal?: string;
  lastArchivedTime?: string;
  lastFailedWal?: string;
  lastFailedTime?: string;
  archivedCount: number;
  failedCount: number;
  isArchivingEnabled: boolean;
  isReplicaLevel: boolean;
}

export interface WalArchiveInfo {
  fileCount: number;
  totalSizeBytes: number;
  latestFile?: string;
}

export interface WalStatusResponse {
  wal: WalStatus;
  archive: WalArchiveInfo;
}

// ─── Replication models ─────────────────────────────────

export interface ConnectedReplica {
  pid: number;
  username: string;
  applicationName: string;
  clientAddress: string;
  state: string;
  sentLsn: string;
  writeLsn: string;
  flushLsn: string;
  replayLsn: string;
  syncState: string;
  uptimeSeconds: number;
  lagBytes: number;
}

export interface ReplicationSlot {
  slotName: string;
  slotType: string;
  active: boolean;
  restartLsn: string;
  confirmedFlushLsn: string;
  retainedBytes: number;
}

export interface ReplicationStatus {
  serverRole: string;
  isReplicating: boolean;
  currentLsn: string;
  maxWalSenders: number;
  maxReplicationSlots: number;
  synchronousStandbyNames: string;
  connectedReplicas: ConnectedReplica[];
  replicationSlots: ReplicationSlot[];
}

export interface ReplicationSetupStep {
  title: string;
  description: string;
  command: string;
}

export interface ReplicationSetupGuide {
  steps: ReplicationSetupStep[];
  dockerComposeExample: string;
}

// ─── WAL Archive listing ────────────────────────────────

export interface WalArchiveFile {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
  checksum?: string;
}

export interface WalArchiveListResponse {
  files: WalArchiveFile[];
  totalCount: number;
  totalSizeBytes: number;
}

// ─── Replication activation ─────────────────────────────

export interface ReplicationActivationResult {
  success: boolean;
  steps: string[];
  nextAction: string;
}
