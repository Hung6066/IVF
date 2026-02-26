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
  CloudBackupObject,
  CloudConfig,
  CloudStatusResult,
  CloudUploadResult,
  ComplianceReport,
  CreateDataBackupStrategyRequest,
  DataBackupFile,
  DataBackupStatus,
  DataBackupStrategy,
  ReplicationActivationResult,
  ReplicationSetupGuide,
  ReplicationStatus,
  StartDataBackupRequest,
  StartDataRestoreRequest,
  TestCloudConfigRequest,
  TestCloudResult,
  UpdateCloudConfigRequest,
  UpdateDataBackupStrategyRequest,
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
