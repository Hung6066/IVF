import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import {
  BackupInfo,
  BackupLogLine,
  BackupOperation,
  BackupSchedule,
  BackupValidationResult,
  CaDetail,
  CaDashboard,
  CaListItem,
  CertBundle,
  CertDeployResult,
  CertListItem,
  CertRenewalBatchResult,
  CloudBackupObject,
  CloudConfig,
  CloudReplicationConfig,
  CloudReplicationSetupResult,
  CloudStatusResult,
  CloudUploadResult,
  ComplianceReport,
  CreateCaRequest,
  CreateDataBackupStrategyRequest,
  DataBackupFile,
  DataBackupStatus,
  DataBackupStrategy,
  DbCloudReplicationStatus,
  DeployCertRequest,
  DeployLogItem,
  ExternalReplicationGuide,
  IssueCertRequest,
  MinioCloudReplicationStatus,
  MinioSyncResult,
  ReplicationActivationResult,
  ReplicationSetupGuide,
  ReplicationStatus,
  StartDataBackupRequest,
  StartDataRestoreRequest,
  StartPitrRestoreRequest,
  TestCloudConfigRequest,
  TestCloudResult,
  UpdateCloudConfigRequest,
  UpdateDataBackupStrategyRequest,
  UpdateDbReplicationRequest,
  UpdateMinioReplicationRequest,
  UpdateScheduleRequest,
  WalArchiveListResponse,
  WalStatusResponse,
} from '../models/backup.models';

