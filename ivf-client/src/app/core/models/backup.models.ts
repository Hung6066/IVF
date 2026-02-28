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
  totalPkiBackups: number;
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

// ─── PITR Restore models ─────────────────────────────────

export interface StartPitrRestoreRequest {
  baseBackupFile: string;
  targetTime?: string;
  dryRun?: boolean;
}

// ─── Cloud Replication models ───────────────────────────

export interface CloudReplicationConfig {
  id: string;
  dbReplicationEnabled: boolean;
  remoteDbHost?: string;
  remoteDbPort: number;
  remoteDbUser?: string;
  remoteDbPassword?: string;
  remoteDbSslMode: string;
  remoteDbSlotName?: string;
  remoteDbAllowedIps?: string;
  minioReplicationEnabled: boolean;
  remoteMinioEndpoint?: string;
  remoteMinioAccessKey?: string;
  remoteMinioSecretKey?: string;
  remoteMinioBucket: string;
  remoteMinioUseSsl: boolean;
  remoteMinioRegion: string;
  remoteMinioSyncMode: string;
  remoteMinioSyncCron?: string;
  lastDbSyncAt?: string;
  lastMinioSyncAt?: string;
  lastDbSyncStatus?: string;
  lastMinioSyncStatus?: string;
  lastMinioSyncBytes: number;
  lastMinioSyncFiles: number;
}

export interface UpdateDbReplicationRequest {
  enabled?: boolean;
  remoteHost?: string;
  remotePort?: number;
  remoteUser?: string;
  remotePassword?: string;
  sslMode?: string;
  slotName?: string;
  allowedIps?: string;
}

export interface UpdateMinioReplicationRequest {
  enabled?: boolean;
  endpoint?: string;
  accessKey?: string;
  secretKey?: string;
  bucket?: string;
  useSsl?: boolean;
  region?: string;
  syncMode?: string;
  syncCron?: string;
}

export interface CloudReplicationSetupResult {
  success: boolean;
  steps: string[];
  connectionInfo?: DbReplicationConnectionInfo;
}

export interface DbReplicationConnectionInfo {
  host: string;
  port: number;
  user: string;
  password: string;
  slotName: string;
  sslMode: string;
  primaryConnInfo: string;
  baseBackupCommand: string;
  standbySignalContent: string;
}

export interface DbCloudReplicationStatus {
  enabled: boolean;
  sslEnabled: boolean;
  currentLsn: string;
  slotName?: string;
  slotStatus?: string;
  slotRetainedBytes: number;
  externalReplicas: ExternalReplica[];
  localReplicas: ExternalReplica[];
  lastSyncAt?: string;
  lastSyncStatus?: string;
}

export interface ExternalReplica {
  pid: number;
  username: string;
  applicationName: string;
  clientAddress: string;
  state: string;
  sentLsn: string;
  replayLsn: string;
  syncState: string;
  uptimeSeconds: number;
  lagBytes: number;
  isExternal: boolean;
}

export interface MinioSyncResult {
  success: boolean;
  message: string;
  totalFiles: number;
  totalBytes: number;
  bucketResults: MinioBucketSyncResult[];
}

export interface MinioBucketSyncResult {
  bucket: string;
  success: boolean;
  filesSync: number;
  message: string;
}

export interface MinioCloudReplicationStatus {
  enabled: boolean;
  remoteEndpoint?: string;
  remoteBucket: string;
  useSsl: boolean;
  syncMode: string;
  syncCron?: string;
  localBuckets: MinioBucketInfo[];
  lastSyncAt?: string;
  lastSyncStatus?: string;
  lastSyncBytes: number;
  lastSyncFiles: number;
}

export interface MinioBucketInfo {
  name: string;
  sizeBytes: number;
  objectCount: number;
}

export interface ExternalReplicationGuide {
  steps: ReplicationSetupStep[];
  minioSteps: ReplicationSetupStep[];
  securityNotes: string[];
}

// ─── Certificate Authority & mTLS models ────────────────

export interface CaDashboard {
  totalCAs: number;
  activeCAs: number;
  totalCerts: number;
  activeCerts: number;
  expiringSoon: number;
  revokedCerts: number;
  expiringSoonList: CertListItem[];
  recentRenewals: CertListItem[];
}

