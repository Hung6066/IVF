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
