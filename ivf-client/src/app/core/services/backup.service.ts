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
  CloudBackupObject,
  CloudConfig,
  CloudStatusResult,
  CloudUploadResult,
  TestCloudConfigRequest,
  TestCloudResult,
  UpdateCloudConfigRequest,
  UpdateScheduleRequest,
} from '../models/backup.models';

@Injectable({ providedIn: 'root' })
export class BackupService {
  private http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/admin/backup`;
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