export interface CaListItem {
  id: string;
  name: string;
  commonName: string;
  type: string; // 'Root' | 'Intermediate'
  status: string; // 'Active' | 'Revoked' | 'Expired'
  keyAlgorithm: string;
  keySize: number;
  fingerprint: string;
  notBefore: string;
  notAfter: string;
  parentCaId?: string;
  activeCertCount: number;
}

export interface CaDetail {
  id: string;
  name: string;
  commonName: string;
  organization: string;
  organizationalUnit?: string;
  country: string;
  state?: string;
  locality?: string;
  type: string;
  status: string;
  keyAlgorithm: string;
  keySize: number;
  fingerprint: string;
  notBefore: string;
  notAfter: string;
  parentCaId?: string;
  nextSerialNumber: number;
  certificatePem: string;
  chainPem?: string;
  issuedCerts: number;
}

export interface CreateCaRequest {
  name: string;
  commonName: string;
  organization?: string;
  orgUnit?: string;
  country?: string;
  state?: string;
  locality?: string;
  keySize?: number;
  validityDays?: number;
}

export interface CertListItem {
  id: string;
  commonName: string;
  subjectAltNames?: string;
  type: string; // 'Server' | 'Client'
  purpose: string;
  status: string; // 'Active' | 'Revoked' | 'Expired' | 'Superseded'
  fingerprint: string;
  serialNumber: string;
  notBefore: string;
  notAfter: string;
  issuingCaId: string;
  deployedTo?: string;
  deployedAt?: string;
  autoRenewEnabled: boolean;
  renewBeforeDays: number;
  replacedCertId?: string;
  replacedByCertId?: string;
  lastRenewalAttempt?: string;
  lastRenewalResult?: string;
  isExpiringSoon: boolean;
}

export interface IssueCertRequest {
  caId: string;
  commonName: string;
  subjectAltNames?: string;
  type: number;
  purpose: string;
  validityDays?: number;
  keySize?: number;
  renewBeforeDays?: number;
}

export interface CertBundle {
  certificatePem: string;
  privateKeyPem: string;
  caChainPem: string;
  commonName: string;
  purpose: string;
}

export interface CertDeployResult {
  success: boolean;
  steps: string[];
  operationId?: string;
}

export interface DeployLogLine {
  timestamp: string;
  level: string; // info, warn, error, success
  message: string;
}

export interface DeployLogItem {
  id: string;
  operationId: string;
  certificateId: string;
  target: string;
  container: string;
  remoteHost?: string;
  status: string; // Running, Completed, Failed
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  logLines: DeployLogLine[];
}

export interface CertRenewalResult {
  oldCertId: string;
  newCertId?: string;
  commonName: string;
  purpose: string;
  success: boolean;
  message: string;
}

export interface CertRenewalBatchResult {
  totalCandidates: number;
  renewedCount: number;
  results: CertRenewalResult[];
}

export interface DeployCertRequest {
  container: string;
  certPath: string;
  keyPath: string;
  caPath?: string;
}

// ─── Enterprise CA/mTLS Models ──────────────────────────

export interface CreateIntermediateCaRequest {
  parentCaId: string;
  name: string;
  commonName: string;
  organization?: string;
  orgUnit?: string;
  country?: string;
  state?: string;
  locality?: string;
  keySize?: number;
  validityDays?: number;
}

export interface CrlListItem {
  id: string;
  crlNumber: number;
  thisUpdate: string;
  nextUpdate: string;
  revokedCount: number;
  fingerprint: string;
}

export interface OcspResponse {
  status: string; // 'Good' | 'Revoked' | 'Unknown'
  serialNumber: string;
  revokedAt?: string;
  revocationReason?: string;
  producedAt: string;
}

export interface CertAuditItem {
  id: string;
  certificateId?: string;
  caId?: string;
  eventType: string;
  description: string;
  actor: string;
  sourceIp?: string;
  metadata?: string;
  success: boolean;
  errorMessage?: string;
  createdAt: string;
}

export type RevocationReason =
  | 'Unspecified'
  | 'KeyCompromise'
  | 'CaCompromise'
  | 'AffiliationChanged'
  | 'Superseded'
  | 'CessationOfOperation'
  | 'CertificateHold'
  | 'RemoveFromCrl'
  | 'PrivilegeWithdrawn'
  | 'AaCompromise';

export interface RevokeCertRequest {
  reason: RevocationReason;
}