@Injectable({ providedIn: 'root' })
export class BackupService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/backup`;
  private readonly dataUrl = `${environment.apiUrl}/admin/data-backup`;
  private readonly hubUrl = environment.apiUrl.replace('/api', '/hubs/backup');

  private hubConnection?: signalR.HubConnection;
  private logLineSubject = new Subject<{ operationId: string } & BackupLogLine>();
  private statusSubject = new Subject<BackupOperation>();

  logLine$ = this.logLineSubject.asObservable();
  statusChanged$ = this.statusSubject.asObservable();

  // ─── REST API ─────────────────────────────────────────

  listArchives(): Observable<BackupInfo[]> {
    return this.http.get<BackupInfo[]>(`${this.baseUrl}/archives`);
  }

  startBackup(keysOnly = false): Observable<{ operationId: string }> {
    return this.http.post<{ operationId: string }>(`${this.baseUrl}/start`, { keysOnly });
  }

  startRestore(
    archiveFileName: string,
    keysOnly = false,
    dryRun = false,
  ): Observable<{ operationId: string }> {
    return this.http.post<{ operationId: string }>(`${this.baseUrl}/restore`, {
      archiveFileName,
      keysOnly,
      dryRun,
    });
  }

  getOperation(operationId: string): Observable<BackupOperation> {
    return this.http.get<BackupOperation>(`${this.baseUrl}/operations/${operationId}`);
  }

  listOperations(): Observable<BackupOperation[]> {
    return this.http.get<BackupOperation[]>(`${this.baseUrl}/operations`);
  }

  cancelOperation(operationId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/operations/${operationId}/cancel`,
      {},
    );
  }

  getSchedule(): Observable<BackupSchedule> {
    return this.http.get<BackupSchedule>(`${this.baseUrl}/schedule`);
  }

  updateSchedule(
    request: UpdateScheduleRequest,
  ): Observable<{ message: string; options: BackupSchedule }> {
    return this.http.put<{ message: string; options: BackupSchedule }>(
      `${this.baseUrl}/schedule`,
      request,
    );
  }

  runCleanup(): Observable<{ deletedCount: number; deletedFiles: string[] }> {
    return this.http.post<{ deletedCount: number; deletedFiles: string[] }>(
      `${this.baseUrl}/cleanup`,
      {},
    );
  }

  // ─── Cloud API ────────────────────────────────────────

  getCloudStatus(): Observable<CloudStatusResult> {
    return this.http.get<CloudStatusResult>(`${this.baseUrl}/cloud/status`);
  }

  listCloudBackups(): Observable<CloudBackupObject[]> {
    return this.http.get<CloudBackupObject[]>(`${this.baseUrl}/cloud/list`);
  }

  uploadToCloud(archiveFileName: string): Observable<CloudUploadResult> {
    return this.http.post<CloudUploadResult>(`${this.baseUrl}/cloud/upload`, { archiveFileName });
  }

  downloadFromCloud(objectKey: string): Observable<{ fileName: string; message: string }> {
    return this.http.post<{ fileName: string; message: string }>(`${this.baseUrl}/cloud/download`, {
      objectKey,
    });
  }

  deleteFromCloud(objectKey: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.baseUrl}/cloud/${encodeURIComponent(objectKey)}`,
    );
  }

  // ─── Cloud Config API ─────────────────────────────────

  getCloudConfig(): Observable<CloudConfig> {
    return this.http.get<CloudConfig>(`${this.baseUrl}/cloud/config`);
  }

  updateCloudConfig(
    request: UpdateCloudConfigRequest,
  ): Observable<{ message: string; provider: string }> {
    return this.http.put<{ message: string; provider: string }>(
      `${this.baseUrl}/cloud/config`,
      request,
    );
  }

  testCloudConfig(request: TestCloudConfigRequest): Observable<TestCloudResult> {
    return this.http.post<TestCloudResult>(`${this.baseUrl}/cloud/config/test`, request);
  }

  // ─── Data Backup API ─────────────────────────────────

  getDataBackupStatus(): Observable<DataBackupStatus> {
    return this.http.get<DataBackupStatus>(`${this.dataUrl}/status`);
  }

  startDataBackup(request: StartDataBackupRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.dataUrl}/start`, request);
  }

  startDataRestore(request: StartDataRestoreRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.dataUrl}/restore`, request);
  }

  deleteDataBackup(fileName: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.dataUrl}/${encodeURIComponent(fileName)}`);
  }

  validateBackup(fileName: string): Observable<BackupValidationResult> {
    return this.http.post<BackupValidationResult>(`${this.dataUrl}/validate`, { fileName });
  }

  // ─── Data Backup Strategy API ──────────────────────────

  listStrategies(): Observable<DataBackupStrategy[]> {
    return this.http.get<DataBackupStrategy[]>(`${this.dataUrl}/strategies`);
  }

  getStrategy(id: string): Observable<DataBackupStrategy> {
    return this.http.get<DataBackupStrategy>(`${this.dataUrl}/strategies/${id}`);
  }

  createStrategy(
    request: CreateDataBackupStrategyRequest,
  ): Observable<{ id: string; message: string }> {
    return this.http.post<{ id: string; message: string }>(`${this.dataUrl}/strategies`, request);
  }

  updateStrategy(
    id: string,
    request: UpdateDataBackupStrategyRequest,
  ): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.dataUrl}/strategies/${id}`, request);
  }

  deleteStrategy(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.dataUrl}/strategies/${id}`);
  }

  runStrategy(id: string): Observable<{ operationId: string; message: string }> {
    return this.http.post<{ operationId: string; message: string }>(
      `${this.dataUrl}/strategies/${id}/run`,
      {},
    );
  }

  // ─── 3-2-1 Compliance API ─────────────────────────────

  getCompliance(): Observable<ComplianceReport> {
    return this.http.get<ComplianceReport>(`${this.dataUrl}/compliance`);
  }

  // ─── WAL API ──────────────────────────────────────────

  getWalStatus(): Observable<WalStatusResponse> {
    return this.http.get<WalStatusResponse>(`${this.dataUrl}/wal/status`);
  }

  enableWalArchiving(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.dataUrl}/wal/enable`, {});
  }

  switchWal(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.dataUrl}/wal/switch`, {});
  }

  createBaseBackup(): Observable<{ fileName: string; sizeBytes: number; message: string }> {
    return this.http.post<{ fileName: string; sizeBytes: number; message: string }>(
      `${this.dataUrl}/wal/base-backup`,
      {},
    );
  }

  listBaseBackups(): Observable<DataBackupFile[]> {
    return this.http.get<DataBackupFile[]>(`${this.dataUrl}/wal/base-backups`);
  }

  // ─── Replication API ──────────────────────────────────

  getReplicationStatus(): Observable<ReplicationStatus> {
    return this.http.get<ReplicationStatus>(`${this.dataUrl}/replication/status`);
  }

  getReplicationGuide(): Observable<ReplicationSetupGuide> {
    return this.http.get<ReplicationSetupGuide>(`${this.dataUrl}/replication/guide`);
  }

  createReplicationSlot(slotName: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.dataUrl}/replication/slots`, { slotName });
  }

  dropReplicationSlot(slotName: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.dataUrl}/replication/slots/${encodeURIComponent(slotName)}`,
    );
  }

  activateReplication(): Observable<ReplicationActivationResult> {
    return this.http.post<ReplicationActivationResult>(`${this.dataUrl}/replication/activate`, {});
  }

  // ─── WAL Archive Listing ──────────────────────────────

  listWalArchives(): Observable<WalArchiveListResponse> {
    return this.http.get<WalArchiveListResponse>(`${this.dataUrl}/wal/archives`);
  }

  // ─── PITR Restore ─────────────────────────────────────

  startPitrRestore(request: StartPitrRestoreRequest): Observable<{ operationId: string }> {
    return this.http.post<{ operationId: string }>(`${this.dataUrl}/pitr-restore`, request);
  }

  // ─── Cloud Replication API ────────────────────────────

  private readonly cloudReplUrl = `${this.dataUrl}/cloud-replication`;

  getCloudReplicationConfig(): Observable<CloudReplicationConfig> {
    return this.http.get<CloudReplicationConfig>(`${this.cloudReplUrl}/config`);
  }

  updateDbReplicationConfig(
    request: UpdateDbReplicationRequest,
  ): Observable<CloudReplicationConfig> {
    return this.http.put<CloudReplicationConfig>(`${this.cloudReplUrl}/db/config`, request);
  }

  testDbReplicationConnection(): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.cloudReplUrl}/db/test`,
      {},
    );
  }

  setupDbReplication(): Observable<CloudReplicationSetupResult> {
    return this.http.post<CloudReplicationSetupResult>(`${this.cloudReplUrl}/db/setup`, {});
  }

  getDbReplicationStatus(): Observable<DbCloudReplicationStatus> {
    return this.http.get<DbCloudReplicationStatus>(`${this.cloudReplUrl}/db/status`);
  }

  updateMinioReplicationConfig(
    request: UpdateMinioReplicationRequest,
  ): Observable<CloudReplicationConfig> {
    return this.http.put<CloudReplicationConfig>(`${this.cloudReplUrl}/minio/config`, request);
  }

  testMinioReplicationConnection(): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.cloudReplUrl}/minio/test`,
      {},
    );
  }

  setupMinioReplication(): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.cloudReplUrl}/minio/setup`,
      {},
    );
  }

  syncMinioNow(): Observable<MinioSyncResult> {
    return this.http.post<MinioSyncResult>(`${this.cloudReplUrl}/minio/sync`, {});
  }

  getMinioReplicationStatus(): Observable<MinioCloudReplicationStatus> {
    return this.http.get<MinioCloudReplicationStatus>(`${this.cloudReplUrl}/minio/status`);
  }

  getCloudReplicationGuide(): Observable<ExternalReplicationGuide> {
    return this.http.get<ExternalReplicationGuide>(`${this.cloudReplUrl}/guide`);
  }

  // ─── Certificate Authority API ────────────────────────

  private readonly certUrl = `${environment.apiUrl}/admin/certificates`;

  getCaDashboard(): Observable<CaDashboard> {
    return this.http.get<CaDashboard>(`${this.certUrl}/dashboard`);
  }

  listCAs(): Observable<CaListItem[]> {
    return this.http.get<CaListItem[]>(`${this.certUrl}/ca`);
  }

  getCA(id: string): Observable<CaDetail> {
    return this.http.get<CaDetail>(`${this.certUrl}/ca/${id}`);
  }

  createRootCA(
    req: CreateCaRequest,
  ): Observable<{ id: string; name: string; fingerprint: string }> {
    return this.http.post<{ id: string; name: string; fingerprint: string }>(
      `${this.certUrl}/ca`,
      req,
    );
  }

  downloadCaChain(id: string): Observable<string> {
    return this.http.get(`${this.certUrl}/ca/${id}/chain`, { responseType: 'text' });
  }

  listCertificates(caId?: string): Observable<CertListItem[]> {
    const params = caId ? `?caId=${caId}` : '';
    return this.http.get<CertListItem[]>(`${this.certUrl}/certs${params}`);
  }

  issueCertificate(req: IssueCertRequest): Observable<any> {
    return this.http.post(`${this.certUrl}/certs`, req);
  }

  getCertBundle(id: string): Observable<CertBundle> {
    return this.http.get<CertBundle>(`${this.certUrl}/certs/${id}/bundle`);
  }

  renewCertificate(id: string): Observable<any> {
    return this.http.post(`${this.certUrl}/certs/${id}/renew`, {});
  }

  setAutoRenew(id: string, enabled: boolean, renewBeforeDays?: number): Observable<void> {
    return this.http.put<void>(`${this.certUrl}/certs/${id}/auto-renew`, {
      enabled,
      renewBeforeDays,
    });
  }

  getExpiringCertificates(): Observable<CertListItem[]> {
    return this.http.get<CertListItem[]>(`${this.certUrl}/certs/expiring`);
  }

  triggerAutoRenewal(): Observable<CertRenewalBatchResult> {
    return this.http.post<CertRenewalBatchResult>(`${this.certUrl}/certs/auto-renew-now`, {});
  }

  revokeCertificate(id: string, reason?: string): Observable<void> {
    return this.http.post<void>(`${this.certUrl}/certs/${id}/revoke`, {
      reason: reason || 'Unspecified',
    });
  }

  // ─── Intermediate CA ───────────────────────────────────

  createIntermediateCA(
    req: any,
  ): Observable<{
    id: string;
    name: string;
    fingerprint: string;
    parentCaId: string;
    type: string;
  }> {
    return this.http.post<{
      id: string;
      name: string;
      fingerprint: string;
      parentCaId: string;
      type: string;
    }>(`${this.certUrl}/ca/intermediate`, req);
  }

  // ─── CRL (Certificate Revocation List) ────────────────

  generateCrl(
    caId: string,
  ): Observable<{
    id: string;
    crlNumber: number;
    thisUpdate: string;
    nextUpdate: string;
    revokedCount: number;
  }> {
    return this.http.post<any>(`${this.certUrl}/ca/${caId}/crl/generate`, {});
  }

  listCrls(caId: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.certUrl}/ca/${caId}/crl`);
  }

  downloadLatestCrl(caId: string): Observable<string> {
    return this.http.get(`${this.certUrl}/ca/${caId}/crl/latest`, { responseType: 'text' });
  }

  // ─── OCSP ─────────────────────────────────────────────

  checkCertStatus(caId: string, serialNumber: string): Observable<any> {
    return this.http.get<any>(`${this.certUrl}/ocsp/${caId}/${serialNumber}`);
  }

  // ─── Audit Trail ──────────────────────────────────────

  listCertAuditEvents(
    certId?: string,
    caId?: string,
    eventType?: string,
    limit?: number,
  ): Observable<any[]> {
    const params: string[] = [];
    if (certId) params.push(`certId=${certId}`);
    if (caId) params.push(`caId=${caId}`);
    if (eventType) params.push(`eventType=${eventType}`);
    if (limit) params.push(`limit=${limit}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.http.get<any[]>(`${this.certUrl}/audit${qs}`);
  }

  deployCertificate(id: string, req: DeployCertRequest): Observable<CertDeployResult> {
    return this.http.post<CertDeployResult>(`${this.certUrl}/certs/${id}/deploy`, req);
  }

  deployPgSsl(id: string): Observable<CertDeployResult> {
    return this.http.post<CertDeployResult>(`${this.certUrl}/certs/${id}/deploy-pg`, {});
  }

  deployMinioSsl(id: string): Observable<CertDeployResult> {
    return this.http.post<CertDeployResult>(`${this.certUrl}/certs/${id}/deploy-minio`, {});
  }

  deployReplicaPgSsl(id: string): Observable<CertDeployResult> {
    return this.http.post<CertDeployResult>(`${this.certUrl}/certs/${id}/deploy-replica-pg`, {});
  }

  deployReplicaMinioSsl(id: string): Observable<CertDeployResult> {
    return this.http.post<CertDeployResult>(`${this.certUrl}/certs/${id}/deploy-replica-minio`, {});
  }

  // Deploy logs
  listDeployLogs(certId?: string, limit?: number): Observable<DeployLogItem[]> {
    const params: string[] = [];
    if (certId) params.push(`certId=${certId}`);
    if (limit) params.push(`limit=${limit}`);
    const qs = params.length ? `?${params.join('&')}` : '';
    return this.http.get<DeployLogItem[]>(`${this.certUrl}/deploy-logs${qs}`);
  }

  getDeployLog(operationId: string): Observable<DeployLogItem> {
    return this.http.get<DeployLogItem>(`${this.certUrl}/deploy-logs/${operationId}`);
  }

  // Deploy SignalR connection
  private deployLogSubject = new Subject<{
    operationId: string;
    timestamp: string;
    level: string;
    message: string;
  }>();
  private deployStatusSubject = new Subject<{
    operationId: string;
    status: string;
    error?: string;
  }>();
  public deployLog$ = this.deployLogSubject.asObservable();
  public deployStatus$ = this.deployStatusSubject.asObservable();

  async connectDeployHub(operationId: string): Promise<void> {
    await this.disconnectHub();

    const token = localStorage.getItem('token') ?? '';
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('DeployLog', (data: any) => {
      this.deployLogSubject.next(data);
    });

    this.hubConnection.on('DeployStatus', (data: any) => {
      this.deployStatusSubject.next(data);
    });

    // Also keep backup LogLine listener
    this.hubConnection.on('LogLine', (data: { operationId: string } & BackupLogLine) => {
      this.logLineSubject.next(data);
    });
    this.hubConnection.on('StatusChanged', (data: any) => {
      this.statusSubject.next(data);
    });

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinOperation', operationId);
  }

  // ─── SignalR ──────────────────────────────────────────

  async connectHub(operationId: string): Promise<void> {
    await this.disconnectHub();

    const token = localStorage.getItem('token') ?? '';
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on(
      'LogLine',
      (data: { operationId: string; timestamp: string; level: string; message: string }) => {
        this.logLineSubject.next({
          operationId: data.operationId,
          timestamp: data.timestamp,
          level: data.level as BackupLogLine['level'],
          message: data.message,
        });
      },
    );

    this.hubConnection.on('StatusChanged', (data: any) => {
      this.statusSubject.next(data);
    });

    this.hubConnection.on('OperationUpdated', (data: any) => {
      this.statusSubject.next(data);
    });

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinOperation', operationId);
  }

  async disconnectHub(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = undefined;
    }
  }
}
