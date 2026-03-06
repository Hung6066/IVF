export interface VpsMetrics {
  hostname: string;
  os: string;
  cpuCount: number;
  cpuUsagePercent: number;
  memoryTotalBytes: number;
  memoryUsedBytes: number;
  memoryUsagePercent: number;
  disks: DiskInfo[];
  uptimeSeconds: number;
  collectedAt: string;
}

export interface DiskInfo {
  mountPoint: string;
  totalBytes: number;
  usedBytes: number;
  usagePercent: number;
  fileSystem: string;
}

export interface SwarmService {
  id: string;
  name: string;
  mode: string;
  runningReplicas: number;
  desiredReplicas: number;
  image: string;
  ports: string;
  status: string;
}

export interface SwarmNode {
  id: string;
  hostname: string;
  status: string;
  availability: string;
  managerStatus: string;
  engineVersion: string;
}

export interface InfraHealth {
  overallStatus: string;
  checks: HealthCheckItem[];
  checkedAt: string;
}

export interface HealthCheckItem {
  name: string;
  status: string;
  responseTime: string | null;
  error: string | null;
}

export interface InfraAlert {
  level: string;
  source: string;
  message: string;
  timestamp: string;
}

export interface S3Status {
  connected: boolean;
  providerName: string;
  buckets: S3BucketSummary[];
  totalObjects: number;
  totalSizeBytes: number;
  latestBackupAt: string | null;
}

export interface S3BucketSummary {
  prefix: string;
  objectCount: number;
  totalSizeBytes: number;
  latestModified: string;
}

export interface S3Object {
  key: string;
  fileName: string;
  sizeBytes: number;
  lastModified: string;
  eTag: string | null;
}

export interface ServiceScaleResult {
  success: boolean;
  message: string;
}

export interface ServiceTask {
  id: string;
  name: string;
  node: string;
  desiredState: string;
  currentState: string;
  error: string;
  ports: string;
}

export interface ServiceLogs {
  serviceName: string;
  lines: string[];
  fetchedAt: string;
}

export interface ServiceInspect {
  image: string;
  desiredReplicas: number;
  updateParallelism: number;
  updateDelay: string;
  failureAction: string;
  rollbackParallelism: number;
  constraints: string;
  createdAt: string;
  updatedAt: string;
  updateState: string;
  updateMessage: string;
}

export interface SwarmEvent {
  timestamp: string;
  type: string;
  action: string;
  name: string;
  actorId: string;
}

export interface HealingEvent {
  timestamp: string;
  type: string;
  target: string;
  action: string;
  result: string;
  message: string;
}

export interface S3UploadResult {
  success: boolean;
  objectKey: string | null;
  message: string;
}

export interface S3DownloadResult {
  success: boolean;
  fileName: string | null;
  sizeBytes: number;
  message: string;
}
